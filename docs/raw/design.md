# dbos-transact-csharp — Design

Primary design document for the C# port of [dbos-transact-java](https://github.com/dbos-inc/dbos-transact-java).

## Goal

Port DBOS Transact — a lightweight durable-workflow library built on top of a relational database — to C#/.NET. Preserve feature parity with the Java runtime where it makes sense, and adopt .NET-idiomatic shapes where they differ.

Core capabilities to deliver:

- Durable workflows checkpointed to the database, with automatic resume-on-restart.
- Durable queues (no external broker).
- Scheduled execution (cron + long durable sleeps).
- Notifications and workflow events with exactly-once delivery.
- Async workflow handles with status polling and result retrieval.
- Admin HTTP endpoints and conductor (WebSocket) protocol parity.

## Non-goals (v1)

- AOT / native-image support. Revisit once the runtime-reflection design is settled.
- Cross-runtime client interop for exotic types (first pass supports the subset the portable serializer in Python/TS/Java already handles).
- Drop-in Spring Boot starter API shapes — we use `Microsoft.Extensions.Hosting` idioms instead of mirroring Spring annotations.

## Target framework

`net10.0` as the initial single target. .NET 10 is the current LTS (released Nov 2025); adoption is straightforward. Multi-targeting `net8.0` can be added later if there is pull from users on the previous LTS. No lower floor than `net8.0` — we depend on modern `System.Text.Json` polymorphism and `IAsyncEnumerable` patterns that predate it.

## Repo layout

```
dbos-transact-csharp/
├── Dbos.Transact.sln
├── Directory.Build.props              # TFM, nullable, warnings-as-errors, analyzers
├── Directory.Packages.props           # central package management
├── .editorconfig
├── README.md / DEVELOPING.md / LICENSE
├── prek.toml
├── docs/
│   └── design.md                       # (this file)
│
├── src/
│   ├── Dbos.Transact/                  # core — dialect-agnostic
│   │   ├── Dbos.cs                         # DBOS.java — static facade + fluent builder
│   │   ├── DbosClient.cs                   # DBOSClient.java — standalone client
│   │   ├── DbosOptions.cs                  # DBOSConfig.java (Options pattern)
│   │   ├── StartWorkflowOptions.cs
│   │   ├── Constants.cs
│   │   ├── Admin/                          # AdminServer + request DTOs
│   │   ├── Conductor/
│   │   │   ├── Conductor.cs                # ClientWebSocket-backed
│   │   │   └── Protocol/                   # all *Request / *Response DTOs
│   │   ├── Context/
│   │   │   ├── DbosContext.cs
│   │   │   ├── DbosContextHolder.cs        # AsyncLocal<DbosContext> (≙ ThreadLocal)
│   │   │   ├── WorkflowInfo.cs
│   │   │   └── WorkflowOptions.cs
│   │   ├── Database/
│   │   │   ├── SystemDatabase.cs           # abstract base — shared orchestration
│   │   │   ├── ISqlDialect.cs              # dialect-specific primitives
│   │   │   └── Daos/                       # Workflow / Steps / Queues / Streams / Schedules / Notifications
│   │   ├── Exceptions/                     # Dbos*Exception types (10)
│   │   ├── Execution/
│   │   │   ├── DbosExecutor.cs
│   │   │   ├── QueueService.cs
│   │   │   ├── SchedulerService.cs
│   │   │   ├── RegisteredWorkflow.cs
│   │   │   └── DbosLifecycleListener.cs
│   │   ├── Internal/
│   │   │   ├── DbosInvocationInterceptor.cs    # Castle.DynamicProxy IInterceptor
│   │   │   ├── DbosProxyFactory.cs
│   │   │   ├── WorkflowRegistry.cs
│   │   │   ├── QueueRegistry.cs
│   │   │   ├── AppVersionComputer.cs
│   │   │   └── Validation.cs
│   │   ├── Json/
│   │   │   ├── IDbosSerializer.cs
│   │   │   ├── DbosJsonSerializer.cs           # System.Text.Json-backed
│   │   │   ├── DbosPortableSerializer.cs       # cross-runtime interop w/ py/ts/java
│   │   │   ├── Boxed.cs
│   │   │   ├── JsonWorkflowArgs.cs
│   │   │   ├── PortableWorkflowException.cs
│   │   │   └── ArgumentCoercion.cs
│   │   ├── Migrations/
│   │   │   ├── MigrationManager.cs
│   │   │   ├── Sql/Postgres/*.sql              # embedded resources
│   │   │   └── Sql/Sqlite/*.sql                # embedded resources
│   │   └── Workflow/                           # PUBLIC surface
│   │       ├── WorkflowAttribute.cs            # [Workflow]     ← @Workflow
│   │       ├── StepAttribute.cs                # [Step]         ← @Step
│   │       ├── ScheduledAttribute.cs           # [Scheduled]    ← @Scheduled
│   │       ├── Queue.cs
│   │       ├── WorkflowHandle.cs / WorkflowStatus.cs / WorkflowState.cs
│   │       ├── WorkflowEvent.cs / WorkflowStream.cs / WorkflowSchedule.cs
│   │       ├── StepInfo.cs / StepOptions.cs / Timeout.cs / ForkOptions.cs
│   │       └── Internal/
│   │           ├── StepResult.cs
│   │           ├── WorkflowHandleDbPoll.cs
│   │           └── WorkflowHandleTcs.cs        # backed by TaskCompletionSource<T>
│   │
│   ├── Dbos.Transact.Postgres/         # Npgsql-backed dialect
│   │   ├── PostgresSystemDatabase.cs       # LISTEN/NOTIFY, SKIP LOCKED, advisory locks
│   │   ├── PostgresDialect.cs
│   │   └── DbosPostgresExtensions.cs       # .UsePostgres(connectionString)
│   │
│   ├── Dbos.Transact.Sqlite/           # Microsoft.Data.Sqlite-backed dialect
│   │   ├── SqliteSystemDatabase.cs         # polling notifications, IMMEDIATE tx
│   │   ├── SqliteDialect.cs
│   │   └── DbosSqliteExtensions.cs         # .UseSqlite(connectionString)
│   │
│   ├── Dbos.Transact.Hosting/          # ← transact-spring-boot-starter/
│   │   ├── DbosHostingExtensions.cs        # services.AddDbos(cfg => …)
│   │   ├── DbosHostedService.cs            # IHostedService — lifecycle parity w/ auto-config
│   │   ├── DbosOptionsConfigurator.cs      # IConfiguration binding (≙ DBOSProperties)
│   │   └── WorkflowRegistrationExtensions.cs   # AddDbosWorkflow<TInterface, TImpl>()
│   │
│   └── Dbos.Transact.Cli/              # ← transact-cli/
│       ├── Program.cs                      # System.CommandLine (≙ picocli)
│       ├── Commands/                       # Migrate / Reset / Workflow / Postgres
│       └── DatabaseOptions.cs
│
└── test/
    ├── Dbos.Transact.Tests/
    │   ├── Fixtures/                       # PostgresFixture (Testcontainers) + SqliteFixture (file/memory)
    │   ├── Admin/ Conductor/ Config/ Database/ Execution/
    │   ├── Invocation/ Json/ Migrations/ Notifications/ Queue/
    │   └── TestServices/                   # BearService, HawkService, etc.
    ├── Dbos.Transact.Hosting.Tests/
    └── Dbos.Transact.Cli.Tests/
```

