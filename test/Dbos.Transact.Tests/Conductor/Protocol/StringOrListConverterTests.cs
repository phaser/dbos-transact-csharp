using System.Text.Json;
using Dbos.Transact.Conductor.Protocol;

namespace Dbos.Transact.Tests.Conductor.Protocol;

public class StringOrListConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new StringOrListConverter() }
    };

    [Fact]
    public void Read_NullJson_ReturnsNull()
    {
        var result = JsonSerializer.Deserialize<List<string>?>("null", Options);
        Assert.Null(result);
    }

    [Fact]
    public void Read_ScalarString_ReturnsSingleElementList()
    {
        var result = JsonSerializer.Deserialize<List<string>?>("\"hello\"", Options);
        Assert.Equal(["hello"], result);
    }

    [Fact]
    public void Read_Array_ReturnsList()
    {
        var result = JsonSerializer.Deserialize<List<string>?>("""["a","b","c"]""", Options);
        Assert.Equal(["a", "b", "c"], result);
    }

    [Fact]
    public void Read_EmptyArray_ReturnsEmptyList()
    {
        var result = JsonSerializer.Deserialize<List<string>?>("[]", Options);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Read_InvalidToken_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<List<string>?>("42", Options));
    }

    [Fact]
    public void Write_NullValue_WritesNull()
    {
        List<string>? value = null;
        var json = JsonSerializer.Serialize(value, Options);
        Assert.Equal("null", json);
    }

    [Fact]
    public void Write_List_WritesJsonArray()
    {
        var value = new List<string> { "x", "y" };
        var json = JsonSerializer.Serialize(value, Options);
        Assert.Equal("""["x","y"]""", json);
    }

    [Fact]
    public void Roundtrip_ScalarThenWrite_ProducesArray()
    {
        var deserialized = JsonSerializer.Deserialize<List<string>?>("\"single\"", Options);
        var serialized = JsonSerializer.Serialize(deserialized, Options);
        Assert.Equal("""["single"]""", serialized);
    }
}
