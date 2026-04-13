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

    private const double RegionClusterInternalScale = 16.0;
    private const double RegionClusterPadding = 24.0;
    private const double RegionClusterMinGap = 28.0;

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

        if (LayoutHint.Contains("region-cluster", StringComparison.OrdinalIgnoreCase))
            DrawRegionClusterMap(nodes, edges);
        else
            DrawStandardGraph(nodes, edges);

        ApplyZoom();
    }

    private void DrawStandardGraph(List<SimulationGraphNodeDto> nodes, List<SimulationGraphEdgeDto> edges)
    {
        if (_graphCanvas == null)
            return;

        var bounds = GetNodeBounds(nodes);

        const double scale = 28.0;

        var width = Math.Max(MinCanvasSize, (bounds.MaxX - bounds.MinX) * scale + CanvasPadding * 2);
        var height = Math.Max(MinCanvasSize, (bounds.MaxY - bounds.MinY) * scale + CanvasPadding * 2);

        _graphCanvas.Width = width;
        _graphCanvas.Height = height;

        var points = nodes.ToDictionary(
            n => n.Id,
            n => new Point(
                CanvasPadding + (n.RenderX - bounds.MinX) * scale,
                CanvasPadding + (n.RenderY - bounds.MinY) * scale));

        var selectedNodeId = SelectedNode?.Id;
        var selectedEdgeIds = GetSelectedEdgeIds(edges, selectedNodeId);
        var neighborIds = GetNeighborIds(edges, selectedNodeId);

        foreach (var edge in edges)
            DrawEdge(edge, points, selectedNodeId, selectedEdgeIds, isBridge: false);

        foreach (var node in nodes.OrderBy(n => n.IsBurning ? 0 : 1))
            DrawNode(node, edges, points, selectedNodeId, neighborIds, useMapStyle: false);
    }

    private void DrawRegionClusterMap(List<SimulationGraphNodeDto> nodes, List<SimulationGraphEdgeDto> edges)
    {
        if (_graphCanvas == null)
            return;

        var groups = nodes
            .GroupBy(n => string.IsNullOrWhiteSpace(n.GroupKey) ? "region-0" : n.GroupKey)
            .OrderBy(g => g.Key)
            .ToList();

        if (groups.Count == 0)
        {
            DrawStandardGraph(nodes, edges);
            return;
        }

        var nodeMap = nodes.ToDictionary(n => n.Id);
        var placedRegions = BuildPlacedRegions(groups, edges, nodeMap);
        var points = BuildRegionClusterScreenPoints(placedRegions);

        var selectedNodeId = SelectedNode?.Id;
        var selectedEdgeIds = GetSelectedEdgeIds(edges, selectedNodeId);
        var neighborIds = GetNeighborIds(edges, selectedNodeId);

        var size = MeasurePlacedRegions(placedRegions);

        _graphCanvas.Width = Math.Max(MinCanvasSize, size.Width);
        _graphCanvas.Height = Math.Max(MinCanvasSize, size.Height);

        foreach (var region in placedRegions)
            DrawRegionBackground(region, points);

        var edgesToDraw = SelectRegionClusterEdgesForDrawing(edges, nodeMap, selectedNodeId);

        foreach (var edge in edgesToDraw.Where(e => IsInterRegionEdge(e, nodeMap)))
            DrawEdge(edge, points, selectedNodeId, selectedEdgeIds, isBridge: true);

        foreach (var edge in edgesToDraw.Where(e => !IsInterRegionEdge(e, nodeMap)))
            DrawEdge(edge, points, selectedNodeId, selectedEdgeIds, isBridge: false);

        foreach (var region in placedRegions)
        {
            foreach (var node in region.Nodes.OrderBy(n => n.IsBurning ? 0 : 1).ThenBy(n => n.Y).ThenBy(n => n.X))
                DrawNode(node, edges, points, selectedNodeId, neighborIds, useMapStyle: true);

            DrawRegionLabel(region);
        }
    }

    private List<PlacedRegion> BuildPlacedRegions(
        List<IGrouping<string, SimulationGraphNodeDto>> groups,
        List<SimulationGraphEdgeDto> edges,
        Dictionary<Guid, SimulationGraphNodeDto> nodeMap)
    {
        var regions = groups
            .Select(group =>
            {
                var nodes = group.ToList();
                var bounds = GetModelBounds(nodes);

                double width = Math.Max(3.0, bounds.MaxX - bounds.MinX + 1.0);
                double height = Math.Max(3.0, bounds.MaxY - bounds.MinY + 1.0);

                double centerX = nodes.Average(n => n.RenderX);
                double centerY = nodes.Average(n => n.RenderY);

                return new PlacedRegion(
                    group.Key,
                    nodes,
                    bounds.MinX,
                    bounds.MinY,
                    bounds.MaxX,
                    bounds.MaxY,
                    width,
                    height,
                    centerX,
                    centerY);
            })
            .ToList();

        var regionGraph = BuildRegionGraph(regions, edges, nodeMap);

        PlaceRegionsFromModelGeography(regions);
        RefineRegionClusterPositions(regions, regionGraph);

        return regions;
    }

    private Dictionary<string, Dictionary<string, double>> BuildRegionGraph(
        List<PlacedRegion> regions,
        List<SimulationGraphEdgeDto> edges,
        Dictionary<Guid, SimulationGraphNodeDto> nodeMap)
    {
        var result = regions.ToDictionary(r => r.RegionId, _ => new Dictionary<string, double>());

        foreach (var edge in edges)
        {
            if (!nodeMap.TryGetValue(edge.FromCellId, out var fromNode) ||
                !nodeMap.TryGetValue(edge.ToCellId, out var toNode))
                continue;

            var fromRegion = string.IsNullOrWhiteSpace(fromNode.GroupKey) ? "region-0" : fromNode.GroupKey;
            var toRegion = string.IsNullOrWhiteSpace(toNode.GroupKey) ? "region-0" : toNode.GroupKey;

            if (fromRegion == toRegion)
                continue;

            if (!result[fromRegion].ContainsKey(toRegion))
                result[fromRegion][toRegion] = 0.0;

            if (!result[toRegion].ContainsKey(fromRegion))
                result[toRegion][fromRegion] = 0.0;

            double weight = Math.Max(0.20, edge.FireSpreadModifier);
            result[fromRegion][toRegion] += weight;
            result[toRegion][fromRegion] += weight;
        }

        return result;
    }

    private void PlaceRegionsFromModelGeography(List<PlacedRegion> regions)
    {
        if (regions.Count == 0)
            return;

        double minCenterX = regions.Min(r => r.ModelCenterX);
        double maxCenterX = regions.Max(r => r.ModelCenterX);
        double minCenterY = regions.Min(r => r.ModelCenterY);
        double maxCenterY = regions.Max(r => r.ModelCenterY);

        if (Math.Abs(maxCenterX - minCenterX) < 0.001)
            maxCenterX = minCenterX + 1.0;

        if (Math.Abs(maxCenterY - minCenterY) < 0.001)
            maxCenterY = minCenterY + 1.0;

        double averageRegionWidthPx = regions.Average(r => r.Width * RegionClusterInternalScale + RegionClusterPadding * 2);
        double averageRegionHeightPx = regions.Average(r => r.Height * RegionClusterInternalScale + RegionClusterPadding * 2);

        double xRange = maxCenterX - minCenterX;
        double yRange = maxCenterY - minCenterY;

        double scaleX = xRange > 0.0
            ? averageRegionWidthPx / Math.Max(2.8, xRange / Math.Sqrt(regions.Count))
            : averageRegionWidthPx;

        double scaleY = yRange > 0.0
            ? averageRegionHeightPx / Math.Max(2.6, yRange / Math.Sqrt(regions.Count))
            : averageRegionHeightPx;

        double mapScale = Math.Max(14.0, Math.Min(34.0, Math.Min(scaleX, scaleY)));

        foreach (var region in regions)
        {
            double targetCenterX = CanvasPadding + (region.ModelCenterX - minCenterX) * mapScale;
            double targetCenterY = CanvasPadding + (region.ModelCenterY - minCenterY) * mapScale;

            region.TargetCenterX = targetCenterX;
            region.TargetCenterY = targetCenterY;

            double localCenterOffsetX = RegionClusterPadding + (region.ModelCenterX - region.MinModelX) * RegionClusterInternalScale;
            double localCenterOffsetY = RegionClusterPadding + (region.ModelCenterY - region.MinModelY) * RegionClusterInternalScale;

            region.ScreenOriginX = targetCenterX - localCenterOffsetX;
            region.ScreenOriginY = targetCenterY - localCenterOffsetY;
        }

        ShiftRegionsToPositiveSpace(regions);
    }

    private void RefineRegionClusterPositions(
        List<PlacedRegion> regions,
        Dictionary<string, Dictionary<string, double>> regionGraph)
    {
        if (regions.Count <= 1)
            return;

        var regionById = regions.ToDictionary(r => r.RegionId);

        for (int iteration = 0; iteration < 32; iteration++)
        {
            foreach (var region in regions)
            {
                double currentCenterX = GetRegionCenterX(region);
                double currentCenterY = GetRegionCenterY(region);

                double targetPullX = (region.TargetCenterX - currentCenterX) * 0.12;
                double targetPullY = (region.TargetCenterY - currentCenterY) * 0.12;

                double bridgePullX = 0.0;
                double bridgePullY = 0.0;
                double bridgeWeightSum = 0.0;

                if (regionGraph.TryGetValue(region.RegionId, out var neighbors))
                {
                    foreach (var neighborPair in neighbors)
                    {
                        if (!regionById.TryGetValue(neighborPair.Key, out var neighbor))
                            continue;

                        double weight = neighborPair.Value;
                        if (weight <= 0.0)
                            continue;

                        double nx = GetRegionCenterX(neighbor);
                        double ny = GetRegionCenterY(neighbor);

                        bridgePullX += (nx - currentCenterX) * weight;
                        bridgePullY += (ny - currentCenterY) * weight;
                        bridgeWeightSum += weight;
                    }
                }

                if (bridgeWeightSum > 0.0)
                {
                    bridgePullX = (bridgePullX / bridgeWeightSum) * 0.065;
                    bridgePullY = (bridgePullY / bridgeWeightSum) * 0.065;
                }

                region.ScreenOriginX += targetPullX + bridgePullX;
                region.ScreenOriginY += targetPullY + bridgePullY;
            }

            ResolveRegionOverlaps(regions);
            ShiftRegionsToPositiveSpace(regions);
        }
    }

    private void ResolveRegionOverlaps(List<PlacedRegion> regions)
    {
        if (regions.Count <= 1)
            return;

        for (int i = 0; i < regions.Count; i++)
        {
            for (int j = i + 1; j < regions.Count; j++)
            {
                var a = regions[i];
                var b = regions[j];

                var aRect = GetRegionRect(a, extraPadding: RegionClusterMinGap * 0.5);
                var bRect = GetRegionRect(b, extraPadding: RegionClusterMinGap * 0.5);

                bool overlap =
                    aRect.Left < bRect.Right &&
                    aRect.Right > bRect.Left &&
                    aRect.Top < bRect.Bottom &&
                    aRect.Bottom > bRect.Top;

                if (!overlap)
                    continue;

                double centerAx = GetRegionCenterX(a);
                double centerAy = GetRegionCenterY(a);
                double centerBx = GetRegionCenterX(b);
                double centerBy = GetRegionCenterY(b);

                double dx = centerBx - centerAx;
                double dy = centerBy - centerAy;

                if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                {
                    dx = 1.0;
                    dy = 0.2;
                }

                double overlapX = Math.Min(aRect.Right, bRect.Right) - Math.Max(aRect.Left, bRect.Left);
                double overlapY = Math.Min(aRect.Bottom, bRect.Bottom) - Math.Max(aRect.Top, bRect.Top);

                double pushDistance = Math.Max(10.0, Math.Min(overlapX, overlapY) * 0.48);

                double length = Math.Sqrt(dx * dx + dy * dy);
                double ux = dx / length;
                double uy = dy / length;

                a.ScreenOriginX -= ux * pushDistance * 0.5;
                a.ScreenOriginY -= uy * pushDistance * 0.5;

                b.ScreenOriginX += ux * pushDistance * 0.5;
                b.ScreenOriginY += uy * pushDistance * 0.5;
            }
        }
    }

    private void ShiftRegionsToPositiveSpace(List<PlacedRegion> regions)
    {
        if (regions.Count == 0)
            return;

        double minLeft = regions.Min(r => GetRegionRect(r, 0.0).Left);
        double minTop = regions.Min(r => GetRegionRect(r, 0.0).Top);

        double shiftX = 0.0;
        double shiftY = 0.0;

        if (minLeft < CanvasPadding)
            shiftX = CanvasPadding - minLeft;

        if (minTop < CanvasPadding)
            shiftY = CanvasPadding - minTop;

        if (Math.Abs(shiftX) < 0.001 && Math.Abs(shiftY) < 0.001)
            return;

        foreach (var region in regions)
        {
            region.ScreenOriginX += shiftX;
            region.ScreenOriginY += shiftY;

            region.TargetCenterX += shiftX;
            region.TargetCenterY += shiftY;
        }
    }

    private Rect GetRegionRect(PlacedRegion region, double extraPadding)
    {
        double width = region.Width * RegionClusterInternalScale + RegionClusterPadding * 2 + extraPadding * 2;
        double height = region.Height * RegionClusterInternalScale + RegionClusterPadding * 2 + extraPadding * 2;

        return new Rect(
            region.ScreenOriginX - extraPadding,
            region.ScreenOriginY - extraPadding,
            width,
            height);
    }

    private double GetRegionCenterX(PlacedRegion region)
    {
        return region.ScreenOriginX
               + RegionClusterPadding
               + (region.ModelCenterX - region.MinModelX) * RegionClusterInternalScale;
    }

    private double GetRegionCenterY(PlacedRegion region)
    {
        return region.ScreenOriginY
               + RegionClusterPadding
               + (region.ModelCenterY - region.MinModelY) * RegionClusterInternalScale;
    }

    private Dictionary<Guid, Point> BuildRegionClusterScreenPoints(List<PlacedRegion> regions)
    {
        var result = new Dictionary<Guid, Point>();

        foreach (var region in regions)
        {
            foreach (var node in region.Nodes)
            {
                double localX = node.RenderX - region.MinModelX;
                double localY = node.RenderY - region.MinModelY;

                double x = region.ScreenOriginX + RegionClusterPadding + localX * RegionClusterInternalScale;
                double y = region.ScreenOriginY + RegionClusterPadding + localY * RegionClusterInternalScale;

                result[node.Id] = new Point(x, y);
            }
        }

        return result;
    }

    private Size MeasurePlacedRegions(List<PlacedRegion> regions)
    {
        double maxX = MinCanvasSize;
        double maxY = MinCanvasSize;

        foreach (var region in regions)
        {
            var rect = GetRegionRect(region, extraPadding: 20.0);

            if (rect.Right + CanvasPadding > maxX)
                maxX = rect.Right + CanvasPadding;

            if (rect.Bottom + CanvasPadding > maxY)
                maxY = rect.Bottom + CanvasPadding;
        }

        return new Size(maxX, maxY);
    }

    private void DrawRegionBackground(PlacedRegion region, Dictionary<Guid, Point> points)
    {
        if (_graphCanvas == null)
            return;

        var regionPoints = region.Nodes
            .Where(n => points.ContainsKey(n.Id))
            .Select(n => points[n.Id])
            .ToList();

        if (regionPoints.Count == 0)
            return;

        var hull = BuildSoftHull(regionPoints);
        if (hull.Count < 3)
            return;

        var polygon = new Polygon
        {
            Points = new Points(hull),
            Fill = new SolidColorBrush(GetRegionFillColor(region.Nodes)),
            Stroke = new SolidColorBrush(GetRegionStrokeColor(region.Nodes)),
            StrokeThickness = 1.2,
            Opacity = 0.78,
            IsHitTestVisible = false
        };

        _graphCanvas.Children.Add(polygon);
    }

    private void DrawRegionLabel(PlacedRegion region)
    {
        if (_graphCanvas == null)
            return;

        var label = new TextBlock
        {
            Text = region.RegionId,
            Foreground = new SolidColorBrush(Color.Parse("#6E675A")),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(label, region.ScreenOriginX + 10);
        Canvas.SetTop(label, region.ScreenOriginY + 6);
        _graphCanvas.Children.Add(label);
    }

    private List<SimulationGraphEdgeDto> SelectRegionClusterEdgesForDrawing(
        List<SimulationGraphEdgeDto> edges,
        Dictionary<Guid, SimulationGraphNodeDto> nodeMap,
        Guid? selectedNodeId)
    {
        if (selectedNodeId.HasValue)
        {
            return edges
                .Where(e => e.FromCellId == selectedNodeId.Value || e.ToCellId == selectedNodeId.Value)
                .ToList();
        }

        var intra = edges
            .Where(e => !IsInterRegionEdge(e, nodeMap))
            .ToList();

        var bridges = edges
            .Where(e => IsInterRegionEdge(e, nodeMap))
            .GroupBy(e => NormalizeRegionPair(nodeMap[e.FromCellId].GroupKey, nodeMap[e.ToCellId].GroupKey))
            .Select(g => g
                .OrderByDescending(e => e.FireSpreadModifier)
                .ThenBy(e => e.Distance)
                .First())
            .ToList();

        return intra.Concat(bridges).ToList();
    }

    private bool IsInterRegionEdge(SimulationGraphEdgeDto edge, Dictionary<Guid, SimulationGraphNodeDto> nodeMap)
    {
        if (!nodeMap.TryGetValue(edge.FromCellId, out var fromNode) ||
            !nodeMap.TryGetValue(edge.ToCellId, out var toNode))
            return false;

        return !string.Equals(fromNode.GroupKey, toNode.GroupKey, StringComparison.Ordinal);
    }

    private string NormalizeRegionPair(string? a, string? b)
    {
        a ??= string.Empty;
        b ??= string.Empty;
        return string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
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
        double opacity = isBridge ? 0.52 : 0.72;

        if (selectedNodeId.HasValue)
        {
            if (isSelectedEdge)
            {
                thickness += 1.2;
                opacity = 1.0;
            }
            else
            {
                thickness = Math.Max(0.9, thickness - 0.2);
                opacity = isBridge ? 0.14 : 0.18;
            }
        }

        var line = new Line
        {
            StartPoint = fromPoint,
            EndPoint = toPoint,
            Stroke = new SolidColorBrush(isBridge ? GetBridgeColor(edge) : GetEdgeColor(edge)),
            StrokeThickness = thickness,
            Opacity = opacity,
            IsHitTestVisible = false
        };

        _graphCanvas.Children.Add(line);
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

        double radius = isSelected
            ? SelectedNodeRadius
            : isNeighbor
                ? NeighborNodeRadius
                : BaseNodeRadius;

        if (isIgnitionSelected)
            radius = Math.Max(radius, useMapStyle ? 10.5 : 11.5);

        if (isIgnitionSelected)
            DrawIgnitionGlow(point, radius, useMapStyle);

        var shape = new Rectangle
        {
            Width = radius * 2,
            Height = radius * 2,
            RadiusX = useMapStyle ? 3.0 : radius,
            RadiusY = useMapStyle ? 3.0 : radius,
            Fill = new SolidColorBrush(isIgnitionSelected ? Color.Parse("#9EC5FE") : (useMapStyle ? GetMapCellColor(node) : GetNodeColor(node))),
            Stroke = new SolidColorBrush(
                isIgnitionSelected
                    ? Color.Parse("#D6402B")
                    : isSelected
                        ? Color.Parse("#355CBE")
                        : isNeighbor
                            ? Color.Parse("#B45A4A")
                            : Color.Parse("#F8F5EE")),
            StrokeThickness = isIgnitionSelected ? 2.6 : isSelected ? 2.2 : isNeighbor ? 1.8 : 0.9,
            Opacity = opacity,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        ToolTip.SetTip(shape, BuildTooltip(node, edges));

        shape.PointerPressed += (_, _) =>
        {
            SelectedNode = node;
            NodeClicked?.Invoke(this, node);
        };

        Canvas.SetLeft(shape, point.X - radius);
        Canvas.SetTop(shape, point.Y - radius);
        _graphCanvas.Children.Add(shape);

        if (isIgnitionSelected)
            DrawIgnitionMarker(point, radius, useMapStyle);
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

    private void DrawIgnitionMarker(Point point, double radius, bool useMapStyle)
    {
        if (_graphCanvas == null)
            return;

        var outerSize = Math.Max(7, radius * 0.95);
        var innerSize = Math.Max(4, radius * 0.42);

        var outer = new Ellipse
        {
            Width = outerSize,
            Height = outerSize,
            Fill = new SolidColorBrush(Color.Parse("#FFF4A3")),
            Stroke = new SolidColorBrush(Color.Parse("#C94F3D")),
            StrokeThickness = useMapStyle ? 1.6 : 1.8,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(outer, point.X - outerSize / 2.0);
        Canvas.SetTop(outer, point.Y - outerSize / 2.0);
        _graphCanvas.Children.Add(outer);

        var inner = new Ellipse
        {
            Width = innerSize,
            Height = innerSize,
            Fill = new SolidColorBrush(Color.Parse("#D6402B")),
            IsHitTestVisible = false
        };

        Canvas.SetLeft(inner, point.X - innerSize / 2.0);
        Canvas.SetTop(inner, point.Y - innerSize / 2.0);
        _graphCanvas.Children.Add(inner);
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

    private (double MinX, double MaxX, double MinY, double MaxY) GetModelBounds(List<SimulationGraphNodeDto> nodes)
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

    private Color GetNodeColor(SimulationGraphNodeDto node)
    {
        if (node.IsBurned)
            return Color.Parse("#777777");

        if (node.IsBurning)
            return Color.Parse("#E34A33");

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

    private Color GetMapCellColor(SimulationGraphNodeDto node)
    {
        if (node.IsBurned)
            return Color.Parse("#CE2D2D");

        if (node.IsBurning)
            return Color.Parse("#E34A33");

        if (node.BurnProbability >= 0.75)
            return Color.Parse("#F46D43");

        if (node.BurnProbability >= 0.50)
            return Color.Parse("#FDAE61");

        if (node.BurnProbability >= 0.25)
            return Color.Parse("#FEE08B");

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

    private Color GetRegionFillColor(List<SimulationGraphNodeDto> nodes)
    {
        var burning = nodes.Count(n => n.IsBurning);
        var burned = nodes.Count(n => n.IsBurned);
        var avgProbability = nodes.Count > 0 ? nodes.Average(n => n.BurnProbability) : 0.0;
        var dominantVegetation = nodes
            .GroupBy(n => n.Vegetation)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key?.ToLowerInvariant())
            .FirstOrDefault();

        if (burning > 0)
            return Color.Parse("#F2DED4");

        if (burned > nodes.Count * 0.45)
            return Color.Parse("#E8DDD7");

        if (avgProbability >= 0.60)
            return Color.Parse("#F1ECD2");

        return dominantVegetation switch
        {
            "coniferous" => Color.Parse("#DCEAD6"),
            "deciduous" => Color.Parse("#E5F0DD"),
            "mixed" => Color.Parse("#E8EDD8"),
            "grass" => Color.Parse("#F3EBCF"),
            "shrub" => Color.Parse("#EEE0D1"),
            _ => Color.Parse("#ECE9DD")
        };
    }

    private Color GetRegionStrokeColor(List<SimulationGraphNodeDto> nodes)
    {
        var burning = nodes.Count(n => n.IsBurning);
        if (burning > 0)
            return Color.Parse("#D7B9AA");

        return Color.Parse("#D5D0C2");
    }

    private Color GetEdgeColor(SimulationGraphEdgeDto edge)
    {
        if (edge.FireSpreadModifier >= 0.70)
            return Color.Parse("#D88A5B");

        if (edge.FireSpreadModifier >= 0.35)
            return Color.Parse("#A59BAE");

        return Color.Parse("#CFC8D5");
    }

    private Color GetBridgeColor(SimulationGraphEdgeDto edge)
    {
        if (edge.FireSpreadModifier >= 0.70)
            return Color.Parse("#DE9A62");

        if (edge.FireSpreadModifier >= 0.35)
            return Color.Parse("#AAA2B3");

        return Color.Parse("#CFC9D5");
    }

    private double GetEdgeThickness(SimulationGraphEdgeDto edge)
    {
        if (edge.FireSpreadModifier >= 0.70)
            return BaseEdgeThickness + 1.1;

        if (edge.FireSpreadModifier >= 0.35)
            return BaseEdgeThickness + 0.35;

        return BaseEdgeThickness;
    }

    private void DrawEmptyState()
    {
        if (_graphCanvas == null)
            return;

        _graphCanvas.Width = MinCanvasSize;
        _graphCanvas.Height = MinCanvasSize;

        var text = new TextBlock
        {
            Text = "Граф не загружен",
            Foreground = new SolidColorBrush(Color.Parse("#6E6A78")),
            FontSize = 18,
            FontWeight = FontWeight.SemiBold
        };

        text.Measure(Size.Infinity);

        Canvas.SetLeft(text, (MinCanvasSize - text.DesiredSize.Width) / 2);
        Canvas.SetTop(text, (MinCanvasSize - text.DesiredSize.Height) / 2);
        _graphCanvas.Children.Add(text);
    }

    private string BuildTooltip(SimulationGraphNodeDto node, List<SimulationGraphEdgeDto> edges)
    {
        var nodeEdges = edges
            .Where(e => e.FromCellId == node.Id || e.ToCellId == node.Id)
            .ToList();

        var degree = nodeEdges.Count;
        var strong = nodeEdges.Count(e => e.FireSpreadModifier >= 0.70);
        var medium = nodeEdges.Count(e => e.FireSpreadModifier >= 0.35 && e.FireSpreadModifier < 0.70);
        var weak = nodeEdges.Count(e => e.FireSpreadModifier < 0.35);
        var ignitionText = node.IsSelectedIgnition ? "\nСтартовый очаг: Да" : string.Empty;

        return $"Координаты модели: ({node.X}, {node.Y})\n" +
               $"Координаты отрисовки: ({node.RenderX:F2}, {node.RenderY:F2})\n" +
               $"Группа: {node.GroupKey}\n" +
               $"Тип: {node.Vegetation}\n" +
               $"Состояние: {node.State}\n" +
               $"Влажность: {node.Moisture:P0}\n" +
               $"Высота: {node.Elevation:F0} м\n" +
               $"Вероятность возгорания: {node.BurnProbability:P0}" +
               ignitionText + "\n" +
               $"Связей: {degree}\n" +
               $"Сильных: {strong}, средних: {medium}, слабых: {weak}";
    }

    private sealed class PlacedRegion
    {
        public string RegionId { get; }
        public List<SimulationGraphNodeDto> Nodes { get; }

        public double MinModelX { get; }
        public double MinModelY { get; }
        public double MaxModelX { get; }
        public double MaxModelY { get; }

        public double Width { get; }
        public double Height { get; }

        public double ModelCenterX { get; }
        public double ModelCenterY { get; }

        public double TargetCenterX { get; set; }
        public double TargetCenterY { get; set; }

        public double ScreenOriginX { get; set; }
        public double ScreenOriginY { get; set; }

        public PlacedRegion(
            string regionId,
            List<SimulationGraphNodeDto> nodes,
            double minModelX,
            double minModelY,
            double maxModelX,
            double maxModelY,
            double width,
            double height,
            double modelCenterX,
            double modelCenterY)
        {
            RegionId = regionId;
            Nodes = nodes;
            MinModelX = minModelX;
            MinModelY = minModelY;
            MaxModelX = maxModelX;
            MaxModelY = maxModelY;
            Width = width;
            Height = height;
            ModelCenterX = modelCenterX;
            ModelCenterY = modelCenterY;
        }
    }
}