using System.Text.Json;
using DataGuard.Core.Models;

namespace DataGuard.Core.Services;

/// <summary>
/// Parses and validates rule configuration files.
/// </summary>
public static class RuleSuiteParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static (RuleSuiteDefinition? Suite, IReadOnlyList<string> Errors) ParseAndValidate(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return (null, new[] { "Rule configuration content is empty." });
        }

        RuleSuiteDefinition? suite;
        try
        {
            suite = JsonSerializer.Deserialize<RuleSuiteDefinition>(jsonContent, JsonOptions);
        }
        catch (JsonException ex)
        {
            return (null, new[] { $"Invalid JSON format: {ex.Message}" });
        }

        if (suite == null || suite.Rules == null)
        {
            return (null, new[] { "Root 'rules' array is missing." });
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(suite.Version))
        {
            errors.Add("Missing 'version' field at root level.");
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < suite.Rules.Count; i++)
        {
            var rule = suite.Rules[i];
            var prefix = $"Rule[{i}]";

            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                errors.Add($"{prefix}: Missing required 'id'.");
            }
            else if (!seenIds.Add(rule.Id))
            {
                errors.Add($"{prefix}: Duplicate rule id '{rule.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                errors.Add($"{prefix}: Missing required 'name'.");
            }

            if (string.IsNullOrWhiteSpace(rule.TargetTable) && string.IsNullOrWhiteSpace(rule.ChildTable))
            {
                errors.Add($"{prefix} ('{rule.Id}'): Missing required 'targetTable'.");
            }

            if (rule.TimeoutSeconds <= 0)
            {
                errors.Add($"{prefix} ('{rule.Id}'): 'timeoutSeconds' must be greater than 0.");
            }

            // Type specific validations
            switch (rule.Type)
            {
                case Enums.RuleType.ColumnAggregate:
                    if (string.IsNullOrWhiteSpace(rule.TargetColumn))
                        errors.Add($"{prefix} ('{rule.Id}'): ColumnAggregate requires 'targetColumn'.");
                    break;
                case Enums.RuleType.OrphanKey:
                    if (string.IsNullOrWhiteSpace(rule.ChildTable) || string.IsNullOrWhiteSpace(rule.ChildColumn) ||
                        string.IsNullOrWhiteSpace(rule.ParentTable) || string.IsNullOrWhiteSpace(rule.ParentColumn))
                    {
                        errors.Add($"{prefix} ('{rule.Id}'): OrphanKey requires 'childTable', 'childColumn', 'parentTable', and 'parentColumn'.");
                    }
                    break;
                case Enums.RuleType.ValueCheck:
                    if (string.IsNullOrWhiteSpace(rule.TargetColumn))
                        errors.Add($"{prefix} ('{rule.Id}'): ValueCheck requires 'targetColumn'.");
                    if (rule.AllowedValues == null && rule.MinValue == null && rule.MaxValue == null && string.IsNullOrWhiteSpace(rule.Pattern))
                        errors.Add($"{prefix} ('{rule.Id}'): ValueCheck requires at least one of 'allowedValues', 'minValue', 'maxValue', or 'pattern'.");
                    break;
                case Enums.RuleType.CustomSql:
                    if (string.IsNullOrWhiteSpace(rule.CustomSql))
                        errors.Add($"{prefix} ('{rule.Id}'): CustomSql requires 'customSql' statement.");
                    break;
            }
        }

        return errors.Count > 0 ? (null, errors) : (suite, Array.Empty<string>());
    }
}
