namespace Lizard;

public class Request<T> : IRequest
{
    public delegate T RequestQueueAction(IDebugSession session);
    public delegate void FinaliserAction(T result);

    public int Version { get; }
    readonly RequestQueueAction _action;
    readonly FinaliserAction _finaliser;
    T? _result;

    public Request(int version, RequestQueueAction action, FinaliserAction finaliser)
    {
        Version = version;
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _finaliser = finaliser ?? throw new ArgumentNullException(nameof(finaliser));
    }

    public void Execute(IDebugSession session) => _result = _action(session); // Runs on worker thread, can take a long time

    public void Complete() // To be run on main thread, must complete quickly
    {
        if (_result != null)
            _finaliser(_result);
    }
}
