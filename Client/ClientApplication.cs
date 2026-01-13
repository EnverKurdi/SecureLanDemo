using System.Net.Security;
using System.Net.Sockets;

namespace ClientApp;

public sealed class ClientApplication
{
    private readonly string _ip;
    private readonly int _port;

    public ClientApplication(string ip, int port)
    {
        _ip = ip;
        _port = port;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[CLIENT] Connecting to {_ip}:{_port}");
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_ip, _port, ct);

        Console.WriteLine("[CLIENT] Connected to Server. Starting TLS handshake...");

        // For demo: accept any server cert (i praksis: valider cert!)
        using var ssl = new SslStream(
            tcp.GetStream(),
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: (_, _, _, _) => true);

        await ssl.AuthenticateAsClientAsync("SecureLanDemo-Server");

        Console.WriteLine("[CLIENT] TLS OK (Secure in Transit).");

        // Login
        string? currentUser = null;
        Console.Write("Username: ");
        var user = Console.ReadLine() ?? "";
        Console.Write("Password: ");
        var pass = ReadPassword();

        await WireProtocol.WriteStringAsync(ssl, "LOGIN", ct);
        await WireProtocol.WriteStringAsync(ssl, user, ct);
        await WireProtocol.WriteStringAsync(ssl, pass, ct);

        var ok = await WireProtocol.ReadBoolAsync(ssl, ct);
        var msg = await WireProtocol.ReadStringAsync(ssl, ct);
        if (!ok)
        {
            Console.WriteLine($"[CLIENT] Login failed: {msg}");
            return;
        }

        var group = await WireProtocol.ReadStringAsync(ssl, ct);
        currentUser = user;
        var folderCount = await WireProtocol.ReadInt32Async(ssl, ct);
        var folders = new List<string>();
        for (int i = 0; i < folderCount; i++) folders.Add(await WireProtocol.ReadStringAsync(ssl, ct));

        Console.WriteLine($"[CLIENT user={currentUser}] Login OK. Group={group}");
        Console.WriteLine($"[CLIENT] Allowed folders: {string.Join(", ", folders)}");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("1) List files");
            Console.WriteLine("2) Upload file");
            Console.WriteLine("3) Upload file from disk");
            Console.WriteLine("4) Download file to disk");
            Console.WriteLine("5) Exit");
            Console.Write("> ");
            var choice = Console.ReadLine()?.Trim();

            if (choice == "1")
            {
                await WireProtocol.WriteStringAsync(ssl, "LIST", ct);
                var lok = await WireProtocol.ReadBoolAsync(ssl, ct);
                if (!lok)
                {
                    Console.WriteLine($"[CLIENT] LIST error: {await WireProtocol.ReadStringAsync(ssl, ct)}");
                    continue;
                }

                var count = await WireProtocol.ReadInt32Async(ssl, ct);
                Console.WriteLine($"[CLIENT] Files you can see: {count}");
                for (int i = 0; i < count; i++)
                {
                    var id = await WireProtocol.ReadStringAsync(ssl, ct);
                    var folder = await WireProtocol.ReadStringAsync(ssl, ct);
                    var name = await WireProtocol.ReadStringAsync(ssl, ct);
                    var owner = await WireProtocol.ReadStringAsync(ssl, ct);
                    var ticks = await WireProtocol.ReadInt64Async(ssl, ct);
                    var dt = new DateTime(ticks, DateTimeKind.Utc);

                    Console.WriteLine($"- id={id} | folder={folder} | name={name} | owner={owner} | created(UTC)={dt:O}");
                }
                continue;
            }

            if (choice == "2")
            {
                Console.WriteLine($"Folder (suggested: {string.Join(", ", folders)}):");
                Console.Write("> ");
                var folder = Console.ReadLine() ?? "";

                Console.Write("Filename: ");
                var fileName = Console.ReadLine() ?? "demo.txt";

                Console.WriteLine("Enter file content (single line):");
                Console.Write("> ");
                var content = Console.ReadLine() ?? "";

                var bytes = System.Text.Encoding.UTF8.GetBytes(content);

                var actor = currentUser ?? "unknown";
                Console.WriteLine($"[CLIENT user={actor}] Sending file over TLS (encrypted in transit) -> folder={folder}, name={fileName}, bytes={bytes.Length}");
                await WireProtocol.WriteStringAsync(ssl, "UPLOAD", ct);
                await WireProtocol.WriteStringAsync(ssl, folder, ct);
                await WireProtocol.WriteStringAsync(ssl, fileName, ct);
                await WireProtocol.WriteBytesAsync(ssl, bytes, ct);
                Console.WriteLine($"[CLIENT user={actor}] Upload payload sent over TLS.");

                var uok = await WireProtocol.ReadBoolAsync(ssl, ct);
                var umsg = await WireProtocol.ReadStringAsync(ssl, ct);
                if (!uok)
                {
                    Console.WriteLine($"[CLIENT] Upload failed: {umsg}");
                    continue;
                }

                var fileId = await WireProtocol.ReadStringAsync(ssl, ct);
                Console.WriteLine($"[CLIENT user={actor}] Upload OK. fileId={fileId}");
                continue;
            }

