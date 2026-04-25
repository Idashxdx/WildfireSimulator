using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using WildfireSimulator.Client.Models;

namespace WildfireSimulator.Client.Views;

public partial class ClusteredGraphEditorDialog : Window
{
    private enum EditorMode
    {
        SelectAndMove = 0,
        AddNodes = 1,
        CreateEdges = 2,
        CreateBridges = 3,
        DeleteEdges = 4
    }
    private Button? _zoomInButton;
    private Button? _zoomOutButton;
    private Button? _resetZoomButton;
    private ToggleSwitch? _labelsToggle;

    private double _zoom = 1.0;
    private bool _showLabels = true;

    private const double MinZoom = 0.45;
    private const double MaxZoom = 2.60;
    private const double ZoomStep = 0.18;
    private sealed class AreaDraft
    {
        public string Name { get; set; } = string.Empty;
        public VegetationType Vegetation { get; set; } = VegetationType.Mixed;
        public double Moisture { get; set; } = 0.45;
        public double Elevation { get; set; } = 0.0;
    }

    private const double CanvasPadding = 44.0;
    private const double NodeRadius = 9.0;
    private const double SelectedNodeRadius = 12.0;
    private const double EdgeHitDistance = 8.0;

    private Canvas? _graphCanvas;
    private ComboBox? _modeBox;

    private ComboBox? _areaBox;
    private Button? _addAreaButton;
    private Button? _deleteAreaButton;
    private TextBox? _areaNameBox;
    private ComboBox? _areaVegetationBox;
    private TextBox? _areaMoistureBox;
    private TextBox? _areaElevationBox;
    private Button? _applyAreaButton;

    private TextBlock? _selectedNodeSummaryTextBlock;
    private TextBox? _nodeClusterIdBox;
    private ComboBox? _nodeVegetationBox;
    private TextBox? _nodeMoistureBox;
    private TextBox? _nodeElevationBox;
    private Button? _applyNodeButton;
    private Button? _deleteNodeButton;

    private TextBlock? _selectedEdgeSummaryTextBlock;
    private TextBox? _edgeDistanceBox;
    private TextBox? _edgeModifierBox;
    private Button? _applyEdgeButton;
    private Button? _deleteEdgeButton;

    private TextBlock? _summaryTextBlock;
    private TextBlock? _hintTextBlock;

    private Button? _cancelButton;
    private Button? _applyButton;

    private readonly int _canvasWidth;
    private readonly int _canvasHeight;

    private readonly List<AreaDraft> _areas = new();
    private readonly List<ClusteredNodeDraftDto> _nodes = new();
    private readonly List<ClusteredEdgeDraftDto> _edges = new();

    private Guid? _selectedNodeId;
    private Guid? _selectedEdgeId;
    private Guid? _pendingEdgeStartNodeId;
    private Guid? _draggedNodeId;

    private bool _drawScheduled;
    private bool _isDragging;

