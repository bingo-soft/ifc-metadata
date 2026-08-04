using System;
using System.Diagnostics;
using System.IO;

using Bingosoft.Net.IfcMetadata.FastStep;
using Bingosoft.Net.IfcMetadata.FastStep.Mmf;

namespace Bingosoft.Net.IfcMetadata;

internal static class FastStepJsonExporter
{
    internal static IfcExportReport Export(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        Action<string> diagnosticsLogger = null)
    {
        diagnosticsLogger?.Invoke("fast-step: scan start");
        var scanStopwatch = Stopwatch.StartNew();
        var scanResult = StepEntityScanner.ScanWithHeader(
            ifcSourceFile,
            FastStepScanOptions.Default with
            {
                ProgressReporter = CreatePhaseProgressReporter(progressReporter, 0, 5000),
                DiagnosticsLogger = diagnosticsLogger,
            });
        scanStopwatch.Stop();
        diagnosticsLogger?.Invoke($"fast-step: scan complete schema={scanResult.Header.Schema} entities={scanResult.Indexes.EntityCount} elapsedMs={scanStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");
        LogScanShapeDiagnostics(scanResult, diagnosticsLogger);

        diagnosticsLogger?.Invoke("fast-step: emit start");
        var emitStopwatch = Stopwatch.StartNew();
        var report = FastStepJsonEmitter.Export(
            scanResult.Indexes,
            scanResult.Header,
            jsonTargetFile,
            preserveOrder,
            outputFileBufferSize,
            writeThrough,
            CreatePhaseProgressReporter(progressReporter, 5000, 10000),
            diagnosticsLogger: diagnosticsLogger);
        emitStopwatch.Stop();
        diagnosticsLogger?.Invoke($"fast-step: emit complete metaObjects={report.MetaObjectCount} elapsedMs={emitStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");
        return report;
    }

    internal static IfcExportReport Export(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        FastStepScanOptions scanOptions,
        Action<string> diagnosticsLogger = null)
    {
        var scanProgressReporter = scanOptions.ProgressReporter ?? CreatePhaseProgressReporter(progressReporter, 0, 5000);
        var emitProgressReporter = CreatePhaseProgressReporter(progressReporter, 5000, 10000);
        diagnosticsLogger?.Invoke("fast-step: scan start");
        var scanStopwatch = Stopwatch.StartNew();
        var scanResult = StepEntityScanner.ScanWithHeader(ifcSourceFile, scanOptions with { ProgressReporter = scanProgressReporter, DiagnosticsLogger = diagnosticsLogger });
        scanStopwatch.Stop();
        diagnosticsLogger?.Invoke($"fast-step: scan complete schema={scanResult.Header.Schema} entities={scanResult.Indexes.EntityCount} elapsedMs={scanStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");
        LogScanShapeDiagnostics(scanResult, diagnosticsLogger);

        if (!scanOptions.UseMmfIntermediateStore)
        {
            diagnosticsLogger?.Invoke("fast-step: emit start");
            var emitStopwatch = Stopwatch.StartNew();
            var report = FastStepJsonEmitter.Export(
                scanResult.Indexes,
                scanResult.Header,
                jsonTargetFile,
                preserveOrder,
                outputFileBufferSize,
                writeThrough,
                emitProgressReporter,
                diagnosticsLogger: diagnosticsLogger);
            emitStopwatch.Stop();
            diagnosticsLogger?.Invoke($"fast-step: emit complete metaObjects={report.MetaObjectCount} elapsedMs={emitStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");
            return report;
        }

        diagnosticsLogger?.Invoke("fast-step: open mmf intermediate reader start");
        using var intermediateReader = new FastStepMmfIntermediateReader(scanOptions.MmfIntermediateDirectoryPath);
        diagnosticsLogger?.Invoke("fast-step: open mmf intermediate reader complete");

        diagnosticsLogger?.Invoke("fast-step: emit start");
        var spillEmitStopwatch = Stopwatch.StartNew();
        var spillReport = FastStepJsonEmitter.Export(
            scanResult.Indexes,
            scanResult.Header,
            jsonTargetFile,
            preserveOrder,
            outputFileBufferSize,
            writeThrough,
            emitProgressReporter,
            intermediateReader,
            diagnosticsLogger);
        spillEmitStopwatch.Stop();
        diagnosticsLogger?.Invoke($"fast-step: emit complete metaObjects={spillReport.MetaObjectCount} elapsedMs={spillEmitStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");
        return spillReport;
    }

    private static void LogScanShapeDiagnostics(FastStepScanResult scanResult, Action<string> diagnosticsLogger)
    {
        if (diagnosticsLogger is null)
        {
            return;
        }

        if (scanResult.Indexes.EntityCount == 0)
        {
            diagnosticsLogger("fast-step scan warning: scan found 0 STEP entities; check STEP entity assignment formatting and DATA section contents.");
            return;
        }

        if (scanResult.Indexes.Project is null)
        {
            diagnosticsLogger($"fast-step scan warning: IFCPROJECT was not found after scanning {scanResult.Indexes.EntityCount} STEP entities.");
        }
        else if (string.IsNullOrWhiteSpace(scanResult.Indexes.Project.Value.GlobalId))
        {
            diagnosticsLogger($"fast-step scan warning: IFCPROJECT #{scanResult.Indexes.Project.Value.EntityId} was found, but its GlobalId is empty.");
        }
    }

    private static Action<int, int> CreatePhaseProgressReporter(Action<int, int> progressReporter, int phaseStart, int phaseEnd)
    {
        if (progressReporter is null)
        {
            return null;
        }

        return (processed, total) =>
        {
            if (total <= 0)
            {
                return;
            }

            var phaseLength = phaseEnd - phaseStart;
            var phaseProcessed = (int)Math.Min(phaseLength, (processed * (long)phaseLength) / total);
            progressReporter(phaseStart + phaseProcessed, 10000);
        };
    }
}
