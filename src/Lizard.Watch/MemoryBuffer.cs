namespace Lizard.Watch;

public class MemoryBuffer
{
    public uint Offset { get; init; }
    public byte[]? Data { get; set; }
    public ReadOnlySpan<byte> Read(uint offset, uint size)
        => offset < Offset
            ? ReadOnlySpan<byte>.Empty
            : Util.SafeSlice<byte>(Data.AsSpan(), offset - Offset, size);
}