using System;
using System.IO;

namespace Bingosoft.Net.IfcMetadata;

internal static class IfcEngineRouter
{
    internal delegate IfcExportReport IfcEngineExporter(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        Action<string> diagnosticsLogger);

    internal static IfcExportReport Export(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        IfcExportEngine engine,
        int outputFileBufferSize = IfcStreamingJsonExporter.DefaultOutputFileBufferSize,
        bool writeThrough = false,
        Action<int, int> progressReporter = null,
        Action<string> diagnosticsLogger = null)
    {
        return Export(
            ifcSourceFile,
            jsonTargetFile,
            preserveOrder,
            engine,
            outputFileBufferSize,
            writeThrough,
            progressReporter,
            diagnosticsLogger,
            IfcStreamingJsonExporter.Export,
            FastStepJsonExporter.Export);
    }

    internal static IfcExportReport Export(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        IfcExportEngine engine,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        Action<string> diagnosticsLogger,
        IfcEngineExporter xbimExporter,
        IfcEngineExporter fastStepExporter)
    {
        switch (engine)
        {
            case IfcExportEngine.Xbim:
                {
                    diagnosticsLogger?.Invoke("router: selected xbim");
                    var report = xbimExporter(ifcSourceFile, jsonTargetFile, preserveOrder, outputFileBufferSize, writeThrough, progressReporter, diagnosticsLogger);
                    return report.WithExecutionDetails(new IfcEngineExecutionDetails(
                        requestedEngine: IfcExportEngine.Xbim,
                        effectiveEngine: IfcExportEngine.Xbim,
                        fastStepRequestedCount: 0,
                        fastStepAttemptCount: 0,
                        fastStepSuccessCount: 0,
                        xbimRunCount: 1,
                        fallbackToXbimCount: 0,
                        fallbackReason: null,
                        fastStepSchema: null));
                }
            case IfcExportEngine.FastStep:
                {
                    diagnosticsLogger?.Invoke("router: selected fast-step");
                    var report = fastStepExporter(ifcSourceFile, jsonTargetFile, preserveOrder, outputFileBufferSize, writeThrough, progressReporter, diagnosticsLogger);
                    return report.WithExecutionDetails(new IfcEngineExecutionDetails(
                        requestedEngine: IfcExportEngine.FastStep,
                        effectiveEngine: IfcExportEngine.FastStep,
                        fastStepRequestedCount: 1,
                        fastStepAttemptCount: 1,
                        fastStepSuccessCount: 1,
                        xbimRunCount: 0,
                        fallbackToXbimCount: 0,
                        fallbackReason: null,
                        fastStepSchema: report.SchemaVersion));
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported export engine.");
        }
    }
}
