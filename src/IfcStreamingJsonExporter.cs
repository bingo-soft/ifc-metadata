using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace Bingosoft.Net.IfcMetadata
{
    internal static class IfcStreamingJsonExporter
    {
        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        internal static void Export(FileInfo ifcSourceFile, FileInfo jsonTargetFile, bool preserveOrder)
        {
            using var model = IfcStore.Open(ifcSourceFile.FullName);
            var project = model.Instances.FirstOrDefault<IIfcProject>()
                          ?? throw new InvalidOperationException("IFC project root (IIfcProject) was not found.");

            var counts = BuildObjectIdCounts(project, preserveOrder);

            using var stream = File.Create(jsonTargetFile.FullName);
            using var writer = new Utf8JsonWriter(stream, WriterOptions);

            writer.WriteStartObject();

            writer.WriteString("id", project.Name);
            writer.WriteString("projectId", project.GlobalId.ToString());
            writer.WriteString("author", GetAuthor(model.Header.FileName.AuthorName));
            writer.WriteString("createdAt", model.Header.TimeStamp);
            writer.WriteString("schema", model.Header.SchemaVersion);
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
                WriteMetaObject(writer, node.ObjectDefinition, node.ParentId);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        internal static int CountMetaObjects(FileInfo ifcSourceFile, bool preserveOrder)
        {
            using var model = IfcStore.Open(ifcSourceFile.FullName);
            var project = model.Instances.FirstOrDefault<IIfcProject>()
                          ?? throw new InvalidOperationException("IFC project root (IIfcProject) was not found.");

            var counts = BuildObjectIdCounts(project, preserveOrder);
            return counts.Count;
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
            var children = new List<IIfcObjectDefinition>();
            foreach (var relation in spatialElement.ContainsElements)
            {
                foreach (var relatedElement in relation.RelatedElements)
                {
                    children.Add(relatedElement);
                }
            }

            PushChildren(stack, children, parentObjectId, preserveOrder);
        }

        private static void PushRelatedObjects(Stack<TraversalNode> stack, IIfcObjectDefinition objectDefinition, string parentObjectId, bool preserveOrder)
        {
            var children = new List<IIfcObjectDefinition>();
            foreach (var relation in objectDefinition.IsDecomposedBy)
            {
                foreach (var relatedObject in relation.RelatedObjects)
                {
                    children.Add(relatedObject);
                }
            }

            PushChildren(stack, children, parentObjectId, preserveOrder);
        }

        private static void PushChildren(Stack<TraversalNode> stack, List<IIfcObjectDefinition> children, string parentObjectId, bool preserveOrder)
        {
            if (children.Count == 0)
            {
                return;
            }

            if (preserveOrder)
            {
                children.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.GlobalId, right.GlobalId));
            }

            for (var i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(new TraversalNode(children[i], parentObjectId));
            }
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

        private static void WriteMetaObject(Utf8JsonWriter writer, IIfcObjectDefinition objectDefinition, string parentId)
        {
            writer.WriteStartObject(objectDefinition.GlobalId);

            writer.WriteString("id", objectDefinition.GlobalId.ToString());
            writer.WriteString("name", objectDefinition.Name);
            writer.WriteString("type", objectDefinition.GetType().Name);
            writer.WriteString("parent", parentId);

            WriteProperties(writer, objectDefinition);

            writer.WriteString("material_id", GetMaterialsV2(objectDefinition));
            writer.WriteString("type_id", GetTypedId(objectDefinition));

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

        private static string GetTypedId(IIfcObjectDefinition element)
        {
            var isTypedByInfo = element.GetType().GetProperty("IsTypedBy");
            if (isTypedByInfo is null)
            {
                return null;
            }

            var isTypedByValue = isTypedByInfo.GetValue(element);
            return isTypedByValue is null ? null : GetGlobalId(isTypedByValue);
        }

        private static string GetGlobalId(object obj)
        {
            var isTypedByGlobalIdInfo = obj.GetType().GetProperty("GlobalId");
            if (isTypedByGlobalIdInfo is null)
            {
                return null;
            }

            var isTypedByGlobalIdValue = isTypedByGlobalIdInfo.GetValue(obj);
            return isTypedByGlobalIdValue switch
            {
                Xbim.Ifc2x3.UtilityResource.IfcGloballyUniqueId global2x3Id => global2x3Id.Value.ToString(),
                Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId global4Id => global4Id.Value.ToString(),
                _ => null,
            };
        }

        private static int? GetEntityLabel(object obj)
        {
            var entityLabelInfo = obj.GetType().GetProperty("EntityLabel");

            var entityLabelValue = entityLabelInfo?.GetValue(obj);
            return entityLabelValue is int value ? value : null;
        }

        private static string GetMaterialsV2(IIfcObjectDefinition objectDefinition)
        {
            var material = objectDefinition.GetType().GetProperty("Material");

            var materialValue = material?.GetValue(objectDefinition);
            if (materialValue is null)
            {
                return null;
            }

            var entityLabel = GetEntityLabel(materialValue);
            return entityLabel is null
                ? null
                : $"{objectDefinition.Material.ExpressType.Name}_{entityLabel}";
        }

        private static string GetAuthor(IList<string> authors)
        {
            if (authors.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < authors.Count; i++)
            {
                builder.Append(authors[i]);
                if (i < authors.Count - 1)
                {
                    builder.Append(';');
                }
            }

            return builder.ToString();
        }
    }
}
