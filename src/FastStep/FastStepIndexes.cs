using System.Collections.Generic;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal sealed class FastStepIndexes
{
    internal FastStepStringPool StringPool { get; } = new();

    internal Dictionary<int, string> NormalizedTypeByEntityId { get; } = new();

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
    private readonly Dictionary<string, string> _pool = new(System.StringComparer.Ordinal);

    internal string Intern(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (_pool.TryGetValue(value, out var pooled))
        {
            return pooled;
        }

        _pool[value] = value;
        return value;
    }
}
