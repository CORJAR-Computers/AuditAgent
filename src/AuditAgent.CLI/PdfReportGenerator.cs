using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AuditAgent.Core.Models;

namespace AuditAgent.CLI;

/// <summary>
/// Genera reportes PDF profesionales usando QuestPDF.
/// Layout tipo informe corporativo con tablas, secciones y colores.
/// </summary>
public static class PdfReportGenerator
{
    public static byte[] Generate(AuditReport report)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                // === HEADER ===
                page.Header().Element(c => ComposeHeader(c, report));

                // === CONTENT ===
                page.Content().Element(c => ComposeContent(c, report));

                // === FOOTER ===
                page.Footer().Element(ComposeFooter);
            });
        });

        return doc.GeneratePdf();
    }

    static void ComposeHeader(IContainer c, AuditReport report)
    {
        c.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("REPORTE DE AUDITORIA").Bold().FontSize(20).FontColor("#2c3e50");
                    col.Item().Text($"Equipo: {report.Computer.ComputerName}").FontSize(11).FontColor("#7f8c8d");
                });
                row.ConstantItem(120).Column(col =>
                {
                    col.Item().AlignRight().Text("CORJAR").Bold().FontSize(16).FontColor("#3498db");
                    col.Item().AlignRight().Text("Computers").FontSize(10).FontColor("#7f8c8d");
                });
            });
            column.Item().PaddingTop(5).LineHorizontal(1).LineColor("#3498db");
        });
    }

    static void ComposeContent(IContainer c, AuditReport report)
    {
        c.PaddingVertical(10).Column(column =>
        {
            // --- Resumen ---
            Section(column.Item(), "Resumen de la Auditoria", comp =>
        {
            comp.Row(row =>
            {
                row.Spacing(10);
                StatColumn(row, "Programas", $"{report.InstalledSoftware.Count}");
                StatColumn(row, "Parches", $"{report.SecurityPatches.Count}");
                StatColumn(row, "Redes", $"{report.NetworkAdapters.Count}");
                StatColumn(row, "RAM", $"{report.Hardware.TotalMemoryGb} GB");
                StatColumn(row, "Duracion", $"{report.AuditDurationMs} ms");
            });
        });

            // --- Equipo ---
            Section(column.Item(), "Informacion del Equipo", comp =>
            {
            comp.Grid(grid =>
            {
                grid.Columns(2);
                grid.Spacing(5);
                Field(grid, "Fabricante", report.Computer.Manufacturer);
                Field(grid, "Modelo", report.Computer.Model);
                Field(grid, "Tipo", report.Computer.SystemType);
                Field(grid, "Numero de Serie", report.Computer.SerialNumber);
                Field(grid, "Dominio", report.Computer.Domain);
                Field(grid, "UUID", report.Computer.SystemUuid);
                Field(grid, "Usuario Actual", report.Computer.CurrentUser);
                Field(grid, "MAC Principal", report.Computer.PrimaryMacAddress);
            });
        });

            // --- SO ---
            Section(column.Item(), "Sistema Operativo", comp =>
            {
            comp.Grid(grid =>
            {
                grid.Columns(2);
                grid.Spacing(5);
                Field(grid, "Nombre", report.OperatingSystem.Caption);
                Field(grid, "Version", report.OperatingSystem.Version);
                Field(grid, "Build", report.OperatingSystem.BuildNumber);
                Field(grid, "Arquitectura", report.OperatingSystem.OSArchitecture);
                Field(grid, "Instalado", report.OperatingSystem.InstallDate?.ToString("yyyy-MM-dd") ?? "N/A");
                Field(grid, "Ultimo Arranque", report.OperatingSystem.LastBootUpTime?.ToLocalTime().ToString("g") ?? "N/A");
            });
        });

            // --- Hardware ---
            var cpu = report.Hardware.Processors.FirstOrDefault();
            Section(column.Item(), "Hardware", comp =>
            {
            comp.Grid(grid =>
            {
                grid.Columns(2);
                grid.Spacing(5);
                Field(grid, "CPU", cpu?.Name ?? "N/A");
                Field(grid, "Cores / Hilos", $"{report.Hardware.Processors.Sum(p => p.NumberOfCores)} / {report.Hardware.Processors.Sum(p => p.NumberOfLogicalProcessors)}");
                Field(grid, "RAM Total", $"{report.Hardware.TotalMemoryGb} GB");
                Field(grid, "Placa Base", $"{report.Hardware.BaseBoardManufacturer} - {report.Hardware.BaseBoardProduct}");
                Field(grid, "BIOS", $"{report.Hardware.BiosVersion} ({report.Hardware.BiosManufacturer})");
                if (report.Hardware.Battery is not null)
                    Field(grid, "Bateria", $"{report.Hardware.Battery.Name} - {report.Hardware.Battery.Status} ({report.Hardware.Battery.EstimatedChargeRemaining}%)");
            });
        });

            // --- Discos ---
            if (report.Hardware.Disks.Count > 0)
            {
                Section(column.Item(), "Almacenamiento", comp =>
                {
                comp.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(3);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Modelo");
                        HeaderCell(header.Cell(), "Tipo");
                        HeaderCell(header.Cell(), "Capacidad");
                        HeaderCell(header.Cell(), "Particiones");
                    });

                    foreach (var disk in report.Hardware.Disks)
                    {
                        var parts = string.Join(", ", disk.Partitions.Select(p =>
                            $"{p.DriveLetter} {p.FreeSpaceGb}/{p.SizeGb} GB"));
                        table.Cell().Text(disk.Model);
                        table.Cell().Text($"{disk.MediaType} ({disk.InterfaceType})");
                        table.Cell().Text($"{disk.SizeGb} GB");
                        table.Cell().Text(parts);
                    }
                });
            });
        }

            // --- Red ---
            if (report.NetworkAdapters.Count > 0)
            {
                Section(column.Item(), "Red", comp =>
                {
                comp.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Adaptador");
                        HeaderCell(header.Cell(), "MAC");
                        HeaderCell(header.Cell(), "IP");
                        HeaderCell(header.Cell(), "DNS");
                        HeaderCell(header.Cell(), "DHCP");
                    });

                    foreach (var net in report.NetworkAdapters)
                    {
                        table.Cell().Text(net.Name);
                        table.Cell().Text(net.MacAddress);
                        table.Cell().Text(string.Join(", ", net.IpAddresses));
                        table.Cell().Text(net.DnsServer);
                        table.Cell().Text(net.DhcpEnabled);
                    }
                });
            });
        }

            // --- Software (tabla larga, con salto de pagina automatico) ---
            Section(column.Item(), $"Software Instalado ({report.InstalledSoftware.Count} programas)", comp =>
            {
            comp.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "#");
                    HeaderCell(header.Cell(), "Nombre");
                    HeaderCell(header.Cell(), "Version");
                    HeaderCell(header.Cell(), "Fabricante");
                    HeaderCell(header.Cell(), "Fecha");
                    HeaderCell(header.Cell(), "Tamano");
                });

                var i = 1;
                foreach (var sw in report.InstalledSoftware)
                {
                    table.Cell().Text(i++.ToString()).FontSize(8);
                    table.Cell().Text(sw.Name).FontSize(8);
                    table.Cell().Text(sw.Version).FontSize(8);
                    table.Cell().Text(sw.Publisher).FontSize(8);
                    table.Cell().Text(sw.InstallDate).FontSize(8);
                    table.Cell().Text(sw.EstimatedSizeMb.HasValue ? $"{sw.EstimatedSizeMb:N0} MB" : "").FontSize(8);
                }
            });
        });

            // --- Parches ---
            if (report.SecurityPatches.Count > 0)
            {
                Section(column.Item(), $"Parches de Seguridad ({report.SecurityPatches.Count})", comp =>
                {
                comp.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "KB");
                        HeaderCell(header.Cell(), "Descripcion");
                        HeaderCell(header.Cell(), "Fecha");
                        HeaderCell(header.Cell(), "Instalado por");
                    });

                    foreach (var p in report.SecurityPatches)
                    {
                        table.Cell().Text(p.HotFixId).FontSize(8);
                        table.Cell().Text(p.Description).FontSize(8);
                        table.Cell().Text(p.InstalledOn).FontSize(8);
                        table.Cell().Text(p.InstalledBy).FontSize(8);
                    }
                });
            });
        }

            // --- Warnings ---
            if (report.Warnings.Count > 0)
            {
                Section(column.Item(), "Advertencias", comp =>
                {
                    comp.Column(wCol =>
                    {
                        foreach (var w in report.Warnings)
                        {
                            wCol.Item().Text($"[{w.Category}] {w.Message}").FontSize(8).FontColor("#e67e22");
                            if (!string.IsNullOrEmpty(w.Details))
                                wCol.Item().Text($"  {w.Details}").FontSize(7).FontColor("#95a5a6");
                        }
                    });
                });
            }
        });
    }

    static void ComposeFooter(IContainer c)
    {
        c.Column(column =>
        {
            column.Item().AlignCenter().Text(x =>
            {
                x.Span("Generado por AuditAgent v1.0.0 | CORJAR Computers").FontSize(8).FontColor("#95a5a6");
                x.Span("   |   ");
                x.Span("Documento confidencial - Uso interno").FontSize(8).FontColor("#bdc3c7");
            });

            column.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor("#ecf0f1");
            column.Item().AlignCenter().Text(x =>
            {
                x.Span("Pagina ");
                x.CurrentPageNumber();
                x.Span(" de ");
                x.TotalPages();
            });
        });
    }

    // --- Helpers ---
    static void Section(IContainer c, string title, Action<IContainer> content)
    {
        c.PaddingVertical(8).Column(column =>
        {
            column.Item().LineHorizontal(0.5f).LineColor("#ecf0f1");
            column.Item().PaddingTop(5).Text(title).SemiBold().FontSize(13).FontColor("#2c3e50");
            column.Item().PaddingTop(5).Element(content);
        });
    }

    static void StatColumn(QuestPDF.Fluent.RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Column(col =>
        {
            col.Item().Background("#f8f9fa").Padding(8)
                .AlignCenter().Text(value).Bold().FontSize(16).FontColor("#3498db");
            col.Item().AlignCenter().Text(label).FontSize(8).FontColor("#7f8c8d");
        });
    }

    static void Field(GridDescriptor grid, string label, string value)
    {
        grid.Item().Text(label).SemiBold().FontSize(9).FontColor("#34495e");
        grid.Item().Text(value ?? "N/A").FontSize(9);
    }

    static void HeaderCell(IContainer cell, string text)
    {
        cell.Background("#34495e").PaddingVertical(4).PaddingHorizontal(6)
            .Text(text).FontColor(QuestPDF.Helpers.Colors.White).Bold().FontSize(8);
    }
}
