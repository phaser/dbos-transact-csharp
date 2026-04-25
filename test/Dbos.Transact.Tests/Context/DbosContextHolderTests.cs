using Dbos.Transact.Context;

namespace Dbos.Transact.Tests.Context;

public class DbosContextHolderTests
{
    // Each test clears the holder to avoid cross-test bleed.
    public DbosContextHolderTests() => DbosContextHolder.Clear();

    [Fact]
    public void Get_ReturnsNull_WhenNotSet()
    {
        Assert.Null(DbosContextHolder.Get());
    }

    [Fact]
    public void Set_And_Get_RoundTrip()
    {
        var ctx = new DbosContext("wf-1", null, null, null);
        DbosContextHolder.Set(ctx);
        Assert.Same(ctx, DbosContextHolder.Get());
    }

    [Fact]
    public void Clear_SetsToNull()
    {
        DbosContextHolder.Set(new DbosContext("wf-1", null, null, null));
        DbosContextHolder.Clear();
        Assert.Null(DbosContextHolder.Get());
    }

    [Fact]
    public async Task AsyncLocal_IsolatesChildTask()
    {
        // Set a context in the parent.
        DbosContextHolder.Set(new DbosContext("parent-wf", null, null, null));

        string? childSaw = null;

        // Child task gets a copy of the AsyncLocal value but changes don't affect parent.
        await Task.Run(() =>
        {
            childSaw = DbosContextHolder.Get()?.WorkflowId;

            // Replace context inside child — should not affect parent.
            DbosContextHolder.Set(new DbosContext("child-wf", null, null, null));
        });

        // Parent sees its original context.
        Assert.Equal("parent-wf", DbosContextHolder.Get()?.WorkflowId);
        // Child inherited the parent's value.
        Assert.Equal("parent-wf", childSaw);
    }

    [Fact]
    public async Task AsyncLocal_ParallelTasks_DoNotBleed()
    {
        var results = new string?[2];

        var t1 = Task.Run(async () =>
        {
            DbosContextHolder.Set(new DbosContext("wf-A", null, null, null));
            await Task.Delay(10);
            results[0] = DbosContextHolder.Get()?.WorkflowId;
        });

        var t2 = Task.Run(async () =>
        {
            DbosContextHolder.Set(new DbosContext("wf-B", null, null, null));
            await Task.Delay(10);
            results[1] = DbosContextHolder.Get()?.WorkflowId;
        });

        await Task.WhenAll(t1, t2);

        Assert.Equal("wf-A", results[0]);
        Assert.Equal("wf-B", results[1]);
    }
}
