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
        using var stream = ifcSourceFile.OpenRead();
        using var reader = new StreamReader(stream);
        return Scan(reader);
    }

    internal static FastStepIndexes Scan(TextReader reader)
    {
        var indexes = new FastStepIndexes();
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
                foreach (var entity in StepLexer.EnumerateEntities(reader))
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
            await foreach (var entity in entitiesChannel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                IndexEntity(indexes, entity);
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

        return indexes;
    }

    private static void IndexEntity(FastStepIndexes indexes, StepEntityToken entity)
    {
        var args = StepParsingUtilities.SplitTopLevelArguments(entity.RawArguments);
        var pooledEntityType = indexes.StringPool.Intern(entity.EntityType);

        indexes.Entities[entity.EntityId] = new FastStepEntityRecord(entity.EntityId, pooledEntityType, entity.RawArguments);
        indexes.EntityRanges[entity.EntityId] = new FastStepEntityRange(
            entity.StatementStartOffset,
            entity.StatementEndOffset,
            entity.ArgumentsStartOffset,
            entity.ArgumentsEndOffset);
        IndexEntityIdentity(indexes, entity.EntityId, args);
        IndexKnownEntity(indexes, entity.EntityId, pooledEntityType, args);
    }

    private static void IndexKnownEntity(FastStepIndexes indexes, int entityId, string entityType, List<string> args)
    {

        switch (entityType.ToUpperInvariant())
        {
            case "IFCPROJECT":
                IndexProject(indexes, entityId, args);
                break;
            case "IFCRELAGGREGATES":
                IndexRelation(indexes.DecompositionRelations, entityId, args, relatingArgIndex: 4, relatedArgIndex: 5);
                break;
            case "IFCRELCONTAINEDINSPATIALSTRUCTURE":
                IndexRelation(indexes.ContainmentRelations, entityId, args, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCRELDEFINESBYPROPERTIES":
                IndexRelation(indexes.DefinesByPropertiesRelations, entityId, args, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCRELASSOCIATESMATERIAL":
                IndexRelation(indexes.AssociatesMaterialRelations, entityId, args, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCRELDEFINESBYTYPE":
                IndexRelation(indexes.DefinesByTypeRelations, entityId, args, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCPROPERTYSET":
                IndexPropertySet(indexes, entityId, args);
                break;
        }
    }

    private static void IndexProject(FastStepIndexes indexes, int entityId, IReadOnlyList<string> args)
    {
        var globalId = args.Count > 0 ? StepParsingUtilities.ParseStepString(args[0]) : null;
        var name = args.Count > 2 ? StepParsingUtilities.ParseStepString(args[2]) : null;

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

    private static void IndexPropertySet(FastStepIndexes indexes, int entityId, IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return;
        }

        var globalId = StepParsingUtilities.ParseStepString(args[0]);
        if (string.IsNullOrEmpty(globalId))
        {
            return;
        }

        indexes.PropertySetGlobalIds[entityId] = indexes.StringPool.Intern(globalId);
    }

    private static void IndexRelation(
        List<FastStepRelationRecord> target,
        int relationId,
        List<string> args,
        int relatingArgIndex,
        int relatedArgIndex)
    {
        if (args.Count <= Math.Max(relatingArgIndex, relatedArgIndex))
        {
            return;
        }

        var relatingId = StepParsingUtilities.ParseStepReference(args[relatingArgIndex]);
        if (relatingId is null)
        {
            return;
        }

        var relatedIds = StepParsingUtilities.ParseStepReferenceList(args[relatedArgIndex]);
        if (relatedIds.Count == 0)
        {
            return;
        }

        target.Add(new FastStepRelationRecord(relationId, relatingId.Value, relatedIds));
    }

    private static void IndexEntityIdentity(FastStepIndexes indexes, int entityId, IReadOnlyList<string> args)
    {
        if (args.Count > 0)
        {
            var globalId = StepParsingUtilities.ParseStepString(args[0]);
            if (!string.IsNullOrWhiteSpace(globalId))
            {
                indexes.EntityGlobalIds[entityId] = indexes.StringPool.Intern(globalId);
            }
        }

        if (args.Count > 2)
        {
            var name = StepParsingUtilities.ParseStepString(args[2]);
            if (name is not null)
            {
                indexes.EntityNames[entityId] = indexes.StringPool.Intern(name);
            }
        }
    }
}
