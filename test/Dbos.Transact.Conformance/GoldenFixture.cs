using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dbos.Transact.Conformance;

/// <summary>
/// Reads and writes normalized <see cref="DbSnapshot"/> golden fixtures for cross-runtime comparison.
/// Fixtures live under <c>fixtures/{runtime}/{scenarioName}.json</c> relative to the output directory.
/// </summary>
public static class GoldenFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string FixturesRoot =>
        Path.Combine(AppContext.BaseDirectory, "fixtures");

    public static string FixturePath(string runtime, string scenarioName) =>
        Path.Combine(FixturesRoot, runtime, $"{scenarioName}.json");

    /// <summary>Returns true if a golden fixture file exists for the given runtime and scenario.</summary>
    public static bool Exists(string runtime, string scenarioName) =>
        File.Exists(FixturePath(runtime, scenarioName));

    /// <summary>Loads and deserializes the golden fixture.</summary>
    /// <exception cref="FileNotFoundException">If the fixture file does not exist.</exception>
    public static DbSnapshot Load(string runtime, string scenarioName)
    {
        var path = FixturePath(runtime, scenarioName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DbSnapshot>(json, JsonOptions)
            ?? throw new InvalidDataException($"Empty golden fixture at '{path}'");
    }

    /// <summary>
    /// Serializes and saves <paramref name="snapshot"/> as a golden fixture.
    /// Used to capture C# output for committing as future regression baselines.
    /// </summary>
    public static void Save(string runtime, string scenarioName, DbSnapshot snapshot)
    {
        var dir = Path.Combine(FixturesRoot, runtime);
        Directory.CreateDirectory(dir);
        File.WriteAllText(FixturePath(runtime, scenarioName), JsonSerializer.Serialize(snapshot, JsonOptions));
    }
}
