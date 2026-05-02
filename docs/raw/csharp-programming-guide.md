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

---

## Next Steps

- Learn how to add DBOS to your own application.
- Use `IHostedService` / `IHost` integration via `Dbos.Transact.Hosting` for production apps.
- Check out the test suite under `test/Dbos.Transact.Tests/` for more patterns.
