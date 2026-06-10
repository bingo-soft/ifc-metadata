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
        Action<int, int> progressReporter = null,
        Action<string> diagnosticsLogger = null)
    {
        diagnosticsLogger?.Invoke("xbim: open model start");
        using var model = IfcStore.Open(ifcSourceFile.FullName);
        diagnosticsLogger?.Invoke("xbim: open model complete");
        diagnosticsLogger?.Invoke("xbim: find project start");
        var project = model.Instances.FirstOrDefault<IIfcProject>()
                      ?? throw new InvalidOperationException("IFC project root (IIfcProject) was not found.");
        diagnosticsLogger?.Invoke("xbim: find project complete");

        var schemaVersion = model.Header.SchemaVersion;
        var isFallbackForced = IsFallbackForced();
        diagnosticsLogger?.Invoke($"xbim: schema={schemaVersion}");

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
        : this(schemaVersion, metaObjectCount, IfcEngineExecutionDetails.None, FastStepTelemetrySnapshot.Empty)
    {
    }

    internal IfcExportReport(string schemaVersion, int metaObjectCount, IfcEngineExecutionDetails executionDetails)
        : this(schemaVersion, metaObjectCount, executionDetails, FastStepTelemetrySnapshot.Empty)
    {
    }

    internal IfcExportReport(string schemaVersion, int metaObjectCount, FastStepTelemetrySnapshot fastStepTelemetry)
        : this(schemaVersion, metaObjectCount, IfcEngineExecutionDetails.None, fastStepTelemetry)
    {
    }

    private IfcExportReport(string schemaVersion, int metaObjectCount, IfcEngineExecutionDetails executionDetails, FastStepTelemetrySnapshot fastStepTelemetry)
    {
        SchemaVersion = schemaVersion;
        MetaObjectCount = metaObjectCount;
        ExecutionDetails = executionDetails;
        FastStepTelemetry = fastStepTelemetry;
    }

    internal string SchemaVersion { get; }

    internal int MetaObjectCount { get; }

    internal IfcEngineExecutionDetails ExecutionDetails { get; }

    internal FastStepTelemetrySnapshot FastStepTelemetry { get; }

    internal IfcExportReport WithExecutionDetails(IfcEngineExecutionDetails executionDetails)
    {
        return new IfcExportReport(SchemaVersion, MetaObjectCount, executionDetails, FastStepTelemetry);
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

internal readonly struct FastStepTelemetrySnapshot
{
    internal static readonly FastStepTelemetrySnapshot Empty = new(
        globalIdIndexHits: 0,
        globalIdRawFallbackHits: 0,
        globalIdMisses: 0,
        nameIndexHits: 0,
        nameRawFallbackHits: 0,
        nameMisses: 0,
        propertySetHits: 0,
        propertySetMisses: 0,
        materialHits: 0,
        materialMisses: 0,
        typeHits: 0,
        typeMisses: 0);

    internal FastStepTelemetrySnapshot(
        long globalIdIndexHits,
        long globalIdRawFallbackHits,
        long globalIdMisses,
        long nameIndexHits,
        long nameRawFallbackHits,
        long nameMisses,
        long propertySetHits,
        long propertySetMisses,
        long materialHits,
        long materialMisses,
        long typeHits,
        long typeMisses)
    {
        GlobalIdIndexHits = globalIdIndexHits;
        GlobalIdRawFallbackHits = globalIdRawFallbackHits;
        GlobalIdMisses = globalIdMisses;
        NameIndexHits = nameIndexHits;
        NameRawFallbackHits = nameRawFallbackHits;
        NameMisses = nameMisses;
        PropertySetHits = propertySetHits;
        PropertySetMisses = propertySetMisses;
        MaterialHits = materialHits;
        MaterialMisses = materialMisses;
        TypeHits = typeHits;
        TypeMisses = typeMisses;
    }

    internal long GlobalIdIndexHits { get; }

    internal long GlobalIdRawFallbackHits { get; }

    internal long GlobalIdMisses { get; }

    internal long NameIndexHits { get; }

    internal long NameRawFallbackHits { get; }

    internal long NameMisses { get; }

    internal long PropertySetHits { get; }

    internal long PropertySetMisses { get; }

    internal long MaterialHits { get; }

    internal long MaterialMisses { get; }

    internal long TypeHits { get; }

    internal long TypeMisses { get; }

    internal bool HasValues =>
        GlobalIdIndexHits != 0
        || GlobalIdRawFallbackHits != 0
        || GlobalIdMisses != 0
        || NameIndexHits != 0
        || NameRawFallbackHits != 0
        || NameMisses != 0
        || PropertySetHits != 0
        || PropertySetMisses != 0
        || MaterialHits != 0
        || MaterialMisses != 0
        || TypeHits != 0
        || TypeMisses != 0;
}
