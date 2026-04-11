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

        if (trimmed.Length < 2 || trimmed[0] != '\'' || trimmed[^1] != '\'')
        {
            return DecodeStepEscapes(trimmed.ToString());
        }

        var unescaped = trimmed[1..^1].ToString().Replace("''", "'", StringComparison.Ordinal);
        return DecodeStepEscapes(unescaped);
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
        var result = new List<int>();
        var argumentIndex = 0;

        while (TryGetTopLevelArgumentBounds(inner, argumentIndex, out var tokenStart, out var tokenLength))
        {
            var reference = ParseStepReference(inner.Slice(tokenStart, tokenLength));
            if (reference is not null)
            {
                result.Add(reference.Value);
            }

            argumentIndex++;
        }

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

    private static string DecodeStepEscapes(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf("\\X", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length;)
        {
            if (i + 3 < value.Length
                && value[i] == '\\'
                && (value[i + 1] == 'X' || value[i + 1] == 'x')
                && value[i + 2] == '2'
                && value[i + 3] == '\\')
            {
                var start = i + 4;
                var endMarker = value.IndexOf(@"\X0\", start, StringComparison.OrdinalIgnoreCase);
                if (endMarker > start)
                {
                    var hex = value[start..endMarker];
                    builder.Append(DecodeUtf16Hex(hex));
                    i = endMarker + 4;
                    continue;
                }
            }

            if (i + 4 < value.Length
                && value[i] == '\\'
                && (value[i + 1] == 'X' || value[i + 1] == 'x')
                && value[i + 2] == '\\')
            {
                var hexByte = value.Substring(i + 3, 2);
                if (byte.TryParse(hexByte, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)
                    && i + 5 < value.Length
                    && value[i + 5] == '\\')
                {
                    builder.Append((char)b);
                    i += 6;
                    continue;
                }
            }

            builder.Append(value[i]);
            i++;
        }

        return builder.ToString();
    }

    private static string DecodeUtf16Hex(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length % 4 != 0)
        {
            return hex;
        }

        var builder = new StringBuilder(hex.Length / 4);
        for (var i = 0; i < hex.Length; i += 4)
        {
            var chunk = hex.Substring(i, 4);
            if (!ushort.TryParse(chunk, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codeUnit))
            {
                return hex;
            }

            builder.Append((char)codeUnit);
        }

        return builder.ToString();
    }
}
