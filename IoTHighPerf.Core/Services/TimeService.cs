using Microsoft.Extensions.Hosting;
using IoTHighPerf.Core.Interfaces;

namespace IoTHighPerf.Core.Services;

public class TimeService : ITimeService, IHostedService
{
    private long _currentTicks;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromMilliseconds(3));
    private Task? _updateTask;
    private readonly CancellationTokenSource _cts = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Starting TimeService...");
        _updateTask =  UpdateTimeAsync(_cts.Token);
    }

    private async Task UpdateTimeAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && await _timer.WaitForNextTickAsync(token))
        {
            Interlocked.Exchange(ref _currentTicks, DateTime.UtcNow.Ticks);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        if (_updateTask != null)
            await _updateTask;
        _cts.Dispose();
    }

    public long GetCurrentTicks() => _currentTicks;
}