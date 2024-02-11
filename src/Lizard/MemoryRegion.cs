namespace Lizard;

public record MemoryRegion(uint MemoryStart, uint FileStart, uint Length, MemoryType Type)
{
    public long Offset { get; } = (long)MemoryStart - FileStart; // Mem->File: x-Offset, File->Mem: x+Offset
    public uint MemoryEnd { get; } = MemoryStart + Length;
    public uint FileEnd { get; } = FileStart + Length;

    public bool Contains(uint memoryAddress) => memoryAddress >= MemoryStart && memoryAddress <= MemoryEnd;
}