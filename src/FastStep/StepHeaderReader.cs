using System;
using System.IO;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal static class StepHeaderReader
{
    internal static FastStepHeader Read(FileInfo ifcSourceFile)
    {
        var content = File.ReadAllText(ifcSourceFile.FullName);

        var fileNameArgs = ReadHeaderArguments(content, "FILE_NAME");
        var fileSchemaArgs = ReadHeaderArguments(content, "FILE_SCHEMA");

        var author = string.Empty;
        var createdAt = string.Empty;
        var schema = string.Empty;
        string creatingApplication = null;

        if (fileNameArgs.Count > 0)
        {
            createdAt = fileNameArgs.Count > 1 ? StepParsingUtilities.ParseStepString(fileNameArgs[1]) ?? string.Empty : string.Empty;

            if (fileNameArgs.Count > 2)
            {
                var authors = StepParsingUtilities.ParseStepStringList(fileNameArgs[2]);
                author = authors.Count == 0 ? string.Empty : string.Join(';', authors);
            }

            if (fileNameArgs.Count > 5)
            {
                creatingApplication = StepParsingUtilities.ParseStepString(fileNameArgs[5]);
            }
        }

        if (fileSchemaArgs.Count <= 0) return new FastStepHeader(author, createdAt, schema, creatingApplication);
        var schemas = StepParsingUtilities.ParseStepStringList(fileSchemaArgs[0]);
        if (schemas.Count > 0)
        {
            schema = NormalizeSchema(schemas[0]);
        }

        return new FastStepHeader(author, createdAt, schema, creatingApplication);
    }

    private static string NormalizeSchema(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return schema;
        }

        return schema.StartsWith("IFC2X2", StringComparison.OrdinalIgnoreCase)
            ? "IFC2X3"
            : schema;
    }

    private static System.Collections.Generic.List<string> ReadHeaderArguments(string content, string headerFunctionName)
    {
        var marker = headerFunctionName + "(";
        var markerIndex = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return [];
        }

        var index = markerIndex + marker.Length;
        var depth = 1;
        var inString = false;

        while (index < content.Length && depth > 0)
        {
            var ch = content[index];
            switch (ch)
            {
                case '\'':
                    if (inString && index + 1 < content.Length && content[index + 1] == '\'')
                    {
                        index += 2;
                        continue;
                    }

                    inString = !inString;
                    break;
                case '(' when !inString:
                    depth++;
                    break;
                case ')' when !inString:
                    depth--;
                    break;
            }

            index++;
        }

        if (depth != 0)
        {
            return [];
        }

        var argsLength = index - (markerIndex + marker.Length) - 1;
        if (argsLength < 0)
        {
            return [];
        }

        var args = content.Substring(markerIndex + marker.Length, argsLength);
        return StepParsingUtilities.SplitTopLevelArguments(args);
    }
}

internal readonly record struct FastStepHeader(string Author, string CreatedAt, string Schema, string CreatingApplication);
