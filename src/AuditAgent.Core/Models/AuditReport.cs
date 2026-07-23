namespace AuditAgent.Core.Models;

/// <summary>
/// Reporte de auditoría completo que contiene toda la información
/// recolectada de un equipo. Este es el objeto principal que se
/// serializa, cifra y envía al servidor central.
/// </summary>
public class AuditReport
{
    /// <summary>Versión del formato del reporte (para compatibilidad futura).</summary>
    public string ReportVersion { get; set; } = "1.0.0";

    /// <summary>Versión del agente que generó este reporte.</summary>
    public string AgentVersion { get; set; } = string.Empty;

    /// <summary>Información general del equipo.</summary>
    public ComputerInfo Computer { get; set; } = new();

    /// <summary>Información detallada de hardware.</summary>
    public HardwareInfo Hardware { get; set; } = new();

    /// <summary>Sistema operativo.</summary>
    public OperatingSystemInfo OperatingSystem { get; set; } = new();

    /// <summary>Lista completa de software instalado.</summary>
    public List<SoftwareInfo> InstalledSoftware { get; set; } = new();

    /// <summary>Lista de actualizaciones/parches de seguridad.</summary>
    public List<SecurityPatchInfo> SecurityPatches { get; set; } = new();

    /// <summary>Adaptadores de red.</summary>
    public List<NetworkAdapterInfo> NetworkAdapters { get; set; } = new();

    /// <summary>Firma digital del reporte (RSA-SHA256 en base64).</summary>
    public string? DigitalSignature { get; set; }

    /// <summary>Hash SHA-256 del reporte (para integridad).</summary>
    public string? ReportHash { get; set; }

    /// <summary>Duración total de la auditoría en milisegundos.</summary>
    public long AuditDurationMs { get; set; }

    /// <summary>Lista de errores o advertencias durante la auditoría.</summary>
    public List<AuditWarning> Warnings { get; set; } = new();
}

/// <summary>
/// Advertencia o error no fatal ocurrido durante la auditoría.
/// </summary>
public class AuditWarning
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}