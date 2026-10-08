using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using DataGuard.Core.Rules;
using Moq;

namespace DataGuard.Core.Tests;

public class RuleFactoryAndRulesTests
{
    private readonly Mock<ISqlExecutor> _sqlExecutorMock = new();

    private RuleContext CreateTestContext() =>
        new()
        {
            RunId = Guid.NewGuid(),
            SourceConnectionString = "Server=localhost;Database=LegacyDB;",
            TargetConnectionString = "Server=localhost;Database=MigratedDB;"
        };

    [Fact]
    public void RuleFactory_ShouldCreateAllNineRuleTypes()
    {
        var factory = new RuleFactory(_sqlExecutorMock.Object);

        var ruleTypes = Enum.GetValues<RuleType>();
        foreach (var ruleType in ruleTypes)
        {
            var def = new RuleDefinition
            {
                Id = $"RULE-{ruleType}",
                Name = $"Rule {ruleType}",
                Type = ruleType,
                Severity = RuleSeverity.High,
                TargetTable = "dbo.TestTable",
                TargetColumn = "TestColumn",
                ChildTable = "dbo.Child",
                ChildColumn = "Id",
                ParentTable = "dbo.Parent",
                ParentColumn = "Id",
                CustomSql = "SELECT 1"
            };

            var rule = factory.Create(def);
            rule.Should().NotBeNull();
            rule.Definition.Type.Should().Be(ruleType);
        }
    }

