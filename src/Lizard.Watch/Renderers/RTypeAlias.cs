using GhidraProgramData;

namespace Lizard.Watch.Renderers;

public class RTypeAlias : IGhidraRenderer
{
    readonly GTypeAlias _type;
    IGhidraRenderer? _renderer;

    public RTypeAlias(GTypeAlias type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => _renderer?.GetSize(history) ?? _type.FixedSize ?? 0;
    public History HistoryConstructor(string path, IHistoryCreationContext context) => History.DefaultConstructor(path, _type);
    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        _renderer ??= context.Renderers.Get(_type.Type);
        return _renderer.Draw(history, address, buffer, previousBuffer, context);
    }
}