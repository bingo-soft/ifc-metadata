using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal static class StepLexer
{
    private const int DefaultBufferSize = 128 * 1024;

    internal static IEnumerable<StepEntityToken> EnumerateEntities(TextReader reader)
    {
        return EnumerateEntities(reader, captureRawArguments: true);
    }

    internal static IEnumerable<StepEntityToken> EnumerateEntities(TextReader reader, bool captureRawArguments)
    {
        var buffer = ArrayPool<char>.Shared.Rent(DefaultBufferSize);
        var state = LexerState.OutsideEntity;
        var typeBuilder = new StringBuilder(64);
        var argsBuilder = new StringBuilder(256);

        var currentEntityId = 0;
        var statementStartOffset = 0;
        var argumentsStartOffset = 0;
        var argumentsEndOffset = 0;
        var depth = 0;
        var inString = false;
        var idDigits = 0;
        var currentOffset = 0;

        try
        {
            while (true)
            {
                var read = reader.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    yield break;
                }

                for (var i = 0; i < read; i++)
                {
                    var ch = buffer[i];
                    var reprocessCurrentCharacter = true;

                    while (reprocessCurrentCharacter)
                    {
                        reprocessCurrentCharacter = false;

                        switch (state)
                        {
                            case LexerState.OutsideEntity:
                                if (ch == '#')
                                {
                                    BeginEntityCandidate(
                                        currentOffset,
                                        ref state,
                                        ref currentEntityId,
                                        ref idDigits,
                                        ref depth,
                                        ref inString,
                                        ref statementStartOffset,
                                        ref argumentsStartOffset,
                                        ref argumentsEndOffset,
                                        typeBuilder,
                                        argsBuilder);
                                }

                                break;

                            case LexerState.ReadingEntityId:
                                if (char.IsDigit(ch))
                                {
                                    currentEntityId = (currentEntityId * 10) + (ch - '0');
                                    idDigits++;
                                    break;
                                }

                                if (ch == '=' && idDigits > 0)
                                {
                                    state = LexerState.BeforeEntityType;
                                    break;
                                }

                                ResetEntityCandidate(ref state, typeBuilder, argsBuilder);
                                if (ch == '#')
                                {
                                    reprocessCurrentCharacter = true;
                                }

                                break;

                            case LexerState.BeforeEntityType:
                                if (char.IsWhiteSpace(ch))
                                {
                                    break;
                                }

                                if (IsEntityTypeCharacter(ch))
                                {
                                    typeBuilder.Append(ch);
                                    state = LexerState.ReadingEntityType;
                                    break;
                                }

                                ResetEntityCandidate(ref state, typeBuilder, argsBuilder);
                                if (ch == '#')
                                {
                                    reprocessCurrentCharacter = true;
                                }

                                break;

                            case LexerState.ReadingEntityType:
                                if (IsEntityTypeCharacter(ch))
                                {
                                    typeBuilder.Append(ch);
                                    break;
                                }

                                if (char.IsWhiteSpace(ch))
                                {
                                    state = LexerState.BeforeArguments;
                                    break;
                                }

                                if (ch == '(')
                                {
                                    state = LexerState.ReadingArguments;
                                    argumentsStartOffset = currentOffset + 1;
                                    depth = 1;
                                    inString = false;
                                    break;
                                }

                                ResetEntityCandidate(ref state, typeBuilder, argsBuilder);
                                if (ch == '#')
                                {
                                    reprocessCurrentCharacter = true;
                                }

                                break;

                            case LexerState.BeforeArguments:
                                if (char.IsWhiteSpace(ch))
                                {
                                    break;
                                }

                                if (ch == '(')
                                {
                                    state = LexerState.ReadingArguments;
                                    argumentsStartOffset = currentOffset + 1;
                                    depth = 1;
                                    inString = false;
                                    break;
                                }

                                ResetEntityCandidate(ref state, typeBuilder, argsBuilder);
                                if (ch == '#')
                                {
                                    reprocessCurrentCharacter = true;
                                }

                                break;

                            case LexerState.ReadingArguments:
                                if (ch == '\'')
                                {
                                    inString = !inString;
                                }
                                else if (!inString)
                                {
                                    switch (ch)
                                    {
                                        case '(':
                                            depth++;
                                            break;
                                        case ')':
                                            depth--;
                                            break;
                                    }
                                }

                                if (depth == 0)
                                {
                                    argumentsEndOffset = currentOffset;
                                    state = LexerState.AfterArguments;
                                    break;
                                }

                                argsBuilder.Append(ch);

                                break;

                            case LexerState.AfterArguments:
                                if (char.IsWhiteSpace(ch))
                                {
                                    break;
                                }

                                if (ch == ';')
                                {
                                    var entityWithSemicolon = CreateToken(currentOffset + 1, statementStartOffset, argumentsStartOffset, argumentsEndOffset, currentEntityId, typeBuilder, argsBuilder, captureRawArguments);
                                    ResetEntityCandidate(ref state, typeBuilder, argsBuilder);
                                    yield return entityWithSemicolon;
                                    break;
                                }

                                var entityWithoutSemicolon = CreateToken(currentOffset, statementStartOffset, argumentsStartOffset, argumentsEndOffset, currentEntityId, typeBuilder, argsBuilder, captureRawArguments);
                                ResetEntityCandidate(ref state, typeBuilder, argsBuilder);
                                yield return entityWithoutSemicolon;
                                reprocessCurrentCharacter = true;
                                break;
                        }
                    }

                    currentOffset++;
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer, clearArray: false);
        }
    }

    internal static List<StepEntityToken> ReadEntities(TextReader reader)
    {
        return [.. EnumerateEntities(reader)];
    }

    private static void BeginEntityCandidate(
        int currentOffset,
        ref LexerState state,
        ref int currentEntityId,
        ref int idDigits,
        ref int depth,
        ref bool inString,
        ref int statementStartOffset,
        ref int argumentsStartOffset,
        ref int argumentsEndOffset,
        StringBuilder typeBuilder,
        StringBuilder argsBuilder)
    {
        state = LexerState.ReadingEntityId;
        currentEntityId = 0;
        idDigits = 0;
        depth = 0;
        inString = false;
        statementStartOffset = currentOffset;
        argumentsStartOffset = 0;
        argumentsEndOffset = 0;
        typeBuilder.Clear();
        argsBuilder.Clear();
    }

    private static void ResetEntityCandidate(ref LexerState state, StringBuilder typeBuilder, StringBuilder argsBuilder)
    {
        state = LexerState.OutsideEntity;
        typeBuilder.Clear();
        argsBuilder.Clear();
    }

    private static bool IsEntityTypeCharacter(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch == '_';
    }

    private static StepEntityToken CreateToken(
        int statementEndOffset,
        int statementStartOffset,
        int argumentsStartOffset,
        int argumentsEndOffset,
        int entityId,
        StringBuilder typeBuilder,
        StringBuilder argsBuilder,
        bool captureRawArguments)
    {
        var entityType = typeBuilder.Length == 0 ? string.Empty : typeBuilder.ToString();
        var rawArguments = argsBuilder.Length == 0 ? string.Empty : argsBuilder.ToString();

        return new StepEntityToken(
            entityId,
            entityType,
            rawArguments,
            statementStartOffset,
            statementEndOffset,
            argumentsStartOffset,
            argumentsEndOffset);
    }

    private enum LexerState
    {
        OutsideEntity,
        ReadingEntityId,
        BeforeEntityType,
        ReadingEntityType,
        BeforeArguments,
        ReadingArguments,
        AfterArguments,
    }
}

internal readonly record struct StepEntityToken(
    int EntityId,
    string EntityType,
    string RawArguments,
    int StatementStartOffset,
    int StatementEndOffset,
    int ArgumentsStartOffset,
    int ArgumentsEndOffset);
