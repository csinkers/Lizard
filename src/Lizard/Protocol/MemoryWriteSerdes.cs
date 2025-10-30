using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SerdesNet;

namespace Lizard.Protocol;

public sealed class MemoryWriterSerdes : ISerdes
{
    const int MaxSize = 0x78000000;
    readonly Action<string>? _assertionFailed;
    readonly Action? _disposeAction;
    byte[] _buffer;
    int _position;
    int _length;
    int _capacity;

    public MemoryWriterSerdes(int capacity = 0, Action<string>? assertionFailed = null, Action? disposeAction = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity, nameof(capacity));
        _buffer = capacity != 0 ? new byte[capacity] : [];
        _capacity = capacity;
        _assertionFailed = assertionFailed;
        _disposeAction = disposeAction;
    }

    public ReadOnlyMemory<byte> GetMemory() => new(_buffer, 0, _length);

    /// <inheritdoc />
    public long Offset => _position;

    /// <inheritdoc />
    public SerializerFlags Flags => SerializerFlags.Write;

    /// <inheritdoc />
    public long BytesRemaining => int.MaxValue;

    /// <inheritdoc />
    public void Comment(string msg, bool inline) { }

    /// <inheritdoc />
    public void Begin(SerdesName name = default) { }

    /// <inheritdoc />
    public void End() { }

    /// <inheritdoc />
    public void NewLine() { }

    /// <inheritdoc />

    /// <inheritdoc />
    public void Seek(long newOffset)
    {
        if (newOffset > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(newOffset), "Tried to seek past the max offset");
        if (newOffset < 0)
            throw new IOException("Tried to seek to a position before the start");
        _position = (int)newOffset;
    }

    /// <inheritdoc />
    public void Pad(int bytes, byte value) => Pad(null, bytes, value);

    /// <inheritdoc />
    public void Pad(SerdesName name, int count, byte value)
    {
        Extend(count);
        for (int i = 0; i < count; i++)
            _buffer[_position++] = value;
    }

    /// <inheritdoc />
    public sbyte Int8(SerdesName name, sbyte value)
    {
        Extend(1);
        _buffer[_position++] = (byte)value;
        return value;
    }

    /// <inheritdoc />
    public short Int16(SerdesName name, short value)
    {
        Span<byte> span = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(span, value);
        Write(span);
        return value;
    }

    /// <inheritdoc />
    public int Int32(SerdesName name, int value)
    {
        Span<byte> span = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(span, value);
        Write(span);
        return value;
    }

    /// <inheritdoc />
    public long Int64(SerdesName name, long value)
    {
        Span<byte> span = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(span, value);
        Write(span);
        return value;
    }

    /// <inheritdoc />
    public byte UInt8(SerdesName name, byte value)
    {
        Extend(1);
        _buffer[_position++] = value;
        return value;
    }

    /// <inheritdoc />
    public ushort UInt16(SerdesName name, ushort value)
    {
        Span<byte> span = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(span, value);
        Write(span);
        return value;
    }

    /// <inheritdoc />
    public uint UInt32(SerdesName name, uint value)
    {
        Span<byte> span = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        Write(span);
        return value;
    }

    /// <inheritdoc />
    public ulong UInt64(SerdesName name, ulong value)
    {
        Span<byte> span = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        Write(span);
        return value;
    }

    /// <inheritdoc />
    public Guid Guid(SerdesName name, Guid value)
    {
        Write(value.ToByteArray());
        return value;
    }

    /// <inheritdoc />
    public byte[] Bytes(SerdesName name, byte[] value, int n)
    {
        if (value is { Length: > 0 })
            Write(value.AsSpan(0, n));

        return value;
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
    /// The actual implementation of <see cref="IDisposable.Dispose"/>
    /// </summary>
    /// <param name="disposing"></param>
    void Dispose(bool disposing)
    {
        if (disposing)
            _disposeAction?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose() => Dispose(true);

    void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length == 0)
            return;

        Extend(buffer.Length);

        buffer.CopyTo(new Span<byte>(_buffer, _position, buffer.Length));
        _position += buffer.Length;
    }

    void Extend(int length)
    {
        int finalPosition = _position + length;
        if (finalPosition < 0)
            throw new IOException("Tried to write too much data to a MemoryWriterSerdes");

        if (finalPosition > _length)
        {
            bool clearRequired = _position > _length;

            if (finalPosition > _capacity && EnsureCapacity(finalPosition))
                clearRequired = false;

            if (clearRequired)
                Array.Clear(_buffer, _length, finalPosition - _length);

            _length = finalPosition;
        }
    }

    bool EnsureCapacity(int value)
    {
        if (value < 0)
            throw new IOException("Tried to set capacity to a negative value");

        if (value <= _capacity)
            return false;

        int newCapacity = Math.Max(value, 256);
        if (newCapacity < _capacity * 2)
            newCapacity = _capacity * 2;

        if ((uint)(_capacity * 2) > MaxSize)
            newCapacity = Math.Max(value, MaxSize);

        if (newCapacity < _length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newCapacity),
                $"Tried to set the capacity to a value ({newCapacity}) too small to hold the current content ({_length} bytes)"
            );
        }

        if (newCapacity == _capacity)
            return true;

        if (newCapacity <= 0)
        {
            _buffer = [];
            _capacity = newCapacity;
            return true;
        }

        byte[] newBuffer = new byte[newCapacity];
        if (_length > 0)
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _length);

        _buffer = newBuffer;
        _capacity = newCapacity;

        return true;
    }
}
