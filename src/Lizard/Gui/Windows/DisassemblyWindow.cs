﻿using System.Text;
using ImGuiColorTextEditNet;
using ImGuiNET;
using LizardProtocol;

namespace Lizard.Gui.Windows;

class DisassemblyWindow : SingletonWindow
{
    const int MaxInstructionBytes = 16;
    const int MaxByteStringLength = MaxInstructionBytes * 2 + (MaxInstructionBytes - 1);
    const int LinesToShow = 48;

    record Line(Address Address, string Bytes, string Asm)
    {
        public string AddrString { get; } = $"{Address.segment:X4}:{Address.offset:X8}";
    };

    readonly Debugger _debugger;
    readonly TextEditor _textViewer;
    int _version = -1;
    bool _showBytes;
    (Address Start, Line[] Lines)? _lastResult;

    public DisassemblyWindow(Debugger debugger) : base("Disassembly")
    {
        _debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));
        _textViewer = new TextEditor
        {
            Options = { IsReadOnly = true, IsColorizerEnabled = true },
            SyntaxHighlighter = new DisassemblyHighlighter()
        };
    }

    void Refresh()
    {
        if (!_debugger.IsPaused)
            return;

        var version = _debugger.Version;
        if (version <= _version)
            return;

        var address = new Address(_debugger.Registers.cs, _debugger.Registers.eip);

        _debugger.Defer(new Request<Line[]>(version,
            host =>
            {
                var rawLines = host.Disassemble(address, LinesToShow);
                var sb = new StringBuilder(MaxByteStringLength);
                var formattedLines = new Line[rawLines.Length];

                for (var i = 0; i < rawLines.Length; i++)
                {
                    var rawLine = rawLines[i];
                    sb.Clear();
                    for (int j = 0; j < rawLine.bytes.Length; j++)
                        sb.AppendFormat(j > 0 ? " {0:X2}" : "{0:X2}", rawLine.bytes[j]);
                    sb.Append(' ');

                    formattedLines[i] = new Line(rawLine.address, sb.ToString(), rawLine.line);
                }

                return formattedLines;
            },
            result =>
            {
                _lastResult = (address, result);
                int maxLength = result.Max(x => x.Bytes.Length);

                _textViewer.TextLines = result.Select(x => 
                    _showBytes 
                        ? $"{x.AddrString} {x.Bytes.PadRight(maxLength)} {x.Asm}" 
                        : $"{x.AddrString} {x.Asm}")
                    .ToList();

                for (int i = 0; i < result.Length; i++)
                {
                    if (result[i].Address != address) continue;
                    _textViewer.Selection.HighlightedLine = i;
                    break;
                }
            }));

        _version = version;
    }

    protected override void DrawContents()
    {
        Refresh();
        if (ImGui.Checkbox("Show bytes", ref _showBytes))
            _version = -1;

        _textViewer.Render("##dasm");
    }

    class DisassemblyHighlighter : ISyntaxHighlighter
    {
        static readonly object DummyState = new();
        public bool AutoIndentation => false;
        public int MaxLinesPerFrame => 1024;
        public string? GetTooltip(string id) => null;
        public object Colorize(Span<Glyph> line, object? state)
        {
            for (int i = 0; i < line.Length; i++)
                line[i] = new Glyph(line[i].Char, PaletteIndex.Identifier);
            return DummyState;
        }
    }
}
