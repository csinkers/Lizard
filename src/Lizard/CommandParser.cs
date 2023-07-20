using System.Globalization;
using System.Text;
using Lizard.generated;

namespace Lizard;

static class CommandParser
{
    static void PrintAsm(AssemblyLine[] lines)
    {
        foreach (var line in lines)
            Log.Debug($"{line.address.segment:X}:{line.address.offset:X8} {line.line}");
    }

    static void PrintBytes(Address address, byte[] bytes)
    {
        const string hexChars = "0123456789ABCDEF";
        var result = new StringBuilder(128);
        var chars = new StringBuilder(16);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i % 16 == 0)
            {
                result.Append(chars);
                Log.Debug(result.ToString());
                result.Clear();
                chars.Clear();
                result.Append($"{address.segment:X}:{address.offset + i:X8}: ");
            }

            var b = bytes[i];
            result.Append(hexChars[b >> 4]);
            result.Append(hexChars[b & 0xf]);
            result.Append(i % 2 == 0 ? '-' : ' ');
            char c = (char)b;
            if (c < 0x20 || c > 0x7f) c = '.';
            chars.Append(c);
        }

        if (chars.Length > 0)
            result.Append(chars);

        if (result.Length > 0)
            Log.Debug(result.ToString());
    }

    static void PrintBps(Breakpoint[] breakpoints)
    {
        foreach (var bp in breakpoints)
            Log.Debug($"{bp.address.segment:X}:{bp.address.offset:X8} {bp.type} {bp.ah:X2} {bp.al:X2}");
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

    static void PrintRegisters(Registers reg, Debugger d)
    {
        d.Update(reg);
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
            new(new[] {"help", "?"}, "Show help", (_,  d) =>
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
            new(new []  { "clear", "cls", ".cls" }, "Clear the log history", (_,  d) => d.Log.Clear()),
            new(new []  {"exit", "quit" }, "Exits the debugger", (_, d) => d.Exit()),

            new(new[] { "Continue", "g" }, "Resume execution", (_,  d) => d.Continue()),

            // TODO
            new(new[] { "Break", "b" }, "Pause execution", (_,  d) =>
            {
                if (!d.IsConnected) return;
                PrintRegisters(d.Break(), d);
            }),
            new(new[] { "StepOver", "p" }, "Steps to the next instruction, ignoring function calls / interrupts etc", (_, d) =>
            {
                if (!d.IsConnected) return;
                PrintRegisters(d.StepOver(), d);
            }),
            new(new[] { "StepIn", "n" }, "Steps to the next instruction, including into function calls etc", (_,  d) =>
            {
                if (!d.IsConnected) return;
                PrintRegisters(d.StepIn(), d);
            }),
            new(new[] { "StepMultiple", "gn" }, "Runs the CPU for the given number of cycles", (getArg, d) =>
            {
                var n = ParseUtil.ParseVal(getArg());
                if (!d.IsConnected) return;
                PrintRegisters(d.StepMultiple(n), d);
            }),
            new(new[] { "StepOut", "go" }, "Run until the current function returns", (_, d) =>
            {
                if (!d.IsConnected) return;
                PrintRegisters(d.StepOut(), d);
            }),
            new(new[] { "RunToCall", "gc" }, "Run until the next 'call' instruction is encountered", (_,  _) =>
            {
                // TODO
             }),

            new(new[] { "RunToAddress", "ga" }, "Run until the given address is reached", (getArg,  d) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), d, true);
                d.RunToAddress(address);
            }),

            new(new[] { "GetState", "r" }, "Get the current CPU state", (_,  d) =>
            {
                if (!d.IsConnected) return;
                PrintRegisters(d.GetState(), d);
            }),
            new(new[] { "Disassemble", "u" }, "Disassemble instructions at the given address", (getArg,  d) =>
            {
                var addressArg = getArg();
                var address = addressArg == ""
                    ? new Address(d.Registers.cs, d.Registers.eip)
                    : ParseUtil.ParseAddress(addressArg, d, true);

                var lengthArg = getArg();
                var length = addressArg == "" || lengthArg == ""
                    ? 10
                    : ParseUtil.ParseVal(lengthArg);

                if (!d.IsConnected) return;
                PrintAsm(d.Disassemble(address, length));
            }),

            new(new[] { "GetMemory", "d", "dc" }, "Gets the contents of memory at the given address", (getArg,  d) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), d, false);
                var lengthArg = getArg();
                var length = lengthArg == ""
                    ? 64
                    : ParseUtil.ParseVal(lengthArg);

                if (!d.IsConnected) return;
                PrintBytes(address, d.GetMemory(address, length));
            }),

            new(new[] { "SetMemory", "e" }, "Changes the contents of memory at the given address", (getArg,  d) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), d, false);
                var value = ParseUtil.ParseVal(getArg());
                var bytes = BitConverter.GetBytes(value);
                d.SetMemory(address, bytes);
            }),

            new(new[] { "GetMaxAddress" }, "Gets the maximum address that has been used in the given segment", (getArg,  d) =>
            {
                var segString = getArg();
                if (!ParseUtil.TryParseSegment(segString, d, out var segment))
                {
                    Log.Error($"Could not parse \"{segString}\" as a segment");
                    return;
                }

                if (!d.IsConnected) return;
                int maxAddress = d.GetMaxNonEmptyAddress(segment);
                Log.Info($"MaxAddress: 0x{(uint)maxAddress:X8}");
            }),

            new(new[] { "Search", "s" }, "Searches for occurrences of a byte pattern in a memory range (e.g. \"s 0 -1 24 3a 99\"", (getArg,  d) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), d, false);
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

                if (!d.IsConnected) return;
                var results = d.SearchMemory(address, length, pattern.ToArray(), 1);
                int displayLength = 16 * ((pattern.Count + 15) / 16);
                foreach (var result in results)
                    PrintBytes(result, d.GetMemory(result, displayLength));
            }),

            new(new[] { "SearchDwords", "s-d" }, "Searches for occurrences of one or more little-endian dwords in a memory range (e.g. \"s 0 -1 badf00d 12341234\")", (getArg,  d) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), d, false);
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

                if (!d.IsConnected) return;
                var results = d.SearchMemory(address, length, pattern.ToArray(), 4);
                int displayLength = 16 * ((pattern.Count + 15) / 16);
                foreach (var result in results)
                    PrintBytes(result, d.GetMemory(result, displayLength));
            }),

            new(new[] { "SearchAscii", "s-a" }, "Searches for occurrences of an ASCII pattern in a memory range (e.g. \"s-a 0 -1 test\"", (getArg,  d) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), d, false);
                var length = ParseUtil.ParseVal(getArg());

                string pattern = getArg();
                if (string.IsNullOrEmpty(pattern))
                    return;

                var bytes = Encoding.ASCII.GetBytes(pattern);

                if (!d.IsConnected) return;
                var results = d.SearchMemory(address, length, bytes, 1);
                int displayLength = 16 * ((pattern.Length + 15) / 16);
                foreach (var result in results)
                    PrintBytes(result, d.GetMemory(result, displayLength));
            }),

            new(new[] { ".dumpmem" }, "<path> <addr> <len> : Dumps a section of memory to a local file, e.g. .dumpmem c:\\data.bin cs:0 0x800000", (getArg, d) =>
            {
                var filename = getArg();
                if (!Directory.Exists(Path.GetDirectoryName(filename)))
                    throw new DirectoryNotFoundException("The directory could not be found");

                var address = ParseUtil.ParseAddress(getArg(), d, false);
                var lengthArg = getArg();
                var length = lengthArg == ""
                    ? 64
                    : ParseUtil.ParseVal(lengthArg);

                if (!d.IsConnected) return;
                var bytes = d.GetMemory(address, length);

                if (bytes != null)
                    File.WriteAllBytes(filename, bytes);
            }),

            new(new[] { "ListBreakpoints", "bps", "bl" }, "Retrieves the current breakpoint list", (_,  d) =>
            {
                if (!d.IsConnected) return;
                PrintBps(d.ListBreakpoints());
            }),
            new(new[] { "SetBreakpoint", "bp" }, "<address> [type] [ah] [al] - Sets or updates a breakpoint", (getArg,  d) =>
            {
                var address = ParseUtil.ParseAddress(getArg(), d, true);
                var s = getArg();
                var type = s == "" ? BreakpointType.Normal : ParseUtil.ParseBpType(getArg());

                s = getArg();
                byte ah = s == "" ? (byte)0 : (byte)ParseUtil.ParseVal(s);

                s = getArg();
                byte al = s == "" ? (byte)0 : (byte)ParseUtil.ParseVal(s);

                var bp = new Breakpoint(address, type, ah, al);
                d.SetBreakpoint(bp);
            }),

            new(new[] { "DelBreakpoint", "bd" }, "Removes the breakpoint at the given address", (getArg,  d) =>
            {
                var addr = ParseUtil.ParseAddress(getArg(), d, true);
                d.DelBreakpoint(addr);
            }),

            new(new[] { "SetReg", "reg" }, "Updates the contents of a CPU register", (getArg,  d) =>
            {
                Register reg = ParseUtil.ParseReg(getArg());
                int value = ParseUtil.ParseVal(getArg());
                d.SetReg(reg, value);
            }),

            new(new[] { "GetGDT", "gdt"}, "Retrieves the Global Descriptor Table", (getArg, d) =>
            {
                if (!d.IsConnected) return;
                PrintDescriptors(d.GetGdt(), false);
            }),

            new(new[] { "GetLDT", "ldt"}, "Retrieves the Local Descriptor Table", (getArg, d) =>
            {
                if (!d.IsConnected) return;
                PrintDescriptors(d.GetLdt(), true);
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

    public static void RunCommand(string line, Debugger d)
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
                command.Func(() => curArg >= parts.Count ? "" : parts[curArg++], d);
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