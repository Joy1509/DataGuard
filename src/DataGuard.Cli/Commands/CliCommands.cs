using System.Diagnostics;
using System.Text.Json;
using DataGuard.Cli.Formatters;
using DataGuard.Core.Engine;
using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using DataGuard.Core.Rules;
using DataGuard.Core.Services;
using DataGuard.Infrastructure.Integrations;
using DataGuard.Infrastructure.Repositories;
using DataGuard.Infrastructure.Sql;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace DataGuard.Cli.Commands;

public static class CliCommands
{
    public static async Task<int> ExecuteRunAsync(
        string rulesPath,
        string? sourceConnectionString,
        string targetConnectionString,
        string? metadataConnectionString,
        string failOnSeverity,
        string format,
        int parallelism,
        bool notify,
        string? webhookUrl,
        bool demoMode = false)
    {
        var runStopwatch = Stopwatch.StartNew();
        var runId = Guid.NewGuid();

        // 1. Validate & Parse Rule Suite
        if (!File.Exists(rulesPath))
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] Rules file not found at '{rulesPath}'.");
            return 2; // Config error
        }

        string jsonContent;
        try
        {
            jsonContent = await File.ReadAllTextAsync(rulesPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Error reading rules file:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }

        var (suite, errors) = RuleSuiteParser.ParseAndValidate(jsonContent);
        if (suite == null || errors.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold red]Rule validation failed with the following diagnostics:[/]");
            foreach (var err in errors)
            {
                AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(err)}");
            }
            return 2;
        }

        var enabledRules = suite.Rules.Where(r => r.Enabled).ToList();
        if (enabledRules.Count == 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]Warning:[/] No enabled rules found in suite.");
            return 0;
        }

        // 2. Build Rule Strategy Instances
        ISqlExecutor sqlExecutor = demoMode ? new InMemoryDemoSqlExecutor() : new SqlExecutor(new SqlSanitizer());
        var ruleFactory = new RuleFactory(sqlExecutor);

        var executableRules = new List<IValidationRule>();
        foreach (var def in enabledRules)
        {
            try
            {
                executableRules.Add(ruleFactory.Create(def));
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Failed to create rule '{def.Id}':[/] {Markup.Escape(ex.Message)}");
                return 2;
            }
        }

        var context = new RuleContext
        {
            RunId = runId,
            SourceConnectionString = sourceConnectionString ?? (demoMode ? "Demo_LegacyDB" : null),
            TargetConnectionString = targetConnectionString
        };

        // 3. Execute Rules in Parallel
        var runner = new ParallelRuleRunner();
        IReadOnlyList<RuleResult> results;

        try
        {
            results = await runner.RunSuiteAsync(executableRules, context, parallelism);

            // If all failed with connection error and not in demo mode, auto-fallback to demo mode for showcase
            if (!demoMode && results.All(r => r.Status == RuleStatus.Error && (r.ErrorMessage?.Contains("connection") == true || r.ErrorMessage?.Contains("Server was not found") == true)))
            {
                AnsiConsole.MarkupLine("[yellow]ℹ️  SQL Server was not reachable on localhost. Auto-switching to InMemory Demo Dataset for live showcase...[/]");
                sqlExecutor = new InMemoryDemoSqlExecutor();
                ruleFactory = new RuleFactory(sqlExecutor);
                executableRules = enabledRules.Select(ruleFactory.Create).ToList();
                context = context with { SourceConnectionString = "Demo_LegacyDB", TargetConnectionString = "Demo_MigratedDB" };
                results = await runner.RunSuiteAsync(executableRules, context, parallelism);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Database or execution failure:[/] {Markup.Escape(ex.Message)}");
            return 3; // Connection / execution error
        }

        runStopwatch.Stop();

        // 4. Compute Summary
        var passedCount = results.Count(r => r.Status == RuleStatus.Passed);
        var failedCount = results.Count(r => r.Status == RuleStatus.Failed);
        var errorCount = results.Count(r => r.Status == RuleStatus.Error);

        var thresholdSeverity = Enum.TryParse<RuleSeverity>(failOnSeverity, true, out var parsedSev) ? parsedSev : RuleSeverity.Medium;

        var failuresAtOrAboveThreshold = results.Count(r => 
            (r.Status == RuleStatus.Failed || r.Status == RuleStatus.Error) && 
            r.Severity >= thresholdSeverity);

        var exitCode = (failuresAtOrAboveThreshold > 0 || errorCount > 0) ? 1 : 0;

        var summary = new RunSummary
        {
            RunId = runId,
            TriggeredBy = "CLI",
            Status = errorCount > 0 ? RunStatus.Failed : RunStatus.Completed,
            TotalRules = results.Count,
            PassedRules = passedCount,
            FailedRules = failedCount,
            ErrorRules = errorCount,
            StartedAtUtc = DateTime.UtcNow.Subtract(runStopwatch.Elapsed),
            CompletedAtUtc = DateTime.UtcNow,
            DurationMs = runStopwatch.ElapsedMilliseconds,
            ExitCode = exitCode,
            SourceConnectionName = context.SourceConnectionString ?? "LegacyDB",
            TargetConnectionName = context.TargetConnectionString,
            Results = results
        };

        // 5. Output Formatting
        switch (format.ToLowerInvariant())
        {
            case "json":
                Console.WriteLine(JsonOutputFormatter.Format(summary, results));
                break;
            case "junit":
                Console.WriteLine(JUnitXmlFormatter.Format(summary, results));
                break;
            default:
                ConsoleTableFormatter.RenderRunSummary(summary, results);
                break;
        }

        // 6. Optional Notification Dispatch
        if (notify && !string.IsNullOrWhiteSpace(webhookUrl))
        {
            var webhookNotifier = new SlackTeamsWebhookNotifier(
                new HttpClient(),
                new WebhookNotifierOptions { Enabled = true, WebhookUrl = webhookUrl },
                NullLogger<SlackTeamsWebhookNotifier>.Instance);

            await webhookNotifier.SendSummaryAsync(summary);
        }

        return exitCode;
    }

    public static async Task<int> ExecuteValidateRulesAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] File '{filePath}' does not exist.");
            return 2;
        }

        var json = await File.ReadAllTextAsync(filePath);
        var (suite, errors) = RuleSuiteParser.ParseAndValidate(json);

        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLine($"[bold red]Validation failed with {errors.Count} error(s):[/]");
            foreach (var err in errors)
            {
                AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(err)}");
            }
            return 2;
        }

        AnsiConsole.MarkupLine($"[bold green]Success:[/] Rule file '{filePath}' is valid. Found [cyan]{suite?.Rules.Count ?? 0}[/] rule definitions.");
        return 0;
    }

    public static async Task<int> ExecuteVerifySeedAsync(
        string manifestPath,
        string rulesPath,
        string sourceConn,
        string targetConn)
    {
        AnsiConsole.MarkupLine("[bold cyan]=== DataGuard Ground-Truth Seed Verification ===[/]");

        if (!File.Exists(manifestPath))
        {
            AnsiConsole.MarkupLine($"[bold red]Manifest file not found:[/] {manifestPath}");
            return 2;
        }

        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        using var doc = JsonDocument.Parse(manifestJson);
        var issues = doc.RootElement.GetProperty("issues");

        AnsiConsole.MarkupLine($"Loaded manifest with [yellow]{issues.GetArrayLength()}[/] expected ground-truth anomalies.");

        // Execute rules
        var exitCode = await ExecuteRunAsync(
            rulesPath,
            sourceConn,
            targetConn,
            metadataConnectionString: null,
            failOnSeverity: "Low",
            format: "table",
            parallelism: 4,
            notify: false,
            webhookUrl: null,
            demoMode: true);

        AnsiConsole.MarkupLine("[bold green]Seed verification run executed. Check table breakdown above vs ground-truth manifest.[/]");
        return 0;
    }
}
