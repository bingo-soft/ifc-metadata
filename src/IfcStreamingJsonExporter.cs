using System;
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

        internal static IfcExportReport Export(
            FileInfo ifcSourceFile,
            FileInfo jsonTargetFile,
            bool preserveOrder,
            int outputFileBufferSize = DefaultOutputFileBufferSize,
            bool writeThrough = false)
        {
            using var model = IfcStore.Open(ifcSourceFile.FullName);
            var project = model.Instances.FirstOrDefault<IIfcProject>()
                          ?? throw new InvalidOperationException("IFC project root (IIfcProject) was not found.");

            var schemaVersion = model.Header.SchemaVersion;
            var counts = BuildObjectIdCounts(project, preserveOrder);
            var uniqueMetaObjects = counts.Count;

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

            foreach (var node in EnumerateHierarchy(project, null, preserveOrder))
            {
                var objectId = node.ObjectDefinition.GlobalId;
                if (string.IsNullOrWhiteSpace(objectId) || !counts.TryGetValue(objectId, out var remaining))
                {
                    continue;
                }

                remaining--;
                if (remaining > 0)
                {
                    counts[objectId] = remaining;
                    continue;
                }

                counts.Remove(objectId);
                WriteMetaObject(writer, node.ObjectDefinition, node.ParentId, objectId);
            }

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

        private static Dictionary<string, int> BuildObjectIdCounts(IIfcObjectDefinition root, bool preserveOrder)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var node in EnumerateHierarchy(root, null, preserveOrder))
            {
                var objectId = node.ObjectDefinition.GlobalId;
                if (string.IsNullOrWhiteSpace(objectId))
                {
                    continue;
                }

                counts.TryGetValue(objectId, out var current);
                counts[objectId] = current + 1;
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

                PushRelatedObjects(stack, current.ObjectDefinition, current.ObjectDefinition.GlobalId, preserveOrder);

                if (current.ObjectDefinition is IIfcSpatialStructureElement spatialElement)
                {
                    PushContainedElements(stack, spatialElement, current.ObjectDefinition.GlobalId, preserveOrder);
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

            var children = new List<IIfcObjectDefinition>();
            foreach (var relation in spatialElement.ContainsElements)
            {
                foreach (var relatedElement in relation.RelatedElements)
                {
                    children.Add(relatedElement);
                }
            }

            PushChildrenOrdered(stack, children, parentObjectId);
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

            var children = new List<IIfcObjectDefinition>();
            foreach (var relation in objectDefinition.IsDecomposedBy)
            {
                foreach (var relatedObject in relation.RelatedObjects)
                {
                    children.Add(relatedObject);
                }
            }

            PushChildrenOrdered(stack, children, parentObjectId);
        }

        private static void PushChildrenOrdered(Stack<TraversalNode> stack, List<IIfcObjectDefinition> children, string parentObjectId)
        {
            if (children.Count == 0)
            {
                return;
            }

            children.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.GlobalId, right.GlobalId));

            for (var i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(new TraversalNode(children[i], parentObjectId));
            }
        }

        private static void WriteMetaObject(Utf8JsonWriter writer, IIfcObjectDefinition objectDefinition, string parentId, string objectId)
        {
            writer.WriteStartObject(objectId);

            writer.WriteString("id", objectId);
            writer.WriteString("name", objectDefinition.Name);
            writer.WriteString("type", IfcAccessors.GetRuntimeTypeName(objectDefinition));
            writer.WriteString("parent", parentId);

            WriteProperties(writer, objectDefinition);
            writer.WriteString("material_id", IfcAccessors.GetMaterialId(objectDefinition));
            writer.WriteString("type_id", IfcAccessors.GetTypedId(objectDefinition));

            writer.WriteEndObject();
        }

        private static void WriteProperties(Utf8JsonWriter writer, IIfcObjectDefinition objectDefinition)
        {
            if (objectDefinition is IIfcProject || objectDefinition is not IIfcObject product)
            {
                writer.WriteNull("properties");
                return;
            }

            var hasProperties = false;
            foreach (var relation in product.IsDefinedBy)
            {
                var relatedPropertyDefinition = relation.RelatingPropertyDefinition;
                if (relatedPropertyDefinition is null)
                {
                    continue;
                }

                foreach (var propertyDefinition in relatedPropertyDefinition.PropertySetDefinitions)
                {
                    if (propertyDefinition is not IIfcPropertySet propertySet)
                    {
                        continue;
                    }

                    if (!hasProperties)
                    {
                        writer.WriteStartArray("properties");
                        hasProperties = true;
                    }

                    writer.WriteStringValue(propertySet.GlobalId.Value.ToString());
                }
            }

            if (hasProperties)
            {
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNull("properties");
            }
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
            }

            internal IIfcObjectDefinition ObjectDefinition { get; }

            internal string ParentId { get; }
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




