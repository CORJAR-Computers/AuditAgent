using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using AuditAgent.Collectors;
using AuditAgent.Core.Services;
using AuditAgent.Security;
using Spectre.Console;

namespace AuditAgent.Agent;

/// <summary>
/// Agente de Auditoria de Software.
/// Ejecuta la auditoria completa y permite al tecnico elegir
/// el formato de salida del informe (JSON, HTML, PDF, CSV).
/// </summary>
public static class Program
{
    private static readonly string Version = "1.0.0";
    private static readonly string ReportsDir = Path.Combine(AppContext.BaseDirectory, "reports");
    private static readonly string KeysDir = Path.Combine(AppContext.BaseDirectory, "keys");

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        try
        {
            // Verificar privilegios de administrador
            if (!IsRunningAsAdmin())
            {
                AnsiConsole.MarkupLine("[yellow][ADVERTENCIA] No se ejecuta como administrador.[/]");
                AnsiConsole.MarkupLine("[yellow]  Alguna informacion puede no estar disponible.[/]");
                Console.WriteLine();
            }

            // Paso 1: Inicializar seguridad
            AnsiConsole.MarkupLine("[bold blue][1/5][/] Inicializando seguridad...");
            var (privateKey, _) = EnsureRsaKeysExist();
            AnsiConsole.MarkupLine("  [green]Claves RSA-4096 cargadas.[/]");

            // Paso 2: Crear recolectores
            AnsiConsole.MarkupLine("[bold blue][2/5][/] Configurando recolectores...");
            var collectors = new List<AuditAgent.Core.Interfaces.ICollector>
            {
                new SystemCollector(),
                new HardwareCollector(),
                new OsCollector(),
                new NetworkCollector(),
                new SoftwareCollector
                {
                    UseWmiProduct = args.Contains("--use-wmi"),
                    IncludeSystemUpdates = !args.Contains("--no-updates")
                }
            };
            AnsiConsole.MarkupLine($"  [green]{collectors.Count} recolectores listos.[/]");

            // Paso 3: Ejecutar auditoria
            AnsiConsole.MarkupLine("[bold blue][3/5][/] Ejecutando auditoria...");
            var report = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("[bold]Recolectando datos...[/]", async _ =>
                {
                    var orch = new AuditOrchestrator(collectors, new RsaSigner(), privateKey);
                    return await orch.ExecuteAuditAsync();
                });
            report.AgentVersion = Version;

            // Paso 4: Mostrar resumen
            Console.WriteLine();
            var grid = new Grid().AddColumn().AddColumn();
            grid.AddRow("[bold]Equipo:[/]", report.Computer.ComputerName);
            grid.AddRow("[bold]Fabricante:[/]", report.Computer.Manufacturer);
            grid.AddRow("[bold]Modelo:[/]", report.Computer.Model);
            grid.AddRow("[bold]Serial:[/]", report.Computer.SerialNumber);
            grid.AddRow("[bold]S.O.:[/]", report.OperatingSystem.Caption);
            grid.AddRow("[bold]RAM:[/]", $"{report.Hardware.TotalMemoryGb} GB");
            grid.AddRow("[bold]Software:[/]", $"{report.InstalledSoftware.Count} programas");
            grid.AddRow("[bold]Parches:[/]", $"{report.SecurityPatches.Count} actualizaciones");
            grid.AddRow("[bold]Duracion:[/]", $"{report.AuditDurationMs} ms");
            AnsiConsole.Write(new Panel(grid).Header("[bold]Resumen[/]").BorderColor(Color.Blue));

            // Paso 5: Seleccionar formato y guardar
            Console.WriteLine();
            AnsiConsole.MarkupLine("[bold blue][5/5][/] Generar informe...\n");

            var format = SelectFormat(args);
            var baseName = $"audit_{report.Computer.ComputerName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var basePath = Path.Combine(ReportsDir, baseName);

            Directory.CreateDirectory(ReportsDir);
            var generated = ExportReport(report, format, basePath);

            Console.WriteLine();
            foreach (var f in generated)
                AnsiConsole.MarkupLine($"[green]  [checkmark] Generado:[/] {f}");

            AnsiConsole.MarkupLine("\n[bold green]Auditoria completada exitosamente.[/]");
            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[red]Auditoria cancelada por timeout.[/]");
            return 130;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]ERROR FATAL:[/] {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Menu interactivo para elegir formato de salida.
    /// Si se paso --format por argumento, lo usa directamente.
    /// </summary>
    static List<string> SelectFormat(string[] args)
    {
        var formatArg = args.FirstOrDefault(a => a.StartsWith("--format="));
        if (formatArg != null)
        {
            return formatArg.Substring(9).Split(',').ToList();
        }

        return AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("[bold]En que formato desea el informe?[/]")
                .PageSize(10)
                .AddChoices(new[] { "json", "html", "pdf", "csv" })
                .InstructionsText("[grey](Espacio para seleccionar, A para todos, Enter para confirmar)[/]"));
    }

