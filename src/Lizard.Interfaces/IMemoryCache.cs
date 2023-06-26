namespace Lizard.Interfaces;

public interface IMemoryCache
{
    IMemoryReader? Reader { get; set; }
    ReadOnlySpan<byte> Read(uint offset, uint size);
    ReadOnlySpan<byte> ReadPrevious(uint offset, uint size);
    void Refresh();
    void Clear();
}