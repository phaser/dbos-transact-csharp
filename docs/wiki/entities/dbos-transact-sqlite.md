---
title: "Dbos.Transact.Sqlite"
type: entity
tags: [project, dialect-sqlite, persistence]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
---

## Overview

`Dbos.Transact.Sqlite` is the Microsoft.Data.Sqlite-backed dialect NuGet. It subclasses `SystemDatabase` with SQLite-native primitives — polling notifications, `BEGIN IMMEDIATE` for claim-in-single-transaction queue dispatch, and an application-level equivalent for advisory locks. Pairs with the [[concepts/in-process-notification-optimization]] to deliver near-instant notifications in single-process deployments, and is explicitly a [[concepts/sqlite-production-target]], not a test-only dialect.

## Characteristics

- **Assembly / namespace / NuGet ID:** `Dbos.Transact.Sqlite`.
- **Key types:** `SqliteSystemDatabase : SystemDatabase`, `SqliteDialect : ISqlDialect`, `DbosSqliteExtensions` (`services.AddDbos().UseSqlite(connectionString)`).
- **Driver:** Microsoft.Data.Sqlite.
- **SQLite-specific handling:** WAL-mode assumed; connect-string sanitization (stripping Postgres-only args); statement-splitting for multi-statement migrations; ISO-8601 text or unix-microsecond integers for timestamps; `INTEGER PRIMARY KEY AUTOINCREMENT` in place of `GENERATED AS IDENTITY`; `TEXT`-as-JSON in place of `jsonb`.
- **Test infra:** integration tests use file-backed temp databases or shared-cache `:memory:` — no containers required.
- **Operational pairing:** [[entities/litestream]] is the documented backup-replication path.

## Common Strategies

- [[concepts/dialect-abstraction]]
- [[concepts/no-orm-constraint]]
- [[concepts/sqlite-production-target]]
- [[concepts/postgres-feature-fallbacks]]
- [[concepts/in-process-notification-optimization]]

## Related Entities

- [[entities/dbos-transact]]
- [[entities/dbos-transact-postgres]]
- [[entities/litestream]]
- [[entities/dbos-transact-py]]
