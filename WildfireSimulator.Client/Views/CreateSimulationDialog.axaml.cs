using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        _mapModeDescriptionTextBlock = this.FindControl<TextBlock>("MapModeDescriptionTextBlock");
        _scenarioDescriptionTextBlock = this.FindControl<TextBlock>("ScenarioDescriptionTextBlock");
        _semiManualDescriptionTextBlock = this.FindControl<TextBlock>("SemiManualDescriptionTextBlock");
        _mapEditorSummaryTextBlock = this.FindControl<TextBlock>("MapEditorSummaryTextBlock");

        _scenarioPanel = this.FindControl<StackPanel>("ScenarioPanel");
        _semiManualPanel = this.FindControl<StackPanel>("SemiManualPanel");

        _openMapEditorButton = this.FindControl<Button>("OpenMapEditorButton");

        _coniferousBox = this.FindControl<TextBox>("ConiferousBox");
        _deciduousBox = this.FindControl<TextBox>("DeciduousBox");
        _mixedBox = this.FindControl<TextBox>("MixedBox");
        _grassBox = this.FindControl<TextBox>("GrassBox");
        _shrubBox = this.FindControl<TextBox>("ShrubBox");
        _waterBox = this.FindControl<TextBox>("WaterBox");
        _bareBox = this.FindControl<TextBox>("BareBox");
    }
    private void AttachEvents()
    {
        var createButton = this.FindControl<Button>("CreateButton");
        var cancelButton = this.FindControl<Button>("CancelButton");

        if (createButton != null)
            createButton.Click += OnCreateClick;

        if (cancelButton != null)
            cancelButton.Click += OnCancelClick;

        if (_widthBox != null)
            _widthBox.PropertyChanged += (_, __) => UpdateStructurePreview();

        if (_heightBox != null)
            _heightBox.PropertyChanged += (_, __) => UpdateStructurePreview();

        if (_mapCreationModeBox != null)
            _mapCreationModeBox.SelectionChanged += (_, __) => UpdateMapModeUi();

        if (_scenarioTypeBox != null)
            _scenarioTypeBox.SelectionChanged += (_, __) => UpdateScenarioDescription();

        if (_openMapEditorButton != null)
            _openMapEditorButton.Click += async (_, __) => await OpenMapEditorAsync();
    }
    private async System.Threading.Tasks.Task OpenMapEditorAsync()
    {
        ClearErrors();

        if (_page != AppPage.Grid)
        {
            ShowError("Полуручное создание карты сейчас поддерживается только для сеточного режима.");
            return;
        }

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
            .ToList();

        UpdateMapEditorSummary();
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

        if (_coniferousBox != null) _coniferousBox.Text = "30";
        if (_deciduousBox != null) _deciduousBox.Text = "20";
        if (_mixedBox != null) _mixedBox.Text = "25";
        if (_grassBox != null) _grassBox.Text = "15";
        if (_shrubBox != null) _shrubBox.Text = "10";
        if (_waterBox != null) _waterBox.Text = "0";
        if (_bareBox != null) _bareBox.Text = "0";
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
                    "Карта будет сформирована автоматически по текущим параметрам и распределениям.",
                MapCreationMode.Scenario =>
                    "Пользователь выбирает готовый сценарий, а карта строится по его правилам с естественной вариативностью.",
                MapCreationMode.SemiManual =>
                    "Карта задаётся крупными объектами и областями, а детальные параметры строятся автоматически.",
                _ =>
                    "Карта будет сформирована автоматически."
            };
        }

        if (_semiManualDescriptionTextBlock != null)
        {
            _semiManualDescriptionTextBlock.Text = _page == AppPage.Grid
                ? "Используйте редактор областей, чтобы добавить водоёмы, просеки, зоны влажности и формы рельефа."
                : "Для первой версии полуручное создание рекомендуется использовать в сеточном режиме.";
        }

        UpdateMapEditorSummary();
        UpdateStructurePreview();
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
            : $"Добавлено объектов: {MapRegionObjects.Count}";
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
                "Сбалансированный лесной ландшафт с несколькими типами растительности и умеренной неоднородностью.",
            MapScenarioType.DryConiferousMassif =>
                "Преобладает сухой хвойный лес с пониженной влажностью и более высокой потенциальной интенсивностью пожара.",
            MapScenarioType.ForestWithRiver =>
                "Через территорию проходит река, рядом с водой выше влажность и ниже вероятность устойчивого распространения.",
            MapScenarioType.ForestWithLake =>
                "В центральной части находится озеро, формирующее влажную зону и естественный водный барьер.",
            MapScenarioType.ForestWithFirebreak =>
                "Часть леса разделена просекой, уменьшающей запас топлива и тормозящей распространение огня.",
            MapScenarioType.HillyTerrain =>
                "Рельеф содержит несколько возвышенностей и склонов, которые влияют на уклон и динамику распространения.",
            MapScenarioType.WetForestAfterRain =>
                "Вся территория более влажная, с локальными переувлажнёнными участками после недавних осадков.",
            _ =>
                "Сценарий не выбран."
        };
    }

    private void UpdateStructurePreview()
    {
        if (_structureSummaryTextBlock == null || _structureDetailTextBlock == null)
            return;

        var width = ParseInt(_widthBox?.Text, 20);
        var height = ParseInt(_heightBox?.Text, 20);

        if (_page == AppPage.Grid)
        {
            _structureSummaryTextBlock.Text = $"Сетка {width}×{height} = {width * height} клеток.";

            _structureDetailTextBlock.Text = SelectedMapCreationMode switch
            {
                MapCreationMode.Random =>
                    "Карта будет сгенерирована автоматически на основе распределений и диапазонов параметров.",
                MapCreationMode.Scenario =>
                    "Карта будет построена по выбранному сценарию, который задаёт структуру, профиль влажности и рельеф.",
                MapCreationMode.SemiManual =>
                    "Карта будет собрана из крупных объектов и областей, а вторичные параметры будут рассчитаны автоматически.",
                _ =>
                    "Параметры автоматически учитываются при построении модели."
            };
        }
        else
        {
            _structureSummaryTextBlock.Text = $"Территория {width}×{height}.";
            _structureDetailTextBlock.Text = "Сейчас новые режимы карты в первую очередь ориентированы на сеточную модель.";
        }
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        ClearErrors();

        if (!TryReadValues(out var error))
        {
            ShowError(error);
            return;
        }

        if (SelectedMapCreationMode == MapCreationMode.SemiManual &&
            _page == AppPage.Grid &&
            MapRegionObjects.Count == 0)
        {
            await OpenMapEditorAsync();

            if (MapRegionObjects.Count == 0)
            {
                ShowError("Для полуручного режима нужно добавить хотя бы одну область на карте.");
                return;
            }
        }

        await CloseAsync(true);
    }

    private async void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        await CloseAsync(false);
    }

    private bool TryReadValues(out string error)
    {
        error = string.Empty;

        SimulationName = (_nameBox?.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(SimulationName))
        {
            error = "Введите название симуляции.";
            return false;
        }

        GridWidth = ParseInt(_widthBox?.Text, 20);
        GridHeight = ParseInt(_heightBox?.Text, 20);
        InitialFireCells = ParseInt(_fireCellsBox?.Text, 3);
        SimulationSteps = ParseInt(_stepsBox?.Text, 100);
        StepDurationSeconds = ParseInt(_stepDurationBox?.Text, 900);

        MoistureMin = ParseDouble(_moistureMinBox?.Text, 0.3);
        MoistureMax = ParseDouble(_moistureMaxBox?.Text, 0.7);
        ElevationVariation = ParseDouble(_elevationBox?.Text, 50.0);

        Temperature = ParseDouble(_tempBox?.Text, 25.0);
        Humidity = ParseDouble(_humidityBox?.Text, 40.0);
        WindSpeed = ParseDouble(_windSpeedBox?.Text, 5.0);
        Precipitation = ParseDouble(_precipitationBox?.Text, 0.0);

        WindDirection = (_windDirBox?.SelectedIndex ?? 1) switch
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

        MapNoiseStrength = ParseDouble(_mapNoiseBox?.Text, 0.08);

        var randomSeedText = (_randomSeedBox?.Text ?? string.Empty).Trim();
        RandomSeed = int.TryParse(randomSeedText, out var seed) ? seed : null;

        if (GridWidth < 5 || GridWidth > 100)
        {
            error = "Ширина должна быть в диапазоне от 5 до 100.";
            return false;
        }

        if (GridHeight < 5 || GridHeight > 100)
        {
            error = "Высота должна быть в диапазоне от 5 до 100.";
            return false;
        }

        if (InitialFireCells < 1)
        {
            error = "Количество начальных очагов должно быть не меньше 1.";
            return false;
        }

        if (SimulationSteps < 1)
        {
            error = "Количество шагов должно быть не меньше 1.";
            return false;
        }

        if (StepDurationSeconds < 1)
        {
            error = "Длительность шага должна быть не меньше 1 секунды.";
            return false;
        }

        if (MoistureMin < 0.0 || MoistureMin > 1.0 || MoistureMax < 0.0 || MoistureMax > 1.0)
        {
            error = "Влажность топлива должна быть в диапазоне от 0.0 до 1.0.";
            return false;
        }

        if (MoistureMax < MoistureMin)
        {
            error = "Максимальная влажность не может быть меньше минимальной.";
            return false;
        }

        if (Humidity < 0.0 || Humidity > 100.0)
        {
            error = "Влажность воздуха должна быть в диапазоне от 0 до 100.";
            return false;
        }

        if (WindSpeed < 0.0)
        {
            error = "Скорость ветра не может быть отрицательной.";
            return false;
        }

        if (MapNoiseStrength < 0.0 || MapNoiseStrength > 0.30)
        {
            error = "Сила локальной неоднородности должна быть в диапазоне от 0.00 до 0.30.";
            return false;
        }

        SelectedMapCreationMode = _mapCreationModeBox?.SelectedIndex switch
        {
            1 => MapCreationMode.Scenario,
            2 => MapCreationMode.SemiManual,
            _ => MapCreationMode.Random
        };

        if (SelectedMapCreationMode == MapCreationMode.Scenario)
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
        else
        {
            SelectedScenarioType = null;
        }

        VegetationDistributions = ReadVegetationDistributions();
        var totalProbability = 0.0;
        foreach (var item in VegetationDistributions)
            totalProbability += item.Probability;

        if (totalProbability <= 0.0)
        {
            error = "Сумма распределений растительности должна быть больше 0.";
            return false;
        }

        UpdateMapEditorSummary();
        return true;
    }

    private List<(int VegetationType, double Probability)> ReadVegetationDistributions()
    {
        return new List<(int VegetationType, double Probability)>
        {
            ((int)VegetationType.Coniferous, ParseDouble(_coniferousBox?.Text, 30)),
            ((int)VegetationType.Deciduous, ParseDouble(_deciduousBox?.Text, 20)),
            ((int)VegetationType.Mixed, ParseDouble(_mixedBox?.Text, 25)),
            ((int)VegetationType.Grass, ParseDouble(_grassBox?.Text, 15)),
            ((int)VegetationType.Shrub, ParseDouble(_shrubBox?.Text, 10)),
            ((int)VegetationType.Water, ParseDouble(_waterBox?.Text, 0)),
            ((int)VegetationType.Bare, ParseDouble(_bareBox?.Text, 0))
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
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        text = text.Replace(',', '.');

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private void ShowError(string message)
    {
        if (_errorTextBlock != null)
            _errorTextBlock.Text = message;
    }

    private void ClearErrors()
    {
        if (_errorTextBlock != null)
            _errorTextBlock.Text = string.Empty;
    }

    private async System.Threading.Tasks.Task CloseAsync(bool result)
    {
        await System.Threading.Tasks.Task.Yield();
        Close(result);
    }
}