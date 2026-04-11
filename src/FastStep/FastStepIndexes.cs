using System;
using System.Collections.Generic;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal sealed class FastStepIndexes
{
    private const int Missing = -1;
    private const int InitialEntityCapacity = 256;

    private int[] _entityIdsBySlot = new int[InitialEntityCapacity];
    private int[] _normalizedTypeStringIndexesBySlot = CreateInitializedArray(InitialEntityCapacity, Missing);
    private int[] _globalIdStringIndexesBySlot = CreateInitializedArray(InitialEntityCapacity, Missing);
    private int[] _nameStringIndexesBySlot = CreateInitializedArray(InitialEntityCapacity, Missing);

    internal FastStepIndexes()
    {
        EntityIdToSlot = CreateInitializedArray(1024, Missing);
    }

    internal FastStepStringPool StringPool { get; } = new();

    internal int[] EntityIdToSlot { get; private set; }

    internal int EntityCount { get; private set; }

    internal ReadOnlySpan<int> EntityIdsBySlot => _entityIdsBySlot.AsSpan(0, EntityCount);

    internal List<FastStepRelationRecord> DecompositionRelations { get; } = [];

    internal List<FastStepRelationRecord> ContainmentRelations { get; } = [];

    internal List<FastStepRelationRecord> DefinesByPropertiesRelations { get; } = [];

    internal List<FastStepRelationRecord> AssociatesMaterialRelations { get; } = [];

    internal List<FastStepRelationRecord> DefinesByTypeRelations { get; } = [];

    internal Dictionary<int, string> PropertySetGlobalIds { get; } = new();

    internal FastStepAdjacency DecompositionAdjacency { get; private set; } = FastStepAdjacency.Empty;

    internal FastStepAdjacency ContainmentAdjacency { get; private set; } = FastStepAdjacency.Empty;

    internal FastStepProjectRecord? Project { get; set; }


    internal int EnsureEntitySlot(int entityId)
    {
        if (entityId < 0)
        {
            return Missing;
        }

        EnsureEntityIdCapacity(entityId);

        var slot = EntityIdToSlot[entityId];
        if (slot != Missing)
        {
            return slot;
        }

        slot = EntityCount;
        EnsureSlotCapacity(slot + 1);

        _entityIdsBySlot[slot] = entityId;
        _normalizedTypeStringIndexesBySlot[slot] = Missing;
        _globalIdStringIndexesBySlot[slot] = Missing;
        _nameStringIndexesBySlot[slot] = Missing;

        EntityIdToSlot[entityId] = slot;
        EntityCount++;
        return slot;
    }

    internal void SetNormalizedType(int entityId, string normalizedType)
    {
        var slot = EnsureEntitySlot(entityId);
        if (slot == Missing)
        {
            return;
        }

        _normalizedTypeStringIndexesBySlot[slot] = StringPool.InternIndex(normalizedType);
    }

    internal void SetGlobalId(int entityId, string globalId)
    {
        var slot = EnsureEntitySlot(entityId);
        if (slot == Missing)
        {
            return;
        }

        _globalIdStringIndexesBySlot[slot] = StringPool.InternIndex(globalId);
    }

    internal void SetName(int entityId, string name)
    {
        var slot = EnsureEntitySlot(entityId);
        if (slot == Missing)
        {
            return;
        }

        _nameStringIndexesBySlot[slot] = StringPool.InternIndex(name);
    }

    internal string GetNormalizedTypeName(int entityId)
    {
        return TryGetStringByEntityId(entityId, _normalizedTypeStringIndexesBySlot);
    }

    internal string GetGlobalId(int entityId)
    {
        return TryGetStringByEntityId(entityId, _globalIdStringIndexesBySlot);
    }

    internal string GetName(int entityId)
    {
        return TryGetStringByEntityId(entityId, _nameStringIndexesBySlot);
    }

    internal string GetNormalizedTypeNameBySlot(int slot)
    {
        return TryGetStringBySlot(slot, _normalizedTypeStringIndexesBySlot);
    }

    internal string GetGlobalIdBySlot(int slot)
    {
        return TryGetStringBySlot(slot, _globalIdStringIndexesBySlot);
    }

    internal string GetNameBySlot(int slot)
    {
        return TryGetStringBySlot(slot, _nameStringIndexesBySlot);
    }

    internal int GetSlotOrMissing(int entityId)
    {
        if (entityId < 0 || entityId >= EntityIdToSlot.Length)
        {
            return Missing;
        }

        return EntityIdToSlot[entityId];
    }

    internal void BuildRelationAdjacency()
    {
        DecompositionAdjacency = FastStepAdjacency.Build(this, DecompositionRelations);
        ContainmentAdjacency = FastStepAdjacency.Build(this, ContainmentRelations);
    }


    private string TryGetStringByEntityId(int entityId, int[] indexesBySlot)
    {
        var slot = GetSlotOrMissing(entityId);
        return slot == Missing
            ? null
            : TryGetStringBySlot(slot, indexesBySlot);
    }

    private string TryGetStringBySlot(int slot, int[] indexesBySlot)
    {
        if (slot < 0 || slot >= EntityCount)
        {
            return null;
        }

        var stringIndex = indexesBySlot[slot];
        return StringPool.GetOrNull(stringIndex);
    }

    private void EnsureEntityIdCapacity(int entityId)
    {
        if (entityId < EntityIdToSlot.Length)
        {
            return;
        }

        var newLength = EntityIdToSlot.Length;
        while (newLength <= entityId)
        {
            newLength *= 2;
        }

        var resized = CreateInitializedArray(newLength, Missing);
        Array.Copy(EntityIdToSlot, resized, EntityIdToSlot.Length);
        EntityIdToSlot = resized;
    }

    private void EnsureSlotCapacity(int required)
    {
        if (required <= _entityIdsBySlot.Length)
        {
            return;
        }

        var newLength = _entityIdsBySlot.Length;
        while (newLength < required)
        {
            newLength *= 2;
        }

        Array.Resize(ref _entityIdsBySlot, newLength);
        ResizeWithFill(ref _normalizedTypeStringIndexesBySlot, newLength, Missing);
        ResizeWithFill(ref _globalIdStringIndexesBySlot, newLength, Missing);
        ResizeWithFill(ref _nameStringIndexesBySlot, newLength, Missing);
    }

    private static int[] CreateInitializedArray(int length, int value)
    {
        var array = new int[length];
        Array.Fill(array, value);
        return array;
    }

    private static void ResizeWithFill(ref int[] array, int newLength, int fillValue)
    {
        var previousLength = array.Length;
        Array.Resize(ref array, newLength);
        Array.Fill(array, fillValue, previousLength, newLength - previousLength);
    }
}

