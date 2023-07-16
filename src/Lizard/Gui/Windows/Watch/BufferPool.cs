namespace Lizard.Gui.Windows.Watch;

public class BufferPool
{
    const int FlushIntervalSeconds = 30;
    readonly Dictionary<int, Stack<byte[]>> _buffers = new();
    DateTime _lastFlush;

    Stack<byte[]> GetStack(int size)
    {
        if (_buffers.TryGetValue(size, out var stack))
            return stack;

        stack = new Stack<byte[]>();
        _buffers[size] = stack;
        return stack;
    }

    public byte[] Borrow(int size) =>
        GetStack(size).TryPop(out var buffer)
            ? buffer
            : new byte[size];

    public void Return(byte[] buffer)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        GetStack(buffer.Length).Push(buffer);
    }

    public void Flush()
    {
        if (!((DateTime.UtcNow - _lastFlush).TotalSeconds > FlushIntervalSeconds))
            return;

        _buffers.Clear();
        _lastFlush = DateTime.UtcNow;
    }
}