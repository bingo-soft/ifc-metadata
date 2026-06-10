using Bingosoft.Net.IfcMetadata;

using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

using Xunit;

namespace IfcMetadata.Tests;

public sealed class IfcAccessorsTypedIdTests
{
    [Fact]
    public void GetTypedId_MatchesInterfaceResolution_ForIfc2x3Sample()
    {
        var ifcPath = ResolveIfcFilePath();
        if (ifcPath is null)
        {
            return;
        }

        using var model = IfcStore.Open(ifcPath);

        var checkedCount = 0;
        foreach (var objectDefinition in model.Instances.OfType<IIfcObjectDefinition>())
        {
            var expected = ResolveExpectedTypedId(objectDefinition);
            var actual = IfcAccessors.GetTypedId(objectDefinition);
            Assert.True(
                string.Equals(expected, actual, StringComparison.Ordinal),
                $"Type={objectDefinition.GetType().FullName}; ObjectId={objectDefinition.GlobalId}; ExpectedTypedId={expected}; ActualTypedId={actual}");
            checkedCount++;
        }

        Assert.True(checkedCount > 0);
    }

    [Fact]
    public void GetTypedId_ReturnsNull_ForKnownIfc2x3NonTypedHotTypes()
    {
        var ifcPath = ResolveIfcFilePath();
        if (ifcPath is null)
        {
            return;
        }

        using var model = IfcStore.Open(ifcPath);

        var targetTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "IfcProject",
            "IfcBuilding",
            "IfcSite",
            "IfcBuildingStorey"
        };

        var checkedCount = 0;
        foreach (var objectDefinition in model.Instances.OfType<IIfcObjectDefinition>())
        {
            if (!targetTypeNames.Contains(objectDefinition.GetType().Name))
            {
                continue;
            }

            Assert.Null(IfcAccessors.GetTypedId(objectDefinition));
            checkedCount++;
        }

        Assert.True(checkedCount > 0);
    }

    private static string? ResolveExpectedTypedId(IIfcObjectDefinition objectDefinition)
    {
        if (objectDefinition is not IIfcObject ifcObject)
        {
            return null;
        }

        foreach (var relation in ifcObject.IsTypedBy)
        {
            var typedGlobalId = relation.RelatingType?.GlobalId;
            if (!string.IsNullOrWhiteSpace(typedGlobalId))
            {
                return typedGlobalId;
            }
        }

        return null;
    }

    private static string? ResolveIfcFilePath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("IFC_BENCHMARK_FILE");
        if (TryResolveExistingPath(fromEnv, out var envPath))
        {
            return envPath;
        }

        string[] candidates =
        [
            "ifc/01_26_Slavyanka_4.ifc",
            "ifc/sample.ifc",
            "sample.ifc"
        ];

        foreach (var candidate in candidates)
        {
            if (TryResolveExistingPath(candidate, out var resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private static bool TryResolveExistingPath(string? path, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (Path.IsPathRooted(path))
        {
            if (!File.Exists(path))
            {
                return false;
            }

            resolvedPath = path;
            return true;
        }

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var candidate = Path.GetFullPath(Path.Combine(root, path));
        if (!File.Exists(candidate))
        {
            return false;
        }

        resolvedPath = candidate;
        return true;
    }
}
