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
        if (!TryParseArguments(args, out var ifcSourceFile, out var jsonTargetFile, out var preserveOrder, out var verbosity))
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
            var process = Process.GetCurrentProcess();
            var managedMemoryBefore = GC.GetTotalMemory(forceFullCollection: false);
            var stopwatch = Stopwatch.StartNew();

            IfcAccessors.ResetTelemetry();
            var exportReport = IfcStreamingJsonExporter.Export(ifcSourceFile, jsonTargetFile, preserveOrder);

            stopwatch.Stop();
            var managedMemoryAfter = GC.GetTotalMemory(forceFullCollection: false);

            if (verbosity is not Verbosity.None)
            {
                var telemetrySnapshot = IfcAccessors.GetTelemetrySnapshot();
                PrintExecutionReport(
                    ifcSourceFile,
                    jsonTargetFile,
                    preserveOrder,
                    exportReport,
                    telemetrySnapshot,
                    stopwatch.Elapsed,
                    managedMemoryAfter - managedMemoryBefore,
                    process.WorkingSet64,
                    process.PeakWorkingSet64);
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
        out Verbosity verbosity)
    {
        ifcSourceFile = null;
        jsonTargetFile = null;
        preserveOrder = true;
        verbosity = Verbosity.None;

        string sourcePath = null;
        string targetPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--preserve-order":
                    {
                        if (i + 1 >= args.Length || !bool.TryParse(args[i + 1], out preserveOrder))
                        {
                            return false;
                        }

                        i++;
                        break;
                    }
                case "--no-preserve-order":
                    {
                        preserveOrder = false;
                        break;
                    }
                case "--verbosity":
                    {
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
                    }
                default:
                    {
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

    private static bool TryParseVerbosity(string value, out Verbosity verbosity)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "none":
            case "quiet":
                verbosity = Verbosity.None;
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

    private static void PrintExecutionReport(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
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

    private static void PrintUsage()
    {
        Console.WriteLine("Please specify the path to the IFC and optional output json.");
        Console.WriteLine("Usage: ifc_metadata /path_to_file.ifc [/path_to_file.json] [--preserve-order true|false]");
        Console.WriteLine("Usage: ifc_metadata /path_to_file.ifc --no-preserve-order");
        Console.WriteLine("Usage: ifc_metadata /path_to_file.ifc [output.json] [--verbosity [summary|detailed|none]]");
        Console.WriteLine("Default: preserve order is true.");
        Console.WriteLine("Default verbosity: none.");
        Console.WriteLine("If output path is not passed, target defaults to source name with .json extension.");
    }

    private enum Verbosity
    {
        None,
        Detailed,
    }
}