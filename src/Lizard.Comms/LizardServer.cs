using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Lizard.Comms;

public class LizardServer : IDisposable
{
    const int MaxVersion = 1;
    readonly ILogger _log;
    readonly CancellationToken _ct;
    readonly TcpListener _listener;
    readonly Lizard1Deserializer _deserializer;
    Duplex? _duplex;

    public LizardClient1Serializer Serializer { get; }

    public LizardServer(ILogger log, ushort port, ILizard1 receiver, CancellationToken ct)
    {
        _log = log;
        _ct = ct;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();

        _deserializer = new Lizard1Deserializer(receiver);
        Serializer = new LizardClient1Serializer(data =>
        {
            var duplex = _duplex;
            if (duplex == null)
                throw new InvalidOperationException("No client connected");

            return duplex.Send(data);
        });
    }

    public async Task Run()
    {
        _log.LogInformation("Lizard server started [{endpoint}]", _listener.LocalEndpoint);
        while (!_ct.IsCancellationRequested)
        {
            try
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(_ct).ConfigureAwait(false);
                _log.LogInformation("Client connecting from {address}", client.Client.RemoteEndPoint);
                var version = await LizardHandshake.ServerHandshakeAsync(client, MaxVersion, _ct);
                _log.LogInformation("Client connected as version {version}", version);
                client.NoDelay = true;

                if (version != 1)
                    throw new NotSupportedException($"Protocol version {version} not supported");

                _duplex = new Duplex(_log, 10, client, _deserializer.HandleMessage, _ct);
                await _duplex.Task;
                _log.LogInformation("Client disconnected");
            }
            catch (OperationCanceledException)
            { /* Expected during shutdown */
            }
            catch (Exception ex)
            {
                _log.LogError("{ex}", ex);
            }
        }

        _log.LogInformation("Lizard server stopped [{endpoint}]", _listener.LocalEndpoint);
    }

    public void Dispose() => _listener.Dispose();
}
