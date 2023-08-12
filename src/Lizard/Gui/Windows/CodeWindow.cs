using GhidraProgramData;
using GhidraProgramData.Types;
using ImGuiColorTextEditNet;

namespace Lizard.Gui.Windows;

public class CodeWindow : SingletonWindow
{
    readonly Debugger _debugger;
    readonly ProgramDataManager _program;
    readonly TextEditor _textViewer;
    DecompiledFunction? _decompiled;
    GFunction? _function;
    int _version = -1;

    public CodeWindow(Debugger debugger, ProgramDataManager program) : base("Code")
    {
        _debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));
        _program = program ?? throw new ArgumentNullException(nameof(program));
        _textViewer = new TextEditor
        {
            Options = { IsReadOnly = true, IsColorizerEnabled = true },
            SyntaxHighlighter = new CStyleHighlighter(true)
        };
    }

    void Refresh()
    {
        if (!_debugger.IsPaused)
            return;

        var version = _debugger.Version;
        if (version <= _version)
            return;

        _version = version;

        var eip = (uint)_debugger.Registers.eip;
        var mappedEip = _program.Mapping.ToFile(eip);
        if (mappedEip == null)
        {
            _decompiled = null;
            _function = null;
            _textViewer.AllText = "/* EIP outside mapped range */";
            return;
        }

        var symbol = _program.LookupSymbol(eip);

        if (symbol == null)
        {
            _decompiled = null;
            _function = null;
            _textViewer.AllText = "/* No code found for address */";
            return;
        }

        if (_function?.Address != symbol.Address)
        {
            _decompiled = _program.Code?.TryGetFunction(symbol.Address);
            if (_decompiled == null)
            {
                _textViewer.AllText = "/* No code found for address */";
                return;
            }

            _textViewer.TextLines = _decompiled.Lines;
        }
        else if (_decompiled == null)
            return;

        var lineNumber = 0;
        var bestOffset = long.MaxValue;
        for (int i = 0; i < _decompiled.Lines.Length; i++)
        {
            var offset = (long)mappedEip.Value.FileOffset - _decompiled.LineAddresses[i];
            if (offset < 0)
                continue;

            if (offset < bestOffset)
            {
                bestOffset = offset;
                lineNumber = i;
            }
        }

        // Still in the same function, just need to highlight the current line.
        _textViewer.Selection.HighlightedLine = lineNumber;
        _textViewer.ScrollToLine(lineNumber);
        // var coords = new Coordinates(lineNumber, 0);
        // _textViewer.Selection.Select(coords, coords, SelectionMode.Line);
    }

    protected override void DrawContents()
    {
        Refresh();
        _textViewer.Render("##code");
    }
}