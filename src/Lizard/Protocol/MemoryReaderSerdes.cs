using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SerdesNet;

namespace Lizard.Protocol;

/// <summary>
/// A serializer that reads data from a <see cref="ReadOnlyMemory{T}"/>.
/// </summary>
public sealed class MemoryReaderSerdes : ISerdes
{
    readonly ReadOnlyMemory<byte> _memory;
    readonly Action<string>? _assertionFailed;
    readonly Action? _disposeAction;
    ReadOnlyMemory<byte> _current;

    /// <summary>
    /// Create a new MemoryReaderSerdes based on a BinaryReader
    /// </summary>
    /// <param name="memory">The memory use for reading</param>
    /// <param name="assertionFailed">An optional callback to be invoked when an assertion failure occurs</param>
    /// <param name="disposeAction">An optional callback to be invoked when the <see cref="MemoryReaderSerdes"/> is disposed</param>
    public MemoryReaderSerdes(
        ReadOnlyMemory<byte> memory,
        Action<string>? assertionFailed = null,
        Action? disposeAction = null
    )
    {
        _memory = memory;
        _current = memory;
        _assertionFailed = assertionFailed;
        _disposeAction = disposeAction;
    }

    /// <inheritdoc />
    public SerializerFlags Flags => SerializerFlags.Read;

    /// <inheritdoc />
    public long BytesRemaining => _current.Length;

    /// <inheritdoc />
    public void Comment(string msg, bool inline) { }

    /// <inheritdoc />
    public void Begin(SerdesName name = default) { }

    /// <inheritdoc />
    public void End() { }

    /// <inheritdoc />
    public void NewLine() { }

    /// <inheritdoc />
    public long Offset => _memory.Length - _current.Length;

    /// <inheritdoc />
    public void Seek(long newOffset) => _current = _memory[(int)newOffset..];

    /// <inheritdoc />
    public void Pad(int count, byte value) => Pad(null, count, value);

    /// <inheritdoc />
    public void Pad(SerdesName name, int count, byte value)
    {
        var bytes = _current.Span[..count];
        _current = _current[count..];

        foreach (var b in bytes)
            if (b != value)
                Assert(false, $"Unexpected value \"{b}\" found in repeating byte pattern (expected {value}");
    }

    /// <inheritdoc />
    public sbyte Int8(SerdesName name, sbyte value)
    {
        sbyte v = MemoryMarshal.Read<sbyte>(_current.Span);
        _current = _current[1..];
        return v;
    }

    /// <inheritdoc />
    public short Int16(SerdesName name, short value)
    {
        short v = MemoryMarshal.Read<short>(_current.Span);
        _current = _current[2..];
        return v;
    }

    /// <inheritdoc />
    public int Int32(SerdesName name, int value)
    {
        int v = MemoryMarshal.Read<int>(_current.Span);
        _current = _current[4..];
        return v;
    }

    /// <inheritdoc />
    public long Int64(SerdesName name, long value)
    {
        long v = MemoryMarshal.Read<long>(_current.Span);
        _current = _current[8..];
        return v;
    }

    /// <inheritdoc />
    public byte UInt8(SerdesName name, byte value)
    {
        byte v = MemoryMarshal.Read<byte>(_current.Span);
        _current = _current[1..];
        return v;
    }

    /// <inheritdoc />
    public ushort UInt16(SerdesName name, ushort value)
    {
        ushort v = MemoryMarshal.Read<ushort>(_current.Span);
        _current = _current[2..];
        return v;
    }

    /// <inheritdoc />
    public uint UInt32(SerdesName name, uint value)
    {
        uint v = MemoryMarshal.Read<uint>(_current.Span);
        _current = _current[4..];
        return v;
    }

    /// <inheritdoc />
    public ulong UInt64(SerdesName name, ulong value)
    {
        ulong v = MemoryMarshal.Read<ulong>(_current.Span);
        _current = _current[8..];
        return v;
    }

    /// <inheritdoc />
    public Guid Guid(SerdesName name, Guid value)
    {
        var v = new Guid(_current.Span[..16]);
        _current = _current[16..];
        return v;
    }

    /// <inheritdoc />
    public byte[] Bytes(SerdesName name, byte[] value, int n)
    {
        if (n > _current.Length)
            throw new ArgumentOutOfRangeException(
                nameof(n),
                $"Not enough data to read {n} bytes (only {_current.Length} available)"
            );

        var v = _current.Span[..n].ToArray();
        _current = _current[n..];
        return v;
    }

    void ISerdes.Assert(bool condition, string message) => Assert(condition, message);

    void Assert(
        bool condition,
        string? message = null,
        [CallerMemberName] string function = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        if (condition)
            return;

        var formatted = $"Assertion failed: {message} at {function} in {file}:{line}";
        _assertionFailed?.Invoke(formatted);
    }

    /// <summary>
    /// The actual implementation of the Dispose method.
    /// </summary>
    void Dispose(bool disposing)
    {
        if (disposing)
            _disposeAction?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose() => Dispose(true);
}
