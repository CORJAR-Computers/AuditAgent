using System.Security.Cryptography;
using System.Text;
using AuditAgent.Core.Interfaces;

namespace AuditAgent.Security;

/// <summary>
/// Implementación de firma digital RSA-SHA256 (RSASSA-PKCS1-v1_5).
/// 
/// Flujo de seguridad:
/// 1. El agente genera un par de claves RSA-4096 al instalarse
/// 2. El agente envía la clave pública al servidor central al registrarse
/// 3. Cada reporte se firma con la clave privada del agente
/// 4. El servidor verifica la firma con la clave pública registrada
/// 
/// Esto garantiza:
/// - Autenticidad: solo el agente legítimo pudo generar el reporte
/// - Integridad: el reporte no fue modificado en tránsito
/// - No repudio: el agente no puede negar haber enviado el reporte
/// </summary>
public class RsaSigner : ISigner
{
    private const int KeySizeBits = 4096;
    private static readonly HashAlgorithmName SignatureHash = HashAlgorithmName.SHA256;

    /// <summary>
    /// Firma los datos usando RSA-SHA256.
    /// </summary>
    /// <param name="data">Datos a firmar (reporte JSON serializado).</param>
    /// <param name="privateKey">Clave privada RSA del agente.</param>
    /// <returns>Firma digital en base64.</returns>
    public string Sign(string data, RSA privateKey)
    {
        if (privateKey is null)
            throw new ArgumentNullException(nameof(privateKey));

        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signatureBytes = privateKey.SignData(dataBytes, SignatureHash, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signatureBytes);
    }

    /// <summary>
    /// Verifica la firma de los datos.
    /// </summary>
    /// <param name="data">Datos originales.</param>
    /// <param name="signature">Firma digital en base64.</param>
    /// <param name="publicKey">Clave pública RSA del agente.</param>
    /// <returns>True si la firma es válida.</returns>
    public bool Verify(string data, string signature, RSA publicKey)
    {
        if (publicKey is null) throw new ArgumentNullException(nameof(publicKey));
        if (string.IsNullOrEmpty(signature)) throw new ArgumentException("Firma vacía.", nameof(signature));

        try
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = Convert.FromBase64String(signature);

            return publicKey.VerifyData(dataBytes, signatureBytes, SignatureHash, RSASignaturePadding.Pkcs1);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Genera un nuevo par de claves RSA-4096.
    /// </summary>
    /// <returns>Tupla con (clave privada, clave pública).</returns>
    public static (RSA PrivateKey, RSA PublicKey) GenerateKeyPair()
    {
        var rsa = RSA.Create(KeySizeBits);
        var publicKey = RSA.Create();
        publicKey.ImportRSAPublicKey(rsa.ExportRSAPublicKey(), out _);
        return (rsa, publicKey);
    }

    /// <summary>
    /// Exporta una clave pública RSA a formato PEM (texto).
    /// </summary>
    public static string ExportPublicKeyPem(RSA publicKey)
    {
        var base64 = Convert.ToBase64String(publicKey.ExportRSAPublicKey());
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("-----BEGIN PUBLIC KEY-----");

        for (var i = 0; i < base64.Length; i += 64)
        {
            var lineLength = Math.Min(64, base64.Length - i);
            sb.AppendLine(base64.Substring(i, lineLength));
        }

        sb.Append("-----END PUBLIC KEY-----");
        return sb.ToString();
    }

    /// <summary>
    /// Exporta una clave privada RSA a formato PEM (texto).
    /// ADVERTENCIA: Solo para uso del agente. Nunca compartir.
    /// </summary>
    public static string ExportPrivateKeyPem(RSA privateKey)
    {
        var base64 = Convert.ToBase64String(privateKey.ExportRSAPrivateKey());
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("-----BEGIN PRIVATE KEY-----");

        for (var i = 0; i < base64.Length; i += 64)
        {
            var lineLength = Math.Min(64, base64.Length - i);
            sb.AppendLine(base64.Substring(i, lineLength));
        }

        sb.Append("-----END PRIVATE KEY-----");
        return sb.ToString();
    }

    /// <summary>
    /// Importa una clave pública desde formato PEM.
    /// </summary>
    public static RSA ImportPublicKeyFromPem(string pem)
    {
        var rsa = RSA.Create();
        // Extraer solo el contenido base64 del PEM
        var base64 = pem
            .Replace("-----BEGIN PUBLIC KEY-----", "")
            .Replace("-----END PUBLIC KEY-----", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Trim();

        rsa.ImportRSAPublicKey(Convert.FromBase64String(base64), out _);
        return rsa;
    }

    /// <summary>
    /// Importa una clave privada desde formato PEM.
    /// </summary>
    public static RSA ImportPrivateKeyFromPem(string pem)
    {
        var rsa = RSA.Create();
        var base64 = pem
            .Replace("[REDACTED:ssh_private_key]", "")
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("-----END RSA PRIVATE KEY-----", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Trim();

        try
        {
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(base64), out _);
        }
        catch
        {
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(base64), out _);
        }

        return rsa;
    }
}