namespace Lizard.Interfaces;

public interface ITextureStore
{
    (int handle, object texture) Get(int? handle, uint width, uint height);

    void Update(object texture, uint width, uint height, int stride, ReadOnlySpan<byte> pixelData, ReadOnlySpan<uint> paletteBuf);
    IntPtr GetImGuiBinding(int handle);
}