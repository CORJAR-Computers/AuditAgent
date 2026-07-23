using System.Management;
using AuditAgent.Core.Interfaces;
using AuditAgent.Core.Models;

namespace AuditAgent.Collectors;

/// <summary>
/// Recolector de información general del sistema: nombre, fabricante, modelo,
/// número de serie, dominio, usuario actual y UUID del hardware.
/// </summary>
public class SystemCollector : ICollector
{
    public string CollectorName => "SystemCollector";

    public Task CollectAsync(AuditReport report, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ── Win32_ComputerSystem ─────────────────────────────
        using var computerSystem = new ManagementObjectSearcher(
            "SELECT * FROM Win32_ComputerSystem");

        foreach (var mo in computerSystem.Get())
        {
            report.Computer.ComputerName = SafeGetString(mo, "Name");
            report.Computer.Manufacturer = SafeGetString(mo, "Manufacturer");
            report.Computer.Model = SafeGetString(mo, "Model");
            report.Computer.SystemType = SafeGetString(mo, "PCSystemType") switch
            {
                "1" => "Desktop",
                "2" => "Laptop",
                "3" => "Server",
                "4" => "Domain Controller",
                "5" => "Server (Domain Controller)",
                _ => SafeGetString(mo, "SystemType")
            };
            report.Computer.Domain = SafeGetString(mo, "Domain");
            report.Computer.CurrentUser = SafeGetString(mo, "UserName");
        }

        // ── Win32_BIOS (Serial Number) ───────────────────────
        using var bios = new ManagementObjectSearcher(
            "SELECT * FROM Win32_BIOS");

        foreach (var mo in bios.Get())
        {
            report.Computer.SerialNumber = SafeGetString(mo, "SerialNumber");
            report.Hardware.BiosVersion = SafeGetString(mo, "SMBIOSBIOSVersion")
                .IfEmpty(SafeGetString(mo, "Version"));
            report.Hardware.BiosManufacturer = SafeGetString(mo, "Manufacturer");
            report.Hardware.BiosReleaseDate = FormatDate(SafeGetString(mo, "ReleaseDate"));
        }

        // ── Win32_ComputerSystemProduct (Asset Tag + UUID) ──
        using var product = new ManagementObjectSearcher(
            "SELECT * FROM Win32_ComputerSystemProduct");

        foreach (var mo in product.Get())
        {
            report.Computer.AssetTag = SafeGetString(mo, "IdentifyingNumber");
            report.Computer.SystemUuid = SafeGetString(mo, "UUID");
        }

        // ── Win32_BaseBoard ──────────────────────────────────
        using var baseBoard = new ManagementObjectSearcher(
            "SELECT * FROM Win32_BaseBoard");

        foreach (var mo in baseBoard.Get())
        {
            report.Hardware.BaseBoardManufacturer = SafeGetString(mo, "Manufacturer");
            report.Hardware.BaseBoardProduct = SafeGetString(mo, "Product");
        }

        // ── Win32_NetworkAdapter (obtener MAC principal) ────
        using var network = new ManagementObjectSearcher(
            "SELECT * FROM Win32_NetworkAdapter WHERE MACAddress IS NOT NULL AND PhysicalAdapter = TRUE");

        var firstMac = network.Get().Cast<ManagementBaseObject>().FirstOrDefault();
        if (firstMac is not null)
        {
            report.Computer.PrimaryMacAddress = SafeGetString(firstMac, "MACAddress");
        }

        return Task.CompletedTask;
    }

    private static string SafeGetString(ManagementBaseObject mo, string property)
    {
        try
        {
            var value = mo[property]?.ToString();
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FormatDate(string? dmtfDate)
    {
        if (string.IsNullOrWhiteSpace(dmtfDate) || dmtfDate.Length < 8)
            return dmtfDate ?? string.Empty;

        try
        {
            // DMTF format: yyyymmddHHMMSS.mmmmmm+UUU
            var year = int.Parse(dmtfDate.Substring(0, 4));
            var month = int.Parse(dmtfDate.Substring(4, 2));
            var day = int.Parse(dmtfDate.Substring(6, 2));
            return new DateTime(year, month, day).ToString("yyyy-MM-dd");
        }
        catch
        {
            return dmtfDate;
        }
    }
}

/// <summary>Método de extensión para simplificar null-coalescing.</summary>
internal static class StringExtensions
{
    public static string IfEmpty(this string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
