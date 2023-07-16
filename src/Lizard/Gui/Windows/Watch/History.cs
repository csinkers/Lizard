using GhidraProgramData;

namespace Lizard.Gui.Windows.Watch;

public class History
{
    public History(string path, IGhidraType type)
    {
        Path = path;
        Type = type ?? throw new ArgumentNullException(nameof(type));
        LastModifiedTicks = DateTime.UtcNow.Ticks;
    }

    public string Path { get; }
    public IGhidraType Type { get; }
    public long LastModifiedTicks { get; set; }
    public uint LastAddress { get; set; }
    public List<IDirective>? Directives { get; set; } // null = no directives or not yet initialised.
    public override string ToString() => $"H:{Path}:{Util.Timestamp(LastModifiedTicks):g3}";
    public static History DefaultConstructor(string path, IGhidraType type) => new(path, type) { LastModifiedTicks = DateTime.UtcNow.Ticks };
}