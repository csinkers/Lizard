namespace Lizard.Core.Unwind;

public interface IStackUnwinder
{
    StackFrame? TryUnwindFrame(UnwinderContext context, StackFrame currentFrame);
}
