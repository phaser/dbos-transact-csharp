---
title: "dbos-transact-py"
type: entity
tags: [upstream, python, reference-implementation]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
---

## Overview

`dbos-transact-py` ([github.com/dbos-inc/dbos-transact-py](https://github.com/dbos-inc/dbos-transact-py)) is the Python DBOS runtime. The C# port takes this as the primary reference for the dual-dialect pattern — `_sys_db.py` base class + `_sys_db_postgres.py` / `_sys_db_sqlite.py` subclasses is exactly the structure replicated as `SystemDatabase` + `PostgresSystemDatabase` / `SqliteSystemDatabase`.

## Characteristics

- **Dual-dialect first.** Postgres and SQLite are both production-supported in the Python runtime; the split lives inside the system-database layer via inheritance.
- **API shape.** Python decorators `@workflow`, `@step`, `@scheduled` — the C# attribute names mirror these directly.
- **Portable serializer.** Python emits golden-fixture payloads used to validate round-tripping in the C# implementation — see [[concepts/portable-serializer]].

## Common Strategies

- [[concepts/dialect-abstraction]] (primary pattern source)
- [[concepts/sqlite-production-target]]
- [[concepts/portable-serializer]]

## Related Entities

- [[entities/dbos-transact-java]]
- [[entities/dbos-transact-ts]]
