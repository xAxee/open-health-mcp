using System.Text.Json;

namespace OpenHealthMCP.Tests;

internal static class FixtureLoader
{
    public static JsonDocument Load(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}