using System;
using System.Collections.Generic;
using System.IO;

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
        var entities = StepLexer.ReadEntities(reader);

        foreach (var entity in entities)
        {
            indexes.Entities[entity.EntityId] = new FastStepEntityRecord(entity.EntityId, entity.EntityType, entity.RawArguments);
            IndexEntityIdentity(indexes, entity);
            IndexKnownEntity(indexes, entity);
        }

        return indexes;
    }

    private static void IndexKnownEntity(FastStepIndexes indexes, StepEntityToken entity)
    {
        var args = StepParsingUtilities.SplitTopLevelArguments(entity.RawArguments);

        switch (entity.EntityType.ToUpperInvariant())
        {
            case "IFCPROJECT":
                IndexProject(indexes, entity.EntityId, args);
                break;
            case "IFCRELAGGREGATES":
                IndexRelation(indexes.DecompositionRelations, entity.EntityId, args, relatingArgIndex: 4, relatedArgIndex: 5);
                break;
            case "IFCRELCONTAINEDINSPATIALSTRUCTURE":
                IndexRelation(indexes.ContainmentRelations, entity.EntityId, args, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCRELDEFINESBYPROPERTIES":
                IndexRelation(indexes.DefinesByPropertiesRelations, entity.EntityId, args, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCRELASSOCIATESMATERIAL":
                IndexRelation(indexes.AssociatesMaterialRelations, entity.EntityId, args, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCRELDEFINESBYTYPE":
                IndexRelation(indexes.DefinesByTypeRelations, entity.EntityId, args, relatingArgIndex: 5, relatedArgIndex: 4);
                break;
            case "IFCPROPERTYSET":
                IndexPropertySet(indexes, entity.EntityId, args);
                break;
        }
    }

    private static void IndexProject(FastStepIndexes indexes, int entityId, IReadOnlyList<string> args)
    {
        var globalId = args.Count > 0 ? StepParsingUtilities.ParseStepString(args[0]) : null;
        var name = args.Count > 2 ? StepParsingUtilities.ParseStepString(args[2]) : null;
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

        indexes.PropertySetGlobalIds[entityId] = globalId;
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

    private static void IndexEntityIdentity(FastStepIndexes indexes, StepEntityToken entity)
    {
        var args = StepParsingUtilities.SplitTopLevelArguments(entity.RawArguments);

        if (args.Count > 0)
        {
            var globalId = StepParsingUtilities.ParseStepString(args[0]);
            if (!string.IsNullOrWhiteSpace(globalId))
            {
                indexes.EntityGlobalIds[entity.EntityId] = globalId;
            }
        }

        if (args.Count > 2)
        {
            var name = StepParsingUtilities.ParseStepString(args[2]);
            if (name is not null)
            {
                indexes.EntityNames[entity.EntityId] = name;
            }
        }
    }
}
