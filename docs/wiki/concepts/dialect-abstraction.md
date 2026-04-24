---
title: "Dialect Abstraction"
type: concept
tags: [core, persistence, dialect-postgres, dialect-sqlite]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
confidence: high
---

## Definition

Postgres and SQLite are both first-class dialects. A shared `SystemDatabase` abstract class owns the dialect-portable orchestration and SQL; an `ISqlDialect` interface exposes the small set of primitives that vary between engines; two subclasses — `PostgresSystemDatabase` and `SqliteSystemDatabase` — plug in the dialect-specific behaviors. Consumers pull exactly one dialect package and register it via an extension method.

## How It Works

- `SystemDatabase` (abstract, in `src/Dbos.Transact/Database/`) contains the bulk of the SQL that is portable across both engines — schema CRUD, workflow/step lookups, queue enqueue, status transitions.
- `ISqlDialect` captures the primitives that do vary: dequeue query shape, notify/listen mechanism, JSON column type, advisory-lock semantics, `now()` expression, and any connection-string or statement-splitting behaviors the engine needs.
- `PostgresSystemDatabase : SystemDatabase` in [[entities/dbos-transact-postgres]] uses `LISTEN`/`NOTIFY`, `FOR UPDATE SKIP LOCKED`, and `pg_try_advisory_lock`.
- `SqliteSystemDatabase : SystemDatabase` in [[entities/dbos-transact-sqlite]] uses a polling notification loop and `BEGIN IMMEDIATE` transactions — see [[concepts/postgres-feature-fallbacks]] for the full mapping.
- Consumer wiring: `services.AddDbos().UsePostgres(connectionString)` or `.UseSqlite(connectionString)`. The core package (`Dbos.Transact`) carries no driver dependency.
- Pattern is borrowed from the Python runtime's `_sys_db.py` + `_sys_db_postgres.py` + `_sys_db_sqlite.py` layout; see [[entities/dbos-transact-py]].

## Key Parameters

- **Primitive set** — the surface of `ISqlDialect`. Narrow enough that each dialect isn't reimplementing orchestration; broad enough that the base class doesn't smuggle Postgres-isms in string templates.
- **Packaging split** — separate NuGets per dialect (`Dbos.Transact.Postgres`, `Dbos.Transact.Sqlite`) so users pull only the driver they need. Core has no Npgsql / Microsoft.Data.Sqlite dependency.
- **DAO layout** — whether DAOs are per-entity classes (Java style) or methods on `SystemDatabase` (Python style) is an open question in `design.md`; the answer depends on how much SQL is truly dialect-portable.

## When To Use

Any code that reads or writes DBOS system tables belongs here. User workflow SQL (the user's own queries against their own tables) is not constrained — they can use EF Core, Dapper, or raw SQL as they see fit.

## Risks & Pitfalls

- **Primitive-set drift.** A new Postgres feature used inside the base class without an `ISqlDialect` hook silently breaks SQLite.
- **Parallel DAO hierarchies.** If the actual divergence between dialects turns out to be high, a single abstract + two overrides is worse than two complete dialect-specific DAO hierarchies. See open question #1 in `design.md`.
- **Dapper mapping assumptions.** Column types differ (e.g. SQLite has no `TIMESTAMPTZ`, no `jsonb`) — result mapping must go through the dialect's expected representation, not hard-code Postgres shapes.
- **Connection-string arguments.** SQLite must strip Postgres-only keys (`application_name`, `connect_timeout`, etc.); this lives inside the SQLite dialect.

## Related Concepts

- [[concepts/postgres-feature-fallbacks]]
- [[concepts/no-orm-constraint]]
- [[concepts/sqlite-production-target]]
- [[concepts/in-process-notification-optimization]]

## Related Entities

- [[entities/dbos-transact-postgres]]
- [[entities/dbos-transact-sqlite]]
- [[entities/dbos-transact-py]]

## Sources

- `raw/design.md` — §Key design decisions → Dialect abstraction; §Repo layout (`Database/` folder); §Open questions #1 (DAO layout)
