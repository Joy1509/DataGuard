using DataGuard.Core.Engine;
using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using Moq;

namespace DataGuard.Core.Tests;

public class ParallelRuleRunnerTests
{
    [Fact]
    public async Task RunSuiteAsync_ShouldExecuteAllRulesAndCollectResults()
    {
        var runner = new ParallelRuleRunner();
        var context = new RuleContext
        {
            RunId = Guid.NewGuid(),
            TargetConnectionString = "Server=localhost;"
        };

        var ruleMocks = new List<IValidationRule>();
        for (var i = 1; i <= 5; i++)
        {
            var def = new RuleDefinition
            {
                Id = $"R{i}",
                Name = $"Rule {i}",
                Type = RuleType.RowCount,
                Severity = RuleSeverity.Medium,
                TargetTable = "dbo.Table"
            };

            var mock = new Mock<IValidationRule>();
            mock.SetupGet(x => x.Definition).Returns(def);
            mock.Setup(x => x.ExecuteAsync(context, It.IsAny<CancellationToken>()))
                .ReturnsAsync(RuleResult.Pass(def, $"FP_{i}", 10));

            ruleMocks.Add(mock.Object);
        }

        var results = await runner.RunSuiteAsync(ruleMocks, context, maxDegreeOfParallelism: 3);

        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => r.Status == RuleStatus.Passed);
    }

    [Fact]
    public async Task RunSuiteAsync_WhenOneRuleFailsOrThrows_ShouldNotAbortSuite()
    {
        var runner = new ParallelRuleRunner();
        var context = new RuleContext
        {
            RunId = Guid.NewGuid(),
            TargetConnectionString = "Server=localhost;"
        };

        var def1 = new RuleDefinition { Id = "R1", Name = "Pass Rule", Type = RuleType.RowCount, Severity = RuleSeverity.Low, TargetTable = "T1" };
        var def2 = new RuleDefinition { Id = "R2", Name = "Exploding Rule", Type = RuleType.RowCount, Severity = RuleSeverity.High, TargetTable = "T2" };
        var def3 = new RuleDefinition { Id = "R3", Name = "Another Pass Rule", Type = RuleType.RowCount, Severity = RuleSeverity.Medium, TargetTable = "T3" };

        var mock1 = new Mock<IValidationRule>();
        mock1.SetupGet(x => x.Definition).Returns(def1);
        mock1.Setup(x => x.ExecuteAsync(context, It.IsAny<CancellationToken>())).ReturnsAsync(RuleResult.Pass(def1, "FP1", 5));

        var mock2 = new Mock<IValidationRule>();
        mock2.SetupGet(x => x.Definition).Returns(def2);
        mock2.Setup(x => x.ExecuteAsync(context, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Fatal database error"));

        var mock3 = new Mock<IValidationRule>();
        mock3.SetupGet(x => x.Definition).Returns(def3);
        mock3.Setup(x => x.ExecuteAsync(context, It.IsAny<CancellationToken>())).ReturnsAsync(RuleResult.Pass(def3, "FP3", 5));

        var results = await runner.RunSuiteAsync(new[] { mock1.Object, mock2.Object, mock3.Object }, context, maxDegreeOfParallelism: 2);

        results.Should().HaveCount(3);
        results.Single(r => r.RuleId == "R1").Status.Should().Be(RuleStatus.Passed);
        results.Single(r => r.RuleId == "R2").Status.Should().Be(RuleStatus.Error);
        results.Single(r => r.RuleId == "R2").ErrorMessage.Should().Contain("Fatal database error");
        results.Single(r => r.RuleId == "R3").Status.Should().Be(RuleStatus.Passed);
    }
}
