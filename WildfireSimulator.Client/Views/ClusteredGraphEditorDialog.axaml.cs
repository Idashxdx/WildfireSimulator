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
        SelectNodes = 0,
        CreateEdges = 1,
        DeleteEdges = 2
    }

    private const double CanvasPadding = 34.0;
    private const double CandidateRadius = 4.0;
    private const double NodeRadius = 8.5;

    private Canvas? _graphCanvas;
    private ComboBox? _modeBox;
    private TextBox? _candidateCountBox;
    private Button? _regenerateCandidatesButton;
    private Button? _autoConnectButton;

    private TextBox? _autoGroupCountBox;
    private Button? _autoGroupButton;
    private Button? _normalizeEdgesButton;
    private Button? _weakenBridgesButton;
    private Button? _boostLocalEdgesButton;

    private TextBlock? _selectedNodeSummaryTextBlock;
    private TextBox? _nodeClusterIdBox;
    private ComboBox? _nodeVegetationBox;
    private TextBox? _nodeMoistureBox;
    private TextBox? _nodeElevationBox;
    private Button? _applyNodeButton;

    private TextBlock? _selectedEdgeSummaryTextBlock;
    private TextBox? _edgeDistanceBox;
    private TextBox? _edgeModifierBox;
    private Button? _applyEdgeButton;

    private TextBlock? _summaryTextBlock;
    private TextBlock? _hintTextBlock;

    private Button? _cancelButton;
    private Button? _applyButton;

    private readonly int _canvasWidth;
    private readonly int _canvasHeight;

    private readonly List<ClusteredCandidateNodeDto> _candidates = new();
    private readonly List<ClusteredNodeDraftDto> _nodes = new();
    private readonly List<ClusteredEdgeDraftDto> _edges = new();

    private Guid? _selectedNodeId;
    private Guid? _selectedEdgeId;
    private Guid? _pendingEdgeStartNodeId;

    private readonly Random _random = new();

    public ClusteredGraphBlueprintDto EditedBlueprint { get; private set; } = new();

    public ClusteredGraphEditorDialog(
    int canvasWidth,
    int canvasHeight,
    ClusteredGraphBlueprintDto? existingBlueprint = null)
    {
        _canvasWidth = Math.Max(8, canvasWidth);
        _canvasHeight = Math.Max(8, canvasHeight);

        InitializeComponent();
        FindControls();
        AttachEvents();

        if (existingBlueprint != null)
        {
            LoadBlueprint(existingBlueprint);
        }
        else
        {
            RegenerateCandidates(90);
        }

        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        ScheduleDraw();
    }


    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

