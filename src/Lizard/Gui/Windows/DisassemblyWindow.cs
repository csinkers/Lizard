using System.Text;
using ImGuiColorTextEditNet;
using ImGuiNET;
using Lizard.generated;

namespace Lizard.Gui.Windows;

class DisassemblyWindow : SingletonWindow
{
    const int MaxInstructionBytes = 16;
    const int MaxByteStringLength = MaxInstructionBytes * 2 + (MaxInstructionBytes - 1);

    record Line(string Address, string Bytes, string Asm);

    readonly Debugger _debugger;
    readonly TextEditor _textViewer;
    Address _selectedAddress;
    int _linesShown = 48;
    int _version = -1;
    bool _showBytes = true;
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
        var version = _debugger.Version;
        if (version <= _version)
            return;

        var address = new Address(_debugger.Registers.cs, _debugger.Registers.eip);

        _debugger.Defer(new Request<Line[]>(version,
            host =>
            {
                var rawLines = host.Disassemble(address, _linesShown);
                var sb = new StringBuilder(MaxByteStringLength);
                var formattedLines = new Line[rawLines.Length];

                for (var i = 0; i < rawLines.Length; i++)
                {
                    var rawLine = rawLines[i];
                    var addrString = $"{rawLine.address.segment:X4}:{rawLine.address.offset:X8}";
                    sb.Clear();
                    for (int j = 0; j < rawLine.bytes.Length; j++)
                        sb.AppendFormat(j > 0 ? " {0:X2}" : "{0:X2}", rawLine.bytes[j]);

                    formattedLines[i] = new Line(addrString, sb.ToString(), rawLine.line);
                }

                return formattedLines;
            },
            result =>
            {
                _lastResult = (address, result);
                _textViewer.TextLines = result.Select(x => _showBytes ? $"{x.Address} {x.Bytes,-MaxByteStringLength} {x.Asm}" : $"{x.Address} {x.Asm}").ToList();
            }));

        _version = version;
        _selectedAddress = address;
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
