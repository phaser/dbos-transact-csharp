---
title: "C# Record Validation Patterns"
type: concept
tags: [csharp, records, validation, port-decision, foundational]
created: 2026-04-25
updated: 2026-04-25
sources: []
confidence: high
---

# C# Record Validation Patterns

## Definition

C# positional records expose three syntactically plausible locations for constructor-time validation, but only two of them also cover `with` expressions. Choosing the wrong pattern causes invariants to be silently bypassed whenever a caller clones a record with a mutated field.

## How It Works

### Pattern A — Compact Constructor (unsupported in .NET 10.0.202 SDK)

```csharp
public sealed record Foo(string Bar)
{
    public Foo  // This is NOT a compact constructor body — it is parsed as a
    {           // malformed property declaration in SDK 10.0.202.
        if (string.IsNullOrEmpty(Bar)) throw ...;
    }
}
```

Errors produced: `CS1001 Identifier expected`, `CS1014 A get or set accessor expected`.  
**Status: do not use.** The compact constructor syntax was introduced in C# 10 but is not recognised by the .NET 10.0.202 toolchain (confirmed by minimal reproduction).

### Pattern B — Property Initializer (construction only)

```csharp
public sealed record Foo(string Bar)
{
    public string Bar { get; init; } = string.IsNullOrEmpty(Bar)
        ? throw new ArgumentException("Bar must not be empty.", nameof(Bar))
        : Bar;
}
```

The initializer expression runs as part of the synthesized primary constructor, where the positional parameter `Bar` is in scope. It does **not** run when `with { Bar = "" }` is used — the auto-generated `init` setter assigns directly to the backing field without re-running the initializer.

**Use when:** the field is only ever set during initial construction and callers are not expected to produce invalid values via `with`.

### Pattern C — Backing Field + init Accessor (construction and `with`)

```csharp
public sealed record Foo(string Bar)
{
    // Field initializer consumes the positional parameter (satisfies CS8907)
    // and validates during primary constructor execution.
    private string _bar = string.IsNullOrEmpty(Bar)
        ? throw new ArgumentException("Bar must not be empty.", nameof(Bar))
        : Bar;

    // Custom init accessor validates the same rule on 'with' expressions.
    public string Bar
    {
        get => _bar;
        init => _bar = string.IsNullOrEmpty(value)
            ? throw new ArgumentException("Bar must not be empty.", nameof(Bar))
            : value;
    }
}
```

The field initializer references the positional parameter (required to avoid CS8907 "Parameter is unread"). The `init` accessor runs on both `new Foo(...)` (primary constructor calls `this.Bar = Bar`, which invokes `init`) and `x with { Bar = "" }` (which also invokes `init`).

**Use when:** callers may mutate the field via `with` and the invariant must hold in all copies.

## Key Parameters

| Scenario | Pattern B sufficient? | Pattern C needed? |
|---|---|---|
| Validation on `new` only | Yes | Overkill |
| Validation on `new` and `with` | No | Yes |
| Normalization (e.g. `MaxAttempts < 1 → 1`) | Yes | If `with` must normalise too |

## When To Use

Apply Pattern C to any record field where:
- The field can be targeted by a `with` expression, **and**
- There is an invariant or normalisation that must hold on all copies (e.g. non-empty string, positive integer, enum range).

In this project, Pattern C is used for `ConductorKey`, `ConductorDomain`, `AppVersion`, and `ExecutorId` in [[entities/dbos-transact]] `DbosOptions`.

## Risks & Pitfalls

- **CS8907 "Parameter is unread"** — raised when a positional record property is overridden with a custom backing field but the field initializer does not reference the positional parameter. Always include a field initializer that uses the positional parameter.
- **Double-validation on construction** — Pattern C runs validation in both the field initializer and the `init` accessor during primary constructor execution. This is harmless but worth knowing.
- **Property initializer scope** — inside a field initializer for a positional record, bare identifiers (e.g. `Bar`) refer to the positional parameter, not `this.Bar`. This is a record-specific scoping rule; it does not apply in normal class constructors.
- **`ListenQueues` (IReadOnlySet) equality** — C# records compare properties using `EqualityComparer<T>.Default`, which is reference equality for interface types. A `HashSet<string>` used as `IReadOnlySet<string>` breaks record equality across `Defaults()` calls. Fix: override `Equals(T? other)` and `GetHashCode()` to use `SetEquals` and per-element hashing. The same issue applies to `IReadOnlyDictionary` and array fields (`string[]`, `object[]`).

## Related Concepts

- [[concepts/portable-serializer]] — uses similar record types for cross-runtime data shapes
- [[concepts/durable-workflow]] — workflow state records must maintain invariants across checkpoints

## Sources

Discovered empirically during DBOS-02 implementation (PR #32, 2026-04-25). No raw document; the finding arose from test failures on `with` expression cases.
