using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WildfireSimulator.Client.Models;
using WildfireSimulator.Client.ViewModels;

namespace WildfireSimulator.Client.Views;

public partial class CreateGraphSimulationDialog : Window
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
    public double MapNoiseStrength { get; private set; } = 0.08;
    public double MapDrynessFactor { get; private set; } = 1.0;
    public double ReliefStrengthFactor { get; private set; } = 1.0;
    public double FuelDensityFactor { get; private set; } = 1.0;

    public List<(int VegetationType, double Probability)> VegetationDistributions { get; private set; } = new();
    public ClusteredScenarioType? SelectedClusteredScenarioType { get; private set; }
    public ClusteredGraphBlueprintDto? ClusteredBlueprint { get; private set; }

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

    private Button? _randomMapButton;
    private Button? _presetButton1;
    private Button? _presetButton2;
    private Button? _presetButton3;
    private Button? _presetButton4;
    private Button? _presetButton5;
    private Button? _presetButton6;
    private Button? _presetButton7;

    private Button? _createButton;
    private Button? _cancelButton;

    public CreateGraphSimulationDialog(GraphCreationMode mode)
    {
        _mode = mode;

        InitializeComponent();
        FindControls();
        ConfigureScenarioItems();
        AttachEvents();
        ApplyDefaults();
        ApplyModeTexts();
        UpdatePresetButtonsUi();
        UpdateStructurePreview();
        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateMapEditorSummary();
        ClearErrors();

        Title = mode switch
        {
            GraphCreationMode.Small => "Создание симуляции: малый граф",
            GraphCreationMode.Medium => "Создание симуляции: средний граф",
            GraphCreationMode.Large => "Создание симуляции: большой граф",
            _ => "Создание графовой симуляции"
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public SimulationCreationResult GetResult()
    {
        var graphScaleType = _mode switch
        {
            GraphCreationMode.Small => GraphScaleType.Small,
            GraphCreationMode.Medium => GraphScaleType.Medium,
            GraphCreationMode.Large => GraphScaleType.Large,
            _ => GraphScaleType.Medium
        };

        var mapCreationMode = _mode == GraphCreationMode.Large
            ? SelectedMapCreationMode
            : MapCreationMode.Random;

        var clusteredScenarioType =
            mapCreationMode == MapCreationMode.Scenario && _mode == GraphCreationMode.Large
                ? SelectedClusteredScenarioType
                : null;

        var clusteredBlueprint =
            mapCreationMode == MapCreationMode.SemiManual && _mode == GraphCreationMode.Large
                ? ClusteredBlueprint
                : null;

        return new SimulationCreationResult
        {
            GraphType = GraphType.ClusteredGraph,
            GraphScaleType = graphScaleType,

            SimulationName = SimulationName,

            GridWidth = GridWidth,
            GridHeight = GridHeight,
            InitialFireCells = InitialFireCells,

            MoistureMin = MoistureMin,
            MoistureMax = MoistureMax,
            ElevationVariation = ElevationVariation,

            SimulationSteps = SimulationSteps,
            StepDurationSeconds = StepDurationSeconds,

            Temperature = Temperature,
            Humidity = Humidity,
            WindSpeed = WindSpeed,
            WindDirection = WindDirection,
            Precipitation = Precipitation,

            RandomSeed = RandomSeed,

            SelectedMapCreationMode = mapCreationMode,
            SelectedScenarioType = null,
            SelectedClusteredScenarioType = clusteredScenarioType,

            MapNoiseStrength = MapNoiseStrength,
            MapDrynessFactor = MapDrynessFactor,
            ReliefStrengthFactor = ReliefStrengthFactor,
            FuelDensityFactor = FuelDensityFactor,

            VegetationDistributions = new List<(int VegetationType, double Probability)>(VegetationDistributions),
            ClusteredBlueprint = clusteredBlueprint
        };
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
        _mapEditorSummaryTextBlock = this.FindControl<TextBlock>("MapEditorSummaryTextBlock");
        _presetHintTextBlock = this.FindControl<TextBlock>("PresetHintTextBlock");

        _randomMapButton = this.FindControl<Button>("RandomMapButton");
        _presetButton1 = this.FindControl<Button>("PresetButton1");
        _presetButton2 = this.FindControl<Button>("PresetButton2");
        _presetButton3 = this.FindControl<Button>("PresetButton3");
        _presetButton4 = this.FindControl<Button>("PresetButton4");
        _presetButton5 = this.FindControl<Button>("PresetButton5");
        _presetButton6 = this.FindControl<Button>("PresetButton6");
        _presetButton7 = this.FindControl<Button>("PresetButton7");

        _createButton = this.FindControl<Button>("CreateButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
    }

    private void ConfigureScenarioItems()
    {
        if (_scenarioTypeBox == null)
            return;

        _scenarioTypeBox.ItemsSource = GetScenarioDisplayItems();
        _scenarioTypeBox.SelectedIndex = -1;
    }

    private List<string> GetScenarioDisplayItems()
    {
        return _mode == GraphCreationMode.Large
            ? new List<string>
            {
                "Смешанный лес",
                "Сухой хвойный + ветер",
                "Река как барьер",
                "Озеро и берег",
                "Влажный лес",
                "Просека",
                "Холмы"
            }
            : new List<string>();
    }

    private void AttachEvents()
    {
        AttachTextChanged(_nameBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_widthBox, () =>
        {
            UpdateStructurePreview();
            UpdateMapEditorSummary();
            ClearErrors();
        });

        AttachTextChanged(_heightBox, () =>
        {
            UpdateStructurePreview();
            UpdateMapEditorSummary();
            ClearErrors();
        });

        AttachTextChanged(_fireCellsBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_moistureMinBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_moistureMaxBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_elevationBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_stepsBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_stepDurationBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_tempBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_humidityBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_windSpeedBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_precipitationBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_randomSeedBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_mapNoiseBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_mapDrynessBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_reliefStrengthBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_fuelDensityBox, () =>
        {
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_coniferousBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            ClearErrors();
        });

        AttachTextChanged(_deciduousBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            ClearErrors();
        });

        AttachTextChanged(_mixedBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            ClearErrors();
        });

        AttachTextChanged(_grassBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            ClearErrors();
        });

        AttachTextChanged(_shrubBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            ClearErrors();
        });

        AttachTextChanged(_waterBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            ClearErrors();
        });

        AttachTextChanged(_bareBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            ClearErrors();
        });

        if (_mapCreationModeBox != null)
        {
            _mapCreationModeBox.SelectionChanged += (_, _) =>
            {
                SelectedMapCreationMode = _mapCreationModeBox.SelectedIndex switch
                {
                    1 => MapCreationMode.Scenario,
                    2 => MapCreationMode.SemiManual,
                    _ => MapCreationMode.Random
                };

                if (SelectedMapCreationMode != MapCreationMode.SemiManual)
                    ClusteredBlueprint = null;

                if (SelectedMapCreationMode != MapCreationMode.Scenario)
                {
                    SelectedClusteredScenarioType = null;

                    if (_scenarioTypeBox != null)
                        _scenarioTypeBox.SelectedIndex = -1;
                }

                UpdateMapModeUi();
                UpdateScenarioDescription();
                UpdateMapEditorSummary();
                UpdateStructurePreview();
                ClearErrors();
            };
        }

        if (_scenarioTypeBox != null)
        {
            _scenarioTypeBox.SelectionChanged += (_, _) =>
            {
                SelectedClusteredScenarioType = GetSelectedScenarioType();
                UpdateScenarioDescription();
                UpdateStructurePreview();
                ClearErrors();
            };
        }

        if (_windDirBox != null)
        {
            _windDirBox.SelectionChanged += (_, _) =>
            {
                UpdateStructurePreview();
                ClearErrors();
            };
        }

        if (_openMapEditorButton != null)
        {
            _openMapEditorButton.Click += async (_, _) =>
            {
                await OpenGraphEditorAsync();
            };
        }

        if (_randomMapButton != null)
            _randomMapButton.Click += (_, _) => ApplyRandomPreset();

        if (_presetButton1 != null)
            _presetButton1.Click += (_, _) => ApplyPresetFromButton(_presetButton1);

        if (_presetButton2 != null)
            _presetButton2.Click += (_, _) => ApplyPresetFromButton(_presetButton2);

        if (_presetButton3 != null)
            _presetButton3.Click += (_, _) => ApplyPresetFromButton(_presetButton3);

        if (_presetButton4 != null)
            _presetButton4.Click += (_, _) => ApplyPresetFromButton(_presetButton4);

        if (_presetButton5 != null)
            _presetButton5.Click += (_, _) => ApplyPresetFromButton(_presetButton5);

        if (_presetButton6 != null)
            _presetButton6.Click += (_, _) => ApplyPresetFromButton(_presetButton6);

        if (_presetButton7 != null)
            _presetButton7.Click += (_, _) => ApplyPresetFromButton(_presetButton7);

        if (_createButton != null)
        {
            _createButton.Click += (_, _) =>
            {
                if (!TryCollectValues(out var error))
                {
                    ShowError(error);
                    return;
                }

                Close(true);
            };
        }

        if (_cancelButton != null)
            _cancelButton.Click += (_, _) => Close(false);
    }

    private void AttachTextChanged(Control? control, Action handler)
    {
        switch (control)
        {
            case TextBox textBox:
                textBox.TextChanged += (_, _) => handler();
                break;

            case ComboBox comboBox:
                comboBox.SelectionChanged += (_, _) => handler();
                break;
        }
    }

    private void ApplyDefaults()
    {
        SimulationName = _mode switch
        {
            GraphCreationMode.Small => "Малый случайный граф",
            GraphCreationMode.Medium => "Средний случайный граф",
            GraphCreationMode.Large => "Большой случайный граф",
            _ => "Графовая симуляция"
        };

        GridWidth = _mode switch
        {
            GraphCreationMode.Small => 20,
            GraphCreationMode.Medium => 34,
            GraphCreationMode.Large => 56,
            _ => 34
        };

        GridHeight = _mode switch
        {
            GraphCreationMode.Small => 18,
            GraphCreationMode.Medium => 30,
            GraphCreationMode.Large => 46,
            _ => 30
        };

        InitialFireCells = _mode switch
        {
            GraphCreationMode.Small => 1,
            GraphCreationMode.Medium => 2,
            GraphCreationMode.Large => 3,
            _ => 2
        };

        MoistureMin = 0.24;
        MoistureMax = 0.62;
        ElevationVariation = _mode == GraphCreationMode.Large ? 90.0 : 55.0;
        SimulationSteps = 100;
        StepDurationSeconds = 900;
        Temperature = 26.0;
        Humidity = 38.0;
        WindSpeed = 5.0;
        WindDirection = 45.0;
        Precipitation = 0.0;
        RandomSeed = null;

        SelectedMapCreationMode = MapCreationMode.Random;
        SelectedClusteredScenarioType = null;
        ClusteredBlueprint = null;

        MapNoiseStrength = 0.08;
        MapDrynessFactor = 1.0;
        ReliefStrengthFactor = 1.0;
        FuelDensityFactor = 1.0;

        VegetationDistributions = new List<(int VegetationType, double Probability)>
        {
            ((int)VegetationType.Coniferous, 0.24),
            ((int)VegetationType.Deciduous, 0.18),
            ((int)VegetationType.Mixed, 0.28),
            ((int)VegetationType.Grass, 0.12),
            ((int)VegetationType.Shrub, 0.10),
            ((int)VegetationType.Water, 0.04),
            ((int)VegetationType.Bare, 0.04)
        };

        SetText(_nameBox, SimulationName);
        SetText(_widthBox, GridWidth.ToString(CultureInfo.InvariantCulture));
        SetText(_heightBox, GridHeight.ToString(CultureInfo.InvariantCulture));
        SetText(_fireCellsBox, InitialFireCells.ToString(CultureInfo.InvariantCulture));
        SetText(_moistureMinBox, MoistureMin.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_moistureMaxBox, MoistureMax.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_elevationBox, ElevationVariation.ToString("0", CultureInfo.InvariantCulture));
        SetText(_stepsBox, SimulationSteps.ToString(CultureInfo.InvariantCulture));
        SetText(_stepDurationBox, StepDurationSeconds.ToString(CultureInfo.InvariantCulture));
        SetText(_tempBox, Temperature.ToString("0", CultureInfo.InvariantCulture));
        SetText(_humidityBox, Humidity.ToString("0", CultureInfo.InvariantCulture));
        SetText(_windSpeedBox, WindSpeed.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_precipitationBox, Precipitation.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_randomSeedBox, string.Empty);

        if (_windDirBox != null)
            _windDirBox.SelectedIndex = 1;

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectedIndex = 0;

        if (_scenarioTypeBox != null)
            _scenarioTypeBox.SelectedIndex = -1;

        SetText(_mapNoiseBox, MapNoiseStrength.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_mapDrynessBox, MapDrynessFactor.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_reliefStrengthBox, ReliefStrengthFactor.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_fuelDensityBox, FuelDensityFactor.ToString("0.00", CultureInfo.InvariantCulture));

        WriteVegetationDistributionToInputs();
    }

    private void ApplyModeTexts()
    {
        if (_typeInfoTextBlock != null)
        {
            _typeInfoTextBlock.Text = _mode switch
            {
                GraphCreationMode.Small => "Малый случайный граф",
                GraphCreationMode.Medium => "Средний случайный граф",
                GraphCreationMode.Large => "Большой граф",
                _ => "Графовая симуляция"
            };
        }

        if (_typeHintTextBlock != null)
        {
            _typeHintTextBlock.Text = _mode switch
            {
                GraphCreationMode.Small =>
                    "Компактный случайный граф без демо-сценариев. Используется для быстрой проверки модели.",
                GraphCreationMode.Medium =>
                    "Средний граф создаётся случайно: отдельные непересекающиеся области соединяются мостами.",
                GraphCreationMode.Large =>
                    "Большой граф может строиться случайно или по демо-сценарию. Области могут пересекаться, также используются мосты и обходные пути.",
                _ =>
                    "Графовая симуляция распространения пожара."
            };
        }

        if (_widthLabelTextBlock != null)
            _widthLabelTextBlock.Text = "Ширина области размещения";

        if (_heightLabelTextBlock != null)
            _heightLabelTextBlock.Text = "Высота области размещения";

        if (_widthHintTextBlock != null)
            _widthHintTextBlock.Text = "Не количество вершин, а размер поля, внутри которого размещается граф.";

        if (_heightHintTextBlock != null)
            _heightHintTextBlock.Text = "Не количество вершин, а размер поля, внутри которого размещается граф.";

        if (_fireCellsHintTextBlock != null)
            _fireCellsHintTextBlock.Text = "Стартовые очаги будут выбраны случайно или вручную на визуализации графа.";
    }

    private void UpdatePresetButtonsUi()
    {
        bool showDemoButtons = _mode == GraphCreationMode.Large;

        SetPresetButtonVisible(_randomMapButton, showDemoButtons);
        SetPresetButtonVisible(_presetButton1, showDemoButtons);
        SetPresetButtonVisible(_presetButton2, showDemoButtons);
        SetPresetButtonVisible(_presetButton3, showDemoButtons);
        SetPresetButtonVisible(_presetButton4, showDemoButtons);
        SetPresetButtonVisible(_presetButton5, showDemoButtons);
        SetPresetButtonVisible(_presetButton6, showDemoButtons);
        SetPresetButtonVisible(_presetButton7, showDemoButtons);

        UpdatePresetButtonStyle(_randomMapButton, SelectedMapCreationMode == MapCreationMode.Random);
        UpdatePresetButtonStyle(_presetButton7, SelectedClusteredScenarioType == ClusteredScenarioType.MixedForest);
        UpdatePresetButtonStyle(_presetButton1, SelectedClusteredScenarioType == ClusteredScenarioType.DryConiferousMassif);
        UpdatePresetButtonStyle(_presetButton2, SelectedClusteredScenarioType == ClusteredScenarioType.ForestWithRiver);
        UpdatePresetButtonStyle(_presetButton6, SelectedClusteredScenarioType == ClusteredScenarioType.ForestWithLake);
        UpdatePresetButtonStyle(_presetButton3, SelectedClusteredScenarioType == ClusteredScenarioType.WetForestAfterRain);
        UpdatePresetButtonStyle(_presetButton4, SelectedClusteredScenarioType == ClusteredScenarioType.ForestWithFirebreak);
        UpdatePresetButtonStyle(_presetButton5, SelectedClusteredScenarioType == ClusteredScenarioType.HillyTerrain);

        if (_presetHintTextBlock != null)
        {
            _presetHintTextBlock.Text = _mode switch
            {
                GraphCreationMode.Large when SelectedMapCreationMode == MapCreationMode.Random =>
                    "Сейчас выбрана случайная карта",

                GraphCreationMode.Large when SelectedClusteredScenarioType.HasValue =>
                    $"Сейчас выбрано демо: {GetScenarioDisplayName(SelectedClusteredScenarioType.Value)}",

                GraphCreationMode.Large =>
                    "Выберите готовое демо или случайную карту",

                GraphCreationMode.Medium =>
                    "Средний граф создаётся случайно: отдельные непересекающиеся области соединяются мостами.",

                GraphCreationMode.Small =>
                    "Малый граф создаётся случайно для быстрой проверки модели.",

                _ =>
                    "Настройте параметры графа."
            };
        }
    }

    private static void UpdatePresetButtonStyle(Button? button, bool isActive)
    {
        if (button == null)
            return;

        button.Opacity = isActive ? 1.0 : 0.88;
    }

    private void ApplyRandomPreset()
    {
        ClearErrors();

        SelectedMapCreationMode = MapCreationMode.Random;
        SelectedClusteredScenarioType = null;
        ClusteredBlueprint = null;

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectedIndex = 0;

        if (_scenarioTypeBox != null)
            _scenarioTypeBox.SelectedIndex = -1;

        SetText(_nameBox, _mode switch
        {
            GraphCreationMode.Small => "Малый случайный граф",
            GraphCreationMode.Medium => "Средний случайный граф",
            GraphCreationMode.Large => "Большой случайный граф",
            _ => "Случайный граф"
        });

        SetText(_tempBox, "26");
        SetText(_humidityBox, "38");
        SetText(_windSpeedBox, "5");
        SetText(_precipitationBox, "0");
        SetText(_moistureMinBox, "0.24");
        SetText(_moistureMaxBox, "0.62");
        SetText(_mapNoiseBox, "0.08");
        SetText(_mapDrynessBox, "1.00");
        SetText(_reliefStrengthBox, "1.00");
        SetText(_fuelDensityBox, "1.00");

        SetVegetationDistribution(24, 18, 28, 12, 10, 4, 4);

        UpdateVegetationDistributionFromInputs();
        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateMapEditorSummary();
        UpdateStructurePreview();
        UpdatePresetButtonsUi();
    }

    private void ApplyPresetFromButton(Button? button)
    {
        if (button == null || _mode != GraphCreationMode.Large)
            return;

        ClearErrors();

        SelectedMapCreationMode = MapCreationMode.Scenario;
        ClusteredBlueprint = null;

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectedIndex = 1;

        ApplyCommonGraphDemoDefaults();

        var tag = button.Tag?.ToString()?.Trim().ToLowerInvariant();

        switch (tag)
        {
            case "mixed":
                SelectedClusteredScenarioType = ClusteredScenarioType.MixedForest;

                SetText(_nameBox, "ДЕМО: Смешанный лес");
                SetText(_tempBox, "26");
                SetText(_humidityBox, "38");
                SetText(_windSpeedBox, "5");
                SetText(_precipitationBox, "0");
                SetText(_moistureMinBox, "0.22");
                SetText(_moistureMaxBox, "0.62");
                SetText(_elevationBox, "85");
                SetText(_mapNoiseBox, "0.08");
                SetText(_mapDrynessBox, "1.00");
                SetText(_reliefStrengthBox, "1.00");
                SetText(_fuelDensityBox, "1.00");
                SetVegetationDistribution(18, 24, 40, 8, 10, 0, 0);
                SelectWindDirectionIndex(1);
                break;

            case "dry-coniferous":
                SelectedClusteredScenarioType = ClusteredScenarioType.DryConiferousMassif;

                SetText(_nameBox, "ДЕМО: Сухой хвойный + ветер");
                SetText(_tempBox, "31");
                SetText(_humidityBox, "22");
                SetText(_windSpeedBox, "8");
                SetText(_precipitationBox, "0");
                SetText(_moistureMinBox, "0.08");
                SetText(_moistureMaxBox, "0.32");
                SetText(_elevationBox, "95");
                SetText(_mapNoiseBox, "0.06");
                SetText(_mapDrynessBox, "1.20");
                SetText(_reliefStrengthBox, "1.00");
                SetText(_fuelDensityBox, "1.15");
                SetVegetationDistribution(76, 1, 14, 3, 6, 0, 0);
                SelectWindDirectionIndex(2);
                break;

            case "river":
                SelectedClusteredScenarioType = ClusteredScenarioType.ForestWithRiver;

                SetText(_nameBox, "ДЕМО: Река как барьер");
                SetText(_tempBox, "24");
                SetText(_humidityBox, "46");
                SetText(_windSpeedBox, "4");
                SetText(_precipitationBox, "0");
                SetText(_moistureMinBox, "0.22");
                SetText(_moistureMaxBox, "0.58");
                SetText(_elevationBox, "70");
                SetText(_mapNoiseBox, "0.04");
                SetText(_mapDrynessBox, "0.95");
                SetText(_reliefStrengthBox, "0.85");
                SetText(_fuelDensityBox, "0.96");
                SetVegetationDistribution(15, 32, 38, 6, 9, 0, 0);
                SelectWindDirectionIndex(1);
                break;

            case "lake":
                SelectedClusteredScenarioType = ClusteredScenarioType.ForestWithLake;

                SetText(_nameBox, "ДЕМО: Озеро и берег");
                SetText(_tempBox, "24");
                SetText(_humidityBox, "48");
                SetText(_windSpeedBox, "4");
                SetText(_precipitationBox, "0");
                SetText(_moistureMinBox, "0.24");
                SetText(_moistureMaxBox, "0.62");
                SetText(_elevationBox, "70");
                SetText(_mapNoiseBox, "0.04");
                SetText(_mapDrynessBox, "0.92");
                SetText(_reliefStrengthBox, "0.85");
                SetText(_fuelDensityBox, "0.96");
                SetVegetationDistribution(16, 30, 36, 8, 10, 0, 0);
                SelectWindDirectionIndex(1);
                break;

            case "wet":
                SelectedClusteredScenarioType = ClusteredScenarioType.WetForestAfterRain;

                SetText(_nameBox, "ДЕМО: Влажный лес");
                SetText(_tempBox, "21");
                SetText(_humidityBox, "72");
                SetText(_windSpeedBox, "3");
                SetText(_precipitationBox, "8");
                SetText(_moistureMinBox, "0.48");
                SetText(_moistureMaxBox, "0.82");
                SetText(_elevationBox, "60");
                SetText(_mapNoiseBox, "0.05");
                SetText(_mapDrynessBox, "0.70");
                SetText(_reliefStrengthBox, "0.90");
                SetText(_fuelDensityBox, "0.90");
                SetVegetationDistribution(10, 32, 38, 8, 12, 0, 0);
                SelectWindDirectionIndex(1);
                break;

            case "firebreak":
                SelectedClusteredScenarioType = ClusteredScenarioType.ForestWithFirebreak;

                SetText(_nameBox, "ДЕМО: Просека");
                SetText(_tempBox, "28");
                SetText(_humidityBox, "32");
                SetText(_windSpeedBox, "6");
                SetText(_precipitationBox, "0");
                SetText(_moistureMinBox, "0.16");
                SetText(_moistureMaxBox, "0.46");
                SetText(_elevationBox, "75");
                SetText(_mapNoiseBox, "0.05");
                SetText(_mapDrynessBox, "1.08");
                SetText(_reliefStrengthBox, "0.95");
                SetText(_fuelDensityBox, "1.06");
                SetVegetationDistribution(24, 18, 40, 8, 10, 0, 0);
                SelectWindDirectionIndex(2);
                break;

            case "hills":
                SelectedClusteredScenarioType = ClusteredScenarioType.HillyTerrain;

                SetText(_nameBox, "ДЕМО: Холмы");
                SetText(_tempBox, "26");
                SetText(_humidityBox, "40");
                SetText(_windSpeedBox, "5");
                SetText(_precipitationBox, "0");
                SetText(_moistureMinBox, "0.18");
                SetText(_moistureMaxBox, "0.56");
                SetText(_elevationBox, "120");
                SetText(_mapNoiseBox, "0.07");
                SetText(_mapDrynessBox, "1.00");
                SetText(_reliefStrengthBox, "1.25");
                SetText(_fuelDensityBox, "1.00");
                SetVegetationDistribution(24, 18, 34, 10, 14, 0, 0);
                SelectWindDirectionIndex(1);
                break;

            default:
                ApplyRandomPreset();
                return;
        }

        if (_scenarioTypeBox != null)
            _scenarioTypeBox.SelectedIndex = GetScenarioIndex(SelectedClusteredScenarioType);

        UpdateVegetationDistributionFromInputs();
        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateMapEditorSummary();
        UpdateStructurePreview();
        UpdatePresetButtonsUi();
    }

    private void ApplyCommonGraphDemoDefaults()
    {
        SetText(_stepsBox, "160");
        SetText(_stepDurationBox, "900");
        SetText(_fireCellsBox, "3");
        SetText(_randomSeedBox, string.Empty);
    }

    private void SetPresetButtonVisible(Button? button, bool isVisible)
    {
        if (button == null)
            return;

        button.IsVisible = isVisible;
    }

    private void SetVegetationDistribution(
        double coniferous,
        double deciduous,
        double mixed,
        double grass,
        double shrub,
        double water,
        double bare)
    {
        SetText(_coniferousBox, coniferous.ToString("0.##", CultureInfo.InvariantCulture));
        SetText(_deciduousBox, deciduous.ToString("0.##", CultureInfo.InvariantCulture));
        SetText(_mixedBox, mixed.ToString("0.##", CultureInfo.InvariantCulture));
        SetText(_grassBox, grass.ToString("0.##", CultureInfo.InvariantCulture));
        SetText(_shrubBox, shrub.ToString("0.##", CultureInfo.InvariantCulture));
        SetText(_waterBox, water.ToString("0.##", CultureInfo.InvariantCulture));
        SetText(_bareBox, bare.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private void SelectWindDirectionIndex(int index)
    {
        if (_windDirBox == null)
            return;

        _windDirBox.SelectedIndex = Math.Max(0, index);
    }

    private int GetScenarioIndex(ClusteredScenarioType? scenario)
    {
        return scenario switch
        {
            ClusteredScenarioType.MixedForest => 0,
            ClusteredScenarioType.DryConiferousMassif => 1,
            ClusteredScenarioType.ForestWithRiver => 2,
            ClusteredScenarioType.ForestWithLake => 3,
            ClusteredScenarioType.WetForestAfterRain => 4,
            ClusteredScenarioType.ForestWithFirebreak => 5,
            ClusteredScenarioType.HillyTerrain => 6,
            _ => -1
        };
    }

    private ClusteredScenarioType? GetSelectedScenarioType()
    {
        if (_scenarioTypeBox == null || _scenarioTypeBox.SelectedIndex < 0)
            return SelectedClusteredScenarioType;

        return _scenarioTypeBox.SelectedIndex switch
        {
            0 => ClusteredScenarioType.MixedForest,
            1 => ClusteredScenarioType.DryConiferousMassif,
            2 => ClusteredScenarioType.ForestWithRiver,
            3 => ClusteredScenarioType.ForestWithLake,
            4 => ClusteredScenarioType.WetForestAfterRain,
            5 => ClusteredScenarioType.ForestWithFirebreak,
            6 => ClusteredScenarioType.HillyTerrain,
            _ => null
        };
    }

    private void UpdateMapModeUi()
    {
        if (_mode != GraphCreationMode.Large)
        {
            SelectedMapCreationMode = MapCreationMode.Random;
            SelectedClusteredScenarioType = null;
            ClusteredBlueprint = null;

            if (_mapCreationModeBox != null)
            {
                _mapCreationModeBox.SelectedIndex = 0;
                _mapCreationModeBox.IsEnabled = false;
            }

            if (_scenarioPanel != null)
                _scenarioPanel.IsVisible = false;

            if (_semiManualPanel != null)
                _semiManualPanel.IsVisible = false;

            if (_mapModeDescriptionTextBlock != null)
            {
                _mapModeDescriptionTextBlock.Text = _mode == GraphCreationMode.Medium
                    ? "Средний граф создаётся случайно: отдельные непересекающиеся области и мосты между ними."
                    : "Малый граф создаётся случайно. Демо и ручной редактор для него отключены.";
            }

            return;
        }

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.IsEnabled = true;

        if (_scenarioPanel != null)
            _scenarioPanel.IsVisible = SelectedMapCreationMode == MapCreationMode.Scenario;

        if (_semiManualPanel != null)
            _semiManualPanel.IsVisible = SelectedMapCreationMode == MapCreationMode.SemiManual;

        if (_mapModeDescriptionTextBlock == null)
            return;

        _mapModeDescriptionTextBlock.Text = SelectedMapCreationMode switch
        {
            MapCreationMode.Scenario =>
                "Демо-сценарий большого графа. Области могут пересекаться, между ними создаются мосты, обходы и барьеры.",

            MapCreationMode.SemiManual =>
                "Полуручной режим: структура большого графа задаётся через редактор вершин и рёбер.",

            _ =>
                "Случайный большой граф: макрообласти генерируются автоматически."
        };
    }

    private void UpdateScenarioDescription()
    {
        if (_scenarioDescriptionTextBlock != null)
        {
            _scenarioDescriptionTextBlock.Text = SelectedClusteredScenarioType switch
            {
                ClusteredScenarioType.MixedForest =>
                    "Смешанный лес: несколько разных лесных зон с умеренной влажностью и неоднородным распространением огня.",

                ClusteredScenarioType.DryConiferousMassif =>
                    "Сухой хвойный массив: много горючей хвойной растительности, низкая влажность и усиление распространения ветром.",

                ClusteredScenarioType.ForestWithRiver =>
                    "Река как барьер: водная зона разделяет лесные области, распространение возможно через обходные мосты.",

                ClusteredScenarioType.ForestWithLake =>
                    "Озеро и берег: водная зона замедляет пожар, береговые влажные участки дополнительно ослабляют распространение.",

                ClusteredScenarioType.WetForestAfterRain =>
                    "Влажный лес: высокая влажность и осадки снижают скорость распространения пожара.",

                ClusteredScenarioType.ForestWithFirebreak =>
                    "Просека: негорючая или слабогорючая зона разрывает лесной массив, пожар ищет обходные пути.",

                ClusteredScenarioType.HillyTerrain =>
                    "Холмы: рельеф влияет на распространение, подъём и спуск меняют скорость перехода между областями.",

                _ =>
                    "Выберите сценарий для большого графа."
            };
        }

        if (_semiManualDescriptionTextBlock != null)
        {
            _semiManualDescriptionTextBlock.Text =
                ClusteredBlueprint != null && ClusteredBlueprint.Nodes.Any()
                    ? $"Редактор использован: вершин {ClusteredBlueprint.Nodes.Count}, рёбер {ClusteredBlueprint.Edges.Count}."
                    : "Редактор пока не использован. Можно создать вершины, соединить их рёбрами и задать параметры.";
        }
    }

    private void UpdateMapEditorSummary()
    {
        if (_mapEditorSummaryTextBlock == null)
            return;

        if (_mode != GraphCreationMode.Large)
        {
            _mapEditorSummaryTextBlock.Text = "Редактор доступен только для большого графа.";
            return;
        }

        _mapEditorSummaryTextBlock.Text =
            ClusteredBlueprint != null && ClusteredBlueprint.Nodes.Any()
                ? $"Подготовлен граф: вершин {ClusteredBlueprint.Nodes.Count}, рёбер {ClusteredBlueprint.Edges.Count}"
                : "Редактор графа не использован";
    }

    private void UpdateStructurePreview()
    {
        if (_structureSummaryTextBlock == null || _structureDetailTextBlock == null)
            return;

        int width = Math.Max(1, ParseInt(_widthBox?.Text, GridWidth));
        int height = Math.Max(1, ParseInt(_heightBox?.Text, GridHeight));
        int fireCells = Math.Max(1, ParseInt(_fireCellsBox?.Text, InitialFireCells));

        double moistureMin = ParseDouble(_moistureMinBox?.Text, MoistureMin);
        double moistureMax = ParseDouble(_moistureMaxBox?.Text, MoistureMax);
        double elevation = ParseDouble(_elevationBox?.Text, ElevationVariation);

        string scaleText = _mode switch
        {
            GraphCreationMode.Small => "малый граф",
            GraphCreationMode.Medium => "средний граф",
            GraphCreationMode.Large => "большой граф",
            _ => "граф"
        };

        string sourceText;

        if (_mode != GraphCreationMode.Large)
        {
            sourceText = "Случайный граф";
        }
        else if (SelectedMapCreationMode == MapCreationMode.SemiManual && ClusteredBlueprint != null)
        {
            sourceText = "Граф из редактора";
        }
        else if (SelectedMapCreationMode == MapCreationMode.Scenario && SelectedClusteredScenarioType.HasValue)
        {
            sourceText = $"Демо: {GetScenarioDisplayName(SelectedClusteredScenarioType.Value)}";
        }
        else
        {
            sourceText = "Случайная карта";
        }

        _structureSummaryTextBlock.Text =
            $"{sourceText} • {scaleText} • область {width}×{height}";

        _structureDetailTextBlock.Text =
            $"Очагов: {fireCells} • влажность {moistureMin:F2}..{moistureMax:F2} • перепад высот ±{elevation:F0}";
    }

    private string GetScenarioDisplayName(ClusteredScenarioType scenario)
    {
        return scenario switch
        {
            ClusteredScenarioType.MixedForest => "смешанный лес",
            ClusteredScenarioType.DryConiferousMassif => "сухой хвойный + ветер",
            ClusteredScenarioType.ForestWithRiver => "река как барьер",
            ClusteredScenarioType.ForestWithLake => "озеро и берег",
            ClusteredScenarioType.WetForestAfterRain => "влажный лес",
            ClusteredScenarioType.ForestWithFirebreak => "просека",
            ClusteredScenarioType.HillyTerrain => "холмы",
            _ => "графовое демо"
        };
    }

    private async Task OpenGraphEditorAsync()
    {
        if (_mode != GraphCreationMode.Large)
            return;

        if (SelectedMapCreationMode != MapCreationMode.SemiManual)
            return;

        int width = Math.Max(8, ParseInt(_widthBox?.Text, 24));
        int height = Math.Max(8, ParseInt(_heightBox?.Text, 24));

        var editor = new ClusteredGraphEditorDialog(width, height, ClusteredBlueprint);
        var result = await editor.ShowDialog<bool>(this);

        if (!result)
            return;

        ClusteredBlueprint = editor.EditedBlueprint;
        UpdateMapEditorSummary();
        UpdateScenarioDescription();
        UpdateStructurePreview();
        ClearErrors();
    }

    private void UpdateVegetationDistributionFromInputs()
    {
        var values = new List<(int VegetationType, double Probability)>
        {
            ((int)VegetationType.Coniferous, ParseDouble(_coniferousBox?.Text, 24)),
            ((int)VegetationType.Deciduous, ParseDouble(_deciduousBox?.Text, 18)),
            ((int)VegetationType.Mixed, ParseDouble(_mixedBox?.Text, 28)),
            ((int)VegetationType.Grass, ParseDouble(_grassBox?.Text, 12)),
            ((int)VegetationType.Shrub, ParseDouble(_shrubBox?.Text, 10)),
            ((int)VegetationType.Water, ParseDouble(_waterBox?.Text, 4)),
            ((int)VegetationType.Bare, ParseDouble(_bareBox?.Text, 4))
        };

        double sum = values.Sum(x => Math.Max(0.0, x.Probability));
        if (sum <= 0.0001)
            sum = 1.0;

        VegetationDistributions = values
            .Select(x => (x.VegetationType, Math.Max(0.0, x.Probability) / sum))
            .ToList();

        UpdateStructurePreview();
    }

    private void WriteVegetationDistributionToInputs()
    {
        var map = VegetationDistributions.ToDictionary(x => x.VegetationType, x => x.Probability * 100.0);

        SetText(_coniferousBox, GetVegetationPercentText(map, VegetationType.Coniferous, 24));
        SetText(_deciduousBox, GetVegetationPercentText(map, VegetationType.Deciduous, 18));
        SetText(_mixedBox, GetVegetationPercentText(map, VegetationType.Mixed, 28));
        SetText(_grassBox, GetVegetationPercentText(map, VegetationType.Grass, 12));
        SetText(_shrubBox, GetVegetationPercentText(map, VegetationType.Shrub, 10));
        SetText(_waterBox, GetVegetationPercentText(map, VegetationType.Water, 4));
        SetText(_bareBox, GetVegetationPercentText(map, VegetationType.Bare, 4));
    }

    private string GetVegetationPercentText(
        Dictionary<int, double> map,
        VegetationType vegetationType,
        double fallback)
    {
        return map.TryGetValue((int)vegetationType, out var value)
            ? value.ToString("0.##", CultureInfo.InvariantCulture)
            : fallback.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private bool TryCollectValues(out string error)
    {
        error = string.Empty;

        SimulationName = _nameBox?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(SimulationName))
        {
            SimulationName = _mode switch
            {
                GraphCreationMode.Small => "Малый случайный граф",
                GraphCreationMode.Medium => "Средний случайный граф",
                GraphCreationMode.Large => "Большой граф",
                _ => "Графовая симуляция"
            };
        }

        GridWidth = Math.Max(8, ParseInt(_widthBox?.Text, GridWidth));
        GridHeight = Math.Max(8, ParseInt(_heightBox?.Text, GridHeight));
        InitialFireCells = Math.Max(1, ParseInt(_fireCellsBox?.Text, InitialFireCells));

        MoistureMin = ParseDouble(_moistureMinBox?.Text, MoistureMin);
        MoistureMax = ParseDouble(_moistureMaxBox?.Text, MoistureMax);

        if (MoistureMin < 0.0 || MoistureMax > 1.0 || MoistureMin > MoistureMax)
        {
            error = "Влажность должна быть в диапазоне 0..1, минимум не должен быть больше максимума.";
            return false;
        }

        ElevationVariation = Math.Max(0.0, ParseDouble(_elevationBox?.Text, ElevationVariation));

        SimulationSteps = Math.Max(1, ParseInt(_stepsBox?.Text, SimulationSteps));
        StepDurationSeconds = Math.Clamp(ParseInt(_stepDurationBox?.Text, StepDurationSeconds), 1, 7200);

        Temperature = ParseDouble(_tempBox?.Text, Temperature);
        Humidity = Math.Clamp(ParseDouble(_humidityBox?.Text, Humidity), 0.0, 100.0);
        WindSpeed = Math.Max(0.0, ParseDouble(_windSpeedBox?.Text, WindSpeed));
        Precipitation = Math.Max(0.0, ParseDouble(_precipitationBox?.Text, Precipitation));

        MapNoiseStrength = Math.Clamp(ParseDouble(_mapNoiseBox?.Text, MapNoiseStrength), 0.0, 1.0);
        MapDrynessFactor = Math.Max(0.1, ParseDouble(_mapDrynessBox?.Text, MapDrynessFactor));
        ReliefStrengthFactor = Math.Max(0.1, ParseDouble(_reliefStrengthBox?.Text, ReliefStrengthFactor));
        FuelDensityFactor = Math.Max(0.1, ParseDouble(_fuelDensityBox?.Text, FuelDensityFactor));

        RandomSeed = string.IsNullOrWhiteSpace(_randomSeedBox?.Text)
            ? null
            : ParseInt(_randomSeedBox?.Text, 0);

        WindDirection = _windDirBox?.SelectedIndex switch
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

        if (_mode != GraphCreationMode.Large)
        {
            SelectedMapCreationMode = MapCreationMode.Random;
            SelectedClusteredScenarioType = null;
            ClusteredBlueprint = null;
        }
        else
        {
            if (SelectedMapCreationMode == MapCreationMode.Scenario &&
                SelectedClusteredScenarioType == null)
            {
                SelectedClusteredScenarioType = ClusteredScenarioType.MixedForest;
            }

            if (SelectedMapCreationMode == MapCreationMode.SemiManual &&
                (ClusteredBlueprint == null || !ClusteredBlueprint.Nodes.Any()))
            {
                error = "Для полуручного режима нужно создать граф в редакторе.";
                return false;
            }
        }

        UpdateVegetationDistributionFromInputs();

        return true;
    }

    private void ShowError(string error)
    {
        if (_errorTextBlock == null)
            return;

        _errorTextBlock.Text = error;
        _errorTextBlock.IsVisible = true;
    }

    private void ClearErrors()
    {
        if (_errorTextBlock == null)
            return;

        _errorTextBlock.Text = string.Empty;
        _errorTextBlock.IsVisible = false;
    }

    private static void SetText(TextBox? textBox, string text)
    {
        if (textBox != null)
            textBox.Text = text;
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
}