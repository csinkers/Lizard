using System.Numerics;
using ImGuiColorTextEditNet;
using ImGuiNET;
using Lizard.Commands;
using Lizard.Util;

namespace Lizard.Gui.Windows;

public class CommandWindow : SingletonWindow
{
    readonly ImCommandText _commandText = new(256, CommandParser.GetCompletions);
    readonly CommandContext _context;
    readonly LogHistory _logs;
    readonly TextEditor _textEditor = new();
    bool _autoScroll = true;
    bool _scrollToBottom = true;

    public const PaletteIndex ErrorColor = PaletteIndex.Custom;
    public const PaletteIndex WarningColor = PaletteIndex.Custom + 1;
    public const PaletteIndex InfoColor = PaletteIndex.Custom + 2;
    public const PaletteIndex DebugColor = PaletteIndex.Custom + 3;
    public const PaletteIndex CodeColor = PaletteIndex.Custom + 4;
    public const PaletteIndex DataColor = PaletteIndex.Custom + 5;
    public const PaletteIndex StackColor = PaletteIndex.Custom + 6;

    public CommandWindow(CommandContext context, LogHistory history)
        : base("Command")
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logs = history ?? throw new ArgumentNullException(nameof(history));
        _textEditor.Options.IsReadOnly = true;
        _textEditor.SetColor(ErrorColor, 0xff0000ff);
        _textEditor.SetColor(WarningColor, 0xff00ffff);
        _textEditor.SetColor(InfoColor, 0xffffffff);
        _textEditor.SetColor(DebugColor, 0xffd0d0d0);
        _textEditor.SetColor(CodeColor, 0xffc570ff);
        _textEditor.SetColor(DataColor, 0xffffc677);
        _textEditor.SetColor(StackColor, 0xfffcff7f);

        _logs.Cleared += () => _textEditor.AllText = "";
        _logs.EntryAdded += x =>
        {
            _textEditor.AppendLine(x.Line);
            _textEditor.Movement.MoveToEndOfFile();
        };
    }

    protected override void DrawContents()
    {
        ImGui.SetNextWindowSize(new Vector2(800, 600), ImGuiCond.FirstUseEver);

        // Reserve enough left-over height for 1 separator + 1 input text
        float footerHeightToReserve = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing();
        ImGui.BeginChild(
            "ScrollingRegion",
            new Vector2(0, -footerHeightToReserve),
            ImGuiChildFlags.None,
            ImGuiWindowFlags.HorizontalScrollbar
        );

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 1)); // Tighten spacing

        _textEditor.Render("Log");

        if (_scrollToBottom || _autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1.0f);

        _scrollToBottom = false;

        ImGui.PopStyleVar();
        ImGui.EndChild();
        ImGui.Separator();

        if (_commandText.Draw("##command"))
        {
            var command = _commandText.Text;
            _commandText.Text = "";

            if (!string.IsNullOrWhiteSpace(command))
            {
                _logs.Add("", "> " + command, Severity.Info);
                CommandParser.RunCommand(command, _context);
            }
            ImGui.SetKeyboardFocusHere(-1);
        }

        if (JustOpened)
            ImGui.SetKeyboardFocusHere(-1);

        ImGui.SameLine();
        ImGui.Checkbox("Scroll", ref _autoScroll);
    }
}
