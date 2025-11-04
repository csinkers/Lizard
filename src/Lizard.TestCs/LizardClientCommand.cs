using CsConsole;
using Lizard.Comms;

namespace Lizard.TestCs;

internal class LizardClientCommand : IAsyncCommand
{
    public string[] Names { get; } = ["client", "c"];
    public string Description => "Starts a lizard client";
    public string ShortDescription => Description;
    public string Usage => "<host> <port>";

    public async Task InvokeAsync(ArgumentSource args, IConsoleOutput o, CancellationToken ct)
    {
        string host = args.Optional() ?? "127.0.0.1";
        int port = int.Parse(args.Optional() ?? "9189");

        var log = new ConsoleLogger(o);
        var cts = new CancellationTokenSource();
        ct.Register(cts.Cancel);

        using var client = await LizardClient.StartAsync(log, host, port, cts.Token);
        client.Stopped += r =>
        {
            o.WriteLine("Target stopped");
            PrintRegisters(o, r);
        };

        var state = new ClientState(client, cts);
        var parser = new CommandParser<ClientState>();
        parser.Add(new HelpCommand(parser));
        parser.Add(new SyncCommand<ClientState>(["q", "exit"], static (_, _, st) => st.Stop()));
        parser.Add(
            new SyncCommand<ClientState>(["break", "b"], static (_, o, st) => PrintRegisters(o, st.Serializer.Break()))
        );
        parser.Add(
            new SyncCommand<ClientState>(
                ["g", "continue"],
                static (_, o, st) =>
                {
                    st.Serializer.Continue();
                    o.WriteLine("Continue complete");
                }
            )
        );

        ConsoleLoop<ClientState> loop = new(parser, state);
        var i = new ConsoleInput();
        await loop.RunMain(i, o, true);
    }

    static void PrintRegisters(IConsoleOutput o, LRegisters1 r)
    {
        o.WriteLine($"EAX:{r.Eax:x8} EBX:{r.Ebx:x8} ECX:{r.Ecx:x8} EDX:{r.Edx:x8}");
        o.WriteLine($"ESI:{r.Esi:x8} EDI:{r.Edi:x8} EBP:{r.Ebp:x8} ESI:{r.Esi:x8}");
        o.WriteLine($"EIP:{r.Eip:x8} CS:{r.Cs:x4} DS:{r.Ds:x4} SS:{r.Ss:x4} ES:{r.Es:x4} FS:{r.Fs:x4} GS:{r.Gs:x4}");
    }

    class ClientState : ICommandState
    {
        readonly LizardClient _client;
        readonly CancellationTokenSource _cts;

        public ClientState(LizardClient client, CancellationTokenSource cts)
        {
            _client = client;
            _cts = cts;
            _client.Complete.ContinueWith(_ => _cts.Cancel());
        }

        public Lizard1Serializer Serializer => _client.Serializer;
        public bool Done => _cts.IsCancellationRequested;

        public void Stop() => _cts.Cancel();
    }
}
