# dbos-transact-csharp

A C#/.NET port of [dbos-transact-java](https://github.com/dbos-inc/dbos-transact-java) — a lightweight durable-workflow library built on top of a relational database.

> **Status:** pre-implementation. The design is captured in [`docs/raw/design.md`](docs/raw/design.md); no source code has been committed yet.

## What DBOS Transact gives you

- **Durable workflows** checkpointed to the database, with automatic resume-on-restart.
- **Durable queues** with no external broker.
- **Scheduled execution** — cron and long durable sleeps.
- **Workflow events and notifications** with exactly-once delivery.
- **Async workflow handles** with status polling and result retrieval.
- **Admin HTTP endpoints** and **conductor (WebSocket) protocol** parity with the other DBOS runtimes.

## Planned package layout

| NuGet | Role |
|---|---|
| `Dbos.Transact` | Core — dialect-agnostic. Public `[Workflow]` / `[Step]` / `[Scheduled]` surface, executor, registries, portable serializer, migrations. |
| `Dbos.Transact.Postgres` | Npgsql-backed Postgres dialect. `LISTEN`/`NOTIFY`, `SKIP LOCKED`, advisory locks. |
| `Dbos.Transact.Sqlite` | Microsoft.Data.Sqlite-backed dialect. First-class production target for small single-host projects, with Litestream as the documented backup path. |
| `Dbos.Transact.Hosting` | `Microsoft.Extensions.Hosting` integration — `services.AddDbos(…)` + `AddDbosWorkflow<TInterface, TImpl>()`. |
| `Dbos.Transact.Cli` | `System.CommandLine`-based CLI (`migrate`, `reset`, `workflow`, `postgres`). |

## Target framework

`net10.0` — current LTS (released November 2025). `net8.0` multi-targeting can be added later if there is pull from users on the previous LTS.

## Documentation

- **Design document:** [`docs/raw/design.md`](docs/raw/design.md) — authoritative source for v1 scope, layout, and decisions.
- **Knowledge base:** [`docs/wiki/`](docs/wiki/) — LLM-maintained concept, entity, and summary pages. Start at [`docs/wiki/index.md`](docs/wiki/index.md). Schema in [`docs/CLAUDE.md`](docs/CLAUDE.md).
- **Agent instructions:** [`CLAUDE.md`](CLAUDE.md) — coding conventions, test layout, and knowledge-management protocol for LLM-assisted work on this repo.

## Upstream references

- [dbos-transact-java](https://github.com/dbos-inc/dbos-transact-java) — primary reference implementation.
- [dbos-transact-py](https://github.com/dbos-inc/dbos-transact-py) — reference for the dual-dialect pattern.
- [dbos-transact-ts](https://github.com/dbos-inc/dbos-transact-ts) — reference for the public API shape.

## License

[MIT](LICENSE)
