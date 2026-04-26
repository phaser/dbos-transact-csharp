using Dbos.Transact.Sqlite.Database;

namespace Dbos.Transact.Tests.Database;

public class SqliteSqlDialectTests
{
    private static readonly SqliteSqlDialect Sut = SqliteSqlDialect.Instance;

    [Fact]
    public void JsonColumnType_IsText()
    {
        Assert.Equal("TEXT", Sut.JsonColumnType);
    }

    [Fact]
    public void EpochMillisExpression_ContainsStrftime()
    {
        Assert.Contains("strftime", Sut.EpochMillisExpression, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupportsSchemas_IsFalse()
    {
        Assert.False(Sut.SupportsSchemas);
    }

    [Fact]
    public void SupportsSkipLocked_IsFalse()
    {
        Assert.False(Sut.SupportsSkipLocked);
    }

    [Fact]
    public void SupportsListenNotify_IsFalse()
    {
        Assert.False(Sut.SupportsListenNotify);
    }

    [Fact]
    public void SchemaPrefix_AnySchema_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Sut.SchemaPrefix("myschema"));
        Assert.Equal(string.Empty, Sut.SchemaPrefix(string.Empty));
    }

    [Fact]
    public void SplitStatements_SingleStatement_ReturnsSingle()
    {
        var result = Sut.SplitStatements("SELECT 1");
        Assert.Single(result);
        Assert.Equal("SELECT 1", result[0]);
    }

    [Fact]
    public void SplitStatements_MultipleStatements_SplitsOnSemicolon()
    {
        var result = Sut.SplitStatements("SELECT 1; SELECT 2;");
        Assert.Equal(2, result.Count);
        Assert.Contains("SELECT 1", result);
        Assert.Contains("SELECT 2", result);
    }

    [Fact]
    public void SplitStatements_EmptyPartsSkipped()
    {
        var result = Sut.SplitStatements("SELECT 1;;;SELECT 2");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SplitStatements_LineCommentsStripped()
    {
        var result = Sut.SplitStatements("SELECT 1 -- get one\n; SELECT 2 -- get two\n;");
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain("--", result[0]);
        Assert.DoesNotContain("--", result[1]);
    }

    [Fact]
    public void SplitStatements_CommentOnlyLine_Skipped()
    {
        var result = Sut.SplitStatements("-- comment only\nSELECT 1;");
        Assert.Single(result);
        Assert.Equal("SELECT 1", result[0]);
    }
}
