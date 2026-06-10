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
            xbimExporter: (_, _, _, _, _, _) =>
            {
                xbimCalls++;
                return new IfcExportReport("xbim", 1);
            },
            fastStepExporter: (_, _, _, _, _, _) =>
            {
                fastCalls++;
                return new IfcExportReport("fast", 1);
            },
            fastStepSchemaReader: _ => "IFC4");

        Assert.Equal("xbim", report.SchemaVersion);
        Assert.Equal(IfcExportEngine.Xbim, report.ExecutionDetails.EffectiveEngine);
        Assert.Equal(0, report.ExecutionDetails.FastStepAttemptCount);
        Assert.Equal(1, report.ExecutionDetails.XbimRunCount);
        Assert.Equal(0, report.ExecutionDetails.FallbackToXbimCount);
        Assert.Equal(1, xbimCalls);
        Assert.Equal(0, fastCalls);
    }

    [Fact]
    public void Export_FallsBackToXbim_WhenSchemaIsUnsupported()
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
            xbimExporter: (_, _, _, _, _, _) =>
            {
                xbimCalls++;
                return new IfcExportReport("xbim", 2);
            },
            fastStepExporter: (_, _, _, _, _, _) =>
            {
                fastCalls++;
                return new IfcExportReport("fast", 2);
            },
            fastStepSchemaReader: _ => "IFC5");

        Assert.Equal("xbim", report.SchemaVersion);
        Assert.Equal(IfcExportEngine.Xbim, report.ExecutionDetails.EffectiveEngine);
        Assert.Equal(0, report.ExecutionDetails.FastStepAttemptCount);
        Assert.Equal(1, report.ExecutionDetails.FallbackToXbimCount);
        Assert.StartsWith("UnsupportedSchema:", report.ExecutionDetails.FallbackReason);
        Assert.Equal(1, xbimCalls);
        Assert.Equal(0, fastCalls);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    [InlineData("IFC4X3_ADD2")]
    [InlineData("IFC2X2_FINAL")]
    public void Export_UsesFastStep_WhenSchemaIsSupported(string schema)
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
            xbimExporter: (_, _, _, _, _, _) =>
            {
                xbimCalls++;
                return new IfcExportReport("xbim", 3);
            },
            fastStepExporter: (_, _, _, _, _, _) =>
            {
                fastCalls++;
                return new IfcExportReport("fast", 3);
            },
            fastStepSchemaReader: _ => schema);

        Assert.Equal("fast", report.SchemaVersion);
        Assert.Equal(IfcExportEngine.FastStep, report.ExecutionDetails.EffectiveEngine);
        Assert.Equal(1, report.ExecutionDetails.FastStepAttemptCount);
        Assert.Equal(1, report.ExecutionDetails.FastStepSuccessCount);
        Assert.Equal(0, report.ExecutionDetails.FallbackToXbimCount);
        Assert.Equal(0, xbimCalls);
        Assert.Equal(1, fastCalls);
    }

    [Fact]
    public void Export_FallsBackToXbim_WhenFastStepThrows()
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
            xbimExporter: (_, _, _, _, _, _) =>
            {
                xbimCalls++;
                return new IfcExportReport("xbim", 4);
            },
            fastStepExporter: (_, _, _, _, _, _) =>
            {
                fastCalls++;
                throw new InvalidOperationException("fast-step failed");
            },
            fastStepSchemaReader: _ => "IFC2X3");

        Assert.Equal("xbim", report.SchemaVersion);
        Assert.Equal(IfcExportEngine.Xbim, report.ExecutionDetails.EffectiveEngine);
        Assert.Equal(1, report.ExecutionDetails.FastStepAttemptCount);
        Assert.Equal(1, report.ExecutionDetails.FallbackToXbimCount);
        Assert.StartsWith("FastStepFailed:", report.ExecutionDetails.FallbackReason);
        Assert.Equal(1, xbimCalls);
        Assert.Equal(1, fastCalls);
    }

    [Fact]
    public void Export_FallsBackToXbim_WhenSchemaReadThrows()
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
            xbimExporter: (_, _, _, _, _, _) =>
            {
                xbimCalls++;
                return new IfcExportReport("xbim", 5);
            },
            fastStepExporter: (_, _, _, _, _, _) =>
            {
                fastCalls++;
                return new IfcExportReport("fast", 5);
            },
            fastStepSchemaReader: _ => throw new InvalidOperationException("header read failed"));

        Assert.Equal("xbim", report.SchemaVersion);
        Assert.Equal(IfcExportEngine.Xbim, report.ExecutionDetails.EffectiveEngine);
        Assert.Equal(0, report.ExecutionDetails.FastStepAttemptCount);
        Assert.Equal(1, report.ExecutionDetails.FallbackToXbimCount);
        Assert.StartsWith("SchemaReadFailed:", report.ExecutionDetails.FallbackReason);
        Assert.Equal(1, xbimCalls);
        Assert.Equal(0, fastCalls);
    }
}
