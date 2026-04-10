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
        var indexes = StepEntityScanner.Scan(ifcSourceFile);
        var header = StepHeaderReader.Read(ifcSourceFile);

        return FastStepJsonEmitter.Export(
            indexes,
            header,
            jsonTargetFile,
            preserveOrder,
            outputFileBufferSize,
            writeThrough,
            progressReporter);
    }
}
