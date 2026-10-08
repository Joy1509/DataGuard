using DataGuard.Core.Enums;
using DataGuard.Core.Services;

namespace DataGuard.Core.Tests;

public class RuleSuiteParserTests
{
    [Fact]
    public void ParseAndValidate_WithValidJson_ShouldReturnSuite()
    {
        var json = @"
        {
            ""version"": ""1.0"",
            ""description"": ""Test suite"",
            ""rules"": [
                {
                    ""id"": ""R-01"",
                    ""name"": ""Orders Count"",
                    ""type"": ""RowCount"",
                    ""severity"": ""Critical"",
                    ""targetTable"": ""dbo.Orders""
                }
            ]
        }";

        var (suite, errors) = RuleSuiteParser.ParseAndValidate(json);

        errors.Should().BeEmpty();
        suite.Should().NotBeNull();
        suite!.Rules.Should().HaveCount(1);
        suite.Rules[0].Id.Should().Be("R-01");
        suite.Rules[0].Type.Should().Be(RuleType.RowCount);
        suite.Rules[0].Severity.Should().Be(RuleSeverity.Critical);
    }

    [Fact]
    public void ParseAndValidate_WithInvalidJson_ShouldReturnError()
    {
        var (suite, errors) = RuleSuiteParser.ParseAndValidate("invalid json {{{");

        suite.Should().BeNull();
        errors.Should().NotBeEmpty();
        errors[0].Should().Contain("Invalid JSON format");
    }

    [Fact]
    public void ParseAndValidate_WithDuplicateRuleId_ShouldReturnError()
    {
        var json = @"
        {
            ""version"": ""1.0"",
            ""rules"": [
                { ""id"": ""DUP"", ""name"": ""R1"", ""type"": ""RowCount"", ""severity"": ""Low"", ""targetTable"": ""dbo.A"" },
                { ""id"": ""DUP"", ""name"": ""R2"", ""type"": ""RowCount"", ""severity"": ""Low"", ""targetTable"": ""dbo.B"" }
            ]
        }";

        var (suite, errors) = RuleSuiteParser.ParseAndValidate(json);

        suite.Should().BeNull();
        errors.Should().Contain(e => e.Contains("Duplicate rule id 'DUP'"));
    }

    [Fact]
    public void ParseAndValidate_WithMissingRequiredFields_ShouldReturnDiagnostics()
    {
        var json = @"
        {
            ""version"": ""1.0"",
            ""rules"": [
                { ""id"": """", ""name"": """", ""type"": ""ColumnAggregate"", ""severity"": ""High"", ""targetTable"": """" }
            ]
        }";

        var (suite, errors) = RuleSuiteParser.ParseAndValidate(json);

        suite.Should().BeNull();
        errors.Should().Contain(e => e.Contains("Missing required 'id'"));
        errors.Should().Contain(e => e.Contains("Missing required 'name'"));
        errors.Should().Contain(e => e.Contains("Missing required 'targetTable'"));
        errors.Should().Contain(e => e.Contains("ColumnAggregate requires 'targetColumn'"));
    }
}
