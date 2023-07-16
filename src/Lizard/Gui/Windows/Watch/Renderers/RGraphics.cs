using System.Numerics;
using System.Runtime.InteropServices;
using GhidraProgramData;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class RGraphics : IGhidraRenderer
{
    readonly GGraphics _type;

    class GraphicsHistory : History
    {
        public GraphicsHistory(string path, IGhidraType type, string? width, string? height, string? stride, string? palette) : base(path, type)
        {
            Width = width;
            Height = height;
            Stride = stride;
            Palette = palette;
        }
        public int? TextureHandle { get; set; }
        public uint LastCheckSum { get; set; }

        // Resolved history absolute paths
        public string? Width { get; }
        public string? Height { get; }
        public string? Stride { get; }
        public string? Palette { get; }
    }

    public RGraphics(GGraphics type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => Constants.PointerSize;
    public History HistoryConstructor(string path, IHistoryCreationContext context) =>
        new GraphicsHistory(
            path,
            _type,
            context.ResolvePath(_type.Width, path),
            context.ResolvePath(_type.Height, path),
            context.ResolvePath(_type.Stride, path),
            context.ResolvePath(_type.Palette, path));

    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        history.LastAddress = address;
        if (buffer.Length < Constants.PointerSize)
        {
            ImGui.TextUnformatted("-GFX-");
            return false;
        }

        if (!previousBuffer.IsEmpty && !buffer.SequenceEqual(previousBuffer))
            history.LastModifiedTicks = context.Now;

        var h = (GraphicsHistory)history;
        uint width = context.ReadUShort(h.Width);
        uint height = context.ReadUShort(h.Height);
        uint stride = context.ReadUShort(h.Stride);
        if (stride == 0)
            stride = width;

        var rawAddress = BitConverter.ToUInt32(buffer);

        ImGui.TextUnformatted($"-GFX {width}x{height} @ {rawAddress:X}-");

        var paletteBuf = context.ReadBytes(h.Palette, 256 * 4);
        var pixelData = context.Memory.Read(rawAddress, width * height);

        if (paletteBuf.IsEmpty) { ImGui.Text("!! NO PAL !!"); return false; }
        if (pixelData.IsEmpty) { ImGui.Text("!! NO IMG !!"); return false; }

        uint sum = 0;
        foreach (var b in paletteBuf) sum = unchecked(sum + b);
        foreach (var b in pixelData) sum = unchecked(sum + b);

        var (handle, texture) = context.TextureStore.Get(h.TextureHandle, width, height);
        if (handle != h.TextureHandle || sum != h.LastCheckSum)
        {
            context.TextureStore.Update(texture, width, height, (int)stride, pixelData, MemoryMarshal.Cast<byte, uint>(paletteBuf));
            h.TextureHandle = handle;
            h.LastCheckSum = sum;
        }

        var imguiBinding = context.TextureStore.GetImGuiBinding(handle);
        if (imguiBinding == IntPtr.Zero)
            ImGui.Text("!! NO IMG !!");
        else
            ImGui.Image(imguiBinding, new Vector2(width, height));

        return false;
    }
}