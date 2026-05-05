# Learn DBOS C# — Programming Guide

> Translated from: https://docs.dbos.dev/java/programming-guide
> Source version: Java guide (dev.dbos:transact:0.8.0)
> Translated for: Dbos.Transact v0.0.0-alpha.0.35
> Date: 2026-05-02

This guide shows you how to use DBOS to build C# apps that are resilient to any failure.

---

## 1. Setting Up Your Environment

### Prerequisites

- .NET 10 SDK
- PostgreSQL (or use SQLite for a zero-dependency local setup — see below)
- NuGet packages: `Dbos.Transact` and `Dbos.Transact.Postgres` (or `Dbos.Transact.Sqlite`)

### Create a new project

```bash
dotnet new console -n MyDbosApp
cd MyDbosApp
```

### Install DBOS packages

With PostgreSQL:
```bash
dotnet add package Dbos.Transact --version 0.0.0-alpha.0.35
dotnet add package Dbos.Transact.Postgres --version 0.0.0-alpha.0.35
```

Or with SQLite (no Docker required — useful for local development):
```bash
dotnet add package Dbos.Transact --version 0.0.0-alpha.0.35
dotnet add package Dbos.Transact.Sqlite --version 0.0.0-alpha.0.35
```

### Start PostgreSQL (Docker, if not already running)

```bash
docker run --rm \
  -e POSTGRES_PASSWORD=dbos \
  -e POSTGRES_USER=dbos \
  -e POSTGRES_DB=dbos \
  -p 5432:5432 \
  postgres
```

### Connection string

PostgreSQL:
```
Host=localhost;Port=5432;Database=dbos;Username=dbos;Password=dbos
```

SQLite (file-backed):
```
Data Source=dbos.db
```

---

## 2. Workflows and Steps

DBOS helps you add reliability to .NET programs.
The key feature is **workflow methods** comprised of **steps**.
DBOS automatically provides durability by checkpointing the state of your workflows and steps to its system database.
If your program crashes or is interrupted, DBOS uses this saved state to recover each workflow from its last completed step.
Thus, DBOS makes your application resilient to any failure.

### C# design: attributes + proxies

Unlike Java (which uses `dbos.runStep(() -> ...)` lambdas), the C# port uses the **proxy/attribute model**:

- Mark step methods with `[Step]` on an interface.
- Mark workflow entry-point methods with `[Workflow]`.
- Register both through `dbos.RegisterProxy<T>(impl)`, which returns a durable proxy.
- Inject the step proxy into your workflow class. When the workflow calls the step proxy, DBOS intercepts the call, checkpoints the result, and replays it on recovery without re-executing.

### Simple two-step workflow

**Program.cs**
```csharp
using Dbos.Transact;
using Dbos.Transact.Postgres;

var connectionString = "Host=localhost;Port=5432;Database=dbos;Username=dbos;Password=dbos";

await using var dbos = Dbos.Builder("my-app")
    .UsePostgres(connectionString)
    .Build();

var stepProxy = dbos.RegisterProxy<IExampleSteps>(new ExampleSteps());
dbos.RegisterProxy<IExampleWorkflow>(new ExampleWorkflow(stepProxy));

await dbos.LaunchAsync();

var handle = await dbos.StartWorkflowAsync<string>(
    workflowName: nameof(ExampleWorkflow.RunAsync),
    className: typeof(ExampleWorkflow).FullName,
    instanceName: null,
    args: Array.Empty<object?>());

var result = await handle.GetResultAsync();
Console.WriteLine($"Result: {result}");
```

**IExampleSteps.cs**
```csharp
using Dbos.Transact.Workflow;

public interface IExampleSteps
{
    [Step]
    Task<string> StepOneAsync();

    [Step]
    Task<string> StepTwoAsync();
}
```

**ExampleSteps.cs**
```csharp
public sealed class ExampleSteps : IExampleSteps
{
    public async Task<string> StepOneAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        Console.WriteLine("Step 1 complete");
        return "step-one-result";
    }

    public async Task<string> StepTwoAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        Console.WriteLine("Step 2 complete");
        return "step-two-result";
    }
}
```

