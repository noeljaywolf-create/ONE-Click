using System.IO;

namespace OneClick.Desktop.Services;

public static class BaselineFixtureLocator
{
    public const string FixtureRelativePath = "fixtures/reference-network.json";

    public static string ResolveFixturePath()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, FixtureRelativePath),
            Path.Combine(AppContext.BaseDirectory, "sprinkler-agent-kit", "fixtures", "reference-network.json"),
        };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            candidates.Add(Path.Combine(dir.FullName, "sprinkler-agent-kit", "fixtures", "reference-network.json"));
            candidates.Add(Path.Combine(dir.FullName, FixtureRelativePath));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        throw new FileNotFoundException(
            $"Could not locate synthetic reference network fixture '{FixtureRelativePath}'. " +
            $"Probed: {string.Join("; ", candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(6))}");
    }
}
