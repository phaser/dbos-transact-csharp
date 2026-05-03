---
title: "Workflow Recovery"
type: concept
tags: [core, workflows, recovery, resilience, conductor, admin, foundational]
created: 2026-05-03
updated: 2026-05-03
sources: []
confidence: high
---

# Workflow Recovery

## Definition

When a DBOS executor process crashes or is killed while a workflow is running, the workflow record remains in `PENDING` state in the system database. Workflow recovery is the process of detecting these orphaned `PENDING` workflows and re-executing them on a live executor. Recovery is always **externally triggered** — nothing inside a running process automatically detects and recovers its own crashed siblings.

## How It Works

### State preserved in the database

Every workflow is written to `workflow_status` with `status = PENDING` at the moment it starts. When it completes (success or error), the status is updated to `SUCCESS`, `ERROR`, or `RETRIES_EXCEEDED`. A workflow that was running when the process died stays `PENDING` indefinitely until a recovery call arrives.

### Recovery entry points

Two callers can trigger recovery, both ultimately calling `DbosExecutor.RecoverPendingWorkflowsAsync(executorIds)`:

1. **DBOS Cloud Conductor** (`src/Dbos.Transact/Conductor/Conductor.cs:HandleRecoveryAsync`) — when the executor is connected to DBOS Cloud, the cloud platform detects that an executor ID has gone silent and sends a `RecoveryRequest` message over the WebSocket connection. The conductor handler calls `RecoverPendingWorkflowsAsync` with the list of dead executor IDs.

2. **Admin HTTP endpoint** (`src/Dbos.Transact/Admin/AdminServer.cs:WorkflowRecoveryAsync`) — `POST /dbos-workflow-recovery` with a JSON body containing the list of executor IDs. An external operator, deployment script, or the cloud platform can POST to this endpoint on any live executor to trigger the same recovery path.

### What `RecoverPendingWorkflowsAsync` does

```csharp
var pending = await _db.GetPendingWorkflowsAsync(executorIds, _appVersion, ct);
foreach (var status in pending)
    await ExecuteWorkflowByIdAsync(status.WorkflowId, isRecoveryRequest: true, ...);
```

It queries the system database for all `PENDING` workflows owned by the given executor IDs (optionally filtered by app version), then re-dispatches each one via `ExecuteWorkflowByIdAsync` with `isRecoveryRequest = true`. The workflow body re-runs from the beginning, but every step that already has a persisted output is skipped — this is the idempotent replay guarantee of [[concepts/durable-workflow]].

### Recovery in self-hosted deployments

Without DBOS Cloud, the operator is responsible for calling the admin endpoint. A common pattern: when a new process starts up, it queries the database for executor IDs with `PENDING` workflows that haven't heartbeated recently and POSTs them to `POST /dbos-workflow-recovery` on itself.

## Key Parameters

- **`executorIds`** — The list of dead executor IDs whose `PENDING` workflows should be recovered. Workflows are matched by executor ID stored at the time the workflow started.
- **`appVersion`** — Recovery is scoped to matching app versions to avoid replaying a workflow on an incompatible code version.
- **`MaxRecoveryAttempts`** — Set on `[Workflow(MaxRecoveryAttempts = N)]`. Defaults to `Constants.DefaultMaxRecoveryAttempts` (100, matching Java). After N recovery attempts, the workflow is dead-lettered as `RETRIES_EXCEEDED`. (Note: using `int.MaxValue` here overflows the DAO check — always use the constant or a reasonable integer.)

## When To Use

- Understand this concept when diagnosing why a crashed workflow is not automatically re-running: recovery must be explicitly triggered.
- When building a self-hosted deployment, wire up a startup recovery call (or a periodic dead-executor sweep) to restore `PENDING` workflows after node failures.
- When connecting to DBOS Cloud, this is handled automatically by the conductor.

## Risks & Pitfalls

- **Nothing auto-recovers locally.** A standalone executor with no conductor connection and no external call to the admin endpoint will leave `PENDING` workflows orphaned forever.
- **`MaxRecoveryAttempts` overflow bug** (fixed in DBOS-25): Using `int.MaxValue` as the default caused `maxRetries + 1` to overflow to `int.MinValue`, dead-lettering every workflow on its first recovery attempt. Always default to `Constants.DefaultMaxRecoveryAttempts = 100`.
- **App version mismatch.** If the recovery executor runs a different `appVersion` than the original, `GetPendingWorkflowsAsync` will not return those workflows. Ensure version continuity across rolling restarts.
- **Step retries vs workflow recovery are distinct.** Step-level retries (`[Step(RetriesAllowed = true)]`) happen synchronously within a single execution attempt. Workflow recovery is a separate mechanism that re-dispatches the entire workflow after a process crash.

## Related Concepts

- [[concepts/durable-workflow]] — PENDING state and idempotent replay are the foundation that makes recovery safe
- [[concepts/step-retry-policy]] — step-level retry is distinct from workflow-level crash recovery
- [[concepts/scheduler-leadership-and-cron]] — the scheduler uses advisory locks to elect a leader; if the leader crashes, the lock is released and another instance takes over (different mechanism, similar concern)

## Sources

Empirically derived from reading `src/Dbos.Transact/Execution/DbosExecutor.cs` (`RecoverPendingWorkflowsAsync`), `src/Dbos.Transact/Conductor/Conductor.cs` (`HandleRecoveryAsync`), and `src/Dbos.Transact/Admin/AdminServer.cs` (`WorkflowRecoveryAsync`). Also informed by DBOS-25 port notes (log entry 2026-04-29) documenting the `MaxRecoveryAttempts` overflow bug.
