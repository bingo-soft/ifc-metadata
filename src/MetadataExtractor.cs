using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MaterialResource;

namespace Bingosoft.Net.IfcMetadata
{
    internal sealed class MetadataExtractor
    {
        /// <summary>
        ///   The Id field is populated with the name of the project.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        ///   The GlobalId of the project.
        /// </summary>
        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; }

        /// <summary>
        ///   The author of the project.
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>
        ///   The creation date of the project.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; }

        /// <summary>
        ///   The schema of the ifc model.
        /// </summary>
        [JsonPropertyName("schema")]
        public string Schema { get; set; }

        /// <summary>
        ///   The application with which the model was created.
        /// </summary>
        [JsonPropertyName("creatingApplication")]
        public string CreatingApplication { get; set; }

        /// <summary>
        ///   A list of all building elements as MetaObjects within the project.
        /// </summary>
        [JsonPropertyName("metaObjects")]
        public List<Metadata> MetaObjects { get; set; }

        /// <summary>
        ///   The convenience initialiser creates and returns an instance of the
        ///   MetaModel by parsing the IFC at the provided path.
        /// </summary>
        /// <param name="ifcPath">A string path of the IFC path.</param>
        /// <returns>Returns the complete MetaModel of the IFC.</returns>
        public static MetadataExtractor FromIfc(FileInfo ifcPath)
        {
            using (var model = IfcStore.Open(ifcPath.FullName))
            {
                var project = model.Instances.FirstOrDefault<IIfcProject>();

                var header = model.Header;
                var extractor = new MetadataExtractor();
                extractor.Init(
                            project.Name,
                            project.GlobalId,
                            GetAuthor(header.FileName.AuthorName),
                            header.TimeStamp,
                            header.SchemaVersion,
                            header.CreatingApplication);

                extractor.MetaObjects = ExtractHierarchy(project);
                return extractor;
            }
        }

        private void Init(string id, string projectId, string author, string createdAt, string schema, string creatingApplication)
        {
            Id = id;
            ProjectId = projectId;
            Author = author;
            CreatedAt = createdAt;
            Schema = schema;
            CreatingApplication = creatingApplication;
        }

        private static string GetAuthor(IList<string> authors)
        {
            var sb = new StringBuilder();
            foreach (var item in authors)
            {
                sb.Append(item);
                if (!item.Equals(authors.LastOrDefault()))
                {
                    sb.Append(';');
                }
            }

            return sb.ToString();
        }

        private static List<Metadata> ExtractHierarchy(IIfcObjectDefinition objectDefinition, string parentId = null)
        {
            var metaObjects = new List<Metadata>();

            var objectType = objectDefinition.GetType();
            var parentObject = new Metadata
            {
                Id = objectDefinition.GlobalId,
                Name = objectDefinition.Name,
                Type = objectType.Name,
                Parent = parentId,
                TypeId = GetTypedId(objectDefinition)
            };

            if (objectDefinition is not IIfcProject)
            {
                var parentProps = GetProperties((IIfcObject)objectDefinition);
                if (parentProps.Length > 0)
                {
                    parentObject.PropertyIds = parentProps;
                }
            }

            parentObject.Material = GetMaterialsV2(objectDefinition);

            metaObjects.Add(parentObject);

            if (objectDefinition is IIfcSpatialStructureElement spatialElement)
            {
                var containedElements = spatialElement.ContainsElements.SelectMany(rel => rel.RelatedElements);
                foreach (var element in containedElements)
                {
                    var typeId = GetTypedId(element);
                    var mo = new Metadata
                    {
                        Id = element.GlobalId,
                        Name = element.Name,
                        Type = element.GetType().Name,
                        Parent = spatialElement.GlobalId,
                        TypeId = typeId
                    };

                    var props = GetProperties(element);
                    if (props.Length > 0)
                    {
                        mo.PropertyIds = props;
                    }

                    mo.Material = GetMaterialsV2(element);

                    metaObjects.Add(mo);
                    ExtractRelatedObjects(element, ref metaObjects, mo.Id);
                }
            }

            ExtractRelatedObjects(objectDefinition, ref metaObjects, parentObject.Id);

            return metaObjects;
        }

