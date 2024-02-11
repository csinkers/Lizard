using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using GhidraProgramData.Types;
using ImGuiColorTextEditNet;
using Lizard.Gui;
using Lizard.Gui.Windows;
using LizardProtocol;
using Exception = System.Exception;

namespace Lizard;

static class CommandParser
{
    static readonly LogTopic Log = new("Command");
    static void PrintAsm(AssemblyLine[] lines)
    {
        foreach (var line in lines)
            Log.Debug($"{line.address.segment:X}:{line.address.offset:X8} {line.line}");
    }

    const string HexChars = "0123456789ABCDEF";

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

    static void PrintMem(Address address, byte[] bytes, LinePrinter printer)
    {
        var sb = new StringBuilder(128);

        int bytesPerLine = 16;
        for (var i = 0; i < bytes.Length; i += bytesPerLine)
        {
            sb.Clear();
            sb.Append($"{address.segment:X}:{address.offset + i:X8} ");

            var lineBytes = bytes.AsSpan(i);
            printer(sb, lineBytes, bytesPerLine);

            for (int j = 0; j < lineBytes.Length && j < bytesPerLine; j++)
            {
                char c = (char)lineBytes[j];
                if (c < 0x20 || c > 0x7f) c = '.';
                sb.Append(c);
            }

            Log.Debug(sb.ToString());
        }
    }

