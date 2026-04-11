using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

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
        Action<int, int> progressReporter)
    {
        if (indexes.Project is null || string.IsNullOrWhiteSpace(indexes.Project.Value.GlobalId))
        {
            throw new InvalidOperationException("IFC project root (IFCPROJECT) was not found in STEP data.");
        }

        var project = indexes.Project.Value;
        var relationAdjacency = new FastStepRelationAdjacency(indexes.DecompositionAdjacency, indexes.ContainmentAdjacency);
        var lastNodesByObjectId = BuildLastNodesByObjectId(indexes, project, preserveOrder, relationAdjacency, out var lastVisitOrderByObjectId);
        var uniqueMetaObjects = lastNodesByObjectId.Count;

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

        List<string> orderedObjectIds = [.. lastNodesByObjectId.Keys];
        orderedObjectIds.Sort((left, right) => lastVisitOrderByObjectId[left].CompareTo(lastVisitOrderByObjectId[right]));

        for (var i = 0; i < orderedObjectIds.Count; i++)
        {
            var objectId = orderedObjectIds[i];
            var node = lastNodesByObjectId[objectId];
            WriteMetaObject(writer, indexes, mappings, project, node);

            processedMetaObjects++;
            progressReporter?.Invoke(processedMetaObjects, uniqueMetaObjects);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        stream.Flush();

        return new IfcExportReport(header.Schema, uniqueMetaObjects);
    }

    private static Dictionary<string, FastStepTraversalNode> BuildLastNodesByObjectId(
        FastStepIndexes indexes,
        FastStepProjectRecord project,
        bool preserveOrder,
        FastStepRelationAdjacency relationAdjacency,
        out Dictionary<string, int> lastVisitOrderByObjectId)
    {
        var stack = new Stack<FastStepTraversalNode>();
        var lastNodesByObjectId = new Dictionary<string, FastStepTraversalNode>(StringComparer.Ordinal);
        lastVisitOrderByObjectId = new Dictionary<string, int>(StringComparer.Ordinal);

        var visitOrder = 0;
        stack.Push(new FastStepTraversalNode(project.EntityId, project.GlobalId, null));

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!string.IsNullOrWhiteSpace(current.ObjectId))
            {
                lastNodesByObjectId[current.ObjectId] = current;
                lastVisitOrderByObjectId[current.ObjectId] = visitOrder;
                visitOrder++;
            }

            PushChildren(stack, indexes, relationAdjacency.Decomposition, current, preserveOrder);
            PushChildren(stack, indexes, relationAdjacency.Containment, current, preserveOrder);
        }

        return lastNodesByObjectId;
    }

    private static void PushChildren(
        Stack<FastStepTraversalNode> stack,
        FastStepIndexes indexes,
        FastStepAdjacency adjacency,
        FastStepTraversalNode current,
        bool preserveOrder)
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
                stack.Push(new FastStepTraversalNode(childEntityId, GetGlobalId(indexes, childEntityId), current.ObjectId));
            }

            return;
        }

        var childIds = new List<int>(end - start);
        for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
        {
            childIds.Add(indexes.EntityIdsBySlot[adjacency.Edges[edgeIndex]]);
        }

        childIds.Sort((left, right) => StringComparer.Ordinal.Compare(GetGlobalId(indexes, left), GetGlobalId(indexes, right)));

        for (var i = childIds.Count - 1; i >= 0; i--)
        {
            var childId = childIds[i];
            stack.Push(new FastStepTraversalNode(childId, GetGlobalId(indexes, childId), current.ObjectId));
        }
    }

    private static void WriteMetaObject(
        Utf8JsonWriter writer,
        FastStepIndexes indexes,
        FastStepMappingCache mappings,
        FastStepProjectRecord project,
        FastStepTraversalNode node)
    {
        var objectId = node.ObjectId;
        writer.WritePropertyName(objectId);
        writer.WriteStartObject();

        writer.WriteString("id", objectId);
        writer.WriteString("name", GetName(indexes, node.EntityId));
        writer.WriteString("type", mappings.GetTypeName(node.EntityId));
        writer.WriteString("parent", node.ParentObjectId);

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

    private static string GetGlobalId(FastStepIndexes indexes, int entityId)
    {
        return indexes.GetGlobalId(entityId);
    }

    private static string GetName(FastStepIndexes indexes, int entityId)
    {
        return indexes.GetName(entityId);
    }

    private readonly record struct FastStepTraversalNode(int EntityId, string ObjectId, string ParentObjectId);

    private readonly record struct FastStepRelationAdjacency(FastStepAdjacency Decomposition, FastStepAdjacency Containment);
}

