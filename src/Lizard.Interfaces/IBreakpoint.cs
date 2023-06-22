namespace Lizard.Interfaces;

public interface IBreakpoint
{
    public ushort Segment { get; }
    public uint Address { get; }
    public bool IsEnabled { get; }
}