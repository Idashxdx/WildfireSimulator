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
        var graphScaleType = _mode switch
        {
            GraphCreationMode.Small => GraphScaleType.Small,
            GraphCreationMode.Medium => GraphScaleType.Medium,
            GraphCreationMode.Large => GraphScaleType.Large,
            _ => GraphScaleType.Medium
        };

        var mapCreationMode = _mode == GraphCreationMode.Small
            ? MapCreationMode.Random
            : SelectedMapCreationMode;

        var clusteredScenarioType =
            mapCreationMode == MapCreationMode.Scenario && _mode != GraphCreationMode.Small
                ? SelectedClusteredScenarioType
                : null;

        var clusteredBlueprint =
            mapCreationMode == MapCreationMode.SemiManual
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
            GraphCreationMode.Small => new List<string>(),

            GraphCreationMode.Medium => new List<string>
        {
            "Сухой хвойный массив",
            "Водный барьер",
            "Просека",
            "Холмистые зоны",
            "Смешанный лес"
        },

            GraphCreationMode.Large => new List<string>
        {
            "Сухие макрозоны",
            "Водный барьер",
            "Просека и обходы",
            "Холмистые макрозоны",
            "Смешанные очаги"
        },

            _ => new List<string>()
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

        if (_nameBox != null) _nameBox.Text = SimulationName;
        if (_widthBox != null) _widthBox.Text = GridWidth.ToString();
        if (_heightBox != null) _heightBox.Text = GridHeight.ToString();
        if (_fireCellsBox != null) _fireCellsBox.Text = InitialFireCells.ToString();
        if (_moistureMinBox != null) _moistureMinBox.Text = MoistureMin.ToString("0.00", CultureInfo.InvariantCulture);
        if (_moistureMaxBox != null) _moistureMaxBox.Text = MoistureMax.ToString("0.00", CultureInfo.InvariantCulture);
        if (_elevationBox != null) _elevationBox.Text = ElevationVariation.ToString("0", CultureInfo.InvariantCulture);
        if (_stepsBox != null) _stepsBox.Text = SimulationSteps.ToString();
        if (_stepDurationBox != null) _stepDurationBox.Text = StepDurationSeconds.ToString();
        if (_tempBox != null) _tempBox.Text = Temperature.ToString("0", CultureInfo.InvariantCulture);
        if (_humidityBox != null) _humidityBox.Text = Humidity.ToString("0", CultureInfo.InvariantCulture);
        if (_windSpeedBox != null) _windSpeedBox.Text = WindSpeed.ToString("0.0", CultureInfo.InvariantCulture);
        if (_precipitationBox != null) _precipitationBox.Text = Precipitation.ToString("0.0", CultureInfo.InvariantCulture);
        if (_randomSeedBox != null) _randomSeedBox.Text = string.Empty;

        if (_windDirBox != null)
            _windDirBox.SelectedIndex = 1;

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectedIndex = 0;

        if (_scenarioTypeBox != null)
            _scenarioTypeBox.SelectedIndex = -1;

        if (_mapNoiseBox != null) _mapNoiseBox.Text = MapNoiseStrength.ToString("0.00", CultureInfo.InvariantCulture);
        if (_mapDrynessBox != null) _mapDrynessBox.Text = MapDrynessFactor.ToString("0.00", CultureInfo.InvariantCulture);
        if (_reliefStrengthBox != null) _reliefStrengthBox.Text = ReliefStrengthFactor.ToString("0.00", CultureInfo.InvariantCulture);
        if (_fuelDensityBox != null) _fuelDensityBox.Text = FuelDensityFactor.ToString("0.00", CultureInfo.InvariantCulture);

        WriteVegetationDistributionToInputs();
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

        _typeInfoTextBlock.Text = _mode switch
        {
            GraphCreationMode.Small => "Малый случайный граф",
            GraphCreationMode.Medium => "Средний случайный граф",
            GraphCreationMode.Large => "Большой случайный граф",
            _ => "Графовая симуляция"
        };

        _typeHintTextBlock.Text = _mode switch
        {
            GraphCreationMode.Small =>
                "Компактный случайный граф без демо-сценариев. Используется для проверки связности, стартовых очагов и влияния параметров среды.",
            GraphCreationMode.Medium =>
                "Случайный граф из нескольких связанных областей. Внутри области распространение легче, между областями — сложнее.",
            GraphCreationMode.Large =>
                "Крупный случайный граф из нескольких макрообластей с редкими переходами между ними.",
            _ =>
                "Графовая симуляция распространения пожара."
        };

        _widthLabelTextBlock.Text = "Ширина области размещения";
        _heightLabelTextBlock.Text = "Высота области размещения";

        _widthHintTextBlock.Text = "Не количество вершин, а размер поля, внутри которого размещается граф.";
        _heightHintTextBlock.Text = "Не количество вершин, а размер поля, внутри которого размещается граф.";

        _fireCellsHintTextBlock.Text =
            "Стартовые очаги будут выбраны случайно или вручную на визуализации графа.";

        UpdatePresetButtonsUi();
    }


    private void UpdatePresetButtonsUi()
    {
        bool showDemoButtons = _mode != GraphCreationMode.Small;

        SetPresetButtonVisible(_presetButton1, showDemoButtons);
        SetPresetButtonVisible(_presetButton2, showDemoButtons);
        SetPresetButtonVisible(_presetButton3, showDemoButtons);
        SetPresetButtonVisible(_presetButton4, showDemoButtons);
        SetPresetButtonVisible(_presetButton5, showDemoButtons);

        if (_presetHintTextBlock != null)
        {
            _presetHintTextBlock.Text = _mode switch
            {
                GraphCreationMode.Small =>
                    "Малый граф создаётся только случайно. Демо-сценарии для него отключены.",
                GraphCreationMode.Medium =>
                    "По умолчанию создаётся случайный средний граф. Демо можно будет доработать отдельно.",
                GraphCreationMode.Large =>
                    "По умолчанию создаётся случайный большой граф. Демо можно будет доработать отдельно.",
                _ =>
                    "Графовая модель."
            };
        }

        if (_presetButton1 != null)
        {
            _presetButton1.Content = _mode == GraphCreationMode.Large
                ? "Сухие макрозоны"
                : "Сухой хвойный массив";
            _presetButton1.Tag = "dry";
        }

        if (_presetButton2 != null)
        {
            _presetButton2.Content = "Водный барьер";
            _presetButton2.Tag = "water";
        }

        if (_presetButton3 != null)
        {
            _presetButton3.Content = _mode == GraphCreationMode.Large
                ? "Просека и обходы"
                : "Просека";
            _presetButton3.Tag = "firebreak";
        }

        if (_presetButton4 != null)
        {
            _presetButton4.Content = _mode == GraphCreationMode.Large
                ? "Холмистые макрозоны"
                : "Холмистые зоны";
            _presetButton4.Tag = "hills";
        }

        if (_presetButton5 != null)
        {
            _presetButton5.Content = _mode == GraphCreationMode.Large
                ? "Смешанные очаги"
                : "Смешанный лес";
            _presetButton5.Tag = "mixed";
        }
    }
    private void SetPresetButtonVisible(Button? button, bool isVisible)
    {
        if (button == null)
            return;

        button.IsVisible = isVisible;
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

        if (_mode == GraphCreationMode.Small)
            return;

        ClearErrors();
        ClusteredBlueprint = null;

        SelectedMapCreationMode = MapCreationMode.Scenario;

        var tag = button.Tag?.ToString()?.Trim().ToLowerInvariant();

        switch (tag)
        {
            case "dry":
                SelectedClusteredScenarioType = ClusteredScenarioType.DenseDryConiferous;

                if (_nameBox != null) _nameBox.Text = _mode == GraphCreationMode.Large ? "ДЕМО: Сухие макрозоны" : "ДЕМО: Сухой хвойный граф";
                if (_tempBox != null) _tempBox.Text = "31";
                if (_humidityBox != null) _humidityBox.Text = "22";
                if (_windSpeedBox != null) _windSpeedBox.Text = "8";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.08";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.32";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.20";
                if (_fuelDensityBox != null) _fuelDensityBox.Text = "1.15";
                break;

            case "water":
                SelectedClusteredScenarioType = ClusteredScenarioType.WaterBarrier;

                if (_nameBox != null) _nameBox.Text = "ДЕМО: Граф водный барьер";
                if (_tempBox != null) _tempBox.Text = "24";
                if (_humidityBox != null) _humidityBox.Text = "46";
                if (_windSpeedBox != null) _windSpeedBox.Text = "4";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.22";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.58";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "0.95";
                break;

            case "firebreak":
                SelectedClusteredScenarioType = ClusteredScenarioType.FirebreakGap;

                if (_nameBox != null) _nameBox.Text = _mode == GraphCreationMode.Large ? "ДЕМО: Просека и обходы" : "ДЕМО: Граф просека";
                if (_tempBox != null) _tempBox.Text = "28";
                if (_humidityBox != null) _humidityBox.Text = "32";
                if (_windSpeedBox != null) _windSpeedBox.Text = "6";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.16";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.46";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.08";
                break;

            case "hills":
                SelectedClusteredScenarioType = ClusteredScenarioType.HillyClusters;

                if (_nameBox != null) _nameBox.Text = _mode == GraphCreationMode.Large ? "ДЕМО: Холмистые макрозоны" : "ДЕМО: Граф холмистые зоны";
                if (_tempBox != null) _tempBox.Text = "26";
                if (_humidityBox != null) _humidityBox.Text = "40";
                if (_windSpeedBox != null) _windSpeedBox.Text = "5";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_elevationBox != null) _elevationBox.Text = _mode == GraphCreationMode.Large ? "120" : "85";
                if (_reliefStrengthBox != null) _reliefStrengthBox.Text = "1.25";
                break;

            case "mixed":
            default:
                SelectedClusteredScenarioType = ClusteredScenarioType.MixedDryHotspots;

                if (_nameBox != null) _nameBox.Text = _mode == GraphCreationMode.Large ? "ДЕМО: Смешанные макрозоны" : "ДЕМО: Смешанный граф";
                if (_tempBox != null) _tempBox.Text = "25";
                if (_humidityBox != null) _humidityBox.Text = "40";
                if (_windSpeedBox != null) _windSpeedBox.Text = "5";
                if (_precipitationBox != null) _precipitationBox.Text = "0";
                if (_moistureMinBox != null) _moistureMinBox.Text = "0.24";
                if (_moistureMaxBox != null) _moistureMaxBox.Text = "0.62";
                if (_mapDrynessBox != null) _mapDrynessBox.Text = "1.00";
                break;
        }

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectedIndex = 1;

        UpdateMapModeUi();
        UpdateScenarioDescription();
        UpdateMapEditorSummary();
        UpdateStructurePreview();
    }
    private void UpdateMapModeUi()
    {
        if (_mode == GraphCreationMode.Small)
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
                _mapModeDescriptionTextBlock.Text =
                    "Малый граф всегда создаётся случайно. Демо и ручной редактор для него отключены.";
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
            MapCreationMode.Random =>
                "Случайная генерация: граф строится из областей, внутри которых связи плотнее, а переходы между областями слабее.",
            MapCreationMode.Scenario =>
                "Демо-сценарий: структура графа и параметры среды задаются выбранным готовым вариантом.",
            MapCreationMode.SemiManual =>
                "Ручной режим: структура будет задаваться через редактор графа. Редактор будет доработан отдельно.",
            _ =>
                "Выберите режим построения графа."
        };
    }


    private void UpdateScenarioDescription()
    {
        if (_scenarioDescriptionTextBlock == null)
            return;

        if (_mode == GraphCreationMode.Small)
        {
            _scenarioDescriptionTextBlock.Text =
                "Для малого графа сценарии не используются.";
            return;
        }

        if (SelectedMapCreationMode == MapCreationMode.Random)
        {
            _scenarioDescriptionTextBlock.Text =
                "Будет создан случайный граф: несколько областей, плотные связи внутри областей и слабые переходы между ними.";
            return;
        }

        var scenario = SelectedClusteredScenarioType ?? GetDefaultScenarioForMode();

        _scenarioDescriptionTextBlock.Text = scenario switch
        {
            ClusteredScenarioType.DenseDryConiferous =>
                "Сухие хвойные зоны с высокой скоростью распространения и несколькими переходами между группами.",
            ClusteredScenarioType.WaterBarrier =>
                "Лесные зоны разделены водным барьером. Распространение возможно только через ограниченные обходные связи.",
            ClusteredScenarioType.FirebreakGap =>
                "Просека снижает вероятность перехода огня между частями графа, но отдельные переходы сохраняют риск распространения.",
            ClusteredScenarioType.HillyClusters =>
                "Холмистая карта: высота и уклон влияют на переход огня между зонами.",
            ClusteredScenarioType.WetAfterRain =>
                "Влажный лес после дождя. Повышенная влажность снижает скорость распространения.",
            ClusteredScenarioType.MixedDryHotspots =>
                "Смешанная территория с сухими очагами, травяными участками и влажными зонами.",
            _ =>
                "Графовое демо."
        };
    }
    private void UpdateMapEditorSummary()
    {
        if (_mapEditorSummaryTextBlock == null)
            return;

        if (_mode == GraphCreationMode.Small)
        {
            _mapEditorSummaryTextBlock.Text =
                "Для малого графа редактор не используется.";
            return;
        }

        if (SelectedMapCreationMode == MapCreationMode.Random)
        {
            _mapEditorSummaryTextBlock.Text =
                "Случайная генерация: редактор итогового графа не используется.";
            return;
        }

        if (SelectedMapCreationMode == MapCreationMode.Scenario)
        {
            _mapEditorSummaryTextBlock.Text =
                "Демо задаёт структуру графа автоматически.";
            return;
        }

        _mapEditorSummaryTextBlock.Text =
            ClusteredBlueprint == null
                ? "Редактор графа пока не настроен."
                : $"Редактор графа: вершин {ClusteredBlueprint.Nodes.Count}, рёбер {ClusteredBlueprint.Edges.Count}.";
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

        if (_mode == GraphCreationMode.Small)
        {
            _structureSummaryTextBlock.Text = $"Случайная генерация • {scaleText}";
            _structureDetailTextBlock.Text =
                "Будет создан компактный связный граф без сценариев: разные типы растительности, локальные связи и случайные стартовые очаги.";
            return;
        }

        if (SelectedMapCreationMode == MapCreationMode.Random)
        {
            _structureSummaryTextBlock.Text = $"Случайная генерация • {scaleText}";

            _structureDetailTextBlock.Text = _mode switch
            {
                GraphCreationMode.Medium =>
                    "Будет создан граф из нескольких областей. Внутри области связи плотнее, между областями — редкие слабые переходы.",
                GraphCreationMode.Large =>
                    "Будет создан крупный граф из макрообластей. Внутри макрообластей огонь распространяется легче, между ними — сложнее.",
                _ =>
                    "Будет создан случайный граф."
            };

            return;
        }

        var scenario = SelectedClusteredScenarioType ?? GetDefaultScenarioForMode();

        _structureSummaryTextBlock.Text =
            $"Демо: {GetScenarioDisplayName(scenario)} • {scaleText}";

        _structureDetailTextBlock.Text = _mode switch
        {
            GraphCreationMode.Medium =>
                "Средний демо-граф строится как карта из нескольких связанных зон.",
            GraphCreationMode.Large =>
                "Большой демо-граф строится как карта макрообластей.",
            _ =>
                "Графовая модель распространения пожара."
        };
    }

    private string GetScenarioDisplayName(ClusteredScenarioType scenario)
    {
        return scenario switch
        {
            ClusteredScenarioType.DenseDryConiferous => "сухой хвойный массив",
            ClusteredScenarioType.WaterBarrier => "водный барьер",
            ClusteredScenarioType.FirebreakGap => "просека",
            ClusteredScenarioType.HillyClusters => "холмистые зоны",
            ClusteredScenarioType.WetAfterRain => "влажный лес после дождя",
            ClusteredScenarioType.MixedDryHotspots => "смешанный лес и сухие очаги",
            _ => "графовое демо"
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
        error = string.Empty;

        SimulationName = _nameBox?.Text?.Trim() ?? string.Empty;

        GridWidth = Math.Max(8, ParseInt(_widthBox?.Text, GridWidth));
        GridHeight = Math.Max(8, ParseInt(_heightBox?.Text, GridHeight));
        InitialFireCells = Math.Max(1, ParseInt(_fireCellsBox?.Text, InitialFireCells));

        MoistureMin = ParseDouble(_moistureMinBox?.Text, MoistureMin);
        MoistureMax = ParseDouble(_moistureMaxBox?.Text, MoistureMax);
        ElevationVariation = Math.Max(0.0, ParseDouble(_elevationBox?.Text, ElevationVariation));

        SimulationSteps = Math.Max(1, ParseInt(_stepsBox?.Text, SimulationSteps));
        StepDurationSeconds = Math.Clamp(ParseInt(_stepDurationBox?.Text, StepDurationSeconds), 1, 7200);

        Temperature = ParseDouble(_tempBox?.Text, Temperature);
        Humidity = ParseDouble(_humidityBox?.Text, Humidity);
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

        SelectedMapCreationMode = _mode == GraphCreationMode.Small
            ? MapCreationMode.Random
            : _mapCreationModeBox?.SelectedIndex switch
            {
                1 => MapCreationMode.Scenario,
                2 => MapCreationMode.SemiManual,
                _ => MapCreationMode.Random
            };

        SelectedClusteredScenarioType =
            SelectedMapCreationMode == MapCreationMode.Scenario && _mode != GraphCreationMode.Small
                ? GetSelectedScenarioType()
                : null;

        if (SelectedMapCreationMode != MapCreationMode.SemiManual)
            ClusteredBlueprint = null;

        UpdateVegetationDistributionFromInputs();

        if (string.IsNullOrWhiteSpace(SimulationName))
        {
            error = "Введите название симуляции.";
            return false;
        }

        if (GridWidth < 8 || GridHeight < 8)
        {
            error = "Ширина и высота области размещения должны быть не меньше 8.";
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
            error = "Температура должна быть в диапазоне от -50 до 60 °C.";
            return false;
        }

        if (WindSpeed > 40.0)
        {
            error = "Скорость ветра должна быть в диапазоне от 0 до 40 м/с.";
            return false;
        }

        if (Precipitation > 100.0)
        {
            error = "Осадки должны быть в диапазоне от 0 до 100.";
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
            error = "Для демо-сценария нужно выбрать тип графового демо.";
            return false;
        }

        if (SelectedMapCreationMode == MapCreationMode.SemiManual &&
            (ClusteredBlueprint == null || ClusteredBlueprint.Nodes.Count == 0))
        {
            error = "Для ручного режима нужно сначала подготовить граф в редакторе.";
            return false;
        }

        double totalProbability = VegetationDistributions.Sum(x => x.Probability);
        if (totalProbability <= 0.0)
        {
            error = "Суммарная вероятность типов растительности должна быть больше нуля.";
            return false;
        }

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
    private ClusteredScenarioType GetDefaultScenarioForMode()
    {
        return _mode switch
        {
            GraphCreationMode.Medium => ClusteredScenarioType.MixedDryHotspots,
            GraphCreationMode.Large => ClusteredScenarioType.HillyClusters,
            _ => ClusteredScenarioType.MixedDryHotspots
        };
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