using System.Management;
using AuditAgent.Core.Interfaces;
using AuditAgent.Core.Models;

namespace AuditAgent.Collectors;

/// <summary>
/// Recolector de información detallada de hardware:
/// procesadores, memoria RAM, discos, GPU y batería.
/// </summary>
public class HardwareCollector : ICollector
{
    public string CollectorName => "HardwareCollector";

    public Task CollectAsync(AuditReport report, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        CollectProcessors(report);
        CollectMemory(report);
        CollectDisks(report);
        CollectGpus(report);
        CollectBattery(report);

        return Task.CompletedTask;
    }

    // ── Procesadores ────────────────────────────────────────────
    private void CollectProcessors(AuditReport report)
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");

        foreach (var mo in searcher.Get())
        {
            var proc = new ProcessorInfo
            {
                Name = SafeGet(mo, "Name"),
                Manufacturer = SafeGet(mo, "Manufacturer"),
                NumberOfCores = SafeGetInt(mo, "NumberOfCores"),
                NumberOfLogicalProcessors = SafeGetInt(mo, "NumberOfLogicalProcessors"),
                MaxClockSpeedMhz = SafeGetUint(mo, "MaxClockSpeed"),
                SocketDesignation = SafeGet(mo, "SocketDesignation"),
                ProcessorId = SafeGet(mo, "ProcessorId")
            };

            // Interpretar DataWidth para arquitectura
            var dataWidth = SafeGetInt(mo, "DataWidth");
            proc.Architecture = dataWidth switch
            {
                64 => "x64",
                32 => "x86",
                _ => $"{dataWidth}-bit"
            };

            report.Hardware.Processors.Add(proc);
        }
    }

    // ── Memoria RAM ─────────────────────────────────────────────
    private void CollectMemory(AuditReport report)
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
        double totalGb = 0;

        foreach (var mo in searcher.Get())
        {
            var capacityBytes = SafeGetUlong(mo, "Capacity");
            var capacityGb = Math.Round(capacityBytes / (1024.0 * 1024.0 * 1024.0), 2);
            totalGb += capacityGb;

            var memoryType = GetMemoryType(SafeGetUint(mo, "SMBIOSMemoryType"));
            var formFactor = GetFormFactor(SafeGetUint(mo, "FormFactor"));

            var mem = new MemoryInfo
            {
                Manufacturer = SafeGet(mo, "Manufacturer"),
                PartNumber = SafeGet(mo, "PartNumber"),
                Speed = $"{SafeGetUint(mo, "Speed")} MHz",
                MemoryType = memoryType,
                CapacityGb = capacityGb,
                BankLabel = SafeGet(mo, "BankLabel"),
                FormFactor = formFactor
            };

            report.Hardware.MemoryModules.Add(mem);
        }

        report.Hardware.TotalMemoryGb = Math.Round(totalGb, 2);

        // Fallback: usar Win32_ComputerSystem si no se pudo leer
        if (totalGb == 0)
        {
            using var csSearcher = new ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var mo in csSearcher.Get())
            {
                var bytes = SafeGetUlong(mo, "TotalPhysicalMemory");
                report.Hardware.TotalMemoryGb = Math.Round(bytes / (1024.0 * 1024.0 * 1024.0), 2);
            }
        }
    }

    // ── Discos ──────────────────────────────────────────────────
    private void CollectDisks(AuditReport report)
    {
        using var diskSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");

        foreach (var mo in diskSearcher.Get())
        {
            var sizeBytes = SafeGetUlong(mo, "Size");
            var disk = new DiskInfo
            {
                Model = SafeGet(mo, "Model"),
                Manufacturer = SafeGet(mo, "Manufacturer"),
                MediaType = SafeGet(mo, "MediaType"),
                InterfaceType = SafeGet(mo, "InterfaceType"),
                SizeGb = (long)(sizeBytes / (1024UL * 1024UL * 1024UL)),
                SerialNumber = SafeGet(mo, "SerialNumber"),
                FirmwareRevision = SafeGet(mo, "FirmwareRevision")
            };

            // Recolectar particiones de este disco
            CollectPartitions(mo, disk);

            report.Hardware.Disks.Add(disk);
        }
    }

    private void CollectPartitions(ManagementBaseObject disk, DiskInfo diskInfo)
    {
        try
        {
            using var partitionSearcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{EscapeWql(disk["DeviceID"]?.ToString() ?? "")}'}} " +
                "WHERE AssocClass = Win32_DiskDriveToDiskPartition");

            foreach (var part in partitionSearcher.Get())
            {
                using var logicalSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{EscapeWql(part["DeviceID"]?.ToString() ?? "")}'}} " +
                    "WHERE AssocClass = Win32_LogicalDiskToPartition");

                foreach (var logical in logicalSearcher.Get())
                {
                    var sizeBytes = SafeGetUlong(logical, "Size");
                    var freeBytes = SafeGetUlong(logical, "FreeSpace");

                    diskInfo.Partitions.Add(new PartitionInfo
                    {
                        DriveLetter = SafeGet(logical, "DeviceID"),
                        FileSystem = SafeGet(logical, "FileSystem"),
                        SizeGb = (long)(sizeBytes / (1024UL * 1024UL * 1024UL)),
                        FreeSpaceGb = (long)(freeBytes / (1024UL * 1024UL * 1024UL)),
                        VolumeName = SafeGet(logical, "VolumeName")
                    });
                }
            }
        }
        catch
        {
            // La consulta ASSOCIATORS puede fallar en algunos sistemas
        }
    }

    // ── GPUs ─────────────────────────────────────────────────────
    private void CollectGpus(AuditReport report)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_VideoController");

        foreach (var mo in searcher.Get())
        {
            var gpu = new GpuInfo
            {
                Name = SafeGet(mo, "Name"),
                Manufacturer = SafeGet(mo, "AdapterCompatibility"),
                DriverVersion = SafeGet(mo, "DriverVersion"),
                DriverDate = FormatDate(SafeGet(mo, "DriverDate")),
                AdapterRamMb = (long)(SafeGetUlong(mo, "AdapterRAM") / (1024UL * 1024UL)),
                VideoModeDescription = SafeGet(mo, "VideoModeDescription")
            };

            report.Hardware.Gpus.Add(gpu);
        }
    }

    // ── Batería (solo laptops) ───────────────────────────────────
    private void CollectBattery(AuditReport report)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_Battery");

            foreach (var mo in searcher.Get())
            {
                report.Hardware.Battery = new BatteryInfo
                {
                    Name = SafeGet(mo, "Name"),
                    Status = SafeGet(mo, "Status"),
                    EstimatedChargeRemaining = SafeGetInt(mo, "EstimatedChargeRemaining"),
                    DesignCapacity = $"{SafeGetUint(mo, "DesignCapacity")} mWh",
                    FullChargeCapacity = $"{SafeGetUint(mo, "FullChargeCapacity")} mWh"
                };
                break; // Solo la primera batería
            }
        }
        catch
        {
            // Sin batería (desktop) o permisos insuficientes
        }
    }

    // ── Helpers ──────────────────────────────────────────────────
    private static string SafeGet(ManagementBaseObject mo, string property)
    {
        try { return mo[property]?.ToString()?.Trim() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static int SafeGetInt(ManagementBaseObject mo, string property)
    {
        try { return Convert.ToInt32(mo[property]); }
        catch { return 0; }
    }

    private static uint SafeGetUint(ManagementBaseObject mo, string property)
    {
        try { return Convert.ToUInt32(mo[property]); }
        catch { return 0; }
    }

    private static ulong SafeGetUlong(ManagementBaseObject mo, string property)
    {
        try { return Convert.ToUInt64(mo[property]); }
        catch { return 0; }
    }

    private static string GetMemoryType(uint smbiosType) => smbiosType switch
    {
        20 => "DDR",
        21 => "DDR2",
        22 => "DDR2 FB-DIMM",
        24 => "DDR3",
        26 => "DDR4",
        34 => "DDR5",
        0 => "Unknown",
        2 => "DRAM",
        _ => $"Type {smbiosType}"
    };

    private static string GetFormFactor(uint formFactor) => formFactor switch
    {
        1 => "Other",
        2 => "Unknown",
        3 => "SIMM",
        4 => "SIP",
        5 => "Chip",
        6 => "DIP",
        7 => "ZIP",
        8 => "DIMM",
        9 => "TSOP",
        10 => "PGA",
        11 => "RIMM",
        12 => "SODIMM",
        13 => "SRIMM",
        14 => "FB-DIMM",
        _ => $"Form {formFactor}"
    };

    private static string FormatDate(string? dmtfDate)
    {
        if (string.IsNullOrWhiteSpace(dmtfDate) || dmtfDate.Length < 8) return dmtfDate ?? string.Empty;
        try
        {
            return new DateTime(
                int.Parse(dmtfDate.Substring(0, 4)),
                int.Parse(dmtfDate.Substring(4, 2)),
                int.Parse(dmtfDate.Substring(6, 2))
            ).ToString("yyyy-MM-dd");
        }
        catch { return dmtfDate; }
    }

    private static string EscapeWql(string input)
    {
        return input.Replace("'", "''").Replace("\\", "\\\\");
    }
}