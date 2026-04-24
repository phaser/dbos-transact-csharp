---
title: "Design — dbos-transact-csharp"
type: summary
tags: [design, foundational]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
confidence: high
---

## Key Points

- **Scope.** Port DBOS Transact — a durable-workflow library built on a relational database — from Java to C#/.NET. Preserve feature parity with the Java runtime where sensible; adopt .NET-idiomatic shapes where they differ. See [[concepts/durable-workflow]].
- **Core capabilities.** Durable workflows with auto-resume, durable queues (no external broker), scheduled execution (cron + long sleeps), notifications/events with exactly-once delivery, async workflow handles, admin HTTP endpoints, and conductor WebSocket protocol parity.
- **Non-goals for v1.** AOT / native-image support (interception is runtime-IL based — revisit for v2). Cross-runtime client interop for exotic types (first pass supports the subset the other runtimes handle). Drop-in Spring Boot starter shape — use `Microsoft.Extensions.Hosting` idioms instead.
- **Target framework.** `net10.0` as initial single target (current LTS, released Nov 2025). Multi-targeting `net8.0` is available if demand appears; no lower floor — modern STJ polymorphism and `IAsyncEnumerable` are assumed.
- **Repo split.** Core ([[entities/dbos-transact]]) is dialect-agnostic. Dialect drivers live in separate NuGets: [[entities/dbos-transact-postgres]] and [[entities/dbos-transact-sqlite]]. Hosting integration is [[entities/dbos-transact-hosting]]; CLI is [[entities/dbos-transact-cli]].
- **Method interception.** Uses `Castle.DynamicProxy` (see [[entities/castle-dynamicproxy]]) — chosen over `System.Reflection.DispatchProxy` for async handling. Source-generator-based dispatch is deferred to v2. See [[concepts/method-interception]].
- **Serialization.** `System.Text.Json` replaces Jackson. `DbosPortableSerializer` is the cross-runtime JSON interop surface; interop golden tests are emitted by Python / TypeScript / Java. See [[concepts/portable-serializer]].
- **Persistence: no ORM for internals.** Internals use Npgsql / Microsoft.Data.Sqlite with Dapper layered on top. EF Core would fight user migrations, add hot-path overhead, can't express PG-specific primitives, and would be a heavy transitive dependency. User workflows remain free to use EF Core. See [[concepts/no-orm-constraint]].
- **Dialect abstraction.** `SystemDatabase` abstract base + `ISqlDialect` primitives + two dialect subclasses. Pattern mirrors the Python runtime's `_sys_db*.py` layout. See [[concepts/dialect-abstraction]].
- **SQLite as production target.** First-class, not test-only. Operational envelope: single-host, low-thousands steps/sec, [[entities/litestream]] for backup. See [[concepts/sqlite-production-target]].
- **Postgres feature fallbacks.** Each PG-only primitive has a documented SQLite emulation: `LISTEN`/`NOTIFY` → polling, `SKIP LOCKED` → `BEGIN IMMEDIATE`, advisory locks → locks table, `jsonb` → `TEXT`. See [[concepts/postgres-feature-fallbacks]].
- **In-process notification optimization.** Single-executor deployments route notifications through an in-memory `Channel<T>` with the polling loop kept as a safety-net fallback. Default `true` when one executor is registered. Particularly valuable for single-process SQLite deployments. See [[concepts/in-process-notification-optimization]].
- **Hosting shape.** `services.AddDbos(cfg => …)` + `AddDbosWorkflow<TInterface, TImpl>()` + `DbosHostedService`. Not Spring-shaped. See [[entities/dbos-transact-hosting]].
- **CLI.** `System.CommandLine` replaces picocli; same subcommand surface as the Java CLI. Ships as standalone console app + dotnet tool. See [[entities/dbos-transact-cli]].
- **Testing strategy.** xUnit + Testcontainers.NET for Postgres; Microsoft.Data.Sqlite (file-temp or `:memory:`) for SQLite. Ports the Java test suite structure, including fixture service names (`BearService`, `HawkService`). Parameterized fixtures run the same test against both dialects where semantics permit; PG-only tests for `SKIP LOCKED` and cross-process notification latency.
- **Naming conventions.** Root namespace `Dbos.Transact` (+ per-dialect/hosting/cli suffixes). Attributes `[Workflow]` / `[Step]` / `[Scheduled]` (PascalCase, no `@`). Exceptions `Dbos*Exception`. DAOs `{Entity}Dao` (not `DAO`). `Async` suffix on every awaitable.
- **Open questions.** (1) DAO layout — per-entity classes (Java) vs. methods on `SystemDatabase` (Python); depends on how portable the SQL actually is. (2) Attribute-based workflow discovery vs. explicit `AddDbosWorkflow<>()` — likely both, explicit as documented path. (3) When to migrate interception to a source generator for AOT.

## Relevant Concepts

- [[concepts/durable-workflow]]
- [[concepts/method-interception]]
- [[concepts/portable-serializer]]
- [[concepts/dialect-abstraction]]
- [[concepts/sqlite-production-target]]
- [[concepts/postgres-feature-fallbacks]]
- [[concepts/in-process-notification-optimization]]
- [[concepts/no-orm-constraint]]

## Relevant Entities

- [[entities/dbos-transact]]
- [[entities/dbos-transact-postgres]]
- [[entities/dbos-transact-sqlite]]
- [[entities/dbos-transact-hosting]]
- [[entities/dbos-transact-cli]]
- [[entities/dbos-transact-java]]
- [[entities/dbos-transact-py]]
- [[entities/dbos-transact-ts]]
- [[entities/castle-dynamicproxy]]
- [[entities/litestream]]

## Source Metadata

- **Type of source:** internal design document (markdown).
- **Author:** Cristian Bidea (project owner).
- **Identifier:** `raw/design.md`.
- **Status at ingest:** authoritative design for v1 of the C# port; captures both decided items and explicit open questions.
