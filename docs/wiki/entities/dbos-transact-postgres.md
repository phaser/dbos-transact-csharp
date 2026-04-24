---
title: "Dbos.Transact.Postgres"
type: entity
tags: [project, dialect-postgres, persistence]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
---

## Overview

`Dbos.Transact.Postgres` is the Npgsql-backed Postgres dialect NuGet. It subclasses `SystemDatabase` with Postgres-native primitives — `LISTEN`/`NOTIFY` for notifications, `FOR UPDATE SKIP LOCKED` for queue dispatch, and `pg_try_advisory_lock` for scheduler leadership / singleton workflows. It is the "scale-out" dialect; multi-host worker deployments require it.

## Characteristics

- **Assembly / namespace / NuGet ID:** `Dbos.Transact.Postgres`.
- **Key types:** `PostgresSystemDatabase : SystemDatabase`, `PostgresDialect : ISqlDialect`, `DbosPostgresExtensions` (`services.AddDbos().UsePostgres(connectionString)`).
- **Driver:** Npgsql (light, unopinionated, idiomatic .NET PG access).
- **Postgres-native mechanisms used:** `LISTEN` / `NOTIFY`, `FOR UPDATE SKIP LOCKED`, `pg_try_advisory_lock`, `jsonb`, `RETURNING`, `NOW()`, `GENERATED AS IDENTITY`.
- **Test infra:** integration tests use `Testcontainers.NET` to spin up ephemeral Postgres containers.

## Common Strategies

- [[concepts/dialect-abstraction]]
- [[concepts/no-orm-constraint]]
- [[concepts/postgres-feature-fallbacks]] (this entity is the "original" side of each mapping)

## Related Entities

- [[entities/dbos-transact]]
- [[entities/dbos-transact-sqlite]]