private bool _drawScheduled;

    private void ScheduleDraw()
    {
        if (_graphCanvas == null)
            return;

        if (_drawScheduled)
            return;

        _drawScheduled = true;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _drawScheduled = false;
            DrawGraph();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void FindControls()
    {
        _graphCanvas = this.FindControl<Canvas>("GraphCanvas");
        _modeBox = this.FindControl<ComboBox>("ModeBox");
        _candidateCountBox = this.FindControl<TextBox>("CandidateCountBox");
        _regenerateCandidatesButton = this.FindControl<Button>("RegenerateCandidatesButton");
        _autoConnectButton = this.FindControl<Button>("AutoConnectButton");

        _autoGroupCountBox = this.FindControl<TextBox>("AutoGroupCountBox");
        _autoGroupButton = this.FindControl<Button>("AutoGroupButton");
        _normalizeEdgesButton = this.FindControl<Button>("NormalizeEdgesButton");
        _weakenBridgesButton = this.FindControl<Button>("WeakenBridgesButton");
        _boostLocalEdgesButton = this.FindControl<Button>("BoostLocalEdgesButton");

        _selectedNodeSummaryTextBlock = this.FindControl<TextBlock>("SelectedNodeSummaryTextBlock");
        _nodeClusterIdBox = this.FindControl<TextBox>("NodeClusterIdBox");
        _nodeVegetationBox = this.FindControl<ComboBox>("NodeVegetationBox");
        _nodeMoistureBox = this.FindControl<TextBox>("NodeMoistureBox");
        _nodeElevationBox = this.FindControl<TextBox>("NodeElevationBox");
        _applyNodeButton = this.FindControl<Button>("ApplyNodeButton");

        _selectedEdgeSummaryTextBlock = this.FindControl<TextBlock>("SelectedEdgeSummaryTextBlock");
        _edgeDistanceBox = this.FindControl<TextBox>("EdgeDistanceBox");
        _edgeModifierBox = this.FindControl<TextBox>("EdgeModifierBox");
        _applyEdgeButton = this.FindControl<Button>("ApplyEdgeButton");

        _summaryTextBlock = this.FindControl<TextBlock>("SummaryTextBlock");
        _hintTextBlock = this.FindControl<TextBlock>("HintTextBlock");

        _cancelButton = this.FindControl<Button>("CancelButton");
        _applyButton = this.FindControl<Button>("ApplyButton");
    }

    private void AttachEvents()
    {
        if (_regenerateCandidatesButton != null)
        {
            _regenerateCandidatesButton.Click += (_, _) =>
            {
                int count = ParseInt(_candidateCountBox?.Text, 90);
                RegenerateCandidates(Math.Clamp(count, 20, 300));
                ScheduleDraw();
            };
        }

        if (_autoConnectButton != null)
        {
            _autoConnectButton.Click += (_, _) =>
            {
                AutoConnectSelectedNodes();
                ScheduleDraw();
            };
        }

        if (_autoGroupButton != null)
        {
            _autoGroupButton.Click += (_, _) =>
            {
                AutoAssignClusters();
                ScheduleDraw();
            };
        }

        if (_normalizeEdgesButton != null)
        {
            _normalizeEdgesButton.Click += (_, _) =>
            {
                NormalizeEdges();
                ScheduleDraw();
            };
        }

        if (_weakenBridgesButton != null)
        {
            _weakenBridgesButton.Click += (_, _) =>
            {
                WeakenBridges();
                ScheduleDraw();
            };
        }

        if (_boostLocalEdgesButton != null)
        {
            _boostLocalEdgesButton.Click += (_, _) =>
            {
                BoostLocalEdges();
                ScheduleDraw();
            };
        }

        if (_applyNodeButton != null)
        {
            _applyNodeButton.Click += (_, _) =>
            {
                ApplyNodeChanges();
                ScheduleDraw();
            };
        }

        if (_applyEdgeButton != null)
        {
            _applyEdgeButton.Click += (_, _) =>
            {
                ApplyEdgeChanges();
                ScheduleDraw();
            };
        }

        if (_cancelButton != null)
            _cancelButton.Click += (_, _) => Close(false);

        if (_applyButton != null)
        {
            _applyButton.Click += (_, _) =>
            {
                EditedBlueprint = BuildBlueprint();
                Close(true);
            };
        }
    }


    private void LoadBlueprint(ClusteredGraphBlueprintDto blueprint)
    {
        _candidates.Clear();
        _nodes.Clear();
        _edges.Clear();

        if (blueprint.Candidates != null)
            _candidates.AddRange(blueprint.Candidates.Select(CloneCandidate));

        if (blueprint.Nodes != null)
            _nodes.AddRange(blueprint.Nodes.Select(CloneNode));

        if (blueprint.Edges != null)
            _edges.AddRange(blueprint.Edges.Select(CloneEdge));

        if (_candidates.Count == 0)
            RegenerateCandidates(Math.Max(60, _nodes.Count * 2));
    }

    private ClusteredGraphBlueprintDto BuildBlueprint()
    {
        return new ClusteredGraphBlueprintDto
        {
            CanvasWidth = _canvasWidth,
            CanvasHeight = _canvasHeight,
            Candidates = _candidates.Select(CloneCandidate).ToList(),
            Nodes = _nodes.Select(CloneNode).ToList(),
            Edges = _edges.Select(CloneEdge).ToList()
        };
    }

    private void RegenerateCandidates(int count)
    {
        var preservedNodes = _nodes.Select(CloneNode).ToList();
        var preservedEdges = _edges
            .Where(e =>
                preservedNodes.Any(n => n.Id == e.FromNodeId) &&
                preservedNodes.Any(n => n.Id == e.ToNodeId))
            .Select(CloneEdge)
            .ToList();

        var preservedNodePoints = preservedNodes
            .Select(x => (x.X, x.Y))
            .ToHashSet();

        _candidates.Clear();
        _nodes.Clear();
        _edges.Clear();

        foreach (var node in preservedNodes)
            _nodes.Add(node);

        foreach (var edge in preservedEdges)
            _edges.Add(edge);

        int totalCapacity = Math.Max(1, _canvasWidth * _canvasHeight);
        int requestedCount = Math.Clamp(count, 1, totalCapacity);

        var used = new HashSet<(int X, int Y)>(preservedNodePoints);

        while (_candidates.Count < requestedCount && used.Count < totalCapacity)
        {
            int x = _random.Next(0, _canvasWidth);
            int y = _random.Next(0, _canvasHeight);

            if (!used.Add((x, y)))
                continue;

            _candidates.Add(new ClusteredCandidateNodeDto
            {
                Id = Guid.NewGuid(),
                X = x,
                Y = y
            });
        }

        foreach (var node in preservedNodes)
        {
            if (!_candidates.Any(c => c.X == node.X && c.Y == node.Y))
            {
                _candidates.Add(new ClusteredCandidateNodeDto
                {
                    Id = Guid.NewGuid(),
                    X = node.X,
                    Y = node.Y
                });
            }
        }

        if (_candidateCountBox != null)
            _candidateCountBox.Text = requestedCount.ToString(CultureInfo.InvariantCulture);

        _selectedNodeId = null;
        _selectedEdgeId = null;
        _pendingEdgeStartNodeId = null;

        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
    }

    private void DrawGraph()
    {
        if (_graphCanvas == null)
            return;

        _graphCanvas.Children.Clear();

        DrawBackground();
        DrawEdges();
        DrawCandidates();
        DrawNodes();
        DrawPendingEdgeHint();
    }


    private void DrawBackground()
    {
        if (_graphCanvas == null)
            return;

        double scale = GetScale();
        double width = _canvasWidth * scale + CanvasPadding * 2;
        double height = _canvasHeight * scale + CanvasPadding * 2;

        _graphCanvas.Width = Math.Max(900, width);
        _graphCanvas.Height = Math.Max(790, height);

        var background = new Rectangle
        {
            Width = _graphCanvas.Width,
            Height = _graphCanvas.Height,
            Fill = new SolidColorBrush(Color.Parse("#FBF9F4"))
        };

        _graphCanvas.Children.Add(background);
    }

    private void DrawCandidates()
    {
        if (_graphCanvas == null)
            return;

        var selectedNodePoints = _nodes
            .Select(n => (n.X, n.Y))
            .ToHashSet();

        foreach (var candidate in _candidates)
        {
            bool isSelectedNode = selectedNodePoints.Contains((candidate.X, candidate.Y));
            var point = ToCanvasPoint(candidate.X, candidate.Y);

            var ellipse = new Ellipse
            {
                Width = CandidateRadius * 2,
                Height = CandidateRadius * 2,
                Fill = new SolidColorBrush(
                    isSelectedNode
                        ? Color.Parse("#D9EFC7")
                        : Color.Parse("#C9C6CE")),
                Stroke = new SolidColorBrush(
                    isSelectedNode
                        ? Color.Parse("#7E9F46")
                        : Color.Parse("#8C8794")),
                StrokeThickness = isSelectedNode ? 1.4 : 1.0,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            ToolTip.SetTip(ellipse, $"Кандидат ({candidate.X}, {candidate.Y})");

            Canvas.SetLeft(ellipse, point.X - CandidateRadius);
            Canvas.SetTop(ellipse, point.Y - CandidateRadius);

            ellipse.PointerPressed += (_, _) =>
            {
                HandleCandidateClick(candidate);
            };

            _graphCanvas.Children.Add(ellipse);
        }
    }

    private void DrawNodes()
    {
        if (_graphCanvas == null)
            return;

        foreach (var node in _nodes)
        {
            var point = ToCanvasPoint(node.X, node.Y);
            bool isSelected = node.Id == _selectedNodeId;
            bool isPendingStart = node.Id == _pendingEdgeStartNodeId;

            var shape = new Ellipse
            {
                Width = NodeRadius * 2,
                Height = NodeRadius * 2,
                Fill = new SolidColorBrush(GetNodeColor(node)),
                Stroke = new SolidColorBrush(
                    isSelected
                        ? Color.Parse("#355CBE")
                        : isPendingStart
                            ? Color.Parse("#D6402B")
                            : Color.Parse("#F8F5EE")),
                StrokeThickness = isSelected ? 2.6 : isPendingStart ? 2.4 : 1.4,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            ToolTip.SetTip(shape,
                $"Узел ({node.X}, {node.Y})\n" +
                $"Cluster: {node.ClusterId}\n" +
                $"Растительность: {node.Vegetation}\n" +
                $"Влажность: {node.Moisture:F2}\n" +
                $"Высота: {node.Elevation:F1}");

            shape.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(shape).Properties.IsLeftButtonPressed)
                    return;

                HandleNodeClick(node);
                e.Handled = true;
            };

            Canvas.SetLeft(shape, point.X - NodeRadius);
            Canvas.SetTop(shape, point.Y - NodeRadius);
            _graphCanvas.Children.Add(shape);
        }
    }

    private void DrawEdges()
    {
        if (_graphCanvas == null)
            return;

        foreach (var edge in _edges)
        {
            var fromNode = _nodes.FirstOrDefault(n => n.Id == edge.FromNodeId);
            var toNode = _nodes.FirstOrDefault(n => n.Id == edge.ToNodeId);

            if (fromNode == null || toNode == null)
                continue;

            var fromPoint = ToCanvasPoint(fromNode.X, fromNode.Y);
            var toPoint = ToCanvasPoint(toNode.X, toNode.Y);

            bool isSelected = edge.Id == _selectedEdgeId;
            bool isBridge = !string.Equals(fromNode.ClusterId, toNode.ClusterId, StringComparison.Ordinal);
            double modifier = Math.Clamp(edge.FireSpreadModifier, 0.02, 1.85);

            var line = new Line
            {
                StartPoint = fromPoint,
                EndPoint = toPoint,
                Stroke = new SolidColorBrush(
                    isSelected
                        ? Color.Parse("#D6402B")
                        : isBridge
                            ? Color.Parse("#7DA6C7")
                            : modifier >= 1.05
                                ? Color.Parse("#8E7CC3")
                                : Color.Parse("#A89FB7")),
                StrokeThickness = isSelected ? 3.6 : isBridge ? 2.2 : modifier >= 1.05 ? 2.5 : 1.8,
                StrokeDashArray = isBridge ? new AvaloniaList<double> { 5, 3 } : null,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            ToolTip.SetTip(line,
                $"Ребро\n" +
                $"Bridge: {(isBridge ? "yes" : "no")}\n" +
                $"Modifier: {edge.FireSpreadModifier:F2}\n" +
                $"Distance override: {(edge.DistanceOverride.HasValue ? edge.DistanceOverride.Value.ToString("F2", CultureInfo.InvariantCulture) : "auto")}");

            line.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(line).Properties.IsLeftButtonPressed)
                    return;

                HandleEdgeClick(edge);
                e.Handled = true;
            };

            _graphCanvas.Children.Add(line);
        }
    }

    private void DrawPendingEdgeHint()
    {
        if (_graphCanvas == null || _pendingEdgeStartNodeId == null)
            return;

        var node = _nodes.FirstOrDefault(n => n.Id == _pendingEdgeStartNodeId.Value);
        if (node == null)
            return;

        var point = ToCanvasPoint(node.X, node.Y);

        var glow = new Ellipse
        {
            Width = (NodeRadius + 5) * 2,
            Height = (NodeRadius + 5) * 2,
            Fill = new SolidColorBrush(Color.FromArgb(60, 255, 230, 120)),
            Stroke = new SolidColorBrush(Color.Parse("#D6402B")),
            StrokeThickness = 1.8,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(glow, point.X - (NodeRadius + 5));
        Canvas.SetTop(glow, point.Y - (NodeRadius + 5));
        _graphCanvas.Children.Add(glow);
    }

    private void HandleCandidateClick(ClusteredCandidateNodeDto candidate)
    {
        var existingNode = _nodes.FirstOrDefault(n => n.X == candidate.X && n.Y == candidate.Y);

        if (existingNode != null)
        {
            var removedNodeId = existingNode.Id;
            _nodes.Remove(existingNode);
            _edges.RemoveAll(e => e.FromNodeId == removedNodeId || e.ToNodeId == removedNodeId);

            if (_selectedNodeId == removedNodeId)
                _selectedNodeId = null;

            if (_pendingEdgeStartNodeId == removedNodeId)
                _pendingEdgeStartNodeId = null;
        }
        else
        {
            var node = new ClusteredNodeDraftDto
            {
                Id = Guid.NewGuid(),
                X = candidate.X,
                Y = candidate.Y,
                ClusterId = $"patch-manual-{GetSuggestedClusterIndex(candidate)}",
                Vegetation = VegetationType.Mixed,
                Moisture = 0.45,
                Elevation = 0.0
            };

            _nodes.Add(node);
            _selectedNodeId = node.Id;
        }

        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        ScheduleDraw();
    }


    private void HandleNodeClick(ClusteredNodeDraftDto node)
    {
        var mode = GetMode();

        if (mode == EditorMode.CreateEdges)
        {
            if (_pendingEdgeStartNodeId == null)
            {
                _pendingEdgeStartNodeId = node.Id;
                _selectedNodeId = node.Id;
                _selectedEdgeId = null;
            }
            else if (_pendingEdgeStartNodeId == node.Id)
            {
                _pendingEdgeStartNodeId = null;
                _selectedNodeId = node.Id;
            }
            else
            {
                var fromId = _pendingEdgeStartNodeId.Value;
                var toId = node.Id;

                if (!TryAddEdge(fromId, toId))
                    _selectedEdgeId = FindEdgeId(fromId, toId);

                _pendingEdgeStartNodeId = null;
                _selectedNodeId = node.Id;
            }
        }
        else
        {
            _selectedNodeId = node.Id;
            _selectedEdgeId = null;
            _pendingEdgeStartNodeId = null;
        }

        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        ScheduleDraw();
    }
private void HandleEdgeClick(ClusteredEdgeDraftDto edge)
{
    var mode = GetMode();

    if (mode == EditorMode.DeleteEdges)
    {
        _edges.RemoveAll(e => e.Id == edge.Id);
        if (_selectedEdgeId == edge.Id)
            _selectedEdgeId = null;
    }
    else
    {
        _selectedEdgeId = edge.Id;
        _selectedNodeId = null;
    }

    RefreshNodeEditor();
    RefreshEdgeEditor();
    RefreshSummary();
    ScheduleDraw();
}
    private void AutoConnectSelectedNodes()
    {
        if (_nodes.Count < 2)
            return;

        var orderedPairs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in _nodes)
        {
            var nearest = _nodes
                .Where(n => n.Id != node.Id)
                .Select(other => new
                {
                    Node = other,
                    Distance = GetDistance(node.X, node.Y, other.X, other.Y)
                })
                .OrderBy(x => x.Distance)
                .Take(3)
                .ToList();

            foreach (var item in nearest)
            {
                var a = node.Id.ToString();
                var b = item.Node.Id.ToString();
                var key = string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

                if (!orderedPairs.Add(key))
                    continue;

                TryAddEdge(node.Id, item.Node.Id);
            }
        }

        RefreshSummary();
        RefreshEdgeEditor();
    }


    private void AutoAssignClusters()
    {
        if (_nodes.Count == 0)
            return;

        int groupCount = Math.Clamp(ParseInt(_autoGroupCountBox?.Text, 4), 2, 12);

        var ordered = _nodes
            .OrderBy(n => n.X)
            .ThenBy(n => n.Y)
            .ToList();

        int bucketSize = Math.Max(1, (int)Math.Ceiling((double)ordered.Count / groupCount));

        for (int i = 0; i < ordered.Count; i++)
        {
            int clusterIndex = i / bucketSize + 1;
            ordered[i].ClusterId = $"patch-manual-{clusterIndex}";
        }

        RefreshNodeEditor();
        RefreshSummary();
    }

    private void NormalizeEdges()
    {
        foreach (var edge in _edges)
            edge.FireSpreadModifier = 1.0;

        RefreshEdgeEditor();
        RefreshSummary();
    }

    private void WeakenBridges()
    {
        foreach (var edge in _edges)
        {
            var fromNode = _nodes.FirstOrDefault(n => n.Id == edge.FromNodeId);
            var toNode = _nodes.FirstOrDefault(n => n.Id == edge.ToNodeId);

            if (fromNode == null || toNode == null)
                continue;

            bool isBridge = !string.Equals(fromNode.ClusterId, toNode.ClusterId, StringComparison.Ordinal);
            if (isBridge)
                edge.FireSpreadModifier = Math.Clamp(edge.FireSpreadModifier * 0.70, 0.02, 1.85);
        }

        RefreshEdgeEditor();
        RefreshSummary();
    }

    private void BoostLocalEdges()
    {
        foreach (var edge in _edges)
        {
            var fromNode = _nodes.FirstOrDefault(n => n.Id == edge.FromNodeId);
            var toNode = _nodes.FirstOrDefault(n => n.Id == edge.ToNodeId);

            if (fromNode == null || toNode == null)
                continue;

            bool sameCluster = string.Equals(fromNode.ClusterId, toNode.ClusterId, StringComparison.Ordinal);
            if (sameCluster)
                edge.FireSpreadModifier = Math.Clamp(edge.FireSpreadModifier * 1.15, 0.02, 1.85);
        }

        RefreshEdgeEditor();
        RefreshSummary();
    }

    private bool TryAddEdge(Guid fromId, Guid toId)
    {
        if (fromId == toId)
            return false;

        bool exists = _edges.Any(e =>
            (e.FromNodeId == fromId && e.ToNodeId == toId) ||
            (e.FromNodeId == toId && e.ToNodeId == fromId));

        if (exists)
            return false;

        _edges.Add(new ClusteredEdgeDraftDto
        {
            Id = Guid.NewGuid(),
            FromNodeId = fromId,
            ToNodeId = toId,
            DistanceOverride = null,
            FireSpreadModifier = 1.0
        });

        return true;
    }

    private Guid? FindEdgeId(Guid fromId, Guid toId)
    {
        return _edges
            .FirstOrDefault(e =>
                (e.FromNodeId == fromId && e.ToNodeId == toId) ||
                (e.FromNodeId == toId && e.ToNodeId == fromId))
            ?.Id;
    }

    private void ApplyNodeChanges()
    {
        if (_selectedNodeId == null)
            return;

        var node = _nodes.FirstOrDefault(n => n.Id == _selectedNodeId.Value);
        if (node == null)
            return;

        node.ClusterId = (_nodeClusterIdBox?.Text ?? string.Empty).Trim();
        node.Vegetation = GetSelectedVegetation();
        node.Moisture = Math.Clamp(ParseDouble(_nodeMoistureBox?.Text, node.Moisture), 0.02, 0.98);
        node.Elevation = ParseDouble(_nodeElevationBox?.Text, node.Elevation);

        RefreshNodeEditor();
        RefreshSummary();
    }

    private void ApplyEdgeChanges()
    {
        if (_selectedEdgeId == null)
            return;

        var edge = _edges.FirstOrDefault(e => e.Id == _selectedEdgeId.Value);
        if (edge == null)
            return;

        var distanceText = (_edgeDistanceBox?.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(distanceText))
            edge.DistanceOverride = null;
        else
            edge.DistanceOverride = Math.Max(0.1, ParseDouble(distanceText, edge.DistanceOverride ?? 1.0));

        edge.FireSpreadModifier = Math.Clamp(
            ParseDouble(_edgeModifierBox?.Text, edge.FireSpreadModifier),
            0.02,
            1.85);

        RefreshEdgeEditor();
        RefreshSummary();
    }

    private void RefreshNodeEditor()
    {
        var node = _nodes.FirstOrDefault(n => n.Id == _selectedNodeId);

        if (node == null)
        {
            if (_selectedNodeSummaryTextBlock != null)
                _selectedNodeSummaryTextBlock.Text = "Узел не выбран";

            if (_nodeClusterIdBox != null)
                _nodeClusterIdBox.Text = "patch-manual-1";

            if (_nodeMoistureBox != null)
                _nodeMoistureBox.Text = "0.45";

            if (_nodeElevationBox != null)
                _nodeElevationBox.Text = "0";

            if (_nodeVegetationBox != null)
                _nodeVegetationBox.SelectedIndex = 4;

            return;
        }

        if (_selectedNodeSummaryTextBlock != null)
            _selectedNodeSummaryTextBlock.Text = $"Узел ({node.X}, {node.Y}) • {node.ClusterId}";

        if (_nodeClusterIdBox != null)
            _nodeClusterIdBox.Text = node.ClusterId;

        if (_nodeMoistureBox != null)
            _nodeMoistureBox.Text = node.Moisture.ToString("0.00", CultureInfo.InvariantCulture);

        if (_nodeElevationBox != null)
            _nodeElevationBox.Text = node.Elevation.ToString("0.0", CultureInfo.InvariantCulture);

        if (_nodeVegetationBox != null)
        {
            _nodeVegetationBox.SelectedIndex = node.Vegetation switch
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
    }

    private void RefreshEdgeEditor()
    {
        var edge = _edges.FirstOrDefault(e => e.Id == _selectedEdgeId);

        if (edge == null)
        {
            if (_selectedEdgeSummaryTextBlock != null)
                _selectedEdgeSummaryTextBlock.Text = "Ребро не выбрано";

            if (_edgeDistanceBox != null)
                _edgeDistanceBox.Text = string.Empty;

            if (_edgeModifierBox != null)
                _edgeModifierBox.Text = "1.00";

            return;
        }

        var fromNode = _nodes.FirstOrDefault(n => n.Id == edge.FromNodeId);
        var toNode = _nodes.FirstOrDefault(n => n.Id == edge.ToNodeId);

        if (_selectedEdgeSummaryTextBlock != null)
        {
            _selectedEdgeSummaryTextBlock.Text = fromNode != null && toNode != null
                ? $"Ребро ({fromNode.X}, {fromNode.Y}) ↔ ({toNode.X}, {toNode.Y})"
                : "Ребро выбрано";
        }

        if (_edgeDistanceBox != null)
            _edgeDistanceBox.Text = edge.DistanceOverride?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;

        if (_edgeModifierBox != null)
            _edgeModifierBox.Text = edge.FireSpreadModifier.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private void RefreshSummary()
    {
        int clusterCount = _nodes
            .Select(n => n.ClusterId?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Count();

        int bridgeCount = _edges.Count(e =>
        {
            var fromNode = _nodes.FirstOrDefault(n => n.Id == e.FromNodeId);
            var toNode = _nodes.FirstOrDefault(n => n.Id == e.ToNodeId);

            if (fromNode == null || toNode == null)
                return false;

            return !string.Equals(fromNode.ClusterId, toNode.ClusterId, StringComparison.Ordinal);
        });

        if (_summaryTextBlock != null)
        {
            _summaryTextBlock.Text =
                $"Выбрано узлов: {_nodes.Count} • рёбер: {_edges.Count} • групп: {clusterCount} • мостов: {bridgeCount}";
        }

        if (_hintTextBlock != null)
        {
            _hintTextBlock.Text = GetMode() switch
            {
                EditorMode.SelectNodes =>
                    "Кликайте по серым точкам: клик добавляет узел, повторный клик снимает его.",
                EditorMode.CreateEdges =>
                    "Кликните по первому выбранному узлу, затем по второму — между ними появится ребро.",
                EditorMode.DeleteEdges =>
                    "Кликайте по рёбрам, чтобы удалить их.",
                _ => "Редактирование clustered graph."
            };
        }
    }

    private EditorMode GetMode()
    {
        return _modeBox?.SelectedIndex switch
        {
            1 => EditorMode.CreateEdges,
            2 => EditorMode.DeleteEdges,
            _ => EditorMode.SelectNodes
        };
    }

    private Point ToCanvasPoint(int x, int y)
    {
        double scale = GetScale();
        return new Point(
            CanvasPadding + x * scale,
            CanvasPadding + y * scale);
    }

    private double GetScale()
    {
        double usableWidth = 780.0;
        double usableHeight = 690.0;

        double scaleX = usableWidth / Math.Max(1, _canvasWidth - 1);
        double scaleY = usableHeight / Math.Max(1, _canvasHeight - 1);

        return Math.Max(16.0, Math.Min(scaleX, scaleY));
    }

    private int GetSuggestedClusterIndex(ClusteredCandidateNodeDto candidate)
    {
        int horizontalBand = Math.Max(0, candidate.X / Math.Max(1, _canvasWidth / 4));
        int verticalBand = Math.Max(0, candidate.Y / Math.Max(1, _canvasHeight / 3));
        return verticalBand * 4 + horizontalBand + 1;
    }

    private static int ParseInt(string? text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static double ParseDouble(string? text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static double GetDistance(int x1, int y1, int x2, int y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private VegetationType GetSelectedVegetation()
    {
        return _nodeVegetationBox?.SelectedIndex switch
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

    private static ClusteredCandidateNodeDto CloneCandidate(ClusteredCandidateNodeDto source)
    {
        return new ClusteredCandidateNodeDto
        {
            Id = source.Id,
            X = source.X,
            Y = source.Y
        };
    }

    private static ClusteredNodeDraftDto CloneNode(ClusteredNodeDraftDto source)
    {
        return new ClusteredNodeDraftDto
        {
            Id = source.Id,
            X = source.X,
            Y = source.Y,
            ClusterId = source.ClusterId,
            Vegetation = source.Vegetation,
            Moisture = source.Moisture,
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
            FireSpreadModifier = source.FireSpreadModifier
        };
    }

    private Color GetNodeColor(ClusteredNodeDraftDto node)
    {
        return node.Vegetation switch
        {
            VegetationType.Grass => Color.Parse("#C7E6A3"),
            VegetationType.Shrub => Color.Parse("#A9C97D"),
            VegetationType.Deciduous => Color.Parse("#82B366"),
            VegetationType.Coniferous => Color.Parse("#4F8A5B"),
            VegetationType.Mixed => Color.Parse("#7E9F78"),
            VegetationType.Water => Color.Parse("#8EC5FF"),
            VegetationType.Bare => Color.Parse("#D7C4A4"),
            _ => Color.Parse("#B8B4BE")
        };
    }
}