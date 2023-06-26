using Lizard.Interfaces;
using Veldrid;

namespace Lizard.Gui;

public class TextureStore : ITextureStore
{
    const int CyclePeriod = 60;
    readonly object _syncRoot = new();
    readonly GraphicsDevice _device;
    readonly ImGuiRenderer _imgui;
    Dictionary<int, Texture> _lastCache = new();
    Dictionary<int, Texture> _cache = new();
    int _nextHandle;
    int _cycleCount;

    public TextureStore(GraphicsDevice device, ImGuiRenderer imgui)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _imgui = imgui ?? throw new ArgumentNullException(nameof(imgui));
    }

    (int, Texture) Allocate(uint width, uint height)
    {
        lock (_syncRoot)
        {
            uint mipLevels = MipLevelCount(width, height);
            var description = TextureDescription.Texture2D(
                width,
                height,
                mipLevels,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled);

            var texture = _device.ResourceFactory.CreateTexture(ref description);
            int handle = _nextHandle++;
            _cache[handle] = texture;
            return (handle, texture);
        }
    }

    Texture? TryGet(int handle)
    {
        lock (_syncRoot)
        {
            if (_cache.TryGetValue(handle, out var texture))
                return texture;

            if (_lastCache.TryGetValue(handle, out texture))
            {
                _lastCache.Remove(handle);
                _cache[handle] = texture;
                return texture;
            }

            return null;
        }
    }

    (int handle, object texture) ITextureStore.Get(int? handle, uint width, uint height)
        => Get(handle, width, height);
    public (int handle, Texture texture) Get(int? handle, uint width, uint height)
    {
        var texture = handle.HasValue ? TryGet(handle.Value) : null;

        if (texture == null || texture.Width != width || texture.Height != height)
            (handle, texture) = Allocate(width, height);

        return (handle ?? -1, texture);
    }

    public IntPtr GetImGuiBinding(int handle)
    {
        var texture = TryGet(handle);
        return texture == null ? IntPtr.Zero : _imgui.GetOrCreateImGuiBinding(_device.ResourceFactory, texture);
    }

    public void Cycle()
    {
        if (_cycleCount++ < CyclePeriod)
            return;

        lock (_syncRoot)
        {
            foreach (var kvp in _lastCache)
            {
                _imgui.RemoveImGuiBinding(kvp.Value);
                kvp.Value.Dispose();
            }

            _lastCache.Clear();
            (_lastCache, _cache) = (_cache, _lastCache);
        }
    }

    void ITextureStore.Update(object texture, uint width, uint height, int stride, ReadOnlySpan<byte> pixelData, ReadOnlySpan<uint> paletteBuf)
        => Update((Texture)texture, width, height, stride, pixelData, paletteBuf);

    public void Update(Texture texture, uint width, uint height, int stride, ReadOnlySpan<byte> pixelData, ReadOnlySpan<uint> paletteBuf)
    {
        uint mipLevels = MipLevelCount(width, height);
        for (int i = 0; i < mipLevels; i++)
            UpdateMipLevel(i, texture, width, height, stride, pixelData, paletteBuf);
    }

    void UpdateMipLevel(int mipLevel, Texture texture, uint width, uint height, int stride, ReadOnlySpan<byte> pixelData, ReadOnlySpan<uint> paletteBuf)
    {
        if (pixelData.Length < width * height)
            return;

        uint w = width >> mipLevel;
        uint h = height >> mipLevel;
        uint[] image = new uint[w * h];

        int destIndex = 0;
        int row = 0;
        for (int j = 0; j < h; j++)
        {
            int srcIndex = row;
            for (int i = 0; i < w; i++)
            {
                image[destIndex++] = paletteBuf[pixelData[srcIndex]] | 0xff000000;
                srcIndex += 1 << mipLevel;
            }

            row += stride << mipLevel;
        }

        _device.UpdateTexture(texture, image, 0, 0, 0, w, h, 1, (uint)mipLevel, 0);
    }

    static uint MipLevelCount(uint width, uint height)
    {
        //*
        return 1; /*/
        uint maxDimension = Math.Max(width, height);
        uint levels = 1;
        while (maxDimension > 1)
        {
            maxDimension >>= 1;
            levels++;
        }
        return levels; //*/
    }
}