namespace Dbos.Transact.Hosting;

/// <summary>
/// Bindable settings type that mirrors the Java <c>DBOSProperties</c> shape and converts
/// to <see cref="DbosOptions"/> via <see cref="BuildOptions"/>. Bind from
/// <c>IConfiguration</c> with <c>services.Configure&lt;DbosOptionsConfigurator&gt;(...)</c>
/// or set fields directly via <c>AddDbos(opts =&gt; ...)</c>.
/// </summary>
public sealed class DbosOptionsConfigurator
{
    /// <summary>Application identity: name (required) and version.</summary>
    public ApplicationSettings Application { get; set; } = new();

    /// <summary>Datasource configuration for the DBOS system database.</summary>
    public DatasourceSettings Datasource { get; set; } = new();

    /// <summary>DBOS Cloud conductor connection settings.</summary>
    public ConductorSettings Conductor { get; set; } = new();

    /// <summary>Admin HTTP server settings.</summary>
    public AdminServerSettings AdminServer { get; set; } = new();

    /// <summary>Executor ID for this instance.</summary>
    public string? ExecutorId { get; set; }

    /// <summary>Whether to enable workflow patching.</summary>
    public bool EnablePatching { get; set; }

    /// <summary>Names of queues this executor should listen on. Empty = listen on all.</summary>
#pragma warning disable CA2227 // Collection properties should be read-only — needed for IConfiguration binding
    public IList<string> ListenQueues { get; set; } = new List<string>();
#pragma warning restore CA2227

    /// <summary>Polling interval for the workflow scheduler.</summary>
    public TimeSpan? SchedulerPollingInterval { get; set; }

    /// <summary>
    /// Builds a <see cref="DbosOptions"/> snapshot. <paramref name="defaultAppName"/> is used
    /// only when <see cref="ApplicationSettings.Name"/> is unset.
    /// </summary>
    public DbosOptions BuildOptions(string? defaultAppName = null)
    {
        var name = !string.IsNullOrEmpty(Application.Name)
            ? Application.Name!
            : defaultAppName ?? throw new InvalidOperationException(
                "Dbos:Application:Name must be configured (or pass an appName to AddDbos).");

        var defaults = DbosOptions.Defaults(name);

        return defaults with
        {
            DatabaseUrl = NullIfEmpty(Datasource.Url) ?? defaults.DatabaseUrl,
            DbUser = NullIfEmpty(Datasource.Username) ?? defaults.DbUser,
            DbPassword = NullIfEmpty(Datasource.Password) ?? defaults.DbPassword,
            DatabaseSchema = NullIfEmpty(Datasource.Schema) ?? defaults.DatabaseSchema,
            Migrate = Datasource.Migrate,
            ConductorKey = NullIfEmpty(Conductor.Key),
            ConductorDomain = NullIfEmpty(Conductor.Domain),
            AdminServer = AdminServer.Enabled,
            AdminServerPort = AdminServer.Port,
            ExecutorId = NullIfEmpty(ExecutorId),
            AppVersion = NullIfEmpty(Application.Version),
            EnablePatching = EnablePatching,
            ListenQueues = new HashSet<string>(ListenQueues),
            SchedulerPollingInterval = SchedulerPollingInterval,
        };
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    public sealed class ApplicationSettings
    {
        /// <summary>Application name. Required if no default is supplied to <c>AddDbos</c>.</summary>
        public string? Name { get; set; }

        /// <summary>Application version string.</summary>
        public string? Version { get; set; }
    }

    public sealed class DatasourceSettings
    {
        /// <summary>Connection string / JDBC URL for the DBOS system database.</summary>
        public string? Url { get; set; }

        public string? Username { get; set; }
        public string? Password { get; set; }

        /// <summary>Database schema name for DBOS system tables.</summary>
        public string? Schema { get; set; }

        /// <summary>Whether to run database migrations on startup. Defaults to <c>true</c>.</summary>
        public bool Migrate { get; set; } = true;
    }

    public sealed class ConductorSettings
    {
        public string? Key { get; set; }
        public string? Domain { get; set; }
    }

    public sealed class AdminServerSettings
    {
        public bool Enabled { get; set; }
        public int Port { get; set; } = 3001;
    }
}
