using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using WildfireSimulator.Client.Models;

namespace WildfireSimulator.Client.Views.Controls;

public partial class NodeGraphVisualization : UserControl
{
    private Canvas? _graphCanvas;
    private ScrollViewer? _scrollHost;

    private Button? _zoomInButton;
    private Button? _zoomOutButton;
    private Button? _resetZoomButton;

    private INotifyCollectionChanged? _currentNodesCollection;
    private INotifyCollectionChanged? _currentEdgesCollection;

    private bool _drawScheduled;
    private double _zoom = 1.0;

    private const double MinZoom = 0.55;
    private const double MaxZoom = 2.40;
    private const double ZoomStep = 0.18;

    private const double BaseNodeRadius = 8.5;
    private const double SelectedNodeRadius = 11.0;
    private const double NeighborNodeRadius = 9.5;

    private const double BaseEdgeThickness = 1.4;
    private const double MinCanvasSize = 500.0;
    private const double CanvasPadding = 56.0;

    public static readonly StyledProperty<IEnumerable<SimulationGraphNodeDto>?> NodesProperty =
        AvaloniaProperty.Register<NodeGraphVisualization, IEnumerable<SimulationGraphNodeDto>?>(nameof(Nodes));

    public static readonly StyledProperty<IEnumerable<SimulationGraphEdgeDto>?> EdgesProperty =
        AvaloniaProperty.Register<NodeGraphVisualization, IEnumerable<SimulationGraphEdgeDto>?>(nameof(Edges));

    public static readonly StyledProperty<string> LayoutHintProperty =
        AvaloniaProperty.Register<NodeGraphVisualization, string>(nameof(LayoutHint), "node-link");

    public static readonly StyledProperty<SimulationGraphNodeDto?> SelectedNodeProperty =
        AvaloniaProperty.Register<NodeGraphVisualization, SimulationGraphNodeDto?>(nameof(SelectedNode));

    public static readonly StyledProperty<bool> IsIgnitionSelectionEnabledProperty =
        AvaloniaProperty.Register<NodeGraphVisualization, bool>(nameof(IsIgnitionSelectionEnabled), false);

    public bool IsIgnitionSelectionEnabled
    {
        get => GetValue(IsIgnitionSelectionEnabledProperty);
        set => SetValue(IsIgnitionSelectionEnabledProperty, value);
    }

    public IEnumerable<SimulationGraphNodeDto>? Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public IEnumerable<SimulationGraphEdgeDto>? Edges
    {
        get => GetValue(EdgesProperty);
        set => SetValue(EdgesProperty, value);
    }

    public string LayoutHint
    {
        get => GetValue(LayoutHintProperty);
        set => SetValue(LayoutHintProperty, value);
    }

    public SimulationGraphNodeDto? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public event EventHandler<SimulationGraphNodeDto>? NodeClicked;

    public NodeGraphVisualization()
    {
        InitializeComponent();

        _graphCanvas = this.FindControl<Canvas>("GraphCanvas");
        _scrollHost = this.FindControl<ScrollViewer>("ScrollHost");

        _zoomInButton = this.FindControl<Button>("ZoomInButton");
        _zoomOutButton = this.FindControl<Button>("ZoomOutButton");
        _resetZoomButton = this.FindControl<Button>("ResetZoomButton");

        if (_zoomInButton != null)
            _zoomInButton.Click += (_, _) => ChangeZoom(ZoomStep);

        if (_zoomOutButton != null)
            _zoomOutButton.Click += (_, _) => ChangeZoom(-ZoomStep);

        if (_resetZoomButton != null)
            _resetZoomButton.Click += (_, _) => ResetZoom();

        PropertyChanged += OnGraphPropertyChanged;
        AttachedToVisualTree += (_, _) => ScheduleDraw();

        if (_scrollHost != null)
            _scrollHost.SizeChanged += (_, _) => ScheduleDraw();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ChangeZoom(double delta)
    {
        _zoom = Math.Clamp(_zoom + delta, MinZoom, MaxZoom);
        ApplyZoom();
    }

    private void ResetZoom()
    {
        _zoom = 1.0;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (_graphCanvas == null)
            return;

        _graphCanvas.RenderTransform = new ScaleTransform(_zoom, _zoom);
    }

    private void OnGraphPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == NodesProperty)
        {
            RebindNodesCollection();
            ScheduleDraw();
            return;
        }

