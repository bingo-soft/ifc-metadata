using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace Bingosoft.Net.IfcMetadata
{
    internal static class IfcStreamingJsonExporter
    {
        internal const int DefaultOutputFileBufferSize = 512 * 1024;

        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static readonly IComparer<IIfcObjectDefinition> GlobalIdComparer = Comparer<IIfcObjectDefinition>.Create(
            static (left, right) => StringComparer.Ordinal.Compare(left.GlobalId, right.GlobalId));

                internal static IfcExportReport Export(
            FileInfo ifcSourceFile,
            FileInfo jsonTargetFile,
            bool preserveOrder,
            int outputFileBufferSize = DefaultOutputFileBufferSize,
            bool writeThrough = false,
            Action<int, int> progressReporter = null)
        {
            using var model = IfcStore.Open(ifcSourceFile.FullName);
            var project = model.Instances.FirstOrDefault<IIfcProject>()
                          ?? throw new InvalidOperationException("IFC project root (IIfcProject) was not found.");

            var schemaVersion = model.Header.SchemaVersion;
            var isFallbackForced = IsFallbackForced();

            if (!isFallbackForced && IfcSchemaRouter.IsIfc2x3(schemaVersion) && project is Xbim.Ifc2x3.Kernel.IfcProject ifc2x3Project)
            {
                return Ifc2x3StreamingJsonExporter.Export(
                    model,
                    ifc2x3Project,
                    jsonTargetFile,
                    preserveOrder,
                    outputFileBufferSize,
                    writeThrough,
                    progressReporter);
            }

            if (!isFallbackForced && IfcSchemaRouter.IsIfc4(schemaVersion) && project is Xbim.Ifc4.Kernel.IfcProject ifc4Project)
            {
                return Ifc4StreamingJsonExporter.Export(
                    model,
                    ifc4Project,
                    jsonTargetFile,
                    preserveOrder,
                    outputFileBufferSize,
                    writeThrough,
                    progressReporter);
            }

            var bufferedTraversal = new List<TraversalNode>();
            var counts = BuildObjectIdCounts(project, preserveOrder, bufferedTraversal);
            var uniqueMetaObjects = counts.Count;
            var ir = BuildExportIr(bufferedTraversal, counts, uniqueMetaObjects, progressReporter);

            using var stream = OpenOutputStream(jsonTargetFile, outputFileBufferSize, writeThrough);
            using var writer = new Utf8JsonWriter(stream, WriterOptions);

            writer.WriteStartObject();
            writer.WriteString("id", project.Name);
            writer.WriteString("projectId", project.GlobalId.ToString());
            writer.WriteString("author", GetAuthor(model.Header.FileName.AuthorName));
            writer.WriteString("createdAt", model.Header.TimeStamp);
            writer.WriteString("schema", schemaVersion);
            writer.WriteString("creatingApplication", model.Header.CreatingApplication);
            writer.WriteStartObject("metaObjects");

            IfcExportIrPipeline.WriteMetaObjects(writer, ir);

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();
            stream.Flush();

            return new IfcExportReport(schemaVersion, uniqueMetaObjects);
        }

        internal static int CountMetaObjects(FileInfo ifcSourceFile, bool preserveOrder)
        {
            using var model = IfcStore.Open(ifcSourceFile.FullName);
            var project = model.Instances.FirstOrDefault<IIfcProject>()
                          ?? throw new InvalidOperationException("IFC project root (IIfcProject) was not found.");

            var counts = BuildObjectIdCounts(project, preserveOrder);
            return counts.Count;
        }

        private static FileStream OpenOutputStream(FileInfo jsonTargetFile, int outputFileBufferSize, bool writeThrough)
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = outputFileBufferSize,
                Options = writeThrough ? FileOptions.WriteThrough : FileOptions.SequentialScan,
            };

            return new FileStream(jsonTargetFile.FullName, options);
        }

        private static Dictionary<string, int> BuildObjectIdCounts(IIfcObjectDefinition root, bool preserveOrder, List<TraversalNode> bufferedTraversal = null)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var node in EnumerateHierarchy(root, null, preserveOrder))
            {
                bufferedTraversal?.Add(node);

                if (string.IsNullOrWhiteSpace(node.ObjectId))
                {
                    continue;
                }

                counts.TryGetValue(node.ObjectId, out var current);
                counts[node.ObjectId] = current + 1;
            }

            return counts;
        }

        private static IEnumerable<TraversalNode> EnumerateHierarchy(IIfcObjectDefinition root, string parentId, bool preserveOrder)
        {
            var stack = new Stack<TraversalNode>();
            stack.Push(new TraversalNode(root, parentId));

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;

                PushRelatedObjects(stack, current.ObjectDefinition, current.ObjectId, preserveOrder);

                if (current.ObjectDefinition is IIfcSpatialStructureElement spatialElement)
                {
                    PushContainedElements(stack, spatialElement, current.ObjectId, preserveOrder);
                }
            }
        }

        private static void PushContainedElements(Stack<TraversalNode> stack, IIfcSpatialStructureElement spatialElement, string parentObjectId, bool preserveOrder)
        {
            if (!preserveOrder)
            {
                foreach (var relation in spatialElement.ContainsElements)
                {
                    foreach (var relatedElement in relation.RelatedElements)
                    {
                        stack.Push(new TraversalNode(relatedElement, parentObjectId));
                    }
                }

                return;
            }

            var pooledChildren = ArrayPool<IIfcObjectDefinition>.Shared.Rent(16);
            var childCount = 0;
            try
            {
                foreach (var relation in spatialElement.ContainsElements)
                {
                    foreach (var relatedElement in relation.RelatedElements)
                    {
                        AddPooledChild(ref pooledChildren, ref childCount, relatedElement);
                    }
                }

                PushChildrenOrdered(stack, pooledChildren, childCount, parentObjectId);
            }
            finally
            {
                if (childCount > 0)
                {
                    Array.Clear(pooledChildren, 0, childCount);
                }

                ArrayPool<IIfcObjectDefinition>.Shared.Return(pooledChildren, clearArray: false);
            }
        }

        private static void PushRelatedObjects(Stack<TraversalNode> stack, IIfcObjectDefinition objectDefinition, string parentObjectId, bool preserveOrder)
        {
            if (!preserveOrder)
            {
                foreach (var relation in objectDefinition.IsDecomposedBy)
                {
                    foreach (var relatedObject in relation.RelatedObjects)
                    {
                        stack.Push(new TraversalNode(relatedObject, parentObjectId));
                    }
                }

                return;
            }

            var pooledChildren = ArrayPool<IIfcObjectDefinition>.Shared.Rent(16);
            var childCount = 0;
            try
            {
                foreach (var relation in objectDefinition.IsDecomposedBy)
                {
                    foreach (var relatedObject in relation.RelatedObjects)
                    {
                        AddPooledChild(ref pooledChildren, ref childCount, relatedObject);
                    }
                }

                PushChildrenOrdered(stack, pooledChildren, childCount, parentObjectId);
            }
            finally
            {
                if (childCount > 0)
                {
                    Array.Clear(pooledChildren, 0, childCount);
                }

                ArrayPool<IIfcObjectDefinition>.Shared.Return(pooledChildren, clearArray: false);
            }
        }

        private static void PushChildrenOrdered(Stack<TraversalNode> stack, IIfcObjectDefinition[] children, int childCount, string parentObjectId)
        {
            if (childCount == 0)
            {
                return;
            }

            Array.Sort(children, 0, childCount, GlobalIdComparer);

            for (var i = childCount - 1; i >= 0; i--)
            {
                stack.Push(new TraversalNode(children[i], parentObjectId));
            }
        }

                private static void AddPooledChild(ref IIfcObjectDefinition[] pooledChildren, ref int childCount, IIfcObjectDefinition child)
        {
            if (childCount == pooledChildren.Length)
            {
                var grown = ArrayPool<IIfcObjectDefinition>.Shared.Rent(pooledChildren.Length * 2);
                Array.Copy(pooledChildren, grown, childCount);
                ArrayPool<IIfcObjectDefinition>.Shared.Return(pooledChildren, clearArray: false);
                pooledChildren = grown;
            }

            pooledChildren[childCount++] = child;
        }

        private static bool IsFallbackForced()
        {
            var value = Environment.GetEnvironmentVariable("IFC_FORCE_FALLBACK");
            return string.Equals(value, "1", StringComparison.Ordinal)
                   || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static IfcExportIr BuildExportIr(
            List<TraversalNode> bufferedTraversal,
            Dictionary<string, int> counts,
            int uniqueMetaObjects,
            Action<int, int> progressReporter)
        {
            var ir = new IfcExportIr(uniqueMetaObjects);
            var processedMetaObjects = 0;

            progressReporter?.Invoke(processedMetaObjects, uniqueMetaObjects);

            foreach (var node in bufferedTraversal)
            {
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
                IfcExportIrPipeline.AppendMetaObject(ir, node.ObjectDefinition, node.ParentId, node.ObjectId);

                processedMetaObjects++;
                progressReporter?.Invoke(processedMetaObjects, uniqueMetaObjects);
            }

            return ir;
        }

        private static string GetAuthor(IList<string> authors)
        {
            if (authors.Count == 0)
            {
                return string.Empty;
            }

            var totalLength = authors.Count - 1;
            for (var i = 0; i < authors.Count; i++)
            {
                totalLength += authors[i]?.Length ?? 0;
            }

            return string.Create(totalLength, authors, static (destination, state) =>
            {
                var position = 0;
                for (var i = 0; i < state.Count; i++)
                {
                    var author = state[i];
                    if (!string.IsNullOrEmpty(author))
                    {
                        author.AsSpan().CopyTo(destination[position..]);
                        position += author.Length;
                    }

                    if (i < state.Count - 1)
                    {
                        destination[position++] = ';';
                    }
                }
            });
        }

        private readonly struct TraversalNode
        {
            internal TraversalNode(IIfcObjectDefinition objectDefinition, string parentId)
            {
                ObjectDefinition = objectDefinition;
                ParentId = parentId;
                ObjectId = objectDefinition.GlobalId;
            }

            internal IIfcObjectDefinition ObjectDefinition { get; }

            internal string ParentId { get; }

            internal string ObjectId { get; }
        }
    }

    internal readonly struct IfcExportReport
    {
        internal IfcExportReport(string schemaVersion, int metaObjectCount)
        {
            SchemaVersion = schemaVersion;
            MetaObjectCount = metaObjectCount;
        }

        internal string SchemaVersion { get; }

        internal int MetaObjectCount { get; }
    }
}












