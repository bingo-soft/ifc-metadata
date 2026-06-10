using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

using Xbim.Ifc;
using Xbim.Ifc2x3.Kernel;

namespace Bingosoft.Net.IfcMetadata;

internal static class Ifc2x3StreamingJsonExporter
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static IfcExportReport Export(
        IfcStore model,
        IfcProject project,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter)
    {
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
}