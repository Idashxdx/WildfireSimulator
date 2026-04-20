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
        UpdateStructurePreview();
        UpdateMapModeUi();
        UpdateScenarioDescription();
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

    private void ConfigureScenarioItems()
    {
        if (_scenarioTypeBox == null)
            return;

        _scenarioTypeBox.Items.Clear();
        _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Смешанный лес" });
        _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Сухой хвойный массив" });
        _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Лес с рекой" });
        _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Лес с озером" });
        _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Лес с просекой" });
        _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Холмистая местность" });
        _scenarioTypeBox.Items.Add(new ComboBoxItem { Content = "Влажный лес после дождя" });

        _scenarioTypeBox.SelectedIndex = 0;
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

        if (_presetButton1 != null) _presetButton1.Click += OnPresetClicked;
        if (_presetButton2 != null) _presetButton2.Click += OnPresetClicked;
        if (_presetButton3 != null) _presetButton3.Click += OnPresetClicked;
        if (_presetButton4 != null) _presetButton4.Click += OnPresetClicked;
        if (_presetButton5 != null) _presetButton5.Click += OnPresetClicked;

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

    private void ApplyDefaults()
    {
        if (_nameBox != null)
            _nameBox.Text = "Сеточная симуляция";

        if (_widthBox != null)
            _widthBox.Text = "20";

        if (_heightBox != null)
            _heightBox.Text = "20";

        if (_fireCellsBox != null)
            _fireCellsBox.Text = "3";

        if (_moistureMinBox != null)
            _moistureMinBox.Text = "0.30";

        if (_moistureMaxBox != null)
            _moistureMaxBox.Text = "0.70";

        if (_elevationBox != null)
            _elevationBox.Text = "50";

        if (_stepsBox != null)
            _stepsBox.Text = "100";

        if (_stepDurationBox != null)
            _stepDurationBox.Text = "900";

        if (_tempBox != null)
            _tempBox.Text = "25";

        if (_humidityBox != null)
            _humidityBox.Text = "40";

        if (_windSpeedBox != null)
            _windSpeedBox.Text = "5";

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
    }

    private void UpdatePresetButtonsUi()
    {
        if (_presetHintTextBlock == null)
            return;

        _presetHintTextBlock.Text =
            "Готовые территориальные сценарии для сетки. Кнопка сразу подставляет режим, сценарий, погодные условия и параметры карты.";

        if (_presetButton1 != null)
        {
            _presetButton1.Content = "Сухой хвойный + ветер";
            _presetButton1.Tag = "dry-coniferous";
        }

        if (_presetButton2 != null)
        {
            _presetButton2.Content = "Река как барьер";
            _presetButton2.Tag = "river";
        }

        if (_presetButton3 != null)
        {
            _presetButton3.Content = "Влажный лес";
            _presetButton3.Tag = "wet";
        }

        if (_presetButton4 != null)
        {
            _presetButton4.Content = "Просека";
            _presetButton4.Tag = "firebreak";
        }

        if (_presetButton5 != null)
        {
            _presetButton5.Content = "Холмы";
            _presetButton5.Tag = "hills";
        }
    }

    private void UpdateMapModeUi()
    {
        if (_mapCreationModeBox == null)
            return;

        SelectedMapCreationMode = (MapCreationMode)_mapCreationModeBox.SelectedIndex;

        if (_scenarioPanel != null)
            _scenarioPanel.IsVisible = SelectedMapCreationMode == MapCreationMode.Scenario;

        if (_semiManualPanel != null)
            _semiManualPanel.IsVisible = SelectedMapCreationMode == MapCreationMode.SemiManual;

        if (_mapModeDescriptionTextBlock != null)
        {
            _mapModeDescriptionTextBlock.Text = SelectedMapCreationMode switch
            {
                MapCreationMode.Random =>
                    "Карта будет сформирована автоматически по текущим параметрам и распределениям.",
                MapCreationMode.Scenario =>
                    "Будет использован готовый территориальный сценарий сетки: река, озеро, просека, влажный лес или холмистый рельеф.",
                MapCreationMode.SemiManual =>
                    "Полуручной режим сетки работает через редактор областей карты: можно нарисовать воду, просеки, влажные и сухие зоны, холмы и низины.",
                _ =>
                    "Карта будет сформирована автоматически."
            };
        }

        if (_semiManualDescriptionTextBlock != null)
        {
            _semiManualDescriptionTextBlock.Text =
                SelectedMapCreationMode == MapCreationMode.SemiManual
                    ? "Откроется редактор карты. В нём можно рисовать области леса, воды, просеки, влажные и сухие зоны, холмы и низины. Эти объекты изменяют параметры клеточной территории."
                    : string.Empty;
        }

        if (_openMapEditorButton != null)
        {
            _openMapEditorButton.Content = SelectedMapCreationMode == MapCreationMode.SemiManual
                ? "Открыть редактор карты"
                : "Редактор недоступен";

            _openMapEditorButton.IsEnabled = SelectedMapCreationMode == MapCreationMode.SemiManual;
        }

        UpdateMapEditorSummary();
    }

    private void UpdateScenarioDescription()
    {
        if (_scenarioTypeBox == null)
            return;

        SelectedScenarioType = (MapScenarioType)_scenarioTypeBox.SelectedIndex;

        if (_scenarioDescriptionTextBlock == null)
            return;

        _scenarioDescriptionTextBlock.Text = SelectedScenarioType switch
        {
            MapScenarioType.MixedForest =>
                "Сбалансированный лесной ландшафт с несколькими типами растительности.",
            MapScenarioType.DryConiferousMassif =>
                "Сухой хвойный массив с повышенной горимостью и быстрым фронтом распространения.",
            MapScenarioType.ForestWithRiver =>
                "Сценарий с рекой, которая ослабляет перенос огня и меняет локальную влажность.",
            MapScenarioType.ForestWithLake =>
                "Крупный водоём создаёт барьер и более влажную прибрежную зону.",
            MapScenarioType.ForestWithFirebreak =>
                "Просека и прилегающие области снижают вероятность непрерывного распространения.",
            MapScenarioType.HillyTerrain =>
                "Холмистый рельеф изменяет локальные условия переноса тепла и подъёма огня.",
            MapScenarioType.WetForestAfterRain =>
                "Повышенная влажность существенно замедляет воспламенение и рост площади пожара.",
            _ =>
                "Сценарий сетки выбран."
        };
    }

    private void UpdateMapEditorSummary()
    {
        if (_mapEditorSummaryTextBlock == null)
            return;

        if (SelectedMapCreationMode != MapCreationMode.SemiManual)
        {
            _mapEditorSummaryTextBlock.Text = "Полуручный редактор сетки не используется.";
            return;
        }

        int count = MapRegionObjects?.Count ?? 0;

        if (count == 0)
        {
            _mapEditorSummaryTextBlock.Text =
                "Объекты карты ещё не заданы. Для сеточного полуручного режима нужно добавить хотя бы одну область.";
            return;
        }

        var grouped = MapRegionObjects
            .GroupBy(x => x.ObjectType)
            .OrderBy(g => g.Key.ToString())
            .Select(g => $"{GetMapObjectTypeName(g.Key)}: {g.Count()}")
            .ToList();

        _mapEditorSummaryTextBlock.Text =
            $"Добавлено объектов: {count}" + Environment.NewLine +
            string.Join(Environment.NewLine, grouped);
    }

    private async Task OpenMapEditorAsync()
    {
        if (SelectedMapCreationMode != MapCreationMode.SemiManual)
            return;

        int width = ParseInt(_widthBox?.Text, 20);
        int height = ParseInt(_heightBox?.Text, 20);

        var editor = new MapEditorDialog(width, height, MapRegionObjects);
        var result = await editor.ShowDialog<bool>(this);

        if (!result)
            return;

        MapRegionObjects = editor.EditedObjects
            .Select(CloneMapObject)
            .ToList();

        UpdateMapEditorSummary();
        UpdateStructurePreview();
        ClearErrors();
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
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)MapScenarioType.DryConiferousMassif;
                if (_windSpeedBox != null) _windSpeedBox.Text = "8";
                if (_tempBox != null) _tempBox.Text = "31";
                if (_humidityBox != null) _humidityBox.Text = "22";
                break;

            case "river":
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)MapScenarioType.ForestWithRiver;
                break;

            case "wet":
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)MapScenarioType.WetForestAfterRain;
                if (_precipitationBox != null) _precipitationBox.Text = "2.5";
                break;

            case "firebreak":
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)MapScenarioType.ForestWithFirebreak;
                break;

            case "hills":
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)MapScenarioType.HillyTerrain;
                break;
        }

        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateMapEditorSummary();
        UpdateStructurePreview();
    }

    private void OnCreateClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryCollectValues())
            return;

        Close(true);
    }

    private bool TryCollectValues()
    {
        ClearErrors();

        SimulationName = _nameBox?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(SimulationName))
            SimulationName = "Сеточная симуляция";

        GridWidth = ParseInt(_widthBox?.Text, 20);
        GridHeight = ParseInt(_heightBox?.Text, 20);
        InitialFireCells = ParseInt(_fireCellsBox?.Text, 3);

        MoistureMin = ParseDouble(_moistureMinBox?.Text, 0.30);
        MoistureMax = ParseDouble(_moistureMaxBox?.Text, 0.70);
        ElevationVariation = ParseDouble(_elevationBox?.Text, 50.0);

        SimulationSteps = ParseInt(_stepsBox?.Text, 100);
        StepDurationSeconds = ParseInt(_stepDurationBox?.Text, 900);

        Temperature = ParseDouble(_tempBox?.Text, 25.0);
        Humidity = ParseDouble(_humidityBox?.Text, 40.0);
        WindSpeed = ParseDouble(_windSpeedBox?.Text, 5.0);
        WindDirection = GetWindDirectionDegrees();
        Precipitation = ParseDouble(_precipitationBox?.Text, 0.0);

        var seedText = _randomSeedBox?.Text?.Trim();
        RandomSeed = int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed)
            ? seed
            : null;

        MapNoiseStrength = ParseDouble(_mapNoiseBox?.Text, 0.08);
        MapDrynessFactor = ParseDouble(_mapDrynessBox?.Text, 1.0);
        ReliefStrengthFactor = ParseDouble(_reliefStrengthBox?.Text, 1.0);
        FuelDensityFactor = ParseDouble(_fuelDensityBox?.Text, 1.0);

        SelectedMapCreationMode = _mapCreationModeBox != null
            ? (MapCreationMode)_mapCreationModeBox.SelectedIndex
            : MapCreationMode.Random;

        SelectedScenarioType = _scenarioTypeBox != null
            ? (MapScenarioType)_scenarioTypeBox.SelectedIndex
            : MapScenarioType.MixedForest;

        VegetationDistributions = BuildVegetationDistributions();

        if (GridWidth < 5 || GridHeight < 5)
        {
            ShowError("Размер сетки должен быть не меньше 5x5.");
            return false;
        }

        if (MoistureMin > MoistureMax)
        {
            ShowError("Минимальная влажность не должна быть больше максимальной.");
            return false;
        }

        if (InitialFireCells < 1)
        {
            ShowError("Количество стартовых очагов должно быть не меньше 1.");
            return false;
        }

        if (SelectedMapCreationMode == MapCreationMode.SemiManual &&
            (MapRegionObjects == null || MapRegionObjects.Count == 0))
        {
            ShowError("Для полуручного режима сетки нужно добавить хотя бы один объект карты.");
            return false;
        }

        return true;
    }

    private List<(int VegetationType, double Probability)> BuildVegetationDistributions()
    {
        var values = new List<(VegetationType Type, double Value)>
        {
            (VegetationType.Coniferous, ParseDouble(_coniferousBox?.Text, 0.25)),
            (VegetationType.Deciduous, ParseDouble(_deciduousBox?.Text, 0.20)),
            (VegetationType.Mixed, ParseDouble(_mixedBox?.Text, 0.20)),
            (VegetationType.Grass, ParseDouble(_grassBox?.Text, 0.15)),
            (VegetationType.Shrub, ParseDouble(_shrubBox?.Text, 0.10)),
            (VegetationType.Water, ParseDouble(_waterBox?.Text, 0.05)),
            (VegetationType.Bare, ParseDouble(_bareBox?.Text, 0.05))
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

        int width = ParseInt(_widthBox?.Text, 20);
        int height = ParseInt(_heightBox?.Text, 20);
        int cells = Math.Max(1, width) * Math.Max(1, height);

        _structureSummaryTextBlock.Text = $"Сетка {width}×{height} • клеток: {cells}";

        _structureDetailTextBlock.Text = SelectedMapCreationMode switch
        {
            MapCreationMode.Random =>
                "Сетка будет сгенерирована автоматически по распределению растительности и макропараметрам.",
            MapCreationMode.Scenario =>
                "Будет использован готовый сеточный сценарий.",
            MapCreationMode.SemiManual =>
                $"Полуручной режим: объектов карты {MapRegionObjects.Count}.",
            _ =>
                "Структура сетки будет создана автоматически."
        };
    }

    private static int ParseInt(string? text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static double ParseDouble(string? text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private double GetWindDirectionDegrees()
    {
        if (_windDirBox == null)
            return 45.0;

        return _windDirBox.SelectedIndex switch
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

    private void ClearErrors()
    {
        if (_errorTextBlock != null)
            _errorTextBlock.Text = string.Empty;
    }

    private void ShowError(string message)
    {
        if (_errorTextBlock != null)
            _errorTextBlock.Text = message;
    }

    private static MapRegionObjectDto CloneMapObject(MapRegionObjectDto source)
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

    private static string GetMapObjectTypeName(MapObjectType type)
    {
        return type switch
        {
            MapObjectType.ConiferousArea => "Хвойная область",
            MapObjectType.DeciduousArea => "Лиственная область",
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
}