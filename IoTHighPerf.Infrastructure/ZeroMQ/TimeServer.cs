using System.Text.Json;
using IoTHighPerf.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;
using Microsoft.Extensions.Hosting;

namespace IoTHighPerf.Infrastructure.ZeroMQ;

public sealed class TimeServer : IHostedService, IDisposable
{
    private readonly string _endpoint;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<TimeServer> _logger;
    private readonly ITimeService _timeService;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _workerTasks = new();
    private readonly List<ResponseSocket> _sockets = new();
    private static readonly JsonSerializerOptions _jsonOptions = new() 
    { 
        WriteIndented = false,
        PropertyNamingPolicy = null
    };
    private static readonly byte[] _jsonPrefix = "{\"Value\":"u8.ToArray();
    private static readonly byte[] _jsonSuffix = "}"u8.ToArray();

    public TimeServer(
        IOptions<ZeroMQConfig> config,
        IAuditLogger auditLogger,
        ILogger<TimeServer> logger,
        ITimeService timeService)
    {
        _endpoint = config.Value.TimeEndpoint;
        _auditLogger = auditLogger;
        _logger = logger;
        _timeService = timeService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try 
        {
            _logger.LogInformation("Starting ZMQ server on {Endpoint}", _endpoint);

            // 2x le nombre de cœurs pour optimiser l'utilisation CPU
            int workerCount = Environment.ProcessorCount * 2;
            
            for (int i = 0; i < workerCount; i++)
            {
                var socket = new ResponseSocket();
                socket.Options.SendHighWatermark = 1_000_000;
                socket.Options.ReceiveHighWatermark = 1_000_000;
                socket.Options.Linger = TimeSpan.Zero;

                string endpoint = $"tcp://*:{5555 + i}";
                socket.Bind(endpoint);
                _sockets.Add(socket);
                
                var workerTask = Task.Run(() => ProcessRequests(socket), cancellationToken);
                _workerTasks.Add(workerTask);
            }

            _logger.LogInformation("ZMQ server started with {Count} workers", workerCount);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ZMQ server");
            throw;
        }
    }

    private async Task ProcessRequests(ResponseSocket socket)
    {
        // Préallocation du buffer de réponse
        byte[] responseBuffer = new byte[32];
        
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                string deviceId = socket.ReceiveFrameString();
                
                // Fire-and-forget audit logging
                _ = _auditLogger.LogTimeSyncAsync(deviceId);

                // Construction directe de la réponse JSON
                var time = _timeService.GetCurrentTicks();
                var response = BuildJsonResponse(time, _jsonPrefix, _jsonSuffix);
                
                socket.SendFrame(response);
            }
            catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error processing request");
                await Task.Delay(100, _cts.Token);
            }
        }
    }

    private static byte[] BuildJsonResponse(long value, byte[] prefix, byte[] suffix)
    {
        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value.ToString());
        var response = new byte[prefix.Length + valueBytes.Length + suffix.Length];
        
        Buffer.BlockCopy(prefix, 0, response, 0, prefix.Length);
        Buffer.BlockCopy(valueBytes, 0, response, prefix.Length, valueBytes.Length);
        Buffer.BlockCopy(suffix, 0, response, prefix.Length + valueBytes.Length, suffix.Length);
        
        return response;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping ZMQ server");
        _cts.Cancel();
        
        if (_workerTasks.Count > 0)
        {
            await Task.WhenAll(_workerTasks);
        }
    }

    public void Dispose()
    {
        foreach (var socket in _sockets)
        {
            socket.Dispose();
        }
        _cts.Dispose();
    }
}