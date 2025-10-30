using System.Text;
using ImGuiColorTextEditNet;
using ImGuiColorTextEditNet.Syntax;
using ImGuiNET;
using Lizard.Protocol.ProtocolGen;

namespace Lizard.Gui.Windows;

class DisassemblyWindow : SingletonWindow
{
    const int MaxInstructionBytes = 16;
    const int MaxByteStringLength = MaxInstructionBytes * 2 + (MaxInstructionBytes - 1);
    const int LinesToShow = 48;

    record Line(LAddress1 Address, string Bytes, string Asm)
    {
        public string AddrString { get; } = $"{Address.Segment:X4}:{Address.Offset:X8}";
    };

    readonly CommandContext _context;
    readonly TextEditor _textViewer;

    // (Address Start, Line[] Lines)? _lastResult;
    bool _showBytes;
    uint _address;

    public DisassemblyWindow(CommandContext context)
        : base("Disassembly")
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _textViewer = new TextEditor
        {
            Options = { IsReadOnly = true, IsColorizerEnabled = true },
            SyntaxHighlighter = new DisassemblyHighlighter()
        };
    }

    void Refresh()
    {
        var session = _context.Session;
        if (!session.IsPaused)
            return;

        var ip = _context.SelectedAddress ?? session.Registers.Eip;
        if (_address == ip)
            return;

        _address = ip;
        var address = new LAddress1 { Segment = session.Registers.Cs, Offset = ip };

        session.Defer(
            new Request<Line[]>(
                session.Version,
                host =>
                {
                    var rawLines = host.Disassemble(address, LinesToShow);
                    var sb = new StringBuilder(MaxByteStringLength);
                    var formattedLines = new Line[rawLines.Length];

                    for (var i = 0; i < rawLines.Length; i++)
                    {
                        var rawLine = rawLines[i];
                        sb.Clear();
                        for (int j = 0; j < rawLine.Bytes.Length; j++)
                            sb.AppendFormat(j > 0 ? " {0:X2}" : "{0:X2}", rawLine.Bytes[j]);
                        sb.Append(' ');

                        formattedLines[i] = new Line(rawLine.Address, sb.ToString(), rawLine.Line);
                    }

                    return formattedLines;
                },
                result =>
                {
                    // _lastResult = (address, result);
                    int maxLength = result.Max(x => x.Bytes.Length);

                    _textViewer.TextLines = result
                        .Select(x =>
                            _showBytes
                                ? $"{x.AddrString} {x.Bytes.PadRight(maxLength)} {x.Asm}"
                                : $"{x.AddrString} {x.Asm}"
                        )
                        .ToList();

                    for (int i = 0; i < result.Length; i++)
                    {
                        if (result[i].Address != address)
                            continue;
                        _textViewer.Selection.HighlightedLine = i;
                        break;
                    }
                }
            )
        );
    }

    protected override void DrawContents()
    {
        Refresh();
        if (ImGui.Checkbox("Show bytes", ref _showBytes))
            _address = 0;

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
