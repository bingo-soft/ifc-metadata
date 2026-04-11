using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal static class StepEntityScanner
{
    internal static FastStepIndexes Scan(FileInfo ifcSourceFile)
    {
        return ScanWithHeader(ifcSourceFile).Indexes;
    }

    internal static FastStepIndexes Scan(TextReader reader)
    {
        return Scan(reader, FastStepScanOptions.Default, out _);
    }

    internal static FastStepIndexes Scan(TextReader reader, FastStepScanOptions options, out FastStepScanDiagnostics diagnostics)
    {
        var indexes = new FastStepIndexes();
        var scanDiagnostics = options.CaptureDiagnostics ? new FastStepScanDiagnostics() : null;
        using var relationEdges = new FastStepRelationEdgeBuffers();

        var entitiesChannel = Channel.CreateBounded<StepEntityToken>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var producer = Task.Run(async () =>
        {
            try
            {
                foreach (var entity in StepLexer.EnumerateEntities(reader, captureRawArguments: true))
                {
                    await entitiesChannel.Writer.WriteAsync(entity).ConfigureAwait(false);
                }

                entitiesChannel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                entitiesChannel.Writer.TryComplete(ex);
                throw;
            }
        });

        var consumer = Task.Run(async () =>
        {
            var normalizedByRawType = new Dictionary<string, string>(StringComparer.Ordinal);

            await foreach (var entity in entitiesChannel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                IndexEntity(indexes, relationEdges, entity, scanDiagnostics, normalizedByRawType);
            }
        });

        try
        {
            Task.WaitAll(producer, consumer);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Count > 0)
        {
            throw ex.InnerExceptions[0];
        }

        indexes.BuildRelationAdjacency(
            relationEdges.Decomposition,
            relationEdges.Containment,
            relationEdges.DefinesByProperties,
            relationEdges.AssociatesMaterial,
            relationEdges.DefinesByType);

        diagnostics = scanDiagnostics;
        return indexes;
    }

    internal static FastStepScanResult ScanWithHeader(FileInfo ifcSourceFile)
    {
        return ScanWithHeader(ifcSourceFile, FastStepScanOptions.Default);
    }

    internal static FastStepScanResult ScanWithHeader(FileInfo ifcSourceFile, FastStepScanOptions options)
    {
        using var stream = ifcSourceFile.OpenRead();
        using var reader = new StreamReader(stream);
        return ScanWithHeader(reader, options);
    }

    internal static FastStepScanResult ScanWithHeader(TextReader reader)
    {
        return ScanWithHeader(reader, FastStepScanOptions.Default);
    }

    internal static FastStepScanResult ScanWithHeader(TextReader reader, FastStepScanOptions options)
    {
        var header = StepHeaderReader.Read(reader);
        var indexes = Scan(reader, options, out var diagnostics);
        return new FastStepScanResult(indexes, header, diagnostics);
    }

    private static void IndexEntity(
        FastStepIndexes indexes,
        FastStepRelationEdgeBuffers relationEdges,
        StepEntityToken entity,
        FastStepScanDiagnostics diagnostics,
        Dictionary<string, string> normalizedByRawType)
    {
        var pooledEntityType = indexes.StringPool.Intern(entity.EntityType);
        var normalizedType = GetNormalizedType(indexes, pooledEntityType, normalizedByRawType);

        indexes.EnsureEntitySlot(entity.EntityId);
        indexes.SetNormalizedType(entity.EntityId, normalizedType);

        if (diagnostics is not null)
        {
            diagnostics.EntityRawArguments[entity.EntityId] = entity.RawArguments;
            diagnostics.EntityRanges[entity.EntityId] = new FastStepEntityRange(
                entity.StatementStartOffset,
                entity.StatementEndOffset,
                entity.ArgumentsStartOffset,
                entity.ArgumentsEndOffset);
        }

        IndexEntityIdentity(indexes, entity.EntityId, entity.RawArguments);
        IndexKnownEntity(indexes, relationEdges, entity.EntityId, pooledEntityType, entity.RawArguments);
    }

    private static string GetNormalizedType(
        FastStepIndexes indexes,
        string entityType,
        Dictionary<string, string> normalizedByRawType)
    {
        if (normalizedByRawType.TryGetValue(entityType, out var normalizedType))
        {
            return normalizedType;
        }

        normalizedType = indexes.StringPool.Intern(FastStepTypeNameNormalizer.Normalize(entityType));
        normalizedByRawType[entityType] = normalizedType;
        return normalizedType;
    }

    private static void IndexKnownEntity(
        FastStepIndexes indexes,
        FastStepRelationEdgeBuffers relationEdges,
        int entityId,
        string entityType,
        string rawArguments)
    {
        switch (entityType.ToUpperInvariant())
        {
            case "IFCPROJECT":
                IndexProject(indexes, entityId, rawArguments);
                break;
            case "IFCRELAGGREGATES":
                IndexSingleToListEdges(relationEdges.Decomposition, rawArguments, parentArgIndex: 4, childrenArgIndex: 5);
                break;
            case "IFCRELCONTAINEDINSPATIALSTRUCTURE":
                IndexSingleToListEdges(relationEdges.Containment, rawArguments, parentArgIndex: 5, childrenArgIndex: 4);
                break;
            case "IFCRELDEFINESBYPROPERTIES":
                IndexListToSingleEdges(relationEdges.DefinesByProperties, rawArguments, parentsArgIndex: 4, childArgIndex: 5);
                break;
            case "IFCRELASSOCIATESMATERIAL":
                IndexListToSingleEdges(relationEdges.AssociatesMaterial, rawArguments, parentsArgIndex: 4, childArgIndex: 5);
                break;
            case "IFCRELDEFINESBYTYPE":
                IndexListToSingleEdges(relationEdges.DefinesByType, rawArguments, parentsArgIndex: 4, childArgIndex: 5);
                break;
            case "IFCPROPERTYSET":
                IndexPropertySet(indexes, entityId, rawArguments);
                break;
        }
    }

    private static void IndexProject(FastStepIndexes indexes, int entityId, string rawArguments)
    {
        var rawArgumentsSpan = rawArguments.AsSpan();

        var globalId = StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, 0, out var globalStart, out var globalLength)
            ? StepParsingUtilities.ParseStepString(rawArgumentsSpan.Slice(globalStart, globalLength))
            : null;
        var name = StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, 2, out var nameStart, out var nameLength)
            ? StepParsingUtilities.ParseStepString(rawArgumentsSpan.Slice(nameStart, nameLength))
            : null;

        if (globalId is not null)
        {
            globalId = indexes.StringPool.Intern(globalId);
        }

        if (name is not null)
        {
            name = indexes.StringPool.Intern(name);
        }

        indexes.Project = new FastStepProjectRecord(entityId, globalId, name);
    }

    private static void IndexPropertySet(FastStepIndexes indexes, int entityId, string rawArguments)
    {
        var rawArgumentsSpan = rawArguments.AsSpan();
        if (!StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, 0, out var globalStart, out var globalLength))
        {
            return;
        }

        var globalId = StepParsingUtilities.ParseStepString(rawArgumentsSpan.Slice(globalStart, globalLength));
        if (string.IsNullOrEmpty(globalId))
        {
            return;
        }

        indexes.PropertySetGlobalIds[entityId] = indexes.StringPool.Intern(globalId);
    }

    private static void IndexSingleToListEdges(FastStepRelationEdgeBuffer target, string rawArguments, int parentArgIndex, int childrenArgIndex)
    {
        var rawArgumentsSpan = rawArguments.AsSpan();
        if (!StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, parentArgIndex, out var parentStart, out var parentLength)
            || !StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, childrenArgIndex, out var childrenStart, out var childrenLength))
        {
            return;
        }

        var parentId = StepParsingUtilities.ParseStepReference(rawArgumentsSpan.Slice(parentStart, parentLength));
        if (parentId is null)
        {
            return;
        }

        var childIds = StepParsingUtilities.ParseStepReferenceList(rawArgumentsSpan.Slice(childrenStart, childrenLength));
        for (var i = 0; i < childIds.Count; i++)
        {
            target.Add(new FastStepRelationEdge(parentId.Value, childIds[i]));
        }
    }

    private static void IndexListToSingleEdges(FastStepRelationEdgeBuffer target, string rawArguments, int parentsArgIndex, int childArgIndex)
    {
        var rawArgumentsSpan = rawArguments.AsSpan();
        if (!StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, parentsArgIndex, out var parentsStart, out var parentsLength)
            || !StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, childArgIndex, out var childStart, out var childLength))
        {
            return;
        }

        var childId = StepParsingUtilities.ParseStepReference(rawArgumentsSpan.Slice(childStart, childLength));
        if (childId is null)
        {
            return;
        }

        var parentIds = StepParsingUtilities.ParseStepReferenceList(rawArgumentsSpan.Slice(parentsStart, parentsLength));
        for (var i = 0; i < parentIds.Count; i++)
        {
            target.Add(new FastStepRelationEdge(parentIds[i], childId.Value));
        }
    }

    private static void IndexEntityIdentity(FastStepIndexes indexes, int entityId, string rawArguments)
    {
        var rawArgumentsSpan = rawArguments.AsSpan();

        if (StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, 0, out var globalStart, out var globalLength))
        {
            var globalId = StepParsingUtilities.ParseStepString(rawArgumentsSpan.Slice(globalStart, globalLength));
            if (!string.IsNullOrWhiteSpace(globalId))
            {
                indexes.SetGlobalId(entityId, indexes.StringPool.Intern(globalId));
            }
        }

        if (StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, 2, out var nameStart, out var nameLength))
        {
            var name = StepParsingUtilities.ParseStepString(rawArgumentsSpan.Slice(nameStart, nameLength));
            if (name is not null)
            {
                indexes.SetName(entityId, indexes.StringPool.Intern(name));
            }
        }
    }
}

