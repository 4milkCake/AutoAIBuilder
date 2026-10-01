using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Desktop.ViewModels;

namespace AutoAIBuilder.Desktop.Controls;

public sealed class CadViewportControl : FrameworkElement
{
    public static readonly DependencyProperty SnapshotProperty =
        DependencyProperty.Register(
            nameof(Snapshot),
            typeof(CadVisualizationSnapshot),
            typeof(CadViewportControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnSnapshotChanged));

    public static readonly DependencyProperty OverlayPointsProperty =
        DependencyProperty.Register(
            nameof(OverlayPoints),
            typeof(IEnumerable<CadOverlayPointViewModel>),
            typeof(CadViewportControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnOverlayPointsChanged));

    public static readonly DependencyProperty LayersProperty =
        DependencyProperty.Register(
            nameof(Layers),
            typeof(ObservableCollection<CadLayerItemViewModel>),
            typeof(CadViewportControl),
            new FrameworkPropertyMetadata(null, OnLayersChanged));

    public static readonly DependencyProperty SelectedPointIdProperty =
        DependencyProperty.Register(
            nameof(SelectedPointId),
            typeof(Guid?),
            typeof(CadViewportControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnSelectedPointChanged));

    public static readonly DependencyProperty SelectPointCommandProperty =
        DependencyProperty.Register(
            nameof(SelectPointCommand),
            typeof(ICommand),
            typeof(CadViewportControl));

