using System;
using System.IO;
using Bingosoft.Net.IfcMetadata.FastStep;
using Bingosoft.Net.IfcMetadata.FastStep.Mmf;

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

    internal static IfcExportReport Export(
        FileInfo ifcSourceFile,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        FastStepScanOptions scanOptions)
    {
        var scanResult = StepEntityScanner.ScanWithHeader(ifcSourceFile, scanOptions);

        if (!scanOptions.UseMmfIntermediateStore)
        {
            return FastStepJsonEmitter.Export(
                scanResult.Indexes,
                scanResult.Header,
                jsonTargetFile,
                preserveOrder,
                outputFileBufferSize,
                writeThrough,
                progressReporter);
        }

        using var intermediateReader = new FastStepMmfIntermediateReader(scanOptions.MmfIntermediateDirectoryPath);

        return FastStepJsonEmitter.Export(
            scanResult.Indexes,
            scanResult.Header,
            jsonTargetFile,
            preserveOrder,
            outputFileBufferSize,
            writeThrough,
            progressReporter,
            intermediateReader);
    }
}

