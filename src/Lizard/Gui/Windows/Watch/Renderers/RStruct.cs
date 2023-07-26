using GhidraProgramData;
using GhidraProgramData.Directives;
using GhidraProgramData.Types;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class RStruct : IGhidraRenderer
{
    readonly GStruct _type;

    class StructHistory : History
    {
        public StructHistory(string path, IGhidraType type, string[] memberPaths, IGhidraRenderer[] renderers) : base(path, type)
        {
            MemberPaths = memberPaths ?? throw new ArgumentNullException(nameof(memberPaths));
            MemberRenderers = renderers ?? throw new ArgumentNullException(nameof(renderers));
        }

        public uint? Size { get; set; }
        public string[] MemberPaths { get; }
        public IGhidraRenderer[] MemberRenderers { get; }
        public override string ToString() => $"StructH:{Path}:{Util.Timestamp(LastModifiedTicks):g3}";
    }

    public RStruct(GStruct type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";

    public uint GetSize(History? history) => ((StructHistory?)history)?.Size ?? _type.Size;
    public History HistoryConstructor(string path, IHistoryCreationContext context)
    {
        var memberPaths = _type.Members.Select((_, i) => $"{path}/{i}").ToArray();
        var memberTypes = new IGhidraRenderer[_type.Members.Count]; // Starts off all null, initialised on first draw as we need access to context.Renderers

        List<IDirective>? directives = null;
        foreach (var member in _type.Members)
        {
            if (member.Directives == null) continue;
            directives ??= new List<IDirective>();
            directives.AddRange(member.Directives);
        }

        return new StructHistory(path, _type, memberPaths, memberTypes) { Directives = directives };
    }

    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
        => Draw((StructHistory)history, address, buffer, previousBuffer, context);
    bool Draw(StructHistory history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        bool changed = false;
        history.LastAddress = address;

        if (!ImGui.TreeNode(_type.Key.Name))
        {
            changed = !previousBuffer.IsEmpty && !buffer.SequenceEqual(previousBuffer);
            if (changed)
                history.LastModifiedTicks = context.Now;
            return changed;
        }

        uint size = 0;
        for (var i = 0; i < _type.Members.Count; i++)
        {
            GStructMember member = _type.Members[i];
            string memberPath = history.MemberPaths[i];
            var memberHistory = context.History.TryGetHistory(memberPath) ?? InitialiseMemberHistory(i, history, context);
            var memberRenderer = history.MemberRenderers[i];

            var color = Util.ColorForAge(context.Now - memberHistory.LastModifiedTicks);
            ImGui.TextColored(color, _type.MemberNames[i]);
            ImGui.SameLine();

            if (_type.FixedSize == null)
                size += memberRenderer.GetSize(memberHistory);

            uint memberAddress = address + member.Offset;
            var slice = Util.SafeSlice(buffer, member.Offset, member.Size);
            var oldSlice = Util.SafeSlice(previousBuffer, member.Offset, member.Size);

            ImGui.PushID(i);
            changed |= memberRenderer.Draw(memberHistory, memberAddress, slice, oldSlice, context);
            ImGui.PopID();
        }

        if (changed)
            history.LastModifiedTicks = context.Now;

        if (_type.FixedSize == null)
            history.Size = size;

        ImGui.TreePop();
        return changed;
    }

    History InitialiseMemberHistory(int index, StructHistory history, DrawContext context)
    {
        GStructMember member = _type.Members[index];
        string memberPath = history.MemberPaths[index];
        List<IDirective>? memberDirectives = null;

        history.MemberRenderers[index] = context.Renderers.Get(_type.Members[index].Type);

        if (history.Directives != null)
        {
            foreach (var directive in history.Directives)
            {
                if (directive is not DTargetChild(var path, var childDirective) || path != member.Name) continue;
                if (childDirective is DTypeCast cast)
                {
                    history.MemberRenderers[index] = context.Renderers.Get(cast.Type);
                    continue;
                }

                memberDirectives ??= new List<IDirective>();
                memberDirectives.Add(childDirective);
            }
        }

        var memberHistory = context.History.CreateHistory(memberPath, history.MemberRenderers[index]);
        if (memberDirectives != null)
            memberHistory.Directives = memberDirectives;

        return memberHistory;
    }
}