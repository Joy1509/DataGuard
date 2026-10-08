using System.Net.Http.Json;
using System.Text.Json;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DataGuard.Infrastructure.Integrations;

public sealed class WebhookNotifierOptions
{
    public string? WebhookUrl { get; set; }
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class SlackTeamsWebhookNotifier : INotifier
{
    private readonly HttpClient _httpClient;
    private readonly WebhookNotifierOptions _options;
    private readonly ILogger<SlackTeamsWebhookNotifier> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public SlackTeamsWebhookNotifier(
        HttpClient httpClient,
        WebhookNotifierOptions options,
        ILogger<SlackTeamsWebhookNotifier> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timespan, retryCount, _) =>
                {
                    _logger.LogWarning(exception, "Webhook dispatch failed on attempt {Attempt}. Retrying in {SleepSeconds}s...", retryCount, timespan.TotalSeconds);
                });
    }

    public async Task SendSummaryAsync(RunSummary summary, CancellationToken ct = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogInformation("Webhook notification skipped (disabled or no webhook URL configured).");
            return;
        }

        var criticalFailures = summary.Results.Where(r => r.Severity == Core.Enums.RuleSeverity.Critical && r.Status == Core.Enums.RuleStatus.Failed).ToList();
        var highFailures = summary.Results.Where(r => r.Severity == Core.Enums.RuleSeverity.High && r.Status == Core.Enums.RuleStatus.Failed).ToList();

        var payload = new
        {
            title = $"DataGuard Validation Run: {summary.Status}",
            runId = summary.RunId.ToString(),
            passRate = $"{summary.PassRatePercent}%",
            totalRules = summary.TotalRules,
            passed = summary.PassedRules,
            failed = summary.FailedRules,
            errors = summary.ErrorRules,
            durationMs = summary.DurationMs,
            criticalCount = criticalFailures.Count,
            highCount = highFailures.Count,
            source = summary.SourceConnectionName ?? "N/A",
            target = summary.TargetConnectionName ?? "Target",
            summaryText = $"Validation completed with {summary.PassedRules}/{summary.TotalRules} passing ({summary.PassRatePercent}% pass rate) in {summary.DurationMs}ms."
        };

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

                var response = await _httpClient.PostAsJsonAsync(_options.WebhookUrl, payload, cts.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }).ConfigureAwait(false);

            _logger.LogInformation("Successfully dispatched webhook notification for run {RunId}", summary.RunId);
        }
        catch (Exception ex)
        {
            // Non-negotiable requirement: Webhook failure MUST NOT abort the run
            _logger.LogError(ex, "Failed to deliver webhook notification for run {RunId} after retries.", summary.RunId);
        }
    }
}
