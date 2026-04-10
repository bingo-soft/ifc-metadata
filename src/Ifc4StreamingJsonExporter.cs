using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.ProductExtension;

namespace Bingosoft.Net.IfcMetadata
{
    internal static class Ifc4StreamingJsonExporter
    {
        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static readonly IComparer<IfcObjectDefinition> GlobalIdComparer = Comparer<IfcObjectDefinition>.Create(
            static (left, right) => StringComparer.Ordinal.Compare(GetObjectId(left), GetObjectId(right)));

        internal static IfcExportReport Export(
            IfcStore model,
            IfcProject project,
            FileInfo jsonTargetFile,
            bool preserveOrder,
            int outputFileBufferSize,
            bool writeThrough,
            Action<int, int> progressReporter)
        {
            var schemaVersion = model.Header.SchemaVersion;
            var bufferedTraversal = new List<TraversalNode>();
            var counts = BuildObjectIdCounts(project, preserveOrder, bufferedTraversal);
            var uniqueMetaObjects = counts.Count;
            var ir = BuildExportIr(bufferedTraversal, counts, uniqueMetaObjects, progressReporter);

            using var stream = OpenOutputStream(jsonTargetFile, outputFileBufferSize, writeThrough);
            using var writer = new Utf8JsonWriter(stream, WriterOptions);

            var projectView = (IIfcProject)project;

            writer.WriteStartObject();
            writer.WriteString("id", projectView.Name);
            writer.WriteString("projectId", projectView.GlobalId.ToString());
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

        private static Dictionary<string, int> BuildObjectIdCounts(IfcObjectDefinition root, bool preserveOrder, List<TraversalNode> bufferedTraversal = null)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var stack = new Stack<TraversalNode>();
            stack.Push(new TraversalNode(root, null));

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                bufferedTraversal?.Add(current);

                if (!string.IsNullOrWhiteSpace(current.ObjectId))
                {
                    counts.TryGetValue(current.ObjectId, out var currentCount);
                    counts[current.ObjectId] = currentCount + 1;
                }

                PushRelatedObjects(stack, current.ObjectDefinition, current.ObjectId, preserveOrder);

                if (current.ObjectDefinition is IfcSpatialStructureElement spatialElement)
                {
                    PushContainedElements(stack, spatialElement, current.ObjectId, preserveOrder);
                }
            }

            return counts;
        }

        private static void PushContainedElements(Stack<TraversalNode> stack, IfcSpatialStructureElement spatialElement, string parentObjectId, bool preserveOrder)
        {
            if (!preserveOrder)
            {
                foreach (var relation in spatialElement.ContainsElements)
                {
                    foreach (var relatedElement in relation.RelatedElements)
                    {
                        if (relatedElement is IfcObjectDefinition child)
                        {
                            stack.Push(new TraversalNode(child, parentObjectId));
                        }
                    }
                }

                return;
            }

            var pooledChildren = ArrayPool<IfcObjectDefinition>.Shared.Rent(16);
            var childCount = 0;
            try
            {
                foreach (var relation in spatialElement.ContainsElements)
                {
                    foreach (var relatedElement in relation.RelatedElements)
                    {
                        if (relatedElement is IfcObjectDefinition child)
                        {
                            AddPooledChild(ref pooledChildren, ref childCount, child);
                        }
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

                ArrayPool<IfcObjectDefinition>.Shared.Return(pooledChildren, clearArray: false);
            }
        }

        private static void PushRelatedObjects(Stack<TraversalNode> stack, IfcObjectDefinition objectDefinition, string parentObjectId, bool preserveOrder)
        {
            if (!preserveOrder)
            {
                foreach (var relation in objectDefinition.IsDecomposedBy)
                {
                    foreach (var relatedObject in relation.RelatedObjects)
                    {
                        if (relatedObject is not null)
                        {
                            stack.Push(new TraversalNode(relatedObject, parentObjectId));
                        }
                    }
                }

                return;
            }

            var pooledChildren = ArrayPool<IfcObjectDefinition>.Shared.Rent(16);
            var childCount = 0;
            try
            {
                foreach (var relation in objectDefinition.IsDecomposedBy)
                {
                    foreach (var relatedObject in relation.RelatedObjects)
                    {
                        if (relatedObject is not null)
                        {
                            AddPooledChild(ref pooledChildren, ref childCount, relatedObject);
                        }
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

                ArrayPool<IfcObjectDefinition>.Shared.Return(pooledChildren, clearArray: false);
            }
        }

        private static void PushChildrenOrdered(Stack<TraversalNode> stack, IfcObjectDefinition[] children, int childCount, string parentObjectId)
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

        private static void AddPooledChild(ref IfcObjectDefinition[] pooledChildren, ref int childCount, IfcObjectDefinition child)
        {
            if (childCount == pooledChildren.Length)
            {
                var grown = ArrayPool<IfcObjectDefinition>.Shared.Rent(pooledChildren.Length * 2);
                Array.Copy(pooledChildren, grown, childCount);
                ArrayPool<IfcObjectDefinition>.Shared.Return(pooledChildren, clearArray: false);
                pooledChildren = grown;
            }

            pooledChildren[childCount++] = child;
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

            for (var i = 0; i < bufferedTraversal.Count; i++)
            {
                var node = bufferedTraversal[i];
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

        private static string GetObjectId(IfcObjectDefinition objectDefinition)
        {
            return ((IIfcRoot)objectDefinition).GlobalId;
        }

        private readonly struct TraversalNode
        {
            internal TraversalNode(IfcObjectDefinition objectDefinition, string parentId)
            {
                ObjectDefinition = objectDefinition;
                ParentId = parentId;
                ObjectId = GetObjectId(objectDefinition);
            }

            internal IfcObjectDefinition ObjectDefinition { get; }

            internal string ParentId { get; }

            internal string ObjectId { get; }
        }
    }
}
