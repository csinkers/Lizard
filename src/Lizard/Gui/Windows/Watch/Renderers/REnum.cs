using GhidraProgramData.Types;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class REnum : IGhidraRenderer
{
    readonly GEnum _type;

    public REnum(GEnum type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => _type.Size;
    public History HistoryConstructor(string path, IHistoryCreationContext context) => History.DefaultConstructor(path, _type);
    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        history.LastAddress = address;
        if (buffer.Length < _type.Size)
        {
            ImGui.TextUnformatted("--");
            return false;
        }

        if (!previousBuffer.IsEmpty && !buffer.SequenceEqual(previousBuffer))
            history.LastModifiedTicks = context.Now;

        uint value = _type.Size switch
        {
            1 => buffer[0],
            2 => BitConverter.ToUInt16(buffer),
            4 => BitConverter.ToUInt32(buffer),
            _ => throw new InvalidOperationException($"Unsupported enum size {_type.Size}")
        };

        var color = Util.ColorForAge(context.Now - history.LastModifiedTicks);
        ImGui.TextColored(color, _type.Elements.TryGetValue(value, out var name)
            ? $"{name} ({value})"
            : value.ToString());

        return history.LastModifiedTicks == context.Now;
    }
}