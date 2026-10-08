using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using Microsoft.Extensions.Logging;

namespace DataGuard.Infrastructure.Integrations;

public sealed class GitHubTrackerOptions
{
    public string? RepositoryOwner { get; set; }
    public string? RepositoryName { get; set; }
    public string? ApiToken { get; set; }
    public bool Enabled { get; set; } = false;
}

public sealed class GitHubIssueTracker : IIssueTracker
{
    private readonly HttpClient _httpClient;
    private readonly GitHubTrackerOptions _options;
    private readonly ILogger<GitHubIssueTracker> _logger;

    public GitHubIssueTracker(
        HttpClient httpClient,
        GitHubTrackerOptions options,
        ILogger<GitHubIssueTracker> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DataGuard", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        }
    }

    public async Task SyncDefectsAsync(RunSummary summary, IReadOnlyList<Defect> defects, CancellationToken ct = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.RepositoryOwner) || string.IsNullOrWhiteSpace(_options.RepositoryName))
        {
            _logger.LogInformation("GitHub Issue sync skipped (disabled or repository not configured).");
            return;
        }

        try
        {
            foreach (var defect in defects)
            {
                if (defect.Severity != RuleSeverity.Critical && defect.Severity != RuleSeverity.High)
                {
                    continue;
                }

                var fingerprintLabel = $"dataguard-fp:{defect.Fingerprint[..12]}";

                if (defect.Status == DefectStatus.Open || defect.Status == DefectStatus.Reopened)
                {
                    // Create issue if not already open
                    var title = $"[DataGuard Defect] {defect.RuleId} on {defect.TargetTable}{(defect.TargetColumn != null ? "." + defect.TargetColumn : "")}";
                    var body = $@"### DataGuard Defect Detected
- **Rule ID**: `{defect.RuleId}`
- **Target Table**: `{defect.TargetTable}`
- **Target Column**: `{defect.TargetColumn ?? "N/A"}`
- **Severity**: **{defect.Severity}**
- **Status**: `{defect.Status}`
- **Fingerprint**: `{defect.Fingerprint}`
- **First Seen Run**: `{defect.FirstSeenRunId}`
- **Last Seen Run**: `{defect.LastSeenRunId}`
- **Occurrence Count**: {defect.OccurrenceCount}
- **Regression Count**: {defect.RegressionCount}

_Automated defect tracked by DataGuard CI/CD Pipeline._";

                    var createPayload = new
                    {
                        title,
                        body,
                        labels = new[] { "dataguard", $"severity:{defect.Severity.ToString().ToLowerInvariant()}", fingerprintLabel }
                    };

                    var url = $"https://api.github.com/repos/{_options.RepositoryOwner}/{_options.RepositoryName}/issues";
                    var response = await _httpClient.PostAsJsonAsync(url, createPayload, ct).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Created or updated GitHub issue for defect {Fingerprint}", defect.Fingerprint);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync defects with GitHub Issues for run {RunId}", summary.RunId);
        }
    }
}
