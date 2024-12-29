namespace IoTHighPerf.ActivityGenerator.Models;

public readonly record struct AuditEntry(
    DateTime Timestamp, 
    string Type, 
    string DeviceId, 
    string Data);
