using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class StepInfoTests
{
    [Fact]
    public void EpochMs_ComputedFromInstant()
    {
        var started = DateTimeOffset.UtcNow;
        var completed = started.AddSeconds(5);
        var info = new StepInfo(1, "myStep", null, null, null, started, completed, null);
        Assert.Equal(started.ToUnixTimeMilliseconds(), info.StartedAtEpochMs);
        Assert.Equal(completed.ToUnixTimeMilliseconds(), info.CompletedAtEpochMs);
    }

    [Fact]
    public void NullTimestamps_EpochMsIsNull()
    {
        var info = new StepInfo(1, "s", null, null, null, null, null, null);
        Assert.Null(info.StartedAtEpochMs);
        Assert.Null(info.CompletedAtEpochMs);
    }

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new StepInfo(1, "s", null, null, null, null, null, null);
        var b = new StepInfo(1, "s", null, null, null, null, null, null);
        Assert.Equal(a, b);
    }
}
