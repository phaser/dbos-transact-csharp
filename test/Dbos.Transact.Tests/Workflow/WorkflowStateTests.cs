using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class WorkflowStateTests
{
    [Theory]
    [InlineData(WorkflowState.Pending, 0)]
    [InlineData(WorkflowState.Success, 1)]
    [InlineData(WorkflowState.Error, 2)]
    [InlineData(WorkflowState.MaxRecoveryAttemptsExceeded, 3)]
    [InlineData(WorkflowState.Cancelled, 4)]
    [InlineData(WorkflowState.Enqueued, 5)]
    [InlineData(WorkflowState.Delayed, 6)]
    public void OrdinalMatchesJava(WorkflowState state, int expectedOrdinal)
    {
        Assert.Equal(expectedOrdinal, (int)state);
    }

    [Theory]
    [InlineData(WorkflowState.Pending, true)]
    [InlineData(WorkflowState.Enqueued, true)]
    [InlineData(WorkflowState.Delayed, true)]
    [InlineData(WorkflowState.Success, false)]
    [InlineData(WorkflowState.Error, false)]
    [InlineData(WorkflowState.MaxRecoveryAttemptsExceeded, false)]
    [InlineData(WorkflowState.Cancelled, false)]
    public void IsActive_ReturnsCorrectValue(WorkflowState state, bool expected)
    {
        Assert.Equal(expected, state.IsActive());
    }
}
