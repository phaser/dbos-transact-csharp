---
title: "dbos-transact-ts"
type: entity
tags: [upstream, typescript, reference-implementation]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
---

## Overview

`dbos-transact-ts` ([github.com/dbos-inc/dbos-transact-ts](https://github.com/dbos-inc/dbos-transact-ts)) is the TypeScript DBOS runtime. The C# port references it primarily for public API shape — decorator names, handle ergonomics, option-bag conventions — when deciding whether to follow the Java surface or an ecosystem-idiomatic variant.

## Characteristics

- **Decorator names.** `@Workflow`, `@Step` — the C# `[Workflow]` / `[Step]` attribute surface mirrors these after dropping the `@` and using PascalCase.
- **Handle ergonomics.** TS's async/await-over-`Promise` model informs the C# `Task<T>`-based handle shapes (`WorkflowHandle`, `WorkflowHandleTcs` backed by `TaskCompletionSource<T>`).
- **Portable serializer.** TS emits golden-fixture payloads used to validate round-tripping in the C# implementation — see [[concepts/portable-serializer]].

## Common Strategies

- [[concepts/durable-workflow]]
- [[concepts/portable-serializer]]

## Related Entities

- [[entities/dbos-transact-java]]
- [[entities/dbos-transact-py]]
