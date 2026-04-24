---
title: "Litestream"
type: entity
tags: [operations, dialect-sqlite, backup]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
---

## Overview

[Litestream](https://litestream.io/) is a streaming SQLite replication tool: a background process tails the SQLite WAL and ships frames to object storage (S3, etc.) continuously. The design doc calls it out as the documented backup path for `Dbos.Transact.Sqlite` deployments — continuous S3 replication gives a small-project deployment a durability story comparable to managed Postgres.

## Characteristics

- **Out-of-process.** Runs as a sidecar or daemon alongside the application, not linked into the runtime.
- **WAL-tail replication.** Continuous, not periodic snapshots; recovery is point-in-time.
- **Object-storage targets.** S3 and compatibles, plus SFTP and local paths; no DBaaS dependency.
- **Operator concern, not library concern.** `Dbos.Transact.Sqlite` doesn't ship Litestream integration code; it documents the recommended operational posture.

## Common Strategies

- [[concepts/sqlite-production-target]]

## Related Entities

- [[entities/dbos-transact-sqlite]]
