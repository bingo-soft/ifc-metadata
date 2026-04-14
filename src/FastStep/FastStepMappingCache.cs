using System;
using System.Collections.Generic;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal sealed class FastStepMappingCache
{
    private readonly FastStepIndexes _indexes;

    private FastStepMappingCache(
        FastStepIndexes indexes,
        Dictionary<int, List<string>> propertySetByObjectId,
        Dictionary<int, string> materialByObjectId,
        Dictionary<int, string> typeByObjectId)
    {
        _indexes = indexes;
        PropertySetByObjectId = propertySetByObjectId;
        MaterialByObjectId = materialByObjectId;
        TypeByObjectId = typeByObjectId;
    }

    internal Dictionary<int, List<string>> PropertySetByObjectId { get; }

    internal Dictionary<int, string> MaterialByObjectId { get; }

    internal Dictionary<int, string> TypeByObjectId { get; }

    internal static FastStepMappingCache Build(FastStepIndexes indexes)
    {
        var propertySetByObjectId = BuildPropertySetMap(indexes);
        var materialByObjectId = BuildMaterialMap(indexes);
        var typeByObjectId = BuildTypeMap(indexes);

        return new FastStepMappingCache(indexes, propertySetByObjectId, materialByObjectId, typeByObjectId);
    }

    internal string GetTypeName(int entityId)
    {
        return _indexes.GetNormalizedTypeName(entityId) ?? "Unknown";
    }

    internal string FormatRelatedReference(int relatedEntityId)
    {
        var typeName = GetTypeName(relatedEntityId);
        var value = $"{typeName}_{relatedEntityId}";
        return _indexes.StringPool.Intern(value);
    }

    private static Dictionary<int, List<string>> BuildPropertySetMap(FastStepIndexes indexes)
    {
        var map = new Dictionary<int, List<string>>();

        for (var objectSlot = 0; objectSlot < indexes.EntityCount; objectSlot++)
        {
            var objectEntityId = indexes.GetEntityIdBySlot(objectSlot);
            var start = indexes.DefinesByPropertiesAdjacency.Offsets[objectSlot];
            var end = indexes.DefinesByPropertiesAdjacency.Offsets[objectSlot + 1];

            for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                var propertySetSlot = indexes.DefinesByPropertiesAdjacency.Edges[edgeIndex];
                var propertySetEntityId = indexes.GetEntityIdBySlot(propertySetSlot);
                if (!indexes.PropertySetGlobalIds.TryGetValue(propertySetEntityId, out var psetId) || string.IsNullOrWhiteSpace(psetId))
                {
                    continue;
                }

                if (!map.TryGetValue(objectEntityId, out var psetList))
                {
                    psetList = [];
                    map[objectEntityId] = psetList;
                }

                psetList.Add(psetId);
            }
        }

        return map;
    }

    private static Dictionary<int, string> BuildMaterialMap(FastStepIndexes indexes)
    {
        var map = new Dictionary<int, string>();

        for (var objectSlot = 0; objectSlot < indexes.EntityCount; objectSlot++)
        {
            var objectEntityId = indexes.GetEntityIdBySlot(objectSlot);
            var start = indexes.AssociatesMaterialAdjacency.Offsets[objectSlot];
            var end = indexes.AssociatesMaterialAdjacency.Offsets[objectSlot + 1];

            for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                var materialSlot = indexes.AssociatesMaterialAdjacency.Edges[edgeIndex];
                var materialEntityId = indexes.GetEntityIdBySlot(materialSlot);
                map[objectEntityId] = FormatRelatedReference(indexes, materialEntityId);
            }
        }

        return map;
    }

    private static Dictionary<int, string> BuildTypeMap(FastStepIndexes indexes)
    {
        var map = new Dictionary<int, string>();

        for (var objectSlot = 0; objectSlot < indexes.EntityCount; objectSlot++)
        {
            var objectEntityId = indexes.GetEntityIdBySlot(objectSlot);
            var start = indexes.DefinesByTypeAdjacency.Offsets[objectSlot];
            var end = indexes.DefinesByTypeAdjacency.Offsets[objectSlot + 1];

            for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                var typeSlot = indexes.DefinesByTypeAdjacency.Edges[edgeIndex];
                var typeEntityId = indexes.GetEntityIdBySlot(typeSlot);

                var typeId = indexes.GetGlobalId(typeEntityId);
                if (string.IsNullOrWhiteSpace(typeId))
                {
                    typeId = FormatRelatedReference(indexes, typeEntityId);
                }

                map[objectEntityId] = typeId;
            }
        }

        return map;
    }

    private static string FormatRelatedReference(FastStepIndexes indexes, int relatedEntityId)
    {
        var typeName = indexes.GetNormalizedTypeName(relatedEntityId) ?? "Unknown";
        var value = $"{typeName}_{relatedEntityId}";
        return indexes.StringPool.Intern(value);
    }
}

