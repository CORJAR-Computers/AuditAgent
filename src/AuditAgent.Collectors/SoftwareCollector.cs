using Microsoft.Win32;
using System.Management;
using AuditAgent.Core.Interfaces;
using AuditAgent.Core.Models;

namespace AuditAgent.Collectors;

/// <summary>
/// Recolector de software instalado.
/// Usa múltiples fuentes para máxima cobertura:
/// 1. Registry (Uninstall keys) - Rápido y completo
/// 2. WMI Win32_Product - Lento pero preciso (opcional)
/// 
/// Se prioriza el Registry por rendimiento y menor impacto en el sistema.
/// Win32_Product puede tomar minutos y reinstalar MSI corruptos.
/// </summary>
public class SoftwareCollector : ICollector
{
    public string CollectorName => "SoftwareCollector";

    /// <summary>
    /// Si es true, también ejecuta Win32_Product (lento).
    /// Por defecto false para velocidad.
    /// </summary>
    public bool UseWmiProduct { get; set; } = false;

    /// <summary>
    /// Nombres de software a excluir del reporte (actualizaciones de Windows, etc.)
    /// </summary>
    public HashSet<string> ExclusionPatterns { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Security Update",
        "Update for Microsoft",
        "Microsoft Visual C++",
        "Microsoft .NET",
        "Microsoft Edge",
        "Microsoft Windows",
        "Windows Defender",
        "Microsoft Malware Protection"
    };

    /// <summary>
    /// Si es true, incluye las actualizaciones del sistema.
    /// </summary>
    public bool IncludeSystemUpdates { get; set; } = true;