internal readonly record struct FastStepAdjacency(int[] Offsets, int[] Edges)
{
    internal static readonly FastStepAdjacency Empty = new([0], []);

    internal static FastStepAdjacency Build(FastStepIndexes indexes, List<FastStepRelationRecord> relations)
    {
        if (indexes.EntityCount == 0)
        {
            return Empty;
        }

        var offsets = new int[indexes.EntityCount + 1];

        for (var relationIndex = 0; relationIndex < relations.Count; relationIndex++)
        {
            var relation = relations[relationIndex];
            var parentSlot = indexes.GetSlotOrMissing(relation.RelatingId);
            if (parentSlot < 0)
            {
                continue;
            }

            for (var childIndex = 0; childIndex < relation.RelatedIds.Count; childIndex++)
            {
                var childEntityId = relation.RelatedIds[childIndex];
                if (indexes.GetSlotOrMissing(childEntityId) >= 0)
                {
                    offsets[parentSlot + 1]++;
                }
            }
        }

        for (var slot = 0; slot < indexes.EntityCount; slot++)
        {
            offsets[slot + 1] += offsets[slot];
        }

        var edges = new int[offsets[^1]];
        var cursors = new int[offsets.Length];
        Array.Copy(offsets, cursors, offsets.Length);

        for (var relationIndex = 0; relationIndex < relations.Count; relationIndex++)
        {
            var relation = relations[relationIndex];
            var parentSlot = indexes.GetSlotOrMissing(relation.RelatingId);
            if (parentSlot < 0)
            {
                continue;
            }

            for (var childIndex = 0; childIndex < relation.RelatedIds.Count; childIndex++)
            {
                var childSlot = indexes.GetSlotOrMissing(relation.RelatedIds[childIndex]);
                if (childSlot < 0)
                {
                    continue;
                }

                var cursor = cursors[parentSlot]++;
                edges[cursor] = childSlot;
            }
        }

        return new FastStepAdjacency(offsets, edges);
    }
}

internal sealed class FastStepScanDiagnostics

{
    internal Dictionary<int, FastStepEntityRange> EntityRanges { get; } = new();

    internal Dictionary<int, string> EntityRawArguments { get; } = new();
}

internal readonly record struct FastStepEntityRange(
    int StatementStartOffset,
    int StatementEndOffset,
    int ArgumentsStartOffset,
    int ArgumentsEndOffset);

internal readonly record struct FastStepScanOptions(bool CaptureDiagnostics)
{
    internal static readonly FastStepScanOptions Default = new(CaptureDiagnostics: false);
}

internal readonly record struct FastStepRelationRecord(int RelationId, int RelatingId, IReadOnlyList<int> RelatedIds);

internal readonly record struct FastStepProjectRecord(int EntityId, string GlobalId, string Name);

internal sealed class FastStepStringPool
{
    private readonly Dictionary<string, int> _indexByValue = new(StringComparer.Ordinal);
    private readonly List<string> _values = [];

    internal string Intern(string value)
    {
        return GetOrNull(InternIndex(value));
    }

    internal int InternIndex(string value)
    {
        if (value is null)
        {
            return -1;
        }

        if (_indexByValue.TryGetValue(value, out var index))
        {
            return index;
        }

        index = _values.Count;
        _values.Add(value);
        _indexByValue[value] = index;
        return index;
    }

    internal string GetOrNull(int index)
    {
        return index >= 0 && index < _values.Count
            ? _values[index]
            : null;
    }
}

