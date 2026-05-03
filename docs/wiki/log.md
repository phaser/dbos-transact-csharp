---
title: "Activity Log"
type: log
---

# Activity Log

Append-only record of all wiki changes.

## Format

Each entry follows this format:
```
### YYYY-MM-DD HH:MM — [Action Type]
- **Source/Trigger**: what initiated the action
- **Pages created**: list of new pages
- **Pages updated**: list of updated pages
- **Notes**: any contradictions flagged, decisions made
```

---

### 2026-04-08 00:00 — Setup

- **Source/Trigger**: Repository initialized
- **Pages created**: index.md, log.md, dashboard.md, analytics.md, flashcards.md
- **Pages updated**: none
- **Notes**: Empty knowledge base ready for first source ingestion

### 2026-04-24 — Ingest

- **Source/Trigger**: `ingest raw/design.md`
- **Pages created**:
  - `summaries/design.md`
  - `concepts/durable-workflow.md`
  - `concepts/method-interception.md`
  - `concepts/portable-serializer.md`
  - `concepts/dialect-abstraction.md`
  - `concepts/sqlite-production-target.md`
  - `concepts/postgres-feature-fallbacks.md`
  - `concepts/in-process-notification-optimization.md`
  - `concepts/no-orm-constraint.md`
  - `entities/dbos-transact.md`
  - `entities/dbos-transact-postgres.md`
  - `entities/dbos-transact-sqlite.md`
  - `entities/dbos-transact-hosting.md`
  - `entities/dbos-transact-cli.md`
  - `entities/dbos-transact-java.md`
  - `entities/dbos-transact-py.md`
  - `entities/dbos-transact-ts.md`
  - `entities/castle-dynamicproxy.md`
  - `entities/litestream.md`
- **Pages updated**: `index.md` (entries + statistics)
- **Notes**:
  - First source ingestion; no prior wiki content to contradict.
  - Tagging taxonomy in `docs/CLAUDE.md` is still the placeholder (`tag-1`, `tag-2`, …); pages use ad-hoc descriptive tags (`core`, `persistence`, `dialect-postgres`, `dialect-sqlite`, `interception`, `serialization`, `notifications`, `queues`, `upstream`, etc.). Revisit when the taxonomy is finalized.
  - Open questions from `design.md` (DAO layout, attribute-vs-explicit discovery, source-gen migration) are surfaced in `summaries/design.md` and the relevant concept pages but have no dedicated synthesis page yet — candidate for a synthesis once there is more than one source informing them.

### 2026-04-25 — Port Decision Capture

