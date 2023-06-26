using GhidraProgramData;
using Lizard.Interfaces;

namespace Lizard.Watch;

public class DrawContext
{
    public DrawContext(string ghidraXmlPath, IMemoryCache memory, ITextureStore textureStore)
    {
        Renderers = new RendererCache();
        History = new HistoryCache(Renderers);
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        Data = ProgramData.Load(ghidraXmlPath);
        TextureStore = textureStore ?? throw new ArgumentNullException(nameof(textureStore));
    }

    public ProgramData Data { get; }
    public RendererCache Renderers { get; }
    public IMemoryCache Memory { get; }
    public HistoryCache History { get; }
    public ITextureStore TextureStore { get; }
    public long Now { get; set; }
    public bool Refreshed { get; set; }
    public float SinceStart => Util.Timestamp(Now);
    public string Filter { get; set; } = "";

    public ushort ReadUShort(string? path)
    {
        var bytes = ReadBytes(path, 2);
        return bytes.Length != 2 ? (ushort)0 : BitConverter.ToUInt16(bytes);
    }

    public uint ReadUInt(string? path)
    {
        var bytes = ReadBytes(path, 4);
        return bytes.Length != 4 ? 0 : BitConverter.ToUInt32(bytes);
    }

    public ReadOnlySpan<byte> ReadBytes(string? path, uint size)
    {
        if (path == null)
            return ReadOnlySpan<byte>.Empty;

        var history = History.TryGetHistory(path);
        return history == null ? ReadOnlySpan<byte>.Empty : Memory.Read(history.LastAddress, size);
    }

    public string DescribeAddress(uint address)
    {
        var (symAddress, name, _) = Data.Lookup(address);
        if (address == 0)
            return "(null)";

        var delta = (int)(address - symAddress);
        var sign = delta < 0 ? '-' : '+';
        var absDelta = Math.Abs(delta);
        return delta > 0 
            ? $"{name}{sign}0x{absDelta:X} ({address:X})" 
            : name;
    }
}