using Dbos.Transact.Workflow;
using Timeout = Dbos.Transact.Workflow.Timeout;

namespace Dbos.Transact.Tests.Workflow;

public class TimeoutTests
{
    [Fact]
    public void InheritTimeout_ReturnsInheritInstance()
    {
        var t = Timeout.InheritTimeout();
        Assert.IsType<Timeout.Inherit>(t);
    }

    [Fact]
    public void NoTimeout_ReturnsNoneInstance()
    {
        var t = Timeout.NoTimeout();
        Assert.IsType<Timeout.None>(t);
    }

    [Fact]
    public void Of_PositiveDuration_ReturnsExplicitWithValue()
    {
        var d = TimeSpan.FromSeconds(30);
        var t = Timeout.Of(d);
        var explicit_ = Assert.IsType<Timeout.Explicit>(t);
        Assert.Equal(d, explicit_.Value);
    }

    [Fact]
    public void Explicit_ZeroDuration_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Timeout.Explicit(TimeSpan.Zero));
    }

    [Fact]
    public void Explicit_NegativeDuration_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Timeout.Explicit(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void RecordEquality_SameValue_Equal()
    {
        var a = new Timeout.Explicit(TimeSpan.FromSeconds(10));
        var b = new Timeout.Explicit(TimeSpan.FromSeconds(10));
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentVariants_NotEqual()
    {
        Timeout a = new Timeout.None();
        Timeout b = new Timeout.Inherit();
        Assert.NotEqual(a, b);
    }
}
