using System.Buffers;
using GhidraProgramData.Types;
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

        var memAddress = context.ToMemoryAddress(_type.Address);
        history.LastAddress = memAddress;
        return history;
    }

    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        return _type.Size > 512
            ? DrawLarge(history, context)
            : DrawSmall(history, context);
    }

    bool DrawSmall(History history, DrawContext context)
    {
        Span<byte> curBuffer = stackalloc byte[(int)_type.Size];
        Span<byte> prevBuffer = stackalloc byte[(int)_type.Size];

        var cur = context.Memory.Read(history.LastAddress, _type.Size, curBuffer);
        var prev = context.Memory.TryReadPrevious(history.LastAddress, _type.Size, prevBuffer);

        return DrawInner(history, context, history.LastAddress, cur, prev);
    }

    bool DrawLarge(History history, DrawContext context)
    {
        var curArray = ArrayPool<byte>.Shared.Rent((int)_type.Size);
        var prevArray = ArrayPool<byte>.Shared.Rent((int)_type.Size);
        try
        {
            var cur = context.Memory.Read(history.LastAddress, _type.Size, curArray);
            var prev = context.Memory.TryReadPrevious(history.LastAddress, _type.Size, prevArray);
            return DrawInner(history, context, history.LastAddress, cur, prev);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(curArray);
            ArrayPool<byte>.Shared.Return(prevArray);
        }
    }

    bool DrawInner(History history, DrawContext context, uint address, ReadOnlySpan<byte> cur, ReadOnlySpan<byte> prev)
    {
        ImGui.PushID(_type.Key.Name);
        var renderer = context.Renderers.Get(_type.Type);
        bool result = renderer.Draw(history, address, cur, prev, context);
        ImGui.PopID();
        return result;
    }
}