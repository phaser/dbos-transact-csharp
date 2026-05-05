using System.ComponentModel;
using System.Reflection;
using Dbos.Transact.Workflow;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Dbos.Transact.SemanticKernel;

/// <summary>
/// Bridges DBOS proxy-based step checkpointing to Microsoft Semantic Kernel's plugin model.
/// Mirrors the <c>@function_tool @DBOS.step()</c> decorator stack from <c>dbos-openai-agents</c>:
/// methods marked with both <see cref="StepAttribute"/> and <see cref="KernelFunctionAttribute"/>
/// become Kernel functions whose every invocation is checkpointed to the DBOS system database.
/// </summary>
public static class DbosKernelExtensions
{
    /// <summary>
    /// Registers <paramref name="impl"/> with DBOS as a proxy and adds its <c>[KernelFunction]</c>
    /// methods to <paramref name="kernel"/> as a single plugin. Tool invocations from the kernel —
    /// whether driven by automatic function calling, by an agent runner, or by direct
    /// <c>kernel.InvokeAsync(...)</c> — flow through the proxy and are checkpointed by DBOS.
    /// Must be called before <see cref="Dbos.LaunchAsync"/>.
    /// </summary>
    /// <typeparam name="T">An interface declaring tool methods. Each tool method must carry both
    /// <see cref="KernelFunctionAttribute"/> (so SK exposes it) and <see cref="StepAttribute"/>
    /// (so DBOS checkpoints it).</typeparam>
    /// <param name="kernel">The Semantic Kernel instance to register the plugin on.</param>
    /// <param name="dbos">The DBOS instance that will own the proxy.</param>
    /// <param name="impl">The concrete implementation of <typeparamref name="T"/>.</param>
    /// <param name="pluginName">Plugin name visible to the kernel/agent. Defaults to the
    /// interface name with a leading <c>I</c> stripped.</param>
    /// <param name="instanceName">Optional DBOS instance name (for multi-instance scenarios).</param>
    /// <returns>The registered <see cref="KernelPlugin"/>.</returns>
    public static KernelPlugin AddDbosPlugin<T>(
        this Kernel kernel,
        Dbos dbos,
        T impl,
        string? pluginName = null,
        string? instanceName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(dbos);
        ArgumentNullException.ThrowIfNull(impl);

        var interfaceType = typeof(T);
        if (!interfaceType.IsInterface)
        {
            throw new ArgumentException(
                $"AddDbosPlugin requires an interface type; {interfaceType.Name} is not an interface.",
                nameof(T));
        }

        var kernelMethods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.IsDefined(typeof(KernelFunctionAttribute), inherit: true))
            .ToList();

        if (kernelMethods.Count == 0)
        {
            throw new ArgumentException(
                $"Interface {interfaceType.Name} has no methods marked with [KernelFunction]. " +
                $"Add [KernelFunction] alongside [Step] to expose tools to Semantic Kernel.",
                nameof(T));
        }

        var proxy = dbos.RegisterProxy<T>(impl, instanceName);

        var functions = new List<KernelFunction>(kernelMethods.Count);
        foreach (var method in kernelMethods)
        {
            var kfAttr = method.GetCustomAttribute<KernelFunctionAttribute>(inherit: true);
            var descAttr = method.GetCustomAttribute<DescriptionAttribute>(inherit: true);

            var functionName = !string.IsNullOrEmpty(kfAttr?.Name) ? kfAttr!.Name : StripAsyncSuffix(method.Name);
            var description = descAttr?.Description ?? string.Empty;

            functions.Add(KernelFunctionFactory.CreateFromMethod(
                method: method,
                target: proxy,
                functionName: functionName,
                description: description,
                loggerFactory: kernel.LoggerFactory));
        }

        var resolvedPluginName = pluginName ?? StripInterfacePrefix(interfaceType.Name);
        var plugin = KernelPluginFactory.CreateFromFunctions(resolvedPluginName, description: null, functions);
        kernel.Plugins.Add(plugin);
        return plugin;
    }

    /// <summary>
    /// Wraps the kernel's registered <c>IChatCompletionService</c> with a DBOS-checkpointed
    /// proxy and registers it as <see cref="IDurableChatCompletionService"/>. Each LLM
    /// turn issued through the returned proxy is recorded as a single step, so workflow
    /// recovery returns the cached completion instead of re-issuing the request.
    /// Must be called before <see cref="Dbos.LaunchAsync"/>.
    /// </summary>
    /// <param name="kernel">The kernel — must already have an <c>IChatCompletionService</c>
    /// registered (via <c>AddOpenAIChatCompletion</c>, <c>AddAzureOpenAIChatCompletion</c>, etc.).</param>
    /// <param name="dbos">The DBOS instance that will own the proxy.</param>
    /// <param name="instanceName">Optional DBOS instance name.</param>
    /// <returns>A DBOS-proxied <see cref="IDurableChatCompletionService"/> ready to inject
    /// into a workflow class.</returns>
    public static IDurableChatCompletionService AddDurableChatCompletion(
        this Kernel kernel,
        Dbos dbos,
        string? instanceName = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(dbos);

        var inner = kernel.GetRequiredService<IChatCompletionService>();
        var concrete = new DurableChatCompletionService(inner, kernel);
        return dbos.RegisterProxy<IDurableChatCompletionService>(concrete, instanceName);
    }

    private static string StripAsyncSuffix(string name) =>
        name.EndsWith("Async", StringComparison.Ordinal) && name.Length > "Async".Length
            ? name[..^"Async".Length]
            : name;

    private static string StripInterfacePrefix(string name) =>
        name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1])
            ? name[1..]
            : name;
}
