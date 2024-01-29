using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using GhidraProgramData.Types;
using Lizard.Gui;
using LizardProtocol;

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

    static string DescribeAddress(uint address, CommandContext context)
    {
        var symbol = context.LookupSymbolForAddress(address);
        if (symbol == null)
            return $"{address:X8}";

        var mem = context.Mapping.ToMemory(symbol.Address)!.Value;
        if (address < mem.MemoryOffset)
            return $"{address:X8}";

        var symType = symbol.Context switch
        {
            GFunction _ => " [FUNC]",
            GGlobal _ => " [Global]",
            _ => ""
        };

        if (address == mem.MemoryOffset)
            return $"{address:X8} {symbol.Name}{symType}";

        return $"{address:X8} {symbol.Name}+{address - mem.MemoryOffset:x}{symType}";
    }

    static void PrintMemSymbols(Address address, byte[] bytes, CommandContext c)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(bytes);
        for (int i = 0; i < uints.Length; i++)
            Log.Debug($"{address.segment:X}:{address.offset + i:X8} {DescribeAddress(uints[i], c)}");
    }

    static void PrintMemPointers(Address address, byte[] bytes, CommandContext c)
    {
        var uints = MemoryMarshal.Cast<byte, uint>(bytes);
        Span<byte> temp = stackalloc byte[4];
        for (int i = 0; i < uints.Length; i++)
        {
            var byteVal = c.Session.Memory.Read(uints[i], 4, temp);
            var value = MemoryMarshal.Cast<byte, uint>(byteVal)[0];

            Log.Debug($"{address.segment:X}:{address.offset + i:X8} {uints[i]:X8} {DescribeAddress(value, c)}");
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

    static void PrintRegisters(Registers reg)
    {
        var flagsSb = new StringBuilder();
        var flags = (CpuFlags)reg.flags;
        if ((flags & CpuFlags.CF) != 0) flagsSb.Append(" C");
        if ((flags & CpuFlags.ZF) != 0) flagsSb.Append(" Z");
        if ((flags & CpuFlags.SF) != 0) flagsSb.Append(" S");
        if ((flags & CpuFlags.OF) != 0) flagsSb.Append(" O");
        if ((flags & CpuFlags.AF) != 0) flagsSb.Append(" A");
        if ((flags & CpuFlags.PF) != 0) flagsSb.Append(" P");

        if ((flags & CpuFlags.DF) != 0) flagsSb.Append(" D");
        if ((flags & CpuFlags.IF) != 0) flagsSb.Append(" I");
        if ((flags & CpuFlags.TF) != 0) flagsSb.Append(" T");

        Log.Debug($"EAX {reg.eax:X8} ESI {reg.esi:X8} DS {reg.ds:X4} ES {reg.es:X4}");
        Log.Debug($"EBX {reg.ebx:X8} EDI {reg.edi:X8} FS {reg.fs:X4} GS {reg.gs:X4}");
        Log.Debug($"ECX {reg.ecx:X8} EBP {reg.ebp:X8}");
        Log.Debug($"EDX {reg.edx:X8} ESP {reg.esp:X8} SS {reg.ss:X4}");
        Log.Debug($"CS {reg.cs:X4} EIP {reg.eip:X8}{flagsSb}");
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
                (_,  c) => PrintRegisters(c.Session.Break())),

            new(new[] { "StepOver", "p" }, "Steps to the next instruction, ignoring function calls / interrupts etc", 
                (_, c) => PrintRegisters(c.Session.StepOver())),

            new(new[] { "StepIn", "n" }, "Steps to the next instruction, including into function calls etc", 
                (_,  c) => PrintRegisters(c.Session.StepIn())),

            new(new[] { "StepMultiple", "gn" }, "Runs the CPU for the given number of cycles", (getArg, c) =>
            {
                var n = ParseUtil.ParseVal(getArg());
                PrintRegisters(c.Session.StepMultiple(n));
            }),
            new(new[] { "StepOut", "go" }, "Run until the current function returns", 
                (_, c) => PrintRegisters(c.Session.StepOut())),

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
                    PrintRegisters(c.Session.GetState());
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
                        Log.Debug($"{symbol.Address:X8} {symbol.Name}");
                })
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
}