using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

using Bingosoft.Net.IfcMetadata;

[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class IfcFilePipelineBenchmark
{
    private FileInfo _ifcSourceFile = null!;
    private FileInfo _targetJsonFile = null!;
    private MetadataExtractor _metadata = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (!IfcBenchmarkSettings.TryGetIfcPath(out _ifcSourceFile))
        {
            throw new InvalidOperationException(IfcBenchmarkSettings.GetMissingFileConfigurationMessage());
        }

        _metadata = MetadataExtractor.FromIfc(_ifcSourceFile);
        _targetJsonFile = new FileInfo(Path.Combine(Path.GetTempPath(), "ifc-metadata-benchmark-end-to-end.json"));
    }

    [Benchmark]
    public int Extract_Only()
    {
        var metadata = MetadataExtractor.FromIfc(_ifcSourceFile);
        return metadata.MetaObjects?.Count ?? 0;
    }

    [Benchmark]
    public void Serialize_Only_From_Extracted_Metadata()
    {
        IfcJsonHelper.ToJson(_targetJsonFile, ref _metadata);
    }

    [Benchmark]
    public void EndToEnd_Extract_And_Serialize()
    {
        var metadata = MetadataExtractor.FromIfc(_ifcSourceFile);
        IfcJsonHelper.ToJson(_targetJsonFile, ref metadata);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_targetJsonFile.Exists)
        {
            _targetJsonFile.Delete();
        }
    }
}
