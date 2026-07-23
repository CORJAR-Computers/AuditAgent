using Microsoft.AspNetCore.Mvc;
using AuditAgent.Api.Services;
using System.Security.Cryptography;

namespace AuditAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly AuditStorageService _storage;
    private readonly EncryptionKeyService _keys;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        AuditStorageService storage,
        EncryptionKeyService keys,
        ILogger<AuditController> logger)
    {
        _storage = storage;
        _keys = keys;
        _logger = logger;
    }

    /// <summary>
    /// Recibe un reporte de auditoria cifrado de un agente.
    /// </summary>
    [HttpPost("submit")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB max
    public async Task<IActionResult> SubmitReport([FromBody] SubmitRequest request)
    {
        try
        {
            // 1. Validar que tenemos los campos requeridos
            if (string.IsNullOrEmpty(request.EncryptedReport) ||
                string.IsNullOrEmpty(request.Signature) ||
                string.IsNullOrEmpty(request.ReportHash))
            {
                return BadRequest(new { error = "Faltan campos requeridos." });
            }

            // 2. Descifrar el reporte con la clave AES maestra
            var jsonReport = _keys.Decrypt(request.EncryptedReport);

            // 3. Verificar hash de integridad
            var computedHash = AuditAgent.Core.Services.AuditOrchestrator
                .ComputeHashFromJson(jsonReport);
            if (!string.Equals(computedHash, request.ReportHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Hash mismatch. Esperado: {Expected}, Recibido: {Received}",
                    request.ReportHash, computedHash);
                return BadRequest(new { error = "Hash de integridad no coincide." });
            }

            // 4. Almacenar el reporte descifrado
            await _storage.StoreReportAsync(
                jsonReport,
                request.Signature,
                request.ReportHash);

            _logger.LogInformation("Reporte recibido y almacenado. Hash: {Hash}", request.ReportHash);

            return Ok(new
            {
                success = true,
                serverTimestamp = DateTime.UtcNow.ToString("O"),
                message = "Reporte recibido exitosamente."
            });
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _logger.LogError(ex, "Error al descifrar reporte.");
            return Unauthorized(new { error = "No se pudo descifrar el reporte." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar reporte.");
            return StatusCode(500, new { error = "Error interno del servidor." });
        }
    }

    /// <summary>
    /// Registra un nuevo agente en el sistema.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAgent([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.MachineFingerprint) ||
            string.IsNullOrEmpty(request.PublicKey))
        {
            return BadRequest(new { error = "Fingerprint y clave publica son requeridos." });
        }

        var agentId = await _storage.RegisterAgentAsync(
            request.MachineFingerprint,
            request.PublicKey);

        _logger.LogInformation("Agente registrado: {AgentId}", agentId);

        return Ok(new
        {
            agentId,
            serverPublicKey = "SERVER_PUBLIC_KEY_PLACEHOLDER",
            message = "Agente registrado exitosamente."
        });
    }

    /// <summary>
    /// Obtiene todos los reportes almacenados.
    /// </summary>
    [HttpGet("reports")]
    public async Task<IActionResult> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? computerName = null)
    {
        var (reports, total) = await _storage.GetReportsAsync(
            page, pageSize, computerName);

        return Ok(new { reports, total, page, pageSize });
    }

    /// <summary>
    /// Obtiene estadisticas generales de la flota.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _storage.GetFleetStatsAsync();
        return Ok(stats);
    }
}

public record SubmitRequest(
    string EncryptedReport,
    string Signature,
    string ReportHash,
    string? Timestamp);

public record RegisterRequest(
    string MachineFingerprint,
    string PublicKey,
    string? Timestamp);
