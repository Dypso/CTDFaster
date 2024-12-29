public readonly struct ChunkRequest
{
    public required string FileId { get; init; }
    public required long Offset { get; init; }
    public required int Size { get; init; }

    public bool IsValid => Size <= 4096; // Max 4KB chunks
}