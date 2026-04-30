using Dbos.Transact.Hosting;
using Microsoft.Extensions.Configuration;

namespace Dbos.Transact.Hosting.Tests;

public class DbosOptionsConfiguratorTests
{
    [Fact]
    public void BuildOptions_FromInlineConfig_AppliesAllFields()
    {
        var c = new DbosOptionsConfigurator
        {
            Application = { Name = "my-app", Version = "v1.2.3" },
            Datasource = { Url = "url", Username = "u", Password = "p", Schema = "sch", Migrate = false },
            Conductor = { Key = "ck", Domain = "cd" },
            AdminServer = { Enabled = true, Port = 4242 },
            ExecutorId = "exec-1",
            EnablePatching = true,
            ListenQueues = { "q-a", "q-b" },
            SchedulerPollingInterval = TimeSpan.FromSeconds(45),
        };

        var opts = c.BuildOptions();

        Assert.Equal("my-app", opts.AppName);
        Assert.Equal("v1.2.3", opts.AppVersion);
        Assert.Equal("url", opts.DatabaseUrl);
        Assert.Equal("u", opts.DbUser);
        Assert.Equal("p", opts.DbPassword);
        Assert.Equal("sch", opts.DatabaseSchema);
        Assert.False(opts.Migrate);
        Assert.Equal("ck", opts.ConductorKey);
        Assert.Equal("cd", opts.ConductorDomain);
        Assert.True(opts.AdminServer);
        Assert.Equal(4242, opts.AdminServerPort);
        Assert.Equal("exec-1", opts.ExecutorId);
        Assert.True(opts.EnablePatching);
        Assert.Equal(new HashSet<string> { "q-a", "q-b" }, opts.ListenQueues);
        Assert.Equal(TimeSpan.FromSeconds(45), opts.SchedulerPollingInterval);
    }

    [Fact]
    public void BuildOptions_NoAppName_NoDefault_Throws()
    {
        var c = new DbosOptionsConfigurator();
        Assert.Throws<InvalidOperationException>(() => c.BuildOptions());
    }

    [Fact]
    public void BuildOptions_NoAppName_DefaultUsed()
    {
        var c = new DbosOptionsConfigurator();
        var opts = c.BuildOptions(defaultAppName: "fallback");
        Assert.Equal("fallback", opts.AppName);
    }

    [Fact]
    public void BuildOptions_AppNameOverridesDefault()
    {
        var c = new DbosOptionsConfigurator { Application = { Name = "explicit" } };
        var opts = c.BuildOptions(defaultAppName: "fallback");
        Assert.Equal("explicit", opts.AppName);
    }

    [Fact]
    public void BuildOptions_BindsFromIConfiguration()
    {
        var dict = new Dictionary<string, string?>
        {
            ["Dbos:Application:Name"] = "from-config",
            ["Dbos:Application:Version"] = "9.9",
            ["Dbos:Datasource:Url"] = "Data Source=memory",
            ["Dbos:Datasource:Schema"] = "myschema",
            ["Dbos:Datasource:Migrate"] = "false",
            ["Dbos:AdminServer:Enabled"] = "true",
            ["Dbos:AdminServer:Port"] = "9999",
            ["Dbos:ExecutorId"] = "config-exec",
            ["Dbos:EnablePatching"] = "true",
            ["Dbos:ListenQueues:0"] = "q1",
            ["Dbos:ListenQueues:1"] = "q2",
            ["Dbos:SchedulerPollingInterval"] = "00:01:30",
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var c = new DbosOptionsConfigurator();
        config.GetSection("Dbos").Bind(c);

        var opts = c.BuildOptions();
        Assert.Equal("from-config", opts.AppName);
        Assert.Equal("9.9", opts.AppVersion);
        Assert.Equal("Data Source=memory", opts.DatabaseUrl);
        Assert.Equal("myschema", opts.DatabaseSchema);
        Assert.False(opts.Migrate);
        Assert.True(opts.AdminServer);
        Assert.Equal(9999, opts.AdminServerPort);
        Assert.Equal("config-exec", opts.ExecutorId);
        Assert.True(opts.EnablePatching);
        Assert.Equal(new HashSet<string> { "q1", "q2" }, opts.ListenQueues);
        Assert.Equal(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(30)), opts.SchedulerPollingInterval);
    }

    [Fact]
    public void BuildOptions_EmptyStringFieldsBecomeNull()
    {
        // IConfiguration may yield empty strings for missing keys; ensure they don't trip
        // DbosOptions's "must not be empty" init validators.
        var c = new DbosOptionsConfigurator
        {
            Application = { Name = "x", Version = "" },
            ExecutorId = "",
            Conductor = { Key = "", Domain = "" },
        };

        var opts = c.BuildOptions();
        Assert.Null(opts.AppVersion);
        Assert.Null(opts.ExecutorId);
        Assert.Null(opts.ConductorKey);
        Assert.Null(opts.ConductorDomain);
    }
}
