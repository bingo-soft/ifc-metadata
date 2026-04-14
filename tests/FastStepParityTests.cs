using System.Text.Json;

using Bingosoft.Net.IfcMetadata;

using Xunit;

namespace IfcMetadata.Tests;

public sealed class FastStepParityTests
{
    [Fact]
    public void ContractEquivalent_AllowsMetaObjectsMemberReordering()
    {
        const string left = """
        {
          "id": "model",
          "projectId": "project",
          "author": "",
          "createdAt": "2026-01-01T00:00:00+00:00",
          "schema": "IFC4",
          "creatingApplication": "app",
          "metaObjects": {
            "a": {
              "id": "a",
              "name": "A",
              "type": "IfcWall",
              "parent": null,
              "properties": null,
              "material_id": null,
              "type_id": null
            },
            "b": {
              "id": "b",
              "name": "B",
              "type": "IfcDoor",
              "parent": "a",
              "properties": ["p1"],
              "material_id": null,
              "type_id": "t1"
            }
          }
        }
        """;

        const string right = """
        {
          "id": "model",
          "projectId": "project",
          "author": "",
          "createdAt": "2026-01-01T00:00:00+00:00",
          "schema": "IFC4",
          "creatingApplication": "app",
          "metaObjects": {
            "b": {
              "id": "b",
              "name": "B",
              "type": "IfcDoor",
              "parent": "a",
              "properties": ["p1"],
              "material_id": null,
              "type_id": "t1"
            },
            "a": {
              "id": "a",
              "name": "A",
              "type": "IfcWall",
              "parent": null,
              "properties": null,
              "material_id": null,
              "type_id": null
            }
          }
        }
        """;

        var equivalent = AreContractEquivalent(left, right);

        Assert.True(equivalent);
    }

    [Fact]
    public void ContractEquivalent_DetectsMetaObjectValueDifference()
    {
        const string left = """
        {
          "id": "model",
          "projectId": "project",
          "author": "",
          "createdAt": "2026-01-01T00:00:00+00:00",
          "schema": "IFC4",
          "creatingApplication": "app",
          "metaObjects": {
            "a": {
              "id": "a",
              "name": "A",
              "type": "IfcWall",
              "parent": null,
              "properties": null,
              "material_id": null,
              "type_id": null
            }
          }
        }
        """;

        const string right = """
        {
          "id": "model",
          "projectId": "project",
          "author": "",
          "createdAt": "2026-01-01T00:00:00+00:00",
          "schema": "IFC4",
          "creatingApplication": "app",
          "metaObjects": {
            "a": {
              "id": "a",
              "name": "A changed",
              "type": "IfcWall",
              "parent": null,
              "properties": null,
              "material_id": null,
              "type_id": null
            }
          }
        }
        """;

        var equivalent = AreContractEquivalent(left, right);

        Assert.False(equivalent);
    }

