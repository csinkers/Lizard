using Lizard.Gui.Windows.Watch;
using Lizard.Memory;
using Lizard.Protocol.ProtocolGen;
using Lizard.Util;

namespace Lizard.Session.IceClient;

public sealed class IceDebugSession : IDebugSession, IMemoryReader
{
    static readonly ITracer Log = new LogTopic("IceSession");
    readonly RequestQueue _queue = new();
    readonly CancellationTokenSource _tokenSource = new();
    readonly Thread _queueThread;
    readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(300);
    DateTime _lastVersionBump = DateTime.MinValue;

    readonly DebugClientI _client;
    LRegisters1 _registers;
    int _version;

    public IMemoryCache Memory { get; }
    public LRegisters1 OldRegisters { get; private set; }
    public LRegisters1 Registers
    {
        get => _registers;
        private set
        {
            OldRegisters = _registers;
            _registers = value;
        }
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

    public void Read(uint offset, uint size, Span<byte> buffer)
    {
        if (size > buffer.Length)
            throw new InvalidOperationException(
                $"Tried to retrieve {size} bytes, but the supplied buffer can only hold {buffer.Length}"
            );

        var addr = new LAddress1(_registers.Ds, offset);
        var result = GetMemory(addr, (int)size);
        result.CopyTo(buffer);
    }

    public event Action? Disconnected;
    public event StoppedDelegate? Stopped;
    public bool CanRun => true;
    public bool IsPaused { get; private set; }
    public bool IsActive => true;

    /*
    public IceDebugSession(ProjectManager projectManager, bool autoConnect)
    {
        projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        projectManager.ProjectLoaded += _ => Disconnect();

        if (autoConnect)
        {
            var project = projectManager.Project;
            var hostname = project.GetProperty(ConnectWindow.HostProperty)!;
            var port = project.GetProperty(ConnectWindow.PortProperty);
            Connect(hostname, port);
        }
    }
    */

    public IceDebugSession(string hostname, int port)
    {
        Memory = new MemoryCache(this);
        _client = new DebugClientI();
        _client.StoppedEvent += OnStopped;

        _queueThread = new Thread(QueueThreadMethod) { Name = "Request Queue" };
        _queueThread.Start();
    }

    void QueueThreadMethod()
    {
        try
        {
            while (!_tokenSource.Token.WaitHandle.WaitOne(20))
                _queue.ProcessPendingRequests(this, Version, _tokenSource.Token);
        }
        catch (OperationCanceledException) { }
    }

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

    void OnStopped(LRegisters1 state)
    {
        Update(state);
        Stopped?.Invoke(state);
    }

    LRegisters1 Update(LRegisters1 state)
    {
        IsPaused = state.IsStopped;
        if (Registers.Eip != state.Eip)
            Version++;

        Registers = state;
        return state;
    }

    public void Dispose()
    {
        _client.StoppedEvent -= OnStopped;
        _tokenSource.Cancel();
        _queueThread.Join();
        Disconnected?.Invoke();
    }

    public void Continue()
    {
        _debugHost.Continue();
        IsPaused = false;
    }

    public void SetRegister(LRegister1 reg, uint value) => _debugHost.SetRegister(reg, value);

    public LRegisters1 Break() => Update(_debugHost.Break());

    public LRegisters1 StepIn() => Update(_debugHost.StepIn());

    public LRegisters1 StepOver() => Update(_debugHost.StepOver());

    public LRegisters1 StepOut() => _registers; // TODO

    public LRegisters1 StepMultiple(uint i) => Update(_debugHost.StepMultiple(i));

    public void RunToAddress(LAddress1 address) => _debugHost.RunToAddress(address);

    public LRegisters1 GetState() => Update(_debugHost.GetState());

    public LAssemblyLine1[] Disassemble(LAddress1 address, uint length) => _debugHost.Disassemble(address, length);

    public byte[] GetMemory(LAddress1 addr, uint bufferLength) => _debugHost.GetMemory(addr, bufferLength);

    public void SetMemory(LAddress1 address, byte[] bytes) => _debugHost.SetMemory(address, bytes);

    public uint GetMaxNonEmptyAddress(ushort segment) => _debugHost.GetMaxNonEmptyAddress(segment);

    public IEnumerable<LAddress1> SearchMemory(LAddress1 address, uint length, byte[] toArray, uint advance) =>
        _debugHost.SearchMemory(address, length, toArray, advance);

    public LBreakpoint1[] ListBreakpoints() => _debugHost.ListBreakpoints();

    public void SetBreakpoint(LBreakpoint1 bp)
    {
        _debugHost.SetBreakpoint(bp);
        Version++;
    }

    public void EnableBreakpoint(uint id, bool enable)
    {
        _debugHost.EnableBreakpoint(id, enable);
        Version++;
    }

    public void DelBreakpoint(uint id)
    {
        _debugHost.DelBreakpoint(id);
        Version++;
    }

    public void SetReg(LRegister1 reg, uint value)
    {
        _debugHost.SetRegister(reg, value);
        Version++;
    }

    public LDescriptor1[] GetGdt() => _debugHost.GetGdt();

    public LDescriptor1[] GetLdt() => _debugHost.GetLdt();
}