            if (choice == "3")
            {
                Console.Write("Local file path: ");
                var path = Console.ReadLine() ?? "";
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    Console.WriteLine("[CLIENT] File not found.");
                    continue;
                }

                Console.WriteLine($"Folder (suggested: {string.Join(", ", folders)}):");
                Console.Write("> ");
                var folder = Console.ReadLine() ?? "";

                var fileName = Path.GetFileName(path);
                var bytes = await File.ReadAllBytesAsync(path, ct);

                var actor = currentUser ?? "unknown";
                Console.WriteLine($"[CLIENT user={actor}] Sending file over TLS (encrypted in transit) -> folder={folder}, name={fileName}, bytes={bytes.Length}");
                await WireProtocol.WriteStringAsync(ssl, "UPLOAD", ct);
                await WireProtocol.WriteStringAsync(ssl, folder, ct);
                await WireProtocol.WriteStringAsync(ssl, fileName, ct);
                await WireProtocol.WriteBytesAsync(ssl, bytes, ct);
                Console.WriteLine($"[CLIENT user={actor}] Upload payload sent over TLS.");

                var uok = await WireProtocol.ReadBoolAsync(ssl, ct);
                var umsg = await WireProtocol.ReadStringAsync(ssl, ct);
                if (!uok)
                {
                    Console.WriteLine($"[CLIENT] Upload failed: {umsg}");
                    continue;
                }

                var fileId = await WireProtocol.ReadStringAsync(ssl, ct);
                Console.WriteLine($"[CLIENT user={actor}] Upload OK. fileId={fileId}");
                continue;
            }

            if (choice == "4")
            {
                Console.Write("FileId: ");
                var fileId = Console.ReadLine() ?? "";

                var actor = currentUser ?? "unknown";
                Console.WriteLine($"[CLIENT user={actor}] Requesting download over TLS -> fileId={fileId}");
                await WireProtocol.WriteStringAsync(ssl, "DOWNLOAD", ct);
                await WireProtocol.WriteStringAsync(ssl, fileId, ct);

                var dok = await WireProtocol.ReadBoolAsync(ssl, ct);
                var dmsg = await WireProtocol.ReadStringAsync(ssl, ct);
                if (!dok)
                {
                    Console.WriteLine($"[CLIENT] Download failed: {dmsg}");
                    continue;
                }

                var plaintext = await WireProtocol.ReadBytesAsync(ssl, ct) ?? Array.Empty<byte>();
                Console.WriteLine($"[CLIENT user={actor}] Received file over TLS (bytes={plaintext.Length}).");
                Console.Write("Save to path (leave empty for ./<fileId>.bin): ");
                var savePath = Console.ReadLine() ?? "";
                if (string.IsNullOrWhiteSpace(savePath))
                {
                    savePath = Path.Combine(Environment.CurrentDirectory, $"{fileId}.bin");
                }
                else if (Directory.Exists(savePath))
                {
                    savePath = Path.Combine(savePath, $"{fileId}.bin");
                }

                try
                {
                    await File.WriteAllBytesAsync(savePath, plaintext, ct);
                    Console.WriteLine($"[CLIENT] Saved to {savePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CLIENT] Save failed: {ex.Message}");
                }
                continue;
            }

            if (choice == "5")
            {
                await WireProtocol.WriteStringAsync(ssl, "BYE", ct);
                Console.WriteLine("[CLIENT] Bye.");
                return;
            }
        }
    }

    private static string ReadPassword()
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? "";
        }

        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                    Console.Write("\b \b");
                }
                continue;
            }
            chars.Add(key.KeyChar);
            Console.Write("*");
        }
        return new string(chars.ToArray());
    }
}
