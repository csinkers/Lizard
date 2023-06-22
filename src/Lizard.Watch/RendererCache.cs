using GhidraProgramData;
using Lizard.Watch.Renderers;

namespace Lizard.Watch;

public class RendererCache
{
    readonly Dictionary<IGhidraType, IGhidraRenderer> _cache = new();
    public IGhidraRenderer Get(IGhidraType type)
    {
        if (!_cache.TryGetValue(type, out var renderer))
        {
            renderer = BuildRenderer(type);
            _cache[type] = renderer;
        }

        return renderer;
    }

    IGhidraRenderer BuildRenderer(IGhidraType type) =>
        type switch
        {
            GArray gArray => new RArray(gArray, this),
            GDummy gDummy => new RDummy(gDummy),
            GEnum gEnum => new REnum(gEnum),
            GFuncPointer gFuncPointer => new RFuncPointer(gFuncPointer),
            GGlobal gGlobal => new RGlobal(gGlobal),
            GGraphics gGraphics => new RGraphics(gGraphics),
            GNamespace gNamespace => new RNamespace(gNamespace),
            GPointer gPointer => new RPointer(gPointer),
            GPrimitive gPrimitive => RPrimitive.Get(gPrimitive),
            GString gString => new RString(gString),
            GStruct gStruct => new RStruct(gStruct),
            GTypeAlias gTypeAlias => new RTypeAlias(gTypeAlias),
            GUnion gUnion => new RUnion(gUnion),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
}