**IExampleWorkflow.cs**
```csharp
public interface IExampleWorkflow
{
    Task<string> RunAsync();
}
```

**ExampleWorkflow.cs**
```csharp
using Dbos.Transact.Workflow;

public sealed class ExampleWorkflow : IExampleWorkflow
{
    private readonly IExampleSteps _steps;

    public ExampleWorkflow(IExampleSteps steps) => _steps = steps;

    [Workflow]
    public async Task<string> RunAsync()
    {
        var r1 = await _steps.StepOneAsync();
        var r2 = await _steps.StepTwoAsync();
        return $"{r1} + {r2}";
    }
}
```

Run it:
```bash
dotnet run
```

Expected output:
```
Step 1 complete
Step 2 complete
Result: step-one-result + step-two-result
```

### Observing durable recovery with an HTTP server

To observe DBOS's durable execution, add `Microsoft.AspNetCore.App` and convert to a minimal API server:

```bash
dotnet add package Microsoft.AspNetCore.App
```

**Program.cs (HTTP server version)**
```csharp
using Dbos.Transact;
using Dbos.Transact.Postgres;

var connectionString = "Host=localhost;Port=5432;Database=dbos;Username=dbos;Password=dbos";

await using var dbos = Dbos.Builder("my-app")
    .UsePostgres(connectionString)
    .Build();

var stepProxy = dbos.RegisterProxy<IExampleSteps>(new ExampleSteps());
dbos.RegisterProxy<IExampleWorkflow>(new ExampleWorkflow(stepProxy));

await dbos.LaunchAsync();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", async () =>
{
    var handle = await dbos.StartWorkflowAsync<string>(
        workflowName: nameof(ExampleWorkflow.RunAsync),
        className: typeof(ExampleWorkflow).FullName,
        instanceName: null,
        args: Array.Empty<object?>());
    return await handle.GetResultAsync();
});

await app.RunAsync();
```

Run the app, then visit http://localhost:5000.

In your terminal you should see:
```
Step 1 complete
Step 2 complete
```

Press Ctrl+C to stop the app mid-execution (e.g. after "Step 1 complete").
Then run `dotnet run` again to restart it. You should see:

```
Step 2 complete
```

DBOS recovers your workflow from the last completed step, executing step two without re-executing step one.

---

## 3. Queues and Parallelism

To run many workflows concurrently, use DBOS queues.

**Program.cs (queue version)**
```csharp
using Dbos.Transact;
using Dbos.Transact.Postgres;
using Dbos.Transact.Workflow;

var connectionString = "Host=localhost;Port=5432;Database=dbos;Username=dbos;Password=dbos";

var exampleQueue = new Queue("example-queue");

await using var dbos = Dbos.Builder("my-app")
    .UsePostgres(connectionString)
    .Build();

dbos.RegisterQueue(exampleQueue);

var stepProxy = dbos.RegisterProxy<IExampleSteps>(new ExampleSteps());
dbos.RegisterProxy<IExampleWorkflow>(new ExampleWorkflow(stepProxy));

await dbos.LaunchAsync();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", async () =>
{
    var options = new StartWorkflowOptions(exampleQueue);

    var handles = new List<WorkflowHandle<string>>();
    for (int i = 0; i < 10; i++)
    {
        var handle = await dbos.StartWorkflowAsync<string>(
            workflowName: nameof(ExampleWorkflow.RunAsync),
            className: typeof(ExampleWorkflow).FullName,
            instanceName: null,
            args: Array.Empty<object?>(),
            options: options);
        handles.Add(handle);
    }

    var results = await Task.WhenAll(handles.Select(h => h.GetResultAsync()));
    return string.Join(Environment.NewLine, results);
});

await app.RunAsync();
```

When you enqueue a workflow via `new StartWorkflowOptions(exampleQueue)`, DBOS executes it **asynchronously** — running it in the background without waiting for it to finish. `StartWorkflowAsync` returns a `WorkflowHandle<T>` representing the state of the enqueued workflow.

