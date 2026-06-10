using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Bingosoft.Net.IfcMetadata;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (!TryParseArguments(
                args,
                out var ifcSourceFile,
                out var jsonTargetFile,
                out var preserveOrder,
                out var outputBufferSize,
                out var writeThrough,
                out var engine,
                out var verbosity,
                out var progressMode,
                out var diagnosticsLogFile))
        {
            PrintUsage();
            Environment.Exit(1);
        }

        if (!ifcSourceFile.Exists)
        {
            Console.WriteLine($"File: {ifcSourceFile} does not exist.");
            Environment.Exit(1);
        }

        try
        {
            var managedMemoryBefore = verbosity is Verbosity.Detailed
                ? GC.GetTotalMemory(forceFullCollection: false)
                : 0L;

            var stopwatch = Stopwatch.StartNew();
            using var diagnosticsLog = OpenDiagnosticsLog(diagnosticsLogFile);
            var diagnosticsLogger = CreateDiagnosticsLogger(diagnosticsLog, stopwatch);
            var progressReporter = CreateProgressReporter(progressMode);

            diagnosticsLogger?.Invoke("program: start");
            diagnosticsLogger?.Invoke($"program: source={ifcSourceFile.FullName}");
            diagnosticsLogger?.Invoke($"program: target={jsonTargetFile.FullName}");
            diagnosticsLogger?.Invoke($"program: engine={engine}");

            IfcAccessors.SetTelemetryEnabled(verbosity is Verbosity.Detailed);
            IfcAccessors.ResetTelemetry();

            progressReporter?.Invoke(0, 10000);
            diagnosticsLogger?.Invoke("router: export start");

            var exportReport = IfcEngineRouter.Export(
                ifcSourceFile,
                jsonTargetFile,
                preserveOrder,
                engine,
                outputBufferSize,
                writeThrough,
                progressReporter,
                diagnosticsLogger);

            diagnosticsLogger?.Invoke("router: export complete");

            if (progressReporter is not null)
            {
                Console.WriteLine();
            }

            stopwatch.Stop();

            switch (verbosity)
            {
                case Verbosity.Detailed:
                    {
                        var process = Process.GetCurrentProcess();
                        var telemetrySnapshot = IfcAccessors.GetTelemetrySnapshot();
                        var managedMemoryAfter = GC.GetTotalMemory(forceFullCollection: false);
                        PrintExecutionReport(
                            ifcSourceFile,
                            jsonTargetFile,
                            preserveOrder,
                            outputBufferSize,
                            writeThrough,
                            engine,
                            exportReport,
                            telemetrySnapshot,
                            stopwatch.Elapsed,
                            managedMemoryAfter - managedMemoryBefore,
                            process.WorkingSet64,
                            process.PeakWorkingSet64);
                        break;
                    }
                case Verbosity.Timing:
                    PrintTimingReport(stopwatch.Elapsed);
                    break;
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Environment.Exit(1);
        }
    }

    private static bool TryParseArguments(
        string[] args,
        out FileInfo ifcSourceFile,
        out FileInfo jsonTargetFile,
        out bool preserveOrder,
        out int outputBufferSize,
        out bool writeThrough,
        out IfcExportEngine engine,
        out Verbosity verbosity,
        out ProgressMode progressMode,
        out FileInfo diagnosticsLogFile)
    {
        ifcSourceFile = null;
        jsonTargetFile = null;
        diagnosticsLogFile = null;
        preserveOrder = true;
        outputBufferSize = IfcStreamingJsonExporter.DefaultOutputFileBufferSize;
        writeThrough = false;
        engine = IfcExportEngine.Xbim;
        verbosity = Verbosity.None;
        progressMode = ProgressMode.Completed;

        string sourcePath = null;
        string targetPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--preserve-order":
                    if (i + 1 >= args.Length || !bool.TryParse(args[i + 1], out preserveOrder))
                    {
                        return false;
                    }

                    i++;
                    break;

                case "--no-preserve-order":
                    preserveOrder = false;
                    break;

                case "--engine":
                    if (i + 1 >= args.Length
                        || args[i + 1].StartsWith("--", StringComparison.Ordinal)
                        || !IfcExportEngineParser.TryParse(args[i + 1], out engine))
                    {
                        return false;
                    }

                    i++;
                    break;

                case "--verbosity":
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        verbosity = Verbosity.Detailed;
                        break;
                    }

                    if (!TryParseVerbosity(args[i + 1], out verbosity))
                    {
                        return false;
                    }

                    i++;
                    break;

                case "--progress":
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        progressMode = ProgressMode.Completed;
                        break;
                    }

                    if (!TryParseProgressMode(args[i + 1], out progressMode))
                    {
                        return false;
                    }

                    i++;
                    break;

                case "--output-buffer-kb":
                    if (i + 1 >= args.Length
                        || !int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bufferSizeKb)
                        || bufferSizeKb <= 0
                        || bufferSizeKb > int.MaxValue / 1024)
                    {
                        return false;
                    }

                    outputBufferSize = bufferSizeKb * 1024;
                    i++;
                    break;

                case "--diagnostics-log":
                case "--phase-log":
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    diagnosticsLogFile = new FileInfo(args[i + 1]);
                    i++;
                    break;

                case "--write-through":
                    writeThrough = true;
                    break;

                case "--no-write-through":
                    writeThrough = false;
                    break;

                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    if (sourcePath is null)
                    {
                        sourcePath = arg;
                        break;
                    }

                    if (targetPath is null)
                    {
                        targetPath = arg;
                        break;
                    }

                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        ifcSourceFile = new FileInfo(sourcePath);
        jsonTargetFile = targetPath is null
            ? new FileInfo(Path.ChangeExtension(sourcePath, ".json"))
            : new FileInfo(targetPath);

        return true;
    }

    private static StreamWriter OpenDiagnosticsLog(FileInfo diagnosticsLogFile)
    {
        if (diagnosticsLogFile is null)
        {
            return null;
        }

        if (diagnosticsLogFile.Directory is not null && !diagnosticsLogFile.Directory.Exists)
        {
            diagnosticsLogFile.Directory.Create();
        }

        return new StreamWriter(diagnosticsLogFile.FullName, append: false)
        {
            AutoFlush = true,
        };
    }

    private static Action<string> CreateDiagnosticsLogger(StreamWriter writer, Stopwatch stopwatch)
    {
        if (writer is null)
        {
            return null;
        }

        return message =>
        {
            var elapsed = stopwatch.Elapsed.TotalMilliseconds.ToString("N2", CultureInfo.InvariantCulture);
            writer.WriteLine($"{DateTimeOffset.Now:O}\t+{elapsed} ms\t{message}");
        };
    }

    private static bool TryParseVerbosity(string value, out Verbosity verbosity)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "none":
            case "quiet":
                verbosity = Verbosity.None;
                return true;
            case "timing":
            case "time":
                verbosity = Verbosity.Timing;
                return true;
            case "normal":
            case "summary":
            case "detailed":
            case "verbose":
                verbosity = Verbosity.Detailed;
                return true;
            default:
                verbosity = Verbosity.None;
                return false;
        }
    }

    private static bool TryParseProgressMode(string value, out ProgressMode progressMode)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "none":
            case "off":
                progressMode = ProgressMode.None;
                return true;
            case "completed":
                progressMode = ProgressMode.Completed;
                return true;
            case "remaining":
                progressMode = ProgressMode.Remaining;
                return true;
            default:
                progressMode = ProgressMode.Completed;
                return false;
        }
    }

    private static Action<int, int> CreateProgressReporter(ProgressMode progressMode)
    {
        if (progressMode == ProgressMode.None)
        {
            return null;
        }

        var lastDisplayPermille = int.MinValue;


        return (processed, total) =>
        {
            if (total <= 0)
            {
                return;
            }

            var completedPermille = (int)Math.Min(1000, (processed * 1000L) / total);
            var displayPermille = progressMode switch
            {
                ProgressMode.Completed => completedPermille,
                ProgressMode.Remaining => 1000 - completedPermille,
                _ => completedPermille,
            };

            if (displayPermille == lastDisplayPermille)
            {
                return;
            }

            lastDisplayPermille = displayPermille;
            var label = progressMode == ProgressMode.Remaining ? "Remaining" : "Progress";
            var progressText = $"{label}: {FormatProgressPercent(displayPermille)}";

            if (Console.IsOutputRedirected)
            {
                Console.WriteLine(progressText);
            }
            else
            {
                Console.Write($"\r{progressText}");
            }

            Console.Out.Flush();
        };
    }

    private static string FormatProgressPercent(int permille)
    {
        var whole = permille / 10;
        var fraction = permille % 10;
        return string.Format(CultureInfo.InvariantCulture, "{0}.{1}%", whole, fraction);
    }

    private static void PrintExecutionReport(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputBufferSize,
        bool writeThrough,
        IfcExportEngine engine,
        IfcExportReport exportReport,
        IfcAccessorTelemetrySnapshot telemetry,
        TimeSpan elapsed,
        long managedMemoryDeltaBytes,
        long workingSetBytes,
        long peakWorkingSetBytes)
    {
        var targetFileSize = jsonTargetFile.Exists ? jsonTargetFile.Length : 0L;

        Console.WriteLine();
        Console.WriteLine("=== ifc-metadata execution report ===");
        Console.WriteLine($"Source IFC: {ifcSourceFile.FullName}");
        Console.WriteLine($"Target JSON: {jsonTargetFile.FullName}");
        Console.WriteLine($"PreserveOrder: {preserveOrder}");
        Console.WriteLine($"Output buffer: {FormatBytes(outputBufferSize)} ({outputBufferSize} bytes)");
        Console.WriteLine($"WriteThrough: {writeThrough}");

        Console.WriteLine($"Requested engine: {engine}");
        Console.WriteLine($"Effective engine: {exportReport.ExecutionDetails.EffectiveEngine}");
        Console.WriteLine($"Fast-step requested count: {exportReport.ExecutionDetails.FastStepRequestedCount}");
        Console.WriteLine($"Fast-step attempt count: {exportReport.ExecutionDetails.FastStepAttemptCount}");
        Console.WriteLine($"Fast-step success count: {exportReport.ExecutionDetails.FastStepSuccessCount}");
        Console.WriteLine($"xBIM run count: {exportReport.ExecutionDetails.XbimRunCount}");
        Console.WriteLine($"Fallback-to-xBIM count: {exportReport.ExecutionDetails.FallbackToXbimCount}");
        Console.WriteLine($"Fast-step detected schema: {exportReport.ExecutionDetails.FastStepSchema ?? "n/a"}");
        Console.WriteLine($"Fallback reason: {exportReport.ExecutionDetails.FallbackReason ?? "n/a"}");
        Console.WriteLine($"Schema: {exportReport.SchemaVersion}");
        Console.WriteLine($"MetaObjects: {exportReport.MetaObjectCount}");

        Console.WriteLine($"Output size: {FormatBytes(targetFileSize)}");
        Console.WriteLine($"Elapsed: {elapsed.TotalMilliseconds.ToString("N2", CultureInfo.InvariantCulture)} ms");
        Console.WriteLine($"Managed memory delta: {FormatSignedBytes(managedMemoryDeltaBytes)}");
        Console.WriteLine($"Working set: {FormatBytes(workingSetBytes)}");
        Console.WriteLine($"Peak working set: {FormatBytes(peakWorkingSetBytes)}");
        Console.WriteLine();

        Console.WriteLine("Accessor telemetry:");
        PrintTelemetryRow("TypedId", telemetry.TypedIdFastHits, telemetry.TypedIdFallbackHits);
        PrintTelemetryRow("MaterialId", telemetry.MaterialIdFastHits, telemetry.MaterialIdFallbackHits);
        PrintTelemetryRow("EntityLabel", telemetry.EntityLabelFastHits, telemetry.EntityLabelFallbackHits);
        PrintTelemetryRow("GlobalId", telemetry.GlobalIdFastHits, telemetry.GlobalIdFallbackHits);

        if (exportReport.FastStepTelemetry.HasValues)
        {
            PrintFastStepTelemetry(exportReport.FastStepTelemetry);
        }

        if (telemetry.FallbackTypeHits.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Top fallback runtime types:");
            foreach (var entry in telemetry.FallbackTypeHits.OrderByDescending(static x => x.Value).ThenBy(static x => x.Key).Take(10))
            {
                Console.WriteLine($"  {entry.Key}: {entry.Value}");
            }
        }

        Console.WriteLine("=== end of report ===");
    }

    private static void PrintFastStepTelemetry(FastStepTelemetrySnapshot telemetry)
    {
        Console.WriteLine();
        Console.WriteLine("Fast-step telemetry:");
        Console.WriteLine($"  GlobalId: index={telemetry.GlobalIdIndexHits}, rawFallback={telemetry.GlobalIdRawFallbackHits}, missing={telemetry.GlobalIdMisses}");
        Console.WriteLine($"  Name: index={telemetry.NameIndexHits}, rawFallback={telemetry.NameRawFallbackHits}, missing={telemetry.NameMisses}");
        Console.WriteLine($"  Properties: found={telemetry.PropertySetHits}, null={telemetry.PropertySetMisses}");
        Console.WriteLine($"  MaterialId: found={telemetry.MaterialHits}, null={telemetry.MaterialMisses}");
        Console.WriteLine($"  TypeId: found={telemetry.TypeHits}, null={telemetry.TypeMisses}");
    }

    private static void PrintTelemetryRow(string accessorName, long fastHits, long fallbackHits)
    {
        var total = fastHits + fallbackHits;
        var fallbackRate = total == 0
            ? "n/a"
            : string.Format(CultureInfo.InvariantCulture, "{0:0.00}%", (fallbackHits * 100d) / total);

        Console.WriteLine($"  {accessorName}: fast={fastHits}, fallback={fallbackHits}, rate={fallbackRate}, total={total}");
    }

    private static string FormatSignedBytes(long bytes)
    {
        var sign = bytes < 0 ? "-" : "+";
        return sign + FormatBytes(Math.Abs(bytes));
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.00} {1}", value, units[unitIndex]);
    }

    private static void PrintTimingReport(TimeSpan elapsed)
    {
        Console.WriteLine($"Elapsed: {elapsed.TotalMilliseconds.ToString("N2", CultureInfo.InvariantCulture)} ms");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Please specify the path to the IFC and optional output json.");
        Console.WriteLine("Usage: ifc_metadata /path_to_file.ifc [/path_to_file.json] [--preserve-order true|false]");
        Console.WriteLine("Usage: ifc_metadata /path_to_file.ifc --no-preserve-order");
        Console.WriteLine("Usage: ifc_metadata /path_to_file.ifc [output.json] [--engine xbim|fast-step] [--verbosity [summary|detailed|timing|none]] [--progress [none|completed|remaining]] [--output-buffer-kb N] [--diagnostics-log path] [--write-through|--no-write-through]");
        Console.WriteLine("Default: preserve order is true.");
        Console.WriteLine($"Default output buffer: {IfcStreamingJsonExporter.DefaultOutputFileBufferSize / 1024} KB.");
        Console.WriteLine("Default write-through: disabled.");
        Console.WriteLine("Default engine: xbim.");
        Console.WriteLine("Default verbosity: none.");
        Console.WriteLine("Default progress mode: completed.");
        Console.WriteLine("If output path is not passed, target defaults to source name with .json extension.");
    }

    private enum Verbosity
    {
        None,
        Timing,
        Detailed,
    }

    private enum ProgressMode
    {
        None,
        Completed,
        Remaining,
    }
}
