using GhidraProgramData;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class RGlobal : IGhidraRenderer
{
    readonly GGlobal _type;

    public RGlobal(GGlobal type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => _type.Size;
    public History HistoryConstructor(string path, IHistoryCreationContext context)
    {
        var renderer = context.Renderers.Get(_type.Type);
        var history = renderer.HistoryConstructor(path, context);
        history.LastAddress = _type.Address;
        return history;
    }

    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        // var history = context.History.GetOrCreateHistory(Name, Data.Type);
        // var active = IsActive;
        // ImGui.Checkbox(CheckboxId, ref active);
        // IsActive = active;
        // ImGui.SameLine();

        // var color = Util.ColorForAge(context.Now - history.LastModifiedTicks);
        // ImGui.TextColored(color, _label);
        // ImGui.SameLine();

        ImGui.PushID(_type.Key.Name);
        var cur = context.Memory.Read(_type.Address, _type.Size);
        var prev = context.Refreshed ? context.Memory.ReadPrevious(_type.Address, _type.Size) : ReadOnlySpan<byte>.Empty;

        var renderer = context.Renderers.Get(_type.Type);
        bool result = renderer.Draw(history, _type.Address, cur, prev, context);
        ImGui.PopID();
        return result;
    }
}