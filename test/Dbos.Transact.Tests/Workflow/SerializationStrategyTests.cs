using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class SerializationStrategyTests
{
    [Fact]
    public void Ordinals_MatchJava()
    {
        Assert.Equal(0, (int)SerializationStrategy.Default);
        Assert.Equal(1, (int)SerializationStrategy.Portable);
        Assert.Equal(2, (int)SerializationStrategy.Native);
    }

    [Fact]
    public void HasThreeValues()
    {
        Assert.Equal(3, Enum.GetValues<SerializationStrategy>().Length);
    }
}
