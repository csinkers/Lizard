using Lizard.generated;

namespace Lizard;

public interface IRequest
{
    int Version { get; }
    void Execute(DebugHostPrx host);
    void Complete();
}