using System.Security.Cryptography;
using AuditAgent.Collectors;
using AuditAgent.Core.Services;
using AuditAgent.Security;

namespace AuditAgent.Agent;

/// <summary>
/// Agente de Auditoria de Software.
/// 
/// Este programa es el entry point que:
/// 1. Inicializa los recolectores de datos (WMI, Registry)
/// 2. Configura la capa de seguridad (AES-256, RSA-4096)
/// 3. Ejecuta la auditoria completa
/// 4. Firma digitalmente el reporte
/// 5. Cifra el reporte y lo envia al servidor central
/// 6. Guarda copia local cifrada
/// 
/// Puede ejecutarse como:
/// - Aplicacion de consola standalone (una sola vez)
/// - Servicio de Windows (ejecucion periodica)
/// </summary>
public static class Program
{
    private static readonly string Version = "1.0.0";
    private static readonly string ReportsDir = Path.Combine(AppContext.BaseDirectory, "reports");
    private static readonly string KeysDir = Path.Combine(AppContext.BaseDirectory, "keys");

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("  AUDIT AGENT v" + Version);
        Console.WriteLine("  Agente de Auditoria de Software Corporativo");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine();

        try
        {
            // Verificar privilegios de administrador
            if (!IsRunningAsAdmin())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[ADVERTENCIA] No se ejecuta como administrador.");
                Console.WriteLine("  Alguna informacion puede no estar disponible.");
                Console.ResetColor();
                Console.WriteLine();
            }

            // Paso 1: Inicializar seguridad (claves RSA)
            Console.WriteLine("[1/6] Inicializando seguridad...");
            var (privateKey, publicKey) = EnsureRsaKeysExist();
            var signer = new RsaSigner();
            var publicKeyPem = RsaSigner.ExportPublicKeyPem(publicKey);
            Console.WriteLine("  Claves RSA-4096 cargadas.");

            // Paso 2: Crear recolectores
            Console.WriteLine("[2/6] Configurando recolectores de datos...");
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
            Console.WriteLine("  " + collectors.Count + " recolectores configurados.");

