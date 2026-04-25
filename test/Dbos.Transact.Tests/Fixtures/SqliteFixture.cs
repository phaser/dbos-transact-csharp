using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Tests.Fixtures;

/// <summary>
/// xUnit class fixture providing an isolated SQLite database.
/// Two modes:
///   <list type="bullet">
///     <item><see cref="Mode.File"/> — WAL-enabled temp file, cleaned up after tests.</item>
///     <item><see cref="Mode.Memory"/> — shared-cache in-memory database kept alive for the fixture lifetime.</item>
///   </list>
/// Share across a test class with <c>IClassFixture&lt;SqliteFixture&gt;</c>.
/// </summary>
public sealed class SqliteFixture : IAsyncLifetime, IDisposable
{
    public enum Mode { File, Memory }

    private readonly Mode _mode;
    private readonly string _tempPath;
    private SqliteConnection? _keepAlive;

    public SqliteFixture() : this(Mode.File) { }

    public SqliteFixture(Mode mode)
    {
        _mode = mode;
        _tempPath = mode == Mode.File
            ? Path.Combine(Path.GetTempPath(), $"dbos_test_{Guid.NewGuid():N}.sqlite")
            : string.Empty;
    }

    public string ConnectionString => _mode == Mode.Memory
        ? $"Data Source=dbos_test_{GetHashCode():X};Mode=Memory;Cache=Shared;"
        : $"Data Source={_tempPath};";

    public Task InitializeAsync()
    {
        if (_mode == Mode.Memory)
        {
            _keepAlive = new SqliteConnection(ConnectionString);
            _keepAlive.Open();
        }
        else
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_keepAlive is not null)
            await _keepAlive.DisposeAsync();

        if (_mode == Mode.File)
        {
            TryDelete(_tempPath);
            TryDelete(_tempPath + "-wal");
            TryDelete(_tempPath + "-shm");
        }
    }

    public void Dispose() { _keepAlive?.Dispose(); GC.SuppressFinalize(this); }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
    }
}
