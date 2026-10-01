using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AutoAIBuilder.Domain.Recognition;
using AutoAIBuilder.Infrastructure.CadVisualization;

namespace AutoAIBuilder.Infrastructure.Recognition;

public sealed class CadLegendInterpreter
{
    private static readonly string[] TechnicalTerms =
    [
        "TOMADA",
        "INTERRUPTOR",
        "LUMINARIA",
        "ILUMINACAO",
        "LUZ",
        "LED",
        "SPOT",
        "PLAFON",
        "BALIZADOR",
        "ARANDELA",
        "PENDENTE",
        "PONTO",
        "INTERNET",
        "INTERFONE",
        "VIDEOFONE",
        "CAMPAINHA",
        "ANTENA",
        "TV",
        "SENSOR",
        "AUTOMACAO",
        "CORTINA",
        "SOM",
        "GAS",
        "QUADRO",
        "BOMBA",
        "HIDROMASSAGEM",
        "ACIONAMENTO",
        "SPLIT",
        "EMBUTIDO",
        "REFLETOR",
        "RALO",
        "TORNEIRA",
        "DUCHA",
        "CHUVEIRO",
        "BACIA",
        "SANITARIA",
        "ESGOTO",
        "AGUA",
        "LAVATORIO",
        "PIA",
        "PURIFICADOR",
        "GELADEIRA",
        "MAQUINA",
        "COIFA",
        "DEPURADOR"
    ];

    private static readonly string[] ExcludedPrefixes =
    [
        "NOTA",
        "ATENCAO",
        "OBSERVACAO",
        "OBS ",
        "TODOS ",
        "TODAS ",
        "PREVER ",
        "CASA ",
        "ESCALA "
    ];

    private static readonly string[] SectionHeadings =
    [
        "ILUMINACAO LED",
        "PONTOS ELETRICOS",
        "PONTOS HIDRAULICOS",
        "PONTOS DE ILUMINACAO",
        "SIMBOLOS ELETRICOS",
        "SIMBOLOS HIDRAULICOS"
    ];

