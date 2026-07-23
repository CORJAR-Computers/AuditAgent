using System.Net;
using System.Text;
using AuditAgent.Core.Models;

namespace AuditAgent.CLI;

/// <summary>
/// Genera reportes HTML profesionales con escape de entidades
/// para prevenir inyeccion XSS.
/// </summary>
public static class HtmlReportGenerator
{
    public static string Generate(AuditReport report)
    {
        var sb = new StringBuilder();

        var computerName = WebUtility.HtmlEncode(report.Computer.ComputerName);
        var currentUser = WebUtility.HtmlEncode(report.Computer.CurrentUser);
        var auditDate = WebUtility.HtmlEncode(
            report.Computer.AuditTimestampUtc.ToLocalTime().ToString("g"));
        var version = WebUtility.HtmlEncode(report.AgentVersion);
        var manufacturer = WebUtility.HtmlEncode(report.Computer.Manufacturer);
        var model = WebUtility.HtmlEncode(report.Computer.Model);
        var systemType = WebUtility.HtmlEncode(report.Computer.SystemType);
        var serial = WebUtility.HtmlEncode(report.Computer.SerialNumber);
        var domain = WebUtility.HtmlEncode(report.Computer.Domain);
        var osCaption = WebUtility.HtmlEncode(report.OperatingSystem.Caption);
        var osVersion = WebUtility.HtmlEncode(report.OperatingSystem.Version);
        var osArch = WebUtility.HtmlEncode(report.OperatingSystem.OSArchitecture);
        var osInstallDate = WebUtility.HtmlEncode(
            report.OperatingSystem.InstallDate?.ToString("yyyy-MM-dd") ?? "N/A");

        var cpuName = WebUtility.HtmlEncode(
            report.Hardware.Processors.FirstOrDefault()?.Name ?? "Desconocido");
        var cpuCores = report.Hardware.Processors.Sum(p => p.NumberOfCores);
        var ramGb = report.Hardware.TotalMemoryGb;
        var boardMfr = WebUtility.HtmlEncode(report.Hardware.BaseBoardManufacturer);
        var boardProduct = WebUtility.HtmlEncode(report.Hardware.BaseBoardProduct);

        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Reporte de Auditoria - " + computerName + @"</title>
    <style>
        :root {
            --bg-color: #f4f7f6;
            --text-color: #333;
            --card-bg: #fff;
            --primary-color: #2c3e50;
            --secondary-color: #34495e;
            --accent-color: #3498db;
            --border-color: #e0e0e0;
            --success-color: #27ae60;
            --warning-color: #f39c12;
        }
        * { box-sizing: border-box; }
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: var(--bg-color);
            color: var(--text-color);
            line-height: 1.6;
            margin: 0;
            padding: 20px;
        }
        .container { max-width: 1200px; margin: 0 auto; }
        h1, h2, h3 { color: var(--primary-color); margin-top: 0; }
        .header {
            text-align: center;
            margin-bottom: 30px;
            padding: 30px 20px;
            background: linear-gradient(135deg, var(--primary-color), var(--accent-color));
            color: white;
            border-radius: 12px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.1);
        }
        .header h1 { color: white; margin: 0 0 10px 0; font-size: 1.8em; }
        .header p { margin: 5px 0; opacity: 0.9; }
        .meta-bar {
            display: flex;
            justify-content: space-between;
            flex-wrap: wrap;
            gap: 10px;
            margin-bottom: 25px;
            padding: 12px 20px;
            background: var(--card-bg);
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.05);
            font-size: 0.85em;
            color: #7f8c8d;
        }
        .card {
            background-color: var(--card-bg);
            border-radius: 10px;
            padding: 24px;
            margin-bottom: 24px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.06);
            border-left: 4px solid var(--accent-color);
        }
        .card h2 { margin-top: 0; padding-bottom: 10px; border-bottom: 2px solid #eee; }
        .grid-2 {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 24px;
        }
        @media (max-width: 768px) { .grid-2 { grid-template-columns: 1fr; } }
        .info-row { display: flex; padding: 8px 0; border-bottom: 1px solid #f0f0f0; }
        .info-row:last-child { border-bottom: none; }
        .info-label { font-weight: 600; min-width: 160px; color: var(--secondary-color); }
        .info-value { flex: 1; }
        table { width: 100%; border-collapse: collapse; margin-top: 12px; font-size: 0.9em; }
        th, td { text-align: left; padding: 12px 14px; border-bottom: 1px solid var(--border-color); }
        th { background-color: var(--secondary-color); color: white; font-weight: 600; }
        th:first-child { border-radius: 6px 0 0 0; }
        th:last-child { border-radius: 0 6px 0 0; }
        tr:hover { background-color: #f5f8fa; }
        tr:nth-child(even) { background-color: #fafbfc; }
        tr:nth-child(even):hover { background-color: #f0f4f8; }
        .stat-number { font-size: 2em; font-weight: 700; color: var(--accent-color); }
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 16px;
            margin-top: 16px;
        }
        .stat-card {
            text-align: center;
            padding: 16px;
            background: #f8f9fa;
            border-radius: 8px;
        }
        .stat-card .label { font-size: 0.8em; color: #7f8c8d; text-transform: uppercase; letter-spacing: 0.5px; }
        .footer {
            text-align: center;
            margin-top: 40px;
            padding: 20px;
            color: #95a5a6;
            font-size: 0.85em;
            border-top: 1px solid var(--border-color);
        }
        @media print {
            body { padding: 0; background: white; }
            .card { box-shadow: none; border: 1px solid #ddd; break-inside: avoid; }
            .header { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Reporte de Auditoria de Sistema</h1>
            <p><strong>Equipo:</strong> " + computerName + @"</p>
            <p><strong>Tecnico:</strong> " + currentUser + @"</p>
        </div>

        <div class=""meta-bar"">
            <span>Generado: " + auditDate + @"</span>
            <span>Agente v" + version + @"</span>
            <span>Duracion: " + report.AuditDurationMs + @" ms</span>
            <span>Hash: " + WebUtility.HtmlEncode(report.ReportHash ?? "") + @"</span>
        </div>

        <div class=""stats-grid"">
            <div class=""stat-card"">
                <div class=""stat-number"">" + report.InstalledSoftware.Count + @"</div>
                <div class=""label"">Programas</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-number"">" + report.SecurityPatches.Count + @"</div>
                <div class=""label"">Parches</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-number"">" + report.NetworkAdapters.Count + @"</div>
                <div class=""label"">Redes</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-number"">" + ramGb + @"</div>
                <div class=""label"">GB RAM</div>
            </div>
        </div>

        <div class=""grid-2"">
            <div class=""card"">
                <h2>Informacion del Equipo</h2>
                <div class=""info-row""><span class=""info-label"">Fabricante</span><span class=""info-value"">" + manufacturer + @"</span></div>
                <div class=""info-row""><span class=""info-label"">Modelo</span><span class=""info-value"">" + model + @"</span></div>
                <div class=""info-row""><span class=""info-label"">Tipo</span><span class=""info-value"">" + systemType + @"</span></div>
                <div class=""info-row""><span class=""info-label"">Numero de Serie</span><span class=""info-value"">" + serial + @"</span></div>
                <div class=""info-row""><span class=""info-label"">Dominio</span><span class=""info-value"">" + domain + @"</span></div>
                <div class=""info-row""><span class=""info-label"">UUID</span><span class=""info-value"" style=""font-family:monospace;font-size:0.9em;"">" + WebUtility.HtmlEncode(report.Computer.SystemUuid) + @"</span></div>
            </div>

            <div class=""card"">
                <h2>Sistema Operativo</h2>
                <div class=""info-row""><span class=""info-label"">Nombre</span><span class=""info-value"">" + osCaption + @"</span></div>
                <div class=""info-row""><span class=""info-label"">Version</span><span class=""info-value"">" + osVersion + @"</span></div>
                <div class=""info-row""><span class=""info-label"">Arquitectura</span><span class=""info-value"">" + osArch + @"</span></div>
                <div class=""info-row""><span class=""info-label"">Build</span><span class=""info-value"">" + WebUtility.HtmlEncode(report.OperatingSystem.BuildNumber) + @"</span></div>
                <div class=""info-row""><span class=""info-label"">Fecha Instalacion</span><span class=""info-value"">" + osInstallDate + @"</span></div>
                <div class=""info-row""><span class=""info-label"">Ultimo Arranque</span><span class=""info-value"">" + WebUtility.HtmlEncode(
                    report.OperatingSystem.LastBootUpTime?.ToLocalTime().ToString("g") ?? "N/A") + @"</span></div>
            </div>
        </div>

        <div class=""card"">
            <h2>Hardware Principal</h2>
                <div class=""info-row""><span class=""info-label"">CPU</span><span class=""info-value"">" + cpuName + @" (" + cpuCores + @" Cores / " +
                    report.Hardware.Processors.Sum(p => p.NumberOfLogicalProcessors) + @" Hilos)</span></div>
                <div class=""info-row""><span class=""info-label"">Memoria RAM</span><span class=""info-value"">" + ramGb + @" GB" +
                    (report.Hardware.MemoryModules.Count > 0
                        ? " (" + string.Join(", ", report.Hardware.MemoryModules.Select(m => WebUtility.HtmlEncode($"{m.CapacityGb}GB {m.MemoryType}"))) + ")"
                        : "") + @"</span></div>
                <div class=""info-row""><span class=""info-label"">Placa Base</span><span class=""info-value"">" + boardMfr + @" - " + boardProduct + @"</span></div>
                <div class=""info-row""><span class=""info-label"">BIOS</span><span class=""info-value"">" + WebUtility.HtmlEncode(report.Hardware.BiosVersion) +
                    " (" + WebUtility.HtmlEncode(report.Hardware.BiosManufacturer) + ")" + @"</span></div>
        </div>

        <div class=""card"">
            <h2>Almacenamiento</h2>
            <table>
                <tr><th>Modelo</th><th>Tipo</th><th>Capacidad</th><th>Particiones</th></tr>");

        foreach (var disk in report.Hardware.Disks)
        {
            var dModel = WebUtility.HtmlEncode(disk.Model);
            var dType = WebUtility.HtmlEncode($"{disk.MediaType} ({disk.InterfaceType})");
            var parts = string.Join(", ", disk.Partitions.Select(p =>
                WebUtility.HtmlEncode($"{p.DriveLetter} {p.FileSystem} - {p.FreeSpaceGb}GB libres / {p.SizeGb}GB")));
            sb.AppendLine($@"<tr><td>{dModel}</td><td>{dType}</td><td>{disk.SizeGb} GB</td><td>{parts}</td></tr>");
        }

        sb.AppendLine(@"            </table>
        </div>

        <div class=""card"">
            <h2>Red</h2>
            <table>
                <tr><th>Adaptador</th><th>MAC</th><th>IP</th><th>DNS</th><th>DHCP</th></tr>");

        foreach (var net in report.NetworkAdapters)
        {
            sb.AppendLine($@"<tr>
                    <td>{WebUtility.HtmlEncode(net.Name)}</td>
                    <td style=""font-family:monospace"">{WebUtility.HtmlEncode(net.MacAddress)}</td>
                    <td>{WebUtility.HtmlEncode(string.Join(", ", net.IpAddresses))}</td>
                    <td>{WebUtility.HtmlEncode(net.DnsServer)}</td>
                    <td>{WebUtility.HtmlEncode(net.DhcpEnabled)}</td>
                </tr>");
        }

        sb.AppendLine(@"            </table>
        </div>

        <div class=""card"">
            <h2>Software Instalado (" + report.InstalledSoftware.Count + @" programas)</h2>
            <table>
                <tr><th>#</th><th>Nombre</th><th>Version</th><th>Fabricante</th><th>Fecha Inst.</th><th>Tamano</th></tr>");

        var idx = 1;
        foreach (var sw in report.InstalledSoftware)
        {
            sb.AppendLine($@"<tr>
                    <td>{idx++}</td>
                    <td>{WebUtility.HtmlEncode(sw.Name)}</td>
                    <td>{WebUtility.HtmlEncode(sw.Version)}</td>
                    <td>{WebUtility.HtmlEncode(sw.Publisher)}</td>
                    <td>{WebUtility.HtmlEncode(sw.InstallDate)}</td>
                    <td>{sw.EstimatedSizeMb:N0} MB</td>
                </tr>");
        }

        sb.AppendLine(@"            </table>
        </div>

        <div class=""card"">
            <h2>Parches de Seguridad (" + report.SecurityPatches.Count + @")</h2>
            <table>
                <tr><th>KB</th><th>Descripcion</th><th>Instalado el</th><th>Instalado por</th></tr>");

        foreach (var patch in report.SecurityPatches)
        {
            sb.AppendLine($@"<tr>
                    <td>{WebUtility.HtmlEncode(patch.HotFixId)}</td>
                    <td>{WebUtility.HtmlEncode(patch.Description)}</td>
                    <td>{WebUtility.HtmlEncode(patch.InstalledOn)}</td>
                    <td>{WebUtility.HtmlEncode(patch.InstalledBy)}</td>
                </tr>");
        }

        sb.AppendLine(@"            </table>
        </div>

        <div class=""footer"">
            <p>Generado por AuditAgent v" + version + @" | CORJAR Computers</p>
            <p>Este documento es confidencial y de uso interno.</p>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }
}
