using System;

namespace IoTHighPerf.Core.Models;

public readonly struct ManifestResource
{
    public string Id { get; init; }
    public long Size { get; init; }
    public string Version { get; init; }
    public string Hash { get; init; }
}