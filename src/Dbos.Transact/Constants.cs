namespace Dbos.Transact;

public static class Constants
{
    public const string DbSchema = "dbos";
    public const string SysDbSuffix = "_dbos_sys";
    public const string PostgresDefaultDb = "postgres";

    public const string PostgresPasswordEnvVar = "PGPASSWORD";
    public const string PostgresUserEnvVar = "PGUSER";

    public const string DefaultAppVersion = "";
    public const string DefaultExecutorId = "local";

    public const string DbosNullTopic = "__null__topic__";
    public const string DbosInternalQueue = "_dbos_internal_queue";

    public const string SystemJdbcUrlEnvVar = "DBOS_SYSTEM_JDBC_URL";
    public const int DefaultMaxRecoveryAttempts = 100;
}
