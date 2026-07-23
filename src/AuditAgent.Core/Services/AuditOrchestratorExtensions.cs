using System.Text.Json;

namespace AuditAgent.Core.Services;

/// <summary>
/// Metodos de extension para AuditOrchestrator.
/// </summary>
public static class AuditOrchestratorExtensions
{
    /// <summary>
    /// Calcula el hash SHA-256 de un JSON string.
    /// </summary>
    public static string ComputeHashFromJson(string json)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
