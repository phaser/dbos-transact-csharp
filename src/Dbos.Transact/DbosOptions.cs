using Dbos.Transact.Json;
using Dbos.Transact.Workflow;

namespace Dbos.Transact;

/// <summary>
/// Immutable configuration record for a DBOS executor. Port of DBOSConfig.java.
/// DataSource is omitted — connection lifecycle is managed by dialect extension methods.
/// </summary>
public sealed record DbosOptions(
    string AppName,
    string? DatabaseUrl,
    string? DbUser,
    string? DbPassword,
    bool AdminServer,
    int AdminServerPort,
    bool Migrate,
    string? ConductorKey,
    string? ConductorDomain,
    IReadOnlyDictionary<string, object?>? ConductorExecutorMetadata,
    string? AppVersion,
    string? ExecutorId,
    string? DatabaseSchema,
    bool EnablePatching,
    IReadOnlySet<string> ListenQueues,
    IDbosSerializer? Serializer,
    TimeSpan? SchedulerPollingInterval)
{
    public string AppName { get; init; } = string.IsNullOrEmpty(AppName)
        ? throw new ArgumentException("DbosOptions.AppName must not be null or empty.", nameof(AppName))
        : AppName;

    // Explicit backing fields + init accessors so that 'with' expressions also validate.
    // Field initializers reference the positional parameter (satisfies CS8907) and validate on
    // initial construction; the init accessor validates on 'with' expressions.
    private string? _conductorKey = ConductorKey is { Length: 0 }
        ? throw new ArgumentException("DbosOptions.ConductorKey must not be empty if specified.", nameof(ConductorKey))
        : ConductorKey;
    public string? ConductorKey
    {
        get => _conductorKey;
        init => _conductorKey = value is { Length: 0 }
            ? throw new ArgumentException("DbosOptions.ConductorKey must not be empty if specified.", nameof(ConductorKey))
            : value;
    }

    private string? _conductorDomain = ConductorDomain is { Length: 0 }
        ? throw new ArgumentException("DbosOptions.ConductorDomain must not be empty if specified.", nameof(ConductorDomain))
        : ConductorDomain;
    public string? ConductorDomain
    {
        get => _conductorDomain;
        init => _conductorDomain = value is { Length: 0 }
            ? throw new ArgumentException("DbosOptions.ConductorDomain must not be empty if specified.", nameof(ConductorDomain))
            : value;
    }

    private string? _appVersion = AppVersion is { Length: 0 }
        ? throw new ArgumentException("DbosOptions.AppVersion must not be empty if specified.", nameof(AppVersion))
        : AppVersion;
    public string? AppVersion
    {
        get => _appVersion;
        init => _appVersion = value is { Length: 0 }
            ? throw new ArgumentException("DbosOptions.AppVersion must not be empty if specified.", nameof(AppVersion))
            : value;
    }

    private string? _executorId = ExecutorId is { Length: 0 }
        ? throw new ArgumentException("DbosOptions.ExecutorId must not be empty if specified.", nameof(ExecutorId))
        : ExecutorId;
    public string? ExecutorId
    {
        get => _executorId;
        init => _executorId = value is { Length: 0 }
            ? throw new ArgumentException("DbosOptions.ExecutorId must not be empty if specified.", nameof(ExecutorId))
            : value;
    }

    public IReadOnlySet<string> ListenQueues { get; init; } =
        new HashSet<string>(ListenQueues ?? Enumerable.Empty<string>());

    public static DbosOptions Defaults(string appName) => new(
        AppName: appName,
        DatabaseUrl: null,
        DbUser: null,
        DbPassword: null,
        AdminServer: false,
        AdminServerPort: 3001,
        Migrate: true,
        ConductorKey: null,
        ConductorDomain: null,
        ConductorExecutorMetadata: null,
        AppVersion: null,
        ExecutorId: null,
        DatabaseSchema: null,
        EnablePatching: false,
        ListenQueues: new HashSet<string>(),
        Serializer: null,
        SchedulerPollingInterval: null);

    public static DbosOptions DefaultsFromEnv(string appName)
    {
        var dbUrl = Environment.GetEnvironmentVariable(Constants.SystemJdbcUrlEnvVar);
        var dbUser = Environment.GetEnvironmentVariable(Constants.PostgresUserEnvVar);
        if (string.IsNullOrEmpty(dbUser)) dbUser = "postgres";
        var dbPassword = Environment.GetEnvironmentVariable(Constants.PostgresPasswordEnvVar);
        return Defaults(appName) with { DatabaseUrl = dbUrl, DbUser = dbUser, DbPassword = dbPassword };
    }

    public DbosOptions EnableAdminServer() => this with { AdminServer = true };
    public DbosOptions DisableAdminServer() => this with { AdminServer = false };

    public DbosOptions WithListenQueue(Queue queue) => WithListenQueues(queue.Name);
    public DbosOptions WithListenQueue(string queueName) => WithListenQueues(queueName);

    public DbosOptions WithListenQueues(params string?[] queueNames)
    {
        var merged = new HashSet<string>(ListenQueues);
        foreach (var name in queueNames)
            if (!string.IsNullOrEmpty(name)) merged.Add(name!);
        return this with { ListenQueues = merged };
    }

    public DbosOptions WithListenQueues(params Queue[] queues)
        => WithListenQueues(queues.Select(q => q.Name).ToArray());

    // IReadOnlySet<string> uses reference equality by default; use set equality instead.
    public bool Equals(DbosOptions? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return AppName == other.AppName
            && DatabaseUrl == other.DatabaseUrl
            && DbUser == other.DbUser
            && DbPassword == other.DbPassword
            && AdminServer == other.AdminServer
            && AdminServerPort == other.AdminServerPort
            && Migrate == other.Migrate
            && ConductorKey == other.ConductorKey
            && ConductorDomain == other.ConductorDomain
            && EqualityComparer<IReadOnlyDictionary<string, object?>?>.Default.Equals(ConductorExecutorMetadata, other.ConductorExecutorMetadata)
            && AppVersion == other.AppVersion
            && ExecutorId == other.ExecutorId
            && DatabaseSchema == other.DatabaseSchema
            && EnablePatching == other.EnablePatching
            && ListenQueues.SetEquals(other.ListenQueues)
            && EqualityComparer<IDbosSerializer?>.Default.Equals(Serializer, other.Serializer)
            && SchedulerPollingInterval == other.SchedulerPollingInterval;
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(AppName);
        hc.Add(DatabaseUrl);
        hc.Add(DbUser);
        hc.Add(DbPassword);
        hc.Add(AdminServer);
        hc.Add(AdminServerPort);
        hc.Add(Migrate);
        hc.Add(ConductorKey);
        hc.Add(ConductorDomain);
        hc.Add(ConductorExecutorMetadata);
        hc.Add(AppVersion);
        hc.Add(ExecutorId);
        hc.Add(DatabaseSchema);
        hc.Add(EnablePatching);
        foreach (var q in ListenQueues.OrderBy(s => s, StringComparer.Ordinal))
            hc.Add(q);
        hc.Add(Serializer);
        hc.Add(SchedulerPollingInterval);
        return hc.ToHashCode();
    }

    public override string ToString() =>
        $"DbosOptions[AppName={AppName}, DatabaseUrl={DatabaseUrl}, DbUser={DbUser}, DbPassword=***, " +
        $"DatabaseSchema={DatabaseSchema}, AdminServer={AdminServer}, AdminServerPort={AdminServerPort}, " +
        $"Migrate={Migrate}, ConductorKey={ConductorKey}, ConductorDomain={ConductorDomain}, " +
        $"ConductorExecutorMetadata={ConductorExecutorMetadata}, AppVersion={AppVersion}, ExecutorId={ExecutorId}, " +
        $"EnablePatching={EnablePatching}, ListenQueues=[{string.Join(", ", ListenQueues)}], " +
        $"Serializer={Serializer?.Name}, SchedulerPollingInterval={SchedulerPollingInterval}]";
}
