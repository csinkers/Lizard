using GhidraProgramData;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class RPointer : IGhidraRenderer
{
    readonly GPointer _type;

    class PointerHistory : History
    {
        public PointerHistory(string path, IGhidraType type) : base(path, type) { }
        public string? ReferentPath { get; set; }
        public override string ToString() => $"PtrH:{Path}:{Util.Timestamp(LastModifiedTicks):g3}";
    }

    public RPointer(GPointer type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => Constants.PointerSize;
    public History HistoryConstructor(string path, IHistoryCreationContext context) => new PointerHistory(path, _type);
    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
        => Draw((PointerHistory)history, address, buffer, previousBuffer, context);

    bool Draw(PointerHistory history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        history.LastAddress = address;
        if (buffer.Length < Constants.PointerSize)
        {
            ImGui.TextUnformatted("--");
            return false;
        }

        history.ReferentPath ??= history.Path + "*";

        if (!previousBuffer.IsEmpty && !buffer.SequenceEqual(previousBuffer))
            history.LastModifiedTicks = context.Now;

        var color = Util.ColorForAge(context.Now - history.LastModifiedTicks);
        var targetAddress = BitConverter.ToUInt32(buffer);
        ImGui.TextColored(color, context.DescribeAddress(targetAddress)); // TODO: Ensure unformatted
        ImGui.SameLine();

        if (ImGui.TreeNode(_type.Key.Name))
        {
            var referentRenderer = context.Renderers.Get(_type.Type);
            var referentHistory = context.History.GetOrCreateHistory(history.ReferentPath, referentRenderer);
            if (history.Directives != null)
            {
                referentHistory.Directives = history.Directives;
                history.Directives = null;
            }

            var size = referentRenderer.GetSize(referentHistory);
            var slice = context.Memory.Read(targetAddress, size);
            var oldSlice = context.Memory.ReadPrevious(targetAddress, size);

            ImGui.SetNextItemOpen(true);
            if (referentRenderer.Draw(referentHistory, targetAddress, slice, oldSlice, context))
                history.LastModifiedTicks = context.Now;

            ImGui.TreePop();
        }

        return history.LastModifiedTicks == context.Now;
    }
}
