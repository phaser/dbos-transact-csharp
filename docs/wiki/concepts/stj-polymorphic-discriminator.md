---
title: "System.Text.Json Polymorphic Discriminator"
type: concept
tags: [csharp, serialization, port-decision, conductor-protocol, json, foundational]
created: 2026-04-25
updated: 2026-04-25
sources: []
confidence: high
---

# System.Text.Json Polymorphic Discriminator

## Definition

The pattern used in the conductor protocol to deserialize a `"type"`-discriminated JSON message
into the correct concrete C# class, replicating Java's Jackson `@JsonTypeInfo` + `@JsonSubTypes`
mechanism using System.Text.Json's `[JsonPolymorphic]` and `[JsonDerivedType]` attributes.

## How It Works

### Java (Jackson)

```java
@JsonTypeInfo(use = Id.NAME, include = As.PROPERTY, property = "type", visible = true)
@JsonSubTypes({
    @JsonSubTypes.Type(value = AlertRequest.class, name = "alert"),
    @JsonSubTypes.Type(value = CancelRequest.class, name = "cancel"),
    // ...
})
public abstract class BaseMessage { ... }
```

`visible = true` means the `"type"` field is written into JSON *and* also set on the deserialized
object (it is "visible" in the POJO).

### C# Port

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", IgnoreUnrecognizedTypeDiscriminators = true)]
[JsonDerivedType(typeof(AlertRequest), "alert")]
[JsonDerivedType(typeof(CancelRequest), "cancel")]
// ...
public abstract class BaseMessage
{
    [JsonIgnore]
    public string? Type { get; protected set; }  // set in each concrete ctor
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
```

Each concrete class sets `Type = MessageType.XYZ.GetValue()` in its constructor, mirroring
Jackson's `visible = true` — the discriminator value is available on the object but not
re-read from JSON (because `[JsonIgnore]` prevents the JSON `"type"` field from writing to it;
the polymorphic mechanism handles the routing before property binding).

### Serialization

When serializing a `BaseMessage` reference holding an `AlertRequest`, STJ writes
`"type":"alert"` at the top level, driven by `[JsonDerivedType]`. The `Type` property
is `[JsonIgnore]`, so it does not produce a duplicate field.

### Deserialization

STJ reads the `"type"` discriminator, looks it up in the `[JsonDerivedType]` table, and
instantiates the correct concrete class. The `Type` property on the resulting object is set
by the concrete constructor, not by JSON binding.

## Key Parameters

- `TypeDiscriminatorPropertyName = "type"` — the JSON property name for the discriminator
- `IgnoreUnrecognizedTypeDiscriminators = true` — unknown discriminator values do not throw
  a discriminator-specific error (but see Risks below)
- `[JsonIgnore]` on `Type` — prevents double-writing the discriminator in serialized output
  and prevents STJ from overwriting the constructor-set value during deserialization

## When To Use

- Porting Java code that uses `@JsonTypeInfo(visible = true)` to C#
- Any WebSocket or HTTP protocol with a tagged-union message envelope (e.g., the DBOS conductor
  protocol's 29 request types and 18 response types)
- Wherever a single deserialization target type (e.g., `BaseMessage`) dispatches to many concrete
  subtypes based on a JSON field value

## Risks & Pitfalls

### `IgnoreUnrecognizedTypeDiscriminators` does NOT return null for abstract base types

When an unrecognized discriminator value is encountered and `IgnoreUnrecognizedTypeDiscriminators = true`,
STJ skips the discriminator error but then tries to instantiate the base type. If the base type
is `abstract`, STJ throws `NotSupportedException: "The JSON payload for polymorphic interface or
abstract type must specify a type discriminator."` The error message is misleading — the discriminator
*was* present, but unknown, and the fallback of deserializing as the abstract base type is impossible.

**Consequence**: Receiving a message with an unrecognized `"type"` string will throw, not silently
return `null`. Handle this at the caller by catching `NotSupportedException` or `JsonException`.

### CA1716: Reserved keyword conflicts in nested types

C# reserved keywords (`step`, `event`, `delegate`, etc.) conflict with class names when used as
nested types (e.g., `ListStepsResponse.Step` triggers CA1716). Rename to `StepEntry`, `StepRecord`,
or prefix with the outer class name. This is a pure C# naming concern not present in Java.

## Related Concepts

- [[concepts/csharp-record-validation]] — other C# vs Java serialization/type-system differences
- [[concepts/portable-serializer]] — the separate portable (cross-runtime) serialization layer

## Sources

Empirical — discovered during DBOS-15 conductor protocol DTO implementation (PR #35). No external
documentation consulted beyond .NET SDK reference for `[JsonPolymorphic]`.
