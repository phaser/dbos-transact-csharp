using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

/// <summary>
/// Deserializes a JSON value that can be either a plain string or an array of strings
/// into <c>List&lt;string&gt;</c>. Mirrors Jackson's <c>StringOrListDeserializer</c>.
/// </summary>
public sealed class StringOrListConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        if (reader.TokenType == JsonTokenType.String)
            return [reader.GetString()!];

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                list.Add(reader.GetString()!);
            return list;
        }

        throw new JsonException($"Expected string or array, got {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        writer.WriteStartArray();
        foreach (var s in value) writer.WriteStringValue(s);
        writer.WriteEndArray();
    }
}
