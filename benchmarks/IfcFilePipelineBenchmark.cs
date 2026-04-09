using System.Text;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

using Bingosoft.Net.IfcMetadata;

[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class IfcFilePipelineBenchmark
{
    private FileInfo _ifcSourceFile = null!;
    private FileInfo _targetJsonFile = null!;

    [Params(true, false)]
    public bool PreserveOrder { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!IfcBenchmarkSettings.TryGetIfcPath(out _ifcSourceFile))
        {
            throw new InvalidOperationException(IfcBenchmarkSettings.GetMissingFileConfigurationMessage());
        }

        _targetJsonFile = new FileInfo(Path.Combine(Path.GetTempPath(), "ifc-metadata-benchmark-end-to-end.json"));
        IfcAccessors.ResetTelemetry();
    }

    [Benchmark]
    public void EndToEnd_Extract_And_Serialize()
    {
        IfcStreamingJsonExporter.Export(_ifcSourceFile, _targetJsonFile, PreserveOrder);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_targetJsonFile.Exists)
        {
            _targetJsonFile.Delete();
        }

        WriteAccessorTelemetry();
    }

    private void WriteAccessorTelemetry()
    {
        var snapshot = IfcAccessors.GetTelemetrySnapshot();

        var outputDirectory = Environment.GetEnvironmentVariable("IFC_BENCHMARK_TELEMETRY_DIR");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.Combine("BenchmarkDotNet.Artifacts", "results");
        }

        Directory.CreateDirectory(outputDirectory);

        var fileName = $"IfcFilePipelineBenchmark-accessor-telemetry-PreserveOrder-{PreserveOrder}.md";
        var outputPath = Path.Combine(outputDirectory, fileName);

        var lines = new StringBuilder();
        lines.AppendLine("# Ifc accessor telemetry");
        lines.AppendLine();
        lines.AppendLine($"PreserveOrder: {PreserveOrder}");
        lines.AppendLine();
        lines.AppendLine("| Accessor | Fast hits | Fallback hits | Fallback rate | Total | ");
        lines.AppendLine("|---|---:|---:|---:|---:|");

        AppendAccessorRow(lines, "TypedId", snapshot.TypedIdFastHits, snapshot.TypedIdFallbackHits);
        AppendAccessorRow(lines, "MaterialId", snapshot.MaterialIdFastHits, snapshot.MaterialIdFallbackHits);
        AppendAccessorRow(lines, "EntityLabel", snapshot.EntityLabelFastHits, snapshot.EntityLabelFallbackHits);
        AppendAccessorRow(lines, "GlobalId", snapshot.GlobalIdFastHits, snapshot.GlobalIdFallbackHits);

        lines.AppendLine();
        lines.AppendLine("## Top fallback runtime types");
        lines.AppendLine();

        if (snapshot.FallbackTypeHits.Count == 0)
        {
            lines.AppendLine("No fallback types were hit.");
        }
        else
        {
            lines.AppendLine("| Type | Hits |");
            lines.AppendLine("|---|---:|");

            foreach (var entry in snapshot.FallbackTypeHits.OrderByDescending(static x => x.Value).ThenBy(static x => x.Key).Take(20))
            {
                lines.AppendLine($"| {entry.Key} | {entry.Value} |");
            }
        }

        File.WriteAllText(outputPath, lines.ToString());
    }

    private static void AppendAccessorRow(StringBuilder lines, string accessorName, long fastHits, long fallbackHits)
    {
        var total = fastHits + fallbackHits;
        var fallbackRate = total == 0
            ? "n/a"
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00}%", (fallbackHits * 100d) / total);

        lines.AppendLine($"| {accessorName} | {fastHits} | {fallbackHits} | {fallbackRate} | {total} |");
    }
}
