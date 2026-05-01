using Dbos.Transact.Conformance.Scenarios;

namespace Dbos.Transact.Conformance;

/// <summary>
/// Cross-runtime conformance tests.
/// Each test runs a <see cref="ConformanceScenario"/> against a shared Postgres container
/// and verifies observable <c>dbos.*</c> DB state against declarative expectations.
/// When Java golden fixtures are present under <c>fixtures/java/</c>, the C# output
/// is also diffed against the Java snapshot (normalizing timestamps and UUIDs).
/// </summary>
[Collection("Conformance")]
public sealed class ConformanceTests(ConformanceFixture fixture)
{
    private ScenarioRunner Runner => new(fixture.ConnectionString, ConformanceFixture.Schema);

    // ── Scenario: SimpleWorkflowLifecycle ────────────────────────────────────

    [Fact]
    public async Task SimpleWorkflowLifecycle_Passes()
    {
        var scenario = new SimpleWorkflowLifecycle();

        var result = await Runner.RunAsync(scenario);

        DbSnapshotAssertion.Assert(result.Snapshot, scenario.Expected);
    }

    [Fact]
    public async Task SimpleWorkflowLifecycle_JavaGolden_MatchesCsharpOutput()
    {
        var scenario = new SimpleWorkflowLifecycle();
        if (!GoldenFixture.Exists("java", scenario.Name))
            return; // Java golden fixture not present — skip without failing

        var result = await Runner.RunAsync(scenario);

        var javaSnapshot = GoldenFixture.Load("java", scenario.Name);
        DbSnapshotAssertion.AssertEqual(
            result.Snapshot,
            javaSnapshot,
            leftLabel: "csharp",
            rightLabel: "java");
    }

    // ── Divergence injection ─────────────────────────────────────────────────

    [Fact]
    public async Task InjectDivergence_FailsClearlyAndReadably()
    {
        var scenario = new SimpleWorkflowLifecycle();
        var runner = Runner;

        // Run to completion — workflow_status should be SUCCESS
        var result = await runner.RunAsync(scenario);
        DbSnapshotAssertion.Assert(result.Snapshot, scenario.Expected);

        // Corrupt the status to simulate a cross-runtime divergence
        await runner.InjectDivergenceAsync(result.WorkflowId, corruptStatus: "PENDING");

        // Re-snapshot the corrupted state
        var corrupted = await runner.CaptureSnapshotAsync(result.WorkflowId);

        // The assertion must fail with a readable message naming both values
        var ex = Assert.Throws<ConformanceAssertionException>(
            () => DbSnapshotAssertion.Assert(corrupted, scenario.Expected));

        Assert.Contains("workflow status", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SUCCESS", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PENDING", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
