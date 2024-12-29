using System.Text;
using FASTER.core;
using IoTHighPerf.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace IoTHighPerf.Infrastructure.PubSub;

public class FasterSubscriber<T> : IFasterSubscriber<T> where T : class
{
    private readonly FasterLog _log;
    private readonly ILogger<FasterSubscriber<T>> _logger;

    public FasterSubscriber(FasterLog log, ILogger<FasterSubscriber<T>> logger)
    {
        _log = log;
        _logger = logger;
    }

    public async Task SubscribeAsync(
        string topic,
        Func<T, string, ValueTask> messageHandler,
        CancellationToken cancellationToken,
        FasterSubscriberConfiguration? configuration = null)
    {
        try
        {
            long currentAddress = 0;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                using var iter = _log.Scan(currentAddress, long.MaxValue);
                while (iter.GetNext(out byte[] entry, out int address, out long entryLength))
                {
                    if (entry != null)
                    {
                        try
                        {
                            if (entry is T typedEntry)
                            {
                                await messageHandler(typedEntry, address.ToString());
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erreur lors du traitement du message à l'adresse {Address}", address);
                        }
                    }
                    currentAddress = address + entryLength;
                }

                await _log.WaitForCommitAsync(currentAddress);

                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken); // Pause courte avant de réessayer
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Arrêt normal
            _logger.LogInformation("Subscription arrêtée normalement");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur dans la boucle de subscription");
            throw;
        }
    }
}