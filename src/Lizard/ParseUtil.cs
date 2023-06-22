using System.Globalization;

namespace Lizard;

static class ParseUtil
{
    public static Address ParseAddress(string s, Debugger d, bool code)
    {
        int index = s.IndexOf(':');
        uint offset;
        short segment;

        if (index == -1)
        {
            offset = ParseOffset(s, d, out segment);
            if (segment == 0)
                segment = code ? d.Registers.cs : d.Registers.ds;
        }
        else
        {
            var part = s[..index];
            if (!TryParseSegment(part, d, out segment))
                throw new FormatException($"Invalid segment \"{part}\"");

            offset = ParseOffset(s[(index+1)..], d, out _);
        }

        var signedOffset = unchecked((int)offset);
        return new Address(segment, signedOffset);
    }

    public static bool TryParseSegment(string s, Debugger d, out short segment)
    {
        if (ushort.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var temp))
        {
            segment = (short)temp;
            return true;
        }

        switch (s.ToUpperInvariant())
        {
            case "CS": segment = d.Registers.cs; break;
            case "DS": segment = d.Registers.ds; break;
            case "SS": segment = d.Registers.ss; break;
            case "ES": segment = d.Registers.es; break;
            case "FS": segment = d.Registers.fs; break;
            case "GS": segment = d.Registers.gs; break;
            default: segment = 0; return false;
        }

        return true;
    }

    public static int ParseVal(string s)
    {
        if (s.StartsWith("0x"))
            return int.Parse(s[2..], NumberStyles.HexNumber);

        if (s.StartsWith("0"))
            return int.Parse(s[1..], NumberStyles.HexNumber);

        return int.Parse(s);
    }

    static uint ParseOffset(string s, Debugger d, out short segmentHint)
    {
        segmentHint = 0;
        if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset))
        {
            var upper = s.ToUpperInvariant();
            switch (upper)
            {
                case "EAX": segmentHint = d.Registers.ds; return (uint)d.Registers.eax;
                case "EBX": segmentHint = d.Registers.ds; return (uint)d.Registers.ebx;
                case "ECX": segmentHint = d.Registers.ds; return (uint)d.Registers.ecx;
                case "EDX": segmentHint = d.Registers.ds; return (uint)d.Registers.edx;
                case "ESI": segmentHint = d.Registers.ds; return (uint)d.Registers.esi;
                case "EDI": segmentHint = d.Registers.ds; return (uint)d.Registers.edi;
                case "EBP": segmentHint = d.Registers.ss; return (uint)d.Registers.ebp;
                case "ESP": segmentHint = d.Registers.ss; return (uint)d.Registers.esp;
                case "EIP": segmentHint = d.Registers.cs; return (uint)d.Registers.eip;
            }

            if (!d.TryFindSymbol(s, out offset))
                throw new FormatException($"Could not resolve an address for \"{s}\"");
        }

        return offset;
    }

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

    public static BreakpointType ParseBpType(string s) =>
        s.ToUpperInvariant() switch
        {
            "NORMAL" => BreakpointType.Normal,
            "N" => BreakpointType.Normal,
            "X" => BreakpointType.Normal,
            "READ" => BreakpointType.Read,
            "R" => BreakpointType.Read,
            "WRITE" => BreakpointType.Write,
            "W" => BreakpointType.Write,
            "INTERRUPT" => BreakpointType.Interrupt,
            "INT" => BreakpointType.Interrupt,
            "INTERRUPTWITHAH" => BreakpointType.InterruptWithAH,
            "INTAH" => BreakpointType.InterruptWithAH,
            "INTAL" => BreakpointType.InterruptWithAX,
            _ => throw new FormatException($"Unexpected breakpoint type \"{s}\"")
        };
}