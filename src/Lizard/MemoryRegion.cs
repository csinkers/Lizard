namespace Lizard;

public record MemoryRegion(uint MemoryStart, uint FileStart, uint Length, MemoryType Type)
{
    public long Offset { get; } = (long)MemoryStart - FileStart; // Mem->File: x-Offset, File->Mem: x+Offset
    public ulong MemoryEnd { get; } = MemoryStart + Length;
    public ulong FileEnd { get; } = FileStart + Length;
}