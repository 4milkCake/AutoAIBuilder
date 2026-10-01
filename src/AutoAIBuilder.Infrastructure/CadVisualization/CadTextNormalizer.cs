using System.Globalization;
using System.Text;

namespace AutoAIBuilder.Infrastructure.CadVisualization;

public static class CadTextNormalizer
{
    private static readonly HashSet<char> FormattingCommands =
    [
        'A', 'a',
        'C', 'c',
        'F', 'f',
        'H', 'h',
        'Q', 'q',
        'T', 't',
        'W', 'w',
        'P', 'p'
    ];

    private static readonly HashSet<char> ToggleCommands =
    [
        'K', 'k',
        'L', 'l',
        'O', 'o'
    ];

    public static string Normalize(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        var decoded = DecodeMText(source);
        decoded = DecodeAutoCadPercentCodes(decoded);
        return NormalizeWhitespace(decoded);
    }

    private static string DecodeMText(string source)
    {
        var result = new StringBuilder(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            if (current is '{' or '}')
            {
                continue;
            }

            if (current != '\\' || index + 1 >= source.Length)
            {
                AppendPrintable(result, current);
                continue;
            }

            var command = source[++index];
            switch (command)
            {
                case 'P':
                    AppendLineBreak(result);
                    break;
                case '~':
                    result.Append(' ');
                    break;
                case '\\':
                case '{':
                case '}':
                    result.Append(command);
                    break;
                case 'S':
                case 's':
                    var stack = ReadUntilSemicolon(source, ref index);
                    result.Append(NormalizeStack(stack));
                    break;
                case 'U':
                case 'u':
                    if (!TryAppendUnicodeEscape(
                            result,
                            source,
                            ref index))
                    {
                        result.Append(command);
                    }
                    break;
                default:
                    if (ToggleCommands.Contains(command))
                    {
                        break;
                    }

                    if (FormattingCommands.Contains(command))
                    {
                        _ = ReadUntilSemicolon(source, ref index);
                        break;
                    }

                    result.Append(command);
                    break;
            }
        }

        return result.ToString();
    }

    private static string ReadUntilSemicolon(
        string source,
        ref int index)
    {
        var start = index + 1;
        var end = source.IndexOf(';', start);
        if (end < 0)
        {
            index = source.Length - 1;
            return source[start..];
        }

        index = end;
        return source[start..end];
    }

    private static string NormalizeStack(string value)
    {
        var separatorIndex = value.IndexOfAny(['#', '/', '^']);
        if (separatorIndex < 0)
        {
            return value;
        }

        var numerator = value[..separatorIndex].Trim();
        var denominator = value[(separatorIndex + 1)..].Trim();
        return denominator.Length == 0
            ? numerator
            : $"{numerator}/{denominator}";
    }

    private static bool TryAppendUnicodeEscape(
        StringBuilder result,
        string source,
        ref int index)
    {
        if (index + 5 >= source.Length || source[index + 1] != '+')
        {
            return false;
        }

        var hexadecimal = source.Substring(index + 2, 4);
        if (!int.TryParse(
                hexadecimal,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var codePoint))
        {
            return false;
        }

        result.Append(char.ConvertFromUtf32(codePoint));
        index += 5;
        return true;
    }

    private static string DecodeAutoCadPercentCodes(string source)
    {
        var result = new StringBuilder(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] != '%'
                || index + 2 >= source.Length
                || source[index + 1] != '%')
            {
                AppendPrintable(result, source[index]);
                continue;
            }

            var code = char.ToUpperInvariant(source[index + 2]);
            switch (code)
            {
                case 'D':
                    result.Append('°');
                    index += 2;
                    break;
                case 'P':
                    result.Append('±');
                    index += 2;
                    break;
                case 'C':
                    result.Append('Ø');
                    index += 2;
                    break;
                case 'U':
                case 'O':
                    index += 2;
                    break;
                default:
                    result.Append('%');
                    break;
            }
        }

        return result.ToString();
    }

    private static string NormalizeWhitespace(string value)
    {
        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var result = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var normalized = NormalizeLine(line);
            if (normalized.Length == 0
                && (result.Count == 0 || result[^1].Length == 0))
            {
                continue;
            }

            result.Add(normalized);
        }

        while (result.Count > 0 && result[^1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return string.Join(Environment.NewLine, result);
    }

    private static string NormalizeLine(string value)
    {
        var result = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (char.IsControl(character))
            {
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(character);
        }

        return result.ToString().Trim();
    }

    private static void AppendLineBreak(StringBuilder result)
    {
        if (result.Length == 0 || result[^1] == '\n')
        {
            return;
        }

        result.Append('\n');
    }

    private static void AppendPrintable(
        StringBuilder result,
        char value)
    {
        if (!char.IsControl(value) || value is '\r' or '\n' or '\t')
        {
            result.Append(value);
        }
    }
}
