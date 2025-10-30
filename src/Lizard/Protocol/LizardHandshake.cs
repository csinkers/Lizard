using System.Net.Sockets;
using System.Text;

namespace Lizard.Protocol;

public static class LizardHandshake
{
    const string Magic = "Lizard";
    static readonly byte[] MagicBytes = Encoding.UTF8.GetBytes(Magic);

    public static async Task<byte> ClientHandshake(TcpClient tcpClient, byte maxVersion)
    {
        var s = tcpClient.GetStream();
        var buf = new byte[MagicBytes.Length + 1];
        MagicBytes.CopyTo(buf, 0);
        buf[MagicBytes.Length] = maxVersion;
        await s.WriteAsync(buf);
        await s.FlushAsync();
        return (byte)s.ReadByte();
    }

    public static async Task<int> ServerHandshake(TcpClient tcpClient, byte maxVersion)
    {
        var s = tcpClient.GetStream();
        var buf = new byte[MagicBytes.Length + 1];
        int received = await s.ReadAsync(buf);
        if (received < buf.Length)
            throw new InvalidOperationException("Incomplete handshake");

        if (!buf.AsSpan(0, MagicBytes.Length).SequenceEqual(MagicBytes))
            throw new InvalidOperationException("Invalid handshake");

        byte clientVersion = buf[MagicBytes.Length];
        byte version = Math.Min(clientVersion, maxVersion);
        s.WriteByte(version);
        await s.FlushAsync();
        return version;
    }
}
