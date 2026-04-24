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

        foreach (var edge in edges.OrderBy(e => IsCrossClusterEdge(e, nodeMap) ? 1 : 0))
            DrawEdge(edge, points, nodeMap, selectedNodeId, selectedEdgeIds);

        foreach (var node in nodes.OrderBy(n => n.IsBurning ? 1 : 0))
            DrawNode(node, edges, points, selectedNodeId, neighborIds);
    }

    private bool IsCrossClusterEdge(
      SimulationGraphEdgeDto edge,
      Dictionary<Guid, SimulationGraphNodeDto> nodeMap)
    {
        return nodeMap.TryGetValue(edge.FromCellId, out var fromNode) &&
               nodeMap.TryGetValue(edge.ToCellId, out var toNode) &&
               !string.IsNullOrWhiteSpace(fromNode.GroupKey) &&
               !string.IsNullOrWhiteSpace(toNode.GroupKey) &&
               !string.Equals(fromNode.GroupKey, toNode.GroupKey, StringComparison.Ordinal);
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
            var groupPoints = group
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
                Points = new AvaloniaList<Point>(hull),
                Fill = new SolidColorBrush(GetClusterTint(colorIndex), 0.12),
                Stroke = new SolidColorBrush(GetClusterTint(colorIndex), 0.35),
                StrokeThickness = 1.2,
                IsHitTestVisible = false
            };

            _graphCanvas.Children.Add(polygon);
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
            return;

        bool isSelected = selectedEdgeIds.Contains(edge.Id);
        bool isIncidentToSelection =
            selectedNodeId.HasValue &&
            (edge.FromCellId == selectedNodeId.Value || edge.ToCellId == selectedNodeId.Value);

        bool isCrossCluster = IsCrossClusterEdge(edge, nodeMap);

        var line = new Line
        {
            StartPoint = fromPoint,
            EndPoint = toPoint,
            Stroke = new SolidColorBrush(GetEffectiveEdgeColor(edge, isCrossCluster, isSelected, isIncidentToSelection)),
            StrokeThickness = GetEffectiveEdgeThickness(edge, isCrossCluster, isSelected, isIncidentToSelection),
            Opacity = GetEffectiveEdgeOpacity(edge, isCrossCluster, isSelected, isIncidentToSelection),
            ZIndex = isSelected ? 5 : isIncidentToSelection ? 4 : isCrossCluster ? 2 : 1,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        if (isCrossCluster && !isSelected && !isIncidentToSelection)
            line.StrokeDashArray = new AvaloniaList<double> { 6, 5 };

        ToolTip.SetTip(line, BuildEdgeTooltip(edge, nodeMap));
        _graphCanvas.Children.Add(line);
    }

    private void DrawAreaTransitionAccent(
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
            Stroke = new SolidColorBrush(isSelected ? Color.Parse("#5B3CC4") : Color.Parse("#8E7CC3")),
            StrokeThickness = isSelected ? 2.2 : 1.4,
            Opacity = isSelected ? 0.95 : 0.55,
            ZIndex = isSelected ? 7 : 5,
            StrokeDashArray = new AvaloniaList<double> { 6, 5 },
            IsHitTestVisible = false
        };

        _graphCanvas.Children.Add(accent);
    }



    private void DrawCorridorAccent(
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
                Text = "КОРИДОР",
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
    }

    private string BuildTooltip(SimulationGraphNodeDto node, List<SimulationGraphEdgeDto> edges)
    {
        int degree = edges.Count(e => e.FromCellId == node.Id || e.ToCellId == node.Id);

        int transitionEdges = edges.Count(e =>
        {
            if (e.FromCellId != node.Id && e.ToCellId != node.Id)
                return false;

            var otherNodeId = e.FromCellId == node.Id ? e.ToCellId : e.FromCellId;
            var otherNode = Nodes?.FirstOrDefault(n => n.Id == otherNodeId);

            if (otherNode == null)
                return false;

            return !string.IsNullOrWhiteSpace(node.GroupKey) &&
                   !string.IsNullOrWhiteSpace(otherNode.GroupKey) &&
                   !string.Equals(node.GroupKey, otherNode.GroupKey, StringComparison.Ordinal);
        });

        string groupText = string.IsNullOrWhiteSpace(node.GroupKey)
            ? "Область: —"
            : $"Область: {node.GroupKey}";

        return
            $"Вершина ({node.X}, {node.Y})\n" +
            $"Состояние: {GetStateText(node.State)}\n" +
            $"Растительность: {GetVegetationText(node.Vegetation)}\n" +
            $"{groupText}\n" +
            $"Связей: {degree}, переходов в другие области: {transitionEdges}\n" +
            $"Влажность: {node.Moisture:F2}\n" +
            $"Высота: {node.Elevation:F1}\n" +
            $"Стадия: {GetFireStageText(node.FireStage)}\n" +
            $"Интенсивность: {node.FireIntensity:F2}\n" +
            $"Топливо: {node.CurrentFuelLoad:F2} / {node.FuelLoad:F2}\n" +
            $"Накопленное тепло: {node.AccumulatedHeatJ:F2}";
    }

    private string BuildEdgeTooltip(
     SimulationGraphEdgeDto edge,
     Dictionary<Guid, SimulationGraphNodeDto> nodeMap)
    {
        string fromText = BuildEdgeEndpointText(edge.FromCellId, edge.FromX, edge.FromY, nodeMap);
        string toText = BuildEdgeEndpointText(edge.ToCellId, edge.ToX, edge.ToY, nodeMap);

        bool isAreaTransition =
            nodeMap.TryGetValue(edge.FromCellId, out var fromNode) &&
            nodeMap.TryGetValue(edge.ToCellId, out var toNode) &&
            !string.IsNullOrWhiteSpace(fromNode.GroupKey) &&
            !string.IsNullOrWhiteSpace(toNode.GroupKey) &&
            !string.Equals(fromNode.GroupKey, toNode.GroupKey, StringComparison.Ordinal);

        return
            $"Связь\n" +
            $"От: {fromText}\n" +
            $"До: {toText}\n" +
            $"Тип: {(isAreaTransition ? "переход между областями" : "внутренняя связь области")}\n" +
            $"Расстояние: {edge.Distance:F2}\n" +
            $"Уклон: {edge.Slope:F3}\n" +
            $"Модификатор распространения: {edge.FireSpreadModifier:F3}\n" +
            $"Накопленное тепло: {edge.AccumulatedHeat:F2}";
    }


    private string BuildEdgeEndpointText(
     Guid nodeId,
     int x,
     int y,
     Dictionary<Guid, SimulationGraphNodeDto> nodeMap)
    {
        if (nodeMap.TryGetValue(nodeId, out var node))
        {
            string group = string.IsNullOrWhiteSpace(node.GroupKey)
                ? "без области"
                : $"область {node.GroupKey}";

            return $"({x}, {y}), {group}";
        }

        return $"({x}, {y})";
    }
    private void DrawEmptyState()
    {
        if (_graphCanvas == null)
            return;

        _graphCanvas.Width = 700;
        _graphCanvas.Height = 420;

        var text = new TextBlock
        {
            Text = "Граф пока не загружен",
            Foreground = new SolidColorBrush(Color.Parse("#8A8495")),
            FontSize = 15,
            FontWeight = FontWeight.SemiBold
        };

        Canvas.SetLeft(text, 260);
        Canvas.SetTop(text, 190);
        _graphCanvas.Children.Add(text);
    }

    private (double MinX, double MaxX, double MinY, double MaxY) GetNodeBounds(List<SimulationGraphNodeDto> nodes)
    {
        return
        (
            nodes.Min(n => n.RenderX),
            nodes.Max(n => n.RenderX),
            nodes.Min(n => n.RenderY),
            nodes.Max(n => n.RenderY)
        );
    }

    private HashSet<Guid> GetSelectedEdgeIds(List<SimulationGraphEdgeDto> edges, Guid? selectedNodeId)
    {
        if (!selectedNodeId.HasValue)
            return new HashSet<Guid>();

        return edges
            .Where(e => e.FromCellId == selectedNodeId.Value || e.ToCellId == selectedNodeId.Value)
            .Select(e => e.Id)
            .ToHashSet();
    }

    private HashSet<Guid> GetNeighborIds(List<SimulationGraphEdgeDto> edges, Guid? selectedNodeId)
    {
        if (!selectedNodeId.HasValue)
            return new HashSet<Guid>();

        var result = new HashSet<Guid>();

        foreach (var edge in edges)
        {
            if (edge.FromCellId == selectedNodeId.Value)
                result.Add(edge.ToCellId);
            else if (edge.ToCellId == selectedNodeId.Value)
                result.Add(edge.FromCellId);
        }

        return result;
    }

    private List<Point> BuildSoftHull(List<Point> points)
    {
        double minX = points.Min(p => p.X) - 22;
        double maxX = points.Max(p => p.X) + 22;
        double minY = points.Min(p => p.Y) - 18;
        double maxY = points.Max(p => p.Y) + 18;

        return new List<Point>
        {
            new(minX, minY),
            new(maxX, minY),
            new(maxX, maxY),
            new(minX, maxY)
        };
    }

    private Color GetClusterTint(int index)
    {
        Color[] palette =
        {
            Color.Parse("#8E7CC3"),
            Color.Parse("#6FA8DC"),
            Color.Parse("#93C47D"),
            Color.Parse("#E6B8AF"),
            Color.Parse("#FFD966")
        };

        return palette[index % palette.Length];
    }

    private Color GetNodeColor(SimulationGraphNodeDto node)
    {
        if (node.IsSelectedIgnition)
            return Color.Parse("#F8D27A");

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
            _ => Color.Parse("#A8C97F")
        };
    }

    private Color GetNodeStrokeColor(
        SimulationGraphNodeDto node,
        bool isSelected,
        bool isNeighbor,
        bool isIgnition)
    {
        if (isSelected)
            return Color.Parse("#5B3CC4");

        if (isIgnition)
            return Color.Parse("#D97706");

        if (isNeighbor)
            return Color.Parse("#8E7CC3");

        if (node.IsBurning)
            return Color.Parse("#D6402B");

        return Color.Parse("#6F6A78");
    }

    private Color GetEffectiveEdgeColor(
     SimulationGraphEdgeDto edge,
     bool isCrossCluster,
     bool isSelected,
     bool isIncidentToSelection)
    {
        if (isSelected)
            return Color.Parse("#5B3CC4");

        if (isIncidentToSelection)
            return Color.Parse("#8E7CC3");

        if (isCrossCluster)
            return Color.Parse("#D9A441");

        return Color.Parse("#B8B2C5");
    }
    private double GetEffectiveEdgeThickness(
    SimulationGraphEdgeDto edge,
    bool isCrossCluster,
    bool isSelected,
    bool isIncidentToSelection)
    {
        if (isSelected)
            return 3.0;

        if (isIncidentToSelection)
            return 2.1;

        if (isCrossCluster)
            return 1.35;

        return BaseEdgeThickness;
    }

    private double GetEffectiveEdgeOpacity(
    SimulationGraphEdgeDto edge,
    bool isCrossCluster,
    bool isSelected,
    bool isIncidentToSelection)
    {
        if (isSelected)
            return 1.0;

        if (isIncidentToSelection)
            return 0.9;

        if (isCrossCluster)
            return 0.58;

        return 0.55;
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

    private string GetFireStageText(string? stage)
    {
        return stage switch
        {
            "Unburned" => "Не горела",
            "Ignition" => "Воспламенение",
            "Active" => "Активное горение",
            "Intense" => "Интенсивное горение",
            "Smoldering" => "Тление",
            "BurnedOut" => "Выгорела",
            null or "" => "—",
            _ => stage
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
}