using System.Text.Json;
using Bingosoft.Net.IfcMetadata;
using Xunit;

namespace IfcMetadata.Tests;

public sealed class IfcJsonHelperTests
{
    [Fact]
    public void ToJson_WritesExpectedRootFields_AndMetaObjectsMap()
    {
        var targetPath = Path.Combine(Path.GetTempPath(), $"ifc-metadata-{Guid.NewGuid():N}.json");
        var targetFile = new FileInfo(targetPath);

        try
        {
            var metadata = new MetadataExtractor
            {
                Id = "model-name",
                ProjectId = "project-global-id",
                Author = "author",
                CreatedAt = "2024-01-01T00:00:00+00:00",
                Schema = "IFC4",
                CreatingApplication = "test-app",
                MetaObjects = new List<Metadata>
                {
                    new()
                    {
                        Id = "obj-1",
                        Name = "Wall 1",
                        Type = "IfcWall",
                        Parent = "storey-1",
                        PropertyIds = ["prop-1", "prop-2"],
                        Material = "IfcMaterial_123",
                        TypeId = "type-1"
                    }
                }
            };

            IfcJsonHelper.ToJson(targetFile, ref metadata);

            using var document = JsonDocument.Parse(File.ReadAllText(targetPath));
            var root = document.RootElement;

            Assert.Equal("model-name", root.GetProperty("id").GetString());
            Assert.Equal("project-global-id", root.GetProperty("projectId").GetString());
            Assert.Equal("author", root.GetProperty("author").GetString());
            Assert.Equal("IFC4", root.GetProperty("schema").GetString());

            var metaObjects = root.GetProperty("metaObjects");
            var obj = metaObjects.GetProperty("obj-1");

            Assert.Equal("Wall 1", obj.GetProperty("name").GetString());
            Assert.Equal("IfcWall", obj.GetProperty("type").GetString());
            Assert.Equal("storey-1", obj.GetProperty("parent").GetString());
            Assert.Equal("IfcMaterial_123", obj.GetProperty("material_id").GetString());
            Assert.Equal("type-1", obj.GetProperty("type_id").GetString());

            var properties = obj.GetProperty("properties");
            Assert.Equal(JsonValueKind.Array, properties.ValueKind);
            Assert.Equal(2, properties.GetArrayLength());
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [Fact]
    public void ToJson_WritesNullProperties_WhenPropertyListIsMissing()
    {
        var targetPath = Path.Combine(Path.GetTempPath(), $"ifc-metadata-{Guid.NewGuid():N}.json");
        var targetFile = new FileInfo(targetPath);

        try
        {
            var metadata = new MetadataExtractor
            {
                Id = "model-name",
                ProjectId = "project-global-id",
                Author = "author",
                CreatedAt = "2024-01-01T00:00:00+00:00",
                Schema = "IFC4",
                CreatingApplication = "test-app",
                MetaObjects = new List<Metadata>
                {
                    new()
                    {
                        Id = "obj-1",
                        Name = "Storey 1",
                        Type = "IfcBuildingStorey",
                        Parent = "project-1",
                        PropertyIds = null,
                        Material = null,
                        TypeId = null
                    }
                }
            };

            IfcJsonHelper.ToJson(targetFile, ref metadata);

            using var document = JsonDocument.Parse(File.ReadAllText(targetPath));
            var root = document.RootElement;
            var metaObject = root.GetProperty("metaObjects").GetProperty("obj-1");

            Assert.Equal(JsonValueKind.Null, metaObject.GetProperty("properties").ValueKind);
            Assert.Equal(JsonValueKind.Null, metaObject.GetProperty("material_id").ValueKind);
            Assert.Equal(JsonValueKind.Null, metaObject.GetProperty("type_id").ValueKind);
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }
}
