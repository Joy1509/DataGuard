using System.CommandLine;
using System.CommandLine.Invocation;
using DataGuard.Cli.Commands;

namespace DataGuard.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DataGuard — Automated Data Validation & Migration Integrity Engine for SQL Server");

        // 1. `run` command
        var runCommand = new Command("run", "Execute validation rules against source and target databases");

        var rulesOption = new Option<string>("--rules", () => "rules.sample.json", "Path to the rule suite JSON file or 'db' to read from metadata store");
        var sourceOption = new Option<string?>("--source", "Connection string or alias for Source database (e.g. LegacyDB)");
        var targetOption = new Option<string>("--target", () => "Server=localhost;Database=MigratedDB;TrustServerCertificate=True;", "Connection string or alias for Target database");
        var metadataOption = new Option<string?>("--metadata", "Connection string for DataGuard metadata DB");
        var failOnOption = new Option<string>("--fail-on", () => "high", "Fail pipeline if defects exist at or above severity (critical, high, medium, low)");
        var formatOption = new Option<string>("--format", () => "table", "Output format: table, json, or junit");
        var parallelismOption = new Option<int>("--parallelism", () => 4, "Maximum concurrent rules executed in parallel");
        var notifyOption = new Option<bool>("--notify", () => false, "Dispatch notifications to configured webhooks");
        var webhookUrlOption = new Option<string?>("--webhook-url", "Webhook endpoint URL for Slack / Microsoft Teams");
        var demoOption = new Option<bool>("--demo", () => false, "Run in offline demo mode using synthetic ground-truth dataset");

        runCommand.AddOption(rulesOption);
        runCommand.AddOption(sourceOption);
        runCommand.AddOption(targetOption);
        runCommand.AddOption(metadataOption);
        runCommand.AddOption(failOnOption);
        runCommand.AddOption(formatOption);
        runCommand.AddOption(parallelismOption);
        runCommand.AddOption(notifyOption);
        runCommand.AddOption(webhookUrlOption);
        runCommand.AddOption(demoOption);

        runCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var rules = ctx.ParseResult.GetValueForOption(rulesOption) ?? "rules.sample.json";
            var src = ctx.ParseResult.GetValueForOption(sourceOption);
            var tgt = ctx.ParseResult.GetValueForOption(targetOption) ?? "Server=localhost;Database=MigratedDB;TrustServerCertificate=True;";
            var meta = ctx.ParseResult.GetValueForOption(metadataOption);
            var failOn = ctx.ParseResult.GetValueForOption(failOnOption) ?? "high";
            var fmt = ctx.ParseResult.GetValueForOption(formatOption) ?? "table";
            var par = ctx.ParseResult.GetValueForOption(parallelismOption);
            if (par <= 0) par = 4;
            var notif = ctx.ParseResult.GetValueForOption(notifyOption);
            var wh = ctx.ParseResult.GetValueForOption(webhookUrlOption);
            var demo = ctx.ParseResult.GetValueForOption(demoOption);

            var code = await CliCommands.ExecuteRunAsync(rules, src, tgt, meta, failOn, fmt, par, notif, wh, demo);
            ctx.ExitCode = code;
        });

        // 2. `validate-rules` command
        var validateRulesCommand = new Command("validate-rules", "Validate a rule suite JSON definition against the schema and syntax rules");
        var fileOption = new Option<string>("--file", () => "rules.sample.json", "Path to rule JSON file to validate");
        validateRulesCommand.AddOption(fileOption);

        validateRulesCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var file = ctx.ParseResult.GetValueForOption(fileOption) ?? "rules.sample.json";
            var code = await CliCommands.ExecuteValidateRulesAsync(file);
            ctx.ExitCode = code;
        });

        // 3. `verify-seed` command
        var verifySeedCommand = new Command("verify-seed", "Execute verification suite against synthetic seed DB and compare with ground-truth manifest");
        var manifestOption = new Option<string>("--manifest", () => "db/seed/injected-issues.json", "Path to ground-truth manifest JSON");
        var seedRulesOption = new Option<string>("--rules", () => "rules.sample.json", "Path to rules file");
        var seedSourceOption = new Option<string>("--source", () => "Server=localhost;Database=LegacyDB;TrustServerCertificate=True;", "Source connection string");
        var seedTargetOption = new Option<string>("--target", () => "Server=localhost;Database=MigratedDB;TrustServerCertificate=True;", "Target connection string");

        verifySeedCommand.AddOption(manifestOption);
        verifySeedCommand.AddOption(seedRulesOption);
        verifySeedCommand.AddOption(seedSourceOption);
        verifySeedCommand.AddOption(seedTargetOption);

        verifySeedCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var manifest = ctx.ParseResult.GetValueForOption(manifestOption) ?? "db/seed/injected-issues.json";
            var rules = ctx.ParseResult.GetValueForOption(seedRulesOption) ?? "rules.sample.json";
            var src = ctx.ParseResult.GetValueForOption(seedSourceOption) ?? "Server=localhost;Database=LegacyDB;TrustServerCertificate=True;";
            var tgt = ctx.ParseResult.GetValueForOption(seedTargetOption) ?? "Server=localhost;Database=MigratedDB;TrustServerCertificate=True;";

            var code = await CliCommands.ExecuteVerifySeedAsync(manifest, rules, src, tgt);
            ctx.ExitCode = code;
        });

        rootCommand.AddCommand(runCommand);
        rootCommand.AddCommand(validateRulesCommand);
        rootCommand.AddCommand(verifySeedCommand);

        return await rootCommand.InvokeAsync(args);
    }
}
