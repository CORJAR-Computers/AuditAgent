using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AuditAgent.Core.Interfaces;
using AuditAgent.Core.Models;

namespace AuditAgent.Core.Services;

/// <summary>
/// Orquestador principal de la auditoría.
/// Coordina todos los recolectores, ensambla el reporte,
/// lo firma digitalmente y lo prepara para envío.
/// </summary>
public class AuditOrchestrator
{
    private readonly IEnumerable<ICollector> _collectors;
    private readonly ISigner? _signer;
    private readonly ILogger? _logger;
    private RSA? _signingKey;

    public AuditOrchestrator(
        IEnumerable<ICollector> collectors,
        ISigner? signer = null,
        RSA? signingKey = null,
        ILogger? logger = null)
    {
        _collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
        _signer = signer;
        _signingKey = signingKey;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la auditoría completa: recolecta, firma y genera el reporte.
    /// </summary>
    public async Task<AuditReport> ExecuteAuditAsync(CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = new AuditReport();

        _logger?.LogInformation("Iniciando auditoría {AuditId}...", report.Computer.AuditId);

        // Ejecutar todos los recolectores en paralelo
        var tasks = _collectors.Select(c => SafeCollectAsync(c, report, ct));
        await Task.WhenAll(tasks);

        stopwatch.Stop();
        report.AuditDurationMs = stopwatch.ElapsedMilliseconds;

        // Calcular hash del reporte
        report.ReportHash = ComputeHash(report);

        // Firmar digitalmente si hay clave disponible
        if (_signer != null && _signingKey != null)
        {
            try
            {
                var serialized = SerializeReport(report);
                report.DigitalSignature = _signer.Sign(serialized, _signingKey);
                _logger?.LogInformation("Reporte firmado digitalmente exitosamente.");
            }
            catch (Exception ex)
            {
                report.Warnings.Add(new AuditWarning
                {
                    Category = "Signing",
                    Message = "No se pudo firmar el reporte digitalmente.",
                    Details = ex.Message
                });
                _logger?.LogWarning(ex, "Error al firmar el reporte.");
            }
        }

        _logger?.LogInformation(
            "Auditoría completada en {Duration}ms. Software detectado: {Count}",
            report.AuditDurationMs,
            report.InstalledSoftware.Count);

        return report;
    }

    /// <summary>
    /// Serializa el reporte a JSON (sin la firma para poder verificarla después).
    /// </summary>
    public string SerializeReport(AuditReport report)
    {
        // Clonar para excluir la firma de la serialización
        var clone = new AuditReport
        {
            ReportVersion = report.ReportVersion,
            AgentVersion = report.AgentVersion,
            Computer = report.Computer,
            Hardware = report.Hardware,
            OperatingSystem = report.OperatingSystem,
            InstalledSoftware = report.InstalledSoftware,
            SecurityPatches = report.SecurityPatches,
            NetworkAdapters = report.NetworkAdapters,
            DigitalSignature = null,
            ReportHash = report.ReportHash,
            AuditDurationMs = report.AuditDurationMs,
            Warnings = report.Warnings
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(clone, options);
    }

    /// <summary>
    /// Calcula el hash SHA-256 del reporte serializado.
    /// </summary>
    public static string ComputeHash(AuditReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var json = JsonSerializer.Serialize(report, options);
        return ComputeHashFromJson(json);
    }

    public static string ComputeHashFromJson(string json)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task SafeCollectAsync(ICollector collector, AuditReport report, CancellationToken ct)
    {
        try
        {
            _logger?.LogDebug("Ejecutando recolector: {Name}...", collector.CollectorName);
            await collector.CollectAsync(report, ct);
            _logger?.LogDebug("Recolector {Name} completado.", collector.CollectorName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            report.Warnings.Add(new AuditWarning
            {
                Category = "Collector",
                Message = $"Error en recolector '{collector.CollectorName}'.",
                Details = ex.Message
            });
            _logger?.LogError(ex, "Error en recolector {Name}", collector.CollectorName);
        }
    }
}

/// <summary>Interfaz de logging simple para desacoplar de Microsoft.Extensions.Logging.</summary>
public interface ILogger
{
    void LogInformation(string message, params object?[] args);
    void LogWarning(Exception? ex, string message, params object?[] args);
    void LogWarning(string message, params object?[] args);
    void LogError(Exception ex, string message, params object?[] args);
    void LogDebug(string message, params object?[] args);
}
