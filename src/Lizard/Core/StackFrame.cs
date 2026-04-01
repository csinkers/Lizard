namespace Lizard.Core;

public class StackFrame
{
    public StackFrame(uint bp) => BasePointer = bp;

    public uint BasePointer { get; }
    public StackFunction? Function { get; set; }
    public List<uint> Parameters { get; set; } = [];
    public List<uint> Locals { get; set; } = [];
}
