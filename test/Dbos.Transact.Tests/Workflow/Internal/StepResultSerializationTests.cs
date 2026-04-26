using System.Text.Json;
using Dbos.Transact.Json;
using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact.Tests.Workflow.Internal;

/// <summary>
/// Verifies that step output round-trips through the DBOS-16 portable serializer,
/// matching the requirement from DBOS-21: "step result serializes through DBOS-16 round-trip."
/// </summary>
public sealed class StepResultSerializationTests
{
    private static readonly DbosPortableSerializer Serializer = new();

    [Fact]
    public void StringOutput_RoundTrips()
    {
        var original = "hello, world";
        var serialized = Serializer.Serialize(original);
        var stepResult = new StepResult("wf-1", 1, "MyStep", Output: serialized, Serialization: DbosPortableSerializer.SerializerName);

        var deserialized = Serializer.Deserialize(stepResult.Output);

        Assert.Equal(original, ((JsonElement)deserialized!).GetString());
    }

    [Fact]
    public void IntOutput_RoundTrips()
    {
        var serialized = Serializer.Serialize(42);
        var stepResult = new StepResult("wf-1", 2, "MyStep", Output: serialized, Serialization: DbosPortableSerializer.SerializerName);

        var deserialized = Serializer.Deserialize(stepResult.Output);

        Assert.Equal(42, ((JsonElement)deserialized!).GetInt32());
    }

    [Fact]
    public void NullOutput_RoundTrips()
    {
        var serialized = Serializer.Serialize(null);
        var stepResult = new StepResult("wf-1", 3, "MyStep", Output: serialized, Serialization: DbosPortableSerializer.SerializerName);

        var deserialized = Serializer.Deserialize(stepResult.Output);

        Assert.Null(deserialized);
    }

    [Fact]
    public void ObjectOutput_PreservesCamelCaseKeys()
    {
        var serialized = Serializer.Serialize(new { Name = "test", Value = 99 });
        var stepResult = new StepResult("wf-1", 4, "MyStep", Output: serialized, Serialization: DbosPortableSerializer.SerializerName);

        var element = (JsonElement)Serializer.Deserialize(stepResult.Output)!;

        Assert.Equal("test", element.GetProperty("name").GetString());
        Assert.Equal(99, element.GetProperty("value").GetInt32());
    }

    [Fact]
    public void Serialization_SetToPortableJson()
    {
        var stepResult = new StepResult("wf-1", 1, "MyStep",
            Output: Serializer.Serialize("x"),
            Serialization: DbosPortableSerializer.SerializerName);

        Assert.Equal("portable_json", stepResult.Serialization);
    }
}
