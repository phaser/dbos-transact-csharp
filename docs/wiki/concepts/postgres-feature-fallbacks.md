---
title: "Postgres-Feature Fallbacks on SQLite"
type: concept
tags: [dialect-postgres, dialect-sqlite, queues, notifications, scheduling]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
confidence: high
---

## Definition

The specific mappings by which each Postgres-only feature DBOS relies on is emulated on SQLite inside `SqliteSystemDatabase`. The goal is full feature parity at the public API level; the cost is paid inside the dialect implementation.

## How It Works

| Postgres feature | Used for | SQLite fallback |
|---|---|---|
| `LISTEN` / `NOTIFY` | Workflow notifications, events, queue wake-up | Polling loop on a notification table. Interval configurable via `DbosOptions.NotificationPollInterval` (default 200ms). |
| `FOR UPDATE SKIP LOCKED` | Queue dispatch across workers | `BEGIN IMMEDIATE` + claim-in-single-transaction. Serializes workers; fine for modest throughput. |
| Advisory locks (`pg_try_advisory_lock`) | Scheduler leadership, singleton workflows | Same `BEGIN IMMEDIATE` pattern, or a small application-level locks table. |
| `jsonb` columns | Args, outputs, metadata | `TEXT` with JSON content. Loses server-side indexing/operators — DBOS does not rely on them on hot paths. |

Smaller differences also handled inside the SQLite dialect:

- **Connect-string sanitization** — strip Postgres-only args (`application_name`, `connect_timeout`, …) before handing to Microsoft.Data.Sqlite.
- **Statement splitting** — SQLite executes one statement at a time; the `MigrationManager` splits multi-statement migration SQL on `;` for the SQLite dialect.
- **Timestamps** — no `TIMESTAMPTZ`; stored as ISO-8601 text or unix-microsecond integers.
- **Auto-increment PK** — `INTEGER PRIMARY KEY AUTOINCREMENT` replaces `GENERATED AS IDENTITY`.

## Key Parameters

- `DbosOptions.NotificationPollInterval` — the latency/load trade-off knob for the poll-based fallback (default 200ms). Can be reduced at the cost of extra read traffic.
- **Queue throughput ceiling under serialized claim** — workload-dependent but measurable. The serialize-claim pattern is fine into low-thousands steps/sec on modern hardware; beyond that, Postgres is the answer.
- **Single-process vs. multi-process SQLite** — single-process unlocks the in-process notification fast path; see [[concepts/in-process-notification-optimization]].

## When To Use

Reviewers should consult this table any time they touch queue dispatch, notifications, scheduler leadership, or JSON-typed fields, and verify both dialects explicitly. Dialect-portable changes go through `SystemDatabase`; dialect-specific changes go through the dialect subclass and, where needed, a new `ISqlDialect` primitive.

## Risks & Pitfalls

- **Poll-interval trade-off.** Too short → wasted reads. Too long → user-visible latency on notifications.
- **Queue serialization at high concurrency.** A SQLite workload that suddenly scales its queue fan-out will bottleneck before the database is "full" in any traditional sense.
- **Lock-table drift.** If advisory-lock parity is implemented via an application-level locks table, rows must be cleaned up on leader loss — a leaked row prevents leadership failover.
- **Timezone handling.** ISO-8601 text without offset is ambiguous across locales; choose a single canonical format in the dialect and enforce it.
- **Base class leaking Postgres-isms.** Any raw SQL in `SystemDatabase` that implicitly uses `jsonb` / `NOW()` / `RETURNING` must be gated through `ISqlDialect`, or SQLite breaks silently.

## Related Concepts

- [[concepts/dialect-abstraction]]
- [[concepts/sqlite-production-target]]
- [[concepts/in-process-notification-optimization]]

## Related Entities

- [[entities/dbos-transact-postgres]]
- [[entities/dbos-transact-sqlite]]

## Sources

- `raw/design.md` — §Postgres-feature fallbacks on SQLite