        private static string GetTypedId(IIfcObjectDefinition element)
        {
            var isTypedByInfo = element.GetType().GetProperty("IsTypedBy");

            var isTypedByValue = isTypedByInfo?.GetValue(element);
            return isTypedByValue is null ? null : GetGlobalId(isTypedByValue);
        }

        private static string GetGlobalId(object obj)
        {
            var isTypedByGlobalIdInfo = obj.GetType().GetProperty("GlobalId");
            if (isTypedByGlobalIdInfo is null) return null;

            var isTypedByGlobalIdValue = isTypedByGlobalIdInfo.GetValue(obj);
            return isTypedByGlobalIdValue switch
            {
                Xbim.Ifc2x3.UtilityResource.IfcGloballyUniqueId Global2x3Id => Global2x3Id.Value.ToString(),
                Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId Gloval4Id => Gloval4Id.Value.ToString(),
                _ => null
            };
        }

        private static int? GetEntityLabel(object obj)
        {
            var isTypedByGlobalIdInfo = obj.GetType().GetProperty("EntityLabel");

            var isTypedByGlobalIdValue = isTypedByGlobalIdInfo?.GetValue(obj);
            return (int?)isTypedByGlobalIdValue;
        }

        private static string[] GetMaterials(IIfcObjectDefinition objectDefinition)
        {
            var material = objectDefinition.GetType().GetProperty("Material");
            if (material == null) return [];

            var materialsv = material.GetValue(objectDefinition);
            if (materialsv == null) return [];

            var materials = materialsv.GetType().GetProperty("Materials");
            if (materials != null)
            {
                var maters = materials.GetValue(materialsv);
                switch (maters)
                {
                    case Xbim.Ifc4.ItemSet<IfcMaterial> mat1:
                        {
                            var materoalList = new List<string>(mat1.Count);
                            materoalList.AddRange(mat1.Select(item => $"IfcMaterial_{item.EntityLabel}"));

                            return materoalList.ToArray();
                        }
                    case Xbim.Ifc2x3.ItemSet<Xbim.Ifc2x3.MaterialResource.IfcMaterial> mat2:
                        {
                            var materoalList = new List<string>(mat2.Count);
                            materoalList.AddRange(mat2.Select(item => $"IfcMaterial_{item.EntityLabel}"));

                            return materoalList.ToArray();
                        }
                    default:
                        return [];
                }
            }
            else
            {
                return materialsv switch
                {
                    IfcMaterial material4 => [$"IfcMaterial_{material4.EntityLabel}"],
                    Xbim.Ifc2x3.MaterialResource.IfcMaterial material2x3 => [$"IfcMaterial_{material2x3.EntityLabel}"],
                    _ => []
                };
            }
        }

        private static string GetMaterialsV2(IIfcObjectDefinition objectDefinition)
        {
            var material = objectDefinition.GetType().GetProperty("Material");

            var materialsv = material?.GetValue(objectDefinition);
            if (materialsv is null) return null;

            var entLabel = GetEntityLabel(materialsv);
            return entLabel is null ? null : $"{objectDefinition.Material.ExpressType.Name}_{entLabel}";
        }

        private static void ExtractRelatedObjects(IIfcObjectDefinition objectDefinition, ref List<Metadata> metaObjects, string parentObjId)
        {
            var relatedObjects = objectDefinition.IsDecomposedBy.SelectMany(r => r.RelatedObjects);
            foreach (var item in relatedObjects)
            {
                var children = ExtractHierarchy(item, parentObjId);
                metaObjects.AddRange(children);
            }
        }

        private static string[] GetProperties(IIfcObject product)
        {
            return product.IsDefinedBy
                          .SelectMany(r => r.RelatingPropertyDefinition.PropertySetDefinitions)
                          .OfType<IIfcPropertySet>()
                          .Select(pset => pset.GlobalId.Value.ToString()).ToArray();
        }
    }
}