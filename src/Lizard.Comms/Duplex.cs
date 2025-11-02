using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SerdesNet;

namespace Lizard.Comms;

public delegate ReadOnlyMemory<byte> PacketHandler(ReadOnlyMemory<byte> data);

public class Duplex
{
    enum PacketType : byte
    {
        Unknown = 0,
        Request = 1,
        ResponseOk = 2,
        ResponseFail = 3,
    }

    record Packet(PacketType Type, byte Id, ReadOnlyMemory<byte> Data);

    readonly ILogger _log;
    readonly PacketHandler _inboundHandler;
    readonly Channel<(Packet, TaskCompletionSource<ReadOnlyMemory<byte>>?)> _outboundQueue = Channel.CreateUnbounded<(
        Packet,
        TaskCompletionSource<ReadOnlyMemory<byte>>?
    )>();
    readonly ConcurrentDictionary<byte, TaskCompletionSource<ReadOnlyMemory<byte>>> _pendingRequests = new();
    readonly ConcurrentQueue<int> _availableIds = [];

    public Task Task { get; }

    public ReadOnlyMemory<byte> Send(ReadOnlyMemory<byte> requestData)
    {
        var tcs = new TaskCompletionSource<ReadOnlyMemory<byte>>();
        byte id = _availableIds.TryDequeue(out var next)
            ? (byte)next
            : throw new InvalidOperationException("Max concurrency reached");
        var packet = new Packet(PacketType.Request, id, requestData);
        if (!_outboundQueue.Writer.TryWrite((packet, tcs)))
            throw new InvalidOperationException("Failed to enqueue outbound packet");

        return tcs.Task.GetAwaiter().GetResult();
    }

    public Duplex(ILogger log, int maxConcurrency, TcpClient client, PacketHandler inboundHandler, CancellationToken ct)
    {
        _log = log;
        _inboundHandler = inboundHandler ?? throw new ArgumentNullException(nameof(inboundHandler));

        if (maxConcurrency > byte.MaxValue)
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency),
                $"Max concurrency must be less than {byte.MaxValue}"
            );

        for (int i = 0; i < maxConcurrency; i++)
            _availableIds.Enqueue(i + 1);

        Task = Task.Run(
            async () =>
            {
                try
                {
                    using (client)
                    {
                        await using (var stream = client.GetStream())
                        await using (
                            ct.Register(
                                () =>
                                {
                                    stream.Close();
                                },
                                true
                            )
                        )
                        {
                            await Task.WhenAny(Inbound(stream, ct), Outbound(stream, ct)).ConfigureAwait(false);
                        }

                        log.LogInformation("TCP stream closed");
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning("An exception occurred during TCP stream : {ex}", ex);
                }
            },
            ct
        );
    }

    async Task Outbound(Stream stream, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var (packet, tcs) = await _outboundQueue.Reader.ReadAsync(ct);
            MemoryWriterSerdes sw = new(6);
            sw.Int32("packetSize", packet.Data.Length);
            sw.EnumU8("packetType", packet.Type);
            sw.UInt8("id", packet.Id);
            _log.LogDebug("SEND {id} {type} - {size} bytes", packet.Id, packet.Type, packet.Data.Length);

            await stream.WriteAsync(sw.GetMemory(), ct).ConfigureAwait(false);
            await stream.WriteAsync(packet.Data, ct).ConfigureAwait(false);

            if (tcs != null)
                _pendingRequests[packet.Id] = tcs;
        }
    }

    async Task Inbound(Stream stream, CancellationToken ct = default)
    {
        byte[] header = new byte[6];
        bool done = false;
        while (!done && !ct.IsCancellationRequested)
        {
            try
            {
                var packet = await ReceivePacketAsync(stream, header, ct);
                if (packet == null)
                {
                    _log.LogInformation("Connection closed by remote");
                    break;
                }

                DispatchPacket(packet, ct);
            }
            catch (OperationCanceledException)
            {
                done = true;
            }
            catch (Exception ex)
            {
                _log.LogWarning("An exception occurred during inbound processing: {ex}", ex);
                done = true;
            }
        }
    }

    async Task<Packet?> ReceivePacketAsync(Stream stream, byte[] header, CancellationToken ct)
    {
        int bytesRead = await stream.ReadAsync(header.AsMemory(), ct).ConfigureAwait(false);
        if (bytesRead < header.Length)
            return null;

        var sr = new MemoryReaderSerdes(header);
        int packetSize = sr.Int32("packetSize", 0);
        if (packetSize < 0)
            throw new InvalidOperationException("Invalid packet size");

        PacketType packetType = sr.EnumU8("packetType", PacketType.Request);
        byte id = sr.UInt8("id", 0);
        _log.LogDebug("RECV {id} {type} - {size} bytes", id, packetType, packetSize);

        var buffer = new byte[packetSize];
        int totalBytesRead = 0;

        while (totalBytesRead < packetSize)
        {
            bytesRead = await stream
                .ReadAsync(buffer.AsMemory(totalBytesRead, packetSize - totalBytesRead), ct)
                .ConfigureAwait(false);
            if (bytesRead == 0)
                break;

            totalBytesRead += bytesRead;
        }

        if (totalBytesRead < packetSize)
            return null;

        var packet = new Packet(packetType, id, buffer);

        return packet;
    }

    void DispatchPacket(Packet packet, CancellationToken ct)
    {
        switch (packet.Type)
        {
            case PacketType.Request:
            {
                _ = Task.Run(
                    () =>
                    {
                        try
                        {
                            var responseData = _inboundHandler(packet.Data);
                            var outboundPacket = new Packet(PacketType.ResponseOk, packet.Id, responseData);
                            _outboundQueue.Writer.WriteAsync((outboundPacket, null), ct);
                        }
                        catch (Exception ex)
                        {
                            var errorData = Encoding.UTF8.GetBytes(ex.Message);
                            var outboundPacket = new Packet(PacketType.ResponseFail, packet.Id, errorData);
                            _outboundQueue.Writer.WriteAsync((outboundPacket, null), ct);
                        }
                    },
                    ct
                );

                break;
            }

            case PacketType.ResponseOk:
            {
                if (!_pendingRequests.TryRemove(packet.Id, out var tcs))
                {
                    _log.LogWarning($"Received response for unknown request id \"{packet.Id}\", discarding");
                    break;
                }

                _availableIds.Enqueue(packet.Id);
                tcs.SetResult(packet.Data);
                break;
            }

            case PacketType.ResponseFail:
            {
                if (!_pendingRequests.TryRemove(packet.Id, out var tcs))
                {
                    _log.LogWarning($"Received error response for unknown request id \"{packet.Id}\", discarding");
                    break;
                }

                _availableIds.Enqueue(packet.Id);
                var errorMessage = Encoding.UTF8.GetString(packet.Data.Span);
                tcs.SetException(new InvalidOperationException(errorMessage));
                break;
            }

            case PacketType.Unknown:
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
