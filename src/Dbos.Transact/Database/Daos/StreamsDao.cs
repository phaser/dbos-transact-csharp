namespace Dbos.Transact.Database.Daos;

/// <summary>
/// Data-access methods for the <c>workflow_streams</c> table.
/// Port of Java's <c>StreamsDAO</c>. Dialect-specific SQL is provided by subclasses.
/// </summary>
public abstract class StreamsDao
{
    public abstract Task WriteStreamFromStepAsync(
        string workflowId,
        int functionId,
        string key,
        object? value,
        string? serializationFormat,
        CancellationToken ct = default);

    public abstract Task WriteStreamFromWorkflowAsync(
        string workflowId,
        int functionId,
        string key,
        object? value,
        string? serializationFormat,
        CancellationToken ct = default);

    public abstract Task CloseStreamAsync(string workflowId, int functionId, string key, CancellationToken ct = default);

    public abstract Task<object?> ReadStreamAsync(string workflowId, string key, int offset, CancellationToken ct = default);

    public abstract Task<IReadOnlyDictionary<string, IReadOnlyList<object?>>> GetAllStreamEntriesAsync(
        string workflowId,
        CancellationToken ct = default);
}
