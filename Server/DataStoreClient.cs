using System.Net.Sockets;

namespace ServerApp;

public sealed class DataStoreClient
{
    private readonly string _ip;
    private readonly int _port;

    public DataStoreClient(string ip, int port)
    {
        _ip = ip;
        _port = port;
    }

    public async Task<string> SaveAsync(FileRecordForStore r, CancellationToken ct)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_ip, _port, ct);
        await using var s = tcp.GetStream();

        await WireProtocol.WriteStringAsync(s, "SAVE", ct);
        await WireProtocol.WriteStringAsync(s, r.Folder, ct);
        await WireProtocol.WriteStringAsync(s, r.FileName, ct);
        await WireProtocol.WriteStringAsync(s, r.Owner, ct);

        await WireProtocol.WriteBytesAsync(s, r.ContentNonce, ct);
        await WireProtocol.WriteBytesAsync(s, r.EncryptedContent, ct);
        await WireProtocol.WriteBytesAsync(s, r.ContentTag, ct);

        await WireProtocol.WriteBytesAsync(s, r.WrappedDekNonce, ct);
        await WireProtocol.WriteBytesAsync(s, r.WrappedDek, ct);
        await WireProtocol.WriteBytesAsync(s, r.WrappedDekTag, ct);

        var ok = await WireProtocol.ReadBoolAsync(s, ct);
        if (!ok) throw new InvalidOperationException(await WireProtocol.ReadStringAsync(s, ct));

        return await WireProtocol.ReadStringAsync(s, ct);
    }

    public async Task<FileRecordLoaded> LoadAsync(string fileId, CancellationToken ct)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_ip, _port, ct);
        await using var s = tcp.GetStream();

        await WireProtocol.WriteStringAsync(s, "LOAD", ct);
        await WireProtocol.WriteStringAsync(s, fileId, ct);

        var ok = await WireProtocol.ReadBoolAsync(s, ct);
        if (!ok)
        {
            var msg = await WireProtocol.ReadStringAsync(s, ct);
            if (msg == "NOT_FOUND") throw new KeyNotFoundException("File not found.");
            throw new InvalidOperationException(msg);
        }

        // Record layout matches DataStore's WriteRecordAsync
        var id = await WireProtocol.ReadStringAsync(s, ct);
        var folder = await WireProtocol.ReadStringAsync(s, ct);
        var name = await WireProtocol.ReadStringAsync(s, ct);
        var owner = await WireProtocol.ReadStringAsync(s, ct);
        var createdTicks = await WireProtocol.ReadInt64Async(s, ct);

        var contentNonce = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>();
        var encrypted = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>();
        var contentTag = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>();

        var wNonce = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>();
        var w = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>();
        var wTag = await WireProtocol.ReadBytesAsync(s, ct) ?? Array.Empty<byte>();

        return new FileRecordLoaded
        {
            FileId = id,
            Folder = folder,
            FileName = name,
            Owner = owner,
            CreatedUtcTicks = createdTicks,
            ContentNonce = contentNonce,
            EncryptedContent = encrypted,
            ContentTag = contentTag,
            WrappedDekNonce = wNonce,
            WrappedDek = w,
            WrappedDekTag = wTag
        };
    }

    public async Task<List<FileMeta>> ListAsync(CancellationToken ct)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_ip, _port, ct);
        await using var s = tcp.GetStream();

        await WireProtocol.WriteStringAsync(s, "LIST", ct);
        var ok = await WireProtocol.ReadBoolAsync(s, ct);
        if (!ok) throw new InvalidOperationException(await WireProtocol.ReadStringAsync(s, ct));

        var count = await WireProtocol.ReadInt32Async(s, ct);
        var list = new List<FileMeta>(count);
        for (int i = 0; i < count; i++)
        {
            var id = await WireProtocol.ReadStringAsync(s, ct);
            var folder = await WireProtocol.ReadStringAsync(s, ct);
            var name = await WireProtocol.ReadStringAsync(s, ct);
            var owner = await WireProtocol.ReadStringAsync(s, ct);
            var ticks = await WireProtocol.ReadInt64Async(s, ct);
            list.Add(new FileMeta(id, folder, name, owner, ticks));
        }
        return list;
    }
}
