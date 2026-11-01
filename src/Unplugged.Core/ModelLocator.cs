namespace Unplugged.Core;

/// <summary>Finds the bundled model file: next to the app in "models/", or (during development) in the repo's "models/".</summary>
public static class ModelLocator
{
    public static string? Find()
    {
        var env = Environment.GetEnvironmentVariable("UNPLUGGED_MODEL");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        // macOS .app bundles keep resources in Contents/Resources
        var candidates = new List<string>();
        for (var d = dir; d != null; d = d.Parent)
        {
            candidates.Add(Path.Combine(d.FullName, "models"));
            candidates.Add(Path.Combine(d.FullName, "Resources", "models"));
        }
        foreach (var c in candidates)
        {
            if (!Directory.Exists(c)) continue;
            var f = Directory.EnumerateFiles(c, "*.gguf")
                .Where(p => !Path.GetFileName(p).Contains("mmproj", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p).FirstOrDefault();
            if (f != null) return f;
        }
        return null;
    }
}
