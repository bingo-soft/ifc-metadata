using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;


namespace Bingosoft.Net.IfcMetadata;

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
        using var model = IfcStore.Open(ifcPath.FullName);
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

    private static List<Metadata> ExtractHierarchy(IIfcObjectDefinition objectDefinition, string parentId = null)
    {
        var metaObjects = new List<Metadata>();
        ExtractHierarchy(metaObjects, objectDefinition, parentId);
        return metaObjects;
    }

    private static void ExtractHierarchy(List<Metadata> metaObjects, IIfcObjectDefinition objectDefinition, string parentId = null)
    {
        var parentObject = new Metadata
        {
            Id = objectDefinition.GlobalId,
            Name = objectDefinition.Name,
            Type = IfcAccessors.GetRuntimeTypeName(objectDefinition),
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
                        Type = IfcAccessors.GetRuntimeTypeName(element),
                        Parent = spatialElement.GlobalId,
                        TypeId = typeId,
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