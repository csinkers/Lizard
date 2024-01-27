using System.Globalization;
using ImGuiNET;
using LizardProtocol;

namespace Lizard.Gui.Windows;

public class BreakpointsWindow : SingletonWindow
{
    static readonly string[] PossibleTypes = Enum.GetNames(typeof(BreakpointType));
    readonly Debugger _debugger;
    readonly List<Breakpoint> _breakpoints = new();
    string[] _idStrings;
    string[] _checkboxIds;
    string[] _addressStrings;
    string[] _nameStrings;
    string[] _typeStrings;
    int _version = -1;
    string _pendingAddress = "";
    int _pendingType = (int)BreakpointType.Normal;

    public BreakpointsWindow(Debugger debugger) : base("Breakpoints")
    {
        _debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));
    }

    protected override void DrawContents()
    {
        if (_version != _debugger.Version)
        {
            _breakpoints.Clear();
            _breakpoints.AddRange(_debugger.ListBreakpoints());
            _addressStrings = _breakpoints.Select(x => x.type switch
            {
                BreakpointType.Normal    => $"{x.address.segment}:{x.address.offset}",
                BreakpointType.Ephemeral => $"{x.address.segment}:{x.address.offset}",
                BreakpointType.Read      => $"{x.address.segment}:{x.address.offset}",
                BreakpointType.Write     => $"{x.address.segment}:{x.address.offset}",
                BreakpointType.Interrupt => $"INT {x.address.offset:X2}",
                BreakpointType.InterruptWithAH => $"INT {x.address.offset:X2}, AH={x.ah:X2}",
                BreakpointType.InterruptWithAX => $"INT {x.address.offset:X2}, AH={x.ah:X2}, AL={x.al:X2}",
                BreakpointType.Unknown => "Unk",
                _ => throw new ArgumentOutOfRangeException()
            }).ToArray();

            _idStrings = _breakpoints.Select(x => x.id.ToString(CultureInfo.InvariantCulture)).ToArray();
            _checkboxIds = _breakpoints.Select(x => "##" + x.id.ToString(CultureInfo.InvariantCulture)).ToArray();
            _typeStrings = _breakpoints.Select(x => x.type.ToString()).ToArray();
            _nameStrings = _breakpoints.Select(x =>
            {
                var sym = _debugger.TryFindSymbol((uint)x.address.offset);
                if (sym == null)
                    return "";

                var baseAddr = _debugger.ToMemory(sym.Address)?.MemoryOffset ?? 0;
                var offset = (uint)x.address.offset - baseAddr;
                return offset == 0 ? $"{sym.Key}" : $"{sym.Key}+0x{offset:X}";
            }).ToArray();
            _version = _debugger.Version;
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
            bool enabled = bp.enabled;

            ImGui.TableNextColumn();
            if (ImGui.Checkbox(_checkboxIds[i], ref enabled))
                _debugger.EnableBreakpoint(bp.id, enabled);

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
