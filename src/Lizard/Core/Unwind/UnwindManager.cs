namespace Lizard.Core.Unwind;

public class UnwindManager
{
    readonly Lock _lock = new();
    readonly LinkedList<IStackUnwinder> _unwinders = [];

    public void AddUnwinder(IStackUnwinder unwinder)
    {
        lock (_lock)
            _unwinders.AddFirst(unwinder);
    }

    public void RemoveUnwinder(IStackUnwinder unwinder)
    {
        lock (_lock)
            _unwinders.Remove(unwinder);
    }

    public StackFrame? TryUnwindFrame(UnwinderContext context, StackFrame currentFrame)
    {
        lock (_lock)
        {
            foreach (var unwinder in _unwinders)
            {
                StackFrame? frame = unwinder.TryUnwindFrame(context, currentFrame);
                if (frame != null)
                    return frame;
            }

            return null;
        }
    }
}
