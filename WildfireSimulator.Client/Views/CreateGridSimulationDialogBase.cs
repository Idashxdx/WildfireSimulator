using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WildfireSimulator.Client.Models;

namespace WildfireSimulator.Client.Views;

public partial class CreateGridSimulationDialogBase : Window
{
    public string SimulationName { get; protected set; } = string.Empty;
    public int GridWidth { get; protected set; } = 20;
    public int GridHeight { get; protected set; } = 20;
    public int InitialFireCells { get; protected set; } = 3;
    public double MoistureMin { get; protected set; } = 0.3;
    public double MoistureMax { get; protected set; } = 0.7;
    public double ElevationVariation { get; protected set; } = 50.0;
    public int SimulationSteps { get; protected set; } = 100;
    public int StepDurationSeconds { get; protected set; } = 900;
    public double Temperature { get; protected set; } = 25.0;
    public double Humidity { get; protected set; } = 40.0;
    public double WindSpeed { get; protected set; } = 5.0;
    public double WindDirection { get; protected set; } = 45.0;
    public double Precipitation { get; protected set; } = 0.0;
    public int? RandomSeed { get; protected set; }

    public MapCreationMode SelectedMapCreationMode { get; protected set; } = MapCreationMode.Random;
    public MapScenarioType? SelectedScenarioType { get; protected set; }

    public double MapNoiseStrength { get; protected set; } = 0.08;
    public double MapDrynessFactor { get; protected set; } = 1.0;
    public double ReliefStrengthFactor { get; protected set; } = 1.0;
    public double FuelDensityFactor { get; protected set; } = 1.0;

    public List<MapRegionObjectDto> MapRegionObjects { get; protected set; } = new();
    public List<(int VegetationType, double Probability)> VegetationDistributions { get; protected set; } = new();

    public string? SelectedDemoPreset { get; protected set; }
    public PreparedGridMapDto? PreparedMap { get; protected set; }

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
    private TextBox? _mapNoiseBox;
    private TextBox? _mapDrynessBox;
    private TextBox? _reliefStrengthBox;
    private TextBox? _fuelDensityBox;

    private TextBlock? _mapModeDescriptionTextBlock;
    private TextBlock? _scenarioDescriptionTextBlock;
    private TextBlock? _semiManualDescriptionTextBlock;
    private TextBlock? _mapEditorSummaryTextBlock;
    private TextBlock? _presetHintTextBlock;

    private TextBox? _coniferousBox;
    private TextBox? _deciduousBox;
    private TextBox? _mixedBox;
    private TextBox? _grassBox;
    private TextBox? _shrubBox;
    private TextBox? _waterBox;
    private TextBox? _bareBox;

    private Button? _openMapEditorButton;
    private Button? _presetButton1;
    private Button? _presetButton2;
    private Button? _presetButton3;
    private Button? _presetButton4;
    private Button? _presetButton5;
    private Button? _randomMapButton;
    private Button? _cancelButton;
    private Button? _createButton;
    private Button? _presetButton6;
    private Button? _presetButton7;


    protected void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected void InitializeGridDialog()
    {
        FindControls();
        ConfigureScenarioItems();
        AttachEvents();
        ApplyDefaults();
        ApplyModeTexts();
        UpdatePresetButtonsUi();
        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateMapEditorSummary();
        UpdateStructurePreview();
        ClearErrors();
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
        _mapNoiseBox = this.FindControl<TextBox>("MapNoiseBox");
        _mapDrynessBox = this.FindControl<TextBox>("MapDrynessBox");
        _reliefStrengthBox = this.FindControl<TextBox>("ReliefStrengthBox");
        _fuelDensityBox = this.FindControl<TextBox>("FuelDensityBox");

        _mapModeDescriptionTextBlock = this.FindControl<TextBlock>("MapModeDescriptionTextBlock");
        _scenarioDescriptionTextBlock = this.FindControl<TextBlock>("ScenarioDescriptionTextBlock");
        _semiManualDescriptionTextBlock = this.FindControl<TextBlock>("SemiManualDescriptionTextBlock");
        _mapEditorSummaryTextBlock = this.FindControl<TextBlock>("MapEditorSummaryTextBlock");
        _presetHintTextBlock = this.FindControl<TextBlock>("PresetHintTextBlock");

        _coniferousBox = this.FindControl<TextBox>("ConiferousBox");
        _deciduousBox = this.FindControl<TextBox>("DeciduousBox");
        _mixedBox = this.FindControl<TextBox>("MixedBox");
        _grassBox = this.FindControl<TextBox>("GrassBox");
        _shrubBox = this.FindControl<TextBox>("ShrubBox");
        _waterBox = this.FindControl<TextBox>("WaterBox");
        _bareBox = this.FindControl<TextBox>("BareBox");

        _openMapEditorButton = this.FindControl<Button>("OpenMapEditorButton");
        _presetButton1 = this.FindControl<Button>("PresetButton1");
        _presetButton2 = this.FindControl<Button>("PresetButton2");
        _presetButton3 = this.FindControl<Button>("PresetButton3");
        _presetButton4 = this.FindControl<Button>("PresetButton4");
        _presetButton5 = this.FindControl<Button>("PresetButton5");
        _presetButton6 = this.FindControl<Button>("PresetButton6");
        _presetButton7 = this.FindControl<Button>("PresetButton7");
        _randomMapButton = this.FindControl<Button>("RandomMapButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
        _createButton = this.FindControl<Button>("CreateButton");
    }

    private void ConfigureScenarioItems()
    {
        SelectedDemoPreset = null;
        SelectedMapCreationMode = MapCreationMode.Random;
        SelectedScenarioType = null;
    }
    private void AttachEvents()
    {
        if (_presetButton1 != null) _presetButton1.Click += OnPresetClicked;
        if (_presetButton2 != null) _presetButton2.Click += OnPresetClicked;
        if (_presetButton3 != null) _presetButton3.Click += OnPresetClicked;
        if (_presetButton4 != null) _presetButton4.Click += OnPresetClicked;
        if (_presetButton5 != null) _presetButton5.Click += OnPresetClicked;
        if (_presetButton6 != null) _presetButton6.Click += OnPresetClicked;
        if (_presetButton7 != null) _presetButton7.Click += OnPresetClicked;
        if (_randomMapButton != null) _randomMapButton.Click += OnPresetClicked;

        if (_openMapEditorButton != null)
            _openMapEditorButton.Click += async (_, _) => await OpenMapEditorAsync();

        if (_cancelButton != null)
            _cancelButton.Click += (_, _) => Close(false);

        if (_createButton != null)
        {
            _createButton.Click += (_, _) =>
            {
                if (!TryCollectValues())
                    return;

                Close(true);
            };
        }

        if (_widthBox != null) _widthBox.LostFocus += (_, _) => OnGridDefinitionChanged();
        if (_heightBox != null) _heightBox.LostFocus += (_, _) => OnGridDefinitionChanged();

        if (_moistureMinBox != null) _moistureMinBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_moistureMaxBox != null) _moistureMaxBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_elevationBox != null) _elevationBox.LostFocus += (_, _) => OnMapDefinitionChanged();

