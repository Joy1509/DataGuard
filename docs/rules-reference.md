# DataGuard Rule Reference

DataGuard supports 9 built-in validation rule types. Every rule can run across Source and Target databases or single target tables.

| Rule Type | Description | Key Parameters | Sample Use Case |
| :--- | :--- | :--- | :--- |
| `RowCount` | Compares row count between Source and Target tables. | `sourceTable`, `targetTable`, `tolerancePercent`, `filterCondition` | Ensure table migration did not drop records. |
| `ColumnAggregate` | Compares mathematical aggregates (`SUM`, `MIN`, `MAX`, `AVG`) of a column. | `sourceTable`, `targetTable`, `columnName`, `function`, `tolerance` | Validate financial totals and order amounts match. |
| `Checksum` | Calculates row checksums or hash aggregates over column sets. | `sourceTable`, `targetTable`, `columns`, `keyColumn` | Detect silent in-row data corruption across large sets. |
| `NotNull` | Detects forbidden NULLs or blank/whitespace strings in columns. | `targetTable`, `columns`, `allowBlank` | Verify required fields in destination schema. |
| `DuplicateKey` | Identifies duplicate values across single or composite candidate keys. | `targetTable`, `keyColumns`, `filterCondition` | Verify uniqueness constraints prior to index creation. |
| `OrphanKey` | Identifies child foreign key rows that lack matching parent primary keys. | `childTable`, `childColumn`, `parentTable`, `parentColumn` | Verify relational integrity across migrated tables. |
| `ValueCheck` | Validates column values against min/max ranges, allowed sets, or regex/patterns. | `targetTable`, `columnName`, `allowedValues`, `minValue`, `maxValue`, `pattern` | Ensure status codes, emails, and bounds conform to business rules. |
| `SchemaDrift` | Validates column existence, data type, max length, precision, scale, and nullability. | `sourceTable`, `targetTable` (or explicit schema definitions) | Detect unexpected schema alterations between environments. |
| `CustomSql` | Executes custom user-provided read-only SQL queries expected to return 0 rows. | `sqlQuery`, `expectedCount`, `timeoutSeconds` | Complex multi-table business invariant verification. |