- **Source/Trigger**: DBOS-02 implementation (PR #32) — test failures exposed two C# record validation edge cases
- **Pages created**:
  - `concepts/csharp-record-validation.md`
- **Pages updated**: `index.md` (added entry, bumped statistics and updated date)
- **Notes**:
  - Two discoveries captured: (1) compact constructor syntax (`public TypeName { }`) is not parsed by .NET 10.0.202 SDK; (2) property initializers only fire in the primary constructor — they do not fire on `with` expressions, requiring the backing-field + `init`-accessor pattern when `with`-expression invariants must hold.
  - Also documents the `IReadOnlySet`/array equality trap in records and the CS8907 "parameter unread" pitfall when using explicit backing fields.
  - No raw source; finding is empirical, confidence: high.

### 2026-04-25 — Port Decision Capture

- **Source/Trigger**: DBOS-04/05/06 implementation (PR covering issues #4, #6, #7) — context holder and AppVersionComputer port uncovered two more C# vs Java design divergences
- **Pages created**:
  - `concepts/asynclocal-vs-threadlocal.md`
  - `concepts/appversion-signature-hashing.md`
- **Pages updated**: `index.md` (added 2 entries, bumped totals from 20→22, concepts 9→11, high-confidence 9→11)
- **Notes**:
  - `asynclocal-vs-threadlocal.md`: Java uses `ThreadLocal<DBOSContext>` (per-OS-thread); C# port uses `AsyncLocal<DbosContext?>` (flows into child tasks, child mutations don't propagate back). Documents null-initialization difference and reference-vs-value semantics hazard.
  - `appversion-signature-hashing.md`: Java hashes JVM bytecode via ASM (implementation-sensitive). C# port hashes method signatures (FQN + parameter/return types) because IL inspection is complex and the primary use-case (renamed/removed workflow functions) does not require implementation-level sensitivity. Known limitation: body-only changes are undetected until replay.

### 2026-04-25 — Port Decision Capture

- **Source/Trigger**: DBOS-09/14/15 implementation (PR #35) — conductor protocol DTO port revealed System.Text.Json polymorphic deserialization behavior and a C# reserved-keyword naming conflict
- **Pages created**:
  - `concepts/stj-polymorphic-discriminator.md`
- **Pages updated**: `index.md` (added entry, bumped totals from 22→23, concepts 11→12, high-confidence 11→12)
- **Notes**:
  - `stj-polymorphic-discriminator.md`: Documents how `[JsonPolymorphic]` + `[JsonDerivedType]` maps to Java's Jackson `@JsonTypeInfo(visible=true)` + `@JsonSubTypes`. Key gotcha: `IgnoreUnrecognizedTypeDiscriminators = true` does NOT return null for abstract base types — it still throws `NotSupportedException` because STJ falls back to instantiating the abstract base class. Also documents CA1716 (C# reserved keyword `step`) requiring `StepEntry` rename for the `ListStepsResponse` nested class.

### 2026-04-27 — Port Decision Capture

- **Source/Trigger**: DBOS-22 implementation (PR #42) — DbosExecutor integration tests uncovered two runtime bugs
- **Pages created**: none
- **Pages updated**:
  - `concepts/method-interception.md` (added concrete-vs-interface attribute-resolution pitfall)
  - `index.md` (updated date)
- **Notes**:
  - **Castle attribute resolution pitfall**: `Castle.IInvocation.Method` is the interface method; the concrete class method resolved via `target.GetType().GetMethod(...)` typically does NOT carry `[Step]`/`[Workflow]` attributes. Calling `IsDefined` on the concrete method alone silently bypasses all step checkpointing. Fix: prefer the concrete method when it has the attribute, otherwise fall back to `invocation.Method`.
  - **WorkflowState DB enum case mismatch**: The system DB stores workflow states as uppercase strings ("SUCCESS", "PENDING"); C# enum values are PascalCase ("Success"). `Enum.TryParse<WorkflowState>` is case-sensitive and silently returns false, making `ToWorkflowStatus()` yield `Status = null` on every row — causing `AwaitWorkflowResultAsync` to poll forever. Fixed by switching to `WorkflowStateExtensions.ParseDbStatus` (case-insensitive).

### 2026-04-27 — Port Decision Capture

- **Source/Trigger**: DBOS-23 implementation (PR #43) — QueueService, dequeue DAOs, and executor re-execution exposed two bugs and one serialization gap
- **Pages created**:
  - `concepts/queue-dequeue-flow.md`
- **Pages updated**: `index.md` (added entry, bumped totals from 23→24, concepts 12→13)
- **Notes**:
  - **ENQUEUED early-return missing**: Java's `executeWorkflow` returns a poll handle immediately when `queueName != null`, never running the workflow body. The C# port was missing this guard, causing queued workflows to execute immediately and bypass the queue entirely. Fix: check `opts.QueueName is not null` in `StartWorkflowAsync` before the `ShouldExecuteOnThisExecutor` check.
  - **object?[] serializer round-trip**: STJ deserializes `object[]` elements as `JsonElement` rather than their original CLR types. `MethodInfo.Invoke` then throws `ArgumentException` when the parameter expects `int` but receives `JsonElement`. Fix: `DbosJsonSerializer.Serialize(object?[])` now wraps each element in its own `TypeEnvelope`; `Deserialize` decodes per-element envelopes when outer type is `object[]`.
  - **DB locking strategy**: Postgres uses `FOR UPDATE SKIP LOCKED` (no global concurrency limit) or `FOR UPDATE NOWAIT` (with global limit) under `REPEATABLE READ`. SQLite uses `IsolationLevel.Serializable` (= `BEGIN IMMEDIATE`) to serialize access. Documented in `concepts/queue-dequeue-flow.md`.

### 2026-04-27 — Port Decision Capture

- **Source/Trigger**: DBOS-24 implementation (PR for #29) — `SchedulerService` plus prerequisites (`ExternalState` DAO, advisory-lock primitive on `SystemDatabase`, `DbosExecutor` accessors).
- **Pages created**:
  - `concepts/scheduler-leadership-and-cron.md`
- **Pages updated**: `index.md` (added entry, bumped totals from 24→25, concepts 13→14, high-confidence 12→13).
- **Notes**:
  - **Cron library**: `Cronos` (NuGet) supports both 5-field and 6-field expressions, matching Java's `cron-utils Spring53`. `SchedulerService.ParseCron` tries 6-field first then falls back to 5-field. Quartz.NET was rejected as too heavy; NCrontab as 5-field-only and unmaintained.
  - **Leader-lock pattern as port improvement**: Java's scheduler is leaderless and relies on deterministic workflow IDs (`sched-{name}-{instant}`) colliding on the workflow_status PK to dedupe N executors firing the same instant. The C# port adds advisory-lock leadership (PG: `pg_try_advisory_lock(hashtext('dbos-scheduler-leader'))` on a session-scoped connection; SQLite: always-acquired no-op since a SQLite-backed instance is single-host). Documented in design.md §198 as the intended approach.
  - **Per-instance leader scope**: One `SchedulerService` instance holds the leader lock for its entire lifetime. Lost on dispose or process crash (PG releases on connection close). Other instances' poll loop retry-acquire every `LeaderRetryInterval` (5s default).
  - **Annotated schedules in v1**: `[Scheduled]` on a registered workflow is discovered by reflection on each poll tick. `automaticBackfill` is *not* implemented for annotated schedules in v1 (the `ExternalState` infrastructure was added but is unused by the scheduler — flagged for follow-up).
  - **DB schedule context column NOT NULL**: Mirrors a Java/migration constraint. Null `Context` is stored as the JSON literal `"null"` sentinel and decoded back to `null` on read. Implemented in DBOS-24-prerequisite (PR #44 for `SchedulesDao`); reused here via `db.GetScheduleAsync`.


### 2026-04-29 — Port Decision Capture

- **Source/Trigger**: DBOS-25 implementation (PR for #9) — public facade (`Dbos`), `DbosClient`, `DbosBuilder`, dialect builder extensions (`UsePostgres`, `UseSqlite`), `Workflow/Queue` fluent setters.
- **Pages created**: —
- **Pages updated**: —
- **Notes**:
  - **MaxRecoveryAttempts default**: `WorkflowAttribute.MaxRecoveryAttempts` defaults to `-1` (i.e. "use the default"). Java's `DBOSExecutor` (line 1761) falls back to `Constants.DEFAULT_MAX_RECOVERY_ATTEMPTS = 100`. The C# port had `DbosExecutor` substituting `int.MaxValue` instead, which trips the DAO check `row.RecoveryAttempts > maxRetries + 1` because `int.MaxValue + 1` overflows to `int.MinValue`, dead-lettering every workflow on first attempt. Fixed in DBOS-25 to use `Constants.DefaultMaxRecoveryAttempts` — matches Java exactly.
  - **`Dbos` facade — registration vs. proxy semantics**: `RegisterProxy<TInterface>(target)` mirrors Java's `registerProxy`: walks `target.GetType().GetMethods(DeclaredOnly)` for `[Workflow]`, registers them, and returns a Castle.DynamicProxy interface proxy with step interception. `[Workflow]` must therefore be on the *concrete* method (Java has the same constraint). `[Step]` can sit on either the interface or the concrete method — `DbosInvocationInterceptor` already handles both. `HasWorkflowsOrSteps` was relaxed to also walk implemented interfaces so a step-only proxy with `[Step]` on an interface (the typical case) doesn't reject as "no [Workflow] or [Step] methods".
  - **Lazy executor resolution in proxies**: Created proxies must outlive the build/launch transition, so the interceptor receives a closure over `_executor` (resolved at invocation time) rather than the live executor instance. Mirrors Java's `Supplier<DBOSExecutor>` pattern.
  - **Dialect extension methods register both factory and migration runner**: `DbosBuilder.UseSystemDatabase(factory, migrate?)` — the SystemDatabase factory is dialect-agnostic but the migration runner needs to know the SQL dialect (`MigrationManager.SqlDialect.Postgres` vs `Sqlite`). Each dialect extension (`UsePostgres`, `UseSqlite`) supplies both at once. Migrations run from `Dbos.LaunchAsync` only when `options.Migrate` is true, so tests that pre-migrate the DB pass `Migrate = false` to skip a redundant pass.
  - **Top-level workflow start via proxy is deferred**: Java's `dbos.startWorkflow(supplier)` uses a thread-local `NextWorkflowId` set on the ambient `DBOSContext`; the proxy's `[Workflow]` interceptor reads it and treats the call as a top-level start. The C# `DbosContext` already has the `NextWorkflowId`/`NextTimeout`/`NextDeadline` fields (port-of-Java), but the supplier-based entry point is not yet wired — `Dbos.StartWorkflowAsync(workflowName, className, instanceName, args, ...)` looks the workflow up by name instead. Calling a `[Workflow]` method through the proxy still throws `NotSupportedException` (per `DbosExecutor.HandleInvocationAsync`); enabling that path is a follow-up.


### 2026-04-30 — Port Decision Capture

- **Source/Trigger**: DBOS-28 implementation (PR for #10) — `Dbos.Transact.Hosting` integration with `Microsoft.Extensions.Hosting`.
- **Pages created**: —
- **Pages updated**: —
- **Notes**:
  - **Bean-scanning vs explicit registration**: Java's Spring Boot starter (`DBOSWorkflowRegistrar`) auto-discovers `@Workflow`-bearing beans via `SmartInitializingSingleton` + `ApplicationContext.getBeanDefinitionNames()`. The .NET DI container has no equivalent introspection, so the C# port instead requires explicit `AddDbosWorkflow<TInterface, TImpl>()` calls. The hosted service still resolves them all before `LaunchAsync`, mirroring Spring's `SmartLifecycle` ordering: `SmartInitializingSingleton.afterSingletonsInstantiated()` (bean registration) runs before any `SmartLifecycle.start()` (DBOS launch).
  - **`DbosOptionsConfigurator` vs binding `DbosOptions` directly**: `DbosOptions` is a positional `record` with 17 parameters, of which `AppName` is required and many are nullable with non-empty validation in `init` accessors. Binding from `IConfiguration` directly would either fail when keys are absent (`AppName` required) or silently accept empty strings that then trip validation. A hand-written `DbosOptionsConfigurator` (mirroring Java's `DBOSProperties` shape) gives a clean settable-properties surface; `BuildOptions(defaultAppName)` normalizes empty strings to null and applies a fallback for `AppName`. Same pattern Java uses (`DBOSProperties` → `DBOSConfig`).
  - **Workflow factory triggers proxy registration**: `AddDbosWorkflow<TInterface, TImpl>` registers `TImpl` as a `Singleton` and `TInterface` via a singleton factory that calls `dbos.RegisterProxy<TInterface>(impl, instanceName)` and returns the proxy. The `DbosHostedService.StartAsync` resolves each registered interface (via a `DbosWorkflowRegistration` marker per registration) before launch, so the "register before launch" invariant on `Dbos` holds even for callers who never explicitly resolve the interface.
  - **`Microsoft.Extensions.Configuration.Memory` package does not exist** as a separate NuGet package; `AddInMemoryCollection` ships in `Microsoft.Extensions.Configuration` directly. Removed from `Directory.Packages.props`.


### 2026-04-30 — Port Decision Capture

- **Source/Trigger**: DBOS-29 implementation (PR for #11) — `Dbos.Transact.Cli` (System.CommandLine).
- **Pages created**: —
- **Pages updated**: —
- **Notes**:
  - **System.CommandLine version**: pinned to 2.0.7 (the latest stable, after the long beta cycle). 3.0.x previews were rejected for v1 — we want a stable surface. The 2.0+ API is action-based (`command.SetAction((parseResult, ct) => Task<int>)`) rather than the older binder-based API.
  - **Per-invocation IO instead of `Console`**: handlers initially used `Console.WriteLine`. This broke under xUnit's parallel test execution because `Console.SetOut` is a process-global mutation — concurrent tests trampled each other's redirection and only the last-written StringWriter received output. Switched to `parseResult.InvocationConfiguration.Output` / `.Error` (per-invocation `TextWriter`s); tests configure them on a fresh `ParseResult.InvocationConfiguration`. No more process-global state involved in tests.
  - **Dialect autodetection from connection string**: Java's CLI accepted JDBC URLs and dispatched on the `jdbc:postgresql:` / `jdbc:sqlite:` prefix. C# accepts native ADO.NET connection strings (Npgsql / Microsoft.Data.Sqlite shapes), which have no scheme prefix. `DatabaseOptions.ResolveDialect` heuristics: starts with `Data Source=` / `Filename=` or ends with `.sqlite` / `.db` → SQLite, else Postgres. An explicit `--dialect` flag overrides the heuristic.
  - **`reset` for SQLite is "delete the file"**: Java's `reset` is PG-only — it issues `DROP DATABASE ... CREATE DATABASE` against the postgres "control" database. SQLite has no equivalent; the C# port deletes the file and its WAL/SHM sidecars after `SqliteConnection.ClearAllPools()`. In-memory SQLite databases cannot be reset (errors out with exit code 1).
  - **`postgres start/stop` (Docker) intentionally deferred**: Java's CLI shells out to `docker run` to manage a local PG container. The C# port skips this surface for v1 — users running the C# CLI typically already have docker tooling, and the testcontainers fixture covers the dev-loop path. Can be added later as a thin process-shell wrapper.


### 2026-04-30 — Port Decision Capture

- **Source/Trigger**: DBOS-26 implementation (PR for #5) — `Admin/AdminServer.cs` (HTTP admin endpoints).
- **Pages created**: —
- **Pages updated**: —
- **Notes**:
  - **`HttpListener` instead of ASP.NET Core**: Java's `AdminServer` uses `com.sun.net.httpserver.HttpServer` — a low-level BCL HTTP listener with no framework dependencies. The C# port uses `System.Net.HttpListener` for the same reason: keeps the core `Dbos.Transact` library free of `Microsoft.AspNetCore.App` framework references and matches Java's choice closely. Ports a custom dispatch loop (`Pattern.compile("/workflows/([^/]+)(/[^/]*)?")` → `Regex` in C#) and JSON shapes via `System.Text.Json`.
  - **Endpoint surface implemented**: `/dbos-healthz`, `/deactivate`, `/dbos-workflow-queues-metadata`, `/dbos-workflow-recovery`, `/queues`, `/workflows`, `/workflows/{id}`, `/workflows/{id}/steps`, `/workflows/{id}/cancel`, `/workflows/{id}/resume`, `/workflows/{id}/fork` — all match Java path/method/status-code semantics (200, 204, 404, 405, 415, 500).
  - **Endpoints deferred**: `/dbos-garbage-collect` and `/dbos-global-timeout`. Both require new SystemDatabase DAO methods (`GarbageCollectAsync`, `GlobalTimeoutAsync`) that don't exist yet in the C# port. Not exposed by AdminServer (return 404 like any unmatched route) — to be added when the underlying DAO methods land.
  - **Drive-by additions to `DbosExecutor`** to back the admin endpoints:
    - `GetQueues()` — exposes `_queueRegistry.GetSnapshot()` for `/dbos-workflow-queues-metadata`.
    - `RecoverPendingWorkflowsAsync(executorIds)` — calls `GetPendingWorkflowsAsync` then `ExecuteWorkflowByIdAsync(isRecoveryRequest: true)` for each, returning the dispatched IDs. Mirrors Java's `recoverPendingWorkflows`.
    - `DeactivateLifecycleListeners()` / `IsDeactivated` — port-fidelity flag toggle. The Java implementation also stops queue/scheduler lifecycle; in C# the equivalent integration would happen via `IDbosLifecycleListener`. v1 is a no-op flag.
  - **Fork success-path test deferred**: `ForkWorkflowAsync` throws `NotImplementedException` in both Postgres and SQLite DAOs (pre-existing port gap). The fork endpoint is wired and routing is verified via `Fork_GET_Returns405` / `Fork_PostWithoutJson_Returns415`; the success path will be added when the DAO impls land.


### 2026-04-30 — Port Decision Capture

- **Source/Trigger**: DBOS-27 implementation (PR for #8) — `Conductor/Conductor.cs` (WebSocket client to DBOS Cloud).
- **Pages created**: —
- **Pages updated**: —
- **Notes**:
  - **`ClientWebSocket` instead of HttpClient.WebSocket**: `System.Net.WebSockets.ClientWebSocket` is the BCL equivalent of Java's `HttpClient.newWebSocketBuilder()`. Single connect-loop on a background task with reconnect-after-delay on failure. `KeepAliveInterval = pingPeriod` enables the BCL's automatic ping/pong (the C# port doesn't reimplement Java's manual ping/pong scheduler — `ClientWebSocket.Options.KeepAliveInterval` covers it cleanly).
  - **Per-message handler tasks**: Receive loop reassembles fragmented WebSocket frames into a full text message, then dispatches each message via `Task.Run` so subsequent receives aren't blocked by handler work. Mirrors Java's pattern of `getResponseAsync(...).whenComplete(...)`.
  - **Send is serialized via `SemaphoreSlim`**: `ClientWebSocket.SendAsync` is not safe for concurrent calls. A single send-lock ensures handler tasks dispatch responses one at a time without interleaving fragments.
  - **Handler subset**: Backed handlers — ExecutorInfo, Cancel, Resume, Delete, Recovery, ListWorkflows, ListQueuedWorkflows, ListSteps, GetWorkflow, ExistPendingWorkflows, ListSchedules, GetSchedule, PauseSchedule, ResumeSchedule. Deferred handlers (Restart, Fork, Import/ExportWorkflow, GetMetrics, GetWorkflowAggregates, GetWorkflowEvents/Notifications/Streams, Alert, BackfillSchedule, TriggerSchedule, ListApplicationVersions, SetLatestApplicationVersion, Retention) — these all need new `SystemDatabase` / `DbosExecutor` methods that don't exist in the C# port yet, so they reply with `BaseResponse(type, requestId, "Message type not implemented in the C# port v1.")`. The Cloud side sees an explicit error rather than the request hanging.
  - **In-memory test server via `HttpListener.AcceptWebSocketAsync`**: Java's tests use a Netty-backed test server. The C# port uses `HttpListener.AcceptWebSocketAsync` (BCL) — same in-memory pattern, no new dependencies. Caveat: `HttpListener.Prefixes` requires a trailing `/`, so the test server normalizes the prefix path while exposing a slash-free `ws://...` URL for the conductor to dial. Without the trailing slash on the prefix, `HttpListener` throws `ArgumentException: Only Uri prefixes ending in '/' are allowed.`


### 2026-05-01 — Port Decision Capture

- **Source/Trigger**: DBOS-30 implementation (PR for #30) — `test/Dbos.Transact.Conformance/` cross-runtime conformance harness.
- **Pages created**: —
- **Pages updated**: —
- **Notes**:
  - **Golden-fixture approach for Java comparison**: Rather than requiring Java tooling in CI, the harness uses pre-committed JSON golden fixtures under `fixtures/java/`. When a fixture is present, the C# snapshot is normalized and compared; when absent, the test no-ops cleanly. Generate fixtures once from a Java run, commit them, and CI comparison is thereafter Java-tooling-free.
  - **Normalization scope**: Timestamps, UUIDs, and executor IDs are excluded from `DbSnapshot` — only stable fields (`status`, `output`, `error`, `recovery_attempts`, `function_id`, `function_name`) are captured. This avoids flaky cross-run diffs while still catching meaningful divergences.
  - **Divergence injection test**: After a successful run, `ScenarioRunner.InjectDivergenceAsync` corrupts `workflow_status.status` directly via Npgsql. `DbSnapshotAssertion` re-validates and throws `ConformanceAssertionException` with a structured message naming both expected and actual values — verifying the harness fails clearly, not silently.
  - **Shared container via `IClassFixture`**: The Postgres Testcontainers container is spun up once per test class run (not per test method). Reduced from 3 container startups to 1; test time dropped from ~10 s to ~5 s with no isolation trade-off (each workflow uses a unique UUID).
  - **CA1711 / `Collection` suffix**: xUnit `[CollectionDefinition]` bearer classes are conventionally named `*Collection`, but CA1711 rejects that suffix. Named the bearer class `ConformanceGroup` — the string in `[CollectionDefinition("Conformance")]` / `[Collection("Conformance")]` is what xUnit matches, not the class name.

### 2026-05-03 — Concept Capture

- **Source/Trigger**: User questions about step retries and workflow recovery (conversation context)
- **Pages created**:
  - `concepts/step-retry-policy.md`
  - `concepts/workflow-recovery.md`
- **Pages updated**: `index.md` (added 2 entries, bumped totals 26→28, concepts 14→16, high-confidence 14→16)
- **Notes**:
  - **`step-retry-policy.md`**: Documents `[Step(RetriesAllowed, MaxAttempts, IntervalSeconds, BackOffRate)]` — the gate flag pattern, deterministic exponential back-off, and the pitfall that `RetriesAllowed = false` forces `maxAttempts = 1` regardless of other settings. No jitter in current implementation.
  - **`workflow-recovery.md`**: Documents the two external recovery entry points — DBOS Cloud conductor (`RecoveryRequest` WebSocket message → `Conductor.HandleRecoveryAsync`) and admin HTTP endpoint (`POST /dbos-workflow-recovery` → `AdminServer.WorkflowRecoveryAsync`) — both calling `DbosExecutor.RecoverPendingWorkflowsAsync`. Key insight: nothing auto-recovers locally; recovery must be explicitly triggered. Also cross-references the `MaxRecoveryAttempts` overflow bug from DBOS-25.

### 2026-05-02 — Ingest

- **Source/Trigger**: `ingest raw/csharp-programming-guide.md` (translated from https://docs.dbos.dev/java/programming-guide for Dbos.Transact v0.0.0-alpha.0.35)
- **Pages created**:
  - `raw/csharp-programming-guide.md` (raw source — immutable)
  - `summaries/csharp-programming-guide.md`
- **Pages updated**: `index.md` (added summary entry, bumped totals 25→26, summaries 1→2, sources 1→2, high-confidence 13→14)
- **Notes**:
  - Source is a C# translation of the upstream Java programming guide. Key Java→C# mapping documented: `@Workflow`/`@Step` annotations → `[Workflow]`/`[Step]` attributes; `dbos.runStep()` lambda → `[Step]`-annotated proxy methods intercepted by Castle.DynamicProxy; `new DBOS(config)` → `Dbos.Builder(name).UsePostgres(connStr).Build()`; `dbos.launch()` → `await dbos.LaunchAsync()`; `dbos.startWorkflow(supplier)` → `await dbos.StartWorkflowAsync<T>(name, className, null, args, opts)`.
  - All code examples in the raw source are verified against the actual C# public API (read from source files in `src/Dbos.Transact/`).
  - SQLite dialect alternative (`UseSqlite`) documented as a zero-dependency local-dev path — not covered in the Java guide.
  - NuGet package version anchored at `0.0.0-alpha.0.35` (first published alpha, tagged `v0.1.0-alpha.1` on 2026-05-02).
