namespace Lizard.Interfaces;

public interface IMemoryAccess
{
    ReadOnlySpan<byte> Read(uint offset, uint size);
}