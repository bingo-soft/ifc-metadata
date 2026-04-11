using System;
using System.IO;

using Bingosoft.Net.IfcMetadata.FastStep;

namespace Bingosoft.Net.IfcMetadata;

internal static class FastStepJsonExporter
{
    internal static IfcExportReport Export(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter)
    {
        var scanResult = StepEntityScanner.ScanWithHeader(ifcSourceFile);

        return FastStepJsonEmitter.Export(
            scanResult.Indexes,
            scanResult.Header,
            jsonTargetFile,
            preserveOrder,
            outputFileBufferSize,
            writeThrough,
            progressReporter);
    }
}