## Module mapping

| Java module | C# project | Notes |
|---|---|---|
| `transact/` | `Dbos.Transact` | Core library. Dialect-agnostic. No PG or SQLite driver dependencies. |
| — | `Dbos.Transact.Postgres` | New split. Npgsql-backed `ISqlDialect` + `SystemDatabase` implementation. |
| — | `Dbos.Transact.Sqlite` | New split. Microsoft.Data.Sqlite-backed implementation. |
| `transact-spring-boot-starter/` | `Dbos.Transact.Hosting` | `Microsoft.Extensions.Hosting` equivalents. Not Spring-shaped. |
| `transact-cli/` | `Dbos.Transact.Cli` | `System.CommandLine` in place of picocli. |

## Key design decisions

### Interception — Castle.DynamicProxy for v1

Java uses `java.lang.reflect.Proxy` (interface-only, runtime) to wrap user services so `@Workflow`/`@Step`-annotated method calls get routed through `DBOSInvocationHandler`. The direct C# analog is `System.Reflection.DispatchProxy` (built-in, interface-only), but its async return-type handling is awkward.

**Chosen**: `Castle.DynamicProxy` via the `Castle.Core` NuGet. Mature, widely used, composes with any DI container, handles async return types cleanly, and maps closely to the Java semantics. Runtime-registration model matches what the Java version does.

**Deferred**: source-generator-based dispatch (AOT-safe, compile-time). Reconsider for v2 once the shape is stable. Worth the investment only once the interception surface has settled.

### Serialization — System.Text.Json

Built-in, first-class polymorphism via `[JsonPolymorphic]`/`[JsonDerivedType]`, faster than Newtonsoft, no extra dependency. The Java version uses Jackson with custom `JsonTypeInfo` for workflow-args / error payloads; STJ covers the same ground.

