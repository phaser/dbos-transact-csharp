---
title: "dbos-transact-java"
type: entity
tags: [upstream, java, reference-implementation]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
---

## Overview

`dbos-transact-java` ([github.com/dbos-inc/dbos-transact-java](https://github.com/dbos-inc/dbos-transact-java)) is the primary reference implementation that `dbos-transact-csharp` mirrors for feature parity. When a design question doesn't have an obvious .NET-idiomatic answer, the Java runtime's shape is the default.

## Characteristics

- **Annotations:** `@Workflow`, `@Step`, `@Scheduled` on interface methods; registered via an annotation scanner.
- **Interception:** `java.lang.reflect.Proxy` + `DBOSInvocationHandler` — interface-only, runtime-registered. The C# equivalent is `Castle.DynamicProxy` (see [[concepts/method-interception]]).
- **Serialization:** Jackson with custom `JsonTypeInfo` for polymorphic workflow-args and error payloads. The C# runtime uses `System.Text.Json` with `[JsonPolymorphic]` — see [[concepts/portable-serializer]].
- **Hosting:** Spring Boot starter (`transact-spring-boot-starter`) with `DBOSProperties` for configuration binding. The C# equivalent is `Dbos.Transact.Hosting` on `Microsoft.Extensions.Hosting` — intentionally not Spring-shaped.
- **CLI:** picocli-based `transact-cli`. The C# equivalent is `System.CommandLine` — see [[entities/dbos-transact-cli]].
- **Module structure:** `transact/` (core), `transact-spring-boot-starter/`, `transact-cli/`.

## Port Mapping

| Java module | C# project |
|---|---|
| `transact/` | [[entities/dbos-transact]] |
| (split out of core) | [[entities/dbos-transact-postgres]] |
| (split out of core) | [[entities/dbos-transact-sqlite]] |
| `transact-spring-boot-starter/` | [[entities/dbos-transact-hosting]] |
| `transact-cli/` | [[entities/dbos-transact-cli]] |

## Common Strategies

- [[concepts/durable-workflow]]
- [[concepts/method-interception]]
- [[concepts/portable-serializer]]

## Related Entities

- [[entities/dbos-transact-py]]
- [[entities/dbos-transact-ts]]
- [[entities/dbos-transact]]
