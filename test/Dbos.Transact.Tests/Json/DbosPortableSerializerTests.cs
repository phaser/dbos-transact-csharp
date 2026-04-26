using System.Text.Json;
using Dbos.Transact.Json;

namespace Dbos.Transact.Tests.Json;

public sealed class DbosPortableSerializerTests
{
    private static readonly DbosPortableSerializer Sut = DbosPortableSerializer.Instance;

    // ── Basic contract ────────────────────────────────────────────────────────

    [Fact]
    public void Name_IsPortableJson() => Assert.Equal("portable_json", Sut.Name);

    [Fact]
    public void Serialize_Null_ReturnsNull() => Assert.Null(Sut.Serialize(null));

    [Fact]
    public void Deserialize_Null_ReturnsNull() => Assert.Null(Sut.Deserialize(null));

    // ── Primitives (no type envelope) ─────────────────────────────────────────

    [Fact]
    public void Serialize_String_ProducesPlainJson()
    {
        var json = Sut.Serialize("hello");
        Assert.Equal("\"hello\"", json);
    }

    [Fact]
    public void Serialize_Int_ProducesPlainJson()
    {
        var json = Sut.Serialize(42);
        Assert.Equal("42", json);
    }

    [Fact]
    public void Serialize_Bool_ProducesPlainJson()
    {
        Assert.Equal("true", Sut.Serialize(true));
        Assert.Equal("false", Sut.Serialize(false));
    }

    [Fact]
    public void Serialize_NoTypeEnvelope()
    {
        // Portable serializer must NOT embed CLR type info ("t"/"v" envelope used by DbosJsonSerializer)
        var json = Sut.Serialize("any string");
        Assert.DoesNotContain("\"t\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"v\":", json, StringComparison.Ordinal);
    }

    // ── DateTime → ISO-8601 ───────────────────────────────────────────────────

    [Fact]
    public void Serialize_UtcDateTime_ProducesIso8601String()
    {
        var dt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var json = Sut.Serialize(dt);
        Assert.Equal("\"2024-06-15T12:00:00.0000000Z\"", json);
    }

    [Fact]
    public void Serialize_DateTimeOffset_ProducesIso8601String()
    {
        var dto = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var json = Sut.Serialize(dto);
        Assert.Contains("2024-06-15", json, StringComparison.Ordinal);
    }

    // ── Collections ───────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_Array_ProducesJsonArray()
    {
        var json = Sut.Serialize(new object?[] { 1, "two", null });
        Assert.Equal("[1,\"two\",null]", json);
    }

    [Fact]
    public void Serialize_List_ProducesJsonArray()
    {
        var json = Sut.Serialize(new List<int> { 1, 2, 3 });
        Assert.Equal("[1,2,3]", json);
    }

    // ── Exception serialization ───────────────────────────────────────────────

    [Fact]
    public void SerializeException_Null_ReturnsNull() =>
        Assert.Null(Sut.SerializeException(null));

    [Fact]
    public void DeserializeException_Null_ReturnsNull() =>
        Assert.Null(Sut.DeserializeException(null));

    [Fact]
    public void SerializeException_RegularException_ProducesErrorDataJson()
    {
        var ex = new InvalidOperationException("something broke");
        var json = Sut.SerializeException(ex);
        Assert.NotNull(json);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("InvalidOperationException", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("something broke", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void SerializeException_PortableWorkflowException_UsesErrorName()
    {
        var ex = new PortableWorkflowException("MyError", "oops", code: 404);
        var json = Sut.SerializeException(ex);
        Assert.NotNull(json);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("MyError", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void DeserializeException_ReturnsPortableWorkflowException()
    {
        var original = new PortableWorkflowException("TestError", "test message", code: 7);
        var json = Sut.SerializeException(original)!;

        var restored = Sut.DeserializeException(json);

        var pwe = Assert.IsType<PortableWorkflowException>(restored);
        Assert.Equal("TestError", pwe.ErrorName);
        Assert.Equal("test message", pwe.Message);
    }

    // ── Args serialization ────────────────────────────────────────────────────

    [Fact]
    public void SerializeArgs_Null_ReturnsJsonWithNulls()
    {
        var json = DbosPortableSerializer.SerializeArgs(null, null);
        Assert.NotNull(json);
    }

    [Fact]
    public void SerializeDeserializeArgs_RoundTrips()
    {
        var args = new object?[] { "hello", 42 };
        var json = DbosPortableSerializer.SerializeArgs(args, null)!;

        var restored = DbosPortableSerializer.DeserializeArgs(json);

        Assert.NotNull(restored);
        Assert.NotNull(restored.PositionalArgs);
        Assert.Equal(2, restored.PositionalArgs.Length);
    }

    [Fact]
    public void DeserializeArgs_Null_ReturnsNull() =>
        Assert.Null(DbosPortableSerializer.DeserializeArgs(null));

    // ── Interop wire format ───────────────────────────────────────────────────

    [Fact]
    public void Deserialize_JavaEmittedInt_ReturnsValue()
    {
        // Simulates a value emitted by the Java portable serializer (plain JSON, no type wrapper)
        var restored = Sut.Deserialize("42");
        // STJ returns JsonElement for object; coercion is caller's responsibility
        Assert.NotNull(restored);
    }

    [Fact]
    public void Deserialize_JavaEmittedString_ReturnsValue()
    {
        var restored = Sut.Deserialize("\"hello from java\"");
        Assert.NotNull(restored);
    }

    [Fact]
    public void ExceptionWireFormat_CompatibleWithJava()
    {
        // Java serializeThrowable produces {"name":"...","message":"..."}
        // Fields match JsonWorkflowErrorData's camelCase JSON property names (STJ web options)
        var json = "{\"name\":\"CustomError\",\"message\":\"from java\"}";
        var ex = Sut.DeserializeException(json);

        var pwe = Assert.IsType<PortableWorkflowException>(ex);
        Assert.Equal("CustomError", pwe.ErrorName);
        Assert.Equal("from java", pwe.Message);
    }
}
