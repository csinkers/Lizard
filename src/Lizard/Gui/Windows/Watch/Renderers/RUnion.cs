using GhidraProgramData;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class RUnion : IGhidraRenderer
{
    readonly GUnion _type;
    public RUnion(GUnion type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => _type.FixedSize ?? 0;
    public History HistoryConstructor(string path, IHistoryCreationContext context) => History.DefaultConstructor(path, _type);
    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        history.LastAddress = address;
        ImGui.TextUnformatted("<UNION TODO>");
        return false;
    }
}