internal sealed class FastStepRelationEdgeBuffers : IDisposable
{
    internal FastStepRelationEdgeBuffer Decomposition { get; } = new();

    internal FastStepRelationEdgeBuffer Containment { get; } = new();

    internal FastStepRelationEdgeBuffer DefinesByProperties { get; } = new();

    internal FastStepRelationEdgeBuffer AssociatesMaterial { get; } = new();

    internal FastStepRelationEdgeBuffer DefinesByType { get; } = new();

    public void Dispose()
    {
        Decomposition.Dispose();
        Containment.Dispose();
        DefinesByProperties.Dispose();
        AssociatesMaterial.Dispose();
        DefinesByType.Dispose();
    }
}

internal sealed class FastStepRelationEdgeBuffer : IReadOnlyList<FastStepRelationEdge>, IDisposable
{
    private const int SegmentSize = 256;

    private readonly List<FastStepRelationEdge[]> _segments = [];

    private int _count;

    public FastStepRelationEdge this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var segmentIndex = index / SegmentSize;
            var offsetInSegment = index % SegmentSize;
            return _segments[segmentIndex][offsetInSegment];
        }
    }

    public int Count => _count;

    public void Add(FastStepRelationEdge edge)
    {
        var segmentIndex = _count / SegmentSize;
        if (segmentIndex == _segments.Count)
        {
            _segments.Add(ArrayPool<FastStepRelationEdge>.Shared.Rent(SegmentSize));
        }

        var offsetInSegment = _count % SegmentSize;
        _segments[segmentIndex][offsetInSegment] = edge;
        _count++;
    }

    public void Dispose()
    {
        for (var i = 0; i < _segments.Count; i++)
        {
            ArrayPool<FastStepRelationEdge>.Shared.Return(_segments[i]);
        }

        _segments.Clear();
        _count = 0;
    }

    public IEnumerator<FastStepRelationEdge> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

internal readonly record struct FastStepScanResult(FastStepIndexes Indexes, FastStepHeader Header, FastStepScanDiagnostics Diagnostics);

