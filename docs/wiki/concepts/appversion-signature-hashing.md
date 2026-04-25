---
title: "AppVersionComputer: Signature Hashing vs Bytecode Hashing"
type: concept
tags: [csharp, port-decision, versioning, foundational]
created: 2026-04-25
updated: 2026-04-25
sources: []
confidence: high
---

# AppVersionComputer: Signature Hashing vs Bytecode Hashing

## Definition

`AppVersionComputer` computes a deterministic SHA-256 hash over registered workflow methods to detect whether the application has changed between executor restarts. The Java version hashes JVM bytecode; the C# port hashes method signatures.

## How It Works

### Java: Bytecode hashing (via ASM)

Java uses the ASM library to read the compiled `.class` file for each workflow method and hash every instruction opcode, operand, label ordinal, and type reference. This produces a hash that changes whenever the **implementation** of a workflow method changes, even if the signature stays the same.

### C# port: Signature hashing

```csharp
// Hash: dbosVersion + sorted (FQN + method descriptor) per method
// FQN  = {DeclaringType.FullName}//{MethodName}  (or @WorkflowClassName value)
// Desc = ({ParamType1,...})->{ReturnType}
```

The C# port uses `System.Security.Cryptography.SHA256` to hash:
1. The DBOS runtime version string.
2. For each workflow method (sorted by FQN for order-independence):
   - The fully-qualified workflow name.
   - The method descriptor (parameter and return types).

## Key Parameters

| Property | Java (ASM bytecode) | C# (signature) |
|---|---|---|
| Detects signature change | ✓ | ✓ |
| Detects implementation change | ✓ | ✗ |
| Requires bytecode access | ✓ | ✗ |
| IL inspection complexity | Low (JVM class files) | High (IL + reflection) |
| Order-independent | ✓ (sorts by FQN) | ✓ (sorts by FQN) |

## When To Use

The signature-hashing approach is adequate for the primary use case: detecting accidentally renamed or removed workflow functions between deployments. If the project ever requires detecting implementation-level changes (e.g., workflow body was refactored without changing the signature), the hash can be extended using `MethodBase.GetMethodBody()` IL bytes — but this adds complexity and is not needed for the initial port.

## Risks & Pitfalls

- **False negatives on implementation changes**: If a workflow body changes but the signature stays the same, the version hash does NOT change. The executor will not detect the non-determinism at startup (it would surface at replay time as a `DbosUnexpectedStepException`). Document this limitation.
- **`@WorkflowClassName` affects the hash**: If a class carries `[WorkflowClassName("X")]`, the FQN uses `"X"` instead of the runtime type name. Changing the attribute value changes the hash even if no methods changed.
- **Nullable return types**: `method.ReturnType.FullName` may be `null` for generic types; the implementation falls back to `Name`. Verify with generic workflow methods when they are implemented.

## Related Concepts

- [[concepts/durable-workflow]] — versioning prevents replaying a changed workflow against stale checkpoints
- [[concepts/method-interception]] — interception discovers workflow methods that feed into the version hash

## Sources

Observed during DBOS-05 port (2026-04-25). Java source: `transact/src/main/java/dev/dbos/transact/internal/AppVersionComputer.java`.
