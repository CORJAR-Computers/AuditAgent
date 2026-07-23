using System.Text;
using AuditAgent.Core.Models;

namespace AuditAgent.CLI;

public static class HtmlReportGenerator
{
    public static string Generate(AuditReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Reporte de Auditoría - " + report.Computer.ComputerName + @"</title>
    <style>
        :root {
            --bg-color: #f4f7f6;
            --text-color: #333;
            --card-bg: #fff;
            --primary-color: #2c3e50;
            --secondary-color: #34495e;
            --accent-color: #3498db;
            --border-color: #e0e0e0;
        }
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: var(--bg-color);
            color: var(--text-color);
            line-height: 1.6;
            margin: 0;
            padding: 20px;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
        }
        h1, h2, h3 {
            color: var(--primary-color);
        }
        .header {
            text-align: center;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 2px solid var(--accent-color);
        }
        .card {
            background-color: var(--card-bg);
            border-radius: 8px;
            padding: 20px;
            margin-bottom: 20px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.05);
        }
        .grid-2 {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 10px;
            font-size: 0.9em;
        }
        th, td {
            text-align: left;
            padding: 12px;
            border-bottom: 1px solid var(--border-color);
        }
        th {
            background-color: var(--secondary-color);
            color: white;
        }
        tr:nth-child(even) {
            background-color: #f9f9f9;
        }
        .meta-info {
            font-size: 0.85em;
            color: #7f8c8d;
            text-align: right;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Reporte de Auditoría de Sistema</h1>
            <p><strong>Equipo:</strong> " + report.Computer.ComputerName + @" | <strong>Usuario:</strong> " + report.Computer.CurrentUser + @"</p>
        </div>

        <div class=""meta-info"">
            <p>Generado el: " + report.Computer.AuditTimestampUtc.ToLocalTime().ToString("g") + @" | Versión Agente: " + report.AgentVersion + @"</p>
        </div>

        <div class=""grid-2"">
            <div class=""card"">
                <h2>💻 Información del Equipo</h2>
                <p><strong>Fabricante:</strong> " + report.Computer.Manufacturer + @"</p>
                <p><strong>Modelo:</strong> " + report.Computer.Model + @"</p>
                <p><strong>Tipo:</strong> " + report.Computer.SystemType + @"</p>
                <p><strong>Número de Serie:</strong> " + report.Computer.SerialNumber + @"</p>
                <p><strong>Dominio:</strong> " + report.Computer.Domain + @"</p>
            </div>

            <div class=""card"">
                <h2>⚙️ Sistema Operativo</h2>
                <p><strong>Nombre:</strong> " + report.OperatingSystem.Caption + @"</p>
                <p><strong>Versión:</strong> " + report.OperatingSystem.Version + @"</p>
                <p><strong>Arquitectura:</strong> " + report.OperatingSystem.OSArchitecture + @"</p>
                <p><strong>Fecha Instalación:</strong> " + report.OperatingSystem.InstallDate + @"</p>
            </div>
        </div>

        <div class=""card"">
            <h2>🛠️ Hardware Principal</h2>
            <p><strong>CPU:</strong> " + (report.Hardware.Processors.FirstOrDefault()?.Name ?? "Desconocido") + @" (" + report.Hardware.Processors.Sum(p => p.NumberOfCores) + @" Cores)</p>
            <p><strong>Memoria RAM:</strong> " + report.Hardware.TotalMemoryGb + @" GB</p>
            <p><strong>Placa Base:</strong> " + report.Hardware.BaseBoardManufacturer + @" - " + report.Hardware.BaseBoardProduct + @"</p>
            ");

        if (report.Hardware.Battery != null)
        {
            sb.AppendLine($@"<p><strong>Batería:</strong> {report.Hardware.Battery.Name} ({report.Hardware.Battery.Status}) - {report.Hardware.Battery.EstimatedChargeRemaining}%</p>");
        }

        sb.AppendLine(@"
        </div>

        <div class=""card"">
            <h2>💽 Almacenamiento</h2>
            <table>
                <tr>
                    <th>Modelo</th>
                    <th>Tipo</th>
                    <th>Tamaño (GB)</th>
                    <th>Particiones</th>
                </tr>");

        foreach (var disk in report.Hardware.Disks)
        {
            var parts = string.Join(", ", disk.Partitions.Select(p => $"{p.DriveLetter} ({p.FreeSpaceGb}GB Libres)"));
            sb.AppendLine($@"
                <tr>
                    <td>{disk.Model}</td>
                    <td>{disk.MediaType} ({disk.InterfaceType})</td>
                    <td>{disk.SizeGb}</td>
                    <td>{parts}</td>
                </tr>");
        }

        sb.AppendLine(@"
            </table>
        </div>

        <div class=""card"">
            <h2>🌐 Red</h2>
            <table>
                <tr>
                    <th>Nombre</th>
                    <th>MAC</th>
                    <th>IP(s)</th>
                </tr>");

        foreach (var net in report.NetworkAdapters)
        {
            sb.AppendLine($@"
                <tr>
                    <td>{net.Name}</td>
                    <td>{net.MacAddress}</td>
                    <td>{string.Join(", ", net.IpAddresses)}</td>
                </tr>");
        }

        sb.AppendLine(@"
            </table>
        </div>

        <div class=""card"">
            <h2>📦 Software Instalado (" + report.InstalledSoftware.Count + @")</h2>
            <table>
                <tr>
                    <th>Nombre</th>
                    <th>Versión</th>
                    <th>Fabricante</th>
                    <th>Fecha Inst.</th>
                    <th>Tamaño (MB)</th>
                </tr>");

        foreach (var sw in report.InstalledSoftware)
        {
            sb.AppendLine($@"
                <tr>
                    <td>{sw.Name}</td>
                    <td>{sw.Version}</td>
                    <td>{sw.Publisher}</td>
                    <td>{sw.InstallDate}</td>
                    <td>{sw.EstimatedSizeMb}</td>
                </tr>");
        }

        sb.AppendLine(@"
            </table>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }
}
