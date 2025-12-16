namespace DataStore;

public sealed class FileRecord
{
    public string FileId { get; set; } = "";
    public string Folder { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Owner { get; set; } = "";
    public long CreatedUtcTicks { get; set; }

    // Krypteret indhold (AES-GCM)
    public byte[] ContentNonce { get; set; } = Array.Empty<byte>();
    public byte[] EncryptedContent { get; set; } = Array.Empty<byte>();
    public byte[] ContentTag { get; set; } = Array.Empty<byte>();

    // Wrapped DEK (AES-GCM med KEK)
    public byte[] WrappedDekNonce { get; set; } = Array.Empty<byte>();
    public byte[] WrappedDek { get; set; } = Array.Empty<byte>();
    public byte[] WrappedDekTag { get; set; } = Array.Empty<byte>();
}

public readonly record struct FileMeta(string FileId, string Folder, string FileName, string Owner, long CreatedUtcTicks);