        if (e.Property == EdgesProperty)
        {
            RebindEdgesCollection();
            ScheduleDraw();
            return;
        }

        if (e.Property == LayoutHintProperty ||
            e.Property == SelectedNodeProperty ||
            e.Property == IsIgnitionSelectionEnabledProperty)
        {
            ScheduleDraw();
        }
    }

    private void RebindNodesCollection()
    {
        if (_currentNodesCollection != null)
        {
            _currentNodesCollection.CollectionChanged -= OnCollectionChanged;
            _currentNodesCollection = null;
        }

        if (Nodes is INotifyCollectionChanged notify)
        {
            _currentNodesCollection = notify;
            _currentNodesCollection.CollectionChanged += OnCollectionChanged;
        }
    }

    private void RebindEdgesCollection()
    {
        if (_currentEdgesCollection != null)
        {
            _currentEdgesCollection.CollectionChanged -= OnCollectionChanged;
            _currentEdgesCollection = null;
        }

        if (Edges is INotifyCollectionChanged notify)
        {
            _currentEdgesCollection = notify;
            _currentEdgesCollection.CollectionChanged += OnCollectionChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleDraw();
    }

    private void ScheduleDraw()
    {
        if (_drawScheduled)
            return;

        _drawScheduled = true;

        Dispatcher.UIThread.Post(() =>
        {
            _drawScheduled = false;
            DrawGraph();
        }, DispatcherPriority.Background);
    }

    private void DrawGraph()
    {
        if (_graphCanvas == null)
            return;

        _graphCanvas.Children.Clear();

        var nodes = Nodes?.ToList() ?? new List<SimulationGraphNodeDto>();
        var edges = Edges?.ToList() ?? new List<SimulationGraphEdgeDto>();

        if (nodes.Count == 0)
        {
            DrawEmptyState();
            return;
        }

        DrawStandardGraph(nodes, edges);
        ApplyZoom();
    }

    private void DrawStandardGraph(List<SimulationGraphNodeDto> nodes, List<SimulationGraphEdgeDto> edges)
    {
        if (_graphCanvas == null)
            return;

        var bounds = GetNodeBounds(nodes);
        var scale = GetStandardGraphScale(nodes, bounds);

        var width = Math.Max(MinCanvasSize, (bounds.MaxX - bounds.MinX) * scale + CanvasPadding * 2);
        var height = Math.Max(MinCanvasSize, (bounds.MaxY - bounds.MinY) * scale + CanvasPadding * 2);

        _graphCanvas.Width = width;
        _graphCanvas.Height = height;

        var points = nodes.ToDictionary(
            n => n.Id,
            n => new Point(
                CanvasPadding + (n.RenderX - bounds.MinX) * scale,
                CanvasPadding + (n.RenderY - bounds.MinY) * scale));

        var nodeMap = nodes.ToDictionary(n => n.Id);

        var selectedNodeId = SelectedNode?.Id;
        var selectedEdgeIds = GetSelectedEdgeIds(edges, selectedNodeId);
        var neighborIds = GetNeighborIds(edges, selectedNodeId);

        DrawClusterPatchBackgrounds(nodes, points);

        foreach (var edge in edges)
        {
            var isCrossCluster =
                nodeMap.TryGetValue(edge.FromCellId, out var fromNode) &&
                nodeMap.TryGetValue(edge.ToCellId, out var toNode) &&
                !string.IsNullOrWhiteSpace(fromNode.GroupKey) &&
                !string.IsNullOrWhiteSpace(toNode.GroupKey) &&
                !string.Equals(fromNode.GroupKey, toNode.GroupKey, StringComparison.Ordinal);

            DrawEdge(edge, points, selectedNodeId, selectedEdgeIds, isCrossCluster);
        }

        foreach (var node in nodes.OrderBy(n => n.IsBurning ? 0 : 1))
            DrawNode(node, edges, points, selectedNodeId, neighborIds, useMapStyle: false);
    }
    private double GetStandardGraphScale(
        List<SimulationGraphNodeDto> nodes,
        (double MinX, double MaxX, double MinY, double MaxY) bounds)
    {
        int count = nodes.Count;
        double spanX = Math.Max(1.0, bounds.MaxX - bounds.MinX);
        double spanY = Math.Max(1.0, bounds.MaxY - bounds.MinY);
        double maxSpan = Math.Max(spanX, spanY);

        if (count <= 10)
            return 46.0;

        if (count <= 16)
            return 40.0;

        if (count <= 30)
            return 34.0;

        if (count <= 60)
            return 28.0;

        if (count <= 120)
            return maxSpan <= 12.0 ? 24.0 : 22.0;

        if (count <= 200)
            return maxSpan <= 14.0 ? 21.0 : 19.0;

        return 17.0;
    }

    private void DrawClusterPatchBackgrounds(
      List<SimulationGraphNodeDto> nodes,
      Dictionary<Guid, Point> points)
    {
        if (_graphCanvas == null)
            return;

        var groups = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.GroupKey))
            .GroupBy(n => n.GroupKey)
            .Where(g => g.Count() >= 3)
            .OrderBy(g => g.Key)
            .ToList();

        if (groups.Count < 2)
            return;

        foreach (var group in groups)
        {
            var groupNodes = group.ToList();

            var groupPoints = groupNodes
                .Where(n => points.ContainsKey(n.Id))
                .Select(n => points[n.Id])
                .ToList();

            if (groupPoints.Count < 3)
                continue;

            var hull = BuildSoftHull(groupPoints);
            if (hull.Count < 3)
                continue;

            var polygon = new Polygon
            {
                Points = new Points(hull),
                Fill = new SolidColorBrush(GetGroupFillColor(groupNodes)),
                Stroke = new SolidColorBrush(GetGroupStrokeColor(groupNodes)),
                StrokeThickness = 1.1,
                Opacity = 0.40,
                IsHitTestVisible = false
            };

            _graphCanvas.Children.Add(polygon);

            var centerX = groupPoints.Average(p => p.X);
            var centerY = groupPoints.Average(p => p.Y);

            var label = new TextBlock
            {
                Text = group.Key ?? string.Empty,
                Foreground = new SolidColorBrush(Color.Parse("#6E675A")),
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                IsHitTestVisible = false
            };

            label.Measure(Size.Infinity);
            Canvas.SetLeft(label, centerX - label.DesiredSize.Width / 2.0);
            Canvas.SetTop(label, centerY - label.DesiredSize.Height / 2.0);
            _graphCanvas.Children.Add(label);
        }
    }
    private Color GetGroupFillColor(List<SimulationGraphNodeDto> nodes)
    {
        if (nodes.Any(n => n.IsBurning))
            return Color.FromArgb(60, 255, 122, 89);

        if (nodes.All(n => n.IsBurned))
            return Color.FromArgb(45, 120, 120, 120);

        return Color.FromArgb(45, 142, 124, 195);
    }

    private Color GetGroupStrokeColor(List<SimulationGraphNodeDto> nodes)
    {
        if (nodes.Any(n => n.IsBurning))
            return Color.FromArgb(150, 214, 64, 43);

        if (nodes.All(n => n.IsBurned))
            return Color.FromArgb(120, 110, 110, 110);

        return Color.FromArgb(120, 110, 103, 90);
    }
    private void DrawEdge(
     SimulationGraphEdgeDto edge,
     Dictionary<Guid, Point> points,
     Guid? selectedNodeId,
     HashSet<Guid> selectedEdgeIds,
     bool isBridge)
    {
        if (_graphCanvas == null)
            return;

        if (!points.TryGetValue(edge.FromCellId, out var fromPoint) ||
            !points.TryGetValue(edge.ToCellId, out var toPoint))
            return;

        bool isSelectedEdge = selectedEdgeIds.Contains(edge.Id);

        double thickness = GetEdgeThickness(edge);
        double opacity = edge.IsCorridor
            ? 0.96
            : isBridge
                ? 0.58
                : 0.72;

        if (selectedNodeId.HasValue)
        {
            if (isSelectedEdge)
            {
                thickness += edge.IsCorridor ? 1.4 : 1.2;
                opacity = 1.0;
            }
            else
            {
                thickness = Math.Max(0.9, thickness - 0.2);
                opacity = edge.IsCorridor
                    ? 0.34
                    : isBridge
                        ? 0.16
                        : 0.18;
            }
        }

        var strokeColor = edge.IsCorridor
            ? GetBridgeColor(edge)
            : (isBridge ? GetBridgeColor(edge) : GetEdgeColor(edge));

        var line = new Line
        {
            StartPoint = fromPoint,
            EndPoint = toPoint,
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = thickness,
            Opacity = opacity,
            IsHitTestVisible = true
        };

        ToolTip.SetTip(line, BuildEdgeTooltip(edge, isBridge));
        _graphCanvas.Children.Add(line);
    }

    private string BuildEdgeTooltip(SimulationGraphEdgeDto edge, bool isBridge)
    {
        string edgeType = edge.IsCorridor
            ? "Corridor edge"
            : isBridge
                ? "Межкластерное ребро"
                : "Локальное ребро";

        string corridorText = edge.IsCorridor ? "yes" : "no";

        return $"{edgeType}\n" +
               $"От: ({edge.FromX}, {edge.FromY})\n" +
               $"К: ({edge.ToX}, {edge.ToY})\n" +
               $"Расстояние: {edge.Distance:F3}\n" +
               $"Уклон: {edge.Slope:F6}\n" +
               $"Fire spread modifier: {edge.FireSpreadModifier:F6}\n" +
               $"Corridor: {corridorText}";
    }
    private void DrawNode(
        SimulationGraphNodeDto node,
        List<SimulationGraphEdgeDto> edges,
        Dictionary<Guid, Point> points,
        Guid? selectedNodeId,
        HashSet<Guid> neighborIds,
        bool useMapStyle)
    {
        if (_graphCanvas == null)
            return;

        if (!points.TryGetValue(node.Id, out var point))
            return;

        bool isSelected = selectedNodeId.HasValue && node.Id == selectedNodeId.Value;
        bool isNeighbor = !isSelected && neighborIds.Contains(node.Id);
        bool isIgnitionSelected = node.IsSelectedIgnition;

        double opacity = 1.0;
        if (selectedNodeId.HasValue && !isSelected && !isNeighbor)
            opacity = 0.44;

        double radius = GetNodeRadius(node, useMapStyle, isSelected, isNeighbor, isIgnitionSelected);

        if (isIgnitionSelected)
            DrawIgnitionGlow(point, radius, useMapStyle);

        var shape = new Rectangle
        {
            Width = radius * 2,
            Height = radius * 2,
            RadiusX = useMapStyle ? 3.0 : radius,
            RadiusY = useMapStyle ? 3.0 : radius,
            Fill = new SolidColorBrush(
                isIgnitionSelected
                    ? Color.Parse("#9EC5FE")
                    : (useMapStyle ? GetMapCellColor(node) : GetNodeColor(node))),
            Stroke = new SolidColorBrush(
                isIgnitionSelected
                    ? Color.Parse("#D6402B")
                    : isSelected
                        ? Color.Parse("#355CBE")
                        : isNeighbor
                            ? Color.Parse("#B45A4A")
                            : GetNodeBorderColor(node)),
            StrokeThickness = isIgnitionSelected ? 2.6 : isSelected ? 2.2 : isNeighbor ? 1.8 : GetNodeBorderThickness(node),
            Opacity = opacity,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        ToolTip.SetTip(shape, BuildTooltip(node, edges));

        shape.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(shape).Properties.IsLeftButtonPressed)
            {
                NodeClicked?.Invoke(this, node);
                e.Handled = true;
            }
        };

        Canvas.SetLeft(shape, point.X - radius);
        Canvas.SetTop(shape, point.Y - radius);
        _graphCanvas.Children.Add(shape);

        DrawVegetationMarker(node, point, radius, opacity);
    }

    private double GetNodeRadius(
        SimulationGraphNodeDto node,
        bool useMapStyle,
        bool isSelected,
        bool isNeighbor,
        bool isIgnitionSelected)
    {
        int nodeCount = Nodes?.Count() ?? 0;

        double baseRadius = nodeCount switch
        {
            <= 10 => 11.0,
            <= 20 => 10.0,
            <= 60 => 8.8,
            <= 120 => 7.8,
            <= 200 => 7.0,
            _ => 6.5
        };

        if (useMapStyle)
            baseRadius -= 0.6;

        double radius = isSelected
            ? Math.Max(baseRadius + 2.2, SelectedNodeRadius)
            : isNeighbor
                ? Math.Max(baseRadius + 0.8, NeighborNodeRadius - 0.6)
                : baseRadius;

        if (isIgnitionSelected)
            radius = Math.Max(radius, useMapStyle ? 10.5 : 11.5);

        return radius;
    }

    private Color GetNodeBorderColor(SimulationGraphNodeDto node)
    {
        return node.Vegetation?.ToLowerInvariant() switch
        {
            "water" => Color.Parse("#2E79B8"),
            "bare" => Color.Parse("#8C7462"),
            _ => Color.Parse("#F8F5EE")
        };
    }

    private double GetNodeBorderThickness(SimulationGraphNodeDto node)
    {
        return node.Vegetation?.ToLowerInvariant() switch
        {
            "water" => 1.8,
            "bare" => 1.8,
            _ => 1.2
        };
    }

    private void DrawVegetationMarker(
        SimulationGraphNodeDto node,
        Point point,
        double radius,
        double opacity)
    {
        if (_graphCanvas == null)
            return;

        var vegetation = node.Vegetation?.ToLowerInvariant();

        if (vegetation == "water")
        {
            double markerRadius = Math.Max(2.6, radius * 0.30);

            var inner = new Ellipse
            {
                Width = markerRadius * 2,
                Height = markerRadius * 2,
                Fill = new SolidColorBrush(Color.Parse("#1E5F99")),
                Opacity = opacity,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(inner, point.X - markerRadius);
            Canvas.SetTop(inner, point.Y - markerRadius);
            _graphCanvas.Children.Add(inner);
            return;
        }

        if (vegetation == "bare")
        {
            double size = Math.Max(4.0, radius * 0.9);
            double half = size / 2.0;

            var line1 = new Line
            {
                StartPoint = new Point(point.X - half, point.Y - half),
                EndPoint = new Point(point.X + half, point.Y + half),
                Stroke = new SolidColorBrush(Color.Parse("#6D594B")),
                StrokeThickness = 1.4,
                Opacity = opacity,
                IsHitTestVisible = false
            };

            var line2 = new Line
            {
                StartPoint = new Point(point.X - half, point.Y + half),
                EndPoint = new Point(point.X + half, point.Y - half),
                Stroke = new SolidColorBrush(Color.Parse("#6D594B")),
                StrokeThickness = 1.4,
                Opacity = opacity,
                IsHitTestVisible = false
            };

            _graphCanvas.Children.Add(line1);
            _graphCanvas.Children.Add(line2);
        }
    }

    private void DrawIgnitionGlow(Point point, double radius, bool useMapStyle)
    {
        if (_graphCanvas == null)
            return;

        var glowRadius = radius + 3.5;

        var glow = new Ellipse
        {
            Width = glowRadius * 2,
            Height = glowRadius * 2,
            Fill = new SolidColorBrush(Color.FromArgb(80, 255, 235, 120)),
            Stroke = new SolidColorBrush(Color.Parse("#D6402B")),
            StrokeThickness = useMapStyle ? 1.8 : 2.0,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(glow, point.X - glowRadius);
        Canvas.SetTop(glow, point.Y - glowRadius);
        _graphCanvas.Children.Add(glow);
    }

    private List<Point> BuildSoftHull(List<Point> points)
    {
        if (points.Count < 3)
            return points;

        var expanded = new List<Point>();

        foreach (var p in points)
        {
            expanded.Add(new Point(p.X - 18, p.Y));
            expanded.Add(new Point(p.X + 18, p.Y));
            expanded.Add(new Point(p.X, p.Y - 18));
            expanded.Add(new Point(p.X, p.Y + 18));
            expanded.Add(new Point(p.X - 10, p.Y - 10));
            expanded.Add(new Point(p.X + 10, p.Y - 10));
            expanded.Add(new Point(p.X - 10, p.Y + 10));
            expanded.Add(new Point(p.X + 10, p.Y + 10));
        }

        return ComputeConvexHull(expanded);
    }

    private List<Point> ComputeConvexHull(List<Point> points)
    {
        var sorted = points
            .Distinct()
            .OrderBy(p => p.X)
            .ThenBy(p => p.Y)
            .ToList();

        if (sorted.Count <= 1)
            return sorted;

        var lower = new List<Point>();
        foreach (var p in sorted)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
                lower.RemoveAt(lower.Count - 1);

            lower.Add(p);
        }

        var upper = new List<Point>();
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            var p = sorted[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
                upper.RemoveAt(upper.Count - 1);

            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);

        return lower.Concat(upper).ToList();
    }

    private double Cross(Point a, Point b, Point c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    }

    private (double MinX, double MaxX, double MinY, double MaxY) GetNodeBounds(List<SimulationGraphNodeDto> nodes)
    {
        var minX = nodes.Min(n => n.RenderX);
        var maxX = nodes.Max(n => n.RenderX);
        var minY = nodes.Min(n => n.RenderY);
        var maxY = nodes.Max(n => n.RenderY);

        if (Math.Abs(maxX - minX) < 0.001)
            maxX = minX + 1.0;

        if (Math.Abs(maxY - minY) < 0.001)
            maxY = minY + 1.0;

        return (minX, maxX, minY, maxY);
    }

    private HashSet<Guid> GetSelectedEdgeIds(List<SimulationGraphEdgeDto> edges, Guid? selectedNodeId)
    {
        var result = new HashSet<Guid>();

        if (!selectedNodeId.HasValue)
            return result;

        foreach (var edge in edges)
        {
            if (edge.FromCellId == selectedNodeId.Value || edge.ToCellId == selectedNodeId.Value)
                result.Add(edge.Id);
        }

        return result;
    }

    private HashSet<Guid> GetNeighborIds(List<SimulationGraphEdgeDto> edges, Guid? selectedNodeId)
    {
        var result = new HashSet<Guid>();

        if (!selectedNodeId.HasValue)
            return result;

        foreach (var edge in edges)
        {
            if (edge.FromCellId == selectedNodeId.Value)
                result.Add(edge.ToCellId);
            else if (edge.ToCellId == selectedNodeId.Value)
                result.Add(edge.FromCellId);
        }

        return result;
    }

    private double GetEdgeThickness(SimulationGraphEdgeDto edge)
    {
        double thickness = BaseEdgeThickness;

        if (edge.FireSpreadModifier >= 0.70)
            thickness += 1.0;
        else if (edge.FireSpreadModifier >= 0.35)
            thickness += 0.3;

        if (edge.IsCorridor)
            thickness += 1.4;

        return thickness;
    }

    private Color GetEdgeColor(SimulationGraphEdgeDto edge)
    {
        if (edge.IsCorridor)
            return Color.Parse("#C2410C");

        if (edge.FireSpreadModifier >= 0.70)
            return Color.Parse("#9D4EDD");

        if (edge.FireSpreadModifier >= 0.35)
            return Color.Parse("#B8A1D9");

        return Color.Parse("#D8D2E6");
    }

    private Color GetBridgeColor(SimulationGraphEdgeDto edge)
    {
        if (edge.IsCorridor)
            return Color.Parse("#EA580C");

        if (edge.FireSpreadModifier >= 0.70)
            return Color.Parse("#C05621");

        if (edge.FireSpreadModifier >= 0.35)
            return Color.Parse("#D97706");

        return Color.Parse("#E5A45E");
    }

    private Color GetNodeColor(SimulationGraphNodeDto node)
    {
        if (node.IsBurning)
            return Color.Parse("#FF7A59");

        if (node.IsBurned)
            return Color.Parse("#777777");

        return node.Vegetation?.ToLowerInvariant() switch
        {
            "coniferous" => Color.Parse("#5E9B5E"),
            "deciduous" => Color.Parse("#8ACB88"),
            "mixed" => Color.Parse("#A8C97F"),
            "grass" => Color.Parse("#E7D36F"),
            "shrub" => Color.Parse("#CFA46A"),
            "water" => Color.Parse("#7CC6F2"),
            "bare" => Color.Parse("#C9B7A7"),
            _ => Color.Parse("#D9D5CF")
        };
    }

    private Color GetMapCellColor(SimulationGraphNodeDto node)
    {
        return GetNodeColor(node);
    }

    private string BuildTooltip(SimulationGraphNodeDto node, List<SimulationGraphEdgeDto> edges)
    {
        int degree = edges.Count(e => e.FromCellId == node.Id || e.ToCellId == node.Id);

        return $"Вершина ({node.X}, {node.Y})\n" +
               $"Группа: {node.GroupKey}\n" +
               $"Растительность: {node.Vegetation}\n" +
               $"Состояние: {node.State}\n" +
               $"Влажность: {node.Moisture:F2}\n" +
               $"Высота: {node.Elevation:F2}\n" +
               $"Вероятность возгорания: {node.BurnProbability:F3}\n" +
               $"Степень: {degree}\n" +
               $"Fire stage: {node.FireStage}\n" +
               $"Intensity: {node.FireIntensity:F2}\n" +
               $"Fuel: {node.CurrentFuelLoad:F2}/{node.FuelLoad:F2}\n" +
               $"Accumulated heat: {node.AccumulatedHeatJ:F2}";
    }

    private void DrawEmptyState()
    {
        if (_graphCanvas == null)
            return;

        _graphCanvas.Width = MinCanvasSize;
        _graphCanvas.Height = MinCanvasSize;

        var text = new TextBlock
        {
            Text = "Нет данных для отображения графа",
            Foreground = new SolidColorBrush(Color.Parse("#8A8495")),
            FontSize = 16,
            FontWeight = FontWeight.SemiBold
        };

        text.Measure(Size.Infinity);

        Canvas.SetLeft(text, (_graphCanvas.Width - text.DesiredSize.Width) / 2.0);
        Canvas.SetTop(text, (_graphCanvas.Height - text.DesiredSize.Height) / 2.0);

        _graphCanvas.Children.Add(text);
    }
}