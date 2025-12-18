using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace DataStore;

public sealed class DataStoreServer
{
    private readonly IPAddress _ip;
    private readonly int _port;
    private readonly string _storageRoot;

    private readonly Dictionary<string, FileRecord> _db = new();

    public DataStoreServer(string ip, int port)
    {
        _ip = IPAddress.Parse(ip);
        _port = port;
        _storageRoot = ResolveStorageRoot();
        EnsureStorageFolders();
        LoadExistingRecords();
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var listener = new TcpListener(_ip, _port);
        listener.Start();
        Console.WriteLine("[DATASTORE] Listening... (stores ciphertext only)");
        Console.WriteLine($"[DATASTORE] Storage path: {_storageRoot}");

        while (!ct.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(ct);
            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        Console.WriteLine("[DATASTORE] Server received connection from AppServer.");
        using var _ = client;
        await using var stream = client.GetStream();

        try
        {
            while (true)
            {
                var cmd = await WireProtocol.ReadStringAsync(stream, ct);

                if (cmd.Equals("SAVE", StringComparison.OrdinalIgnoreCase))
                {
                    var record = await ReadRecordAsync(stream, ct);
                    record.FileId = Guid.NewGuid().ToString("N");
                    record.CreatedUtcTicks = DateTime.UtcNow.Ticks;

                    _db[record.FileId] = record;
                    PersistRecord(record);

                    Console.WriteLine($"[DATASTORE] Saved record: id={record.FileId}, folder={record.Folder}, file={record.FileName}");
                    Console.WriteLine($"[DATASTORE]   Stored bytes (ciphertext) = {record.EncryptedContent.Length} (no plaintext, no DEK).");
                    Console.WriteLine("[DATASTORE] Ciphertext stored at rest (disk).");

                    await WireProtocol.WriteBoolAsync(stream, true, ct);
                    await WireProtocol.WriteStringAsync(stream, record.FileId, ct);
                    continue;
                }

                if (cmd.Equals("LOAD", StringComparison.OrdinalIgnoreCase))
                {
                    var id = await WireProtocol.ReadStringAsync(stream, ct);
                    if (_db.TryGetValue(id, out var record))
                    {
                        await WireProtocol.WriteBoolAsync(stream, true, ct);
                        await WriteRecordAsync(stream, record, ct);
                        Console.WriteLine($"[DATASTORE] Loaded ciphertext record id={id}.");
                    }
                    else
                    {
                        await WireProtocol.WriteBoolAsync(stream, false, ct);
                        await WireProtocol.WriteStringAsync(stream, "NOT_FOUND", ct);
                    }
                    continue;
                }

                if (cmd.Equals("LIST", StringComparison.OrdinalIgnoreCase))
                {
                    var list = _db.Values
                        .Select(r => new FileMeta(r.FileId, r.Folder, r.FileName, r.Owner, r.CreatedUtcTicks))
                        .OrderBy(m => m.Folder)
                        .ThenBy(m => m.FileName)
                        .ToList();

                    await WireProtocol.WriteBoolAsync(stream, true, ct);
                    await WireProtocol.WriteInt32Async(stream, list.Count, ct);
                    foreach (var m in list)
                    {
                        await WireProtocol.WriteStringAsync(stream, m.FileId, ct);
                        await WireProtocol.WriteStringAsync(stream, m.Folder, ct);
                        await WireProtocol.WriteStringAsync(stream, m.FileName, ct);
                        await WireProtocol.WriteStringAsync(stream, m.Owner, ct);
                        await WireProtocol.WriteInt64Async(stream, m.CreatedUtcTicks, ct);
                    }
                    continue;
                }

                if (cmd.Equals("BYE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[DATASTORE] AppServer closed connection.");
                    return;
                }

                await WireProtocol.WriteBoolAsync(stream, false, ct);
                await WireProtocol.WriteStringAsync(stream, $"Unknown command: {cmd}", ct);
            }
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("[DATASTORE] Connection closed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DATASTORE] Error: {ex.Message}");
        }
    }

    private static async Task<FileRecord> ReadRecordAsync(Stream s, CancellationToken ct)
    {
        var r = new FileRecord
        {
            FileId = "", // sættes af DataStore
            Folder = await WireProtocol.ReadStringAsync(s, ct),
            FileName = await WireProtocol.ReadStringAsync(s, ct),
            Owner = await WireProtocol.ReadStringAsync(s, ct),

            ContentNonce = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>(),
            EncryptedContent = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>(),
            ContentTag = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>(),

            WrappedDekNonce = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>(),
            WrappedDek = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>(),
            WrappedDekTag = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>(),
        };
        return r;
    }

    private static async Task WriteRecordAsync(Stream s, FileRecord r, CancellationToken ct)
    {
        await WireProtocol.WriteStringAsync(s, r.FileId, ct);
        await WireProtocol.WriteStringAsync(s, r.Folder, ct);
        await WireProtocol.WriteStringAsync(s, r.FileName, ct);
        await WireProtocol.WriteStringAsync(s, r.Owner, ct);
        await WireProtocol.WriteInt64Async(s, r.CreatedUtcTicks, ct);

        await WireProtocol.WriteBytesAsync(s, r.ContentNonce, ct);
        await WireProtocol.WriteBytesAsync(s, r.EncryptedContent, ct);
        await WireProtocol.WriteBytesAsync(s, r.ContentTag, ct);

        await WireProtocol.WriteBytesAsync(s, r.WrappedDekNonce, ct);
        await WireProtocol.WriteBytesAsync(s, r.WrappedDek, ct);
        await WireProtocol.WriteBytesAsync(s, r.WrappedDekTag, ct);
    }

    private void PersistRecord(FileRecord record)
    {
        var folderDir = Path.Combine(_storageRoot, record.Folder);
        Directory.CreateDirectory(folderDir);
        var path = Path.Combine(folderDir, $"{record.FileId}.json");
        var json = JsonSerializer.Serialize(record);
        File.WriteAllText(path, json);
    }

    private void LoadExistingRecords()
    {
        if (!Directory.Exists(_storageRoot)) return;
        foreach (var file in Directory.EnumerateFiles(_storageRoot, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                var record = JsonSerializer.Deserialize<FileRecord>(json);
                if (record is null || string.IsNullOrWhiteSpace(record.FileId)) continue;
                _db[record.FileId] = record;
            }
            catch
            {
                // Ignore corrupted files; datastore should keep running.
            }
        }
    }

    private void EnsureStorageFolders()
    {
        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(Path.Combine(_storageRoot, "Folder_Group2"));
        Directory.CreateDirectory(Path.Combine(_storageRoot, "Folder_Group3"));
    }

    private static string ResolveStorageRoot()
    {
        var cwd = Directory.GetCurrentDirectory();
        if (string.Equals(Path.GetFileName(cwd), "DataStore", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(cwd, "storage");
        }

        var candidate = Path.Combine(cwd, "DataStore", "storage");
        if (Directory.Exists(Path.Combine(cwd, "DataStore")))
        {
            return candidate;
        }

        return Path.Combine(cwd, "storage");
    }
}
