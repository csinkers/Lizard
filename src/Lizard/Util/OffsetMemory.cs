using System.Runtime.InteropServices;

namespace Lizard.Util;

public class OffsetMemory
{
    readonly Memory<byte> _memory;
    readonly uint _offset;

    public OffsetMemory(Memory<byte> memory, uint offset)
    {
        _memory = memory;
        _offset = offset;
    }

    public uint Min => _offset;
    public uint Max => _offset + Size;
    public uint Size => (uint)_memory.Length;

    public uint GetDword(uint addr)
    {
        if (addr < _offset)
            return 0;

        uint index = (addr - _offset) / 4;
        if (index >= _memory.Length)
            return 0;

        var asDwords = MemoryMarshal.Cast<byte, uint>(_memory.Span);
        return asDwords[(int)index];
    }
}