This example enqueues ten workflows, then waits for them all to finish using `GetResultAsync()`.
You can see how all ten run concurrently — even if each takes five seconds, they all finish at roughly the same time.

---

## 4. Hosting Integration (`Dbos.Transact.Hosting`)

For production apps built on `IHost` / `WebApplication`, the `Dbos.Transact.Hosting` package provides `IServiceCollection` extensions that wire DBOS into the generic .NET host.

### Install the hosting package

```bash
dotnet add package Dbos.Transact.Hosting --version 0.0.0-alpha.0.35
```

### Register DBOS in `Program.cs`

```csharp
using Dbos.Transact.Hosting;
using Dbos.Transact.Postgres;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDbos("my-app", b => b.UsePostgres(
        builder.Configuration.GetConnectionString("Dbos")!))
    .AddDbosWorkflow<IExampleSteps, ExampleSteps>()
    .AddDbosWorkflow<IExampleWorkflow, ExampleWorkflow>()
    .AddDbosQueue(new Queue("example-queue"));

var app = builder.Build();

app.MapGet("/", async (IExampleWorkflow workflow) =>
{
    var handle = await app.Services.GetRequiredService<Dbos>()
        .StartWorkflowAsync<string>(
            nameof(ExampleWorkflow.RunAsync),
            typeof(ExampleWorkflow).FullName!,
            null,
            Array.Empty<object?>());
    return await handle.GetResultAsync();
});

await app.RunAsync();
```

`AddDbos` registers `Dbos` as a singleton, configures options, and starts the hosted service lifecycle. `AddDbosWorkflow<TInterface, TImpl>` registers the impl as a singleton and binds the interface to a DBOS-managed proxy — resolving `IExampleWorkflow` from DI returns the durable proxy, not the raw impl.

### Auto-discovery with `AddDbosWorkflowsFromAssembly`

For large projects with many workflow/step classes, you can replace individual `AddDbosWorkflow` calls with a single assembly scan:

```csharp
builder.Services
    .AddDbos("my-app", b => b.UsePostgres(connectionString))
    .AddDbosWorkflowsFromAssembly(typeof(ExampleWorkflow).Assembly);
```

`AddDbosWorkflowsFromAssembly` scans the given assembly for concrete classes whose methods (or whose interface methods) are annotated with `[Workflow]` or `[Step]`, and auto-registers each `(interface, impl)` pair — the same as calling `AddDbosWorkflow<TInterface, TImpl>` for each one explicitly. Pairs where no matching interface is found are silently skipped.

This is analogous to ASP.NET Core's `AddControllers` / `MapControllers` pattern: opt-in, convention-based, zero manual wiring.

> **Note:** Explicit `AddDbosWorkflow<TInterface, TImpl>` calls are always supported alongside `AddDbosWorkflowsFromAssembly`. If you need per-registration `instanceName` values you must still use the explicit form.

---

## 5. Building Durable AI Agents (`Dbos.Transact.SemanticKernel`)

Microsoft Semantic Kernel is the recommended path for AI agents in C#. The `Dbos.Transact.SemanticKernel` package bridges SK's plugin model to DBOS's `[Step]` checkpointing: any tool invoked by an SK kernel or agent — whether via automatic function calling, an agent runner, or a direct `kernel.InvokeAsync(...)` — is recorded to the DBOS system database, replays from cache on workflow recovery, and never re-bills the underlying API call.

This is the C# analogue of the [`dbos-openai-agents`](https://github.com/dbos-inc/dbos-openai) Python package shown on https://dbos.dev.

### Define tools as a `[Step]` + `[KernelFunction]` interface

```csharp
using System.ComponentModel;
using Dbos.Transact.Workflow;
using Microsoft.SemanticKernel;

public interface IWeatherTools
{
    [KernelFunction]
    [Description("Get the weather for a city.")]
    [Step]
    Task<string> GetWeatherAsync(string city);
}

public sealed class WeatherTools : IWeatherTools
{
    public Task<string> GetWeatherAsync(string city) =>
        Task.FromResult($"Sunny in {city}");
}
```

