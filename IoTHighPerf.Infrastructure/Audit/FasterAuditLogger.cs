using System;
using System.Threading.Tasks;
using FASTER.core;
using IoTHighPerf.Core.Interfaces;
using System.Threading.Channels;

namespace IoTHighPerf.Infrastructure.Audit;

public sealed class FasterAuditLogger : IAuditLogger, IDisposable
{
    private readonly FasterLog _log;
    private readonly Channel<byte[]> _auditChannel;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _commitTask;
    private static readonly byte[] NewLine = "\n"u8.ToArray();
    private const int CommitIntervalMs = 1000; // 1 second commit interval

    public FasterAuditLogger(string logPath)
    {
        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var device = Devices.CreateLogDevice(logPath);
        var logSettings = new FasterLogSettings 
        { 
            LogDevice = device,
            PageSizeBits = 12, // 4KB pages
            MemorySizeBits = 20, // 1MB memory
            SegmentSizeBits = 22 // 4MB segments
        };
        
        _log = new FasterLog(logSettings);
        _auditChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions 
        { 
            SingleReader = true,
            SingleWriter = false 
        });

        _cancellationTokenSource = new CancellationTokenSource();
        
        StartAuditProcessor();
        _commitTask = StartPeriodicCommit(_cancellationTokenSource.Token);
    }

    private void StartAuditProcessor()
    {
        Task.Run(async () =>
        {
            try 
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await foreach (var entry in _auditChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
                    {
                        _ = await _log.EnqueueAsync(entry);
                        _ = await _log.EnqueueAsync(NewLine);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Arrêt normal
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erreur dans le processeur d'audit : {ex}");
                throw;
            }
        });
    }

    private Task StartPeriodicCommit(CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(CommitIntervalMs, cancellationToken);
                    await _log.CommitAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Arrêt normal
            }
        }, cancellationToken);
    }

    private static byte[] SerializeAuditEntry(string type, string data)
    {
        // Format optimisé du timestamp sans allocations
        Span<char> timestampBuffer = stackalloc char[27]; // "yyyy-MM-ddTHH:mm:ss.fffffff"
        DateTime.UtcNow.TryFormat(timestampBuffer, out _, "yyyy-MM-ddTHH:mm:ss.fff");
        
        // Pré-calcul de la taille du buffer
        var typeLength = type.Length;
        var dataLength = data.Length;
        var totalLength = timestampBuffer.Length + 2 + typeLength + dataLength; // 2 pour les séparateurs '|'
        
        var result = new byte[totalLength];
        var position = 0;

        // Copie du timestamp
        position += System.Text.Encoding.UTF8.GetBytes(timestampBuffer, result.AsSpan(position));
        
        // Ajout du séparateur
        result[position++] = (byte)'|';
        
        // Copie du type
        position += System.Text.Encoding.UTF8.GetBytes(type, result.AsSpan(position));
        
        // Ajout du séparateur
        result[position++] = (byte)'|';
        
        // Copie des données
        System.Text.Encoding.UTF8.GetBytes(data, result.AsSpan(position));
        
        return result;
    }

    public ValueTask LogManifestRequestAsync(string deviceId)
    {
        var entry = SerializeAuditEntry("MANIFEST", deviceId);
        return _auditChannel.Writer.WriteAsync(entry);
    }

    public ValueTask LogDownloadAsync(string deviceId, string fileId, long offset, int size)
    {
        var entry = SerializeAuditEntry("DOWNLOAD", $"{deviceId}:{fileId}:{offset}:{size}");
        return _auditChannel.Writer.WriteAsync(entry);
    }

    public ValueTask LogTimeSyncAsync(string deviceId)
    {
        var entry = SerializeAuditEntry("TIME", deviceId);
        return _auditChannel.Writer.WriteAsync(entry);
    }

    public ValueTask LogConfirmationAsync(string deviceId, string fileId)
    {
        var entry = SerializeAuditEntry("CONFIRM", $"{deviceId}:{fileId}");
        return _auditChannel.Writer.WriteAsync(entry);
    }

    public void Dispose()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _auditChannel.Writer.Complete();
            
            // Attente de la fin du task de commit
            _commitTask.Wait(TimeSpan.FromSeconds(5));
            
            // Commit final
            _log.CommitAsync().GetAwaiter().GetResult();
            
            _log.Dispose();
            _cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erreur lors de la fermeture du logger d'audit : {ex}");
            throw;
        }
    }
}