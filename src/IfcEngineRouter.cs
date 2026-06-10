using System;
using System.IO;

using Bingosoft.Net.IfcMetadata.FastStep;

namespace Bingosoft.Net.IfcMetadata;

internal static class IfcEngineRouter
{
    internal delegate IfcExportReport IfcEngineExporter(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter);

    internal static IfcExportReport Export(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        IfcExportEngine engine,
        int outputFileBufferSize = IfcStreamingJsonExporter.DefaultOutputFileBufferSize,
        bool writeThrough = false,
        Action<int, int> progressReporter = null)
    {
        return Export(
            ifcSourceFile,
            jsonTargetFile,
            preserveOrder,
            engine,
            outputFileBufferSize,
            writeThrough,
            progressReporter,
            IfcStreamingJsonExporter.Export,
            FastStepJsonExporter.Export,
            static file => StepHeaderReader.Read(file).Schema);
    }

    internal static IfcExportReport Export(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        IfcExportEngine engine,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        IfcEngineExporter xbimExporter,
        IfcEngineExporter fastStepExporter,
        Func<FileInfo, string> fastStepSchemaReader)
    {
        switch (engine)
        {
            case IfcExportEngine.Xbim:
                {
                    var report = xbimExporter(ifcSourceFile, jsonTargetFile, preserveOrder, outputFileBufferSize, writeThrough, progressReporter);
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
                return ExportFastStepWithFallback(
                    ifcSourceFile,
                    jsonTargetFile,
                    preserveOrder,
                    outputFileBufferSize,
                    writeThrough,
                    progressReporter,
                    xbimExporter,
                    fastStepExporter,
                    fastStepSchemaReader);
            default:
                throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported export engine.");
        }
    }

    private static IfcExportReport ExportFastStepWithFallback(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        IfcEngineExporter xbimExporter,
        IfcEngineExporter fastStepExporter,
        Func<FileInfo, string> fastStepSchemaReader)
    {
        if (!TryGetSchema(fastStepSchemaReader, ifcSourceFile, out var schema, out var schemaReadError))
        {
            return ExportViaXbimWithDiagnostics(
                ifcSourceFile,
                jsonTargetFile,
                preserveOrder,
                outputFileBufferSize,
                writeThrough,
                progressReporter,
                xbimExporter,
                fastStepSchema: null,
                fallbackReason: $"SchemaReadFailed:{schemaReadError}",
                fastStepAttemptCount: 0);
        }

        if (!IsFastStepSupportedSchema(schema))
        {
            return ExportViaXbimWithDiagnostics(
                ifcSourceFile,
                jsonTargetFile,
                preserveOrder,
                outputFileBufferSize,
                writeThrough,
                progressReporter,
                xbimExporter,
                fastStepSchema: schema,
                fallbackReason: $"UnsupportedSchema:{schema}",
                fastStepAttemptCount: 0);
        }

        try
        {
            var fastStepReport = fastStepExporter(ifcSourceFile, jsonTargetFile, preserveOrder, outputFileBufferSize, writeThrough, progressReporter);
            return fastStepReport.WithExecutionDetails(new IfcEngineExecutionDetails(
                requestedEngine: IfcExportEngine.FastStep,
                effectiveEngine: IfcExportEngine.FastStep,
                fastStepRequestedCount: 1,
                fastStepAttemptCount: 1,
                fastStepSuccessCount: 1,
                xbimRunCount: 0,
                fallbackToXbimCount: 0,
                fallbackReason: null,
                fastStepSchema: schema));
        }
        catch (Exception ex)
        {
            return ExportViaXbimWithDiagnostics(
                ifcSourceFile,
                jsonTargetFile,
                preserveOrder,
                outputFileBufferSize,
                writeThrough,
                progressReporter,
                xbimExporter,
                fastStepSchema: schema,
                fallbackReason: $"FastStepFailed:{ex.GetType().Name}",
                fastStepAttemptCount: 1);
        }
    }

    private static IfcExportReport ExportViaXbimWithDiagnostics(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        IfcEngineExporter xbimExporter,
        string fastStepSchema,
        string fallbackReason,
        int fastStepAttemptCount)
    {
        var xbimReport = xbimExporter(ifcSourceFile, jsonTargetFile, preserveOrder, outputFileBufferSize, writeThrough, progressReporter);
        return xbimReport.WithExecutionDetails(new IfcEngineExecutionDetails(
            requestedEngine: IfcExportEngine.FastStep,
            effectiveEngine: IfcExportEngine.Xbim,
            fastStepRequestedCount: 1,
            fastStepAttemptCount: fastStepAttemptCount,
            fastStepSuccessCount: 0,
            xbimRunCount: 1,
            fallbackToXbimCount: 1,
            fallbackReason: fallbackReason,
            fastStepSchema: fastStepSchema));
    }

    private static bool TryGetSchema(Func<FileInfo, string> schemaReader, FileInfo ifcSourceFile, out string schema, out string error)
    {
        try
        {
            schema = schemaReader(ifcSourceFile);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            schema = string.Empty;
            error = ex.GetType().Name;
            return false;
        }
    }

    private static bool IsFastStepSupportedSchema(string schema)
    {
        return schema switch
        {
            null => false,
            _ when schema.StartsWith("IFC2X2", StringComparison.OrdinalIgnoreCase) => true,
            _ when schema.StartsWith("IFC2X3", StringComparison.OrdinalIgnoreCase) => true,
            _ when schema.StartsWith("IFC4X3", StringComparison.OrdinalIgnoreCase) => true,
            _ when schema.Equals("IFC4", StringComparison.OrdinalIgnoreCase) => true,
            _ => false,
        };
    }
}
