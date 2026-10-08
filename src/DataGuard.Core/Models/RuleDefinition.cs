using System.Text.Json.Serialization;
using DataGuard.Core.Enums;

namespace DataGuard.Core.Models;

/// <summary>
/// Serializable rule specification defining validation checks and parameters.
/// </summary>
public record RuleDefinition
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required RuleType Type { get; init; }

    [JsonPropertyName("severity")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required RuleSeverity Severity { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("sourceTable")]
    public string? SourceTable { get; init; }

    [JsonPropertyName("targetTable")]
    public required string TargetTable { get; init; }

    [JsonPropertyName("targetColumn")]
    public string? TargetColumn { get; init; }

    [JsonPropertyName("columns")]
    public IReadOnlyList<string>? Columns { get; init; }

    [JsonPropertyName("keyColumns")]
    public IReadOnlyList<string>? KeyColumns { get; init; }

    [JsonPropertyName("aggregateFunction")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AggregateFunction? AggregateFunction { get; init; }

    [JsonPropertyName("tolerance")]
    public decimal? Tolerance { get; init; }

    [JsonPropertyName("tolerancePercent")]
    public decimal? TolerancePercent { get; init; }

    [JsonPropertyName("parentTable")]
    public string? ParentTable { get; init; }

    [JsonPropertyName("parentColumn")]
    public string? ParentColumn { get; init; }

    [JsonPropertyName("childTable")]
    public string? ChildTable { get; init; }

    [JsonPropertyName("childColumn")]
    public string? ChildColumn { get; init; }

    [JsonPropertyName("allowedValues")]
    public IReadOnlyList<string>? AllowedValues { get; init; }

    [JsonPropertyName("minValue")]
    public decimal? MinValue { get; init; }

    [JsonPropertyName("maxValue")]
    public decimal? MaxValue { get; init; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; init; }

    [JsonPropertyName("allowBlank")]
    public bool? AllowBlank { get; init; }

    [JsonPropertyName("customSql")]
    public string? CustomSql { get; init; }

    [JsonPropertyName("expectedCount")]
    public long? ExpectedCount { get; init; }

    [JsonPropertyName("filterCondition")]
    public string? FilterCondition { get; init; }

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// Root container for rule suites parsed from JSON files.
/// </summary>
public record RuleSuiteDefinition
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("rules")]
    public required IReadOnlyList<RuleDefinition> Rules { get; init; }
}
