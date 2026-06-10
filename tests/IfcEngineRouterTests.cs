using System;
using System.IO;

using Bingosoft.Net.IfcMetadata;

using Xunit;

namespace IfcMetadata.Tests;

public sealed class IfcEngineRouterTests
{
    [Fact]
    public void Export_UsesXbim_WhenEngineIsXbim()
    {
        var source = new FileInfo("source.ifc");
        var target = new FileInfo("target.json");
        var xbimCalls = 0;
        var fastCalls = 0;

        var report = IfcEngineRouter.Export(
            source,
            target,
            preserveOrder: true,
            engine: IfcExportEngine.Xbim,
            outputFileBufferSize: 1024,
            writeThrough: false,
            progressReporter: null,
            diagnosticsLogger: null,
            xbimExporter: (_, _, _, _, _, _, _) =>
            {
                xbimCalls++;
                return new IfcExportReport("xbim", 1);
            },
            fastStepExporter: (_, _, _, _, _, _, _) =>
            {
                fastCalls++;
                return new IfcExportReport("fast", 1);
            });

        Assert.Equal("xbim", report.SchemaVersion);
        Assert.Equal(IfcExportEngine.Xbim, report.ExecutionDetails.EffectiveEngine);
        Assert.Equal(0, report.ExecutionDetails.FastStepAttemptCount);
        Assert.Equal(1, report.ExecutionDetails.XbimRunCount);
        Assert.Equal(0, report.ExecutionDetails.FallbackToXbimCount);
        Assert.Equal(1, xbimCalls);
        Assert.Equal(0, fastCalls);
    }

    [Fact]
    public void Export_UsesFastStep_WithoutReadingSchemaFirst()
    {
        var source = new FileInfo("source.ifc");
        var target = new FileInfo("target.json");
        var xbimCalls = 0;
        var fastCalls = 0;

        var report = IfcEngineRouter.Export(
            source,
            target,
            preserveOrder: true,
            engine: IfcExportEngine.FastStep,
            outputFileBufferSize: 1024,
            writeThrough: false,
            progressReporter: null,
            diagnosticsLogger: null,
            xbimExporter: (_, _, _, _, _, _, _) =>
            {
                xbimCalls++;
                return new IfcExportReport("xbim", 2);
            },
            fastStepExporter: (_, _, _, _, _, _, _) =>
            {
                fastCalls++;
                return new IfcExportReport("fast", 2);
            });

        Assert.Equal("fast", report.SchemaVersion);
        Assert.Equal(IfcExportEngine.FastStep, report.ExecutionDetails.EffectiveEngine);
        Assert.Equal(1, report.ExecutionDetails.FastStepAttemptCount);
        Assert.Equal(1, report.ExecutionDetails.FastStepSuccessCount);
        Assert.Equal(0, report.ExecutionDetails.FallbackToXbimCount);
        Assert.Null(report.ExecutionDetails.FallbackReason);
        Assert.Equal("fast", report.ExecutionDetails.FastStepSchema);
        Assert.Equal(0, xbimCalls);
        Assert.Equal(1, fastCalls);
    }

    [Fact]
    public void Export_PropagatesFastStepError_WithoutFallback()
    {
        var source = new FileInfo("source.ifc");
        var target = new FileInfo("target.json");
        var xbimCalls = 0;
        var fastCalls = 0;

        var exception = Assert.Throws<InvalidOperationException>(() => IfcEngineRouter.Export(
            source,
            target,
            preserveOrder: true,
            engine: IfcExportEngine.FastStep,
            outputFileBufferSize: 1024,
            writeThrough: false,
            progressReporter: null,
            diagnosticsLogger: null,
            xbimExporter: (_, _, _, _, _, _, _) =>
            {
                xbimCalls++;
                return new IfcExportReport("xbim", 3);
            },
            fastStepExporter: (_, _, _, _, _, _, _) =>
            {
                fastCalls++;
                throw new InvalidOperationException("fast-step failed");
            }));

        Assert.Equal("fast-step failed", exception.Message);
        Assert.Equal(0, xbimCalls);
        Assert.Equal(1, fastCalls);
    }
}