        if (_mapNoiseBox != null) _mapNoiseBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_mapDrynessBox != null) _mapDrynessBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_reliefStrengthBox != null) _reliefStrengthBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_fuelDensityBox != null) _fuelDensityBox.LostFocus += (_, _) => OnMapDefinitionChanged();

        if (_randomSeedBox != null) _randomSeedBox.LostFocus += (_, _) => OnMapDefinitionChanged();

        if (_coniferousBox != null) _coniferousBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_deciduousBox != null) _deciduousBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_mixedBox != null) _mixedBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_grassBox != null) _grassBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_shrubBox != null) _shrubBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_waterBox != null) _waterBox.LostFocus += (_, _) => OnMapDefinitionChanged();
        if (_bareBox != null) _bareBox.LostFocus += (_, _) => OnMapDefinitionChanged();

        if (_tempBox != null) _tempBox.LostFocus += (_, _) => OnWeatherDefinitionChanged();
        if (_humidityBox != null) _humidityBox.LostFocus += (_, _) => OnWeatherDefinitionChanged();
        if (_windSpeedBox != null) _windSpeedBox.LostFocus += (_, _) => OnWeatherDefinitionChanged();
        if (_precipitationBox != null) _precipitationBox.LostFocus += (_, _) => OnWeatherDefinitionChanged();

        if (_windDirBox != null)
            _windDirBox.SelectionChanged += (_, _) => OnWeatherDefinitionChanged();
    }
    private void OnGridDefinitionChanged()
    {
        int width = ParseInt(_widthBox?.Text, 20);
        int height = ParseInt(_heightBox?.Text, 20);

        if (PreparedMap != null &&
            (PreparedMap.Width != width || PreparedMap.Height != height))
        {
            PreparedMap = null;
            UpdateMapEditorSummary();
        }

        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateStructurePreview();
        ClearErrors();
    }
    private void OnMapDefinitionChanged()
    {
        if (PreparedMap != null)
        {
            PreparedMap = null;
            UpdateMapEditorSummary();
        }

        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateStructurePreview();
        ClearErrors();
    }
    private void OnWeatherDefinitionChanged()
    {
        UpdateStructurePreview();
        ClearErrors();
    }

    private void ApplyMapFactors(
    double noise,
    double dryness,
    double relief,
    double fuel)
    {
        if (_mapNoiseBox != null)
            _mapNoiseBox.Text = noise.ToString("0.00", CultureInfo.InvariantCulture);

        if (_mapDrynessBox != null)
            _mapDrynessBox.Text = dryness.ToString("0.00", CultureInfo.InvariantCulture);

        if (_reliefStrengthBox != null)
            _reliefStrengthBox.Text = relief.ToString("0.00", CultureInfo.InvariantCulture);

        if (_fuelDensityBox != null)
            _fuelDensityBox.Text = fuel.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private void ApplyVegetation(
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
    private void OnPresetClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var preset = button.Tag?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(preset))
            return;

        ClearErrors();

        PreparedMap = null;
        MapRegionObjects = new();

        switch (preset)
        {
            case "mixed":
                SelectedDemoPreset = "mixed";
                if (_nameBox != null) _nameBox.Text = "Смешанный лес";
                if (_tempBox != null) _tempBox.Text = "24";
                if (_humidityBox != null) _humidityBox.Text = "45";
                if (_windSpeedBox != null) _windSpeedBox.Text = "4";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.28";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.55";
                if (_elevationBox != null) _elevationBox.Text = "55";
                ApplyMapFactors(0.08, 1.00, 1.00, 1.00);
                ApplyVegetation(0.22, 0.22, 0.34, 0.10, 0.08, 0.02, 0.02);
                break;

            case "dry-coniferous":
                SelectedDemoPreset = "dry-coniferous";
                if (_nameBox != null) _nameBox.Text = "Сухой хвойный + ветер";
                if (_tempBox != null) _tempBox.Text = "31";
                if (_humidityBox != null) _humidityBox.Text = "22";
                if (_windSpeedBox != null) _windSpeedBox.Text = "8";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.08";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.24";
                if (_elevationBox != null) _elevationBox.Text = "70";

                ApplyMapFactors(0.10, 1.20, 1.05, 1.15);
                ApplyVegetation(0.72, 0.04, 0.12, 0.04, 0.06, 0.01, 0.01);
                break;

            case "river":
                SelectedDemoPreset = "river";
                if (_nameBox != null) _nameBox.Text = "Река как барьер";
                if (_tempBox != null) _tempBox.Text = "24";
                if (_humidityBox != null) _humidityBox.Text = "48";
                if (_windSpeedBox != null) _windSpeedBox.Text = "4";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.32";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.58";
                if (_elevationBox != null) _elevationBox.Text = "35";
                ApplyMapFactors(0.07, 0.90, 0.85, 0.95);
                ApplyVegetation(0.14, 0.30, 0.34, 0.07, 0.08, 0.06, 0.01);
                break;

            case "lake":
                SelectedDemoPreset = "lake";
                if (_nameBox != null) _nameBox.Text = "Озеро и берег";
                if (_tempBox != null) _tempBox.Text = "23";
                if (_humidityBox != null) _humidityBox.Text = "52";
                if (_windSpeedBox != null) _windSpeedBox.Text = "3";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.38";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.68";
                if (_elevationBox != null) _elevationBox.Text = "30";
                ApplyMapFactors(0.06, 0.85, 0.80, 0.95);
                ApplyVegetation(0.12, 0.30, 0.34, 0.08, 0.08, 0.07, 0.01);
                break;

            case "wet":
                SelectedDemoPreset = "wet";
                if (_nameBox != null) _nameBox.Text = "Влажный лес";
                if (_tempBox != null) _tempBox.Text = "17";
                if (_humidityBox != null) _humidityBox.Text = "82";
                if (_windSpeedBox != null) _windSpeedBox.Text = "2.5";
                if (_precipitationBox != null) _precipitationBox.Text = "2.0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.72";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.95";
                if (_elevationBox != null) _elevationBox.Text = "30";
                ApplyMapFactors(0.06, 0.65, 0.80, 0.85);
                ApplyVegetation(0.08, 0.34, 0.36, 0.08, 0.10, 0.03, 0.01);
                break;

            case "firebreak":
                SelectedDemoPreset = "firebreak";
                if (_nameBox != null) _nameBox.Text = "Просека";
                if (_tempBox != null) _tempBox.Text = "28";
                if (_humidityBox != null) _humidityBox.Text = "33";
                if (_windSpeedBox != null) _windSpeedBox.Text = "6";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.16";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.30";
                if (_elevationBox != null) _elevationBox.Text = "25";
                ApplyMapFactors(0.08, 1.08, 0.90, 1.05);
                ApplyVegetation(0.28, 0.16, 0.32, 0.08, 0.08, 0.02, 0.06);
                break;

            case "hills":
                SelectedDemoPreset = "hills";
                if (_nameBox != null) _nameBox.Text = "Холмы";
                if (_tempBox != null) _tempBox.Text = "23";
                if (_humidityBox != null) _humidityBox.Text = "42";
                if (_windSpeedBox != null) _windSpeedBox.Text = "5";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.20";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.40";
                if (_elevationBox != null) _elevationBox.Text = "170";
                ApplyMapFactors(0.09, 1.05, 1.35, 1.00);
                ApplyVegetation(0.26, 0.18, 0.30, 0.10, 0.12, 0.02, 0.02);
                break;
            case "random":
            default:
                SelectedDemoPreset = null;
                if (_nameBox != null) _nameBox.Text = "Сеточная симуляция";
                if (_tempBox != null) _tempBox.Text = "25";
                if (_humidityBox != null) _humidityBox.Text = "40";
                if (_windSpeedBox != null) _windSpeedBox.Text = "5";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.30";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.70";
                if (_elevationBox != null) _elevationBox.Text = "50";
                ApplyMapFactors(0.08, 1.00, 1.00, 1.00);
                ApplyVegetation(0.25, 0.20, 0.25, 0.15, 0.10, 0.03, 0.02);
                break;
        }

        SelectedMapCreationMode = string.IsNullOrWhiteSpace(SelectedDemoPreset)
            ? MapCreationMode.Random
            : MapCreationMode.Scenario;

        SelectedScenarioType = ParseDemoPresetToScenarioType(SelectedDemoPreset);

        UpdatePresetButtonsUi();
        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateMapEditorSummary();
        UpdateStructurePreview();
    }

    private void UpdateMapEditorSummary()
    {
        if (_mapEditorSummaryTextBlock == null)
            return;

        if (PreparedMap != null && PreparedMap.Cells.Count > 0)
        {
            var sourceText = string.IsNullOrWhiteSpace(SelectedDemoPreset)
                ? "случайной карты"
                : $"демо «{GetPresetDisplayName(SelectedDemoPreset)}»";

            _mapEditorSummaryTextBlock.Text =
                $"Итоговая карта подготовлена: {PreparedMap.Width}×{PreparedMap.Height}, клеток: {PreparedMap.Cells.Count}. Основа: {sourceText}.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedDemoPreset))
        {
            _mapEditorSummaryTextBlock.Text =
                $"Выбрано демо «{GetPresetDisplayName(SelectedDemoPreset)}». Редактор можно открыть для ручной доработки клеток.";
            return;
        }

        _mapEditorSummaryTextBlock.Text =
            "Выбрана случайная карта. Редактор можно открыть для ручной доработки клеток.";
    }
    private void UpdateScenarioDescription()
    {
        if (_scenarioDescriptionTextBlock != null)
        {
            _scenarioDescriptionTextBlock.Text = SelectedDemoPreset switch
            {
                "mixed" =>
                    "Базовый реалистичный смешанный лес без выраженного искусственного барьера. Подходит как нейтральное демо для сравнения с остальными сценариями.",

                "dry-coniferous" =>
                    "Сухой хвойный массив с жаркой и ветреной погодой. Подходит для демонстрации быстрого распространения пожара в высокогорючей среде.",

                "river" =>
                    "Лесной массив с узкой извилистой рекой и влажной прибрежной зоной. Подходит для анализа естественного водного барьера и разрыва фронта пожара.",

                "lake" =>
                    "Лесная карта с озером и более влажной береговой полосой. Подходит для демонстрации локального естественного барьера и берегового микроклимата.",

                "wet" =>
                    "Влажный лес после осадков с высокой влажностью топлива и слабым ветром. Подходит для исследования замедленного распространения пожара.",

                "firebreak" =>
                    "Карта с просекой: узкая минеральная полоса и более слабогорючие полосы по краям. Подходит для анализа искусственного противопожарного барьера.",

                "hills" =>
                    "Холмистая территория с несколькими возвышенностями и ложбинами. Подходит для анализа влияния рельефа на распространение огня.",

                _ =>
                    "Случайная карта. Можно сразу создать симуляцию или открыть редактор и подготовить итоговую карту вручную."
            };
        }

        if (_semiManualDescriptionTextBlock != null)
        {
            _semiManualDescriptionTextBlock.Text = PreparedMap != null
                ? "Итоговая карта уже подготовлена. Можно создавать симуляцию."
                : "Подготовьте итоговую карту после настройки параметров генерации.";
        }
    }

    private void ApplyDefaults()
    {
        if (_nameBox != null) _nameBox.Text = "Сеточная симуляция";
        if (_widthBox != null) _widthBox.Text = "20";
        if (_heightBox != null) _heightBox.Text = "20";
        if (_fireCellsBox != null) _fireCellsBox.Text = "3";
        if (_moistureMinBox != null) _moistureMinBox.Text = "0.30";
        if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.70";
        if (_elevationBox != null) _elevationBox.Text = "50";
        if (_stepsBox != null) _stepsBox.Text = "100";
        if (_stepDurationBox != null) _stepDurationBox.Text = "900";
        if (_tempBox != null) _tempBox.Text = "25";
        if (_humidityBox != null) _humidityBox.Text = "40";
        if (_windSpeedBox != null) _windSpeedBox.Text = "5";
        if (_precipitationBox != null) _precipitationBox.Text = "0";
        if (_randomSeedBox != null) _randomSeedBox.Text = string.Empty;

        if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.08";
        if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.0";
        if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.0";
        if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.0";

        if (_coniferousBox != null) _coniferousBox.Text = "0.25";
        if (_deciduousBox != null) _deciduousBox.Text = "0.20";
        if (_mixedBox != null) _mixedBox.Text = "0.25";
        if (_grassBox != null) _grassBox.Text = "0.15";
        if (_shrubBox != null) _shrubBox.Text = "0.10";
        if (_waterBox != null) _waterBox.Text = "0.03";
        if (_bareBox != null) _bareBox.Text = "0.02";

        if (_windDirBox != null && _windDirBox.SelectedIndex < 0)
            _windDirBox.SelectedIndex = 1;

        SelectedDemoPreset = null;
        PreparedMap = null;
        MapRegionObjects = new List<MapRegionObjectDto>();

        UpdateMapEditorSummary();
    }

    private void ApplyModeTexts()
    {
        if (_widthLabelTextBlock != null)
            _widthLabelTextBlock.Text = "Ширина";

        if (_heightLabelTextBlock != null)
            _heightLabelTextBlock.Text = "Высота";

        if (_widthHintTextBlock != null)
            _widthHintTextBlock.Text = "Диапазон: 5..200";

        if (_heightHintTextBlock != null)
            _heightHintTextBlock.Text = "Диапазон: 5..200";

        if (_fireCellsHintTextBlock != null)
            _fireCellsHintTextBlock.Text = "Диапазон: 1..50";
    }

    private void OnGridSizeChanged()
    {
        int width = ParseInt(_widthBox?.Text, 20);
        int height = ParseInt(_heightBox?.Text, 20);

        if (PreparedMap != null &&
            (PreparedMap.Width != width || PreparedMap.Height != height))
        {
            PreparedMap = null;
            UpdateMapEditorSummary();
        }

        UpdateStructurePreview();
        ClearErrors();
    }


    private bool TryCollectValues()
    {
        ClearErrors();

        var errors = new List<string>();

        SimulationName = _nameBox?.Text?.Trim() ?? string.Empty;

        if (!TryReadInt(_widthBox, "Ширина", 5, 200, errors, out var width))
            width = GridWidth;

        if (!TryReadInt(_heightBox, "Высота", 5, 200, errors, out var height))
            height = GridHeight;

        if (!TryReadInt(_fireCellsBox, "Количество очагов", 1, 50, errors, out var fireCells))
            fireCells = InitialFireCells;

        if (!TryReadDouble(_moistureMinBox, "Минимальная влажность", 0.0, 1.0, errors, out var moistureMin))
            moistureMin = MoistureMin;

        if (!TryReadDouble(_moistureMaxBox, "Максимальная влажность", 0.0, 1.0, errors, out var moistureMax))
            moistureMax = MoistureMax;

        if (!TryReadDouble(_elevationBox, "Перепад высот", 0.0, 500.0, errors, out var elevation))
            elevation = ElevationVariation;

        if (!TryReadInt(_stepsBox, "Количество шагов", 1, 10000, errors, out var steps))
            steps = SimulationSteps;

        if (!TryReadInt(_stepDurationBox, "Длительность шага", 1, 7200, errors, out var stepDuration))
            stepDuration = StepDurationSeconds;

        if (!TryReadDouble(_tempBox, "Температура", -50.0, 60.0, errors, out var temperature))
            temperature = Temperature;

        if (!TryReadDouble(_humidityBox, "Влажность воздуха", 0.0, 100.0, errors, out var humidity))
            humidity = Humidity;

        if (!TryReadDouble(_windSpeedBox, "Скорость ветра", 0.0, 60.0, errors, out var windSpeed))
            windSpeed = WindSpeed;

        if (!TryReadDouble(_precipitationBox, "Осадки", 0.0, 100.0, errors, out var precipitation))
            precipitation = Precipitation;

        if (!TryReadNullableInt(_randomSeedBox, "RandomSeed", errors, out var randomSeed))
            randomSeed = RandomSeed;

        if (moistureMin > moistureMax)
            errors.Add("Минимальная влажность не может быть больше максимальной.");

        int maxInitialFireCells = width * height;
        if (fireCells > maxInitialFireCells)
            errors.Add($"Количество очагов не может быть больше количества клеток карты ({maxInitialFireCells}).");

        var vegetation = TryBuildVegetationDistributions(errors);

        if (errors.Count > 0)
        {
            ShowError("Исправьте параметры:\n• " + string.Join("\n• ", errors));
            return false;
        }

        GridWidth = width;
        GridHeight = height;
        InitialFireCells = fireCells;
        MoistureMin = moistureMin;
        MoistureMax = moistureMax;
        ElevationVariation = elevation;
        SimulationSteps = steps;
        StepDurationSeconds = stepDuration;
        Temperature = temperature;
        Humidity = humidity;
        WindSpeed = windSpeed;
        WindDirection = ParseWindDirection(_windDirBox?.SelectedIndex ?? 1);
        Precipitation = precipitation;
        RandomSeed = randomSeed;

        MapNoiseStrength = ReadOptionalDouble(_mapNoiseBox, 0.08, 0.0, 1.0);
        MapDrynessFactor = ReadOptionalDouble(_mapDrynessBox, 1.0, 0.5, 2.0);
        ReliefStrengthFactor = ReadOptionalDouble(_reliefStrengthBox, 1.0, 0.5, 2.0);
        FuelDensityFactor = ReadOptionalDouble(_fuelDensityBox, 1.0, 0.5, 2.0);

        VegetationDistributions = vegetation;

        if (PreparedMap != null &&
            (PreparedMap.Width != GridWidth || PreparedMap.Height != GridHeight))
        {
            PreparedMap = null;
        }

        SelectedMapCreationMode = string.IsNullOrWhiteSpace(SelectedDemoPreset)
            ? MapCreationMode.Random
            : MapCreationMode.Scenario;

        SelectedScenarioType = ParseDemoPresetToScenarioType(SelectedDemoPreset);

        return true;
    }
    private bool TryReadInt(
        TextBox? box,
        string fieldName,
        int min,
        int max,
        List<string> errors,
        out int value)
    {
        value = 0;
        string text = box?.Text?.Trim() ?? string.Empty;

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            errors.Add($"{fieldName}: введите целое число.");
            return false;
        }

        if (value < min || value > max)
        {
            errors.Add($"{fieldName}: допустимый диапазон {min}..{max}.");
            return false;
        }

        return true;
    }

    private bool TryReadNullableInt(
        TextBox? box,
        string fieldName,
        List<string> errors,
        out int? value)
    {
        value = null;
        string text = box?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            errors.Add($"{fieldName}: введите целое число или оставьте поле пустым.");
            return false;
        }

        value = parsed;
        return true;
    }

    private bool TryReadDouble(
        TextBox? box,
        string fieldName,
        double min,
        double max,
        List<string> errors,
        out double value)
    {
        value = 0.0;
        string text = box?.Text?.Trim() ?? string.Empty;

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            errors.Add($"{fieldName}: введите число через точку, например 0.35.");
            return false;
        }

        if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
        {
            errors.Add($"{fieldName}: допустимый диапазон {min}..{max}.");
            return false;
        }

        return true;
    }

    private double ReadOptionalDouble(TextBox? box, double fallback, double min, double max)
    {
        string text = box?.Text?.Trim() ?? string.Empty;

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return fallback;

        return Math.Clamp(value, min, max);
    }

    private List<(int VegetationType, double Probability)> TryBuildVegetationDistributions(List<string> errors)
    {
        var values = new List<(VegetationType Type, double Value)>();

        AddVegetationValue(values, _coniferousBox, VegetationType.Coniferous, "Хвойный лес", errors);
        AddVegetationValue(values, _deciduousBox, VegetationType.Deciduous, "Лиственный лес", errors);
        AddVegetationValue(values, _mixedBox, VegetationType.Mixed, "Смешанный лес", errors);
        AddVegetationValue(values, _grassBox, VegetationType.Grass, "Трава", errors);
        AddVegetationValue(values, _shrubBox, VegetationType.Shrub, "Кустарник", errors);
        AddVegetationValue(values, _waterBox, VegetationType.Water, "Вода", errors);
        AddVegetationValue(values, _bareBox, VegetationType.Bare, "Пустая поверхность", errors);

        double total = values.Sum(x => Math.Max(0.0, x.Value));

        if (total <= 0.000001)
        {
            errors.Add("Вероятности растительности: сумма должна быть больше 0.");
            total = 1.0;
        }

        return values
            .Select(x => ((int)x.Type, Math.Max(0.0, x.Value) / total))
            .ToList();
    }

    private void AddVegetationValue(
        List<(VegetationType Type, double Value)> values,
        TextBox? box,
        VegetationType type,
        string fieldName,
        List<string> errors)
    {
        string text = box?.Text?.Trim() ?? string.Empty;

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            errors.Add($"{fieldName}: введите число от 0 до 1.");
            value = 0.0;
        }

        if (value < 0.0 || value > 1.0)
        {
            errors.Add($"{fieldName}: допустимый диапазон 0..1.");
            value = Math.Clamp(value, 0.0, 1.0);
        }

        values.Add((type, value));
    }

    protected MapScenarioType? ParseDemoPresetToScenarioType(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
            return null;

        return preset.Trim().ToLowerInvariant() switch
        {
            "mixed" => MapScenarioType.MixedForest,
            "dry-coniferous" => MapScenarioType.DryConiferousMassif,
            "river" => MapScenarioType.ForestWithRiver,
            "lake" => MapScenarioType.ForestWithLake,
            "wet" => MapScenarioType.WetForestAfterRain,
            "firebreak" => MapScenarioType.ForestWithFirebreak,
            "hills" => MapScenarioType.HillyTerrain,
            _ => null
        };
    }

    private List<(int VegetationType, double Probability)> BuildVegetationDistributions()
    {
        var values = new List<(VegetationType Type, double Value)>
        {
            (VegetationType.Coniferous, ParseDouble(_coniferousBox?.Text, 0.25)),
            (VegetationType.Deciduous, ParseDouble(_deciduousBox?.Text, 0.20)),
            (VegetationType.Mixed, ParseDouble(_mixedBox?.Text, 0.25)),
            (VegetationType.Grass, ParseDouble(_grassBox?.Text, 0.15)),
            (VegetationType.Shrub, ParseDouble(_shrubBox?.Text, 0.10)),
            (VegetationType.Water, ParseDouble(_waterBox?.Text, 0.03)),
            (VegetationType.Bare, ParseDouble(_bareBox?.Text, 0.02))
        };

        var total = values.Sum(x => Math.Max(0.0, x.Value));
        if (total <= 0.000001)
            total = 1.0;

        return values
            .Select(x => ((int)x.Type, Math.Max(0.0, x.Value) / total))
            .ToList();
    }

    private void UpdateStructurePreview()
    {
        if (_structureSummaryTextBlock == null || _structureDetailTextBlock == null)
            return;

        int width = Math.Max(1, ParseInt(_widthBox?.Text, 20));
        int height = Math.Max(1, ParseInt(_heightBox?.Text, 20));
        int totalCells = width * height;

        double moistureMin = ParseDouble(_moistureMinBox?.Text, 0.30);
        double moistureMax = ParseDouble(_moistureMaxBox?.Text, 0.70);
        double elevation = ParseDouble(_elevationBox?.Text, 50.0);

        string sourceText = PreparedMap != null
            ? "Подготовленная карта"
            : !string.IsNullOrWhiteSpace(SelectedDemoPreset)
                ? $"Демо: {GetPresetDisplayName(SelectedDemoPreset)}"
                : "Случайная карта";

        _structureSummaryTextBlock.Text =
            $"{sourceText} • {width}×{height} • клеток: {totalCells}";

        _structureDetailTextBlock.Text =
            $"Влажность {moistureMin:F2}..{moistureMax:F2} • перепад высот ±{elevation:F0} • " +
            $"редактор {(PreparedMap != null ? "использован" : "не использован")}";
    }

    private void UpdatePresetButtonsUi()
    {
        UpdatePresetButtonStyle(_randomMapButton, SelectedDemoPreset == null);
        UpdatePresetButtonStyle(_presetButton1, SelectedDemoPreset == "dry-coniferous");
        UpdatePresetButtonStyle(_presetButton2, SelectedDemoPreset == "river");
        UpdatePresetButtonStyle(_presetButton3, SelectedDemoPreset == "wet");
        UpdatePresetButtonStyle(_presetButton4, SelectedDemoPreset == "firebreak");
        UpdatePresetButtonStyle(_presetButton5, SelectedDemoPreset == "hills");
        UpdatePresetButtonStyle(_presetButton6, SelectedDemoPreset == "lake");
        UpdatePresetButtonStyle(_presetButton7, SelectedDemoPreset == "mixed");

        if (_presetHintTextBlock != null)
        {
            _presetHintTextBlock.Text = SelectedDemoPreset == null
                ? "Сейчас выбрана случайная карта"
                : $"Сейчас выбрано демо: {GetPresetDisplayName(SelectedDemoPreset)}";
        }
    }

    private static void UpdatePresetButtonStyle(Button? button, bool isActive)
    {
        if (button == null)
            return;

        button.Opacity = isActive ? 1.0 : 0.88;
    }

    private void UpdateMapModeUi()
    {
        if (_mapModeDescriptionTextBlock != null)
        {
            _mapModeDescriptionTextBlock.Text = PreparedMap != null
                ? "Будет создана подготовленная итоговая карта из редактора."
                : !string.IsNullOrWhiteSpace(SelectedDemoPreset)
                    ? $"Будет создана карта на основе демо «{GetPresetDisplayName(SelectedDemoPreset)}»."
                    : "Будет создана случайная карта по выбранным параметрам.";
        }

        if (_semiManualDescriptionTextBlock != null)
        {
            _semiManualDescriptionTextBlock.Text = PreparedMap != null
                ? "Итоговая карта подготовлена. Можно создать симуляцию или снова открыть редактор."
                : "При необходимости откройте редактор и измените отдельные клетки перед созданием симуляции.";
        }
    }
    private string GetPresetDisplayName(string preset)
    {
        return preset switch
        {
            "mixed" => "Смешанный лес",
            "dry-coniferous" => "Сухой хвойный + ветер",
            "river" => "Река как барьер",
            "lake" => "Озеро и берег",
            "wet" => "Влажный лес",
            "firebreak" => "Просека",
            "hills" => "Холмы",
            _ => "Случайная карта"
        };
    }

    private async Task OpenMapEditorAsync()
    {
        int width = Math.Max(5, ParseInt(_widthBox?.Text, 20));
        int height = Math.Max(5, ParseInt(_heightBox?.Text, 20));

        var previewMap = BuildPreviewPreparedMap(width, height);
        var editor = new MapEditorDialog(previewMap.Width, previewMap.Height, previewMap.Cells);

        var result = await editor.ShowDialog<bool>(this);

        if (!result)
            return;

        PreparedMap = editor.GetPreparedMap();

        if (PreparedMap.Width != width || PreparedMap.Height != height)
        {
            PreparedMap.Width = width;
            PreparedMap.Height = height;
        }

        UpdateMapEditorSummary();
        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateStructurePreview();
        ClearErrors();
    }

    private PreparedGridMapDto BuildPreviewPreparedMap(int width, int height)
    {
        width = Math.Max(5, width);
        height = Math.Max(5, height);

        if (PreparedMap != null &&
            PreparedMap.Width == width &&
            PreparedMap.Height == height &&
            PreparedMap.Cells.Count > 0)
        {
            return new PreparedGridMapDto
            {
                Width = PreparedMap.Width,
                Height = PreparedMap.Height,
                Cells = PreparedMap.Cells
                    .Select(ClonePreparedCell)
                    .ToList()
            };
        }

        var cells = BuildPreparedCellsFromDemoPreset(width, height, SelectedDemoPreset);

        return new PreparedGridMapDto
        {
            Width = width,
            Height = height,
            Cells = cells
        };
    }

    private List<PreparedGridCellDto> BuildPreparedCellsFromDemoPreset(int width, int height, string? preset)
    {
        var cells = new List<PreparedGridCellDto>(width * height);

        double moistureMin = ParseDouble(_moistureMinBox?.Text, 0.30);
        double moistureMax = ParseDouble(_moistureMaxBox?.Text, 0.70);
        double defaultMoisture = Math.Clamp((moistureMin + moistureMax) / 2.0, 0.0, 1.0);
        double elevationRange = ParseDouble(_elevationBox?.Text, 50.0);

        var random = RandomSeed.HasValue
            ? new Random(RandomSeed.Value)
            : new Random();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells.Add(new PreparedGridCellDto
                {
                    X = x,
                    Y = y,
                    Vegetation = "Mixed",
                    Moisture = defaultMoisture,
                    Elevation = (random.NextDouble() * 2.0 - 1.0) * elevationRange * 0.15
                });
            }
        }

        switch (preset)
        {
            case "dry-coniferous":
                PaintAll(cells, c =>
                {
                    c.Vegetation = RandomRoll(random, 0.72) ? "Coniferous" :
                                   RandomRoll(random, 0.16) ? "Mixed" :
                                   RandomRoll(random, 0.08) ? "Shrub" :
                                   RandomRoll(random, 0.03) ? "Grass" : "Deciduous";
                    c.Moisture = Clamp01(0.08 + random.NextDouble() * 0.14);
                });
                AddDryHotspots(cells, width, height, random, 4, 0.10);
                break;

            case "river":
                PaintAll(cells, c =>
                {
                    c.Vegetation = RandomMixedForest(random);
                    c.Moisture = Clamp01(0.30 + random.NextDouble() * 0.30);
                });
                PaintVerticalRiver(cells, width, height);
                break;

            case "wet":
                PaintAll(cells, c =>
                {
                    c.Vegetation = RandomWetForest(random);
                    c.Moisture = Clamp01(0.70 + random.NextDouble() * 0.22);
                });
                break;

            case "firebreak":
                PaintAll(cells, c =>
                {
                    c.Vegetation = RandomFirebreakForest(random);
                    c.Moisture = Clamp01(0.18 + random.NextDouble() * 0.17);
                });
                PaintFirebreak(cells, width, height);
                break;

            case "hills":
                PaintAll(cells, c =>
                {
                    c.Vegetation = RandomHillForest(random);
                    c.Moisture = Clamp01(0.20 + random.NextDouble() * 0.20);
                });
                AddHillGradient(cells, width, height, elevationRange <= 0 ? 120.0 : elevationRange);
                break;

            default:
                ApplyRandomDistribution(cells, random);
                break;
        }

        return cells;
    }

    private static PreparedGridCellDto ClonePreparedCell(PreparedGridCellDto cell)
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

    private void ApplyRandomDistribution(List<PreparedGridCellDto> cells, Random random)
    {
        var distributions = BuildVegetationDistributions();
        var cumulative = new List<(int Type, double Sum)>();
        double sum = 0.0;

        foreach (var item in distributions)
        {
            sum += Math.Max(0.0, item.Probability);
            cumulative.Add((item.VegetationType, sum));
        }

        if (sum <= 0.000001)
            sum = 1.0;

        double moistureMin = ParseDouble(_moistureMinBox?.Text, 0.30);
        double moistureMax = ParseDouble(_moistureMaxBox?.Text, 0.70);
        double elevationRange = ParseDouble(_elevationBox?.Text, 50.0);

        foreach (var cell in cells)
        {
            double roll = random.NextDouble() * sum;
            int vegetationType = cumulative.FirstOrDefault(x => roll <= x.Sum).Type;

            var vegetation = (VegetationType)vegetationType;
            if (vegetation == VegetationType.Water || vegetation == VegetationType.Bare)
                vegetation = VegetationType.Mixed;

            cell.Vegetation = vegetation.ToString();
            cell.Moisture = Clamp01(moistureMin + random.NextDouble() * Math.Max(0.0, moistureMax - moistureMin));
            cell.Elevation = (random.NextDouble() * 2.0 - 1.0) * elevationRange * 0.20;
        }

        ApplyConnectedSurfaceZones(cells, VegetationType.Water, random);
        ApplyConnectedSurfaceZones(cells, VegetationType.Bare, random);
    }
    private void ApplyConnectedSurfaceZones(List<PreparedGridCellDto> cells, VegetationType surfaceType, Random random)
    {
        int targetCount = GetTargetSurfaceCount(surfaceType, cells.Count);
        if (targetCount <= 0)
            return;

        int width = Math.Max(1, cells.Max(c => c.X) + 1);
        int height = Math.Max(1, cells.Max(c => c.Y) + 1);

        var lookup = cells.ToDictionary(c => (c.X, c.Y));
        var painted = new HashSet<(int X, int Y)>();

        int zones = targetCount switch
        {
            <= 8 => 1,
            <= 20 => 2,
            <= 45 => 3,
            _ => 4
        };

        int remaining = targetCount;

        for (int zoneIndex = 0; zoneIndex < zones && remaining > 0; zoneIndex++)
        {
            if (!TryFindZoneSeed(cells, lookup, painted, random, out var seed))
                break;

            int zoneSize = zoneIndex == zones - 1
                ? remaining
                : Math.Max(1, remaining / (zones - zoneIndex));

            PaintZone(surfaceType, zoneSize, seed, lookup, painted, width, height, random);
            remaining = targetCount - painted.Count;
        }
    }

    private int GetTargetSurfaceCount(VegetationType surfaceType, int totalCells)
    {
        var distributions = BuildVegetationDistributions();
        var probability = distributions
            .Where(x => x.VegetationType == (int)surfaceType)
            .Select(x => Math.Max(0.0, x.Probability))
            .DefaultIfEmpty(0.0)
            .First();

        if (probability <= 0.000001 || totalCells <= 0)
            return 0;

        int count = (int)Math.Round(totalCells * probability, MidpointRounding.AwayFromZero);
        return Math.Clamp(count, 1, totalCells);
    }

    private bool TryFindZoneSeed(
        List<PreparedGridCellDto> cells,
        Dictionary<(int X, int Y), PreparedGridCellDto> lookup,
        HashSet<(int X, int Y)> painted,
        Random random,
        out PreparedGridCellDto seed)
    {
        var candidates = cells
            .Where(c => !painted.Contains((c.X, c.Y)) && c.Vegetation != "Water" && c.Vegetation != "Bare")
            .OrderBy(_ => random.Next())
            .ToList();

        seed = null!;

        if (candidates.Count == 0)
            return false;

        seed = candidates[0];
        return true;
    }

    private void PaintZone(
        VegetationType surfaceType,
        int zoneSize,
        PreparedGridCellDto seed,
        Dictionary<(int X, int Y), PreparedGridCellDto> lookup,
        HashSet<(int X, int Y)> painted,
        int width,
        int height,
        Random random)
    {
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((seed.X, seed.Y));

        while (queue.Count > 0 && painted.Count < zoneSize)
        {
            var current = queue.Dequeue();

            if (!lookup.TryGetValue(current, out var cell))
                continue;

            if (!painted.Add(current))
                continue;

            cell.Vegetation = surfaceType.ToString();

            if (surfaceType == VegetationType.Water)
            {
                cell.Moisture = 1.0;
                cell.Elevation -= 4.0;
            }
            else
            {
                cell.Moisture = Clamp01(Math.Max(0.05, cell.Moisture * 0.45));
            }

            var neighbors = new List<(int X, int Y)>
        {
            (current.X - 1, current.Y),
            (current.X + 1, current.Y),
            (current.X, current.Y - 1),
            (current.X, current.Y + 1)
        }
            .Where(p => p.X >= 0 && p.Y >= 0 && p.X < width && p.Y < height)
            .OrderBy(_ => random.Next())
            .ToList();

            foreach (var neighbor in neighbors)
            {
                if (!painted.Contains(neighbor))
                    queue.Enqueue(neighbor);
            }
        }
    }
    private static void PaintAll(IEnumerable<PreparedGridCellDto> cells, Action<PreparedGridCellDto> paint)
    {
        foreach (var cell in cells)
            paint(cell);
    }

    private static bool RandomRoll(Random random, double probability)
    {
        return random.NextDouble() < probability;
    }

    private static string RandomMixedForest(Random random)
    {
        var roll = random.NextDouble();
        if (roll < 0.40) return "Mixed";
        if (roll < 0.70) return "Deciduous";
        if (roll < 0.88) return "Coniferous";
        if (roll < 0.95) return "Shrub";
        return "Grass";
    }

    private static string RandomWetForest(Random random)
    {
        var roll = random.NextDouble();
        if (roll < 0.40) return "Mixed";
        if (roll < 0.70) return "Deciduous";
        if (roll < 0.82) return "Coniferous";
        if (roll < 0.92) return "Shrub";
        return "Grass";
    }

    private static string RandomFirebreakForest(Random random)
    {
        var roll = random.NextDouble();
        if (roll < 0.38) return "Mixed";
        if (roll < 0.64) return "Coniferous";
        if (roll < 0.82) return "Deciduous";
        if (roll < 0.92) return "Shrub";
        return "Grass";
    }

    private static string RandomHillForest(Random random)
    {
        var roll = random.NextDouble();
        if (roll < 0.34) return "Mixed";
        if (roll < 0.60) return "Coniferous";
        if (roll < 0.78) return "Deciduous";
        if (roll < 0.90) return "Shrub";
        return "Grass";
    }

    private static void PaintVerticalRiver(List<PreparedGridCellDto> cells, int width, int height)
    {
        int centerX = Math.Max(1, width / 2);
        int thickness = Math.Max(1, Math.Min(2, width / 20));

        foreach (var cell in cells)
        {
            if (Math.Abs(cell.X - centerX) <= thickness)
            {
                cell.Vegetation = "Water";
                cell.Moisture = 1.0;
                cell.Elevation -= 4.0;
            }
            else if (Math.Abs(cell.X - centerX) <= thickness + 1)
            {
                cell.Moisture = Clamp01(Math.Max(cell.Moisture, 0.75));
            }
        }
    }

    private static void PaintFirebreak(List<PreparedGridCellDto> cells, int width, int height)
    {
        int centerY = Math.Max(1, height / 2);
        int halfThickness = Math.Max(1, Math.Min(2, height / 24));

        foreach (var cell in cells)
        {
            if (Math.Abs(cell.Y - centerY) <= halfThickness)
            {
                cell.Vegetation = "Bare";
                cell.Moisture = Clamp01(Math.Max(cell.Moisture, 0.10));
            }
        }
    }

    private static void AddDryHotspots(List<PreparedGridCellDto> cells, int width, int height, Random random, int hotspotCount, double delta)
    {
        for (int i = 0; i < hotspotCount; i++)
        {
            int cx = random.Next(0, width);
            int cy = random.Next(0, height);
            int radius = random.Next(2, 5);

            foreach (var cell in cells)
            {
                int dx = cell.X - cx;
                int dy = cell.Y - cy;

                if (dx * dx + dy * dy <= radius * radius)
                    cell.Moisture = Clamp01(cell.Moisture - delta);
            }
        }
    }

    private static void AddHillGradient(List<PreparedGridCellDto> cells, int width, int height, double elevationRange)
    {
        double cx = width * 0.55;
        double cy = height * 0.50;
        double maxDistance = Math.Sqrt(width * width + height * height);

        foreach (var cell in cells)
        {
            double dx = cell.X - cx;
            double dy = cell.Y - cy;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double factor = 1.0 - Math.Clamp(distance / Math.Max(1.0, maxDistance * 0.45), 0.0, 1.0);

            cell.Elevation = factor * elevationRange - elevationRange * 0.35;
        }
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0.0, 1.0);
    }

    private static int ParseInt(string? text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static int? ParseNullableInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double ParseDouble(string? text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static double ParseWindDirection(int selectedIndex)
    {
        return selectedIndex switch
        {
            0 => 0.0,
            1 => 45.0,
            2 => 90.0,
            3 => 135.0,
            4 => 180.0,
            5 => 225.0,
            6 => 270.0,
            7 => 315.0,
            _ => 45.0
        };
    }

    private void ShowError(string message)
    {
        if (_errorTextBlock == null)
            return;

        _errorTextBlock.Text = message;

        if (_errorTextBlock.Parent is Border border)
            border.IsVisible = true;
    }

    private void ClearErrors()
    {
        if (_errorTextBlock == null)
            return;

        _errorTextBlock.Text = string.Empty;

        if (_errorTextBlock.Parent is Border border)
            border.IsVisible = false;
    }
}