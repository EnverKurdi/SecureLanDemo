namespace ServerApp;

// Sendes til DataStore ved SAVE
public sealed class FileRecordForStore
{
    public string Folder { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Owner { get; set; } = "";

    public byte[] ContentNonce { get; set; } = Array.Empty<byte>();
    public byte[] EncryptedContent { get; set; } = Array.Empty<byte>();
    public byte[] ContentTag { get; set; } = Array.Empty<byte>();

    public byte[] WrappedDekNonce { get; set; } = Array.Empty<byte>();
    public byte[] WrappedDek { get; set; } = Array.Empty<byte>();
    public byte[] WrappedDekTag { get; set; } = Array.Empty<byte>();
}

// Modtages fra DataStore ved LOAD
public sealed class FileRecordLoaded
{
    public string FileId { get; set; } = "";
    public string Folder { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Owner { get; set; } = "";
    public long CreatedUtcTicks { get; set; }

    public byte[] ContentNonce { get; set; } = Array.Empty<byte>();
    public byte[] EncryptedContent { get; set; } = Array.Empty<byte>();
    public byte[] ContentTag { get; set; } = Array.Empty<byte>();

    public byte[] WrappedDekNonce { get; set; } = Array.Empty<byte>();
    public byte[] WrappedDek { get; set; } = Array.Empty<byte>();
    public byte[] WrappedDekTag { get; set; } = Array.Empty<byte>();
}

public readonly record struct FileMeta(string FileId, string Folder, string FileName, string Owner, long CreatedUtcTicks);