    /// <summary>
    /// Exporta el reporte en uno o varios formatos.
    /// </summary>
    static List<string> ExportReport(
        AuditAgent.Core.Models.AuditReport report,
        List<string> formats,
        string basePath)
    {
        var files = new List<string>();
        var orchestrator = new AuditOrchestrator(
            new List<AuditAgent.Core.Interfaces.ICollector>(), null, null);

        foreach (var fmt in formats)
        {
            try
            {
                var path = fmt.ToLowerInvariant() switch
                {
                    "json" => ExportJson(report, orchestrator, basePath + ".json"),
                    "html" => ExportHtml(report, basePath + ".html"),
                    "pdf" => ExportPdf(report, basePath + ".pdf"),
                    "csv" => ExportCsv(report, basePath + "_software.csv").Result,
                    _ => throw new ArgumentException($"Formato: {fmt}")
                };
                files.Add(path);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]  Error ({fmt}): {ex.Message}[/]");
            }
        }
        return files;
    }

    static string ExportJson(AuditAgent.Core.Models.AuditReport report, AuditOrchestrator orch, string path)
    {
        var json = orch.SerializeReport(report);
        File.WriteAllText(path, json);
        return path;
    }

    static string ExportHtml(AuditAgent.Core.Models.AuditReport report, string path)
    {
        var html = AuditAgent.CLI.HtmlReportGenerator.Generate(report);
        File.WriteAllText(path, html);
        return path;
    }

    static string ExportPdf(AuditAgent.Core.Models.AuditReport report, string path)
    {
        var pdf = AuditAgent.CLI.PdfReportGenerator.Generate(report);
        File.WriteAllBytes(path, pdf);
        return path;
    }

    static async Task<string> ExportCsv(AuditAgent.Core.Models.AuditReport report, string path)
    {
        using var w = new StreamWriter(path);
        await w.WriteLineAsync("Nombre,Version,Fabricante,Fecha,Tamano MB,Fuente,Arq");
        foreach (var sw in report.InstalledSoftware)
        {
            await w.WriteLineAsync($"{sw.Name?.Replace(",",";")},{sw.Version},{sw.Publisher?.Replace(",",";")},{sw.InstallDate},{sw.EstimatedSizeMb},{sw.Source},{sw.Architecture}");
        }
        return path;
    }

    /// <summary>
    /// Genera claves RSA y las protege con ACL restrictiva.
    /// FIX: Ahora restringe correctamente solo a Administrators.
    /// </summary>
    static (RSA PrivateKey, RSA PublicKey) EnsureRsaKeysExist()
    {
        Directory.CreateDirectory(KeysDir);
        var privPath = Path.Combine(KeysDir, "agent-private.pem");
        var pubPath = Path.Combine(KeysDir, "agent-public.pem");

        if (File.Exists(privPath) && File.Exists(pubPath))
        {
            return (RsaSigner.ImportPrivateKeyFromPem(File.ReadAllText(privPath)),
                    RsaSigner.ImportPublicKeyFromPem(File.ReadAllText(pubPath)));
        }

        var (priv, pub) = RsaSigner.GenerateKeyPair();
        File.WriteAllText(privPath, RsaSigner.ExportPrivateKeyPem(priv));
        File.WriteAllText(pubPath, RsaSigner.ExportPublicKeyPem(pub));

        if (OperatingSystem.IsWindows())
            ProtectPrivateKeyFile(privPath);

        AnsiConsole.MarkupLine("  [green]Nuevas claves RSA-4096 generadas.[/]");
        return (priv, pub);
    }

    /// <summary>
    /// FIX: Protege la clave privada restringiendo acceso SOLO
    /// al grupo Administrators. Elimina permisos heredados.
    /// </summary>
    static void ProtectPrivateKeyFile(string path)
    {
        try
        {
            // Crear ACL nuevo (vacio, sin herencia)
            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false); // No heredar

            // Solo Administrators tienen acceso total
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            // SYSTEM tambien tiene acceso (necesario para servicios)
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            new FileInfo(path).SetAccessControl(security);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]  No se pudo proteger clave: {ex.Message}[/]");
        }
    }

    static bool IsRunningAsAdmin()
    {
        if (!OperatingSystem.IsWindows()) return true;
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity)
                .IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }
}
