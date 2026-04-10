using System;
using System.IO;

namespace Bingosoft.Net.IfcMetadata;

internal static class IfcEngineRouter
{
    internal static IfcExportReport Export(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        IfcExportEngine engine,
        int outputFileBufferSize = IfcStreamingJsonExporter.DefaultOutputFileBufferSize,
        bool writeThrough = false,
        Action<int, int> progressReporter = null)
    {
        return engine switch
        {
            IfcExportEngine.Xbim => IfcStreamingJsonExporter.Export(
                ifcSourceFile,
                jsonTargetFile,
                preserveOrder,
                outputFileBufferSize,
                writeThrough,
                progressReporter),
            IfcExportEngine.FastStep => FastStepJsonExporter.Export(
                ifcSourceFile,
                jsonTargetFile,
                preserveOrder,
                outputFileBufferSize,
                writeThrough,
                progressReporter),
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported export engine."),
        };
    }
}
