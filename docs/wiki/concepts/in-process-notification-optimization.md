---
title: "In-Process Notification Optimization"
type: concept
tags: [notifications, performance, dialect-sqlite, dialect-postgres]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
confidence: high
---

## Definition

When exactly one `DbosExecutor` is registered (single-process deployment), workflow notifications, events, and queue wake-ups are delivered through an in-memory `Channel<NotificationEvent>` instead of relying on database polling or `LISTEN`/`NOTIFY`. A polling fallback continues to run as a safety net. The design gives SQLite-backed single-process deployments effectively-instant notification delivery — the feature that otherwise most visibly differentiates Postgres.

## How It Works

- **Primary path.** After a notification is committed to the system-table, the writer posts the event to an in-memory `Channel<NotificationEvent>`. Consumers drain the channel. Delivery latency is near-zero (memory handoff, no I/O).
- **Fallback path.** The same polling loop used in multi-process deployments continues to run on an interval. It covers two cases: a missed post-commit channel write (defensive), and a second process that joins at runtime. On Postgres this falls back to `LISTEN`/`NOTIFY`; on SQLite it falls back to the polling loop described in [[concepts/postgres-feature-fallbacks]].
- **Toggle.** `DbosOptions.UseInProcessNotifications`; default is `true` when a single executor is registered, otherwise `false`.
- Both paths coexist in the runtime — switching is a configuration concern, not a compile-time branch. Idempotent consumer logic ensures a double-delivery (channel + fallback poll picking up the same row) is harmless.

## Key Parameters

- `UseInProcessNotifications` — explicit toggle. Force-off for diagnostics; force-on only when deployment is verifiably single-process.
- **Channel bounded vs. unbounded** — bounded channels push back pressure to producers if consumers stall; unbounded channels keep producers fast but risk memory growth. The design doc doesn't yet pin this — check `DbosOptions` when implementing.
- **Idempotency invariant on the consumer side.** The fallback loop must not double-process events already handled through the channel.

## When To Use

- Single-process SQLite deployments — the marquee use case; turns SQLite's polling-only notification story into a near-instant one.
- Single-process Postgres deployments — still a win; avoids the extra round-trip through `LISTEN`/`NOTIFY`.
- Development and test loops where latency matters for ergonomics.

## Risks & Pitfalls

- **Multi-process misconfiguration.** If two processes both run executors and both have `UseInProcessNotifications=true` with the dialect's fallback disabled, cross-process events never arrive. The rule: the fallback poller must always run.
- **Missed post-commit publish.** If the writer crashes between `COMMIT` and the channel `Write`, the event is durable (table row exists) but the channel doesn't see it — the fallback poll is what catches it. This is why the poll can't be fully disabled.
- **Backpressure semantics.** An unbounded channel masks slow consumers; a bounded channel that fills blocks the writer. Pick one deliberately.
- **Consumer idempotency.** Fallback + channel can both deliver the same event; the consumer must treat events by unique ID, not by arrival.

## Related Concepts

- [[concepts/sqlite-production-target]]
- [[concepts/postgres-feature-fallbacks]]
- [[concepts/dialect-abstraction]]

## Sources

- `raw/design.md` — §Key design decisions → In-process notification optimization
