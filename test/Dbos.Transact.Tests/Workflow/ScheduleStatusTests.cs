using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class ScheduleStatusTests
{
    [Theory]
    [InlineData(ScheduleStatus.Active, 0)]
    [InlineData(ScheduleStatus.Paused, 1)]
    public void OrdinalMatchesJava(ScheduleStatus status, int expectedOrdinal)
    {
        Assert.Equal(expectedOrdinal, (int)status);
    }
}
