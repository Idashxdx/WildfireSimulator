using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WildfireSimulator.Client.Models;
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

    public MapCreationMode SelectedMapCreationMode { get; private set; } = MapCreationMode.Random;
    public MapScenarioType? SelectedScenarioType { get; private set; }
    public double MapNoiseStrength { get; private set; } = 0.08;
    public double MapDrynessFactor { get; private set; } = 1.0;
    public double ReliefStrengthFactor { get; private set; } = 1.0;
    public double FuelDensityFactor { get; private set; } = 1.0;
    public List<MapRegionObjectDto> MapRegionObjects { get; private set; } = new();

    public List<(int VegetationType, double Probability)> VegetationDistributions { get; private set; } = new();

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

    private ComboBox? _mapCreationModeBox;
    private ComboBox? _scenarioTypeBox;
    private TextBox? _mapNoiseBox;
    private TextBox? _mapDrynessBox;
    private TextBox? _reliefStrengthBox;
    private TextBox? _fuelDensityBox;
    private TextBlock? _mapModeDescriptionTextBlock;
    private TextBlock? _scenarioDescriptionTextBlock;
    private TextBlock? _semiManualDescriptionTextBlock;

    private StackPanel? _scenarioPanel;
    private StackPanel? _semiManualPanel;

    private TextBox? _coniferousBox;
    private TextBox? _deciduousBox;
    private TextBox? _mixedBox;
    private TextBox? _grassBox;
    private TextBox? _shrubBox;
    private TextBox? _waterBox;
    private TextBox? _bareBox;
    private Button? _openMapEditorButton;
    private TextBlock? _mapEditorSummaryTextBlock;

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
        UpdateMapModeUi();
        UpdateScenarioDescription();
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

        _mapCreationModeBox = this.FindControl<ComboBox>("MapCreationModeBox");
        _scenarioTypeBox = this.FindControl<ComboBox>("ScenarioTypeBox");
        _mapNoiseBox = this.FindControl<TextBox>("MapNoiseBox");
        _mapDrynessBox = this.FindControl<TextBox>("MapDrynessBox");
        _reliefStrengthBox = this.FindControl<TextBox>("ReliefStrengthBox");
        _fuelDensityBox = this.FindControl<TextBox>("FuelDensityBox");

        _mapModeDescriptionTextBlock = this.FindControl<TextBlock>("MapModeDescriptionTextBlock");
        _scenarioDescriptionTextBlock = this.FindControl<TextBlock>("ScenarioDescriptionTextBlock");
        _semiManualDescriptionTextBlock = this.FindControl<TextBlock>("SemiManualDescriptionTextBlock");
        _mapEditorSummaryTextBlock = this.FindControl<TextBlock>("MapEditorSummaryTextBlock");

        _scenarioPanel = this.FindControl<StackPanel>("ScenarioPanel");
        _semiManualPanel = this.FindControl<StackPanel>("SemiManualPanel");

        _coniferousBox = this.FindControl<TextBox>("ConiferousBox");
        _deciduousBox = this.FindControl<TextBox>("DeciduousBox");
        _mixedBox = this.FindControl<TextBox>("MixedBox");
        _grassBox = this.FindControl<TextBox>("GrassBox");
        _shrubBox = this.FindControl<TextBox>("ShrubBox");
        _waterBox = this.FindControl<TextBox>("WaterBox");
        _bareBox = this.FindControl<TextBox>("BareBox");

        _openMapEditorButton = this.FindControl<Button>("OpenMapEditorButton");
    }

    private void AttachEvents()
    {
        if (_mapCreationModeBox != null)
        {
            _mapCreationModeBox.SelectionChanged += (_, _) =>
            {
                UpdateMapModeUi();
                UpdateStructurePreview();
                ClearErrors();
            };
        }

        if (_scenarioTypeBox != null)
        {
            _scenarioTypeBox.SelectionChanged += (_, _) =>
            {
                UpdateScenarioDescription();
                UpdateStructurePreview();
                ClearErrors();
            };
        }

        if (_openMapEditorButton != null)
            _openMapEditorButton.Click += async (_, _) => await OpenMapEditorAsync();

        var createButton = this.FindControl<Button>("CreateButton");
        if (createButton != null)
            createButton.Click += OnCreateClicked;

        var cancelButton = this.FindControl<Button>("CancelButton");
        if (cancelButton != null)
            cancelButton.Click += (_, _) => Close(false);

        if (_widthBox != null)
            _widthBox.LostFocus += (_, _) => UpdateStructurePreview();

        if (_heightBox != null)
            _heightBox.LostFocus += (_, _) => UpdateStructurePreview();

        if (_fireCellsBox != null)
            _fireCellsBox.LostFocus += (_, _) => UpdateStructurePreview();

        if (_mapDrynessBox != null)
            _mapDrynessBox.LostFocus += (_, _) => UpdateStructurePreview();

        if (_reliefStrengthBox != null)
            _reliefStrengthBox.LostFocus += (_, _) => UpdateStructurePreview();

        if (_fuelDensityBox != null)
            _fuelDensityBox.LostFocus += (_, _) => UpdateStructurePreview();
    }

    private void OnPresetClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var preset = button.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(preset))
            return;

        ClearErrors();
        MapRegionObjects = new List<MapRegionObjectDto>();

        switch (preset)
        {
            case "dry-coniferous":
                if (_nameBox != null) _nameBox.Text = "Демо: Сухой хвойный массив + сильный ветер";
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = 1;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 1;

                if (_widthBox != null) _widthBox.Text = "24";
                if (_heightBox != null) _heightBox.Text = "24";
                if (_fireCellsBox != null) _fireCellsBox.Text = "2";

                if (_moistureMinBox != null) _moistureMinBox.Text = "0.10";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.24";
                if (_elevationBox != null) _elevationBox.Text = "70";

                if (_stepsBox != null) _stepsBox.Text = "90";
                if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                if (_tempBox != null) _tempBox.Text = "31";
                if (_humidityBox != null) _humidityBox.Text = "24";
                if (_windSpeedBox != null) _windSpeedBox.Text = "11";
                if (_windDirBox != null) _windDirBox.SelectedIndex = 2;
                if (_precipitationBox != null) _precipitationBox.Text = "0";

                if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.10";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.30";
                if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.05";
                if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.25";
                if (_randomSeedBox != null) _randomSeedBox.Text = "101";

                SetVegetationDistributionTexts(0.55, 0.08, 0.18, 0.07, 0.07, 0.03, 0.02);
                break;

            case "river":
                if (_nameBox != null) _nameBox.Text = "Демо: Лес с рекой как барьер";
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = 1;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 2;

                if (_widthBox != null) _widthBox.Text = "26";
                if (_heightBox != null) _heightBox.Text = "20";
                if (_fireCellsBox != null) _fireCellsBox.Text = "2";

                if (_moistureMinBox != null) _moistureMinBox.Text = "0.26";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.62";
                if (_elevationBox != null) _elevationBox.Text = "55";

                if (_stepsBox != null) _stepsBox.Text = "100";
                if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                if (_tempBox != null) _tempBox.Text = "26";
                if (_humidityBox != null) _humidityBox.Text = "42";
                if (_windSpeedBox != null) _windSpeedBox.Text = "6";
                if (_windDirBox != null) _windDirBox.SelectedIndex = 2;
                if (_precipitationBox != null) _precipitationBox.Text = "0";

                if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.08";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.00";
                if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.00";
                if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.00";
                if (_randomSeedBox != null) _randomSeedBox.Text = "202";

                SetVegetationDistributionTexts(0.20, 0.22, 0.28, 0.10, 0.10, 0.06, 0.04);
                break;

            case "wet":
                if (_nameBox != null) _nameBox.Text = "Демо: Влажный лес после дождя";
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = 1;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 6;

                if (_widthBox != null) _widthBox.Text = "22";
                if (_heightBox != null) _heightBox.Text = "22";
                if (_fireCellsBox != null) _fireCellsBox.Text = "1";

                if (_moistureMinBox != null) _moistureMinBox.Text = "0.48";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.88";
                if (_elevationBox != null) _elevationBox.Text = "45";

                if (_stepsBox != null) _stepsBox.Text = "80";
                if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                if (_tempBox != null) _tempBox.Text = "20";
                if (_humidityBox != null) _humidityBox.Text = "78";
                if (_windSpeedBox != null) _windSpeedBox.Text = "3";
                if (_windDirBox != null) _windDirBox.SelectedIndex = 1;
                if (_precipitationBox != null) _precipitationBox.Text = "2.5";

                if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.06";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "0.80";
                if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "0.95";
                if (_fuelDensityBox != null) _fuelDensityBox.Text = "0.95";
                if (_randomSeedBox != null) _randomSeedBox.Text = "303";

                SetVegetationDistributionTexts(0.18, 0.24, 0.28, 0.08, 0.10, 0.07, 0.05);
                break;

            case "firebreak":
                if (_nameBox != null) _nameBox.Text = "Демо: Лес с просекой";
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = 1;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 4;

                if (_widthBox != null) _widthBox.Text = "24";
                if (_heightBox != null) _heightBox.Text = "20";
                if (_fireCellsBox != null) _fireCellsBox.Text = "2";

                if (_moistureMinBox != null) _moistureMinBox.Text = "0.24";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.58";
                if (_elevationBox != null) _elevationBox.Text = "50";

                if (_stepsBox != null) _stepsBox.Text = "90";
                if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                if (_tempBox != null) _tempBox.Text = "28";
                if (_humidityBox != null) _humidityBox.Text = "36";
                if (_windSpeedBox != null) _windSpeedBox.Text = "7";
                if (_windDirBox != null) _windDirBox.SelectedIndex = 2;
                if (_precipitationBox != null) _precipitationBox.Text = "0";

                if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.08";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.05";
                if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.00";
                if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.05";
                if (_randomSeedBox != null) _randomSeedBox.Text = "404";

                SetVegetationDistributionTexts(0.22, 0.20, 0.24, 0.12, 0.10, 0.04, 0.08);
                break;

            case "hills":
                if (_nameBox != null) _nameBox.Text = "Демо: Холмистая местность";
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = 1;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 5;

                if (_widthBox != null) _widthBox.Text = "24";
                if (_heightBox != null) _heightBox.Text = "24";
                if (_fireCellsBox != null) _fireCellsBox.Text = "2";

                if (_moistureMinBox != null) _moistureMinBox.Text = "0.22";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.60";
                if (_elevationBox != null) _elevationBox.Text = "110";

                if (_stepsBox != null) _stepsBox.Text = "100";
                if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                if (_tempBox != null) _tempBox.Text = "27";
                if (_humidityBox != null) _humidityBox.Text = "38";
                if (_windSpeedBox != null) _windSpeedBox.Text = "6";
                if (_windDirBox != null) _windDirBox.SelectedIndex = 3;
                if (_precipitationBox != null) _precipitationBox.Text = "0";

                if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.10";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.00";
                if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.35";
                if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.00";
                if (_randomSeedBox != null) _randomSeedBox.Text = "505";

                SetVegetationDistributionTexts(0.22, 0.18, 0.24, 0.14, 0.12, 0.05, 0.05);
                break;

            default:
                return;
        }

        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateMapEditorSummary();
        UpdateStructurePreview();
    }

    private void SetVegetationDistributionTexts(
        double coniferous,
        double deciduous,
        double mixed,
        double grass,
        double shrub,
        double water,
        double bare)
    {
        if (_coniferousBox != null) _coniferousBox.Text = coniferous.ToString("0.00", CultureInfo.InvariantCulture);
        if (_deciduousBox != null) _deciduousBox.Text = deciduous.ToString("0.00", CultureInfo.InvariantCulture);
        if (_mixedBox != null) _mixedBox.Text = mixed.ToString("0.00", CultureInfo.InvariantCulture);
        if (_grassBox != null) _grassBox.Text = grass.ToString("0.00", CultureInfo.InvariantCulture);
        if (_shrubBox != null) _shrubBox.Text = shrub.ToString("0.00", CultureInfo.InvariantCulture);
        if (_waterBox != null) _waterBox.Text = water.ToString("0.00", CultureInfo.InvariantCulture);
        if (_bareBox != null) _bareBox.Text = bare.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private void OnCreateClicked(object? sender, RoutedEventArgs e)
    {
        ClearErrors();

        var name = (_nameBox?.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Введите название симуляции.");
            return;
        }

        int width = ParseInt(_widthBox?.Text, 0);
        int height = ParseInt(_heightBox?.Text, 0);
        int initialFireCells = ParseInt(_fireCellsBox?.Text, 0);
        int simulationSteps = ParseInt(_stepsBox?.Text, 0);
        int stepDurationSeconds = ParseInt(_stepDurationBox?.Text, 0);

        double moistureMin = ParseDouble(_moistureMinBox?.Text, -1);
        double moistureMax = ParseDouble(_moistureMaxBox?.Text, -1);
        double elevationVariation = ParseDouble(_elevationBox?.Text, -1);
        double temperature = ParseDouble(_tempBox?.Text, double.NaN);
        double humidity = ParseDouble(_humidityBox?.Text, double.NaN);
        double windSpeed = ParseDouble(_windSpeedBox?.Text, double.NaN);
        double precipitation = ParseDouble(_precipitationBox?.Text, 0.0);

        double mapNoiseStrength = ParseDouble(_mapNoiseBox?.Text, 0.08);
        double mapDrynessFactor = ParseDouble(_mapDrynessBox?.Text, 1.0);
        double reliefStrengthFactor = ParseDouble(_reliefStrengthBox?.Text, 1.0);
        double fuelDensityFactor = ParseDouble(_fuelDensityBox?.Text, 1.0);

        if (_page == AppPage.Grid)
        {
            if (width < 5 || height < 5)
            {
                ShowError("Для сеточной модели размеры карты должны быть не меньше 5×5.");
                return;
            }
        }
        else
        {
            if (width < 3 || height < 3)
            {
                ShowError("Размеры модели должны быть не меньше 3×3.");
                return;
            }
        }

        if (initialFireCells <= 0)
        {
            ShowError("Количество стартовых очагов должно быть больше 0.");
            return;
        }

        if (simulationSteps <= 0)
        {
            ShowError("Количество шагов моделирования должно быть больше 0.");
            return;
        }

        if (stepDurationSeconds <= 0)
        {
            ShowError("Длительность шага должна быть больше 0 секунд.");
            return;
        }

        if (moistureMin < 0 || moistureMin > 1 || moistureMax < 0 || moistureMax > 1)
        {
            ShowError("Влажность должна быть в диапазоне от 0 до 1.");
            return;
        }

        if (moistureMax < moistureMin)
        {
            ShowError("Максимальная влажность не может быть меньше минимальной.");
            return;
        }

        if (elevationVariation < 0)
        {
            ShowError("Перепад высот не может быть отрицательным.");
            return;
        }

        if (double.IsNaN(temperature) || temperature < -50 || temperature > 60)
        {
            ShowError("Температура должна быть в диапазоне от -50 до 60 °C.");
            return;
        }

        if (double.IsNaN(humidity) || humidity < 0 || humidity > 100)
        {
            ShowError("Влажность воздуха должна быть в диапазоне от 0 до 100%.");
            return;
        }

        if (double.IsNaN(windSpeed) || windSpeed < 0)
        {
            ShowError("Скорость ветра не может быть отрицательной.");
            return;
        }

        if (precipitation < 0)
        {
            ShowError("Осадки не могут быть отрицательными.");
            return;
        }

        mapNoiseStrength = Math.Clamp(mapNoiseStrength, 0.0, 0.30);
        mapDrynessFactor = Math.Clamp(mapDrynessFactor, 0.5, 1.5);
        reliefStrengthFactor = Math.Clamp(reliefStrengthFactor, 0.5, 1.5);
        fuelDensityFactor = Math.Clamp(fuelDensityFactor, 0.5, 1.5);

        int? randomSeed = null;
        var seedText = (_randomSeedBox?.Text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(seedText))
        {
            if (!int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeed))
            {
                ShowError("Seed должен быть целым числом.");
                return;
            }

            randomSeed = parsedSeed;
        }

        var vegetationDistributions = ReadVegetationDistributions();
        if (vegetationDistributions.Count == 0)
        {
            ShowError("Нужно задать хотя бы одно ненулевое распределение растительности.");
            return;
        }

        double totalProbability = vegetationDistributions.Sum(x => x.Probability);
        if (totalProbability <= 0.000001)
        {
            ShowError("Сумма вероятностей растительности должна быть больше 0.");
            return;
        }

        VegetationDistributions = vegetationDistributions
            .Select(x => (x.VegetationType, x.Probability / totalProbability))
            .ToList();

        SimulationName = name;
        GridWidth = width;
        GridHeight = height;
        InitialFireCells = initialFireCells;
        MoistureMin = moistureMin;
        MoistureMax = moistureMax;
        ElevationVariation = elevationVariation;
        SimulationSteps = simulationSteps;
        StepDurationSeconds = stepDurationSeconds;
        Temperature = temperature;
        Humidity = humidity;
        WindSpeed = windSpeed;
        Precipitation = precipitation;
        RandomSeed = randomSeed;

        MapNoiseStrength = mapNoiseStrength;
        MapDrynessFactor = mapDrynessFactor;
        ReliefStrengthFactor = reliefStrengthFactor;
        FuelDensityFactor = fuelDensityFactor;

        SelectedMapCreationMode = _mapCreationModeBox?.SelectedIndex switch
        {
            1 => MapCreationMode.Scenario,
            2 => MapCreationMode.SemiManual,
            _ => MapCreationMode.Random
        };

        SelectedScenarioType = SelectedMapCreationMode == MapCreationMode.Scenario
            ? _scenarioTypeBox?.SelectedIndex switch
            {
                1 => MapScenarioType.DryConiferousMassif,
                2 => MapScenarioType.ForestWithRiver,
                3 => MapScenarioType.ForestWithLake,
                4 => MapScenarioType.ForestWithFirebreak,
                5 => MapScenarioType.HillyTerrain,
                6 => MapScenarioType.WetForestAfterRain,
                _ => MapScenarioType.MixedForest
            }
            : null;

        WindDirection = _windDirBox?.SelectedIndex switch
        {
            0 => 0,
            1 => 45,
            2 => 90,
            3 => 135,
            4 => 180,
            5 => 225,
            6 => 270,
            7 => 315,
            _ => 45
        };

        if (SelectedMapCreationMode == MapCreationMode.SemiManual && MapRegionObjects.Count == 0)
        {
            ShowError("Для полуручного режима нужно добавить хотя бы один объект карты.");
            return;
        }

        Close(true);
    }

    private async Task OpenMapEditorAsync()
    {
        int width = ParseInt(_widthBox?.Text, 20);
        int height = ParseInt(_heightBox?.Text, 20);

        if (width < 5 || height < 5)
        {
            ShowError("Сначала задайте корректные размеры карты.");
            return;
        }

        var editor = new MapEditorDialog(width, height, MapRegionObjects);
        var result = await editor.ShowDialog<bool>(this);

        if (!result)
            return;

        MapRegionObjects = editor.EditedObjects
            .Select(x => new MapRegionObjectDto
            {
                Id = x.Id,
                ObjectType = x.ObjectType,
                Shape = x.Shape,
                StartX = x.StartX,
                StartY = x.StartY,
                Width = x.Width,
                Height = x.Height,
                Strength = x.Strength,
                Priority = x.Priority
            })
            .OrderBy(x => x.Priority)
            .ToList();

        UpdateMapEditorSummary();
        UpdateMapModeUi();
        UpdateStructurePreview();
        ClearErrors();
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
            _nameBox.Text = "Региональный граф";
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

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectedIndex = 0;

        if (_scenarioTypeBox != null)
            _scenarioTypeBox.SelectedIndex = 0;

        if (_mapNoiseBox != null)
            _mapNoiseBox.Text = "0.08";

        if (_mapDrynessBox != null)
            _mapDrynessBox.Text = "1.0";

        if (_reliefStrengthBox != null)
            _reliefStrengthBox.Text = "1.0";

        if (_fuelDensityBox != null)
            _fuelDensityBox.Text = "1.0";

        if (_coniferousBox != null) _coniferousBox.Text = "0.25";
        if (_deciduousBox != null) _deciduousBox.Text = "0.20";
        if (_mixedBox != null) _mixedBox.Text = "0.20";
        if (_grassBox != null) _grassBox.Text = "0.15";
        if (_shrubBox != null) _shrubBox.Text = "0.10";
        if (_waterBox != null) _waterBox.Text = "0.05";
        if (_bareBox != null) _bareBox.Text = "0.05";

        MapRegionObjects = new List<MapRegionObjectDto>();
        UpdateMapEditorSummary();
    }

    private void ApplyModeTexts()
    {
        if (_typeInfoTextBlock == null ||
            _typeHintTextBlock == null ||
            _widthLabelTextBlock == null ||
            _heightLabelTextBlock == null ||
            _widthHintTextBlock == null ||
            _heightHintTextBlock == null ||
            _fireCellsHintTextBlock == null)
        {
            return;
        }

        if (_page == AppPage.Grid)
        {
            _typeInfoTextBlock.Text = "Сеточная симуляция";
            _typeHintTextBlock.Text = "Карта задаётся на регулярной сетке. Это лучший режим для сценариев и полуручного построения.";
            _widthLabelTextBlock.Text = "Ширина";
            _heightLabelTextBlock.Text = "Высота";
            _widthHintTextBlock.Text = "Минимум 5, максимум 100.";
            _heightHintTextBlock.Text = "Минимум 5, максимум 100.";
            _fireCellsHintTextBlock.Text = "Минимум 1.";
        }
        else if (_mode == GraphCreationMode.RegionCluster)
        {
            _typeInfoTextBlock.Text = "Региональный граф";
            _typeHintTextBlock.Text = "Территория делится на регионы с плотными внутренними связями.";
            _widthLabelTextBlock.Text = "Ширина территории";
            _heightLabelTextBlock.Text = "Высота территории";
            _widthHintTextBlock.Text = "Используется для построения регионов.";
            _heightHintTextBlock.Text = "Используется для построения регионов.";
            _fireCellsHintTextBlock.Text = "Обычно достаточно 1 очага.";
        }
        else
        {
            _typeInfoTextBlock.Text = "Кластерный граф";
            _typeHintTextBlock.Text = "Будет построен граф с локально связанными кластерами.";
            _widthLabelTextBlock.Text = "Ширина области";
            _heightLabelTextBlock.Text = "Высота области";
            _widthHintTextBlock.Text = "Влияет на геометрию кластеров.";
            _heightHintTextBlock.Text = "Влияет на геометрию кластеров.";
            _fireCellsHintTextBlock.Text = "Обычно достаточно 1 очага.";
        }
    }

    private void UpdateMapModeUi()
    {
        SelectedMapCreationMode = _mapCreationModeBox?.SelectedIndex switch
        {
            1 => MapCreationMode.Scenario,
            2 => MapCreationMode.SemiManual,
            _ => MapCreationMode.Random
        };

        if (_scenarioPanel != null)
            _scenarioPanel.IsVisible = SelectedMapCreationMode == MapCreationMode.Scenario;

        if (_semiManualPanel != null)
            _semiManualPanel.IsVisible = SelectedMapCreationMode == MapCreationMode.SemiManual;

        if (_mapModeDescriptionTextBlock != null)
        {
            _mapModeDescriptionTextBlock.Text = SelectedMapCreationMode switch
            {
                MapCreationMode.Random =>
                    "Карта создаётся автоматически по распределениям растительности и общим параметрам. Подходит для быстрого запуска экспериментов и серии сравнений.",

                MapCreationMode.Scenario =>
                    "Карта создаётся по готовому демонстрационному сценарию. Сценарий заранее задаёт характер ландшафта, влажности, рельефа и барьеров.",

                MapCreationMode.SemiManual =>
                    "Вы сами задаёте крупные области территории: лес, воду, просеки, влажные и сухие зоны, холмы и низины. Режим подходит для демонстрации влияния особенностей местности на пожар.",

                _ => "Карта будет создана автоматически."
            };
        }

        if (_semiManualDescriptionTextBlock != null)
        {
            _semiManualDescriptionTextBlock.Text = MapRegionObjects.Count == 0
                ? "Объекты карты ещё не добавлены. Откройте редактор и создайте области вручную."
                : BuildMapObjectsDetailedSummary();
        }

        UpdateScenarioDescription();
        UpdateMapEditorSummary();
    }

    private string BuildMapObjectsDetailedSummary()
    {
        if (MapRegionObjects == null || MapRegionObjects.Count == 0)
            return "Объекты карты не добавлены.";

        var grouped = MapRegionObjects
            .GroupBy(x => GetMapObjectTypeName(x.ObjectType))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();

        int rectangles = MapRegionObjects.Count(x => x.Shape == MapObjectShape.Rectangle);
        int ellipses = MapRegionObjects.Count(x => x.Shape == MapObjectShape.Ellipse);

        return
            $"Добавлено объектов: {MapRegionObjects.Count}.{Environment.NewLine}" +
            $"Формы: прямоугольники — {rectangles}, эллипсы — {ellipses}.{Environment.NewLine}" +
            $"Состав: {string.Join(" • ", grouped)}";
    }

    private string GetMapObjectTypeName(MapObjectType type)
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

    private void UpdateMapEditorSummary()
    {
        if (_mapEditorSummaryTextBlock == null)
            return;

        if (SelectedMapCreationMode != MapCreationMode.SemiManual)
        {
            _mapEditorSummaryTextBlock.Text = string.Empty;
            return;
        }

        if (_page != AppPage.Grid)
        {
            _mapEditorSummaryTextBlock.Text = "Редактор доступен для сеточного режима.";
            return;
        }

        _mapEditorSummaryTextBlock.Text = MapRegionObjects.Count == 0
            ? "Пока не добавлено ни одной области."
            : BuildMapObjectsDetailedSummary();
    }

    private void UpdateScenarioDescription()
    {
        SelectedScenarioType = _scenarioTypeBox?.SelectedIndex switch
        {
            1 => MapScenarioType.DryConiferousMassif,
            2 => MapScenarioType.ForestWithRiver,
            3 => MapScenarioType.ForestWithLake,
            4 => MapScenarioType.ForestWithFirebreak,
            5 => MapScenarioType.HillyTerrain,
            6 => MapScenarioType.WetForestAfterRain,
            _ => MapScenarioType.MixedForest
        };

        if (_scenarioDescriptionTextBlock == null)
            return;

        _scenarioDescriptionTextBlock.Text = SelectedScenarioType switch
        {
            MapScenarioType.MixedForest =>
                "Сбалансированный лесной ландшафт с несколькими типами растительности. Подходит как базовый сценарий для сравнения с более выраженными условиями.",

            MapScenarioType.DryConiferousMassif =>
                "Преобладает сухой хвойный лес с пониженной влажностью. Ожидается быстрое и интенсивное распространение огня.",

            MapScenarioType.ForestWithRiver =>
                "Через лес проходит река, которая создаёт естественный барьер. Сценарий подходит для демонстрации сдерживания распространения пожара водой.",

            MapScenarioType.ForestWithLake =>
                "На карте расположен водоём с влажной прибрежной зоной. Позволяет показать локальное замедление пожара и изменение фронта огня.",

            MapScenarioType.ForestWithFirebreak =>
                "На территории сформирована просека или противопожарная полоса. Сценарий хорошо показывает влияние искусственных барьеров.",

            MapScenarioType.HillyTerrain =>
                "Рельеф выражен сильнее обычного, присутствуют холмы и перепады высот. Подходит для демонстрации влияния уклона на распространение огня.",

            MapScenarioType.WetForestAfterRain =>
                "Лес после осадков: повышенная влажность, локально более сырые участки. Ожидается более медленное распространение и меньшее число новых возгораний.",

            _ =>
                "Выберите сценарий карты."
        };
    }

    private void UpdateStructurePreview()
    {
        int width = ParseInt(_widthBox?.Text, 20);
        int height = ParseInt(_heightBox?.Text, 20);
        int fireCells = ParseInt(_fireCellsBox?.Text, 3);

        double dryness = ParseDouble(_mapDrynessBox?.Text, 1.0);
        double relief = ParseDouble(_reliefStrengthBox?.Text, 1.0);
        double fuel = ParseDouble(_fuelDensityBox?.Text, 1.0);

        if (_structureSummaryTextBlock != null)
        {
            var graphName = _page == AppPage.Grid
                ? "Сетка"
                : _mode == GraphCreationMode.RegionCluster
                    ? "Региональный граф"
                    : "Кластерный граф";

            _structureSummaryTextBlock.Text = $"{graphName} • {width}×{height} • стартовых очагов: {fireCells}";
        }

        if (_structureDetailTextBlock != null)
        {
            var modeText = SelectedMapCreationMode switch
            {
                MapCreationMode.Random => "случайная генерация",
                MapCreationMode.Scenario => "готовый сценарий",
                MapCreationMode.SemiManual => "полуручное создание",
                _ => "случайная генерация"
            };

            var scenarioText = SelectedMapCreationMode == MapCreationMode.Scenario
                ? SelectedScenarioType switch
                {
                    MapScenarioType.MixedForest => "смешанный лес",
                    MapScenarioType.DryConiferousMassif => "сухой хвойный массив",
                    MapScenarioType.ForestWithRiver => "лес с рекой",
                    MapScenarioType.ForestWithLake => "лес с озером",
                    MapScenarioType.ForestWithFirebreak => "лес с просекой",
                    MapScenarioType.HillyTerrain => "холмистая местность",
                    MapScenarioType.WetForestAfterRain => "влажный лес после дождя",
                    _ => "смешанный лес"
                }
                : null;

            var semiManualPart = SelectedMapCreationMode == MapCreationMode.SemiManual
                ? $"объектов на карте: {MapRegionObjects.Count}"
                : null;

            var parts = new List<string>
            {
                $"режим: {modeText}",
                string.IsNullOrWhiteSpace(scenarioText) ? string.Empty : $"сценарий: {scenarioText}",
                semiManualPart ?? string.Empty,
                $"сухость: {dryness:F2}",
                $"рельеф: {relief:F2}",
                $"горючий покров: {fuel:F2}"
            };

            _structureDetailTextBlock.Text = string.Join(" • ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }

    private List<(int VegetationType, double Probability)> ReadVegetationDistributions()
    {
        var values = new List<(int VegetationType, double Probability)>
        {
            ((int)VegetationType.Coniferous, ParseDouble(_coniferousBox?.Text, 0.0)),
            ((int)VegetationType.Deciduous, ParseDouble(_deciduousBox?.Text, 0.0)),
            ((int)VegetationType.Mixed, ParseDouble(_mixedBox?.Text, 0.0)),
            ((int)VegetationType.Grass, ParseDouble(_grassBox?.Text, 0.0)),
            ((int)VegetationType.Shrub, ParseDouble(_shrubBox?.Text, 0.0)),
            ((int)VegetationType.Water, ParseDouble(_waterBox?.Text, 0.0)),
            ((int)VegetationType.Bare, ParseDouble(_bareBox?.Text, 0.0))
        };

        return values
            .Where(x => x.Probability > 0.0)
            .ToList();
    }

    private static int ParseInt(string? text, int fallback)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return value;

        return fallback;
    }

    private static double ParseDouble(string? text, double fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        var normalized = text.Replace(',', '.');

        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return fallback;
    }

    private void ClearErrors()
    {
        if (_errorTextBlock == null)
            return;

        _errorTextBlock.Text = string.Empty;

        if (_errorTextBlock.Parent is Border border)
            border.IsVisible = false;
    }

    private void ShowError(string message)
    {
        if (_errorTextBlock == null)
            return;

        _errorTextBlock.Text = message;

        if (_errorTextBlock.Parent is Border border)
            border.IsVisible = true;
    }
}