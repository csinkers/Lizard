using System.Numerics;
using System.Text;
using ImGuiColorTextEditNet;
using ImGuiNET;

namespace Lizard.Gui;

public class CommandWindow : SingletonWindow
{
    readonly Debugger _debugger;
    readonly LogHistory _history;
    readonly byte[] _inputBuffer = new byte[512]; // TODO: Initial size
    readonly TextEditor _textEditor = new();
    bool _autoScroll = true;
    bool _scrollToBottom = true;
    bool _focus;

    const PaletteIndex ErrorColor = PaletteIndex.Custom;
    const PaletteIndex WarningColor = PaletteIndex.Custom + 1;
    const PaletteIndex InfoColor = PaletteIndex.Custom + 2;
    const PaletteIndex DebugColor = PaletteIndex.Custom + 3;

    public CommandWindow(Debugger debugger, LogHistory history) : base("Command")
    {
        _debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _textEditor.Options.IsReadOnly = true;
        _textEditor.SetColor(ErrorColor, 0xff0000ff);
        _textEditor.SetColor(WarningColor, 0xff00ffff);
        _textEditor.SetColor(InfoColor, 0xffffffff);
        _textEditor.SetColor(DebugColor, 0xffc0c0c0);

        _history.EntryAdded += x =>
        {
            var color = x.Severity switch
            {
                Severity.Info => InfoColor,
                Severity.Warn => WarningColor,
                Severity.Error => ErrorColor,
                _ => DebugColor
            };

            _textEditor.AppendLine(x.Line, color);
            _textEditor.ScrollToEnd();
        };

        _history.Cleared += () => _textEditor.AllText = "";
    }

    protected override void DrawContents()
    {
        ImGui.SetWindowPos(Vector2.Zero, ImGuiCond.FirstUseEver);

        // Reserve enough left-over height for 1 separator + 1 input text
        float footerHeightToReserve = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing();
        ImGui.BeginChild(
            "ScrollingRegion",
            new Vector2(0, -footerHeightToReserve),
            false,
            ImGuiWindowFlags.HorizontalScrollbar);

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 1)); // Tighten spacing

        _textEditor.Render("Log");

        if (_scrollToBottom || _autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1.0f);
        _scrollToBottom = false;

        ImGui.PopStyleVar();
        ImGui.EndChild();
        ImGui.Separator();

        if (_focus)
        {
            ImGui.SetKeyboardFocusHere(0);
            _focus = false;
        }

        bool reclaimFocus = false;
        var inputTextFlags = ImGuiInputTextFlags.EnterReturnsTrue;
        if (ImGui.InputText("", _inputBuffer, (uint)_inputBuffer.Length, inputTextFlags))
        {
            var command = Encoding.ASCII.GetString(_inputBuffer);
            int index = command.IndexOf((char)0, StringComparison.Ordinal);
            command = command[..index];

            for (int i = 0; i < command.Length; i++)
                _inputBuffer[i] = 0;

            _history.Add("> " + command, Severity.Info);
            CommandParser.RunCommand(command, _debugger);
            reclaimFocus = true;
        }

        ImGui.SetItemDefaultFocus();
        if (reclaimFocus)
            ImGui.SetKeyboardFocusHere(-1); // Auto focus previous widget

        ImGui.SameLine();
        ImGui.Checkbox("Scroll", ref _autoScroll);
    }
}