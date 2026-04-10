using System;
using System.Collections.Generic;
using System.Text.Json;

using Xbim.Ifc4.Interfaces;

namespace Bingosoft.Net.IfcMetadata;

internal readonly struct MetaRow
{
    internal MetaRow(
        int idIdx,
        int nameIdx,
        int typeIdx,
        int parentIdIdx,
        int materialIdIdx,
        int typeIdIdx,
        int propertiesStart,
        int propertiesCount)
    {
        IdIdx = idIdx;
        NameIdx = nameIdx;
        TypeIdx = typeIdx;
        ParentIdIdx = parentIdIdx;
        MaterialIdIdx = materialIdIdx;
        TypeIdIdx = typeIdIdx;
        PropertiesStart = propertiesStart;
        PropertiesCount = propertiesCount;
    }

    internal int IdIdx { get; }

    internal int NameIdx { get; }

    internal int TypeIdx { get; }

    internal int ParentIdIdx { get; }

    internal int MaterialIdIdx { get; }

    internal int TypeIdIdx { get; }

    internal int PropertiesStart { get; }

    internal int PropertiesCount { get; }
}

internal sealed class IfcExportIr
{
    private readonly Dictionary<string, int> stringToIndex;
    private readonly List<string> strings;

    internal IfcExportIr(int metaRowCapacity)
    {
        stringToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        strings = new List<string>(metaRowCapacity * 4);
        Rows = new List<MetaRow>(metaRowCapacity);
        PropertyStringIndexes = new List<int>(metaRowCapacity);
    }

    internal List<MetaRow> Rows { get; }

    internal List<int> PropertyStringIndexes { get; }

    internal int InternRequired(string value)
    {
        if (stringToIndex.TryGetValue(value, out var existingIndex))
        {
            return existingIndex;
        }

        var index = strings.Count;
        strings.Add(value);
        stringToIndex.Add(value, index);
        return index;
    }

    internal int InternNullable(string value)
    {
        return value is null ? -1 : InternRequired(value);
    }

    internal string ResolveNullable(int stringIndex)
    {
        return stringIndex < 0 ? null : strings[stringIndex];
    }

    internal string ResolveRequired(int stringIndex)
    {
        return strings[stringIndex];
    }
}

internal static class IfcExportIrPipeline
{
    internal static void AppendMetaObject(IfcExportIr ir, IIfcObjectDefinition objectDefinition, string parentId, string objectId)
    {
        var propertiesStart = -1;
        var propertiesCount = 0;

        if (objectDefinition is not IIfcProject && objectDefinition is IIfcObject product)
        {
            foreach (var relation in product.IsDefinedBy)
            {
                var relatedPropertyDefinition = relation.RelatingPropertyDefinition;
                if (relatedPropertyDefinition is null)
                {
                    continue;
                }

                foreach (var propertyDefinition in relatedPropertyDefinition.PropertySetDefinitions)
                {
                    if (propertyDefinition is not IIfcPropertySet propertySet)
                    {
                        continue;
                    }

                    if (propertiesCount == 0)
                    {
                        propertiesStart = ir.PropertyStringIndexes.Count;
                    }

                    ir.PropertyStringIndexes.Add(ir.InternRequired(propertySet.GlobalId.Value.ToString()));
                    propertiesCount++;
                }
            }
        }

        var row = new MetaRow(
            ir.InternRequired(objectId),
            ir.InternNullable(objectDefinition.Name),
            ir.InternRequired(IfcAccessors.GetRuntimeTypeName(objectDefinition)),
            ir.InternNullable(parentId),
            ir.InternNullable(IfcAccessors.GetMaterialId(objectDefinition)),
            ir.InternNullable(IfcAccessors.GetTypedId(objectDefinition)),
            propertiesStart,
            propertiesCount);

        ir.Rows.Add(row);
    }

    internal static void WriteMetaObjects(Utf8JsonWriter writer, IfcExportIr ir)
    {
        var rows = ir.Rows;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var objectId = ir.ResolveRequired(row.IdIdx);
            var name = ir.ResolveNullable(row.NameIdx);
            var type = ir.ResolveRequired(row.TypeIdx);
            var parent = ir.ResolveNullable(row.ParentIdIdx);
            var materialId = ir.ResolveNullable(row.MaterialIdIdx);
            var typeId = ir.ResolveNullable(row.TypeIdIdx);

            writer.WriteStartObject(objectId);
            writer.WriteString("id", objectId);
            WriteNullableString(writer, "name", name);
            writer.WriteString("type", type);
            WriteNullableString(writer, "parent", parent);

            WriteProperties(writer, ir, row);
            WriteNullableString(writer, "material_id", materialId);
            WriteNullableString(writer, "type_id", typeId);

            writer.WriteEndObject();
        }
    }

    private static void WriteProperties(Utf8JsonWriter writer, IfcExportIr ir, in MetaRow row)
    {
        if (row.PropertiesStart < 0 || row.PropertiesCount == 0)
        {
            writer.WriteNull("properties");
            return;
        }

        writer.WriteStartArray("properties");
        var endExclusive = row.PropertiesStart + row.PropertiesCount;
        for (var i = row.PropertiesStart; i < endExclusive; i++)
        {
            writer.WriteStringValue(ir.ResolveRequired(ir.PropertyStringIndexes[i]));
        }

        writer.WriteEndArray();
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string propertyName, string value)
    {
        if (value is null)
        {
            writer.WriteNull(propertyName);
            return;
        }

        writer.WriteString(propertyName, value);
    }
}