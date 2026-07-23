namespace AuditAgent.Core.Models;

/// <summary>
/// Información general del equipo auditado.
/// Recolectada vía WMI Win32_ComputerSystem y Win32_BIOS.
/// </summary>
public class ComputerInfo
{
    /// <summary>Nombre NetBIOS del equipo.</summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>Fabricante del equipo (Dell, HP, Lenovo, etc.).</summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>Modelo del equipo.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Número de serie de la BIOS/equipo.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>SMBIOS Asset Tag si está configurado por la empresa.</summary>
    public string AssetTag { get; set; } = string.Empty;

    /// <summary>Tipo de sistema (Desktop, Laptop, Server).</summary>
    public string SystemType { get; set; } = string.Empty;

    /// <summary>Identificador único generado para esta auditoría (GUID).</summary>
    public string AuditId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Fecha y hora UTC en que se ejecutó la auditoría.</summary>
    public DateTime AuditTimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Dominio o grupo de trabajo al que pertenece el equipo.</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Nombre del usuario actualmente logueado.</summary>
    public string CurrentUser { get; set; } = string.Empty;

    /// <summary>Dirección MAC principal (para identificar el equipo en la red).</summary>
    public string PrimaryMacAddress { get; set; } = string.Empty;

    /// <summary>UUID del sistema hardware.</summary>
    public string SystemUuid { get; set; } = string.Empty;
}