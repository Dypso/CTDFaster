using System.Globalization;
using System.Text;
using System.Threading.Tasks.Dataflow;
using IoTHighPerf.ActivityGenerator.Models;
using IoTHighPerf.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IoTHighPerf.ActivityGenerator.Services;

public sealed class ReactiveActivityGenerator : BackgroundService
{
    private readonly ILogger<ReactiveActivityGenerator> _logger;
    private readonly IFasterSubscriber<byte[]> _subscriber;
    private readonly IActivityFileWriter _fileWriter;
    private readonly ICounterManager _counterManager;
    private readonly ActivityFileOptions _options;
    private ITargetBlock<AuditEntry>? _pipeline;
    private readonly CancellationTokenSource _pipelineCts = new();

    public ReactiveActivityGenerator(
        ILogger<ReactiveActivityGenerator> logger,
        IFasterSubscriber<byte[]> subscriber,
        IActivityFileWriter fileWriter,
        ICounterManager counterManager,
        IOptions<ActivityFileOptions> options)
    {
        _logger = logger;
        _subscriber = subscriber;
        _fileWriter = fileWriter;
        _counterManager = counterManager;
        _options = options.Value;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _counterManager.InitializeAsync();
        SetupPipeline();
        await base.StartAsync(cancellationToken);
    }

    private void SetupPipeline()
    {
        var bufferBlock = new BufferBlock<AuditEntry>(new DataflowBlockOptions 
        { 
            CancellationToken = _pipelineCts.Token,
            BoundedCapacity = ActivityFileOptions.MaxBatchSize * 2
        });

        var batchBlock = new BatchBlock<AuditEntry>(
            ActivityFileOptions.MaxBatchSize, 
            new GroupingDataflowBlockOptions 
            { 
                CancellationToken = _pipelineCts.Token,
                BoundedCapacity = ActivityFileOptions.MaxBatchSize * 2
            });

        var generateFileBlock = new TransformBlock<AuditEntry[], string>(
            async entries =>
            {
                try
                {
                    var currentCounter = await _counterManager.GetCurrentCounterAsync();
                    return await _fileWriter.GenerateActivityFileAtomicAsync(entries.ToList(), currentCounter);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors de la génération du fichier d'activité");
                    throw;
                }
            },
            new ExecutionDataflowBlockOptions 
            { 
                MaxDegreeOfParallelism = 1,
                BoundedCapacity = 10,
                CancellationToken = _pipelineCts.Token
            });

        var logBlock = new ActionBlock<string>(
            fileName =>
            {
                if (!string.IsNullOrEmpty(fileName))
                {
                    _logger.LogInformation("Fichier d'activité généré: {FileName}", fileName);
                }
            },
            new ExecutionDataflowBlockOptions 
            { 
                CancellationToken = _pipelineCts.Token 
            });

        // Timer pour forcer le batch toutes les minutes
        _ = new Timer(
            _ => batchBlock.TriggerBatch(), 
            null, 
            TimeSpan.FromSeconds(ActivityFileOptions.BatchTimeoutSeconds),
            TimeSpan.FromSeconds(ActivityFileOptions.BatchTimeoutSeconds));

        // Liaison des blocks avec propagation d'erreur
        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
        bufferBlock.LinkTo(batchBlock, linkOptions);
        batchBlock.LinkTo(generateFileBlock, linkOptions);
        generateFileBlock.LinkTo(logBlock, linkOptions);

        _pipeline = bufferBlock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _pipelineCts.Token);

        var config = new FasterSubscriberConfiguration 
        { 
            SubscriberId = $"activity-generator-{Environment.MachineName}",
            Topic = ActivityFileOptions.TopicName,
            RetainMessages = true,
            StartFromBeginning = true
        };

        try
        {
            await _subscriber.SubscribeAsync(
                ActivityFileOptions.TopicName,
                async (data, _) =>
                {
                    var entry = ParseAuditEntry(data);
                    if (entry.HasValue && _pipeline != null)
                    {
                        await _pipeline.SendAsync(entry.Value, linkedCts.Token);
                    }
                },
                linkedCts.Token,
                config);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Arrêt normal
        }
    }

    private static AuditEntry? ParseAuditEntry(byte[] data)
    {
        try
        {
            var text = Encoding.UTF8.GetString(data);
            var parts = text.Split('|');
            if (parts.Length < 4) return null;

            return new AuditEntry(
                DateTime.ParseExact(parts[0], "yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
                parts[1],
                parts[2],
                parts[3]
            );
        }
        catch
        {
            return null;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _pipelineCts.Cancel();

            if (_pipeline != null)
            {
                _pipeline.Complete();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                await Task.WhenAny(DrainPipelineAsync(), timeoutTask);
            }
        }
        finally
        {
            _pipelineCts.Dispose();
            await base.StopAsync(cancellationToken);
        }
    }

    private async Task DrainPipelineAsync()
    {
        if (_pipeline is BatchBlock<AuditEntry> batchBlock)
        {
            try
            {
                batchBlock.TriggerBatch();
                await Task.Delay(1000); // Attendre que les derniers messages soient traités
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du drainage du pipeline");
            }
        }
    }
}