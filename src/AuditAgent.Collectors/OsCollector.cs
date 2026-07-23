using System.Management;
using AuditAgent.Core.Interfaces;
using AuditAgent.Core.Models;

namespace AuditAgent.Collectors;

/// <summary>
/// Recolector de información del sistema operativo y parches de seguridad.
/// </summary>
public class OsCollector : ICollector
{
    public string CollectorName => "OsCollector";

    public Task CollectAsync(AuditReport report, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        CollectOsInfo(report);
        CollectSecurityPatches(report);

        return Task.CompletedTask;
    }

    private void CollectOsInfo(AuditReport report)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_OperatingSystem");

        foreach (var mo in searcher.Get())
        {
            var os = new OperatingSystemInfo
            {
                Caption = SafeGet(mo, "Caption"),
                Version = SafeGet(mo, "Version"),
                BuildNumber = SafeGet(mo, "BuildNumber"),
                OSArchitecture = SafeGet(mo, "OSArchitecture"),
                Organization = SafeGet(mo, "Organization"),
                RegisteredUser = SafeGet(mo, "RegisteredUser"),
                WindowsDirectory = SafeGet(mo, "WindowsDirectory"),
                SystemDirectory = SafeGet(mo, "SystemDirectory"),
                ProcessCount = SafeGetInt(mo, "NumberOfProcesses")
            };

            // InstallDate viene en formato DMTF
            var installDateStr = SafeGet(mo, "InstallDate");
            if (!string.IsNullOrEmpty(installDateStr) && installDateStr.Length >= 14)
            {
                try
                {
                    os.InstallDate = new DateTime(
                        int.Parse(installDateStr.Substring(0, 4)),
                        int.Parse(installDateStr.Substring(4, 2)),
                        int.Parse(installDateStr.Substring(6, 2)),
                        int.Parse(installDateStr.Substring(8, 2)),
                        int.Parse(installDateStr.Substring(10, 2)),
                        int.Parse(installDateStr.Substring(12, 2)),
                        DateTimeKind.Utc
                    );
                }
                catch { /* formato inválido */ }
            }

            // LastBootUpTime
            var bootStr = SafeGet(mo, "LastBootUpTime");
            if (!string.IsNullOrEmpty(bootStr) && bootStr.Length >= 14)
            {
                try
                {
                    os.LastBootUpTime = new DateTime(
                        int.Parse(bootStr.Substring(0, 4)),
                        int.Parse(bootStr.Substring(4, 2)),
                        int.Parse(bootStr.Substring(6, 2)),
                        int.Parse(bootStr.Substring(8, 2)),
                        int.Parse(bootStr.Substring(10, 2)),
                        int.Parse(bootStr.Substring(12, 2)),
                        DateTimeKind.Utc
                    );
                }
                catch { /* formato inválido */ }
            }

            report.OperatingSystem = os;
        }
    }

    private void CollectSecurityPatches(AuditReport report)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_QuickFixEngineering");

        foreach (var mo in searcher.Get())
        {
            var patch = new SecurityPatchInfo
            {
                HotFixId = SafeGet(mo, "HotFixID"),
                Description = SafeGet(mo, "Description"),
                InstalledOn = SafeGet(mo, "InstalledOn"),
                InstalledBy = SafeGet(mo, "InstalledBy"),
                Status = SafeGet(mo, "Status")
            };

            // Solo agregar si tiene un KB number válido
            if (!string.IsNullOrWhiteSpace(patch.HotFixId) &&
                (patch.HotFixId.StartsWith("KB", StringComparison.OrdinalIgnoreCase) ||
                 patch.Description.Length > 0))
            {
                report.SecurityPatches.Add(patch);
            }
        }
    }

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
}