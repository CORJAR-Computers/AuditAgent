namespace AuditAgent.Core.Models;

/// <summary>
/// Información detallada del hardware del equipo.
/// Recolectada vía múltiples clases WMI.
/// </summary>
public class HardwareInfo
{
    // ── Procesador ──────────────────────────────────────────────
    public List<ProcessorInfo> Processors { get; set; } = new();

    // ── Memoria RAM ────────────────────────────────────────────
    public List<MemoryInfo> MemoryModules { get; set; } = new();

    /// <summary>Memoria RAM total en GB.</summary>
    public double TotalMemoryGb { get; set; }

    // ── Discos ─────────────────────────────────────────────────
    public List<DiskInfo> Disks { get; set; } = new();

    // ── Placas de video ────────────────────────────────────────
    public List<GpuInfo> Gpus { get; set; } = new();

    // ── Placa madre / BIOS ─────────────────────────────────────
    public string BiosVersion { get; set; } = string.Empty;
    public string BiosManufacturer { get; set; } = string.Empty;
    public string BiosReleaseDate { get; set; } = string.Empty;
    public string BaseBoardManufacturer { get; set; } = string.Empty;
    public string BaseBoardProduct { get; set; } = string.Empty;

    // ── Batería (solo laptops) ─────────────────────────────────
    public BatteryInfo? Battery { get; set; }
}

public class ProcessorInfo
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public int NumberOfCores { get; set; }
    public int NumberOfLogicalProcessors { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public double MaxClockSpeedMhz { get; set; }
    public string SocketDesignation { get; set; } = string.Empty;
    public string ProcessorId { get; set; } = string.Empty;
}

public class MemoryInfo
{
    public string Manufacturer { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public string MemoryType { get; set; } = string.Empty;
    public double CapacityGb { get; set; }
    public string BankLabel { get; set; } = string.Empty;
    public string FormFactor { get; set; } = string.Empty;
}

public class DiskInfo
{
    public string Model { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty; // Fixed, Removable, SSD
    public string InterfaceType { get; set; } = string.Empty; // SATA, NVMe, USB
    public long SizeGb { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string FirmwareRevision { get; set; } = string.Empty;
    public string PartitionStyle { get; set; } = string.Empty;
    public List<PartitionInfo> Partitions { get; set; } = new();
}

public class PartitionInfo
{
    public string DriveLetter { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public long SizeGb { get; set; }
    public long FreeSpaceGb { get; set; }
    public string VolumeName { get; set; } = string.Empty;
}

public class GpuInfo
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public string DriverDate { get; set; } = string.Empty;
    public long AdapterRamMb { get; set; }
    public string VideoModeDescription { get; set; } = string.Empty;
}

public class BatteryInfo
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int EstimatedChargeRemaining { get; set; }
    public string DesignCapacity { get; set; } = string.Empty;
    public string FullChargeCapacity { get; set; } = string.Empty;
    public string ExpectedLife { get; set; } = string.Empty;
}