using System.Globalization;
using AutoAIBuilder.Application.CadVisualization;

namespace AutoAIBuilder.Infrastructure.CadVisualization;

public sealed class CadGeometryArtifactParser
{
    public ParsedCadGeometry Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Informe o artefato gráfico.",
                nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "O artefato gráfico não foi encontrado.",
                path);
        }

        CadDrawingBounds? bounds = null;
        var layers = new Dictionary<string, CadLayerInfo>(
            StringComparer.OrdinalIgnoreCase);
        var primitives = new List<CadPrimitive>();
        CadVisualizationCoverage? coverage = null;
        var version = 0;
        var lineNumber = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var fields = rawLine.Split('|');
            var recordType = fields[0];
            try
            {
                switch (recordType)
                {
                    case "AIV":
                        if (fields.Length < 2
                            || !int.TryParse(
                                fields[1],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out version)
                            || version is < 1 or > 2)
                        {
                            throw new InvalidDataException(
                                "Versão de artefato gráfico não suportada.");
                        }
                        break;
                    case "BOUNDS":
                        RequireFieldCount(fields, 5);
                        bounds = new CadDrawingBounds(
                            Number(fields[1]),
                            Number(fields[2]),
                            Number(fields[3]),
                            Number(fields[4]));
                        break;
                    case "LAYER":
                        RequireFieldCount(fields, 4);
                        var layerName = Decode(fields[1]);
                        layers[layerName] = new CadLayerInfo(
                            layerName,
                            Integer(fields[2]),
                            fields[3] == "1");
                        break;
                    case "LINE":
                        RequireFieldCount(fields, 7);
                        primitives.Add(new CadPrimitive(
                            CadPrimitiveKind.Line,
                            Decode(fields[1]),
                            Decode(fields[2]),
                            [
                                new CadPoint2D(
                                    Number(fields[3]),
                                    Number(fields[4])),
                                new CadPoint2D(
                                    Number(fields[5]),
                                    Number(fields[6]))
                            ]));
                        break;
                    case "POLYLINE":
                        RequireFieldCount(fields, 5);
                        var points = ParsePoints(fields[4]);
                        if (points.Count >= 2)
                        {
                            if (fields[3] == "1" && points[0] != points[^1])
                            {
                                points.Add(points[0]);
                            }

                            primitives.Add(new CadPrimitive(
                                CadPrimitiveKind.Polyline,
                                Decode(fields[1]),
                                Decode(fields[2]),
                                points));
                        }
                        break;
                    case "CIRCLE":
                        RequireFieldCount(fields, 6);
                        primitives.Add(new CadPrimitive(
                            CadPrimitiveKind.Circle,
                            Decode(fields[1]),
                            Decode(fields[2]),
                            [new CadPoint2D(Number(fields[3]), Number(fields[4]))],
                            Number(fields[5])));
                        break;
                    case "ARC":
                        RequireFieldCount(fields, 8);
                        primitives.Add(new CadPrimitive(
                            CadPrimitiveKind.Arc,
                            Decode(fields[1]),
                            Decode(fields[2]),
                            [new CadPoint2D(Number(fields[3]), Number(fields[4]))],
                            Number(fields[5]),
                            Number(fields[6]),
                            Number(fields[7])));
                        break;
                    case "TEXT":
                        RequireFieldCount(fields, version >= 2 ? 12 : 8);
                        var attachment = version >= 2
                            ? TextAttachment(fields[8])
                            : CadTextAttachment.BaselineLeft;
                        primitives.Add(new CadPrimitive(
                            CadPrimitiveKind.Text,
                            Decode(fields[1]),
                            Decode(fields[2]),
                            [new CadPoint2D(Number(fields[3]), Number(fields[4]))],
                            Text: CadTextNormalizer.Normalize(
                                Decode(fields[7])),
                            TextHeight: Number(fields[5]),
                            RotationDegrees: Number(fields[6]),
                            TextAttachment: attachment,
                            TextWidth: version >= 2
                                ? Number(fields[9])
                                : 0,
                            TextStyle: version >= 2
                                ? Decode(fields[10])
                                : string.Empty,
                            SourceObjectType: version >= 2
                                ? Decode(fields[11])
                                : "TEXT"));
                        break;
                    case "BOX":
                        RequireFieldCount(fields, 7);
                        var minimum = new CadPoint2D(
                            Number(fields[3]),
                            Number(fields[4]));
                        var maximum = new CadPoint2D(
                            Number(fields[5]),
                            Number(fields[6]));
                        primitives.Add(new CadPrimitive(
                            CadPrimitiveKind.Bounds,
                            Decode(fields[1]),
                            Decode(fields[2]),
                            [
                                minimum,
                                new CadPoint2D(maximum.X, minimum.Y),
                                maximum,
                                new CadPoint2D(minimum.X, maximum.Y),
                                minimum
                            ]));
                        break;
                    case "COVERAGE":
                        RequireFieldCount(fields, 4);
                        coverage = new CadVisualizationCoverage(
                            Integer(fields[1]),
                            Integer(fields[2]),
                            Integer(fields[3]));
                        break;
                }
            }
            catch (Exception exception) when (
                exception is FormatException
                    or OverflowException
                    or InvalidDataException)
            {
                throw new InvalidDataException(
                    $"Artefato gráfico inválido na linha {lineNumber}: "
                    + exception.Message,
                    exception);
            }
        }

        if (bounds is null)
        {
            throw new InvalidDataException(
                "O artefato gráfico não contém os limites do desenho.");
        }

        if (primitives.Count == 0)
        {
            throw new InvalidDataException(
                "O AutoCAD não exportou nenhuma geometria visualizável.");
        }

        coverage ??= new CadVisualizationCoverage(
            primitives.Count,
            primitives.Count,
            0);
        return new ParsedCadGeometry(
            bounds,
            layers.Values
                .OrderBy(layer => layer.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            primitives,
            coverage);
    }

    private static List<CadPoint2D> ParsePoints(string value)
    {
        var points = new List<CadPoint2D>();
        foreach (var pair in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var coordinates = pair.Split(',');
            if (coordinates.Length != 2)
            {
                throw new InvalidDataException(
                    "Uma polilinha contém coordenadas incompletas.");
            }

            points.Add(new CadPoint2D(
                Number(coordinates[0]),
                Number(coordinates[1])));
        }

        return points;
    }

    private static double Number(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int Integer(string value) =>
        int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static string Decode(string value) => Uri.UnescapeDataString(value);

    private static CadTextAttachment TextAttachment(string value) =>
        Enum.TryParse<CadTextAttachment>(
            Decode(value),
            ignoreCase: true,
            out var attachment)
            ? attachment
            : CadTextAttachment.BaselineLeft;

    private static void RequireFieldCount(string[] fields, int minimum)
    {
        if (fields.Length < minimum)
        {
            throw new InvalidDataException(
                "Registro com quantidade insuficiente de campos.");
        }
    }
}

public sealed record ParsedCadGeometry(
    CadDrawingBounds Bounds,
    IReadOnlyList<CadLayerInfo> Layers,
    IReadOnlyList<CadPrimitive> Primitives,
    CadVisualizationCoverage Coverage);
