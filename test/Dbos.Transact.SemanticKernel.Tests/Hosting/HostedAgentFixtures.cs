using System.ComponentModel;
using Dbos.Transact.SemanticKernel;
using Dbos.Transact.Workflow;
using Microsoft.SemanticKernel;

#pragma warning disable CA1812 // proxied via Castle.DynamicProxy / instantiated by DI

// Public types so assembly.GetExportedTypes() finds them during AddDbosWorkflowsFromAssembly.
namespace Dbos.Transact.SemanticKernel.Tests.Hosting.Fixtures;

public interface IWeatherTools
{
    [KernelFunction, Description("Get the weather for a city.")]
    [Step]
    Task<string> GetWeatherAsync(string city);
}

public sealed class WeatherTools : IWeatherTools
{
    public int InvocationCount { get; private set; }
    public Task<string> GetWeatherAsync(string city)
    {
        InvocationCount++;
        return Task.FromResult($"Sunny in {city}");
    }
}

public interface IHostedAgentWorkflow
{
    Task<string> RunAsync(string city);
}

public sealed class HostedAgentWorkflow : IHostedAgentWorkflow
{
    private readonly Kernel _kernel;
    private readonly IDurableChatCompletionService _chat;

    public HostedAgentWorkflow(Kernel kernel, IDurableChatCompletionService chat)
    {
        _kernel = kernel;
        _chat = chat;
    }

    [Workflow]
    public async Task<string> RunAsync(string city)
    {
        // Both injected dependencies are hot-wired by the pre-launch configurators.
        // Each call below is a top-level [Step] checkpointed by DBOS.
        var llm = await _chat.CompleteAsync([new("user", $"weather in {city}?")]).ConfigureAwait(false);
        // AddDbosSemanticKernelPluginsFromAssembly registers the plugin under the default
        // name (interface minus leading I): IWeatherTools → WeatherTools.
        var tool = await _kernel.InvokeAsync("WeatherTools", "GetWeather", new() { ["city"] = city }).ConfigureAwait(false);
        return $"{llm.Content}|{tool.GetValue<string>()}";
    }
}

#pragma warning restore CA1812
