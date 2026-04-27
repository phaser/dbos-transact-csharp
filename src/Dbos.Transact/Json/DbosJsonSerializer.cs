using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dbos.Transact.Json;

/// <summary>
/// C#-native DBOS serializer backed by System.Text.Json. Embeds the CLR type name in a
/// <c>{"t":"TypeName, Assembly","v":{...}}</c> envelope so that <see cref="Deserialize"/> can
/// reconstruct the original type without an out-of-band type hint.
/// </summary>
public sealed class DbosJsonSerializer : IDbosSerializer
{
    public const string SerializerName = "csharp_stj";

    public static readonly DbosJsonSerializer Instance = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Name => SerializerName;

    public string? Serialize(object? value)
    {
        if (value is null) return null;
        var type = value.GetType();
        var typeName = $"{type.FullName}, {type.Assembly.GetName().Name}";

        // object?[] elements lose their CLR types on STJ round-trip unless each element
        // carries its own type envelope. Workflow args are always stored as object?[].
        if (value is object?[] argArray)
        {
            var perElemEnvelopes = argArray
                .Select(elem => elem is null ? (TypeEnvelope?)null : MakeEnvelope(elem))
                .ToArray();
            var arrayElement = JsonSerializer.SerializeToElement<TypeEnvelope?[]>(perElemEnvelopes, Options);
            return JsonSerializer.Serialize(new TypeEnvelope(typeName, arrayElement), Options);
        }

        var valueElement = JsonSerializer.SerializeToElement(value, type, Options);
        return JsonSerializer.Serialize(new TypeEnvelope(typeName, valueElement), Options);
    }

    public object? Deserialize(string? text)
    {
        if (text is null) return null;
        var envelope = JsonSerializer.Deserialize<TypeEnvelope>(text, Options)
            ?? throw new InvalidOperationException("Failed to parse serialization envelope.");
        var type = Type.GetType(envelope.T)
            ?? throw new InvalidOperationException($"Cannot resolve type: {envelope.T}");

        // Reconstruct object?[] by decoding each per-element envelope
        if (type == typeof(object[]))
        {
            var perElemEnvelopes = JsonSerializer.Deserialize<TypeEnvelope?[]>(envelope.V.GetRawText(), Options) ?? [];
            return perElemEnvelopes.Select(e => e is null ? null : DecodeEnvelope(e)).ToArray();
        }

        return JsonSerializer.Deserialize(envelope.V.GetRawText(), type, Options);
    }

    private static TypeEnvelope MakeEnvelope(object value)
    {
        var type = value.GetType();
        var typeName = $"{type.FullName}, {type.Assembly.GetName().Name}";
        return new TypeEnvelope(typeName, JsonSerializer.SerializeToElement(value, type, Options));
    }

    private static object? DecodeEnvelope(TypeEnvelope envelope)
    {
        var type = Type.GetType(envelope.T)
            ?? throw new InvalidOperationException($"Cannot resolve type: {envelope.T}");
        return JsonSerializer.Deserialize(envelope.V.GetRawText(), type, Options);
    }

    public string? SerializeException(Exception? exception)
    {
        if (exception is null) return null;
        return JsonSerializer.Serialize(ExceptionWire.FromException(exception), Options);
    }

    public Exception? DeserializeException(string? text)
    {
        if (text is null) return null;
        var wire = JsonSerializer.Deserialize<ExceptionWire>(text, Options)
            ?? throw new InvalidOperationException("Failed to parse exception wire format.");
        return wire.ToException();
    }

    private sealed record TypeEnvelope(string T, JsonElement V);

    private sealed record ExceptionWire(
        string Type,
        string? Message,
        string? StackTrace,
        ExceptionWire? Inner)
    {
        public static ExceptionWire FromException(Exception ex) =>
            new(
                Type: ex.GetType().AssemblyQualifiedName ?? ex.GetType().FullName ?? ex.GetType().Name,
                Message: ex.Message,
                StackTrace: ex.StackTrace,
                Inner: ex.InnerException is null ? null : FromException(ex.InnerException));

        public Exception ToException()
        {
            var inner = Inner?.ToException();
            var clrType = System.Type.GetType(Type);
            if (clrType is not null && typeof(Exception).IsAssignableFrom(clrType))
            {
                try
                {
                    var ex = inner is null
                        ? Activator.CreateInstance(clrType, Message) as Exception
                        : Activator.CreateInstance(clrType, Message, inner) as Exception;
                    if (ex is not null) return ex;
                }
                catch (Exception) { }
            }
            return new InvalidOperationException($"[{Type}] {Message}", inner);
        }
    }
}
