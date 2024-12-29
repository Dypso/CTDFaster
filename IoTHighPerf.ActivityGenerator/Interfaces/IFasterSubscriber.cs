using System.Text;

namespace IoTHighPerf.Core.Interfaces;

public interface IFasterSubscriber<T> where T : class
{
    Task SubscribeAsync(string topic, 
        Func<T, string, ValueTask> messageHandler, 
        CancellationToken cancellationToken,
        FasterSubscriberConfiguration? configuration = null);
}

public class FasterSubscriberConfiguration
{
    public required string Topic { get; init; }
    public required string SubscriberId { get; init; }
    public bool RetainMessages { get; init; }
    public bool StartFromBeginning { get; init; }
}