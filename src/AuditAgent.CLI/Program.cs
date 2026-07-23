using System.Security.Cryptography;
using AuditAgent.Collectors;
using AuditAgent.Core.Services;
using AuditAgent.Security;
using Spectre.Console;

namespace AuditAgent.CLI;

/// <summary>
/// Herramienta CLI para ejecutar auditorias puntuales.
/// Uso: AuditAgent.CLI.exe [opciones]
/// 
/// Opciones:
///   --output <ruta>   Ruta de salida del JSON (default: ./audit-report.json)
///   --pretty            Formato JSON con indentacion
///   --no-updates       Excluir actualizaciones del sistema
///   --csv               Exportar tambien como CSV
///   --html              Exportar informe detallado como HTML interactivo
///   --quiet             Solo mostrar errores
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string outputPath = "./audit-report.json";
        bool includeUpdates = true;
        bool csvExport = false;
        bool htmlExport = false;
        bool quiet = false;

        // Parsear argumentos
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--output":
                    if (i + 1 < args.Length) outputPath = args[++i];
                    break;
                case "--no-updates": includeUpdates = false; break;
                case "--csv": csvExport = true; break;
                case "--html": htmlExport = true; break;
                case "--quiet": quiet = true; break;
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
            }
        }

        try
        {
            if (!quiet)
            {
                AnsiConsole.MarkupLine("[bold blue]AuditAgent CLI v1.0.0 - Auditoría de Sistema[/]");
            }

            var collectors = new List<AuditAgent.Core.Interfaces.ICollector>
            {
                new SystemCollector(),
                new HardwareCollector(),
                new OsCollector(),
                new NetworkCollector(),
                new SoftwareCollector { IncludeSystemUpdates = includeUpdates }
            };

            var (privKey, pubKey) = RsaSigner.GenerateKeyPair();
            var orchestrator = new AuditOrchestrator(
                collectors, new RsaSigner(), privKey);

            AuditAgent.Core.Models.AuditReport report = null!;

            if (!quiet)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("blue"))
                    .StartAsync("Recolectando datos del sistema...", async ctx =>
                    {
                        report = await orchestrator.ExecuteAuditAsync();
                    });
            }
            else
            {
                report = await orchestrator.ExecuteAuditAsync();
            }

            report.AgentVersion = "1.0.0-CLI";
            var json = orchestrator.SerializeReport(report);

            // Guardar JSON
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            await File.WriteAllTextAsync(outputPath, json);

            if (!quiet)
            {
                AnsiConsole.MarkupLine($"[green]Reporte JSON guardado:[/] {outputPath}");
            }

            // Exportar HTML si se solicita
            if (htmlExport)
            {
                var htmlPath = Path.ChangeExtension(outputPath, ".html");
                var html = HtmlReportGenerator.Generate(report);
                await File.WriteAllTextAsync(htmlPath, html);
                if (!quiet) AnsiConsole.MarkupLine($"[green]Reporte HTML exportado:[/] {htmlPath}");
            }

            // Exportar CSV si se solicita
            if (csvExport)
            {
                var csvPath = Path.ChangeExtension(outputPath, ".csv");
                await ExportCsvAsync(report, csvPath);
                if (!quiet) AnsiConsole.MarkupLine($"[green]Reporte CSV exportado:[/] {csvPath}");
            }

            if (!quiet)
            {
                var grid = new Grid()
                    .AddColumn(new GridColumn().NoWrap().PadRight(4))
                    .AddColumn();

                grid.AddRow("[bold]Equipo:[/]", report.Computer.ComputerName);
                grid.AddRow("[bold]Serial:[/]", report.Computer.SerialNumber ?? "N/A");
                grid.AddRow("[bold]Software:[/]", $"{report.InstalledSoftware.Count} programas");
                grid.AddRow("[bold]Duración:[/]", $"{report.AuditDurationMs} ms");

                var panel = new Panel(grid)
                    .Header("[bold]Resumen de Auditoría[/]")
                    .BorderColor(Color.Blue)
                    .Padding(1, 1, 1, 1);

                AnsiConsole.Write(panel);
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (!quiet)
                AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            else
                Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    private static async Task ExportCsvAsync(
        AuditAgent.Core.Models.AuditReport report, string csvPath)
    {
        using var writer = new StreamWriter(csvPath);
        await writer.WriteLineAsync("Nombre,Version,Fabricante,Fecha Instalacion,Tamano MB,Fuente,Arquitectura");

        foreach (var sw in report.InstalledSoftware)
        {
            var name = sw.Name?.Replace(",", ";") ?? "";
            var publisher = sw.Publisher?.Replace(",", ";") ?? "";
            await writer.WriteLineAsync(
                $"{name},{sw.Version},{publisher},{sw.InstallDate},{sw.EstimatedSizeMb},{sw.Source},{sw.Architecture}");
        }
    }

    private static void PrintHelp()
    {
        AnsiConsole.MarkupLine(@"[bold blue]AuditAgent CLI[/] - Herramienta de Auditoría de Sistema

[bold]Uso:[/] AuditAgent.CLI.exe [opciones]

[bold]Opciones:[/]
  [green]--output <ruta>[/]   Ruta del archivo JSON de salida
  [green]--pretty[/]            JSON con indentación (default)
  [green]--no-updates[/]       Excluir actualizaciones del sistema
  [green]--csv[/]               Exportar lista de software como CSV
  [green]--html[/]              Exportar informe detallado como HTML interactivo
  [green]--quiet[/]             Solo mostrar errores
  [green]--help[/]              Mostrar esta ayuda

[bold]Ejemplos:[/]
  AuditAgent.CLI.exe
  AuditAgent.CLI.exe --output C:\auditoria\pc-001.json --html
  AuditAgent.CLI.exe --no-updates --quiet
");
    }
}
