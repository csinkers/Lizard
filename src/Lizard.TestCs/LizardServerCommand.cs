using System.Collections.Concurrent;
using CsConsole;
using Lizard.Comms;
using Microsoft.Extensions.Logging;

namespace Lizard.TestCs;

internal class LizardServerCommand : IAsyncCommand
{
    public string[] Names { get; } = ["server", "s"];
    public string? Description => "Start a lizard server";
    public string? ShortDescription => Description;
    public string? Usage => "[port]";

    public async Task InvokeAsync(ArgumentSource args, IConsoleOutput o, CancellationToken ct)
    {
        string? portString = args.Optional();
        if (string.IsNullOrEmpty(portString))
            portString = "9189";

        if (!ushort.TryParse(portString, out var port))
            throw new ConsoleCommandException("Invalid port");

        var log = new ConsoleLogger(o);
        var state = new ServerState(log, port, o);
        Task serverTask = state.Server.Run();

        var parser = new CommandParser<ServerState>();
        parser.Add(new HelpCommand(parser));
        parser.Add(new SyncCommand<ServerState>(["q", "exit"], static (_, _, st) => st.Stop()));
        parser.Add(
            new SyncCommand<ServerState>(
                ["break", "b"],
                static (_, o, st) =>
                {
                    st.Serializer.Stopped(st.Registers);
                }
            )
        );

        parser.Add(
            new SyncCommand<ServerState>(
                ["pending", "p"],
                static (a, o, st) =>
                {
                    foreach (var kvp in st.PendingOperations)
                    {
                        o.WriteLine($"  {kvp.Key}: {kvp.Value.Name}");
                    }
                }
            )
        );
        parser.Add(
            new SyncCommand<ServerState>(
                ["respond", "r"],
                static (a, o, st) =>
                {
                    int id = a.Int("id");
                    var op = st.PendingOperations.Remove(id, out var pending) ? pending : null;
                    if (op == null)
                    {
                        o.WriteLine($"No pending operation {id}");
                        return;
                    }

                    op.Tcs.SetResult(null);
                }
            )
        );

        ConsoleLoop<ServerState> loop = new(parser, state);
        var i = new ConsoleInput();
        await loop.RunMain(i, o, true);
        await serverTask;
    }

    class PendingOperation(string name)
    {
        public TaskCompletionSource<object?> Tcs { get; } = new();
        public string Name => name;
    }

    class ServerState : ICommandState, ILizard1
    {
        readonly CancellationTokenSource _cts = new();
        int _nextId;
        readonly IConsoleOutput _output;

        public ServerState(ILogger log, ushort port, IConsoleOutput output)
        {
            _output = output;
            Server = new LizardServer(log, port, this, _cts.Token);
            Serializer = Server.Serializer;
        }

        public bool Done => _cts.IsCancellationRequested;
        public LizardServer Server { get; }
        public LizardClient1Serializer Serializer { get; }
        public LRegisters1 Registers { get; } = new();

        public ConcurrentDictionary<int, PendingOperation> PendingOperations { get; } = new();

        public void Stop() => _cts.Cancel();

        public void Continue()
        {
            PendingOperation op = AddPending("Continue");
            op.Tcs.Task.Wait(_cts.Token);
        }

        PendingOperation AddPending(string name)
        {
            int n = Interlocked.Increment(ref _nextId);
            _output.WriteLine($"Received {name} as pending operation {n}");
            var op = new PendingOperation(name);
            PendingOperations.AddOrUpdate(
                n,
                op,
                (_, _) => throw new InvalidOperationException($"Operation {n} already added!")
            );
            return op;
        }

        public LRegisters1 Break()
        {
            throw new NotImplementedException();
        }

        public LRegisters1 StepIn()
        {
            throw new NotImplementedException();
        }

        public LRegisters1 StepOver()
        {
            throw new NotImplementedException();
        }

        public LRegisters1 StepOut()
        {
            throw new NotImplementedException();
        }

        public LRegisters1 StepMultiple(uint cycles)
        {
            throw new NotImplementedException();
        }

        public void RunToAddress(LAddress1 address)
        {
            throw new NotImplementedException();
        }

        public LRegisters1 GetState()
        {
            throw new NotImplementedException();
        }

        public uint GetMaxNonEmptyAddress(ushort seg)
        {
            throw new NotImplementedException();
        }

        public List<LAddress1> SearchMemory(LAddress1 start, uint length, byte[] pattern, uint advance)
        {
            throw new NotImplementedException();
        }

        public List<LAssemblyLine1> Disassemble(LAddress1 address, uint length)
        {
            throw new NotImplementedException();
        }

        public byte[] GetMemory(LAddress1 address, uint length)
        {
            throw new NotImplementedException();
        }

        public void SetMemory(LAddress1 address, byte[] bytes)
        {
            throw new NotImplementedException();
        }

        public List<LBreakpoint1> ListBreakpoints()
        {
            throw new NotImplementedException();
        }

        public void SetBreakpoint(LBreakpoint1 breakpoint)
        {
            throw new NotImplementedException();
        }

        public void EnableBreakpoint(uint id, bool isEnabled)
        {
            throw new NotImplementedException();
        }

        public void DeleteBreakpoint(uint id)
        {
            throw new NotImplementedException();
        }

        public void SetRegister(LRegister1 reg, uint value)
        {
            throw new NotImplementedException();
        }

        public List<LDescriptor1> GetGdt()
        {
            throw new NotImplementedException();
        }

        public List<LDescriptor1> GetLdt()
        {
            throw new NotImplementedException();
        }
    }
}
