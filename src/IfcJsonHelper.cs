using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Bingosoft.Net.IfcMetadata;

internal static class IfcJsonHelper
{
    private static readonly JsonWriterOptions Jwo = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static void ToJson(FileInfo jsonTargetFile, ref MetadataExtractor metadata)
    {
        using var stream = File.OpenWrite(jsonTargetFile.FullName);
        using var writer = new Utf8JsonWriter(stream, Jwo);
        writer.WriteStartObject();

        writer.WriteString("id", metadata.Id);
        writer.WriteString("projectId", metadata.ProjectId);
        writer.WriteString("author", metadata.Author);
        writer.WriteString("createdAt", metadata.CreatedAt);
        writer.WriteString("schema", metadata.Schema);
        writer.WriteString("creatingApplication", metadata.CreatingApplication);

        writer.WriteStartObject("metaObjects");

        foreach (var item in metadata.MetaObjects)
        {
            writer.WriteStartObject(item.Id);

            writer.WriteString("id", item.Id);
            writer.WriteString("name", item.Name);
            writer.WriteString("type", item.Type);
            writer.WriteString("parent", item.Parent);

            if (item.PropertyIds?.Length > 0)
            {
                writer.WriteStartArray("properties");

                foreach (var propertyId in item.PropertyIds)
                {
                    writer.WriteStringValue(propertyId);
                }

                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNull("properties");
            }

            writer.WriteString("material_id", item.Material);
            writer.WriteString("type_id", item.TypeId);

            writer.WriteEndObject();
        }

        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}