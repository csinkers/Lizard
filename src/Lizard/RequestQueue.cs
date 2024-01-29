using System.Collections.Concurrent;

namespace Lizard;

public class RequestQueue
{
    readonly object _syncRoot = new();
    readonly BlockingCollection<IRequest> _pending = new();
    readonly Queue<IRequest> _completed = new();

    public void Add(IRequest action)
    {
        lock (_syncRoot)
            _pending.Add(action);
    }

    public void ProcessPendingRequests(IDebugSession session, int version, CancellationToken token)
    {
        for (; ; )
        {
            var request = _pending.Take(token);
            if (request.Version < version)
                continue;

            request.Execute(session);

            lock (_syncRoot)
                _completed.Enqueue(request);
        }
    }

    public void ApplyResults()
    {
        for (; ; )
        {
            IRequest? request;
            lock (_syncRoot)
                if (!_completed.TryDequeue(out request))
                    break;

            request.Complete();
        }
    }
}