    [Theory]
    [InlineData("IFC2X2_FINAL", true)]
    [InlineData("IFC2X2_FINAL", false)]
    [InlineData("IFC2X3", true)]
    [InlineData("IFC2X3", false)]
    [InlineData("IFC4", true)]
    [InlineData("IFC4", false)]
    [InlineData("IFC4X3_ADD2", true)]
    [InlineData("IFC4X3_ADD2", false)]
    public void Xbim_And_FastStep_Engines_ProduceEquivalentOutput_ForIfcSchemaFixtures(string schema, bool preserveOrder)
    {
        var ifcContent = CreateIfcFixture(schema);
        var ifcPath = Path.Combine(Path.GetTempPath(), $"ifc-metadata-{Guid.NewGuid():N}-{schema}.ifc");

        try
        {
            File.WriteAllText(ifcPath, ifcContent);
            AssertEquivalentEngineOutput(new FileInfo(ifcPath), preserveOrder);
        }
        finally
        {
            if (File.Exists(ifcPath))
            {
                File.Delete(ifcPath);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Xbim_And_FastStep_Engines_ProduceEquivalentOutput_WhenIfcFileIsConfigured(bool preserveOrder)
    {
        if (!TryGetIfcPath(out var ifcSourceFile))
        {
            return;
        }

        AssertEquivalentEngineOutput(ifcSourceFile, preserveOrder);
    }

    private static void AssertEquivalentEngineOutput(FileInfo ifcSourceFile, bool preserveOrder)
    {
        var xbimTargetPath = Path.Combine(Path.GetTempPath(), $"ifc-metadata-{Guid.NewGuid():N}-xbim.json");
        var fastTargetPath = Path.Combine(Path.GetTempPath(), $"ifc-metadata-{Guid.NewGuid():N}-fast.json");

        try
        {
            var xbimTargetFile = new FileInfo(xbimTargetPath);
            var fastTargetFile = new FileInfo(fastTargetPath);

            IfcEngineRouter.Export(ifcSourceFile, xbimTargetFile, preserveOrder, IfcExportEngine.Xbim);
            IfcEngineRouter.Export(ifcSourceFile, fastTargetFile, preserveOrder, IfcExportEngine.FastStep);

            var xbimJson = File.ReadAllText(xbimTargetPath);
            var fastJson = File.ReadAllText(fastTargetPath);

            var equivalent = TryGetContractDifference(xbimJson, fastJson, out var difference);
            Assert.True(equivalent, difference);
        }
        finally
        {
            if (File.Exists(xbimTargetPath))
            {
                File.Delete(xbimTargetPath);
            }

            if (File.Exists(fastTargetPath))
            {
                File.Delete(fastTargetPath);
            }
        }
    }

    private static string CreateIfcFixture(string schema)
    {
        return $"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
        FILE_NAME('fixture.ifc','2024-01-01T00:00:00',('author'),('org'),'app','system','auth');
        FILE_SCHEMA(('{schema}'));
        ENDSEC;
        DATA;
        #10=IFCPROJECT('project-guid',$,'Project Name',$,$,$,$,$,$);
        ENDSEC;
        END-ISO-10303-21;
        """;
    }

    private static bool AreContractEquivalent(string leftJson, string rightJson)
    {
        return TryGetContractDifference(leftJson, rightJson, out _);
    }

    private static bool TryGetContractDifference(string leftJson, string rightJson, out string difference)
    {
        using var leftDocument = JsonDocument.Parse(leftJson);
        using var rightDocument = JsonDocument.Parse(rightJson);

        return AreRootObjectsEquivalent(leftDocument.RootElement, rightDocument.RootElement, out difference);
    }

    private static bool AreRootObjectsEquivalent(JsonElement leftRoot, JsonElement rightRoot, out string difference)
    {
        difference = null!;

        if (leftRoot.ValueKind != JsonValueKind.Object || rightRoot.ValueKind != JsonValueKind.Object)
        {
            difference = "Root value kind mismatch.";
            return false;
        }

        string[] orderedRootFields = ["id", "projectId", "author", "createdAt", "schema", "creatingApplication", "metaObjects"];

        if (!HasOrderedFields(leftRoot, orderedRootFields) || !HasOrderedFields(rightRoot, orderedRootFields))
        {
            difference = "Root field order mismatch.";
            return false;
        }

        for (var i = 0; i < orderedRootFields.Length - 1; i++)
        {
            var fieldName = orderedRootFields[i];
            var leftValue = leftRoot.GetProperty(fieldName);
            var rightValue = rightRoot.GetProperty(fieldName);

            if (!JsonElement.DeepEquals(leftValue, rightValue))
            {
                difference = $"Root field mismatch: {fieldName}. left={leftValue.GetRawText()} right={rightValue.GetRawText()}";
                return false;
            }
        }

        if (!AreMetaObjectsEquivalent(leftRoot.GetProperty("metaObjects"), rightRoot.GetProperty("metaObjects"), out difference))
        {
            return false;
        }

        difference = string.Empty;
        return true;
    }

    private static bool AreMetaObjectsEquivalent(JsonElement leftMetaObjects, JsonElement rightMetaObjects, out string difference)
    {
        difference = null!;

        if (leftMetaObjects.ValueKind != JsonValueKind.Object || rightMetaObjects.ValueKind != JsonValueKind.Object)
        {
            difference = "metaObjects value kind mismatch.";
            return false;
        }

        var leftMap = ToObjectMap(leftMetaObjects);
        var rightMap = ToObjectMap(rightMetaObjects);

        if (leftMap.Count != rightMap.Count)
        {
            difference = $"metaObjects count mismatch: left={leftMap.Count} right={rightMap.Count}";
            return false;
        }

        foreach (var (key, leftValue) in leftMap)
        {
            if (!rightMap.TryGetValue(key, out var rightValue))
            {
                difference = $"metaObjects key missing in right: {key}";
                return false;
            }

            if (!AreMetaObjectPayloadsEquivalent(leftValue, rightValue, out difference))
            {
                difference = $"metaObjects[{key}] mismatch: {difference}";
                return false;
            }
        }

        difference = string.Empty;
        return true;
    }

    private static bool AreMetaObjectPayloadsEquivalent(JsonElement leftPayload, JsonElement rightPayload, out string difference)
    {
        difference = null!;
        string[] orderedMetaObjectFields = ["id", "name", "type", "parent", "properties", "material_id", "type_id"];

        if (!HasOrderedFields(leftPayload, orderedMetaObjectFields) || !HasOrderedFields(rightPayload, orderedMetaObjectFields))
        {
            difference = "Meta-object field order mismatch.";
            return false;
        }

        foreach (var field in orderedMetaObjectFields)
        {
            var leftValue = leftPayload.GetProperty(field);
            var rightValue = rightPayload.GetProperty(field);
            if (!JsonElement.DeepEquals(leftValue, rightValue))
            {
                difference = $"Field {field} mismatch. left={leftValue.GetRawText()} right={rightValue.GetRawText()}";
                return false;
            }
        }

        difference = string.Empty;
        return true;
    }

    private static Dictionary<string, JsonElement> ToObjectMap(JsonElement jsonObject)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var property in jsonObject.EnumerateObject())
        {
            map[property.Name] = property.Value;
        }

        return map;
    }

    private static bool HasOrderedFields(JsonElement jsonObject, IReadOnlyList<string> expectedOrder)
    {
        if (jsonObject.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var index = 0;
        foreach (var property in jsonObject.EnumerateObject())
        {
            if (index >= expectedOrder.Count || property.Name != expectedOrder[index])
            {
                return false;
            }

            index++;
        }

        return index == expectedOrder.Count;
    }

    private static bool TryGetIfcPath(out FileInfo ifcFile)
    {
        var configuredPath = Environment.GetEnvironmentVariable("IFC_BENCHMARK_FILE");
        if (TryResolvePath(configuredPath, out ifcFile))
        {
            return true;
        }

        var defaultCandidates = new[]
        {
            "ifc/1.ifc",
            "ifc/01_26_Slavyanka_4.ifc",
            "ifc/sample.ifc",
            "sample.ifc"
        };

        foreach (var candidate in defaultCandidates)
        {
            if (TryResolvePath(candidate, out ifcFile))
            {
                return true;
            }

            var rootRelativePath = ResolveFromBaseDirectory(candidate);
            if (TryResolvePath(rootRelativePath, out ifcFile))
            {
                return true;
            }
        }

        ifcFile = null!;
        return false;
    }

    private static bool TryResolvePath(string? path, out FileInfo ifcFile)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            ifcFile = new FileInfo(path);
            return true;
        }

        ifcFile = null!;
        return false;
    }

    private static string? ResolveFromBaseDirectory(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
