---
title: "Dbos.Transact.Hosting"
type: entity
tags: [project, hosting, integration]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
---

## Overview

`Dbos.Transact.Hosting` is the `Microsoft.Extensions.Hosting` integration NuGet. It is host-agnostic — works in console apps, ASP.NET Core, and Worker Services alike — and intentionally does not mirror Spring Boot's auto-configuration shape. The .NET DI + Options pattern covers the same ground more directly.

## Characteristics

- **Assembly / namespace / NuGet ID:** `Dbos.Transact.Hosting`. Name chosen to match the `Microsoft.Extensions.Hosting` convention rather than `.DependencyInjection` or `.AspNetCore`.
- **Public entry points:**
  - `services.AddDbos(cfg => …)` — registers options and core services.
  - `services.AddDbosWorkflow<TInterface, TImpl>()` — registers a workflow implementation with proxy interception (see [[concepts/method-interception]]).
  - `DbosHostedService : IHostedService` — drives the executor, scheduler, queue workers, and admin server per host lifecycle.
  - `DbosOptionsConfigurator` — binds `IConfiguration` sections (equivalent to the Java `DBOSProperties`).
- **Upstream equivalent:** `transact-spring-boot-starter/` in [[entities/dbos-transact-java]] — adapted to .NET hosting idioms, not mechanically translated.

## Common Strategies

- [[concepts/method-interception]]
- [[concepts/durable-workflow]]

## Related Entities

- [[entities/dbos-transact]]
- [[entities/dbos-transact-java]]
