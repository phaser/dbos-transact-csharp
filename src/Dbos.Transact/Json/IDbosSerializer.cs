namespace Dbos.Transact.Json;

/// <summary>
/// Pluggable serializer contract for DBOS. All step outputs, workflow arguments,
/// and error payloads are stored via this interface. Implementations must round-trip
/// any CLR type including primitives, records, collections, and DBOS exceptions.
/// </summary>
public interface IDbosSerializer
{
    /// <summary>Identifies the serialization format stored in the database.</summary>
    string Name { get; }

    /// <summary>Serialize <paramref name="value"/> to a JSON string embedding type metadata.</summary>
    string? Serialize(object? value);

    /// <summary>Deserialize a string produced by <see cref="Serialize"/> back to the original value.</summary>
    object? Deserialize(string? text);

    /// <summary>Serialize an exception for durable storage.</summary>
    string? SerializeException(Exception? exception);

    /// <summary>Deserialize an exception previously stored by <see cref="SerializeException"/>.</summary>
    Exception? DeserializeException(string? text);
}
