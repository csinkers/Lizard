namespace Lizard;

public interface IRequest
{
    int Version { get; }
    void Execute(IDebugTarget target);
    void Complete();
}