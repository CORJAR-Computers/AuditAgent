using System.Security.Cryptography;

namespace AuditAgent.Api.Services;

/// <summary>
/// Servicio de gestion de claves de cifrado del servidor.
/// Genera y gestiona la clave AES-256 maestra.
/// </summary>
public class EncryptionKeyService
{
    private readonly byte[] _masterKey;
    private readonly AesEncryptor _encryptor;

    public EncryptionKeyService(IConfiguration config)
    {
        var keyPath = config["Encryption:MasterKeyPath"] 
            ?? Path.Combine(AppContext.BaseDirectory, "keys", "master-aes-key.bin");

        if (File.Exists(keyPath))
        {
            _masterKey = File.ReadAllBytes(keyPath);
            if (_masterKey.Length != 32)
                throw new InvalidOperationException("La clave maestra debe tener 32 bytes (AES-256).");
        }
        else
        {
            _masterKey = AesEncryptor.GenerateKey();
            Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
            File.WriteAllBytes(keyPath, _masterKey);
            Console.WriteLine($"Clave maestra generada: {keyPath}");
            Console.WriteLine("IMPORTANTE: Guarde esta clave en un lugar seguro!");
        }

        _encryptor = new AesEncryptor();
    }

    public string Encrypt(string plainData) 
        => _encryptor.Encrypt(plainData, _masterKey);

    public string Decrypt(string encryptedData) 
        => _encryptor.Decrypt(encryptedData, _masterKey);
}
