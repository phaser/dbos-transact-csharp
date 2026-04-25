using System.Reflection;
using Dbos.Transact.Internal;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Internal;

public class AppVersionComputerTests
{
    private static string Hash(string version, params MethodInfo[] methods) =>
        AppVersionComputer.ComputeAppVersion(version, methods);

    private static MethodInfo M(string name) =>
        typeof(WorkflowStubs).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void SameInputs_ProduceDeterministicHash()
    {
        var h1 = Hash("1.0", M(nameof(WorkflowStubs.Alpha)));
        var h2 = Hash("1.0", M(nameof(WorkflowStubs.Alpha)));
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void DifferentDbosVersions_ProduceDifferentHashes()
    {
        var h1 = Hash("1.0", M(nameof(WorkflowStubs.Alpha)));
        var h2 = Hash("2.0", M(nameof(WorkflowStubs.Alpha)));
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void DifferentMethods_ProduceDifferentHashes()
    {
        var h1 = Hash("1.0", M(nameof(WorkflowStubs.Alpha)));
        var h2 = Hash("1.0", M(nameof(WorkflowStubs.Beta)));
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void AddingMethod_ChangesHash()
    {
        var h1 = Hash("1.0", M(nameof(WorkflowStubs.Alpha)));
        var h2 = Hash("1.0", M(nameof(WorkflowStubs.Alpha)), M(nameof(WorkflowStubs.Beta)));
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void MethodOrder_DoesNotAffectHash()
    {
        var h1 = Hash("1.0", M(nameof(WorkflowStubs.Alpha)), M(nameof(WorkflowStubs.Beta)));
        var h2 = Hash("1.0", M(nameof(WorkflowStubs.Beta)), M(nameof(WorkflowStubs.Alpha)));
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void EmptyMethods_ProducesNonEmptyHash()
    {
        var h = Hash("1.0");
        Assert.NotEmpty(h);
        Assert.Equal(64, h.Length); // SHA-256 = 32 bytes = 64 hex chars
    }

    [Fact]
    public void WorkflowClassNameAttribute_UsedInFqn()
    {
        var method = typeof(NamedService).GetMethod(nameof(NamedService.Run), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        var fqn = AppVersionComputer.GetFullyQualifiedName(method);
        Assert.Equal("PortableName//Run", fqn);
    }

    // Stub types for testing
    public static class WorkflowStubs
    {
        public static void Alpha() { }
        public static int Beta(string s) => 0;
    }

    [WorkflowClassName("PortableName")]
    public sealed class NamedService
    {
        public static void Run() { }
    }
}
