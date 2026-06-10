using System.Text.Json.Serialization;

namespace Bingosoft.Net.IfcMetadata;

/// <summary>
///   The MetaObject is used to serialise the building elements within the IFC
///   model. It is a representation of a single element (e.g. IfcProject,
///   IfcStorey, IfcWindow, etc.).
/// </summary>
public struct Metadata
{
    /// <summary>
    ///   The GlobalId of the building element
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    ///   The Name of the building element
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    ///   The IFC type of the building element, e.g. 'IfcStandardWallCase'
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    ///   The GlobalId of the parent element if any.
    /// </summary>
    [JsonPropertyName("parent")]
    public string Parent { get; set; }

    [JsonPropertyName("properties")]
    public string[] PropertyIds { get; set; }

    [JsonPropertyName("material_id")]
    public string Material { get; set; }

    [JsonPropertyName("type_id")]
    public string TypeId { get; set; }
}