The `[KernelFunction]` attribute makes the method discoverable by Semantic Kernel; `[Step]` makes it durable under DBOS. Both must be on the **interface** method (not the concrete impl) — DBOS's interceptor reads attributes off the interface so the proxy can route calls correctly.

### Bridge the plugin to the kernel

Call `kernel.AddDbosPlugin<T>(dbos, impl)` *before* `dbos.LaunchAsync()`. It registers the impl with DBOS as a proxy and adds the matching `KernelPlugin` to the kernel:

```csharp
using Dbos.Transact.SemanticKernel;

var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4o-mini", apiKey: "...") // or any IChatCompletionService
    .Build();

await using var dbos = Dbos.Builder("weather-agent")
    .UseSqlite("Data Source=agent.db")
    .Build();

kernel.AddDbosPlugin<IWeatherTools>(dbos, new WeatherTools(), pluginName: "Weather");
dbos.RegisterProxy<IAgentWorkflow>(new AgentWorkflow(kernel));

await dbos.LaunchAsync();
```

### The agent loop is a `[Workflow]`

```csharp
public interface IAgentWorkflow
{
    Task<string> RunAsync(string userInput);
}

public sealed class AgentWorkflow(Kernel kernel) : IAgentWorkflow
{
    [Workflow]
    public async Task<string> RunAsync(string userInput)
    {
        // The kernel's auto function-calling will dispatch to GetWeatherAsync
        // through the DBOS proxy → each tool call gets checkpointed.
        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };
        var result = await kernel.InvokePromptAsync(userInput, new(settings));
        return result.GetValue<string>() ?? string.Empty;
    }
}

// Start the workflow durably:
var handle = await dbos.StartWorkflowAsync<string>(
    workflowName: nameof(AgentWorkflow.RunAsync),
    className: typeof(AgentWorkflow).FullName,
    instanceName: null,
    args: ["What's the weather in Boston?"]);

var answer = await handle.GetResultAsync();
```

If the worker crashes mid-agent-loop, DBOS recovers the workflow on restart, replays completed tool calls from the database (without re-invoking them), and resumes at the next pending step.

### What's checkpointed and what isn't

- **Checkpointed:** every `[Step]+[KernelFunction]` tool method registered via `AddDbosPlugin`. Each tool call is a single step row in `operation_outputs` keyed by `(workflow_id, function_id)`.
- **Not checkpointed (yet):** raw `IChatCompletionService` calls. The LLM call itself is not durable unless you wrap it in your own `[Step]`-annotated interface. A future release may ship a built-in `DurableChatCompletionService` adapter; for now, mirror the tool-interface pattern for any non-deterministic external call you want replay-safe.

---

## Key API Mapping: Java → C#

| Java | C# |
|------|----|
| `new DBOS(config)` | `Dbos.Builder("app-name").UsePostgres(connStr).Build()` |
| `dbos.registerProxy(Interface.class, impl)` | `dbos.RegisterProxy<IInterface>(impl)` |
| `dbos.launch()` | `await dbos.LaunchAsync()` |
| `dbos.shutdown()` | `await dbos.ShutdownAsync()` (or `await using`) |
| `@Workflow` annotation | `[Workflow]` attribute |
| `@Step` annotation | `[Step]` attribute |
| `dbos.runStep(() -> ..., "name")` | Call step proxy method directly (intercepted automatically) |
| `dbos.startWorkflow(() -> proxy.method(), opts)` | `await dbos.StartWorkflowAsync<T>(name, className, null, args, opts)` |
| `handle.getResult()` | `await handle.GetResultAsync()` |
| `new StartWorkflowOptions().withQueue("q")` | `new StartWorkflowOptions(queue)` |
| `dbos.registerQueue(queue)` | `dbos.RegisterQueue(queue)` |
| `new Queue("name")` | `new Queue("name")` |
| `@EnableAutoConfiguration` / Spring beans | `AddDbosWorkflowsFromAssembly(assembly)` |

---

## Next Steps

- Check out the test suite under `test/Dbos.Transact.Tests/` for more patterns.
- See `test/Dbos.Transact.Hosting.Tests/DbosHostingAutoDiscoveryTests.cs` for examples of assembly-scan registration.
