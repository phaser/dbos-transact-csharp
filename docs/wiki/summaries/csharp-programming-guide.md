---
title: "Summary: C# Programming Guide"
type: summary
tags: [guide, workflows, steps, queues, port-decision, foundational, csharp]
created: 2026-05-02
updated: 2026-05-02
sources: ["raw/csharp-programming-guide.md"]
confidence: high
---

## Key Points

- **Attributes replace annotations**: Java `@Workflow` / `@Step` → C# `[Workflow]` / `[Step]` attributes on interface/class methods.
- **Proxy model for steps**: Java uses `dbos.runStep(() -> ..., "name")` lambdas. C# uses the Castle.DynamicProxy interceptor — step logic lives in a concrete class implementing an interface; call the registered proxy and DBOS automatically checkpoints the call.
- **Builder pattern**: `Dbos.Builder("app-name").UsePostgres(connStr).Build()` replaces `new DBOS(DBOSConfig)`.
- **Async everywhere**: All lifecycle methods (`LaunchAsync`, `ShutdownAsync`), workflow start (`StartWorkflowAsync`), and result retrieval (`GetResultAsync`) are `Task`-returning and must be awaited.
- **No lambda-based `startWorkflow`**: C# uses `dbos.StartWorkflowAsync<T>(workflowName, className, null, args, options)` — the workflow is looked up by registered name, not by a lambda that wraps a proxy call.
- **Queue registration must precede launch**: `dbos.RegisterQueue(queue)` must be called before `await dbos.LaunchAsync()`, same as Java.
- **`StartWorkflowOptions(queue)`**: Enqueue a workflow by passing `new StartWorkflowOptions(queue)` as options; this is the C# equivalent of Java's `new StartWorkflowOptions().withQueue("queue-name")`.
- **SQLite dialect available**: For local development without Docker, swap `UsePostgres` for `UseSqlite("Data Source=dbos.db")`.
- **`await using` for cleanup**: `Dbos` implements `IAsyncDisposable`; `await using var dbos = ...` ensures `ShutdownAsync` is called on exit.
- **Three-package split**: `Dbos.Transact` (core), `Dbos.Transact.Postgres` (Postgres dialect), `Dbos.Transact.Sqlite` (SQLite dialect); current alpha version is `0.0.0-alpha.0.35`.

## Relevant Concepts

- [[concepts/durable-workflow]] — checkpointing, recovery from last completed step
- [[concepts/method-interception]] — how `[Step]` calls are intercepted by the proxy
- [[concepts/queue-dequeue-flow]] — queue registration, `StartWorkflowOptions`, async dispatch
- [[concepts/dialect-abstraction]] — `UsePostgres` / `UseSqlite` extension methods
- [[concepts/sqlite-production-target]] — SQLite as a zero-dependency local-dev option

## Source Metadata

- **Type**: Translated programming guide (Java → C#)
- **Original URL**: https://docs.dbos.dev/java/programming-guide
- **Java SDK version**: `dev.dbos:transact:0.8.0`
- **C# SDK version**: `Dbos.Transact 0.0.0-alpha.0.35`
- **Translated**: 2026-05-02
