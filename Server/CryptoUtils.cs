using System.Security.Cryptography;

namespace ServerApp;

public static class CryptoUtils
{
    public readonly record struct AesGcmBlob(byte[] Nonce, byte[] Ciphertext, byte[] Tag);

    public static AesGcmBlob EncryptAesGcm(byte[] key32, byte[] plaintext)
    {
        if (key32.Length != 32) throw new ArgumentException("AES-256 key must be 32 bytes.");

        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key32);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return new AesGcmBlob(nonce, ciphertext, tag);
    }

    public static byte[] DecryptAesGcm(byte[] key32, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        if (key32.Length != 32) throw new ArgumentException("AES-256 key must be 32 bytes.");

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key32);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
