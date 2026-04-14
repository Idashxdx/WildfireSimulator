using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using WildfireSimulator.Client.Models;

namespace WildfireSimulator.Client.Views.Controls;

public partial class MetricsHistoryView : UserControl
{
    private Canvas? _chartCanvas;
    private TextBlock? _currentAreaTextBlock;
    private TextBlock? _forecastTextBlock;
    private TextBlock? _maeTextBlock;
    private TextBlock? _trendTextBlock;
    private TextBlock? _anomalyTextBlock;
    private TextBlock? _minStepTextBlock;
    private TextBlock? _maxStepTextBlock;

    private bool _drawScheduled;
    private INotifyCollectionChanged? _currentCollection;

    public static readonly StyledProperty<IEnumerable<FireMetricsHistoryDto>?> MetricsHistoryProperty =
        AvaloniaProperty.Register<MetricsHistoryView, IEnumerable<FireMetricsHistoryDto>?>(nameof(MetricsHistory));

    public static readonly StyledProperty<int> CurrentStepProperty =
        AvaloniaProperty.Register<MetricsHistoryView, int>(nameof(CurrentStep), 0);

    public static readonly StyledProperty<double> FireAreaProperty =
        AvaloniaProperty.Register<MetricsHistoryView, double>(nameof(FireArea), 0);

    public static readonly StyledProperty<double> ForecastNextAreaProperty =
        AvaloniaProperty.Register<MetricsHistoryView, double>(nameof(ForecastNextArea), 0);

    public static readonly StyledProperty<double> MeanAbsoluteErrorProperty =
        AvaloniaProperty.Register<MetricsHistoryView, double>(nameof(MeanAbsoluteError), 0);

    public static readonly StyledProperty<string> TrendTextProperty =
        AvaloniaProperty.Register<MetricsHistoryView, string>(nameof(TrendText), "—");

    public static readonly StyledProperty<string> LastAnomalyTextProperty =
        AvaloniaProperty.Register<MetricsHistoryView, string>(nameof(LastAnomalyText), "Нет");

    public IEnumerable<FireMetricsHistoryDto>? MetricsHistory
    {
        get => GetValue(MetricsHistoryProperty);
        set => SetValue(MetricsHistoryProperty, value);
    }

    public int CurrentStep
    {
        get => GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    public double FireArea
    {
        get => GetValue(FireAreaProperty);
        set => SetValue(FireAreaProperty, value);
    }

    public double ForecastNextArea
    {
        get => GetValue(ForecastNextAreaProperty);
        set => SetValue(ForecastNextAreaProperty, value);
    }

    public double MeanAbsoluteError
    {
        get => GetValue(MeanAbsoluteErrorProperty);
        set => SetValue(MeanAbsoluteErrorProperty, value);
    }

    public string TrendText
    {
        get => GetValue(TrendTextProperty);
        set => SetValue(TrendTextProperty, value);
    }

    public string LastAnomalyText
    {
        get => GetValue(LastAnomalyTextProperty);
        set => SetValue(LastAnomalyTextProperty, value);
    }

    public MetricsHistoryView()
    {
        InitializeComponent();

        _chartCanvas = this.FindControl<Canvas>("ChartCanvas");
        _currentAreaTextBlock = this.FindControl<TextBlock>("CurrentAreaTextBlock");
        _forecastTextBlock = this.FindControl<TextBlock>("ForecastTextBlock");
        _maeTextBlock = this.FindControl<TextBlock>("MaeTextBlock");
        _trendTextBlock = this.FindControl<TextBlock>("TrendTextBlock");
        _anomalyTextBlock = this.FindControl<TextBlock>("AnomalyTextBlock");
        _minStepTextBlock = this.FindControl<TextBlock>("MinStepTextBlock");
        _maxStepTextBlock = this.FindControl<TextBlock>("MaxStepTextBlock");

        PropertyChanged += OnControlPropertyChanged;
        AttachedToVisualTree += (_, _) => ScheduleRefresh();

        RebindCollection();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MetricsHistoryProperty)
        {
            RebindCollection();
            ScheduleRefresh();
            return;
        }

        if (e.Property == CurrentStepProperty ||
            e.Property == FireAreaProperty ||
            e.Property == ForecastNextAreaProperty ||
            e.Property == MeanAbsoluteErrorProperty ||
            e.Property == TrendTextProperty ||
            e.Property == LastAnomalyTextProperty)
        {
            ScheduleRefresh();
        }
    }

    private void RebindCollection()
    {
        if (_currentCollection != null)
        {
            _currentCollection.CollectionChanged -= OnHistoryCollectionChanged;
            _currentCollection = null;
        }

        if (MetricsHistory is INotifyCollectionChanged notifyCollection)
        {
            _currentCollection = notifyCollection;
            _currentCollection.CollectionChanged += OnHistoryCollectionChanged;
        }
    }

    private void OnHistoryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        if (_drawScheduled)
            return;

        _drawScheduled = true;