    public RecognitionLegendAnalysis Analyze(CadEntityInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var texts = inventory.Entities
            .Where(IsText)
            .Select(entity => new TextEntity(
                entity,
                CadTextNormalizer.Normalize(entity.Text),
                Normalize(CadTextNormalizer.Normalize(entity.Text))))
            .Where(item => item.Normalized.Length > 0)
            .ToArray();
        var anchors = texts
            .Where(item => IsLegendHeading(item.Normalized))
            .ToArray();
        if (anchors.Length == 0)
        {
            return RecognitionLegendAnalysis.Empty;
        }

        var scale = ResolveDrawingScale(inventory.Entities);
        var textHeight = ResolveTypicalTextHeight(texts);
        var horizontalRadius = Math.Max(
            scale * 0.02d,
            Math.Min(textHeight * 30d, scale * 0.05d));
        var verticalMargin = Math.Max(
            scale * 0.015d,
            Math.Min(textHeight * 25d, scale * 0.04d));
        var pairRadius = Math.Max(
            scale * 0.01d,
            Math.Min(textHeight * 15d, scale * 0.03d));

        var minimumAnchorX = anchors.Min(anchor => anchor.Entity.X);
        var maximumAnchorX = anchors.Max(anchor => anchor.Entity.X);
        var minimumAnchorY = anchors.Min(anchor => anchor.Entity.Y);
        var maximumAnchorY = anchors.Max(anchor => anchor.Entity.Y);

        bool IsInLegendBand(CadInventoryEntity entity) =>
            entity.X >= minimumAnchorX - horizontalRadius
            && entity.X <= maximumAnchorX + horizontalRadius
            && entity.Y >= minimumAnchorY - verticalMargin
            && entity.Y <= maximumAnchorY + verticalMargin;

        var descriptions = texts
            .Where(item => !IsLegendHeading(item.Normalized))
            .Where(item => IsInLegendBand(item.Entity))
            .Where(item => LooksLikeLegendDescription(item.Normalized))
            .GroupBy(item => item.Entity.Handle, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var symbols = inventory.Entities
            .Where(entity => entity.ObjectType.Equals(
                "INSERT",
                StringComparison.OrdinalIgnoreCase))
            .Where(entity => !string.IsNullOrWhiteSpace(entity.BlockName))
            .Where(IsInLegendBand)
            .ToArray();

        var possiblePairs = (
            from description in descriptions
            from symbol in symbols
            let distance = Distance(description.Entity, symbol)
            where distance <= pairRadius
            where !HasExplicitSemanticConflict(
                symbol.BlockName,
                description.Normalized)
            let anchor = anchors
                .OrderBy(item => Distance(item.Entity, description.Entity))
                .First()
            orderby distance, symbol.ExpansionDepth descending
            select new PairCandidate(
                description,
                symbol,
                anchor,
                distance))
            .ToArray();

        var usedDescriptions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var usedSymbols = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var entries = new List<RecognitionLegendEntry>();
        foreach (var pair in possiblePairs)
        {
            if (!usedDescriptions.Add(pair.Description.Entity.Handle)
                || !usedSymbols.Add(pair.Symbol.Handle))
            {
                continue;
            }

            entries.Add(new RecognitionLegendEntry(
                pair.Symbol.Handle,
                pair.Description.Entity.Handle,
                pair.Symbol.BlockName,
                pair.Description.DisplayText,
                pair.Symbol.X,
                pair.Symbol.Y,
                pair.Description.Entity.X,
                pair.Description.Entity.Y,
                pair.Distance,
                $"Símbolo e descrição pareados a "
                + $"{pair.Distance.ToString("0.###", CultureInfo.InvariantCulture)} "
                + "unidades; referência espacial do título "
                + $"\"{pair.Anchor.DisplayText}\".",
                pair.Symbol.ExpansionDepth,
                pair.Symbol.RootHandle,
                pair.Symbol.StablePath,
                pair.Symbol.GeometrySignature,
                pair.Symbol.PrimitiveCount,
                pair.Symbol.NestedInsertCount));
        }

        var regionEntities = inventory.Entities
            .Where(IsInLegendBand)
            .ToArray();
        var looseEntries = BuildLooseGeometryEntries(
            inventory,
            descriptions,
            usedDescriptions,
            entries,
            regionEntities,
            pairRadius);
        entries.AddRange(looseEntries);
        foreach (var entry in looseEntries)
        {
            usedDescriptions.Add(entry.DescriptionHandle);
        }

        entries.AddRange(BuildGlyphEntries(
            inventory,
            descriptions,
            usedDescriptions,
            entries,
            regionEntities,
            pairRadius));
        var bounds = ResolveBounds(regionEntities, anchors);
        var orderedEntries = entries
            .OrderByDescending(entry => entry.SymbolY)
            .ThenBy(entry => entry.SymbolX)
            .ThenBy(entry => entry.Description)
            .ToArray();
        var status = orderedEntries.Length == 0
            ? "Título de legenda localizado, mas nenhum par símbolo–descrição "
              + "foi confirmado automaticamente."
            : $"{orderedEntries.Length} par(es) símbolo–descrição proposto(s) "
              + "pela legenda; confirmação visual pendente.";

        return new RecognitionLegendAnalysis(
            true,
            status,
            "Cabeçalhos LEGENDA/SIMBOLOGIA, faixa espacial adaptada à escala "
            + "do desenho e proximidade entre bloco INSERT e texto técnico.",
            anchors.Length,
            regionEntities.Length,
            descriptions.Length,
            Math.Max(0, descriptions.Length - orderedEntries.Length),
            bounds.MinimumX,
            bounds.MinimumY,
            bounds.MaximumX,
            bounds.MaximumY,
            orderedEntries);
    }

    private static bool IsText(CadInventoryEntity entity) =>
        entity.ObjectType.Equals("TEXT", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("MTEXT", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("ATTRIB", StringComparison.OrdinalIgnoreCase);

    private static bool IsLegendHeading(string normalized) =>
        normalized.Equals("LEGENDA", StringComparison.Ordinal)
        || normalized.StartsWith("LEGENDA ", StringComparison.Ordinal)
        || normalized.Contains(" SIMBOLOGIA ", StringComparison.Ordinal)
        || normalized.StartsWith("SIMBOLOGIA", StringComparison.Ordinal);

    private static bool LooksLikeLegendDescription(string normalized)
    {
        if (normalized.Length is < 3 or > 180
            || ExcludedPrefixes.Any(prefix =>
                normalized.StartsWith(prefix, StringComparison.Ordinal))
            || SectionHeadings.Contains(
                normalized,
                StringComparer.Ordinal))
        {
            return false;
        }

        return TechnicalTerms.Any(term => ContainsTerm(normalized, term));
    }

    private static bool HasExplicitSemanticConflict(
        string blockName,
        string normalizedDescription)
    {
        var normalizedBlock = Normalize(blockName);
        var explicitBlockMeaning = TechnicalTerms.FirstOrDefault(term =>
            normalizedBlock.Equals(term, StringComparison.Ordinal));
        return explicitBlockMeaning is not null
               && !ContainsTerm(
                   normalizedDescription,
                   explicitBlockMeaning);
    }

    private static bool ContainsTerm(string normalized, string term) =>
        normalized.Equals(term, StringComparison.Ordinal)
        || normalized.Contains($" {term} ", StringComparison.Ordinal)
        || normalized.StartsWith($"{term} ", StringComparison.Ordinal)
        || normalized.EndsWith($" {term}", StringComparison.Ordinal);

    private static IReadOnlyList<RecognitionLegendEntry>
        BuildLooseGeometryEntries(
            CadEntityInventory inventory,
            IReadOnlyList<TextEntity> descriptions,
            IReadOnlySet<string> usedDescriptions,
            IReadOnlyList<RecognitionLegendEntry> blockEntries,
            IReadOnlyList<CadInventoryEntity> regionEntities,
            double pairRadius)
    {
        if (blockEntries.Count == 0)
        {
            return [];
        }

        var offsets = blockEntries
            .Select(entry => entry.DescriptionX - entry.SymbolX)
            .Where(offset => offset > 0)
            .Order()
            .ToArray();
        var horizontalOffset = offsets.Length == 0
            ? pairRadius * 0.3d
            : Percentile(offsets, 0.5d);
        var entityByHandle = inventory.Entities
            .GroupBy(entity => entity.Handle, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
        var symbolSizes = blockEntries
            .Select(entry => entityByHandle.GetValueOrDefault(
                entry.SymbolHandle)?.Bounds)
            .Where(bounds => bounds is not null)
            .Select(bounds => Math.Max(
                bounds!.MaximumX - bounds.MinimumX,
                bounds.MaximumY - bounds.MinimumY))
            .Where(size => double.IsFinite(size) && size > 0)
            .Order()
            .ToArray();
        var symbolSize = symbolSizes.Length == 0
            ? horizontalOffset
            : Percentile(symbolSizes, 0.5d);
        var searchHalfWidth = Math.Max(
            Math.Max(horizontalOffset * 1.35d, symbolSize * 1.8d),
            pairRadius * 0.2d);
        var searchHalfHeight = Math.Max(
            symbolSize * 1.3d,
            searchHalfWidth * 0.55d);
        var connectionTolerance = Math.Max(symbolSize * 0.08d, 0.5d);
        var usedRoots = blockEntries
            .Select(entry => entry.RootHandle)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedGeometryHandles = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var primitives = regionEntities
            .Where(IsGeometryPrimitive)
            .Where(entity => entity.Bounds is not null)
            .Where(entity =>
                string.IsNullOrWhiteSpace(entity.RootHandle)
                || !usedRoots.Contains(entity.RootHandle))
            .ToArray();
        var insertSignatures = inventory.Entities
            .Where(entity => entity.ObjectType.Equals(
                "INSERT",
                StringComparison.OrdinalIgnoreCase))
            .Where(entity => !string.IsNullOrWhiteSpace(
                entity.GeometrySignature))
            .Select(entity => new SignatureOccurrence(
                entity.GeometrySignature,
                ParseSignature(entity.GeometrySignature)))
            .Where(item => item.Parsed is not null)
            .ToArray();
        var result = new List<RecognitionLegendEntry>();

        foreach (var description in descriptions
                     .Where(item => !usedDescriptions.Contains(
                         item.Entity.Handle))
                     .OrderByDescending(item => item.Entity.Y))
        {
            var expectedX = description.Entity.X - horizontalOffset;
            var expectedY = description.Entity.Y;
            var nearby = primitives
                .Where(entity => !usedGeometryHandles.Contains(entity.Handle))
                .Where(entity =>
                {
                    var center = Center(entity.Bounds!);
                    return Math.Abs(center.X - expectedX) <= searchHalfWidth
                           && Math.Abs(center.Y - expectedY)
                           <= searchHalfHeight
                           && center.X < description.Entity.X
                           + (searchHalfWidth * 0.15d);
                })
                .ToArray();
            var components = BuildConnectedComponents(
                nearby,
                connectionTolerance);
            LooseGeometryMatch? selected = null;
            foreach (var component in components)
            {
                if (component.Count == 0 || component.Count > 60)
                {
                    continue;
                }

                var componentBounds = UnionBounds(component);
                var width = componentBounds.MaximumX
                            - componentBounds.MinimumX;
                var height = componentBounds.MaximumY
                             - componentBounds.MinimumY;
                if (Math.Max(width, height) > searchHalfWidth * 2.2d)
                {
                    continue;
                }

                var looseSignature = BuildGeometrySignature(
                    component,
                    componentBounds);
                var looseParsed = ParseSignature(looseSignature);
                if (looseParsed is null)
                {
                    continue;
                }

                var matches = insertSignatures
                    .Where(item => item.Parsed!.ShapeKey.Equals(
                        looseParsed.ShapeKey,
                        StringComparison.Ordinal))
                    .Where(item => Math.Abs(
                        item.Parsed!.AspectRatio
                        - looseParsed.AspectRatio) <= 0.08d)
                    .GroupBy(item => item.Signature)
                    .Select(group => new
                    {
                        Signature = group.Key,
                        Occurrences = group.Count(),
                        Difference = Math.Abs(
                            group.First().Parsed!.AspectRatio
                            - looseParsed.AspectRatio)
                    })
                    .OrderBy(item => item.Difference)
                    .ThenByDescending(item => item.Occurrences)
                    .ToArray();
                if (matches.Length == 0)
                {
                    continue;
                }

                var center = Center(componentBounds);
                var distance = Math.Sqrt(
                    Math.Pow(center.X - expectedX, 2d)
                    + Math.Pow(center.Y - expectedY, 2d));
                var candidate = new LooseGeometryMatch(
                    component,
                    center,
                    matches[0].Signature,
                    matches[0].Occurrences,
                    distance);
                if (selected is null
                    || candidate.Distance < selected.Distance)
                {
                    selected = candidate;
                }
            }

            if (selected is null)
            {
                continue;
            }

            foreach (var entity in selected.Entities)
            {
                usedGeometryHandles.Add(entity.Handle);
            }

            var handles = selected.Entities
                .Select(entity => entity.Handle)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            result.Add(new RecognitionLegendEntry(
                $"GEOM:{description.Entity.Handle}",
                description.Entity.Handle,
                "Geometria solta",
                description.DisplayText,
                selected.Center.X,
                selected.Center.Y,
                description.Entity.X,
                description.Entity.Y,
                Math.Sqrt(
                    Math.Pow(selected.Center.X - description.Entity.X, 2d)
                    + Math.Pow(selected.Center.Y - description.Entity.Y, 2d)),
                $"{selected.Entities.Count} primitiva(s) da legenda formam "
                + "uma assinatura estrutural também encontrada em "
                + $"{selected.Occurrences} bloco(s) do desenho.",
                0,
                string.Join(
                    "+",
                    selected.Entities
                        .Select(entity => entity.RootHandle)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(6)),
                $"LOOSE:{string.Join(",", handles.Take(20))}",
                selected.MatchedSignature,
                selected.Entities.Count,
                0,
                true));
        }

        return result;
    }

    private static IReadOnlyList<RecognitionLegendEntry> BuildGlyphEntries(
        CadEntityInventory inventory,
        IReadOnlyList<TextEntity> descriptions,
        IReadOnlySet<string> usedDescriptions,
        IReadOnlyList<RecognitionLegendEntry> existingEntries,
        IReadOnlyList<CadInventoryEntity> regionEntities,
        double pairRadius)
    {
        var regionHandles = regionEntities
            .Select(entity => entity.Handle)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedSymbolHandles = existingEntries
            .Select(entry => entry.SymbolHandle)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var glyphs = inventory.Entities
            .Where(IsText)
            .Select(entity => new
            {
                Entity = entity,
                Value = Normalize(CadTextNormalizer.Normalize(entity.Text))
            })
            .Where(item => item.Value.Length is > 0 and <= 3)
            .ToArray();
        var result = new List<RecognitionLegendEntry>();
        foreach (var description in descriptions
                     .Where(item => !usedDescriptions.Contains(
                         item.Entity.Handle))
                     .Where(item => item.Normalized.Equals(
                         "INTERRUPTOR",
                         StringComparison.Ordinal)))
        {
            var symbol = glyphs
                .Where(item => regionHandles.Contains(item.Entity.Handle))
                .Where(item => !usedSymbolHandles.Contains(item.Entity.Handle))
                .Where(item => item.Entity.X < description.Entity.X)
                .Select(item => new
                {
                    item.Entity,
                    item.Value,
                    Distance = Distance(item.Entity, description.Entity)
                })
                .Where(item => item.Distance <= pairRadius)
                .OrderBy(item => item.Distance)
                .FirstOrDefault();
            if (symbol is null)
            {
                continue;
            }

            var occurrences = glyphs.Count(item =>
                item.Value.Equals(symbol.Value, StringComparison.Ordinal)
                && !regionHandles.Contains(item.Entity.Handle));
            if (occurrences == 0)
            {
                continue;
            }

            var signature = $"GLYPH:{symbol.Value}";
            result.Add(new RecognitionLegendEntry(
                symbol.Entity.Handle,
                description.Entity.Handle,
                $"Texto simbólico \"{symbol.Value}\"",
                description.DisplayText,
                symbol.Entity.X,
                symbol.Entity.Y,
                description.Entity.X,
                description.Entity.Y,
                symbol.Distance,
                $"Glifo \"{symbol.Value}\" pareado à descrição e repetido "
                + $"{occurrences} vez(es) fora da legenda.",
                symbol.Entity.ExpansionDepth,
                symbol.Entity.RootHandle,
                symbol.Entity.StablePath,
                signature,
                1,
                0,
                true));
            usedSymbolHandles.Add(symbol.Entity.Handle);
        }

        return result;
    }

    private static IReadOnlyList<IReadOnlyList<CadInventoryEntity>>
        BuildConnectedComponents(
            IReadOnlyList<CadInventoryEntity> entities,
            double tolerance)
    {
        var result = new List<IReadOnlyList<CadInventoryEntity>>();
        var remaining = new HashSet<int>(Enumerable.Range(0, entities.Count));
        while (remaining.Count > 0)
        {
            var seed = remaining.First();
            remaining.Remove(seed);
            var queue = new Queue<int>();
            queue.Enqueue(seed);
            var component = new List<CadInventoryEntity>();
            while (queue.Count > 0)
            {
                var currentIndex = queue.Dequeue();
                var current = entities[currentIndex];
                component.Add(current);
                var connected = remaining
                    .Where(index => BoundsGap(
                        current.Bounds!,
                        entities[index].Bounds!) <= tolerance)
                    .ToArray();
                foreach (var index in connected)
                {
                    remaining.Remove(index);
                    queue.Enqueue(index);
                }
            }

            result.Add(component);
        }

        return result;
    }

    private static bool IsGeometryPrimitive(CadInventoryEntity entity) =>
        entity.ObjectType.Equals("LINE", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Contains(
            "POLYLINE",
            StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("CIRCLE", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("ARC", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("ELLIPSE", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("SPLINE", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("SOLID", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("TRACE", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("3DFACE", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("HATCH", StringComparison.OrdinalIgnoreCase);

    private static string BuildGeometrySignature(
        IReadOnlyList<CadInventoryEntity> entities,
        CadInventoryBounds bounds)
    {
        var lines = CountType(entities, "LINE");
        var polylines = entities.Count(entity =>
            entity.ObjectType.Contains(
                "POLYLINE",
                StringComparison.OrdinalIgnoreCase));
        var circles = CountType(entities, "CIRCLE");
        var arcs = CountType(entities, "ARC");
        var curves = entities.Count(entity =>
            entity.ObjectType.Equals(
                "ELLIPSE",
                StringComparison.OrdinalIgnoreCase)
            || entity.ObjectType.Equals(
                "SPLINE",
                StringComparison.OrdinalIgnoreCase));
        var solids = entities.Count(entity =>
            entity.ObjectType.Equals("SOLID", StringComparison.OrdinalIgnoreCase)
            || entity.ObjectType.Equals(
                "TRACE",
                StringComparison.OrdinalIgnoreCase)
            || entity.ObjectType.Equals(
                "3DFACE",
                StringComparison.OrdinalIgnoreCase)
            || entity.ObjectType.Equals(
                "HATCH",
                StringComparison.OrdinalIgnoreCase));
        var others = Math.Max(
            0,
            entities.Count
            - lines
            - polylines
            - circles
            - arcs
            - curves
            - solids);
        var width = Math.Abs(bounds.MaximumX - bounds.MinimumX);
        var height = Math.Abs(bounds.MaximumY - bounds.MinimumY);
        var aspect = Math.Max(width, height) <= 0.000001d
            ? 0d
            : Math.Min(width, height) / Math.Max(width, height);
        return $"L{lines}P{polylines}C{circles}A{arcs}V{curves}"
               + $"S{solids}T0I0O{others}R"
               + aspect.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private static int CountType(
        IReadOnlyList<CadInventoryEntity> entities,
        string type) =>
        entities.Count(entity => entity.ObjectType.Equals(
            type,
            StringComparison.OrdinalIgnoreCase));

    private static ParsedSignature? ParseSignature(string signature)
    {
        var match = Regex.Match(
            signature ?? string.Empty,
            @"^(L\d+P\d+C\d+A\d+V\d+S\d+T\d+I\d+O\d+)R([0-9.]+)$",
            RegexOptions.CultureInvariant);
        var ratioText = match.Success ? match.Groups[2].Value : string.Empty;
        if (!match.Success
            || !double.TryParse(
                ratioText.StartsWith('.') ? $"0{ratioText}" : ratioText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var ratio))
        {
            return null;
        }

        return new ParsedSignature(match.Groups[1].Value, ratio);
    }

    private static CadInventoryBounds UnionBounds(
        IReadOnlyList<CadInventoryEntity> entities) =>
        new(
            entities.Min(entity => entity.Bounds!.MinimumX),
            entities.Min(entity => entity.Bounds!.MinimumY),
            entities.Min(entity => entity.Bounds!.MinimumZ),
            entities.Max(entity => entity.Bounds!.MaximumX),
            entities.Max(entity => entity.Bounds!.MaximumY),
            entities.Max(entity => entity.Bounds!.MaximumZ));

    private static (double X, double Y) Center(CadInventoryBounds bounds) =>
        (
            (bounds.MinimumX + bounds.MaximumX) / 2d,
            (bounds.MinimumY + bounds.MaximumY) / 2d
        );

    private static double BoundsGap(
        CadInventoryBounds left,
        CadInventoryBounds right)
    {
        var deltaX = Math.Max(
            0d,
            Math.Max(
                left.MinimumX - right.MaximumX,
                right.MinimumX - left.MaximumX));
        var deltaY = Math.Max(
            0d,
            Math.Max(
                left.MinimumY - right.MaximumY,
                right.MinimumY - left.MaximumY));
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static double ResolveDrawingScale(
        IReadOnlyList<CadInventoryEntity> entities)
    {
        var scaleEntities = entities
            .Where(entity => entity.ExpansionDepth == 0)
            .ToArray();
        if (scaleEntities.Length == 0)
        {
            scaleEntities = entities.ToArray();
        }

        // Usa as âncoras robustas do Model Space. Limites de blocos com ponto
        // base remoto podem ser enormes e não devem ampliar a região da legenda.
        var xs = scaleEntities
            .Select(entity => entity.X)
            .Where(double.IsFinite)
            .Order()
            .ToArray();
        var ys = scaleEntities
            .Select(entity => entity.Y)
            .Where(double.IsFinite)
            .Order()
            .ToArray();
        if (xs.Length == 0 || ys.Length == 0)
        {
            return 1d;
        }

        var minimumX = Percentile(xs, 0.02d);
        var maximumX = Percentile(xs, 0.98d);
        var minimumY = Percentile(ys, 0.02d);
        var maximumY = Percentile(ys, 0.98d);
        var diagonal = Math.Sqrt(
            Math.Pow(maximumX - minimumX, 2d)
            + Math.Pow(maximumY - minimumY, 2d));
        return Math.Max(diagonal, 1d);
    }

    private static double ResolveTypicalTextHeight(
        IReadOnlyList<TextEntity> texts)
    {
        var heights = texts
            .Where(item => item.Entity.Bounds is not null)
            .Select(item =>
                item.Entity.Bounds!.MaximumY - item.Entity.Bounds.MinimumY)
            .Where(height => double.IsFinite(height) && height > 0.000001d)
            .Order()
            .ToArray();
        return heights.Length == 0
            ? 1d
            : Math.Max(Percentile(heights, 0.5d), 0.000001d);
    }

    private static LegendBounds ResolveBounds(
        IReadOnlyList<CadInventoryEntity> entities,
        IReadOnlyList<TextEntity> anchors)
    {
        var source = entities.Count > 0
            ? entities
            : anchors.Select(anchor => anchor.Entity).ToArray();
        return new LegendBounds(
            source.Min(entity => entity.Bounds?.MinimumX ?? entity.X),
            source.Min(entity => entity.Bounds?.MinimumY ?? entity.Y),
            source.Max(entity => entity.Bounds?.MaximumX ?? entity.X),
            source.Max(entity => entity.Bounds?.MaximumY ?? entity.Y));
    }

    private static double Percentile(double[] values, double percentile)
    {
        var position = (values.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return values[lower];
        }

        var fraction = position - lower;
        return values[lower] + ((values[upper] - values[lower]) * fraction);
    }

    private static double Distance(
        CadInventoryEntity left,
        CadInventoryEntity right)
    {
        var deltaX = left.X - right.X;
        var deltaY = left.Y - right.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character)
                == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToUpperInvariant(character)
                : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString().Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record TextEntity(
        CadInventoryEntity Entity,
        string DisplayText,
        string Normalized);

    private sealed record PairCandidate(
        TextEntity Description,
        CadInventoryEntity Symbol,
        TextEntity Anchor,
        double Distance);

    private sealed record ParsedSignature(
        string ShapeKey,
        double AspectRatio);

    private sealed record SignatureOccurrence(
        string Signature,
        ParsedSignature? Parsed);

    private sealed record LooseGeometryMatch(
        IReadOnlyList<CadInventoryEntity> Entities,
        (double X, double Y) Center,
        string MatchedSignature,
        int Occurrences,
        double Distance);

    private sealed record LegendBounds(
        double MinimumX,
        double MinimumY,
        double MaximumX,
        double MaximumY);
}
