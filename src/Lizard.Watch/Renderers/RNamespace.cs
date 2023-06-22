using GhidraProgramData;
using ImGuiNET;

namespace Lizard.Watch.Renderers;

public class RNamespace : IGhidraRenderer
{
    readonly GNamespace _type;

    class NamespaceHistory : History
    {
        public NamespaceHistory(string path, IGhidraType type, string[] memberPaths, string[] memberLabels) : base(path, type)
        {
            MemberPaths = memberPaths ?? throw new ArgumentNullException(nameof(memberPaths));
            MemberLabels = memberLabels ?? throw new ArgumentNullException(nameof(memberLabels));
        }

        public string[] MemberPaths { get; }
        public string[] MemberLabels { get; }
    }

    public RNamespace(GNamespace type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => 0;
    public History HistoryConstructor(string path, IHistoryCreationContext context)
    {
        var memberPaths = _type.Members.Select((_, i) => $"{path}/{i}").ToArray();
        var memberLabels = _type.Members.Select(x => x.Key.Name.Replace("%", "%%") + ": ").ToArray();
        return new NamespaceHistory(path, _type, memberPaths, memberLabels);
    }

    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        var h = (NamespaceHistory)history;
        for (var index = 0; index < _type.Members.Count; index++)
        {
            var member = _type.Members[index];
            var memberRenderer = context.Renderers.Get(member);
            if (member is GGlobal g && !IsShown(g, false, context.Filter))
                continue;

            var memberPath = h.MemberPaths[index];
            var memberLabel = h.MemberLabels[index];
            var memberHistory = context.History.GetOrCreateHistory(memberPath, memberRenderer);

            if (!ImGui.TreeNode(memberLabel))
                continue;

            ImGui.PushID(index);
            memberRenderer.Draw(memberHistory, address, buffer, previousBuffer, context);
            ImGui.PopID();
            ImGui.TreePop();
        }

        return false;
    }

    static bool IsShown(GGlobal g, bool onlyShowActive, string filter)
    {
        if (!onlyShowActive && string.IsNullOrEmpty(filter))
            return true;

        // if (onlyShowActive && watch.IsActive)
        //     return true;

        return !string.IsNullOrEmpty(filter) && g.Key.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}