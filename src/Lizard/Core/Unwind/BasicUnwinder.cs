using GhidraProgramData.Types;

namespace Lizard.Core.Unwind;

public class BasicUnwinder : IStackUnwinder
{
    public StackFrame? TryUnwindFrame(UnwinderContext context, StackFrame currentFrame)
    {
        uint prevBp = currentFrame.BasePointer;
        var stackMemory = context.StackMemory;
        if (prevBp == 0 || prevBp + 4 > stackMemory.Max)
            return null;

        uint thisBp = stackMemory.GetDword(prevBp);
        uint nextBp = stackMemory.GetDword(thisBp);
        var result = new StackFrame(thisBp);

        for (uint addr = thisBp; addr < nextBp; addr += 4)
        {
            var value = stackMemory.GetDword(addr);
            var symbol = context.CommandContext.LookupSymbolForAddress(value, out var offset);
            if (symbol?.Context is GFunction)
            {
                result.Function = new StackFunction(symbol, value, offset);
                return result;
            }
        }

        return result;
    }
}
