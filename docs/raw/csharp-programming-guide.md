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

Microsoft Semantic Kernel is the recommended path for AI agents in C#. The `Dbos.Transact.SemanticKernel` package gives you two checkpointed primitives:

1. **`AddDbosPlugin<T>`** — wraps a `[Step]+[KernelFunction]` interface so every tool invocation through the kernel is recorded.
2. **`AddDurableChatCompletion`** — wraps the kernel's `IChatCompletionService` so every LLM turn is recorded.

Together they let you write an agent loop where **both** the LLM call and each tool call are durable: a worker crash mid-loop replays from cache without re-spending tokens or re-firing tool side effects. This is the C# analogue of the [`dbos-openai-agents`](https://github.com/dbos-inc/dbos-openai) Python package shown on https://dbos.dev.

### Why a manual loop instead of `kernel.InvokePromptAsync`?

SK's auto-function-calling does the LLM round-trip *and* the tool dispatch inside one call. Wrapping that in a single `[Step]` would nest tool steps inside the LLM step — DBOS supports nested step calls but doesn't checkpoint the inner step's context-stack interaction perfectly, so this port treats nested `[Step]`-from-`[Step]` as a non-goal. The fix is to drive the loop yourself: each LLM turn is one top-level step, each tool dispatch is another. The code is a few lines of `while`, and recovery is exact.

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

`[KernelFunction]` makes the method discoverable by SK; `[Step]` makes it durable under DBOS. Both attributes go on the **interface** method — DBOS's interceptor reads attributes off the interface so the proxy can route calls correctly.

### Wire up the kernel, the plugin, and the durable chat client

Both `AddDbosPlugin` and `AddDurableChatCompletion` must be called *before* `dbos.LaunchAsync()`.

```csharp
using Dbos.Transact;
using Dbos.Transact.SemanticKernel;
using Dbos.Transact.Sqlite;
using Microsoft.SemanticKernel;

var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4o-mini", apiKey: "...") // or any IChatCompletionService
    .Build();

await using var dbos = Dbos.Builder("weather-agent")
    .UseSqlite("Data Source=agent.db")
    .Build();

kernel.AddDbosPlugin<IWeatherTools>(dbos, new WeatherTools(), pluginName: "Weather");
var chat = kernel.AddDurableChatCompletion(dbos);
dbos.RegisterProxy<IAgentWorkflow>(new AgentWorkflow(kernel, chat));

await dbos.LaunchAsync();
```

### The agent loop is a `[Workflow]`

The workflow body alternates between **one durable LLM turn** and **dispatching the tool calls it returned**. The loop terminates when the LLM returns a response with no tool calls.

```csharp
public interface IAgentWorkflow
{
    Task<string> RunAsync(string userInput);
}

public sealed class AgentWorkflow(Kernel kernel, IDurableChatCompletionService chat) : IAgentWorkflow
{
    [Workflow]
    public async Task<string> RunAsync(string userInput)
    {
        var history = new List<DurableChatMessage> { new("user", userInput) };

        for (int turn = 0; turn < 10; turn++)
        {
            // Step: one LLM turn (cached on replay → no token re-spend).
            var response = await chat.CompleteAsync(history);

            if (response.ToolCalls.Count == 0)
                return response.Content ?? string.Empty;

            history.Add(new DurableChatMessage("assistant", response.Content ?? string.Empty));

            foreach (var call in response.ToolCalls)
            {
                var args = new KernelArguments();
                foreach (var kv in call.Arguments) args[kv.Key] = kv.Value;

                // Step: one tool dispatch (cached on replay → no side-effect re-fire).
                var toolResult = await kernel.InvokeAsync(call.PluginName, call.FunctionName, args);
                history.Add(new DurableChatMessage("tool", toolResult.GetValue<string>() ?? string.Empty, ToolCallId: call.Id));
            }
        }

        throw new InvalidOperationException("agent exceeded max turns");
    }
}
```

Start the workflow durably:

