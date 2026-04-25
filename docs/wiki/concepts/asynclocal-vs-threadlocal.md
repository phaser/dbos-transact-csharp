---
title: "AsyncLocal vs ThreadLocal for Workflow Context"
type: concept
tags: [csharp, async, context, port-decision, foundational]
created: 2026-04-25
updated: 2026-04-25
sources: []
confidence: high
---

# AsyncLocal vs ThreadLocal for Workflow Context

## Definition

Java's `DBOSContextHolder` uses `ThreadLocal<DBOSContext>` to associate workflow execution state with the current thread. The C# port uses `AsyncLocal<DbosContext?>` instead, which is the idiomatic .NET equivalent for ambient state in async code.

## How It Works

### Java: ThreadLocal

```java
private static final ThreadLocal<DBOSContext> contextHolder =
    ThreadLocal.withInitial(DBOSContext::new);
```

- Each OS thread gets its own copy, initialized lazily on first access.
- Mutations are scoped to the thread and persist across all synchronous calls on that thread.
- No automatic propagation to threads spawned via thread pools.

### C#: AsyncLocal

```csharp
private static readonly AsyncLocal<DbosContext?> _holder = new();
```

- Values flow **down** into child `Task`s (child tasks inherit a snapshot of the parent's value at the point of spawning).
- Mutations in a child task do **not** propagate back to the parent.
- Two sibling tasks spawned from the same parent each get their own copy of the ambient value.

## Key Parameters

| Property | Java ThreadLocal | C# AsyncLocal |
|---|---|---|
| Per-thread isolation | ✓ | ✓ (via async execution context) |
| Child inherits parent value | ✗ | ✓ (snapshot at spawn time) |
| Child mutation visible to parent | N/A (different thread) | ✗ (intentional) |
| Auto-initialization | ✓ (`withInitial`) | ✗ (starts null; init in `Get()`) |

## When To Use

Use `AsyncLocal<T>` whenever ambient state must be available across `await` points and through child tasks without manual propagation. This is the standard pattern for .NET ambient contexts (ASP.NET Core `HttpContext` accessor, distributed tracing, etc.).

For DBOS specifically: the executor pushes a new `DbosContext` via `DbosContextHolder.Set()` before executing a workflow step, and pops it (via `Set(previous)` or `Clear()`) on exit. Because child task mutations don't propagate back, forked workflows get an independent context without risking corruption of the parent workflow's step counter.

## Risks & Pitfalls

- **Null initialization**: Unlike `ThreadLocal.withInitial()`, `AsyncLocal` starts as `null` in any async flow that hasn't called `Set()`. The `Get()` implementation must handle null explicitly — in the DBOS port, `Get()` returns `null` and callers check before use.
- **Reference vs value semantics**: `AsyncLocal<T>` stores the reference. If `T` is a mutable class, child tasks that inherit the reference and mutate the object's fields WILL affect the parent (since they share the object). Isolation requires the executor to always `Set()` a **new** `DbosContext` object for each workflow scope, not mutate a shared one.
- **Static accessor divergence**: Java `DBOSContext.workflowId()` etc. are static methods that delegate to `DBOSContextHolder.get()`. The C# port mirrors this with `DbosContext.GetWorkflowId()` etc., returning `null` (not throwing) when outside a workflow context.

## Related Concepts

- [[concepts/durable-workflow]] — the workflow execution model that requires this context plumbing
- [[concepts/method-interception]] — the interceptor layer that sets/clears the context around each workflow step

## Sources

Observed during DBOS-06 port (PR covering issues 04–06, 2026-04-25). No raw document.
