---
title: "SQLite as a First-Class Production Target"
type: concept
tags: [dialect-sqlite, persistence, deployment]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
confidence: high
---

## Definition

SQLite is supported as a production deployment target for small single-host projects, not merely as a test or development database. The design explicitly calls out its operational envelope and pairs it with Litestream-based backup guidance so that a small-project deployment has a comparable durability story to managed Postgres.

## How It Works

- `Dbos.Transact.Sqlite` ([[entities/dbos-transact-sqlite]]) provides a full `SqliteSystemDatabase : SystemDatabase` implementation — not a stubbed or limited one. See [[concepts/dialect-abstraction]].
- WAL (write-ahead log) mode is assumed: concurrent reads proceed without blocking, writes serialize through an in-memory mutex rather than OS file locks.
- For single-process deployments, [[concepts/in-process-notification-optimization]] collapses the poll-interval latency, making notification delivery effectively instant.
- Postgres-only features are emulated rather than elided — see [[concepts/postgres-feature-fallbacks]] for the specific mappings.
- Backup story: [[entities/litestream]] streams WAL frames to object storage continuously, giving point-in-time recovery comparable to managed Postgres.

## Key Parameters

- **Single-host constraint.** File-based storage means all workers must run on the same host.
- **Throughput envelope.** "Low thousands of workflow-steps/sec" is the stated comfort zone; modest write contention.
- **Notification latency tolerance.** Without the in-process optimization, notifications are bounded by `DbosOptions.NotificationPollInterval` (default 200ms). With the optimization active, near-zero.
- **Backup mechanism.** Litestream-based replication is the documented path; operators should be pointed at it rather than left to reinvent.

## When To Use

- Single-host small-project deployments (hobby, internal tools, small SaaS, edge/embedded).
- In-process, multi-worker configurations on one host — the sweet spot.
- Local development and CI — identical dialect to production means fewer "works in dev, breaks in prod" surprises.

## Risks & Pitfalls

- **Multi-host workers are unsupported.** The moment you need workers across hosts, switch to Postgres. This is a hard constraint, not a tuning knob.
- **High-concurrency queue dispatch.** Without `SKIP LOCKED` parallelism, queue claim serializes workers. Fine for modest throughput; a bottleneck at high concurrency.
- **Sub-50ms notification latency.** Only achievable with the in-process path. Multi-process SQLite deployments pay the poll interval.
- **No server-side JSON operators.** Stored as `TEXT` — DBOS does not rely on them on hot paths, but operator-side ad-hoc queries are more awkward.
- **Timestamp handling.** SQLite has no `TIMESTAMPTZ`; the dialect stores ISO-8601 text or unix-microsecond integers. Misuse at the edges (raw SQL ad-hoc queries) can silently drop timezone information.

## Related Concepts

- [[concepts/dialect-abstraction]]
- [[concepts/postgres-feature-fallbacks]]
- [[concepts/in-process-notification-optimization]]

## Related Entities

- [[entities/dbos-transact-sqlite]]
- [[entities/litestream]]

## Sources

- `raw/design.md` — §Key design decisions → SQLite is a first-class production target; §Postgres-feature fallbacks on SQLite; §Key design decisions → In-process notification optimization
