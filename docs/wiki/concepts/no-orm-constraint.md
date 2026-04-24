---
title: "No ORM for DBOS Internals"
type: concept
tags: [persistence, core, design-constraint]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
confidence: high
---

## Definition

`dbos-transact-csharp` does not use EF Core for its own system-table access. Internals go through the raw ADO.NET drivers — Npgsql or Microsoft.Data.Sqlite — with Dapper layered on for result-mapping ergonomics. This constraint applies only to DBOS-owned tables; user workflows are free to use EF Core, Dapper, or raw SQL against their own schema.

## How It Works

- DAOs (`Workflow`, `Steps`, `Queues`, `Streams`, `Schedules`, `Notifications`) issue parameterized SQL strings through Dapper's `Execute` / `Query` / `QueryAsync` surface.
- Each dialect binds to its own driver: Postgres via Npgsql, SQLite via Microsoft.Data.Sqlite. See [[concepts/dialect-abstraction]].
- Dapper provides type-to-row mapping without change tracking, a model builder, or LINQ translation — the ergonomic win without the overhead.
- The separation is also a packaging concern: the core `Dbos.Transact` NuGet has no driver or ORM dependency at all; dialect packages bring their own.

## Key Parameters

- **Schema-owned-by-library invariant.** System tables are fixed and migrated by `MigrationManager` using embedded SQL resources. EF Migrations would either fight user migrations or entangle them.
- **Hot-path checkpoint performance.** Every `[Step]` invocation writes a row. Change tracking, entity materialization, and LINQ translation are pure overhead on a path that matters.
- **Postgres-specific primitives.** `LISTEN`/`NOTIFY`, advisory locks, `FOR UPDATE SKIP LOCKED`, `jsonb` operators — all require raw SQL; none LINQ-translate cleanly.
- **Consumer dependency weight.** DBOS ships inside user applications; a transitive EF Core dependency is a much larger ask than Npgsql + Microsoft.Data.Sqlite + Dapper.

## When To Use

Every internal DAO and every internal system-table read/write. If a new subsystem needs persistent state, it belongs in a system table accessed via a new DAO, not via a detached `DbContext`.

## Risks & Pitfalls

- **Hand-maintained SQL.** Without a query builder, typos and schema drift show up at runtime. Migration resources and the DAO SQL must stay in lock-step; tests should cover both dialects.
- **Parameterization discipline.** Dapper is safe *when* parameters are passed through its API. Interpolated-string SQL via `ExecuteAsync($"...{userInput}...")`-style calls is an injection vector. Code reviews should catch this; analyzers can too.
- **Result-mapping edge cases.** Dapper's default mapper handles the common case; custom type handlers are needed for JSON columns, enums, and the ISO-8601 timestamps used on SQLite.
- **Two SQL variants to maintain.** Dialect-split SQL is strictly more work than one-SQL-for-all; this is the explicit trade for shipping two first-class dialects. See [[concepts/dialect-abstraction]] and open question #1 in `design.md`.

## Related Concepts

- [[concepts/dialect-abstraction]]
- [[concepts/durable-workflow]]

## Sources

- `raw/design.md` — §Key design decisions → Data access — Npgsql + Microsoft.Data.Sqlite, no ORM
