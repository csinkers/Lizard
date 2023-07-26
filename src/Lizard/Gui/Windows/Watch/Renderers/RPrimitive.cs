using System.Numerics;
using GhidraProgramData;
using GhidraProgramData.Types;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class RPrimitive : IGhidraRenderer
{
    delegate void DrawFunc(ReadOnlySpan<byte> buffer, Vector4 color);
    readonly GPrimitive _type;
    readonly DrawFunc _drawFunc;

    public static IGhidraRenderer Get(GPrimitive gPrimitive)
    {
        if (!PrimitiveTypes.TryGetValue(gPrimitive, out var renderer))
            throw new InvalidOperationException($"No renderer has been registered for primitive type \"{gPrimitive}\"");

        return renderer;
    }

    RPrimitive(GPrimitive type, DrawFunc drawFunc)
    {
        _drawFunc = drawFunc ?? throw new ArgumentNullException(nameof(drawFunc));
        _type = type ?? throw new ArgumentNullException(nameof(type));
    }
    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => _type.FixedSize ?? 0;
    public History HistoryConstructor(string path, IHistoryCreationContext context) => History.DefaultConstructor(path, _type);
    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
    {
        history.LastAddress = address;
        if (_type.FixedSize == 0)
        {
            ImGui.TextUnformatted("");
            return false;
        }

        if (buffer.Length < _type.FixedSize)
        {
            ImGui.TextUnformatted("--");
            return false;
        }

        if (!previousBuffer.IsEmpty && !buffer.SequenceEqual(previousBuffer))
            history.LastModifiedTicks = context.Now;

        var color = Util.ColorForAge(context.Now - history.LastModifiedTicks);
        _drawFunc(buffer, color);
        return history.LastModifiedTicks == context.Now;
    }

    public static RPrimitive Bool { get; } = new(GPrimitive.Bool, DrawBool);
    public static RPrimitive SByte { get; } = new(GPrimitive.SByte, DrawInt1);
    public static RPrimitive Word { get; } = new(GPrimitive.Word, DrawInt2);
    public static RPrimitive Short { get; } = new(GPrimitive.Short, DrawInt2);
    public static RPrimitive Int { get; } = new(GPrimitive.Int, DrawInt4);
    public static RPrimitive Long { get; } = new(GPrimitive.Long, DrawInt4);
    public static RPrimitive Dword { get; } = new(GPrimitive.Dword, DrawInt4);
    public static RPrimitive LongLong { get; } = new(GPrimitive.LongLong, DrawInt8);
    public static RPrimitive Qword { get; } = new(GPrimitive.Qword, DrawInt8);
    public static RPrimitive Byte { get; } = new(GPrimitive.Byte, DrawUInt1);
    public static RPrimitive UChar { get; } = new(GPrimitive.UChar, DrawUInt1);
    public static RPrimitive UShort { get; } = new(GPrimitive.UShort, DrawUInt2);
    public static RPrimitive UInt { get; } = new(GPrimitive.UInt, DrawUInt4);
    public static RPrimitive ULong { get; } = new(GPrimitive.ULong, DrawUInt4);
    public static RPrimitive ULongLong { get; } = new(GPrimitive.ULongLong, DrawUInt8);
    public static RPrimitive Undefined { get; } = new(GPrimitive.Undefined, DrawUInt1);
    public static RPrimitive Undefined1 { get; } = new(GPrimitive.Undefined1, DrawUInt1);
    public static RPrimitive Undefined2 { get; } = new(GPrimitive.Undefined2, DrawUInt2);
    public static RPrimitive Undefined4 { get; } = new(GPrimitive.Undefined4, DrawUInt4);
    public static RPrimitive Undefined6 { get; } = new(GPrimitive.Undefined6, DrawUInt6);
    public static RPrimitive Undefined8 { get; } = new(GPrimitive.Undefined8, DrawUInt8);
    public static RPrimitive Char { get; } = new(GPrimitive.Char, DrawString);
    public static RPrimitive Float { get; } = new(GPrimitive.Float, DrawFloat);
    public static RPrimitive Double { get; } = new(GPrimitive.Double, DrawDouble);
    public static RPrimitive Float10 { get; } = new(GPrimitive.Float10, DrawFloat10);
    public static RPrimitive Void { get; } = new(GPrimitive.Void, DrawVoid);
    public static RPrimitive VaList { get; } = new(GPrimitive.VaList, DrawList);
    public static RPrimitive ImageBaseOffset32 { get; } = new(GPrimitive.ImageBaseOffset32, DrawUInt4);
    public static RPrimitive Pointer32 { get; } = new(GPrimitive.Pointer32, DrawUInt4);
    public static RPrimitive Pointer { get; } = new(GPrimitive.Pointer, Constants.PointerSize == 8 ? DrawUInt8 : DrawUInt4);
    public static RPrimitive SizeT { get; } = new(GPrimitive.SizeT, Constants.PointerSize == 8 ? DrawUInt8 : DrawUInt4);

    static readonly Dictionary<GPrimitive, RPrimitive> PrimitiveTypes = new[] {
        Bool,
        SByte, Word, Short, Int, Long, Dword, LongLong, Qword,
        Byte, UChar, UShort, UInt, ULong, ULongLong,
        Undefined, Undefined1, Undefined2, Undefined4, Undefined6, Undefined8,
        Char,
        Float, Double, Float10,
        Void,
        VaList,
        ImageBaseOffset32,
        Pointer32, Pointer,
        SizeT
    }.ToDictionary(x => x._type);

    static void DrawFloat10(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, "float10");
    static void DrawDouble(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, ((float)BitConverter.ToDouble(buffer)).ToString("g3"));
    static void DrawFloat(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, BitConverter.ToSingle(buffer).ToString("g3"));
    static void DrawInt1(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, ((sbyte)buffer[0]).ToString());
    static void DrawInt2(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, BitConverter.ToInt16(buffer).ToString());
    static void DrawInt4(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, BitConverter.ToInt32(buffer).ToString());
    static void DrawInt8(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, BitConverter.ToInt64(buffer).ToString());

    static void DrawUInt1(ReadOnlySpan<byte> buffer, Vector4 color)
    {
        var value = buffer[0];
        ImGui.TextColored(color, $"{value} ({value:X})");
    }

    static void DrawUInt2(ReadOnlySpan<byte> buffer, Vector4 color)
    {
        var value = BitConverter.ToUInt16(buffer);
        ImGui.TextColored(color, $"{value} ({value:X})");
    }

    static void DrawUInt4(ReadOnlySpan<byte> buffer, Vector4 color)
    {
        var value = BitConverter.ToUInt32(buffer);
        ImGui.TextColored(color, $"{value} ({value:X})");
    }

    static void DrawUInt6(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, "undefined6");
    static void DrawUInt8(ReadOnlySpan<byte> buffer, Vector4 color)
    {
        var value = BitConverter.ToUInt64(buffer);
        ImGui.TextColored(color, $"{value} ({value:X})");
    }

    static void DrawList(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, "va_list");
    static void DrawVoid(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, "void");
    static void DrawString(ReadOnlySpan<byte> buffer, Vector4 color) => ImGui.TextColored(color, Constants.Encoding.GetString(buffer));
    static void DrawBool(ReadOnlySpan<byte> buffer, Vector4 color)
    {
        switch (buffer.Length)
        {
            case 1: ImGui.TextColored(color, buffer[0] == 0 ? "false" : "true"); break;
            case 4: ImGui.TextColored(color, BitConverter.ToUInt32(buffer) == 0 ? "false" : "true"); break;
            default: ImGui.TextColored(color, $"bool len {buffer.Length}"); break;
        }
    }
}