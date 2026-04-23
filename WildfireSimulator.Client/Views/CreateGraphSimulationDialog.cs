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
            "Критический мост",
            "Водный барьер",
            "Просека"
        },
            GraphCreationMode.Medium => new List<string>
        {
            "Плотные кластеры",
            "Водный барьер",
            "Влажные зоны"
        },
            GraphCreationMode.Large => new List<string>
        {
            "Макрозоны с просекой",
            "Холмистые макрозоны",
            "Влажные макрозоны"
        },
            _ => new List<string> { "Сценарий по умолчанию" }
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
            GraphCreationMode.Small => "ДЕМО: Малый граф",
            GraphCreationMode.Medium => "ДЕМО: Средний граф",
            GraphCreationMode.Large => "ДЕМО: Большой граф",
            _ => "ДЕМО: Графовая симуляция"
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

        MoistureMin = 0.30;
        MoistureMax = 0.70;
        ElevationVariation = 50.0;
        SimulationSteps = 100;
        StepDurationSeconds = 900;
        Temperature = 25.0;
        Humidity = 40.0;
        WindSpeed = 5.0;
        WindDirection = 45.0;
        Precipitation = 0.0;
        RandomSeed = null;

        SelectedMapCreationMode = MapCreationMode.Random;
        SelectedClusteredScenarioType = ClusteredScenarioType.DenseDryConiferous;

        MapNoiseStrength = 0.08;
        MapDrynessFactor = 1.0;
        ReliefStrengthFactor = 1.0;
        FuelDensityFactor = 1.0;

        VegetationDistributions = new List<(int VegetationType, double Probability)>
    {
        ((int)VegetationType.Coniferous, 0.25),
        ((int)VegetationType.Deciduous, 0.20),
        ((int)VegetationType.Mixed, 0.20),
        ((int)VegetationType.Grass, 0.15),
        ((int)VegetationType.Shrub, 0.10),
        ((int)VegetationType.Water, 0.05),
        ((int)VegetationType.Bare, 0.05)
    };

        ClusteredBlueprint = null;

        if (_nameBox != null) _nameBox.Text = SimulationName;
        if (_widthBox != null) _widthBox.Text = GridWidth.ToString();
        if (_heightBox != null) _heightBox.Text = GridHeight.ToString();
        if (_fireCellsBox != null) _fireCellsBox.Text = InitialFireCells.ToString();
        if (_moistureMinBox != null) _moistureMinBox.Text = MoistureMin.ToString("0.00", CultureInfo.InvariantCulture);
        if (_moistureMaxBox != null) _moistureMaxBox.Text = MoistureMax.ToString("0.00", CultureInfo.InvariantCulture);
        if (_elevationBox != null) _elevationBox.Text = ElevationVariation.ToString("0.0", CultureInfo.InvariantCulture);
        if (_stepsBox != null) _stepsBox.Text = SimulationSteps.ToString();
        if (_stepDurationBox != null) _stepDurationBox.Text = StepDurationSeconds.ToString();
        if (_tempBox != null) _tempBox.Text = Temperature.ToString("0.0", CultureInfo.InvariantCulture);
        if (_humidityBox != null) _humidityBox.Text = Humidity.ToString("0.0", CultureInfo.InvariantCulture);
        if (_windSpeedBox != null) _windSpeedBox.Text = WindSpeed.ToString("0.0", CultureInfo.InvariantCulture);
        if (_precipitationBox != null) _precipitationBox.Text = Precipitation.ToString("0.0", CultureInfo.InvariantCulture);
        if (_randomSeedBox != null) _randomSeedBox.Text = string.Empty;

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectedIndex = 0;

        if (_scenarioTypeBox != null)
            _scenarioTypeBox.SelectedIndex = 0;

        if (_mapNoiseBox != null) _mapNoiseBox.Text = "0.08";
        if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.0";
        if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.0";
        if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.0";

        if (_coniferousBox != null) _coniferousBox.Text = "0.25";
        if (_deciduousBox != null) _deciduousBox.Text = "0.20";
        if (_mixedBox != null) _mixedBox.Text = "0.20";
        if (_grassBox != null) _grassBox.Text = "0.15";
        if (_shrubBox != null) _shrubBox.Text = "0.10";
        if (_waterBox != null) _waterBox.Text = "0.05";
        if (_bareBox != null) _bareBox.Text = "0.05";

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

        _typeInfoTextBlock.Text = "Графовая симуляция";

        _typeHintTextBlock.Text = _mode switch
        {
            GraphCreationMode.Small =>
                "Малый граф для анализа мостов, развилок и критических переходов.",
            GraphCreationMode.Medium =>
                "Средний граф для анализа кластеров, барьеров и локальных связей.",
            GraphCreationMode.Large =>
                "Большой граф для анализа макрозон и длинных коридоров распространения.",
            _ =>
                "Графовая симуляция распространения пожара."
        };

        _widthLabelTextBlock.Text = "Ширина области";
        _heightLabelTextBlock.Text = "Высота области";

        _widthHintTextBlock.Text = "Размер области по горизонтали.";
        _heightHintTextBlock.Text = "Размер области по вертикали.";

        _fireCellsHintTextBlock.Text =
            "Стартовые очаги будут выбраны случайно или вручную на визуализации графа.";

        UpdatePresetButtonsUi();
    }

    private void UpdatePresetButtonsUi()
    {
        if (_presetHintTextBlock == null)
            return;

        _presetHintTextBlock.Text =
            "Готовые сценарии для графовой модели. Кнопка сразу подставляет режим, сценарий и основные параметры.";

        if (_presetButton1 != null)
        {
            _presetButton1.Content = _mode switch
            {
                GraphCreationMode.Small => "Критический мост",
                GraphCreationMode.Medium => "Плотные кластеры",
                GraphCreationMode.Large => "Макрозоны с просекой",
                _ => "Сценарий 1"
            };
            _presetButton1.Tag = "dry-coniferous";
        }

        if (_presetButton2 != null)
        {
            _presetButton2.Content = _mode switch
            {
                GraphCreationMode.Small => "Водный барьер",
                GraphCreationMode.Medium => "Водный барьер",
                GraphCreationMode.Large => "Холмистые зоны",
                _ => "Сценарий 2"
            };
            _presetButton2.Tag = "river";
        }

        if (_presetButton3 != null)
        {
            _presetButton3.Content = _mode switch
            {
                GraphCreationMode.Small => "Просека",
                GraphCreationMode.Medium => "Влажные зоны",
                GraphCreationMode.Large => "Влажные зоны",
                _ => "Сценарий 3"
            };
            _presetButton3.Tag = "wet";
        }

        if (_presetButton4 != null)
        {
            _presetButton4.Content = _mode switch
            {
                GraphCreationMode.Small => "Сухой ветер",
                GraphCreationMode.Medium => "Жаркие кластеры",
                GraphCreationMode.Large => "Нагрузка на коридоры",
                _ => "Сценарий 4"
            };
            _presetButton4.Tag = "firebreak";
        }

        if (_presetButton5 != null)
        {
            _presetButton5.Content = "Сбалансированный";
            _presetButton5.Tag = "hills";
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
                if (_scenarioTypeBox != null)
                    _scenarioTypeBox.SelectedIndex = 0;

                if (_windSpeedBox != null) _windSpeedBox.Text = "8";
                if (_tempBox != null) _tempBox.Text = "31";
                if (_humidityBox != null) _humidityBox.Text = "22";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.20";
                if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.15";
                if (_precipitationBox != null) _precipitationBox.Text = "0.0";
                break;

            case "river":
                if (_scenarioTypeBox != null)
                    _scenarioTypeBox.SelectedIndex = 1;

                if (_humidityBox != null) _humidityBox.Text = "45";
                if (_windSpeedBox != null) _windSpeedBox.Text = "4";
                if (_precipitationBox != null) _precipitationBox.Text = "0.0";
                break;

            case "wet":
                if (_scenarioTypeBox != null)
                    _scenarioTypeBox.SelectedIndex = 2;

                if (_humidityBox != null) _humidityBox.Text = "78";
                if (_precipitationBox != null) _precipitationBox.Text = "2.5";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "0.82";
                break;

            case "firebreak":
                if (_scenarioTypeBox != null)
                    _scenarioTypeBox.SelectedIndex = 0;

                if (_windSpeedBox != null) _windSpeedBox.Text = "7";
                if (_tempBox != null) _tempBox.Text = "29";
                if (_humidityBox != null) _humidityBox.Text = "28";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.10";
                break;

            case "hills":
                if (_scenarioTypeBox != null)
                    _scenarioTypeBox.SelectedIndex = Math.Min(2, _scenarioTypeBox.ItemCount - 1);

                if (_windSpeedBox != null) _windSpeedBox.Text = "5";
                if (_tempBox != null) _tempBox.Text = "26";
                if (_humidityBox != null) _humidityBox.Text = "40";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.00";
                if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.20";
                break;
        }

        UpdateScenarioDescription();
        UpdateStructurePreview();
        UpdateMapEditorSummary();
    }

    private void UpdateMapModeUi()
    {
        if (_scenarioPanel == null ||
            _semiManualPanel == null ||
            _mapModeDescriptionTextBlock == null ||
            _semiManualDescriptionTextBlock == null)
        {
            return;
        }

        _scenarioPanel.IsVisible = SelectedMapCreationMode == MapCreationMode.Scenario;
        _semiManualPanel.IsVisible = SelectedMapCreationMode == MapCreationMode.SemiManual;

        _mapModeDescriptionTextBlock.Text = SelectedMapCreationMode switch
        {
            MapCreationMode.Random =>
                "Граф будет сгенерирован автоматически по выбранному масштабу и параметрам среды.",
            MapCreationMode.Scenario =>
                "Будет использован готовый сценарий с характерной структурой графа.",
            MapCreationMode.SemiManual =>
                "Структура графа задаётся вручную: вершины, рёбра, кластеры и свойства узлов.",
            _ =>
                "Выберите режим создания графа."
        };

        _semiManualDescriptionTextBlock.Text =
            "Откройте редактор, чтобы построить собственную структуру: добавить вершины, соединить их рёбрами и настроить параметры.";
    }

    private void UpdateScenarioDescription()
    {
        if (_scenarioDescriptionTextBlock == null)
            return;

        _scenarioDescriptionTextBlock.Text = SelectedClusteredScenarioType switch
        {
            ClusteredScenarioType.DenseDryConiferous =>
                "Плотные сухие кластеры хвойной растительности. Высокий риск быстрого распространения.",
            ClusteredScenarioType.WaterBarrier =>
                "Между группами вершин присутствует водный барьер, который сдерживает огонь.",
            ClusteredScenarioType.FirebreakGap =>
                "В графе есть разрыв или просека, ограничивающие переход огня между зонами.",
            ClusteredScenarioType.HillyClusters =>
                "Кластеры расположены на неоднородном рельефе, что влияет на уклон и распространение.",
            ClusteredScenarioType.WetAfterRain =>
                "Повышенная влажность после осадков уменьшает вероятность распространения.",
            ClusteredScenarioType.MixedDryHotspots =>
                "Смешанная структура с сухими очагами, где возможны локальные ускорения.",
            _ =>
                "Выберите сценарий графа."
        };
    }

    private void UpdateMapEditorSummary()
    {
        if (_mapEditorSummaryTextBlock == null)
            return;

        if (SelectedMapCreationMode != MapCreationMode.SemiManual)
        {
            _mapEditorSummaryTextBlock.Text = "Полуручной режим не выбран.";
            return;
        }

        if (ClusteredBlueprint == null || ClusteredBlueprint.Nodes.Count == 0)
        {
            _mapEditorSummaryTextBlock.Text = "Структура графа не задана.";
            return;
        }

        int clusterCount = ClusteredBlueprint.Nodes
            .Select(n => n.ClusterId?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Count();

        _mapEditorSummaryTextBlock.Text =
            $"Структура задана: вершин {ClusteredBlueprint.Nodes.Count}, рёбер {ClusteredBlueprint.Edges.Count}, кластеров {clusterCount}.";
    }
    private void UpdateStructurePreview()
    {
        if (_structureSummaryTextBlock == null || _structureDetailTextBlock == null)
            return;

        string scaleText = _mode switch
        {
            GraphCreationMode.Small => "малый граф",
            GraphCreationMode.Medium => "средний граф",
            GraphCreationMode.Large => "большой граф",
            _ => "граф"
        };

        _structureSummaryTextBlock.Text = SelectedMapCreationMode switch
        {
            MapCreationMode.Random => $"Режим: случайная генерация • {scaleText}",
            MapCreationMode.Scenario => $"Режим: сценарий • {scaleText}",
            MapCreationMode.SemiManual => $"Режим: полуручная структура • {scaleText}",
            _ => $"Режим: {scaleText}"
        };

        if (SelectedMapCreationMode == MapCreationMode.SemiManual)
        {
            if (ClusteredBlueprint == null || ClusteredBlueprint.Nodes.Count == 0)
            {
                _structureDetailTextBlock.Text =
                    "Структура пока не задана. Откройте редактор и создайте вершины и рёбра.";
                return;
            }

            int clusterCount = ClusteredBlueprint.Nodes
                .Select(n => n.ClusterId?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Count();

            _structureDetailTextBlock.Text =
                $"Структура задана: вершин {ClusteredBlueprint.Nodes.Count}, рёбер {ClusteredBlueprint.Edges.Count}, кластеров {clusterCount}.";
            return;
        }

        _structureDetailTextBlock.Text = _mode switch
        {
            GraphCreationMode.Small =>
                "Подходит для анализа мостов, узких мест и небольших переходов.",
            GraphCreationMode.Medium =>
                "Подходит для анализа кластеров, барьеров и локального распространения.",
            GraphCreationMode.Large =>
                "Подходит для анализа макрозон и коридоров распространения.",
            _ =>
                "Графовая модель распространения пожара."
        };
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