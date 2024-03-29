﻿using GhidraProgramData;
using GhidraProgramData.Types;
using ImGuiColorTextEditNet;

namespace Lizard.Gui.Windows;

public class CodeWindow : SingletonWindow
{
    readonly CommandContext _context;
    readonly TextEditor _textViewer;
    DecompiledFunction? _decompiled;
    GFunction? _function;
    uint _address;

    public CodeWindow(CommandContext context)
        : base("Code")
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _textViewer = new TextEditor
        {
            Options = { IsReadOnly = true, IsColorizerEnabled = true },
            SyntaxHighlighter = new CStyleHighlighter(true)
        };
    }

    void Refresh()
    {
        var session = _context.Session;
        if (!session.IsPaused)
            return;

        var ip = _context.SelectedAddress ?? (uint)session.Registers.eip;
        if (_address == ip)
            return;

        _address = ip;

        if (!_context.Mapping.ToFile(ip, out var mappedEip, out _))
        {
            _decompiled = null;
            _function = null;
            _textViewer.AllText = "/* EIP outside mapped range */";
            return;
        }

        var symbol = _context.LookupSymbolForAddress(ip);

        if (symbol == null)
        {
            _decompiled = null;
            _function = null;
            _textViewer.AllText = "/* No code found for address */";
            return;
        }

        if (_function?.Address != symbol.Address)
        {
            _decompiled = _context.Symbols.Code?.TryGetFunction(symbol.Address);
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
            var offset = (long)mappedEip - _decompiled.LineAddresses[i];
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
