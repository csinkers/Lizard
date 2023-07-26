namespace Lizard;

public interface IMemoryCache
{
    IMemoryReader? Reader { get; set; }
    ReadOnlySpan<byte> Read(uint offset, uint size, Span<byte> backingArray); // Returns cached memory if it exists, otherwise immediately fetches entire pages that cover the range
    ReadOnlySpan<byte> TryReadPrevious(uint offset, uint size, Span<byte> backingArray); // Returns an empty span if there is no previous version of the range
    void Clear();
    void Dirty();
}