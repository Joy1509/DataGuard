namespace DataGuard.Core.Enums;

public enum RuleSeverity
{
    Critical = 4,
    High = 3,
    Medium = 2,
    Low = 1
}

public enum RuleType
{
    RowCount,
    ColumnAggregate,
    Checksum,
    NotNull,
    DuplicateKey,
    OrphanKey,
    ValueCheck,
    SchemaDrift,
    CustomSql
}

public enum RuleStatus
{
    Passed,
    Failed,
    Error,
    Skipped
}

public enum RunStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum DefectStatus
{
    Open,
    Acknowledged,
    Resolved,
    Reopened,
    WontFix
}

public enum ResultClassification
{
    New,
    Recurring,
    Fixed,
    SteadyPass,
    Unknown
}

public enum AggregateFunction
{
    Sum,
    Min,
    Max,
    Avg
}
