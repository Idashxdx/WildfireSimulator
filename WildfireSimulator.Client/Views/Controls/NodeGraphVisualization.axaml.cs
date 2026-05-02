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
    private bool _hasManualZoom;
    private double _zoom = 1.0;

    private const double MinZoom = 0.25;
    private const double MaxZoom = 2.80;
    private const double ZoomStep = 0.16;

    private const double BaseNodeRadius = 8.5;
    private const double SelectedNodeRadius = 11.5;
    private const double NeighborNodeRadius = 9.5;

    private const double BaseEdgeThickness = 1.4;
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
    public event EventHandler? BackgroundClicked;

    public NodeGraphVisualization()
    {
        InitializeComponent();

        _graphCanvas = this.FindControl<Canvas>("GraphCanvas");
        _scrollHost = this.FindControl<ScrollViewer>("ScrollHost");

        _zoomInButton = this.FindControl<Button>("ZoomInButton");
        _zoomOutButton = this.FindControl<Button>("ZoomOutButton");
        _resetZoomButton = this.FindControl<Button>("ResetZoomButton");

        if (_graphCanvas != null)
            _graphCanvas.PointerPressed += OnCanvasPointerPressed;

        if (_zoomInButton != null)
            _zoomInButton.Click += (_, _) => ChangeZoom(ZoomStep);

        if (_zoomOutButton != null)
            _zoomOutButton.Click += (_, _) => ChangeZoom(-ZoomStep);

        if (_resetZoomButton != null)
            _resetZoomButton.Click += (_, _) => ResetZoom();

        PropertyChanged += OnGraphPropertyChanged;
        AttachedToVisualTree += (_, _) => ScheduleDraw();

        if (_scrollHost != null)
        {
            _scrollHost.SizeChanged += (_, _) =>
            {
                if (!_hasManualZoom)
                    ScheduleDraw();
            };
        }
    }
    public static readonly StyledProperty<SimulationGraphEdgeDto?> SelectedEdgeProperty =
    AvaloniaProperty.Register<NodeGraphVisualization, SimulationGraphEdgeDto?>(nameof(SelectedEdge));

    public SimulationGraphEdgeDto? SelectedEdge
    {
        get => GetValue(SelectedEdgeProperty);
        set => SetValue(SelectedEdgeProperty, value);
    }

    public event EventHandler<SimulationGraphEdgeDto>? EdgeClicked;

    public static readonly StyledProperty<double> PrecipitationProperty =
        AvaloniaProperty.Register<NodeGraphVisualization, double>(nameof(Precipitation), 0.0);

    public static readonly StyledProperty<double> WindDirectionDegreesProperty =
        AvaloniaProperty.Register<NodeGraphVisualization, double>(nameof(WindDirectionDegrees), 45.0);

    public static readonly StyledProperty<int> CurrentStepProperty =
        AvaloniaProperty.Register<NodeGraphVisualization, int>(nameof(CurrentStep), 0);

    public static readonly StyledProperty<int> StepDurationSecondsProperty =
        AvaloniaProperty.Register<NodeGraphVisualization, int>(nameof(StepDurationSeconds), 900);

    public double Precipitation
    {
        get => GetValue(PrecipitationProperty);
        set => SetValue(PrecipitationProperty, value);
    }

    public double WindDirectionDegrees
    {
        get => GetValue(WindDirectionDegreesProperty);
        set => SetValue(WindDirectionDegreesProperty, value);
    }

    public int CurrentStep
    {
        get => GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    public int StepDurationSeconds
    {
        get => GetValue(StepDurationSecondsProperty);
        set => SetValue(StepDurationSecondsProperty, value);
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_graphCanvas == null)
            return;

        var properties = e.GetCurrentPoint(_graphCanvas).Properties;

        if (!properties.IsLeftButtonPressed)
            return;

        if (!ReferenceEquals(e.Source, _graphCanvas))
            return;

        BackgroundClicked?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ChangeZoom(double delta)
    {
        double factor = delta > 0 ? 1.18 : 1.0 / 1.18;

        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        _hasManualZoom = true;

        ScheduleDraw();
    }

    private void ResetZoom()
    {
        _hasManualZoom = false;
        ScheduleDraw();
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
    }

    private void DrawStandardGraph(List<SimulationGraphNodeDto> nodes, List<SimulationGraphEdgeDto> edges)
    {
        if (_graphCanvas == null)
            return;

        var bounds = GetNodeBounds(nodes);
        double graphScale = GetStandardGraphScale(nodes, bounds);

        double logicalWidth = (bounds.MaxX - bounds.MinX) * graphScale + CanvasPadding * 2;
        double logicalHeight = (bounds.MaxY - bounds.MinY) * graphScale + CanvasPadding * 2;

        if (!_hasManualZoom)
            _zoom = CalculateFitZoom(logicalWidth, logicalHeight);

        double scaledWidth = logicalWidth * _zoom;
        double scaledHeight = logicalHeight * _zoom;

        _graphCanvas.Width = scaledWidth;
        _graphCanvas.Height = scaledHeight;
        _graphCanvas.MinWidth = scaledWidth;
        _graphCanvas.MinHeight = scaledHeight;

        double scaledPadding = CanvasPadding * _zoom;
        double scaledGraphScale = graphScale * _zoom;

        var points = nodes.ToDictionary(
            n => n.Id,
            n => new Point(
                scaledPadding + (n.RenderX - bounds.MinX) * scaledGraphScale,
                scaledPadding + (n.RenderY - bounds.MinY) * scaledGraphScale));

        var nodeMap = nodes.ToDictionary(n => n.Id);

        var selectedNodeId = SelectedNode?.Id;
        var selectedEdgeId = SelectedEdge?.Id;
        var selectedEdgeIds = GetSelectedEdgeIds(edges, selectedNodeId);
        var neighborIds = GetNeighborIds(edges, selectedNodeId);

        DrawClusterPatchBackgrounds(nodes, points, _zoom);

        foreach (var edge in edges.OrderBy(e => IsBridgeEdge(e, nodeMap) ? 1 : 0))
            DrawEdge(edge, points, nodeMap, selectedNodeId, selectedEdgeId, selectedEdgeIds, _zoom);

        foreach (var node in nodes.OrderBy(n => n.IsBurning ? 1 : 0))
            DrawNode(node, edges, points, selectedNodeId, neighborIds, _zoom);
    }
    private void OnGraphPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == NodesProperty)
        {
            _hasManualZoom = false;
            RebindNodesCollection();
            ScheduleDraw();
            return;
        }

        if (e.Property == EdgesProperty)
        {
            _hasManualZoom = false;
            RebindEdgesCollection();
            ScheduleDraw();
            return;
        }

        if (e.Property == LayoutHintProperty ||
            e.Property == IsIgnitionSelectionEnabledProperty ||
            e.Property == PrecipitationProperty ||
            e.Property == WindDirectionDegreesProperty ||
            e.Property == CurrentStepProperty ||
            e.Property == StepDurationSecondsProperty)
        {
            ScheduleDraw();
            return;
        }

        if (e.Property == SelectedNodeProperty ||
            e.Property == SelectedEdgeProperty)
        {
            ScheduleDraw();
        }
    }
    private void DrawMovingPrecipitationFront(
        List<SimulationGraphNodeDto> nodes,
        Dictionary<Guid, Point> points,
        double zoom)
    {
        if (_graphCanvas == null || Precipitation <= 0.0 || CurrentStep <= 0 || nodes.Count == 0)
            return;

        var band = BuildGraphPrecipitationFrontPolygon(nodes, points);

        if (band.Count < 4)
            return;

        double opacity = Math.Clamp(0.12 + Precipitation * 0.018, 0.14, 0.40);

        var polygon = new Polygon
        {
            Points = new Avalonia.Collections.AvaloniaList<Point>(band),
            Fill = new SolidColorBrush(Color.Parse("#5DADEC"), opacity),
            Stroke = new SolidColorBrush(Color.Parse("#2F80C0"), Math.Min(0.55, opacity + 0.12)),
            StrokeThickness = Math.Max(1.0, 1.4 * zoom),
            IsHitTestVisible = false,
            ZIndex = 5
        };

        _graphCanvas.Children.Add(polygon);
    }

    private List<Point> BuildGraphPrecipitationFrontPolygon(
        List<SimulationGraphNodeDto> nodes,
        Dictionary<Guid, Point> points)
    {
        double minX = points.Values.Min(p => p.X);
        double maxX = points.Values.Max(p => p.X);
        double minY = points.Values.Min(p => p.Y);
        double maxY = points.Values.Max(p => p.Y);

        double width = Math.Max(1.0, maxX - minX);
        double height = Math.Max(1.0, maxY - minY);

        double centerX = minX + width / 2.0;
        double centerY = minY + height / 2.0;

        double diagonal = Math.Sqrt(width * width + height * height);

        double frontLength = Math.Max(160.0, diagonal * 1.35);
        double frontThickness = Math.Max(70.0, diagonal * 0.24);

        var moveDirection = GetPrecipitationFlowDirection(WindDirectionDegrees);

        double bandX = -moveDirection.Y;
        double bandY = moveDirection.X;

        double modelTimeSeconds =
            Math.Max(0, CurrentStep - 1) * Math.Max(1, StepDurationSeconds);

        double speedPixelsPerSecond =
            0.018 + 5.0 * 0.0028;

        speedPixelsPerSecond = Math.Clamp(speedPixelsPerSecond, 0.018, 0.075);

        double travelDistance = diagonal + frontThickness * 2.0;

        double position =
            (modelTimeSeconds * speedPixelsPerSecond) % travelDistance
            - diagonal / 2.0
            - frontThickness;

        double frontCenterX = centerX + moveDirection.X * position;
        double frontCenterY = centerY + moveDirection.Y * position;

        var p1 = new Point(
            frontCenterX + bandX * frontLength / 2.0 + moveDirection.X * frontThickness / 2.0,
            frontCenterY + bandY * frontLength / 2.0 + moveDirection.Y * frontThickness / 2.0);

        var p2 = new Point(
            frontCenterX - bandX * frontLength / 2.0 + moveDirection.X * frontThickness / 2.0,
            frontCenterY - bandY * frontLength / 2.0 + moveDirection.Y * frontThickness / 2.0);

        var p3 = new Point(
            frontCenterX - bandX * frontLength / 2.0 - moveDirection.X * frontThickness / 2.0,
            frontCenterY - bandY * frontLength / 2.0 - moveDirection.Y * frontThickness / 2.0);

        var p4 = new Point(
            frontCenterX + bandX * frontLength / 2.0 - moveDirection.X * frontThickness / 2.0,
            frontCenterY + bandY * frontLength / 2.0 - moveDirection.Y * frontThickness / 2.0);

        return new List<Point> { p1, p2, p3, p4 };
    }

    private (double X, double Y) GetPrecipitationFlowDirection(double windDirectionDegrees)
    {
        double flowDirectionDegrees = (windDirectionDegrees + 180.0) % 360.0;
        double radians = flowDirectionDegrees * Math.PI / 180.0;

        double x = Math.Sin(radians);
        double y = -Math.Cos(radians);

        double length = Math.Sqrt(x * x + y * y);

        if (length < 0.0001)
            return (0.0, 1.0);

        return (x / length, y / length);
    }


    private double CalculateFitZoom(double logicalWidth, double logicalHeight)
    {
        if (_scrollHost == null || logicalWidth <= 0)
            return 1.0;

        double availableWidth = _scrollHost.Bounds.Width;

        if (availableWidth <= 0)
            availableWidth = 680;

        double zoomByWidth = (availableWidth - 24) / logicalWidth;

        return Math.Clamp(zoomByWidth, MinZoom, MaxZoom);
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

    private bool IsBridgeEdge(
        SimulationGraphEdgeDto edge,
        Dictionary<Guid, SimulationGraphNodeDto> nodeMap)
    {
        if (edge.IsCorridor)
            return true;

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
     Dictionary<Guid, Point> points,
     double zoom)
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

            var hull = BuildSoftHull(groupPoints, zoom);
            if (hull.Count < 3)
                continue;

            var polygon = new Polygon
            {
                Points = new AvaloniaList<Point>(hull),
                Fill = new SolidColorBrush(GetClusterTint(colorIndex), 0.12),
                Stroke = new SolidColorBrush(GetClusterTint(colorIndex), 0.35),
                StrokeThickness = Math.Max(0.8, 1.2 * zoom),
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
     Guid? selectedEdgeId,
     HashSet<Guid> selectedEdgeIds,
     double zoom)
    {
        if (_graphCanvas == null)
            return;

        if (!points.TryGetValue(edge.FromCellId, out var fromPoint) ||
            !points.TryGetValue(edge.ToCellId, out var toPoint))
        {
            return;
        }

        bool isBridge = IsBridgeEdge(edge, nodeMap);
        bool isSelectedEdge = selectedEdgeId.HasValue && edge.Id == selectedEdgeId.Value;
        bool isIncidentToSelectedNode = selectedEdgeIds.Contains(edge.Id);

        var line = new Line
        {
            StartPoint = fromPoint,
            EndPoint = toPoint,
            Stroke = new SolidColorBrush(GetEffectiveEdgeColor(edge, isBridge, isSelectedEdge, isIncidentToSelectedNode)),
            StrokeThickness = Math.Max(0.8, GetEffectiveEdgeThickness(edge, isBridge, isSelectedEdge, isIncidentToSelectedNode) * zoom),
            Opacity = GetEffectiveEdgeOpacity(edge, isBridge, isSelectedEdge, isIncidentToSelectedNode),
            ZIndex = isSelectedEdge ? 9 : isIncidentToSelectedNode ? 7 : isBridge ? 6 : 4,
            Cursor = new Cursor(StandardCursorType.Hand),
            StrokeDashArray = isBridge
                ? new AvaloniaList<double> { 7 * zoom, 5 * zoom }
                : null
        };

        ToolTip.SetTip(line, BuildEdgeTooltip(edge, nodeMap));

        line.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(line).Properties.IsLeftButtonPressed)
                return;

            EdgeClicked?.Invoke(this, edge);
            e.Handled = true;
        };

        _graphCanvas.Children.Add(line);
    }
    private void DrawNode(
     SimulationGraphNodeDto node,
     List<SimulationGraphEdgeDto> edges,
     Dictionary<Guid, Point> points,
     Guid? selectedNodeId,
     HashSet<Guid> neighborIds,
     double zoom)
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

        radius *= zoom;

        if (isSelected || isNeighbor)
        {
            double glowExtra = (isSelected ? 10 : 6) * zoom;

            var glow = new Ellipse
            {
                Width = radius * 2 + glowExtra,
                Height = radius * 2 + glowExtra,
                Stroke = new SolidColorBrush(isSelected ? Color.Parse("#5B3CC4") : Color.Parse("#8E7CC3")),
                StrokeThickness = Math.Max(0.8, (isSelected ? 1.8 : 1.2) * zoom),
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
            StrokeThickness = Math.Max(0.7, (isSelected ? 3.0 : isIgnition ? 2.6 : isNeighbor ? 2.1 : 1.4) * zoom),
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
        DrawPrecipitationOverlay(node, point, radius, zoom);

        if (isIgnition)
        {
            double ringExtra = 8 * zoom;

            var ignitionRing = new Ellipse
            {
                Width = radius * 2 + ringExtra,
                Height = radius * 2 + ringExtra,
                Stroke = new SolidColorBrush(Color.Parse("#D97706")),
                StrokeThickness = Math.Max(0.8, 1.8 * zoom),
                StrokeDashArray = new AvaloniaList<double> { 4 * zoom, 3 * zoom },
                Opacity = 0.95,
                ZIndex = 9,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(ignitionRing, point.X - ignitionRing.Width / 2.0);
            Canvas.SetTop(ignitionRing, point.Y - ignitionRing.Height / 2.0);
            _graphCanvas.Children.Add(ignitionRing);
        }
    }
    private void DrawPrecipitationOverlay(
    SimulationGraphNodeDto node,
    Point point,
    double radius,
    double zoom)
    {
        if (_graphCanvas == null)
            return;

        if (node.PrecipitationIntensity <= 0.001)
            return;

        double rainLevel = Math.Clamp(node.PrecipitationIntensity / 100.0, 0.0, 1.0);
        double overlayRadius = radius + (4.0 + rainLevel * 7.0) * zoom;

        byte alpha = (byte)Math.Clamp(45 + rainLevel * 135, 45, 180);

        var rainCircle = new Ellipse
        {
            Width = overlayRadius * 2,
            Height = overlayRadius * 2,
            Fill = new SolidColorBrush(Color.FromArgb(alpha, 70, 170, 255)),
            Stroke = new SolidColorBrush(Color.FromArgb(190, 35, 120, 220)),
            StrokeThickness = Math.Max(0.8, (0.9 + rainLevel * 1.2) * zoom),
            ZIndex = 13,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(rainCircle, point.X - overlayRadius);
        Canvas.SetTop(rainCircle, point.Y - overlayRadius);

        _graphCanvas.Children.Add(rainCircle);
    }

    private string BuildTooltip(
      SimulationGraphNodeDto node,
      List<SimulationGraphEdgeDto> edges)
    {
        int degree = edges.Count(e => e.FromCellId == node.Id || e.ToCellId == node.Id);

        int bridgeEdges = edges.Count(edge =>
        {
            if (edge.FromCellId != node.Id && edge.ToCellId != node.Id)
                return false;

            var otherNodeId = edge.FromCellId == node.Id
                ? edge.ToCellId
                : edge.FromCellId;

            var otherNode = Nodes?.FirstOrDefault(n => n.Id == otherNodeId);

            return otherNode != null &&
                   !string.IsNullOrWhiteSpace(node.GroupKey) &&
                   !string.IsNullOrWhiteSpace(otherNode.GroupKey) &&
                   !string.Equals(node.GroupKey, otherNode.GroupKey, StringComparison.Ordinal);
        });

        string groupText = string.IsNullOrWhiteSpace(node.GroupKey)
            ? "Область: не задана"
            : $"Область: {node.GroupKey}";

        return
            $"Вершина ({node.X}, {node.Y})\n" +
            $"Состояние: {GetStateText(node.State)}\n" +
            $"Тип поверхности: {GetVegetationText(node.Vegetation)}\n" +
            $"{groupText}\n" +
            $"Связей: {degree}, мостов в другие области: {bridgeEdges}\n" +
            $"Влажность: {node.Moisture:F2}\n" +
            $"Высота: {node.Elevation:F1} м\n" +
            $"Стадия пожара: {GetFireStageText(node.FireStage)}\n" +
            $"Интенсивность горения: {node.FireIntensity:F2}\n" +
            $"Остаток топлива: {node.CurrentFuelLoad:F2} / {node.FuelLoad:F2}\n" +
            $"Накопленное тепло: {node.AccumulatedHeatJ:F2} Дж";
    }
    private string BuildEdgeTooltip(
     SimulationGraphEdgeDto edge,
     Dictionary<Guid, SimulationGraphNodeDto> nodeMap)
    {
        string fromText = BuildEdgeEndpointText(edge.FromCellId, edge.FromX, edge.FromY, nodeMap);
        string toText = BuildEdgeEndpointText(edge.ToCellId, edge.ToX, edge.ToY, nodeMap);

        bool isBridge = IsBridgeEdge(edge, nodeMap);

        return
            $"Связь графа\n" +
            $"От: {fromText}\n" +
            $"До: {toText}\n" +
            $"Тип: {(isBridge ? "мост между областями" : "связь внутри области")}\n" +
            $"Расстояние: {edge.Distance:F2}\n" +
            $"Уклон: {edge.Slope:F3}\n" +
            $"Сила передачи огня: {edge.FireSpreadModifier:F3}\n" +
            $"Накопленное тепло на связи: {edge.AccumulatedHeat:F2}";
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

            return $"вершина ({x}, {y}), {group}";
        }

        return $"вершина ({x}, {y})";
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

    private List<Point> BuildSoftHull(List<Point> points, double zoom)
    {
        double horizontalPadding = 22 * zoom;
        double verticalPadding = 18 * zoom;

        double minX = points.Min(p => p.X) - horizontalPadding;
        double maxX = points.Max(p => p.X) + horizontalPadding;
        double minY = points.Min(p => p.Y) - verticalPadding;
        double maxY = points.Max(p => p.Y) + verticalPadding;

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
     bool isBridge,
     bool isSelected,
     bool isIncidentToSelection)
    {
        if (isSelected)
            return Color.Parse("#5B3CC4");

        if (isIncidentToSelection)
            return Color.Parse("#8E7CC3");

        if (isBridge)
            return Color.Parse("#4DA6FF");

        return Color.Parse("#B8B2C5");
    }

    private double GetEffectiveEdgeThickness(
     SimulationGraphEdgeDto edge,
     bool isBridge,
     bool isSelected,
     bool isIncidentToSelection)
    {
        if (isSelected)
            return 3.0;

        if (isIncidentToSelection)
            return 2.1;

        if (isBridge)
            return 2.0;

        return BaseEdgeThickness;
    }

    private double GetEffectiveEdgeOpacity(
        SimulationGraphEdgeDto edge,
        bool isBridge,
        bool isSelected,
        bool isIncidentToSelection)
    {
        if (isSelected)
            return 1.0;

        if (isIncidentToSelection)
            return 0.9;

        if (isBridge)
            return 0.82;

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