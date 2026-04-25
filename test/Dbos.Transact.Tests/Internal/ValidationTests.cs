using Dbos.Transact.Internal;

namespace Dbos.Transact.Tests.Internal;

public class ValidationTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData(null, false)]
    [InlineData("hello", false)]
    public void NullableIsEmpty_ReturnsExpected(string? value, bool expected)
    {
        Assert.Equal(expected, Validation.NullableIsEmpty(value));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public void NullableIsNotPositive_WithValue_ReturnsExpected(int seconds, bool expected)
    {
        Assert.Equal(expected, Validation.NullableIsNotPositive(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void NullableIsNotPositive_Null_ReturnsFalse()
    {
        Assert.False(Validation.NullableIsNotPositive(null));
    }
}
