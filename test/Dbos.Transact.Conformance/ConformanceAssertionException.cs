namespace Dbos.Transact.Conformance;

/// <summary>
/// Thrown by <see cref="DbSnapshotAssertion"/> when observed DB state diverges from expectation.
/// The message is structured for human readability: one bullet per failing field.
/// </summary>
public sealed class ConformanceAssertionException(string message) : Exception(message);
