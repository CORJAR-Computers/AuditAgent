using AuditAgent.Security;
using Xunit;

namespace AuditAgent.Tests.Security;

public class RsaSignerTests
{
    private readonly RsaSigner _signer = new();

    [Fact]
    public void SignAndVerify_ValidSignature_ReturnsTrue()
    {
        var (privKey, pubKey) = RsaSigner.GenerateKeyPair();
        var data = "{\"computerName\": \"PC-TEST\"}";

        var signature = _signer.Sign(data, privKey);
        var isValid = _signer.Verify(data, signature, pubKey);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_TamperedData_ReturnsFalse()
    {
        var (privKey, pubKey) = RsaSigner.GenerateKeyPair();
        var data = "datos originales";
        var signature = _signer.Sign(data, privKey);

        var tampered = "datos modificados";
        var isValid = _signer.Verify(tampered, signature, pubKey);

        Assert.False(isValid);
    }

    [Fact]
    public void GenerateKeyPair_Creates4096BitKeys()
    {
        var (priv, pub) = RsaSigner.GenerateKeyPair();
        Assert.Equal(4096, priv.KeySize);
    }

    [Fact]
    public void ExportImport_PublicKey_MaintainsValidity()
    {
        var (_, pubKey) = RsaSigner.GenerateKeyPair();
        var pem = RsaSigner.ExportPublicKeyPem(pubKey);

        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", pem);
        Assert.EndsWith("-----END PUBLIC KEY-----", pem);

        var imported = RsaSigner.ImportPublicKeyFromPem(pem);
        Assert.NotNull(imported);
    }
}
