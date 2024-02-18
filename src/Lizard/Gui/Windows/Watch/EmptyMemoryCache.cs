using Lizard.Memory;

namespace Lizard.Gui.Windows.Watch;

public class EmptyMemoryCache : IMemoryCache
{
    public ReadOnlySpan<byte> Read(uint offset, uint size, Span<byte> backingArray) => ReadOnlySpan<byte>.Empty;

    public ReadOnlySpan<byte> TryReadPrevious(uint offset, uint size, Span<byte> backingArray) =>
        ReadOnlySpan<byte>.Empty;

    public void ReadIntoSpan(uint offset, uint size, Span<byte> span) => span.Clear();

    public void Dirty() { }
}
