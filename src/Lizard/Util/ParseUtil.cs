using System.Globalization;
using Lizard.Comms;
using Lizard.Core;

namespace Lizard.Util;

internal static class ParseUtil
{
    public static LAddress1 ParseAddress(string s, CommandContext c, bool code)
    {
        var r = c.Session.Registers;
        int index = s.IndexOf(':');
        uint offset;
        ushort segment;

        if (index == -1)
        {
            offset = ParseOffset(s, c, out segment);
            if (segment == 0)
                segment = code ? r.Cs : r.Ds;
        }
        else
        {
            var part = s[..index];
            if (!TryParseSegment(part, r, out segment))
                throw new FormatException($"Invalid segment \"{part}\"");

            offset = ParseOffset(s[(index + 1)..], c, out _);
        }

        return new LAddress1 { Segment = segment, Offset = offset };
    }

    // csharpier-ignore
    public static bool TryParseSegment(string s, LRegisters1 r, out ushort segment)
    {
        if (ushort.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort temp))
        {
            segment = temp;
            return true;
        }

        switch (s.ToUpperInvariant())
        {
            case "CS": segment = r.Cs; return true;
            case "DS": segment = r.Ds; return true;
            case "SS": segment = r.Ss; return true;
            case "ES": segment = r.Es; return true;
            case "FS": segment = r.Fs; return true;
            case "GS": segment = r.Gs; return true;
            default: segment = 0; return false;
        }
    }

    public static uint ParseUInt32(string s)
    {
        if (s.StartsWith("0x"))
            return uint.Parse(s[2..], NumberStyles.HexNumber);

        if (s.StartsWith("0"))
            return uint.Parse(s[1..], NumberStyles.HexNumber);

        return uint.Parse(s);
    }

    public static int ParseInt32(string s)
    {
        if (s.StartsWith("0x"))
            return int.Parse(s[2..], NumberStyles.HexNumber);

        if (s.StartsWith("0"))
            return int.Parse(s[1..], NumberStyles.HexNumber);

        return int.Parse(s);
    }

    static uint ParseOffset(string s, CommandContext c, out ushort segmentHint)
    {
        var r = c.Session.Registers;
        segmentHint = 0;
        if (uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset))
            return offset;

        var upper = s.ToUpperInvariant();
        switch (upper)
        {
            case "EAX":
                segmentHint = r.Ds;
                return r.Eax;
            case "EBX":
                segmentHint = r.Ds;
                return r.Ebx;
            case "ECX":
                segmentHint = r.Ds;
                return r.Ecx;
            case "EDX":
                segmentHint = r.Ds;
                return r.Edx;
            case "ESI":
                segmentHint = r.Ds;
                return r.Esi;
            case "EDI":
                segmentHint = r.Ds;
                return r.Edi;
            case "EBP":
                segmentHint = r.Ss;
                return r.Ebp;
            case "ESP":
                segmentHint = r.Ss;
                return r.Esp;
            case "EIP":
                segmentHint = r.Cs;
                return r.Eip;
        }

        var sym = c.Symbols.LookupSymbol(s);
        if (sym == null)
            throw new FormatException($"Could not resolve an address for \"{s}\"");

        if (!c.Mapping.ToMemory(sym.Address, out offset, out _))
            throw new FormatException($"Symbol address {sym.Address:X8} could not be mapped to a memory address");

        return offset;
    }

    // csharpier-ignore
    public static LRegister1 ParseReg(string s) =>
        s.ToUpperInvariant() switch
        {
            "Flags" => LRegister1.Flags,
            "EAX"   => LRegister1.EAX,
            "EBX"   => LRegister1.EBX,
            "ECX"   => LRegister1.ECX,
            "EDX"   => LRegister1.EDX,
            "ESI"   => LRegister1.ESI,
            "EDI"   => LRegister1.EDI,
            "EBP"   => LRegister1.EBP,
            "ESP"   => LRegister1.ESP,
            "EIP"   => LRegister1.EIP,
            "ES"    => LRegister1.ES,
            "CS"    => LRegister1.CS,
            "SS"    => LRegister1.SS,
            "DS"    => LRegister1.DS,
            "FS"    => LRegister1.FS,
            "GS"    => LRegister1.GS,
            _ => throw new FormatException($"Unexpected register \"{s}\"")
        };

    // csharpier-ignore
    public static LBreakpointType1 ParseBpType(string s) =>
        s.ToUpperInvariant() switch
        {
            "N" => LBreakpointType1.Normal,
            "X" => LBreakpointType1.Normal,
            "R" => LBreakpointType1.Read,
            "W" => LBreakpointType1.Write,
            "NORMAL" => LBreakpointType1.Normal,
            "READ" => LBreakpointType1.Read,
            "WRITE" => LBreakpointType1.Write,
            "INTERRUPT" => LBreakpointType1.Interrupt,
            "INT" => LBreakpointType1.Interrupt,
            "INTERRUPTWITHAH" => LBreakpointType1.InterruptWithAH,
            "INTAH" => LBreakpointType1.InterruptWithAH,
            "INTAL" => LBreakpointType1.InterruptWithAX,
            _ => throw new FormatException($"Unexpected breakpoint type \"{s}\"")
        };
}
