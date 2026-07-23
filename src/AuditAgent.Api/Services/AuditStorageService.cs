using System.Security.Cryptography;
using System.Text.Json;

namespace AuditAgent.Api.Services;

/// <summary>
/// Servicio de almacenamiento de reportes de auditoria.
/// Para produccion, reemplazar con Entity Framework + SQL Server/PostgreSQL.
/// Esta implementacion guarda en archivos JSON como demo.
/// </summary>
public class AuditStorageService
{
    private readonly string _storagePath;
    private readonly string _agentsPath;
    private readonly ILogger<AuditStorageService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public AuditStorageService(ILogger<AuditStorageService> logger)
    {
        _logger = logger;
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
            // Extraer nombre del equipo del JSON
            using var doc = JsonDocument.Parse(jsonReport);
            var computerName = doc.RootElement
                .GetProperty("computer")
                .GetProperty("computerName")
                .GetString() ?? "unknown";

            var filename = $"{computerName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{reportHash.Substring(0, 8)}.json";
            var filepath = Path.Combine(_storagePath, filename);

            // Guardar metadatos + reporte
            var envelope = new
            {
                receivedAt = DateTime.UtcNow.ToString("O"),
                reportHash,
                signature,
                sourceIp = "TODO", // Se llena desde middleware
                report = JsonDocument.Parse(jsonReport).RootElement
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(
                filepath,
                JsonSerializer.Serialize(envelope, options));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task<string> RegisterAgentAsync(string fingerprint, string publicKey)
    {
        var agentId = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var agentFile = Path.Combine(_agentsPath, $"{agentId}.json");

        var agentData = new
        {
            agentId,
            fingerprint,
            publicKey,
            registeredAt = DateTime.UtcNow.ToString("O"),
            lastSeenAt = DateTime.UtcNow.ToString("O")
        };

        File.WriteAllText(
            agentFile,
            JsonSerializer.Serialize(agentData, new JsonSerializerOptions { WriteIndented = true }));

        return Task.FromResult(agentId);
    }

    public async Task<(List<object> reports, int total)> GetReportsAsync(
        int page, int pageSize, string? computerName)
    {
        var files = Directory.GetFiles(_storagePath, "*.json")
            .OrderByDescending(f => f)
            .ToList();

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
            storagePath = _storagePath,
            lastReportAt = files.Length > 0
                ? File.GetLastWriteTime(files.OrderByDescending(f => f).First()).ToString("O")
                : null
        };
    }
}
