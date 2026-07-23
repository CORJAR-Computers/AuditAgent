using AuditAgent.Security;
using Xunit;

namespace AuditAgent.Tests.Security;

public class AesEncryptorTests
{
    private readonly AesEncryptor _encryptor = new();

    [Fact]
    public void EncryptDecrypt_Roundtrip_ReturnsOriginalData()
    {
        // Arrange
        var key = AesEncryptor.GenerateKey();
        var plainText = "Este es un reporte de auditoria confidencial.";

        // Act
        var encrypted = _encryptor.Encrypt(plainText, key);
        var decrypted = _encryptor.Decrypt(encrypted, key);

        // Assert
        Assert.Equal(plainText, decrypted);
        Assert.NotEqual(plainText, encrypted);
    }

    [Fact]
    public void Encrypt_DifferentCalls_ProduceDifferentCiphertext()
    {
        var key = AesEncryptor.GenerateKey();
        var plainText = "Datos de prueba";

        var encrypted1 = _encryptor.Encrypt(plainText, key);
        var encrypted2 = _encryptor.Encrypt(plainText, key);

        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void GenerateKey_Returns32Bytes()
    {
        var key = AesEncryptor.GenerateKey();
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void Decrypt_WrongKey_ThrowsCryptographicException()
    {
        var key1 = AesEncryptor.GenerateKey();
        var key2 = AesEncryptor.GenerateKey();
        var encrypted = _encryptor.Encrypt("datos", key1);

        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() =>
            _encryptor.Decrypt(encrypted, key2));
    }

    [Fact]
    public void DeriveKeyFromPassword_ProducesValidKey()
    {
        var password = "MiClaveSecreta123!";
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);

        var key = AesEncryptor.DeriveKeyFromPassword(password, salt);

        Assert.Equal(32, key.Length);

        // Verificar que la clave funciona para cifrado
        var encryptor = new AesEncryptor();
        var encrypted = encryptor.Encrypt("test", key);
        var decrypted = encryptor.Decrypt(encrypted, key);
        Assert.Equal("test", decrypted);
    }
}
