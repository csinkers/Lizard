using System.Globalization;
using Lizard.Gui;
using LizardProtocol;

namespace Lizard.Util;

internal static class ParseUtil
{
    public static Address ParseAddress(string s, CommandContext c, bool code)
    {
        var r = c.Session.Registers;
        int index = s.IndexOf(':');
        uint offset;
        short segment;

        if (index == -1)
        {
            offset = ParseOffset(s, c, out segment);
            if (segment == 0)
                segment = code ? r.cs : r.ds;
        }
        else
        {
            var part = s[..index];
            if (!TryParseSegment(part, r, out segment))
                throw new FormatException($"Invalid segment \"{part}\"");

            offset = ParseOffset(s[(index + 1)..], c, out _);
        }

        var signedOffset = unchecked((int)offset);
        return new Address(segment, signedOffset);
    }

    // csharpier-ignore
    public static bool TryParseSegment(string s, Registers r, out short segment)
    {
        if (ushort.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var temp))
        {
            segment = (short)temp;
            return true;
        }

        switch (s.ToUpperInvariant())
        {
            case "CS": segment = r.cs; return true;
            case "DS": segment = r.ds; return true;
            case "SS": segment = r.ss; return true;
            case "ES": segment = r.es; return true;
            case "FS": segment = r.fs; return true;
            case "GS": segment = r.gs; return true;
            default: segment = 0; return false;
        }
    }

    public static int ParseVal(string s)
    {
        if (s.StartsWith("0x"))
            return int.Parse(s[2..], NumberStyles.HexNumber);

        if (s.StartsWith("0"))
            return int.Parse(s[1..], NumberStyles.HexNumber);

        return int.Parse(s);
    }

    static uint ParseOffset(string s, CommandContext c, out short segmentHint)
    {
        var r = c.Session.Registers;
        segmentHint = 0;
        if (uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset))
            return offset;

        var upper = s.ToUpperInvariant();
        switch (upper)
        {
            case "EAX":
                segmentHint = r.ds;
                return (uint)r.eax;
            case "EBX":
                segmentHint = r.ds;
                return (uint)r.ebx;
            case "ECX":
                segmentHint = r.ds;
                return (uint)r.ecx;
            case "EDX":
                segmentHint = r.ds;
                return (uint)r.edx;
            case "ESI":
                segmentHint = r.ds;
                return (uint)r.esi;
            case "EDI":
                segmentHint = r.ds;
                return (uint)r.edi;
            case "EBP":
                segmentHint = r.ss;
                return (uint)r.ebp;
            case "ESP":
                segmentHint = r.ss;
                return (uint)r.esp;
            case "EIP":
                segmentHint = r.cs;
                return (uint)r.eip;
        }

        var sym = c.Symbols.LookupSymbol(s);
        if (sym == null)
            throw new FormatException($"Could not resolve an address for \"{s}\"");

        if (!c.Mapping.ToMemory(sym.Address, out offset, out _))
            throw new FormatException($"Symbol address {sym.Address:X8} could not be mapped to a memory address");

        return offset;
    }

    // csharpier-ignore
    public static Register ParseReg(string s) =>
        s.ToUpperInvariant() switch
        {
            "Flags" => Register.Flags,
            "EAX" => Register.EAX,
            "EBX" => Register.EBX,
            "ECX" => Register.ECX,
            "EDX" => Register.EDX,
            "ESI" => Register.ESI,
            "EDI" => Register.EDI,
            "EBP" => Register.EBP,
            "ESP" => Register.ESP,
            "EIP" => Register.EIP,
            "ES" => Register.ES,
            "CS" => Register.CS,
            "SS" => Register.SS,
            "DS" => Register.DS,
            "FS" => Register.FS,
            "GS" => Register.GS,
            _ => throw new FormatException($"Unexpected register \"{s}\"")
        };

    // csharpier-ignore
    public static BreakpointType ParseBpType(string s) =>
        s.ToUpperInvariant() switch
        {
            "N" => BreakpointType.Normal,
            "X" => BreakpointType.Normal,
            "R" => BreakpointType.Read,
            "W" => BreakpointType.Write,
            "NORMAL" => BreakpointType.Normal,
            "READ" => BreakpointType.Read,
            "WRITE" => BreakpointType.Write,
            "INTERRUPT" => BreakpointType.Interrupt,
            "INT" => BreakpointType.Interrupt,
            "INTERRUPTWITHAH" => BreakpointType.InterruptWithAH,
            "INTAH" => BreakpointType.InterruptWithAH,
            "INTAL" => BreakpointType.InterruptWithAX,
            _ => throw new FormatException($"Unexpected breakpoint type \"{s}\"")
        };
}
