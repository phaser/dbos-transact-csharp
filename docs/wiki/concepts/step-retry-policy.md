---
title: "Step Retry Policy"
type: concept
tags: [core, workflows, steps, retries, resilience, foundational]
created: 2026-05-03
updated: 2026-05-03
sources: []
confidence: high
---

# Step Retry Policy

## Definition

Each `[Step]`-annotated method in a DBOS workflow can be configured with an automatic retry policy. When a step throws an unhandled exception and `RetriesAllowed = true`, the executor re-invokes the step up to `MaxAttempts` total times with an exponential back-off between attempts. A step that exhausts all attempts without success propagates the final exception to the workflow body.

## How It Works

Retry configuration lives on the `[Step]` attribute (`src/Dbos.Transact/Workflow/StepAttribute.cs`):

```csharp
[Step(RetriesAllowed = true, MaxAttempts = 5, IntervalSeconds = 2.0, BackOffRate = 2.0)]
public virtual async Task<string> CallExternalApi() { ... }
```

At execution time (`DbosExecutor.cs:320`), the executor derives the effective attempt count:

```csharp
var maxAttempts = stepAttr.RetriesAllowed ? stepAttr.MaxAttempts : 1;
```

If `RetriesAllowed` is `false`, `maxAttempts` is forced to `1` regardless of `MaxAttempts`. Between each failed attempt the executor sleeps for the current retry interval, then multiplies it by `BackOffRate` for the next gap:

```csharp
await Task.Delay(retryInterval, ct);
retryInterval = TimeSpan.FromTicks((long)(retryInterval.Ticks * stepAttr.BackOffRate));
```

## Key Parameters

| Parameter | Default | Description |
|---|---|---|
| `RetriesAllowed` | `false` | Gate flag — must be `true` to enable any retries |
| `MaxAttempts` | `3` | Total invocations including the first attempt |
| `IntervalSeconds` | `1.0` | Initial wait in seconds before the first retry |
| `BackOffRate` | `2.0` | Exponential multiplier applied to the interval after each failure |

Defaults are defined as constants on `StepOptions` (`DefaultIntervalSeconds = 1.0`, `DefaultBackOff = 2.0`).

## When To Use

- Any step that calls external services, APIs, or infrastructure that can transiently fail.
- Steps that access external databases, message brokers, or file systems where transient errors are expected.
- Do **not** enable retries for steps that have non-idempotent side effects that must not be repeated.

## Risks & Pitfalls

- **`RetriesAllowed` is the gate.** Forgetting to set it means retries never happen even if `MaxAttempts` is set to a large number.
- **Steps must be idempotent.** DBOS persists the step result after a *successful* invocation and replays from DB on subsequent workflow executions. A step with side effects that must not repeat (e.g. sending an email) will re-execute the side effect on every retry attempt within the same execution.
- **Back-off can be long.** With `IntervalSeconds = 1.0`, `BackOffRate = 2.0`, and `MaxAttempts = 5`, the delays are 1s, 2s, 4s, 8s — over 15 seconds total wait before the step is dead-lettered.
- **No jitter.** The current implementation applies deterministic exponential back-off with no random jitter, which can cause thundering-herd retries when many parallel steps fail simultaneously.

## Related Concepts

- [[concepts/durable-workflow]] — steps are the checkpointed units of work within a workflow
- [[concepts/method-interception]] — the Castle.DynamicProxy interceptor reads `[Step]` attributes and drives the retry loop
- [[concepts/workflow-recovery]] — workflow-level recovery (PENDING → re-execution) is distinct from step-level retries

## Sources

Empirically derived from reading `src/Dbos.Transact/Workflow/StepAttribute.cs`, `src/Dbos.Transact/Workflow/StepOptions.cs`, and `src/Dbos.Transact/Execution/DbosExecutor.cs`.
