using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bingosoft.Net.IfcMetadata.FastStep.Mmf;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal static class FastStepJsonEmitter
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly IComparer<ChildSortEntry> ChildSortEntryComparer = Comparer<ChildSortEntry>.Create(
        static (left, right) => StringComparer.Ordinal.Compare(left.GlobalId, right.GlobalId));

    internal static IfcExportReport Export(
        FastStepIndexes indexes,
        FastStepHeader header,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        FastStepMmfIntermediateReader intermediateReader = null,
        Action<string> diagnosticsLogger = null)
    {
        if (indexes.Project is null || string.IsNullOrWhiteSpace(indexes.Project.Value.GlobalId))
        {
            throw new InvalidOperationException("IFC project root (IFCPROJECT) was not found in STEP data.");
        }

        return intermediateReader is null
            ? ExportWithoutSpill(indexes, header, jsonTargetFile, preserveOrder, outputFileBufferSize, writeThrough, progressReporter, diagnosticsLogger)
            : ExportWithSpill(indexes, header, jsonTargetFile, preserveOrder, outputFileBufferSize, writeThrough, progressReporter, intermediateReader, diagnosticsLogger);
    }

    private static IfcExportReport ExportWithoutSpill(
        FastStepIndexes indexes,
        FastStepHeader header,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        Action<string> diagnosticsLogger)
    {
        var project = indexes.Project.Value;
        var telemetry = new FastStepTelemetry();
        var relationAdjacency = new FastStepRelationAdjacency(indexes.DecompositionAdjacency, indexes.ContainmentAdjacency);
        diagnosticsLogger?.Invoke("fast-step emit: build traversal order start");
        var traversalStopwatch = Stopwatch.StartNew();
        var orderedNodes = BuildLastNodesByVisitOrder(indexes, project, preserveOrder, relationAdjacency, intermediateReader: null);
        traversalStopwatch.Stop();
        var uniqueMetaObjects = orderedNodes.Count;
        diagnosticsLogger?.Invoke($"fast-step emit: build traversal order complete uniqueMetaObjects={uniqueMetaObjects} elapsedMs={traversalStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");
        diagnosticsLogger?.Invoke("fast-step emit: build mapping cache start");
        var mappingStopwatch = Stopwatch.StartNew();
        var mappings = FastStepMappingCache.Build(indexes);
        mappingStopwatch.Stop();
        diagnosticsLogger?.Invoke($"fast-step emit: build mapping cache complete elapsedMs={mappingStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");

        diagnosticsLogger?.Invoke("fast-step emit: open output stream start");
        using var stream = IfcStreamingExportUtilities.OpenOutputStream(jsonTargetFile, outputFileBufferSize, writeThrough);
        using var writer = new Utf8JsonWriter(stream, WriterOptions);
        diagnosticsLogger?.Invoke("fast-step emit: open output stream complete");

        writer.WriteStartObject();
        writer.WriteString("id", project.Name);
        writer.WriteString("projectId", project.GlobalId);
        writer.WriteString("author", header.Author ?? string.Empty);
        writer.WriteString("createdAt", header.CreatedAt);
        writer.WriteString("schema", header.Schema);
        writer.WriteString("creatingApplication", header.CreatingApplication);
        writer.WriteStartObject("metaObjects");

        var processedMetaObjects = 0;
        progressReporter?.Invoke(processedMetaObjects, uniqueMetaObjects);

        diagnosticsLogger?.Invoke("fast-step emit: write metaObjects start");
        var writeStopwatch = Stopwatch.StartNew();
        const int emitBatchSize = 4096;
        foreach (var node in orderedNodes.Values)
        {
            WriteMetaObject(writer, indexes, mappings, project, node, intermediateReader: null, telemetry);

            processedMetaObjects++;
            progressReporter?.Invoke(processedMetaObjects, uniqueMetaObjects);

            if (processedMetaObjects % emitBatchSize == 0)
            {
                writer.Flush();
            }
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        stream.Flush();
        writeStopwatch.Stop();
        diagnosticsLogger?.Invoke($"fast-step emit: write metaObjects complete processedMetaObjects={processedMetaObjects} elapsedMs={writeStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");

        return new IfcExportReport(header.Schema, uniqueMetaObjects, telemetry.GetSnapshot());
    }

    private static IfcExportReport ExportWithSpill(
        FastStepIndexes indexes,
        FastStepHeader header,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        FastStepMmfIntermediateReader intermediateReader,
        Action<string> diagnosticsLogger)
    {
        var project = indexes.Project.Value;
        var telemetry = new FastStepTelemetry();
        var relationAdjacency = new FastStepRelationAdjacency(indexes.DecompositionAdjacency, indexes.ContainmentAdjacency);

        diagnosticsLogger?.Invoke("fast-step emit: cleanup object segments start");
        CleanupObjectSegments(intermediateReader.DirectoryPath);
        diagnosticsLogger?.Invoke("fast-step emit: cleanup object segments complete");
        diagnosticsLogger?.Invoke("fast-step emit: build traversal order spill start");
        var traversalStopwatch = Stopwatch.StartNew();
        var lastOrderByObjectToken = BuildLastVisitOrderAndSpill(indexes, project, preserveOrder, relationAdjacency, intermediateReader);
        traversalStopwatch.Stop();
        var uniqueMetaObjects = lastOrderByObjectToken.Count;
        diagnosticsLogger?.Invoke($"fast-step emit: build traversal order spill complete uniqueMetaObjects={uniqueMetaObjects} elapsedMs={traversalStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");
        diagnosticsLogger?.Invoke("fast-step emit: build mapping cache start");
        var mappingStopwatch = Stopwatch.StartNew();
        var mappings = FastStepMappingCache.Build(indexes);
        mappingStopwatch.Stop();
        diagnosticsLogger?.Invoke($"fast-step emit: build mapping cache complete elapsedMs={mappingStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");

        diagnosticsLogger?.Invoke("fast-step emit: open output stream start");
        using var stream = IfcStreamingExportUtilities.OpenOutputStream(jsonTargetFile, outputFileBufferSize, writeThrough);
        using var writer = new Utf8JsonWriter(stream, WriterOptions);
        diagnosticsLogger?.Invoke("fast-step emit: open output stream complete");

        writer.WriteStartObject();
        writer.WriteString("id", project.Name);
        writer.WriteString("projectId", project.GlobalId);
        writer.WriteString("author", header.Author ?? string.Empty);
        writer.WriteString("createdAt", header.CreatedAt);
        writer.WriteString("schema", header.Schema);
        writer.WriteString("creatingApplication", header.CreatingApplication);
        writer.WriteStartObject("metaObjects");

        var processedMetaObjects = 0;
        progressReporter?.Invoke(processedMetaObjects, uniqueMetaObjects);

        diagnosticsLogger?.Invoke("fast-step emit: write metaObjects start");
        var writeStopwatch = Stopwatch.StartNew();
        const int emitBatchSize = 4096;
        foreach (var record in intermediateReader.EnumerateObjectRecords())
        {
            if (!TryGetGlobalIdToken(indexes, intermediateReader, record.EntityId, cache: null, out var objectToken)
                || !lastOrderByObjectToken.TryGetValue(objectToken, out var lastOrder)
                || lastOrder != record.OutputOrder)
            {
                continue;
            }

            WriteMetaObject(
                writer,
                indexes,
                mappings,
                project,
                new FastStepTraversalNode(record.EntityId, record.PayloadLength),
                intermediateReader,
                telemetry);

            processedMetaObjects++;
            progressReporter?.Invoke(processedMetaObjects, uniqueMetaObjects);

            if (processedMetaObjects % emitBatchSize == 0)
            {
                writer.Flush();
            }
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        stream.Flush();
        writeStopwatch.Stop();
        diagnosticsLogger?.Invoke($"fast-step emit: write metaObjects complete processedMetaObjects={processedMetaObjects} elapsedMs={writeStopwatch.Elapsed.TotalMilliseconds.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");

        return new IfcExportReport(header.Schema, uniqueMetaObjects, telemetry.GetSnapshot());
    }

    private static Dictionary<ulong, int> BuildLastVisitOrderAndSpill(
        FastStepIndexes indexes,
        FastStepProjectRecord project,
        bool preserveOrder,
        FastStepRelationAdjacency relationAdjacency,
        FastStepMmfIntermediateReader intermediateReader)
    {
        var stack = new Stack<FastStepTraversalNode>();
        var lastOrderByObjectToken = new Dictionary<ulong, int>();
        var globalIdCache = new Dictionary<int, string>();

        using var objectStore = new FastStepMmfObjectStore(intermediateReader.DirectoryPath);
        var visitOrder = 0;
        stack.Push(new FastStepTraversalNode(project.EntityId, ParentEntityId: -1));

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (TryGetGlobalIdToken(indexes, intermediateReader, current.EntityId, globalIdCache, out var objectToken))
            {
                lastOrderByObjectToken[objectToken] = visitOrder;
                objectStore.Append(new FastStepObjectRecord
                {
                    EntityId = current.EntityId,
                    TypeToken = 0,
                    PayloadSegmentId = 0,
                    PayloadOffset = 0,
                    PayloadLength = current.ParentEntityId,
                    OutputOrder = visitOrder,
                    Flags = (uint)(FastStepObjectFlags.ReadyToEmit | FastStepObjectFlags.Ordered),
                    Reserved = 0,
                });

                visitOrder++;
            }

            PushChildren(stack, indexes, relationAdjacency.Decomposition, current, preserveOrder, intermediateReader, globalIdCache);
            PushChildren(stack, indexes, relationAdjacency.Containment, current, preserveOrder, intermediateReader, globalIdCache);
        }

        return lastOrderByObjectToken;
    }

    private static SortedDictionary<int, FastStepTraversalNode> BuildLastNodesByVisitOrder(
        FastStepIndexes indexes,
        FastStepProjectRecord project,
        bool preserveOrder,
        FastStepRelationAdjacency relationAdjacency,
        FastStepMmfIntermediateReader intermediateReader)
    {
        var stack = new Stack<FastStepTraversalNode>();
        var orderByObjectToken = new Dictionary<ulong, int>();
        var nodesByOrder = new SortedDictionary<int, FastStepTraversalNode>();
        var globalIdCache = new Dictionary<int, string>();
        var visitOrder = 0;

        stack.Push(new FastStepTraversalNode(project.EntityId, ParentEntityId: -1));

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (TryGetGlobalIdToken(indexes, intermediateReader, current.EntityId, globalIdCache, out var objectToken))
            {
                if (orderByObjectToken.TryGetValue(objectToken, out var previousOrder))
                {
                    nodesByOrder.Remove(previousOrder);
                }

                orderByObjectToken[objectToken] = visitOrder;
                nodesByOrder[visitOrder] = current;
                visitOrder++;
            }

            PushChildren(stack, indexes, relationAdjacency.Decomposition, current, preserveOrder, intermediateReader, globalIdCache);
            PushChildren(stack, indexes, relationAdjacency.Containment, current, preserveOrder, intermediateReader, globalIdCache);
        }

        return nodesByOrder;
    }

    private static void CleanupObjectSegments(string directoryPath)
    {
        var files = Directory.GetFiles(directoryPath, "object_*.seg", SearchOption.TopDirectoryOnly);
        for (var i = 0; i < files.Length; i++)
        {
            File.Delete(files[i]);
        }
    }

    private static void PushChildren(
        Stack<FastStepTraversalNode> stack,
        FastStepIndexes indexes,
        FastStepAdjacency adjacency,
        FastStepTraversalNode current,
        bool preserveOrder,
        FastStepMmfIntermediateReader intermediateReader,
        Dictionary<int, string> globalIdCache)
    {
        var parentSlot = indexes.GetSlotOrMissing(current.EntityId);
        if (parentSlot < 0 || parentSlot + 1 >= adjacency.Offsets.Length)
        {
            return;
        }

        var start = adjacency.Offsets[parentSlot];
        var end = adjacency.Offsets[parentSlot + 1];
        if (start >= end)
        {
            return;
        }

        if (!preserveOrder)
        {
            for (var edgeIndex = end - 1; edgeIndex >= start; edgeIndex--)
            {
                var childSlot = adjacency.Edges[edgeIndex];
                var childEntityId = indexes.EntityIdsBySlot[childSlot];
                stack.Push(new FastStepTraversalNode(childEntityId, current.EntityId));
            }

            return;
        }

        var childCount = end - start;
        var pooledChildren = ArrayPool<ChildSortEntry>.Shared.Rent(childCount);

        try
        {
            var childIndex = 0;
            for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                var childEntityId = indexes.EntityIdsBySlot[adjacency.Edges[edgeIndex]];
                var childGlobalId = GetGlobalIdCached(indexes, intermediateReader, childEntityId, globalIdCache);
                pooledChildren[childIndex++] = new ChildSortEntry(childEntityId, childGlobalId);
            }

            PushChildrenOrdered(stack, pooledChildren, childCount, current.EntityId);
        }
        finally
        {
            Array.Clear(pooledChildren, 0, childCount);
            ArrayPool<ChildSortEntry>.Shared.Return(pooledChildren, clearArray: false);
        }
    }

    private static void PushChildrenOrdered(
        Stack<FastStepTraversalNode> stack,
        ChildSortEntry[] children,
        int childCount,
        int parentEntityId)
    {
        if (childCount == 0)
        {
            return;
        }

        Array.Sort(children, 0, childCount, ChildSortEntryComparer);

        for (var i = childCount - 1; i >= 0; i--)
        {
            stack.Push(new FastStepTraversalNode(children[i].EntityId, parentEntityId));
        }
    }

    private static void WriteMetaObject(
        Utf8JsonWriter writer,
        FastStepIndexes indexes,
        FastStepMappingCache mappings,
        FastStepProjectRecord project,
        FastStepTraversalNode node,
        FastStepMmfIntermediateReader intermediateReader,
        FastStepTelemetry telemetry)
    {
        var objectId = GetGlobalId(indexes, intermediateReader, node.EntityId, telemetry);
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        var parentObjectId = node.ParentEntityId < 0
            ? null
            : GetGlobalId(indexes, intermediateReader, node.ParentEntityId, telemetry);

        writer.WritePropertyName(objectId);
        writer.WriteStartObject();

        writer.WriteString("id", objectId);
        writer.WriteString("name", GetName(indexes, intermediateReader, node.EntityId, telemetry));
        writer.WriteString("type", mappings.GetTypeName(node.EntityId));
        writer.WriteString("parent", parentObjectId);

        if (string.Equals(objectId, project.GlobalId, StringComparison.Ordinal))
        {
            writer.WriteNull("properties");
        }
        else if (mappings.PropertySetByObjectId.TryGetValue(node.EntityId, out var psetIds) && psetIds.Count > 0)
        {
            telemetry.PropertySetHits++;
            writer.WritePropertyName("properties");
            writer.WriteStartArray();
            for (var i = 0; i < psetIds.Count; i++)
            {
                writer.WriteStringValue(psetIds[i]);
            }

            writer.WriteEndArray();
        }
        else
        {
            telemetry.PropertySetMisses++;
            writer.WriteNull("properties");
        }

        if (mappings.MaterialByObjectId.TryGetValue(node.EntityId, out var materialId) && !string.IsNullOrWhiteSpace(materialId))
        {
            telemetry.MaterialHits++;
            writer.WriteString("material_id", materialId);
        }
        else
        {
            telemetry.MaterialMisses++;
            writer.WriteNull("material_id");
        }

        if (mappings.TypeByObjectId.TryGetValue(node.EntityId, out var typeId) && !string.IsNullOrWhiteSpace(typeId))
        {
            telemetry.TypeHits++;
            writer.WriteString("type_id", typeId);
        }
        else
        {
            telemetry.TypeMisses++;
            writer.WriteNull("type_id");
        }

        writer.WriteEndObject();
    }

    private static bool TryGetGlobalIdToken(
        FastStepIndexes indexes,
        FastStepMmfIntermediateReader intermediateReader,
        int entityId,
        Dictionary<int, string> cache,
        out ulong token)
    {
        var globalId = GetGlobalIdCached(indexes, intermediateReader, entityId, cache);
        if (string.IsNullOrWhiteSpace(globalId))
        {
            token = 0;
            return false;
        }

        token = ComputeObjectIdToken(globalId);
        return true;
    }

    private static ulong ComputeObjectIdToken(string objectId)
    {
        const ulong fnvOffset = 14695981039346656037;
        const ulong fnvPrime = 1099511628211;

        var hash = fnvOffset;
        for (var i = 0; i < objectId.Length; i++)
        {
            var ch = objectId[i];

            hash ^= (byte)(ch & 0xFF);
            hash *= fnvPrime;

            hash ^= (byte)(ch >> 8);
            hash *= fnvPrime;
        }

        return hash;
    }

    private static string GetGlobalIdCached(
        FastStepIndexes indexes,
        FastStepMmfIntermediateReader intermediateReader,
        int entityId,
        Dictionary<int, string> cache)
    {
        if (cache is not null && cache.TryGetValue(entityId, out var cached))
        {
            return cached;
        }

        var globalId = GetGlobalIdWithoutTelemetry(indexes, intermediateReader, entityId);
        cache?[entityId] = globalId;
        return globalId;
    }

    private static string GetGlobalIdWithoutTelemetry(FastStepIndexes indexes, FastStepMmfIntermediateReader intermediateReader, int entityId)
    {
        var globalId = indexes.GetGlobalId(entityId);
        return !string.IsNullOrWhiteSpace(globalId)
            ? globalId
            : TryReadStringArgumentFromMmf(intermediateReader, entityId, argumentIndex: 0);
    }

    private static string GetGlobalId(FastStepIndexes indexes, FastStepMmfIntermediateReader intermediateReader, int entityId, FastStepTelemetry telemetry)
    {
        var globalId = indexes.GetGlobalId(entityId);
        if (!string.IsNullOrWhiteSpace(globalId))
        {
            telemetry.GlobalIdIndexHits++;
            return globalId;
        }

        globalId = TryReadStringArgumentFromMmf(intermediateReader, entityId, argumentIndex: 0);
        if (!string.IsNullOrWhiteSpace(globalId))
        {
            telemetry.GlobalIdRawFallbackHits++;
            return globalId;
        }

        telemetry.GlobalIdMisses++;
        return globalId;
    }

    private static string GetName(FastStepIndexes indexes, FastStepMmfIntermediateReader intermediateReader, int entityId, FastStepTelemetry telemetry)
    {
        var name = indexes.GetName(entityId);
        if (name is not null)
        {
            telemetry.NameIndexHits++;
            return name;
        }

        name = TryReadStringArgumentFromMmf(intermediateReader, entityId, argumentIndex: 2);
        if (name is not null)
        {
            telemetry.NameRawFallbackHits++;
            return name;
        }

        telemetry.NameMisses++;
        return name;
    }

    private static string TryReadStringArgumentFromMmf(FastStepMmfIntermediateReader intermediateReader, int entityId, int argumentIndex)
    {
        if (intermediateReader is null || !intermediateReader.TryReadRawArguments(entityId, out var rawArguments) || string.IsNullOrEmpty(rawArguments))
        {
            return null;
        }

        var rawArgumentsSpan = rawArguments.AsSpan();
        if (!StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, argumentIndex, out var start, out var length))
        {
            return null;
        }

        return StepParsingUtilities.ParseStepString(rawArgumentsSpan.Slice(start, length));
    }

    private readonly record struct FastStepTraversalNode(int EntityId, int ParentEntityId);

    private readonly record struct FastStepRelationAdjacency(FastStepAdjacency Decomposition, FastStepAdjacency Containment);

    private readonly record struct ChildSortEntry(int EntityId, string GlobalId);

    private sealed class FastStepTelemetry
    {
        internal long GlobalIdIndexHits;
        internal long GlobalIdRawFallbackHits;
        internal long GlobalIdMisses;
        internal long NameIndexHits;
        internal long NameRawFallbackHits;
        internal long NameMisses;
        internal long PropertySetHits;
        internal long PropertySetMisses;
        internal long MaterialHits;
        internal long MaterialMisses;
        internal long TypeHits;
        internal long TypeMisses;

        internal FastStepTelemetrySnapshot GetSnapshot()
        {
            return new FastStepTelemetrySnapshot(
                GlobalIdIndexHits,
                GlobalIdRawFallbackHits,
                GlobalIdMisses,
                NameIndexHits,
                NameRawFallbackHits,
                NameMisses,
                PropertySetHits,
                PropertySetMisses,
                MaterialHits,
                MaterialMisses,
                TypeHits,
                TypeMisses);
        }
    }
}



















