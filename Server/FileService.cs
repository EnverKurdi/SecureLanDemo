using System.Security.Cryptography;

namespace ServerApp;

public sealed class FileService
{
    private readonly HsmClient _hsm;
    private readonly DataStoreClient _data;
    private readonly AccessControlManager _acl;

    public FileService(HsmClient hsm, DataStoreClient data, AccessControlManager acl)
    {
        _hsm = hsm;
        _data = data;
        _acl = acl;
    }

    public async Task<string> SaveFileAsync(string user, string folder, string fileName, byte[] plaintext, CancellationToken ct)
    {
        if (!_acl.HasPermission(user, folder, "write"))
            throw new UnauthorizedAccessException("ACL denied.");

        // 1) Generér DEK (AES-256)
        var dek = RandomNumberGenerator.GetBytes(32);

        // 2) Krypter fil med DEK (AES-GCM => integritet + fortrolighed)
        var contentBlob = CryptoUtils.EncryptAesGcm(dek, plaintext);

        // Simuler "plaintext var i RAM kort" og så overskriv det
        CryptographicOperations.ZeroMemory(plaintext);

        // 3) Wrap DEK i HSM med KEK
        Console.WriteLine("[SERVER] Calling HSM to WRAP DEK (KEK never leaves HSM)...");
        var wrapped = await _hsm.WrapKeyAsync(dek, ct);

        // 4) Slet DEK fra RAM ASAP
        CryptographicOperations.ZeroMemory(dek);

        // 5) Send ciphertext + wrapped DEK til DataStore (Secure at Rest)
        var record = new FileRecordForStore
        {
            Folder = folder,
            FileName = fileName,
            Owner = user,

            ContentNonce = contentBlob.Nonce,
            EncryptedContent = contentBlob.Ciphertext,
            ContentTag = contentBlob.Tag,

            WrappedDekNonce = wrapped.Nonce,
            WrappedDek = wrapped.Ciphertext,
            WrappedDekTag = wrapped.Tag,
        };

        Console.WriteLine("[SERVER] Saving EncryptedContent + WrappedDEK to DataStore (no plaintext, no DEK stored).");
        var fileId = await _data.SaveAsync(record, ct);
        return fileId;
    }

    public async Task<byte[]> LoadFileAsync(string user, string fileId, CancellationToken ct)
    {
        var record = await _data.LoadAsync(fileId, ct);

        if (!_acl.HasPermission(user, record.Folder, "read"))
            throw new UnauthorizedAccessException("ACL denied.");

        // 1) Unwrap DEK via HSM
        Console.WriteLine("[SERVER] Calling HSM to UNWRAP DEK...");
        var dek = await _hsm.UnwrapKeyAsync(record.WrappedDekNonce, record.WrappedDek, record.WrappedDekTag, ct);

        // 2) Dekrypter indhold i RAM
        Console.WriteLine("[SERVER] Decrypting file in RAM with DEK...");
        var plaintext = CryptoUtils.DecryptAesGcm(dek, record.ContentNonce, record.EncryptedContent, record.ContentTag);

        // 3) Slet DEK fra RAM ASAP
        CryptographicOperations.ZeroMemory(dek);

        return plaintext;
    }
}
