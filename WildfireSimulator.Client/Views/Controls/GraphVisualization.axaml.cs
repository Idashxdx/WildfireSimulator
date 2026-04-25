using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using WildfireSimulator.Client.Models;

namespace WildfireSimulator.Client.Views.Controls;

public partial class GraphVisualization : UserControl
{
    private Canvas? _graphCanvas;
    private ScrollViewer? _scrollHost;
    private ToggleSwitch? _elevationToggle;
    private INotifyCollectionChanged? _currentCollection;
    private bool _drawScheduled;
    private Dictionary<(int X, int Y), GraphCellDto> _cellLookup = new();
    private Button? _zoomInButton;
    private Button? _zoomOutButton;
    private Button? _resetZoomButton;

    private bool _hasManualZoom;
    private double _zoom = 1.0;
    private bool _needCenterAfterDraw;
    public static readonly StyledProperty<GraphCellDto?> SelectedCellProperty =
        AvaloniaProperty.Register<GraphVisualization, GraphCellDto?>(nameof(SelectedCell));

    public GraphCellDto? SelectedCell
    {
        get => GetValue(SelectedCellProperty);
        set => SetValue(SelectedCellProperty, value);
    }

    public event EventHandler? BackgroundClicked;
    private const double MinZoom = 0.35;
    private const double MaxZoom = 3.0;

    private const int PaddingSize = 24;
    private const int MinCellSize = 8;

    public static readonly StyledProperty<IEnumerable<GraphCellDto>?> CellsProperty =
        AvaloniaProperty.Register<GraphVisualization, IEnumerable<GraphCellDto>?>(nameof(Cells));

    public static readonly StyledProperty<int> GridWidthProperty =
        AvaloniaProperty.Register<GraphVisualization, int>(nameof(GridWidth), 20);

    public static readonly StyledProperty<int> GridHeightProperty =
        AvaloniaProperty.Register<GraphVisualization, int>(nameof(GridHeight), 20);

    public static readonly StyledProperty<bool> IsIgnitionSelectionEnabledProperty =
        AvaloniaProperty.Register<GraphVisualization, bool>(nameof(IsIgnitionSelectionEnabled), false);

    public static readonly StyledProperty<bool> ShowElevationLabelsProperty =
        AvaloniaProperty.Register<GraphVisualization, bool>(nameof(ShowElevationLabels), false);

    public IEnumerable<GraphCellDto>? Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    public int GridWidth
    {
        get => GetValue(GridWidthProperty);
        set => SetValue(GridWidthProperty, value);
    }

    public int GridHeight
    {
        get => GetValue(GridHeightProperty);
        set => SetValue(GridHeightProperty, value);
    }

    public bool IsIgnitionSelectionEnabled
    {
        get => GetValue(IsIgnitionSelectionEnabledProperty);
        set => SetValue(IsIgnitionSelectionEnabledProperty, value);
    }

    public bool ShowElevationLabels
    {
        get => GetValue(ShowElevationLabelsProperty);
        set => SetValue(ShowElevationLabelsProperty, value);
    }

    public event EventHandler<GraphCellDto>? CellClicked;

