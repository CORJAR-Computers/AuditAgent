using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuditAgent.Core.Models;
using AuditAgent.Core.Services;
using AuditAgent.Collectors;
using AuditAgent.Security;
using System.Security.Cryptography;
using AuditAgent.CLI;

namespace AuditAgent.GUI
{
    public partial class MainWindow : Window
    {
        private AuditReport? _report;
        private CancellationTokenSource? _cts;
        private RSA? _rsaKey;
        private string _outputDir = string.Empty;
        private readonly ObservableCollection<SoftwareRow> _softwareList = new();
        private List<SoftwareRow> _allSoftware = new();

        public MainWindow()
        {
            InitializeComponent();
            DgSoftware.ItemsSource = _softwareList;
            TxtTimestamp.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show(
                    "AuditAgent requiere privilegios de administrador para acceder a toda la informacion del sistema.\n" +
                    "La aplicacion se cerrara.",
                    "Privilegios insuficientes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Close();
                return;
            }

            TxtStatusBar.Text = "Cargando informacion del sistema...";

            try
            {
                await LoadBasicSystemInfo();
                TxtStatusBar.Text = "Listo";
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = $"Error al cargar informacion del sistema: {ex.Message}";
            }
        }

        private async Task LoadBasicSystemInfo()
        {
            var systemCollector = new SystemCollector();
            var osCollector = new OsCollector();

            var quickReport = new AuditReport();
            await systemCollector.CollectAsync(quickReport, CancellationToken.None);
            await osCollector.CollectAsync(quickReport, CancellationToken.None);

            Dispatcher.Invoke(() =>
            {
                TxtComputerName.Text = quickReport.Computer?.ComputerName ?? "---";
                TxtManufacturer.Text = quickReport.Computer?.Manufacturer ?? "---";
                TxtModel.Text = quickReport.Computer?.Model ?? "---";
                TxtSerial.Text = quickReport.Computer?.SerialNumber ?? "---";
                TxtDomain.Text = quickReport.Computer?.Domain ?? "(Workgroup)";
                TxtSystemType.Text = quickReport.Computer?.SystemType ?? "---";
                TxtUuid.Text = quickReport.Computer?.SystemUuid ?? "---";
                TxtOS.Text = quickReport.OperatingSystem?.Caption ?? "---";
            });
        }

        private async void BtnStartAudit_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            _report = new AuditReport();

            BtnStartAudit.IsEnabled = false;
            BtnCancel.IsEnabled = true;
            BtnGenerate.IsEnabled = false;
            CardSummary.Visibility = Visibility.Collapsed;
            CardSoftware.Visibility = Visibility.Collapsed;
            WarningsList.ItemsSource = null;

            ResetSteps();
            SetStatus("Auditoria en progreso...", "Orange");

            var sw = Stopwatch.StartNew();
            var warnings = new List<string>();

            try
            {
                // Paso 1: Sistema
                await RunStep("StepSystem", "Recopilando informacion del sistema...", 10, async () =>
                {
                    var collector = new SystemCollector();
                    await collector.CollectAsync(_report, _cts.Token);
                });

                // Paso 2: Hardware
                await RunStep("StepHardware", "Recopilando informacion de hardware...", 30, async () =>
                {
                    var collector = new HardwareCollector();
                    await collector.CollectAsync(_report, _cts.Token);
                });

                // Paso 3: Software
                await RunStep("StepSoftware", "Recopilando software instalado (esto puede tardar)...", 55, async () =>
                {
                    var collector = new SoftwareCollector();
                    await collector.CollectAsync(_report, _cts.Token);
                });

                // Paso 4: S.O.
                await RunStep("StepOS", "Recopilando informacion del sistema operativo...", 72, async () =>
                {
                    var collector = new OsCollector();
                    await collector.CollectAsync(_report, _cts.Token);
                });

                // Paso 5: Red
                await RunStep("StepNetwork", "Recopilando configuracion de red...", 85, async () =>
                {
                    var collector = new NetworkCollector();
                    await collector.CollectAsync(_report, _cts.Token);
                });

                // Paso 6: Firma digital
                await RunStep("StepSigning", "Firmando digitalmente el informe...", 95, async () =>
                {
                    var keyPair = RsaSigner.GenerateKeyPair();
                    _rsaKey = keyPair.PrivateKey;
                    var signer = new RsaSigner();
                    var orchestrator = new AuditOrchestrator(Array.Empty<AuditAgent.Core.Interfaces.ICollector>());
                    var json = orchestrator.SerializeReport(_report);
                    var hash = AuditOrchestrator.ComputeHashFromJson(json);
                    _report.DigitalSignature = signer.Sign(hash, _rsaKey);
                    _report.ReportHash = hash;
                });

                sw.Stop();
                _report.AuditDurationMs = sw.ElapsedMilliseconds;

                Progress(100, "Auditoria completada");
                SetStatus("Auditoria completada exitosamente", "Green");

                // Mostrar resumen
                ShowSummary(sw.Elapsed);
                ShowSoftwareTable();
                BtnGenerate.IsEnabled = true;

                // Actualizar info del sistema con datos completos
                UpdateSystemInfo();
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                SetStatus("Auditoria cancelada por el usuario", "Red");
                ResetSteps();
            }
            catch (Exception ex)
            {
                sw.Stop();
                SetStatus($"Error en la auditoria: {ex.Message}", "Red");
                warnings.Add($"Error: {ex.Message}");
            }
            finally
            {
                BtnStartAudit.IsEnabled = true;
                BtnCancel.IsEnabled = false;
            }

