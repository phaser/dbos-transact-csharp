---
title: "Portable Serializer"
type: concept
tags: [core, serialization, cross-runtime-interop]
created: 2026-04-24
updated: 2026-04-24
sources: ["raw/design.md"]
confidence: high
---

## Definition

`DbosPortableSerializer` is the cross-runtime JSON serializer that round-trips workflow arguments, step outputs, and portable exceptions with the Python, TypeScript, and Java DBOS runtimes against the shared system-table schema. It is the delicate interop surface: every value that is persisted to or read from the system database passes through it, and wire compatibility is a correctness requirement, not a nice-to-have.

## How It Works

- Backed by `System.Text.Json`. Polymorphic args and error payloads use `[JsonPolymorphic]` / `[JsonDerivedType]` — the STJ equivalent of the Jackson `JsonTypeInfo` the Java runtime uses.
- A default internal serializer `DbosJsonSerializer` handles same-runtime persistence. `DbosPortableSerializer` sits alongside it for cross-runtime payloads.
- `Boxed`, `JsonWorkflowArgs`, `PortableWorkflowException`, and `ArgumentCoercion` cover the shape normalization needed so that a Python-emitted payload deserializes into an equivalent C# object tree (and vice versa).
- Correctness is validated via **interop golden-file tests** — fixtures emitted by the Python / TypeScript / Java runtimes are asserted to round-trip cleanly under the C# implementation. See [[concepts/durable-workflow]] §Risks for the replay-consistency implication.

## Key Parameters

- **Supported-type envelope** — the intersection of types the Python/TS/Java runtimes already handle. Exotic types (arbitrary byte streams, language-specific records, custom date/time shapes) are out of scope for v1.
- **Polymorphism discriminator compatibility** — type-tag field names and values must match the other runtimes' conventions.
- **Exception shape parity** — `PortableWorkflowException` carries `class_name`, `message`, and a portable structured cause, matching upstream naming.

## When To Use

Any payload that crosses the system-table boundary: workflow args at start, step outputs on checkpoint, notifications/events, `WorkflowEvent` / `WorkflowStream` bodies, and error captures. Do not use `System.Text.Json` directly for these — the non-portable serializer lacks the coercion pass and golden-fixture coverage.

## Risks & Pitfalls

- **Numeric semantics drift.** JS `number` is IEEE 754 double; Python `int` is unbounded. Round-tripping large integers without string-coercing can silently change value.
- **DateTime representation.** ISO-8601 text string is the lingua franca; `DateTime.Kind` and offset preservation are the usual failure modes.
- **Null vs. missing.** STJ options around `JsonIgnoreCondition.WhenWritingNull` have to match the upstream runtimes' field-presence conventions or replays diverge.
- **Fixture drift.** Upstream runtimes evolve; golden fixtures must be refreshed deliberately and reviewed, not silently regenerated.
- **Type shape changes are migrations.** Changing a serialized record's field set breaks replay of in-flight workflows — handle like a schema migration, not a refactor.

## Related Concepts

- [[concepts/durable-workflow]]

## Related Entities

- [[entities/dbos-transact-java]]
- [[entities/dbos-transact-py]]
- [[entities/dbos-transact-ts]]

## Sources

- `raw/design.md` — §Key design decisions → Serialization; §Testing strategy (interop golden tests); §Repo layout (`Json/` folder)
