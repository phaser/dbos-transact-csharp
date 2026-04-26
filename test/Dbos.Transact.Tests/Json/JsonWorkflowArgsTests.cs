using Dbos.Transact.Json;

namespace Dbos.Transact.Tests.Json;

public sealed class JsonWorkflowArgsTests
{
    [Fact]
    public void Default_BothPropertiesNull()
    {
        var args = new JsonWorkflowArgs();
        Assert.Null(args.PositionalArgs);
        Assert.Null(args.NamedArgs);
    }

    [Fact]
    public void FromPositional_SetsPositionalArgs()
    {
        var args = JsonWorkflowArgs.FromPositional("a", 1, null);
        Assert.Equal(["a", 1, null], args.PositionalArgs);
        Assert.Null(args.NamedArgs);
    }

    [Fact]
    public void PositionalArgs_RoundTrips()
    {
        var args = new JsonWorkflowArgs(PositionalArgs: [1, "two", 3.0]);
        Assert.Equal(3, args.PositionalArgs!.Length);
        Assert.Equal(1, args.PositionalArgs[0]);
    }

    [Fact]
    public void NamedArgs_RoundTrips()
    {
        var named = new Dictionary<string, object?> { ["x"] = 42 };
        var args = new JsonWorkflowArgs(NamedArgs: named);
        Assert.Equal(42, args.NamedArgs!["x"]);
    }
}
