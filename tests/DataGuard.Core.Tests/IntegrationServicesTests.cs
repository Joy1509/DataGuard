using System.Net;
using DataGuard.Core.Enums;
using DataGuard.Core.Models;
using DataGuard.Infrastructure.Integrations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace DataGuard.Core.Tests;

public class IntegrationServicesTests
{
    [Fact]
    public async Task SlackTeamsWebhookNotifier_WhenEnabled_ShouldPostSummaryPayload()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(handlerMock.Object);
        var options = new WebhookNotifierOptions
        {
            Enabled = true,
            WebhookUrl = "https://hooks.slack.com/services/test/123"
        };

        var notifier = new SlackTeamsWebhookNotifier(httpClient, options, NullLogger<SlackTeamsWebhookNotifier>.Instance);

        var summary = new RunSummary
        {
            RunId = Guid.NewGuid(),
            Status = RunStatus.Completed,
            TotalRules = 10,
            PassedRules = 9,
            FailedRules = 1,
            StartedAtUtc = DateTime.UtcNow
        };

        await notifier.SendSummaryAsync(summary);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri!.ToString().Contains("slack")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SlackTeamsWebhookNotifier_WhenDisabled_ShouldNotSendHttp()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handlerMock.Object);
        var options = new WebhookNotifierOptions { Enabled = false };

        var notifier = new SlackTeamsWebhookNotifier(httpClient, options, NullLogger<SlackTeamsWebhookNotifier>.Instance);
        var summary = new RunSummary { RunId = Guid.NewGuid(), Status = RunStatus.Completed, StartedAtUtc = DateTime.UtcNow };

        await notifier.SendSummaryAsync(summary);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GitHubIssueTracker_WhenCriticalDefect_ShouldPostIssue()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created));

        var httpClient = new HttpClient(handlerMock.Object);
        var options = new GitHubTrackerOptions
        {
            Enabled = true,
            RepositoryOwner = "octocat",
            RepositoryName = "dataguard-repo",
            ApiToken = "test_token"
        };

        var tracker = new GitHubIssueTracker(httpClient, options, NullLogger<GitHubIssueTracker>.Instance);
        var summary = new RunSummary { RunId = Guid.NewGuid(), Status = RunStatus.Completed, StartedAtUtc = DateTime.UtcNow };

        var defects = new[]
        {
            new Defect
            {
                Fingerprint = "FP1234567890ABCDEF",
                RuleId = "RULE-01",
                TargetTable = "dbo.Orders",
                Severity = RuleSeverity.Critical,
                Status = DefectStatus.Open,
                FirstSeenRunId = summary.RunId,
                LastSeenRunId = summary.RunId
            }
        };

        await tracker.SyncDefectsAsync(summary, defects);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri!.ToString().Contains("github.com/repos/octocat/dataguard-repo/issues")),
            ItExpr.IsAny<CancellationToken>());
    }
}
