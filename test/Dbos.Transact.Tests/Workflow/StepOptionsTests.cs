using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class StepOptionsTests
{
    [Fact]
    public void DefaultConstants_MatchJavaDefaults()
    {
        Assert.Equal(1.0, StepOptions.DefaultIntervalSeconds);
        Assert.Equal(2.0, StepOptions.DefaultBackOff);
    }

    [Fact]
    public void SingleNameCtor_SetsDefaults()
    {
        var opts = new StepOptions("myStep");
        Assert.Equal("myStep", opts.Name);
        Assert.Equal(1, opts.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(StepOptions.DefaultIntervalSeconds), opts.RetryInterval);
        Assert.Equal(StepOptions.DefaultBackOff, opts.BackOffRate);
    }

    [Fact]
    public void MaxAttempts_BelowOne_NormalizedToOne()
    {
        var opts = new StepOptions("s", 0, TimeSpan.FromSeconds(1), 2.0);
        Assert.Equal(1, opts.MaxAttempts);
    }

    [Fact]
    public void MaxAttempts_NegativeValue_NormalizedToOne()
    {
        var opts = new StepOptions("s", -5, TimeSpan.FromSeconds(1), 2.0);
        Assert.Equal(1, opts.MaxAttempts);
    }

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new StepOptions("x", 3, TimeSpan.FromSeconds(2), 1.5);
        var b = new StepOptions("x", 3, TimeSpan.FromSeconds(2), 1.5);
        Assert.Equal(a, b);
    }

    [Fact]
    public void WithExpression_ProducesModifiedCopy()
    {
        var original = new StepOptions("step1");
        var modified = original with { MaxAttempts = 5 };
        Assert.Equal(1, original.MaxAttempts);
        Assert.Equal(5, modified.MaxAttempts);
    }
}
