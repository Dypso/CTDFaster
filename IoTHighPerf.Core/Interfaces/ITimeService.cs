namespace IoTHighPerf.Core.Interfaces;

public interface ITimeService
{
    long GetCurrentTicks();
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}