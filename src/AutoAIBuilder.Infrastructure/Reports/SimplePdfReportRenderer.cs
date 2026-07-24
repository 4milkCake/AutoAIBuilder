using System.Globalization;
using System.Text;

namespace AutoAIBuilder.Infrastructure.Reports;

public sealed class SimplePdfReportRenderer
{
    private const int MaximumCharactersPerLine = 94;
    private const int LinesPerPage = 58;

    public byte[] Render(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("O conteúdo do PDF é obrigatório.", nameof(content));
        }

        var lines = WrapLines(content).ToArray();
        var pageCount = Math.Max(1, (int)Math.Ceiling(lines.Length / (double)LinesPerPage));
        var objects = new SortedDictionary<int, byte[]>();
        var pageReferences = new List<string>();

        objects[1] = Ascii("<< /Type /Catalog /Pages 2 0 R >>");
        objects[3] = Ascii(
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var pageObjectId = 4 + pageIndex * 2;
            var contentObjectId = pageObjectId + 1;
            pageReferences.Add($"{pageObjectId} 0 R");

            objects[pageObjectId] = Ascii(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] "
                + $"/Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectId} 0 R >>");

            var pageLines = lines
                .Skip(pageIndex * LinesPerPage)
                .Take(LinesPerPage)
                .ToArray();
            var stream = BuildPageStream(pageLines, pageIndex + 1, pageCount);
            objects[contentObjectId] = Combine(
                Ascii($"<< /Length {stream.Length} >>\nstream\n"),
                stream,
                Ascii("\nendstream"));
        }

        objects[2] = Ascii(
            $"<< /Type /Pages /Kids [{string.Join(" ", pageReferences)}] /Count {pageCount} >>");

        return BuildDocument(objects);
    }

    private static byte[] BuildPageStream(
        IReadOnlyList<string> lines,
        int pageNumber,
        int pageCount)
    {
        var builder = new StringBuilder();
        var y = 798;

        foreach (var line in lines)
        {
            builder.Append("BT /F1 9 Tf 48 ");
            builder.Append(y.ToString(CultureInfo.InvariantCulture));
            builder.Append(" Td <");
            builder.Append(ToWinAnsiHex(line));
            builder.AppendLine("> Tj ET");
            y -= 12;
        }

        builder.Append("BT /F1 8 Tf 48 38 Td <");
        builder.Append(ToWinAnsiHex($"AutoAIBuilder - página {pageNumber} de {pageCount}"));
        builder.AppendLine("> Tj ET");
        return Ascii(builder.ToString());
    }

    private static byte[] BuildDocument(SortedDictionary<int, byte[]> objects)
    {
        using var stream = new MemoryStream();
        Write(stream, Ascii("%PDF-1.4\n%AutoAIBuilder\n"));

        var offsets = new Dictionary<int, long>();
        foreach (var (id, body) in objects)
        {
            offsets[id] = stream.Position;
            Write(stream, Ascii($"{id} 0 obj\n"));
            Write(stream, body);
            Write(stream, Ascii("\nendobj\n"));
        }

        var xrefOffset = stream.Position;
        var maximumObjectId = objects.Keys.Max();
        Write(stream, Ascii($"xref\n0 {maximumObjectId + 1}\n"));
        Write(stream, Ascii("0000000000 65535 f \n"));

        for (var id = 1; id <= maximumObjectId; id++)
        {
            var offset = offsets.TryGetValue(id, out var value) ? value : 0;
            var state = offsets.ContainsKey(id) ? "n" : "f";
            Write(stream, Ascii($"{offset:0000000000} 00000 {state} \n"));
        }

        Write(
            stream,
            Ascii(
                $"trailer\n<< /Size {maximumObjectId + 1} /Root 1 0 R >>\n"
                + $"startxref\n{xrefOffset}\n%%EOF"));
        return stream.ToArray();
    }

    private static IEnumerable<string> WrapLines(string content)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        foreach (var sourceLine in normalized.Split('\n'))
        {
            if (sourceLine.Length == 0)
            {
                yield return string.Empty;
                continue;
            }

            var remaining = sourceLine;
            while (remaining.Length > MaximumCharactersPerLine)
            {
                var breakAt = remaining.LastIndexOf(
                    ' ',
                    MaximumCharactersPerLine,
                    MaximumCharactersPerLine);
                if (breakAt < 1)
                {
                    breakAt = MaximumCharactersPerLine;
                }

                yield return remaining[..breakAt].TrimEnd();
                remaining = remaining[breakAt..].TrimStart();
            }

            yield return remaining;
        }
    }

    private static string ToWinAnsiHex(string value)
    {
        var builder = new StringBuilder(value.Length * 2);

        foreach (var character in value)
        {
            var normalized = character switch
            {
                '—' or '–' => '-',
                '“' or '”' => '"',
                '‘' or '’' => '\'',
                '•' => '-',
                '✓' => 'V',
                _ => character
            };
            var code = normalized <= byte.MaxValue ? (byte)normalized : (byte)'?';
            builder.Append(code.ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);

    private static byte[] Combine(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;

        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }

    private static void Write(Stream stream, byte[] bytes) =>
        stream.Write(bytes, 0, bytes.Length);
}
