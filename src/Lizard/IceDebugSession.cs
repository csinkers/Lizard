using System.Globalization;
using Lizard.Gui.Windows.Watch;
using LizardProtocol;

namespace Lizard;

public sealed class IceDebugSession : IDebugSession, IMemoryReader
{
    static readonly ITracer Log = new LogTopic("IceSession");
    readonly RequestQueue _queue = new();
    readonly CancellationTokenSource _tokenSource = new();
    readonly Thread _queueThread;
    readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(300);
    DateTime _lastVersionBump = DateTime.MinValue;

    readonly Ice.Communicator _communicator;
    readonly DebugHostPrx _debugHost;
    readonly DebugClientI _client;
    Registers _registers;
    int _version;

    public IMemoryCache Memory { get; }
    public Registers OldRegisters { get; private set; }
    public Registers Registers
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

        var addr = new Address(_registers.ds, (int)offset);
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
        var properties = Ice.Util.createProperties();
        properties.setProperty("Ice.MessageSizeMax", (2 * 1024 * 1024).ToString(CultureInfo.InvariantCulture));

        var initData = new Ice.InitializationData { properties = properties };
        _communicator = Ice.Util.initialize(initData);

        Ice.ObjectPrx? proxy = _communicator
            .stringToProxy($"DebugHost:default -h {hostname} -p {port}")
            .ice_twoway()
            .ice_secure(false);

        _debugHost = DebugHostPrxHelper.uncheckedCast(proxy);

        if (_debugHost == null)
            throw new ApplicationException("Invalid proxy");

        var adapter = _communicator.createObjectAdapterWithEndpoints("Callback.Client", $"default -h {hostname}");
        _client = new DebugClientI();
        adapter.add(_client, Ice.Util.stringToIdentity("debugClient"));
        adapter.activate();

        var clientProxy = DebugClientPrxHelper.uncheckedCast(
            adapter.createProxy(Ice.Util.stringToIdentity("debugClient"))
        );

        if (clientProxy == null)
            throw new ApplicationException("Could not build client");

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

    void OnStopped(Registers state)
    {
        Update(state);
        Stopped?.Invoke(state);
    }

    Registers Update(Registers state)
    {
        IsPaused = state.stopped;
        if (Registers.eip != state.eip)
            Version++;

        Registers = state;
        return state;
    }

    public void Dispose()
    {
        _client.StoppedEvent -= OnStopped;
        _communicator.Dispose();
        _tokenSource.Cancel();
        _queueThread.Join();
        Disconnected?.Invoke();
    }

    public void Continue()
    {
        _debugHost.Continue();
        IsPaused = false;
    }

    public void SetRegister(Register reg, int value) => _debugHost.SetRegister(reg, value);

    public Registers Break() => Update(_debugHost.Break());

    public Registers StepIn() => Update(_debugHost.StepIn());

    public Registers StepOver() => Update(_debugHost.StepOver());

    public Registers StepOut() => _registers; // TODO

    public Registers StepMultiple(int i) => Update(_debugHost.StepMultiple(i));

    public void RunToAddress(Address address) => _debugHost.RunToAddress(address);

    public Registers GetState() => Update(_debugHost.GetState());

    public AssemblyLine[] Disassemble(Address address, int length) => _debugHost.Disassemble(address, length);

    public byte[] GetMemory(Address addr, int bufferLength) => _debugHost.GetMemory(addr, bufferLength);

    public void SetMemory(Address address, byte[] bytes) => _debugHost.SetMemory(address, bytes);

    public int GetMaxNonEmptyAddress(short segment) => _debugHost.GetMaxNonEmptyAddress(segment);

    public IEnumerable<Address> SearchMemory(Address address, int length, byte[] toArray, int advance) =>
        _debugHost.SearchMemory(address, length, toArray, advance);

    public Breakpoint[] ListBreakpoints() => _debugHost.ListBreakpoints();

    public void SetBreakpoint(Breakpoint bp)
    {
        _debugHost.SetBreakpoint(bp);
        Version++;
    }

    public void EnableBreakpoint(int id, bool enable)
    {
        _debugHost.EnableBreakpoint(id, enable);
        Version++;
    }

    public void DelBreakpoint(int id)
    {
        _debugHost.DelBreakpoint(id);
        Version++;
    }

    public void SetReg(Register reg, int value)
    {
        _debugHost.SetRegister(reg, value);
        Version++;
    }

    public Descriptor[] GetGdt() => _debugHost.GetGdt();

    public Descriptor[] GetLdt() => _debugHost.GetLdt();
}
