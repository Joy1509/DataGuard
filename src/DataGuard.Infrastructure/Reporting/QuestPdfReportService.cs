using DataGuard.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DataGuard.Infrastructure.Reporting;

public interface IPdfReportService
{
    byte[] GenerateRunSummaryReport(RunSummary summary);
}

public sealed class QuestPdfReportService : IPdfReportService
{
    static QuestPdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateRunSummaryReport(RunSummary summary)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("DataGuard — Data Quality Summary").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                        col.Item().Text($"Run ID: {summary.RunId} | {summary.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC").FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                    row.ConstantItem(100).AlignRight().Text(summary.Status.ToString()).Bold().FontSize(14).FontColor(summary.Status == Core.Enums.RunStatus.Completed ? Colors.Green.Medium : Colors.Red.Medium);
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                {
                    // Executive Metrics Cards
                    col.Item().Row(metricsRow =>
                    {
                        metricsRow.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Pass Rate").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{summary.PassRatePercent}%").Bold().FontSize(16).FontColor(summary.PassRatePercent >= 90 ? Colors.Green.Medium : Colors.Red.Medium);
                        });

                        metricsRow.ConstantItem(10);

                        metricsRow.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Total Rules").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{summary.TotalRules}").Bold().FontSize(16);
                        });

                        metricsRow.ConstantItem(10);

                        metricsRow.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Failures").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{summary.FailedRules}").Bold().FontSize(16).FontColor(Colors.Red.Medium);
                        });

                        metricsRow.ConstantItem(10);

                        metricsRow.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Duration").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{summary.DurationMs ?? 0} ms").Bold().FontSize(16);
                        });
                    });

                    col.Item().PaddingTop(15).Text("Rule Execution Breakdown").Bold().FontSize(12);

                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(50);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(60);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Status").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Rule Name").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Target Table").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Severity").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Discrepancy").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Time (ms)").Bold();
                        });

                        foreach (var r in summary.Results)
                        {
                            var statusColor = r.Status == Core.Enums.RuleStatus.Passed ? Colors.Green.Medium : Colors.Red.Medium;

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(r.Status.ToString()).Bold().FontColor(statusColor);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(r.RuleName);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(r.TargetTable);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(r.Severity.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(r.DiscrepancyCount.ToString("N0"));
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(r.ExecutionTimeMs.ToString());
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
