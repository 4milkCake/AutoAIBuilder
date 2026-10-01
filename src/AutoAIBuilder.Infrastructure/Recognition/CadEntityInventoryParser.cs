using System.Globalization;

namespace AutoAIBuilder.Infrastructure.Recognition;

public sealed record CadInventoryEntity(
    string Handle,
    string ObjectType,
    string Layer,
    string BlockName,
    double X,
    double Y,
    double Z,
    double RotationDegrees,
    double ScaleX,
    double ScaleY,
    double ScaleZ,
    string Text,
    string CoordinateSystem,
    string AnchorSource,
    double InsertionX,
    double InsertionY,
    double InsertionZ,
    CadInventoryBounds? Bounds,
    int ExpansionDepth = 0,
    string RootHandle = "",
    string StablePath = "",
    string RawBlockName = "",
    string GeometrySignature = "",
    int PrimitiveCount = 0,
    int NestedInsertCount = 0);

public sealed record CadInventoryBounds(
    double MinimumX,
    double MinimumY,
    double MinimumZ,
    double MaximumX,
    double MaximumY,
    double MaximumZ);

public sealed record CadEntityInventory(
    int EntityCount,
    int InsertCount,
    IReadOnlyList<CadInventoryEntity> Entities,
    int TopLevelEntityCount = 0,
    int ExpandedEntityCount = 0);

public sealed class CadEntityInventoryParser
{
    public CadEntityInventory Parse(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "O inventário CAD não foi encontrado.",
                path);
        }

        var entities = new List<CadInventoryEntity>();
        var entityCount = 0;
        var insertCount = 0;
        var topLevelEntityCount = 0;
        var expandedEntityCount = 0;
        var versionFound = false;
        var version = 0;
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split('|');
            try
            {
                switch (fields[0])
                {
                    case "AAR":
                        if (fields.Length < 2
                            || !int.TryParse(
                                fields[1],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out version)
                            || version is < 1 or > 3)
                        {
                            throw new InvalidDataException(
                                "Versão do inventário não suportada.");
                        }

                        versionFound = true;
                        break;
                    case "ENTITY":
                        if (fields.Length < 13)
                        {
                            throw new InvalidDataException(
                                "Registro ENTITY incompleto.");
                        }

                        var x = Number(fields[5]);
                        var y = Number(fields[6]);
                        var z = Number(fields[7]);
                        var hasSpatialContract = version >= 2;
                        if (hasSpatialContract && fields.Length < 25)
                        {
                            throw new InvalidDataException(
                                "Registro ENTITY v2 incompleto.");
                        }

                        CadInventoryBounds? bounds = null;
                        if (hasSpatialContract && fields[18] == "1")
                        {
                            bounds = new CadInventoryBounds(
                                Number(fields[19]),
                                Number(fields[20]),
                                Number(fields[21]),
                                Number(fields[22]),
                                Number(fields[23]),
                                Number(fields[24]));
                        }

                        var hasHierarchyContract = version >= 3;
                        if (hasHierarchyContract && fields.Length < 32)
                        {
                            throw new InvalidDataException(
                                "Registro ENTITY v3 incompleto.");
                        }

                        entities.Add(new CadInventoryEntity(
                            Decode(fields[1]),
                            Decode(fields[2]),
                            Decode(fields[3]),
                            Decode(fields[4]),
                            x,
                            y,
                            z,
                            Number(fields[8]),
                            Number(fields[9]),
                            Number(fields[10]),
                            Number(fields[11]),
                            Decode(fields[12]),
                            hasSpatialContract
                                ? Decode(fields[13])
                                : "LEGACY_UNSPECIFIED",
                            hasSpatialContract
                                ? Decode(fields[14])
                                : "LEGACY_ENTITY_POINT",
                            hasSpatialContract ? Number(fields[15]) : x,
                            hasSpatialContract ? Number(fields[16]) : y,
                            hasSpatialContract ? Number(fields[17]) : z,
                            bounds,
                            hasHierarchyContract ? Integer(fields[25]) : 0,
                            hasHierarchyContract ? Decode(fields[26]) : fields[1],
                            hasHierarchyContract ? Decode(fields[27]) : fields[1],
                            hasHierarchyContract ? Decode(fields[28]) : fields[4],
                            hasHierarchyContract ? Decode(fields[29]) : string.Empty,
                            hasHierarchyContract ? Integer(fields[30]) : 0,
                            hasHierarchyContract ? Integer(fields[31]) : 0));
                        break;
                    case "SUMMARY":
                        if (fields.Length < 3)
                        {
                            throw new InvalidDataException(
                                "Registro SUMMARY incompleto.");
                        }

                        entityCount = Integer(fields[1]);
                        insertCount = Integer(fields[2]);
                        topLevelEntityCount =
                            fields.Length >= 4 ? Integer(fields[3]) : 0;
                        expandedEntityCount =
                            fields.Length >= 5 ? Integer(fields[4]) : 0;
                        break;
                }
            }
            catch (Exception exception) when (
                exception is FormatException
                    or OverflowException
                    or InvalidDataException)
            {
                throw new InvalidDataException(
                    $"Inventário CAD inválido na linha {lineNumber}: "
                    + exception.Message,
                    exception);
            }
        }

        if (!versionFound)
        {
            throw new InvalidDataException(
                "O inventário CAD não possui cabeçalho AAR.");
        }

        if (entityCount == 0)
        {
            entityCount = entities.Count;
            insertCount = entities.Count(entity =>
                entity.ObjectType.Equals(
                    "INSERT",
                    StringComparison.OrdinalIgnoreCase));
        }

        if (topLevelEntityCount == 0)
        {
            topLevelEntityCount = entities.Count(entity =>
                entity.ExpansionDepth == 0);
            expandedEntityCount = entities.Count - topLevelEntityCount;
        }

        return new CadEntityInventory(
            entityCount,
            insertCount,
            entities,
            topLevelEntityCount,
            expandedEntityCount);
    }

    private static double Number(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int Integer(string value) =>
        int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static string Decode(string value) => Uri.UnescapeDataString(value);
}
