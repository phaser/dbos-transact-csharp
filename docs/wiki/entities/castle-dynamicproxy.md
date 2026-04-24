---
title: "Castle.DynamicProxy"
type: entity
tags: [library, interception, decided-v1]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
---

## Overview

`Castle.DynamicProxy` is the .NET method-interception library shipped via the `Castle.Core` NuGet. It generates proxy types at runtime that forward calls through a user-supplied `IInterceptor`, with proper async-return-type support. `dbos-transact-csharp` uses it to wrap workflow/service implementations so that `[Workflow]` and `[Step]` attribute-driven orchestration works transparently at call sites.

## Characteristics

- **Runtime IL emit.** Proxies are generated on first use at runtime — incompatible with AOT / native-image publish.
- **Interface and class proxies.** v1 uses interface proxying, which is simpler and doesn't require virtual members on user classes.
- **Async-aware interceptor chain.** The specific feature that ruled out the built-in `System.Reflection.DispatchProxy` (awkward async handling).
- **DI container independent.** Composes with any `IServiceCollection`-compatible container.
- **Mature.** Widely used in the .NET ecosystem (NSubstitute, Moq, logging/tracing interceptors), so the risk of unexpected breakage is low.

## Alternatives Considered

- **`System.Reflection.DispatchProxy`** (built-in, no dependency) — interface-only, but awkward async return-type handling led to it being rejected.
- **Source generators** — AOT-safe, compile-time dispatch. Deferred to v2; the interception contract should be designed so the migration is a swap rather than a rewrite.

## Common Strategies

- [[concepts/method-interception]]

## Related Entities

- [[entities/dbos-transact]]
