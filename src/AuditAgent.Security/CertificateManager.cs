using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AuditAgent.Security;

/// <summary>
/// Gestor de certificados X.509 para mTLS y firma Authenticode.
/// 
/// Para desarrollo genera certificados autofirmados.
/// Para producción se recomienda una PKI empresarial
/// (Active Directory Certificate Services o similar).
/// </summary>
public class CertificateManager
{
    /// <summary>
    /// Genera un certificado autofirmado para el servidor central (con SAN).
    /// </summary>
    public static X509Certificate2 CreateServerCertificate(
        string subjectName,
        string[]? sanDnsNames = null,
        int validityYears = 3)
    {
        var subject = new X500DistinguishedName($"CN={subjectName}, O=AuditAgent, C=CO");
        var extensions = new List<X509Extension>();

        // Key Usage: Digital Signature, Key Encipherment
        var keyUsage = new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature |
            X509KeyUsageFlags.KeyEncipherment,
            critical: true);
        extensions.Add(keyUsage);

        // Enhanced Key Usage: TLS Server Authentication
        var eku = new X509EnhancedKeyUsageExtension(
            new OidCollection
            {
                new Oid("1.3.6.1.5.5.7.3.1") // Server Authentication
            },
            critical: false);
        extensions.Add(eku);

        // Subject Alternative Names (SAN)
        if (sanDnsNames is not null && sanDnsNames.Length > 0)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var dns in sanDnsNames)
                sanBuilder.AddDnsName(dns);
            extensions.Add(sanBuilder.Build());
        }

        using var rsa = RSA.Create(4096);
        var req = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        foreach (var ext in extensions)
            req.CertificateExtensions.Add(ext);

        var now = DateTimeOffset.UtcNow;
        var cert = req.CreateSelfSigned(
            now.AddSeconds(-5),
            now.AddYears(validityYears));

        return cert.CopyWithPrivateKey(rsa);
    }

    /// <summary>
    /// Genera un certificado para un agente (para mTLS client auth).
    /// En producción, esto debería ser firmado por una CA empresarial.
    /// </summary>
    public static X509Certificate2 CreateAgentCertificate(
        string agentId,
        string organization,
        int validityYears = 2)
    {
        var subject = new X500DistinguishedName(
            $"CN=Agent-{agentId}, O={organization}, C=CO");

        using var rsa = RSA.Create(2048);

        var req = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Client Authentication EKU
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") },
            critical: false));

        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature,
            critical: true));

        var now = DateTimeOffset.UtcNow;
        var cert = req.CreateSelfSigned(
            now.AddSeconds(-5),
            now.AddYears(validityYears));

        return cert.CopyWithPrivateKey(rsa);
    }

    /// <summary>
    /// Exporta un certificado a archivo PFX (con clave privada).
    /// </summary>
    public static void ExportToPfx(
        X509Certificate2 cert,
        string filePath,
        string password)
    {
        var pfxBytes = cert.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(filePath, pfxBytes);

        // En Windows, marcar como archivo cifrado (EFS)
        if (OperatingSystem.IsWindows())
        {
            new System.IO.FileInfo(filePath).Attributes |=
                System.IO.FileAttributes.Encrypted;
        }
    }

    /// <summary>
    /// Exporta solo la clave pública a archivo CER.
    /// </summary>
    public static void ExportToCer(
        X509Certificate2 cert,
        string filePath)
    {
        var cerBytes = cert.Export(X509ContentType.Cert);
        File.WriteAllBytes(filePath, cerBytes);
    }

    /// <summary>
    /// Genera un fingerprint único para la máquina basado en
    /// CPU ID + Motherboard Serial + BIOS Serial.
    /// </summary>
    public static string GenerateMachineFingerprint(
        string processorId,
        string baseBoardSerial,
        string biosSerial)
    {
        var combined = $"{processorId}|{baseBoardSerial}|{biosSerial}";
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
