using System.Globalization;
using ImGuiNET;
using Lizard.Comms;

namespace Lizard.Gui.Windows;

public class BreakpointsWindow : SingletonWindow
{
    static readonly string[] PossibleTypes = Enum.GetNames(typeof(LBreakpointType1));

    readonly CommandContext _context;
    readonly List<LBreakpoint1> _breakpoints = [];
    string[] _idStrings = [];
    string[] _checkboxIds = [];
    string[] _addressStrings = [];
    string[] _nameStrings = [];
    string[] _typeStrings = [];
    string _pendingAddress = "";
    int _pendingType = (int)LBreakpointType1.Normal;
    int _version = -1;

    public BreakpointsWindow(CommandContext context)
        : base("Breakpoints") => _context = context ?? throw new ArgumentNullException(nameof(context));

    protected override void DrawContents()
    {
        var session = _context.Session;
        if (_version != session.Version)
        {
            _breakpoints.Clear();
            _breakpoints.AddRange(session.ListBreakpoints());
            // csharpier-ignore
            _addressStrings = _breakpoints
                .Select(x =>
                {
                    if (x.Address == null)
                        return "Null";

                    return x.Type switch
                    {
                        LBreakpointType1.Normal          => $"{x.Address.Segment}:{x.Address.Offset}",
                        LBreakpointType1.Ephemeral       => $"{x.Address.Segment}:{x.Address.Offset}",
                        LBreakpointType1.Read            => $"{x.Address.Segment}:{x.Address.Offset}",
                        LBreakpointType1.Write           => $"{x.Address.Segment}:{x.Address.Offset}",
                        LBreakpointType1.Interrupt       => $"INT {x.Address.Offset:X2}",
                        LBreakpointType1.InterruptWithAH => $"INT {x.Address.Offset:X2}, AH={x.Ah:X2}",
                        LBreakpointType1.InterruptWithAX => $"INT {x.Address.Offset:X2}, AH={x.Ah:X2}, AL={x.Al:X2}",
                        LBreakpointType1.Unknown         => "Unk",
                        _ => throw new ArgumentOutOfRangeException()
                    };
                })
                .ToArray();

            _idStrings = _breakpoints.Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToArray();
            _checkboxIds = _breakpoints.Select(x => "##" + x.Id.ToString(CultureInfo.InvariantCulture)).ToArray();
            _typeStrings = _breakpoints.Select(x => x.Type.ToString()).ToArray();
            _nameStrings = _breakpoints
                .Select(x =>
                {
                    if (x.Address == null)
                        return "";

                    var sym = _context.LookupSymbolForAddress(x.Address.Offset);
                    if (sym == null)
                        return "";

                    _context.Mapping.ToMemory(sym.Address, out var baseAddr, out _);
                    var offset = x.Address.Offset - baseAddr;
                    return offset == 0 ? $"{sym.Key}" : $"{sym.Key}+0x{offset:X}";
                })
                .ToArray();
            _version = session.Version;
        }

        ImGui.BeginTable("Breakpoints", 5);

        ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("Type");
        ImGui.TableSetupColumn("Address");
        ImGui.TableSetupColumn("Name");
        ImGui.TableHeadersRow();

        for (var i = 0; i < _breakpoints.Count; i++)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(_idStrings[i]);

            var bp = _breakpoints[i];
            bool enabled = bp.IsEnabled;

            ImGui.TableNextColumn();
            if (ImGui.Checkbox(_checkboxIds[i], ref enabled))
                session.EnableBreakpoint(bp.Id, enabled);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(_typeStrings[i]);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(_addressStrings[i]);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(_nameStrings[i]);
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();

        ImGui.TableNextColumn();
        ImGui.Combo("##type", ref _pendingType, PossibleTypes, PossibleTypes.Length);

        ImGui.TableNextColumn();
        ImGui.InputText("##addr", ref _pendingAddress, 32); // TODO: Autocomplete etc

        ImGui.EndTable();
    }
}
