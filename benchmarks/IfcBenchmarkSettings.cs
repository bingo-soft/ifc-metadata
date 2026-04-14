internal static class IfcBenchmarkSettings
{
    private const string IfcPathEnvironmentVariable = "IFC_BENCHMARK_FILE";

    public static bool TryGetIfcPath(out FileInfo ifcFile)
    {
        var configuredPath = Environment.GetEnvironmentVariable(IfcPathEnvironmentVariable);
        if (TryResolvePath(configuredPath, out ifcFile))
        {
            return true;
        }

        var defaultCandidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "ifc", "01_26_Slavyanka_4.ifc"),
            Path.Combine(Directory.GetCurrentDirectory(), "ifc", "sample.ifc"),
            Path.Combine(AppContext.BaseDirectory, "sample.ifc")
        };

        foreach (var candidate in defaultCandidates)
        {
            if (TryResolvePath(candidate, out ifcFile))
            {
                return true;
            }
        }

        ifcFile = null!;
        return false;
    }

    public static string GetMissingFileConfigurationMessage() =>
        $"IFC file benchmark requires a model. Set {IfcPathEnvironmentVariable}=<path-to.ifc> " +
        "or place ifc/01_26_Slavyanka_4.ifc in repository root.";

    private static bool TryResolvePath(string? path, out FileInfo ifcFile)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            ifcFile = new FileInfo(path);
            return true;
        }

        ifcFile = null!;
        return false;
    }
}
