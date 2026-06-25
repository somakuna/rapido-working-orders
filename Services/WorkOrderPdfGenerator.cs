using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RapidoWorkingOrders.Models;

namespace RapidoWorkingOrders.Services;

public static class WorkOrderPdfGenerator
{
    private const string BorderGray = "#555555";

    private static readonly string[] TableHeaders =
        ["Stavka", "Tehnologija", "Materijal", "Napomena", "Str.", "m²", "Kol.", "Uk.m²"];

    private static readonly bool[] TableHeadersAlignRight =
        [false,     false,         false,       false,       true,   true,  true,   true];

    public static void Generate(Work work, List<WorkFile> files, string outputPath, bool a5 = false)
    {
        Document.Create(c => c.Page(p => BuildPage(p, work, files, a5))).GeneratePdf(outputPath);
    }

    public static byte[] GenerateBytes(Work work, List<WorkFile> files, bool a5 = false)
    {
        return Document.Create(c => c.Page(p => BuildPage(p, work, files, a5))).GeneratePdf();
    }

    // ──────────────────────────────────────────────────────────────────

    private static void BuildPage(PageDescriptor page, Work work, List<WorkFile> files, bool a5)
    {
        page.Size(a5 ? PageSizes.A5 : PageSizes.A4);
        page.Margin(15, Unit.Millimetre);
        page.DefaultTextStyle(s => s.FontFamily("Segoe UI").FontSize(a5 ? 8 : 9));

        // ── Header ──
        page.Header().Column(col =>
        {
            col.Item().Row(row =>
            {
                // Lijevo: broj naloga + datum + operater
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text($"RADNI NALOG  {work.FormattedNumber}")
                         .Bold().FontSize(a5 ? 14 : 16);
                    inner.Item().PaddingTop(4).Text(t =>
                    {
                        t.Span("Datum: ").Bold();
                        t.Span(work.WorkDate.HasValue ? work.WorkDate.Value.ToString("d.M.yyyy") : "—");
                    });
                    inner.Item().Text(t =>
                    {
                        t.Span("Klijent: ").Bold();
                        t.Span(work.Client?.Name ?? "—");
                    });
                    inner.Item().Text(t =>
                    {
                        t.Span("Operater: ").Bold();
                        t.Span(work.User?.Name ?? "—");
                    });
                });

                // Desno: veliki naziv klijenta + logo ispod
                row.ConstantItem(a5 ? 130 : 170).Column(right =>
                {
                    right.Item().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.Bold().FontSize(a5 ? 16 : 22));
                        t.AlignRight();
                        t.Span(work.Client?.Name ?? "—");
                    });

                    var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png");
                    if (File.Exists(logoPath))
                        right.Item().PaddingTop(4).AlignRight().Height(22).Image(logoPath).FitArea();
                });
            });

            col.Item().PaddingTop(6).LineHorizontal(1.5f);
        });

        // ── Content ──
        page.Content().PaddingTop(8).Column(col =>
        {
            col.Item().PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (work.OfferNumber.HasValue)
                        DetailRow(left, "Ponuda br.:", work.OfferNumber.Value.ToString());
                    if (!string.IsNullOrWhiteSpace(work.OrderMethod))
                        DetailRow(left, "Narudžba:", work.OrderMethod);
                    if (work.OrderDate.HasValue)
                        DetailRow(left, "Datum narudžbe:", work.OrderDate.Value.ToString("d.M.yyyy"));
                    if (work.DeliveryDate.HasValue)
                        DetailRow(left, "Datum isporuke:", work.DeliveryDate.Value.ToString("d.M.yyyy"));
                });

                row.RelativeItem().Column(right =>
                {
                    if (!string.IsNullOrWhiteSpace(work.Location))
                        DetailRow(right, "Lokacija:", work.Location!);
                    if (!string.IsNullOrWhiteSpace(work.Delivery))
                        DetailRow(right, "Isporuka:", work.Delivery!);
                });
            });

            if (!string.IsNullOrWhiteSpace(work.FileLocation))
            {
                col.Item().PaddingBottom(4).Row(r =>
                {
                    r.ConstantItem(90).Text(t => { t.DefaultTextStyle(s => s.Bold()); t.Span("Mjesto datoteka:"); });
                    r.RelativeItem().Text(work.FileLocation);
                });
            }

            var finishingNames = GetFinishingNames(work);
            if (finishingNames.Count > 0)
            {
                col.Item().PaddingBottom(8).Column(inner =>
                {
                    inner.Item().PaddingBottom(4).Text("Završna obrada:").Bold();
                    inner.Item().Row(r =>
                    {
                        foreach (var finName in finishingNames)
                        {
                            r.AutoItem()
                             .Border(1)
                             .BorderColor(BorderGray)
                             .PaddingHorizontal(6).PaddingVertical(4)
                             .Text(finName);
                            r.ConstantItem(6);
                        }
                    });
                });
            }

            if (files.Count > 0)
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(4);
                        cd.RelativeColumn(2);
                        cd.RelativeColumn(2);
                        cd.RelativeColumn(2);
                        cd.ConstantColumn(35);
                        cd.ConstantColumn(55);
                        cd.ConstantColumn(45);
                        cd.ConstantColumn(60);
                    });

                    table.Header(h =>
                    {
                        for (int i = 0; i < TableHeaders.Length; i++)
                        {
                            var cell = h.Cell().BorderTop(1).BorderBottom(1).BorderColor(BorderGray).Padding(4);
                            bool right = TableHeadersAlignRight[i];
                            if (right)
                            {
                                cell.AlignRight().Text(t =>
                                {
                                    t.DefaultTextStyle(s => s.Bold().FontSize(a5 ? 7 : 8));
                                    t.Span(TableHeaders[i]);
                                });
                            }
                            else
                            {
                                cell.Text(t =>
                                {
                                    t.DefaultTextStyle(s => s.Bold().FontSize(a5 ? 7 : 8));
                                    t.Span(TableHeaders[i]);
                                });
                            }
                        }
                    });

                    foreach (var f in files)
                    {
                        DataCell(table, f.Filename, false, a5);
                        DataCell(table, f.Technology?.Name ?? "", false, a5);
                        DataCell(table, f.Material?.Name ?? "", false, a5);
                        DataCell(table, f.Note ?? "", false, a5);
                        DataCell(table, f.PageCount.ToString(), true, a5);
                        DataCell(table, f.M2.ToString("F2"), true, a5);
                        DataCell(table, f.Amount.ToString("F3"), true, a5);
                        DataCell(table, f.TotalM2.ToString("F2"), true, a5);
                    }

                    int     totalPages = files.Sum(f => f.PageCount);
                    decimal totalAmt   = files.Sum(f => f.Amount);
                    decimal totalM2    = files.Sum(f => f.TotalM2);

                    table.Cell().ColumnSpan(3).BorderBottom(1).BorderColor(BorderGray).Padding(4)
                         .Text(t => { t.DefaultTextStyle(s => s.Bold()); t.Span("UKUPNO"); });
                    table.Cell().BorderBottom(1).BorderColor(BorderGray).Padding(4).Text("");
                    DataCellBold(table, totalPages.ToString(), true, a5);
                    table.Cell().BorderBottom(1).BorderColor(BorderGray).Padding(4).Text("");
                    DataCellBold(table, totalAmt.ToString("F3"), true, a5);
                    DataCellBold(table, totalM2.ToString("F2"),  true, a5);
                });
            }
        });

        // ── Footer ──
        page.Footer().AlignRight().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Medium));
            t.Span("Rapido Working Orders  |  ");
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    }

    // ──────────────────────────────────────────────────────────────────

    private static List<string> GetFinishingNames(Work w)
    {
        if (w.SelectedFinishings == null || !w.SelectedFinishings.Any())
            return [];
        return w.SelectedFinishings
            .Where(wfo => wfo.FinishingOption != null)
            .OrderBy(wfo => wfo.FinishingOption!.SortOrder)
            .ThenBy(wfo => wfo.FinishingOption!.Name)
            .Select(wfo => wfo.FinishingOption!.Name)
            .ToList();
    }

    private static void DetailRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(90).Text(t => { t.DefaultTextStyle(s => s.Bold()); t.Span(label); });
            r.RelativeItem().Text(value);
        });
    }

    private static void DataCell(TableDescriptor t, string text, bool rightAlign, bool a5)
    {
        var cell = t.Cell().BorderBottom(1).BorderColor(BorderGray).Padding(3);
        if (rightAlign)
            cell.AlignRight().Text(tt => { tt.DefaultTextStyle(s => s.FontSize(a5 ? 7 : 8)); tt.Span(text); });
        else
            cell.Text(tt => { tt.DefaultTextStyle(s => s.FontSize(a5 ? 7 : 8)); tt.Span(text); });
    }

    private static void DataCellBold(TableDescriptor t, string text, bool rightAlign, bool a5)
    {
        var cell = t.Cell().BorderBottom(1).BorderColor(BorderGray).Padding(3);
        if (rightAlign)
            cell.AlignRight().Text(tt => { tt.DefaultTextStyle(s => s.Bold().FontSize(a5 ? 7 : 8)); tt.Span(text); });
        else
            cell.Text(tt => { tt.DefaultTextStyle(s => s.Bold().FontSize(a5 ? 7 : 8)); tt.Span(text); });
    }
}
