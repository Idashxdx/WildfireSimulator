using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WildfireSimulator.Client.ViewModels;

namespace WildfireSimulator.Client.Views;

public partial class CreateSimulationDialog : Window
{
    public string SimulationName { get; private set; } = string.Empty;
    public int GridWidth { get; private set; } = 20;
    public int GridHeight { get; private set; } = 20;
    public int InitialFireCells { get; private set; } = 3;
    public double MoistureMin { get; private set; } = 0.3;
    public double MoistureMax { get; private set; } = 0.7;
    public double ElevationVariation { get; private set; } = 50.0;
    public int SimulationSteps { get; private set; } = 100;
    public int StepDurationSeconds { get; private set; } = 900;
    public double Temperature { get; private set; } = 25.0;
    public double Humidity { get; private set; } = 40.0;
    public double WindSpeed { get; private set; } = 5.0;
    public double WindDirection { get; private set; } = 45.0;

    public double Precipitation { get; private set; } = 0.0;
    public int? RandomSeed { get; private set; }

    public List<(int VegetationType, double Probability)> VegetationDistributions { get; private set; } = new();
    private TextBox? _coniferousBox;
    private TextBox? _deciduousBox;
    private TextBox? _mixedBox;
    private TextBox? _grassBox;
    private TextBox? _shrubBox;
    private TextBox? _waterBox;
    private TextBox? _bareBox;

    private readonly AppPage _page;
    private readonly GraphCreationMode _mode;

    private TextBox? _nameBox;
    private TextBox? _widthBox;
    private TextBox? _heightBox;
    private TextBox? _fireCellsBox;
    private TextBox? _moistureMinBox;
    private TextBox? _moistureMaxBox;
    private TextBox? _elevationBox;
    private TextBox? _stepsBox;
    private TextBox? _stepDurationBox;
    private TextBox? _tempBox;
    private TextBox? _humidityBox;
    private TextBox? _windSpeedBox;
    private ComboBox? _windDirBox;
    private TextBlock? _errorTextBlock;

    private TextBlock? _typeInfoTextBlock;
    private TextBlock? _typeHintTextBlock;
    private TextBlock? _widthLabelTextBlock;
    private TextBlock? _heightLabelTextBlock;
    private TextBlock? _widthHintTextBlock;
    private TextBlock? _heightHintTextBlock;
    private TextBlock? _fireCellsHintTextBlock;

    private TextBlock? _structureSummaryTextBlock;
    private TextBlock? _structureDetailTextBlock;

    private TextBox? _precipitationBox;
    private TextBox? _randomSeedBox;

