using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Cli.Tests;

/// <summary>
/// Per-test SQLite database file. Owns its temp path and cleans up sidecars.
/// </summary>
internal sealed class SqliteFixture : IDisposable
{
    public string Path { get; }
    public string ConnectionString { get; }

    public SqliteFixture()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"dbos_cli_{Guid.NewGuid():N}.sqlite");
        ConnectionString = $"Data Source={Path};";
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDelete(Path);
        TryDelete(Path + "-wal");
        TryDelete(Path + "-shm");
        GC.SuppressFinalize(this);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* best-effort */ }
    }
}
