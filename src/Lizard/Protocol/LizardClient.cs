using System.Net.Sockets;
using Lizard.Protocol.ProtocolGen;

namespace Lizard.Protocol;

public class LizardClient : ILizardClient1, IDisposable
{
    const int MaxVersion = 1;
    readonly TcpClient _tcpClient;
    readonly Duplex _duplex;
    public event Action<LRegisters1>? Stopped;

    public static async Task<LizardClient> StartAsync(ILogger log, string host, int port, CancellationToken ct)
    {
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(host, port, ct);
        int version = await LizardHandshake.ClientHandshake(tcpClient, MaxVersion);
        log.LogInformation("Connected as version {version}", version);
        return new LizardClient(log, tcpClient, version, ct);
    }

    LizardClient(ILogger log, TcpClient tcpClient, int version, CancellationToken ct)
    {
        if (version != 1)
            throw new NotSupportedException($"Protocol version {version} not supported");

        _tcpClient = tcpClient;

        var deserializer = new LizardClient1Deserializer(this);
        _duplex = new Duplex(log, 10, tcpClient, deserializer.HandleMessage, ct);
        Serializer = new Lizard1Serializer(_duplex.Send);
    }

    public Lizard1Serializer Serializer { get; }
    public Task Complete => _duplex.Task;

    void ILizardClient1.Stopped(LRegisters1 state) => Stopped?.Invoke(state);

    public void Dispose()
    {
        _tcpClient.Dispose();
    }
}
