---
title: "Durable Workflow"
type: concept
tags: [core, workflows, persistence, foundational]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
confidence: high
---

## Definition

A durable workflow is a user-authored method whose execution progress — every `[Step]` invocation, its arguments, and its output — is checkpointed to a relational database as it runs. On process restart, the runtime reads outstanding workflows from the database and resumes each one from its last committed checkpoint, so that each step runs exactly once across crashes, redeploys, and host migrations. This is the core capability that `dbos-transact-csharp` delivers; every other subsystem (queues, scheduler, notifications, admin/conductor endpoints) exists to support or extend it.

## How It Works

- User methods are marked with `[Workflow]` and `[Step]` attributes on an interface, implemented by a user class, and registered via `services.AddDbosWorkflow<TInterface, TImpl>()`.
- Registration wraps the implementation in a `Castle.DynamicProxy` proxy — see [[concepts/method-interception]] — whose `IInterceptor` routes each intercepted call through `DbosExecutor`.
- Before a `[Step]` executes, the executor looks up whether that `(workflow_id, step_number)` already has a recorded result. If yes, return it. If no, run the step, serialize its output via [[concepts/portable-serializer]], and record it.
- Step outputs, workflow arguments, portable exceptions, and workflow state live in fixed system tables owned by the library (see [[concepts/no-orm-constraint]]). Reads and writes go through `SystemDatabase` and the dialect-specific subclass — see [[concepts/dialect-abstraction]].
- Workflow handles (`WorkflowHandle`, `WorkflowHandleDbPoll`, `WorkflowHandleTcs`) expose status polling and result retrieval.
- `DbosHostedService` (in [[entities/dbos-transact-hosting]]) drives the lifecycle: on host start it runs the recovery scan, starts queue workers, the scheduler, and the admin server; on host stop it drains gracefully.

## Key Parameters

- **Exactly-once step delivery** — the invariant the checkpoint protocol must preserve.
- **Async handles** — workflows can be started and awaited out-of-band via `WorkflowHandle`, with status polling (`WorkflowHandleDbPoll`) or in-process completion (`WorkflowHandleTcs` over `TaskCompletionSource<T>`).
- **Scheduled execution** — cron and long durable sleeps via `[Scheduled]` and `WorkflowSchedule`.
- **Notifications / events** — `WorkflowEvent` and `WorkflowStream` for exactly-once inter-workflow signaling (see [[concepts/in-process-notification-optimization]] and [[concepts/postgres-feature-fallbacks]]).
- **Timeouts, forks** — `Timeout`, `ForkOptions`, `StepOptions` modulate individual step/workflow behavior.

## When To Use

Any code path that crosses a side-effect boundary where re-running on failure would be harmful — external API calls, payments, outbound emails, multi-step database mutations that span other side effects. Workflows are the right shape when you need guaranteed progress without building ad-hoc reconciliation or idempotency-key plumbing.

## Risks & Pitfalls

- **Checkpoint latency on hot paths.** Every `[Step]` is a write. Steps that are called millions of times per second are not a fit — batch or inline them.
- **Serializer drift.** Workflow args and outputs flow through [[concepts/portable-serializer]]; changing a type shape without a migration path breaks replays of in-flight workflows.
- **Non-deterministic non-step code.** Only `[Step]`-annotated calls are recorded. If a workflow body consults `DateTime.UtcNow`, randomness, or external state outside a step, recovery will diverge.
- **Long-lived transactions.** The checkpoint write happens inside the step's own transaction context; steps that hold locks or run lengthy external work must be structured to commit promptly.
- **Cross-runtime interop ceiling.** Workflows that share state with Python/TS/Java peers are bounded by the portable serializer's supported-type subset.

## Related Concepts

- [[concepts/method-interception]]
- [[concepts/portable-serializer]]
- [[concepts/dialect-abstraction]]
- [[concepts/in-process-notification-optimization]]

## Sources

- `raw/design.md` — §Goal, §Repo layout (`Workflow/`, `Execution/`), §Key design decisions (Interception, Serialization)
