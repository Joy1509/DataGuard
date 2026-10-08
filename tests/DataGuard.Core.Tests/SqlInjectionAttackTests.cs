using DataGuard.Infrastructure.Sql;

namespace DataGuard.Core.Tests;

public class SqlInjectionAttackTests
{
    private readonly SqlSanitizer _sanitizer = new();

    [Theory]
    [InlineData("Customers; DROP TABLE Orders;--")]
    [InlineData("Customers' OR '1'='1")]
    [InlineData("Customers/*comment*/")]
    [InlineData("dbo.Customers; TRUNCATE TABLE Users;")]
    [InlineData("dbo.[Customers]]; SELECT * FROM Secret;--")]
    [InlineData("Customers WHERE 1=1 UNION ALL SELECT @@version, 2, 3--")]
    [InlineData("`Customers`")]
    [InlineData("Customers$#@")]
    public void QuoteIdentifier_WithMaliciousPayload_ShouldThrowArgumentException(string maliciousIdentifier)
    {
        var act = () => _sanitizer.QuoteIdentifier(maliciousIdentifier);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*Invalid SQL identifier*");
    }

    [Theory]
    [InlineData("dbo.Orders; DROP DATABASE Master;--")]
    [InlineData("dbo.Orders.Extra.Invalid.Dot.Count")]
    [InlineData("'; EXEC xp_cmdshell('whoami');--")]
    public void SanitizeTableName_WithMaliciousPayload_ShouldThrowArgumentException(string maliciousTable)
    {
        var act = () => _sanitizer.SanitizeTableName(maliciousTable);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Customers", "[Customers]")]
    [InlineData("dbo.Customers", "[dbo].[Customers]")]
    [InlineData("[dbo].[Customers]", "[dbo].[Customers]")]
    [InlineData("Archive_2024", "[Archive_2024]")]
    public void SanitizeTableName_WithValidIdentifiers_ShouldProperlyQuote(string input, string expected)
    {
        var result = _sanitizer.SanitizeTableName(input);
        result.Should().Be(expected);
    }
}
