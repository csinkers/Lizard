using Lizard.Interfaces;

namespace Lizard.Watch;

public class MemoryCache : IMemoryCache
{
    readonly HashSet<uint> _requestedPages = new();
    readonly BufferPool _pool = new();
    Dictionary<uint, MemoryBuffer> _current = new();
    Dictionary<uint, MemoryBuffer> _previous = new();
    List<MemoryBuffer> _currentBuffers = new();
    List<MemoryBuffer> _previousBuffers = new();
    IMemoryReader? _reader;

    static uint PageNum(uint offset) => offset >> 12;
    static uint PageNumRoundUp(uint offset) => (offset + 4095) >> 12;
    static uint PageAddr(uint pageNum) => pageNum << 12;

    public IMemoryReader? Reader
    {
        get => _reader;
        set
        {
            Clear();
            _reader = value;
        }
    }

    public void Clear()
    {
        _requestedPages.Clear();
        ClearBuffers(_currentBuffers);
        ClearBuffers(_previousBuffers);
        _current.Clear();
        _previous.Clear();
    }

    void ClearBuffers(List<MemoryBuffer> buffers)
    {
        foreach (var curBuffer in buffers)
            if (curBuffer.Data != null)
                _pool.Return(curBuffer.Data);

        buffers.Clear();
    }

    public ReadOnlySpan<byte> Read(uint offset, uint size)
    {
        uint fromPage = PageNum(offset);
        uint toPage = PageNumRoundUp(offset + size);

        for (uint i = fromPage; i < toPage; i++)
            _requestedPages.Add(i);

        if (!_current.TryGetValue(fromPage, out var buffer))
            return ReadOnlySpan<byte>.Empty;

        return buffer.Read(offset, size);
    }

    public ReadOnlySpan<byte> ReadPrevious(uint offset, uint size)
        => _previous.TryGetValue(PageNum(offset), out var buffer)
            ? buffer.Read(offset, size)
            : ReadOnlySpan<byte>.Empty;

    public void Refresh()
    {
        var pages = _requestedPages.ToArray();
        (_previous, _current) = (_current, _previous);
        (_previousBuffers, _currentBuffers) = (_currentBuffers, _previousBuffers);
        ClearBuffers(_currentBuffers);

        _current.Clear();
        _requestedPages.Clear();
        Array.Sort(pages);

        uint lastPage = 0;
        MemoryBuffer? buffer = null;

        for (int i = 0; i < pages.Length; i++)
        {
            uint page = pages[i];

            if (buffer == null) // First iteration
            {
                buffer = new MemoryBuffer { Offset = PageAddr(page) };
                _currentBuffers.Add(buffer);
            }
            else if (lastPage + 1 != page) // Not contiguous, need to read in the last one and create a new one going forward
            {
                PopulateBuffer(buffer, lastPage);
                buffer = new MemoryBuffer { Offset = PageAddr(page) };
                _currentBuffers.Add(buffer);
            }

            _current[page] = buffer;
            lastPage = page;
        }

        if (buffer != null) // Read in the final buffer
            PopulateBuffer(buffer, lastPage);

        _pool.Flush(); // Throw away any buffers that couldn't be reused
    }

    void PopulateBuffer(MemoryBuffer buffer, uint lastPage)
    {
        var bufferEnd = PageAddr(lastPage + 1);
        var bufferSize = bufferEnd - buffer.Offset;
        buffer.Data = _pool.Borrow((int)bufferSize);
        Reader?.Read(buffer.Offset, buffer.Data);
    }
}