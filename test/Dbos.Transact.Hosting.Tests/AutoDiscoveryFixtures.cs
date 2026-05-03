using Dbos.Transact.Workflow;

#pragma warning disable CA1812 // proxied via Castle.DynamicProxy

// Public types so assembly.GetExportedTypes() finds them during scan tests.
namespace Dbos.Transact.Hosting.Tests.AutoDiscoveryFixtures;

public interface IAutoDiscoveryStep
{
    [Step]
    Task<string> EchoAsync(string value);
}

public sealed class AutoDiscoveryStep : IAutoDiscoveryStep
{
    public Task<string> EchoAsync(string value) => Task.FromResult(value);
}

public interface IAutoDiscoveryWorkflow
{
    Task<string> RunAsync(string value);
}

public sealed class AutoDiscoveryWorkflow : IAutoDiscoveryWorkflow
{
    private readonly IAutoDiscoveryStep _steps;
    public AutoDiscoveryWorkflow(IAutoDiscoveryStep steps) => _steps = steps;

    [Workflow]
    public Task<string> RunAsync(string value) => _steps.EchoAsync(value);
}

// Not annotated — must NOT be registered by auto-discovery.
public interface IAutoDiscoveryPlain { Task DoSomethingAsync(); }

public sealed class AutoDiscoveryPlain : IAutoDiscoveryPlain
{
    public Task DoSomethingAsync() => Task.CompletedTask;
}

#pragma warning restore CA1812
