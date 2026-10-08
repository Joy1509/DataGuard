using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;

namespace DataGuard.Core.Rules;

/// <summary>
/// Default implementation of IRuleFactory building rules based on type discriminator.
/// </summary>
public sealed class RuleFactory : IRuleFactory
{
    private readonly ISqlExecutor _sqlExecutor;

    public RuleFactory(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
    }

    public IValidationRule Create(RuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Type switch
        {
            RuleType.RowCount => new RowCountRule(definition, _sqlExecutor),
            RuleType.ColumnAggregate => new ColumnAggregateRule(definition, _sqlExecutor),
            RuleType.Checksum => new ChecksumRule(definition, _sqlExecutor),
            RuleType.NotNull => new NotNullRule(definition, _sqlExecutor),
            RuleType.DuplicateKey => new DuplicateKeyRule(definition, _sqlExecutor),
            RuleType.OrphanKey => new OrphanKeyRule(definition, _sqlExecutor),
            RuleType.ValueCheck => new ValueCheckRule(definition, _sqlExecutor),
            RuleType.SchemaDrift => new SchemaDriftRule(definition, _sqlExecutor),
            RuleType.CustomSql => new CustomSqlRule(definition, _sqlExecutor),
            _ => throw new NotSupportedException($"Unsupported rule type '{definition.Type}' for rule ID '{definition.Id}'.")
        };
    }
}
