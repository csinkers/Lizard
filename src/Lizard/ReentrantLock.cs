namespace Lizard;

public class ReentrantLock
{
    readonly object _syncRoot = new();
    bool _isLocked;

    public void Lock() // Will block until the lock can be acquired
    {
        lock (_syncRoot)
        {
            while (_isLocked)
                Monitor.Wait(_syncRoot);

            _isLocked = true;
        }
    }

    public bool TryLock() // If it's already locked return false immediately.
    {
        lock (_syncRoot)
        {
            if (_isLocked)
                return false;

            _isLocked = true;
            return true;
        }
    }

    public void Unlock()
    {
        lock (_syncRoot)
        {
            _isLocked = false;
            Monitor.Pulse(_syncRoot);
        }
    }
}
