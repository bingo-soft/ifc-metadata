using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

using Bingosoft.Net.IfcMetadata;

[Config(typeof(IfcFilePipelineBenchmarkConfig))]
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

public sealed class IfcFilePipelineBenchmarkConfig : ManualConfig
{
    public IfcFilePipelineBenchmarkConfig()
    {
        AddJob(CreateGcJob("WS_Concurrent", gcServer: false, gcConcurrent: true));
        AddJob(CreateGcJob("WS_NonConcurrent", gcServer: false, gcConcurrent: false));
        AddJob(CreateGcJob("Server_Concurrent", gcServer: true, gcConcurrent: true));
    }

    private static Job CreateGcJob(string id, bool gcServer, bool gcConcurrent)
    {
        return Job.Default
            .WithId(id)
            .WithLaunchCount(1)
            .WithWarmupCount(3)
            .WithIterationCount(8)
            .WithGcServer(gcServer)
            .WithGcConcurrent(gcConcurrent);
    }
}
