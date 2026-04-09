using BenchmarkDotNet.Running;

if (!IfcBenchmarkSettings.TryGetIfcPath(out var ifcFile))
{
    throw new InvalidOperationException(IfcBenchmarkSettings.GetMissingFileConfigurationMessage());
}

Console.WriteLine($"Using IFC benchmark file: {ifcFile.FullName}");
BenchmarkRunner.Run<IfcFilePipelineBenchmark>();
