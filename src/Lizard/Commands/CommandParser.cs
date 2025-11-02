using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using GhidraProgramData.Types;
using ImGuiColorTextEditNet;
using Lizard.Comms;
using Lizard.Gui;
using Lizard.Gui.Windows;
using Lizard.Memory;
using Lizard.Session.Dump;
using Lizard.Util;

namespace Lizard.Commands;

static class CommandParser
{
    const string HexChars = "0123456789ABCDEF";
    static readonly LogTopic Log = new("Command");

    static void PrintAsm(List<LAssemblyLine1> lines)
    {
        foreach (var line in lines)
            if (line.Address != null)
                Log.Debug($"{line.Address.Segment:X}:{line.Address.Offset:X8} {line.Line}");
    }

    delegate void LinePrinter(StringBuilder sb, ReadOnlySpan<byte> bytes, int bytesPerLine);

    static void PrintLineBytes(StringBuilder sb, ReadOnlySpan<byte> bytes, int bytesPerLine)
    {
        for (int j = 0; j < bytesPerLine; j++)
        {
            if (j < bytes.Length)
            {
                var b = bytes[j];
                sb.Append(HexChars[b >> 4]);
                sb.Append(HexChars[b & 0xf]);
                sb.Append(j % 2 == 0 ? '-' : ' ');
            }
            else
                sb.Append("   ");
        }
    }

