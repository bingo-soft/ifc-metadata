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
        var traversal = BuildTraversal(indexes, project, preserveOrder);
        var counts = BuildObjectIdCounts(traversal);
        var uniqueMetaObjects = counts.Count;

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

        for (var i = 0; i < traversal.Count; i++)
        {
            var node = traversal[i];
            if (string.IsNullOrWhiteSpace(node.ObjectId) || !counts.TryGetValue(node.ObjectId, out var remaining))
            {
                continue;
            }

            remaining--;
            if (remaining > 0)
            {
                counts[node.ObjectId] = remaining;
                continue;
            }

            counts.Remove(node.ObjectId);
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

    private static List<FastStepTraversalNode> BuildTraversal(FastStepIndexes indexes, FastStepProjectRecord project, bool preserveOrder)
    {
        var decompositionMap = BuildRelationMap(indexes.DecompositionRelations);
        var containmentMap = BuildRelationMap(indexes.ContainmentRelations);

        var stack = new Stack<FastStepTraversalNode>();
        var traversal = new List<FastStepTraversalNode>();

        stack.Push(new FastStepTraversalNode(project.EntityId, project.GlobalId, null));

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            traversal.Add(current);

            PushChildren(stack, indexes, decompositionMap, current, preserveOrder);
            PushChildren(stack, indexes, containmentMap, current, preserveOrder);
        }

        return traversal;
    }

    private static Dictionary<int, List<int>> BuildRelationMap(List<FastStepRelationRecord> relations)
    {
        var map = new Dictionary<int, List<int>>();

        foreach (var relation in relations)
        {
            if (!map.TryGetValue(relation.RelatingId, out var children))
            {
                children = [];
                map[relation.RelatingId] = children;
            }

            for (var i = 0; i < relation.RelatedIds.Count; i++)
            {
                children.Add(relation.RelatedIds[i]);
            }
        }

        return map;
    }

    private static void PushChildren(
        Stack<FastStepTraversalNode> stack,
        FastStepIndexes indexes,
        Dictionary<int, List<int>> relationMap,
        FastStepTraversalNode current,
        bool preserveOrder)
    {
        if (!relationMap.TryGetValue(current.EntityId, out var children) || children.Count == 0)
        {
            return;
        }

        var childIds = new List<int>(children);

        if (preserveOrder)
        {
            childIds.Sort((left, right) => StringComparer.Ordinal.Compare(GetGlobalId(indexes, left), GetGlobalId(indexes, right)));
        }

        for (var i = childIds.Count - 1; i >= 0; i--)
        {
            var childId = childIds[i];
            stack.Push(new FastStepTraversalNode(childId, GetGlobalId(indexes, childId), current.ObjectId));
        }
    }

    private static Dictionary<string, int> BuildObjectIdCounts(List<FastStepTraversalNode> traversal)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < traversal.Count; i++)
        {
            var objectId = traversal[i].ObjectId;
            if (string.IsNullOrWhiteSpace(objectId))
            {
                continue;
            }

            counts.TryGetValue(objectId, out var currentCount);
            counts[objectId] = currentCount + 1;
        }

        return counts;
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
        return indexes.EntityGlobalIds.GetValueOrDefault(entityId);
    }

    private static string GetName(FastStepIndexes indexes, int entityId)
    {
        return indexes.EntityNames.GetValueOrDefault(entityId);
    }

    private readonly record struct FastStepTraversalNode(int EntityId, string ObjectId, string ParentObjectId);
}
