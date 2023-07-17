﻿using System.Numerics;
using ImGuiColorTextEditNet;
using ImGuiNET;

namespace Lizard.Gui.Windows;

public class CommandWindow : SingletonWindow
{
    const int CommandHistoryLimit = 100;
    readonly Debugger _debugger;
    readonly LogHistory _logs;
    readonly ImText _inputBuffer = new(512);
    readonly TextEditor _textEditor = new();
    readonly IndexQueue<string> _commandHistory = new();
    int _commandHistoryPosition;
    bool _autoScroll = true;
    bool _scrollToBottom = true;
    bool _pendingFocus;

    const PaletteIndex ErrorColor = PaletteIndex.Custom;
    const PaletteIndex WarningColor = PaletteIndex.Custom + 1;
    const PaletteIndex InfoColor = PaletteIndex.Custom + 2;
    const PaletteIndex DebugColor = PaletteIndex.Custom + 3;

    public CommandWindow(Debugger debugger, LogHistory history) : base("Command")
    {
        _debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));
        _logs = history ?? throw new ArgumentNullException(nameof(history));
        _textEditor.Options.IsReadOnly = true;
        _textEditor.SetColor(ErrorColor, 0xff0000ff);
        _textEditor.SetColor(WarningColor, 0xff00ffff);
        _textEditor.SetColor(InfoColor, 0xffffffff);
        _textEditor.SetColor(DebugColor, 0xffd0d0d0);

        _logs.Cleared += () => _textEditor.AllText = "";
        _logs.EntryAdded += x =>
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
    }

    public void Focus()
    {
        _pendingFocus = true;
    }

    protected override unsafe void DrawContents()
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

        if (_inputBuffer.Draw("", ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CallbackCompletion | ImGuiInputTextFlags.CallbackHistory, CommandCallback))
        {
            var command = _inputBuffer.Text;
            _inputBuffer.Text = "";

            if (!string.IsNullOrWhiteSpace(command))
            {
                _logs.Add("> " + command, Severity.Info);
                CommandParser.RunCommand(command, _debugger);

                if (_commandHistory.Count >= CommandHistoryLimit)
                    _commandHistory.Dequeue();
                _commandHistory.Enqueue(command);
            }

            _pendingFocus = true;
        }

        ImGui.SetItemDefaultFocus();
        if (_pendingFocus)
        {
            ImGui.SetKeyboardFocusHere(-1); // Auto focus previous widget
            _pendingFocus = false;
        }

        ImGui.SameLine();
        ImGui.Checkbox("Scroll", ref _autoScroll);
    }

    unsafe int CommandCallback(ImGuiInputTextCallbackData* dataPtr)
    {
        var data = new ImGuiInputTextCallbackDataPtr(dataPtr);
        switch (data.EventFlag)
        {
            case ImGuiInputTextFlags.CallbackCompletion:
                // data.InsertChars(data.CursorPos, "..");
                return 0;

            case ImGuiInputTextFlags.CallbackHistory:
                if (_commandHistory.Count == 0)
                    break;

                if (data.EventKey == ImGuiKey.UpArrow && _commandHistoryPosition < _commandHistory.Count)
                {
                    _commandHistoryPosition++;
                    data.DeleteChars(0, data.BufTextLen);
                    data.InsertChars(0, _commandHistory[^_commandHistoryPosition] ?? "");
                }
                else if (data.EventKey == ImGuiKey.DownArrow && _commandHistoryPosition > 1)
                {
                    _commandHistoryPosition--;
                    data.DeleteChars(0, data.BufTextLen);
                    data.InsertChars(0, _commandHistory[^_commandHistoryPosition] ?? "");
                }
                break;
        }

        return 0;
    }
}