    static void PrintMemBytes(Address address, byte[] bytes, CommandContext _) => PrintMem(address, bytes, PrintLineBytes);
    static void PrintMemDwords(Address address, byte[] bytes, CommandContext _) => PrintMem(address, bytes, PrintLineDwords);

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
            GFunction _ => (" [FUNC]", CommandWindow.CodeColor),
            GGlobal _ => (" [Global]", CommandWindow.DataColor),
            _ => ("", PaletteIndex.Number),
        };

        line.Append(color, symbol.Name);
        if (address != symMemOffset)
            line.Append(color, $"+{address - symMemOffset:x}");

        line.Append(color, symType);
    }

    static void PrintMemSymbols(Address address, byte[] bytes, CommandContext c)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(bytes);
        for (int i = 0; i < uints.Length; i++)
        {
            var line = new Line();
            PrintAddress((uint)(address.offset + i * 4), line, c);
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
            _ => throw new ArgumentOutOfRangeException()
        };

        line.Append(color, text);
    }

    static void PrintMemPointers(Address address, byte[] bytes, CommandContext c)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(bytes);
        Span<byte> temp = stackalloc byte[4];
        for (int i = 0; i < uints.Length; i++)
        {
            var byteVal = c.Session.Memory.Read(uints[i], 4, temp);
            var value = MemoryMarshal.Cast<byte, uint>(byteVal)[0];

            var line = new Line();
            PrintAddress((uint)(address.offset + i), line, c);
            line.Append(" ");
            PrintAddress(uints[i], line, c);
            line.Append(" ");
            DescribeAddress(value, line, c);
        }
    }

    static DebugCommand BasePrintMem(Action<Address, byte[], CommandContext> printFunc)
    {
        return (getArg, c) =>
        {
            var address = ParseUtil.ParseAddress(getArg(), c, false);
            var lengthArg = getArg();
            var length = lengthArg == ""
                ? 64
                : ParseUtil.ParseVal(lengthArg);

            if (!c.Session.IsActive) return;
            printFunc(address, c.Session.GetMemory(address, length), c);
        };
    }

    static void PrintBps(Breakpoint[] breakpoints)
    {
        foreach (var bp in breakpoints)
            Log.Debug($"{bp.id} {bp.address.segment:X}:{bp.address.offset:X8} {bp.type} {bp.ah:X2} {bp.al:X2}{(bp.enabled ? "" : " [disabled]")}");
    }

    static void PrintDescriptors(Descriptor[] descriptors, bool ldt)
    {
        for (int i = 0; i < descriptors.Length; i++)
        {
            var descriptor = descriptors[i];
            switch (descriptor.type)
            {
                case SegmentType.SysInvalid:
                    break;

                case SegmentType.Sys286CallGate:
                case SegmentType.SysTaskGate:
                case SegmentType.Sys286IntGate:
                case SegmentType.Sys286TrapGate:
                case SegmentType.Sys386CallGate:
                case SegmentType.Sys386IntGate:
                case SegmentType.Sys386TrapGate:
                    var gate = (GateDescriptor)descriptor;
                    Log.Debug($"{i:X4} {gate.type} {(gate.big ? "32" : "16")} {gate.selector:X4}: {gate.offset:X8} R{gate.dpl}");
                    break;

                default:
                    var seg = (SegmentDescriptor)descriptor;
                    ushort selector = (ushort)((i << 3) | seg.dpl);
                    if (ldt)
                        selector |= 4;
                    Log.Debug($"{i:X4}={selector:X4} {seg.type} {(seg.big ? "32" : "16")} {seg.@base:X8} {seg.limit:X8} R{seg.dpl}");
                    break;
            }
        }
    }

    static void PrintRegisters(Registers reg, CommandContext c)
    {
        Log.Debug($"EAX {reg.eax:X8} ESI {reg.esi:X8} DS {reg.ds:X4} ES {reg.es:X4}");
        Log.Debug($"EBX {reg.ebx:X8} EDI {reg.edi:X8} FS {reg.fs:X4} GS {reg.gs:X4}");
        Log.Debug($"ECX {reg.ecx:X8} EBP {reg.ebp:X8}");
        Log.Debug($"EDX {reg.edx:X8} ESP {reg.esp:X8} SS {reg.ss:X4}");

        var symbol = c.LookupSymbolForAddress((uint)reg.eip);
        if (symbol != null)
        {
            if (!c.Mapping.ToMemory(symbol.Address, out var symMemOffset, out _))
                Log.Debug($"CS {reg.cs:X4} EIP {reg.eip:X8} ???");
            else if (symMemOffset == reg.eip)
                Log.Debug($"CS {reg.cs:X4} EIP {reg.eip:X8} {symbol.Name}");
            else
                Log.Debug($"CS {reg.cs:X4} EIP {reg.eip:X8} {symbol.Name}+{reg.eip - symbol.Address:X}");
        }
        else
        {
            Log.Debug($"CS {reg.cs:X4} EIP {reg.eip:X8} ???");
        }

        var flagsSb = new StringBuilder();
        flagsSb.Append('[');
        var flags = (CpuFlags)reg.flags;
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
            new(new[] { "help", "?" }, "Show help", (_,  c) =>
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
            }),
            new(new []  { "clear", "cls", ".cls" }, "Clear the log history",
                (_,  c) => LogHistory.Instance.Clear()),

            new(new []  { "exit", "quit" }, "Exits the debugger",
                (_, c) => c.Exit()),

            new(new[] { "Continue", "g" }, "Resume execution",
                (_,  c) => c.Session.Continue()),

            // TODO
            new(new[] { "Break", "b" }, "Pause execution",
                (_,  c) => PrintRegisters(c.Session.Break(), c)),

            new(new[] { "StepOver", "p" }, "Steps to the next instruction, ignoring function calls / interrupts etc",
                (_, c) => PrintRegisters(c.Session.StepOver(), c)),

            new(new[] { "StepIn", "n" }, "Steps to the next instruction, including into function calls etc",
                (_,  c) => PrintRegisters(c.Session.StepIn(), c)),

            new(new[] { "StepMultiple", "gn" }, "Runs the CPU for the given number of cycles", (getArg, c) =>
            {
                var n = ParseUtil.ParseVal(getArg());
                PrintRegisters(c.Session.StepMultiple(n), c);
            }),
            new(new[] { "StepOut", "go" }, "Run until the current function returns",
                (_, c) => PrintRegisters(c.Session.StepOut(), c)),

            new(new[] { "RunToCall", "gc" }, "Run until the next 'call' instruction is encountered", (_,  _) =>
            {
                // TODO
             }),

            new(new[] { "RunToAddress", "ga" }, "Run until the given address is reached", (getArg,  c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, true);
                c.Session.RunToAddress(address);
            }),

            new(new[] { "GetState", "r" }, "Get the current CPU state or update the contents of a CPU register", (getArg,  c) =>
            {
                var arg1 = getArg();
                var arg2 = getArg();

                if (string.IsNullOrEmpty(arg1) || string.IsNullOrEmpty(arg2))
                {
                    PrintRegisters(c.Session.GetState(), c);
                    return;
                }

                Register reg = ParseUtil.ParseReg(arg1);
                int value = ParseUtil.ParseVal(arg2);
                c.Session.SetRegister(reg, value);
            }),

            new(new[] { "Disassemble", "u" }, "Disassemble instructions at the given address", (getArg,  c) =>
            {
                var addressArg = getArg();
                var address = addressArg == ""
                    ? new Address(c.Session.Registers.cs, c.Session.Registers.eip)
                    : ParseUtil.ParseAddress(addressArg, c, true);

                var lengthArg = getArg();
                var length = addressArg == "" || lengthArg == ""
                    ? 10
                    : ParseUtil.ParseVal(lengthArg);

                PrintAsm(c.Session.Disassemble(address, length));
            }),

            new(new[] { "GetMemory", "d", "db" }, "Gets the contents of memory at the given address", BasePrintMem(PrintMemBytes)),
            new(new[] { "dc" }, "Gets the contents of memory at the given address, formatting as DWORDs", BasePrintMem(PrintMemDwords)),
            new(new[] { "dps" }, "Gets the contents of memory at the given address, formatting as symbols", BasePrintMem(PrintMemSymbols)),
            new(new[] { "dpp" }, "Gets the contents of memory at the given address, formatting as pointers", BasePrintMem(PrintMemPointers)),

            new(new[] { "SetMemory", "e" }, "Changes the contents of memory at the given address", (getArg,  c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, false);
                var value = ParseUtil.ParseVal(getArg());
                var bytes = BitConverter.GetBytes(value);
                c.Session.SetMemory(address, bytes);
            }),

            new(new[] { "GetMaxAddress" }, "Gets the maximum address that has been used in the given segment", (getArg,  c) =>
            {
                var segString = getArg();
                var r = c.Session.Registers;
                if (!ParseUtil.TryParseSegment(segString, r, out var segment))
                {
                    Log.Error($"Could not parse \"{segString}\" as a segment");
                    return;
                }

                int maxAddress = c.Session.GetMaxNonEmptyAddress(segment);
                Log.Info($"MaxAddress: 0x{(uint)maxAddress:X8}");
            }),

            new(new[] { "Search", "s" }, "Searches for occurrences of a byte pattern in a memory range (e.g. \"s 0 -1 24 3a 99\"", (getArg,  c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, false);
                var length = ParseUtil.ParseVal(getArg());
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
                    PrintMemBytes(result, c.Session.GetMemory(result, displayLength), c);
            }),

            new(new[] { "SearchDwords", "s-d" }, "Searches for occurrences of one or more little-endian dwords in a memory range (e.g. \"s 0 -1 badf00d 12341234\")", (getArg,  c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, false);
                var length = ParseUtil.ParseVal(getArg());
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
                    pattern.Add((byte)((dword >> 8)  & 0xff));
                    pattern.Add((byte)((dword >> 16) & 0xff));
                    pattern.Add((byte)((dword >> 24) & 0xff));
                }

                var results = c.Session.SearchMemory(address, length, pattern.ToArray(), 4);
                int displayLength = 16 * ((pattern.Count + 15) / 16);
                foreach (var result in results)
                    PrintMemBytes(result, c.Session.GetMemory(result, displayLength), c);
            }),

            new(new[] { "SearchAscii", "s-a" }, "Searches for occurrences of an ASCII pattern in a memory range (e.g. \"s-a 0 -1 test\"", (getArg,  c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, false);
                var length = ParseUtil.ParseVal(getArg());

                string pattern = getArg();
                if (string.IsNullOrEmpty(pattern))
                    return;

                var bytes = Encoding.ASCII.GetBytes(pattern);

                var results = c.Session.SearchMemory(address, length, bytes, 1);
                int displayLength = 16 * ((pattern.Length + 15) / 16);
                foreach (var result in results)
                    PrintMemBytes(result, c.Session.GetMemory(result, displayLength), c);
            }),

            new(new[] { ".writemem" }, "<path> <addr> <len> : Writes a section of memory to a local file, e.g. .dumpmem c:\\data.bin cs:0 0x800000", (getArg, c) =>
            {
                var filename = getArg();
                if (!Directory.Exists(Path.GetDirectoryName(filename)))
                    throw new DirectoryNotFoundException("The directory could not be found");

                var address = ParseUtil.ParseAddress(getArg(), c, false);
                var lengthArg = getArg();
                var length = lengthArg == ""
                    ? 64
                    : ParseUtil.ParseVal(lengthArg);

                var bytes = c.Session.GetMemory(address, length);
                File.WriteAllBytes(filename, bytes);
            }),

            new(new[] { ".dump" }, "<path> : Saves a dump file containing the entire memory space as well as the current processor context", (getArg, c) =>
            {
                var filename = getArg();
                DumpFile.Save(filename, c);
            }),

            new(new[] { "ListBreakpoints", "bps", "bl" }, "Retrieves the current breakpoint list", (_,  c) =>
            {
                PrintBps(c.Session.ListBreakpoints());
            }),
            new(new[] { "SetBreakpoint", "bp" }, "<address> [type] [ah] [al] - Sets or updates a breakpoint", (getArg,  c) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), c, true);
                var s = getArg();
                var type = s == "" ? BreakpointType.Normal : ParseUtil.ParseBpType(getArg());

                s = getArg();
                byte ah = s == "" ? (byte)0 : (byte)ParseUtil.ParseVal(s);

                s = getArg();
                byte al = s == "" ? (byte)0 : (byte)ParseUtil.ParseVal(s);

                var bp = new Breakpoint(-1, address, type, true, ah, al);
                c.Session.SetBreakpoint(bp);
            }),

            new(new[] { "EnableBreakpoint", "be" }, "Enables the breakpoint with the given id", (getArg,  c) =>
            {
                var id = ParseUtil.ParseVal(getArg());
                c.Session.EnableBreakpoint(id, true);
            }),

            new(new[] { "DisableBreakpoint", "bd" }, "Disables the breakpoint with the given id", (getArg,  c) =>
            {
                var id = ParseUtil.ParseVal(getArg());
                c.Session.EnableBreakpoint(id, false);
            }),

            new(new[] { "DelBreakpoint", "bc" }, "Removes the breakpoint with the given id. * will remove all breakpoints.", (getArg,  c) =>
            {
                var idString = getArg();
                if (idString == "*")
                {
                    var all = c.Session.ListBreakpoints();
                    foreach(var bp in all)
                        c.Session.DelBreakpoint(bp.id);
                }

                var id = ParseUtil.ParseVal(idString);
                c.Session.DelBreakpoint(id);
            }),
            new(new[] { "GetGDT", "gdt" }, "Retrieves the Global Descriptor Table",
                (getArg, c) => PrintDescriptors(c.Session.GetGdt(), false)),

            new(new[] { "GetLDT", "ldt" }, "Retrieves the Local Descriptor Table",
                (getArg, c) => PrintDescriptors(c.Session.GetLdt(), true)),

            new(new[] { "x" }, "Retrieves the nearest symbol on or before the given address",
                (getArg, c) =>
                {
                    var address = ParseUtil.ParseAddress(getArg(), c, true);
                    var symbol = c.LookupSymbolForAddress((uint)address.offset);
                    if (symbol == null)
                        Log.Warn("No symbol found");
                    else
                    {
                        c.Mapping.ToMemory(symbol.Address, out var symMem, out _);
                        Log.Debug($"{symMem:X8} {symbol.Name} + {address.offset - symMem:x} = {address.offset:X8}");
                    }
                }),
            new(new[] { "k" }, "Print a stack trace",
                (getArg, c) => PrintStackTrace(c)),
            new(new[] { ".ghidra_script" }, "Generate a python script for ghidra to add symbols to a dump file",
                (getArg, c) =>
                {
                    var path = getArg();
                    GenerateGhidraDumpFixups(path, c);
                }),

        }.SelectMany(x => x.Names.Select(name => (name, x)))
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

                case '"': inQuotes = true; break;
                case ' ' when inQuotes: sb.Append(c); break;
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
            else Log.Error($"Unknown command \"{parts[0]}\"");
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
        sw.WriteLine(@"
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

");
        /*
         */
        string Esc(string s) => s.Replace("\"", "\\\"");

        var r = c.Session.Registers;
        sw.WriteLine($"make_label(0x{r.ebp:x}, \"base_pointer\")");
        sw.WriteLine($"make_label(0x{r.eip:x}, \"instruction_pointer\")");
        sw.WriteLine($"make_label(0x{r.esp:x}, \"stack_pointer\")");

        var stackRegion = c.Mapping.Regions
                .OrderBy(x => x.MemoryStart)
                .FirstOrDefault(x => x.Type == MemoryType.Stack);

        if (stackRegion != null)
        {
            sw.WriteLine($"make_label(0x{stackRegion.MemoryStart:x}, \"stack_limit\")");
            sw.WriteLine($"make_label(0x{stackRegion.MemoryEnd:x}, \"stack_base\")");
        }

        foreach (var symbol in c.Symbols.Data.Symbols)
        {
            if (!c.Mapping.ToMemory(symbol.Address, out var memAddress, out _))
                continue;

            sw.WriteLine(symbol.Context is GFunction
                ? $"make_func(0x{memAddress:x}, \"{Esc(symbol.Name)}\")"
                : $"make_label(0x{memAddress:x}, \"{Esc(symbol.Name)}\")");
        }
    }
}