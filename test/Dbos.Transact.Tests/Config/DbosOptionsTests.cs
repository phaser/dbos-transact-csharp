using Dbos.Transact;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Config;

public class DbosOptionsTests
{
    [Fact]
    public void Defaults_MatchJavaDefaults()
    {
        var opts = DbosOptions.Defaults("my-app");
        Assert.Equal("my-app", opts.AppName);
        Assert.Null(opts.DatabaseUrl);
        Assert.Null(opts.DbUser);
        Assert.Null(opts.DbPassword);
        Assert.False(opts.AdminServer);
        Assert.Equal(3001, opts.AdminServerPort);
        Assert.True(opts.Migrate);
        Assert.Null(opts.ConductorKey);
        Assert.Null(opts.ConductorDomain);
        Assert.Null(opts.ConductorExecutorMetadata);
        Assert.Null(opts.AppVersion);
        Assert.Null(opts.ExecutorId);
        Assert.Null(opts.DatabaseSchema);
        Assert.False(opts.EnablePatching);
        Assert.Empty(opts.ListenQueues);
        Assert.Null(opts.Serializer);
        Assert.Null(opts.SchedulerPollingInterval);
    }

    [Fact]
    public void NullOrEmptyAppName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DbosOptions.Defaults(""));
        Assert.Throws<ArgumentException>(() => DbosOptions.Defaults(null!));
    }

    [Theory]
    [InlineData("ConductorKey", "")]
    [InlineData("ConductorDomain", "")]
    [InlineData("AppVersion", "")]
    [InlineData("ExecutorId", "")]
    public void EmptyOptionalFields_ThrowArgumentException(string fieldName, string emptyValue)
    {
        var base_ = DbosOptions.Defaults("app");
        Assert.Throws<ArgumentException>(() => fieldName switch
        {
            "ConductorKey" => base_ with { ConductorKey = emptyValue },
            "ConductorDomain" => base_ with { ConductorDomain = emptyValue },
            "AppVersion" => base_ with { AppVersion = emptyValue },
            "ExecutorId" => base_ with { ExecutorId = emptyValue },
            _ => throw new InvalidOperationException()
        });
    }

    [Fact]
    public void EnableAdminServer_SetsAdminServerTrue()
    {
        var opts = DbosOptions.Defaults("app").EnableAdminServer();
        Assert.True(opts.AdminServer);
    }

    [Fact]
    public void DisableAdminServer_SetsAdminServerFalse()
    {
        var opts = DbosOptions.Defaults("app").EnableAdminServer().DisableAdminServer();
        Assert.False(opts.AdminServer);
    }

    [Fact]
    public void WithListenQueue_AddsToSet()
    {
        var opts = DbosOptions.Defaults("app").WithListenQueue("q1");
        Assert.Contains("q1", opts.ListenQueues);
    }

    [Fact]
    public void WithListenQueue_QueueObject_AddsName()
    {
        var q = new Queue("q2");
        var opts = DbosOptions.Defaults("app").WithListenQueue(q);
        Assert.Contains("q2", opts.ListenQueues);
    }

    [Fact]
    public void WithListenQueues_AccumulatesQueues()
    {
        var opts = DbosOptions.Defaults("app")
            .WithListenQueue("q1")
            .WithListenQueue("q2");
        Assert.Contains("q1", opts.ListenQueues);
        Assert.Contains("q2", opts.ListenQueues);
    }

    [Fact]
    public void ListenQueues_NullDefaultIsEmptySet()
    {
        var opts = DbosOptions.Defaults("app");
        Assert.NotNull(opts.ListenQueues);
        Assert.Empty(opts.ListenQueues);
    }

    [Fact]
    public void ToString_MasksDbPassword()
    {
        var opts = DbosOptions.Defaults("app") with { DbPassword = "secret123" };
        var str = opts.ToString();
        Assert.DoesNotContain("secret123", str);
        Assert.Contains("***", str);
    }

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = DbosOptions.Defaults("my-app");
        var b = DbosOptions.Defaults("my-app");
        Assert.Equal(a, b);
    }

    [Fact]
    public void WithExpression_ProducesModifiedCopy()
    {
        var original = DbosOptions.Defaults("app");
        var modified = original with { AdminServerPort = 9000 };
        Assert.Equal(3001, original.AdminServerPort);
        Assert.Equal(9000, modified.AdminServerPort);
    }
}
