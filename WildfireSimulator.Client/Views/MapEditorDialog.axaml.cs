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
    private const int MinCellSize = 12;

    private Canvas? _mapCanvas;
    private ComboBox? _toolBox;
    private ComboBox? _shapeBox;
    private TextBox? _strengthBox;
    private TextBlock? _selectionInfoTextBlock;
    private TextBlock? _objectsSummaryTextBlock;
    private ListBox? _objectsListBox;

    private Button? _undoButton;
    private Button? _removeSelectedButton;
    private Button? _clearButton;
    private Button? _applyButton;
    private Button? _cancelButton;
    private Button? _closeOnlyButton;

    private bool _isDragging;
    private int _dragStartX;
    private int _dragStartY;
    private int _dragCurrentX;
    private int _dragCurrentY;

    private Guid? _selectedObjectId;

    public int GridWidth { get; }
    public int GridHeight { get; }

    public List<MapRegionObjectDto> EditedObjects { get; private set; } = new();

    public MapEditorDialog(int gridWidth, int gridHeight, IEnumerable<MapRegionObjectDto>? existingObjects = null)
    {
        GridWidth = Math.Max(5, gridWidth);
        GridHeight = Math.Max(5, gridHeight);

        if (existingObjects != null)
            EditedObjects = existingObjects.Select(CloneObject).ToList();

        InitializeComponent();
        FindControls();
        AttachEvents();
        NormalizePriorities();
        RefreshObjectsList();
        UpdateSummaryText();
        UpdateSelectionText();
        DrawMap();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void FindControls()
    {
        _mapCanvas = this.FindControl<Canvas>("MapCanvas");
        _toolBox = this.FindControl<ComboBox>("ToolBox");
        _shapeBox = this.FindControl<ComboBox>("ShapeBox");
        _strengthBox = this.FindControl<TextBox>("StrengthBox");
        _selectionInfoTextBlock = this.FindControl<TextBlock>("SelectionInfoTextBlock");
        _objectsSummaryTextBlock = this.FindControl<TextBlock>("ObjectsSummaryTextBlock");
        _objectsListBox = this.FindControl<ListBox>("ObjectsListBox");

        _undoButton = this.FindControl<Button>("UndoButton");
        _removeSelectedButton = this.FindControl<Button>("RemoveSelectedButton");
        _clearButton = this.FindControl<Button>("ClearButton");
        _applyButton = this.FindControl<Button>("ApplyButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
        _closeOnlyButton = this.FindControl<Button>("CloseOnlyButton");
    }

    private void AttachEvents()
    {
        if (_mapCanvas != null)
        {
            _mapCanvas.PointerPressed += OnCanvasPointerPressed;
            _mapCanvas.PointerMoved += OnCanvasPointerMoved;
            _mapCanvas.PointerReleased += OnCanvasPointerReleased;
        }

        if (_objectsListBox != null)
        {
            _objectsListBox.SelectionChanged += (_, _) =>
            {
                if (_objectsListBox.SelectedIndex >= 0 &&
                    _objectsListBox.SelectedIndex < EditedObjects.Count)
                {
                    _selectedObjectId = EditedObjects[_objectsListBox.SelectedIndex].Id;
                }
                else
                {
                    _selectedObjectId = null;
                }

                UpdateSummaryText();
                DrawMap();
            };
        }

        if (_undoButton != null)
        {
            _undoButton.Click += (_, _) =>
            {
                if (EditedObjects.Count == 0)
                    return;

                var removed = EditedObjects[^1];
                EditedObjects.RemoveAt(EditedObjects.Count - 1);

                if (_selectedObjectId == removed.Id)
                    _selectedObjectId = null;

                NormalizePriorities();
                RefreshObjectsList();
                UpdateSummaryText();
                UpdateSelectionText();
                DrawMap();
            };
        }

        if (_removeSelectedButton != null)
        {
            _removeSelectedButton.Click += (_, _) =>
            {
                if (_selectedObjectId == null)
                    return;

                var removed = EditedObjects.RemoveAll(x => x.Id == _selectedObjectId.Value);
                if (removed == 0)
                    return;

                _selectedObjectId = null;
                NormalizePriorities();
                RefreshObjectsList();
                UpdateSummaryText();
                UpdateSelectionText();
                DrawMap();
            };
        }

        if (_clearButton != null)
        {
            _clearButton.Click += (_, _) =>
            {
                EditedObjects.Clear();
                _selectedObjectId = null;
                NormalizePriorities();
                RefreshObjectsList();
                UpdateSummaryText();
                UpdateSelectionText();
                DrawMap();
            };
        }

        if (_applyButton != null)
            _applyButton.Click += (_, _) => Close(true);

        if (_cancelButton != null)
            _cancelButton.Click += (_, _) => Close(false);

        if (_closeOnlyButton != null)
            _closeOnlyButton.Click += (_, _) => Close(false);
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_mapCanvas == null)
            return;

        var point = e.GetPosition(_mapCanvas);
        if (!TryGetGridCell(point, out var cellX, out var cellY))
            return;

        _isDragging = true;
        _dragStartX = cellX;
        _dragStartY = cellY;
        _dragCurrentX = cellX;
        _dragCurrentY = cellY;

        UpdateSelectionText();
        DrawMap();
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _mapCanvas == null)
            return;

        var point = e.GetPosition(_mapCanvas);
        if (!TryGetGridCell(point, out var cellX, out var cellY))
            return;

        _dragCurrentX = cellX;
        _dragCurrentY = cellY;

        UpdateSelectionText();
        DrawMap();
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;

        var normalized = GetNormalizedSelection();
        if (normalized.Width <= 0 || normalized.Height <= 0)
        {
            UpdateSelectionText();
            DrawMap();
            return;
        }

        var newObject = new MapRegionObjectDto
        {
            Id = Guid.NewGuid(),
            ObjectType = GetSelectedTool(),
            Shape = GetSelectedShape(),
            StartX = normalized.StartX,
            StartY = normalized.StartY,
            Width = normalized.Width,
            Height = normalized.Height,
            Strength = ParseStrength(),
            Priority = EditedObjects.Count
        };

        EditedObjects.Add(newObject);
        _selectedObjectId = newObject.Id;

        NormalizePriorities();
        RefreshObjectsList();
        UpdateSummaryText();
        UpdateSelectionText();
        DrawMap();
    }

    private void DrawMap()
    {
        if (_mapCanvas == null)
            return;

        _mapCanvas.Children.Clear();

        int cellSize = GetCellSize(GridWidth, GridHeight);
        double contentWidth = GridWidth * cellSize;
        double contentHeight = GridHeight * cellSize;

        _mapCanvas.Width = contentWidth + CanvasPadding * 2;
        _mapCanvas.Height = contentHeight + CanvasPadding * 2;

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                var rect = new Rectangle
                {
                    Width = cellSize,
                    Height = cellSize,
                    Fill = GetPreviewBrush(x, y),
                    Stroke = new SolidColorBrush(Color.Parse("#E7E0F2")),
                    StrokeThickness = 0.6
                };

                Canvas.SetLeft(rect, CanvasPadding + x * cellSize);
                Canvas.SetTop(rect, CanvasPadding + y * cellSize);
                _mapCanvas.Children.Add(rect);
            }
        }

        foreach (var obj in EditedObjects.OrderBy(o => o.Priority))
            DrawObjectOutline(obj, cellSize);

        if (_isDragging)
        {
            var normalized = GetNormalizedSelection();
            DrawSelectionOverlay(normalized.StartX, normalized.StartY, normalized.Width, normalized.Height, cellSize);
        }
    }

    private void DrawObjectOutline(MapRegionObjectDto obj, int cellSize)
    {
        if (_mapCanvas == null)
            return;

        bool isSelected = _selectedObjectId == obj.Id;

        Shape overlay;

        if (obj.Shape == MapObjectShape.Ellipse)
        {
            overlay = new Ellipse();
        }
        else
        {
            overlay = new Rectangle();
        }

        overlay.Width = obj.Width * cellSize;
        overlay.Height = obj.Height * cellSize;
        overlay.Fill = Brushes.Transparent;
        overlay.Stroke = new SolidColorBrush(isSelected ? Color.Parse("#3F345C") : Color.Parse("#6E6A78"));
        overlay.StrokeThickness = isSelected ? 2.6 : 1.2;
        overlay.IsHitTestVisible = false;

        Canvas.SetLeft(overlay, CanvasPadding + obj.StartX * cellSize);
        Canvas.SetTop(overlay, CanvasPadding + obj.StartY * cellSize);
        _mapCanvas.Children.Add(overlay);
    }

    private void DrawSelectionOverlay(int startX, int startY, int width, int height, int cellSize)
    {
        if (_mapCanvas == null || width <= 0 || height <= 0)
            return;

        var overlay = new Rectangle
        {
            Width = width * cellSize,
            Height = height * cellSize,
            Fill = new SolidColorBrush(Color.FromArgb(70, 142, 124, 195)),
            Stroke = new SolidColorBrush(Color.Parse("#8E7CC3")),
            StrokeThickness = 2,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(overlay, CanvasPadding + startX * cellSize);
        Canvas.SetTop(overlay, CanvasPadding + startY * cellSize);
        _mapCanvas.Children.Add(overlay);
    }

    private IBrush GetPreviewBrush(int x, int y)
    {
        var cellColor = Color.Parse("#6C8B55");

        foreach (var obj in EditedObjects.OrderBy(o => o.Priority))
        {
            if (!IsCellInsideObject(x, y, obj))
                continue;

            cellColor = obj.ObjectType switch
            {
                MapObjectType.ConiferousArea => Color.Parse("#446B3C"),
                MapObjectType.DeciduousArea => Color.Parse("#6C9A57"),
                MapObjectType.MixedForestArea => Color.Parse("#5E8050"),
                MapObjectType.GrassArea => Color.Parse("#A7B85A"),
                MapObjectType.ShrubArea => Color.Parse("#7D8F4D"),
                MapObjectType.WaterBody => Color.Parse("#6BA7D6"),
                MapObjectType.Firebreak => Color.Parse("#B8A389"),
                MapObjectType.WetZone => Color.Parse("#9ED3E8"),
                MapObjectType.DryZone => Color.Parse("#D8C1A1"),
                MapObjectType.Hill => Color.Parse("#C49472"),
                MapObjectType.Lowland => Color.Parse("#8AB7A8"),
                _ => cellColor
            };
        }

        return new SolidColorBrush(cellColor);
    }

    private bool TryGetGridCell(Point point, out int cellX, out int cellY)
    {
        cellX = -1;
        cellY = -1;

        if (_mapCanvas == null)
            return false;

        int cellSize = GetCellSize(GridWidth, GridHeight);

        double localX = point.X - CanvasPadding;
        double localY = point.Y - CanvasPadding;

        if (localX < 0 || localY < 0)
            return false;

        cellX = (int)(localX / cellSize);
        cellY = (int)(localY / cellSize);

        if (cellX < 0 || cellX >= GridWidth || cellY < 0 || cellY >= GridHeight)
            return false;

        return true;
    }

    private (int StartX, int StartY, int Width, int Height) GetNormalizedSelection()
    {
        int startX = Math.Min(_dragStartX, _dragCurrentX);
        int startY = Math.Min(_dragStartY, _dragCurrentY);
        int endX = Math.Max(_dragStartX, _dragCurrentX);
        int endY = Math.Max(_dragStartY, _dragCurrentY);

        return (startX, startY, endX - startX + 1, endY - startY + 1);
    }

    private void UpdateSelectionText()
    {
        if (_selectionInfoTextBlock == null)
            return;

        if (_isDragging)
        {
            var selection = GetNormalizedSelection();
            _selectionInfoTextBlock.Text =
                $"Выделение: X={selection.StartX}, Y={selection.StartY}, ширина={selection.Width}, высота={selection.Height}";
            return;
        }

        if (_selectedObjectId != null)
        {
            var selected = EditedObjects.FirstOrDefault(x => x.Id == _selectedObjectId.Value);
            if (selected != null)
            {
                _selectionInfoTextBlock.Text = $"Выбран объект: {BuildObjectDescription(selected)}";
                return;
            }
        }

        _selectionInfoTextBlock.Text = "Выделение завершено. Можно продолжать добавлять области.";
    }

    private void UpdateSummaryText()
    {
        if (_objectsSummaryTextBlock == null)
            return;

        if (EditedObjects.Count == 0)
        {
            _objectsSummaryTextBlock.Text = "Добавленных объектов: 0";
            return;
        }

        var grouped = EditedObjects
            .GroupBy(x => GetObjectTypeName(x.ObjectType))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();

        var selectedText = _selectedObjectId == null
            ? "объект не выбран"
            : "объект выбран";

        _objectsSummaryTextBlock.Text =
            $"Добавленных объектов: {EditedObjects.Count} ({selectedText}){Environment.NewLine}" +
            string.Join(" • ", grouped);
    }

    private void RefreshObjectsList()
    {
        if (_objectsListBox == null)
            return;

        var items = EditedObjects
            .OrderBy(x => x.Priority)
            .Select(BuildObjectDescription)
            .ToList();

        _objectsListBox.ItemsSource = items;

        if (_selectedObjectId == null)
        {
            _objectsListBox.SelectedIndex = -1;
            return;
        }

        var selectedIndex = EditedObjects.FindIndex(x => x.Id == _selectedObjectId.Value);
        _objectsListBox.SelectedIndex = selectedIndex;
    }

    private void NormalizePriorities()
    {
        for (int i = 0; i < EditedObjects.Count; i++)
            EditedObjects[i].Priority = i;
    }

    private string BuildObjectDescription(MapRegionObjectDto obj)
    {
        var shape = obj.Shape == MapObjectShape.Ellipse ? "эллипс" : "прямоугольник";
        return $"{GetObjectTypeName(obj.ObjectType)} • {shape} • X={obj.StartX}, Y={obj.StartY}, W={obj.Width}, H={obj.Height}, S={obj.Strength:F1}";
    }

    private string GetObjectTypeName(MapObjectType type)
    {
        return type switch
        {
            MapObjectType.ConiferousArea => "Хвойный лес",
            MapObjectType.DeciduousArea => "Лиственный лес",
            MapObjectType.MixedForestArea => "Смешанный лес",
            MapObjectType.GrassArea => "Трава",
            MapObjectType.ShrubArea => "Кустарник",
            MapObjectType.WaterBody => "Водоём",
            MapObjectType.Firebreak => "Просека",
            MapObjectType.WetZone => "Влажная зона",
            MapObjectType.DryZone => "Сухая зона",
            MapObjectType.Hill => "Холм",
            MapObjectType.Lowland => "Низина",
            _ => "Объект"
        };
    }

    private MapObjectType GetSelectedTool()
    {
        return _toolBox?.SelectedIndex switch
        {
            1 => MapObjectType.DeciduousArea,
            2 => MapObjectType.MixedForestArea,
            3 => MapObjectType.GrassArea,
            4 => MapObjectType.ShrubArea,
            5 => MapObjectType.WaterBody,
            6 => MapObjectType.Firebreak,
            7 => MapObjectType.WetZone,
            8 => MapObjectType.DryZone,
            9 => MapObjectType.Hill,
            10 => MapObjectType.Lowland,
            _ => MapObjectType.ConiferousArea
        };
    }

    private MapObjectShape GetSelectedShape()
    {
        return _shapeBox?.SelectedIndex == 1
            ? MapObjectShape.Ellipse
            : MapObjectShape.Rectangle;
    }

    private double ParseStrength()
    {
        var text = (_strengthBox?.Text ?? "1.0").Replace(',', '.');

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0.1, 3.0)
            : 1.0;
    }

    private bool IsCellInsideObject(int x, int y, MapRegionObjectDto obj)
    {
        if (obj.Shape == MapObjectShape.Rectangle)
        {
            return x >= obj.StartX &&
                   x < obj.StartX + obj.Width &&
                   y >= obj.StartY &&
                   y < obj.StartY + obj.Height;
        }

        double centerX = obj.StartX + (obj.Width - 1) / 2.0;
        double centerY = obj.StartY + (obj.Height - 1) / 2.0;
        double radiusX = Math.Max(0.5, obj.Width / 2.0);
        double radiusY = Math.Max(0.5, obj.Height / 2.0);

        double nx = (x - centerX) / radiusX;
        double ny = (y - centerY) / radiusY;

        return nx * nx + ny * ny <= 1.0;
    }

    private int GetCellSize(int width, int height)
    {
        var maxDimension = Math.Max(width, height);

        if (maxDimension <= 20) return 24;
        if (maxDimension <= 30) return 20;
        if (maxDimension <= 40) return 16;
        if (maxDimension <= 60) return 13;

        return MinCellSize;
    }

    private static MapRegionObjectDto CloneObject(MapRegionObjectDto source)
    {
        return new MapRegionObjectDto
        {
            Id = source.Id,
            ObjectType = source.ObjectType,
            Shape = source.Shape,
            StartX = source.StartX,
            StartY = source.StartY,
            Width = source.Width,
            Height = source.Height,
            Strength = source.Strength,
            Priority = source.Priority
        };
    }
}