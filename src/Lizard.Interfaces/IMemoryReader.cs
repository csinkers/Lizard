namespace Lizard.Interfaces;

public interface IMemoryReader : IDisposable
{
    void Read(uint offset, byte[] buffer);
}