using BenchmarkDotNet.Running;

if (!IfcBenchmarkSettings.TryGetIfcPath(out var ifcFile))
{
    throw new InvalidOperationException(IfcBenchmarkSettings.GetMissingFileConfigurationMessage());
}

var runDetailed = args.Any(static arg => string.Equals(arg, "--detailed", StringComparison.OrdinalIgnoreCase));

Console.WriteLine($"Using IFC benchmark file: {ifcFile.FullName}");
if (runDetailed)
{
    Console.WriteLine("Benchmark mode: detailed");
    BenchmarkRunner.Run<IfcFilePipelineDetailedBenchmark>();
}
else
{
    Console.WriteLine("Benchmark mode: overall (default)");
    BenchmarkRunner.Run<IfcFilePipelineBenchmark>();
}