    public static readonly RoutedEvent OverlayPointSelectedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(OverlayPointSelected),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(CadViewportControl));

    public static readonly DependencyProperty FitAllByDefaultProperty =
        DependencyProperty.Register(
            nameof(FitAllByDefault),
            typeof(bool),
            typeof(CadViewportControl),
            new FrameworkPropertyMetadata(false, OnFitAllByDefaultChanged));

    private const double Padding = 18;
    private Point _pan;
    private Point _lastPointer;
    private bool _isPanning;
    private double _zoom = 1;
    private bool _fitToSemanticArea = true;
    private bool _pendingSelectedPointCenter;
    private readonly Dictionary<string, Pen> _regularPens =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Pen> _boundsPens =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Brush> _layerBrushes =
        new(StringComparer.OrdinalIgnoreCase);

    public CadVisualizationSnapshot? Snapshot
    {
        get => (CadVisualizationSnapshot?)GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    public IEnumerable<CadOverlayPointViewModel>? OverlayPoints
    {
        get => (IEnumerable<CadOverlayPointViewModel>?)
            GetValue(OverlayPointsProperty);
        set => SetValue(OverlayPointsProperty, value);
    }

    public ObservableCollection<CadLayerItemViewModel>? Layers
    {
        get => (ObservableCollection<CadLayerItemViewModel>?)
            GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    public Guid? SelectedPointId
    {
        get => (Guid?)GetValue(SelectedPointIdProperty);
        set => SetValue(SelectedPointIdProperty, value);
    }

    public ICommand? SelectPointCommand
    {
        get => (ICommand?)GetValue(SelectPointCommandProperty);
        set => SetValue(SelectPointCommandProperty, value);
    }

    public event RoutedEventHandler OverlayPointSelected
    {
        add => AddHandler(OverlayPointSelectedEvent, value);
        remove => RemoveHandler(OverlayPointSelectedEvent, value);
    }

    public bool FitAllByDefault
    {
        get => (bool)GetValue(FitAllByDefaultProperty);
        set => SetValue(FitAllByDefaultProperty, value);
    }

    public void Fit()
    {
        _fitToSemanticArea = true;
        _zoom = 1;
        _pan = default;
        InvalidateVisual();
    }

    public void FitAll()
    {
        _fitToSemanticArea = false;
        _zoom = 1;
        _pan = default;
        InvalidateVisual();
    }

    public void ZoomIn() => ChangeZoom(1.25, new Point(ActualWidth / 2, ActualHeight / 2));

    public void ZoomOut() => ChangeZoom(0.8, new Point(ActualWidth / 2, ActualHeight / 2));

    public void CenterSelectedPoint()
    {
        _pendingSelectedPointCenter = true;
        if (TryCenterSelectedPoint())
        {
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        TryCenterSelectedPoint();
        drawingContext.DrawRectangle(
            new SolidColorBrush(Color.FromRgb(5, 14, 24)),
            null,
            new Rect(RenderSize));
        if (Snapshot is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            DrawCenteredMessage(
                drawingContext,
                "Gere a planta com o AutoCAD para ativar o visualizador.");
            return;
        }

        var transform = CreateTransform(Snapshot.Bounds);
        var visibleLayers = Layers?
            .Where(layer => layer.IsVisible)
            .Select(layer => layer.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var primitive in Snapshot.Primitives)
        {
            if (visibleLayers is not null
                && !visibleLayers.Contains(primitive.Layer))
            {
                continue;
            }

            DrawPrimitive(drawingContext, primitive, transform);
        }

        foreach (var point in OverlayPoints ?? [])
        {
            DrawOverlayPoint(drawingContext, point, transform);
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        ChangeZoom(
            e.Delta > 0 ? 1.15 : 1 / 1.15,
            e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        var pointer = e.GetPosition(this);
        var hit = FindNearestPoint(pointer);
        if (hit is not null
            && SelectPointCommand?.CanExecute(hit) == true)
        {
            SelectPointCommand.Execute(hit);
            RaiseEvent(new RoutedEventArgs(
                OverlayPointSelectedEvent,
                this));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        _isPanning = true;
        _lastPointer = pointer;
        CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var pointer = e.GetPosition(this);
        _pan = new Point(
            _pan.X + pointer.X - _lastPointer.X,
            _pan.Y + pointer.Y - _lastPointer.Y);
        _lastPointer = pointer;
        InvalidateVisual();
        e.Handled = true;
    }

    private static void OnSnapshotChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CadViewportControl control)
        {
            control._regularPens.Clear();
            control._boundsPens.Clear();
            control._layerBrushes.Clear();
            control.ResetToDefaultFit();
            control.CenterSelectedPoint();
        }
    }

    private static void OnFitAllByDefaultChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CadViewportControl control)
        {
            control.ResetToDefaultFit();
        }
    }

    private static void OnOverlayPointsChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CadViewportControl control)
        {
            control.CenterSelectedPoint();
        }
    }

    private static void OnSelectedPointChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CadViewportControl control)
        {
            control.CenterSelectedPoint();
        }
    }

    private static void OnLayersChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not CadViewportControl control)
        {
            return;
        }

        control.UnsubscribeLayers(
            e.OldValue as ObservableCollection<CadLayerItemViewModel>);
        control.SubscribeLayers(
            e.NewValue as ObservableCollection<CadLayerItemViewModel>);
        control.InvalidateVisual();
    }

    private void SubscribeLayers(
        ObservableCollection<CadLayerItemViewModel>? layers)
    {
        if (layers is null)
        {
            return;
        }

        layers.CollectionChanged += LayersCollectionChanged;
        foreach (var layer in layers)
        {
            layer.PropertyChanged += LayerPropertyChanged;
        }
    }

    private void UnsubscribeLayers(
        ObservableCollection<CadLayerItemViewModel>? layers)
    {
        if (layers is null)
        {
            return;
        }

        layers.CollectionChanged -= LayersCollectionChanged;
        foreach (var layer in layers)
        {
            layer.PropertyChanged -= LayerPropertyChanged;
        }
    }

    private void LayersCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (CadLayerItemViewModel layer in e.OldItems)
            {
                layer.PropertyChanged -= LayerPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (CadLayerItemViewModel layer in e.NewItems)
            {
                layer.PropertyChanged += LayerPropertyChanged;
            }
        }

        InvalidateVisual();
    }

    private void LayerPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        InvalidateVisual();

    private void ResetToDefaultFit()
    {
        if (FitAllByDefault)
        {
            FitAll();
            return;
        }

        Fit();
    }

    private ViewportTransform CreateTransform(CadDrawingBounds bounds)
    {
        bounds = _fitToSemanticArea
            ? GetSemanticAreaBounds(bounds)
            : bounds;
        var availableWidth = Math.Max(ActualWidth - (2 * Padding), 1);
        var availableHeight = Math.Max(ActualHeight - (2 * Padding), 1);
        var fitScale = Math.Min(
            availableWidth / bounds.Width,
            availableHeight / bounds.Height);
        var scale = fitScale * _zoom;
        var centeredX = Padding
            + ((availableWidth - (bounds.Width * fitScale)) / 2);
        var centeredY = Padding
            + ((availableHeight - (bounds.Height * fitScale)) / 2);
        return new ViewportTransform(
            bounds,
            scale,
            centeredX + _pan.X,
            centeredY + _pan.Y);
    }

    private CadDrawingBounds GetSemanticAreaBounds(
        CadDrawingBounds drawingBounds)
    {
        var points = (OverlayPoints ?? []).ToArray();
        if (points.Length < 2)
        {
            return drawingBounds;
        }

        var minimumX = points.Min(point => point.X);
        var maximumX = points.Max(point => point.X);
        var minimumY = points.Min(point => point.Y);
        var maximumY = points.Max(point => point.Y);
        var rangeX = maximumX - minimumX;
        var rangeY = maximumY - minimumY;
        if (rangeX <= 0 || rangeY <= 0)
        {
            return drawingBounds;
        }

        var marginX = Math.Max(rangeX * 0.15, drawingBounds.Width * 0.02);
        var marginY = Math.Max(rangeY * 0.15, drawingBounds.Height * 0.02);
        var focused = new CadDrawingBounds(
            Math.Max(drawingBounds.MinimumX, minimumX - marginX),
            Math.Max(drawingBounds.MinimumY, minimumY - marginY),
            Math.Min(drawingBounds.MaximumX, maximumX + marginX),
            Math.Min(drawingBounds.MaximumY, maximumY + marginY));
        return focused.Width > 0 && focused.Height > 0
            ? focused
            : drawingBounds;
    }

    private void DrawPrimitive(
        DrawingContext context,
        CadPrimitive primitive,
        ViewportTransform transform)
    {
        var pen = GetLayerPen(
            primitive.Layer,
            primitive.Kind == CadPrimitiveKind.Bounds);
        switch (primitive.Kind)
        {
            case CadPrimitiveKind.Line:
                if (primitive.Points.Count >= 2)
                {
                    context.DrawLine(
                        pen,
                        transform.ToScreen(primitive.Points[0]),
                        transform.ToScreen(primitive.Points[1]));
                }
                break;
            case CadPrimitiveKind.Polyline:
            case CadPrimitiveKind.Bounds:
                DrawPolyline(context, primitive.Points, transform, pen);
                break;
            case CadPrimitiveKind.Circle:
                if (primitive.Points.Count > 0)
                {
                    var center = transform.ToScreen(primitive.Points[0]);
                    var radius = primitive.Radius * transform.Scale;
                    context.DrawEllipse(null, pen, center, radius, radius);
                }
                break;
            case CadPrimitiveKind.Arc:
                DrawArc(context, primitive, transform, pen);
                break;
            case CadPrimitiveKind.Text:
                DrawText(
                    context,
                    primitive,
                    transform,
                    GetLayerBrush(primitive.Layer));
                break;
        }
    }

    private static void DrawPolyline(
        DrawingContext context,
        IReadOnlyList<CadPoint2D> points,
        ViewportTransform transform,
        Pen pen)
    {
        if (points.Count < 2)
        {
            return;
        }

        for (var index = 1; index < points.Count; index++)
        {
            context.DrawLine(
                pen,
                transform.ToScreen(points[index - 1]),
                transform.ToScreen(points[index]));
        }
    }

    private static void DrawArc(
        DrawingContext context,
        CadPrimitive primitive,
        ViewportTransform transform,
        Pen pen)
    {
        if (primitive.Points.Count == 0)
        {
            return;
        }

        var sweep = primitive.EndAngleDegrees - primitive.StartAngleDegrees;
        if (sweep <= 0)
        {
            sweep += 360;
        }

        var segments = Math.Clamp((int)Math.Ceiling(sweep / 8), 4, 64);
        Point? previous = null;
        for (var index = 0; index <= segments; index++)
        {
            var degrees = primitive.StartAngleDegrees
                + (sweep * index / segments);
            var radians = degrees * Math.PI / 180;
            var world = new CadPoint2D(
                primitive.Points[0].X + primitive.Radius * Math.Cos(radians),
                primitive.Points[0].Y + primitive.Radius * Math.Sin(radians));
            var current = transform.ToScreen(world);
            if (previous is not null)
            {
                context.DrawLine(pen, previous.Value, current);
            }

            previous = current;
        }
    }

    private void DrawText(
        DrawingContext context,
        CadPrimitive primitive,
        ViewportTransform transform,
        Brush brush)
    {
        if (primitive.Points.Count == 0
            || string.IsNullOrWhiteSpace(primitive.Text))
        {
            return;
        }

        var rawSize = Math.Max(
            primitive.TextHeight * transform.Scale,
            0.001);
        var size = Math.Clamp(rawSize, 3, 22);
        if (size < 4.5 && _zoom < 2)
        {
            return;
        }

        var text = new FormattedText(
            primitive.Text,
            CultureInfo.GetCultureInfo("pt-BR"),
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var widthScale = size / rawSize;
        if (primitive.TextWidth > 0)
        {
            text.MaxTextWidth = Math.Max(
                primitive.TextWidth * transform.Scale * widthScale,
                1);
        }

        text.TextAlignment = GetTextAlignment(
            primitive.TextAttachment);
        var layoutWidth = primitive.TextWidth > 0
            ? text.MaxTextWidth
            : Math.Max(text.WidthIncludingTrailingWhitespace, 1);
        if (primitive.TextWidth <= 0)
        {
            text.MaxTextWidth = layoutWidth;
        }

        var anchor = transform.ToScreen(primitive.Points[0]);
        var origin = new Point(
            anchor.X - GetHorizontalTextOffset(
                primitive.TextAttachment,
                layoutWidth),
            anchor.Y - GetVerticalTextOffset(
                primitive.TextAttachment,
                text));
        context.PushTransform(new RotateTransform(
            -primitive.RotationDegrees,
            anchor.X,
            anchor.Y));
        context.DrawText(text, origin);
        context.Pop();
    }

    private static TextAlignment GetTextAlignment(
        CadTextAttachment attachment) =>
        attachment switch
        {
            CadTextAttachment.BaselineCenter
                or CadTextAttachment.BottomCenter
                or CadTextAttachment.MiddleCenter
                or CadTextAttachment.TopCenter =>
                TextAlignment.Center,
            CadTextAttachment.BaselineRight
                or CadTextAttachment.BottomRight
                or CadTextAttachment.MiddleRight
                or CadTextAttachment.TopRight =>
                TextAlignment.Right,
            _ => TextAlignment.Left
        };

    private static double GetHorizontalTextOffset(
        CadTextAttachment attachment,
        double width) =>
        attachment switch
        {
            CadTextAttachment.BaselineCenter
                or CadTextAttachment.BottomCenter
                or CadTextAttachment.MiddleCenter
                or CadTextAttachment.TopCenter =>
                width / 2,
            CadTextAttachment.BaselineRight
                or CadTextAttachment.BottomRight
                or CadTextAttachment.MiddleRight
                or CadTextAttachment.TopRight =>
                width,
            _ => 0
        };

    private static double GetVerticalTextOffset(
        CadTextAttachment attachment,
        FormattedText text) =>
        attachment switch
        {
            CadTextAttachment.MiddleLeft
                or CadTextAttachment.MiddleCenter
                or CadTextAttachment.MiddleRight =>
                text.Height / 2,
            CadTextAttachment.BottomLeft
                or CadTextAttachment.BottomCenter
                or CadTextAttachment.BottomRight =>
                text.Height,
            CadTextAttachment.BaselineLeft
                or CadTextAttachment.BaselineCenter
                or CadTextAttachment.BaselineRight =>
                text.Baseline,
            _ => 0
        };

    private void DrawOverlayPoint(
        DrawingContext context,
        CadOverlayPointViewModel point,
        ViewportTransform transform)
    {
        var center = transform.ToScreen(new CadPoint2D(point.X, point.Y));
        var selected = point.PointId == SelectedPointId;
        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(
                selected ? "#FFF4C2" : point.Accent));
        brush.Freeze();
        if (selected)
        {
            var halo = new Pen(brush, 2.2);
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(55, 255, 244, 194)),
                halo,
                center,
                12,
                12);
            context.DrawLine(
                halo,
                new Point(center.X - 18, center.Y),
                new Point(center.X + 18, center.Y));
            context.DrawLine(
                halo,
                new Point(center.X, center.Y - 18),
                new Point(center.X, center.Y + 18));
        }

        context.DrawEllipse(
            brush,
            new Pen(Brushes.White, selected ? 1.5 : 0.5),
            center,
            selected ? 5 : 3,
            selected ? 5 : 3);
    }

    private CadOverlayPointViewModel? FindNearestPoint(Point pointer)
    {
        if (Snapshot is null)
        {
            return null;
        }

        var transform = CreateTransform(Snapshot.Bounds);
        return (OverlayPoints ?? [])
            .Select(point => new
            {
                Point = point,
                Distance = (transform.ToScreen(
                    new CadPoint2D(point.X, point.Y)) - pointer).Length
            })
            .Where(candidate => candidate.Distance <= 12)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Point)
            .FirstOrDefault();
    }

    private bool TryCenterSelectedPoint()
    {
        if (!_pendingSelectedPointCenter
            || Snapshot is null
            || SelectedPointId is null
            || ActualWidth <= 0
            || ActualHeight <= 0)
        {
            return false;
        }

        var selected = (OverlayPoints ?? [])
            .FirstOrDefault(point => point.PointId == SelectedPointId);
        if (selected is null)
        {
            return false;
        }

        var transform = CreateTransform(Snapshot.Bounds);
        var screen = transform.ToScreen(
            new CadPoint2D(selected.X, selected.Y));
        _pan = new Point(
            _pan.X + (ActualWidth / 2) - screen.X,
            _pan.Y + (ActualHeight / 2) - screen.Y);
        _pendingSelectedPointCenter = false;
        return true;
    }

    private void ChangeZoom(double factor, Point anchor)
    {
        if (Snapshot is null)
        {
            return;
        }

        var newZoom = Math.Clamp(_zoom * factor, 0.2, 40);
        var appliedFactor = newZoom / _zoom;
        _pan = new Point(
            anchor.X - ((anchor.X - _pan.X) * appliedFactor),
            anchor.Y - ((anchor.Y - _pan.Y) * appliedFactor));
        _zoom = newZoom;
        InvalidateVisual();
    }

    private void DrawCenteredMessage(DrawingContext context, string message)
    {
        var text = new FormattedText(
            message,
            CultureInfo.GetCultureInfo("pt-BR"),
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            new SolidColorBrush(Color.FromRgb(98, 112, 135)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        context.DrawText(
            text,
            new Point(
                Math.Max((ActualWidth - text.Width) / 2, 8),
                Math.Max((ActualHeight - text.Height) / 2, 8)));
    }

    private static Color GetLayerColor(string layer)
    {
        if (layer.Contains("HID", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromRgb(64, 181, 173);
        }

        if (layer.Contains("ELE", StringComparison.OrdinalIgnoreCase)
            || layer.Contains("PONTOS_", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromRgb(73, 139, 210);
        }

        if (layer.Contains("TEXT", StringComparison.OrdinalIgnoreCase)
            || layer.Contains("COTA", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromRgb(143, 157, 176);
        }

        return Color.FromRgb(179, 194, 211);
    }

    private Pen GetLayerPen(string layer, bool bounds)
    {
        var cache = bounds ? _boundsPens : _regularPens;
        if (cache.TryGetValue(layer, out var pen))
        {
            return pen;
        }

        pen = new Pen(GetLayerBrush(layer), bounds ? 0.6 : 0.85);
        pen.Freeze();
        cache[layer] = pen;
        return pen;
    }

    private Brush GetLayerBrush(string layer)
    {
        if (_layerBrushes.TryGetValue(layer, out var brush))
        {
            return brush;
        }

        var solid = new SolidColorBrush(GetLayerColor(layer));
        solid.Freeze();
        _layerBrushes[layer] = solid;
        return solid;
    }

    private sealed record ViewportTransform(
        CadDrawingBounds Bounds,
        double Scale,
        double OffsetX,
        double OffsetY)
    {
        public Point ToScreen(CadPoint2D point) => new(
            OffsetX + ((point.X - Bounds.MinimumX) * Scale),
            OffsetY + ((Bounds.MaximumY - point.Y) * Scale));
    }
}