    static void PrintLineDwords(StringBuilder sb, ReadOnlySpan<byte> bytes, int bytesPerLine)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(bytes);
        for (int j = 0; j < bytesPerLine / 4; j++)
        {
            if (j < uints.Length)
            {
                var u = uints[j];
                sb.Append(u.ToString("X8"));
                sb.Append(j % 2 == 0 ? '-' : ' ');
            }
            else
                sb.Append("         ");
        }
    }

    static void PrintMem(LAddress1 address, byte[] bytes, LinePrinter printer)
    {
        var sb = new StringBuilder(128);

        int bytesPerLine = 16;
        for (var i = 0; i < bytes.Length; i += bytesPerLine)
        {
            sb.Clear();
            sb.Append($"{address.Segment:X}:{address.Offset + i:X8} ");

            var lineBytes = bytes.AsSpan(i);
            printer(sb, lineBytes, bytesPerLine);

            for (int j = 0; j < lineBytes.Length && j < bytesPerLine; j++)
            {
                char c = (char)lineBytes[j];
                if (c < 0x20 || c > 0x7f)
                    c = '.';
                sb.Append(c);
            }

            Log.Debug(sb.ToString());
        }
    }

    static void PrintMemBytes(LAddress1 address, byte[] bytes, CommandContext _) =>
        PrintMem(address, bytes, PrintLineBytes);

    static void PrintMemDwords(LAddress1 address, byte[] bytes, CommandContext _) =>
        PrintMem(address, bytes, PrintLineDwords);

    static void DescribeAddress(uint address, Line line, CommandContext context)
    {
        if (!context.Mapping.ToFile(address, out _, out var region))
        {
            line.Append(PaletteIndex.Number, $"{address:X8}");
            return;
        }

        PrintAddress(address, line, region);

        var symbol = context.LookupSymbolForAddress(address);
        if (symbol == null)
            return;

        if (!context.Mapping.ToMemory(symbol.Address, out var symMemOffset, out var symbolRegion))
            return;

        if (address < symMemOffset)
            return;

        if (symbolRegion != region)
            return;

        line.Append(" ");

        var (symType, color) = symbol.Context switch
        {
            GFunction => (" [FUNC]", CommandWindow.CodeColor),
            GGlobal => (" [Global]", CommandWindow.DataColor),
            _ => ("", PaletteIndex.Number),
        };

        line.Append(color, symbol.Name);
        if (address != symMemOffset)
            line.Append(color, $"+{address - symMemOffset:x}");

        line.Append(color, symType);
    }

    static void PrintMemSymbols(LAddress1 address, byte[] bytes, CommandContext c)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(bytes);
        for (int i = 0; i < uints.Length; i++)
        {
            var line = new Line();
            PrintAddress((uint)(address.Offset + i * 4), line, c);
            line.Append(" ");
            DescribeAddress(uints[i], line, c);
            Log.Debug(line);
        }
    }

    static void PrintAddress(uint address, Line line, CommandContext c)
    {
        if (c.Mapping.ToFile(address, out _, out var region))
            PrintAddress(address, line, region);
        else
            line.Append(PaletteIndex.Number, address.ToString("X8"));
    }

    static void PrintAddress(uint address, Line line, MemoryRegion region)
    {
        var text = address.ToString("X8");
        var color = region.Type switch
        {
            MemoryType.Unknown => PaletteIndex.Number,
            MemoryType.Code => CommandWindow.CodeColor,
            MemoryType.Data => CommandWindow.DataColor,
            MemoryType.Stack => CommandWindow.StackColor,
            _
                => throw new ArgumentOutOfRangeException(
                    nameof(region),
                    $"{nameof(region)} was of unexpected type \"{region.Type}\""
                )
        };

        line.Append(color, text);
    }

    static void PrintMemPointers(LAddress1 address, byte[] bytes, CommandContext c)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(bytes);
        Span<byte> temp = stackalloc byte[4];
        for (int i = 0; i < uints.Length; i++)
        {
            var byteVal = c.Session.Memory.Read(uints[i], 4, temp);
            var value = MemoryMarshal.Cast<byte, uint>(byteVal)[0];

            var line = new Line();
            PrintAddress((uint)(address.Offset + i), line, c);
            line.Append(" ");
            PrintAddress(uints[i], line, c);
            line.Append(" ");
            DescribeAddress(value, line, c);
        }
    }

    static DebugCommand BasePrintMem(Action<LAddress1, byte[], CommandContext> printFunc)
    {
        return (getArg, c) =>
        {
            var address = ParseUtil.ParseAddress(getArg(), c, false);
            var lengthArg = getArg();
            var length = lengthArg == "" ? 64 : ParseUtil.ParseUInt32(lengthArg);

            if (!c.Session.IsActive)
                return;
            printFunc(address, c.Session.GetMemory(address, length), c);
        };
    }

    static void PrintBps(List<LBreakpoint1> breakpoints)
    {
        foreach (var bp in breakpoints)
        {
            Log.Debug(
                bp.Address == null
                    ? $"{bp.Id} {bp.Type} {bp.Ah:X2} {bp.Al:X2}{(bp.IsEnabled ? "" : " [disabled]")}"
                    : $"{bp.Id} {bp.Address.Segment:X}:{bp.Address.Offset:X8} {bp.Type} {bp.Ah:X2} {bp.Al:X2}{(bp.IsEnabled ? "" : " [disabled]")}"
            );
        }
    }

    static void PrintDescriptors(List<LDescriptor1> descriptors, bool ldt)
    {
        for (int i = 0; i < descriptors.Count; i++)
        {
            var descriptor = descriptors[i];
            switch (descriptor.Type)
            {
                case LDescriptorType1.SysInvalid:
                    break;

                case LDescriptorType1.Sys286CallGate:
                case LDescriptorType1.SysTaskGate:
                case LDescriptorType1.Sys286IntGate:
                case LDescriptorType1.Sys286TrapGate:
                case LDescriptorType1.Sys386CallGate:
                case LDescriptorType1.Sys386IntGate:
                case LDescriptorType1.Sys386TrapGate:
                {
                    var gate = descriptor;
                    Log.Debug(
                        $"{i:X4} {gate.Type} {(gate.IsBig ? "32" : "16")} {gate.Selector:X4}: {gate.Offset:X8} R{gate.Dpl}"
                    );
                    break;
                }

                default:
                    LDescriptor1 seg = descriptor;
                    ushort selector = (ushort)(i << 3 | seg.Dpl);
                    if (ldt)
                        selector |= 4;

                    var segmentBase = seg.Offset; // See LizardProtocol1.lproto
                    var segmentLimit = seg.Selector;

                    Log.Debug(
                        $"{i:X4}={selector:X4} {seg.Type} {(seg.IsBig ? "32" : "16")} {segmentBase:X8} {segmentLimit:X8} R{seg.Dpl}"
                    );
                    break;
            }
        }
    }

    static void PrintRegisters(LRegisters1 reg, CommandContext c)
    {
        Log.Debug($"EAX {reg.Eax:X8} ESI {reg.Esi:X8} DS {reg.Ds:X4} ES {reg.Es:X4}");
        Log.Debug($"EBX {reg.Ebx:X8} EDI {reg.Edi:X8} FS {reg.Fs:X4} GS {reg.Gs:X4}");
        Log.Debug($"ECX {reg.Ecx:X8} EBP {reg.Ebp:X8}");
        Log.Debug($"EDX {reg.Edx:X8} ESP {reg.Esp:X8} SS {reg.Ss:X4}");

        var symbol = c.LookupSymbolForAddress(reg.Eip);
        if (symbol != null)
        {
            if (!c.Mapping.ToMemory(symbol.Address, out var symMemOffset, out _))
                Log.Debug($"CS {reg.Cs:X4} EIP {reg.Eip:X8} ???");
            else if (symMemOffset == reg.Eip)
                Log.Debug($"CS {reg.Cs:X4} EIP {reg.Eip:X8} {symbol.Name}");
            else
                Log.Debug($"CS {reg.Cs:X4} EIP {reg.Eip:X8} {symbol.Name}+{reg.Eip - symbol.Address:X}");
        }
        else
        {
            Log.Debug($"CS {reg.Cs:X4} EIP {reg.Eip:X8} ???");
        }

        var flagsSb = new StringBuilder();
        flagsSb.Append('[');
        var flags = (CpuFlags)reg.Flags;
        flagsSb.Append((flags & CpuFlags.CF) != 0 ? 'C' : ' ');
        flagsSb.Append((flags & CpuFlags.ZF) != 0 ? 'Z' : ' ');
        flagsSb.Append((flags & CpuFlags.SF) != 0 ? 'S' : ' ');
        flagsSb.Append((flags & CpuFlags.OF) != 0 ? 'O' : ' ');
        flagsSb.Append((flags & CpuFlags.AF) != 0 ? 'A' : ' ');
        flagsSb.Append((flags & CpuFlags.PF) != 0 ? 'P' : ' ');

        flagsSb.Append((flags & CpuFlags.DF) != 0 ? 'D' : ' ');
        flagsSb.Append((flags & CpuFlags.IF) != 0 ? 'I' : ' ');
        flagsSb.Append((flags & CpuFlags.TF) != 0 ? 'T' : ' ');
        flagsSb.Append(']');
        Log.Debug(flagsSb.ToString());
    }

    static readonly Dictionary<string, Command> Commands = new Command[]
    {
        new(
            ["help", "?"],
            "Show help",
            (_, _) =>
            {
                var commands = Commands!.Values.Distinct().OrderBy(x => x.Names[0]).ToList();
                int maxLength = 0;
                foreach (var cmd in commands)
                {
                    var length = cmd.Names.Sum(x => x.Length) + cmd.Names.Length - 1;
                    if (length > maxLength)
                        maxLength = length;
                }

                foreach (var cmd in commands)
                {
                    var names = string.Join(" ", cmd.Names);
                    var pad = new string(' ', maxLength - names.Length);
                    Log.Debug($"{names}{pad}: {cmd.Description}");
                }
            }
        ),
        new(["clear", "cls", ".cls"], "Clear the log history", (_, _) => LogHistory.Instance.Clear()),
        new(["exit", "quit"], "Exits the debugger", (_, c) => c.Exit()),
        new(["Continue", "g"], "Resume execution", (_, c) => c.Session.Continue()),
        // TODO
        new(["Break", "b"], "Pause execution", (_, c) => PrintRegisters(c.Session.Break(), c)),
        new(
            ["StepOver", "p"],
            "Steps to the next instruction, ignoring function calls / interrupts etc",
            (_, c) => PrintRegisters(c.Session.StepOver(), c)
        ),
        new(
            ["StepIn", "n"],
            "Steps to the next instruction, including into function calls etc",
            (_, c) => PrintRegisters(c.Session.StepIn(), c)
        ),
        new(
            ["StepMultiple", "gn"],
            "Runs the CPU for the given number of cycles",
            (getArg, c) =>
            {
                var n = ParseUtil.ParseUInt32(getArg());
                PrintRegisters(c.Session.StepMultiple(n), c);
            }
        ),
        new(
            ["StepOut", "go"],
            "Run until the current function returns",
            (_, c) => PrintRegisters(c.Session.StepOut(), c)
        ),
        new(
            ["RunToCall", "gc"],
            "Run until the next 'call' instruction is encountered",
            (_, _) => {
                // TODO
            }
        ),
        new(
            ["RunToAddress", "ga"],
            "Run until the given address is reached",
            (getArg, c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, true);
                c.Session.RunToAddress(address);
            }
        ),
        new(
            ["GetState", "r"],
            "Get the current CPU state or update the contents of a CPU register",
            (getArg, c) =>
            {
                var arg1 = getArg();
                var arg2 = getArg();

                if (string.IsNullOrEmpty(arg1) || string.IsNullOrEmpty(arg2))
                {
                    PrintRegisters(c.Session.GetState(), c);
                    return;
                }

                LRegister1 reg = ParseUtil.ParseReg(arg1);
                uint value = ParseUtil.ParseUInt32(arg2);
                c.Session.SetRegister(reg, value);
            }
        ),
        new(
            ["Disassemble", "u"],
            "Disassemble instructions at the given address",
            (getArg, c) =>
            {
                var addressArg = getArg();
                var address =
                    addressArg == ""
                        ? new LAddress1 { Segment = c.Session.Registers.Cs, Offset = c.Session.Registers.Eip }
                        : ParseUtil.ParseAddress(addressArg, c, true);

                var lengthArg = getArg();
                var length = addressArg == "" || lengthArg == "" ? 10 : ParseUtil.ParseUInt32(lengthArg);

                PrintAsm(c.Session.Disassemble(address, length));
            }
        ),
        new(["GetMemory", "d", "db"], "Gets the contents of memory at the given address", BasePrintMem(PrintMemBytes)),
        new(
            ["dc"],
            "Gets the contents of memory at the given address, formatting as DWORDs",
            BasePrintMem(PrintMemDwords)
        ),
        new(
            ["dps"],
            "Gets the contents of memory at the given address, formatting as symbols",
            BasePrintMem(PrintMemSymbols)
        ),
        new(
            ["dpp"],
            "Gets the contents of memory at the given address, formatting as pointers",
            BasePrintMem(PrintMemPointers)
        ),
        new(
            ["SetMemory", "e"],
            "Changes the contents of memory at the given address",
            (getArg, c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, false);
                var value = ParseUtil.ParseUInt32(getArg());
                var bytes = BitConverter.GetBytes(value);
                c.Session.SetMemory(address, bytes);
            }
        ),
        new(
            ["GetMaxAddress"],
            "Gets the maximum address that has been used in the given segment",
            (getArg, c) =>
            {
                var segString = getArg();
                var r = c.Session.Registers;
                if (!ParseUtil.TryParseSegment(segString, r, out var segment))
                {
                    Log.Error($"Could not parse \"{segString}\" as a segment");
                    return;
                }

                uint maxAddress = c.Session.GetMaxNonEmptyAddress(segment);
                Log.Info($"MaxAddress: 0x{maxAddress:X8}");
            }
        ),
        new(
            ["Search", "s"],
            "Searches for occurrences of a byte pattern in a memory range (e.g. \"s 0 -1 24 3a 99\"",
            (getArg, c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, false);
                var length = ParseUtil.ParseUInt32(getArg());
                var pattern = new List<byte>();

                string arg;
                while (!string.IsNullOrEmpty(arg = getArg()))
                {
                    if (!byte.TryParse(arg, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                    {
                        Log.Error($"Could not parse \"{arg}\" as a hex byte");
                        return;
                    }

                    pattern.Add(b);
                }

                var results = c.Session.SearchMemory(address, length, pattern.ToArray(), 1);
                int displayLength = 16 * ((pattern.Count + 15) / 16);
                foreach (var result in results)
                    PrintMemBytes(result, c.Session.GetMemory(result, (uint)displayLength), c);
            }
        ),
        new(
            ["SearchDwords", "s-d"],
            "Searches for occurrences of one or more little-endian dwords in a memory range (e.g. \"s 0 -1 badf00d 12341234\")",
            (getArg, c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, false);
                var length = ParseUtil.ParseUInt32(getArg());
                var pattern = new List<byte>();

                string arg;
                while (!string.IsNullOrEmpty(arg = getArg()))
                {
                    if (!uint.TryParse(arg, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var dword))
                    {
                        Log.Error($"Could not parse \"{arg}\" as a hex byte");
                        return;
                    }

                    pattern.Add((byte)(dword & 0xff));
                    pattern.Add((byte)(dword >> 8 & 0xff));
                    pattern.Add((byte)(dword >> 16 & 0xff));
                    pattern.Add((byte)(dword >> 24 & 0xff));
                }

                var results = c.Session.SearchMemory(address, length, pattern.ToArray(), 4);
                int displayLength = 16 * ((pattern.Count + 15) / 16);
                foreach (var result in results)
                    PrintMemBytes(result, c.Session.GetMemory(result, (uint)displayLength), c);
            }
        ),
        new(
            ["SearchAscii", "s-a"],
            "Searches for occurrences of an ASCII pattern in a memory range (e.g. \"s-a 0 -1 test\"",
            (getArg, c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, false);
                var length = ParseUtil.ParseUInt32(getArg());

                string pattern = getArg();
                if (string.IsNullOrEmpty(pattern))
                    return;

                var bytes = Encoding.ASCII.GetBytes(pattern);

                var results = c.Session.SearchMemory(address, length, bytes, 1);
                int displayLength = 16 * ((pattern.Length + 15) / 16);
                foreach (var result in results)
                    PrintMemBytes(result, c.Session.GetMemory(result, (uint)displayLength), c);
            }
        ),
        new(
            [".writemem"],
            "<path> <addr> <len> : Writes a section of memory to a local file, e.g. .dumpmem c:\\data.bin cs:0 0x800000",
            (getArg, c) =>
            {
                var filename = getArg();
                if (!Directory.Exists(Path.GetDirectoryName(filename)))
                    throw new DirectoryNotFoundException("The directory could not be found");

                var address = ParseUtil.ParseAddress(getArg(), c, false);
                var lengthArg = getArg();
                var length = lengthArg == "" ? 64 : ParseUtil.ParseUInt32(lengthArg);

                var bytes = c.Session.GetMemory(address, length);
                File.WriteAllBytes(filename, bytes);
            }
        ),
        new(
            [".dump"],
            "<path> : Saves a dump file containing the entire memory space as well as the current processor context",
            (getArg, c) =>
            {
                var filename = getArg();
                DumpFile.Save(filename, c);
            }
        ),
        new(
            ["ListBreakpoints", "bps", "bl"],
            "Retrieves the current breakpoint list",
            (_, c) =>
            {
                PrintBps(c.Session.ListBreakpoints());
            }
        ),
        new(
            ["SetBreakpoint", "bp"],
            "<address> [type] [ah] [al] - Sets or updates a breakpoint",
            (getArg, c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, true);
                var s = getArg();
                var type = s == "" ? LBreakpointType1.Normal : ParseUtil.ParseBpType(getArg());

                s = getArg();
                byte ah = s == "" ? (byte)0 : (byte)ParseUtil.ParseUInt32(s);

                s = getArg();
                byte al = s == "" ? (byte)0 : (byte)ParseUtil.ParseUInt32(s);

                var bp = new LBreakpoint1
                {
                    Id = uint.MaxValue,
                    Address = address,
                    Type = type,
                    IsEnabled = true,
                    Ah = ah,
                    Al = al
                };
                c.Session.SetBreakpoint(bp);
            }
        ),
        new(
            ["EnableBreakpoint", "be"],
            "Enables the breakpoint with the given id",
            (getArg, c) =>
            {
                var id = ParseUtil.ParseUInt32(getArg());
                c.Session.EnableBreakpoint(id, true);
            }
        ),
        new(
            ["DisableBreakpoint", "bd"],
            "Disables the breakpoint with the given id",
            (getArg, c) =>
            {
                var id = ParseUtil.ParseUInt32(getArg());
                c.Session.EnableBreakpoint(id, false);
            }
        ),
        new(
            ["DelBreakpoint", "bc"],
            "Removes the breakpoint with the given id. * will remove all breakpoints.",
            (getArg, c) =>
            {
                var idString = getArg();
                if (idString == "*")
                {
                    var all = c.Session.ListBreakpoints();
                    foreach (var bp in all)
                        c.Session.DeleteBreakpoint(bp.Id);
                }

                var id = ParseUtil.ParseUInt32(idString);
                c.Session.DeleteBreakpoint(id);
            }
        ),
        new(
            ["GetGDT", "gdt"],
            "Retrieves the Global Descriptor Table",
            (_, c) => PrintDescriptors(c.Session.GetGdt(), false)
        ),
        new(
            ["GetLDT", "ldt"],
            "Retrieves the Local Descriptor Table",
            (_, c) => PrintDescriptors(c.Session.GetLdt(), true)
        ),
        new(
            ["x"],
            "Retrieves the nearest symbol on or before the given address",
            (getArg, c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, true);
                var symbol = c.LookupSymbolForAddress(address.Offset);
                if (symbol == null)
                    Log.Warn("No symbol found");
                else
                {
                    c.Mapping.ToMemory(symbol.Address, out var symMem, out _);
                    Log.Debug($"{symMem:X8} {symbol.Name} + {address.Offset - symMem:x} = {address.Offset:X8}");
                }
            }
        ),
        new(["k"], "Print a stack trace", (_, c) => PrintStackTrace(c)),
        new(
            [".ghidra_script"],
            "Generate a python script for ghidra to add symbols to a dump file",
            (getArg, c) =>
            {
                var path = getArg();
                GenerateGhidraDumpFixups(path, c);
            }
        ),
    }
        .SelectMany(x => x.Names.Select(name => (name, x)))
        .ToDictionary(x => x.name, x => x.x, StringComparer.OrdinalIgnoreCase);

    static List<string> SplitArgs(string line)
    {
        var results = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        foreach (var c in line)
        {
            switch (c)
            {
                case '"' when inQuotes:
                    inQuotes = false;
                    if (sb.Length > 0)
                    {
                        results.Add(sb.ToString());
                        sb.Clear();
                    }
                    break;

                case '"':
                    inQuotes = true;
                    break;
                case ' ' when inQuotes:
                    sb.Append(c);
                    break;
                case ' ':
                    if (sb.Length > 0)
                    {
                        results.Add(sb.ToString());
                        sb.Clear();
                    }
                    break;

                default:
                    sb.Append(c);
                    break;
            }
        }

        if (sb.Length > 0)
            results.Add(sb.ToString());

        return results;
    }

    public static void RunCommand(string line, CommandContext c)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            var parts = SplitArgs(line); //line.Split(' ');
            var name = parts[0].ToUpperInvariant();

            if (Commands.TryGetValue(name, out var command))
            {
                int curArg = 1;
                command.Func(() => curArg >= parts.Count ? "" : parts[curArg++], c);
            }
            else
                Log.Error($"Unknown command \"{parts[0]}\"");
        }
        catch (Exception ex)
        {
            Log.Error("Parse error: " + ex.Message);
        }
    }

    public static void GetCompletions(string text, List<string> results, int maxResults)
    {
        results.Clear();
        foreach (var command in Commands)
        {
            if (!command.Key.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(command.Key);
            if (results.Count >= maxResults)
                break;
        }
    }

    static void PrintStackTrace(CommandContext c)
    {
        var stack = c.GetStackTrace();
        for (int i = 0; i < stack.Count; i++)
        {
            var frame = stack[i];
            foreach (var f in frame.Functions)
                Log.Debug($"[{i}] BP:{frame.BasePointer:x8} {f.Symbol.Name}+{f.Offset:x}");
        }
    }

    static void GenerateGhidraDumpFixups(string path, CommandContext c)
    {
        // c.Mapping, c.Symbols
        // Define functions
        // Define globals
        if (c.Symbols.Data == null)
        {
            Log.Warn("No symbol data loaded");
            return;
        }

        using var fileStream = File.Open(path, FileMode.Create, FileAccess.Write);
        using var sw = new StreamWriter(fileStream);

        // Write preamble
        sw.WriteLine(
            @"
from ghidra.program.model.symbol.SourceType import *
import string

functionManager = currentProgram.getFunctionManager()
def make_func(address, name):
    func = functionManager.getFunctionAt(toAddr(address))
    if func is not None:
        func.setName(name, USER_DEFINED)
    else:
        func = createFunction(toAddr(address), name)

def make_label(address, name):
    createLabel(toAddr(address), name, False)

"
        );
        /*
         */
        string Esc(string s) => s.Replace("\"", "\\\"");

        var r = c.Session.Registers;
        sw.WriteLine($"make_label(0x{r.Ebp:x}, \"base_pointer\")");
        sw.WriteLine($"make_label(0x{r.Eip:x}, \"instruction_pointer\")");
        sw.WriteLine($"make_label(0x{r.Esp:x}, \"stack_pointer\")");

        var stackRegion = c.Mapping.Regions.OrderBy(x => x.MemoryStart).FirstOrDefault(x => x.Type == MemoryType.Stack);

        if (stackRegion != null)
        {
            sw.WriteLine($"make_label(0x{stackRegion.MemoryStart:x}, \"stack_limit\")");
            sw.WriteLine($"make_label(0x{stackRegion.MemoryEnd:x}, \"stack_base\")");
        }

        foreach (var symbol in c.Symbols.Data.Symbols)
        {
            if (!c.Mapping.ToMemory(symbol.Address, out var memAddress, out _))
                continue;

            sw.WriteLine(
                symbol.Context is GFunction
                    ? $"make_func(0x{memAddress:x}, \"{Esc(symbol.Name)}\")"
                    : $"make_label(0x{memAddress:x}, \"{Esc(symbol.Name)}\")"
            );
        }
    }
}
