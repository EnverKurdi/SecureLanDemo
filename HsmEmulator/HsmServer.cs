using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace HsmEmulator;

public sealed class HsmServer
{
    private readonly IPAddress _ip;
    private readonly int _port;

    // Simuleret "hardware-beskyttet" KEK (32 bytes = AES-256).
    // I den virkelige verden: ikke hardcoded, og aldrig eksporterbar.
    private static readonly byte[] Kek = Convert.FromBase64String(
        "5lQ3mL+1o3u9kq9Vb5f2c9mYy2kq4bqg3xKQ3m3p0uE="); // 32 bytes

    public HsmServer(string ip, int port)
    {
        _ip = IPAddress.Parse(ip);
        _port = port;
        if (Kek.Length != 32) throw new InvalidOperationException("KEK must be 32 bytes (AES-256).");
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var listener = new TcpListener(_ip, _port);
        listener.Start();
        Console.WriteLine("[HSM] Listening...");

        while (!ct.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(ct);
            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private static async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        Console.WriteLine("[HSM] Server received connection from AppServer.");
        await using var _ = client;
        await using var stream = client.GetStream();

        try
        {
            while (true)
            {
                var cmd = await WireProtocol.ReadStringAsync(stream, ct);
                if (cmd.Equals("PING", StringComparison.OrdinalIgnoreCase))
                {
                    await WireProtocol.WriteBoolAsync(stream, true, ct);
                    await WireProtocol.WriteStringAsync(stream, "PONG", ct);
                    continue;
                }

                if (cmd.Equals("WRAP", StringComparison.OrdinalIgnoreCase))
                {
                    // Input: dek (klartekst)
                    var dek = await WireProtocol.ReadBytesAsync(stream, ct);
                    if (dek is null) throw new InvalidDataException("DEK missing.");

                    // Wrap med KEK (AES-GCM)
                    var wrapped = CryptoUtils.EncryptAesGcm(Kek, dek);

                    // Sikkerhed: slet DEK fra RAM ASAP
                    CryptographicOperations.ZeroMemory(dek);

                    await WireProtocol.WriteBoolAsync(stream, true, ct);
                    await WireProtocol.WriteBytesAsync(stream, wrapped.Nonce, ct);
                    await WireProtocol.WriteBytesAsync(stream, wrapped.Ciphertext, ct);
                    await WireProtocol.WriteBytesAsync(stream, wrapped.Tag, ct);

                    Console.WriteLine("[HSM] Wrapped a DEK (returned wrapped key blob).");
                    continue;
                }

                if (cmd.Equals("UNWRAP", StringComparison.OrdinalIgnoreCase))
                {
                    var nonce = await WireProtocol.ReadBytesAsync(stream, ct) ?? throw new InvalidDataException("Nonce missing.");
                    var cipher = await WireProtocol.ReadBytesAsync(stream, ct) ?? throw new InvalidDataException("Cipher missing.");
                    var tag = await WireProtocol.ReadBytesAsync(stream, ct) ?? throw new InvalidDataException("Tag missing.");

                    var dek = CryptoUtils.DecryptAesGcm(Kek, nonce, cipher, tag);

                    await WireProtocol.WriteBoolAsync(stream, true, ct);
                    await WireProtocol.WriteBytesAsync(stream, dek, ct);

                    Console.WriteLine("[HSM] Unwrapped a DEK (returned plaintext DEK to AppServer).");

                    // Bemærk: AppServer er ansvarlig for at zero’e DEK efter brug.
                    continue;
                }

                if (cmd.Equals("BYE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[HSM] AppServer closed connection.");
                    return;
                }

                await WireProtocol.WriteBoolAsync(stream, false, ct);
                await WireProtocol.WriteStringAsync(stream, $"Unknown command: {cmd}", ct);
            }
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("[HSM] Connection closed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HSM] Error: {ex.Message}");
        }
    }
}
