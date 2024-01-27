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

    public void ProcessPendingRequests(IDebugTargetProvider targetProvider, int version, CancellationToken token)
    {
        for(;;)
        {
            var request = _pending.Take(token);
            if (request.Version < version)
                continue;

            if (!targetProvider.TryLock()) // Make sure the session won't be recreated when we're about to call it
            {
                _pending.Add(request, token); // Re-enqueue
                continue;
            }

            try
            {
                var host = targetProvider.Host;
                if (host == null)
                    continue;

                request.Execute(host);
            }
            finally { targetProvider.Unlock(); }

            lock (_syncRoot)
                _completed.Enqueue(request);
        }
    }

    public void ApplyResults()
    {
        for(;;)
        {
            IRequest? request;
            lock (_syncRoot)
                if (!_completed.TryDequeue(out request))
                    break;

            request.Complete();
        }
    }
}