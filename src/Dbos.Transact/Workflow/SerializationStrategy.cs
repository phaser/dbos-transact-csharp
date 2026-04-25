namespace Dbos.Transact.Workflow;

/// <summary>
/// Serialization strategy for workflow arguments, results, events, messages, and streams.
/// Port of Java SerializationStrategy enum.
/// </summary>
public enum SerializationStrategy
{
    /// <summary>Use the default (native) serialization for this runtime.</summary>
    Default = 0,

    /// <summary>Use portable JSON serialization compatible across DBOS runtimes.</summary>
    Portable = 1,

    /// <summary>Explicitly use the native serialization format for this runtime.</summary>
    Native = 2,
}
