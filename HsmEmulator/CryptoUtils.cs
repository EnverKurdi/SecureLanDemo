using System.Security.Cryptography;

namespace HsmEmulator;

public static class CryptoUtils
{
    public readonly record struct AesGcmBlob(byte[] Nonce, byte[] Ciphertext, byte[] Tag);

    public static AesGcmBlob EncryptAesGcm(byte[] key32, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key32);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return new AesGcmBlob(nonce, ciphertext, tag);
    }

    public static byte[] DecryptAesGcm(byte[] key32, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key32);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
