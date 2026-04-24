---
title: "Dbos.Transact (core package)"
type: entity
tags: [project, core, dialect-agnostic]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
---

## Overview

`Dbos.Transact` is the dialect-agnostic core NuGet of the C# port. It contains the public workflow surface (`[Workflow]`, `[Step]`, `[Scheduled]`, `WorkflowHandle`, `Queue`, `WorkflowEvent`, `WorkflowStream`), the executor and registries, the portable serializer, the migration manager, and the abstract `SystemDatabase` — everything except the concrete database drivers. It depends on `Castle.Core` for interception but carries no Npgsql or Microsoft.Data.Sqlite dependency.

## Characteristics

- **Assembly / namespace / NuGet ID:** `Dbos.Transact`.
- **Public surface** (`Workflow/`): `[Workflow]`, `[Step]`, `[Scheduled]`, `Queue`, `WorkflowHandle`, `WorkflowStatus`, `WorkflowState`, `WorkflowEvent`, `WorkflowStream`, `WorkflowSchedule`, `StepInfo`, `StepOptions`, `Timeout`, `ForkOptions`.
- **Facade / client:** `Dbos` (static facade + fluent builder — `DBOS.java` equivalent), `DbosClient` (standalone client — `DBOSClient.java` equivalent).
- **Options:** `DbosOptions`, `StartWorkflowOptions`, bound via the standard .NET Options pattern (equivalent to Java `DBOSConfig`).
- **Admin:** `Admin/AdminServer` exposes the admin HTTP endpoints.
- **Conductor:** `Conductor/` with a `ClientWebSocket`-backed implementation and `Protocol/` DTOs for request/response parity with the other runtimes.
- **Execution:** `DbosExecutor`, `QueueService`, `SchedulerService`, `RegisteredWorkflow`, `DbosLifecycleListener`.
- **Internal / registries:** `WorkflowRegistry`, `QueueRegistry`, `DbosInvocationInterceptor` (the Castle `IInterceptor`), `DbosProxyFactory`, `AppVersionComputer`, `Validation`.
- **Context:** `DbosContext`, `DbosContextHolder` (`AsyncLocal<DbosContext>`, the C# equivalent of `ThreadLocal`), `WorkflowInfo`, `WorkflowOptions`.
- **Persistence surface:** `Database/SystemDatabase.cs` (abstract), `Database/ISqlDialect.cs`, `Database/Daos/*` for workflow/steps/queues/streams/schedules/notifications.
- **Exceptions:** 10 `Dbos*Exception` types mirroring the Java `DBOS*Exception` surface.
- **Migrations:** `MigrationManager` plus embedded SQL resources under `Migrations/Sql/Postgres/` and `Migrations/Sql/Sqlite/`.

## Common Strategies

- [[concepts/durable-workflow]]
- [[concepts/method-interception]]
- [[concepts/portable-serializer]]
- [[concepts/dialect-abstraction]]
- [[concepts/no-orm-constraint]]

## Related Entities

- [[entities/dbos-transact-postgres]]
- [[entities/dbos-transact-sqlite]]
- [[entities/dbos-transact-hosting]]
- [[entities/dbos-transact-cli]]
- [[entities/castle-dynamicproxy]]
- [[entities/dbos-transact-java]]
