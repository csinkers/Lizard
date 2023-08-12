namespace Lizard.Gui.Windows.Watch;

public class MemoryCache : IMemoryCache
{
    const int PageSizeBits = 12;
    const int PageSize = 1 << PageSizeBits;

    readonly BufferPool _pool = new();
    Dictionary<uint, byte[]> _current = new();
    Dictionary<uint, byte[]> _previous = new();
    IMemoryReader? _reader;
    bool _dirty;

    static uint PageNum(uint offset) => offset >> PageSizeBits;
    static uint PageNumRoundUp(uint offset) => offset + 4095 >> PageSizeBits;
    static uint PageAddr(uint pageNum) => pageNum << PageSizeBits;

    public IMemoryReader? Reader
    {
        get => _reader;
        set
        {
            Clear();
            _reader = value;
        }
    }

    public void Dirty() => _dirty = true;

    public void Clear()
    {
        foreach (var kvp in _current) _pool.Return(kvp.Value);
        foreach (var kvp in _previous) _pool.Return(kvp.Value);
        _current.Clear();
        _previous.Clear();
        _dirty = false;
    }

    public ReadOnlySpan<byte> Read(uint offset, uint size, Span<byte> backingArray)
    {
        if (_dirty)
        {
            (_previous, _current) = (_current, _previous);
            foreach (var kvp in _current) _pool.Return(kvp.Value);
            _current.Clear();
            _dirty = false;
            _pool.Flush();
        }

        return ReadInner(offset, size, backingArray, GetPage);
    }

    byte[] GetPage(uint pageNum)
    {
        if (_current.TryGetValue(pageNum, out var buffer))
            return buffer;

        var pageOffset = PageAddr(pageNum);
        buffer = _pool.Borrow(PageSize);
        _reader?.Read(pageOffset, buffer);
        _current[pageNum] = buffer;
        return buffer;
    }

    public ReadOnlySpan<byte> TryReadPrevious(uint offset, uint size, Span<byte> backingArray)
        => ReadInner(offset, size, backingArray, TryGetPreviousPage);

    byte[]? TryGetPreviousPage(uint pageNum)
    {
        _previous.TryGetValue(pageNum, out var buffer);
        return buffer;
    }

    static ReadOnlySpan<byte> ReadInner(uint offset, uint size, Span<byte> backingArray, Func<uint, byte[]?> tryGetPage)
    {
        if (size == 0)
            return ReadOnlySpan<byte>.Empty;

        uint firstPage = PageNum(offset);
        uint lastPage = PageNumRoundUp(offset + size) - 1;
        uint endOffset = offset + size;

        if (firstPage == lastPage) // Entire range is inside one page so can return a span of the cached page without any copying
        {
            var buffer = tryGetPage(firstPage);
            if (buffer == null)
                return ReadOnlySpan<byte>.Empty;

            var pageOffset = PageAddr(firstPage);
            return buffer.AsSpan((int)(offset - pageOffset), (int)size);
        }

        bool copying = true;
        int bytesCopied = 0;
        for (uint i = firstPage; i <= lastPage; i++)
        {
            var buffer = tryGetPage(i);
            if (buffer == null)
            {
                copying = false;
                continue;
            }

            if (!copying) // Still want to loop through the rest so any missing pages get requested
                continue;

            var pageOffset = PageAddr(i);
            var nextPageOffset = pageOffset + PageSize;

            // If it ends after this page then copy to the end of the page
            int startOffset = offset >= pageOffset && offset < nextPageOffset 
                ? (int)(offset - pageOffset) 
                : 0;

            int len = endOffset >= nextPageOffset
                ? PageSize - startOffset
                : (int)(endOffset - pageOffset);

            var fromSpan = buffer.AsSpan(startOffset, len);
            fromSpan.CopyTo(backingArray[bytesCopied..]);
            bytesCopied += fromSpan.Length;
        }

        return backingArray[..bytesCopied];
    }
}