            if (warnings.Count > 0)
                WarningsList.ItemsSource = warnings;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            BtnCancel.IsEnabled = false;
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_report == null) return;

            _outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents", "AuditAgent", "Reports",
                _report.Computer?.ComputerName ?? "Unknown",
                DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));

            Directory.CreateDirectory(_outputDir);
            TxtOutputPath.Text = $"Carpeta de salida: {_outputDir}";
            TxtStatusBar.Text = "Generando informes...";
            BtnGenerate.IsEnabled = false;

            var generated = new List<string>();

            try
            {
                if (ChkHtml.IsChecked == true)
                {
                    await Task.Run(() =>
                    {
                        var html = HtmlReportGenerator.Generate(_report);
                        var path = Path.Combine(_outputDir, $"Auditoria_{_report.Computer?.ComputerName ?? "PC"}.html");
                        File.WriteAllText(path, html, System.Text.Encoding.UTF8);
                        generated.Add(path);
                    });
                }

                if (ChkPdf.IsChecked == true)
                {
                    await Task.Run(() =>
                    {
                        var pdfBytes = PdfReportGenerator.Generate(_report);
                        var path = Path.Combine(_outputDir, $"Auditoria_{_report.Computer?.ComputerName ?? "PC"}.pdf");
                        File.WriteAllBytes(path, pdfBytes);
                        generated.Add(path);
                    });
                }

                if (ChkJson.IsChecked == true)
                {
                    await Task.Run(() =>
                    {
                        var orchestrator = new AuditOrchestrator(Array.Empty<AuditAgent.Core.Interfaces.ICollector>());
                        var json = orchestrator.SerializeReport(_report);
                        var path = Path.Combine(_outputDir, $"Auditoria_{_report.Computer?.ComputerName ?? "PC"}.json");
                        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
                        generated.Add(path);
                    });
                }

                if (ChkCsv.IsChecked == true)
                {
                    await Task.Run(() =>
                    {
                        var csv = ExportCsv(_report);
                        var path = Path.Combine(_outputDir, $"Auditoria_{_report.Computer?.ComputerName ?? "PC"}_Software.csv");
                        File.WriteAllText(path, csv);
                        generated.Add(path);
                    });
                }

                TxtStatusBar.Text = $"Se generaron {generated.Count} informe(s) exitosamente";
                SetStatus("Informes generados", "Green");

                var result = MessageBox.Show(
                    $"Se generaron {generated.Count} archivo(s) en:\n{_outputDir}\n\nDesea abrir la carpeta?",
                    "Informes Generados",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    Process.Start("explorer.exe", _outputDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar informes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatusBar.Text = "Error al generar informes";
            }
            finally
            {
                BtnGenerate.IsEnabled = true;
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_outputDir))
                Process.Start("explorer.exe", _outputDir);
            else
            {
                var docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "AuditAgent", "Reports");
                if (Directory.Exists(docs))
                    Process.Start("explorer.exe", docs);
                else
                    MessageBox.Show("Aun no se han generado informes.", "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // --- Helpers ---

        private async Task RunStep(string stepName, string message, int percent, Func<Task> action)
        {
            Dispatcher.Invoke(() =>
            {
                var step = (TextBlock)FindName(stepName);
                if (step != null) step.Text = step.Text.Replace("○", "◎");
                TxtProgressText.Text = message;
            });

            Progress(percent, message);
            await action();

            Dispatcher.Invoke(() =>
            {
                var step = (TextBlock)FindName(stepName);
                if (step != null)
                {
                    step.Text = step.Text.Replace("◎", "✓");
                    step.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#28A745"));
                }
            });
        }

        private void Progress(int value, string text)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = value;
                TxtProgressPercent.Text = $"{value}%";
            });
        }

        private void SetStatus(string text, string color)
        {
            Dispatcher.Invoke(() =>
            {
                TxtAuditStatus.Text = text;
                TxtAuditStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color switch
                    {
                        "Green" => "#28A745",
                        "Orange" => "#F0AD4E",
                        "Red" => "#DC3545",
                        _ => "#28A745"
                    }));
                TxtStatusBar.Text = text;
            });
        }

        private void ResetSteps()
        {
            foreach (var name in new[] { "StepSystem", "StepHardware", "StepSoftware", "StepOS", "StepNetwork", "StepSigning" })
            {
                var step = (TextBlock)FindName(name);
                if (step != null)
                {
                    var label = name.Replace("Step", "");
                    step.Text = $"○ {label}";
                    step.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#999999"));
                }
            }
            ProgressBar.Value = 0;
            TxtProgressText.Text = "";
            TxtProgressPercent.Text = "";
        }

        private void ShowSummary(TimeSpan elapsed)
        {
            CardSummary.Visibility = Visibility.Visible;
            TxtSoftwareCount.Text = _report?.InstalledSoftware?.Count.ToString() ?? "0";
            TxtPatchCount.Text = _report?.SecurityPatches?.Count.ToString() ?? "0";
            TxtHardwareInfo.Text = _report?.Hardware?.Disks?.Count.ToString() ?? "0";
            TxtNetworkInfo.Text = _report?.NetworkAdapters?.Count.ToString() ?? "0";
            TxtDuration.Text = $"{elapsed.TotalSeconds:F1} segundos";
            TxtSignature.Text = _report?.DigitalSignature != null
                ? $"RSA-4096/SHA-256 ({_report.DigitalSignature.Length} bytes)"
                : "Sin firma";

            if (_report?.Warnings?.Count > 0)
                WarningsList.ItemsSource = _report.Warnings.Select(w => $"[{w.Category}] {w.Message}").ToList();
        }

        private void ShowSoftwareTable()
        {
            CardSoftware.Visibility = Visibility.Visible;
            _allSoftware = _report?.InstalledSoftware
                ?.Select((s, i) => new SoftwareRow
                {
                    RowNumber = i + 1,
                    Name = s.DisplayName,
                    Version = s.Version ?? "",
                    Publisher = s.Publisher ?? "",
                    InstallDate = s.InstallDate ?? "",
                    Size = s.EstimatedSizeMb > 0 ? $"{((double)s.EstimatedSizeMb / 1024.0 / 1024.0):F1} MB" : ""
                }).ToList() ?? new List<SoftwareRow>();

            _softwareList.Clear();
            foreach (var item in _allSoftware)
                _softwareList.Add(item);

            TxtSoftwareTotal.Text = $"{_allSoftware.Count} programas";
        }

        private void UpdateSystemInfo()
        {
            if (_report?.Computer != null)
            {
                TxtComputerName.Text = _report.Computer.ComputerName;
                TxtManufacturer.Text = _report.Computer.Manufacturer;
                TxtModel.Text = _report.Computer.Model;
                TxtSerial.Text = _report.Computer.SerialNumber;
                TxtDomain.Text = _report.Computer.Domain ?? "(Workgroup)";
                TxtSystemType.Text = _report.Computer.SystemType;
                TxtUuid.Text = _report.Computer.SystemUuid;
            }
            if (_report?.OperatingSystem != null)
                TxtOS.Text = _report.OperatingSystem.Caption;
        }

        private static string ExportCsv(AuditReport report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("#;Nombre;Version;Editor;Fecha Instalacion;Tamano (MB);Ubicacion Registro");
            int i = 1;
            foreach (var sw in report.InstalledSoftware ?? Enumerable.Empty<SoftwareInfo>())
            {
                var size = sw.EstimatedSizeMb > 0 ? ((double)sw.EstimatedSizeMb / 1024.0 / 1024.0).ToString("F1") : "";
                var name = sw.DisplayName?.Replace(";", ",") ?? "";
                var publisher = sw.Publisher?.Replace(";", ",") ?? "";
                sb.AppendLine($"{i};{name};{sw.Version};{publisher};{sw.InstallDate};{size};{sw.RegistryKey ?? ""}");
                i++;
            }
            return sb.ToString();
        }

        private static bool IsRunningAsAdmin()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        // --- Search ---

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtSearch.Text == "Buscar software...")
                TxtSearch.Text = "";
        }

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSearch.Text))
                TxtSearch.Text = "Buscar software...";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSearch.Text == "Buscar software...") return;

            var filter = TxtSearch.Text.ToLowerInvariant();
            _softwareList.Clear();

            var filtered = string.IsNullOrWhiteSpace(filter)
                ? _allSoftware
                : _allSoftware.Where(s =>
                    (s.Name?.ToLowerInvariant().Contains(filter) == true) ||
                    (s.Publisher?.ToLowerInvariant().Contains(filter) == true) ||
                    (s.Version?.ToLowerInvariant().Contains(filter) == true));

            foreach (var item in filtered)
                _softwareList.Add(item);
        }
    }

    public class SoftwareRow
    {
        public int RowNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string InstallDate { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
    }
}