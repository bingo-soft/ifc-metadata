using System;
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
                IndexEntity(indexes, entity, scanDiagnostics, normalizedByRawType);
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
        StepEntityToken entity,
        FastStepScanDiagnostics diagnostics,
        Dictionary<string, string> normalizedByRawType)
    {
        var pooledEntityType = indexes.StringPool.Intern(entity.EntityType);
        var normalizedType = GetNormalizedType(indexes, pooledEntityType, normalizedByRawType);

        indexes.NormalizedTypeByEntityId[entity.EntityId] = normalizedType;

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
        IndexKnownEntity(indexes, entity.EntityId, pooledEntityType, entity.RawArguments);
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

    private static void IndexKnownEntity(FastStepIndexes indexes, int entityId, string entityType, string rawArguments)
    {
        switch (entityType.ToUpperInvariant())
        {
            case "IFCPROJECT":
                IndexProject(indexes, entityId, rawArguments);
                break;
            case "IFCRELAGGREGATES":
                IndexRelation(indexes.DecompositionRelations, entityId, rawArguments, relatingArgIndex: 4, relatedArgIndex: 5);
                break;
            case "IFCRELCONTAINEDINSPATIALSTRUCTURE":
                IndexRelation(indexes.ContainmentRelations, entityId, rawArguments, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCRELDEFINESBYPROPERTIES":
                IndexRelation(indexes.DefinesByPropertiesRelations, entityId, rawArguments, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCRELASSOCIATESMATERIAL":
                IndexRelation(indexes.AssociatesMaterialRelations, entityId, rawArguments, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCRELDEFINESBYTYPE":
                IndexRelation(indexes.DefinesByTypeRelations, entityId, rawArguments, relatingArgIndex: 5, relatedArgIndex: 4);
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

    private static void IndexRelation(
        List<FastStepRelationRecord> target,
        int relationId,
        string rawArguments,
        int relatingArgIndex,
        int relatedArgIndex)
    {
        var rawArgumentsSpan = rawArguments.AsSpan();
        if (!StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, relatingArgIndex, out var relatingStart, out var relatingLength)
            || !StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, relatedArgIndex, out var relatedStart, out var relatedLength))
        {
            return;
        }

        var relatingId = StepParsingUtilities.ParseStepReference(rawArgumentsSpan.Slice(relatingStart, relatingLength));
        if (relatingId is null)
        {
            return;
        }

        var relatedIds = StepParsingUtilities.ParseStepReferenceList(rawArgumentsSpan.Slice(relatedStart, relatedLength));
        if (relatedIds.Count == 0)
        {
            return;
        }

        target.Add(new FastStepRelationRecord(relationId, relatingId.Value, relatedIds));
    }

    private static void IndexEntityIdentity(FastStepIndexes indexes, int entityId, string rawArguments)
    {
        var rawArgumentsSpan = rawArguments.AsSpan();

        if (StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, 0, out var globalStart, out var globalLength))
        {
            var globalId = StepParsingUtilities.ParseStepString(rawArgumentsSpan.Slice(globalStart, globalLength));
            if (!string.IsNullOrWhiteSpace(globalId))
            {
                indexes.EntityGlobalIds[entityId] = indexes.StringPool.Intern(globalId);
            }
        }

        if (StepParsingUtilities.TryGetTopLevelArgumentBounds(rawArgumentsSpan, 2, out var nameStart, out var nameLength))
        {
            var name = StepParsingUtilities.ParseStepString(rawArgumentsSpan.Slice(nameStart, nameLength));
            if (name is not null)
            {
                indexes.EntityNames[entityId] = indexes.StringPool.Intern(name);
            }
        }
    }
}

internal readonly record struct FastStepScanResult(FastStepIndexes Indexes, FastStepHeader Header, FastStepScanDiagnostics Diagnostics);
