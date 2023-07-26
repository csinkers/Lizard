namespace Lizard;

public interface IMemoryReader : IDisposable
{
    int Version { get; }
    void Read(uint offset, byte[] buffer);
}