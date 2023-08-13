using GhidraProgramData;
using LizardProtocol;

namespace Lizard;

public delegate void StoppedDelegate(Registers state);

public class Debugger : IMemoryReader
{
    readonly ProgramDataManager _programDataManager;
    readonly RequestQueue _queue = new();
    readonly CancellationTokenSource _tokenSource = new();
    readonly Thread _queueThread;
    readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(300);
    DateTime _lastVersionBump = DateTime.MinValue;
    Registers _registers;
    int _version;

    public event Action? ExitRequested;
    public ITracer Log { get; }
    public IceSessionManager SessionManager { get; }
    public IMemoryCache Memory { get; }
    public Registers OldRegisters { get; private set; }
    public Registers Registers
    {
        get => _registers;
        private set { OldRegisters = _registers; _registers = value; }
    }

    public int Version
    {
        get => _version;
        private set
        {
            _version = value;
            Memory.Dirty();
        }
    }

    public bool IsPaused { get; private set; }
    DebugHostPrx? Host => SessionManager.Host;
    public bool IsConnected => Host != null;

    public Debugger(IceSessionManager iceManager, ITracer log, IMemoryCache memory, ProgramDataManager programDataManager)
    {
        _programDataManager = programDataManager ?? throw new ArgumentNullException(nameof(programDataManager));
        SessionManager = iceManager ?? throw new ArgumentNullException(nameof(iceManager));
        Log = log ?? throw new ArgumentNullException(nameof(log));
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        Memory.Reader = this;
        SessionManager.Connected += OnSessionManagerConnected;
        SessionManager.Stopped += state => Update(state);

        _queueThread = new Thread(QueueThreadMethod) { Name = "Request Queue"};
        _queueThread.Start();
    }

    void QueueThreadMethod()
    {
        try
        {
            while (!_tokenSource.Token.WaitHandle.WaitOne(20))
                _queue.ProcessPendingRequests(SessionManager, Version, _tokenSource.Token);
        }
        catch (OperationCanceledException) { }
    }

    void OnSessionManagerConnected()
    {
        Memory.Clear();
        if (Host != null)
        {
            var state = Host.GetState();
            Update(state);
            // Do a second update so the old registers will match the current ones and it won't show everything in red on connect
            Update(state);
        }
    }

    public Symbol? TryFindSymbol(uint offset) => _programDataManager.LookupSymbol(offset);
    public Symbol? TryFindSymbol(string name) => _programDataManager.LookupSymbol(name);
    public (uint MemoryOffset, MemoryRegion Region)? ToMemory(uint fileOffset) => _programDataManager.Mapping.ToMemory(fileOffset);
    public (uint FileOffset, MemoryRegion Region)? ToFile(uint memoryOffset) => _programDataManager.Mapping.ToFile(memoryOffset);

    public void Defer(IRequest request) => _queue.Add(request);
    public void FlushDeferredResults() => _queue.ApplyResults();

    public void Refresh()
    {
        if (IsPaused)
            return;

        var now = DateTime.UtcNow;
        if (now > _lastVersionBump + _refreshInterval)
        {
            Version++;
            _lastVersionBump = now;
        }
    }

    public Registers Update(Registers state)
    {
        IsPaused = state.stopped;
        if (Registers.eip != state.eip)
            Version++;

        Registers = state;
        return state;
    }

    public void Read(uint offset, byte[] buffer)
    {
        var host = Host;
        if (host == null)
            return;

        var addr = new Address(_registers.ds, (int)offset);
        var result = host.GetMemory(addr, buffer.Length);
        result.CopyTo(buffer, 0);
    }

    public void Exit() => ExitRequested?.Invoke();

    public void Continue()
    {
        Host?.Continue();
        IsPaused = false;
    }

    public Registers Break() => Host == null ? _registers : Update(Host.Break());
    public Registers StepIn() => Host == null ? _registers : Update(Host.StepIn());
    public Registers StepOver() => Host == null ? _registers : Update(Host.StepOver());
    public Registers StepOut() => _registers; // TODO
    public Registers StepMultiple(int i) => Host == null ? _registers : Update(Host.StepMultiple(i));
    public void RunToAddress(Address address) => Host?.RunToAddress(address);
    public Registers GetState() => Host == null ? _registers : Update(Host.GetState());
    public AssemblyLine[] Disassemble(Address address, int length) => Host?.Disassemble(address, length) ?? Array.Empty<AssemblyLine>();
    public byte[] GetMemory(Address address, int length) => Host?.GetMemory(address, length) ?? Array.Empty<byte>();
    public void SetMemory(Address address, byte[] bytes) => Host?.SetMemory(address, bytes);
    public int GetMaxNonEmptyAddress(short segment) => Host?.GetMaxNonEmptyAddress(segment) ?? 0;
    public IEnumerable<Address> SearchMemory(Address address, int length, byte[] toArray, int advance)
        => Host?.SearchMemory(address, length, toArray, advance) ?? Enumerable.Empty<Address>();
    public Breakpoint[] ListBreakpoints() => Host?.ListBreakpoints() ?? Array.Empty<Breakpoint>();
    public void SetBreakpoint(Breakpoint bp) { Host?.SetBreakpoint(bp); Version++; }
    public void EnableBreakpoint(int id, bool enable) { Host?.EnableBreakpoint(id, enable); Version++; }
    public void DelBreakpoint(int id) { Host?.DelBreakpoint(id); Version++; }
    public void SetReg(Register reg, int value) { Host?.SetRegister(reg, value); Version++; } 
    public Descriptor[] GetGdt() => Host?.GetGdt() ?? Array.Empty<Descriptor>();
    public Descriptor[] GetLdt() => Host?.GetLdt() ?? Array.Empty<Descriptor>();
    public void Dispose()
    {
        _tokenSource.Cancel();
        _queueThread.Join();
    }
}

