using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ServerApp;

public static class CertificateFactory
{
    public static X509Certificate2 CreateSelfSigned(string subjectName)
    {
        var pfxPath = Path.Combine(AppContext.BaseDirectory, "certs", "server.pfx");
        if (File.Exists(pfxPath))
        {
            return new X509Certificate2(pfxPath, (string?)null, X509KeyStorageFlags.Exportable);
        }

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

        // Gyldigt i 1 år
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        // EphemeralKeySet undgår nogle keychain/permission quirks på macOS.
        return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }
}