    public GraphVisualization()
    {
        InitializeComponent();

        _graphCanvas = this.FindControl<Canvas>("GraphCanvas");
        _scrollHost = this.FindControl<ScrollViewer>("ScrollHost");
        _elevationToggle = this.FindControl<ToggleSwitch>("ElevationToggle");

        _zoomInButton = this.FindControl<Button>("ZoomInButton");
        _zoomOutButton = this.FindControl<Button>("ZoomOutButton");
        _resetZoomButton = this.FindControl<Button>("ResetZoomButton");

        if (_graphCanvas != null)
            _graphCanvas.PointerPressed += OnCanvasPointerPressed;

        if (_zoomInButton != null)
            _zoomInButton.Click += (_, _) => ChangeZoom(true);

        if (_zoomOutButton != null)
            _zoomOutButton.Click += (_, _) => ChangeZoom(false);

        if (_resetZoomButton != null)
            _resetZoomButton.Click += (_, _) => ResetZoom();

        this.PropertyChanged += OnGraphPropertyChanged;
        this.AttachedToVisualTree += (_, _) => ScheduleDraw();

        if (_scrollHost != null)
        {
            _scrollHost.SizeChanged += (_, _) =>
            {
                if (!_hasManualZoom)
                    ScheduleDraw();
            };
        }

        if (_elevationToggle != null)
        {
            _elevationToggle.IsChecked = ShowElevationLabels;

            _elevationToggle.Checked += (_, _) =>
            {
                ShowElevationLabels = true;
                ScheduleDraw();
            };

            _elevationToggle.Unchecked += (_, _) =>
            {
                ShowElevationLabels = false;
                ScheduleDraw();
            };
        }
    }
    private void ChangeZoom(bool zoomIn)
    {
        double factor = zoomIn ? 1.18 : 1.0 / 1.18;

        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        _hasManualZoom = true;
        _needCenterAfterDraw = true;

        ScheduleDraw();
    }

    private void ResetZoom()
    {
        _zoom = 1.0;
        _hasManualZoom = false;
        _needCenterAfterDraw = true;

        ScheduleDraw();
    }

    private void OnCanvasPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
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