    public Task CollectAsync(AuditReport report, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var softwareSet = new Dictionary<string, SoftwareInfo>(StringComparer.OrdinalIgnoreCase);

        // Fuente primaria: Registry (rápido y confiable)
        CollectFromRegistry(softwareSet, RegistryView.Registry64, "x64", ct);
        CollectFromRegistry(softwareSet, RegistryView.Registry32, "x86", ct);

        // Filtrar exclusiones
        var filtered = softwareSet.Values
            .Where(s => IncludeSystemUpdates || !s.IsUpdate)
            .Where(s => !IsExcluded(s))
            .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        report.InstalledSoftware = filtered;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Recolecta software del Registry (método rápido y recomendado).
    /// Lee tanto la clave Uninstall como la clave de usuario actual.
    /// </summary>
    private void CollectFromRegistry(
        Dictionary<string, SoftwareInfo> softwareSet,
        RegistryView registryView,
        string archLabel,
        CancellationToken ct)
    {
        // Rutas del Registry donde Windows registra el software instalado
        var uninstallPaths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var path in uninstallPaths)
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
            using var uninstallKey = baseKey.OpenSubKey(path);

            if (uninstallKey is null) continue;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();

                using var subKey = uninstallKey.OpenSubKey(subKeyName);
                if (subKey is null) continue;

                var name = subKey.GetValue("DisplayName")?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var software = new SoftwareInfo
                {
                    Name = name,
                    Version = (subKey.GetValue("DisplayVersion")?.ToString() ?? "").Trim(),
                    Publisher = (subKey.GetValue("Publisher")?.ToString() ?? "").Trim(),
                    InstallDate = FormatInstallDate(subKey.GetValue("InstallDate")?.ToString()),
                    InstallLocation = (subKey.GetValue("InstallLocation")?.ToString() ?? "").Trim(),
                    UninstallString = (subKey.GetValue("UninstallString")?.ToString() ?? "").Trim(),
                    RegistryKey = subKeyName,
                    Source = "Registry",
                    Architecture = archLabel,
                    IsModifiable = IsTruthy(subKey.GetValue("ModifyPath")?.ToString()),
                    IsUpdate = IsUpdateOrPatch(subKey)
                };

                // Tamaño estimado (viene en KB)
                if (uint.TryParse(subKey.GetValue("EstimatedSize")?.ToString(), out var sizeKb))
                {
                    software.EstimatedSizeMb = (decimal)Math.Round(sizeKb / 1024.0, 2);
                }

                // Merge: si ya existe, mantener la versión con más información
                var key = $"{software.Name}_{software.Version}".ToLowerInvariant();
                if (!softwareSet.ContainsKey(key) ||
                    string.IsNullOrEmpty(softwareSet[key].Publisher) && !string.IsNullOrEmpty(software.Publisher))
                {
                    softwareSet[key] = software;
                }
            }
        }

        // También leer del HKCU (software instalado solo para el usuario actual)
        try
        {
            using var currentUserBase = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, registryView);
            using var currentUserUninstall = currentUserBase.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            if (currentUserUninstall is not null)
            {
                foreach (var subKeyName in currentUserUninstall.GetSubKeyNames())
                {
                    using var subKey = currentUserUninstall.OpenSubKey(subKeyName);
                    if (subKey is null) continue;

                    var name = subKey.GetValue("DisplayName")?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var key = $"{name}_{subKey.GetValue("DisplayVersion")?.ToString() ?? ""}"
                        .ToLowerInvariant();

                    if (!softwareSet.ContainsKey(key))
                    {
                        softwareSet[key] = new SoftwareInfo
                        {
                            Name = name,
                            Version = (subKey.GetValue("DisplayVersion")?.ToString() ?? "").Trim(),
                            Publisher = (subKey.GetValue("Publisher")?.ToString() ?? "").Trim(),
                            InstallDate = FormatInstallDate(subKey.GetValue("InstallDate")?.ToString()),
                            InstallLocation = (subKey.GetValue("InstallLocation")?.ToString() ?? "").Trim(),
                            UninstallString = (subKey.GetValue("UninstallString")?.ToString() ?? "").Trim(),
                            RegistryKey = subKeyName,
                            Source = "Registry (HKCU)",
                            Architecture = archLabel
                        };
                    }
                }
            }
        }
        catch
        {
            // Puede fallar si no hay perfil de usuario cargado
        }
    }

    /// <summary>
    /// Recolecta software vía WMI Win32_Product.
    /// ADVERTENCIA: Este método es muy lento (puede tardar 5-10 minutos)
    /// y puede disparar reparaciones de MSI. Solo usar cuando se necesite
    /// información precisa de MSI.
    /// </summary>
    private void CollectFromWmi(Dictionary<string, SoftwareInfo> softwareSet, CancellationToken ct)
    {
        if (!UseWmiProduct) return;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_Product");

            foreach (var mo in searcher.Get())
            {
                ct.ThrowIfCancellationRequested();

                var name = (mo["Name"]?.ToString() ?? "").Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var key = $"{name}_{mo["Version"]?.ToString() ?? ""}".ToLowerInvariant();
                softwareSet[key] = new SoftwareInfo
                {
                    Name = name,
                    Version = (mo["Version"]?.ToString() ?? "").Trim(),
                    Publisher = (mo["Vendor"]?.ToString() ?? "").Trim(),
                    InstallDate = (mo["InstallDate"]?.ToString() ?? "").Trim(),
                    InstallLocation = (mo["InstallLocation"]?.ToString() ?? "").Trim(),
                    RegistryKey = mo["IdentifyingNumber"]?.ToString() ?? "",
                    Source = "WMI Win32_Product",
                    Architecture = mo["Architecture"]?.ToString() ?? ""
                };
            }
        }
        catch (Exception)
        {
            // Fallback: tratar de recolectar al menos la información básica de WMItimeout
        }
    }

    private bool IsExcluded(SoftwareInfo sw)
    {
        foreach (var pattern in ExclusionPatterns)
        {
            if (sw.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsUpdateOrPatch(RegistryKey subKey)
    {
        var name = subKey.GetValue("DisplayName")?.ToString() ?? "";
        var parentName = subKey.GetValue("ParentDisplayName")?.ToString() ?? "";
        var releaseType = subKey.GetValue("ReleaseType")?.ToString() ?? "";

        return !string.IsNullOrEmpty(parentName) ||
               releaseType.Equals("Update", StringComparison.OrdinalIgnoreCase) ||
               releaseType.Equals("Security Update", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("KB", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatInstallDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date) || date.Length < 8)
            return date ?? string.Empty;

        try
        {
            // Registry date format: yyyymmdd
            return $"{date.Substring(0, 4)}-{date.Substring(4, 2)}-{date.Substring(6, 2)}";
        }
        catch
        {
            return date;
        }
    }

    private static bool IsTruthy(string? value) =>
        !string.IsNullOrWhiteSpace(value);
}
