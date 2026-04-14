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
    private INotifyCollectionChanged? _currentCollection;
    private bool _drawScheduled;

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

    public event EventHandler<GraphCellDto>? CellClicked;

    public GraphVisualization()
    {
        InitializeComponent();

        _graphCanvas = this.FindControl<Canvas>("GraphCanvas");
        _scrollHost = this.FindControl<ScrollViewer>("ScrollHost");

        this.PropertyChanged += OnGraphPropertyChanged;
        this.AttachedToVisualTree += (_, _) => ScheduleDraw();

        if (_scrollHost != null)
            _scrollHost.SizeChanged += (_, _) => ScheduleDraw();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnGraphPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CellsProperty)
        {
            RebindCollectionSubscription();
            ScheduleDraw();
            return;
        }

        if (e.Property == GridWidthProperty ||
            e.Property == GridHeightProperty ||
            e.Property == IsIgnitionSelectionEnabledProperty)
        {
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

        var safeGridWidth = Math.Max(1, GridWidth);
        var safeGridHeight = Math.Max(1, GridHeight);
        var cellSize = GetCellSize(safeGridWidth, safeGridHeight);

        var contentWidth = safeGridWidth * cellSize;
        var contentHeight = safeGridHeight * cellSize;

        var viewportWidth = _scrollHost?.Bounds.Width ?? 0;
        var viewportHeight = _scrollHost?.Bounds.Height ?? 0;

        var canvasWidth = Math.Max(contentWidth + PaddingSize * 2, viewportWidth > 0 ? viewportWidth : contentWidth + PaddingSize * 2);
        var canvasHeight = Math.Max(contentHeight + PaddingSize * 2, viewportHeight > 0 ? viewportHeight : contentHeight + PaddingSize * 2);

        _graphCanvas.Width = canvasWidth;
        _graphCanvas.Height = canvasHeight;

        var originX = Math.Max(PaddingSize, (canvasWidth - contentWidth) / 2.0);
        var originY = Math.Max(PaddingSize, (canvasHeight - contentHeight) / 2.0);

        DrawGridBackground(originX, originY, contentWidth, contentHeight);

        if (cells.Count == 0)
        {
            DrawEmptyState(canvasWidth, canvasHeight);
            return;
        }

        foreach (var cell in cells.OrderBy(c => c.Y).ThenBy(c => c.X))
            DrawCell(cell, originX, originY, cellSize);
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

    private void DrawCell(GraphCellDto cell, double originX, double originY, int cellSize)
    {
        if (_graphCanvas == null)
            return;

        var x = originX + cell.X * cellSize;
        var y = originY + cell.Y * cellSize;

        var fillColor = GetCellColor(cell);
        var strokeColor = GetStrokeColor(cell);

        var strokeThickness = cell.IsSelectedIgnition
            ? 3
            : cell.IsBurning
                ? 2
                : 1;

        var rect = new Rectangle
        {
            Width = cellSize - 2,
            Height = cellSize - 2,
            Fill = new SolidColorBrush(fillColor),
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = strokeThickness,
            RadiusX = 3,
            RadiusY = 3,
            Cursor = IsIgnitionSelectionEnabled && cell.IsIgnitable
                ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                : new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow)
        };

        ToolTip.SetTip(rect, GetCellTooltip(cell));

        if (IsIgnitionSelectionEnabled && cell.IsIgnitable)
        {
            rect.PointerPressed += (_, _) =>
            {
                CellClicked?.Invoke(this, cell);
            };
        }

        Canvas.SetLeft(rect, x + 1);
        Canvas.SetTop(rect, y + 1);
        _graphCanvas.Children.Add(rect);

        if (cell.IsSelectedIgnition)
        {
            DrawIgnitionSelectionGlow(x, y, cellSize);
            DrawIgnitionMarker(x, y, cellSize);
        }
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

    private Color GetCellColor(GraphCellDto cell)
    {
        if (cell.IsSelectedIgnition)
            return Color.Parse("#9EC5FE");

        if (cell.IsBurned)
            return Color.Parse("#777777");

        if (cell.IsBurning)
            return Color.Parse("#FF7A59");

        return cell.Vegetation?.ToLower() switch
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