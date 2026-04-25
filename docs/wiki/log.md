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