    private void OnGraphPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CellsProperty)
        {
            _zoom = 1.0;
            _hasManualZoom = false;
            _needCenterAfterDraw = true;

            RebindCollectionSubscription();
            ScheduleDraw();
            return;
        }

        if (e.Property == GridWidthProperty ||
            e.Property == GridHeightProperty)
        {
            _zoom = 1.0;
            _hasManualZoom = false;
            _needCenterAfterDraw = true;

            ScheduleDraw();
            return;
        }

        if (e.Property == IsIgnitionSelectionEnabledProperty ||
            e.Property == SelectedCellProperty ||
            e.Property == ShowElevationLabelsProperty)
        {
            if (e.Property == ShowElevationLabelsProperty && _elevationToggle != null)
            {
                bool newValue = e.NewValue is bool b && b;
                if (_elevationToggle.IsChecked != newValue)
                    _elevationToggle.IsChecked = newValue;
            }

            ScheduleDraw();
        }
    }
    private void RebindCollectionSubscription()
    {
        if (_currentCollection != null)
        {
            _currentCollection.CollectionChanged -= OnCellsCollectionChanged;
            _currentCollection = null;
        }

        if (Cells is INotifyCollectionChanged notifyCollection)
        {
            _currentCollection = notifyCollection;
            _currentCollection.CollectionChanged += OnCellsCollectionChanged;
        }
    }

    private void OnCellsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _zoom = 1.0;
        _hasManualZoom = false;
        _needCenterAfterDraw = true;

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

        var cells = Cells?.ToList() ?? new List<GraphCellDto>();
        _cellLookup = cells
            .GroupBy(c => (c.X, c.Y))
            .ToDictionary(g => g.Key, g => g.Last());

        var safeGridWidth = Math.Max(1, GridWidth);
        var safeGridHeight = Math.Max(1, GridHeight);

        int normalCellSize = GetNormalCellSize(safeGridWidth, safeGridHeight);
        int cellSize = Math.Max(MinCellSize, (int)Math.Round(normalCellSize * _zoom));

        double normalContentWidth = safeGridWidth * normalCellSize;
        double normalContentHeight = safeGridHeight * normalCellSize;

        double contentWidth = safeGridWidth * cellSize;
        double contentHeight = safeGridHeight * cellSize;

        double normalViewportHeight = normalContentHeight + PaddingSize * 2;
        double normalViewportWidth = normalContentWidth + PaddingSize * 2;

        if (_scrollHost != null)
        {
            _scrollHost.Height = normalViewportHeight;
            _scrollHost.MinHeight = normalViewportHeight;
            _scrollHost.MaxHeight = normalViewportHeight;
        }

        double viewportWidth = _scrollHost?.Bounds.Width ?? 0;
        double viewportHeight = normalViewportHeight;

        double canvasWidth = Math.Max(
            contentWidth + PaddingSize * 2,
            Math.Max(normalViewportWidth, viewportWidth));

        double canvasHeight = Math.Max(
            contentHeight + PaddingSize * 2,
            viewportHeight);

        _graphCanvas.Width = canvasWidth;
        _graphCanvas.Height = canvasHeight;

        var originX = Math.Max(PaddingSize, (canvasWidth - contentWidth) / 2.0);
        var originY = Math.Max(PaddingSize, (canvasHeight - contentHeight) / 2.0);

       DrawGridBackground(originX, originY, (int)Math.Round(contentWidth), (int)Math.Round(contentHeight));
        if (cells.Count == 0)
        {
            DrawEmptyState(canvasWidth, canvasHeight);
            return;
        }

        foreach (var cell in cells.OrderBy(c => c.Y).ThenBy(c => c.X))
            DrawCell(cell, originX, originY, cellSize);

        if (ShowElevationLabels)
            DrawElevationLabelsForWholeMap(originX, originY, cellSize);

        if (_needCenterAfterDraw)
            CenterScrollViewerAfterDraw();
    }
    private int GetNormalCellSize(int gridWidth, int gridHeight)
    {
        int maxDimension = Math.Max(gridWidth, gridHeight);

        if (maxDimension <= 20)
            return 28;

        if (maxDimension <= 30)
            return 24;

        if (maxDimension <= 40)
            return 20;

        if (maxDimension <= 60)
            return 16;

        if (maxDimension <= 90)
            return 12;

        return MinCellSize;
    }
    private void CenterScrollViewerAfterDraw()
    {
        if (_scrollHost == null || _graphCanvas == null)
            return;

        _needCenterAfterDraw = false;

        Dispatcher.UIThread.Post(() =>
        {
            if (_scrollHost == null || _graphCanvas == null)
                return;

            double viewportWidth = _scrollHost.Viewport.Width;
            double viewportHeight = _scrollHost.Viewport.Height;

            double extentWidth = _scrollHost.Extent.Width;
            double extentHeight = _scrollHost.Extent.Height;

            if (viewportWidth <= 0 || viewportHeight <= 0)
                return;

            double offsetX = Math.Max(0, (extentWidth - viewportWidth) / 2.0);
            double offsetY = Math.Max(0, (extentHeight - viewportHeight) / 2.0);

            _scrollHost.Offset = new Vector(offsetX, offsetY);
        }, DispatcherPriority.Background);
    }
    private void DrawCell(GraphCellDto cell, double originX, double originY, int cellSize)
    {
        if (_graphCanvas == null)
            return;

        var x = originX + cell.X * cellSize;
        var y = originY + cell.Y * cellSize;

        var fillColor = GetCellColor(cell);
        var strokeColor = GetStrokeColor(cell);

        bool isSelectedCell = SelectedCell?.Id == cell.Id;

        var strokeThickness = isSelectedCell
            ? 3
            : cell.IsSelectedIgnition
                ? 3
                : cell.IsBurning
                    ? 2
                    : 1;

        if (isSelectedCell)
            strokeColor = Color.Parse("#5B3CC4");

        var rect = new Rectangle
        {
            Width = Math.Max(2, cellSize - 2),
            Height = Math.Max(2, cellSize - 2),
            Fill = new SolidColorBrush(fillColor),
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = strokeThickness,
            RadiusX = 3,
            RadiusY = 3,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        ToolTip.SetTip(rect, GetCellTooltip(cell));

        rect.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(rect).Properties.IsLeftButtonPressed)
                return;

            CellClicked?.Invoke(this, cell);
            e.Handled = true;
        };

        Canvas.SetLeft(rect, x + 1);
        Canvas.SetTop(rect, y + 1);
        _graphCanvas.Children.Add(rect);

        if (isSelectedCell)
            DrawSelectedCellFrame(x, y, cellSize);

        if (cell.IsSelectedIgnition)
        {
            DrawIgnitionSelectionGlow(x, y, cellSize);
            DrawIgnitionMarker(x, y, cellSize);
        }
    }
    private void DrawSelectedCellFrame(double x, double y, int cellSize)
    {
        if (_graphCanvas == null)
            return;

        var frame = new Rectangle
        {
            Width = Math.Max(2, cellSize - 2),
            Height = Math.Max(2, cellSize - 2),
            Fill = Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.Parse("#5B3CC4")),
            StrokeThickness = 3,
            RadiusX = 4,
            RadiusY = 4,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(frame, x + 1);
        Canvas.SetTop(frame, y + 1);
        _graphCanvas.Children.Add(frame);
    }

    private void DrawElevationLabelsForWholeMap(double originX, double originY, int cellSize)
    {
        if (_graphCanvas == null || _cellLookup.Count == 0)
            return;

        if (!TryGetElevationRange(out var minElevation, out var maxElevation))
            return;

        double span = maxElevation - minElevation;
        if (span < 1.0)
            return;

        bool compactMode = cellSize <= 14;
        bool ultraCompactMode = cellSize <= 11;

        foreach (var cell in _cellLookup.Values.OrderBy(c => c.Y).ThenBy(c => c.X))
        {
            if (cell.IsBurning || cell.IsBurned)
                continue;

            DrawElevationLabel(
                cell,
                originX,
                originY,
                cellSize,
                minElevation,
                maxElevation,
                compactMode,
                ultraCompactMode);
        }
    }

    private void DrawElevationLabel(
        GraphCellDto cell,
        double originX,
        double originY,
        int cellSize,
        double minElevation,
        double maxElevation,
        bool compactMode,
        bool ultraCompactMode)
    {
        if (_graphCanvas == null)
            return;

        double normalized = NormalizeElevation(cell.Elevation, minElevation, maxElevation);

        string textValue = ultraCompactMode
            ? Math.Round(cell.Elevation).ToString("0")
            : Math.Round(cell.Elevation).ToString("0");

        var textBlock = new TextBlock
        {
            Text = textValue,
            FontSize = ultraCompactMode ? 7 : compactMode ? 8 : 9,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(GetElevationTextColor(normalized)),
            IsHitTestVisible = false
        };

        textBlock.Measure(Size.Infinity);

        double x = originX + cell.X * cellSize;
        double y = originY + cell.Y * cellSize;

        double boxWidth = Math.Max(textBlock.DesiredSize.Width + 4, ultraCompactMode ? 12 : 16);
        double boxHeight = Math.Max(textBlock.DesiredSize.Height + 2, ultraCompactMode ? 9 : 11);

        var background = new Border
        {
            Background = new SolidColorBrush(GetElevationBoxBackground(normalized)),
            BorderBrush = new SolidColorBrush(GetElevationBoxBorder(normalized)),
            BorderThickness = new Thickness(0.7),
            CornerRadius = new CornerRadius(2),
            Width = boxWidth,
            Height = boxHeight,
            IsHitTestVisible = false,
            Child = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = textValue,
                        FontSize = ultraCompactMode ? 7 : compactMode ? 8 : 9,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(GetElevationTextColor(normalized)),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        IsHitTestVisible = false
                    }
                }
            }
        };

        double left = x + (cellSize - boxWidth) / 2.0;
        double top = y + (cellSize - boxHeight) / 2.0;

        Canvas.SetLeft(background, left);
        Canvas.SetTop(background, top);
        _graphCanvas.Children.Add(background);
    }

    private double NormalizeElevation(double elevation, double minElevation, double maxElevation)
    {
        double span = Math.Max(1.0, maxElevation - minElevation);
        return Math.Clamp((elevation - minElevation) / span, 0.0, 1.0);
    }

    private Color GetElevationBoxBackground(double normalizedElevation)
    {
        if (normalizedElevation >= 0.80)
            return Color.FromArgb(210, 255, 244, 225);

        if (normalizedElevation >= 0.60)
            return Color.FromArgb(210, 250, 240, 220);

        if (normalizedElevation >= 0.40)
            return Color.FromArgb(210, 243, 245, 239);

        if (normalizedElevation >= 0.20)
            return Color.FromArgb(210, 231, 240, 246);

        return Color.FromArgb(210, 224, 236, 247);
    }

    private Color GetElevationBoxBorder(double normalizedElevation)
    {
        if (normalizedElevation >= 0.80)
            return Color.Parse("#C9A36F");

        if (normalizedElevation >= 0.60)
            return Color.Parse("#BCAA76");

        if (normalizedElevation >= 0.40)
            return Color.Parse("#B2B0A8");

        if (normalizedElevation >= 0.20)
            return Color.Parse("#95AABD");

        return Color.Parse("#7F98B1");
    }

    private Color GetElevationTextColor(double normalizedElevation)
    {
        if (normalizedElevation >= 0.75)
            return Color.Parse("#6C4922");

        if (normalizedElevation <= 0.25)
            return Color.Parse("#3F5C79");

        return Color.Parse("#4E4A45");
    }

    private Color GetCellColor(GraphCellDto cell)
    {
        if (cell.IsSelectedIgnition)
            return Color.Parse("#9EC5FE");

        if (cell.IsBurned)
            return Color.Parse("#777777");

        if (cell.IsBurning)
            return Color.Parse("#FF7A59");

        return (cell.Vegetation ?? "").Trim() switch
        {
            "Coniferous" => Color.Parse("#5E9B5E"),
            "Deciduous" => Color.Parse("#8ACB88"),
            "Mixed" => Color.Parse("#A8C97F"),
            "Grass" => Color.Parse("#E7D36F"),
            "Shrub" => Color.Parse("#CFA46A"),
            "Water" => Color.Parse("#7CC6F2"),
            "Bare" => Color.Parse("#C9B7A7"),

            "Хвойный лес" => Color.Parse("#5E9B5E"),
            "Лиственный лес" => Color.Parse("#8ACB88"),
            "Смешанный лес" => Color.Parse("#A8C97F"),
            "Трава" => Color.Parse("#E7D36F"),
            "Кустарник" => Color.Parse("#CFA46A"),
            "Вода" => Color.Parse("#7CC6F2"),
            "Пустая поверхность" => Color.Parse("#C9B7A7"),

            _ => Color.Parse("#A8C97F")
        };
    }

    private Color GetStrokeColor(GraphCellDto cell)
    {
        if (cell.IsSelectedIgnition)
            return Color.Parse("#D6402B");

        if (cell.IsBurned)
            return Color.Parse("#5F5F5F");

        if (cell.IsBurning)
            return Color.Parse("#B5473E");

        if (IsIgnitionSelectionEnabled && !cell.IsIgnitable)
            return Color.Parse("#8E8A94");

        if (IsIgnitionSelectionEnabled && cell.IsIgnitable)
            return Color.Parse("#F4EFE6");

        return Color.Parse("#FFFFFF");
    }

    private int GetCellSize(int gridWidth, int gridHeight)
    {
        var maxDimension = Math.Max(gridWidth, gridHeight);

        if (maxDimension <= 20) return 26;
        if (maxDimension <= 30) return 22;
        if (maxDimension <= 40) return 18;
        if (maxDimension <= 60) return 14;
        if (maxDimension <= 80) return 11;

        return MinCellSize;
    }

    private void DrawEmptyState(double canvasWidth, double canvasHeight)
    {
        if (_graphCanvas == null)
            return;

        var text = new TextBlock
        {
            Text = "Выберите симуляцию или создайте новую",
            Foreground = new SolidColorBrush(Color.Parse("#6E6A78")),
            FontSize = 18,
            FontWeight = FontWeight.SemiBold
        };

        text.Measure(Size.Infinity);

        Canvas.SetLeft(text, Math.Max(20, (canvasWidth - text.DesiredSize.Width) / 2));
        Canvas.SetTop(text, Math.Max(20, (canvasHeight - text.DesiredSize.Height) / 2));

        _graphCanvas.Children.Add(text);
    }

    private void DrawGridBackground(double originX, double originY, int contentWidth, int contentHeight)
    {
        if (_graphCanvas == null)
            return;

        var background = new Rectangle
        {
            Width = contentWidth,
            Height = contentHeight,
            Fill = new SolidColorBrush(Color.Parse("#F5F2EA")),
            RadiusX = 10,
            RadiusY = 10,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(background, originX);
        Canvas.SetTop(background, originY);
        _graphCanvas.Children.Add(background);
    }

    private bool TryGetElevationRange(out double minElevation, out double maxElevation)
    {
        minElevation = 0.0;
        maxElevation = 0.0;

        if (_cellLookup.Count == 0)
            return false;

        var values = _cellLookup.Values.Select(c => c.Elevation).ToList();
        if (values.Count == 0)
            return false;

        minElevation = values.Min();
        maxElevation = values.Max();
        return true;
    }

    private bool TryGetCell(int x, int y, out GraphCellDto cell)
    {
        return _cellLookup.TryGetValue((x, y), out cell!);
    }

    private void DrawIgnitionMarker(double x, double y, int cellSize)
    {
        if (_graphCanvas == null)
            return;

        var centerX = x + cellSize / 2.0;
        var centerY = y + cellSize / 2.0;

        var outerSize = Math.Max(8, cellSize * 0.52);
        var innerSize = Math.Max(4, cellSize * 0.22);

        var outer = new Ellipse
        {
            Width = outerSize,
            Height = outerSize,
            Fill = new SolidColorBrush(Color.Parse("#FFF4A3")),
            Stroke = new SolidColorBrush(Color.Parse("#C94F3D")),
            StrokeThickness = 2,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(outer, centerX - outerSize / 2.0);
        Canvas.SetTop(outer, centerY - outerSize / 2.0);
        _graphCanvas.Children.Add(outer);

        var inner = new Ellipse
        {
            Width = innerSize,
            Height = innerSize,
            Fill = new SolidColorBrush(Color.Parse("#D6402B")),
            IsHitTestVisible = false
        };

        Canvas.SetLeft(inner, centerX - innerSize / 2.0);
        Canvas.SetTop(inner, centerY - innerSize / 2.0);
        _graphCanvas.Children.Add(inner);
    }

    private void DrawIgnitionSelectionGlow(double x, double y, int cellSize)
    {
        if (_graphCanvas == null)
            return;

        var glow = new Rectangle
        {
            Width = cellSize - 2,
            Height = cellSize - 2,
            Fill = new SolidColorBrush(Color.FromArgb(90, 255, 235, 120)),
            Stroke = new SolidColorBrush(Color.Parse("#D6402B")),
            StrokeThickness = 2.5,
            RadiusX = 4,
            RadiusY = 4,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(glow, x + 1);
        Canvas.SetTop(glow, y + 1);
        _graphCanvas.Children.Add(glow);
    }

    private string GetCellTooltip(GraphCellDto cell)
    {
        var ignitableText = cell.IsIgnitable ? "Да" : "Нет";
        var selectedText = cell.IsSelectedIgnition ? "\nВыбран как стартовый очаг: Да" : string.Empty;

        var vegetationText = cell.Vegetation?.Trim() switch
        {
            "Coniferous" => "Хвойный лес",
            "Deciduous" => "Лиственный лес",
            "Mixed" => "Смешанный лес",
            "Grass" => "Трава",
            "Shrub" => "Кустарник",
            "Water" => "Вода",
            "Bare" => "Пустая поверхность",
            _ => cell.Vegetation
        };

        var stateText = cell.State?.Trim() switch
        {
            "Burning" => "Горит",
            "Burned" => "Сгорела",
            "Normal" => "Нормальная",
            _ => cell.State
        };

        return $"Координаты: ({cell.X}, {cell.Y})\n" +
               $"Тип поверхности: {vegetationText}\n" +
               $"Состояние: {stateText}\n" +
               $"Можно выбрать как очаг: {ignitableText}" +
               selectedText + "\n" +
               $"Влажность: {cell.Moisture:F2}\n" +
               $"Высота: {cell.Elevation:F0} м\n" +
               $"Вероятность возгорания: {cell.BurnProbability:F3}";
    }
}