# dbos-transact-csharp

A C#/.NET port of [dbos-transact-java](https://github.com/dbos-inc/dbos-transact-java) — a lightweight durable-workflow library built on top of a relational database.

> **Status:** alpha (`0.0.0-alpha.x`) — published to NuGet for early testing. The API closely mirrors the Java reference implementation.

## What DBOS Transact gives you

- **Durable workflows** checkpointed to the database, with automatic resume-on-restart.
- **Durable queues** with no external broker.
- **Scheduled execution** — cron and long durable sleeps.
- **Workflow events and notifications** with exactly-once delivery.
- **Async workflow handles** with status polling and result retrieval.
- **Admin HTTP endpoints** and **conductor (WebSocket) protocol** parity with the other DBOS runtimes.

## Quick Start

### Install

```bash
dotnet add package Dbos.Transact --version 0.0.0-alpha.0.43
dotnet add package Dbos.Transact.Postgres --version 0.0.0-alpha.0.43
```

Or use SQLite for a zero-dependency local setup:

```bash
dotnet add package Dbos.Transact --version 0.0.0-alpha.0.43
dotnet add package Dbos.Transact.Sqlite --version 0.0.0-alpha.0.43
```

### Declare steps and a workflow

```csharp
using Dbos.Transact.Workflow;

// Steps — durable, checkpointed on each call
public interface IMySteps
{
    [Step] Task<string> FetchDataAsync(string url);
    [Step] Task SaveResultAsync(string data);
}

public sealed class MySteps : IMySteps
{
    public async Task<string> FetchDataAsync(string url) { /* ... */ }
    public async Task SaveResultAsync(string data) { /* ... */ }
}

// Workflow — resumes from the last completed step after a crash
public interface IMyWorkflow
{
    Task RunAsync(string url);
}

public sealed class MyWorkflow(IMySteps steps) : IMyWorkflow
{
    [Workflow]
    public async Task RunAsync(string url)
    {
        var data = await steps.FetchDataAsync(url);
        await steps.SaveResultAsync(data);
    }
}
```

### Register, launch, and run

```csharp
using Dbos.Transact;
using Dbos.Transact.Sqlite;

await using var dbos = Dbos.Builder("my-app")
    .UseSqlite("Data Source=myapp.db")
    .Build();

var stepProxy = dbos.RegisterProxy<IMySteps>(new MySteps());
dbos.RegisterProxy<IMyWorkflow>(new MyWorkflow(stepProxy));

await dbos.LaunchAsync();

var handle = await dbos.StartWorkflowAsync<object?>(
    workflowName: nameof(MyWorkflow.RunAsync),
    className: typeof(MyWorkflow).FullName,
    instanceName: null,
    args: ["https://example.com"]);

await handle.GetResultAsync();
```

With `Microsoft.Extensions.Hosting`:

```csharp
services.AddDbos("my-app", builder => builder.UsePostgres(connectionString));
services.AddDbosWorkflow<IMySteps, MySteps>();
services.AddDbosWorkflow<IMyWorkflow, MyWorkflow>();
// Or auto-register every [Workflow]/[Step]/[Scheduled]-bearing type in an assembly:
// services.AddDbosWorkflowsFromAssembly(typeof(Program).Assembly);
```

For a full walkthrough see [`docs/raw/csharp-programming-guide.md`](docs/raw/csharp-programming-guide.md).

## Packages

| NuGet | Role |
|---|---|
| `Dbos.Transact` | Core — dialect-agnostic. `[Workflow]` / `[Step]` / `[Scheduled]` surface, executor, registries, portable serializer, migrations. |
| `Dbos.Transact.Postgres` | Npgsql-backed Postgres dialect. `LISTEN`/`NOTIFY`, `SKIP LOCKED`, advisory locks. |
| `Dbos.Transact.Sqlite` | Microsoft.Data.Sqlite-backed dialect. First-class production target for small single-host projects; [Litestream](https://litestream.io) is the documented backup path. |
| `Dbos.Transact.Hosting` | `Microsoft.Extensions.Hosting` integration — `services.AddDbos(…)` + `AddDbosWorkflow<TInterface, TImpl>()`. |
| `Dbos.Transact.SemanticKernel` | Microsoft Semantic Kernel bridge. `kernel.AddDbosPlugin(dbos, tools)` checkpoints every `[Step]+[KernelFunction]` tool invocation; `kernel.AddDurableChatCompletion(dbos)` checkpoints every LLM turn. Together they let an agent loop replay end-to-end without re-spending tokens or re-firing tool side effects. |
| `Dbos.Transact.Cli` | `System.CommandLine`-based CLI (`migrate`, `reset`, `workflow`). |

## Target framework

`net8.0` (current LTS). Multi-targeting `net10.0` can be added later if there is demand.

## Documentation

- **Programming guide:** [`docs/raw/csharp-programming-guide.md`](docs/raw/csharp-programming-guide.md) — workflows, steps, queues, and a Java→C# API mapping table.
- **Design document:** [`docs/raw/design.md`](docs/raw/design.md) — v1 scope, repo layout, and architecture decisions.
- **Knowledge base:** [`docs/wiki/`](docs/wiki/) — concept, entity, and summary pages. Start at [`docs/wiki/index.md`](docs/wiki/index.md).
- **Agent instructions:** [`CLAUDE.md`](CLAUDE.md) — coding conventions, test layout, and knowledge-management protocol for LLM-assisted work on this repo.

## Upstream references

- [dbos-transact-java](https://github.com/dbos-inc/dbos-transact-java) — primary reference implementation.
- [dbos-transact-py](https://github.com/dbos-inc/dbos-transact-py) — reference for the dual-dialect pattern.
- [dbos-transact-ts](https://github.com/dbos-inc/dbos-transact-ts) — reference for the public API shape.

## License

[MIT](LICENSE)
