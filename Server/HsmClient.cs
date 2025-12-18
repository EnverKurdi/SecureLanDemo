using System.Net.Sockets;

namespace ServerApp;

public sealed class HsmClient
{
    private readonly string _ip;
    private readonly int _port;

    public HsmClient(string ip, int port)
    {
        _ip = ip;
        _port = port;
    }

    public async Task<CryptoUtils.AesGcmBlob> WrapKeyAsync(byte[] dekPlaintext, CancellationToken ct)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_ip, _port, ct);
        await using var s = tcp.GetStream();

        Console.WriteLine("[SERVER] -> HSM: WRAP request (DEK).");
        await WireProtocol.WriteStringAsync(s, "WRAP", ct);
        await WireProtocol.WriteBytesAsync(s, dekPlaintext, ct);

        var ok = await WireProtocol.ReadBoolAsync(s, ct);
        if (!ok) throw new InvalidOperationException(await WireProtocol.ReadStringAsync(s, ct));

        var nonce = await WireProtocol.ReadBytesAsync(s, ct) ?? throw new InvalidDataException("nonce missing");
        var cipher = await WireProtocol.ReadBytesAsync(s, ct) ?? throw new InvalidDataException("cipher missing");
        var tag = await WireProtocol.ReadBytesAsync(s, ct) ?? throw new InvalidDataException("tag missing");

        Console.WriteLine("[SERVER] <- HSM: WRAP response (wrapped DEK).");
        return new CryptoUtils.AesGcmBlob(nonce, cipher, tag);
    }

    public async Task<byte[]> UnwrapKeyAsync(byte[] nonce, byte[] cipher, byte[] tag, CancellationToken ct)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_ip, _port, ct);
        await using var s = tcp.GetStream();

        Console.WriteLine("[SERVER] -> HSM: UNWRAP request (wrapped DEK).");
        await WireProtocol.WriteStringAsync(s, "UNWRAP", ct);
        await WireProtocol.WriteBytesAsync(s, nonce, ct);
        await WireProtocol.WriteBytesAsync(s, cipher, ct);
        await WireProtocol.WriteBytesAsync(s, tag, ct);

        var ok = await WireProtocol.ReadBoolAsync(s, ct);
        if (!ok) throw new InvalidOperationException(await WireProtocol.ReadStringAsync(s, ct));

        var dek = await WireProtocol.ReadBytesAsync(s, ct) ?? throw new InvalidDataException("DEK missing");
        Console.WriteLine("[SERVER] <- HSM: UNWRAP response (plaintext DEK).");
        return dek;
    }
}
