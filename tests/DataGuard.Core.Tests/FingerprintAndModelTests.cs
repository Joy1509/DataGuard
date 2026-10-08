using DataGuard.Core.Enums;
using DataGuard.Core.Models;

namespace DataGuard.Core.Tests;

public class FingerprintAndModelTests
{
    [Fact]
    public void GenerateFingerprint_ShouldBeDeterministicAndCaseInsensitive()
    {
        var fp1 = FingerprintGenerator.Generate("RULE-01", "dbo.Customers", "Email");
        var fp2 = FingerprintGenerator.Generate("rule-01", "DBO.CUSTOMERS", "email");
        var fp3 = FingerprintGenerator.Generate("RULE-02", "dbo.Customers", "Email");

        fp1.Should().NotBeNullOrWhiteSpace();
        fp1.Length.Should().Be(64); // SHA-256 hex string
        fp1.Should().Be(fp2);
        fp1.Should().NotBe(fp3);
    }

    [Fact]
    public void RuleResult_Pass_ShouldSetPassedStatusAndZeroDiscrepancy()
    {
        var def = new RuleDefinition
        {
            Id = "R1",
            Name = "Rule 1",
            Type = RuleType.RowCount,
            Severity = RuleSeverity.High,
            TargetTable = "dbo.Orders"
        };

        var res = RuleResult.Pass(def, "FP1", 42, "100", "100");

        res.Status.Should().Be(RuleStatus.Passed);
        res.ExecutionTimeMs.Should().Be(42);
        res.DiscrepancyCount.Should().Be(0);
        res.ErrorMessage.Should().BeNull();
        res.SourceValue.Should().Be("100");
        res.TargetValue.Should().Be("100");
    }

    [Fact]
    public void RuleResult_Fail_ShouldSetFailedStatusAndDiscrepancies()
    {
        var def = new RuleDefinition
        {
            Id = "R2",
            Name = "Rule 2",
            Type = RuleType.NotNull,
            Severity = RuleSeverity.Critical,
            TargetTable = "dbo.Customers",
            TargetColumn = "Email"
        };

        var samples = new[] { "{\"CustomerId\": 101, \"Email\": null}" };
        var res = RuleResult.Fail(def, "FP2", 15, 25, sampleRows: samples, error: "25 null rows found");

        res.Status.Should().Be(RuleStatus.Failed);
        res.DiscrepancyCount.Should().Be(25);
        res.SampleRowsJson.Should().HaveCount(1);
        res.ErrorMessage.Should().Be("25 null rows found");
    }

    [Fact]
    public void RuleResult_Error_ShouldSetErrorStatusAndMessage()
    {
        var def = new RuleDefinition
        {
            Id = "R3",
            Name = "Rule 3",
            Type = RuleType.CustomSql,
            Severity = RuleSeverity.Medium,
            TargetTable = "dbo.Invoices"
        };

        var res = RuleResult.Error(def, "FP3", 5, "Connection timeout");

        res.Status.Should().Be(RuleStatus.Error);
        res.DiscrepancyCount.Should().Be(0);
        res.ErrorMessage.Should().Be("Connection timeout");
    }
}
