using GhidraProgramData;

namespace Lizard.Gui.Windows.Watch;

public class DrawContext
{
    public DrawContext(ProgramData data, IMemoryCache memory, ITextureStore textureStore)
    {
        Renderers = new RendererCache();
        History = new HistoryCache(Renderers);
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        Data = data ?? throw new ArgumentNullException(nameof(data));
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
        Span<byte> backingArray = stackalloc byte[2];
        var bytes = ReadBytes(path, 2, backingArray);
        return bytes.Length != 2 ? (ushort)0 : BitConverter.ToUInt16(bytes);
    }

    public uint ReadUInt(string? path)
    {
        Span<byte> backingArray = stackalloc byte[4];
        var bytes = ReadBytes(path, 4, backingArray);
        return bytes.Length != 4 ? 0 : BitConverter.ToUInt32(bytes);
    }

    public ReadOnlySpan<byte> ReadBytes(string? path, uint size, Span<byte> backingArray)
    {
        if (path == null)
            return ReadOnlySpan<byte>.Empty;

        var history = History.TryGetHistory(path);
        if (history == null)
            return ReadOnlySpan<byte>.Empty;

        return Memory.Read(history.LastAddress, size, backingArray);
    }

    public string DescribeAddress(uint address)
    {
        var symbol = Data.LookupSymbol(address);
        if (symbol == null)
            return "(null)";

        var delta = (int)(address - symbol.Address);
        var sign = delta < 0 ? '-' : '+';
        var absDelta = Math.Abs(delta);
        return delta > 0
            ? $"{symbol.Name}{sign}0x{absDelta:X} ({address:X})"
            : symbol.Name;
    }
}