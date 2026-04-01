using Lizard.Comms;
using Lizard.Gui.Windows.Watch;
using Lizard.Memory;
using Lizard.Util;

namespace Lizard.Session;

public sealed class NetDebugSession : IDebugSession, IMemoryReader, ILizardClient1
{
    readonly RequestQueue _queue = new();
    readonly CancellationTokenSource _tokenSource;
    readonly Thread _queueThread;
    readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(300);
    DateTime _lastVersionBump = DateTime.MinValue;

    readonly ILizard1 _serializer;
    readonly LizardClient _client;
    LRegisters1 _registers = new();
    int _version;

    public IMemoryCache Memory { get; }
    public LRegisters1 OldRegisters { get; private set; } = new();
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
        {
            throw new InvalidOperationException(
                $"Tried to retrieve {size} bytes, but the supplied buffer can only hold {buffer.Length}"
            );
        }

        var addr = new LAddress1 { Segment = _registers.Ds, Offset = offset };
        var result = GetMemory(addr, size);
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

    public static NetDebugSession Start(string hostname, int port)
    {
        var log = new Logger<LizardClient>();
        var cts = new CancellationTokenSource();
        var client = LizardClient.Start(log, hostname, port, cts.Token);
        return new NetDebugSession(client, cts);
    }

    NetDebugSession(LizardClient client, CancellationTokenSource cts)
    {
        _client = client;
        _tokenSource = cts;
        Memory = new MemoryCache(this);

        _serializer = client.Serializer;
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
        _tokenSource.Cancel();
        _queueThread.Join();
        Disconnected?.Invoke();
        _client.Dispose();
    }

    public void Continue()
    {
        _serializer.Continue();
        IsPaused = false;
    }

    public void SetRegister(LRegister1 reg, uint value) => _serializer.SetRegister(reg, value);

    public LRegisters1 Break() => Update(_serializer.Break());

    public LRegisters1 StepIn() => Update(_serializer.StepIn());

    public LRegisters1 StepOver() => Update(_serializer.StepOver());

    public LRegisters1 StepOut() => _registers; // TODO

    public LRegisters1 StepMultiple(uint i) => Update(_serializer.StepMultiple(i));

    public void RunToAddress(LAddress1 address) => _serializer.RunToAddress(address);

    public LRegisters1 GetState() => Update(_serializer.GetState());

    public List<LAssemblyLine1> Disassemble(LAddress1 address, uint length) => _serializer.Disassemble(address, length);

    public byte[] GetMemory(LAddress1 addr, uint bufferLength) => _serializer.GetMemory(addr, bufferLength);

    public void SetMemory(LAddress1 address, byte[] bytes) => _serializer.SetMemory(address, bytes);

    public uint GetMaxNonEmptyAddress(ushort segment) => _serializer.GetMaxNonEmptyAddress(segment);

    public IEnumerable<LAddress1> SearchMemory(LAddress1 address, uint length, byte[] toArray, uint advance) =>
        _serializer.SearchMemory(address, length, toArray, advance);

    public List<LBreakpoint1> ListBreakpoints() => _serializer.ListBreakpoints();

    public void SetBreakpoint(LBreakpoint1 bp)
    {
        _serializer.SetBreakpoint(bp);
        Version++;
    }

    public void EnableBreakpoint(uint id, bool enable)
    {
        _serializer.EnableBreakpoint(id, enable);
        Version++;
    }

    public void DeleteBreakpoint(uint id)
    {
        _serializer.DeleteBreakpoint(id);
        Version++;
    }

    public void SetReg(LRegister1 reg, uint value)
    {
        _serializer.SetRegister(reg, value);
        Version++;
    }

    public List<LDescriptor1> GetGdt() => _serializer.GetGdt();

    public List<LDescriptor1> GetLdt() => _serializer.GetLdt();

    void ILizardClient1.Stopped(LRegisters1 state)
    {
        Update(state);
        Stopped?.Invoke(state);
    }
}
