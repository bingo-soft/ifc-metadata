using System;
using System.Collections.Generic;
using System.Reflection;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal static class FastStepTypeNameNormalizer
{
    private static readonly Lazy<Dictionary<string, string>> CanonicalTypeNames = new(CreateCanonicalTypeMap);

    internal static string Normalize(string rawTypeName)
    {
        if (string.IsNullOrWhiteSpace(rawTypeName))
        {
            return rawTypeName;
        }

        var key = rawTypeName.Trim().ToUpperInvariant();
        return CanonicalTypeNames.Value.TryGetValue(key, out var canonical)
            ? canonical
            : rawTypeName.Trim();
    }

    private static Dictionary<string, string> CreateCanonicalTypeMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        Assembly[] assemblies =
        [
            typeof(Xbim.Ifc2x3.Kernel.IfcProject).Assembly,
            typeof(Xbim.Ifc4.Kernel.IfcProject).Assembly,
        ];

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                var typeName = type.Name;
                if (!typeName.StartsWith("Ifc", StringComparison.Ordinal) || typeName.Length <= 3)
                {
                    continue;
                }

                map[typeName.ToUpperInvariant()] = typeName;
            }
        }

        return map;
    }
}
