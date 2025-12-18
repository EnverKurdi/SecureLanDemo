using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace ServerApp;

public sealed class ServerApplication
{
    private readonly IPAddress _ip;
    private readonly int _port;

    private readonly UserAuthenticator _auth = new();
    private readonly AccessControlManager _acl = new();
    private readonly HsmClient _hsm;
    private readonly DataStoreClient _data;
    private readonly FileService _files;

    private readonly X509Certificate2 _cert;

    public ServerApplication(string serverIp, int serverPort, string hsmIp, int hsmPort, string dataIp, int dataPort)
    {
        _ip = IPAddress.Parse(serverIp);
        _port = serverPort;

        _hsm = new HsmClient(hsmIp, hsmPort);
        _data = new DataStoreClient(dataIp, dataPort);
        _files = new FileService(_hsm, _data, _acl);

        _cert = CertificateFactory.CreateSelfSigned("CN=SecureLanDemo-Server");
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var listener = new TcpListener(_ip, _port);
        listener.Start();
        Console.WriteLine("[SERVER] Listening for TLS clients...");

        while (!ct.IsCancellationRequested)
        {
            var tcp = await listener.AcceptTcpClientAsync(ct);
            _ = Task.Run(() => HandleClientAsync(tcp, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        using var _ = tcp;

        Console.WriteLine("[SERVER] Client connected. Starting TLS handshake (SslStream)...");
        await using var netStream = tcp.GetStream();
        await using var ssl = new SslStream(netStream, leaveInnerStreamOpen: false);

        try
        {
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _cert,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ClientCertificateRequired = false,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, ct);

            Console.WriteLine("[SERVER] TLS OK (Secure in Transit).");

            // Session state
            string? user = null;
            string? group = null;

            while (true)
            {
                var cmd = await WireProtocol.ReadStringAsync(ssl, ct);

                if (cmd.Equals("LOGIN", StringComparison.OrdinalIgnoreCase))
                {
                    var username = await WireProtocol.ReadStringAsync(ssl, ct);
                    var password = await WireProtocol.ReadStringAsync(ssl, ct);

                    if (_auth.Authenticate(username, password, out var g))
                    {
                        user = username;
                        group = g;
                        Console.WriteLine($"[SERVER] Auth OK for {user} ({group}).");

                        await WireProtocol.WriteBoolAsync(ssl, true, ct);
                        await WireProtocol.WriteStringAsync(ssl, "LOGIN_OK", ct);
                        await WireProtocol.WriteStringAsync(ssl, group, ct);

                        var folders = _acl.AllowedFolders(user).ToArray();
                        await WireProtocol.WriteInt32Async(ssl, folders.Length, ct);
                        foreach (var f in folders) await WireProtocol.WriteStringAsync(ssl, f, ct);
                    }
                    else
                    {
                        Console.WriteLine($"[SERVER] Auth FAIL for {username}.");
                        await WireProtocol.WriteBoolAsync(ssl, false, ct);
                        await WireProtocol.WriteStringAsync(ssl, "LOGIN_FAILED", ct);
                    }

                    continue;
                }

                if (cmd.Equals("BYE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[SERVER] Client ended session.");
                    return;
                }

                if (user is null || group is null)
                {
                    await WireProtocol.WriteBoolAsync(ssl, false, ct);
                    await WireProtocol.WriteStringAsync(ssl, "ERROR_NOT_LOGGED_IN", ct);
                    continue;
                }

                if (cmd.Equals("LIST", StringComparison.OrdinalIgnoreCase))
                {
                    var metas = await _data.ListAsync(ct);
                    var filtered = metas.Where(m => _acl.HasPermission(user, m.Folder, "read")).ToList();

                    await WireProtocol.WriteBoolAsync(ssl, true, ct);
                    await WireProtocol.WriteInt32Async(ssl, filtered.Count, ct);
                    foreach (var m in filtered)
                    {
                        await WireProtocol.WriteStringAsync(ssl, m.FileId, ct);
                        await WireProtocol.WriteStringAsync(ssl, m.Folder, ct);
                        await WireProtocol.WriteStringAsync(ssl, m.FileName, ct);
                        await WireProtocol.WriteStringAsync(ssl, m.Owner, ct);
                        await WireProtocol.WriteInt64Async(ssl, m.CreatedUtcTicks, ct);
                    }
                    continue;
                }

                if (cmd.Equals("UPLOAD", StringComparison.OrdinalIgnoreCase))
                {
                    var folder = await WireProtocol.ReadStringAsync(ssl, ct);
                    var fileName = await WireProtocol.ReadStringAsync(ssl, ct);
                    var contentBytes = await WireProtocol.ReadBytesAsync(ssl, ct) ?? Array.Empty<byte>();

                    Console.WriteLine($"[SERVER] TLS received upload (encrypted in transit) from {user} -> folder={folder}, name={fileName}, bytes={contentBytes.Length}");
                    Console.WriteLine("[SERVER] Preparing to encrypt payload at rest (plaintext in RAM briefly).");

                    try
                    {
                        var fileId = await _files.SaveFileAsync(user, folder, fileName, contentBytes, ct);

                        await WireProtocol.WriteBoolAsync(ssl, true, ct);
                        await WireProtocol.WriteStringAsync(ssl, "UPLOAD_OK", ct);
                        await WireProtocol.WriteStringAsync(ssl, fileId, ct);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        await WireProtocol.WriteBoolAsync(ssl, false, ct);
                        await WireProtocol.WriteStringAsync(ssl, "DENIED", ct);
                    }
                    catch (Exception ex)
                    {
                        await WireProtocol.WriteBoolAsync(ssl, false, ct);
                        await WireProtocol.WriteStringAsync(ssl, $"ERROR: {ex.Message}", ct);
                    }
                    finally
                    {
                        // Slet plaintext fra RAM (best effort)
                        System.Security.Cryptography.CryptographicOperations.ZeroMemory(contentBytes);
                    }

                    continue;
                }

                if (cmd.Equals("DOWNLOAD", StringComparison.OrdinalIgnoreCase))
                {
                    var fileId = await WireProtocol.ReadStringAsync(ssl, ct);

                    try
                    {
                        var plaintext = await _files.LoadFileAsync(user, fileId, ct);

                        Console.WriteLine($"[SERVER] Sending file to {user} over TLS (bytes={plaintext.Length}).");
                        await WireProtocol.WriteBoolAsync(ssl, true, ct);
                        await WireProtocol.WriteStringAsync(ssl, "DOWNLOAD_OK", ct);
                        await WireProtocol.WriteBytesAsync(ssl, plaintext, ct);

                        // Server side RAM cleanup
                        System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        await WireProtocol.WriteBoolAsync(ssl, false, ct);
                        await WireProtocol.WriteStringAsync(ssl, "DENIED", ct);
                    }
                    catch (KeyNotFoundException)
                    {
                        await WireProtocol.WriteBoolAsync(ssl, false, ct);
                        await WireProtocol.WriteStringAsync(ssl, "NOT_FOUND", ct);
                    }
                    catch (Exception ex)
                    {
                        await WireProtocol.WriteBoolAsync(ssl, false, ct);
                        await WireProtocol.WriteStringAsync(ssl, $"ERROR: {ex.Message}", ct);
                    }

                    continue;
                }

                await WireProtocol.WriteBoolAsync(ssl, false, ct);
                await WireProtocol.WriteStringAsync(ssl, $"Unknown command: {cmd}", ct);
            }
        }
        catch (AuthenticationException ex)
        {
            Console.WriteLine($"[SERVER] TLS failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SERVER] Error: {ex.Message}");
        }
    }
}