    [Fact]
    public async Task RowCountRule_WhenCountsMatch_ShouldPass()
    {
        _sqlExecutorMock
            .Setup(x => x.GetRowCountAsync(It.IsAny<string>(), "dbo.Orders", null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000L);

        var def = new RuleDefinition
        {
            Id = "R1",
            Name = "RowCount Test",
            Type = RuleType.RowCount,
            Severity = RuleSeverity.Critical,
            TargetTable = "dbo.Orders"
        };

        var rule = new RowCountRule(def, _sqlExecutorMock.Object);
        var result = await rule.ExecuteAsync(CreateTestContext(), CancellationToken.None);

        result.Status.Should().Be(RuleStatus.Passed);
        result.DiscrepancyCount.Should().Be(0);
    }

    [Fact]
    public async Task RowCountRule_WhenCountsDiffer_ShouldFailWithDelta()
    {
        _sqlExecutorMock
            .Setup(x => x.GetRowCountAsync(It.Is<string>(s => s.Contains("LegacyDB")), "dbo.Orders", null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000L);

        _sqlExecutorMock
            .Setup(x => x.GetRowCountAsync(It.Is<string>(s => s.Contains("MigratedDB")), "dbo.Orders", null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(985L);

        var def = new RuleDefinition
        {
            Id = "R1",
            Name = "RowCount Test",
            Type = RuleType.RowCount,
            Severity = RuleSeverity.Critical,
            TargetTable = "dbo.Orders",
            TolerancePercent = 0
        };

        var rule = new RowCountRule(def, _sqlExecutorMock.Object);
        var result = await rule.ExecuteAsync(CreateTestContext(), CancellationToken.None);

        result.Status.Should().Be(RuleStatus.Failed);
        result.DiscrepancyCount.Should().Be(15);
        result.ErrorMessage.Should().Contain("Row count mismatch");
    }

    [Fact]
    public async Task ColumnAggregateRule_WhenSumsDiffer_ShouldFail()
    {
        _sqlExecutorMock
            .Setup(x => x.GetAggregateAsync(It.Is<string>(s => s.Contains("LegacyDB")), "dbo.Orders", "TotalAmount", AggregateFunction.Sum, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50000.00m);

        _sqlExecutorMock
            .Setup(x => x.GetAggregateAsync(It.Is<string>(s => s.Contains("MigratedDB")), "dbo.Orders", "TotalAmount", AggregateFunction.Sum, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(51100.00m);

        var def = new RuleDefinition
        {
            Id = "R2",
            Name = "TotalAmount Sum",
            Type = RuleType.ColumnAggregate,
            Severity = RuleSeverity.High,
            TargetTable = "dbo.Orders",
            TargetColumn = "TotalAmount",
            AggregateFunction = AggregateFunction.Sum,
            Tolerance = 0.0m
        };

        var rule = new ColumnAggregateRule(def, _sqlExecutorMock.Object);
        var result = await rule.ExecuteAsync(CreateTestContext(), CancellationToken.None);

        result.Status.Should().Be(RuleStatus.Failed);
        result.DiscrepancyCount.Should().Be(1100);
    }

    [Fact]
    public async Task ChecksumRule_WhenChecksumMismatches_ShouldFail()
    {
        _sqlExecutorMock
            .Setup(x => x.GetChecksumAggAsync(It.Is<string>(s => s.Contains("LegacyDB")), "dbo.OrderItems", It.IsAny<IReadOnlyList<string>>(), null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(12345678L);

        _sqlExecutorMock
            .Setup(x => x.GetChecksumAggAsync(It.Is<string>(s => s.Contains("MigratedDB")), "dbo.OrderItems", It.IsAny<IReadOnlyList<string>>(), null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(87654321L);

        var def = new RuleDefinition
        {
            Id = "R3",
            Name = "Checksum Check",
            Type = RuleType.Checksum,
            Severity = RuleSeverity.High,
            TargetTable = "dbo.OrderItems",
            Columns = new[] { "UnitPrice", "Quantity" }
        };

        var rule = new ChecksumRule(def, _sqlExecutorMock.Object);
        var result = await rule.ExecuteAsync(CreateTestContext(), CancellationToken.None);

        result.Status.Should().Be(RuleStatus.Failed);
        result.DiscrepancyCount.Should().Be(1);
    }

    [Fact]
    public async Task NotNullRule_WhenNullsFound_ShouldFailWithBadRows()
    {
        var sampleJson = new[] { "{\"CustomerId\": 101, \"Email\": null}" };
        _sqlExecutorMock
            .Setup(x => x.GetNullOrBlankRowsAsync(It.IsAny<string>(), "dbo.Customers", It.IsAny<IReadOnlyList<string>>(), false, 20, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((25L, sampleJson));

        var def = new RuleDefinition
        {
            Id = "R4",
            Name = "NotNull Email",
            Type = RuleType.NotNull,
            Severity = RuleSeverity.Critical,
            TargetTable = "dbo.Customers",
            TargetColumn = "Email"
        };

        var rule = new NotNullRule(def, _sqlExecutorMock.Object);
        var result = await rule.ExecuteAsync(CreateTestContext(), CancellationToken.None);

        result.Status.Should().Be(RuleStatus.Failed);
        result.DiscrepancyCount.Should().Be(25);
        result.SampleRowsJson.Should().HaveCount(1);
    }

    [Fact]
    public async Task DuplicateKeyRule_WhenDuplicatesFound_ShouldFail()
    {
        _sqlExecutorMock
            .Setup(x => x.GetDuplicateKeyRowsAsync(It.IsAny<string>(), "dbo.Customers", It.IsAny<IReadOnlyList<string>>(), null, 20, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((10L, new[] { "{\"CustomerCode\": \"CUST-001\", \"DuplicateCount\": 10}" }));

        var def = new RuleDefinition
        {
            Id = "R5",
            Name = "Duplicate CustomerCode",
            Type = RuleType.DuplicateKey,
            Severity = RuleSeverity.Critical,
            TargetTable = "dbo.Customers",
            KeyColumns = new[] { "CustomerCode" }
        };

        var rule = new DuplicateKeyRule(def, _sqlExecutorMock.Object);
        var result = await rule.ExecuteAsync(CreateTestContext(), CancellationToken.None);

        result.Status.Should().Be(RuleStatus.Failed);
        result.DiscrepancyCount.Should().Be(10);
    }

    [Fact]
    public async Task OrphanKeyRule_WhenOrphanRowsFound_ShouldFail()
    {
        _sqlExecutorMock
            .Setup(x => x.GetOrphanKeyRowsAsync(It.IsAny<string>(), "dbo.Orders", "CustomerId", "dbo.Customers", "CustomerId", 20, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((20L, new[] { "{\"OrderId\": 301, \"CustomerId\": 999999}" }));

        var def = new RuleDefinition
        {
            Id = "R6",
            Name = "Orphan Orders Check",
            Type = RuleType.OrphanKey,
            Severity = RuleSeverity.Critical,
            TargetTable = "dbo.Orders",
            ChildTable = "dbo.Orders",
            ChildColumn = "CustomerId",
            ParentTable = "dbo.Customers",
            ParentColumn = "CustomerId"
        };

        var rule = new OrphanKeyRule(def, _sqlExecutorMock.Object);
        var result = await rule.ExecuteAsync(CreateTestContext(), CancellationToken.None);

        result.Status.Should().Be(RuleStatus.Failed);
        result.DiscrepancyCount.Should().Be(20);
    }

    [Fact]
    public async Task ValueCheckRule_WhenInvalidValuesFound_ShouldFail()
    {
        _sqlExecutorMock
            .Setup(x => x.GetValueCheckRowsAsync(It.IsAny<string>(), "dbo.Orders", "Status", It.IsAny<IReadOnlyList<string>>(), null, null, null, 20, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((12L, new[] { "{\"OrderId\": 701, \"Status\": \"INVALID_STATUS\"}" }));

        var def = new RuleDefinition
        {
            Id = "R7",
            Name = "Status Domain Check",
            Type = RuleType.ValueCheck,
            Severity = RuleSeverity.Medium,
            TargetTable = "dbo.Orders",
            TargetColumn = "Status",
            AllowedValues = new[] { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" }
        };

        var rule = new ValueCheckRule(def, _sqlExecutorMock.Object);
        var result = await rule.ExecuteAsync(CreateTestContext(), CancellationToken.None);

        result.Status.Should().Be(RuleStatus.Failed);
        result.DiscrepancyCount.Should().Be(12);
    }

    [Fact]
    public async Task SchemaDriftRule_WhenDriftDetected_ShouldFailWithDiscrepancyList()
    {
        _sqlExecutorMock
            .Setup(x => x.GetSchemaDriftAsync(It.IsAny<string>(), "dbo.Customers", It.IsAny<string>(), "dbo.Customers", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, new[] { "Column 'Phone' max length mismatch: Source=25, Target=20." }));

        var def = new RuleDefinition
        {
            Id = "R8",
            Name = "Schema Drift Customers",
            Type = RuleType.SchemaDrift,
            Severity = RuleSeverity.Medium,
            TargetTable = "dbo.Customers"
        };

        var rule = new SchemaDriftRule(def, _sqlExecutorMock.Object);
        var result = await rule.ExecuteAsync(CreateTestContext(), CancellationToken.None);

        result.Status.Should().Be(RuleStatus.Failed);
        result.DiscrepancyCount.Should().Be(1);
        result.ErrorMessage.Should().Contain("Schema drift detected");
    }

    [Fact]
    public async Task CustomSqlRule_DisallowNonSelectStatements()
    {
        var def = new RuleDefinition
        {
            Id = "R9",
            Name = "Dangerous Mutation Query",
            Type = RuleType.CustomSql,
            Severity = RuleSeverity.Critical,
            TargetTable = "dbo.Invoices",
            CustomSql = "DELETE FROM dbo.Invoices WHERE AmountDue = 0"
        };

        var rule = new CustomSqlRule(def, _sqlExecutorMock.Object);
        var result = await rule.ExecuteAsync(CreateTestContext(), CancellationToken.None);

        result.Status.Should().Be(RuleStatus.Error);
        result.ErrorMessage.Should().Contain("CustomSql queries must be SELECT-only statements.");
    }
}