```csharp
var handle = await dbos.StartWorkflowAsync<string>(
    workflowName: nameof(AgentWorkflow.RunAsync),
    className: typeof(AgentWorkflow).FullName,
    instanceName: null,
    args: ["What's the weather in Boston?"]);

var answer = await handle.GetResultAsync();
```

### What's checkpointed

Every non-deterministic external call inside the workflow body is a step row in `operation_outputs`:

- **LLM turn** — `IDurableChatCompletionService.CompleteAsync` is `[Step]`-annotated. The full `DurableChatResponse` (text + tool calls) is checkpointed and returned verbatim on replay.
- **Tool calls** — `kernel.InvokeAsync(plugin, function, args)` routes through the DBOS proxy registered by `AddDbosPlugin`. Each call is its own step.

Replay = no LLM tokens spent + no tool side effects re-fired. The `while` loop walks the same path on every recovery because each step it depends on returns its cached value.

### Complete runnable example: support-triage agent

Two tools (lookup an order, issue a refund) and the agent loop above. Re-running with the same workflow ID demonstrates that the LLM is not re-called and the refund is not re-issued.

**Install packages**

```bash
dotnet new console -n SupportAgent
cd SupportAgent
dotnet add package Dbos.Transact --version 0.0.0-alpha.0.43
dotnet add package Dbos.Transact.Sqlite --version 0.0.0-alpha.0.43
dotnet add package Dbos.Transact.SemanticKernel --version 0.0.0-alpha.0.43
dotnet add package Microsoft.SemanticKernel --version 1.75.0
dotnet add package Microsoft.SemanticKernel.Connectors.OpenAI --version 1.75.0
```

**ISupportTools.cs / SupportTools.cs**

```csharp
using System.ComponentModel;
using Dbos.Transact.Workflow;
using Microsoft.SemanticKernel;

public interface ISupportTools
{
    [KernelFunction, Description("Look up a customer's most recent order by email.")]
    [Step]
    Task<string> LookupOrderAsync(string customerEmail);

    [KernelFunction, Description("Issue a refund for the given order ID and amount in USD. Returns the refund confirmation code.")]
    [Step(RetriesAllowed = true, MaxAttempts = 3, IntervalSeconds = 2)]
    Task<string> IssueRefundAsync(string orderId, double amountUsd);
}

public sealed class SupportTools : ISupportTools
{
    public Task<string> LookupOrderAsync(string customerEmail)
    {
        Console.WriteLine($"[tool] LookupOrder({customerEmail})");
        return Task.FromResult("ORD-7421:$59.99");
    }

    public Task<string> IssueRefundAsync(string orderId, double amountUsd)
    {
        Console.WriteLine($"[tool] IssueRefund({orderId}, ${amountUsd}) — calling payment processor");
        return Task.FromResult($"REFUND-{Guid.NewGuid().ToString()[..8]}");
    }
}
```

**ISupportWorkflow.cs / SupportWorkflow.cs**

```csharp
using Dbos.Transact.SemanticKernel;
using Dbos.Transact.Workflow;
using Microsoft.SemanticKernel;

public interface ISupportWorkflow
{
    Task<string> HandleAsync(string supportRequest);
}

public sealed class SupportWorkflow(Kernel kernel, IDurableChatCompletionService chat) : ISupportWorkflow
{
    [Workflow]
    public async Task<string> HandleAsync(string supportRequest)
    {
        var history = new List<DurableChatMessage>
        {
            new("system", "You are a support agent. Use the available tools to resolve the request, then summarize what you did in one sentence."),
            new("user", supportRequest),
        };

        for (int turn = 0; turn < 10; turn++)
        {
            var response = await chat.CompleteAsync(history);

            if (response.ToolCalls.Count == 0)
                return response.Content ?? string.Empty;

            history.Add(new DurableChatMessage("assistant", response.Content ?? string.Empty));

            foreach (var call in response.ToolCalls)
            {
                var args = new KernelArguments();
                foreach (var kv in call.Arguments) args[kv.Key] = kv.Value;

                Console.WriteLine($"[agent] dispatching {call.PluginName}.{call.FunctionName}({string.Join(", ", call.Arguments.Select(kv => $"{kv.Key}={kv.Value}"))})");
                var toolResult = await kernel.InvokeAsync(call.PluginName, call.FunctionName, args);
                history.Add(new DurableChatMessage("tool", toolResult.GetValue<string>() ?? string.Empty, ToolCallId: call.Id));
            }
        }

        throw new InvalidOperationException("agent exceeded max turns");
    }
}
```

