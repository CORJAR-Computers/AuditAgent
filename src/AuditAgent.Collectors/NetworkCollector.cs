using System.Management;
using AuditAgent.Core.Interfaces;
using AuditAgent.Core.Models;

namespace AuditAgent.Collectors;

/// <summary>
/// Recolector de información de red: adaptadores, IPs, DNS, DHCP, gateways.
/// </summary>
public class NetworkCollector : ICollector
{
    public string CollectorName => "NetworkCollector";

    public Task CollectAsync(AuditReport report, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Usar Win32_NetworkAdapterConfiguration que tiene toda la info de red
        using var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");

        foreach (var mo in searcher.Get())
        {
            var adapter = new NetworkAdapterInfo
            {
                Name = SafeGet(mo, "Description"),
                MacAddress = FormatMac(SafeGet(mo, "MACAddress")),
                DnsServer = SafeGetStringArray(mo, "DNSServerSearchOrder"),
                DefaultGateway = SafeGetStringArray(mo, "DefaultIPGateway"),
                DhcpEnabled = SafeGetBool(mo, "DHCPEnabled") ? "Yes" : "No",
                DhcpServer = SafeGet(mo, "DHCPServer"),
                SubnetMask = SafeGetStringArray(mo, "IPSubnet"),
                ConnectionStatus = SafeGet(mo, "IPConnectionMetric") != "0" ? "Connected" : "Disconnected",
                AdapterType = SafeGet(mo, "Description"),
                IsActive = true,
                DnsSuffix = SafeGet(mo, "DNSDomain")
            };

            // Recolectar todas las IPs
            var ipAddresses = mo["IPAddress"] as string[];
            if (ipAddresses is not null)
            {
                adapter.IpAddresses = ipAddresses.Where(ip =>
                    !string.IsNullOrWhiteSpace(ip)).ToList();
            }

            // Solo agregar si tiene IP asignada
            if (adapter.IpAddresses.Count > 0)
            {
                report.NetworkAdapters.Add(adapter);
            }
        }

        return Task.CompletedTask;
    }

    private static string SafeGet(ManagementBaseObject mo, string property)
    {
        try { return mo[property]?.ToString()?.Trim() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static bool SafeGetBool(ManagementBaseObject mo, string property)
    {
        try { return Convert.ToBoolean(mo[property]); }
        catch { return false; }
    }

    private static string SafeGetStringArray(ManagementBaseObject mo, string property)
    {
        try
        {
            var arr = mo[property] as string[];
            return arr is null ? string.Empty : string.Join(", ", arr.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        catch { return string.Empty; }
    }

    private static string FormatMac(string mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return string.Empty;
        // Normalizar: quitar separadores y poner en formato AA:BB:CC:DD:EE:FF
        var clean = mac.Replace(":", "").Replace("-", "").Replace(".", "").ToUpper();
        if (clean.Length != 12) return mac;
        return string.Concat(
            Enumerable.Range(0, 6).Select(i => $"{clean.Substring(i * 2, 2)}:"))
            .TrimEnd(':');
    }
}
