using IoTHighPerf.Core.Interfaces;
using IoTHighPerf.ActivityGenerator.Models;
using Microsoft.Extensions.Logging;

namespace IoTHighPerf.ActivityGenerator.Services;

public class CounterManager : ICounterManager
{
    private readonly string _counterFilePath;
    private readonly ILogger<CounterManager> _logger;
    private readonly SemaphoreSlim _lock = new(1);

    public CounterManager(string counterFilePath, ILogger<CounterManager> logger)
    {
        _counterFilePath = counterFilePath;
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        var directory = Path.GetDirectoryName(_counterFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return Task.CompletedTask;
    }

    public async Task<CounterState> GetCurrentCounterAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (File.Exists(_counterFilePath))
            {
                var lines = await File.ReadAllLinesAsync(_counterFilePath);
                if (lines.Length >= 2 &&
                    DateOnly.TryParse(lines[0], out var date) &&
                    int.TryParse(lines[1], out var counter))
                {
                    var lastFile = lines.Length > 2 ? lines[2] : string.Empty;
                    return new CounterState(date, counter, lastFile);
                }
            }

            return new CounterState(DateOnly.FromDateTime(DateTime.UtcNow), 0, string.Empty);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveCounterAsync(CounterState state)
    {
        await _lock.WaitAsync();
        try
        {
            var tempFile = Path.GetTempFileName();
            await File.WriteAllLinesAsync(tempFile, new[]
            {
                state.Date.ToString("yyyy-MM-dd"),
                state.Counter.ToString(),
                state.LastGeneratedFile
            });
            File.Move(tempFile, _counterFilePath, true);
        }
        finally
        {
            _lock.Release();
        }
    }
}