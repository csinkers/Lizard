﻿namespace Lizard.Memory;

public class PassthroughMemoryCache : IMemoryCache
{
    readonly IMemoryReader _reader;

    public PassthroughMemoryCache(IMemoryReader reader) =>
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    public ReadOnlySpan<byte> Read(uint offset, uint size, Span<byte> backingArray)
    {
        _reader.Read(offset, size, backingArray);
        return backingArray;
    }

    public ReadOnlySpan<byte> TryReadPrevious(uint offset, uint size, Span<byte> backingArray)
    {
        _reader.Read(offset, size, backingArray);
        return backingArray;
    }

    public void ReadIntoSpan(uint offset, uint size, Span<byte> span) => _reader.Read(offset, size, span);

    public void Dirty() { }
}