The **portable serializer** (`DbosPortableSerializer`) is the delicate piece — it needs to round-trip with the Python, TypeScript, and Java runtimes against the shared system tables. Interop golden-file tests will be authored using fixtures emitted by the other runtimes.

### Data access — Npgsql + Microsoft.Data.Sqlite, no ORM

EF Core is not used for DBOS internals. Reasons:

1. **DBOS owns the schema.** System tables are fixed and owned by the library; EF Migrations would fight or entangle user migrations.
2. **Hot path.** Every step invocation writes a checkpoint. Change tracking, entity materialization, and LINQ translation are pure overhead.
3. **Postgres-specific features don't LINQ well.** `LISTEN/NOTIFY`, advisory locks, `SELECT … FOR UPDATE SKIP LOCKED`, `jsonb` — all require raw SQL.
4. **Dependency weight on consumers.** DBOS ships inside user apps; forcing a transitive EF Core dependency is a large ask. Npgsql and Microsoft.Data.Sqlite are light and unopinionated.

**Dapper** sits on top of the raw drivers for DAO ergonomics — removes result-mapping boilerplate without bringing change tracking or a model builder. User-facing workflows remain free to use EF Core, Dapper, or raw SQL; this constraint only applies to DBOS's own system-table access.

### Dialect abstraction — Postgres + SQLite as peers

Both dialects are first-class. Pattern borrowed from Python DBOS (`_sys_db.py` base + `_sys_db_postgres.py` + `_sys_db_sqlite.py` subclasses), where the base class contains the bulk of the shared SQL and each dialect is a small override.

C# shape:

- `SystemDatabase` (abstract) — shared orchestration and dialect-portable SQL.
- `ISqlDialect` — primitives that vary: dequeue query, notify/listen, JSON column type, advisory-lock semantics, `now()` expression.
- `PostgresSystemDatabase : SystemDatabase` — uses `LISTEN/NOTIFY`, `FOR UPDATE SKIP LOCKED`, `pg_try_advisory_lock`.
- `SqliteSystemDatabase : SystemDatabase` — uses polling notifications, `BEGIN IMMEDIATE` transactions.

Separate NuGet packages (`Dbos.Transact.Postgres`, `Dbos.Transact.Sqlite`) so consumers pull only the driver they use. Registered via extension methods: `services.AddDbos().UsePostgres(cs)` or `.UseSqlite(cs)`.

### SQLite is a first-class production target

SQLite is explicitly supported for small-project production deployments, not framed as test-only. Rationale and practical envelope:

- **Fine for**: single-host deployments, in-process or multi-process on the same host, workflow-step throughput in the low thousands/sec, modest write contention.
- **Not suitable for**: workers distributed across multiple hosts (file-based storage is single-host only), sustained high-concurrency queue dispatch that benefits from `SKIP LOCKED` parallelism, sub-50ms notification-latency requirements.

The in-process, multi-worker case on SQLite is particularly good: WAL mode yields unblocked concurrent reads, writes serialize through an in-memory mutex (not OS file locks), and notification delivery can be instant (see below). For small projects this configuration has no meaningful disadvantage versus Postgres.

**Operational guidance**: document Litestream replication alongside the SQLite dialect — continuous S3 replication gives small-project deployments a backup story comparable to managed Postgres.

### Postgres-feature fallbacks on SQLite

| Postgres feature | Used for | SQLite fallback |
|---|---|---|
| `LISTEN`/`NOTIFY` | Workflow notifications, events, queue wake-up | Polling loop on a notification table. Poll interval is configurable via `DbosOptions.NotificationPollInterval` (default 200ms). |
| `FOR UPDATE SKIP LOCKED` | Queue dispatch across workers | `BEGIN IMMEDIATE` + claim-in-single-transaction. Serializes workers; fine for modest throughput. |
| Advisory locks (`pg_try_advisory_lock`) | Scheduler leadership, singleton workflows | Same `BEGIN IMMEDIATE` pattern, or a small application-level locks table. |
| `jsonb` columns | Args, outputs, metadata | `TEXT` with JSON content. Loses server-side indexing/operators; DBOS does not rely on them on hot paths. |

Plus smaller differences handled inside the SQLite dialect:

- Strip Postgres-only connect-string args (`application_name`, `connect_timeout`, etc.).
- Split multi-statement migration SQL on `;` (SQLite executes one statement at a time).
- Store timestamps as ISO-8601 text or unix-microsecond integers (SQLite has no `TIMESTAMPTZ`).
- `INTEGER PRIMARY KEY AUTOINCREMENT` in place of `GENERATED AS IDENTITY`.