**Program.cs**

```csharp
using Dbos.Transact;
using Dbos.Transact.SemanticKernel;
using Dbos.Transact.Sqlite;
using Microsoft.SemanticKernel;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("Set OPENAI_API_KEY to run.");

var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4o-mini", apiKey)
    .Build();

await using var dbos = Dbos.Builder("support-agent")
    .UseSqlite("Data Source=support.db")
    .Build();

kernel.AddDbosPlugin<ISupportTools>(dbos, new SupportTools(), pluginName: "Support");
var chat = kernel.AddDurableChatCompletion(dbos);
dbos.RegisterProxy<ISupportWorkflow>(new SupportWorkflow(kernel, chat));

await dbos.LaunchAsync();

var workflowId = args.Length > 0 ? args[0] : $"req-{Guid.NewGuid()}";

var handle = await dbos.StartWorkflowAsync<string>(
    workflowName: nameof(SupportWorkflow.HandleAsync),
    className: typeof(SupportWorkflow).FullName,
    instanceName: null,
    args: ["My order arrived broken — alice@example.com, please refund."],
    options: new StartWorkflowOptions(workflowId: workflowId));

Console.WriteLine($"workflow {handle.WorkflowId} started");
Console.WriteLine($"result:  {await handle.GetResultAsync()}");

var steps = await dbos.ListWorkflowStepsAsync(handle.WorkflowId);
foreach (var s in steps)
    Console.WriteLine($"  step #{s.FunctionId} {s.FunctionName}");
```

**Run it**

```bash
export OPENAI_API_KEY=sk-...
dotnet run
```

First-run output (the LLM phrasing varies):

```
[agent] dispatching Support.LookupOrder(customerEmail=alice@example.com)
[tool] LookupOrder(alice@example.com)
[agent] dispatching Support.IssueRefund(orderId=ORD-7421, amountUsd=59.99)
[tool] IssueRefund(ORD-7421, $59.99) — calling payment processor
workflow req-abc123 started
result:  I looked up Alice's order ORD-7421 ($59.99) and issued refund REFUND-3f8a91c2.
  step #0 CompleteAsync
  step #1 LookupOrderAsync
  step #2 CompleteAsync
  step #3 IssueRefundAsync
  step #4 CompleteAsync
```

Three `CompleteAsync` steps (LLM turns) and two tool steps. Each is in `operation_outputs` keyed by `(workflow_id, function_id)`.

### Observing durable recovery

Re-run with the same workflow ID:

```bash
dotnet run -- req-abc123
```

Output:

```
workflow req-abc123 started
result:  I looked up Alice's order ORD-7421 ($59.99) and issued refund REFUND-3f8a91c2.
  step #0 CompleteAsync
  step #1 LookupOrderAsync
  step #2 CompleteAsync
  step #3 IssueRefundAsync
  step #4 CompleteAsync
```

**Notice what's missing:**

- No `[agent] dispatching ...` lines — the workflow body didn't dispatch any tools.
- No `[tool] ...` lines — the tool implementations didn't run.
- Same refund code as before — `REFUND-3f8a91c2`, not a fresh one.

DBOS recognized the workflow ID, replayed each cached step result (LLM turns, tool calls) directly from `support.db`, and reconstructed the same final answer without making any external call. Same mechanism kicks in automatically when a worker crashes mid-loop and another worker (or the same one after restart) picks the workflow up via recovery.

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
