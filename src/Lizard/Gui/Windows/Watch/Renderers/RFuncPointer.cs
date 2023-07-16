using System.Runtime.InteropServices;
using GhidraProgramData;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class RFuncPointer : IGhidraRenderer
{
    readonly GFuncPointer _type;

    public RFuncPointer(GFuncPointer type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => Constants.PointerSize;
    public History HistoryConstructor(string path, IHistoryCreationContext context) => History.DefaultConstructor(path, _type);
    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        history.LastAddress = address;
        if (buffer.IsEmpty)
        {
            ImGui.TextUnformatted("--");
            return false;
        }

        if (!previousBuffer.IsEmpty && !buffer.SequenceEqual(previousBuffer))
            history.LastModifiedTicks = context.Now;

        var color = Util.ColorForAge(context.Now - history.LastModifiedTicks);
        var targetAddress = MemoryMarshal.Read<uint>(buffer);
        ImGui.TextColored(color, context.DescribeAddress(targetAddress)); // TODO: Ensure unformatted

        return history.LastModifiedTicks == context.Now;
    }
}