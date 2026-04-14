using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Bingosoft.Net.IfcMetadata.FastStep;

internal static class StepParsingUtilities
{
    internal static List<string> SplitTopLevelArguments(string arguments)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return result;
        }

        var depth = 0;
        var inString = false;
        var tokenStart = 0;

        for (var i = 0; i < arguments.Length; i++)
        {
            var ch = arguments[i];
            switch (ch)
            {
                case '\'':
                    if (inString && i + 1 < arguments.Length && arguments[i + 1] == '\'')
                    {
                        i++;
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
                case ',' when !inString && depth == 0:
                    result.Add(arguments[tokenStart..i].Trim());
                    tokenStart = i + 1;
                    break;
            }
        }

        if (tokenStart <= arguments.Length)
        {
            result.Add(arguments[tokenStart..].Trim());
        }

        return result;
    }

    internal static bool TryGetTopLevelArgument(string arguments, int argumentIndex, out string token)
    {
        token = null;

        if (!TryGetTopLevelArgumentBounds(arguments.AsSpan(), argumentIndex, out var tokenStart, out var tokenLength))
        {
            return false;
        }

        token = arguments.Substring(tokenStart, tokenLength);
        return true;
    }

    internal static bool TryGetTopLevelArgumentBounds(ReadOnlySpan<char> arguments, int argumentIndex, out int tokenStart, out int tokenLength)
    {
        tokenStart = 0;
        tokenLength = 0;

        if (argumentIndex < 0)
        {
            return false;
        }

        arguments = TrimSpan(arguments);
        if (arguments.IsEmpty)
        {
            return false;
        }

        var depth = 0;
        var inString = false;
        var currentTokenStart = 0;
        var currentArgumentIndex = 0;

        for (var i = 0; i < arguments.Length; i++)
        {
            var ch = arguments[i];
            switch (ch)
            {
                case '\'':
                    if (inString && i + 1 < arguments.Length && arguments[i + 1] == '\'')
                    {
                        i++;
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
                case ',' when !inString && depth == 0:
                    if (currentArgumentIndex == argumentIndex)
                    {
                        return TryBuildTrimmedTokenBounds(arguments, currentTokenStart, i, out tokenStart, out tokenLength);
                    }

                    currentArgumentIndex++;
                    currentTokenStart = i + 1;
                    break;
            }
        }

        if (currentArgumentIndex != argumentIndex || currentTokenStart > arguments.Length)
        {
            return false;
        }

        return TryBuildTrimmedTokenBounds(arguments, currentTokenStart, arguments.Length, out tokenStart, out tokenLength);
    }

    internal static string ParseStepString(string token)
    {
        return string.IsNullOrWhiteSpace(token)
            ? null
            : ParseStepString(token.AsSpan());
    }

    internal static string ParseStepString(ReadOnlySpan<char> token)
    {
        var trimmed = TrimSpan(token);
        if (trimmed.IsEmpty)
        {
            return null;
        }

        if (trimmed.SequenceEqual("$") || trimmed.SequenceEqual("*"))
        {
            return null;
        }

        var isQuoted = trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'';
        var content = isQuoted ? trimmed[1..^1] : trimmed;

        if (isQuoted)
        {
            if (content.IndexOfAny('\\', '\'') < 0)
            {
                return content.ToString();
            }

            return DecodeStepEscapes(content, decodeDoubledQuotes: true);
        }

        return content.IndexOf('\\') < 0
            ? content.ToString()
            : DecodeStepEscapes(content, decodeDoubledQuotes: false);
    }

    internal static int? ParseStepReference(string token)
    {
        return string.IsNullOrWhiteSpace(token)
            ? null
            : ParseStepReference(token.AsSpan());
    }

    internal static int? ParseStepReference(ReadOnlySpan<char> token)
    {
        var trimmed = TrimSpan(token);
        if (trimmed.Length < 2 || trimmed[0] != '#')
        {
            return null;
        }

        return int.TryParse(trimmed[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }

    internal static List<int> ParseStepReferenceList(string token)
    {
        return string.IsNullOrWhiteSpace(token)
            ? []
            : ParseStepReferenceList(token.AsSpan());
    }

    internal static List<int> ParseStepReferenceList(ReadOnlySpan<char> token)
    {
        var trimmed = TrimSpan(token);
        if (trimmed.Length < 2 || trimmed[0] != '(' || trimmed[^1] != ')')
        {
            return [];
        }

        var inner = trimmed[1..^1];
        if (inner.IsEmpty)
        {
            return [];
        }

        var result = new List<int>();
        var depth = 0;
        var inString = false;
        var tokenStart = 0;

        for (var i = 0; i < inner.Length; i++)
        {
            var ch = inner[i];
            switch (ch)
            {
                case '\'':
                    if (inString && i + 1 < inner.Length && inner[i + 1] == '\'')
                    {
                        i++;
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
                case ',' when !inString && depth == 0:
                    AppendReferenceToken(inner, tokenStart, i, result);
                    tokenStart = i + 1;
                    break;
            }
        }

        AppendReferenceToken(inner, tokenStart, inner.Length, result);
        return result;
    }

    internal static List<string> ParseStepStringList(string token)
    {
        var trimmed = token.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '(' || trimmed[^1] != ')')
        {
            return [];
        }

        var inner = trimmed[1..^1];
        var parts = SplitTopLevelArguments(inner);
        var result = new List<string>(parts.Count);

        foreach (var part in parts)
        {
            var value = ParseStepString(part);
            if (!string.IsNullOrEmpty(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static void AppendReferenceToken(ReadOnlySpan<char> source, int start, int exclusiveEnd, List<int> result)
    {
        if (!TryBuildTrimmedTokenBounds(source, start, exclusiveEnd, out var tokenStart, out var tokenLength) || tokenLength == 0)
        {
            return;
        }

        var reference = ParseStepReference(source.Slice(tokenStart, tokenLength));
        if (reference is not null)
        {
            result.Add(reference.Value);
        }
    }

    private static ReadOnlySpan<char> TrimSpan(ReadOnlySpan<char> value)
    {
        var start = 0;
        var end = value.Length - 1;

        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return start > end ? ReadOnlySpan<char>.Empty : value[start..(end + 1)];
    }

    private static bool TryBuildTrimmedTokenBounds(
        ReadOnlySpan<char> arguments,
        int start,
        int exclusiveEnd,
        out int tokenStart,
        out int tokenLength)
    {
        tokenStart = start;
        tokenLength = 0;

        while (tokenStart < exclusiveEnd && char.IsWhiteSpace(arguments[tokenStart]))
        {
            tokenStart++;
        }

        var trimmedEnd = exclusiveEnd - 1;
        while (trimmedEnd >= tokenStart && char.IsWhiteSpace(arguments[trimmedEnd]))
        {
            trimmedEnd--;
        }

        if (trimmedEnd < tokenStart)
        {
            tokenStart = 0;
            tokenLength = 0;
            return true;
        }

        tokenLength = trimmedEnd - tokenStart + 1;
        return true;
    }

    private static string DecodeStepEscapes(ReadOnlySpan<char> value, bool decodeDoubledQuotes)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        StringBuilder builder = null;

        for (var i = 0; i < value.Length;)
        {
            if (decodeDoubledQuotes
                && i + 1 < value.Length
                && value[i] == '\''
                && value[i + 1] == '\'')
            {
                EnsureBuilder(ref builder, value, i);
                builder.Append('\'');
                i += 2;
                continue;
            }

            if (i + 3 < value.Length
                && value[i] == '\\'
                && IsX(value[i + 1])
                && value[i + 2] == '2'
                && value[i + 3] == '\\')
            {
                var utf16Start = i + 4;
                if (TryFindUtf16EndMarker(value, utf16Start, out var utf16End) && utf16End > utf16Start)
                {
                    EnsureBuilder(ref builder, value, i);

                    var hexChunk = value.Slice(utf16Start, utf16End - utf16Start);
                    if (!TryAppendUtf16Hex(builder, hexChunk))
                    {
                        builder.Append(hexChunk);
                    }

                    i = utf16End + 4;
                    continue;
                }
            }

            if (i + 5 < value.Length
                && value[i] == '\\'
                && IsX(value[i + 1])
                && value[i + 2] == '\\'
                && value[i + 5] == '\\'
                && TryParseHexByte(value[i + 3], value[i + 4], out var escapedByte))
            {
                EnsureBuilder(ref builder, value, i);
                builder.Append((char)escapedByte);
                i += 6;
                continue;
            }

            if (builder is not null)
            {
                builder.Append(value[i]);
            }

            i++;
        }

        return builder is null ? value.ToString() : builder.ToString();
    }

    private static bool TryFindUtf16EndMarker(ReadOnlySpan<char> value, int startIndex, out int markerIndex)
    {
        for (var i = startIndex; i + 3 < value.Length; i++)
        {
            if (value[i] == '\\'
                && IsX(value[i + 1])
                && value[i + 2] == '0'
                && value[i + 3] == '\\')
            {
                markerIndex = i;
                return true;
            }
        }

        markerIndex = -1;
        return false;
    }

    private static bool TryAppendUtf16Hex(StringBuilder builder, ReadOnlySpan<char> hex)
    {
        if (hex.IsEmpty || hex.Length % 4 != 0)
        {
            return false;
        }

        for (var i = 0; i < hex.Length; i += 4)
        {
            if (!TryParseHexWord(hex.Slice(i, 4), out var codeUnit))
            {
                return false;
            }

            builder.Append((char)codeUnit);
        }

        return true;
    }

    private static bool TryParseHexByte(char high, char low, out byte value)
    {
        if (TryParseHexNibble(high, out var highNibble) && TryParseHexNibble(low, out var lowNibble))
        {
            value = (byte)((highNibble << 4) | lowNibble);
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryParseHexWord(ReadOnlySpan<char> chunk, out ushort value)
    {
        if (chunk.Length != 4)
        {
            value = 0;
            return false;
        }

        if (TryParseHexNibble(chunk[0], out var n0)
            && TryParseHexNibble(chunk[1], out var n1)
            && TryParseHexNibble(chunk[2], out var n2)
            && TryParseHexNibble(chunk[3], out var n3))
        {
            value = (ushort)((n0 << 12) | (n1 << 8) | (n2 << 4) | n3);
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryParseHexNibble(char value, out int nibble)
    {
        nibble = value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'a' and <= 'f' => value - 'a' + 10,
            >= 'A' and <= 'F' => value - 'A' + 10,
            _ => -1,
        };

        return nibble >= 0;
    }

    private static bool IsX(char value)
    {
        return value is 'X' or 'x';
    }

    private static void EnsureBuilder(ref StringBuilder builder, ReadOnlySpan<char> source, int copyLength)
    {
        if (builder is not null)
        {
            return;
        }

        builder = new StringBuilder(source.Length);
        if (copyLength > 0)
        {
            builder.Append(source[..copyLength]);
        }
    }
}

