using GhidraProgramData;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class RArray : IGhidraRenderer
{
    class ArrayHistory : History
    {
        public ArrayHistory(string path, IGhidraType type, string[] elementPaths) : base(path, type)
            => ElementPaths = elementPaths ?? throw new ArgumentNullException(nameof(elementPaths));

        public string[] ElementPaths { get; }
        public override string ToString() => $"ArrayH:{Path}:{Util.Timestamp(LastModifiedTicks):g3}";
    }

    static readonly List<string> NumberLabels = new();
    readonly GArray _type;
    readonly IGhidraRenderer _elementRenderer;

    public RArray(GArray type, RendererCache renderers)
    {
        if (renderers == null) throw new ArgumentNullException(nameof(renderers));
        _type = type ?? throw new ArgumentNullException(nameof(type));
        _elementRenderer = renderers.Get(_type.Type);
        while (NumberLabels.Count < _type.Count)
            NumberLabels.Add($"[{NumberLabels.Count}] ");
    }

    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => _elementRenderer.GetSize(null) * _type.Count;
    public History HistoryConstructor(string path, IHistoryCreationContext context)
    {
        var elemPaths = Enumerable.Range(0, (int)_type.Count).Select(x => $"{path}/{x}").ToArray();
        return new ArrayHistory(path, _type, elemPaths);
    }

    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
        => Draw((ArrayHistory)history, address, buffer, previousBuffer, context);
    bool Draw(ArrayHistory history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        history.LastAddress = address;
        if (_type.Count == 0)
        {
            ImGui.TextUnformatted("<EMPTY>");
            return false;
        }

        if (_type.Type == GPrimitive.Char)
        {
            if (!previousBuffer.IsEmpty && !buffer.SequenceEqual(previousBuffer))
                history.LastModifiedTicks = context.Now;

            var str = Constants.Encoding.GetString(buffer);
            var color = Util.ColorForAge(context.Now - history.LastModifiedTicks);
            ImGui.TextColored(color, str.Replace("%", "%%"));
            return history.LastModifiedTicks == context.Now;
        }

        bool openAll = ImGui.Button("+"); ImGui.SameLine();
        bool closeAll = ImGui.Button("-"); ImGui.SameLine();

        if (openAll) ImGui.SetNextItemOpen(true);
        if (closeAll) ImGui.SetNextItemOpen(false);

        bool changed = false;

        if (!ImGui.TreeNode(_type.Key.Name))
        {
            changed = !previousBuffer.IsEmpty && !buffer.SequenceEqual(previousBuffer);
            if (changed)
                history.LastModifiedTicks = context.Now;

            if (closeAll)
                ImGui.TreePush(_type.Key.Name);
            else
                return changed;
        }

        var elementRenderer = context.Renderers.Get(_type.Type);
        var size = elementRenderer.GetSize(null);
        for (int i = 0; i < _type.Count; i++)
        {
            var elemHistory = context.History.GetOrCreateHistory(history.ElementPaths[i], elementRenderer);
            var color = Util.ColorForAge(context.Now - elemHistory.LastModifiedTicks);

            ImGui.TextColored(color, NumberLabels[i]);
            ImGui.SameLine();
            uint elemAddress = address + (uint)i * size;
            var slice = Util.SafeSlice(buffer, (uint)i * size, size);
            var oldSlice = Util.SafeSlice(previousBuffer, (uint)i * size, size);

            ImGui.PushID(i);
            if (openAll) ImGui.SetNextItemOpen(true);
            if (closeAll) ImGui.SetNextItemOpen(false);
            changed |= elementRenderer.Draw(elemHistory, elemAddress, slice, oldSlice, context);
            ImGui.PopID();
        }

        if (changed)
            history.LastModifiedTicks = context.Now;

        ImGui.TreePop();
        return changed;
    }
}
