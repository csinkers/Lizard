using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Lizard.Comms;
using Lizard.Config;
using Lizard.Config.Properties;
using Lizard.Core;
using Lizard.Util;

namespace Lizard.Session.Dump;

public class DumpFile
{
    const string StateName = "dump.json";
    const string MemoryName = "memory";
    static readonly Property<DumpRegisters> RegistersProperty = new(nameof(DumpFile), "Registers", new DumpRegisters());
    static readonly LogTopic Log = new("Dump");

    public ProjectConfig State { get; }
    public DumpRegisters Registers { get; }
    public byte[] Memory { get; }

    DumpFile(byte[] memory, ProjectConfig state)
    {
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        State = state ?? throw new ArgumentNullException(nameof(state));
        Registers =
            state.GetProperty(RegistersProperty, null)
            ?? throw new InvalidOperationException("Dump file did not contain register info");
    }

    public static DumpFile Load(string path)
    {
        if (Directory.Exists(path))
        {
            var statePath = Path.Combine(path, StateName);
            var memoryPath = Path.Combine(path, MemoryName);

            if (!File.Exists(statePath))
                throw new FormatException($"Dump archive did not contain a {StateName} entry");

            if (!File.Exists(memoryPath))
                throw new FormatException($"Dump archive did not contain a {MemoryName} entry");

            var state = ProjectConfig.Load(statePath);
            byte[] memory = File.ReadAllBytes(memoryPath);

            return new DumpFile(memory, state);
        }

        if (File.Exists(path))
        {
            using var file = File.Open(path, FileMode.Open, FileAccess.Read);
            using var zip = new ZipArchive(file);

            var entries = zip.Entries.ToDictionary(x => x.Name);
            if (!entries.TryGetValue(StateName, out var stateEntry))
                throw new FormatException($"Dump archive did not contain a {StateName} entry");

            ProjectConfig state;
            using (var stateStream = stateEntry.Open())
                state = ProjectConfig.Load(stateStream);

            if (!entries.TryGetValue(MemoryName, out var memoryEntry))
                throw new FormatException($"Dump archive did not contain a {MemoryName} entry");

            byte[] memory;
            using (var ms = new MemoryStream((int)memoryEntry.Length))
            {
                using (var memoryStream = memoryEntry.Open())
                    memoryStream.CopyTo(ms);

                memory = ms.ToArray();
            }

            return new DumpFile(memory, state);
        }
        /*
        var lenBuffer = new byte[4];
        file.Position = file.Length - 4;
        if (file.Read(lenBuffer) != 4)
            throw new InvalidOperationException("Could not read length from dump file");

        int len = BitConverter.ToInt32(lenBuffer);
        file.Position = 0;
        var memory = new byte[len];
        if (file.Read(memory) < len)
            throw new InvalidOperationException("Could not read expected number of memory bytes from dump file");

        var state = ProjectConfig.Load(file);

        return new DumpFile(memory, state)
        {
        };
        */
        throw new FileNotFoundException($"Could not find dump file / directory \"{path}\"");
    }

    public static void Save(string filename, CommandContext c)
    {
        if (!Directory.Exists(Path.GetDirectoryName(filename)))
            throw new DirectoryNotFoundException("The directory could not be found");

        if (!c.Session.IsPaused)
        {
            Log.Error("Can only write a dump when execution is paused");
            return;
        }

        var r = c.Session.Registers;
        uint maxAddress = c.Session.GetMaxNonEmptyAddress(r.Cs);
        var bytes = c.Session.GetMemory(new LAddress1 { Segment = r.Cs, Offset = 0 }, maxAddress);
        var state = new ProjectConfig();
        c.ProjectManager.Save(state);

        state.SetProperty(
            RegistersProperty,
            new DumpRegisters
            {
                Cs = r.Cs,
                Ds = r.Ds,
                Es = r.Es,
                Fs = r.Fs,
                Gs = r.Gs,
                Ss = r.Ss,
                Eax = r.Eax,
                Ebx = r.Ebx,
                Ecx = r.Ecx,
                Edx = r.Edx,
                Esi = r.Esi,
                Edi = r.Edi,
                Ebp = r.Ebp,
                Esp = r.Esp,
                Eip = r.Eip,
                Flags = r.Flags,
            }
        );

        using var stream = File.Open(filename, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        var dataJson = JsonSerializer.Serialize(state);
        AddEntry(zip, MemoryName, x => x.Write(bytes));
        AddEntry(zip, StateName, x => x.Write(Encoding.UTF8.GetBytes(dataJson)));
    }

    /* static void AddFile(ZipArchive zip, StringProperty property, ProjectConfig state)
    {
        var path = state.GetProperty(property);
        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path)) return;

        var filename = Path.GetFileName(path);
        state.SetProperty(property, filename);
        AddEntry(zip, filename, x =>
        {
            using var s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            s.CopyTo(x);
        });
    } */

    static void AddEntry(ZipArchive zip, string name, Action<Stream> writer)
    {
        var entry = zip.CreateEntry(name);
        using var stream = entry.Open();
        writer(stream);
    }

    /*
    dump.json
    memory
     */
}
