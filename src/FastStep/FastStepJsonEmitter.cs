using System;
using System.Collections.Generic;
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

    internal static IfcExportReport Export(
        FastStepIndexes indexes,
        FastStepHeader header,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        FastStepMmfIntermediateReader intermediateReader = null)
    {
        if (indexes.Project is null || string.IsNullOrWhiteSpace(indexes.Project.Value.GlobalId))
        {
            throw new InvalidOperationException("IFC project root (IFCPROJECT) was not found in STEP data.");
        }

        return intermediateReader is null
            ? ExportWithoutSpill(indexes, header, jsonTargetFile, preserveOrder, outputFileBufferSize, writeThrough, progressReporter)
            : ExportWithSpill(indexes, header, jsonTargetFile, preserveOrder, outputFileBufferSize, writeThrough, progressReporter, intermediateReader);
    }

    private static IfcExportReport ExportWithoutSpill(
        FastStepIndexes indexes,
        FastStepHeader header,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter)
    {
        var project = indexes.Project.Value;
        var relationAdjacency = new FastStepRelationAdjacency(indexes.DecompositionAdjacency, indexes.ContainmentAdjacency);
        var orderedNodes = BuildLastNodesByVisitOrder(indexes, project, preserveOrder, relationAdjacency, intermediateReader: null);
        var uniqueMetaObjects = orderedNodes.Count;
        var mappings = FastStepMappingCache.Build(indexes);

        using var stream = IfcStreamingExportUtilities.OpenOutputStream(jsonTargetFile, outputFileBufferSize, writeThrough);
        using var writer = new Utf8JsonWriter(stream, WriterOptions);

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

        const int emitBatchSize = 4096;
        foreach (var node in orderedNodes.Values)
        {
            WriteMetaObject(writer, indexes, mappings, project, node, intermediateReader: null);

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

        return new IfcExportReport(header.Schema, uniqueMetaObjects);
    }

    private static IfcExportReport ExportWithSpill(
        FastStepIndexes indexes,
        FastStepHeader header,
        FileInfo jsonTargetFile,
        bool preserveOrder,
        int outputFileBufferSize,
        bool writeThrough,
        Action<int, int> progressReporter,
        FastStepMmfIntermediateReader intermediateReader)
    {
        var project = indexes.Project.Value;
        var relationAdjacency = new FastStepRelationAdjacency(indexes.DecompositionAdjacency, indexes.ContainmentAdjacency);

        CleanupObjectSegments(intermediateReader.DirectoryPath);
        var lastOrderByObjectToken = BuildLastVisitOrderAndSpill(indexes, project, preserveOrder, relationAdjacency, intermediateReader);
        var uniqueMetaObjects = lastOrderByObjectToken.Count;
        var mappings = FastStepMappingCache.Build(indexes);

        using var stream = IfcStreamingExportUtilities.OpenOutputStream(jsonTargetFile, outputFileBufferSize, writeThrough);
        using var writer = new Utf8JsonWriter(stream, WriterOptions);

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

        const int emitBatchSize = 4096;
        foreach (var record in intermediateReader.EnumerateObjectRecords())
        {
            if (!TryGetGlobalIdToken(indexes, intermediateReader, record.EntityId, out var objectToken)
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
                intermediateReader);

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

        return new IfcExportReport(header.Schema, uniqueMetaObjects);
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

        using var objectStore = new FastStepMmfObjectStore(intermediateReader.DirectoryPath);
        var visitOrder = 0;
        stack.Push(new FastStepTraversalNode(project.EntityId, ParentEntityId: -1));

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (TryGetGlobalIdToken(indexes, intermediateReader, current.EntityId, out var objectToken))
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

            PushChildren(stack, indexes, relationAdjacency.Decomposition, current, preserveOrder, intermediateReader);
            PushChildren(stack, indexes, relationAdjacency.Containment, current, preserveOrder, intermediateReader);
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
        var visitOrder = 0;

        stack.Push(new FastStepTraversalNode(project.EntityId, ParentEntityId: -1));

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (TryGetGlobalIdToken(indexes, intermediateReader, current.EntityId, out var objectToken))
            {
                if (orderByObjectToken.TryGetValue(objectToken, out var previousOrder))
                {
                    nodesByOrder.Remove(previousOrder);
                }

                orderByObjectToken[objectToken] = visitOrder;
                nodesByOrder[visitOrder] = current;
                visitOrder++;
            }

            PushChildren(stack, indexes, relationAdjacency.Decomposition, current, preserveOrder, intermediateReader);
            PushChildren(stack, indexes, relationAdjacency.Containment, current, preserveOrder, intermediateReader);
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
        FastStepMmfIntermediateReader intermediateReader)
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

        var childIds = new List<int>(end - start);
        for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
        {
            childIds.Add(indexes.EntityIdsBySlot[adjacency.Edges[edgeIndex]]);
        }

        childIds.Sort((left, right) => StringComparer.Ordinal.Compare(GetGlobalId(indexes, intermediateReader, left), GetGlobalId(indexes, intermediateReader, right)));

        for (var i = childIds.Count - 1; i >= 0; i--)
        {
            var childId = childIds[i];
            stack.Push(new FastStepTraversalNode(childId, current.EntityId));
        }
    }

    private static void WriteMetaObject(
        Utf8JsonWriter writer,
        FastStepIndexes indexes,
        FastStepMappingCache mappings,
        FastStepProjectRecord project,
        FastStepTraversalNode node,
        FastStepMmfIntermediateReader intermediateReader)
    {
        var objectId = GetGlobalId(indexes, intermediateReader, node.EntityId);
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        var parentObjectId = node.ParentEntityId < 0
            ? null
            : GetGlobalId(indexes, intermediateReader, node.ParentEntityId);

        writer.WritePropertyName(objectId);
        writer.WriteStartObject();

        writer.WriteString("id", objectId);
        writer.WriteString("name", GetName(indexes, intermediateReader, node.EntityId));
        writer.WriteString("type", mappings.GetTypeName(node.EntityId));
        writer.WriteString("parent", parentObjectId);

        if (string.Equals(objectId, project.GlobalId, StringComparison.Ordinal))
        {
            writer.WriteNull("properties");
        }
        else if (mappings.PropertySetByObjectId.TryGetValue(node.EntityId, out var psetIds) && psetIds.Count > 0)
        {
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
            writer.WriteNull("properties");
        }

        if (mappings.MaterialByObjectId.TryGetValue(node.EntityId, out var materialId) && !string.IsNullOrWhiteSpace(materialId))
        {
            writer.WriteString("material_id", materialId);
        }
        else
        {
            writer.WriteNull("material_id");
        }

        if (mappings.TypeByObjectId.TryGetValue(node.EntityId, out var typeId) && !string.IsNullOrWhiteSpace(typeId))
        {
            writer.WriteString("type_id", typeId);
        }
        else
        {
            writer.WriteNull("type_id");
        }

        writer.WriteEndObject();
    }

    private static bool TryGetGlobalIdToken(FastStepIndexes indexes, FastStepMmfIntermediateReader intermediateReader, int entityId, out ulong token)
    {
        var globalId = GetGlobalId(indexes, intermediateReader, entityId);
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

    private static string GetGlobalId(FastStepIndexes indexes, FastStepMmfIntermediateReader intermediateReader, int entityId)
    {
        var globalId = indexes.GetGlobalId(entityId);
        if (!string.IsNullOrWhiteSpace(globalId))
        {
            return globalId;
        }

        return TryReadStringArgumentFromMmf(intermediateReader, entityId, argumentIndex: 0);
    }

    private static string GetName(FastStepIndexes indexes, FastStepMmfIntermediateReader intermediateReader, int entityId)
    {
        var name = indexes.GetName(entityId);
        if (name is not null)
        {
            return name;
        }

        return TryReadStringArgumentFromMmf(intermediateReader, entityId, argumentIndex: 2);
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
}













