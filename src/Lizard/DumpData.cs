using System.Text;
using System.Text.Json;
using LizardProtocol;

namespace Lizard;

public class DumpData
{
    public int Version { get; set; }
    public DumpRegisters Registers { get; set; }
    public List<string>? Mapping { get; set; }
    public string? DataPath { get; set; }
    public string? CodePath { get; set; }
}

public class DumpFile
{
    public DumpData? State { get; }
    public byte[] Memory { get; }
    public Registers Registers { get; }

    DumpFile(byte[] memory, DumpData data)
    {
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        State = data ?? throw new ArgumentNullException(nameof(data));

        var r = data.Registers;
        Registers = new Registers(
            true,
            r.flags,
            r.eax, r.ebx, r.ecx, r.edx,
            r.esi, r.edi,
            r.ebp, r.esp, r.eip,
            (short)r.es, (short)r.cs, (short)r.ss,
            (short)r.ds, (short)r.fs, (short)r.gs);
    }

    public static DumpFile Load(string filename)
    {
        using var file = File.Open(filename, FileMode.Open, FileAccess.Read);
        var lenBuffer = new byte[4];
        file.Position = file.Length - 4;
        if (file.Read(lenBuffer) != 4)
            throw new InvalidOperationException("Could not read length from dump file");

        int len = BitConverter.ToInt32(lenBuffer);
        file.Position = 0;
        var memory = new byte[len];
        if (file.Read(memory) < len)
            throw new InvalidOperationException("Could not read expected number of memory bytes from dump file");

        var metadata = JsonSerializer.Deserialize<DumpData>(file)
            ?? throw new InvalidOperationException("Could not read metadata from dump file");

        return new DumpFile(memory, metadata);
    }

    public static void Save(string filename, DumpData data, byte[] memoryBytes)
    {
        var dataJson = JsonSerializer.Serialize(data);
        var lengthBytes = BitConverter.GetBytes(memoryBytes.Length);
        var metadataBytes = Encoding.UTF8.GetBytes(dataJson);

        // Format: all memory bytes, metadata (utf8), memory length in bytes
        using var file = File.Open(filename, FileMode.Create, FileAccess.Write);
        file.Write(memoryBytes);
        file.Write(metadataBytes);
        file.Write(lengthBytes);
    }
}