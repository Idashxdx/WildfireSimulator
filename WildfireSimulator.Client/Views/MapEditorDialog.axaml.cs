using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using WildfireSimulator.Client.Models;

namespace WildfireSimulator.Client.Views;

public partial class MapEditorDialog : Window
{
    private const int CanvasPadding = 24;
    private const int MinCellSize = 14;

    private Canvas? _mapCanvas;
    private ComboBox? _vegetationBox;
    private TextBox? _moistureBox;
    private TextBox? _elevationBox;
    private ComboBox? _brushRadiusBox;
    private TextBlock? _selectionInfoTextBlock;
    private TextBlock? _mapSummaryTextBlock;

    private Button? _applyToSelectedButton;
    private Button? _applyToBrushButton;
    private Button? _resetButton;
    private Button? _cancelButton;
    private Button? _saveButton;

    private readonly Dictionary<(int X, int Y), PreparedGridCellDto> _cellMap = new();
    private readonly List<PreparedGridCellDto> _originalCells = new();

    private int? _selectedX;
    private int? _selectedY;

    public int GridWidth { get; }
    public int GridHeight { get; }

    public MapEditorDialog(int gridWidth, int gridHeight, IEnumerable<PreparedGridCellDto>? cells = null)
    {
        GridWidth = Math.Max(5, gridWidth);
        GridHeight = Math.Max(5, gridHeight);

        InitializeCells(cells);

        InitializeComponent();
        FindControls();
        AttachEvents();
        UpdateSelectionText();
        UpdateMapSummary();
        DrawMap();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeCells(IEnumerable<PreparedGridCellDto>? cells)
    {
        _cellMap.Clear();
        _originalCells.Clear();

        if (cells != null)
        {
            foreach (var cell in cells
                         .GroupBy(c => (c.X, c.Y))
                         .Select(g => g.Last())
                         .Where(c => c.X >= 0 && c.Y >= 0 && c.X < GridWidth && c.Y < GridHeight))
            {
                var copy = CloneCell(cell);
                _cellMap[(copy.X, copy.Y)] = copy;
                _originalCells.Add(CloneCell(copy));
            }
        }

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                if (_cellMap.ContainsKey((x, y)))
                    continue;

                var fallback = new PreparedGridCellDto
                {
                    X = x,
                    Y = y,
                    Vegetation = "Mixed",
                    Moisture = 0.5,
                    Elevation = 0.0
                };

                _cellMap[(x, y)] = fallback;
                _originalCells.Add(CloneCell(fallback));
            }
        }
    }

    private static PreparedGridCellDto CloneCell(PreparedGridCellDto cell)
    {
        return new PreparedGridCellDto
        {
            X = cell.X,
            Y = cell.Y,
            Vegetation = cell.Vegetation,
            Moisture = cell.Moisture,
            Elevation = cell.Elevation
        };
    }

    private void FindControls()
    {
        _mapCanvas = this.FindControl<Canvas>("MapCanvas");
        _vegetationBox = this.FindControl<ComboBox>("VegetationBox");
        _moistureBox = this.FindControl<TextBox>("MoistureBox");
        _elevationBox = this.FindControl<TextBox>("ElevationBox");
        _brushRadiusBox = this.FindControl<ComboBox>("BrushRadiusBox");
        _selectionInfoTextBlock = this.FindControl<TextBlock>("SelectionInfoTextBlock");
        _mapSummaryTextBlock = this.FindControl<TextBlock>("MapSummaryTextBlock");

        _applyToSelectedButton = this.FindControl<Button>("ApplyToSelectedButton");
        _applyToBrushButton = this.FindControl<Button>("ApplyToBrushButton");
        _resetButton = this.FindControl<Button>("ResetButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
        _saveButton = this.FindControl<Button>("SaveButton");
    }

    private void AttachEvents()
    {
        if (_mapCanvas != null)
            _mapCanvas.PointerPressed += OnCanvasPointerPressed;

        if (_applyToSelectedButton != null)
            _applyToSelectedButton.Click += (_, _) => ApplyToSelectedCell();

        if (_applyToBrushButton != null)
            _applyToBrushButton.Click += (_, _) => ApplyBrush();

        if (_resetButton != null)
            _resetButton.Click += (_, _) => ResetMap();

        if (_cancelButton != null)
            _cancelButton.Click += (_, _) => Close(false);

        if (_saveButton != null)
            _saveButton.Click += (_, _) => Close(true);
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_mapCanvas == null)
            return;

        var point = e.GetPosition(_mapCanvas);
        if (!TryGetGridCell(point, out var cellX, out var cellY))
            return;

        _selectedX = cellX;
        _selectedY = cellY;

        if (_cellMap.TryGetValue((cellX, cellY), out var cell))
        {
            SetEditorFromCell(cell);
        }

        UpdateSelectionText();
        DrawMap();
    }

    private void SetEditorFromCell(PreparedGridCellDto cell)
    {
        if (_moistureBox != null)
            _moistureBox.Text = cell.Moisture.ToString("0.00", CultureInfo.InvariantCulture);

        if (_elevationBox != null)
            _elevationBox.Text = cell.Elevation.ToString("0.##", CultureInfo.InvariantCulture);

        if (_vegetationBox != null)
        {
            for (int i = 0; i < _vegetationBox.ItemCount; i++)
            {
                if (_vegetationBox.ContainerFromIndex(i) is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), cell.Vegetation, StringComparison.OrdinalIgnoreCase))
                {
                    _vegetationBox.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    private void ApplyToSelectedCell()
    {
        if (_selectedX == null || _selectedY == null)
            return;

        if (!_cellMap.TryGetValue((_selectedX.Value, _selectedY.Value), out var cell))
            return;

        ApplyEditorValuesToCell(cell);
        UpdateSelectionText();
        UpdateMapSummary();
        DrawMap();
    }

    private void ApplyBrush()
    {
        if (_selectedX == null || _selectedY == null)
            return;

        int radius = GetBrushRadius();

        for (int x = _selectedX.Value - radius; x <= _selectedX.Value + radius; x++)
        {
            for (int y = _selectedY.Value - radius; y <= _selectedY.Value + radius; y++)
            {
                if (x < 0 || y < 0 || x >= GridWidth || y >= GridHeight)
                    continue;

                if (_cellMap.TryGetValue((x, y), out var cell))
                    ApplyEditorValuesToCell(cell);
            }
        }

        UpdateSelectionText();
        UpdateMapSummary();
        DrawMap();
    }

    private int GetBrushRadius()
    {
        if (_brushRadiusBox?.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var radius))
        {
            return Math.Max(0, radius);
        }

        return 0;
    }

    private void ApplyEditorValuesToCell(PreparedGridCellDto cell)
    {
        cell.Vegetation = GetSelectedVegetation();
        cell.Moisture = Math.Clamp(ParseDouble(_moistureBox?.Text, cell.Moisture), 0.0, 1.0);
        cell.Elevation = ParseDouble(_elevationBox?.Text, cell.Elevation);
    }

    private string GetSelectedVegetation()
    {
        if (_vegetationBox?.SelectedItem is ComboBoxItem item)
            return item.Tag?.ToString() ?? "Mixed";

        return "Mixed";
    }

    private void ResetMap()
    {
        _cellMap.Clear();

        foreach (var cell in _originalCells)
            _cellMap[(cell.X, cell.Y)] = CloneCell(cell);

        _selectedX = null;
        _selectedY = null;

        UpdateSelectionText();
        UpdateMapSummary();
        DrawMap();
    }

    private void UpdateSelectionText()
    {
        if (_selectionInfoTextBlock == null)
            return;

        if (_selectedX == null || _selectedY == null)
        {
            _selectionInfoTextBlock.Text = "Клетка не выбрана.";
            return;
        }

        if (!_cellMap.TryGetValue((_selectedX.Value, _selectedY.Value), out var cell))
        {
            _selectionInfoTextBlock.Text = "Клетка не найдена.";
            return;
        }

        _selectionInfoTextBlock.Text =
            $"Клетка: ({cell.X}, {cell.Y}){Environment.NewLine}" +
            $"Тип: {GetVegetationCaption(cell.Vegetation)}{Environment.NewLine}" +
            $"Влажность: {cell.Moisture:F2}{Environment.NewLine}" +
            $"Высота: {cell.Elevation:F1}";
    }

    private void UpdateMapSummary()
    {
        if (_mapSummaryTextBlock == null)
            return;

        var cells = _cellMap.Values.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();

        var grouped = cells
            .GroupBy(c => GetVegetationCaption(c.Vegetation))
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();

        double avgMoisture = cells.Count == 0 ? 0.0 : cells.Average(c => c.Moisture);
        double avgElevation = cells.Count == 0 ? 0.0 : cells.Average(c => c.Elevation);

        _mapSummaryTextBlock.Text =
            $"Размер: {GridWidth}×{GridHeight}{Environment.NewLine}" +
            $"Клеток: {cells.Count}{Environment.NewLine}" +
            $"Средняя влажность: {avgMoisture:F2}{Environment.NewLine}" +
            $"Средняя высота: {avgElevation:F1}{Environment.NewLine}" +
            string.Join(Environment.NewLine, grouped.Take(7));
    }

    private string GetVegetationCaption(string vegetation)
    {
        return vegetation switch
        {
            "Grass" => "Трава",
            "Shrub" => "Кустарник",
            "Deciduous" => "Лиственный лес",
            "Coniferous" => "Хвойный лес",
            "Mixed" => "Смешанный лес",
            "Water" => "Вода",
            "Bare" => "Пустая поверхность",
            _ => vegetation
        };
    }

    private void DrawMap()
    {
        if (_mapCanvas == null)
            return;

        _mapCanvas.Children.Clear();

        int cellSize = GetCellSize();
        double width = GridWidth * cellSize + CanvasPadding * 2;
        double height = GridHeight * cellSize + CanvasPadding * 2;

        _mapCanvas.Width = width;
        _mapCanvas.Height = height;

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                if (!_cellMap.TryGetValue((x, y), out var cell))
                    continue;

                DrawCell(cell, cellSize);
            }
        }
    }

    private int GetCellSize()
    {
        int maxDimension = Math.Max(GridWidth, GridHeight);

        if (maxDimension <= 20) return 28;
        if (maxDimension <= 30) return 24;
        if (maxDimension <= 40) return 20;
        if (maxDimension <= 60) return 16;

        return MinCellSize;
    }

    private void DrawCell(PreparedGridCellDto cell, int cellSize)
    {
        if (_mapCanvas == null)
            return;

        double x = CanvasPadding + cell.X * cellSize;
        double y = CanvasPadding + cell.Y * cellSize;

        var rect = new Rectangle
        {
            Width = cellSize,
            Height = cellSize,
            Fill = GetCellBrush(cell),
            Stroke = IsSelectedCell(cell)
                ? new SolidColorBrush(Color.Parse("#B5473E"))
                : new SolidColorBrush(Color.Parse("#F1EADF")),
            StrokeThickness = IsSelectedCell(cell) ? 2.0 : 0.8
        };

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        _mapCanvas.Children.Add(rect);
    }

    private bool IsSelectedCell(PreparedGridCellDto cell)
    {
        return _selectedX == cell.X && _selectedY == cell.Y;
    }

    private IBrush GetCellBrush(PreparedGridCellDto cell)
    {
        return (cell.Vegetation ?? string.Empty).Trim() switch
        {
            "Coniferous" => new SolidColorBrush(Color.Parse("#5E9B5E")),
            "Deciduous" => new SolidColorBrush(Color.Parse("#8ACB88")),
            "Mixed" => new SolidColorBrush(Color.Parse("#A8C97F")),
            "Grass" => new SolidColorBrush(Color.Parse("#E7D36F")),
            "Shrub" => new SolidColorBrush(Color.Parse("#CFA46A")),
            "Water" => new SolidColorBrush(Color.Parse("#7CC6F2")),
            "Bare" => new SolidColorBrush(Color.Parse("#C9B7A7")),

            "Хвойный лес" => new SolidColorBrush(Color.Parse("#5E9B5E")),
            "Лиственный лес" => new SolidColorBrush(Color.Parse("#8ACB88")),
            "Смешанный лес" => new SolidColorBrush(Color.Parse("#A8C97F")),
            "Трава" => new SolidColorBrush(Color.Parse("#E7D36F")),
            "Кустарник" => new SolidColorBrush(Color.Parse("#CFA46A")),
            "Вода" => new SolidColorBrush(Color.Parse("#7CC6F2")),
            "Пустая поверхность" => new SolidColorBrush(Color.Parse("#C9B7A7")),

            _ => new SolidColorBrush(Color.Parse("#A8C97F"))
        };
    }

    private bool TryGetGridCell(Point point, out int cellX, out int cellY)
    {
        int cellSize = GetCellSize();

        double localX = point.X - CanvasPadding;
        double localY = point.Y - CanvasPadding;

        cellX = (int)(localX / cellSize);
        cellY = (int)(localY / cellSize);

        if (localX < 0 || localY < 0 || cellX < 0 || cellY < 0 || cellX >= GridWidth || cellY >= GridHeight)
            return false;

        return true;
    }

    private static double ParseDouble(string? text, double fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        text = text.Replace(',', '.');

        return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    public PreparedGridMapDto GetPreparedMap()
    {
        return new PreparedGridMapDto
        {
            Width = GridWidth,
            Height = GridHeight,
            Cells = _cellMap.Values
                .OrderBy(c => c.Y)
                .ThenBy(c => c.X)
                .Select(c => new PreparedGridCellDto
                {
                    X = c.X,
                    Y = c.Y,
                    Vegetation = NormalizeVegetation(c.Vegetation),
                    Moisture = Math.Clamp(c.Moisture, 0.0, 1.0),
                    Elevation = c.Elevation
                })
                .ToList()
        };
    }
    private string NormalizeVegetation(string? vegetation)
    {
        return vegetation switch
        {
            "Grass" => "Grass",
            "Shrub" => "Shrub",
            "Deciduous" => "Deciduous",
            "Coniferous" => "Coniferous",
            "Mixed" => "Mixed",
            "Water" => "Water",
            "Bare" => "Bare",
            _ => "Mixed"
        };
    }

}