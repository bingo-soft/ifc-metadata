using System.Collections.Generic;
using System.IO;
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
                var project = GetProject(model)
                              ?? throw new InvalidDataException("IFC project root (IIfcProject) was not found.");

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

        private static IIfcProject GetProject(IfcStore model)
        {
            foreach (var instance in model.Instances)
            {
                if (instance is IIfcProject project)
                {
                    return project;
                }
            }

            return null;
        }

        private static string GetAuthor(IList<string> authors)
        {
            if (authors.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < authors.Count; i++)
            {
                sb.Append(authors[i]);
                if (i < authors.Count - 1)
                {
                    sb.Append(';');
                }
            }

            return sb.ToString();
        }

        private static List<Metadata> ExtractHierarchy(IIfcObjectDefinition objectDefinition, string parentId = null)
        {
            var metaObjects = new List<Metadata>();
            ExtractHierarchy(metaObjects, objectDefinition, parentId);
            return metaObjects;
        }

        private static void ExtractHierarchy(List<Metadata> metaObjects, IIfcObjectDefinition objectDefinition, string parentId = null)
        {
            var objectType = objectDefinition.GetType();
            var parentObject = new Metadata
            {
                Id = objectDefinition.GlobalId,
                Name = objectDefinition.Name,
                Type = objectType.Name,
                Parent = parentId,
                                TypeId = IfcAccessors.GetTypedId(objectDefinition)

            };

            if (objectDefinition is not IIfcProject)
            {
                var parentProps = GetProperties((IIfcObject)objectDefinition);
                if (parentProps.Length > 0)
                {
                    parentObject.PropertyIds = parentProps;
                }
            }

                        parentObject.Material = IfcAccessors.GetMaterialId(objectDefinition);

            metaObjects.Add(parentObject);

            if (objectDefinition is IIfcSpatialStructureElement spatialElement)
            {
                foreach (var relation in spatialElement.ContainsElements)
                {
                    foreach (var element in relation.RelatedElements)
                    {
                                                var typeId = IfcAccessors.GetTypedId(element);

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

                                                mo.Material = IfcAccessors.GetMaterialId(element);


                        metaObjects.Add(mo);
                        ExtractRelatedObjects(element, metaObjects, mo.Id);
                    }
                }
            }

            ExtractRelatedObjects(objectDefinition, metaObjects, parentObject.Id);
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
                            foreach (var item in mat1)
                            {
                                materoalList.Add($"IfcMaterial_{item.EntityLabel}");
                            }

                            return materoalList.ToArray();
                        }
                    case Xbim.Ifc2x3.ItemSet<Xbim.Ifc2x3.MaterialResource.IfcMaterial> mat2:
                        {
                            var materoalList = new List<string>(mat2.Count);
                            foreach (var item in mat2)
                            {
                                materoalList.Add($"IfcMaterial_{item.EntityLabel}");
                            }

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

        

        private static void ExtractRelatedObjects(IIfcObjectDefinition objectDefinition, List<Metadata> metaObjects, string parentObjId)
        {
            foreach (var relation in objectDefinition.IsDecomposedBy)
            {
                foreach (var item in relation.RelatedObjects)
                {
                    ExtractHierarchy(metaObjects, item, parentObjId);
                }
            }
        }

        private static string[] GetProperties(IIfcObject product)
        {
            var propertyIds = new List<string>();
            foreach (var relation in product.IsDefinedBy)
            {
                var relatedPropertyDefinition = relation.RelatingPropertyDefinition;
                if (relatedPropertyDefinition is null)
                {
                    continue;
                }

                foreach (var propertyDefinition in relatedPropertyDefinition.PropertySetDefinitions)
                {
                    if (propertyDefinition is IIfcPropertySet propertySet)
                    {
                        propertyIds.Add(propertySet.GlobalId.Value.ToString());
                    }
                }
            }

            return propertyIds.Count == 0 ? [] : propertyIds.ToArray();
        }
    }
}