using System.Security.Cryptography;
using AuditAgent.Core.Models;

namespace AuditAgent.Core.Interfaces;

/// <summary>
/// Interfaz base para todos los recolectores de datos.
/// Cada recolector obtiene información de una categoría específica.
/// </summary>
public interface ICollector
{
    /// <summary>Nombre descriptivo del recolector.</summary>
    string CollectorName { get; }

    /// <summary>
    /// Ejecuta la recolección de datos.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task CollectAsync(AuditReport report, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interfaz para servicios de cifrado simétrico (AES-256).
/// </summary>
public interface IEncryptor
{
    /// <summary>Cifra datos usando AES-256-GCM.</summary>
    /// <param name="plainData">Datos en texto plano.</param>
    /// <param name="key">Clave de cifrado (32 bytes para AES-256).</param>
    /// <returns>Datos cifrados en base64 con IV prefijado.</returns>
    string Encrypt(string plainData, byte[] key);

    /// <summary>Descifra datos cifrados con AES-256-GCM.</summary>
    string Decrypt(string encryptedData, byte[] key);
}

/// <summary>
/// Interfaz para firmado digital RSA-SHA256.
/// </summary>
public interface ISigner
{
    /// <summary>Genera firma RSA-SHA256 del reporte serializado.</summary>
    string Sign(string data, RSA privateKey);

    /// <summary>Verifica la firma de un reporte.</summary>
    bool Verify(string data, string signature, RSA publicKey);
}

/// <summary>
/// Interfaz para comunicación segura con el servidor central.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Envía el reporte de auditoría cifrado al servidor central.
    /// </summary>
    /// <param name="encryptedReport">Reporte cifrado en base64.</param>
    /// <param name="signature">Firma digital del reporte.</param>
    /// <param name="reportHash">Hash SHA-256 del reporte original.</param>
    /// <returns>True si el servidor aceptó el reporte.</returns>
    Task<ApiSubmissionResult> SubmitReportAsync(
        string encryptedReport,
        string signature,
        string reportHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra el agente en el servidor central y obtiene el certificado/fingerprint.
    /// </summary>
    Task<RegistrationResult> RegisterAgentAsync(
        string machineFingerprint,
        string publicKeyPem,
        CancellationToken cancellationToken = default);
}

public class ApiSubmissionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ServerTimestamp { get; set; }
    public int HttpStatusCode { get; set; }
}

public class RegistrationResult
{
    public bool Success { get; set; }
    public string? AgentId { get; set; }
    public string? ServerPublicKey { get; set; }
    public string? ErrorMessage { get; set; }
}