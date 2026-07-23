using System.Security.Cryptography;
using System.Text;
using AuditAgent.Core.Interfaces;

namespace AuditAgent.Security;

/// <summary>
/// Implementación de cifrado AES-256-GCM (Galois/Counter Mode).
/// 
/// GCM proporciona:
/// - Cifrado autenticado (confidencialidad + integridad + autenticidad)
/// - Protección contra ataques de reordenamiento y replay
/// - No requiere un HMAC separado
/// 
/// Formato del ciphertext (base64):
/// [12 bytes: Nonce/IV] [resto: Ciphertext + 16 bytes: AuthTag]
/// </summary>
public class AesEncryptor : IEncryptor
{
    private const int KeySizeBytes = 32;       // 256 bits
    private const int NonceSizeBytes = 12;      // 96 bits (recomendado para GCM)
    private const int TagSizeBytes = 16;        // 128 bits

    /// <summary>
    /// Cifra datos usando AES-256-GCM.
    /// Genera un nonce aleatorio para cada cifrado.
    /// </summary>
    /// <param name="plainData">Texto plano a cifrar.</param>
    /// <param name="key">Clave AES-256 (exactamente 32 bytes).</param>
    /// <returns>Base64 con formato: [Nonce][Ciphertext+Tag]</returns>
    public string Encrypt(string plainData, byte[] key)
    {
        ValidateKey(key);

        var plaintextBytes = Encoding.UTF8.GetBytes(plainData);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Combinar: nonce + ciphertext + tag
        var result = new byte[NonceSizeBytes + ciphertext.Length + TagSizeBytes];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSizeBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes + ciphertext.Length, TagSizeBytes);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Descifra datos cifrados con AES-256-GCM.
    /// </summary>
    /// <param name="encryptedData">Base64 con formato: [Nonce][Ciphertext+Tag]</param>
    /// <param name="key">Clave AES-256 (exactamente 32 bytes).</param>
    /// <returns>Texto plano descifrado.</returns>
    public string Decrypt(string encryptedData, byte[] key)
    {
        ValidateKey(key);

        var fullData = Convert.FromBase64String(encryptedData);

        if (fullData.Length < NonceSizeBytes + TagSizeBytes)
            throw new CryptographicException("Datos cifrados inválidos: demasiado cortos.");

        var nonce = new byte[NonceSizeBytes];
        var tag = new byte[TagSizeBytes];
        var ciphertext = new byte[fullData.Length - NonceSizeBytes - TagSizeBytes];

        Buffer.BlockCopy(fullData, 0, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(fullData, NonceSizeBytes, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(fullData, NonceSizeBytes + ciphertext.Length, tag, 0, TagSizeBytes);

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Genera una clave AES-256 aleatoria segura.
    /// </summary>
    public static byte[] GenerateKey()
    {
        return RandomNumberGenerator.GetBytes(KeySizeBytes);
    }

    /// <summary>
    /// Deriva una clave AES-256 a partir de una contraseña usando PBKDF2.
    /// </summary>
    /// <param name="password">Contraseña.</param>
    /// <param name="salt">Salt (mínimo 16 bytes).</param>
    /// <param name="iterations">Iteraciones (mínimo 600,000 para 2024+).</param>
    public static byte[] DeriveKeyFromPassword(string password, byte[] salt, int iterations = 600_000)
    {
        if (salt.Length < 16)
            throw new ArgumentException("El salt debe tener al menos 16 bytes.", nameof(salt));
        if (iterations < 100_000)
            throw new ArgumentException("Mínimo 100,000 iteraciones.", nameof(iterations));

        using var pbkdf2 = new Rfc2898DeriveBytes(
            password, salt, iterations, HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(KeySizeBytes);
    }

    private static void ValidateKey(byte[] key)
    {
        if (key is null || key.Length != KeySizeBytes)
            throw new ArgumentException(
                $"La clave AES-256 debe tener exactamente {KeySizeBytes} bytes.", nameof(key));
    }
}
