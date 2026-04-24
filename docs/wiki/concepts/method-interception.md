---
title: "Method Interception"
type: concept
tags: [core, interception, decided-v1]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
confidence: high
---

## Definition

`dbos-transact-csharp` uses `Castle.DynamicProxy` (via the `Castle.Core` NuGet) to wrap registered workflow/service implementations in a runtime-generated proxy. The proxy's `IInterceptor` inspects `[Workflow]` and `[Step]` attributes on the called method and routes execution through `DbosExecutor`, so that checkpointing, resume, and error capture happen transparently around user code.

## How It Works

- Registration flow: `services.AddDbosWorkflow<TInterface, TImpl>()` (in [[entities/dbos-transact-hosting]]) binds `TInterface` to a Castle-generated proxy that forwards into a `TImpl` instance.
- The interceptor (`DbosInvocationInterceptor`) reads the attribute on the target method to decide the code path: `[Workflow]` starts or resumes a workflow via `DbosExecutor`; `[Step]` looks up a cached step result or records a new one; unannotated methods pass through.
- Async return types (`Task`, `Task<T>`, `ValueTask<T>`) are handled by Castle's async-aware interceptor machinery; this is the specific pain point that ruled out `System.Reflection.DispatchProxy`.
- The Java runtime uses `java.lang.reflect.Proxy` + `DBOSInvocationHandler` — interface-only and runtime-registered. Castle matches those semantics closely.

## Key Parameters

- **Interface-based registration** — v1 requires workflow implementations behind an interface; class-only proxying is out of scope.
- **DI container independence** — Castle composes with any container; `AddDbosWorkflow<TInterface, TImpl>()` delegates to `IServiceCollection` without hard-coding a container.
- **Runtime IL emit** — proxies are built at registration time; there is no compile-time code gen on the v1 path.

## When To Use

Whenever a user service exposes `[Workflow]` / `[Step]` methods that the runtime must orchestrate. The attribute-on-interface + proxy registration pattern is the only documented path; there is no "plain function" escape hatch in v1.

## Risks & Pitfalls

- **No AOT / native-image support.** Castle emits IL at runtime — unusable under `PublishAot`. This is the primary reason AOT is an explicit v1 non-goal.
- **Sealed classes / non-virtual methods.** Castle requires virtual members on class proxies; interface proxying sidesteps this and is the v1 requirement.
- **Self-invocation.** A workflow calling another step on `this` bypasses the proxy (same pitfall as Spring AOP). Inject the interface and call through it.
- **Reflection surface in trimming scenarios.** Aggressive assembly trimming can strip types Castle discovers by reflection; trimmer annotations or `DynamicallyAccessedMembers` hints may be needed if users enable trimming.
- **Planned migration.** A source-generator-based dispatch is the AOT-safe replacement for v2; the interception contract should be designed so the switch is a swap, not a rewrite (see [design.md §Open questions](#)).

## Related Concepts

- [[concepts/durable-workflow]]

## Related Entities

- [[entities/castle-dynamicproxy]]

## Sources

- `raw/design.md` — §Key design decisions → Interception; §Open questions #3 (source-gen migration)
