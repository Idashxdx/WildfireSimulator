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
    public ClusteredScenarioType? SelectedClusteredScenarioType { get; private set; }
    public ClusteredGraphBlueprintDto? ClusteredBlueprint { get; private set; }
    private bool IsGridDialog()
    {
        return _page == AppPage.Grid;
    }

    private bool IsClusteredDialog()
    {
        return _page == AppPage.Graph && _mode == GraphCreationMode.Clustered;
    }

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
    private TextBlock? _presetHintTextBlock;

    private Button? _presetButton1;
    private Button? _presetButton2;
    private Button? _presetButton3;
    private Button? _presetButton4;
    private Button? _presetButton5;

    public CreateSimulationDialog(AppPage page, GraphCreationMode mode)
    {
        _page = page;
        _mode = mode;

        InitializeComponent();
        FindControls();
        AttachEvents();
        ApplyDefaults();
        ApplyModeTexts();
        UpdatePresetButtonsUi();
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
        _presetHintTextBlock = this.FindControl<TextBlock>("PresetHintTextBlock");

        _presetButton1 = this.FindControl<Button>("PresetButton1");
        _presetButton2 = this.FindControl<Button>("PresetButton2");
        _presetButton3 = this.FindControl<Button>("PresetButton3");
        _presetButton4 = this.FindControl<Button>("PresetButton4");
        _presetButton5 = this.FindControl<Button>("PresetButton5");
    }
    private void UpdatePresetButtonsUi()
    {
        if (_presetHintTextBlock == null)
            return;

        if (IsGridDialog())
        {
            _presetHintTextBlock.Text =
                "Готовые территориальные сценарии для сетки. Кнопка сразу подставляет режим, сценарий, погодные условия и параметры карты.";

            if (_presetButton1 != null) _presetButton1.Content = "Сухой хвойный + ветер";
            if (_presetButton2 != null) _presetButton2.Content = "Река как барьер";
            if (_presetButton3 != null) _presetButton3.Content = "Влажный лес";
            if (_presetButton4 != null) _presetButton4.Content = "Просека";
            if (_presetButton5 != null) _presetButton5.Content = "Холмы";
            return;
        }

        if (IsClusteredDialog())
        {
            _presetHintTextBlock.Text =
                "Готовые сценарии именно для clustered graph. Кнопка сразу подставляет clustered-сценарий, параметры связности, влажности, рельефа и погодные настройки.";

            if (_presetButton1 != null) _presetButton1.Content = "Плотный сухой массив";
            if (_presetButton2 != null) _presetButton2.Content = "Водный барьер";
            if (_presetButton3 != null) _presetButton3.Content = "Влажные патчи";
            if (_presetButton4 != null) _presetButton4.Content = "Разрыв / просека";
            if (_presetButton5 != null) _presetButton5.Content = "Холмистые кластеры";
            return;
        }

        _presetHintTextBlock.Text =
            "Для текущего режима быстрые сценарии пока не перерабатываются.";

        if (_presetButton1 != null) _presetButton1.Content = "Сценарий 1";
        if (_presetButton2 != null) _presetButton2.Content = "Сценарий 2";
        if (_presetButton3 != null) _presetButton3.Content = "Сценарий 3";
        if (_presetButton4 != null) _presetButton4.Content = "Сценарий 4";
        if (_presetButton5 != null) _presetButton5.Content = "Сценарий 5";
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
        ResetEditorArtifactsForPreset();

        if (IsGridDialog())
        {
            ApplyGridPreset(preset);
        }
        else if (IsClusteredDialog())
        {
            ApplyClusteredPreset(preset);
        }
        else
        {
            return;
        }

        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateMapEditorSummary();
        UpdateStructurePreview();
    }
    private void ResetEditorArtifactsForPreset()
    {
        MapRegionObjects = new List<MapRegionObjectDto>();
        ClusteredBlueprint = null;
    }

    private void ApplyGridPreset(string preset)
    {
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
        }
    }

    private void ApplyClusteredPreset(string preset)
    {
        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectedIndex = 1;

        switch (preset)
        {

            case "dry-coniferous":
                if (_nameBox != null) _nameBox.Text = "Малый clustered: сухой кластер";
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 0;

                if (_widthBox != null) _widthBox.Text = "6";
                if (_heightBox != null) _heightBox.Text = "6";
                if (_fireCellsBox != null) _fireCellsBox.Text = "1";

                if (_moistureMinBox != null) _moistureMinBox.Text = "0.10";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.22";
                if (_elevationBox != null) _elevationBox.Text = "30";

                if (_stepsBox != null) _stepsBox.Text = "40";
                if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                if (_tempBox != null) _tempBox.Text = "30";
                if (_humidityBox != null) _humidityBox.Text = "25";
                if (_windSpeedBox != null) _windSpeedBox.Text = "6";
                if (_windDirBox != null) _windDirBox.SelectedIndex = 2;

                SetVegetationDistributionTexts(0.60, 0.05, 0.15, 0.08, 0.07, 0.03, 0.02);
                break;

            case "river":
                if (_nameBox != null) _nameBox.Text = "Малый clustered: водный барьер";
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 1;

                if (_widthBox != null) _widthBox.Text = "7";
                if (_heightBox != null) _heightBox.Text = "6";
                if (_fireCellsBox != null) _fireCellsBox.Text = "1";

                if (_moistureMinBox != null) _moistureMinBox.Text = "0.30";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.60";
                if (_elevationBox != null) _elevationBox.Text = "25";

                if (_stepsBox != null) _stepsBox.Text = "50";

                SetVegetationDistributionTexts(0.25, 0.20, 0.25, 0.10, 0.10, 0.07, 0.03);
                break;


            case "wet":
                if (_nameBox != null) _nameBox.Text = "Большой clustered: влажные патчи";
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 4;

                if (_widthBox != null) _widthBox.Text = "24";
                if (_heightBox != null) _heightBox.Text = "22";
                if (_fireCellsBox != null) _fireCellsBox.Text = "1";

                if (_moistureMinBox != null) _moistureMinBox.Text = "0.52";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.88";
                if (_elevationBox != null) _elevationBox.Text = "40";

                if (_stepsBox != null) _stepsBox.Text = "80";

                SetVegetationDistributionTexts(0.14, 0.28, 0.30, 0.08, 0.10, 0.06, 0.04);
                break;

            case "firebreak":
                if (_nameBox != null) _nameBox.Text = "Большой clustered: разрыв";
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 2;

                if (_widthBox != null) _widthBox.Text = "26";
                if (_heightBox != null) _heightBox.Text = "20";
                if (_fireCellsBox != null) _fireCellsBox.Text = "2";

                if (_moistureMinBox != null) _moistureMinBox.Text = "0.22";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.52";
                if (_elevationBox != null) _elevationBox.Text = "45";

                SetVegetationDistributionTexts(0.26, 0.18, 0.26, 0.10, 0.10, 0.05, 0.05);
                break;

            case "hills":
                if (_nameBox != null) _nameBox.Text = "Большой clustered: холмистые кластеры";
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 3;

                if (_widthBox != null) _widthBox.Text = "28";
                if (_heightBox != null) _heightBox.Text = "24";
                if (_fireCellsBox != null) _fireCellsBox.Text = "2";

                if (_elevationBox != null) _elevationBox.Text = "120";

                SetVegetationDistributionTexts(0.24, 0.18, 0.24, 0.12, 0.14, 0.04, 0.04);
                break;
        }
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

        bool isGrid = IsGridDialog();
        bool isClustered = IsClusteredDialog();

        if (isGrid)
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
                ShowError("Размеры рабочей области должны быть не меньше 3×3.");
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

        SelectedScenarioType = null;
        SelectedClusteredScenarioType = null;

        if (SelectedMapCreationMode == MapCreationMode.Scenario)
        {
            if (isGrid)
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
            }
            else if (isClustered)
            {
                SelectedClusteredScenarioType = _scenarioTypeBox?.SelectedIndex switch
                {
                    1 => ClusteredScenarioType.WaterBarrier,
                    2 => ClusteredScenarioType.FirebreakGap,
                    3 => ClusteredScenarioType.HillyClusters,
                    4 => ClusteredScenarioType.WetAfterRain,
                    5 => ClusteredScenarioType.MixedDryHotspots,
                    _ => ClusteredScenarioType.DenseDryConiferous
                };
            }
        }

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

        if (SelectedMapCreationMode == MapCreationMode.SemiManual)
        {
            if (isGrid)
            {
                if (MapRegionObjects.Count == 0)
                {
                    ShowError("Для полуручного режима сетки нужно добавить хотя бы один объект карты.");
                    return;
                }
            }
            else if (isClustered)
            {
                if (ClusteredBlueprint == null)
                {
                    ShowError("Для полуручного clustered graph сначала нужно открыть clustered editor и подготовить blueprint.");
                    return;
                }

                int candidateCount = ClusteredBlueprint.Candidates?.Count ?? 0;
                int nodeCount = ClusteredBlueprint.Nodes?.Count ?? 0;
                int edgeCount = ClusteredBlueprint.Edges?.Count ?? 0;

                if (candidateCount == 0)
                {
                    ShowError("В clustered blueprint нет даже подложки-кандидатов. Откройте clustered editor и сгенерируйте рабочее поле.");
                    return;
                }

                if (nodeCount == 0)
                {
                    ShowError("Для полуручного clustered graph нужно выбрать хотя бы один узел.");
                    return;
                }

                if (nodeCount > 1 && edgeCount == 0)
                {
                    ShowError("В clustered graph выбрано несколько узлов, но между ними нет рёбер. Добавьте связи в clustered editor.");
                    return;
                }

                bool hasInvalidMoisture = ClusteredBlueprint.Nodes.Any(n => n.Moisture < 0 || n.Moisture > 1);
                if (hasInvalidMoisture)
                {
                    ShowError("У одного или нескольких узлов clustered blueprint влажность вне диапазона от 0 до 1.");
                    return;
                }

                bool hasSelfLoop = ClusteredBlueprint.Edges.Any(e => e.FromNodeId == e.ToNodeId);
                if (hasSelfLoop)
                {
                    ShowError("В clustered blueprint есть ребро, соединяющее узел с самим собой. Удалите такие связи.");
                    return;
                }
            }
        }

        Close(true);
    }

    private async Task OpenMapEditorAsync()
    {
        int width = ParseInt(_widthBox?.Text, 20);
        int height = ParseInt(_heightBox?.Text, 20);

        if (IsGridDialog())
        {
            if (width < 5 || height < 5)
            {
                ShowError("Сначала задайте корректные размеры территории.");
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
            return;
        }

        if (IsClusteredDialog())
        {
            if (width < 3 || height < 3)
            {
                ShowError("Сначала задайте корректные размеры поля clustered graph.");
                return;
            }

            var editor = new ClusteredGraphEditorDialog(width, height, ClusteredBlueprint);
            var result = await editor.ShowDialog<bool>(this);

            if (!result)
                return;

            ClusteredBlueprint = editor.EditedBlueprint;

            UpdateMapEditorSummary();
            UpdateMapModeUi();
            UpdateStructurePreview();
            ClearErrors();
            return;
        }

        ShowError("Для текущего режима отдельный редактор пока не предусмотрен.");
    }

    private string GetMapObjectTypeName(MapObjectType type)
    {
        return type switch
        {
            MapObjectType.ConiferousArea => "Хвойная зона",
            MapObjectType.DeciduousArea => "Лиственная зона",
            MapObjectType.MixedForestArea => "Смешанный лес",
            MapObjectType.GrassArea => "Трава",
            MapObjectType.ShrubArea => "Кустарник",
            MapObjectType.WaterBody => "Водоём",
            MapObjectType.Firebreak => "Просека",
            MapObjectType.WetZone => "Влажная зона",
            MapObjectType.DryZone => "Сухая зона",
            MapObjectType.Hill => "Холм",
            MapObjectType.Lowland => "Низина",
            _ => type.ToString()
        };
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

        if (IsGridDialog())
        {
            _typeInfoTextBlock.Text = "Сеточная симуляция";
            _typeHintTextBlock.Text =
                "Grid-модель строится как карта из клеток. Для неё доступны территориальные сценарии и полуручное создание областей: вода, просеки, влажные и сухие зоны, холмы и низины.";

            _widthLabelTextBlock.Text = "Ширина сетки";
            _heightLabelTextBlock.Text = "Высота сетки";

            _widthHintTextBlock.Text = "Количество клеток по горизонтали.";
            _heightHintTextBlock.Text = "Количество клеток по вертикали.";

            _fireCellsHintTextBlock.Text =
                "Стартовые очаги пожара будут выбраны случайно или вручную на карте клеток.";

            UpdatePresetButtonsUi();
            return;
        }

        if (IsClusteredDialog())
        {
            _typeInfoTextBlock.Text = "Кластерный граф";
            _typeHintTextBlock.Text =
                "ClusteredGraph — это самостоятельная графовая модель. Узлы образуют патчи и группы, а сценарии управляют плотностью связей, барьерами, мостами, влажностью, рельефом и доминирующей растительностью.";

            _widthLabelTextBlock.Text = "Ширина поля графа";
            _heightLabelTextBlock.Text = "Высота поля графа";

            _widthHintTextBlock.Text = "Размер рабочей области, внутри которой размещаются кандидаты и узлы clustered graph.";
            _heightHintTextBlock.Text = "Размер рабочей области для построения патчей, мостов и связей clustered graph.";

            _fireCellsHintTextBlock.Text =
                "Стартовые очаги можно выбрать случайно или вручную по узлам графа.";

            UpdatePresetButtonsUi();
            return;
        }

        _typeInfoTextBlock.Text = "Региональный граф";
        _typeHintTextBlock.Text =
            "RegionClusterGraph пока оставлен без переработки. Этот режим не входит в текущий этап улучшения clustered graph.";

        _widthLabelTextBlock.Text = "Ширина области";
        _heightLabelTextBlock.Text = "Высота области";

        _widthHintTextBlock.Text = "Используется как размер базовой области.";
        _heightHintTextBlock.Text = "Используется как размер базовой области.";

        _fireCellsHintTextBlock.Text =
            "Очаги пожара будут выбраны случайно или вручную по вершинам графа.";

        UpdatePresetButtonsUi();
    }

    private void UpdateMapModeUi()
    {
        if (_mapCreationModeBox == null)
            return;

        SelectedMapCreationMode = _mapCreationModeBox.SelectedIndex switch
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
            if (IsGridDialog())
            {
                _mapModeDescriptionTextBlock.Text = SelectedMapCreationMode switch
                {
                    MapCreationMode.Random =>
                        "Сетка будет сгенерирована автоматически по общим параметрам влажности, рельефа, плотности топлива и распределения растительности.",

                    MapCreationMode.Scenario =>
                        "Для сетки используются территориальные сценарии: лес с рекой, озером, просекой, холмами, влажный лес после дождя и другие.",

                    MapCreationMode.SemiManual =>
                        "Полуручной режим сетки работает через редактор карты: вы создаёте области воды, просеки, влажные и сухие зоны, холмы и низины прямо на клеточном поле.",

                    _ => "Сетка будет создана автоматически."
                };
            }
            else if (IsClusteredDialog())
            {
                _mapModeDescriptionTextBlock.Text = SelectedMapCreationMode switch
                {
                    MapCreationMode.Random =>
                        "Кластерный граф будет собран автоматически: система создаст патчи, разместит узлы и построит связи между ними без использования территориального редактора сетки.",

                    MapCreationMode.Scenario =>
                        "Для clustered graph используются собственные графовые сценарии: плотные сухие патчи, водные барьеры, разрывы, холмистые кластеры, влажные патчи и смешанные очаги сухости.",

                    MapCreationMode.SemiManual =>
                        "Полуручной clustered-режим работает через отдельный редактор узлов и рёбер: вы выбираете точки-кандидаты, формируете граф, задаёте связи и параметры узлов.",

                    _ => "Кластерный граф будет создан автоматически."
                };
            }
            else
            {
                _mapModeDescriptionTextBlock.Text = SelectedMapCreationMode switch
                {
                    MapCreationMode.Random => "Режим случайной генерации.",
                    MapCreationMode.Scenario => "Сценарный режим пока не перерабатывается для этого типа графа.",
                    MapCreationMode.SemiManual => "Полуручной режим пока не перерабатывается для этого типа графа.",
                    _ => "Режим генерации не выбран."
                };
            }
        }

        if (_semiManualDescriptionTextBlock != null)
        {
            if (SelectedMapCreationMode != MapCreationMode.SemiManual)
            {
                _semiManualDescriptionTextBlock.Text = string.Empty;
            }
            else if (IsGridDialog())
            {
                _semiManualDescriptionTextBlock.Text =
                    "Откроется редактор карты. В нём можно рисовать области леса, воды, просеки, влажные и сухие зоны, холмы и низины. Эти объекты изменяют параметры клеточной территории.";
            }
            else if (IsClusteredDialog())
            {
                _semiManualDescriptionTextBlock.Text =
                    "Откроется clustered graph editor. В нём можно выбрать узлы из подложки-кандидатов, вручную соединить их рёбрами, удалить связи и задать свойства узлов и рёбер.";
            }
            else
            {
                _semiManualDescriptionTextBlock.Text =
                    "Для этого режима отдельный полуручной сценарий пока не входит в текущий этап.";
            }
        }

        if (_openMapEditorButton != null)
        {
            if (SelectedMapCreationMode != MapCreationMode.SemiManual)
            {
                _openMapEditorButton.Content = "Редактор недоступен";
                _openMapEditorButton.IsEnabled = false;
            }
            else if (IsGridDialog())
            {
                _openMapEditorButton.Content = "Открыть редактор карты";
                _openMapEditorButton.IsEnabled = true;
            }
            else if (IsClusteredDialog())
            {
                _openMapEditorButton.Content = "Открыть редактор clustered graph";
                _openMapEditorButton.IsEnabled = true;
            }
            else
            {
                _openMapEditorButton.Content = "Редактор пока не поддержан";
                _openMapEditorButton.IsEnabled = false;
            }
        }
    }


    private void UpdateMapEditorSummary()
    {
        if (_mapEditorSummaryTextBlock == null)
            return;

        bool isGrid = IsGridDialog();
        bool isClustered = IsClusteredDialog();

        if (SelectedMapCreationMode != MapCreationMode.SemiManual)
        {
            _mapEditorSummaryTextBlock.Text = isGrid
                ? "Полуручный редактор сетки не используется."
                : isClustered
                    ? "Полуручный clustered editor не используется."
                    : "Редактор для этого режима не используется.";
            return;
        }

        if (isGrid)
        {
            int count = MapRegionObjects?.Count ?? 0;

            if (count == 0)
            {
                _mapEditorSummaryTextBlock.Text =
                    "Объекты карты ещё не заданы. Для сеточного полуручного режима нужно добавить хотя бы одну область.";
                return;
            }

            _mapEditorSummaryTextBlock.Text = BuildMapObjectsDetailedSummary();
            return;
        }

        if (isClustered)
        {
            if (ClusteredBlueprint == null)
            {
                _mapEditorSummaryTextBlock.Text =
                    "Clustered blueprint ещё не подготовлен. Откройте clustered graph editor и выберите узлы, связи и параметры.";
                return;
            }

            int candidateCount = ClusteredBlueprint.Candidates?.Count ?? 0;
            int nodeCount = ClusteredBlueprint.Nodes?.Count ?? 0;
            int edgeCount = ClusteredBlueprint.Edges?.Count ?? 0;

            int groupCount = (ClusteredBlueprint.Nodes ?? new List<ClusteredNodeDraftDto>())
                .Select(x => x.ClusterId?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Count();

            int bridgeCount = (ClusteredBlueprint.Edges ?? new List<ClusteredEdgeDraftDto>())
                .Count(edge =>
                {
                    var fromNode = ClusteredBlueprint.Nodes.FirstOrDefault(n => n.Id == edge.FromNodeId);
                    var toNode = ClusteredBlueprint.Nodes.FirstOrDefault(n => n.Id == edge.ToNodeId);

                    if (fromNode == null || toNode == null)
                        return false;

                    return !string.Equals(
                        fromNode.ClusterId?.Trim(),
                        toNode.ClusterId?.Trim(),
                        StringComparison.Ordinal);
                });

            _mapEditorSummaryTextBlock.Text =
                $"Подготовлен clustered blueprint: кандидатов — {candidateCount}, узлов — {nodeCount}, рёбер — {edgeCount}, групп — {groupCount}, мостов — {bridgeCount}.";
            return;
        }

        _mapEditorSummaryTextBlock.Text =
            "Для этой структуры отдельный редактор пока не подключён.";
    }

    private string BuildMapObjectsDetailedSummary()
    {
        if (MapRegionObjects == null || MapRegionObjects.Count == 0)
            return "Объекты карты ещё не заданы. Для сеточного полуручного режима нужно добавить хотя бы одну область.";

        int waterCount = MapRegionObjects.Count(x => x.ObjectType == MapObjectType.WaterBody);
        int firebreakCount = MapRegionObjects.Count(x => x.ObjectType == MapObjectType.Firebreak);
        int wetCount = MapRegionObjects.Count(x => x.ObjectType == MapObjectType.WetZone);
        int dryCount = MapRegionObjects.Count(x => x.ObjectType == MapObjectType.DryZone);
        int hillCount = MapRegionObjects.Count(x => x.ObjectType == MapObjectType.Hill);
        int lowlandCount = MapRegionObjects.Count(x => x.ObjectType == MapObjectType.Lowland);

        int vegetationAreas = MapRegionObjects.Count(x =>
            x.ObjectType == MapObjectType.ConiferousArea ||
            x.ObjectType == MapObjectType.DeciduousArea ||
            x.ObjectType == MapObjectType.MixedForestArea ||
            x.ObjectType == MapObjectType.GrassArea ||
            x.ObjectType == MapObjectType.ShrubArea);

        var parts = new List<string>
    {
        $"Объектов карты: {MapRegionObjects.Count}"
    };

        if (vegetationAreas > 0)
            parts.Add($"растительных областей — {vegetationAreas}");

        if (waterCount > 0)
            parts.Add($"водоёмов — {waterCount}");

        if (firebreakCount > 0)
            parts.Add($"просек — {firebreakCount}");

        if (wetCount > 0)
            parts.Add($"влажных зон — {wetCount}");

        if (dryCount > 0)
            parts.Add($"сухих зон — {dryCount}");

        if (hillCount > 0)
            parts.Add($"холмов — {hillCount}");

        if (lowlandCount > 0)
            parts.Add($"низин — {lowlandCount}");

        return string.Join(", ", parts) + ".";
    }


    private void UpdateScenarioDescription()
    {
        SelectedScenarioType = null;
        SelectedClusteredScenarioType = null;

        if (_scenarioDescriptionTextBlock == null || _scenarioTypeBox == null)
            return;

        if (SelectedMapCreationMode != MapCreationMode.Scenario)
        {
            _scenarioDescriptionTextBlock.Text =
                IsGridDialog()
                    ? "Выберите один из территориальных сценариев сетки."
                    : IsClusteredDialog()
                        ? "Выберите один из сценариев clustered graph."
                        : "Сценарии для этого режима пока не перерабатываются.";
            return;
        }

        if (IsGridDialog())
        {
            SelectedScenarioType = _scenarioTypeBox.SelectedIndex switch
            {
                1 => MapScenarioType.DryConiferousMassif,
                2 => MapScenarioType.ForestWithRiver,
                3 => MapScenarioType.ForestWithLake,
                4 => MapScenarioType.ForestWithFirebreak,
                5 => MapScenarioType.HillyTerrain,
                6 => MapScenarioType.WetForestAfterRain,
                _ => MapScenarioType.MixedForest
            };

            _scenarioDescriptionTextBlock.Text = SelectedScenarioType switch
            {
                MapScenarioType.MixedForest =>
                    "Смешанный лес без ярко выраженного барьера. Подходит как базовый сценарий сравнения.",

                MapScenarioType.DryConiferousMassif =>
                    "Преобладает сухой хвойный лес с высокой горючестью. Огонь должен распространяться быстро, особенно при сильном ветре.",

                MapScenarioType.ForestWithRiver =>
                    "На сетке формируется речной барьер. Вода и повышенная влажность по берегам должны ослаблять или останавливать распространение пожара.",

                MapScenarioType.ForestWithLake =>
                    "На карте создаётся озеро с влажной прибрежной зоной. Сценарий удобен для наблюдения обхода водоёма фронтом пожара.",

                MapScenarioType.ForestWithFirebreak =>
                    "На карте формируется просека или разрыв растительности. Сценарий нужен для проверки, насколько барьер сдерживает переход огня.",

                MapScenarioType.HillyTerrain =>
                    "Рельеф выражен сильнее обычного. Можно наблюдать влияние подъёмов и низин на интенсивность и скорость распространения.",

                MapScenarioType.WetForestAfterRain =>
                    "Территория становится заметно более влажной. Вероятность воспламенения и скорость распространения должны снижаться.",

                _ => "Сценарий сетки выбран."
            };

            return;
        }

        if (IsClusteredDialog())
        {
            SelectedClusteredScenarioType = _scenarioTypeBox.SelectedIndex switch
            {
                1 => ClusteredScenarioType.WaterBarrier,
                2 => ClusteredScenarioType.FirebreakGap,
                3 => ClusteredScenarioType.HillyClusters,
                4 => ClusteredScenarioType.WetAfterRain,
                5 => ClusteredScenarioType.MixedDryHotspots,
                _ => ClusteredScenarioType.DenseDryConiferous
            };

            _scenarioDescriptionTextBlock.Text = SelectedClusteredScenarioType switch
            {
                ClusteredScenarioType.DenseDryConiferous =>
                    "Плотные сухие хвойные патчи с хорошей внутренней связностью и сильными межпатчевыми переходами. Это сценарий быстрого и агрессивного распространения по графу.",

                ClusteredScenarioType.WaterBarrier =>
                    "Часть патчей разделена водным барьером. Межкластерные переходы становятся редкими или ослабленными, а граф визуально делится на группы.",

                ClusteredScenarioType.FirebreakGap =>
                    "Между группами патчей формируется разрыв или очень слабые переходы. Сценарий нужен для проверки, как барьерность влияет на переход огня между частями графа.",

                ClusteredScenarioType.HillyClusters =>
                    "Патчи отличаются по высоте сильнее обычного. Сценарий подчёркивает неоднородность рельефа и влияние топологии на распространение.",

                ClusteredScenarioType.WetAfterRain =>
                    "Патчи становятся более влажными, а связи между ними — менее опасными для распространения. Это clustered-сценарий подавленного пожара.",

                ClusteredScenarioType.MixedDryHotspots =>
                    "Большая часть графа остаётся умеренной, но в отдельных патчах появляются сухие очаги с повышенной горючестью и более опасными переходами.",

                _ => "Сценарий clustered graph выбран."
            };

            return;
        }

        _scenarioDescriptionTextBlock.Text =
            "Для текущего режима отдельное описание сценариев пока не входит в этот этап.";
    }

    private void UpdateStructurePreview()
    {
        if (_structureSummaryTextBlock == null || _structureDetailTextBlock == null)
            return;

        int width = ParseInt(_widthBox?.Text, 20);
        int height = ParseInt(_heightBox?.Text, 20);
        int fireCells = ParseInt(_fireCellsBox?.Text, 3);

        double dryness = ParseDouble(_mapDrynessBox?.Text, 1.0);
        double relief = ParseDouble(_reliefStrengthBox?.Text, 1.0);
        double fuel = ParseDouble(_fuelDensityBox?.Text, 1.0);

        width = Math.Max(1, width);
        height = Math.Max(1, height);
        fireCells = Math.Max(1, fireCells);

        if (IsGridDialog())
        {
            int totalCells = width * height;

            _structureSummaryTextBlock.Text =
                $"Сетка {width} × {height} • клеток: {totalCells} • стартовых очагов: {fireCells}";

            string modeText = SelectedMapCreationMode switch
            {
                MapCreationMode.Random => "Случайная клеточная территория",
                MapCreationMode.Scenario => "Сценарная клеточная территория",
                MapCreationMode.SemiManual => "Полуручная клеточная территория",
                _ => "Клеточная территория"
            };

            string objectText = SelectedMapCreationMode == MapCreationMode.SemiManual
                ? $" • объектов карты: {MapRegionObjects.Count}"
                : string.Empty;

            _structureDetailTextBlock.Text =
                $"{modeText}{objectText}\n" +
                $"Факторы: сухость {dryness:F2}, рельеф {relief:F2}, плотность топлива {fuel:F2}.";
            return;
        }

        if (IsClusteredDialog())
        {
            int candidateCount = ClusteredBlueprint?.Candidates?.Count ?? 0;
            int nodeCount = ClusteredBlueprint?.Nodes?.Count ?? 0;
            int edgeCount = ClusteredBlueprint?.Edges?.Count ?? 0;

            string scenarioText = SelectedMapCreationMode switch
            {
                MapCreationMode.Random => "Случайный clustered graph",
                MapCreationMode.Scenario => "Сценарный clustered graph",
                MapCreationMode.SemiManual => "Полуручный clustered graph",
                _ => "Clustered graph"
            };

            _structureSummaryTextBlock.Text =
                $"Поле {width} × {height} • стартовых очагов: {fireCells}";

            if (SelectedMapCreationMode == MapCreationMode.SemiManual)
            {
                _structureDetailTextBlock.Text =
                    $"{scenarioText}\n" +
                    $"Blueprint: кандидатов {candidateCount}, узлов {nodeCount}, рёбер {edgeCount}.";
            }
            else
            {
                _structureDetailTextBlock.Text =
                    $"{scenarioText}\n" +
                    $"Факторы: сухость {dryness:F2}, рельеф {relief:F2}, плотность топлива {fuel:F2}.";
            }

            return;
        }

        _structureSummaryTextBlock.Text =
            $"Область {width} × {height} • стартовых очагов: {fireCells}";

        _structureDetailTextBlock.Text =
            "RegionClusterGraph пока не перерабатывается в этом этапе. Текущая ветка оставлена без отдельной UX-настройки.";
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