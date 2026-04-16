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
        ClusteredBlueprint = null;

        bool isGrid = IsGridDialog();
        bool isClustered = IsClusteredDialog();

        if (isGrid)
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

                default:
                    return;
            }
        }
        else if (isClustered)
        {
            if (_mapCreationModeBox != null)
                _mapCreationModeBox.SelectedIndex = 1;

            switch (preset)
            {
                case "dry-coniferous":
                    if (_nameBox != null) _nameBox.Text = "Демо: Плотный сухой хвойный кластерный граф";
                    if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 0;

                    if (_widthBox != null) _widthBox.Text = "26";
                    if (_heightBox != null) _heightBox.Text = "22";
                    if (_fireCellsBox != null) _fireCellsBox.Text = "2";

                    if (_moistureMinBox != null) _moistureMinBox.Text = "0.10";
                    if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.22";
                    if (_elevationBox != null) _elevationBox.Text = "60";

                    if (_stepsBox != null) _stepsBox.Text = "90";
                    if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                    if (_tempBox != null) _tempBox.Text = "31";
                    if (_humidityBox != null) _humidityBox.Text = "25";
                    if (_windSpeedBox != null) _windSpeedBox.Text = "9";
                    if (_windDirBox != null) _windDirBox.SelectedIndex = 2;
                    if (_precipitationBox != null) _precipitationBox.Text = "0";

                    if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.06";
                    if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.28";
                    if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.00";
                    if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.25";
                    if (_randomSeedBox != null) _randomSeedBox.Text = "601";

                    SetVegetationDistributionTexts(0.56, 0.08, 0.20, 0.06, 0.07, 0.02, 0.01);
                    break;

                case "river":
                    if (_nameBox != null) _nameBox.Text = "Демо: Кластеры, разделённые водным барьером";
                    if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 1;

                    if (_widthBox != null) _widthBox.Text = "28";
                    if (_heightBox != null) _heightBox.Text = "20";
                    if (_fireCellsBox != null) _fireCellsBox.Text = "2";

                    if (_moistureMinBox != null) _moistureMinBox.Text = "0.28";
                    if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.58";
                    if (_elevationBox != null) _elevationBox.Text = "45";

                    if (_stepsBox != null) _stepsBox.Text = "100";
                    if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                    if (_tempBox != null) _tempBox.Text = "26";
                    if (_humidityBox != null) _humidityBox.Text = "42";
                    if (_windSpeedBox != null) _windSpeedBox.Text = "6";
                    if (_windDirBox != null) _windDirBox.SelectedIndex = 2;
                    if (_precipitationBox != null) _precipitationBox.Text = "0";

                    if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.04";
                    if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.00";
                    if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "0.95";
                    if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.00";
                    if (_randomSeedBox != null) _randomSeedBox.Text = "602";

                    SetVegetationDistributionTexts(0.20, 0.24, 0.30, 0.08, 0.10, 0.05, 0.03);
                    break;

                case "wet":
                    if (_nameBox != null) _nameBox.Text = "Демо: Влажные патчи после дождя";
                    if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 4;

                    if (_widthBox != null) _widthBox.Text = "24";
                    if (_heightBox != null) _heightBox.Text = "22";
                    if (_fireCellsBox != null) _fireCellsBox.Text = "1";

                    if (_moistureMinBox != null) _moistureMinBox.Text = "0.52";
                    if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.88";
                    if (_elevationBox != null) _elevationBox.Text = "40";

                    if (_stepsBox != null) _stepsBox.Text = "80";
                    if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                    if (_tempBox != null) _tempBox.Text = "19";
                    if (_humidityBox != null) _humidityBox.Text = "76";
                    if (_windSpeedBox != null) _windSpeedBox.Text = "3";
                    if (_windDirBox != null) _windDirBox.SelectedIndex = 1;
                    if (_precipitationBox != null) _precipitationBox.Text = "2.0";

                    if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.03";
                    if (_mapDrynessBox != null) _mapDrynessBox.Text = "0.78";
                    if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "0.92";
                    if (_fuelDensityBox != null) _fuelDensityBox.Text = "0.95";
                    if (_randomSeedBox != null) _randomSeedBox.Text = "603";

                    SetVegetationDistributionTexts(0.14, 0.28, 0.30, 0.08, 0.10, 0.06, 0.04);
                    break;

                case "firebreak":
                    if (_nameBox != null) _nameBox.Text = "Демо: Кластеры с просекой / разрывом";
                    if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 2;

                    if (_widthBox != null) _widthBox.Text = "26";
                    if (_heightBox != null) _heightBox.Text = "20";
                    if (_fireCellsBox != null) _fireCellsBox.Text = "2";

                    if (_moistureMinBox != null) _moistureMinBox.Text = "0.22";
                    if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.52";
                    if (_elevationBox != null) _elevationBox.Text = "45";

                    if (_stepsBox != null) _stepsBox.Text = "90";
                    if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                    if (_tempBox != null) _tempBox.Text = "28";
                    if (_humidityBox != null) _humidityBox.Text = "35";
                    if (_windSpeedBox != null) _windSpeedBox.Text = "7";
                    if (_windDirBox != null) _windDirBox.SelectedIndex = 2;
                    if (_precipitationBox != null) _precipitationBox.Text = "0";

                    if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.05";
                    if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.05";
                    if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.00";
                    if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.08";
                    if (_randomSeedBox != null) _randomSeedBox.Text = "604";

                    SetVegetationDistributionTexts(0.26, 0.18, 0.26, 0.10, 0.10, 0.05, 0.05);
                    break;

                case "hills":
                    if (_nameBox != null) _nameBox.Text = "Демо: Холмистые кластеры";
                    if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = 3;

                    if (_widthBox != null) _widthBox.Text = "24";
                    if (_heightBox != null) _heightBox.Text = "24";
                    if (_fireCellsBox != null) _fireCellsBox.Text = "2";

                    if (_moistureMinBox != null) _moistureMinBox.Text = "0.20";
                    if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.56";
                    if (_elevationBox != null) _elevationBox.Text = "125";

                    if (_stepsBox != null) _stepsBox.Text = "100";
                    if (_stepDurationBox != null) _stepDurationBox.Text = "900";

                    if (_tempBox != null) _tempBox.Text = "27";
                    if (_humidityBox != null) _humidityBox.Text = "37";
                    if (_windSpeedBox != null) _windSpeedBox.Text = "6";
                    if (_windDirBox != null) _windDirBox.SelectedIndex = 3;
                    if (_precipitationBox != null) _precipitationBox.Text = "0";

                    if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.06";
                    if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.00";
                    if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.35";
                    if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.00";
                    if (_randomSeedBox != null) _randomSeedBox.Text = "605";

                    SetVegetationDistributionTexts(0.24, 0.18, 0.24, 0.12, 0.14, 0.04, 0.04);
                    break;

                default:
                    return;
            }
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

        if (IsGridDialog())
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

        SelectedScenarioType = null;
        SelectedClusteredScenarioType = null;

        if (SelectedMapCreationMode == MapCreationMode.Scenario)
        {
            if (IsGridDialog())
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
            else if (IsClusteredDialog())
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
            if (IsGridDialog())
            {
                if (MapRegionObjects.Count == 0)
                {
                    ShowError("Для полуручного режима сетки нужно добавить хотя бы один объект карты.");
                    return;
                }
            }
            else if (IsClusteredDialog())
            {
                if (ClusteredBlueprint == null || ClusteredBlueprint.Nodes.Count == 0)
                {
                    ShowError("Для кластерного графа нужен отдельный редактор узлов и связей. Пока clustered blueprint ещё не задан.");
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
                ShowError("Сначала задайте корректные размеры рабочей области clustered graph.");
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

        ShowError("Для этого режима редактор пока не подключён.");
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
        bool isGrid = IsGridDialog();
        bool isClustered = IsClusteredDialog();
        bool isRegion = _page == AppPage.Graph && _mode == GraphCreationMode.RegionCluster;

        if (_typeInfoTextBlock != null)
        {
            _typeInfoTextBlock.Text = isGrid
                ? "Сеточная симуляция"
                : isClustered
                    ? "Кластерный граф"
                    : "Региональный граф";
        }

        if (_typeHintTextBlock != null)
        {
            _typeHintTextBlock.Text = isGrid
                ? "Будет создана клеточная модель лесной территории. Сценарии и полуручное создание напрямую формируют карту клеток."
                : isClustered
                    ? "Будет создан самостоятельный кластерный граф. Сценарии для него задают патчи, узлы, плотность внутренних связей и межкластерные переходы."
                    : "Будет создан региональный граф. Эта ветка пока не перерабатывается в рамках текущего этапа.";
        }

        if (_widthLabelTextBlock != null)
            _widthLabelTextBlock.Text = isGrid ? "Ширина сетки" : "Ширина рабочей области";

        if (_heightLabelTextBlock != null)
            _heightLabelTextBlock.Text = isGrid ? "Высота сетки" : "Высота рабочей области";

        if (_widthHintTextBlock != null)
        {
            _widthHintTextBlock.Text = isGrid
                ? "Количество клеток по горизонтали."
                : isClustered
                    ? "Размер условной области размещения патчей и узлов."
                    : "Размер условной области регионов.";
        }

        if (_heightHintTextBlock != null)
        {
            _heightHintTextBlock.Text = isGrid
                ? "Количество клеток по вертикали."
                : isClustered
                    ? "Размер условной области размещения патчей и узлов."
                    : "Размер условной области регионов.";
        }

        if (_fireCellsHintTextBlock != null)
        {
            _fireCellsHintTextBlock.Text = isGrid
                ? "Сколько клеток загорится в начале симуляции."
                : "Сколько узлов загорится в начале симуляции.";
        }

        if (_semiManualDescriptionTextBlock != null)
        {
            if (SelectedMapCreationMode != MapCreationMode.SemiManual)
            {
                _semiManualDescriptionTextBlock.Text = string.Empty;
            }
            else if (isGrid)
            {
                _semiManualDescriptionTextBlock.Text = MapRegionObjects.Count == 0
                    ? "Объекты карты ещё не добавлены. Откройте редактор карты и задайте области вручную."
                    : $"Созданные области напрямую изменят клеточную карту.{Environment.NewLine}{BuildMapObjectsDetailedSummary()}";
            }
            else if (isClustered)
            {
                _semiManualDescriptionTextBlock.Text = ClusteredBlueprint == null || ClusteredBlueprint.Nodes.Count == 0
                    ? "Для кластерного графа нужен отдельный редактор узлов и связей. Сеточный редактор сюда больше не применяется."
                    : $"Подготовлен clustered blueprint: узлов — {ClusteredBlueprint.Nodes.Count}, рёбер — {ClusteredBlueprint.Edges.Count}.";
            }
            else
            {
                _semiManualDescriptionTextBlock.Text =
                    "Региональный граф пока не переводим на отдельный редактор в рамках этого этапа.";
            }
        }

        if (_openMapEditorButton != null)
        {
            _openMapEditorButton.Content = isGrid
                ? "Открыть редактор карты"
                : isClustered
                    ? "Открыть редактор кластерного графа"
                    : "Открыть редактор территории";
        }

        UpdateScenarioDescription();
        UpdateMapEditorSummary();
    }
    private void UpdateMapModeUi()
    {
        bool isGrid = IsGridDialog();
        bool isClustered = IsClusteredDialog();

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
                MapCreationMode.Random => isGrid
                    ? "Карта будет сформирована автоматически по текущим параметрам и распределениям."
                    : isClustered
                        ? "Кластерный граф будет сформирован автоматически: генератор сам создаст патчи, узлы и связи."
                        : "Структура будет создана автоматически.",

                MapCreationMode.Scenario => isGrid
                    ? "Будет применён готовый сценарий клеточной карты."
                    : isClustered
                        ? "Будет применён специализированный сценарий кластерного графа: он задаст плотность патчей, их свойства и межкластерные переходы."
                        : "Будет применён готовый сценарий.",

                MapCreationMode.SemiManual => isGrid
                    ? "Вы вручную задаёте области карты, которые напрямую изменяют сетку."
                    : isClustered
                        ? "Для clustered graph нужен отдельный редактор узлов и связей. Старый редактор карты сюда больше не применяется."
                        : "Этот режим будет вынесен отдельно.",

                _ => "Карта будет сформирована автоматически."
            };
        }

        if (_scenarioTypeBox != null)
        {
            _scenarioTypeBox.Items.Clear();

            if (SelectedMapCreationMode == MapCreationMode.Scenario)
            {
                if (isGrid)
                {
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Смешанный лес" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Сухой хвойный массив" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Лес с рекой" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Лес с озером" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Лес с просекой" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Холмистая местность" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Влажный лес после дождя" });
                }
                else if (isClustered)
                {
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Плотный сухой хвойный массив" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Кластеры, разделённые водным барьером" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Кластеры с просекой / разрывом" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Холмистые кластеры" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Влажные патчи после дождя" });
                    _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Смешанные патчи с очагами сухости" });
                }

                _scenarioTypeBox.SelectedIndex = 0;
            }
        }

        if (_openMapEditorButton != null)
            _openMapEditorButton.IsVisible = SelectedMapCreationMode == MapCreationMode.SemiManual;

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

    private void UpdateMapEditorSummary()
    {
        if (_mapEditorSummaryTextBlock == null)
            return;

        bool isGrid = IsGridDialog();
        bool isClustered = IsClusteredDialog();

        if (SelectedMapCreationMode != MapCreationMode.SemiManual)
        {
            _mapEditorSummaryTextBlock.Text = isGrid
                ? "Редактор карты не используется."
                : isClustered
                    ? "Редактор кластерного графа не используется."
                    : "Редактор не используется.";
            return;
        }

        if (isGrid)
        {
            int count = MapRegionObjects?.Count ?? 0;

            _mapEditorSummaryTextBlock.Text = count == 0
                ? "Объекты карты ещё не заданы."
                : $"Задано объектов карты: {count}.";
            return;
        }

        if (isClustered)
        {
            if (ClusteredBlueprint == null)
            {
                _mapEditorSummaryTextBlock.Text =
                    "Clustered blueprint ещё не подготовлен. Следующим шагом подключим отдельный редактор узлов и связей.";
                return;
            }

            _mapEditorSummaryTextBlock.Text =
                $"Подготовлен clustered blueprint: узлов — {ClusteredBlueprint.Nodes.Count}, рёбер — {ClusteredBlueprint.Edges.Count}.";
            return;
        }

        _mapEditorSummaryTextBlock.Text =
            "Для этой структуры отдельный редактор пока не подключён.";
    }
    private void UpdateScenarioDescription()
    {
        if (_scenarioDescriptionTextBlock == null)
            return;

        if (SelectedMapCreationMode != MapCreationMode.Scenario)
        {
            _scenarioDescriptionTextBlock.Text = string.Empty;
            return;
        }

        bool isGrid = IsGridDialog();
        bool isClustered = IsClusteredDialog();

        if (isGrid)
        {
            var gridScenario = _scenarioTypeBox?.SelectedIndex switch
            {
                1 => MapScenarioType.DryConiferousMassif,
                2 => MapScenarioType.ForestWithRiver,
                3 => MapScenarioType.ForestWithLake,
                4 => MapScenarioType.ForestWithFirebreak,
                5 => MapScenarioType.HillyTerrain,
                6 => MapScenarioType.WetForestAfterRain,
                _ => MapScenarioType.MixedForest
            };

            _scenarioDescriptionTextBlock.Text = gridScenario switch
            {
                MapScenarioType.MixedForest => "Сбалансированный лесной ландшафт с несколькими типами растительности.",
                MapScenarioType.DryConiferousMassif => "Сухая хвойная территория с высокой горючестью и быстрым распространением.",
                MapScenarioType.ForestWithRiver => "На карте формируется речной барьер, который сдерживает распространение огня.",
                MapScenarioType.ForestWithLake => "На карте формируется озеро и влажная прибрежная зона.",
                MapScenarioType.ForestWithFirebreak => "На карте формируется просека, разрывающая фронт огня.",
                MapScenarioType.HillyTerrain => "Карта получает выраженный рельеф и локальные высотные зоны.",
                MapScenarioType.WetForestAfterRain => "Лес после дождя: повышенная влажность и замедленное развитие пожара.",
                _ => "Сценарий клеточной карты."
            };

            return;
        }

        if (isClustered)
        {
            var clusteredScenario = _scenarioTypeBox?.SelectedIndex switch
            {
                1 => ClusteredScenarioType.WaterBarrier,
                2 => ClusteredScenarioType.FirebreakGap,
                3 => ClusteredScenarioType.HillyClusters,
                4 => ClusteredScenarioType.WetAfterRain,
                5 => ClusteredScenarioType.MixedDryHotspots,
                _ => ClusteredScenarioType.DenseDryConiferous
            };

            _scenarioDescriptionTextBlock.Text = clusteredScenario switch
            {
                ClusteredScenarioType.DenseDryConiferous =>
                    "Много плотных хвойных патчей, низкая влажность, сильные внутренние связи и хорошие межкластерные переходы.",

                ClusteredScenarioType.WaterBarrier =>
                    "Часть патчей отделена барьером. Между группами мало переходов, и распространение через них ослаблено.",

                ClusteredScenarioType.FirebreakGap =>
                    "Кластеры разделены разрывом или слабой перемычкой. Огонь хорошо идёт внутри патча, но хуже переходит между группами.",

                ClusteredScenarioType.HillyClusters =>
                    "Кластеры имеют более сильную неоднородность по высоте. Это влияет на связность и на направление распространения.",

                ClusteredScenarioType.WetAfterRain =>
                    "Патчи после дождя: повышенная влажность, более слабая горючесть и ослабленные переходы.",

                ClusteredScenarioType.MixedDryHotspots =>
                    "Смешанный фон с локальными сухими патчами — внутри них распространение заметно сильнее, чем в остальном графе.",

                _ => "Сценарий кластерного графа."
            };

            return;
        }

        _scenarioDescriptionTextBlock.Text =
            "Для регионального графа специализированные сценарии будут выделены отдельно на следующем этапе.";
    }
    private void UpdateStructurePreview()
    {
        int width = ParseInt(_widthBox?.Text, 20);
        int height = ParseInt(_heightBox?.Text, 20);
        int initialFireCells = ParseInt(_fireCellsBox?.Text, 3);

        width = Math.Max(1, width);
        height = Math.Max(1, height);
        initialFireCells = Math.Max(1, initialFireCells);

        bool isGrid = IsGridDialog();
        bool isClustered = IsClusteredDialog();
        bool isRegion = _page == AppPage.Graph && _mode == GraphCreationMode.RegionCluster;

        int area = width * height;

        if (isGrid)
        {
            if (_structureSummaryTextBlock != null)
                _structureSummaryTextBlock.Text =
                    $"Сетка {width}×{height} = {area} клеток. Начальных очагов: {initialFireCells}.";

            if (_structureDetailTextBlock != null)
                _structureDetailTextBlock.Text =
                    "Параметры напрямую определяют размер клеточной карты и число участков поверхности.";
            return;
        }

        if (isClustered)
        {
            int estimatedNodes = Math.Max(10, area / 2);
            int estimatedPatches =
                estimatedNodes <= 20 ? 3 :
                estimatedNodes <= 60 ? 4 :
                estimatedNodes <= 140 ? 5 :
                estimatedNodes <= 260 ? 6 : 7;

            if (SelectedMapCreationMode == MapCreationMode.SemiManual &&
                ClusteredBlueprint != null &&
                ClusteredBlueprint.Nodes.Count > 0)
            {
                int manualNodes = ClusteredBlueprint.Nodes.Count;
                int manualEdges = ClusteredBlueprint.Edges.Count;
                int manualClusters = ClusteredBlueprint.Nodes
                    .Select(n => n.ClusterId?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                if (_structureSummaryTextBlock != null)
                {
                    _structureSummaryTextBlock.Text =
                        $"Clustered blueprint: узлов {manualNodes}, рёбер {manualEdges}, групп {manualClusters}. Начальных очагов: {initialFireCells}.";
                }

                if (_structureDetailTextBlock != null)
                {
                    _structureDetailTextBlock.Text =
                        "Граф будет построен напрямую из выбранных узлов и связей. Здесь важны топология, длина рёбер и свойства самих вершин.";
                }

                return;
            }

            if (_structureSummaryTextBlock != null)
            {
                _structureSummaryTextBlock.Text =
                    $"Рабочая область {width}×{height}. Ожидается около {estimatedNodes} узлов, {estimatedPatches} патчей и локально-плотная связность. Начальных очагов: {initialFireCells}.";
            }

            if (_structureDetailTextBlock != null)
            {
                _structureDetailTextBlock.Text =
                    SelectedMapCreationMode switch
                    {
                        MapCreationMode.Random =>
                            "Random clustered graph: генератор сам создаёт патчи, размещает узлы и строит локальные рёбра с межкластерными мостами.",

                        MapCreationMode.Scenario =>
                            "Scenario clustered graph: сценарий задаёт структуру патчей, влажность, доминирующую растительность и силу межкластерных переходов.",

                        MapCreationMode.SemiManual =>
                            "Semi-manual clustered graph: структура задаётся через clustered editor — выбор узлов, построение рёбер и настройку свойств вершин и связей.",

                        _ =>
                            "Clustered graph формируется как самостоятельный граф, а не как побочный продукт клеточной карты."
                    };
            }

            return;
        }

        if (_structureSummaryTextBlock != null)
        {
            _structureSummaryTextBlock.Text =
                $"Рабочая область {width}×{height}. Региональный граф пока оставляем без изменений. Начальных очагов: {initialFireCells}.";
        }

        if (_structureDetailTextBlock != null)
        {
            _structureDetailTextBlock.Text =
                "RegionClusterGraph будет исправляться отдельным этапом. На текущем шаге его UI и логика не перерабатываются.";
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