---
title: "Dbos.Transact.Cli"
type: entity
tags: [project, cli, tooling]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
---

## Overview

`Dbos.Transact.Cli` is the standalone command-line tool that mirrors the Java `transact-cli` surface on .NET. Built on `System.CommandLine` (in place of picocli), publishable as a dotnet global tool (`dotnet tool install -g dbos-cli`).

## Characteristics

- **Assembly / namespace / NuGet ID:** `Dbos.Transact.Cli`.
- **Subcommands:** `migrate`, `reset`, `workflow`, `postgres` — same surface as the Java CLI.
- **Entry point:** `Program.cs` using `System.CommandLine` parsing.
- **Layout:** `Commands/` (one file per subcommand) + `DatabaseOptions.cs` for shared connection/dialect options.
- **Distribution:** standalone console app *and* packable as a dotnet tool.

## Common Strategies

- (mostly operational tooling — touches migrations and workflow admin via the library)

## Related Entities

- [[entities/dbos-transact]]
- [[entities/dbos-transact-java]]