    public CreateSimulationDialog(AppPage page, GraphCreationMode mode)
    {
        _page = page;
        _mode = mode;

        InitializeComponent();
        FindControls();
        AttachEvents();
        ApplyDefaults();
        ApplyModeTexts();
        UpdateStructurePreview();
        ClearErrors();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void FindControls()
    {
        _nameBox = this.FindControl<TextBox>("NameBox");
        _widthBox = this.FindControl<TextBox>("WidthBox");
        _heightBox = this.FindControl<TextBox>("HeightBox");
        _fireCellsBox = this.FindControl<TextBox>("FireCellsBox");
        _moistureMinBox = this.FindControl<TextBox>("MoistureMinBox");
        _moistureMaxBox = this.FindControl<TextBox>("MoistureMaxBox");
        _elevationBox = this.FindControl<TextBox>("ElevationBox");
        _stepsBox = this.FindControl<TextBox>("StepsBox");
        _stepDurationBox = this.FindControl<TextBox>("StepDurationBox");
        _tempBox = this.FindControl<TextBox>("TempBox");
        _humidityBox = this.FindControl<TextBox>("HumidityBox");
        _windSpeedBox = this.FindControl<TextBox>("WindSpeedBox");
        _windDirBox = this.FindControl<ComboBox>("WindDirBox");
        _errorTextBlock = this.FindControl<TextBlock>("ErrorTextBlock");

        _typeInfoTextBlock = this.FindControl<TextBlock>("TypeInfoTextBlock");
        _typeHintTextBlock = this.FindControl<TextBlock>("TypeHintTextBlock");
        _widthLabelTextBlock = this.FindControl<TextBlock>("WidthLabelTextBlock");
        _heightLabelTextBlock = this.FindControl<TextBlock>("HeightLabelTextBlock");
        _widthHintTextBlock = this.FindControl<TextBlock>("WidthHintTextBlock");
        _heightHintTextBlock = this.FindControl<TextBlock>("HeightHintTextBlock");
        _fireCellsHintTextBlock = this.FindControl<TextBlock>("FireCellsHintTextBlock");

        _structureSummaryTextBlock = this.FindControl<TextBlock>("StructureSummaryTextBlock");
        _structureDetailTextBlock = this.FindControl<TextBlock>("StructureDetailTextBlock");

        _precipitationBox = this.FindControl<TextBox>("PrecipitationBox");
        _randomSeedBox = this.FindControl<TextBox>("RandomSeedBox");

        _coniferousBox = this.FindControl<TextBox>("ConiferousBox");
        _deciduousBox = this.FindControl<TextBox>("DeciduousBox");
        _mixedBox = this.FindControl<TextBox>("MixedBox");
        _grassBox = this.FindControl<TextBox>("GrassBox");
        _shrubBox = this.FindControl<TextBox>("ShrubBox");
        _waterBox = this.FindControl<TextBox>("WaterBox");
        _bareBox = this.FindControl<TextBox>("BareBox");
    }

    private void AttachEvents()
    {
        var createButton = this.FindControl<Button>("CreateButton");
        var cancelButton = this.FindControl<Button>("CancelButton");

        if (createButton != null)
            createButton.Click += OnCreateClick;

        if (cancelButton != null)
            cancelButton.Click += OnCancelClick;

        if (_widthBox != null)
            _widthBox.PropertyChanged += (_, __) => UpdateStructurePreview();

        if (_heightBox != null)
            _heightBox.PropertyChanged += (_, __) => UpdateStructurePreview();
    }

    private void ApplyDefaults()
    {
        if (_nameBox == null ||
            _widthBox == null ||
            _heightBox == null ||
            _fireCellsBox == null ||
            _moistureMinBox == null ||
            _moistureMaxBox == null ||
            _elevationBox == null ||
            _stepsBox == null ||
            _stepDurationBox == null ||
            _tempBox == null ||
            _humidityBox == null ||
            _windSpeedBox == null ||
            _windDirBox == null)
        {
            return;
        }

        if (_page == AppPage.Grid)
        {
            _nameBox.Text = "Сеточная симуляция";
            _widthBox.Text = "20";
            _heightBox.Text = "20";
            _fireCellsBox.Text = "3";
            _stepsBox.Text = "100";
            _stepDurationBox.Text = "900";
        }
        else if (_mode == GraphCreationMode.RegionCluster)
        {
            _nameBox.Text = "Региональный кластерный граф";
            _widthBox.Text = "20";
            _heightBox.Text = "20";
            _fireCellsBox.Text = "1";
            _stepsBox.Text = "30";
            _stepDurationBox.Text = "1800";
        }
        else
        {
            _nameBox.Text = "Кластерный граф";
            _widthBox.Text = "5";
            _heightBox.Text = "5";
            _fireCellsBox.Text = "1";
            _stepsBox.Text = "30";
            _stepDurationBox.Text = "1800";
        }

        _moistureMinBox.Text = "0.3";
        _moistureMaxBox.Text = "0.7";
        _elevationBox.Text = "50";
        _tempBox.Text = "25";
        _humidityBox.Text = "40";
        _windSpeedBox.Text = "5";
        _windDirBox.SelectedIndex = 1;

        if (_precipitationBox != null)
            _precipitationBox.Text = "0";

        if (_randomSeedBox != null)
            _randomSeedBox.Text = string.Empty;

        if (_coniferousBox != null) _coniferousBox.Text = "30";
        if (_deciduousBox != null) _deciduousBox.Text = "20";
        if (_mixedBox != null) _mixedBox.Text = "25";
        if (_grassBox != null) _grassBox.Text = "15";
        if (_shrubBox != null) _shrubBox.Text = "10";
        if (_waterBox != null) _waterBox.Text = "0";
        if (_bareBox != null) _bareBox.Text = "0";
    }
    private void ApplyModeTexts()
    {
        if (_page == AppPage.Grid)
        {
            if (_typeInfoTextBlock != null)
                _typeInfoTextBlock.Text = "Сеточная симуляция";

            if (_typeHintTextBlock != null)
                _typeHintTextBlock.Text = "Размер задаёт число клеток по горизонтали и вертикали.";

            if (_widthLabelTextBlock != null)
                _widthLabelTextBlock.Text = "Ширина сетки";

            if (_heightLabelTextBlock != null)
                _heightLabelTextBlock.Text = "Высота сетки";

            if (_widthHintTextBlock != null)
                _widthHintTextBlock.Text = "Количество клеток по горизонтали. Минимум 5, максимум 100.";

            if (_heightHintTextBlock != null)
                _heightHintTextBlock.Text = "Количество клеток по вертикали. Минимум 5, максимум 100.";

            if (_fireCellsHintTextBlock != null)
                _fireCellsHintTextBlock.Text = "Минимум 1. Максимум — не более половины клеток сетки.";
        }
        else if (_mode == GraphCreationMode.RegionCluster)
        {
            if (_typeInfoTextBlock != null)
                _typeInfoTextBlock.Text = "Региональный кластерный граф";

            if (_typeHintTextBlock != null)
                _typeHintTextBlock.Text =
                    "Граф, разделённый на регионы с плотными внутренними связями и ограниченными межрегиональными переходами.";

            if (_widthLabelTextBlock != null)
                _widthLabelTextBlock.Text = "Ширина территории";

            if (_heightLabelTextBlock != null)
                _heightLabelTextBlock.Text = "Высота территории";

            if (_widthHintTextBlock != null)
                _widthHintTextBlock.Text =
                    "Минимум 5, максимум 100. Для региональной модели внутри генератора действует нижняя граница 12.";

            if (_heightHintTextBlock != null)
                _heightHintTextBlock.Text =
                    "Минимум 5, максимум 100. Для региональной модели внутри генератора действует нижняя граница 12.";

            if (_fireCellsHintTextBlock != null)
                _fireCellsHintTextBlock.Text = "Обычно 1–3 начальных очага.";
        }
        else
        {
            if (_typeInfoTextBlock != null)
                _typeInfoTextBlock.Text = "Кластерный граф";

            if (_typeHintTextBlock != null)
                _typeHintTextBlock.Text =
                    "Граф с локальной кластеризацией вершин без явного разделения на регионы.";

            if (_widthLabelTextBlock != null)
                _widthLabelTextBlock.Text = "Ширина области";

            if (_heightLabelTextBlock != null)
                _heightLabelTextBlock.Text = "Высота области";

            if (_widthHintTextBlock != null)
                _widthHintTextBlock.Text = "Минимум 5, максимум 100.";

            if (_heightHintTextBlock != null)
                _heightHintTextBlock.Text = "Минимум 5, максимум 100.";

            if (_fireCellsHintTextBlock != null)
                _fireCellsHintTextBlock.Text = "Обычно достаточно 1 начального очага.";
        }
    }

    private void UpdateStructurePreview()
    {
        if (_structureSummaryTextBlock == null || _structureDetailTextBlock == null)
            return;

        int.TryParse(_widthBox?.Text, out var inputWidth);
        int.TryParse(_heightBox?.Text, out var inputHeight);

        inputWidth = Math.Max(1, inputWidth);
        inputHeight = Math.Max(1, inputHeight);

        if (_page == AppPage.Grid)
        {
            int cells = inputWidth * inputHeight;
            _structureSummaryTextBlock.Text = $"Сетка {inputWidth}×{inputHeight} = {cells} клеток.";
            _structureDetailTextBlock.Text = "Каждая клетка отображается как отдельный участок модели.";
            return;
        }

        if (_mode == GraphCreationMode.Clustered)
        {
            int estimatedNodes = Math.Max(1, (inputWidth * inputHeight) / 2);
            int estimatedMinDegree = EstimateClusteredMinDegree(estimatedNodes);
            int estimatedMaxDegree = EstimateClusteredMaxDegree(estimatedNodes);

            _structureSummaryTextBlock.Text =
                $"Область {inputWidth}×{inputHeight} → примерно {estimatedNodes} узлов.";

            _structureDetailTextBlock.Text =
                $"Ожидаемая локальная связность: около {estimatedMinDegree}–{estimatedMaxDegree} связей на узел.";
            return;
        }

        int effectiveWidth = Math.Max(12, inputWidth);
        int effectiveHeight = Math.Max(12, inputHeight);
        int effectiveArea = effectiveWidth * effectiveHeight;

        int regionCount = GetRegionClusterRegionCount(effectiveWidth, effectiveHeight);

        int estimatedMinNodes = (int)Math.Round(effectiveArea * 0.58);
        int estimatedMaxNodes = (int)Math.Round(effectiveArea * 0.78);

        _structureSummaryTextBlock.Text =
            $"Территория {inputWidth}×{inputHeight} → генерация на поле {effectiveWidth}×{effectiveHeight}, регионов: {regionCount}, узлов обычно ≈ {estimatedMinNodes}–{estimatedMaxNodes}.";

        _structureDetailTextBlock.Text =
            "Итоговое число узлов меньше полного заполнения: регионы создаются с разной плотностью, внутри региона связность выше, между регионами — редкие мосты.";
    }

    private int EstimateClusteredMinDegree(int nodeCount)
    {
        if (nodeCount <= 12) return 2;
        if (nodeCount <= 40) return 3;
        if (nodeCount <= 120) return 4;
        return 4;
    }

    private int EstimateClusteredMaxDegree(int nodeCount)
    {
        if (nodeCount <= 20) return 4;
        if (nodeCount <= 80) return 5;
        if (nodeCount <= 250) return 6;
        return 6;
    }

    private int GetRegionClusterRegionCount(int width, int height)
    {
        var area = width * height;

        if (area <= 120) return 4;
        if (area <= 240) return 5;
        if (area <= 420) return 6;
        if (area <= 700) return 7;
        return 8;
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        var errors = new List<string>();

        SimulationName = _nameBox?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(SimulationName))
        {
            SimulationName = $"Симуляция {DateTime.Now:HH:mm:ss}";
        }

        var width = ParseInt(_widthBox?.Text, "Ширина", errors);
        var height = ParseInt(_heightBox?.Text, "Высота", errors);
        var fireCells = ParseInt(_fireCellsBox?.Text, "Количество начальных очагов", errors);
        var steps = ParseInt(_stepsBox?.Text, "Количество шагов", errors);
        var stepDuration = ParseInt(_stepDurationBox?.Text, "Длительность шага", errors);

        var moistureMin = ParseDouble(_moistureMinBox?.Text, "Минимальная влажность", errors);
        var moistureMax = ParseDouble(_moistureMaxBox?.Text, "Максимальная влажность", errors);
        var elevation = ParseDouble(_elevationBox?.Text, "Перепад высот", errors);
        var temperature = ParseDouble(_tempBox?.Text, "Температура", errors);
        var humidity = ParseDouble(_humidityBox?.Text, "Влажность воздуха", errors);
        var windSpeed = ParseDouble(_windSpeedBox?.Text, "Скорость ветра", errors);
        var precipitation = ParseDouble(_precipitationBox?.Text, "Осадки", errors);

        var coniferousPercent = ParseDouble(_coniferousBox?.Text, "Хвойный лес", errors);
        var deciduousPercent = ParseDouble(_deciduousBox?.Text, "Лиственный лес", errors);
        var mixedPercent = ParseDouble(_mixedBox?.Text, "Смешанный лес", errors);
        var grassPercent = ParseDouble(_grassBox?.Text, "Трава", errors);
        var shrubPercent = ParseDouble(_shrubBox?.Text, "Кустарник", errors);
        var waterPercent = ParseDouble(_waterBox?.Text, "Вода", errors);
        var barePercent = ParseDouble(_bareBox?.Text, "Пустая поверхность", errors);

        if (width.HasValue && (width.Value < 5 || width.Value > 100))
            errors.Add("Ширина должна быть в диапазоне от 5 до 100.");

        if (height.HasValue && (height.Value < 5 || height.Value > 100))
            errors.Add("Высота должна быть в диапазоне от 5 до 100.");

        if (width.HasValue && height.HasValue && fireCells.HasValue)
        {
            var maxFireCells = Math.Max(1, (width.Value * height.Value) / 2);
            if (fireCells.Value < 1 || fireCells.Value > maxFireCells)
                errors.Add($"Количество начальных очагов должно быть от 1 до {maxFireCells}.");
        }

        if (moistureMin.HasValue && (moistureMin.Value < 0.0 || moistureMin.Value > 1.0))
            errors.Add("Минимальная влажность должна быть в диапазоне от 0.0 до 1.0.");

        if (moistureMax.HasValue && (moistureMax.Value < 0.0 || moistureMax.Value > 1.0))
            errors.Add("Максимальная влажность должна быть в диапазоне от 0.0 до 1.0.");

        if (moistureMin.HasValue && moistureMax.HasValue && moistureMax.Value <= moistureMin.Value)
            errors.Add("Максимальная влажность должна быть больше минимальной.");

        if (elevation.HasValue && (elevation.Value < 0 || elevation.Value > 500))
            errors.Add("Перепад высот должен быть в диапазоне от 0 до 500.");

        if (steps.HasValue && (steps.Value < 1 || steps.Value > 500))
            errors.Add("Количество шагов должно быть в диапазоне от 1 до 500.");

        if (stepDuration.HasValue && (stepDuration.Value < 1 || stepDuration.Value > 5400))
            errors.Add("Длительность шага должна быть в диапазоне от 1 до 5400 секунд.");

        if (temperature.HasValue && (temperature.Value < -50 || temperature.Value > 60))
            errors.Add("Температура должна быть в диапазоне от -50 до 60 °C.");

        if (humidity.HasValue && (humidity.Value < 0 || humidity.Value > 100))
            errors.Add("Влажность воздуха должна быть в диапазоне от 0 до 100 %.");

        if (windSpeed.HasValue && (windSpeed.Value < 0 || windSpeed.Value > 30))
            errors.Add("Скорость ветра должна быть в диапазоне от 0 до 30 м/с.");

        var directionIndex = _windDirBox?.SelectedIndex ?? 1;
        var directions = new[] { 0, 45, 90, 135, 180, 225, 270, 315, 360 };
        WindDirection = directions[Math.Clamp(directionIndex, 0, directions.Length - 1)];

        if (WindDirection < 0 || WindDirection > 360)
            errors.Add("Направление ветра должно быть в диапазоне от 0 до 360 градусов.");

        if (precipitation.HasValue && (precipitation.Value < 0 || precipitation.Value > 100))
            errors.Add("Осадки должны быть в диапазоне от 0 до 100 мм/ч.");

        int? randomSeed = null;
        if (!string.IsNullOrWhiteSpace(_randomSeedBox?.Text))
        {
            if (int.TryParse(_randomSeedBox.Text, out var parsedSeed))
                randomSeed = parsedSeed;
            else
                errors.Add("Поле «Random seed» должно быть целым числом.");
        }

        var vegetationPercents = new[]
        {
        ("Хвойный лес", coniferousPercent),
        ("Лиственный лес", deciduousPercent),
        ("Смешанный лес", mixedPercent),
        ("Трава", grassPercent),
        ("Кустарник", shrubPercent),
        ("Вода", waterPercent),
        ("Пустая поверхность", barePercent)
    };

        foreach (var (name, value) in vegetationPercents)
        {
            if (value.HasValue && (value.Value < 0 || value.Value > 100))
                errors.Add($"Поле «{name}» должно быть в диапазоне от 0 до 100.");
        }

        if (coniferousPercent.HasValue &&
            deciduousPercent.HasValue &&
            mixedPercent.HasValue &&
            grassPercent.HasValue &&
            shrubPercent.HasValue &&
            waterPercent.HasValue &&
            barePercent.HasValue)
        {
            var totalVegetationPercent =
                coniferousPercent.Value +
                deciduousPercent.Value +
                mixedPercent.Value +
                grassPercent.Value +
                shrubPercent.Value +
                waterPercent.Value +
                barePercent.Value;

            if (Math.Abs(totalVegetationPercent - 100.0) > 0.001)
                errors.Add("Сумма процентов растительности и поверхности должна быть равна 100.");
        }

        if (errors.Count > 0)
        {
            ShowErrors(errors);
            return;
        }

        ClearErrors();

        GridWidth = width!.Value;
        GridHeight = height!.Value;
        InitialFireCells = fireCells!.Value;
        MoistureMin = moistureMin!.Value;
        MoistureMax = moistureMax!.Value;
        ElevationVariation = elevation!.Value;
        SimulationSteps = steps!.Value;
        StepDurationSeconds = stepDuration!.Value;
        Temperature = temperature!.Value;
        Humidity = humidity!.Value;
        WindSpeed = windSpeed!.Value;
        Precipitation = precipitation!.Value;
        RandomSeed = randomSeed;

        VegetationDistributions = new List<(int VegetationType, double Probability)>
    {
        ((int)3, coniferousPercent!.Value / 100.0),
        ((int)2, deciduousPercent!.Value / 100.0),
        ((int)4, mixedPercent!.Value / 100.0),
        ((int)0, grassPercent!.Value / 100.0),
        ((int)1, shrubPercent!.Value / 100.0),
        ((int)5, waterPercent!.Value / 100.0),
        ((int)6, barePercent!.Value / 100.0)
    };

        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private int? ParseInt(string? value, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Поле «{fieldName}» не заполнено.");
            return null;
        }

        if (!int.TryParse(value, out var result))
        {
            errors.Add($"Поле «{fieldName}» должно быть целым числом.");
            return null;
        }

        return result;
    }

    private double? ParseDouble(string? value, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Поле «{fieldName}» не заполнено.");
            return null;
        }

        var normalized = value.Replace(',', '.');

        if (!double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            errors.Add($"Поле «{fieldName}» должно быть числом.");
            return null;
        }

        return result;
    }

    private void ShowErrors(List<string> errors)
    {
        if (_errorTextBlock == null)
            return;

        _errorTextBlock.Text = "Исправьте ошибки:\n• " + string.Join("\n• ", errors);
    }

    private void ClearErrors()
    {
        if (_errorTextBlock == null)
            return;

        _errorTextBlock.Text = string.Empty;
    }
}