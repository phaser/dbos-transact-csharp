namespace Dbos.Transact.Conformance;

/// <summary>
/// Validates a <see cref="DbSnapshot"/> against a <see cref="ScenarioExpectation"/> or another snapshot.
/// All failures are collected and reported in a single, readable <see cref="ConformanceAssertionException"/>.
/// </summary>
public static class DbSnapshotAssertion
{
    /// <summary>
    /// Asserts that <paramref name="actual"/> satisfies <paramref name="expected"/>.
    /// </summary>
    /// <exception cref="ConformanceAssertionException">On any mismatch, with one bullet per failing field.</exception>
    public static void Assert(DbSnapshot actual, ScenarioExpectation expected)
    {
        var failures = new List<string>();

        if (!string.Equals(actual.Workflow.Status, expected.WorkflowStatus, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"workflow status: expected '{expected.WorkflowStatus}', got '{actual.Workflow.Status}'");
        }

        if (actual.Steps.Count != expected.StepCount)
        {
            failures.Add(
                $"step count: expected {expected.StepCount}, got {actual.Steps.Count}");
        }

        if (expected.OutputJson is not null &&
            !string.Equals(actual.Workflow.OutputJson, expected.OutputJson, StringComparison.Ordinal))
        {
            failures.Add(
                $"workflow output: expected '{expected.OutputJson}', got '{actual.Workflow.OutputJson}'");
        }

        if (expected.ErrorTypeName is not null &&
            (actual.Workflow.ErrorJson is null ||
             !actual.Workflow.ErrorJson.Contains(expected.ErrorTypeName, StringComparison.Ordinal)))
        {
            failures.Add(
                $"error type: expected error containing '{expected.ErrorTypeName}', " +
                $"got '{actual.Workflow.ErrorJson}'");
        }

        ThrowIfAny(failures, header: "Conformance assertion failed");
    }

    /// <summary>
    /// Asserts that <paramref name="left"/> and <paramref name="right"/> are structurally equal
    /// (modulo normalization already applied by <see cref="ScenarioRunner"/>).
    /// Used for C# vs Java golden-fixture cross-runtime comparison.
    /// </summary>
    /// <exception cref="ConformanceAssertionException">On any mismatch.</exception>
    public static void AssertEqual(
        DbSnapshot left,
        DbSnapshot right,
        string leftLabel,
        string rightLabel)
    {
        var failures = new List<string>();

        if (!string.Equals(left.Workflow.Status, right.Workflow.Status, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"workflow status: {leftLabel}='{left.Workflow.Status}', {rightLabel}='{right.Workflow.Status}'");
        }

        if (left.Steps.Count != right.Steps.Count)
        {
            failures.Add(
                $"step count: {leftLabel}={left.Steps.Count}, {rightLabel}={right.Steps.Count}");
        }
        else
        {
            for (var i = 0; i < left.Steps.Count; i++)
            {
                var ls = left.Steps[i];
                var rs = right.Steps[i];

                if (ls.FunctionId != rs.FunctionId)
                    failures.Add(
                        $"steps[{i}].function_id: {leftLabel}={ls.FunctionId}, {rightLabel}={rs.FunctionId}");

                if (!string.Equals(ls.FunctionName, rs.FunctionName, StringComparison.Ordinal))
                    failures.Add(
                        $"steps[{i}].function_name: {leftLabel}='{ls.FunctionName}', {rightLabel}='{rs.FunctionName}'");

                // Output presence must agree even if values differ (different runtimes may format differently)
                if ((ls.OutputJson is null) != (rs.OutputJson is null))
                    failures.Add(
                        $"steps[{i}].output presence: {leftLabel}={(ls.OutputJson is null ? "null" : "set")}, " +
                        $"{rightLabel}={(rs.OutputJson is null ? "null" : "set")}");

                if ((ls.ErrorJson is null) != (rs.ErrorJson is null))
                    failures.Add(
                        $"steps[{i}].error presence: {leftLabel}={(ls.ErrorJson is null ? "null" : "set")}, " +
                        $"{rightLabel}={(rs.ErrorJson is null ? "null" : "set")}");
            }
        }

        ThrowIfAny(failures, header: $"Cross-runtime conformance failure ({leftLabel} vs {rightLabel})");
    }

    private static void ThrowIfAny(List<string> failures, string header)
    {
        if (failures.Count == 0) return;
        throw new ConformanceAssertionException(
            $"{header}:{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures.Select(f => $"  - {f}")));
    }
}
