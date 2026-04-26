using Dbos.Transact.Json;

namespace Dbos.Transact.Tests.Json;

public sealed class BoxedTests
{
    [Fact]
    public void Of_SetsValue()
    {
        var b = Boxed.Of(42);
        Assert.Equal(42, b.Value);
    }

    [Fact]
    public void Of_Null_SetsNullValue()
    {
        var b = Boxed.Of<string>(null);
        Assert.Null(b.Value);
    }

    [Fact]
    public void Constructor_SetsValue()
    {
        var b = new Boxed<int>(99);
        Assert.Equal(99, b.Value);
    }

    [Fact]
    public void RecordEquality_SameValue_AreEqual()
    {
        Assert.Equal(Boxed.Of("hello"), Boxed.Of("hello"));
    }

    [Fact]
    public void RecordEquality_DifferentValue_AreNotEqual()
    {
        Assert.NotEqual(Boxed.Of(1), Boxed.Of(2));
    }
}
