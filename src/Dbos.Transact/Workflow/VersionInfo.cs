namespace Dbos.Transact.Workflow;

public sealed record VersionInfo(
    string VersionId,
    string VersionName,
    DateTimeOffset VersionTimestamp,
    DateTimeOffset CreatedAt);
