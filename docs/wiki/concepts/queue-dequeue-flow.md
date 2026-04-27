---
title: "Queue Dequeue Flow"
type: concept
tags: [queues, executor, serialization, port-decision, foundational]
created: 2026-04-27
updated: 2026-04-27
sources: []
confidence: high
---

# Queue Dequeue Flow

## Definition

The queue dequeue flow is the two-phase mechanism by which DBOS executes workflows submitted to a named queue. Phase 1 inserts the workflow in `ENQUEUED` state and returns immediately without running the body. Phase 2 is driven by `QueueService`: it transitions rows to `PENDING` and calls back into the executor to run the body.

## How It Works

### Phase 1 — Enqueue (StartWorkflowAsync with QueueName)

In Java's `DBOSExecutor.executeWorkflow`, when `options.queueName() != null`, the method calls `enqueueWorkflow(...)` and immediately returns a `WorkflowHandleDBPoll` — the workflow body never executes on this call path.

In C#, the equivalent guard in `DbosExecutor.StartWorkflowAsync` is:

```csharp
// After InitWorkflowStatusAsync, before ShouldExecuteOnThisExecutor check:
if (opts.QueueName is not null)
    return new WorkflowHandleDbPoll<T>(_db, _serializer, workflowId);
```

This guard must be first: `InitWorkflowStatusAsync` returns `ShouldExecuteOnThisExecutor=true` for a fresh ENQUEUED workflow (the executor "owns" it), so without the QueueName guard the body runs immediately and bypasses the queue entirely.

### Phase 2 — Dequeue (QueueService → ExecuteWorkflowByIdAsync)

1. `QueueService.RunQueuePollerAsync` calls `ProcessPartitionAsync` on a per-queue loop.
2. `GetAndStartQueuedWorkflowsAsync` transitions ENQUEUED → PENDING in the DB and returns the workflow IDs.
3. `ExecuteWorkflowByIdAsync` looks up the `RegisteredWorkflow` in `_workflowMap`, fetches raw input JSON via `GetWorkflowInputsAsync`, deserializes it, and calls `StartWorkflowAsync` with `QueueName=null` and `isDequeuedRequest=true`.
4. `InitWorkflowStatusAsync` ON CONFLICT UPDATE increments `recovery_attempts` and returns `ShouldExecuteOnThisExecutor=true`.
5. The QueueName guard is skipped (null), so the workflow body executes.
6. Result is written to DB; `WorkflowHandleDbPoll` on the caller's handle sees `SUCCESS`.

## Key Parameters

- **`isDequeuedRequest=true`**: Bypasses the `ownerXid != currentXid` ownership check in `InitWorkflowStatusAsync`, allowing the same workflow to be re-initialized for execution after it was transitioned to PENDING by a different connection.
- **`RegisterWorkflow`**: Must be called for any workflow that may be dequeued; `ExecuteWorkflowByIdAsync` looks up workflows by `FqName` in `_workflowMap`.
- **Adaptive polling interval**: 1 s minimum, 120 s maximum; shrinks 10% on success, doubles on error; multiplied by `_speedup` (0.01× in tests).
- **Concurrency limits**: Global (`Concurrency`) and per-worker (`WorkerConcurrency`) caps on PENDING rows; whichever is more restrictive applies.

## When To Use

This pattern applies whenever any code in the executor calls `StartWorkflowAsync` with a non-null `QueueName`. The caller receives a `WorkflowHandleDbPoll` that polls the DB for completion; the queue service drives actual execution.

## Risks & Pitfalls

### object?[] Serialization Round-Trip

Workflow args are serialized as `object?[]` via `DbosJsonSerializer`. STJ deserializes `object[]` elements as `JsonElement` rather than the original CLR types, causing `MethodInfo.Invoke` to throw `ArgumentException: Object of type 'JsonElement' cannot be converted to 'Int32'`.

**Fix**: `DbosJsonSerializer.Serialize(object?[])` wraps each element in its own `TypeEnvelope`, and `Deserialize` decodes per-element envelopes when the outer type is `object[]`:

```json
// object?[] { 5, "hello" }  →  per-element envelopes:
{"t":"System.Object[], ...","v":[
  {"t":"System.Int32, ...","v":5},
  {"t":"System.String, ...","v":"hello"}
]}
```

### Missing RegisterWorkflow

If `RegisterWorkflow` is not called before the queue service dequeues a workflow, `ExecuteWorkflowByIdAsync` throws `InvalidOperationException: Workflow '...' is not registered on this executor.` The queue service catches this and continues processing other IDs, but the workflow stays stuck in PENDING.

### DB Locking Strategy Differences

| Backend | Isolation | Lock Mode |
|---------|-----------|-----------|
| Postgres (no global concurrency) | REPEATABLE READ | `FOR UPDATE SKIP LOCKED` |
| Postgres (with global concurrency) | REPEATABLE READ | `FOR UPDATE NOWAIT` |
| SQLite | `IsolationLevel.Serializable` = `BEGIN IMMEDIATE` | No row-level locking |

`FOR UPDATE NOWAIT` failures (concurrent claim race) are caught and return an empty list, allowing retry on the next polling interval.

## Related Concepts

- [[concepts/durable-workflow]] — The broader workflow execution model this dequeue flow implements
- [[concepts/dialect-abstraction]] — How DAO implementations differ between Postgres and SQLite
- [[concepts/postgres-feature-fallbacks]] — SKIP LOCKED is Postgres-specific; SQLite uses BEGIN IMMEDIATE

## Sources

Derived from DBOS-23 implementation (PR #43). Reference: Java `DBOSExecutor.executeWorkflow` line 1463-1481, Java `QueuesDAO.getAndStartQueuedWorkflowsAsync`.
