using System.Collections.Generic;
using System.IO;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal static class StepLexer
{
    internal static List<StepEntityToken> ReadEntities(TextReader reader)
    {
        var text = reader.ReadToEnd();
        var entities = new List<StepEntityToken>();
        var index = 0;

        while (TryReadNextEntity(text, ref index, out var entity))
        {
            entities.Add(entity);
        }

        return entities;
    }

    private static bool TryReadNextEntity(string text, ref int index, out StepEntityToken entity)
    {
        entity = default;

        while (true)
        {
            while (index < text.Length && text[index] != '#')
            {
                index++;
            }

            if (index >= text.Length)
            {
                return false;
            }

            var start = index;
            index++;

            var idStart = index;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                index++;
            }

            if (idStart == index || index >= text.Length || text[index] != '=' || !int.TryParse(text[idStart..index], out var entityId))
            {
                index = start + 1;
                continue;
            }

            index++;
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            var typeStart = index;

            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
            {
                index++;
            }

            if (typeStart == index)
            {
                index = start + 1;
                continue;
            }

            var entityType = text[typeStart..index];

            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index >= text.Length || text[index] != '(')
            {
                index = start + 1;
                continue;
            }

            var argsStart = index + 1;
            var depth = 1;
            var inString = false;

            index++;
            while (index < text.Length && depth > 0)
            {
                var ch = text[index];
                switch (ch)
                {
                    case '\'':
                        if (inString && index + 1 < text.Length && text[index + 1] == '\'')
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
                return false;
            }

            var argsLength = index - argsStart - 1;

            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index < text.Length && text[index] == ';')
            {
                index++;
            }

            var rawArguments = argsLength > 0
                ? text[argsStart..(argsStart + argsLength)]
                : string.Empty;

            entity = new StepEntityToken(entityId, entityType, rawArguments);
            return true;
        }
    }
}

internal readonly record struct StepEntityToken(int EntityId, string EntityType, string RawArguments);
