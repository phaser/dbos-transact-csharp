---
title: "Scheduler Leadership and Cron Library"
type: concept
tags: [scheduler, executor, cron, leadership, port-decision, foundational]
created: 2026-04-27
updated: 2026-04-27
sources: ["raw/design.md"]
confidence: high
---

## Definition

The C# port's `SchedulerService` (DBOS-24) drives durable cron-style workflow firing. It deviates from `dbos-transact-java` in two deliberate ways: (1) it uses **Cronos** as the cron library instead of `cron-utils`, and (2) it adds an explicit **leader-lock pattern** for cross-process election, where Java relies solely on deterministic workflow IDs.

## How It Works

### Cron parsing

- **Library**: [Cronos](https://github.com/HangfireIO/Cronos) (NuGet `Cronos`).
- **Format**: 5-field (no seconds) and 6-field (with seconds) expressions are both supported. `SchedulerService.ParseCron` tries 6-field first (`CronFormat.IncludeSeconds`) and falls back to 5-field, mirroring how Java's `cron-utils` `CronType.SPRING53` accepts either form.
- **Next-fire**: `CronExpression.GetNextOccurrence(DateTimeOffset, TimeZoneInfo)` returns the next instant strictly after `now`. The runner loop computes `next = nextOccurrence(now, schedule.cronTimezone ?? Local)`, sleeps `next - now + jitter`, fires, then loops.
- **Why not Quartz.NET**: Quartz brings its own thread pool and persistence model — too heavy for an embedded scheduler driver. Why not NCrontab: 5-field only, no seconds-precision, and unmaintained vs. Cronos.

### Leadership

- **Postgres**: `pg_try_advisory_lock(hashtext(@key))` on a session-scoped connection. The connection is owned by the lock holder and released by `pg_advisory_unlock` on disposal (closing the connection also frees the lock — defensive). Multiple executors against the same DB elect a single firing leader; non-leaders sleep on `LeaderRetryInterval` and re-attempt.
- **SQLite**: always grants the lock — single-host SQLite deployments do not need cross-process election (the file lock already serialises writes, and a SQLite-backed DBOS instance is one process by definition). The disposable returned is a no-op.
- **Why this is a deliberate port improvement over Java**: Java's `SchedulerService` has *no* leader election. Every executor's poll loop fires every active schedule independently; deduplication relies on the deterministic workflow ID `sched-{name}-{instant}` colliding on the `workflow_status.workflow_uuid` PK. That's correct but wasteful — N executors each pay the cost of computing next-fire, opening connections, and racing on insert. The C# port adds advisory-lock leadership so only one executor runs the polling loop at a time. See `docs/raw/design.md` §198 ("Postgres-specific features … Scheduler leadership, singleton workflows").

### Two schedule sources

- **DB-stored** (`workflow_schedules` table): created via `db.CreateScheduleAsync(...)` or `db.ApplySchedulesAsync(...)`. Polled every `pollingInterval` (default 60s) and reconciled with in-memory runners. Inactive schedules cancel their runner. `last_fired_at` advances on each fire — survives executor restart.
- **Annotated** (`[Scheduled(...)]` attribute on a registered workflow method): discovered by reflecting over `executor.GetRegisteredWorkflows()` on each poll tick. No DB persistence in v1 (Java persists per-annotated-schedule "lastTime" via `event_dispatch_kv` to enable `automaticBackfill`; the C# port adds the `ExternalState` infrastructure but does not yet wire `automaticBackfill` for annotated schedules).

### Jitter

Each pre-fire delay is extended by up to `min(10%, 10s)` of random jitter to avoid thundering-herd when many executors pick the same minute. Mirrors Java's `SchedulerService.scheduleTask`.

## Key Parameters

- **`pollingInterval`** — how often to reconcile the runner map with DB schedules. Default 60s.
- **`LeaderLockKey`** — `dbos-scheduler-leader`. One scheduler holds it cluster-wide (PG) or always-acquired (SQLite).
- **`LeaderRetryInterval`** — how often a non-leader re-attempts. Default 5s.
- **`MaxJitter`** — 10s ceiling. Below 10s base delay, scaled by 10% rule.
- **Cron timezone** — DB-stored schedules carry a `cron_timezone` column; absent column ⇒ runner uses `TimeZoneInfo.Local`. Annotated workflows always use `TimeZoneInfo.Local` (no per-attribute timezone in v1).

## When To Use

Whenever a workflow needs to fire on a calendar-style schedule. For one-shot delayed workflows use the executor's `Delay` option instead — the scheduler is for recurring fires.

## Risks & Pitfalls

- **No `automaticBackfill` for annotated schedules in v1**. The `ExternalState` DAO is in place (added in DBOS-24) but `SchedulerService` doesn't yet persist last-fire for annotated schedules. After a long downtime the missed instants are lost. DB-stored schedules with `automatic_backfill=true` are also not yet backfilled — `BackfillScheduleAsync` is a follow-up.
- **Single-leader bottleneck on Postgres**. If the leader hangs, all firing stops until the polling-loop's exception kills it (releases the lock through connection close) and another executor's retry-loop picks it up. SQLite has no such risk because there's only one host.
- **Cron parse fallback is silent**. A malformed expression raises during runner creation, is caught, and the schedule is skipped. There is currently no error surface for the user — only debug logs. Consider failing loudly when adopting.
- **`pg_try_advisory_lock` key collisions**. `hashtext` is a 32-bit hash; with one fixed key (`dbos-scheduler-leader`) collisions don't matter, but if future code adds per-schedule advisory locks via the same primitive, two distinct keys with the same hash would conflict. Prefer two-arg `pg_try_advisory_lock(class, obj)` with a stable `class` if extending.
- **Java fidelity drift**. Java's `SchedulerService` is leaderless and fires every instant. The C# port deliberately diverges. Cross-runtime tests (DBOS-30) need to allow for the firing-once-vs-N-times behaviour difference.

## Related Concepts

- [[concepts/queue-dequeue-flow]] — scheduler-fired workflows are typically queued via `Constants.DBOS_INTERNAL_QUEUE`, then dequeued by `QueueService`.
- [[concepts/dialect-abstraction]] — the leader-lock primitive is the only scheduler-related dialect-specific surface.
- [[concepts/postgres-feature-fallbacks]] — advisory locks listed there as the PG primitive backing scheduler leadership.

## Related Entities

- [[entities/dbos-transact-java]] — the upstream we deviate from (no leader election in upstream's scheduler).

## Sources

- `raw/design.md` §Key design decisions → "Postgres-specific features" (§198) lists `pg_try_advisory_lock` for scheduler leadership.
- DBOS-24 implementation (PR opened against #29).
- Upstream Java reference: `dev.dbos.transact.execution.SchedulerService`.
