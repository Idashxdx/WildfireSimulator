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

    private Button? _presetButton1;
    private Button? _presetButton2;
    private Button? _presetButton3;
    private Button? _presetButton4;
    private Button? _presetButton5;

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
        return new SimulationCreationResult
        {
            GraphType = GraphType.ClusteredGraph,
            GraphScaleType = _mode switch
            {
                GraphCreationMode.Small => GraphScaleType.Small,
                GraphCreationMode.Medium => GraphScaleType.Medium,
                GraphCreationMode.Large => GraphScaleType.Large,
                _ => GraphScaleType.Medium
            },

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

            SelectedMapCreationMode = SelectedMapCreationMode,
            SelectedScenarioType = null,
            SelectedClusteredScenarioType = SelectedClusteredScenarioType,

            MapNoiseStrength = MapNoiseStrength,
            MapDrynessFactor = MapDrynessFactor,
            ReliefStrengthFactor = ReliefStrengthFactor,
            FuelDensityFactor = FuelDensityFactor,

            VegetationDistributions = new List<(int VegetationType, double Probability)>(VegetationDistributions),
            ClusteredBlueprint = ClusteredBlueprint
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

        _presetButton1 = this.FindControl<Button>("PresetButton1");
        _presetButton2 = this.FindControl<Button>("PresetButton2");
        _presetButton3 = this.FindControl<Button>("PresetButton3");
        _presetButton4 = this.FindControl<Button>("PresetButton4");
        _presetButton5 = this.FindControl<Button>("PresetButton5");

        _createButton = this.FindControl<Button>("CreateButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
    }

    private void ConfigureScenarioItems()
    {
        if (_scenarioTypeBox == null)
            return;

        _scenarioTypeBox.ItemsSource = GetScenarioDisplayItems();
        _scenarioTypeBox.SelectedIndex = 0;
    }

    private List<string> GetScenarioDisplayItems()
    {
        return _mode switch
        {
            GraphCreationMode.Small => new List<string>
            {
                "Bridge-critical",
                "Water-block",
                "Firebreak"
            },
            GraphCreationMode.Medium => new List<string>
            {
                "Dense clusters",
                "Water barrier",
                "Wet patches"
            },
            GraphCreationMode.Large => new List<string>
            {
                "Firebreak macro",
                "Hilly macro-zones",
                "Wet zones"
            },
            _ => new List<string> { "Default" }
        };
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
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_deciduousBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_mixedBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_grassBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_shrubBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_waterBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            UpdateStructurePreview();
            ClearErrors();
        });

        AttachTextChanged(_bareBox, () =>
        {
            UpdateVegetationDistributionFromInputs();
            UpdateStructurePreview();
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
            GraphCreationMode.Small => "Small Graph Simulation",
            GraphCreationMode.Medium => "Medium Graph Simulation",
            GraphCreationMode.Large => "Large Graph Simulation",
            _ => "Graph Simulation"
        };

        GridWidth = _mode switch
        {
            GraphCreationMode.Small => 20,
            GraphCreationMode.Medium => 24,
            GraphCreationMode.Large => 34,
            _ => 24
        };

        GridHeight = _mode switch
        {
            GraphCreationMode.Small => 20,
            GraphCreationMode.Medium => 24,
            GraphCreationMode.Large => 34,
            _ => 24
        };

        InitialFireCells = _mode switch
        {
            GraphCreationMode.Small => 1,
            GraphCreationMode.Medium => 2,
            GraphCreationMode.Large => 3,
            _ => 2
        };

        MoistureMin = 0.18;
        MoistureMax = 0.55;
        ElevationVariation = _mode switch
        {
            GraphCreationMode.Small => 12,
            GraphCreationMode.Medium => 25,
            GraphCreationMode.Large => 45,
            _ => 25
        };

        SimulationSteps = 100;
        StepDurationSeconds = 900;

        Temperature = 25.0;
        Humidity = 40.0;
        WindSpeed = 5.0;
        WindDirection = 45.0;
        Precipitation = 0.0;
        RandomSeed = null;

        SelectedMapCreationMode = MapCreationMode.Random;
        SelectedClusteredScenarioType = GetDefaultScenarioForScale();
        ClusteredBlueprint = null;

        MapNoiseStrength = 0.08;
        MapDrynessFactor = 1.0;
        ReliefStrengthFactor = 1.0;
        FuelDensityFactor = 1.0;

        SetText(_nameBox, SimulationName);
        SetText(_widthBox, GridWidth.ToString(CultureInfo.InvariantCulture));
        SetText(_heightBox, GridHeight.ToString(CultureInfo.InvariantCulture));
        SetText(_fireCellsBox, InitialFireCells.ToString(CultureInfo.InvariantCulture));
        SetText(_moistureMinBox, MoistureMin.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_moistureMaxBox, MoistureMax.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_elevationBox, ElevationVariation.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_stepsBox, SimulationSteps.ToString(CultureInfo.InvariantCulture));
        SetText(_stepDurationBox, StepDurationSeconds.ToString(CultureInfo.InvariantCulture));
        SetText(_tempBox, Temperature.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_humidityBox, Humidity.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_windSpeedBox, WindSpeed.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_precipitationBox, Precipitation.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_randomSeedBox, string.Empty);

        SetText(_mapNoiseBox, MapNoiseStrength.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_mapDrynessBox, MapDrynessFactor.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_reliefStrengthBox, ReliefStrengthFactor.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_fuelDensityBox, FuelDensityFactor.ToString("0.00", CultureInfo.InvariantCulture));

        if (_windDirBox != null)
            _windDirBox.SelectedIndex = 1;

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectedIndex = 0;

        ApplyBalancedVegetationDefaults();

        if (_scenarioTypeBox != null)
            _scenarioTypeBox.SelectedIndex = 0;
    }

    private void ApplyModeTexts()
    {
        if (_typeInfoTextBlock != null)
        {
            _typeInfoTextBlock.Text = _mode switch
            {
                GraphCreationMode.Small => "Графовая симуляция • SmallGraph",
                GraphCreationMode.Medium => "Графовая симуляция • MediumGraph",
                GraphCreationMode.Large => "Графовая симуляция • LargeGraph",
                _ => "Графовая симуляция"
            };
        }

        if (_typeHintTextBlock != null)
        {
            _typeHintTextBlock.Text = _mode switch
            {
                GraphCreationMode.Small => "Topology-first граф: мало узлов, sparse-структура, bridge-critical сценарии.",
                GraphCreationMode.Medium => "Clustered graph: выраженные кластеры, barrier/patch структура, локальная связность.",
                GraphCreationMode.Large => "Macro-zones graph: corridor logic, длинные межкластерные связи, исследовательские сценарии.",
                _ => "Настройка графовой симуляции."
            };
        }

        if (_widthLabelTextBlock != null)
            _widthLabelTextBlock.Text = "Ширина канвы";

        if (_heightLabelTextBlock != null)
            _heightLabelTextBlock.Text = "Высота канвы";

        if (_widthHintTextBlock != null)
        {
            _widthHintTextBlock.Text = _mode switch
            {
                GraphCreationMode.Small => "Для small обычно достаточно 16–24",
                GraphCreationMode.Medium => "Для medium удобно 20–28",
                GraphCreationMode.Large => "Для large обычно 30–40",
                _ => string.Empty
            };
        }

        if (_heightHintTextBlock != null)
        {
            _heightHintTextBlock.Text = _mode switch
            {
                GraphCreationMode.Small => "Компактное пространство для topology-first графа",
                GraphCreationMode.Medium => "Достаточно места для нескольких кластеров",
                GraphCreationMode.Large => "Нужно пространство для macro-zones и corridor edges",
                _ => string.Empty
            };
        }

        if (_fireCellsHintTextBlock != null)
        {
            _fireCellsHintTextBlock.Text = _mode switch
            {
                GraphCreationMode.Small => "Обычно 1 стартовый очаг",
                GraphCreationMode.Medium => "Обычно 1–2 стартовых очага",
                GraphCreationMode.Large => "Обычно 2–4 стартовых очага",
                _ => string.Empty
            };
        }
    }

    private void UpdatePresetButtonsUi()
    {
        if (_presetHintTextBlock != null)
        {
            _presetHintTextBlock.Text = _mode switch
            {
                GraphCreationMode.Small => "Готовые topology-first сценарии для small graph",
                GraphCreationMode.Medium => "Готовые clustered сценарии для medium graph",
                GraphCreationMode.Large => "Готовые macro / corridor сценарии для large graph",
                _ => "Готовые сценарии"
            };
        }

        if (_presetButton1 != null)
        {
            _presetButton1.Content = _mode switch
            {
                GraphCreationMode.Small => "Bridge-critical",
                GraphCreationMode.Medium => "Dense clusters",
                GraphCreationMode.Large => "Firebreak macro",
                _ => "Preset 1"
            };
            _presetButton1.Tag = "preset-1";
        }

        if (_presetButton2 != null)
        {
            _presetButton2.Content = _mode switch
            {
                GraphCreationMode.Small => "Water-block",
                GraphCreationMode.Medium => "Water barrier",
                GraphCreationMode.Large => "Hilly macro",
                _ => "Preset 2"
            };
            _presetButton2.Tag = "preset-2";
        }

        if (_presetButton3 != null)
        {
            _presetButton3.Content = _mode switch
            {
                GraphCreationMode.Small => "Firebreak",
                GraphCreationMode.Medium => "Wet patches",
                GraphCreationMode.Large => "Wet zones",
                _ => "Preset 3"
            };
            _presetButton3.Tag = "preset-3";
        }

        if (_presetButton4 != null)
        {
            _presetButton4.Content = _mode switch
            {
                GraphCreationMode.Small => "Dry wind demo",
                GraphCreationMode.Medium => "Hot dense demo",
                GraphCreationMode.Large => "Corridor stress",
                _ => "Preset 4"
            };
            _presetButton4.Tag = "preset-4";
        }

        if (_presetButton5 != null)
        {
            _presetButton5.Content = _mode switch
            {
                GraphCreationMode.Small => "Balanced demo",
                GraphCreationMode.Medium => "Balanced medium",
                GraphCreationMode.Large => "Balanced large",
                _ => "Preset 5"
            };
            _presetButton5.Tag = "preset-5";
        }
    }

    private async Task OpenGraphEditorAsync()
    {
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
        UpdateStructurePreview();
        ClearErrors();
    }

    private void ApplyPresetFromButton(Button? button)
    {
        if (button == null)
            return;

        ClearErrors();
        ClusteredBlueprint = null;

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;

        var tag = button.Tag?.ToString()?.Trim().ToLowerInvariant();

        switch (tag)
        {
            case "dry-coniferous":
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)ClusteredScenarioType.DenseDryConiferous;
                if (_windSpeedBox != null) _windSpeedBox.Text = "8";
                if (_tempBox != null) _tempBox.Text = "31";
                if (_humidityBox != null) _humidityBox.Text = "22";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.20";
                if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.15";
                break;

            case "river":
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)ClusteredScenarioType.WaterBarrier;
                if (_humidityBox != null) _humidityBox.Text = "45";
                if (_precipitationBox != null) _precipitationBox.Text = "0.0";
                break;

            case "wet":
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)ClusteredScenarioType.WetAfterRain;
                if (_humidityBox != null) _humidityBox.Text = "78";
                if (_precipitationBox != null) _precipitationBox.Text = "2.5";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "0.82";
                break;

            case "firebreak":
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)ClusteredScenarioType.FirebreakGap;
                if (_windSpeedBox != null) _windSpeedBox.Text = "6";
                if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "0.95";
                break;

            case "hills":
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)ClusteredScenarioType.HillyClusters;
                if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.35";
                if (_elevationBox != null) _elevationBox.Text = _mode switch
                {
                    GraphCreationMode.Small => "18",
                    GraphCreationMode.Medium => "35",
                    _ => "60"
                };
                break;
        }

        SelectedMapCreationMode = MapCreationMode.Scenario;
        SelectedClusteredScenarioType = GetSelectedScenarioType();

        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateMapEditorSummary();
        UpdateStructurePreview();
    }

    private void ApplySmallPreset(string tag)
    {
        switch (tag)
        {
            case "preset-1":
                ApplySmallScenarioPreset(ClusteredScenarioType.DenseDryConiferous, "Small bridge-critical");
                SelectedMapCreationMode = MapCreationMode.Scenario;
                GridWidth = 20;
                GridHeight = 20;
                InitialFireCells = 1;
                Temperature = 28;
                Humidity = 35;
                WindSpeed = 7;
                Precipitation = 0;
                MapDrynessFactor = 1.18;
                ReliefStrengthFactor = 0.90;
                FuelDensityFactor = 1.08;
                break;

            case "preset-2":
                ApplySmallScenarioPreset(ClusteredScenarioType.WaterBarrier, "Small water-block");
                SelectedMapCreationMode = MapCreationMode.Scenario;
                GridWidth = 20;
                GridHeight = 20;
                InitialFireCells = 1;
                Temperature = 24;
                Humidity = 45;
                WindSpeed = 4;
                Precipitation = 0;
                MapDrynessFactor = 0.95;
                ReliefStrengthFactor = 1.00;
                FuelDensityFactor = 0.92;
                break;

            case "preset-3":
                ApplySmallScenarioPreset(ClusteredScenarioType.FirebreakGap, "Small firebreak");
                SelectedMapCreationMode = MapCreationMode.Scenario;
                GridWidth = 20;
                GridHeight = 20;
                InitialFireCells = 1;
                Temperature = 26;
                Humidity = 38;
                WindSpeed = 6;
                Precipitation = 0;
                MapDrynessFactor = 1.08;
                ReliefStrengthFactor = 1.00;
                FuelDensityFactor = 1.00;
                break;

            case "preset-4":
                ApplyBalancedVegetationDefaults();
                SelectedMapCreationMode = MapCreationMode.Random;
                SelectedClusteredScenarioType = null;
                SimulationName = "Small dry wind demo";
                GridWidth = 20;
                GridHeight = 20;
                InitialFireCells = 1;
                Temperature = 30;
                Humidity = 28;
                WindSpeed = 10;
                Precipitation = 0;
                MapNoiseStrength = 0.11;
                MapDrynessFactor = 1.24;
                ReliefStrengthFactor = 0.95;
                FuelDensityFactor = 1.08;
                break;

            default:
                ApplyBalancedVegetationDefaults();
                SelectedMapCreationMode = MapCreationMode.Random;
                SelectedClusteredScenarioType = null;
                SimulationName = "Small balanced demo";
                GridWidth = 20;
                GridHeight = 20;
                InitialFireCells = 1;
                Temperature = 25;
                Humidity = 40;
                WindSpeed = 5;
                Precipitation = 0;
                MapNoiseStrength = 0.08;
                MapDrynessFactor = 1.00;
                ReliefStrengthFactor = 1.00;
                FuelDensityFactor = 1.00;
                break;
        }

        WriteCurrentValuesToControls();
    }

    private void ApplyMediumPreset(string tag)
    {
        switch (tag)
        {
            case "preset-1":
                ApplyMediumScenarioPreset(ClusteredScenarioType.DenseDryConiferous, "Medium dense clusters");
                SelectedMapCreationMode = MapCreationMode.Scenario;
                GridWidth = 24;
                GridHeight = 24;
                InitialFireCells = 2;
                Temperature = 29;
                Humidity = 33;
                WindSpeed = 7;
                Precipitation = 0;
                MapDrynessFactor = 1.15;
                ReliefStrengthFactor = 1.00;
                FuelDensityFactor = 1.10;
                break;

            case "preset-2":
                ApplyMediumScenarioPreset(ClusteredScenarioType.WaterBarrier, "Medium water barrier");
                SelectedMapCreationMode = MapCreationMode.Scenario;
                GridWidth = 24;
                GridHeight = 24;
                InitialFireCells = 2;
                Temperature = 24;
                Humidity = 48;
                WindSpeed = 4;
                Precipitation = 0;
                MapDrynessFactor = 0.92;
                ReliefStrengthFactor = 1.00;
                FuelDensityFactor = 0.96;
                break;

            case "preset-3":
                ApplyMediumScenarioPreset(ClusteredScenarioType.WetAfterRain, "Medium wet patches");
                SelectedMapCreationMode = MapCreationMode.Scenario;
                GridWidth = 24;
                GridHeight = 24;
                InitialFireCells = 2;
                Temperature = 22;
                Humidity = 62;
                WindSpeed = 3;
                Precipitation = 5;
                MapDrynessFactor = 0.82;
                ReliefStrengthFactor = 1.00;
                FuelDensityFactor = 0.92;
                break;

            case "preset-4":
                ApplyBalancedVegetationDefaults();
                SelectedMapCreationMode = MapCreationMode.Random;
                SelectedClusteredScenarioType = null;
                SimulationName = "Medium hot dense demo";
                GridWidth = 24;
                GridHeight = 24;
                InitialFireCells = 2;
                Temperature = 31;
                Humidity = 30;
                WindSpeed = 8;
                Precipitation = 0;
                MapNoiseStrength = 0.10;
                MapDrynessFactor = 1.20;
                ReliefStrengthFactor = 1.05;
                FuelDensityFactor = 1.12;
                break;

            default:
                ApplyBalancedVegetationDefaults();
                SelectedMapCreationMode = MapCreationMode.Random;
                SelectedClusteredScenarioType = null;
                SimulationName = "Medium balanced demo";
                GridWidth = 24;
                GridHeight = 24;
                InitialFireCells = 2;
                Temperature = 25;
                Humidity = 40;
                WindSpeed = 5;
                Precipitation = 0;
                MapNoiseStrength = 0.08;
                MapDrynessFactor = 1.00;
                ReliefStrengthFactor = 1.00;
                FuelDensityFactor = 1.00;
                break;
        }

        WriteCurrentValuesToControls();
    }

    private void ApplyLargePreset(string tag)
    {
        switch (tag)
        {
            case "preset-1":
                ApplyLargeScenarioPreset(ClusteredScenarioType.FirebreakGap, "Large firebreak macro");
                SelectedMapCreationMode = MapCreationMode.Scenario;
                GridWidth = 34;
                GridHeight = 34;
                InitialFireCells = 3;
                Temperature = 29;
                Humidity = 34;
                WindSpeed = 7;
                Precipitation = 0;
                MapDrynessFactor = 1.12;
                ReliefStrengthFactor = 1.00;
                FuelDensityFactor = 1.08;
                break;

            case "preset-2":
                ApplyLargeScenarioPreset(ClusteredScenarioType.HillyClusters, "Large hilly macro");
                SelectedMapCreationMode = MapCreationMode.Scenario;
                GridWidth = 34;
                GridHeight = 34;
                InitialFireCells = 3;
                Temperature = 26;
                Humidity = 42;
                WindSpeed = 6;
                Precipitation = 0;
                MapDrynessFactor = 1.00;
                ReliefStrengthFactor = 1.25;
                FuelDensityFactor = 1.00;
                break;

            case "preset-3":
                ApplyLargeScenarioPreset(ClusteredScenarioType.WetAfterRain, "Large wet zones");
                SelectedMapCreationMode = MapCreationMode.Scenario;
                GridWidth = 34;
                GridHeight = 34;
                InitialFireCells = 3;
                Temperature = 22;
                Humidity = 60;
                WindSpeed = 4;
                Precipitation = 6;
                MapDrynessFactor = 0.84;
                ReliefStrengthFactor = 0.95;
                FuelDensityFactor = 0.92;
                break;

            case "preset-4":
                ApplyBalancedVegetationDefaults();
                SelectedMapCreationMode = MapCreationMode.Random;
                SelectedClusteredScenarioType = null;
                SimulationName = "Large corridor stress";
                GridWidth = 34;
                GridHeight = 34;
                InitialFireCells = 3;
                Temperature = 32;
                Humidity = 30;
                WindSpeed = 11;
                Precipitation = 0;
                MapNoiseStrength = 0.12;
                MapDrynessFactor = 1.22;
                ReliefStrengthFactor = 1.10;
                FuelDensityFactor = 1.10;
                break;

            default:
                ApplyBalancedVegetationDefaults();
                SelectedMapCreationMode = MapCreationMode.Random;
                SelectedClusteredScenarioType = null;
                SimulationName = "Large balanced demo";
                GridWidth = 34;
                GridHeight = 34;
                InitialFireCells = 3;
                Temperature = 25;
                Humidity = 40;
                WindSpeed = 5;
                Precipitation = 0;
                MapNoiseStrength = 0.08;
                MapDrynessFactor = 1.00;
                ReliefStrengthFactor = 1.00;
                FuelDensityFactor = 1.00;
                break;
        }

        WriteCurrentValuesToControls();
    }

    private void ApplySmallScenarioPreset(ClusteredScenarioType scenario, string name)
    {
        SelectedClusteredScenarioType = scenario;
        SimulationName = name;
        ApplyBalancedVegetationDefaults();
    }

    private void ApplyMediumScenarioPreset(ClusteredScenarioType scenario, string name)
    {
        SelectedClusteredScenarioType = scenario;
        SimulationName = name;
        ApplyBalancedVegetationDefaults();
    }

    private void ApplyLargeScenarioPreset(ClusteredScenarioType scenario, string name)
    {
        SelectedClusteredScenarioType = scenario;
        SimulationName = name;
        ApplyBalancedVegetationDefaults();
    }

    private void WriteCurrentValuesToControls()
    {
        SetText(_nameBox, SimulationName);
        SetText(_widthBox, GridWidth.ToString(CultureInfo.InvariantCulture));
        SetText(_heightBox, GridHeight.ToString(CultureInfo.InvariantCulture));
        SetText(_fireCellsBox, InitialFireCells.ToString(CultureInfo.InvariantCulture));
        SetText(_moistureMinBox, MoistureMin.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_moistureMaxBox, MoistureMax.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_elevationBox, ElevationVariation.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_stepsBox, SimulationSteps.ToString(CultureInfo.InvariantCulture));
        SetText(_stepDurationBox, StepDurationSeconds.ToString(CultureInfo.InvariantCulture));
        SetText(_tempBox, Temperature.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_humidityBox, Humidity.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_windSpeedBox, WindSpeed.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_precipitationBox, Precipitation.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(_mapNoiseBox, MapNoiseStrength.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_mapDrynessBox, MapDrynessFactor.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_reliefStrengthBox, ReliefStrengthFactor.ToString("0.00", CultureInfo.InvariantCulture));
        SetText(_fuelDensityBox, FuelDensityFactor.ToString("0.00", CultureInfo.InvariantCulture));

        if (_mapCreationModeBox != null)
        {
            _mapCreationModeBox.SelectedIndex = SelectedMapCreationMode switch
            {
                MapCreationMode.Scenario => 1,
                MapCreationMode.SemiManual => 2,
                _ => 0
            };
        }

        if (_scenarioTypeBox != null)
            _scenarioTypeBox.SelectedIndex = GetScenarioSelectedIndex(SelectedClusteredScenarioType);

        if (_windDirBox != null)
            _windDirBox.SelectedIndex = GetWindDirectionIndex(WindDirection);

        WriteVegetationDistributionToInputs();
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
                    "Случайная clustered-генерация. Структура графа, patch-области и связи будут построены автоматически по масштабу graph.",
                MapCreationMode.Scenario =>
                    "Сценарный clustered-graph. Вы выбираете один из готовых исследовательских сценариев для текущего масштаба.",
                MapCreationMode.SemiManual =>
                    "Полуручной clustered-graph. Вы создаёте blueprint вручную: узлы, cluster ID, vegetation, moisture, elevation и рёбра становятся source of truth.",
                _ =>
                    "Выберите режим создания graph."
            };
        }

        if (_semiManualDescriptionTextBlock != null)
        {
            _semiManualDescriptionTextBlock.Text =
                SelectedMapCreationMode == MapCreationMode.SemiManual
                    ? "В semi-manual режиме нужно открыть graph editor, собрать blueprint и сохранить его перед созданием симуляции."
                    : "Полуручный graph editor сейчас не используется.";
        }

        UpdateMapEditorSummary();
    }

    private void UpdateScenarioDescription()
    {
        SelectedClusteredScenarioType = GetSelectedScenarioType();

        if (_scenarioDescriptionTextBlock == null)
            return;

        if (SelectedMapCreationMode != MapCreationMode.Scenario)
        {
            _scenarioDescriptionTextBlock.Text = "Сценарии используются только в режиме Scenario.";
            return;
        }

        _scenarioDescriptionTextBlock.Text = (_mode, SelectedClusteredScenarioType) switch
        {
            (GraphCreationMode.Small, ClusteredScenarioType.DenseDryConiferous) =>
                "SmallGraph: компактная bridge-critical topology с сухими хвойными узлами и быстрым spread по ключевым связям.",

            (GraphCreationMode.Small, ClusteredScenarioType.WaterBarrier) =>
                "SmallGraph: небольшой граф с водным блокирующим узлом или участком, который ломает прямой маршрут распространения.",

            (GraphCreationMode.Small, ClusteredScenarioType.FirebreakGap) =>
                "SmallGraph: разрыв критической связи. Исследуется зависимость spread от одного-двух ключевых переходов.",

            (GraphCreationMode.Small, ClusteredScenarioType.HillyClusters) =>
                "SmallGraph: topology с выраженным влиянием уклона. Удобно смотреть, как slope меняет локальные вероятности распространения.",

            (GraphCreationMode.Small, ClusteredScenarioType.WetAfterRain) =>
                "SmallGraph: локально влажный граф с быстрым затуханием. Подходит для проверки moisture / precipitation effects.",

            (GraphCreationMode.Small, ClusteredScenarioType.MixedDryHotspots) =>
                "SmallGraph: неоднородный граф с сухими hot spots и контрастными локальными зонами риска.",

            (GraphCreationMode.Medium, ClusteredScenarioType.DenseDryConiferous) =>
                "MediumGraph: плотные dry clusters. Огонь быстро распространяется внутри patch-групп и проверяются локальные мосты между ними.",

            (GraphCreationMode.Medium, ClusteredScenarioType.WaterBarrier) =>
                "MediumGraph: барьер между patch-кластерами. Хорошо показывает обходные связи и торможение фронта.",

            (GraphCreationMode.Medium, ClusteredScenarioType.FirebreakGap) =>
                "MediumGraph: firebreak gap между кластерами. Исследуется, сможет ли огонь перепрыгнуть разрыв через bridge edges.",

            (GraphCreationMode.Medium, ClusteredScenarioType.HillyClusters) =>
                "MediumGraph: высотно-неоднородные кластеры. Видно, как рельеф перераспределяет spread по разным patch-зонам.",

            (GraphCreationMode.Medium, ClusteredScenarioType.WetAfterRain) =>
                "MediumGraph: влажные patch-зоны после дождя. Проверяется затухание, локальные тупики и неравномерное продвижение огня.",

            (GraphCreationMode.Medium, ClusteredScenarioType.MixedDryHotspots) =>
                "MediumGraph: неоднородная clustered-структура с сухими очагами и разной локальной связностью.",

            (GraphCreationMode.Large, ClusteredScenarioType.DenseDryConiferous) =>
                "LargeGraph: крупные сухие макрозоны. Акцент на макрораспространении, длинных переходах и высокой уязвимости corridor-связей.",

            (GraphCreationMode.Large, ClusteredScenarioType.WaterBarrier) =>
                "LargeGraph: макробарьер с обходами. Видно, как corridor-логика и дальние связи меняют глобальную траекторию пожара.",

            (GraphCreationMode.Large, ClusteredScenarioType.FirebreakGap) =>
                "LargeGraph: разрывы corridor-связей и firebreak macro. Хорошо подходит для исследования критических длинных переходов.",

            (GraphCreationMode.Large, ClusteredScenarioType.HillyClusters) =>
                "LargeGraph: крупные высотные зоны и рельефные макросекторы. Проверяется влияние slope на глобальную траекторию spread.",

            (GraphCreationMode.Large, ClusteredScenarioType.WetAfterRain) =>
                "LargeGraph: влажные макрообласти с частичным затуханием и сдерживанием corridor spread.",

            (GraphCreationMode.Large, ClusteredScenarioType.MixedDryHotspots) =>
                "LargeGraph: гетерогенная карта с сухими hot spots, corridor-переходами и разной плотностью связей.",

            _ =>
                "Выберите исследовательский сценарий для текущего масштаба graph."
        };
    }

    private void UpdateMapEditorSummary()
    {
        if (_mapEditorSummaryTextBlock == null)
            return;

        if (SelectedMapCreationMode != MapCreationMode.SemiManual)
        {
            _mapEditorSummaryTextBlock.Text = "Полуручный graph editor не используется.";
            return;
        }

        int width = ParseInt(_widthBox?.Text, 24);
        int height = ParseInt(_heightBox?.Text, 24);

        if (ClusteredBlueprint == null)
        {
            _mapEditorSummaryTextBlock.Text =
                $"Blueprint ещё не задан. Для semi-manual режима нужно открыть graph editor и собрать структуру вручную. Текущая канва: {width}x{height}.";
            return;
        }

        int nodeCount = ClusteredBlueprint.Nodes?.Count ?? 0;
        int edgeCount = ClusteredBlueprint.Edges?.Count ?? 0;
        int candidateCount = ClusteredBlueprint.Candidates?.Count ?? 0;
        int clusterCount = ClusteredBlueprint.Nodes?
            .Select(x => x.ClusterId?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Count() ?? 0;

        string status = nodeCount switch
        {
            0 => "пустой blueprint",
            1 => "недостаточно узлов",
            _ when edgeCount == 0 => "нет рёбер",
            _ => "готов к использованию"
        };

        _mapEditorSummaryTextBlock.Text =
            $"Blueprint: узлов {nodeCount}, рёбер {edgeCount}, кандидатов {candidateCount}, clusters {clusterCount}. Статус: {status}.";
    }

    private void UpdateStructurePreview()
    {
        if (_structureSummaryTextBlock == null || _structureDetailTextBlock == null)
            return;

        int width = Math.Max(8, ParseInt(_widthBox?.Text, GridWidth));
        int height = Math.Max(8, ParseInt(_heightBox?.Text, GridHeight));
        int initialFireCells = Math.Max(1, ParseInt(_fireCellsBox?.Text, InitialFireCells));
        int steps = Math.Max(1, ParseInt(_stepsBox?.Text, SimulationSteps));
        int stepDurationSeconds = Math.Max(1, ParseInt(_stepDurationBox?.Text, StepDurationSeconds));

        double moistureMin = ParseDouble(_moistureMinBox?.Text, MoistureMin);
        double moistureMax = ParseDouble(_moistureMaxBox?.Text, MoistureMax);
        double elevationVariation = ParseDouble(_elevationBox?.Text, ElevationVariation);

        double temperature = ParseDouble(_tempBox?.Text, Temperature);
        double humidity = ParseDouble(_humidityBox?.Text, Humidity);
        double windSpeed = ParseDouble(_windSpeedBox?.Text, WindSpeed);
        double precipitation = ParseDouble(_precipitationBox?.Text, Precipitation);

        double mapDryness = ParseDouble(_mapDrynessBox?.Text, MapDrynessFactor);
        double reliefStrength = ParseDouble(_reliefStrengthBox?.Text, ReliefStrengthFactor);
        double fuelDensity = ParseDouble(_fuelDensityBox?.Text, FuelDensityFactor);

        string scaleText = _mode switch
        {
            GraphCreationMode.Small => "SmallGraph",
            GraphCreationMode.Medium => "MediumGraph",
            GraphCreationMode.Large => "LargeGraph",
            _ => "Graph"
        };

        string modeText = SelectedMapCreationMode switch
        {
            MapCreationMode.Random => "Random",
            MapCreationMode.Scenario => "Scenario",
            MapCreationMode.SemiManual => "Semi-manual",
            _ => "Random"
        };

        int blueprintNodes = ClusteredBlueprint?.Nodes?.Count ?? 0;
        int blueprintEdges = ClusteredBlueprint?.Edges?.Count ?? 0;
        int blueprintClusters = ClusteredBlueprint?.Nodes?
            .Select(x => x.ClusterId?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Count() ?? 0;

        int estimatedNodeCount = _mode switch
        {
            GraphCreationMode.Small => Math.Max(8, Math.Min(20, (width + height) / 2)),
            GraphCreationMode.Medium => Math.Max(20, Math.Min(80, width + height)),
            GraphCreationMode.Large => Math.Max(80, Math.Min(260, width * height / 4)),
            _ => Math.Max(20, width + height)
        };

        _structureSummaryTextBlock.Text =
            SelectedMapCreationMode == MapCreationMode.SemiManual && ClusteredBlueprint != null
                ? $"{scaleText} • semi-manual blueprint • узлов {blueprintNodes} • рёбер {blueprintEdges}"
                : $"{scaleText} • {modeText} • canvas {width}x{height} • стартовых очагов {initialFireCells}";

        _structureDetailTextBlock.Text =
            SelectedMapCreationMode switch
            {
                MapCreationMode.Random =>
                    $"Ожидаемая структура: auto-generated clustered graph. Оценка масштаба: ~{estimatedNodeCount} узлов. " +
                    $"Moisture {moistureMin:0.00}..{moistureMax:0.00}, elevation {elevationVariation:0.0}, " +
                    $"dryness {mapDryness:0.00}, relief {reliefStrength:0.00}, fuel {fuelDensity:0.00}. " +
                    $"Weather: {temperature:0.0}°C, humidity {humidity:0.0}%, wind {windSpeed:0.0} м/с, precipitation {precipitation:0.0}. " +
                    $"Steps: {steps}, длительность шага: {stepDurationSeconds} с.",

                MapCreationMode.Scenario =>
                    $"Сценарий: {SelectedClusteredScenarioType}. " +
                    $"Текущий scale: {scaleText}. " +
                    $"Weather: {temperature:0.0}°C, humidity {humidity:0.0}%, wind {windSpeed:0.0} м/с, precipitation {precipitation:0.0}. " +
                    $"Dryness {mapDryness:0.00}, relief {reliefStrength:0.00}, fuel {fuelDensity:0.00}. " +
                    $"Steps: {steps}, длительность шага: {stepDurationSeconds} с.",

                MapCreationMode.SemiManual when ClusteredBlueprint == null =>
                    $"Semi-manual mode выбран, но blueprint ещё не создан. " +
                    $"Откройте graph editor, добавьте узлы и рёбра, затем сохраните blueprint. " +
                    $"Canvas: {width}x{height}. Weather: {temperature:0.0}°C, humidity {humidity:0.0}%, wind {windSpeed:0.0} м/с.",

                MapCreationMode.SemiManual =>
                    $"Blueprint станет source of truth для clustered graph. " +
                    $"Узлов: {blueprintNodes}, рёбер: {blueprintEdges}, clusters: {blueprintClusters}. " +
                    $"Weather: {temperature:0.0}°C, humidity {humidity:0.0}%, wind {windSpeed:0.0} м/с, precipitation {precipitation:0.0}. " +
                    $"Steps: {steps}, длительность шага: {stepDurationSeconds} с.",

                _ =>
                    "Настройте параметры graph simulation."
            };
    }

    private void ApplyBalancedVegetationDefaults()
    {
        SetText(_coniferousBox, "22");
        SetText(_deciduousBox, "16");
        SetText(_mixedBox, "28");
        SetText(_grassBox, "10");
        SetText(_shrubBox, "12");
        SetText(_waterBox, "7");
        SetText(_bareBox, "5");
        UpdateVegetationDistributionFromInputs();
    }

    private void UpdateVegetationDistributionFromInputs()
    {
        var values = new List<(int VegetationType, double Probability)>
        {
            ((int)VegetationType.Coniferous, ParseDouble(_coniferousBox?.Text, 22)),
            ((int)VegetationType.Deciduous, ParseDouble(_deciduousBox?.Text, 16)),
            ((int)VegetationType.Mixed, ParseDouble(_mixedBox?.Text, 28)),
            ((int)VegetationType.Grass, ParseDouble(_grassBox?.Text, 10)),
            ((int)VegetationType.Shrub, ParseDouble(_shrubBox?.Text, 12)),
            ((int)VegetationType.Water, ParseDouble(_waterBox?.Text, 7)),
            ((int)VegetationType.Bare, ParseDouble(_bareBox?.Text, 5))
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

        SetText(_coniferousBox, GetVegetationPercentText(map, VegetationType.Coniferous, 22));
        SetText(_deciduousBox, GetVegetationPercentText(map, VegetationType.Deciduous, 16));
        SetText(_mixedBox, GetVegetationPercentText(map, VegetationType.Mixed, 28));
        SetText(_grassBox, GetVegetationPercentText(map, VegetationType.Grass, 10));
        SetText(_shrubBox, GetVegetationPercentText(map, VegetationType.Shrub, 12));
        SetText(_waterBox, GetVegetationPercentText(map, VegetationType.Water, 7));
        SetText(_bareBox, GetVegetationPercentText(map, VegetationType.Bare, 5));
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
        ClearErrors();
        error = string.Empty;

        SimulationName = _nameBox?.Text?.Trim() ?? string.Empty;

        GridWidth = Math.Max(8, ParseInt(_widthBox?.Text, GridWidth));
        GridHeight = Math.Max(8, ParseInt(_heightBox?.Text, GridHeight));
        InitialFireCells = Math.Max(1, ParseInt(_fireCellsBox?.Text, InitialFireCells));

        MoistureMin = ParseDouble(_moistureMinBox?.Text, MoistureMin);
        MoistureMax = ParseDouble(_moistureMaxBox?.Text, MoistureMax);
        ElevationVariation = Math.Max(0.0, ParseDouble(_elevationBox?.Text, ElevationVariation));

        SimulationSteps = Math.Max(1, ParseInt(_stepsBox?.Text, SimulationSteps));
        StepDurationSeconds = Math.Max(1, ParseInt(_stepDurationBox?.Text, StepDurationSeconds));

        Temperature = ParseDouble(_tempBox?.Text, Temperature);
        Humidity = ParseDouble(_humidityBox?.Text, Humidity);
        WindSpeed = Math.Max(0.0, ParseDouble(_windSpeedBox?.Text, WindSpeed));
        Precipitation = Math.Max(0.0, ParseDouble(_precipitationBox?.Text, Precipitation));

        MapNoiseStrength = Math.Max(0.0, ParseDouble(_mapNoiseBox?.Text, MapNoiseStrength));
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

        SelectedMapCreationMode = _mapCreationModeBox?.SelectedIndex switch
        {
            1 => MapCreationMode.Scenario,
            2 => MapCreationMode.SemiManual,
            _ => MapCreationMode.Random
        };

        SelectedClusteredScenarioType = GetSelectedScenarioType();
        UpdateVegetationDistributionFromInputs();

        if (string.IsNullOrWhiteSpace(SimulationName))
        {
            error = "Введите название симуляции.";
            return false;
        }

        if (GridWidth < 8 || GridHeight < 8)
        {
            error = "Для graph simulation ширина и высота канвы должны быть не меньше 8.";
            return false;
        }

        if (MoistureMin < 0.0 || MoistureMin > 1.0 || MoistureMax < 0.0 || MoistureMax > 1.0)
        {
            error = "Влажность должна быть в диапазоне от 0 до 1.";
            return false;
        }

        if (MoistureMin > MoistureMax)
        {
            error = "Минимальная влажность не может быть больше максимальной.";
            return false;
        }

        if (Humidity < 0.0 || Humidity > 100.0)
        {
            error = "Влажность воздуха должна быть в диапазоне от 0 до 100%.";
            return false;
        }

        if (Temperature < -50.0 || Temperature > 60.0)
        {
            error = "Температура должна быть в разумном диапазоне от -50 до 60 °C.";
            return false;
        }

        if (SimulationSteps <= 0)
        {
            error = "Количество шагов должно быть больше нуля.";
            return false;
        }

        if (StepDurationSeconds <= 0)
        {
            error = "Длительность шага должна быть больше нуля.";
            return false;
        }

        if (SelectedMapCreationMode == MapCreationMode.Scenario && SelectedClusteredScenarioType == null)
        {
            error = "Для режима Scenario нужно выбрать clustered-сценарий.";
            return false;
        }

        if (SelectedMapCreationMode == MapCreationMode.SemiManual)
        {
            if (ClusteredBlueprint == null)
            {
                error = "Для semi-manual graph нужно открыть editor и сохранить blueprint.";
                return false;
            }

            int nodeCount = ClusteredBlueprint.Nodes?.Count ?? 0;
            int edgeCount = ClusteredBlueprint.Edges?.Count ?? 0;

            if (nodeCount < 2)
            {
                error = "В blueprint должно быть минимум 2 узла.";
                return false;
            }

            if (edgeCount == 0)
            {
                error = "В blueprint должно быть хотя бы одно ребро.";
                return false;
            }

            bool hasEmptyCluster = ClusteredBlueprint.Nodes.Any(x => string.IsNullOrWhiteSpace(x.ClusterId));
            if (hasEmptyCluster)
            {
                error = "У всех узлов в blueprint должен быть заполнен Cluster ID.";
                return false;
            }
        }

        if (VegetationDistributions.Count == 0)
        {
            error = "Нужно задать распределение растительности.";
            return false;
        }

        double totalProbability = VegetationDistributions.Sum(x => x.Probability);
        if (totalProbability <= 0.0)
        {
            error = "Сумма вероятностей растительности должна быть больше нуля.";
            return false;
        }

        UpdateMapEditorSummary();
        UpdateStructurePreview();

        return true;
    }

    private bool ValidateCanvasSize(int width, int height, out string error)
    {
        if (width <= 0 || height <= 0)
        {
            error = "Ширина и высота должны быть больше 0.";
            return false;
        }

        switch (_mode)
        {
            case GraphCreationMode.Small:
                if (width < 10 || width > 28 || height < 10 || height > 28)
                {
                    error = "Для SmallGraph рекомендуются размеры от 10 до 28.";
                    return false;
                }
                break;

            case GraphCreationMode.Medium:
                if (width < 16 || width > 36 || height < 16 || height > 36)
                {
                    error = "Для MediumGraph рекомендуются размеры от 16 до 36.";
                    return false;
                }
                break;

            case GraphCreationMode.Large:
                if (width < 24 || width > 48 || height < 24 || height > 48)
                {
                    error = "Для LargeGraph рекомендуются размеры от 24 до 48.";
                    return false;
                }
                break;
        }

        error = string.Empty;
        return true;
    }

    private bool TryReadGraphCanvasSize(out int width, out int height, out string error)
    {
        width = ParseInt(_widthBox?.Text, 0);
        height = ParseInt(_heightBox?.Text, 0);
        return ValidateCanvasSize(width, height, out error);
    }

    private ClusteredScenarioType? GetSelectedScenarioType()
    {
        if (_scenarioTypeBox == null || _scenarioTypeBox.SelectedIndex < 0)
            return GetDefaultScenarioForScale();

        return _mode switch
        {
            GraphCreationMode.Small => _scenarioTypeBox.SelectedIndex switch
            {
                0 => ClusteredScenarioType.DenseDryConiferous,
                1 => ClusteredScenarioType.WaterBarrier,
                2 => ClusteredScenarioType.FirebreakGap,
                _ => ClusteredScenarioType.DenseDryConiferous
            },
            GraphCreationMode.Medium => _scenarioTypeBox.SelectedIndex switch
            {
                0 => ClusteredScenarioType.DenseDryConiferous,
                1 => ClusteredScenarioType.WaterBarrier,
                2 => ClusteredScenarioType.WetAfterRain,
                _ => ClusteredScenarioType.DenseDryConiferous
            },
            GraphCreationMode.Large => _scenarioTypeBox.SelectedIndex switch
            {
                0 => ClusteredScenarioType.FirebreakGap,
                1 => ClusteredScenarioType.HillyClusters,
                2 => ClusteredScenarioType.WetAfterRain,
                _ => ClusteredScenarioType.FirebreakGap
            },
            _ => ClusteredScenarioType.DenseDryConiferous
        };
    }

    private ClusteredScenarioType GetDefaultScenarioForScale()
    {
        return _mode switch
        {
            GraphCreationMode.Small => ClusteredScenarioType.DenseDryConiferous,
            GraphCreationMode.Medium => ClusteredScenarioType.DenseDryConiferous,
            GraphCreationMode.Large => ClusteredScenarioType.FirebreakGap,
            _ => ClusteredScenarioType.DenseDryConiferous
        };
    }

    private int GetScenarioSelectedIndex(ClusteredScenarioType? scenario)
    {
        if (scenario == null)
            return 0;

        return _mode switch
        {
            GraphCreationMode.Small => scenario switch
            {
                ClusteredScenarioType.DenseDryConiferous => 0,
                ClusteredScenarioType.WaterBarrier => 1,
                ClusteredScenarioType.FirebreakGap => 2,
                _ => 0
            },
            GraphCreationMode.Medium => scenario switch
            {
                ClusteredScenarioType.DenseDryConiferous => 0,
                ClusteredScenarioType.WaterBarrier => 1,
                ClusteredScenarioType.WetAfterRain => 2,
                _ => 0
            },
            GraphCreationMode.Large => scenario switch
            {
                ClusteredScenarioType.FirebreakGap => 0,
                ClusteredScenarioType.HillyClusters => 1,
                ClusteredScenarioType.WetAfterRain => 2,
                _ => 0
            },
            _ => 0
        };
    }

    private string GetScenarioDisplayName(ClusteredScenarioType? scenario)
    {
        return scenario switch
        {
            ClusteredScenarioType.DenseDryConiferous => _mode == GraphCreationMode.Small ? "Bridge-critical" : "Dense clusters",
            ClusteredScenarioType.WaterBarrier => _mode == GraphCreationMode.Small ? "Water-block" : "Water barrier",
            ClusteredScenarioType.FirebreakGap => _mode == GraphCreationMode.Small ? "Firebreak" : "Firebreak macro",
            ClusteredScenarioType.HillyClusters => "Hilly macro-zones",
            ClusteredScenarioType.WetAfterRain => _mode == GraphCreationMode.Medium ? "Wet patches" : "Wet zones",
            ClusteredScenarioType.MixedDryHotspots => "Mixed dry hotspots",
            _ => "—"
        };
    }

    private double GetSelectedWindDirectionDegrees()
    {
        return _windDirBox?.SelectedIndex switch
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

    private int GetWindDirectionIndex(double degrees)
    {
        degrees = ((degrees % 360) + 360) % 360;

        return degrees switch
        {
            >= 337.5 or < 22.5 => 0,
            >= 22.5 and < 67.5 => 1,
            >= 67.5 and < 112.5 => 2,
            >= 112.5 and < 157.5 => 3,
            >= 157.5 and < 202.5 => 4,
            >= 202.5 and < 247.5 => 5,
            >= 247.5 and < 292.5 => 6,
            _ => 7
        };
    }

    private void SetText(TextBox? textBox, string value)
    {
        if (textBox != null)
            textBox.Text = value;
    }

    private void ShowError(string error)
    {
        if (_errorTextBlock != null)
            _errorTextBlock.Text = error;
    }

    private void ClearErrors()
    {
        if (_errorTextBlock != null)
            _errorTextBlock.Text = string.Empty;
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

    private static int? TryParseNullableInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}