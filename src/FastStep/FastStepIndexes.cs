using System.Collections.Generic;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal sealed class FastStepIndexes
{
    internal Dictionary<int, FastStepEntityRecord> Entities { get; } = new();

    internal Dictionary<int, string> EntityGlobalIds { get; } = new();

    internal Dictionary<int, string> EntityNames { get; } = new();

    internal List<FastStepRelationRecord> DecompositionRelations { get; } = [];

    internal List<FastStepRelationRecord> ContainmentRelations { get; } = [];

    internal List<FastStepRelationRecord> DefinesByPropertiesRelations { get; } = [];

    internal List<FastStepRelationRecord> AssociatesMaterialRelations { get; } = [];

    internal List<FastStepRelationRecord> DefinesByTypeRelations { get; } = [];

    internal Dictionary<int, string> PropertySetGlobalIds { get; } = new();

    internal FastStepProjectRecord? Project { get; set; }
}

internal readonly record struct FastStepEntityRecord(int EntityId, string EntityType, string RawArguments);

internal readonly record struct FastStepRelationRecord(int RelationId, int RelatingId, IReadOnlyList<int> RelatedIds);

internal readonly record struct FastStepProjectRecord(int EntityId, string GlobalId, string Name);
