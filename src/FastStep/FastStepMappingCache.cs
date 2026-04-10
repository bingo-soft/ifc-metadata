using System;
using System.Collections.Generic;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal sealed class FastStepMappingCache
{
    private readonly FastStepIndexes _indexes;

    private FastStepMappingCache(
        FastStepIndexes indexes,
        Dictionary<int, string> normalizedTypeByEntityId,
        Dictionary<int, List<string>> propertySetByObjectId,
        Dictionary<int, string> materialByObjectId,
        Dictionary<int, string> typeByObjectId)
    {
        _indexes = indexes;
        NormalizedTypeByEntityId = normalizedTypeByEntityId;
        PropertySetByObjectId = propertySetByObjectId;
        MaterialByObjectId = materialByObjectId;
        TypeByObjectId = typeByObjectId;
    }

    internal Dictionary<int, string> NormalizedTypeByEntityId { get; }

    internal Dictionary<int, List<string>> PropertySetByObjectId { get; }

    internal Dictionary<int, string> MaterialByObjectId { get; }

    internal Dictionary<int, string> TypeByObjectId { get; }

    internal static FastStepMappingCache Build(FastStepIndexes indexes)
    {
        var normalizedTypeByEntityId = BuildNormalizedTypeMap(indexes);
        var propertySetByObjectId = BuildPropertySetMap(indexes);
        var materialByObjectId = BuildMaterialMap(indexes, normalizedTypeByEntityId);
        var typeByObjectId = BuildTypeMap(indexes, normalizedTypeByEntityId);

        return new FastStepMappingCache(indexes, normalizedTypeByEntityId, propertySetByObjectId, materialByObjectId, typeByObjectId);
    }

    internal string GetTypeName(int entityId)
    {
        return NormalizedTypeByEntityId.GetValueOrDefault(entityId, "Unknown");
    }

    internal string FormatRelatedReference(int relatedEntityId)
    {
        var typeName = GetTypeName(relatedEntityId);
        var value = $"{typeName}_{relatedEntityId}";
        return _indexes.StringPool.Intern(value);
    }

    private static Dictionary<int, string> BuildNormalizedTypeMap(FastStepIndexes indexes)
    {
        var normalizedByEntityId = new Dictionary<int, string>();
        var normalizedByRawType = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (entityId, entity) in indexes.Entities)
        {
            if (!normalizedByRawType.TryGetValue(entity.EntityType, out var normalizedType))
            {
                normalizedType = indexes.StringPool.Intern(FastStepTypeNameNormalizer.Normalize(entity.EntityType));
                normalizedByRawType[entity.EntityType] = normalizedType;
            }

            normalizedByEntityId[entityId] = normalizedType;
        }

        return normalizedByEntityId;
    }

    private static Dictionary<int, List<string>> BuildPropertySetMap(FastStepIndexes indexes)
    {
        var map = new Dictionary<int, List<string>>();

        foreach (var relation in indexes.DefinesByPropertiesRelations)
        {
            if (!indexes.PropertySetGlobalIds.TryGetValue(relation.RelatingId, out var psetId) || string.IsNullOrWhiteSpace(psetId))
            {
                continue;
            }

            for (var i = 0; i < relation.RelatedIds.Count; i++)
            {
                var objectEntityId = relation.RelatedIds[i];
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

    private static Dictionary<int, string> BuildMaterialMap(FastStepIndexes indexes, Dictionary<int, string> normalizedTypeByEntityId)
    {
        var map = new Dictionary<int, string>();

        foreach (var relation in indexes.AssociatesMaterialRelations)
        {
            var materialId = FormatRelatedReference(indexes, normalizedTypeByEntityId, relation.RelatingId);
            for (var i = 0; i < relation.RelatedIds.Count; i++)
            {
                map[relation.RelatedIds[i]] = materialId;
            }
        }

        return map;
    }

    private static Dictionary<int, string> BuildTypeMap(FastStepIndexes indexes, Dictionary<int, string> normalizedTypeByEntityId)
    {
        var map = new Dictionary<int, string>();

        foreach (var relation in indexes.DefinesByTypeRelations)
        {
            var typeId = indexes.EntityGlobalIds.GetValueOrDefault(relation.RelatingId);
            if (string.IsNullOrWhiteSpace(typeId))
            {
                typeId = FormatRelatedReference(indexes, normalizedTypeByEntityId, relation.RelatingId);
            }

            for (var i = 0; i < relation.RelatedIds.Count; i++)
            {
                map[relation.RelatedIds[i]] = typeId;
            }
        }

        return map;
    }

    private static string FormatRelatedReference(
        FastStepIndexes indexes,
        Dictionary<int, string> normalizedTypeByEntityId,
        int relatedEntityId)
    {
        var typeName = normalizedTypeByEntityId.GetValueOrDefault(relatedEntityId, "Unknown");
        var value = $"{typeName}_{relatedEntityId}";
        return indexes.StringPool.Intern(value);
    }
}