        Dispatcher.UIThread.Post(() =>
        {
            _drawScheduled = false;
            RefreshView();
        }, DispatcherPriority.Background);
    }

    private void RefreshView()
    {
        UpdateHeader();
        DrawAreaChart();
    }

    private void UpdateHeader()
    {
        if (_currentAreaTextBlock != null)
            _currentAreaTextBlock.Text = $"{FireArea:F0} га";

        if (_forecastTextBlock != null)
            _forecastTextBlock.Text = ForecastNextArea > 0
                ? $"{ForecastNextArea:F0} га"
                : "—";

        if (_maeTextBlock != null)
            _maeTextBlock.Text = MeanAbsoluteError > 0
                ? $"{MeanAbsoluteError:F2} га"
                : "—";

        if (_trendTextBlock != null)
            _trendTextBlock.Text = string.IsNullOrWhiteSpace(TrendText)
                ? "—"
                : TrendText;

        if (_anomalyTextBlock != null)
            _anomalyTextBlock.Text = string.IsNullOrWhiteSpace(LastAnomalyText)
                ? "Нет"
                : LastAnomalyText;
    }

    private void DrawAreaChart()
    {
        if (_chartCanvas == null)
            return;

        _chartCanvas.Children.Clear();

        var history = (MetricsHistory ?? Enumerable.Empty<FireMetricsHistoryDto>())
            .OrderBy(x => x.Step)
            .ToList();

        if (history.Count == 0)
        {
            DrawEmptyState();
            return;
        }

        const double width = 700;
        const double height = 180;
        const double leftPadding = 34;
        const double rightPadding = 16;
        const double topPadding = 10;
        const double bottomPadding = 28;

        _chartCanvas.Width = width;
        _chartCanvas.Height = height;

        double plotWidth = width - leftPadding - rightPadding;
        double plotHeight = height - topPadding - bottomPadding;

        int minStep = history.Min(x => x.Step);
        int maxStep = history.Max(x => x.Step);

        double minArea = 0;
        double maxArea = Math.Max(1, history.Max(x => x.FireArea));

        if (_minStepTextBlock != null)
            _minStepTextBlock.Text = $"Шаг {minStep}";

        if (_maxStepTextBlock != null)
            _maxStepTextBlock.Text = $"Шаг {maxStep}";

        DrawGrid(leftPadding, topPadding, plotWidth, plotHeight);

        var points = new List<Point>();

        for (int i = 0; i < history.Count; i++)
        {
            var item = history[i];

            double x = history.Count == 1
                ? leftPadding + plotWidth / 2.0
                : leftPadding + (i * plotWidth / (history.Count - 1));

            double normalized = (item.FireArea - minArea) / Math.Max(1, maxArea - minArea);
            double y = topPadding + plotHeight - normalized * plotHeight;

            points.Add(new Point(x, y));
        }

        for (int i = 1; i < points.Count; i++)
        {
            var line = new Line
            {
                StartPoint = points[i - 1],
                EndPoint = points[i],
                Stroke = new SolidColorBrush(Color.Parse("#8E7CC3")),
                StrokeThickness = 2
            };

            _chartCanvas.Children.Add(line);
        }

        foreach (var point in points)
        {
            var marker = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = new SolidColorBrush(Color.Parse("#8E7CC3"))
            };

            Canvas.SetLeft(marker, point.X - 2.5);
            Canvas.SetTop(marker, point.Y - 2.5);
            _chartCanvas.Children.Add(marker);
        }

        DrawTopValue(maxArea, leftPadding, topPadding);
        DrawBottomValue(minArea, leftPadding, topPadding + plotHeight);
    }

    private void DrawGrid(double x, double y, double width, double height)
    {
        if (_chartCanvas == null)
            return;

        for (int i = 0; i <= 4; i++)
        {
            double lineY = y + i * height / 4.0;

            var line = new Line
            {
                StartPoint = new Point(x, lineY),
                EndPoint = new Point(x + width, lineY),
                Stroke = new SolidColorBrush(Color.Parse("#E6E1F0")),
                StrokeThickness = 1
            };

            _chartCanvas.Children.Add(line);
        }

        var axisX = new Line
        {
            StartPoint = new Point(x, y + height),
            EndPoint = new Point(x + width, y + height),
            Stroke = new SolidColorBrush(Color.Parse("#D7D0E6")),
            StrokeThickness = 1.2
        };

        var axisY = new Line
        {
            StartPoint = new Point(x, y),
            EndPoint = new Point(x, y + height),
            Stroke = new SolidColorBrush(Color.Parse("#D7D0E6")),
            StrokeThickness = 1.2
        };

        _chartCanvas.Children.Add(axisX);
        _chartCanvas.Children.Add(axisY);
    }

    private void DrawTopValue(double value, double x, double y)
    {
        if (_chartCanvas == null)
            return;

        var text = new TextBlock
        {
            Text = value.ToString("F0", CultureInfo.InvariantCulture),
            Foreground = new SolidColorBrush(Color.Parse("#8A8495")),
            FontSize = 11
        };

        Canvas.SetLeft(text, 0);
        Canvas.SetTop(text, Math.Max(0, y - 6));
        _chartCanvas.Children.Add(text);
    }

    private void DrawBottomValue(double value, double x, double y)
    {
        if (_chartCanvas == null)
            return;

        var text = new TextBlock
        {
            Text = value.ToString("F0", CultureInfo.InvariantCulture),
            Foreground = new SolidColorBrush(Color.Parse("#8A8495")),
            FontSize = 11
        };

        Canvas.SetLeft(text, 0);
        Canvas.SetTop(text, y - 10);
        _chartCanvas.Children.Add(text);
    }

    private void DrawEmptyState()
    {
        if (_chartCanvas == null)
            return;

        _chartCanvas.Width = 700;
        _chartCanvas.Height = 180;

        var text = new TextBlock
        {
            Text = "История шагов пока недоступна",
            Foreground = new SolidColorBrush(Color.Parse("#8A8495")),
            FontSize = 14,
            FontWeight = FontWeight.SemiBold
        };

        Canvas.SetLeft(text, 220);
        Canvas.SetTop(text, 78);
        _chartCanvas.Children.Add(text);

        if (_minStepTextBlock != null)
            _minStepTextBlock.Text = "Шаг 0";

        if (_maxStepTextBlock != null)
            _maxStepTextBlock.Text = "Шаг 0";
    }
}