### In-process notification optimization

When only one `DbosExecutor` is registered (single-process deployment), polling for notifications is unnecessary. A hybrid design:

- **Primary**: in-memory `Channel<NotificationEvent>` that writers publish to after commit. Near-zero delivery latency.
- **Fallback**: polling loop against the notification table. Covers multi-process deployments and acts as a safety net if an in-process publish is ever missed.

Configured via `UseInProcessNotifications`, defaulting to `true` when a single executor is registered. Both code paths live side-by-side; switching is a runtime configuration concern, not a compile-time one.

This optimization gives SQLite-backed single-process deployments effectively-instant notification delivery — the feature that otherwise most visibly differentiates it from Postgres.

### Hosting integration — Microsoft.Extensions.Hosting idioms

Package named `Dbos.Transact.Hosting` (matches `Microsoft.Extensions.Hosting` convention) rather than `.DependencyInjection` or `.AspNetCore`. The integration is host-agnostic: works in console apps, ASP.NET Core, and Worker Services.

Public entry points:

- `services.AddDbos(cfg => …)` — registers options and core services.
- `services.AddDbosWorkflow<TInterface, TImpl>()` — registers a workflow implementation with proxy interception.
- `DbosHostedService : IHostedService` — starts/stops the executor, scheduler, queue workers, and admin server per host lifecycle.
- `DbosOptionsConfigurator` — binds `IConfiguration` sections (parallel to Java's `DBOSProperties`).

No Spring-style auto-configuration is required: .NET's DI + options pattern covers the same ground more directly.

### CLI — System.CommandLine

`System.CommandLine` replaces picocli. Same subcommand surface: `migrate`, `reset`, `workflow`, `postgres`. Ships as a standalone console app (`Dbos.Transact.Cli`) and is publishable as a dotnet tool (`dotnet tool install -g dbos-cli`).

## Naming conventions

- **Namespaces**: `Dbos.Transact` (root), `Dbos.Transact.Postgres`, `Dbos.Transact.Sqlite`, `Dbos.Transact.Hosting`, `Dbos.Transact.Cli`.
- **Assembly/NuGet IDs**: match namespaces.
- **Attributes**: drop the `@` prefix and use PascalCase; `[Workflow]`, `[Step]`, `[Scheduled]`.
- **Exceptions**: `Dbos*Exception` (mirrors Java's `DBOS*Exception`).
- **DAOs**: `{Entity}Dao` (PascalCase `Dao`, not `DAO`, for .NET convention).
- **Async methods**: `Async` suffix on every awaitable.

## Testing strategy

- **xUnit** + **Testcontainers.NET** for Postgres integration tests.
- **Microsoft.Data.Sqlite** with file-backed temp databases (or shared-cache `:memory:`) for SQLite integration tests — no container required, inner-loop friendly.
- Port the Java test suite structure closely: same service fixtures (`BearService`, `HawkService`, etc.), same invocation / recovery / scale / chaos / queue scenarios.
- **Interop golden tests** for the portable serializer: fixtures emitted by the Python / TypeScript / Java runtimes, asserted round-tripping in the C# implementation.
- Run the same test suite against both dialects where semantics permit, using a fixture parameterization. Tests that exercise Postgres-specific features (concurrent `SKIP LOCKED` dispatch, cross-process notification latency) are PG-only.

## Open questions

1. **DAO layout**: separate DAO classes per entity (Java style) vs. inlined on `SystemDatabase` (Python style)? Lean toward separate classes for navigability, but audit the Java SQL first to check how much is truly dialect-portable — if the divergence is high, two dialect-specific DAO hierarchies may be cleaner than one abstract + two overrides.
2. **Attribute vs. interface discovery**: workflow registration via attribute scanning (`[Workflow]` on methods) vs. explicit `AddDbosWorkflow<TInterface, TImpl>()` registrations. Java uses annotations + a registrar; .NET DI prefers explicit registration. Likely: support both, with explicit registration as the documented path and attribute-based scan as a convenience in `Dbos.Transact.Hosting`.
3. **Source generator migration path**: at what point do we move interception from Castle.DynamicProxy to a source generator for AOT support? Not v1, but worth designing the interception contract so the transition is a swap, not a rewrite.

## References

- [dbos-transact-java](https://github.com/dbos-inc/dbos-transact-java) — primary reference implementation.
- [dbos-transact-py](https://github.com/dbos-inc/dbos-transact-py) — reference for the SQLite dialect pattern (`_sys_db_sqlite.py`).
- [dbos-transact-ts](https://github.com/dbos-inc/dbos-transact-ts) — reference for the public API shape.
