using Dbos.Transact.Postgres.Database;

namespace Dbos.Transact.Tests.Database;

public class PostgresSqlDialectTests
{
    private static readonly PostgresSqlDialect Sut = PostgresSqlDialect.Instance;

    [Fact]
    public void JsonColumnType_IsText()
    {
        Assert.Equal("TEXT", Sut.JsonColumnType);
    }

    [Fact]
    public void EpochMillisExpression_ContainsExtract()
    {
        Assert.Contains("EXTRACT", Sut.EpochMillisExpression, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("epoch", Sut.EpochMillisExpression, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupportsSchemas_IsTrue()
    {
        Assert.True(Sut.SupportsSchemas);
    }

    [Fact]
    public void SupportsSkipLocked_IsTrue()
    {
        Assert.True(Sut.SupportsSkipLocked);
    }

    [Fact]
    public void SupportsListenNotify_IsTrue()
    {
        Assert.True(Sut.SupportsListenNotify);
    }

    [Fact]
    public void SchemaPrefix_EmptySchema_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Sut.SchemaPrefix(string.Empty));
    }

    [Fact]
    public void SchemaPrefix_NonEmptySchema_ReturnsQuotedDotted()
    {
        var prefix = Sut.SchemaPrefix("myschema");
        Assert.Equal("\"myschema\".", prefix);
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
