using System.Security.Cryptography;
using AuditAgent.Collectors;
using AuditAgent.Core.Services;
using AuditAgent.Security;
using Spectre.Console;

namespace AuditAgent.CLI;

/// <summary>
/// Herramienta CLI para ejecutar auditorias puntuales con
/// seleccion interactiva de formato de salida.
/// 
/// Formatos soportados: JSON, HTML, PDF, CSV
/// El tecnico puede elegir uno o varios formatos a la vez.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string? outputPath = null;
        bool includeUpdates = true;
        bool quiet = false;
        string? preselectedFormat = null;

        // Parsear argumentos de linea de comandos
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--output":
                case "-o":
                    if (i + 1 < args.Length) outputPath = args[++i];
                    break;
                case "--no-updates":
                    includeUpdates = false;
                    break;
                case "--quiet":
                case "-q":
                    quiet = true;
                    break;
                case "--format":
                case "-f":
                    if (i + 1 < args.Length) preselectedFormat = args[++i];
                    break;
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
                AnsiConsole.Write(new FigletText("AUDIT AGENT").Color(Color.Blue));
                AnsiConsole.MarkupLine("[grey]Auditoria de Software Corporativo v1.0.0[/]\n");
            }

            // Determinar ruta de salida
            if (string.IsNullOrEmpty(outputPath))
            {
                var computerName = Environment.MachineName;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                outputPath = $"./audit_{computerName}_{timestamp}";
            }

            // Asegurar que el directorio existe
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            // Seleccionar formato de salida
            var formats = SelectFormats(preselectedFormat, quiet);

            if (formats.Count == 0)
            {
                if (!quiet) AnsiConsole.MarkupLine("[yellow]No se selecciono ningun formato. Saliendo.[/]");
                return 0;
            }

            // Ejecutar auditoria
            var report = await RunAudit(includeUpdates, quiet);

            // Generar reportes en los formatos seleccionados
            var generatedFiles = new List<string>();

            foreach (var format in formats)
            {
                try
                {
                    var file = GenerateOutput(report, format, outputPath);
                    generatedFiles.Add(file);
                    if (!quiet)
                        AnsiConsole.MarkupLine($"[green]  [checkmark] {format}[/] -> {file}");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]  [cross] Error generando {format}: {ex.Message}[/]");
                }
            }

            // Resumen final
            if (!quiet)
            {
                Console.WriteLine();
                var grid = new Grid()
                    .AddColumn(new GridColumn().NoWrap().PadRight(4))
                    .AddColumn();

                grid.AddRow("[bold]Equipo:[/]", report.Computer.ComputerName);
                grid.AddRow("[bold]Fabricante:[/]", report.Computer.Manufacturer);
                grid.AddRow("[bold]Modelo:[/]", report.Computer.Model);
                grid.AddRow("[bold]Serial:[/]", report.Computer.SerialNumber);
                grid.AddRow("[bold]S.O.:[/]", report.OperatingSystem.Caption);
                grid.AddRow("[bold]RAM:[/]", $"{report.Hardware.TotalMemoryGb} GB");
                grid.AddRow("[bold]Software:[/]", $"{report.InstalledSoftware.Count} programas");
                grid.AddRow("[bold]Parches:[/]", $"{report.SecurityPatches.Count} KBs");
                grid.AddRow("[bold]Duracion:[/]", $"{report.AuditDurationMs} ms");

                var panel = new Panel(grid)
                    .Header("[bold green]Auditoria Completada[/]")
                    .BorderColor(Color.Green)
                    .Padding(1, 1, 1, 1);

                AnsiConsole.Write(panel);

                if (report.Warnings.Count > 0)
                {
                    AnsiConsole.MarkupLine($"\n[yellow]  Advertencias: {report.Warnings.Count}[/]");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (!quiet)
                AnsiConsole.MarkupLine($"[red]ERROR FATAL:[/] {ex.Message}");
            else
                Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Muestra menu interactivo para seleccionar formato de salida.
    /// </summary>
    static List<string> SelectFormats(string? preselected, bool quiet)
    {
        var allFormats = new[]
        {
            ("json", "JSON", "Reporte de datos estructurado (para sistemas)", "[blue]{}[/]"),
            ("html", "HTML", "Informe visual interactivo (para navegador)", "[green]{}[/]"),
            ("pdf", "PDF", "Informe profesional para imprimir o enviar por email", "[red]{}[/]"),
            ("csv", "CSV", "Lista de software en tabla (para Excel)", "[cyan]{}[/]"),
        };

        // Si ya fue seleccionado por argumento
        if (!string.IsNullOrEmpty(preselected))
        {
            var selected = preselected
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim().ToLowerInvariant())
                .Where(f => allFormats.Any(a => a.Item1 == f))
                .ToList();

            if (selected.Count > 0) return selected;
        }

        if (quiet)
        {
            // En modo silencioso, generar JSON por defecto
            return new List<string> { "json" };
        }

        // Menu interactivo con Spectre.Console
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Seleccione el formato de salida:[/]");
        AnsiConsole.WriteLine();

        var choices = allFormats.Select((f, i) =>
            $"{f.Item4.Replace("{}", f.Item2)}   {f.Item3}"
        ).ToArray();

        var selection = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("[grey]Use flechas + Espacio para seleccionar, Enter para confirmar[/]")
                .PageSize(10)
                .AddChoices(allFormats.Select(f => f.Item1))
                .InstructionsText(
                    "[grey](Presione [blue]Espacio[/] para seleccionar/deseleccionar, " +
                    "[blue]A[/] para todos, [blue]Enter[/] para confirmar)[/]")
        );

        return selection.ToList();
    }

    /// <summary>
    /// Ejecuta la auditoria con barra de progreso.
    /// </summary>
    static async Task<AuditAgent.Core.Models.AuditReport> RunAudit(bool includeUpdates, bool quiet)
    {
        var collectors = new List<AuditAgent.Core.Interfaces.ICollector>
        {
            new SystemCollector(),
            new HardwareCollector(),
            new OsCollector(),
            new NetworkCollector(),
            new SoftwareCollector { IncludeSystemUpdates = includeUpdates }
        };

        var (privKey, _) = RsaSigner.GenerateKeyPair();
        var orchestrator = new AuditOrchestrator(
            collectors, new RsaSigner(), privKey);

        if (!quiet)
        {
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .Start("[bold]Recolectando datos del sistema...[/]", ctx =>
                {
                    ctx.Status("[grey]Sistema...[/]");
                    Thread.Sleep(300);
                    ctx.Status("[grey]Hardware (CPU, RAM, Discos)...[/]");
                    Thread.Sleep(300);
                    ctx.Status("[grey]Software instalado...[/]");
                    Thread.Sleep(300);
                    ctx.Status("[grey]Red y parches...[/]");

                    // Ejecutar sincronamente dentro del Status
                    var task = orchestrator.ExecuteAuditAsync();
                    task.Wait();
                    return task.Result;
                });
        }
        else
        {
            return await orchestrator.ExecuteAuditAsync();
        }

        // Este punto no se alcanza si estamos dentro de Status,
        // pero el compilador lo necesita
        throw new InvalidOperationException("Auditoria no completada");
    }

    /// <summary>
    /// Genera el archivo de salida en el formato solicitado.
    /// </summary>
    static string GenerateOutput(
        AuditAgent.Core.Models.AuditReport report,
        string format,
        string basePath)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => GenerateJson(report, basePath),
            "html" => GenerateHtml(report, basePath),
            "pdf" => GeneratePdf(report, basePath),
            "csv" => GenerateCsv(report, basePath),
            _ => throw new ArgumentException($"Formato no soportado: {format}")
        };
    }

    static string GenerateJson(AuditAgent.Core.Models.AuditReport report, string basePath)
    {
        var path = basePath + ".json";
        var orchestrator = new AuditOrchestrator(
            new List<AuditAgent.Core.Interfaces.ICollector>(), null, null);
        var json = orchestrator.SerializeReport(report);
        File.WriteAllText(path, json);
        return path;
    }

    static string GenerateHtml(AuditAgent.Core.Models.AuditReport report, string basePath)
    {
        var path = basePath + ".html";
        var html = HtmlReportGenerator.Generate(report);
        File.WriteAllText(path, html);
        return path;
    }

    static string GeneratePdf(AuditAgent.Core.Models.AuditReport report, string basePath)
    {
        var path = basePath + ".pdf";
        var pdfBytes = PdfReportGenerator.Generate(report);
        File.WriteAllBytes(path, pdfBytes);
        return path;
    }

    static string GenerateCsv(AuditAgent.Core.Models.AuditReport report, string basePath)
    {
        var path = basePath + "_software.csv";
        using var writer = new StreamWriter(path);
        writer.WriteLine("Nombre,Version,Fabricante,Fecha Instalacion,Tamano MB,Fuente,Arquitectura");

        foreach (var sw in report.InstalledSoftware)
        {
            var name = sw.Name?.Replace(",", ";") ?? "";
            var version = sw.Version ?? "";
            var pub = sw.Publisher?.Replace(",", ";") ?? "";
            var date = sw.InstallDate ?? "";
            var size = sw.EstimatedSizeMb;
            writer.WriteLine($"{name},{version},{pub},{date},{size},{sw.Source},{sw.Architecture}");
        }
        return path;
    }

    static void PrintHelp()
    {
        AnsiConsole.MarkupLine(@"[bold blue]AuditAgent CLI[/] - Herramienta de Auditoria de Sistema

[bold]Uso:[/] AuditAgent.CLI.exe [opciones]

[bold]Opciones:[/]
  [green]-o, --output <ruta>[/]   Ruta base para los archivos generados
  [green]-f, --format <fmt>[/]    Formato(s): json, html, pdf, csv (separados por coma)
  [green]--no-updates[/]         Excluir actualizaciones del sistema
  [green]-q, --quiet[/]           Modo silencioso (solo JSON)
  [green]-h, --help[/]            Mostrar esta ayuda

[bold]Ejemplos:[/]
  AuditAgent.CLI.exe
  AuditAgent.CLI.exe -f html,pdf
  AuditAgent.CLI.exe -o C:\\auditoria\\pc-001 -f pdf,csv
  AuditAgent.CLI.exe --no-updates -q
  AuditAgent.CLI.exe -f html -o .\\reportes\\oficina_contabilidad
");
    }
}
