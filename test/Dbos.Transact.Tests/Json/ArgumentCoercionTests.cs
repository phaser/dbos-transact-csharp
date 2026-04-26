using System.Reflection;
using Dbos.Transact.Json;

namespace Dbos.Transact.Tests.Json;

public sealed class ArgumentCoercionTests
{
    // Target methods used as MethodInfo fixtures
    private static void MethodWithInt(int x) { }
    private static void MethodWithString(string s) { }
    private static void MethodWithRecord(CoercionTestPoint pt) { }
    private static void MethodWithNullable(int? n) { }
    private static void MethodWithNoParams() { }

    private static MethodInfo Method(string name) =>
        typeof(ArgumentCoercionTests).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    [Fact]
    public void Coerce_CompatibleType_PassesThrough()
    {
        var result = ArgumentCoercion.Coerce(["hello"], Method(nameof(MethodWithString)));
        Assert.Equal("hello", result[0]);
    }

    [Fact]
    public void Coerce_NullArg_PassesThrough()
    {
        var result = ArgumentCoercion.Coerce([null], Method(nameof(MethodWithString)));
        Assert.Null(result[0]);
    }

    [Fact]
    public void Coerce_IncompatibleType_ConvertsViaJson()
    {
        // STJ JsonElement (from deserialized JSON) can be coerced to int
        var json = System.Text.Json.JsonSerializer.Deserialize<object>("42")!;
        var result = ArgumentCoercion.Coerce([json], Method(nameof(MethodWithInt)));
        Assert.Equal(42, result[0]);
    }

    [Fact]
    public void Coerce_AnonymousRecord_ToTypedRecord()
    {
        var json = System.Text.Json.JsonSerializer.Deserialize<object>("{\"X\":3,\"Y\":4}")!;
        var result = ArgumentCoercion.Coerce([json], Method(nameof(MethodWithRecord)));
        var pt = Assert.IsType<CoercionTestPoint>(result[0]);
        Assert.Equal(3, pt.X);
        Assert.Equal(4, pt.Y);
    }

    [Fact]
    public void Coerce_WrongArgCount_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ArgumentCoercion.Coerce([], Method(nameof(MethodWithInt))));
        Assert.Contains("Expected 1 argument(s) but got 0", ex.Message);
    }

    [Fact]
    public void Coerce_NoParams_EmptyArray_Succeeds()
    {
        var result = ArgumentCoercion.Coerce([], Method(nameof(MethodWithNoParams)));
        Assert.Empty(result);
    }

    internal sealed record CoercionTestPoint(int X, int Y);
}