    public ClusteredGraphBlueprintDto EditedBlueprint { get; private set; } = new();
    private readonly GraphScaleType _graphScaleType;
    public ClusteredGraphEditorDialog(
      int canvasWidth,
      int canvasHeight,
      GraphScaleType graphScaleType,
      ClusteredGraphBlueprintDto? existingBlueprint = null)
    {
        _canvasWidth = Math.Max(8, canvasWidth);
        _canvasHeight = Math.Max(8, canvasHeight);
        _graphScaleType = graphScaleType;

        InitializeComponent();
        FindControls();
        AttachEvents();

        if (existingBlueprint != null && existingBlueprint.Nodes.Any())
            LoadBlueprint(existingBlueprint);
        else
            CreateStarterGraph();

        RefreshAreaBox();
        RefreshAreaEditor();
        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        UpdateHint();
        ScheduleDraw();

        Title = graphScaleType switch
        {
            GraphScaleType.Small => "Редактор итогового графа: малый граф",
            GraphScaleType.Medium => "Редактор итогового графа: средний граф",
            GraphScaleType.Large => "Редактор итогового графа: большой граф",
            _ => "Редактор итогового графа"
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void FindControls()
    {
        _graphCanvas = this.FindControl<Canvas>("GraphCanvas");
        _modeBox = this.FindControl<ComboBox>("ModeBox");

        _zoomInButton = this.FindControl<Button>("ZoomInButton");
        _zoomOutButton = this.FindControl<Button>("ZoomOutButton");
        _resetZoomButton = this.FindControl<Button>("ResetZoomButton");
        _labelsToggle = this.FindControl<ToggleSwitch>("LabelsToggle");

        _areaBox = this.FindControl<ComboBox>("AreaBox");
        _addAreaButton = this.FindControl<Button>("AddAreaButton");
        _deleteAreaButton = this.FindControl<Button>("DeleteAreaButton");
        _areaNameBox = this.FindControl<TextBox>("AreaNameBox");
        _areaVegetationBox = this.FindControl<ComboBox>("AreaVegetationBox");
        _areaMoistureBox = this.FindControl<TextBox>("AreaMoistureBox");
        _areaElevationBox = this.FindControl<TextBox>("AreaElevationBox");
        _applyAreaButton = this.FindControl<Button>("ApplyAreaButton");

        _selectedNodeSummaryTextBlock = this.FindControl<TextBlock>("SelectedNodeSummaryTextBlock");
        _nodeClusterIdBox = this.FindControl<TextBox>("NodeClusterIdBox");
        _nodeVegetationBox = this.FindControl<ComboBox>("NodeVegetationBox");
        _nodeMoistureBox = this.FindControl<TextBox>("NodeMoistureBox");
        _nodeElevationBox = this.FindControl<TextBox>("NodeElevationBox");
        _applyNodeButton = this.FindControl<Button>("ApplyNodeButton");
        _deleteNodeButton = this.FindControl<Button>("DeleteNodeButton");

        _selectedEdgeSummaryTextBlock = this.FindControl<TextBlock>("SelectedEdgeSummaryTextBlock");
        _edgeDistanceBox = this.FindControl<TextBox>("EdgeDistanceBox");
        _edgeModifierBox = this.FindControl<TextBox>("EdgeModifierBox");
        _applyEdgeButton = this.FindControl<Button>("ApplyEdgeButton");
        _deleteEdgeButton = this.FindControl<Button>("DeleteEdgeButton");

        _summaryTextBlock = this.FindControl<TextBlock>("SummaryTextBlock");
        _hintTextBlock = this.FindControl<TextBlock>("HintTextBlock");

        _cancelButton = this.FindControl<Button>("CancelButton");
        _applyButton = this.FindControl<Button>("ApplyButton");
    }
    private void AttachEvents()
    {
        if (_graphCanvas != null)
        {
            _graphCanvas.PointerPressed += OnCanvasPointerPressed;
            _graphCanvas.PointerMoved += OnCanvasPointerMoved;
            _graphCanvas.PointerReleased += OnCanvasPointerReleased;
            _graphCanvas.SizeChanged += (_, _) => ScheduleDraw();
        }

        if (_zoomInButton != null)
            _zoomInButton.Click += (_, _) => ChangeZoom(ZoomStep);

        if (_zoomOutButton != null)
            _zoomOutButton.Click += (_, _) => ChangeZoom(-ZoomStep);

        if (_resetZoomButton != null)
            _resetZoomButton.Click += (_, _) => ResetZoom();

        if (_labelsToggle != null)
        {
            _labelsToggle.IsChecked = true;
            _labelsToggle.Checked += (_, _) =>
            {
                _showLabels = true;
                ScheduleDraw();
            };
            _labelsToggle.Unchecked += (_, _) =>
            {
                _showLabels = false;
                ScheduleDraw();
            };
        }

        if (_modeBox != null)
        {
            _modeBox.SelectionChanged += (_, _) =>
            {
                _pendingEdgeStartNodeId = null;
                _draggedNodeId = null;
                _isDragging = false;
                UpdateHint();
                ScheduleDraw();
            };
        }

        if (_areaBox != null)
        {
            _areaBox.SelectionChanged += (_, _) =>
            {
                RefreshAreaEditor();
                ScheduleDraw();
            };
        }

        if (_addAreaButton != null)
            _addAreaButton.Click += (_, _) => AddArea();

        if (_deleteAreaButton != null)
            _deleteAreaButton.Click += (_, _) => DeleteSelectedArea();

        if (_applyAreaButton != null)
            _applyAreaButton.Click += (_, _) => ApplyAreaChanges();

        if (_applyNodeButton != null)
            _applyNodeButton.Click += (_, _) => ApplyNodeChanges();

        if (_deleteNodeButton != null)
            _deleteNodeButton.Click += (_, _) => DeleteSelectedNode();

        if (_applyEdgeButton != null)
            _applyEdgeButton.Click += (_, _) => ApplyEdgeChanges();

        if (_deleteEdgeButton != null)
            _deleteEdgeButton.Click += (_, _) => DeleteSelectedEdge();

        if (_cancelButton != null)
            _cancelButton.Click += (_, _) => Close(false);

        if (_applyButton != null)
            _applyButton.Click += (_, _) =>
            {
                EditedBlueprint = BuildBlueprint();
                Close(true);
            };
    }
    private void ChangeZoom(double delta)
    {
        _zoom = Math.Clamp(_zoom + delta, MinZoom, MaxZoom);
        ScheduleDraw();
    }

    private void ResetZoom()
    {
        _zoom = 1.0;
        ScheduleDraw();
    }
    private void ApplyZoom()
    {
        ScheduleDraw();
    }
    private void ScheduleDraw()
    {
        if (_graphCanvas == null || _drawScheduled)
            return;

        _drawScheduled = true;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _drawScheduled = false;
            DrawGraph();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void CreateStarterGraph()
    {
        _areas.Clear();
        _nodes.Clear();
        _edges.Clear();

        switch (_graphScaleType)
        {
            case GraphScaleType.Small:
                CreateSmallStarterGraph();
                break;

            case GraphScaleType.Medium:
                CreateMediumStarterGraph();
                break;

            case GraphScaleType.Large:
                CreateLargeStarterGraph();
                break;

            default:
                CreateMediumStarterGraph();
                break;
        }
    }
    private void CreateSmallStarterGraph()
    {
        var area = new AreaDraft
        {
            Name = "Лес",
            Vegetation = VegetationType.Mixed,
            Moisture = 0.42,
            Elevation = 15
        };

        _areas.Add(area);

        var positions = new (int X, int Y)[]
        {
        (5, 5), (9, 4), (13, 5), (17, 6),
        (6, 9), (10, 9), (14, 9), (18, 10),
        (5, 13), (9, 14), (13, 13), (17, 14),
        (10, 17), (14, 17)
        };

        foreach (var position in positions)
            AddNode(position.X, position.Y, area);

        ConnectNearestNodes(targetDegree: 3, maxDistance: 8.0);
    }
    private void CreateMediumStarterGraph()
    {
        _areas.Add(new AreaDraft { Name = "A", Vegetation = VegetationType.Coniferous, Moisture = 0.30, Elevation = 35 });
        _areas.Add(new AreaDraft { Name = "B", Vegetation = VegetationType.Mixed, Moisture = 0.44, Elevation = 20 });
        _areas.Add(new AreaDraft { Name = "C", Vegetation = VegetationType.Deciduous, Moisture = 0.52, Elevation = 12 });
        _areas.Add(new AreaDraft { Name = "D", Vegetation = VegetationType.Shrub, Moisture = 0.36, Elevation = 24 });

        AddAreaCluster(_areas[0], 8, 8, 8);
        AddAreaCluster(_areas[1], 27, 9, 8);
        AddAreaCluster(_areas[2], 12, 24, 8);
        AddAreaCluster(_areas[3], 31, 24, 8);

        AutoConnectWithinAreas();

        AddEdgeBetweenNearestAreas("A", "B", 1.05);
        AddEdgeBetweenNearestAreas("A", "C", 0.90);
        AddEdgeBetweenNearestAreas("B", "D", 1.00);
        AddEdgeBetweenNearestAreas("C", "D", 0.85);
    }
    private void CreateLargeStarterGraph()
    {
        _areas.Add(new AreaDraft { Name = "A", Vegetation = VegetationType.Coniferous, Moisture = 0.30, Elevation = 35 });
        _areas.Add(new AreaDraft { Name = "B", Vegetation = VegetationType.Mixed, Moisture = 0.45, Elevation = 20 });
        _areas.Add(new AreaDraft { Name = "C", Vegetation = VegetationType.Deciduous, Moisture = 0.55, Elevation = 10 });
        _areas.Add(new AreaDraft { Name = "D", Vegetation = VegetationType.Shrub, Moisture = 0.38, Elevation = 25 });
        _areas.Add(new AreaDraft { Name = "E", Vegetation = VegetationType.Grass, Moisture = 0.35, Elevation = 5 });
        _areas.Add(new AreaDraft { Name = "F", Vegetation = VegetationType.Mixed, Moisture = 0.48, Elevation = 18 });

        AddAreaCluster(_areas[0], 10, 9, 10);
        AddAreaCluster(_areas[1], 31, 9, 10);
        AddAreaCluster(_areas[2], 18, 24, 10);
        AddAreaCluster(_areas[3], 49, 24, 10);
        AddAreaCluster(_areas[4], 35, 35, 9);
        AddAreaCluster(_areas[5], 25, 18, 8);

        AutoConnectWithinAreas();

        AddEdgeBetweenNearestAreas("A", "B", 1.05);
        AddEdgeBetweenNearestAreas("A", "C", 0.90);
        AddEdgeBetweenNearestAreas("B", "D", 1.10);
        AddEdgeBetweenNearestAreas("C", "E", 0.95);
        AddEdgeBetweenNearestAreas("D", "E", 0.80);
        AddEdgeBetweenNearestAreas("B", "F", 1.00);
        AddEdgeBetweenNearestAreas("C", "F", 1.00);
    }
    private void AddAreaCluster(AreaDraft area, int centerX, int centerY, int count)
    {
        var offsets = new (int X, int Y)[]
        {
        (0, 0), (-3, -2), (3, -2), (-4, 2), (4, 2),
        (-2, 5), (2, 5), (0, -5), (-5, 0), (5, 0)
        };

        for (int i = 0; i < count && i < offsets.Length; i++)
        {
            AddNode(
                centerX + offsets[i].X,
                centerY + offsets[i].Y,
                area);
        }
    }
    private void ConnectNearestNodes(int targetDegree, double maxDistance)
    {
        foreach (var node in _nodes)
        {
            while (GetNodeDegree(node) < targetDegree)
            {
                var nearest = _nodes
                    .Where(other => other.Id != node.Id)
                    .Where(other => !EdgeExists(node.Id, other.Id))
                    .Select(other => new
                    {
                        Node = other,
                        Distance = CalculateDistance(node, other)
                    })
                    .Where(x => x.Distance <= maxDistance)
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();

                if (nearest == null)
                    break;

                AddEdge(node, nearest.Node, 1.0);
            }
        }
    }
    private void LoadBlueprint(ClusteredGraphBlueprintDto blueprint)
    {
        _areas.Clear();
        _nodes.Clear();
        _edges.Clear();

        _nodes.AddRange(blueprint.Nodes.Select(CloneNode));
        _edges.AddRange(blueprint.Edges.Select(CloneEdge));

        foreach (var group in _nodes.GroupBy(n => NormalizeAreaName(n.ClusterId)).OrderBy(g => g.Key))
        {
            _areas.Add(new AreaDraft
            {
                Name = group.Key,
                Vegetation = group.GroupBy(n => n.Vegetation).OrderByDescending(g => g.Count()).First().Key,
                Moisture = Math.Clamp(group.Average(n => n.Moisture), 0.0, 1.0),
                Elevation = group.Average(n => n.Elevation)
            });

            foreach (var node in group)
                node.ClusterId = group.Key;
        }

        if (_areas.Count == 0)
            _areas.Add(new AreaDraft { Name = "A", Vegetation = VegetationType.Mixed, Moisture = 0.45, Elevation = 0.0 });

        RemoveBrokenEdges();
    }

    private ClusteredGraphBlueprintDto BuildBlueprint()
    {
        RemoveBrokenEdges();

        return new ClusteredGraphBlueprintDto
        {
            CanvasWidth = _canvasWidth,
            CanvasHeight = _canvasHeight,
            Candidates = new List<ClusteredCandidateNodeDto>(),
            Nodes = _nodes.Select(CloneNode).ToList(),
            Edges = _edges.Select(CloneEdge).ToList()
        };
    }

    private void DrawGraph()
    {
        if (_graphCanvas == null)
            return;

        _graphCanvas.Children.Clear();

        var size = GetCanvasSize();
        _graphCanvas.Width = size.Width;
        _graphCanvas.Height = size.Height;

        DrawAreaBackgrounds();
        DrawEdges();
        DrawNodes();
    }
    private void DrawAreaBackgrounds()
    {
        if (_graphCanvas == null)
            return;

        foreach (var area in _areas)
        {
            var areaNodes = _nodes
                .Where(n => string.Equals(NormalizeAreaName(n.ClusterId), area.Name, StringComparison.Ordinal))
                .ToList();

            if (areaNodes.Count == 0)
                continue;

            var points = areaNodes.Select(n => ToCanvasPoint(n.X, n.Y)).ToList();

            double minX = points.Min(p => p.X) - 34;
            double minY = points.Min(p => p.Y) - 34;
            double maxX = points.Max(p => p.X) + 34;
            double maxY = points.Max(p => p.Y) + 34;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(38, 142, 124, 195)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 142, 124, 195)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                IsHitTestVisible = false,
                Child = _showLabels
                    ? new TextBlock
                    {
                        Text = area.Name,
                        Foreground = new SolidColorBrush(Color.Parse("#6A5A8A")),
                        FontSize = 16,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(10, 6, 0, 0)
                    }
                    : null
            };

            Canvas.SetLeft(border, minX);
            Canvas.SetTop(border, minY);
            border.Width = Math.Max(70, maxX - minX);
            border.Height = Math.Max(56, maxY - minY);

            _graphCanvas.Children.Add(border);
        }
    }

    private void DrawEdges()
    {
        if (_graphCanvas == null)
            return;

        foreach (var edge in _edges)
        {
            var fromNode = FindNode(edge.FromNodeId);
            var toNode = FindNode(edge.ToNodeId);

            if (fromNode == null || toNode == null)
                continue;

            var fromPoint = ToCanvasPoint(fromNode.X, fromNode.Y);
            var toPoint = ToCanvasPoint(toNode.X, toNode.Y);

            bool isSelected = edge.Id == _selectedEdgeId;
            bool isBridge = !string.Equals(
                NormalizeAreaName(fromNode.ClusterId),
                NormalizeAreaName(toNode.ClusterId),
                StringComparison.Ordinal);

            var line = new Line
            {
                StartPoint = fromPoint,
                EndPoint = toPoint,
                Stroke = new SolidColorBrush(isSelected
                    ? Color.Parse("#D6402B")
                    : isBridge
                        ? Color.Parse("#4E89B8")
                        : Color.Parse("#9A91AA")),
                StrokeThickness = isSelected ? 4.0 : isBridge ? 2.7 : 1.8,
                StrokeDashArray = isBridge ? new AvaloniaList<double> { 6, 4 } : null,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            ToolTip.SetTip(line, BuildEdgeTooltip(edge, fromNode, toNode, isBridge));

            line.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(line).Properties.IsLeftButtonPressed)
                    return;

                HandleEdgeClick(edge);
                e.Handled = true;
            };

            _graphCanvas.Children.Add(line);

            DrawEdgeLabel(edge, fromPoint, toPoint, isBridge, isSelected);
        }
    }

