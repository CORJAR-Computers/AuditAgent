namespace AuditAgent.Core.Models;

/// <summary>
/// Información de un software instalado en el equipo.
/// Recolectada desde el Registry y WMI Win32_Product.
/// </summary>
public class SoftwareInfo
{
    /// <summary>Nombre del software.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Versión del software.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Fabricante/desarrollador.</summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>Fecha de instalación (si está disponible).</summary>
    public string InstallDate { get; set; } = string.Empty;

    /// <summary>Tamaño estimado de instalación en MB.</summary>
    public decimal? EstimatedSizeMb { get; set; }

    /// <summary>Ruta de instalación.</summary>
    public string InstallLocation { get; set; } = string.Empty;

    /// <summary>Ruta del ejecutable principal o desinstalador.</summary>
    public string UninstallString { get; set; } = string.Empty;

    /// <summary>Identificador único del software en el sistema.</summary>
    public string RegistryKey { get; set; } = string.Empty;

    /// <summary>Fuente de detección (Registry, WMI, etc.).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Architecture (x64, x86).</summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>Si es un update/patch en lugar de un programa completo.</summary>
    public bool IsUpdate { get; set; }

    /// <summary>Si el usuario puede modificar o reparar la instalación.</summary>
    public bool IsModifiable { get; set; }

    /// <summary>Nombre legible para reportes.</summary>
    public string DisplayName => string.IsNullOrEmpty(Name) ? "(Sin nombre)" : Name;
}

/// <summary>
/// Información del sistema operativo.
/// </summary>
public class OperatingSystemInfo
{
    public string Caption { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string BuildNumber { get; set; } = string.Empty;
    public string OSArchitecture { get; set; } = string.Empty;
    public string ProductKey { get; set; } = string.Empty; // Parcialmente enmascarada
    public string Organization { get; set; } = string.Empty;
    public string RegisteredUser { get; set; } = string.Empty;
    public DateTime? InstallDate { get; set; }
    public DateTime? LastBootUpTime { get; set; }
    public string WindowsDirectory { get; set; } = string.Empty;
    public string SystemDirectory { get; set; } = string.Empty;
    public int ProcessCount { get; set; }
}

/// <summary>
/// Información de parches/actualizaciones de seguridad.
/// </summary>
public class SecurityPatchInfo
{
    public string HotFixId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InstalledOn { get; set; } = string.Empty;
    public string InstalledBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Información de adaptador de red.
/// </summary>
public class NetworkAdapterInfo
{
    public string Name { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public List<string> IpAddresses { get; set; } = new();
    public string DnsServer { get; set; } = string.Empty;
    public string DefaultGateway { get; set; } = string.Empty;
    public string DhcpEnabled { get; set; } = string.Empty;
    public string DhcpServer { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string ConnectionStatus { get; set; } = string.Empty;
    public string AdapterType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string DnsSuffix { get; set; } = string.Empty;
}