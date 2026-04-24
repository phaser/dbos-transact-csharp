using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class WorkflowScheduleTests
{
    [Fact]
    public void ConvenienceCtor_SetsDefaults()
    {
        var s = new WorkflowSchedule("my-schedule", "MyWorkflow", "MyClass", "0 * * * *");
        Assert.Null(s.Id);
        Assert.Equal("my-schedule", s.ScheduleName);
        Assert.Equal("MyWorkflow", s.WorkflowName);
        Assert.Equal("MyClass", s.ClassName);
        Assert.Equal("0 * * * *", s.Cron);
        Assert.Equal(ScheduleStatus.Active, s.Status);
        Assert.True(s.IsActive);
        Assert.Null(s.Context);
        Assert.Null(s.LastFiredAt);
        Assert.False(s.AutomaticBackfill);
        Assert.Null(s.CronTimezone);
        Assert.Null(s.QueueName);
    }

    [Fact]
    public void EmptyScheduleName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new WorkflowSchedule("", "wf", "cls", "* * * * *"));
    }

    [Fact]
    public void EmptyWorkflowName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new WorkflowSchedule("sched", "", "cls", "* * * * *"));
    }

    [Fact]
    public void EmptyCron_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new WorkflowSchedule("sched", "wf", "cls", ""));
    }

    [Fact]
    public void PausedStatus_IsActiveReturnsFalse()
    {
        var s = new WorkflowSchedule(null, "sched", "wf", null, "* * * * *",
            ScheduleStatus.Paused, null, null, false, null, null);
        Assert.False(s.IsActive);
    }

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new WorkflowSchedule("s", "wf", "cls", "0 * * * *");
        var b = new WorkflowSchedule("s", "wf", "cls", "0 * * * *");
        Assert.Equal(a, b);
    }
}
