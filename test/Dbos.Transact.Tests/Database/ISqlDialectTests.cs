using Dbos.Transact.Database;

namespace Dbos.Transact.Tests.Database;

public class ISqlDialectTests
{
    private static readonly NoOpSqlDialect Sut = NoOpSqlDialect.Instance;

    [Fact]
    public void NoOpDialect_ImplementsInterface()
    {
        Assert.IsAssignableFrom<ISqlDialect>(Sut);
    }

    [Fact]
    public void NoOpDialect_JsonColumnType_IsText()
    {
        Assert.Equal("TEXT", Sut.JsonColumnType);
    }

    [Fact]
    public void NoOpDialect_EpochMillisExpression_IsZero()
    {
        Assert.Equal("0", Sut.EpochMillisExpression);
    }

    [Fact]
    public void NoOpDialect_SupportsSchemas_IsFalse()
    {
        Assert.False(Sut.SupportsSchemas);
    }

    [Fact]
    public void NoOpDialect_SupportsSkipLocked_IsFalse()
    {
        Assert.False(Sut.SupportsSkipLocked);
    }

    [Fact]
    public void NoOpDialect_SupportsListenNotify_IsFalse()
    {
        Assert.False(Sut.SupportsListenNotify);
    }

    [Fact]
    public void NoOpDialect_SchemaPrefix_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Sut.SchemaPrefix("myschema"));
    }

    [Fact]
    public void NoOpDialect_SplitStatements_SingleStatement()
    {
        var result = Sut.SplitStatements("SELECT 1");
        Assert.Single(result);
        Assert.Equal("SELECT 1", result[0]);
    }

    [Fact]
    public void NoOpDialect_SplitStatements_MultipleStatements()
    {
        var result = Sut.SplitStatements("SELECT 1; SELECT 2;");
        Assert.Equal(2, result.Count);
        Assert.Contains("SELECT 1", result);
        Assert.Contains("SELECT 2", result);
    }

    [Fact]
    public void NoOpDialect_SplitStatements_EmptyPartsSkipped()
    {
        var result = Sut.SplitStatements("SELECT 1;;;SELECT 2");
        Assert.Equal(2, result.Count);
    }
}
