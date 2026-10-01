namespace AutoAIBuilder.Infrastructure.Recognition;

public static class CadExpandedGeometryAnchorResolver
{
    public static CadEntityInventory Resolve(CadEntityInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var expandedBounds = inventory.Entities
            .Where(entity => entity.ExpansionDepth > 0)
            .Where(entity => entity.Bounds is not null)
            .Where(IsGeometry)
            .Where(entity => !string.IsNullOrWhiteSpace(entity.RootHandle))
            .GroupBy(
                entity => entity.RootHandle,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => Union(group.ToArray()),
                StringComparer.OrdinalIgnoreCase);
        if (expandedBounds.Count == 0)
        {
            return inventory;
        }

        var entities = inventory.Entities
            .Select(entity => Resolve(entity, expandedBounds))
            .ToArray();
        return inventory with { Entities = entities };
    }

    private static CadInventoryEntity Resolve(
        CadInventoryEntity entity,
        IReadOnlyDictionary<string, CadInventoryBounds> expandedBounds)
    {
        if (entity.ExpansionDepth != 0
            || !entity.ObjectType.Equals(
                "INSERT",
                StringComparison.OrdinalIgnoreCase)
            || !expandedBounds.TryGetValue(
                entity.Handle,
                out var bounds))
        {
            return entity;
        }

        var width = bounds.MaximumX - bounds.MinimumX;
        var height = bounds.MaximumY - bounds.MinimumY;
        var span = Math.Max(Math.Abs(width), Math.Abs(height));
        var tolerance = Math.Max(span * 0.1d, 0.000001d);
        var distance = OutsideDistance(entity.X, entity.Y, bounds);
        if (distance <= tolerance)
        {
            return entity with { Bounds = bounds };
        }

        return entity with
        {
            X = (bounds.MinimumX + bounds.MaximumX) / 2d,
            Y = (bounds.MinimumY + bounds.MaximumY) / 2d,
            Z = (bounds.MinimumZ + bounds.MaximumZ) / 2d,
            AnchorSource = "EXPANDED_GEOMETRY_CENTER_WCS",
            Bounds = bounds
        };
    }

    private static double OutsideDistance(
        double x,
        double y,
        CadInventoryBounds bounds)
    {
        var deltaX = x < bounds.MinimumX
            ? bounds.MinimumX - x
            : x > bounds.MaximumX
                ? x - bounds.MaximumX
                : 0d;
        var deltaY = y < bounds.MinimumY
            ? bounds.MinimumY - y
            : y > bounds.MaximumY
                ? y - bounds.MaximumY
                : 0d;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static CadInventoryBounds Union(
        IReadOnlyList<CadInventoryEntity> entities) =>
        new(
            entities.Min(entity => entity.Bounds!.MinimumX),
            entities.Min(entity => entity.Bounds!.MinimumY),
            entities.Min(entity => entity.Bounds!.MinimumZ),
            entities.Max(entity => entity.Bounds!.MaximumX),
            entities.Max(entity => entity.Bounds!.MaximumY),
            entities.Max(entity => entity.Bounds!.MaximumZ));

    private static bool IsGeometry(CadInventoryEntity entity) =>
        entity.ObjectType.Equals("LINE", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Contains(
            "POLYLINE",
            StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("CIRCLE", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("ARC", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("ELLIPSE", StringComparison.OrdinalIgnoreCase)
        || entity.ObjectType.Equals("SPLINE", StringComparison.OrdinalIgnoreCase);
}
