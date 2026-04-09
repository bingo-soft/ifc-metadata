using BenchmarkDotNet.Attributes;

using Bingosoft.Net.IfcMetadata;

[MemoryDiagnoser]
public class IfcFilePipelineBenchmark
{
    private FileInfo _ifcSourceFile = null!;
    private FileInfo _targetJsonFile = null!;

    [Params(true, false)]
    public bool PreserveOrder { get; set; }

    [Params(false)]
    public bool TelemetryEnabled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!IfcBenchmarkSettings.TryGetIfcPath(out _ifcSourceFile))
        {
            throw new InvalidOperationException(IfcBenchmarkSettings.GetMissingFileConfigurationMessage());
        }

        _targetJsonFile = new FileInfo(Path.Combine(Path.GetTempPath(), "ifc-metadata-benchmark-end-to-end.json"));
        IfcAccessors.SetTelemetryEnabled(TelemetryEnabled);
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

        IfcAccessors.SetTelemetryEnabled(true);
    }
}
