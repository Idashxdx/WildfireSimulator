using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
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
    private const double SelectedNodeRadius = 11.5;
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
        DrawClusterLabels(nodes, points);

        foreach (var edge in edges)
        {
            DrawEdge(edge, points, nodeMap, selectedNodeId, selectedEdgeIds);
        }

        foreach (var node in nodes.OrderBy(n => n.IsBurning ? 0 : 1))
        {
            DrawNode(node, edges, points, selectedNodeId, neighborIds);
        }
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

        if (groups.Count == 0)
            return;

        int colorIndex = 0;

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
                Points = new Avalonia.Collections.AvaloniaList<Point>(hull),
                Fill = new SolidColorBrush(GetClusterTint(colorIndex), 0.12),
                Stroke = new SolidColorBrush(GetClusterTint(colorIndex), 0.35),
                StrokeThickness = 1.2,
                IsHitTestVisible = false
            };

            _graphCanvas.Children.Add(polygon);
            colorIndex++;
        }
    }

    private void DrawClusterLabels(
        List<SimulationGraphNodeDto> nodes,
        Dictionary<Guid, Point> points)
    {
        if (_graphCanvas == null)
            return;

        var groups = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.GroupKey))
            .GroupBy(n => n.GroupKey)
            .OrderBy(g => g.Key)
            .ToList();

        int colorIndex = 0;

        foreach (var group in groups)
        {
            var groupNodes = group
                .Where(n => points.ContainsKey(n.Id))
                .ToList();

            if (groupNodes.Count == 0)
                continue;

            double centerX = groupNodes.Average(n => points[n.Id].X);
            double minY = groupNodes.Min(n => points[n.Id].Y);

            var labelBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 255, 253, 248)),
                BorderBrush = new SolidColorBrush(GetClusterTint(colorIndex)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4),
                Child = new TextBlock
                {
                    Text = group.Key ?? "cluster",
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(GetClusterTint(colorIndex))
                },
                IsHitTestVisible = false
            };

            labelBorder.Measure(Size.Infinity);

            Canvas.SetLeft(labelBorder, centerX - labelBorder.DesiredSize.Width / 2.0);
            Canvas.SetTop(labelBorder, minY - 28.0);

            _graphCanvas.Children.Add(labelBorder);
            colorIndex++;
        }
    }

    private void DrawEdge(
     SimulationGraphEdgeDto edge,
     Dictionary<Guid, Point> points,
     Dictionary<Guid, SimulationGraphNodeDto> nodeMap,
     Guid? selectedNodeId,
     HashSet<Guid> selectedEdgeIds)
    {
        if (_graphCanvas == null)
            return;

        if (!points.TryGetValue(edge.FromCellId, out var fromPoint) ||
            !points.TryGetValue(edge.ToCellId, out var toPoint))
        {
            return;
        }

        bool isSelected = selectedEdgeIds.Contains(edge.Id);
        bool isIncidentToSelection =
            selectedNodeId.HasValue &&
            (edge.FromCellId == selectedNodeId.Value || edge.ToCellId == selectedNodeId.Value);

        bool isCrossCluster =
            nodeMap.TryGetValue(edge.FromCellId, out var fromNode) &&
            nodeMap.TryGetValue(edge.ToCellId, out var toNode) &&
            !string.IsNullOrWhiteSpace(fromNode.GroupKey) &&
            !string.IsNullOrWhiteSpace(toNode.GroupKey) &&
            !string.Equals(fromNode.GroupKey, toNode.GroupKey, StringComparison.Ordinal);

        var line = new Line
        {
            StartPoint = fromPoint,
            EndPoint = toPoint,
            Stroke = new SolidColorBrush(GetEffectiveEdgeColor(edge, isCrossCluster, isSelected, isIncidentToSelection)),
            StrokeThickness = GetEffectiveEdgeThickness(edge, isSelected, isIncidentToSelection),
            Opacity = GetEffectiveEdgeOpacity(edge, isSelected, isIncidentToSelection),
            ZIndex = edge.IsCorridor ? 4 : isSelected ? 5 : 2,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        ToolTip.SetTip(line, BuildEdgeTooltip(edge, nodeMap));
        _graphCanvas.Children.Add(line);

        if (edge.IsCorridor)
        {
            DrawCorridorAccent(edge, fromPoint, toPoint, isSelected);
        }
    }

    private void DrawCorridorAccent(
     SimulationGraphEdgeDto edge,
     Point fromPoint,
     Point toPoint,
     bool isSelected)
    {
        if (_graphCanvas == null)
            return;

        var accent = new Line
        {
            StartPoint = fromPoint,
            EndPoint = toPoint,
            Stroke = new SolidColorBrush(isSelected ? Color.Parse("#EA580C") : Color.Parse("#FDBA74")),
            StrokeThickness = isSelected ? 2.2 : 1.2,
            Opacity = 0.95,
            ZIndex = 6,
            StrokeDashArray = new AvaloniaList<double> { 6, 4 },
            IsHitTestVisible = false
        };

        _graphCanvas.Children.Add(accent);

        var centerX = (fromPoint.X + toPoint.X) / 2.0;
        var centerY = (fromPoint.Y + toPoint.Y) / 2.0;

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(235, 255, 247, 237)),
            BorderBrush = new SolidColorBrush(Color.Parse("#EA580C")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(6, 2),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = "CORRIDOR",
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#C2410C"))
            }
        };

        badge.Measure(Size.Infinity);

        Canvas.SetLeft(badge, centerX - badge.DesiredSize.Width / 2.0);
        Canvas.SetTop(badge, centerY - badge.DesiredSize.Height / 2.0 - 12.0);

        _graphCanvas.Children.Add(badge);
    }

    private void DrawNode(
     SimulationGraphNodeDto node,
     List<SimulationGraphEdgeDto> edges,
     Dictionary<Guid, Point> points,
     Guid? selectedNodeId,
     HashSet<Guid> neighborIds)
    {
        if (_graphCanvas == null)
            return;

        if (!points.TryGetValue(node.Id, out var point))
            return;

        bool isSelected = selectedNodeId.HasValue && node.Id == selectedNodeId.Value;
        bool isNeighbor = neighborIds.Contains(node.Id);
        bool isIgnition = node.IsSelectedIgnition;

        double radius = BaseNodeRadius;

        if (isSelected)
            radius = SelectedNodeRadius;
        else if (isNeighbor)
            radius = NeighborNodeRadius;
        else if (isIgnition)
            radius = BaseNodeRadius + 1.4;

        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = new SolidColorBrush(GetNodeColor(node)),
            Stroke = new SolidColorBrush(GetNodeStrokeColor(node, isSelected, isNeighbor, isIgnition)),
            StrokeThickness = isSelected ? 3.0 : isIgnition ? 2.6 : isNeighbor ? 2.1 : 1.4,
            ZIndex = isSelected ? 12 : isIgnition ? 11 : isNeighbor ? 10 : 8,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        ToolTip.SetTip(ellipse, BuildTooltip(node, edges));

        ellipse.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(ellipse).Properties.IsLeftButtonPressed)
                return;

            NodeClicked?.Invoke(this, node);
            e.Handled = true;
        };

        Canvas.SetLeft(ellipse, point.X - radius);
        Canvas.SetTop(ellipse, point.Y - radius);
        _graphCanvas.Children.Add(ellipse);

        if (isIgnition)
        {
            var ignitionRing = new Ellipse
            {
                Width = radius * 2 + 8,
                Height = radius * 2 + 8,
                Stroke = new SolidColorBrush(Color.Parse("#D97706")),
                StrokeThickness = 1.8,
                StrokeDashArray = new AvaloniaList<double> { 4, 3 },
                Opacity = 0.95,
                ZIndex = 9,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(ignitionRing, point.X - ignitionRing.Width / 2.0);
            Canvas.SetTop(ignitionRing, point.Y - ignitionRing.Height / 2.0);
            _graphCanvas.Children.Add(ignitionRing);
        }

        if (isSelected || isNeighbor)
        {
            var glow = new Ellipse
            {
                Width = radius * 2 + (isSelected ? 10 : 6),
                Height = radius * 2 + (isSelected ? 10 : 6),
                Stroke = new SolidColorBrush(isSelected ? Color.Parse("#5B3CC4") : Color.Parse("#8E7CC3")),
                StrokeThickness = isSelected ? 1.8 : 1.2,
                Opacity = isSelected ? 0.65 : 0.35,
                ZIndex = 7,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(glow, point.X - glow.Width / 2.0);
            Canvas.SetTop(glow, point.Y - glow.Height / 2.0);
            _graphCanvas.Children.Add(glow);
        }

        if (!string.IsNullOrWhiteSpace(node.GroupKey))
        {
            var label = new TextBlock
            {
                Text = BuildCompactNodeLabel(node),
                FontSize = 10,
                FontWeight = isSelected ? FontWeight.Bold : FontWeight.Medium,
                Foreground = new SolidColorBrush(Color.Parse("#4A4458")),
                Background = new SolidColorBrush(Color.FromArgb(225, 255, 253, 248))
            };

            label.Measure(Size.Infinity);

            Canvas.SetLeft(label, point.X + radius + 4);
            Canvas.SetTop(label, point.Y - label.DesiredSize.Height / 2.0);
            _graphCanvas.Children.Add(label);
        }
    }
    private string BuildCompactNodeLabel(SimulationGraphNodeDto node)
    {
        if (node.IsSelectedIgnition)
            return $"({node.X},{node.Y}) • ignition";

        if (node.IsBurning)
            return $"({node.X},{node.Y}) • fire";

        if (node.IsBurned)
            return $"({node.X},{node.Y}) • burned";

        return $"({node.X},{node.Y})";
    }
    private string BuildTooltip(SimulationGraphNodeDto node, List<SimulationGraphEdgeDto> edges)
    {
        int degree = edges.Count(e => e.FromCellId == node.Id || e.ToCellId == node.Id);
        int corridorEdges = edges.Count(e =>
            (e.FromCellId == node.Id || e.ToCellId == node.Id) &&
            e.IsCorridor);

        return
            $"Node: {node.Id}\n" +
            $"Coords: ({node.X}, {node.Y})\n" +
            $"Render: ({node.RenderX:F2}, {node.RenderY:F2})\n" +
            $"Cluster: {GetSafeText(node.GroupKey)}\n" +
            $"Vegetation: {GetVegetationText(node.Vegetation)}\n" +
            $"Moisture: {node.Moisture:F2}\n" +
            $"Elevation: {node.Elevation:F2}\n" +
            $"State: {GetStateText(node.State)}\n" +
            $"FireStage: {GetFireStageText(node.FireStage)}\n" +
            $"BurnProbability: {node.BurnProbability:F3}\n" +
            $"FireIntensity: {node.FireIntensity:F2}\n" +
            $"Fuel: {node.CurrentFuelLoad:F2} / {node.FuelLoad:F2}\n" +
            $"AccumulatedHeat: {node.AccumulatedHeatJ:F2}\n" +
            $"BurningElapsed: {node.BurningElapsedSeconds:F0} s\n" +
            $"Degree: {degree}\n" +
            $"Corridor edges: {corridorEdges}\n" +
            $"Ignition selected: {(node.IsSelectedIgnition ? "true" : "false")}";
    }
    private void DrawHeatHalo(Point point, double radius)
    {
        if (_graphCanvas == null)
            return;

        var glowRadius = radius + 5.5;

        var glow = new Ellipse
        {
            Width = glowRadius * 2,
            Height = glowRadius * 2,
            Fill = new SolidColorBrush(Color.FromArgb(60, 255, 122, 89)),
            Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 122, 89)),
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
            ZIndex = 12
        };

        Canvas.SetLeft(glow, point.X - glowRadius);
        Canvas.SetTop(glow, point.Y - glowRadius);
        _graphCanvas.Children.Add(glow);
    }

    private void DrawNodeLabel(SimulationGraphNodeDto node, Point point, double radius)
    {
        if (_graphCanvas == null)
            return;

        string text = !string.IsNullOrWhiteSpace(node.GroupKey)
            ? $"{node.GroupKey}"
            : $"({node.X},{node.Y})";

        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(228, 255, 253, 248)),
            BorderBrush = new SolidColorBrush(Color.Parse("#D8D2E6")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(6, 2),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#4F4A5E")),
                FontWeight = FontWeight.SemiBold
            }
        };

        label.Measure(Size.Infinity);

        Canvas.SetLeft(label, point.X - label.DesiredSize.Width / 2.0);
        Canvas.SetTop(label, point.Y + radius + 6.0);
        _graphCanvas.Children.Add(label);
    }

    private bool ShouldDrawNodeLabel(
        SimulationGraphNodeDto node,
        bool isSelected,
        bool isIgnitionSelected)
    {
        if (isSelected || isIgnitionSelected)
            return true;

        var nodeCount = Nodes?.Count() ?? 0;
        return nodeCount <= 35;
    }

    private double GetNodeRadius(
        SimulationGraphNodeDto node,
        bool isSelected,
        bool isNeighbor,
        bool isIgnitionSelected)
    {
        double radius = BaseNodeRadius;

        if (isSelected)
            radius = SelectedNodeRadius;
        else if (isNeighbor)
            radius = NeighborNodeRadius;

        if (node.Vegetation?.Equals("Water", StringComparison.OrdinalIgnoreCase) == true ||
            node.Vegetation?.Equals("Bare", StringComparison.OrdinalIgnoreCase) == true)
        {
            radius += 0.6;
        }

        if (isIgnitionSelected)
            radius = Math.Max(radius, 11.5);

        return radius;
    }

    private Color GetNodeStrokeColor(
     SimulationGraphNodeDto node,
     bool isSelected,
     bool isNeighbor,
     bool isIgnition)
    {
        if (isSelected)
            return Color.Parse("#4C1D95");

        if (isIgnition)
            return Color.Parse("#D97706");

        if (isNeighbor)
            return Color.Parse("#7C6DB3");

        if (node.IsBurning)
            return Color.Parse("#C2410C");

        if (node.IsBurned)
            return Color.Parse("#4B5563");

        return Color.Parse("#6F6A78");
    }

    private double GetNodeStrokeThickness(
        bool isSelected,
        bool isNeighbor,
        bool isIgnitionSelected)
    {
        if (isSelected)
            return 2.8;

        if (isIgnitionSelected)
            return 2.4;

        if (isNeighbor)
            return 2.0;

        return 1.3;
    }

    private void DrawVegetationMarker(
        SimulationGraphNodeDto node,
        Point point,
        double radius)
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
                IsHitTestVisible = false,
                ZIndex = 17
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
                StrokeThickness = 1.5,
                IsHitTestVisible = false,
                ZIndex = 17
            };

            var line2 = new Line
            {
                StartPoint = new Point(point.X - half, point.Y + half),
                EndPoint = new Point(point.X + half, point.Y - half),
                Stroke = new SolidColorBrush(Color.Parse("#6D594B")),
                StrokeThickness = 1.5,
                IsHitTestVisible = false,
                ZIndex = 17
            };

            _graphCanvas.Children.Add(line1);
            _graphCanvas.Children.Add(line2);
        }
    }

    private void DrawIgnitionGlow(Point point, double radius)
    {
        if (_graphCanvas == null)
            return;

        var glowRadius = radius + 3.8;

        var glow = new Ellipse
        {
            Width = glowRadius * 2,
            Height = glowRadius * 2,
            Fill = new SolidColorBrush(Color.FromArgb(72, 255, 235, 120)),
            Stroke = new SolidColorBrush(Color.Parse("#D6402B")),
            StrokeThickness = 2.0,
            IsHitTestVisible = false,
            ZIndex = 13
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

    private double GetEffectiveEdgeThickness(
      SimulationGraphEdgeDto edge,
      bool isSelected,
      bool isIncidentToSelection)
    {
        double thickness = BaseEdgeThickness;

        if (edge.FireSpreadModifier >= 0.70)
            thickness += 1.0;
        else if (edge.FireSpreadModifier >= 0.35)
            thickness += 0.35;

        if (edge.IsCorridor)
            thickness += 1.6;

        if (isIncidentToSelection)
            thickness += 0.6;

        if (isSelected)
            thickness += 0.8;

        return thickness;
    }

    private double GetEffectiveEdgeOpacity(
     SimulationGraphEdgeDto edge,
     bool isSelected,
     bool isIncidentToSelection)
    {
        if (isSelected)
            return 1.0;

        if (isIncidentToSelection)
            return 0.92;

        if (edge.IsCorridor)
            return 0.96;

        return 0.78;
    }

    private Color GetEffectiveEdgeColor(
     SimulationGraphEdgeDto edge,
     bool isCrossCluster,
     bool isSelected,
     bool isIncidentToSelection)
    {
        if (edge.IsCorridor)
            return isSelected
                ? Color.Parse("#C2410C")
                : Color.Parse("#EA580C");

        if (isSelected)
            return Color.Parse("#5B3CC4");

        if (isIncidentToSelection)
            return Color.Parse("#7C6DB3");

        if (isCrossCluster)
            return GetBridgeColor(edge);

        return GetEdgeColor(edge);
    }

    private Color GetEdgeColor(SimulationGraphEdgeDto edge)
    {
        if (edge.FireSpreadModifier >= 0.70)
            return Color.Parse("#9D4EDD");

        if (edge.FireSpreadModifier >= 0.35)
            return Color.Parse("#B8A1D9");

        return Color.Parse("#D8D2E6");
    }

    private Color GetBridgeColor(SimulationGraphEdgeDto edge)
    {
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

    private Color GetClusterTint(int index)
    {
        Color[] palette =
        {
        Color.Parse("#7C6DB3"),
        Color.Parse("#4F9D69"),
        Color.Parse("#D97706"),
        Color.Parse("#2E79B8"),
        Color.Parse("#C05621"),
        Color.Parse("#8E7CC3")
    };

        return palette[index % palette.Length];
    }

    private string BuildNodeTooltip(
        SimulationGraphNodeDto node,
        List<SimulationGraphEdgeDto> edges)
    {
        int degree = edges.Count(e => e.FromCellId == node.Id || e.ToCellId == node.Id);
        int corridorEdges = edges.Count(e =>
            (e.FromCellId == node.Id || e.ToCellId == node.Id) && e.IsCorridor);

        return
            $"ID: {node.Id}\n" +
            $"Координаты: ({node.X}, {node.Y})\n" +
            $"Render: ({node.RenderX:F2}, {node.RenderY:F2})\n" +
            $"Cluster: {GetSafeText(node.GroupKey)}\n" +
            $"Растительность: {GetVegetationText(node.Vegetation)}\n" +
            $"Состояние: {GetStateText(node.State)}\n" +
            $"Влажность: {node.Moisture:F2}\n" +
            $"Высота: {node.Elevation:F2}\n" +
            $"Вероятность возгорания: {node.BurnProbability:F3}\n" +
            $"Fire stage: {GetFireStageText(node.FireStage)}\n" +
            $"Fire intensity: {node.FireIntensity:F2}\n" +
            $"Топливо: {node.CurrentFuelLoad:F2} / {node.FuelLoad:F2}\n" +
            $"Burning elapsed: {node.BurningElapsedSeconds:F1} сек\n" +
            $"Accumulated heat: {node.AccumulatedHeatJ:F2}\n" +
            $"Степень: {degree}\n" +
            $"Corridor-рёбра: {corridorEdges}\n" +
            $"Ignition selected: {(node.IsSelectedIgnition ? "Да" : "Нет")}";
    }

    private string BuildEdgeTooltip(
        SimulationGraphEdgeDto edge,
        Dictionary<Guid, SimulationGraphNodeDto> nodeMap)
    {
        string fromText = BuildEdgeEndpointText(edge.FromCellId, edge.FromX, edge.FromY, nodeMap);
        string toText = BuildEdgeEndpointText(edge.ToCellId, edge.ToX, edge.ToY, nodeMap);

        return
            $"Ребро: {edge.Id}\n" +
            $"From: {fromText}\n" +
            $"To: {toText}\n" +
            $"Distance: {edge.Distance:F2}\n" +
            $"Slope: {edge.Slope:F3}\n" +
            $"FireSpreadModifier: {edge.FireSpreadModifier:F3}\n" +
            $"AccumulatedHeat: {edge.AccumulatedHeat:F2}\n" +
            $"Corridor: {(edge.IsCorridor ? "true" : "false")}";
    }

    private string BuildEdgeEndpointText(
     Guid nodeId,
     int x,
     int y,
     Dictionary<Guid, SimulationGraphNodeDto> nodeMap)
    {
        if (nodeMap.TryGetValue(nodeId, out var node))
        {
            string cluster = string.IsNullOrWhiteSpace(node.GroupKey) ? "—" : node.GroupKey;
            return $"{nodeId} • ({x}, {y}) • {cluster}";
        }

        return $"{nodeId} • ({x}, {y})";
    }

    private string GetSafeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private string GetStateText(string? state)
    {
        return state switch
        {
            "Burning" => "Горит",
            "Burned" => "Сгорела",
            "Normal" => "Нормальная",
            null or "" => "—",
            _ => state
        };
    }

    private string GetVegetationText(string? vegetation)
    {
        return vegetation switch
        {
            "Coniferous" => "Хвойный лес",
            "Deciduous" => "Лиственный лес",
            "Mixed" => "Смешанный лес",
            "Grass" => "Трава",
            "Shrub" => "Кустарник",
            "Water" => "Вода",
            "Bare" => "Пустая поверхность",
            null or "" => "—",
            _ => vegetation
        };
    }

    private string GetFireStageText(string? fireStage)
    {
        return fireStage switch
        {
            "Unburned" => "Не горела",
            "Ignition" => "Воспламенение",
            "Active" => "Активное горение",
            "Intense" => "Интенсивное горение",
            "Smoldering" => "Тление",
            "BurnedOut" => "Полностью выгорела",
            null or "" => "—",
            _ => fireStage
        };
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