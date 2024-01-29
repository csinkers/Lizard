namespace Lizard;

public interface IRequest
{
    int Version { get; }
    void Execute(IDebugSession target);
    void Complete();
}