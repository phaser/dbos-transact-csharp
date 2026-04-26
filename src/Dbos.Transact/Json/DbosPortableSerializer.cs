using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dbos.Transact.Json;

/// <summary>
/// Cross-runtime JSON serializer that produces output compatible with the Python, TypeScript,
/// and Java DBOS runtimes. Does not embed CLR type information — compatibility depends on the
/// caller using <see cref="ArgumentCoercion"/> to restore typed values on read.
/// Port of Java's <c>DBOSPortableSerializer</c>.
/// </summary>
public sealed class DbosPortableSerializer : IDbosSerializer
{
    public const string SerializerName = "portable_json";

    public static readonly DbosPortableSerializer Instance = new();

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Camel-case matches the Java/Python/TypeScript runtime conventions
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    public string Name => SerializerName;

    public string? Serialize(object? value)
    {
        if (value is null) return null;
        return JsonSerializer.Serialize(ToPortable(value), WriteOptions);
    }

    public object? Deserialize(string? text)
    {
        if (text is null) return null;
        return JsonSerializer.Deserialize<object?>(text, ReadOptions);
    }

    public string? SerializeException(Exception? exception)
    {
        if (exception is null) return null;

        string name;
        object? code = null;
        object? errorData = null;

        if (exception is PortableWorkflowException pwe)
        {
            name = pwe.ErrorName;
            code = pwe.Code;
            errorData = pwe.ErrorPayload;
        }
        else
        {
            name = exception.GetType().Name;
        }

        var wire = new JsonWorkflowErrorData(name, exception.Message, Code: code, Data: errorData);
        return JsonSerializer.Serialize(wire, WriteOptions);
    }

    public Exception? DeserializeException(string? text)
    {
        if (text is null) return null;
        var errorData = JsonSerializer.Deserialize<JsonWorkflowErrorData>(text, ReadOptions);
        if (errorData is null) return null;
        return PortableWorkflowException.FromErrorData(errorData);
    }

    /// <summary>Serialize workflow arguments in portable format.</summary>
    public static string? SerializeArgs(object?[]? positionalArgs, IReadOnlyDictionary<string, object?>? namedArgs)
    {
        var args = new JsonWorkflowArgs(
            PositionalArgs: positionalArgs is not null ? ToPortableArray(positionalArgs) : null,
            NamedArgs: namedArgs is not null ? ToPortableMap(namedArgs) : null);
        return JsonSerializer.Serialize(args, WriteOptions);
    }

    /// <summary>Deserialize workflow arguments from portable format.</summary>
    public static JsonWorkflowArgs? DeserializeArgs(string? text)
    {
        if (text is null) return null;
        return JsonSerializer.Deserialize<JsonWorkflowArgs>(text, ReadOptions);
    }

    /// <summary>
    /// Convert a value to its portable representation. Dates become ISO-8601 strings;
    /// collections are recursed; exceptions become error-data records.
    /// </summary>
    private static object? ToPortable(object? value)
    {
        return value switch
        {
            null => null,
            // Dates → ISO-8601 strings (lingua franca across runtimes)
            DateTime dt => dt.Kind == DateTimeKind.Utc
                ? dt.ToString("o")
                : dt.ToUniversalTime().ToString("o"),
            DateTimeOffset dto => dto.ToString("o"),
            // Arrays
            object?[] array => ToPortableArray(array),
            // Lists and other enumerables
            System.Collections.IEnumerable enumerable when value is not string =>
                ToPortableList(enumerable),
            // Exceptions → error data
            Exception ex => new JsonWorkflowErrorData(ex.GetType().Name, ex.Message),
            // Primitives, records, etc. pass through
            _ => value,
        };
    }

    private static object?[] ToPortableArray(object?[] array)
    {
        var result = new object?[array.Length];
        for (int i = 0; i < array.Length; i++)
            result[i] = ToPortable(array[i]);
        return result;
    }

    private static List<object?> ToPortableList(System.Collections.IEnumerable enumerable)
    {
        var result = new List<object?>();
        foreach (var item in enumerable)
            result.Add(ToPortable(item));
        return result;
    }

    private static Dictionary<string, object?> ToPortableMap(IReadOnlyDictionary<string, object?> map)
    {
        var result = new Dictionary<string, object?>(map.Count);
        foreach (var (key, val) in map)
            result[key] = ToPortable(val);
        return result;
    }
}
