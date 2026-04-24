using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class ErrorResultTests
{
    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new ErrorResult("MyEx", "boom", "{}", "portable", null);
        var b = new ErrorResult("MyEx", "boom", "{}", "portable", null);
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentMessage_NotEqual()
    {
        var a = new ErrorResult("MyEx", "boom1", null, null, null);
        var b = new ErrorResult("MyEx", "boom2", null, null, null);
        Assert.NotEqual(a, b);
    }
}
