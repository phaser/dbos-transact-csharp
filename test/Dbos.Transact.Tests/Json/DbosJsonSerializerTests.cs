using Dbos.Transact.Json;

namespace Dbos.Transact.Tests.Json;

public class DbosJsonSerializerTests
{
    private static readonly DbosJsonSerializer Sut = DbosJsonSerializer.Instance;

    private sealed record TestPoint(int X, int Y);

    [Fact]
    public void Serialize_Null_ReturnsNull()
    {
        Assert.Null(Sut.Serialize(null));
    }

    [Fact]
    public void Deserialize_Null_ReturnsNull()
    {
        Assert.Null(Sut.Deserialize(null));
    }

    [Fact]
    public void RoundTrip_String()
    {
        var result = Sut.Deserialize(Sut.Serialize("hello"));
        Assert.Equal("hello", result);
    }

    [Fact]
    public void RoundTrip_Int32()
    {
        var result = Sut.Deserialize(Sut.Serialize(42));
        Assert.Equal(42, result);
    }

    [Fact]
    public void RoundTrip_Double()
    {
        var result = Sut.Deserialize(Sut.Serialize(3.14));
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void RoundTrip_Bool()
    {
        Assert.Equal(true, Sut.Deserialize(Sut.Serialize(true)));
        Assert.Equal(false, Sut.Deserialize(Sut.Serialize(false)));
    }

    [Fact]
    public void RoundTrip_Record()
    {
        var point = new TestPoint(3, 7);
        var result = Sut.Deserialize(Sut.Serialize(point));
        Assert.Equal(point, result);
    }

    [Fact]
    public void RoundTrip_List()
    {
        var list = new List<string> { "a", "b", "c" };
        var result = Sut.Deserialize(Sut.Serialize(list));
        Assert.Equal(list, result);
    }

    [Fact]
    public void RoundTrip_NestedRecord()
    {
        var nested = new List<TestPoint> { new(1, 2), new(3, 4) };
        var result = Sut.Deserialize(Sut.Serialize(nested));
        Assert.Equal(nested, result);
    }

    [Fact]
    public void RoundTrip_ObjectArray_PreservesElementTypes()
    {
        var args = new object?[] { 42, "hello", 3.14, true, null };
        var result = (object?[]?)Sut.Deserialize(Sut.Serialize(args));
        Assert.NotNull(result);
        Assert.Equal(5, result.Length);
        Assert.Equal(42, result[0]);
        Assert.Equal("hello", result[1]);
        Assert.Equal(3.14, result[2]);
        Assert.Equal(true, result[3]);
        Assert.Null(result[4]);
        Assert.IsType<int>(result[0]);
        Assert.IsType<double>(result[2]);
    }

    [Fact]
    public void SerializedText_ContainsTypeName()
    {
        var json = Sut.Serialize(new TestPoint(0, 0));
        Assert.NotNull(json);
        Assert.Contains("DbosJsonSerializerTests", json);
    }

    [Fact]
    public void Name_IsExpectedValue()
    {
        Assert.Equal(DbosJsonSerializer.SerializerName, Sut.Name);
        Assert.Equal("csharp_stj", Sut.Name);
    }

    [Fact]
    public void SerializeException_Null_ReturnsNull()
    {
        Assert.Null(Sut.SerializeException(null));
    }

    [Fact]
    public void DeserializeException_Null_ReturnsNull()
    {
        Assert.Null(Sut.DeserializeException(null));
    }

    [Fact]
    public void SerializeDeserializeException_PreservesMessage()
    {
        var original = new ArgumentException("bad argument");
        var text = Sut.SerializeException(original);
        Assert.NotNull(text);
        var restored = Sut.DeserializeException(text);
        Assert.NotNull(restored);
        Assert.Equal(original.Message, restored.Message);
    }

    [Fact]
    public void SerializeDeserializeException_PreservesInnerException()
    {
        var inner = new InvalidOperationException("inner msg");
        var outer = new ArgumentException("outer msg", inner);
        var restored = Sut.DeserializeException(Sut.SerializeException(outer)!);
        Assert.NotNull(restored);
        Assert.NotNull(restored.InnerException);
        Assert.Equal("inner msg", restored.InnerException!.Message);
    }

    [Fact]
    public void SerializeDeserializeException_NeverThrows()
    {
        // Any exception type should serialize/deserialize without throwing,
        // falling back to InvalidOperationException if the type cannot be reconstructed.
        var original = new InvalidOperationException("app error");
        var text = Sut.SerializeException(original);
        var restored = Sut.DeserializeException(text!);
        Assert.NotNull(restored);
    }
}
