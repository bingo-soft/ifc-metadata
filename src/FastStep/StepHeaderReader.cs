using System;
using System.IO;
using System.Text;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal static class StepHeaderReader
{
    internal static FastStepHeader Read(FileInfo ifcSourceFile)
    {
        using var stream = ifcSourceFile.OpenRead();
        using var reader = new StreamReader(stream);
        return Read(reader);
    }

    internal static FastStepHeader Read(TextReader reader)
    {
        var content = ReadHeaderSection(reader);
        return ParseHeaderContent(content);
    }

    private static string ReadHeaderSection(TextReader reader)
    {
        var headerBuilder = new StringBuilder(1024);
        if (!ReadUntilMarker(reader, "HEADER;"))
        {
            return string.Empty;
        }

        headerBuilder.Append("HEADER;");
        ReadThroughMarker(reader, "ENDSEC;", headerBuilder);
        return headerBuilder.ToString();
    }

    private static bool ReadUntilMarker(TextReader reader, string marker)
    {
        var matched = 0;
        while (true)
        {
            var value = reader.Read();
            if (value < 0)
            {
                return false;
            }

            matched = AdvanceMarkerMatch((char)value, marker, matched);
            if (matched == marker.Length)
            {
                return true;
            }
        }
    }

    private static void ReadThroughMarker(TextReader reader, string marker, StringBuilder destination)
    {
        var matched = 0;
        var inString = false;

        while (true)
        {
            var value = reader.Read();
            if (value < 0)
            {
                return;
            }

            var ch = (char)value;
            destination.Append(ch);

            if (ch == '\'')
            {
                if (inString && reader.Peek() == '\'')
                {
                    destination.Append((char)reader.Read());
                    matched = 0;
                    continue;
                }

                inString = !inString;
                matched = 0;
                continue;
            }

            if (inString)
            {
                matched = 0;
                continue;
            }

            matched = AdvanceMarkerMatch(ch, marker, matched);
            if (matched == marker.Length)
            {
                return;
            }
        }
    }

    private static int AdvanceMarkerMatch(char ch, string marker, int matched)
    {
        if (char.ToUpperInvariant(ch) == marker[matched])
        {
            return matched + 1;
        }

        return char.ToUpperInvariant(ch) == marker[0] ? 1 : 0;
    }

    private static FastStepHeader ParseHeaderContent(string content)
    {
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

        if (fileSchemaArgs.Count <= 0)
        {
            return new FastStepHeader(author, createdAt, schema, creatingApplication);
        }

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
        var markerIndex = content.IndexOf(headerFunctionName, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return [];
        }

        var openParenIndex = markerIndex + headerFunctionName.Length;
        while (openParenIndex < content.Length && char.IsWhiteSpace(content[openParenIndex]))
        {
            openParenIndex++;
        }

        if (openParenIndex >= content.Length || content[openParenIndex] != '(')
        {
            return [];
        }

        var index = openParenIndex + 1;
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

        var argsLength = index - openParenIndex - 2;
        if (argsLength < 0)
        {
            return [];
        }

        var args = content.Substring(openParenIndex + 1, argsLength);
        return StepParsingUtilities.SplitTopLevelArguments(args);
    }
}

internal readonly record struct FastStepHeader(string Author, string CreatedAt, string Schema, string CreatingApplication);
