namespace Lizard.Memory;

public interface IMemoryReader : IDisposable
{
    int Version { get; }
    void Read(uint offset, uint size, Span<byte> buffer);
}
