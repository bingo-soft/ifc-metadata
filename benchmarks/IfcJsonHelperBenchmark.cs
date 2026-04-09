using BenchmarkDotNet.Attributes;
using Bingosoft.Net.IfcMetadata;

[MemoryDiagnoser]
public class IfcJsonHelperBenchmark
{
    [Params(100, 1000)]
    public int MetaObjectCount { get; set; }

    private MetadataExtractor _metadata = null!;
    private FileInfo _targetFile = null!;

    [GlobalSetup]
    public void Setup()
    {
        var objects = new List<Metadata>(MetaObjectCount);
        for (var i = 0; i < MetaObjectCount; i++)
        {
            objects.Add(new Metadata
            {
                Id = $"obj-{i}",
                Name = $"Element {i}",
                Type = "IfcBuildingElementProxy",
                Parent = i == 0 ? null : "obj-0",
                PropertyIds = new[] { $"prop-{i}-1", $"prop-{i}-2" },
                Material = $"IfcMaterial_{i}",
                TypeId = $"type-{i}"
            });
        }

        _metadata = new MetadataExtractor
        {
            Id = "benchmark-model",
            ProjectId = "project-1",
            Author = "benchmark",
            CreatedAt = "2024-01-01T00:00:00+00:00",
            Schema = "IFC4",
            CreatingApplication = "benchmark-app",
            MetaObjects = objects
        };

        var targetPath = Path.Combine(Path.GetTempPath(), "ifc-metadata-benchmark.json");
        _targetFile = new FileInfo(targetPath);
    }

    [Benchmark]
    public void Serialize_To_Json_File()
    {
        IfcJsonHelper.ToJson(_targetFile, ref _metadata);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_targetFile.Exists)
        {
            _targetFile.Delete();
        }
    }
}
