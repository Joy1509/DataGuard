using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DataGuard.Core.Enums;
using DataGuard.Core.Models;
using Spectre.Console;

namespace DataGuard.Cli.Formatters;

public static class ConsoleTableFormatter
{
    public static void RenderRunSummary(RunSummary summary, IReadOnlyList<RuleResult> results)
    {
        AnsiConsole.WriteLine();
        var ruleHeader = new Rule("[bold cyan]DataGuard Validation Execution[/]")
        {
            Justification = Justify.Left
        };
        AnsiConsole.Write(ruleHeader);

        // Summary Grid
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        var passRateColor = summary.PassRatePercent >= 95 ? "green" : (summary.PassRatePercent >= 80 ? "yellow" : "red");

        grid.AddRow(
            new Panel($"[bold]Run ID:[/] {summary.RunId}\n[bold]Started:[/] {summary.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC\n[bold]Duration:[/] {summary.DurationMs ?? 0} ms")
                .Header("Run Info").BorderColor(Color.Grey),
            new Panel($"[bold]Total Rules:[/] {summary.TotalRules}\n[bold]Passed:[/] [green]{summary.PassedRules}[/]\n[bold]Failed:[/] [red]{summary.FailedRules}[/]\n[bold]Errors:[/] [darkorange]{summary.ErrorRules}[/]\n[bold]Pass Rate:[/] [{passRateColor}]{summary.PassRatePercent}%[/]")
                .Header("Metrics").BorderColor(Color.Grey)
        );

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();

        // Results Table
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]Status[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Rule ID[/]"));
        table.AddColumn(new TableColumn("[bold]Rule Name[/]"));
        table.AddColumn(new TableColumn("[bold]Type[/]"));
        table.AddColumn(new TableColumn("[bold]Severity[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Target Table[/]"));
        table.AddColumn(new TableColumn("[bold]Discrepancy[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Time (ms)[/]").RightAligned());

        foreach (var r in results)
        {
            var statusMarkup = r.Status switch
            {
                RuleStatus.Passed => "[green]PASS[/]",
                RuleStatus.Failed => "[red]FAIL[/]",
                RuleStatus.Error => "[bold darkorange]ERROR[/]",
                _ => "[grey]SKIP[/]"
            };

            var sevMarkup = r.Severity switch
            {
                RuleSeverity.Critical => "[bold red]Critical[/]",
                RuleSeverity.High => "[red]High[/]",
                RuleSeverity.Medium => "[yellow]Medium[/]",
                RuleSeverity.Low => "[blue]Low[/]",
                _ => r.Severity.ToString()
            };

            var discMarkup = r.DiscrepancyCount > 0 ? $"[red]{r.DiscrepancyCount:N0}[/]" : "[green]0[/]";

            table.AddRow(
                statusMarkup,
                r.RuleId,
                Markup.Escape(r.RuleName),
                r.RuleType.ToString(),
                sevMarkup,
                Markup.Escape(r.TargetTable),
                discMarkup,
                $"{r.ExecutionTimeMs} ms"
            );
        }

        AnsiConsole.Write(table);

        var failures = results.Where(r => r.Status != RuleStatus.Passed).ToList();
        if (failures.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold red]Failures & Discrepancies Breakdown:[/]");
            foreach (var f in failures)
            {
                AnsiConsole.MarkupLine($"  [bold red]•[/] [bold]{f.RuleId}[/] ({f.RuleName}): {Markup.Escape(f.ErrorMessage ?? $"Discrepancy count: {f.DiscrepancyCount}")}");
                if (f.SampleRowsJson.Count > 0)
                {
                    AnsiConsole.MarkupLine($"    [grey]Sample bad row:[/] {Markup.Escape(f.SampleRowsJson[0])}");
                }
            }
        }
        AnsiConsole.WriteLine();
    }
}

public static class JsonOutputFormatter
{
    public static string Format(RunSummary summary, IReadOnlyList<RuleResult> results)
    {
        var output = new
        {
            summary.RunId,
            summary.TriggeredBy,
            Status = summary.Status.ToString(),
            summary.TotalRules,
            summary.PassedRules,
            summary.FailedRules,
            summary.ErrorRules,
            summary.PassRatePercent,
            summary.StartedAtUtc,
            summary.CompletedAtUtc,
            summary.DurationMs,
            summary.ExitCode,
            results = results.Select(r => new
            {
                r.RuleId,
                r.RuleName,
                RuleType = r.RuleType.ToString(),
                Severity = r.Severity.ToString(),
                r.TargetTable,
                r.TargetColumn,
                r.Fingerprint,
                Status = r.Status.ToString(),
                r.ExecutionTimeMs,
                r.DiscrepancyCount,
                r.ErrorMessage,
                r.SourceValue,
                r.TargetValue,
                r.SampleRowsJson
            })
        };

        return JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
    }
}

public static class JUnitXmlFormatter
{
    public static string Format(RunSummary summary, IReadOnlyList<RuleResult> results)
    {
        var totalTimeSec = (summary.DurationMs ?? 0) / 1000.0;

        var testsuite = new XElement("testsuite",
            new XAttribute("name", "DataGuard.ValidationSuite"),
            new XAttribute("tests", summary.TotalRules),
            new XAttribute("failures", summary.FailedRules),
            new XAttribute("errors", summary.ErrorRules),
            new XAttribute("time", totalTimeSec.ToString("F3")),
            new XAttribute("timestamp", summary.StartedAtUtc.ToString("o"))
        );

        foreach (var r in results)
        {
            var testcase = new XElement("testcase",
                new XAttribute("classname", $"DataGuard.Rules.{r.RuleType}"),
                new XAttribute("name", $"{r.RuleId}: {r.RuleName}"),
                new XAttribute("time", (r.ExecutionTimeMs / 1000.0).ToString("F3"))
            );

            if (r.Status == RuleStatus.Failed)
            {
                testcase.Add(new XElement("failure",
                    new XAttribute("message", r.ErrorMessage ?? $"Validation failed with {r.DiscrepancyCount} discrepancies."),
                    new XAttribute("type", r.Severity.ToString()),
                    r.ErrorMessage ?? string.Empty
                ));
            }
            else if (r.Status == RuleStatus.Error)
            {
                testcase.Add(new XElement("error",
                    new XAttribute("message", r.ErrorMessage ?? "Rule execution error."),
                    new XAttribute("type", "ExecutionError"),
                    r.ErrorMessage ?? string.Empty
                ));
            }

            testsuite.Add(testcase);
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("testsuites", testsuite)
        );

        return doc.Declaration + Environment.NewLine + doc.ToString();
    }
}
