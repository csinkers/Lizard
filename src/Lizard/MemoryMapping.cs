using System.Globalization;
using Lizard.Config;
using Lizard.Config.Properties;

namespace Lizard;

public class MemoryMapping
{
    static readonly StringListProperty MappingProperty = new(nameof(MemoryMapping), "Regions");
    readonly List<MemoryRegion> _memoryOrder = new();
    readonly List<MemoryRegion> _fileOrder = new();

    public event Action? MappingChanged;

    public void Update(IEnumerable<MemoryRegion> regions)
    {
        _memoryOrder.Clear();
        _fileOrder.Clear();

        foreach (var region in regions)
            Add(region);

        MappingChanged?.Invoke();
    }

    void Add(MemoryRegion region)
    {
        ThrowIfOverlapping(region, _memoryOrder, x => x.MemoryStart);
        ThrowIfOverlapping(region, _fileOrder, x => x.FileStart);

        _memoryOrder.Add(region);
        _fileOrder.Add(region);
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

            if (start1 == start2)
                throw new InvalidOperationException(
                    $"Overlapping memory range detected - {start2:x}-{end2:x} overlaps with new region {start1:x}-{end1:x}"
                );
            if (start1 < start2 && end1 > start2)
                throw new InvalidOperationException(
                    $"Overlapping memory range detected - {start2:x}-{end2:x} overlaps with new region {start1:x}-{end1:x}"
                );
            if (start1 > start2 && end2 > start1)
                throw new InvalidOperationException(
                    $"Overlapping memory range detected - {start2:x}-{end2:x} overlaps with new region {start1:x}-{end1:x}"
                );
        }
    }

    public bool ToMemory(uint fileOffset, out uint memoryOffset, out MemoryRegion region)
    {
        memoryOffset = 0;
        region = null!;

        foreach (var r in _fileOrder)
        {
            if (fileOffset < r.FileStart || fileOffset >= r.FileEnd)
                continue;

            var result = fileOffset + r.Offset;
            if (result > uint.MaxValue)
                return false;

            memoryOffset = (uint)result;
            region = r;
            return true;
        }

        return false;
    }

    public bool ToFile(uint memoryOffset, out uint fileOffset, out MemoryRegion region)
    {
        fileOffset = 0;
        region = null!;

        foreach (var r in _fileOrder)
        {
            if (memoryOffset < r.MemoryStart || memoryOffset >= r.MemoryEnd)
                continue;

            var result = memoryOffset - r.Offset;
            if (result < 0)
                return false;

            fileOffset = (uint)result;
            region = r;
            return true;
        }

        return false;
    }

    public void LoadProject(ProjectConfig project)
    {
        List<string> mappingList = project.GetProperty(MappingProperty);
        Deserialize(mappingList);
    }

    public void SaveProject(ProjectConfig project) => project.SetProperty(MappingProperty, Serialize());

    public List<string> Serialize() =>
        Regions
            .Select(region => $"{region.FileStart:x} {region.MemoryStart:x} {region.Length:x} {region.Type}")
            .ToList();

    void Deserialize(List<string> list)
    {
        static uint ParseHex(string s) => uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        _fileOrder.Clear();
        _memoryOrder.Clear();

        foreach (var s in list)
        {
            var parts = s.Split(' ');
            if (parts.Length != 4)
                continue;

            var fileStart = ParseHex(parts[0]);
            var memoryStart = ParseHex(parts[1]);
            var length = ParseHex(parts[2]);
            var type = Enum.Parse<MemoryType>(parts[3]);
            var region = new MemoryRegion(memoryStart, fileStart, length, type);
            Add(region);
        }

        MappingChanged?.Invoke();
    }
}
