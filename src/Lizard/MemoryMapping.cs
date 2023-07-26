namespace Lizard;

public class MemoryMapping
{
    readonly List<MemoryRegion> _memoryOrder = new();
    readonly List<MemoryRegion> _fileOrder = new();

    public void Add(uint memoryStart, uint fileStart, uint length, MemoryType type)
    {
        var region1 = new MemoryRegion(memoryStart, fileStart, length, type);
        ThrowIfOverlapping(region1, _memoryOrder, x => x.MemoryStart);
        ThrowIfOverlapping(region1, _fileOrder, x => x.FileStart);

        _memoryOrder.Add(region1);
        _fileOrder.Add(region1);
        _memoryOrder.Sort((x, y) => Comparer<uint>.Default.Compare(x.MemoryStart, y.MemoryStart));
        _fileOrder.Sort((x, y) => Comparer<uint>.Default.Compare(x.FileStart, y.FileStart));
    }

    public IEnumerable<MemoryRegion> Regions => _fileOrder;

    static void ThrowIfOverlapping(MemoryRegion region1, List<MemoryRegion> regions, Func<MemoryRegion, uint> accessor)
    {
        foreach (var region2 in regions)
        {
            if (region2 == region1)
                continue;

            var start1 = accessor(region1);
            var start2 = accessor(region2);
            var end1 = start1 + region1.Length;
            var end2 = start2 + region2.Length;

            if (start1 == start2) throw new InvalidOperationException($"Overlapping memory range detected - {start2:x}-{end2:x} overlaps with new region {start1:x}-{end1:x}");
            if (start1 < start2 && end1 > start2) throw new InvalidOperationException($"Overlapping memory range detected - {start2:x}-{end2:x} overlaps with new region {start1:x}-{end1:x}");
            if (start1 > start2 && end2 > start1) throw new InvalidOperationException($"Overlapping memory range detected - {start2:x}-{end2:x} overlaps with new region {start1:x}-{end1:x}");
        }
    }

    public (uint MemoryOffset, MemoryRegion Region)? ToMemory(uint fileOffset)
    {
        foreach (var region in _fileOrder)
        {
            if (fileOffset < region.FileStart || fileOffset >= region.FileEnd)
                continue;

            var result = fileOffset + region.Offset;
            if (result > uint.MaxValue)
                return null;
            return ((uint)result, region);
        }

        return null;
    }

    public (uint FileOffset, MemoryRegion Region)? ToFile(uint memoryOffset)
    {
        foreach (var region in _fileOrder)
        {
            if (memoryOffset < region.MemoryStart || memoryOffset >= region.MemoryEnd)
                continue;

            var result = memoryOffset - region.Offset;
            if (result < 0)
                return null;

            return ((uint)result, region);
        }

        return null;
    }
}