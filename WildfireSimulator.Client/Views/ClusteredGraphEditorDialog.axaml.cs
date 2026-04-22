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
    private const double ManualPlacementSnapDistance = 0.42;

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
            LoadBlueprint(existingBlueprint);
        else
            RegenerateCandidates(90);

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
        if (_graphCanvas != null)
        {
            _graphCanvas.PointerPressed += OnCanvasPointerPressed;
        }

        if (_modeBox != null)
        {
            _modeBox.SelectionChanged += (_, _) =>
            {
                _pendingEdgeStartNodeId = null;
                _selectedEdgeId = null;

                if (_hintTextBlock != null)
                {
                    _hintTextBlock.Text = GetMode() switch
                    {
                        EditorMode.SelectNodes =>
                            "ЛКМ по canvas — создать узел. ЛКМ по узлу — выбрать. ПКМ по узлу — удалить узел вместе со связями.",
                        EditorMode.CreateEdges =>
                            "ЛКМ по первому узлу — начало ребра. ЛКМ по второму — создать ребро. Повторный клик по тому же узлу отменяет выбор.",
                        EditorMode.DeleteEdges =>
                            "ЛКМ по ребру — удалить связь. ЛКМ по узлу — только выбрать его для просмотра свойств.",
                        _ =>
                            "Выберите режим редактирования."
                    };
                }

                RefreshSummary();
                RefreshEdgeEditor();
                RefreshNodeEditor();
                ScheduleDraw();
            };
        }

        if (_regenerateCandidatesButton != null)
        {
            _regenerateCandidatesButton.Click += (_, _) =>
            {
                int count = ParseInt(_candidateCountBox?.Text, 90);
                RegenerateCandidates(Math.Clamp(count, 20, 300));

                if (_hintTextBlock != null)
                    _hintTextBlock.Text = $"Кандидатные точки обновлены: {_candidates.Count}. Уже созданные узлы и рёбра сохранены.";

                ScheduleDraw();
            };
        }

        if (_autoConnectButton != null)
        {
            _autoConnectButton.Click += (_, _) =>
            {
                AutoConnectSelectedNodes();

                if (_hintTextBlock != null)
                    _hintTextBlock.Text = "Автосвязь выполнена: ближайшие узлы соединены рёбрами.";

                ScheduleDraw();
            };
        }

        if (_autoGroupButton != null)
        {
            _autoGroupButton.Click += (_, _) =>
            {
                AutoAssignClusters();

                if (_hintTextBlock != null)
                    _hintTextBlock.Text = "Узлы автоматически распределены по cluster ID.";

                ScheduleDraw();
            };
        }

        if (_normalizeEdgesButton != null)
        {
            _normalizeEdgesButton.Click += (_, _) =>
            {
                NormalizeEdges();

                if (_hintTextBlock != null)
                    _hintTextBlock.Text = "Все modifiers рёбер нормализованы к 1.0.";

                ScheduleDraw();
            };
        }

        if (_weakenBridgesButton != null)
        {
            _weakenBridgesButton.Click += (_, _) =>
            {
                WeakenBridges();

                if (_hintTextBlock != null)
                    _hintTextBlock.Text = "Межкластерные bridge-рёбра ослаблены.";

                ScheduleDraw();
            };
        }

        if (_boostLocalEdgesButton != null)
        {
            _boostLocalEdgesButton.Click += (_, _) =>
            {
                BoostLocalEdges();

                if (_hintTextBlock != null)
                    _hintTextBlock.Text = "Локальные внутрикластерные рёбра усилены.";

                ScheduleDraw();
            };
        }

        if (_applyNodeButton != null)
        {
            _applyNodeButton.Click += (_, _) =>
            {
                ApplyNodeChanges();

                if (_hintTextBlock != null && _selectedNodeId != null)
                    _hintTextBlock.Text = "Свойства узла обновлены.";

                ScheduleDraw();
            };
        }

        if (_applyEdgeButton != null)
        {
            _applyEdgeButton.Click += (_, _) =>
            {
                ApplyEdgeChanges();

                if (_hintTextBlock != null && _selectedEdgeId != null)
                    _hintTextBlock.Text = "Свойства ребра обновлены.";

                ScheduleDraw();
            };
        }

        if (_cancelButton != null)
            _cancelButton.Click += (_, _) => Close(false);

        if (_applyButton != null)
        {
            _applyButton.Click += (_, _) =>
            {
                if (!TryValidateBeforeApply(out var validationMessage))
                {
                    if (_hintTextBlock != null)
                        _hintTextBlock.Text = validationMessage;

                    return;
                }

                EditedBlueprint = BuildBlueprint();

                if (_hintTextBlock != null)
                    _hintTextBlock.Text = "Blueprint успешно подготовлен и сохранён.";

                Close(true);
            };
        }
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_graphCanvas == null)
            return;

        var point = e.GetPosition(_graphCanvas);
        var mode = GetMode();
        var currentPoint = e.GetCurrentPoint(_graphCanvas).Properties;

        if (mode != EditorMode.SelectNodes)
            return;

        var gridPoint = TryGetGridCoordinateFromCanvas(point);
        if (gridPoint == null)
            return;

        var existingNode = _nodes.FirstOrDefault(n => n.X == gridPoint.Value.X && n.Y == gridPoint.Value.Y);

        if (currentPoint.IsRightButtonPressed)
        {
            if (existingNode == null)
                return;

            RemoveNode(existingNode.Id);

            if (_hintTextBlock != null)
                _hintTextBlock.Text = $"Узел ({gridPoint.Value.X}, {gridPoint.Value.Y}) удалён вместе со связанными рёбрами.";

            RefreshNodeEditor();
            RefreshEdgeEditor();
            RefreshSummary();
            ScheduleDraw();
            e.Handled = true;
            return;
        }

        if (!currentPoint.IsLeftButtonPressed)
            return;

        if (existingNode != null)
        {
            _selectedNodeId = existingNode.Id;
            _selectedEdgeId = null;
            _pendingEdgeStartNodeId = null;

            if (_hintTextBlock != null)
                _hintTextBlock.Text = $"Выбран узел ({existingNode.X}, {existingNode.Y}). Измените его свойства слева.";

            RefreshNodeEditor();
            RefreshEdgeEditor();
            RefreshSummary();
            ScheduleDraw();
            e.Handled = true;
            return;
        }

        if (TryAddNodeAt(gridPoint.Value.X, gridPoint.Value.Y, selectAfterCreate: true))
        {
            if (_hintTextBlock != null)
                _hintTextBlock.Text = $"Создан новый узел ({gridPoint.Value.X}, {gridPoint.Value.Y}).";

            RefreshNodeEditor();
            RefreshEdgeEditor();
            RefreshSummary();
            ScheduleDraw();
            e.Handled = true;
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

        NormalizeCurrentGraphData();

        if (_candidates.Count == 0)
            RegenerateCandidates(Math.Max(60, _nodes.Count * 2));
    }

    private ClusteredGraphBlueprintDto BuildBlueprint()
    {
        NormalizeCurrentGraphData();

        return new ClusteredGraphBlueprintDto
        {
            CanvasWidth = _canvasWidth,
            CanvasHeight = _canvasHeight,
            Candidates = _candidates
                .Select(CloneCandidate)
                .GroupBy(x => (x.X, x.Y))
                .Select(g => g.First())
                .OrderBy(x => x.X)
                .ThenBy(x => x.Y)
                .ToList(),
            Nodes = _nodes
                .Select(CloneNode)
                .OrderBy(x => x.X)
                .ThenBy(x => x.Y)
                .ToList(),
            Edges = _edges
                .Select(CloneEdge)
                .OrderBy(x => x.FromNodeId)
                .ThenBy(x => x.ToNodeId)
                .ToList()
        };
    }

    private bool TryValidateBeforeApply(out string message)
    {
        NormalizeCurrentGraphData();

        if (_nodes.Count == 0)
        {
            message = "Нельзя сохранить пустой graph blueprint: добавьте хотя бы один узел.";
            return false;
        }

        if (_nodes.Count < 2)
        {
            message = "Для semi-manual graph нужно минимум 2 узла.";
            return false;
        }

        if (_edges.Count == 0)
        {
            message = "Для semi-manual graph нужно хотя бы одно ребро между узлами.";
            return false;
        }

        if (_nodes.Any(n => string.IsNullOrWhiteSpace(n.ClusterId)))
        {
            message = "У некоторых узлов пустой Cluster ID. Заполните cluster для всех узлов.";
            return false;
        }

        bool hasInvalidMoisture = _nodes.Any(n => n.Moisture < 0.02 || n.Moisture > 0.98);
        if (hasInvalidMoisture)
        {
            message = "Влажность узлов должна быть в диапазоне 0.02 .. 0.98.";
            return false;
        }

        var nodeIds = _nodes.Select(n => n.Id).ToHashSet();

        bool hasInvalidEdge = _edges.Any(e =>
            e.FromNodeId == e.ToNodeId ||
            !nodeIds.Contains(e.FromNodeId) ||
            !nodeIds.Contains(e.ToNodeId));

        if (hasInvalidEdge)
        {
            message = "В графе есть некорректные рёбра. Проверьте связи между узлами.";
            return false;
        }

        bool hasInvalidModifier = _edges.Any(e => e.FireSpreadModifier < 0.02 || e.FireSpreadModifier > 1.85);
        if (hasInvalidModifier)
        {
            message = "У одного или нескольких рёбер fireSpreadModifier вне допустимого диапазона 0.02 .. 1.85.";
            return false;
        }

        bool hasInvalidDistance = _edges.Any(e => e.DistanceOverride.HasValue && e.DistanceOverride.Value <= 0.0);
        if (hasInvalidDistance)
        {
            message = "Distance override у рёбер должен быть больше 0.";
            return false;
        }

        bool hasIsolatedNodes = _nodes.Any(node =>
            !_edges.Any(e => e.FromNodeId == node.Id || e.ToNodeId == node.Id));

        if (hasIsolatedNodes)
        {
            message = "Есть изолированные узлы без рёбер. Соедините их или удалите.";
            return false;
        }

        int clusterCount = _nodes
            .Select(n => n.ClusterId?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (clusterCount == 0)
        {
            message = "Не удалось определить ни одного cluster ID.";
            return false;
        }

        message = $"Blueprint валиден: узлов {_nodes.Count}, рёбер {_edges.Count}, clusters {clusterCount}.";
        return true;
    }

    private void NormalizeCurrentGraphData()
    {
        var normalizedNodes = _nodes
            .Where(n => n != null)
            .GroupBy(n => (Math.Clamp(n.X, 0, _canvasWidth - 1), Math.Clamp(n.Y, 0, _canvasHeight - 1)))
            .Select(g =>
            {
                var first = g.First();
                first.X = Math.Clamp(first.X, 0, _canvasWidth - 1);
                first.Y = Math.Clamp(first.Y, 0, _canvasHeight - 1);
                first.ClusterId = string.IsNullOrWhiteSpace(first.ClusterId) ? "patch-manual-1" : first.ClusterId.Trim();
                first.Moisture = Math.Clamp(first.Moisture, 0.02, 0.98);
                return first;
            })
            .ToList();

        _nodes.Clear();
        _nodes.AddRange(normalizedNodes);

        var nodeIds = _nodes.Select(n => n.Id).ToHashSet();
        var uniqueEdges = new Dictionary<string, ClusteredEdgeDraftDto>(StringComparer.Ordinal);

        foreach (var edge in _edges)
        {
            if (edge.FromNodeId == edge.ToNodeId)
                continue;

            if (!nodeIds.Contains(edge.FromNodeId) || !nodeIds.Contains(edge.ToNodeId))
                continue;

            var ordered = GetOrderedEdgeIds(edge.FromNodeId, edge.ToNodeId);
            string key = $"{ordered.A:N}:{ordered.B:N}";

            edge.FromNodeId = ordered.A;
            edge.ToNodeId = ordered.B;
            edge.FireSpreadModifier = Math.Clamp(edge.FireSpreadModifier, 0.02, 1.85);

            if (edge.DistanceOverride.HasValue)
                edge.DistanceOverride = Math.Max(0.1, edge.DistanceOverride.Value);

            if (!uniqueEdges.ContainsKey(key))
                uniqueEdges[key] = edge;
        }

        _edges.Clear();
        _edges.AddRange(uniqueEdges.Values);

        var occupied = _nodes.Select(n => (n.X, n.Y)).ToHashSet();

        foreach (var node in _nodes)
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

        _candidates.RemoveAll(c => c.X < 0 || c.Y < 0 || c.X >= _canvasWidth || c.Y >= _canvasHeight);
        var uniqueCandidates = _candidates
            .GroupBy(c => (c.X, c.Y))
            .Select(g => g.First())
            .ToList();

        _candidates.Clear();
        _candidates.AddRange(uniqueCandidates);

        if (_selectedNodeId.HasValue && !_nodes.Any(n => n.Id == _selectedNodeId.Value))
            _selectedNodeId = null;

        if (_selectedEdgeId.HasValue && !_edges.Any(e => e.Id == _selectedEdgeId.Value))
            _selectedEdgeId = null;

        if (_pendingEdgeStartNodeId.HasValue && !_nodes.Any(n => n.Id == _pendingEdgeStartNodeId.Value))
            _pendingEdgeStartNodeId = null;
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
        DrawGridOverlay();
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

    private void DrawGridOverlay()
    {
        if (_graphCanvas == null)
            return;

        double scale = GetScale();

        for (int x = 0; x < _canvasWidth; x++)
        {
            for (int y = 0; y < _canvasHeight; y++)
            {
                var point = ToCanvasPoint(x, y);

                var dot = new Ellipse
                {
                    Width = 2,
                    Height = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(70, 160, 156, 170)),
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(dot, point.X - 1);
                Canvas.SetTop(dot, point.Y - 1);
                _graphCanvas.Children.Add(dot);
            }
        }

        var info = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 255, 253, 248)),
            BorderBrush = new SolidColorBrush(Color.Parse("#E2DCEB")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 4),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = $"Canvas: {_canvasWidth}×{_canvasHeight} • шаг визуализации {scale:F1}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#5B5568"))
            }
        };

        info.Measure(Size.Infinity);
        Canvas.SetLeft(info, 14);
        Canvas.SetTop(info, 12);
        _graphCanvas.Children.Add(info);
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

            ToolTip.SetTip(
                ellipse,
                $"Кандидат ({candidate.X}, {candidate.Y})\n" +
                $"ЛКМ: добавить/снять узел");

            Canvas.SetLeft(ellipse, point.X - CandidateRadius);
            Canvas.SetTop(ellipse, point.Y - CandidateRadius);

            ellipse.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(ellipse).Properties.IsLeftButtonPressed)
                    return;

                HandleCandidateClick(candidate);
                e.Handled = true;
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

            double radius = isSelected ? NodeRadius + 1.8 : isPendingStart ? NodeRadius + 1.0 : NodeRadius;

            var shape = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = new SolidColorBrush(GetNodeColor(node)),
                Stroke = new SolidColorBrush(
                    isSelected
                        ? Color.Parse("#355CBE")
                        : isPendingStart
                            ? Color.Parse("#D6402B")
                            : Color.Parse("#F8F5EE")),
                StrokeThickness = isSelected ? 2.8 : isPendingStart ? 2.4 : 1.4,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            ToolTip.SetTip(shape, BuildNodeTooltip(node));

            shape.PointerPressed += (_, e) =>
            {
                var properties = e.GetCurrentPoint(shape).Properties;

                if (properties.IsRightButtonPressed)
                {
                    RemoveNode(node.Id);
                    e.Handled = true;
                    return;
                }

                if (!properties.IsLeftButtonPressed)
                    return;

                HandleNodeClick(node);
                e.Handled = true;
            };

            Canvas.SetLeft(shape, point.X - radius);
            Canvas.SetTop(shape, point.Y - radius);
            _graphCanvas.Children.Add(shape);

            DrawNodeLabel(node, point, radius);
        }
    }

    private void DrawNodeLabel(ClusteredNodeDraftDto node, Point point, double radius)
    {
        if (_graphCanvas == null)
            return;

        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(228, 255, 253, 248)),
            BorderBrush = new SolidColorBrush(Color.Parse("#DDD6E9")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(5, 2),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = $"{node.ClusterId}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#564F64"))
            }
        };

        label.Measure(Size.Infinity);
        Canvas.SetLeft(label, point.X - label.DesiredSize.Width / 2.0);
        Canvas.SetTop(label, point.Y + radius + 5.0);

        _graphCanvas.Children.Add(label);
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
                StrokeThickness = isSelected ? 3.7 : isBridge ? 2.3 : modifier >= 1.05 ? 2.5 : 1.8,
                StrokeDashArray = isBridge ? new AvaloniaList<double> { 5, 3 } : null,
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

            DrawEdgeMidLabel(edge, fromNode, toNode, fromPoint, toPoint, isBridge, isSelected);
        }
    }

    private void DrawEdgeMidLabel(
        ClusteredEdgeDraftDto edge,
        ClusteredNodeDraftDto fromNode,
        ClusteredNodeDraftDto toNode,
        Point fromPoint,
        Point toPoint,
        bool isBridge,
        bool isSelected)
    {
        if (_graphCanvas == null)
            return;

        double centerX = (fromPoint.X + toPoint.X) / 2.0;
        double centerY = (fromPoint.Y + toPoint.Y) / 2.0;

        string text = isBridge
            ? $"bridge • {edge.FireSpreadModifier:F2}"
            : $"{edge.FireSpreadModifier:F2}";

        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(235, 255, 253, 248)),
            BorderBrush = new SolidColorBrush(
                isSelected
                    ? Color.Parse("#D6402B")
                    : isBridge
                        ? Color.Parse("#7DA6C7")
                        : Color.Parse("#D8D2E6")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(5, 2),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#5C5569"))
            }
        };

        label.Measure(Size.Infinity);
        Canvas.SetLeft(label, centerX - label.DesiredSize.Width / 2.0);
        Canvas.SetTop(label, centerY - label.DesiredSize.Height / 2.0 - 10.0);
        _graphCanvas.Children.Add(label);
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
            Width = (NodeRadius + 6) * 2,
            Height = (NodeRadius + 6) * 2,
            Fill = new SolidColorBrush(Color.FromArgb(60, 255, 230, 120)),
            Stroke = new SolidColorBrush(Color.Parse("#D6402B")),
            StrokeThickness = 1.8,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(glow, point.X - (NodeRadius + 6));
        Canvas.SetTop(glow, point.Y - (NodeRadius + 6));
        _graphCanvas.Children.Add(glow);
    }

    private void HandleCandidateClick(ClusteredCandidateNodeDto candidate)
    {
        if (GetMode() != EditorMode.SelectNodes)
            return;

        var existingNode = _nodes.FirstOrDefault(n => n.X == candidate.X && n.Y == candidate.Y);

        if (existingNode != null)
        {
            _selectedNodeId = existingNode.Id;
            _selectedEdgeId = null;
            _pendingEdgeStartNodeId = null;

            if (_hintTextBlock != null)
                _hintTextBlock.Text = $"Выбран существующий узел ({existingNode.X}, {existingNode.Y}).";
        }
        else
        {
            var node = CreateDefaultNode(candidate.X, candidate.Y);
            _nodes.Add(node);
            EnsureCandidateExists(node.X, node.Y);

            _selectedNodeId = node.Id;
            _selectedEdgeId = null;
            _pendingEdgeStartNodeId = null;

            if (_hintTextBlock != null)
                _hintTextBlock.Text = $"Создан узел ({node.X}, {node.Y}) с cluster {node.ClusterId}.";
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

                if (_hintTextBlock != null)
                    _hintTextBlock.Text = $"Начало ребра выбрано: ({node.X}, {node.Y}). Теперь выберите второй узел.";
            }
            else if (_pendingEdgeStartNodeId == node.Id)
            {
                _pendingEdgeStartNodeId = null;
                _selectedNodeId = node.Id;
                _selectedEdgeId = null;

                if (_hintTextBlock != null)
                    _hintTextBlock.Text = "Создание ребра отменено: стартовый узел выбран повторно.";
            }
            else
            {
                var fromId = _pendingEdgeStartNodeId.Value;
                var toId = node.Id;

                if (TryAddEdge(fromId, toId))
                {
                    _selectedEdgeId = FindEdgeId(fromId, toId);

                    if (_hintTextBlock != null)
                        _hintTextBlock.Text = $"Новое ребро создано между узлами ({_nodes.First(n => n.Id == fromId).X}, {_nodes.First(n => n.Id == fromId).Y}) и ({node.X}, {node.Y}).";
                }
                else
                {
                    _selectedEdgeId = FindEdgeId(fromId, toId);

                    if (_hintTextBlock != null)
                        _hintTextBlock.Text = "Такое ребро уже существует. Выбрано существующее ребро.";
                }

                _pendingEdgeStartNodeId = null;
                _selectedNodeId = node.Id;
            }
        }
        else if (mode == EditorMode.DeleteEdges)
        {
            _selectedNodeId = node.Id;
            _selectedEdgeId = null;
            _pendingEdgeStartNodeId = null;

            if (_hintTextBlock != null)
                _hintTextBlock.Text = $"Выбран узел ({node.X}, {node.Y}). В режиме удаления связи удаляются кликом по ребру.";
        }
        else
        {
            _selectedNodeId = node.Id;
            _selectedEdgeId = null;
            _pendingEdgeStartNodeId = null;

            if (_hintTextBlock != null)
                _hintTextBlock.Text = $"Выбран узел ({node.X}, {node.Y}). Можно редактировать cluster, vegetation, moisture и elevation.";
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

            _pendingEdgeStartNodeId = null;

            if (_hintTextBlock != null)
                _hintTextBlock.Text = "Ребро удалено.";
        }
        else
        {
            _selectedEdgeId = edge.Id;
            _selectedNodeId = null;

            if (_hintTextBlock != null)
                _hintTextBlock.Text = "Выбрано ребро. Можно изменить distance override и fireSpreadModifier.";
        }

        RefreshNodeEditor();
        RefreshEdgeEditor();
        RefreshSummary();
        ScheduleDraw();
    }

    private void RemoveNode(Guid nodeId)
    {
        var removedNode = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (removedNode == null)
            return;

        int removedEdgesCount = _edges.Count(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId);

        _nodes.RemoveAll(n => n.Id == nodeId);
        _edges.RemoveAll(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId);

        if (_selectedNodeId == nodeId)
            _selectedNodeId = null;

        if (_pendingEdgeStartNodeId == nodeId)
            _pendingEdgeStartNodeId = null;

        if (_selectedEdgeId.HasValue && !_edges.Any(e => e.Id == _selectedEdgeId.Value))
            _selectedEdgeId = null;

        if (_hintTextBlock != null)
            _hintTextBlock.Text = $"Удалён узел ({removedNode.X}, {removedNode.Y}) и связанных рёбер: {removedEdgesCount}.";
    }
    private bool TryAddNodeAt(int x, int y, bool selectAfterCreate)
    {
        x = Math.Clamp(x, 0, _canvasWidth - 1);
        y = Math.Clamp(y, 0, _canvasHeight - 1);

        if (_nodes.Any(n => n.X == x && n.Y == y))
            return false;

        var node = CreateDefaultNode(x, y);
        _nodes.Add(node);
        EnsureCandidateExists(x, y);

        if (selectAfterCreate)
        {
            _selectedNodeId = node.Id;
            _selectedEdgeId = null;
            _pendingEdgeStartNodeId = null;
        }

        return true;
    }

    private ClusteredNodeDraftDto CreateDefaultNode(int x, int y)
    {
        return new ClusteredNodeDraftDto
        {
            Id = Guid.NewGuid(),
            X = x,
            Y = y,
            ClusterId = $"patch-manual-{GetSuggestedClusterIndex(x, y)}",
            Vegetation = VegetationType.Mixed,
            Moisture = 0.45,
            Elevation = 0.0
        };
    }

    private void EnsureCandidateExists(int x, int y)
    {
        if (_candidates.Any(c => c.X == x && c.Y == y))
            return;

        _candidates.Add(new ClusteredCandidateNodeDto
        {
            Id = Guid.NewGuid(),
            X = x,
            Y = y
        });
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
                var ordered = GetOrderedEdgeIds(node.Id, item.Node.Id);
                var key = $"{ordered.A:N}:{ordered.B:N}";

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

        var ordered = GetOrderedEdgeIds(fromId, toId);

        bool exists = _edges.Any(e =>
            e.FromNodeId == ordered.A && e.ToNodeId == ordered.B);

        if (exists)
            return false;

        _edges.Add(new ClusteredEdgeDraftDto
        {
            Id = Guid.NewGuid(),
            FromNodeId = ordered.A,
            ToNodeId = ordered.B,
            DistanceOverride = null,
            FireSpreadModifier = 1.0
        });

        return true;
    }

    private Guid? FindEdgeId(Guid fromId, Guid toId)
    {
        var ordered = GetOrderedEdgeIds(fromId, toId);

        return _edges
            .FirstOrDefault(e => e.FromNodeId == ordered.A && e.ToNodeId == ordered.B)
            ?.Id;
    }

    private (Guid A, Guid B) GetOrderedEdgeIds(Guid first, Guid second)
    {
        return first.CompareTo(second) <= 0 ? (first, second) : (second, first);
    }

    private void ApplyNodeChanges()
    {
        if (_selectedNodeId == null)
            return;

        var node = _nodes.FirstOrDefault(n => n.Id == _selectedNodeId.Value);
        if (node == null)
            return;

        string clusterText = (_nodeClusterIdBox?.Text ?? string.Empty).Trim();
        node.ClusterId = string.IsNullOrWhiteSpace(clusterText) ? "patch-manual-1" : clusterText;
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

        int degree = _edges.Count(e => e.FromNodeId == node.Id || e.ToNodeId == node.Id);

        if (_selectedNodeSummaryTextBlock != null)
        {
            _selectedNodeSummaryTextBlock.Text =
                $"Узел ({node.X}, {node.Y}) • {node.ClusterId}\n" +
                $"Степень: {degree} • {GetVegetationText(node.Vegetation)}";
        }

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

        bool isBridge = fromNode != null &&
                        toNode != null &&
                        !string.Equals(fromNode.ClusterId, toNode.ClusterId, StringComparison.Ordinal);

        if (_selectedEdgeSummaryTextBlock != null)
        {
            _selectedEdgeSummaryTextBlock.Text = fromNode != null && toNode != null
                ? $"Ребро ({fromNode.X}, {fromNode.Y}) ↔ ({toNode.X}, {toNode.Y})\n" +
                  $"Тип: {(isBridge ? "межкластерное" : "локальное")}"
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

        int isolatedCount = _nodes.Count(node =>
            !_edges.Any(e => e.FromNodeId == node.Id || e.ToNodeId == node.Id));

        if (_summaryTextBlock != null)
        {
            _summaryTextBlock.Text =
                $"Узлов: {_nodes.Count} • рёбер: {_edges.Count} • групп: {clusterCount} • мостов: {bridgeCount} • изолированных: {isolatedCount}";
        }

        if (_hintTextBlock != null)
        {
            _hintTextBlock.Text = GetMode() switch
            {
                EditorMode.SelectNodes =>
                    "ЛКМ по canvas — создать узел. ЛКМ по серой точке — добавить/снять узел. ПКМ по узлу — удалить узел вместе с рёбрами.",
                EditorMode.CreateEdges =>
                    "ЛКМ по первому узлу, затем по второму — создать ребро. Повторный клик по тому же узлу снимает выбор старта.",
                EditorMode.DeleteEdges =>
                    "ЛКМ по ребру удаляет его. Используйте этот режим для очистки лишних связей.",
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

    private (int X, int Y)? TryGetGridCoordinateFromCanvas(Point canvasPoint)
    {
        double scale = GetScale();

        double localX = (canvasPoint.X - CanvasPadding) / scale;
        double localY = (canvasPoint.Y - CanvasPadding) / scale;

        if (double.IsNaN(localX) || double.IsNaN(localY))
            return null;

        int x = (int)Math.Round(localX);
        int y = (int)Math.Round(localY);

        if (x < 0 || y < 0 || x >= _canvasWidth || y >= _canvasHeight)
            return null;

        if (Math.Abs(localX - x) > ManualPlacementSnapDistance ||
            Math.Abs(localY - y) > ManualPlacementSnapDistance)
        {
            return null;
        }

        return (x, y);
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
        return GetSuggestedClusterIndex(candidate.X, candidate.Y);
    }

    private int GetSuggestedClusterIndex(int x, int y)
    {
        int horizontalBand = Math.Max(0, x / Math.Max(1, _canvasWidth / 4));
        int verticalBand = Math.Max(0, y / Math.Max(1, _canvasHeight / 3));
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
        int degree = _edges.Count(e => e.FromNodeId == node.Id || e.ToNodeId == node.Id);

        return
            $"Узел: {node.Id}\n" +
            $"Координаты: ({node.X}, {node.Y})\n" +
            $"Cluster: {node.ClusterId}\n" +
            $"Растительность: {GetVegetationText(node.Vegetation)}\n" +
            $"Влажность: {node.Moisture:F2}\n" +
            $"Высота: {node.Elevation:F2}\n" +
            $"Степень: {degree}\n" +
            $"ЛКМ: выбрать\n" +
            $"ПКМ: удалить";
    }

    private string BuildEdgeTooltip(
        ClusteredEdgeDraftDto edge,
        ClusteredNodeDraftDto fromNode,
        ClusteredNodeDraftDto toNode,
        bool isBridge)
    {
        return
            $"Ребро: {edge.Id}\n" +
            $"From: ({fromNode.X}, {fromNode.Y}) • {fromNode.ClusterId}\n" +
            $"To: ({toNode.X}, {toNode.Y}) • {toNode.ClusterId}\n" +
            $"Тип: {(isBridge ? "межкластерное" : "локальное")}\n" +
            $"Modifier: {edge.FireSpreadModifier:F2}\n" +
            $"Distance override: {(edge.DistanceOverride.HasValue ? edge.DistanceOverride.Value.ToString("F2", CultureInfo.InvariantCulture) : "auto")}";
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