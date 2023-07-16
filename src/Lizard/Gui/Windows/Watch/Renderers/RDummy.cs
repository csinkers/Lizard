using GhidraProgramData;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class RDummy : IGhidraRenderer
{
    readonly GDummy _type;
    string? _label;

    public RDummy(GDummy type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";
    public History HistoryConstructor(string path, IHistoryCreationContext context) => History.DefaultConstructor(path, _type);
    public uint GetSize(History? history) => 0;
    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        _label ??= $"<DUMMY TYPE {_type.Key.Namespace}/{_type.Key.Name}>";
        ImGui.TextUnformatted(_label);
        return false;
    }
}