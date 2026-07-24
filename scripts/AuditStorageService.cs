using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AuditAgent.Api.Services;

/// <summary>
/// Servicio de almacenamiento de reportes de auditoria.
/// Para produccion, reemplazar con Entity Framework + SQL Server/PostgreSQL.
/// FIX: Ahora obtiene la IP real del cliente via IHttpContextAccessor.
/// </summary>
public class AuditStorageService
{
    private readonly string _storagePath;
    private readonly string _agentsPath;
    private readonly ILogger<AuditStorageService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public AuditStorageService(
        ILogger<AuditStorageService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _storagePath = Path.Combine(AppContext.BaseDirectory, "data", "reports");
        _agentsPath = Path.Combine(AppContext.BaseDirectory, "data", "agents");
        Directory.CreateDirectory(_storagePath);
        Directory.CreateDirectory(_agentsPath);
    }

    public async Task StoreReportAsync(
        string jsonReport,
        string signature,
        string reportHash)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var doc = JsonDocument.Parse(jsonReport);
            var computerName = doc.RootElement
                .GetProperty("computer")
                .GetProperty("computerName")
                .GetString() ?? "unknown";

            var filename = $"{computerName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{reportHash[..8]}.json";
            var filepath = Path.Combine(_storagePath, filename);

            // FIX: Obtener IP real del cliente
            var sourceIp = GetClientIp();

            var envelope = new
            {
                receivedAt = DateTime.UtcNow.ToString("O"),
                reportHash,
                signature,
                sourceIp,
                report = JsonDocument.Parse(jsonReport).RootElement
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(filepath, JsonSerializer.Serialize(envelope, options));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// FIX: Busca agentes existentes por fingerprint antes de crear uno nuevo.
    /// Si ya existe, actualiza lastSeenAt y retorna el agentId existente.
    /// </summary>
    public Task<string> RegisterAgentAsync(string fingerprint, string publicKey)
    {
        // Buscar agente existente por fingerprint
        var existingFiles = Directory.GetFiles(_agentsPath, "*.json");
        foreach (var file in existingFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("fingerprint", out var fp) &&
                    fp.GetString()?.Equals(fingerprint, StringComparison.OrdinalIgnoreCase) == true)
                {
                    // FIX: Actualizar lastSeenAt en el archivo existente
                    var agentId = doc.RootElement.GetProperty("agentId").GetString()!;
                    var existingData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
                    existingData["lastSeenAt"] = JsonSerializer.SerializeToElement(DateTime.UtcNow.ToString("O"));
                    File.WriteAllText(file, JsonSerializer.Serialize(existingData, new JsonSerializerOptions { WriteIndented = true }));
                    _logger.LogInformation("Agente existente reconnectado: {AgentId}", agentId);
                    return Task.FromResult(agentId);
                }
            }
            catch { /* archivo corrupto, ignorar */ }
        }

        // Nuevo agente
        var newAgentId = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var agentFile = Path.Combine(_agentsPath, $"{newAgentId}.json");

        var agentData = new
        {
            agentId = newAgentId,
            fingerprint,
            publicKey,
            registeredAt = DateTime.UtcNow.ToString("O"),
            lastSeenAt = DateTime.UtcNow.ToString("O")
        };

        File.WriteAllText(agentFile,
            JsonSerializer.Serialize(agentData, new JsonSerializerOptions { WriteIndented = true }));

        return Task.FromResult(newAgentId);
    }

    public async Task<(List<object> reports, int total)> GetReportsAsync(
        int page, int pageSize, string? computerName)
    {
        var files = Directory.GetFiles(_storagePath, "*.json")
            .OrderByDescending(f => f).ToList();

        if (!string.IsNullOrEmpty(computerName))
            files = files.Where(f => f.Contains(computerName, StringComparison.OrdinalIgnoreCase)).ToList();

        var total = files.Count;
        var paged = files.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var reports = new List<object>();
        foreach (var file in paged)
        {
            var json = await File.ReadAllTextAsync(file);
            reports.Add(JsonSerializer.Deserialize<object>(json)!);
        }

        return (reports, total);
    }

    public async Task<object> GetFleetStatsAsync()
    {
        var files = Directory.GetFiles(_storagePath, "*.json");
        var agents = Directory.GetFiles(_agentsPath, "*.json");

        return new
        {
            totalReports = files.Length,
            totalAgents = agents.Length,
            lastReportAt = files.Length > 0
                ? File.GetLastWriteTime(files.OrderByDescending(f => f).First()).ToString("O")
                : null
        };
    }

    /// <summary>
    /// Obtiene la IP real del cliente (soporta proxys).
    /// </summary>
    private string GetClientIp()
    {
        var http = _httpContextAccessor.HttpContext;
        if (http == null) return "unknown";

        // Intentar obtener de headers de proxy primero
        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',').First().Trim();

        var realIp = http.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
            return realIp;

        return http.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
    }
}