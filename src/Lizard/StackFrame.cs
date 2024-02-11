namespace Lizard;

public class StackFrame
{
    public StackFrame(uint bp) => BasePointer = bp;
    public uint BasePointer { get; }
    public List<StackFunction> Functions { get; } = new();
    public List<uint> Parameters { get; set; } = new();
    public List<uint> Locals { get; set; } = new();
}