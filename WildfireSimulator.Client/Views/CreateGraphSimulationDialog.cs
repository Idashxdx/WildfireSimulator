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

            MapRegionObjects = new List<MapRegionObjectDto>(),
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

        _scenarioTypeBox.Items.Add(new ComboBoxItem
        {
            Content = _mode switch
            {
                GraphCreationMode.Small => "Два кластера и мост",
                GraphCreationMode.Medium => "Сухие clustered patches",
                GraphCreationMode.Large => "Сухие крупные сектора",
                _ => "Базовый graph-сценарий"
            }
        });

        _scenarioTypeBox.Items.Add(new ComboBoxItem
        {
            Content = _mode switch
            {
                GraphCreationMode.Small => "Водный узел-барьер",
                GraphCreationMode.Medium => "Водный барьер между патчами",
                GraphCreationMode.Large => "Макро-водный барьер",
                _ => "Водный барьер"
            }
        });

        _scenarioTypeBox.Items.Add(new ComboBoxItem
        {
            Content = _mode switch
            {
                GraphCreationMode.Small => "Разрыв / firebreak",
                GraphCreationMode.Medium => "Firebreak gap между патчами",
                GraphCreationMode.Large => "Разрывы corridor-связей",
                _ => "Разрыв / firebreak"
            }
        });

        _scenarioTypeBox.Items.Add(new ComboBoxItem
        {
            Content = _mode switch
            {
                GraphCreationMode.Small => "Холмистая topology",
                GraphCreationMode.Medium => "Холмистые кластеры",
                GraphCreationMode.Large => "Крупные зоны с рельефом",
                _ => "Холмистый graph"
            }
        });

        _scenarioTypeBox.Items.Add(new ComboBoxItem
        {
            Content = _mode switch
            {
                GraphCreationMode.Small => "Влажный граф после дождя",
                GraphCreationMode.Medium => "Влажные patch-зоны",
                GraphCreationMode.Large => "Влажные крупные области",
                _ => "Влажный graph"
            }
        });

        _scenarioTypeBox.Items.Add(new ComboBoxItem
        {
            Content = _mode switch
            {
                GraphCreationMode.Small => "Компактные dry hot spots",
                GraphCreationMode.Medium => "Mixed hot spots",
                GraphCreationMode.Large => "Неоднородные зоны сухости",
                _ => "Mixed hot spots"
            }
        });

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
            _nameBox.Text = _mode switch
            {
                GraphCreationMode.Small => "Малый граф",
                GraphCreationMode.Medium => "Средний граф",
                GraphCreationMode.Large => "Большой граф",
                _ => "Графовая симуляция"
            };

        if (_widthBox != null)
            _widthBox.Text = _mode switch
            {
                GraphCreationMode.Small => "12",
                GraphCreationMode.Medium => "24",
                GraphCreationMode.Large => "40",
                _ => "24"
            };

        if (_heightBox != null)
            _heightBox.Text = _mode switch
            {
                GraphCreationMode.Small => "12",
                GraphCreationMode.Medium => "24",
                GraphCreationMode.Large => "40",
                _ => "24"
            };

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

        ClusteredBlueprint = null;
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
            GraphCreationMode.Small => "Малый граф",
            GraphCreationMode.Medium => "Средний граф",
            GraphCreationMode.Large => "Большой граф",
            _ => "Графовая симуляция"
        };

        _typeHintTextBlock.Text = _mode switch
        {
            GraphCreationMode.Small =>
                "SmallGraph — topology-first графовая модель. Важны отдельные вершины, рёбра, мосты и локальные развилки распространения.",
            GraphCreationMode.Medium =>
                "MediumGraph — patch/cluster графовая модель. Важны локальные группы узлов, барьеры, мосты между патчами и неоднородная связность.",
            GraphCreationMode.Large =>
                "LargeGraph — area-like графовая модель. Поведение читается на уровне крупных зон, макросекторов и corridor-связей между областями.",
            _ =>
                "Графовая модель леса строится как система узлов и рёбер с неоднородной связностью."
        };

        _widthLabelTextBlock.Text = "Ширина поля графа";
        _heightLabelTextBlock.Text = "Высота поля графа";

        _widthHintTextBlock.Text = "Размер рабочей области, внутри которой размещаются кандидаты, узлы и связи графа.";
        _heightHintTextBlock.Text = "Размер рабочей области для построения патчей, мостов и графовой структуры.";

        _fireCellsHintTextBlock.Text =
            "Стартовые очаги можно выбрать случайно или вручную по узлам графа.";

        UpdatePresetButtonsUi();
    }

    private void UpdatePresetButtonsUi()
    {
        if (_presetHintTextBlock == null)
            return;

        _presetHintTextBlock.Text =
            "Готовые сценарии для графа. Кнопка подставляет scale-aware настройки: сценарий, погоду и режим подготовки graph structure.";

        if (_presetButton1 != null)
        {
            _presetButton1.Content = _mode switch
            {
                GraphCreationMode.Small => "Два кластера и мост",
                GraphCreationMode.Medium => "Сухие clustered patches",
                GraphCreationMode.Large => "Сухие крупные сектора",
                _ => "Сценарий 1"
            };
            _presetButton1.Tag = "dry-coniferous";
        }

        if (_presetButton2 != null)
        {
            _presetButton2.Content = _mode switch
            {
                GraphCreationMode.Small => "Водный узел-барьер",
                GraphCreationMode.Medium => "Водный барьер между патчами",
                GraphCreationMode.Large => "Макро-водный барьер",
                _ => "Сценарий 2"
            };
            _presetButton2.Tag = "river";
        }

        if (_presetButton3 != null)
        {
            _presetButton3.Content = _mode switch
            {
                GraphCreationMode.Small => "Плотный сухой граф",
                GraphCreationMode.Medium => "Смешанные hot spots",
                GraphCreationMode.Large => "Неоднородные зоны",
                _ => "Сценарий 3"
            };
            _presetButton3.Tag = "wet";
        }

        if (_presetButton4 != null)
        {
            _presetButton4.Content = _mode switch
            {
                GraphCreationMode.Small => "Разрыв / firebreak",
                GraphCreationMode.Medium => "Просека между патчами",
                GraphCreationMode.Large => "Bridge corridors",
                _ => "Сценарий 4"
            };
            _presetButton4.Tag = "firebreak";
        }

        if (_presetButton5 != null)
        {
            _presetButton5.Content = _mode switch
            {
                GraphCreationMode.Small => "Компактный demo-граф",
                GraphCreationMode.Medium => "Холмистые кластеры",
                GraphCreationMode.Large => "Крупные area-переходы",
                _ => "Сценарий 5"
            };
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
                    "Граф будет создан автоматически по текущим параметрам масштаба, плотности, рельефа и распределениям.",

                MapCreationMode.Scenario =>
                    "Будет использован scale-aware graph scenario: мосты, барьеры, water/bare зоны, patch-структура и рельеф задаются заранее.",

                MapCreationMode.SemiManual =>
                    "Полуручной graph-режим работает через editor узлов и рёбер: вы выбираете точки-кандидаты, формируете graph blueprint, задаёте связи и параметры узлов.",

                _ =>
                    "Граф будет создан автоматически."
            };
        }

        if (_semiManualDescriptionTextBlock != null)
        {
            _semiManualDescriptionTextBlock.Text =
                SelectedMapCreationMode == MapCreationMode.SemiManual
                    ? "Откроется graph editor. В нём можно выбрать узлы из подложки-кандидатов, вручную соединить их рёбрами, удалить связи и задать свойства узлов и рёбер."
                    : string.Empty;
        }

        if (_openMapEditorButton != null)
        {
            _openMapEditorButton.Content = SelectedMapCreationMode == MapCreationMode.SemiManual
                ? "Открыть редактор графа"
                : "Редактор недоступен";

            _openMapEditorButton.IsEnabled = SelectedMapCreationMode == MapCreationMode.SemiManual;
        }

        UpdateMapEditorSummary();
    }

    private void UpdateScenarioDescription()
    {
        if (_scenarioTypeBox == null)
            return;

        SelectedClusteredScenarioType = (ClusteredScenarioType)_scenarioTypeBox.SelectedIndex;

        if (_scenarioDescriptionTextBlock == null)
            return;

        _scenarioDescriptionTextBlock.Text = SelectedClusteredScenarioType switch
        {
            ClusteredScenarioType.DenseDryConiferous =>
                "Сухая плотная graph-структура с высокой проводимостью огня и выраженными локальными связями.",
            ClusteredScenarioType.WaterBarrier =>
                "Water/barrier scenario: часть узлов и переходов ослабляется водной преградой.",
            ClusteredScenarioType.FirebreakGap =>
                "Firebreak scenario: разрывы и слабые мосты мешают непрерывному распространению.",
            ClusteredScenarioType.HillyClusters =>
                "Холмистая graph-структура с неоднородным рельефом и patch-like поведением.",
            ClusteredScenarioType.WetAfterRain =>
                "Влажный graph после осадков: воспламенение и развитие фронта заметно замедлены.",
            ClusteredScenarioType.MixedDryHotspots =>
                "Неоднородная graph-карта с локальными сухими очагами и контрастными зонами.",
            _ =>
                "Сценарий графа выбран."
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

        if (ClusteredBlueprint == null)
        {
            _mapEditorSummaryTextBlock.Text =
                "Blueprint ещё не задан. Для полуручного режима графа нужно открыть editor и сохранить структуру.";
            return;
        }

        _mapEditorSummaryTextBlock.Text =
            $"Blueprint: узлов {ClusteredBlueprint.Nodes.Count}, рёбер {ClusteredBlueprint.Edges.Count}, кандидатов {ClusteredBlueprint.Candidates.Count}.";
    }

    private async Task OpenMapEditorAsync()
    {
        if (SelectedMapCreationMode != MapCreationMode.SemiManual)
            return;

        int width = ParseInt(_widthBox?.Text, 24);
        int height = ParseInt(_heightBox?.Text, 24);

        var editor = new ClusteredGraphEditorDialog(width, height, ClusteredBlueprint);
        var result = await editor.ShowDialog<bool>(this);

        if (!result)
            return;

        ClusteredBlueprint = editor.EditedBlueprint;
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
        ClusteredBlueprint = null;

        switch (preset)
        {
            case "dry-coniferous":
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)ClusteredScenarioType.DenseDryConiferous;
                if (_windSpeedBox != null) _windSpeedBox.Text = "8";
                if (_tempBox != null) _tempBox.Text = "31";
                if (_humidityBox != null) _humidityBox.Text = "22";
                break;

            case "river":
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)ClusteredScenarioType.WaterBarrier;
                break;

            case "wet":
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)ClusteredScenarioType.WetAfterRain;
                if (_precipitationBox != null) _precipitationBox.Text = "2.5";
                break;

            case "firebreak":
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)ClusteredScenarioType.FirebreakGap;
                break;

            case "hills":
                if (_mapCreationModeBox != null) _mapCreationModeBox.SelectedIndex = (int)MapCreationMode.Scenario;
                if (_scenarioTypeBox != null) _scenarioTypeBox.SelectedIndex = (int)ClusteredScenarioType.HillyClusters;
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
            SimulationName = "Графовая симуляция";

        GridWidth = ParseInt(_widthBox?.Text, 24);
        GridHeight = ParseInt(_heightBox?.Text, 24);
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

        SelectedClusteredScenarioType = _scenarioTypeBox != null
            ? (ClusteredScenarioType)_scenarioTypeBox.SelectedIndex
            : ClusteredScenarioType.DenseDryConiferous;

        VegetationDistributions = BuildVegetationDistributions();

        if (GridWidth < 8 || GridHeight < 8)
        {
            ShowError("Размер поля графа должен быть не меньше 8x8.");
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
            (ClusteredBlueprint == null || ClusteredBlueprint.Nodes.Count == 0))
        {
            ShowError("Для полуручного режима графа нужно создать и сохранить blueprint.");
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

        int width = ParseInt(_widthBox?.Text, 24);
        int height = ParseInt(_heightBox?.Text, 24);

        _structureSummaryTextBlock.Text = _mode switch
        {
            GraphCreationMode.Small => $"Малый граф • поле {width}×{height}",
            GraphCreationMode.Medium => $"Средний граф • поле {width}×{height}",
            GraphCreationMode.Large => $"Большой граф • поле {width}×{height}",
            _ => $"Граф • поле {width}×{height}"
        };

        _structureDetailTextBlock.Text = SelectedMapCreationMode switch
        {
            MapCreationMode.Random =>
                "Граф будет сгенерирован автоматически.",
            MapCreationMode.Scenario =>
                "Будет использован готовый сценарий графа.",
            MapCreationMode.SemiManual =>
                ClusteredBlueprint == null
                    ? "Полуручной режим: blueprint пока не задан."
                    : $"Полуручной режим: узлов {ClusteredBlueprint.Nodes.Count}, рёбер {ClusteredBlueprint.Edges.Count}.",
            _ =>
                "Структура графа будет создана автоматически."
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
}