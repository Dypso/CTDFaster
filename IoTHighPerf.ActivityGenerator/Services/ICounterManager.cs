using IoTHighPerf.ActivityGenerator.Models;

namespace IoTHighPerf.ActivityGenerator.Services;

public interface ICounterManager
{
    Task<CounterState> GetCurrentCounterAsync();
    Task SaveCounterAsync(CounterState state);
    Task InitializeAsync();
}