    private void DrawEdgeLabel(ClusteredEdgeDraftDto edge, Point fromPoint, Point toPoint, bool isBridge, bool isSelected)
    {
        if (_graphCanvas == null || !_showLabels)
            return;

        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(232, 255, 253, 248)),
            BorderBrush = new SolidColorBrush(isSelected ? Color.Parse("#D6402B") : Color.Parse("#DDD6E9")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(5, 2),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = isBridge ? "мост" : edge.FireSpreadModifier.ToString("F2", CultureInfo.InvariantCulture),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#564F64"))
            }
        };

        label.Measure(Size.Infinity);

        Canvas.SetLeft(label, (fromPoint.X + toPoint.X) / 2.0 - label.DesiredSize.Width / 2.0);
        Canvas.SetTop(label, (fromPoint.Y + toPoint.Y) / 2.0 - label.DesiredSize.Height / 2.0);

        _graphCanvas.Children.Add(label);
    }

    private void DrawNodes()
    {
        if (_graphCanvas == null)
            return;

        foreach (var node in _nodes)
        {
            var point = ToCanvasPoint(node.X, node.Y);
            bool isSelected = node.Id == _selectedNodeId;
            bool isPending = node.Id == _pendingEdgeStartNodeId;
            double radius = isSelected ? SelectedNodeRadius : NodeRadius;

            var ellipse = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = new SolidColorBrush(GetNodeColor(node)),
                Stroke = new SolidColorBrush(isPending
                    ? Color.Parse("#D6402B")
                    : isSelected
                        ? Color.Parse("#2F2A3A")
                        : Color.Parse("#FFFDF8")),
                StrokeThickness = isSelected || isPending ? 3 : 1.5,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            ToolTip.SetTip(ellipse, BuildNodeTooltip(node));

            ellipse.PointerPressed += (_, e) =>
            {
                HandleNodePointerPressed(node, e);
                e.Handled = true;
            };

            Canvas.SetLeft(ellipse, point.X - radius);
            Canvas.SetTop(ellipse, point.Y - radius);
            _graphCanvas.Children.Add(ellipse);

            DrawNodeLabel(node, point, radius);
        }
    }

    private void DrawNodeLabel(ClusteredNodeDraftDto node, Point point, double radius)
    {
        return;
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_graphCanvas == null)
            return;

        var point = e.GetPosition(_graphCanvas);
        var properties = e.GetCurrentPoint(_graphCanvas).Properties;

        if (properties.IsRightButtonPressed)
        {
            var node = FindNodeAt(point);
            if (node != null)
            {
                _selectedNodeId = node.Id;
                DeleteSelectedNode();
            }

            return;
        }

        if (!properties.IsLeftButtonPressed)
            return;

        if (GetMode() == EditorMode.AddNodes)
        {
            var nodeAtPoint = FindNodeAt(point);
            if (nodeAtPoint != null)
            {
                SelectNode(nodeAtPoint);
                return;
            }

            var graphPoint = ToGraphPoint(point);
            AddNode(graphPoint.X, graphPoint.Y, GetSelectedArea());
            RefreshAfterStructureChanged();
        }
        else if (GetMode() == EditorMode.DeleteEdges)
        {
            var edge = FindEdgeAt(point);
            if (edge != null)
            {
                _selectedEdgeId = edge.Id;
                DeleteSelectedEdge();
            }
        }
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_graphCanvas == null || !_isDragging || _draggedNodeId == null)
            return;

        var node = FindNode(_draggedNodeId.Value);
        if (node == null)
            return;

        var point = ToGraphPoint(e.GetPosition(_graphCanvas));

        node.X = point.X;
        node.Y = point.Y;

        UpdateIncidentEdgeDistances(node.Id);

        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        ScheduleDraw();
    }
    private void UpdateIncidentEdgeDistances(Guid nodeId)
    {
        foreach (var edge in _edges.Where(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId))
        {
            var fromNode = FindNode(edge.FromNodeId);
            var toNode = FindNode(edge.ToNodeId);

            if (fromNode == null || toNode == null)
                continue;

            edge.DistanceOverride = CalculateDistance(fromNode, toNode);
        }
    }
    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        _draggedNodeId = null;
    }

    private void HandleNodePointerPressed(ClusteredNodeDraftDto node, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(null).Properties;

        if (properties.IsRightButtonPressed)
        {
            _selectedNodeId = node.Id;
            DeleteSelectedNode();
            return;
        }

        if (!properties.IsLeftButtonPressed)
            return;

        switch (GetMode())
        {
            case EditorMode.SelectAndMove:
                SelectNode(node);
                _draggedNodeId = node.Id;
                _isDragging = true;
                break;

            case EditorMode.AddNodes:
                SelectNode(node);
                break;

            case EditorMode.CreateEdges:
                HandleCreateEdgeNodeClick(node, forceBridge: false);
                break;

            case EditorMode.CreateBridges:
                HandleCreateEdgeNodeClick(node, forceBridge: true);
                break;

            case EditorMode.DeleteEdges:
                SelectNode(node);
                break;
        }
    }
    private void HandleCreateEdgeNodeClick(ClusteredNodeDraftDto node, bool forceBridge)
    {
        _selectedNodeId = node.Id;
        _selectedEdgeId = null;

        if (_pendingEdgeStartNodeId == null)
        {
            _pendingEdgeStartNodeId = node.Id;
            RefreshNodeEditor();
            RefreshEdgeEditor();
            UpdateHint();
            ScheduleDraw();
            return;
        }

        if (_pendingEdgeStartNodeId == node.Id)
        {
            _pendingEdgeStartNodeId = null;
            UpdateHint();
            ScheduleDraw();
            return;
        }

        var fromNode = FindNode(_pendingEdgeStartNodeId.Value);
        if (fromNode == null)
        {
            _pendingEdgeStartNodeId = node.Id;
            UpdateHint();
            return;
        }

        if (forceBridge && !IsBridge(fromNode, node))
        {
            _pendingEdgeStartNodeId = node.Id;
            RefreshNodeEditor();
            RefreshEdgeEditor();
            UpdateHint();
            ScheduleDraw();
            return;
        }

        if (!EdgeExists(fromNode.Id, node.Id))
        {
            _edges.Add(new ClusteredEdgeDraftDto
            {
                Id = Guid.NewGuid(),
                FromNodeId = fromNode.Id,
                ToNodeId = node.Id,
                DistanceOverride = CalculateDistance(fromNode, node),
                FireSpreadModifier = forceBridge || IsBridge(fromNode, node) ? 1.15 : 1.0
            });
        }

        _pendingEdgeStartNodeId = null;
        RefreshAfterStructureChanged();
        UpdateHint();
    }
    private void HandleEdgeClick(ClusteredEdgeDraftDto edge)
    {
        if (GetMode() == EditorMode.DeleteEdges)
        {
            _selectedEdgeId = edge.Id;
            DeleteSelectedEdge();
            return;
        }

        _selectedEdgeId = edge.Id;
        _selectedNodeId = null;
        _pendingEdgeStartNodeId = null;

        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        ScheduleDraw();
    }

    private void SelectNode(ClusteredNodeDraftDto node)
    {
        _selectedNodeId = node.Id;
        _selectedEdgeId = null;
        _pendingEdgeStartNodeId = null;

        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        ScheduleDraw();
    }

    private void AddArea()
    {
        if (_graphScaleType == GraphScaleType.Small)
            return;

        int index = 1;

        while (_areas.Any(a => string.Equals(a.Name, $"область-{index}", StringComparison.Ordinal)))
            index++;

        _areas.Add(new AreaDraft
        {
            Name = $"область-{index}",
            Vegetation = VegetationType.Mixed,
            Moisture = 0.45,
            Elevation = 0.0
        });

        RefreshAreaBox();

        if (_areaBox != null)
            _areaBox.SelectedIndex = _areas.Count - 1;

        RefreshAreaEditor();
        RefreshSummary();
        ScheduleDraw();
    }

    private void DeleteSelectedArea()
    {
        if (_graphScaleType == GraphScaleType.Small)
            return;

        if (_areas.Count <= 1)
            return;

        string areaName = GetSelectedAreaName();
        var area = _areas.FirstOrDefault(a => string.Equals(a.Name, areaName, StringComparison.Ordinal));

        if (area == null)
            return;

        var nodeIdsToRemove = _nodes
            .Where(n => string.Equals(NormalizeAreaName(n.ClusterId), areaName, StringComparison.Ordinal))
            .Select(n => n.Id)
            .ToHashSet();

        _nodes.RemoveAll(n => nodeIdsToRemove.Contains(n.Id));

        _edges.RemoveAll(e =>
            nodeIdsToRemove.Contains(e.FromNodeId) ||
            nodeIdsToRemove.Contains(e.ToNodeId));

        _areas.Remove(area);

        _selectedNodeId = null;
        _selectedEdgeId = null;
        _pendingEdgeStartNodeId = null;
        _draggedNodeId = null;
        _isDragging = false;

        RefreshAreaBox();

        if (_areaBox != null)
            _areaBox.SelectedIndex = _areas.Count > 0 ? 0 : -1;

        RefreshAreaEditor();
        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        ScheduleDraw();
    }

    private void ApplyAreaChanges()
    {
        var area = GetSelectedArea();
        if (area == null)
            return;

        string oldName = area.Name;
        string newName = NormalizeAreaName(_areaNameBox?.Text);

        if (_areas.Any(a => !ReferenceEquals(a, area) && string.Equals(a.Name, newName, StringComparison.OrdinalIgnoreCase)))
            newName = oldName;

        area.Name = newName;
        area.Vegetation = ParseVegetation(_areaVegetationBox?.SelectedIndex ?? 4);
        area.Moisture = ParseDouble(_areaMoistureBox?.Text, area.Moisture, 0.0, 1.0);
        area.Elevation = ParseDouble(_areaElevationBox?.Text, area.Elevation, -200.0, 300.0);

        foreach (var node in _nodes.Where(n => string.Equals(NormalizeAreaName(n.ClusterId), oldName, StringComparison.Ordinal)))
        {
            node.ClusterId = area.Name;
            node.Vegetation = area.Vegetation;
            node.Moisture = area.Moisture;
            node.Elevation = area.Elevation;
        }

        RefreshAreaBox(area.Name);
        RefreshAfterStructureChanged();
    }

    private void ApplyNodeChanges()
    {
        var node = GetSelectedNode();
        if (node == null)
            return;

        string clusterId = NormalizeAreaName(_nodeClusterIdBox?.Text);
        node.ClusterId = clusterId;
        node.Vegetation = ParseVegetation(_nodeVegetationBox?.SelectedIndex ?? 4);
        node.Moisture = ParseDouble(_nodeMoistureBox?.Text, node.Moisture, 0.0, 1.0);
        node.Elevation = ParseDouble(_nodeElevationBox?.Text, node.Elevation, -200.0, 300.0);

        EnsureAreaExists(clusterId, node.Vegetation, node.Moisture, node.Elevation);

        RefreshAreaBox(clusterId);
        RefreshAfterStructureChanged();
    }

    private void ApplyEdgeChanges()
    {
        var edge = GetSelectedEdge();

        if (edge == null)
            return;

        var fromNode = FindNode(edge.FromNodeId);
        var toNode = FindNode(edge.ToNodeId);

        if (fromNode == null || toNode == null)
            return;

        double currentDistance = CalculateDistance(fromNode, toNode);

        double newDistance = ParseDouble(
            _edgeDistanceBox?.Text,
            currentDistance,
            1.0,
            Math.Max(_canvasWidth, _canvasHeight) * 2.0);

        double modifier = ParseDouble(
            _edgeModifierBox?.Text,
            edge.FireSpreadModifier,
            0.02,
            2.50);

        MoveEdgeToDistance(fromNode, toNode, newDistance);

        edge.DistanceOverride = CalculateDistance(fromNode, toNode);
        edge.FireSpreadModifier = modifier;

        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        ScheduleDraw();
    }
    private void MoveEdgeToDistance(
        ClusteredNodeDraftDto fromNode,
        ClusteredNodeDraftDto toNode,
        double targetDistance)
    {
        targetDistance = Math.Clamp(targetDistance, 1.0, Math.Max(_canvasWidth, _canvasHeight) * 2.0);

        double dx = toNode.X - fromNode.X;
        double dy = toNode.Y - fromNode.Y;
        double currentDistance = Math.Sqrt(dx * dx + dy * dy);

        if (currentDistance < 0.001)
        {
            dx = 1.0;
            dy = 0.0;
            currentDistance = 1.0;
        }

        double unitX = dx / currentDistance;
        double unitY = dy / currentDistance;

        int newX = (int)Math.Round(fromNode.X + unitX * targetDistance);
        int newY = (int)Math.Round(fromNode.Y + unitY * targetDistance);

        toNode.X = Math.Clamp(newX, 0, _canvasWidth - 1);
        toNode.Y = Math.Clamp(newY, 0, _canvasHeight - 1);

        UpdateIncidentEdgeDistances(toNode.Id);
    }
    private void DeleteSelectedNode()
    {
        var node = GetSelectedNode();
        if (node == null)
            return;

        _edges.RemoveAll(e => e.FromNodeId == node.Id || e.ToNodeId == node.Id);
        _nodes.Remove(node);

        _selectedNodeId = null;
        _selectedEdgeId = null;
        _pendingEdgeStartNodeId = null;

        RefreshAfterStructureChanged();
    }

    private void DeleteSelectedEdge()
    {
        var edge = GetSelectedEdge();
        if (edge == null)
            return;

        _edges.Remove(edge);
        _selectedEdgeId = null;

        RefreshAfterStructureChanged();
    }

    private void RefreshAfterStructureChanged()
    {
        RemoveBrokenEdges();
        RefreshAreaBox(GetSelectedAreaName());
        RefreshAreaEditor();
        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        ScheduleDraw();
    }

    private void RefreshAreaBox(string? selectedName = null)
    {
        if (_areaBox == null)
            return;

        selectedName ??= GetSelectedAreaName();

        _areaBox.Items.Clear();

        foreach (var area in _areas.OrderBy(a => a.Name))
            _areaBox.Items.Add(area.Name);

        if (_areaBox.Items.Count == 0)
            return;

        int selectedIndex = 0;

        for (int i = 0; i < _areaBox.Items.Count; i++)
        {
            if (string.Equals(_areaBox.Items[i]?.ToString(), selectedName, StringComparison.Ordinal))
            {
                selectedIndex = i;
                break;
            }
        }

        _areaBox.SelectedIndex = selectedIndex;
    }

    private void RefreshAreaEditor()
    {
        var area = GetSelectedArea();

        if (_areaNameBox != null)
            _areaNameBox.Text = area?.Name ?? string.Empty;

        if (_areaVegetationBox != null)
            _areaVegetationBox.SelectedIndex = area == null ? 4 : ToVegetationIndex(area.Vegetation);

        if (_areaMoistureBox != null)
            _areaMoistureBox.Text = area?.Moisture.ToString("0.00", CultureInfo.InvariantCulture) ?? "0.45";

        if (_areaElevationBox != null)
            _areaElevationBox.Text = area?.Elevation.ToString("0.0", CultureInfo.InvariantCulture) ?? "0.0";

        bool canEditAreaList = _graphScaleType != GraphScaleType.Small;

        if (_addAreaButton != null)
            _addAreaButton.IsVisible = canEditAreaList;

        if (_deleteAreaButton != null)
            _deleteAreaButton.IsVisible = canEditAreaList;

        if (_areaBox != null)
            _areaBox.IsEnabled = _areas.Count > 1;
    }

    private void RefreshNodeEditor()
    {
        var node = GetSelectedNode();

        if (_selectedNodeSummaryTextBlock != null)
        {
            _selectedNodeSummaryTextBlock.Text = node == null
                ? "Вершина не выбрана."
                : $"Вершина ({node.X}, {node.Y}) • область {NormalizeAreaName(node.ClusterId)} • степень {GetNodeDegree(node)}";
        }

        if (_nodeClusterIdBox != null)
            _nodeClusterIdBox.Text = node?.ClusterId ?? string.Empty;

        if (_nodeVegetationBox != null)
            _nodeVegetationBox.SelectedIndex = node == null ? 4 : ToVegetationIndex(node.Vegetation);

        if (_nodeMoistureBox != null)
            _nodeMoistureBox.Text = node?.Moisture.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty;

        if (_nodeElevationBox != null)
            _nodeElevationBox.Text = node?.Elevation.ToString("F1", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void RefreshEdgeEditor()
    {
        var edge = GetSelectedEdge();
        var fromNode = edge == null ? null : FindNode(edge.FromNodeId);
        var toNode = edge == null ? null : FindNode(edge.ToNodeId);

        if (_selectedEdgeSummaryTextBlock != null)
        {
            _selectedEdgeSummaryTextBlock.Text = edge == null || fromNode == null || toNode == null
                ? "Ребро не выбрано."
                : $"{NormalizeAreaName(fromNode.ClusterId)} ({fromNode.X}, {fromNode.Y}) → {NormalizeAreaName(toNode.ClusterId)} ({toNode.X}, {toNode.Y})";
        }

        if (_edgeDistanceBox != null)
        {
            if (edge == null || fromNode == null || toNode == null)
                _edgeDistanceBox.Text = string.Empty;
            else
                _edgeDistanceBox.Text = (edge.DistanceOverride ?? CalculateDistance(fromNode, toNode))
                    .ToString("F2", CultureInfo.InvariantCulture);
        }

        if (_edgeModifierBox != null)
            _edgeModifierBox.Text = edge?.FireSpreadModifier.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void RefreshSummary()
    {
        if (_summaryTextBlock == null)
            return;

        int bridgeCount = _edges.Count(edge =>
        {
            var fromNode = FindNode(edge.FromNodeId);
            var toNode = FindNode(edge.ToNodeId);
            return fromNode != null && toNode != null && IsBridge(fromNode, toNode);
        });

        var areaSummary = string.Join(", ", _areas
            .OrderBy(a => a.Name)
            .Select(a => $"{a.Name}: {_nodes.Count(n => NormalizeAreaName(n.ClusterId) == a.Name)}"));

        _summaryTextBlock.Text =
            $"Вершин: {_nodes.Count} • рёбер: {_edges.Count} • мостов: {bridgeCount}\n" +
            $"Области: {areaSummary}";
    }

    private void UpdateHint()
    {
        if (_hintTextBlock == null)
            return;

        if (_pendingEdgeStartNodeId != null)
        {
            _hintTextBlock.Text = GetMode() == EditorMode.CreateBridges
                ? "Выберите вторую вершину из другой области — будет создан мост."
                : "Выберите вторую вершину — будет создано ребро.";
            return;
        }

        _hintTextBlock.Text = GetMode() switch
        {
            EditorMode.SelectAndMove =>
                "ЛКМ по вершине — выбрать. Зажмите ЛКМ и перетащите вершину. ПКМ по вершине — удалить.",

            EditorMode.AddNodes =>
                "ЛКМ по пустому месту — добавить вершину в текущую область. ЛКМ по вершине — выбрать её.",

            EditorMode.CreateEdges =>
                "ЛКМ по первой вершине — начало ребра. ЛКМ по второй вершине — создать обычную связь.",

            EditorMode.CreateBridges =>
                "ЛКМ по вершине одной области, затем по вершине другой области — создать мост между областями.",

            EditorMode.DeleteEdges =>
                "ЛКМ по ребру — удалить связь. ЛКМ по вершине — выбрать её для просмотра свойств.",

            _ =>
                "Выберите режим работы."
        };
    }

    private void AddNode(int x, int y, AreaDraft? area)
    {
        area ??= GetSelectedArea() ?? _areas.FirstOrDefault();

        if (area == null)
        {
            area = new AreaDraft { Name = "A", Vegetation = VegetationType.Mixed, Moisture = 0.45, Elevation = 0.0 };
            _areas.Add(area);
        }

        _nodes.Add(new ClusteredNodeDraftDto
        {
            Id = Guid.NewGuid(),
            X = Math.Clamp(x, 0, _canvasWidth - 1),
            Y = Math.Clamp(y, 0, _canvasHeight - 1),
            ClusterId = area.Name,
            Vegetation = area.Vegetation,
            Moisture = area.Moisture,
            Elevation = area.Elevation
        });
    }

    private void AutoConnectWithinAreas()
    {
        foreach (var area in _areas)
        {
            var areaNodes = _nodes
                .Where(n => string.Equals(NormalizeAreaName(n.ClusterId), area.Name, StringComparison.Ordinal))
                .OrderBy(n => n.X)
                .ThenBy(n => n.Y)
                .ToList();

            for (int i = 0; i < areaNodes.Count - 1; i++)
                AddEdge(areaNodes[i], areaNodes[i + 1], 1.0);

            if (areaNodes.Count >= 3)
                AddEdge(areaNodes[0], areaNodes[^1], 0.95);
        }
    }

    private void AddEdgeBetweenNearestAreas(string firstArea, string secondArea, double modifier)
    {
        var firstNodes = _nodes.Where(n => NormalizeAreaName(n.ClusterId) == firstArea).ToList();
        var secondNodes = _nodes.Where(n => NormalizeAreaName(n.ClusterId) == secondArea).ToList();

        if (firstNodes.Count == 0 || secondNodes.Count == 0)
            return;

        ClusteredNodeDraftDto? bestFirst = null;
        ClusteredNodeDraftDto? bestSecond = null;
        double bestDistance = double.MaxValue;

        foreach (var first in firstNodes)
        {
            foreach (var second in secondNodes)
            {
                double distance = CalculateDistance(first, second);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestFirst = first;
                    bestSecond = second;
                }
            }
        }

        if (bestFirst != null && bestSecond != null)
            AddEdge(bestFirst, bestSecond, modifier);
    }

    private void AddEdge(ClusteredNodeDraftDto fromNode, ClusteredNodeDraftDto toNode, double modifier)
    {
        if (fromNode.Id == toNode.Id || EdgeExists(fromNode.Id, toNode.Id))
            return;

        _edges.Add(new ClusteredEdgeDraftDto
        {
            Id = Guid.NewGuid(),
            FromNodeId = fromNode.Id,
            ToNodeId = toNode.Id,
            DistanceOverride = CalculateDistance(fromNode, toNode),
            FireSpreadModifier = Math.Clamp(modifier, 0.02, 2.50)
        });
    }

    private void RemoveBrokenEdges()
    {
        var nodeIds = _nodes.Select(n => n.Id).ToHashSet();
        _edges.RemoveAll(e => !nodeIds.Contains(e.FromNodeId) || !nodeIds.Contains(e.ToNodeId) || e.FromNodeId == e.ToNodeId);

        var duplicates = _edges
            .GroupBy(e => GetEdgeKey(e.FromNodeId, e.ToNodeId))
            .SelectMany(g => g.Skip(1))
            .ToList();

        foreach (var duplicate in duplicates)
            _edges.Remove(duplicate);
    }

    private EditorMode GetMode()
    {
        return _modeBox?.SelectedIndex switch
        {
            1 => EditorMode.AddNodes,
            2 => EditorMode.CreateEdges,
            3 => EditorMode.CreateBridges,
            4 => EditorMode.DeleteEdges,
            _ => EditorMode.SelectAndMove
        };
    }

    private AreaDraft? GetSelectedArea()
    {
        string name = GetSelectedAreaName();
        return _areas.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.Ordinal));
    }

    private string GetSelectedAreaName()
    {
        string? selected = _areaBox?.SelectedItem?.ToString();
        return NormalizeAreaName(selected ?? _areas.FirstOrDefault()?.Name);
    }

    private ClusteredNodeDraftDto? GetSelectedNode()
    {
        return _selectedNodeId == null ? null : FindNode(_selectedNodeId.Value);
    }

    private ClusteredEdgeDraftDto? GetSelectedEdge()
    {
        return _selectedEdgeId == null ? null : _edges.FirstOrDefault(e => e.Id == _selectedEdgeId.Value);
    }

    private ClusteredNodeDraftDto? FindNode(Guid id)
    {
        return _nodes.FirstOrDefault(n => n.Id == id);
    }

    private ClusteredNodeDraftDto? FindNodeAt(Point point)
    {
        return _nodes
            .Select(n => new { Node = n, Point = ToCanvasPoint(n.X, n.Y) })
            .Where(x => Distance(point, x.Point) <= SelectedNodeRadius + 4)
            .OrderBy(x => Distance(point, x.Point))
            .Select(x => x.Node)
            .FirstOrDefault();
    }

    private ClusteredEdgeDraftDto? FindEdgeAt(Point point)
    {
        return _edges
            .Select(edge =>
            {
                var fromNode = FindNode(edge.FromNodeId);
                var toNode = FindNode(edge.ToNodeId);

                if (fromNode == null || toNode == null)
                    return new { Edge = edge, Distance = double.MaxValue };

                return new
                {
                    Edge = edge,
                    Distance = DistanceToSegment(point, ToCanvasPoint(fromNode.X, fromNode.Y), ToCanvasPoint(toNode.X, toNode.Y))
                };
            })
            .Where(x => x.Distance <= EdgeHitDistance)
            .OrderBy(x => x.Distance)
            .Select(x => x.Edge)
            .FirstOrDefault();
    }

    private void EnsureAreaExists(string name, VegetationType vegetation, double moisture, double elevation)
    {
        name = NormalizeAreaName(name);

        if (_areas.Any(a => string.Equals(a.Name, name, StringComparison.Ordinal)))
            return;

        _areas.Add(new AreaDraft
        {
            Name = name,
            Vegetation = vegetation,
            Moisture = moisture,
            Elevation = elevation
        });
    }

    private bool EdgeExists(Guid firstId, Guid secondId)
    {
        var key = GetEdgeKey(firstId, secondId);
        return _edges.Any(e => GetEdgeKey(e.FromNodeId, e.ToNodeId) == key);
    }

    private static string GetEdgeKey(Guid firstId, Guid secondId)
    {
        return string.CompareOrdinal(firstId.ToString(), secondId.ToString()) < 0
            ? $"{firstId:N}:{secondId:N}"
            : $"{secondId:N}:{firstId:N}";
    }

    private bool IsBridge(ClusteredNodeDraftDto fromNode, ClusteredNodeDraftDto toNode)
    {
        return !string.Equals(
            NormalizeAreaName(fromNode.ClusterId),
            NormalizeAreaName(toNode.ClusterId),
            StringComparison.Ordinal);
    }

    private int GetNodeDegree(ClusteredNodeDraftDto node)
    {
        return _edges.Count(e => e.FromNodeId == node.Id || e.ToNodeId == node.Id);
    }

    private Size GetCanvasSize()
    {
        double baseWidth = Math.Max(900, _canvasWidth * 24.0 + CanvasPadding * 2);
        double baseHeight = Math.Max(650, _canvasHeight * 24.0 + CanvasPadding * 2);

        return new Size(baseWidth * _zoom, baseHeight * _zoom);
    }

    private Point ToCanvasPoint(int x, int y)
    {
        var size = GetCanvasSize();

        double scaleX = (size.Width - CanvasPadding * 2) / Math.Max(1, _canvasWidth - 1);
        double scaleY = (size.Height - CanvasPadding * 2) / Math.Max(1, _canvasHeight - 1);

        return new Point(
            CanvasPadding + x * scaleX,
            CanvasPadding + y * scaleY);
    }

    private (int X, int Y) ToGraphPoint(Point point)
    {
        var size = GetCanvasSize();

        double scaleX = (size.Width - CanvasPadding * 2) / Math.Max(1, _canvasWidth - 1);
        double scaleY = (size.Height - CanvasPadding * 2) / Math.Max(1, _canvasHeight - 1);

        int x = (int)Math.Round((point.X - CanvasPadding) / Math.Max(1.0, scaleX));
        int y = (int)Math.Round((point.Y - CanvasPadding) / Math.Max(1.0, scaleY));

        return (
            Math.Clamp(x, 0, _canvasWidth - 1),
            Math.Clamp(y, 0, _canvasHeight - 1));
    }

    private static double CalculateDistance(ClusteredNodeDraftDto fromNode, ClusteredNodeDraftDto toNode)
    {
        double dx = toNode.X - fromNode.X;
        double dy = toNode.Y - fromNode.Y;
        return Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy));
    }

    private static double Distance(Point first, Point second)
    {
        double dx = first.X - second.X;
        double dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double DistanceToSegment(Point point, Point start, Point end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;

        if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)
            return Distance(point, start);

        double t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0.0, 1.0);

        var projection = new Point(start.X + t * dx, start.Y + t * dy);
        return Distance(point, projection);
    }

    private static double ParseDouble(string? text, double fallback, double min, double max)
    {
        if (!double.TryParse(text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return fallback;

        if (double.IsNaN(value) || double.IsInfinity(value))
            return fallback;

        return Math.Clamp(value, min, max);
    }

    private static VegetationType ParseVegetation(int index)
    {
        return index switch
        {
            0 => VegetationType.Grass,
            1 => VegetationType.Shrub,
            2 => VegetationType.Deciduous,
            3 => VegetationType.Coniferous,
            4 => VegetationType.Mixed,
            5 => VegetationType.Water,
            6 => VegetationType.Bare,
            _ => VegetationType.Mixed
        };
    }

    private static int ToVegetationIndex(VegetationType vegetation)
    {
        return vegetation switch
        {
            VegetationType.Grass => 0,
            VegetationType.Shrub => 1,
            VegetationType.Deciduous => 2,
            VegetationType.Coniferous => 3,
            VegetationType.Mixed => 4,
            VegetationType.Water => 5,
            VegetationType.Bare => 6,
            _ => 4
        };
    }

    private static string NormalizeAreaName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "A";

        return name.Trim();
    }

    private static ClusteredNodeDraftDto CloneNode(ClusteredNodeDraftDto source)
    {
        return new ClusteredNodeDraftDto
        {
            Id = source.Id,
            X = source.X,
            Y = source.Y,
            ClusterId = NormalizeAreaName(source.ClusterId),
            Vegetation = source.Vegetation,
            Moisture = Math.Clamp(source.Moisture, 0.0, 1.0),
            Elevation = source.Elevation
        };
    }

    private static ClusteredEdgeDraftDto CloneEdge(ClusteredEdgeDraftDto source)
    {
        return new ClusteredEdgeDraftDto
        {
            Id = source.Id,
            FromNodeId = source.FromNodeId,
            ToNodeId = source.ToNodeId,
            DistanceOverride = source.DistanceOverride,
            FireSpreadModifier = Math.Clamp(source.FireSpreadModifier, 0.02, 2.50)
        };
    }

    private string GetVegetationText(VegetationType vegetation)
    {
        return vegetation switch
        {
            VegetationType.Grass => "Трава",
            VegetationType.Shrub => "Кустарник",
            VegetationType.Deciduous => "Лиственный лес",
            VegetationType.Coniferous => "Хвойный лес",
            VegetationType.Mixed => "Смешанный лес",
            VegetationType.Water => "Вода",
            VegetationType.Bare => "Пустая поверхность",
            _ => vegetation.ToString()
        };
    }

    private string BuildNodeTooltip(ClusteredNodeDraftDto node)
    {
        return
            $"Вершина: ({node.X}, {node.Y})\n" +
            $"Область: {NormalizeAreaName(node.ClusterId)}\n" +
            $"Растительность: {GetVegetationText(node.Vegetation)}\n" +
            $"Влажность: {node.Moisture:F2}\n" +
            $"Высота: {node.Elevation:F1}\n" +
            $"Связей: {GetNodeDegree(node)}";
    }

    private string BuildEdgeTooltip(
        ClusteredEdgeDraftDto edge,
        ClusteredNodeDraftDto fromNode,
        ClusteredNodeDraftDto toNode,
        bool isBridge)
    {
        double distance = edge.DistanceOverride ?? CalculateDistance(fromNode, toNode);

        return
            $"Ребро\n" +
            $"От: {NormalizeAreaName(fromNode.ClusterId)} ({fromNode.X}, {fromNode.Y})\n" +
            $"К: {NormalizeAreaName(toNode.ClusterId)} ({toNode.X}, {toNode.Y})\n" +
            $"Тип: {(isBridge ? "мост между областями" : "локальная связь")}\n" +
            $"Расстояние: {distance:F2}\n" +
            $"Коэффициент: {edge.FireSpreadModifier:F2}";
    }

    private Color GetNodeColor(ClusteredNodeDraftDto node)
    {
        return node.Vegetation switch
        {
            VegetationType.Coniferous => Color.Parse("#5E9B5E"),
            VegetationType.Deciduous => Color.Parse("#8ACB88"),
            VegetationType.Mixed => Color.Parse("#A8C97F"),
            VegetationType.Grass => Color.Parse("#E7D36F"),
            VegetationType.Shrub => Color.Parse("#CFA46A"),
            VegetationType.Water => Color.Parse("#7CC6F2"),
            VegetationType.Bare => Color.Parse("#C9B7A7"),
            _ => Color.Parse("#A8C97F")
        };
    }
}