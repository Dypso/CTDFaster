using IoTHighPerf.ActivityGenerator.Models;

namespace IoTHighPerf.ActivityGenerator.Services;

public interface IActivityFileWriter
{
    Task WriteActivityFileAsync(string filePath, IEnumerable<AuditEntry> entries);
    Task<string> GenerateActivityFileAtomicAsync(IEnumerable<AuditEntry> entries, CounterState counterState);
}