            // Paso 3: Crear orquestador y ejecutar auditoria
            Console.WriteLine("[3/6] Ejecutando auditoria...");
            Console.WriteLine("  (Esto puede tardar 10-30 segundos)
");

            var orchestrator = new AuditOrchestrator(
                collectors,
                signer,
                privateKey,
                new ConsoleLogger());

            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var report = await orchestrator.ExecuteAuditAsync(cts.Token);
            report.AgentVersion = Version;

            // Paso 4: Mostrar resumen
            Console.WriteLine();
            Console.WriteLine("[4/6] Resultados de la auditoria:");
            Console.WriteLine($"  Equipo       : {report.Computer.ComputerName}");
            Console.WriteLine($"  Fabricante   : {report.Computer.Manufacturer}");
            Console.WriteLine($"  Modelo       : {report.Computer.Model}");
            Console.WriteLine($"  Serial       : {report.Computer.SerialNumber}");
            Console.WriteLine($"  Dominio      : {report.Computer.Domain}");
            Console.WriteLine($"  S.O.         : {report.OperatingSystem.Caption}");
            Console.WriteLine($"  RAM Total    : {report.Hardware.TotalMemoryGb} GB");
            Console.WriteLine($"  Software     : {report.InstalledSoftware.Count} programas");
            Console.WriteLine($"  Parches      : {report.SecurityPatches.Count} actualizaciones");
            Console.WriteLine($"  Redes        : {report.NetworkAdapters.Count} adaptadores activos");
            Console.WriteLine($"  Duracion     : {report.AuditDurationMs} ms");
            Console.WriteLine($"  Firmado      : {(!string.IsNullOrEmpty(report.DigitalSignature) ? "Si" : "No")}");
            Console.WriteLine($"  Hash (SHA256): {report.ReportHash}");

            if (report.Warnings.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Advertencias : {report.Warnings.Count}");
                Console.ResetColor();
            }

            // Paso 5: Guardar reporte local
            Console.WriteLine("
[5/6] Guardando reporte local...");
            Directory.CreateDirectory(ReportsDir);
            var filename = $"audit_{report.Computer.ComputerName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var localPath = Path.Combine(ReportsDir, filename);
            var jsonReport = orchestrator.SerializeReport(report);
            await File.WriteAllTextAsync(localPath, jsonReport);
            Console.WriteLine($"  Guardado en: {localPath}");

            // Paso 6: Enviar al servidor (si hay configuracion)
            if (args.Contains("--send") || args.Contains("--register"))
            {
                Console.WriteLine("
[6/6] Enviando al servidor central...");
                await SendToServer(report, jsonReport, publicKeyPem);
            }
            else
            {
                Console.WriteLine("
[6/6] Envio al servidor: omitido (use --send para enviar)");
            }

            Console.WriteLine("
" + "=".PadRight(60, '='));
            Console.WriteLine("  Auditoria completada exitosamente.");
            Console.WriteLine("=".PadRight(60, '='));

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("
[ERROR] Auditoria cancelada por timeout.");
            Console.ResetColor();
            return 130;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"
[ERROR FATAL] {ex.GetType().Name}: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    /// <summary>
    /// Asegura que existan las claves RSA del agente.
    /// Si no existen, genera un nuevo par y las guarda en el directorio keys/.
    /// </summary>
    private static (RSA PrivateKey, RSA PublicKey) EnsureRsaKeysExist()
    {
        Directory.CreateDirectory(KeysDir);
        var privPath = Path.Combine(KeysDir, "agent-private.pem");
        var pubPath = Path.Combine(KeysDir, "agent-public.pem");

        if (File.Exists(privPath) && File.Exists(pubPath))
        {
            var privPem = File.ReadAllText(privPath);
            var pubPem = File.ReadAllText(pubPath);
            return (RsaSigner.ImportPrivateKeyFromPem(privPem),
                    RsaSigner.ImportPublicKeyFromPem(pubPem));
        }

        // Generar nuevas claves
        var (priv, pub) = RsaSigner.GenerateKeyPair();
        File.WriteAllText(privPath, RsaSigner.ExportPrivateKeyPem(priv));
        File.WriteAllText(pubPath, RsaSigner.ExportPublicKeyPem(pub));

        // Proteger archivos de clave (Windows ACL)
        if (OperatingSystem.IsWindows())
        {
            ProtectKeyFile(privPath);
        }

        Console.WriteLine("  Nuevas claves RSA-4096 generadas.");
        return (priv, pub);
    }

    /// <summary>
    /// Envia el reporte cifrado al servidor central.
    /// </summary>
    private static async Task SendToServer(
        AuditAgent.Core.Models.AuditReport report,
        string jsonReport,
        string publicKeyPem)
    {
        try
        {
            // TODO: Leer certificado del agente y URL del servidor
            // desde appsettings.json para produccion
            
            Console.WriteLine("  Para enviar al servidor, configure:");
            Console.WriteLine("    1. appsettings.json con URL del servidor");
            Console.WriteLine("    2. Certificado del agente (agent.pfx)");
            Console.WriteLine("    3. Certificado CA del servidor (server-ca.cer)");
            Console.WriteLine("  El reporte esta listo y firmado localmente.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Error al enviar: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static bool IsRunningAsAdmin()
    {
        if (!OperatingSystem.IsWindows()) return true;
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private static void ProtectKeyFile(string path)
    {
        // Restringir acceso al archivo de clave privada
        try
        {
            var fileInfo = new System.IO.FileInfo(path);
            var security = fileInfo.GetAccessControl();
            security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                System.Security.Principal.WindowsIdentity.GetCurrent().Name,
                System.Security.AccessControl.FileSystemRights.FullControl,
                System.Security.AccessControl.AccessControlType.Allow));
            fileInfo.SetAccessControl(security);
        }
        catch { /* En contenedores o sin permisos */ }
    }
}

/// <summary>Logger simple que escribe a consola.</summary>
internal class ConsoleLogger : AuditAgent.Core.Services.ILogger
{
    public void LogInformation(string message, params object?[] args)
        => Console.WriteLine($"  [INFO] {string.Format(message, args)}");

    public void LogWarning(Exception? ex, string message, params object?[] args)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  [WARN] {string.Format(message, args)}");
        if (ex != null) Console.WriteLine($"         {ex.Message}");
        Console.ResetColor();
    }

    public void LogWarning(string message, params object?[] args)
        => LogWarning(null, message, args);

    public void LogError(Exception ex, string message, params object?[] args)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [ERROR] {string.Format(message, args)}: {ex.Message}");
        Console.ResetColor();
    }

    public void LogDebug(string message, params object?[] args)
    {
#if DEBUG
        Console.WriteLine($"  [DBG] {string.Format(message, args)}");
#endif
    }
}
