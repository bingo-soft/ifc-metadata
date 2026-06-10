using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace Bingosoft.Net.IfcMetadata;

internal static class IfcStreamingJsonExporter
{
    internal const int DefaultOutputFileBufferSize = 512 * 1024;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static IfcExportReport Export(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize = DefaultOutputFileBufferSize,
        bool writeThrough = false,
        Action<int, int> progressReporter = null)
    {
        using var model = IfcStore.Open(ifcSourceFile.FullName);
        var project = model.Instances.FirstOrDefault<IIfcProject>()
                      ?? throw new InvalidOperationException("IFC project root (IIfcProject) was not found.");

        var schemaVersion = model.Header.SchemaVersion;
        var isFallbackForced = IsFallbackForced();

        if (!isFallbackForced && IfcSchemaRouter.IsIfc2x3(schemaVersion) && project is Xbim.Ifc2x3.Kernel.IfcProject ifc2x3Project)
        {
            return Ifc2x3StreamingJsonExporter.Export(
                model,
                ifc2x3Project,
                jsonTargetFile,
                preserveOrder,
                outputFileBufferSize,
                writeThrough,
                progressReporter);
        }

        if (!isFallbackForced && IfcSchemaRouter.IsIfc4(schemaVersion) && project is Xbim.Ifc4.Kernel.IfcProject ifc4Project)
        {
            return Ifc4StreamingJsonExporter.Export(
                model,
                ifc4Project,
                jsonTargetFile,
                preserveOrder,
                outputFileBufferSize,
                writeThrough,
                progressReporter);
        }

        return IfcStreamingExportUtilities.ExportWithSharedPipeline(
            model,
            project,
            jsonTargetFile,
            preserveOrder,
            outputFileBufferSize,
            writeThrough,
            progressReporter,
            WriterOptions);
    }


    internal static int CountMetaObjects(FileInfo ifcSourceFile, bool preserveOrder)
    {
        using var model = IfcStore.Open(ifcSourceFile.FullName);
        var project = model.Instances.FirstOrDefault<IIfcProject>()
                      ?? throw new InvalidOperationException("IFC project root (IIfcProject) was not found.");

        var counts = IfcStreamingExportUtilities.BuildObjectIdCounts(project, preserveOrder);
        return counts.Count;
    }

    private static bool IsFallbackForced()
    {
        var value = Environment.GetEnvironmentVariable("IFC_FORCE_FALLBACK");
        return string.Equals(value, "1", StringComparison.Ordinal)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}

internal readonly struct IfcExportReport
{
    internal IfcExportReport(string schemaVersion, int metaObjectCount)
        : this(schemaVersion, metaObjectCount, IfcEngineExecutionDetails.None)
    {
    }

    internal IfcExportReport(string schemaVersion, int metaObjectCount, IfcEngineExecutionDetails executionDetails)
    {
        SchemaVersion = schemaVersion;
        MetaObjectCount = metaObjectCount;
        ExecutionDetails = executionDetails;
    }

    internal string SchemaVersion { get; }

    internal int MetaObjectCount { get; }

    internal IfcEngineExecutionDetails ExecutionDetails { get; }

    internal IfcExportReport WithExecutionDetails(IfcEngineExecutionDetails executionDetails)
    {
        return new IfcExportReport(SchemaVersion, MetaObjectCount, executionDetails);
    }
}

internal readonly struct IfcEngineExecutionDetails
{
    internal static readonly IfcEngineExecutionDetails None = new(
        requestedEngine: IfcExportEngine.Xbim,
        effectiveEngine: IfcExportEngine.Xbim,
        fastStepRequestedCount: 0,
        fastStepAttemptCount: 0,
        fastStepSuccessCount: 0,
        xbimRunCount: 0,
        fallbackToXbimCount: 0,
        fallbackReason: null,
        fastStepSchema: null);

    internal IfcEngineExecutionDetails(
        IfcExportEngine requestedEngine,
        IfcExportEngine effectiveEngine,
        int fastStepRequestedCount,
        int fastStepAttemptCount,
        int fastStepSuccessCount,
        int xbimRunCount,
        int fallbackToXbimCount,
        string fallbackReason,
        string fastStepSchema)
    {
        RequestedEngine = requestedEngine;
        EffectiveEngine = effectiveEngine;
        FastStepRequestedCount = fastStepRequestedCount;
        FastStepAttemptCount = fastStepAttemptCount;
        FastStepSuccessCount = fastStepSuccessCount;
        XbimRunCount = xbimRunCount;
        FallbackToXbimCount = fallbackToXbimCount;
        FallbackReason = fallbackReason;
        FastStepSchema = fastStepSchema;
    }

    internal IfcExportEngine RequestedEngine { get; }

    internal IfcExportEngine EffectiveEngine { get; }

    internal int FastStepRequestedCount { get; }

    internal int FastStepAttemptCount { get; }

    internal int FastStepSuccessCount { get; }

    internal int XbimRunCount { get; }

    internal int FallbackToXbimCount { get; }

    internal string FallbackReason { get; }

    internal string FastStepSchema { get; }
}