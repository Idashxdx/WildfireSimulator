using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WildfireSimulator.Client.Models;
using WildfireSimulator.Client.Services;
using WildfireSimulator.Client.Views;

namespace WildfireSimulator.Client.ViewModels;

public enum AppPage
{
    Grid = 0,
    Graph = 1
}

public enum GraphCreationMode
{
    Small = 0,
    Medium = 1,
    Large = 2
}

public enum IgnitionMode
{
    Random = 0,
    Manual = 1
}

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private SignalRService? _signalRService;
    private string? _subscribedSimulationId;

    private System.Threading.CancellationTokenSource? _autoSimulationCts;
    private bool _isStepInProgress;
    private const int AutoStepDelayMs = 700;

    private System.Threading.CancellationTokenSource? _statusMessageCts;
    private const int StatusMessageLifetimeMs = 7000;
    private string _persistentStatusText = "Не подключено к API";

    [ObservableProperty]
    private bool _hasSavedIgnitionPreview;

    [ObservableProperty]
    private bool _isAutoSimulationRunning = false;

    [ObservableProperty]
    private bool _isAutoSimulationPaused = false;

    [ObservableProperty]
    private string _autoSimulationStatusText = "Авто-режим выключен";

    [ObservableProperty]
    private string _greeting = "Wildfire Simulator";

    [ObservableProperty]
    private string _statusText = "Не подключено к API";

    [ObservableProperty]
    private string _simulationInfoText = "Выберите симуляцию для просмотра состояния";

    [ObservableProperty]
    private bool _isConnected = false;

    [ObservableProperty]
    private string _apiUrl = "http://localhost:5198";

    [ObservableProperty]
    private ObservableCollection<SimulationDto> _simulations = new();

    [ObservableProperty]
    private ObservableCollection<SimulationDto> _gridSimulations = new();

    [ObservableProperty]
    private ObservableCollection<SimulationDto> _graphSimulations = new();

    [ObservableProperty]
    private SimulationDto? _selectedSimulation;

    [ObservableProperty]
    private ObservableCollection<GraphCellDto> _cells = new();

    [ObservableProperty]
    private ObservableCollection<SimulationGraphNodeDto> _graphNodes = new();

    [ObservableProperty]
    private ObservableCollection<SimulationGraphEdgeDto> _graphEdges = new();

    [ObservableProperty]
    private SimulationGraphNodeDto? _selectedGraphNode;

    [ObservableProperty]
    private string _graphLayoutHint = "grid";

    [ObservableProperty]
    private AppPage _currentPage = AppPage.Grid;

    [ObservableProperty]
    private GraphCreationMode _selectedGraphCreationMode = GraphCreationMode.Medium;

    [ObservableProperty]
    private int _gridWidth = 20;

    [ObservableProperty]
    private int _gridHeight = 20;

    [ObservableProperty]
    private int _initialFireCells = 3;

    [ObservableProperty]
    private int _currentStep = 0;

    [ObservableProperty]
    private double _fireArea = 0;

    [ObservableProperty]
    private bool _isSimulationRunning = false;

    [ObservableProperty]
    private int _selectedSimulationStatus = 0;

    [ObservableProperty]
    private GraphType _selectedSimulationGraphType = GraphType.Grid;

    [ObservableProperty]
    private bool _isSignalRConnected = false;

    [ObservableProperty]
    private string _signalRStatus = "Не подключено";

    [ObservableProperty]
    private double _movingAverage3;

    [ObservableProperty]
    private double _movingAverage5;

    [ObservableProperty]
    private double _movingAverage10;

    [ObservableProperty]
    private string _trendText = "—";

    [ObservableProperty]
    private ObservableCollection<string> _eventLog = new();

    [ObservableProperty]
    private string _windInfo = "—";

    [ObservableProperty]
    private string _temperatureInfo = "—";

    [ObservableProperty]
    private string _humidityInfo = "—";

    [ObservableProperty]
    private string _vegetationStats = "—";

    [ObservableProperty]
    private bool _canResetSimulation;

    [ObservableProperty]
    private double _forecastNextArea;

    [ObservableProperty]
    private double _forecastDelta;

    [ObservableProperty]
    private string _forecastMethod = "—";

    [ObservableProperty]
    private double _lastForecastAbsoluteError;

    [ObservableProperty]
    private double _meanAbsoluteError;

    [ObservableProperty]
    private int _forecastErrorCount;

    [ObservableProperty]
    private IgnitionMode _selectedIgnitionMode = IgnitionMode.Random;

    [ObservableProperty]
    private bool _isPreparedMapLoaded = false;

    [ObservableProperty]
    private bool _isIgnitionSelectionEnabled = false;

    [ObservableProperty]
    private ObservableCollection<GraphCellDto> _selectedIgnitionCells = new();

    [ObservableProperty]
    private ObservableCollection<SimulationGraphNodeDto> _selectedIgnitionNodes = new();

    [ObservableProperty]
    private string _precipitationInfo = "—";

    [ObservableProperty]
    private string _workflowStatusText = "Выберите симуляцию";

    [ObservableProperty]
    private ObservableCollection<FireMetricsHistoryDto> _metricsHistory = new();

    [ObservableProperty]
    private bool _isMetricsHistoryLoading = false;

    [ObservableProperty]
    private string _metricsHistorySummary = "История метрик не загружена";

    [ObservableProperty]
    private string _lastAnomalyText = "Нет";

    [ObservableProperty]
    private double _streamSpeed;

    [ObservableProperty]
    private double _streamAcceleration;

    [ObservableProperty]
    private bool _streamIsCritical;

    [ObservableProperty]
    private double _anomalyDeviation;

    [ObservableProperty]
    private double _anomalyPreviousAverage;

    [ObservableProperty]
    private double _anomalyCurrentArea;

    public string StreamMovingAverageText =>
        $"MA3: {MovingAverage3:F1} • MA5: {MovingAverage5:F1} • MA10: {MovingAverage10:F1}";

    public string StreamDynamicsText =>
        $"Скорость: {StreamSpeed:F2} • Ускорение: {StreamAcceleration:F2}";

    public string StreamCriticalText =>
        StreamIsCritical ? "Критическое ускорение обнаружено" : "Критических изменений нет";

    public string ForecastSummaryText =>
     ForecastNextArea > 0
         ? $"{ForecastNextArea:F0} га • {ForecastMethod}"
         : "Нет данных";

    public string ForecastErrorSummaryText =>
        ForecastErrorCount > 0
            ? $"Средняя ошибка: {MeanAbsoluteError:F2} га • проверок: {ForecastErrorCount}"
            : "Проверки прогноза пока не накоплены";
    private string BuildVegetationStatsText(IEnumerable<(string Name, int Count)> items)
    {
        var preferredOrder = new[]
        {
        "Хвойный лес",
        "Лиственный лес",
        "Смешанный лес",
        "Трава",
        "Кустарник",
        "Вода",
        "Пустая поверхность"
    };

        var ordered = items
            .OrderBy(item =>
            {
                var index = Array.IndexOf(preferredOrder, item.Name);
                return index >= 0 ? index : int.MaxValue;
            })
            .ThenBy(item => item.Name)
            .ToList();

        if (ordered.Count == 0)
            return "—";

        return string.Join(Environment.NewLine, ordered.Select(item => $"• {item.Name}: {item.Count}"));
    }
    public string CompactMetricsSummaryText
    {
        get
        {
            if (MetricsHistory.Count == 0)
                return "История метрик пока пуста";

            var ordered = MetricsHistory.OrderBy(m => m.Step).ToList();
            var last = ordered.Last();

            return $"Записей: {ordered.Count} • последний шаг: {last.Step} • площадь: {last.FireArea:F0} га • скорость: {last.FireSpreadSpeed:F2}";
        }
    }
    public bool HasMetricsHistory => MetricsHistory.Count > 0;

    public string MetricsLastFireAreaText =>
        !HasMetricsHistory
            ? "—"
            : $"{MetricsHistory.OrderBy(m => m.Step).Last().FireArea:F0} га";

    public string MetricsMaxFireAreaText =>
        !HasMetricsHistory
            ? "—"
            : $"{MetricsHistory.Max(m => m.FireArea):F0} га";

    public string MetricsAverageSpreadSpeedText =>
        !HasMetricsHistory
            ? "—"
            : $"{MetricsHistory.Average(m => m.FireSpreadSpeed):F2}";

    public string MetricsLastWeatherText
    {
        get
        {
            if (!HasMetricsHistory)
                return "—";

            var last = MetricsHistory.OrderBy(m => m.Step).Last();
            return $"{last.AverageTemperature:F1} °C, {last.AverageWindSpeed:F1} м/с";
        }
    }
    public bool IsRandomIgnitionMode => SelectedIgnitionMode == IgnitionMode.Random;
    public bool IsManualIgnitionMode => SelectedIgnitionMode == IgnitionMode.Manual;

    public bool CanRefreshIgnitionSetup =>
        IsConnected &&
        SelectedSimulation != null &&
        SelectedSimulationStatus == 0 &&
        !IsSimulationRunning &&
        HasSavedIgnitionPreview;

    public bool CanEditIgnitionSetup =>
        IsConnected &&
        SelectedSimulation != null &&
        SelectedSimulationStatus == 0 &&
        !IsSimulationRunning &&
        !HasSavedIgnitionPreview;

    public bool ShowIgnitionControls =>
        SelectedSimulation != null &&
        SelectedSimulationStatus == 0 &&
        !IsSimulationRunning;

    public string IgnitionModeText => SelectedIgnitionMode switch
    {
        IgnitionMode.Random => "Случайные или сохранённые очаги",
        IgnitionMode.Manual => SelectedSimulationGraphType == GraphType.Grid
            ? "Очаги выбираются вручную на карте"
            : "Очаги выбираются вручную на графе",
        _ => "Неизвестно"
    };

    public string IgnitionSelectionSummary
    {
        get
        {
            var count = SelectedSimulationGraphType == GraphType.Grid
                ? SelectedIgnitionCells.Count
                : SelectedIgnitionNodes.Count;

            return count == 0
                ? "Очаги не выбраны"
                : $"Выбрано очагов: {count}";
        }
    }

    public bool IsGridSelected => SelectedSimulationGraphType == GraphType.Grid;
    public bool IsClusteredGraphSelected => SelectedSimulationGraphType == GraphType.ClusteredGraph;

    public string SelectedGraphNodeTitle =>
        SelectedGraphNode == null
            ? "Вершина не выбрана"
            : $"Вершина ({SelectedGraphNode.X}, {SelectedGraphNode.Y})";

    public string SelectedGraphNodeStateText =>
        SelectedGraphNode?.State switch
        {
            "Burning" => "Горит",
            "Burned" => "Сгорела",
            "Normal" => "Нормальная",
            _ => "Нет данных"
        };

    public string SelectedGraphNodeVegetationText =>
        SelectedGraphNode?.Vegetation switch
        {
            "Coniferous" => "Хвойный лес",
            "Deciduous" => "Лиственный лес",
            "Mixed" => "Смешанный лес",
            "Grass" => "Трава",
            "Shrub" => "Кустарник",
            "Water" => "Вода",
            "Bare" => "Пустая поверхность",
            null => "Нет данных",
            _ => SelectedGraphNode?.Vegetation ?? "Нет данных"
        };
    public string SelectedGraphNodeGroupCaption => "Cluster ID";

    public string SelectedGraphNodeDegreeSummaryText =>
        SelectedGraphNode == null
            ? "—"
            : $"{SelectedGraphNodeNeighborCountText} • сильных: {SelectedGraphNodeStrongEdgesText} • средних: {SelectedGraphNodeMediumEdgesText} • слабых: {SelectedGraphNodeWeakEdgesText}";

    public string SelectedGraphNodeFireSummaryText =>
        SelectedGraphNode == null
            ? "—"
            : $"{SelectedGraphNodeFireStageText} • интенсивность {SelectedGraphNodeFireIntensityText}";

    public string SelectedGraphNodeFuelSummaryText =>
        SelectedGraphNode == null
            ? "—"
            : $"{SelectedGraphNodeCurrentFuelLoadText} / {SelectedGraphNodeFuelLoadText} • остаток {SelectedGraphNodeFuelRatioText}";

    public string SelectedGraphNodeThermalSummaryText =>
        SelectedGraphNode == null
            ? "—"
            : $"{SelectedGraphNodeAccumulatedHeatText} • горение {SelectedGraphNodeBurningElapsedText}";

    public string SelectedGraphNodeMoistureText =>
        SelectedGraphNode == null ? "—" : $"{SelectedGraphNode.Moisture:F2}";

    public string SelectedGraphNodeElevationText =>
        SelectedGraphNode == null ? "—" : $"{SelectedGraphNode.Elevation:F0} м";

    public string SelectedGraphNodeProbabilityText =>
        SelectedGraphNode == null ? "—" : $"{SelectedGraphNode.BurnProbability:F3}";

    public string SelectedGraphNodeGroupText =>
        string.IsNullOrWhiteSpace(SelectedGraphNode?.GroupKey) ? "—" : SelectedGraphNode!.GroupKey;

    public string SelectedGraphNodeRenderPositionText =>
        SelectedGraphNode == null ? "—" : $"{SelectedGraphNode.RenderX:F2}, {SelectedGraphNode.RenderY:F2}";

    public string SelectedGraphNodeNeighborCountText =>
        SelectedGraphNode == null ? "0" : GetNeighborCount(SelectedGraphNode.Id).ToString();

    public string SelectedGraphNodeStrongEdgesText =>
        SelectedGraphNode == null ? "0" : GetSelectedEdgeCount(edge => edge.FireSpreadModifier >= 0.70).ToString();

    public string SelectedGraphNodeMediumEdgesText =>
        SelectedGraphNode == null ? "0" : GetSelectedEdgeCount(edge => edge.FireSpreadModifier >= 0.35 && edge.FireSpreadModifier < 0.70).ToString();

    public string SelectedGraphNodeWeakEdgesText =>
        SelectedGraphNode == null ? "0" : GetSelectedEdgeCount(edge => edge.FireSpreadModifier < 0.35).ToString();

    public string SelectedGraphNodeFireStageText =>
        SelectedGraphNode?.FireStage switch
        {
            "Unburned" => "Не горела",
            "Ignition" => "Воспламенение",
            "Active" => "Активное горение",
            "Intense" => "Интенсивное горение",
            "Smoldering" => "Тление",
            "BurnedOut" => "Полностью выгорела",
            null or "" => "—",
            _ => SelectedGraphNode!.FireStage
        };

    public string SelectedGraphNodeFireIntensityText =>
        SelectedGraphNode == null ? "—" : $"{SelectedGraphNode.FireIntensity:F2}";

    public string SelectedGraphNodeCurrentFuelLoadText =>
        SelectedGraphNode == null ? "—" : $"{SelectedGraphNode.CurrentFuelLoad:F2}";

    public string SelectedGraphNodeFuelLoadText =>
        SelectedGraphNode == null ? "—" : $"{SelectedGraphNode.FuelLoad:F2}";

    public string SelectedGraphNodeFuelRatioText
    {
        get
        {
            if (SelectedGraphNode == null || SelectedGraphNode.FuelLoad <= 0.0)
                return "—";

            var ratio = SelectedGraphNode.CurrentFuelLoad / SelectedGraphNode.FuelLoad;
            return $"{ratio:P0}";
        }
    }

    public string SelectedGraphNodeAccumulatedHeatText =>
        SelectedGraphNode == null ? "—" : $"{SelectedGraphNode.AccumulatedHeatJ:F2} Дж";

    public string SelectedGraphNodeBurningElapsedText =>
        SelectedGraphNode == null ? "—" : $"{SelectedGraphNode.BurningElapsedSeconds:F0} с";

    public string VisualizationMeaningText
    {
        get
        {
            if (SelectedSimulationGraphType == GraphType.Grid)
            {
                return "Регулярная карта: каждая клетка — участок поверхности, огонь распространяется по соседям и формирует фронт.";
            }

            var scale = SelectedSimulation?.GraphScaleType;

            return scale switch
            {
                GraphScaleType.Small =>
                    "Малый граф: topology-first визуализация. Хорошо видны отдельные вершины, рёбра, мосты и развилки распространения.",
                GraphScaleType.Medium =>
                    "Средний граф: patch/cluster-модель. Хорошо заметны локальные группы узлов, барьеры, мосты между кластерами и неоднородная связность.",
                GraphScaleType.Large =>
                    "Большой граф: area-like графовая карта. Поведение ближе к крупным зонам и секторам, чем к отдельным изолированным вершинам.",
                _ =>
                    "Графовая модель: вершины образуют единый граф с локальной кластеризацией, мостами и неоднородной связностью."
            };
        }
    }

    public string StructureScaleText
    {
        get
        {
            if (SelectedSimulationGraphType == GraphType.Grid)
            {
                return "Масштаб: 1 клетка = 1 гектар.";
            }

            var scale = SelectedSimulation?.GraphScaleType;

            return scale switch
            {
                GraphScaleType.Small =>
                    "Масштаб: малый граф, обычно 8–20 узлов. Акцент на вершинах, рёбрах и локальной топологии.",
                GraphScaleType.Medium =>
                    "Масштаб: средний граф, обычно 20–80 узлов. Акцент на патчах, кластерах, мостах и локальных барьерах.",
                GraphScaleType.Large =>
                    "Масштаб: большой граф, обычно 80–250+ узлов. Акцент на макрозонах, секторах и коридорах распространения.",
                _ =>
                    "Масштаб: узел — отдельный объект графа; важнее локальная связность, мосты и кластеры."
            };
        }
    }

    public string SpreadBehaviorText
    {
        get
        {
            if (SelectedSimulationGraphType == GraphType.Grid)
            {
                return "Ожидаемое поведение: компактный фронт, локальное распространение, сдвиг по ветру.";
            }

            var scale = SelectedSimulation?.GraphScaleType;

            return scale switch
            {
                GraphScaleType.Small =>
                    "Ожидаемое поведение: заметные ветвления, локальные мосты, быстро читаемые пути перехода огня между узлами.",
                GraphScaleType.Medium =>
                    "Ожидаемое поведение: распространение по локальным кластерам и близким связям, возможны барьеры, тупики и patch-to-patch переходы.",
                GraphScaleType.Large =>
                    "Ожидаемое поведение: распространение между крупными зонами, секторами и corridor-переходами, важны макроразрывы и длинные связи.",
                _ =>
                    "Ожидаемое поведение: распространение по локальным кластерам и близким связям, возможны тупики и ветвления."
            };
        }
    }

    public string GraphTypeText
    {
        get
        {
            if (SelectedSimulationGraphType == GraphType.Grid)
                return "Сетка";

            return SelectedSimulation?.GraphScaleType switch
            {
                GraphScaleType.Small => "Малый граф",
                GraphScaleType.Medium => "Средний граф",
                GraphScaleType.Large => "Большой граф",
                _ => "Граф"
            };
        }
    }

    public string SimulationStatusText => SelectedSimulationStatus switch
    {
        0 => "Создана",
        1 => "Запущена",
        2 => "Завершена",
        3 => "Отменена",
        _ => "Неизвестно"
    };

    public string SelectedGraphCreationModeText => SelectedGraphCreationMode switch
    {
        GraphCreationMode.Small => "Малый граф — topology-first режим для наглядных узлов, рёбер и мостов",
        GraphCreationMode.Medium => "Средний граф — patch/cluster режим для локальных групп и барьеров",
        GraphCreationMode.Large => "Большой граф — area-like режим для макрозон, секторов и коридоров",
        _ => "Граф"
    };

    public string CurrentPageTitleText => CurrentPage switch
    {
        AppPage.Grid => "Сеточная модель",
        AppPage.Graph => "Графовая модель",
        _ => "Модель"
    };

    public string CurrentPageHintText => CurrentPage switch
    {
        AppPage.Grid =>
            "Grid — клеточная карта леса с территориальными сценариями и полуручным редактором областей.",
        AppPage.Graph => SelectedGraphCreationMode switch
        {
            GraphCreationMode.Small =>
                "SmallGraph — компактный граф для topology demos: хорошо видны отдельные вершины, рёбра и мосты.",
            GraphCreationMode.Medium =>
                "MediumGraph — clustered / patch-like граф для локальных групп, барьеров и межкластерных переходов.",
            GraphCreationMode.Large =>
                "LargeGraph — area-like граф для крупных зон, corridor-связей и макромасштабного распространения.",
            _ =>
                "Графовая модель с несколькими масштабами."
        },
        _ => "Выберите режим моделирования."
    };

    public bool IsGridPage => CurrentPage == AppPage.Grid;
    public bool IsGraphPage => CurrentPage == AppPage.Graph;

    public bool IsSmallGraphCreationMode => SelectedGraphCreationMode == GraphCreationMode.Small;
    public bool IsMediumGraphCreationMode => SelectedGraphCreationMode == GraphCreationMode.Medium;
    public bool IsLargeGraphCreationMode => SelectedGraphCreationMode == GraphCreationMode.Large;
    public bool CanStartAutoSimulation =>
        IsConnected &&
        SelectedSimulation != null &&
        IsSimulationRunning &&
        SelectedSimulationStatus == 1 &&
        !IsAutoSimulationRunning &&
        !_isStepInProgress;

    public bool CanPauseAutoSimulation =>
        IsAutoSimulationRunning &&
        !IsAutoSimulationPaused;

    public bool CanResumeAutoSimulation =>
        IsAutoSimulationRunning &&
        IsAutoSimulationPaused;

    public bool CanStopAutoSimulation =>
        IsAutoSimulationRunning;

    public bool CanManageSimulationActions =>
        !IsAutoSimulationRunning && !_isStepInProgress;

    public bool CanStartSelectedSimulation
    {
        get
        {
            if (!IsConnected || SelectedSimulation == null || SelectedSimulationStatus != 0)
                return false;

            if (!IsManualIgnitionMode)
                return true;

            return SelectedSimulationGraphType == GraphType.Grid
                ? SelectedIgnitionCells.Count > 0
                : SelectedIgnitionNodes.Count > 0;
        }
    }

    public bool CanExecuteStepSimulation =>
        IsConnected &&
        SelectedSimulation != null &&
        IsSimulationRunning &&
        SelectedSimulationStatus == 1;

    public bool HasSelectedGraphNode => SelectedGraphNode != null;

    public MainWindowViewModel()
    {
        _apiService = new ApiService();
        _apiService.SetBaseUrl(ApiUrl);
    }

    partial void OnSelectedIgnitionModeChanged(IgnitionMode value)
    {
        IsIgnitionSelectionEnabled =
            value == IgnitionMode.Manual &&
            IsPreparedMapLoaded &&
            CanEditIgnitionSetup;

        if (value == IgnitionMode.Random)
        {
            ClearSelectedIgnitionCells();
            ClearSelectedIgnitionNodes();
        }

        OnPropertyChanged(nameof(IsRandomIgnitionMode));
        OnPropertyChanged(nameof(IsManualIgnitionMode));
        OnPropertyChanged(nameof(IgnitionModeText));
        OnPropertyChanged(nameof(IgnitionSelectionSummary));
        OnPropertyChanged(nameof(CanStartSelectedSimulation));
        OnPropertyChanged(nameof(CanRefreshIgnitionSetup));
        OnPropertyChanged(nameof(CanEditIgnitionSetup));
        OnPropertyChanged(nameof(ShowIgnitionControls));

        RefreshWorkflowStatus();
    }

    partial void OnApiUrlChanged(string value)
    {
        _apiService.SetBaseUrl(value);
    }

    partial void OnCurrentPageChanged(AppPage value)
    {
        OnPropertyChanged(nameof(IsGridPage));
        OnPropertyChanged(nameof(IsGraphPage));
        OnPropertyChanged(nameof(CurrentPageTitleText));
        OnPropertyChanged(nameof(CurrentPageHintText));

        if (SelectedSimulation != null)
        {
            if (value == AppPage.Grid && SelectedSimulation.GraphType != GraphType.Grid)
            {
                SelectedSimulation = null;
            }
            else if (value == AppPage.Graph && SelectedSimulation.GraphType == GraphType.Grid)
            {
                SelectedSimulation = null;
            }
        }

        SimulationInfoText = value == AppPage.Grid
            ? "Выберите сеточную симуляцию: карта клеток, сценарии территории и полуручное редактирование областей."
            : SelectedGraphCreationMode switch
            {
                GraphCreationMode.Small =>
                    "Выберите или создайте малый граф: topology-first структура с отдельными узлами, рёбрами и мостами.",
                GraphCreationMode.Medium =>
                    "Выберите или создайте средний граф: patch/cluster-модель с локальными группами и межкластерными переходами.",
                GraphCreationMode.Large =>
                    "Выберите или создайте большой граф: area-like графовую карту с макрозонами и corridor-связями.",
                _ =>
                    "Выберите графовую симуляцию."
            };

        RefreshWorkflowStatus();
    }

    partial void OnSelectedGraphCreationModeChanged(GraphCreationMode value)
    {
        OnPropertyChanged(nameof(SelectedGraphCreationModeText));
        OnPropertyChanged(nameof(CurrentPageHintText));
        OnPropertyChanged(nameof(IsSmallGraphCreationMode));
        OnPropertyChanged(nameof(IsMediumGraphCreationMode));
        OnPropertyChanged(nameof(IsLargeGraphCreationMode));

        if (CurrentPage == AppPage.Graph)
        {
            SimulationInfoText = value switch
            {
                GraphCreationMode.Small =>
                    "Сейчас выбран режим создания: малый граф. Он подходит для наглядной topology demo и анализа отдельных мостов.",
                GraphCreationMode.Medium =>
                    "Сейчас выбран режим создания: средний граф. Он подходит для patch/cluster-структур, барьеров и локального распространения.",
                GraphCreationMode.Large =>
                    "Сейчас выбран режим создания: большой граф. Он подходит для макрозон, corridor-связей и area-like поведения.",
                _ =>
                    "Выберите графовую симуляцию."
            };
        }

        RefreshWorkflowStatus();
    }

    partial void OnSelectedSimulationGraphTypeChanged(GraphType value)
    {
        OnPropertyChanged(nameof(GraphTypeText));
        OnPropertyChanged(nameof(IsGridSelected));
        OnPropertyChanged(nameof(IsClusteredGraphSelected));
        OnPropertyChanged(nameof(VisualizationMeaningText));
        OnPropertyChanged(nameof(StructureScaleText));
        OnPropertyChanged(nameof(SpreadBehaviorText));

        OnPropertyChanged(nameof(SelectedGraphNodeGroupCaption));
        OnPropertyChanged(nameof(SelectedGraphNodeTitle));
        OnPropertyChanged(nameof(SelectedGraphNodeGroupText));
        OnPropertyChanged(nameof(SelectedGraphNodeDegreeSummaryText));
        OnPropertyChanged(nameof(SelectedGraphNodeFireSummaryText));
        OnPropertyChanged(nameof(SelectedGraphNodeFuelSummaryText));
        OnPropertyChanged(nameof(SelectedGraphNodeThermalSummaryText));

        OnPropertyChanged(nameof(CurrentPageHintText));

        RefreshWorkflowStatus();
    }

    partial void OnSelectedSimulationChanged(SimulationDto? value)
    {
        ResetStreamAnalysisState();

        SelectedGraphNode = null;
        Cells.Clear();
        GraphNodes.Clear();
        GraphEdges.Clear();

        ClearSelectedIgnitionCells();
        ClearSelectedIgnitionNodes();

        HasSavedIgnitionPreview = false;
        IsPreparedMapLoaded = false;
        IsIgnitionSelectionEnabled = false;

        if (value == null)
        {
            SelectedSimulationStatus = 0;
            SelectedSimulationGraphType = CurrentPage == AppPage.Grid
                ? GraphType.Grid
                : GraphType.ClusteredGraph;

            SimulationInfoText = CurrentPage == AppPage.Grid
                ? "Сеточная симуляция не выбрана."
                : SelectedGraphCreationMode switch
                {
                    GraphCreationMode.Small => "Малый граф не выбран.",
                    GraphCreationMode.Medium => "Средний граф не выбран.",
                    GraphCreationMode.Large => "Большой граф не выбран.",
                    _ => "Графовая симуляция не выбрана."
                };

            RefreshWorkflowStatus();
            return;
        }

        SelectedSimulationStatus = value.Status;
        SelectedSimulationGraphType = value.GraphType;

        SimulationInfoText = value.GraphType == GraphType.Grid
            ? $"Выбрана сеточная симуляция: {value.Name}"
            : value.GraphScaleType switch
            {
                GraphScaleType.Small => $"Выбран малый граф: {value.Name}",
                GraphScaleType.Medium => $"Выбран средний граф: {value.Name}",
                GraphScaleType.Large => $"Выбран большой граф: {value.Name}",
                _ => $"Выбрана графовая симуляция: {value.Name}"
            };

        OnPropertyChanged(nameof(GraphTypeText));
        OnPropertyChanged(nameof(CurrentPageHintText));

        RefreshWorkflowStatus();

    }


    private void SetTransientStatus(string message, bool autoClear = true)
    {
        StatusText = message;

        try
        {
            _statusMessageCts?.Cancel();
        }
        catch
        {
        }

        _statusMessageCts?.Dispose();
        _statusMessageCts = null;

        if (!autoClear)
            return;

        _statusMessageCts = new System.Threading.CancellationTokenSource();
        var token = _statusMessageCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(StatusMessageLifetimeMs, token);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested && StatusText == message)
                        StatusText = string.Empty;
                });
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void RefreshWorkflowStatus()
    {
        if (SelectedSimulation == null)
        {
            WorkflowStatusText = CurrentPage switch
            {
                AppPage.Grid =>
                    "Сетка: выберите симуляцию или создайте новую клеточную карту.",
                AppPage.Graph => SelectedGraphCreationMode switch
                {
                    GraphCreationMode.Small =>
                        "Малый граф: выберите симуляцию или создайте topology-first граф.",
                    GraphCreationMode.Medium =>
                        "Средний граф: выберите симуляцию или создайте patch/cluster-граф.",
                    GraphCreationMode.Large =>
                        "Большой граф: выберите симуляцию или создайте area-like граф.",
                    _ =>
                        "Выберите симуляцию"
                },
                _ => "Выберите симуляцию"
            };
            return;
        }

        var selectedModelText = SelectedSimulationGraphType == GraphType.Grid
            ? "сеточная симуляция"
            : SelectedSimulation?.GraphScaleType switch
            {
                GraphScaleType.Small => "малый граф",
                GraphScaleType.Medium => "средний граф",
                GraphScaleType.Large => "большой граф",
                _ => "графовая симуляция"
            };

        if (IsAutoSimulationRunning && IsAutoSimulationPaused)
        {
            WorkflowStatusText = $"Авто-режим: пауза • {selectedModelText}";
            return;
        }

        if (IsAutoSimulationRunning)
        {
            WorkflowStatusText = $"Авто-режим: моделирование идёт • {selectedModelText}";
            return;
        }

        if (IsSimulationRunning && SelectedSimulationStatus == 1)
        {
            WorkflowStatusText = $"Симуляция идёт • {selectedModelText}";
            return;
        }

        if (SelectedSimulationStatus == 2)
        {
            WorkflowStatusText = $"Симуляция завершена • {selectedModelText}";
            return;
        }

        if (SelectedSimulationStatus == 3)
        {
            WorkflowStatusText = $"Симуляция отменена • {selectedModelText}";
            return;
        }

        if (SelectedSimulationStatus == 0)
        {
            if (!IsPreparedMapLoaded)
            {
                WorkflowStatusText = SelectedSimulationGraphType == GraphType.Grid
                    ? "Сеточная карта загружается или подготавливается..."
                    : "Графовая структура загружается или подготавливается...";
                return;
            }

            if (HasSavedIgnitionPreview)
            {
                WorkflowStatusText = SelectedSimulationGraphType == GraphType.Grid
                    ? "Сохранённые стартовые очаги показаны на карте. Можно запускать или очистить их."
                    : "Сохранённые стартовые очаги показаны на графе. Можно запускать или очистить их.";
                return;
            }

            if (IsManualIgnitionMode)
            {
                var selectedCount = SelectedSimulationGraphType == GraphType.Grid
                    ? SelectedIgnitionCells.Count
                    : SelectedIgnitionNodes.Count;

                WorkflowStatusText = selectedCount > 0
                    ? $"Очаги выбраны: {selectedCount}. Можно запускать {selectedModelText}."
                    : SelectedSimulationGraphType == GraphType.Grid
                        ? "Карта подготовлена. Выберите стартовые очаги вручную."
                        : "Граф подготовлен. Выберите стартовые узлы вручную.";
                return;
            }

            WorkflowStatusText = SelectedSimulationGraphType == GraphType.Grid
                ? "Карта подготовлена. Изучите территорию и запускайте пожар."
                : SelectedSimulation?.GraphScaleType switch
                {
                    GraphScaleType.Small =>
                        "Малый граф подготовлен. Можно запускать пожар и наблюдать переходы по узлам и мостам.",
                    GraphScaleType.Medium =>
                        "Средний граф подготовлен. Можно запускать пожар и анализировать patch-to-patch распространение.",
                    GraphScaleType.Large =>
                        "Большой граф подготовлен. Можно запускать пожар и анализировать макрозоны и corridor-связи.",
                    _ =>
                        "Граф подготовлен. Можно запускать симуляцию."
                };
            return;
        }

        WorkflowStatusText = "Состояние обновляется";
    }

    private void StopAutoSimulationInternal(string? message = null)
    {
        try
        {
            _autoSimulationCts?.Cancel();
        }
        catch
        {
        }

        _autoSimulationCts?.Dispose();
        _autoSimulationCts = null;

        var wasRunning = IsAutoSimulationRunning || IsAutoSimulationPaused;

        IsAutoSimulationRunning = false;
        IsAutoSimulationPaused = false;
        AutoSimulationStatusText = "Авто-режим выключен";

        OnPropertyChanged(nameof(CanStartSelectedSimulation));
        OnPropertyChanged(nameof(CanExecuteStepSimulation));
        OnPropertyChanged(nameof(CanStartAutoSimulation));
        OnPropertyChanged(nameof(CanPauseAutoSimulation));
        OnPropertyChanged(nameof(CanResumeAutoSimulation));
        OnPropertyChanged(nameof(CanStopAutoSimulation));
        OnPropertyChanged(nameof(CanManageSimulationActions));

        if (SelectedSimulation != null)
            CanResetSimulation = (SelectedSimulationStatus == 2 || SelectedSimulationStatus == 3 || SelectedSimulationStatus == 1) && IsConnected;

        if (wasRunning && !string.IsNullOrWhiteSpace(message))
            EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");

        RefreshWorkflowStatus();
    }

    private async Task<bool> ExecuteSingleStepAsync(bool startedByAutoMode, System.Threading.CancellationToken cancellationToken = default)
    {
        if (SelectedSimulation == null || !IsConnected)
            return false;

        if (!IsSimulationRunning || SelectedSimulationStatus != 1)
            return false;

        if (_isStepInProgress)
            return false;

        _isStepInProgress = true;

        OnPropertyChanged(nameof(CanStartSelectedSimulation));
        OnPropertyChanged(nameof(CanExecuteStepSimulation));
        OnPropertyChanged(nameof(CanStartAutoSimulation));
        OnPropertyChanged(nameof(CanPauseAutoSimulation));
        OnPropertyChanged(nameof(CanResumeAutoSimulation));
        OnPropertyChanged(nameof(CanStopAutoSimulation));
        OnPropertyChanged(nameof(CanManageSimulationActions));

        try
        {
            SetTransientStatus(startedByAutoMode
                ? "Автоматическое выполнение шага..."
                : "Выполнение шага...", false);

            var (success, message, cells, stepResult, isRunning, status) =
                await _apiService.ExecuteStepAsync(SelectedSimulation.Id);

            cancellationToken.ThrowIfCancellationRequested();

            if (success && stepResult != null)
            {
                IsSimulationRunning = isRunning;
                FireArea = stepResult.FireArea;
                CurrentStep = stepResult.Step;

                if (status >= 0)
                    SelectedSimulationStatus = status;

                if (SelectedSimulation != null && status >= 0)
                    SelectedSimulation.Status = status;

                if (SelectedSimulationGraphType == GraphType.Grid && cells != null)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Cells = new ObservableCollection<GraphCellDto>(cells);

                        var burning = cells.Count(c => c.IsBurning);
                        var burned = cells.Count(c => c.IsBurned);

                        if (!IsSimulationRunning)
                        {
                            SimulationInfoText = $"Симуляция завершена: клеток {Cells.Count}, горят {burning}, сгорело {burned}";
                            EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Симуляция завершена");
                        }
                        else
                        {
                            SimulationInfoText = $"Шаг {stepResult.Step}: клеток {Cells.Count}, горят {burning}, сгорело {burned}, новых очагов {stepResult.NewlyIgnitedCells}";

                            if (startedByAutoMode)
                                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Авто-шаг {stepResult.Step}, +{stepResult.NewlyIgnitedCells} новых");
                            else
                                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Шаг {stepResult.Step} выполнен, +{stepResult.NewlyIgnitedCells} новых");
                        }
                    });
                }
                else
                {
                    await LoadSimulationGraphAsync(SelectedSimulation!.Id);

                    if (!IsSimulationRunning)
                    {
                        SimulationInfoText = $"Графовая симуляция завершена на шаге {stepResult.Step}";
                        EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Графовая симуляция завершена");
                    }
                    else
                    {
                        SimulationInfoText = $"Шаг {stepResult.Step}: площадь {stepResult.FireArea:F0} га, новых очагов {stepResult.NewlyIgnitedCells}";

                        if (startedByAutoMode)
                            EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Авто-шаг {stepResult.Step}");
                        else
                            EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Графовый шаг {stepResult.Step} выполнен");
                    }
                }

                await LoadSimulationMetricsHistoryAsync(SelectedSimulation!.Id);

                OnPropertyChanged(nameof(CanStartSelectedSimulation));
                OnPropertyChanged(nameof(CanExecuteStepSimulation));
                OnPropertyChanged(nameof(CanStartAutoSimulation));
                OnPropertyChanged(nameof(CanPauseAutoSimulation));
                OnPropertyChanged(nameof(CanResumeAutoSimulation));
                OnPropertyChanged(nameof(CanStopAutoSimulation));
                OnPropertyChanged(nameof(SimulationStatusText));
                OnPropertyChanged(nameof(CanResetSimulation));
                OnPropertyChanged(nameof(CanManageSimulationActions));

                RefreshWorkflowStatus();
                SetTransientStatus("Шаг выполнен", true);

                return true;
            }

            SetTransientStatus($"Ошибка: {message}", true);

            if (SelectedSimulation != null)
            {
                await LoadSimulationStatusAsync(SelectedSimulation.Id);
                await LoadSimulationMetricsHistoryAsync(SelectedSimulation.Id);
            }

            RefreshWorkflowStatus();
            return false;
        }
        finally
        {
            _isStepInProgress = false;

            OnPropertyChanged(nameof(CanStartSelectedSimulation));
            OnPropertyChanged(nameof(CanExecuteStepSimulation));
            OnPropertyChanged(nameof(CanStartAutoSimulation));
            OnPropertyChanged(nameof(CanPauseAutoSimulation));
            OnPropertyChanged(nameof(CanResumeAutoSimulation));
            OnPropertyChanged(nameof(CanStopAutoSimulation));

            RefreshWorkflowStatus();
        }
    }
    public bool HasNoSelectedGraphNode => !HasSelectedGraphNode;
    partial void OnSelectedGraphNodeChanged(SimulationGraphNodeDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedGraphNode));
        OnPropertyChanged(nameof(SelectedGraphNodeTitle));
        OnPropertyChanged(nameof(SelectedGraphNodeStateText));
        OnPropertyChanged(nameof(SelectedGraphNodeVegetationText));
        OnPropertyChanged(nameof(SelectedGraphNodeMoistureText));
        OnPropertyChanged(nameof(SelectedGraphNodeElevationText));
        OnPropertyChanged(nameof(SelectedGraphNodeProbabilityText));
        OnPropertyChanged(nameof(SelectedGraphNodeGroupCaption));
        OnPropertyChanged(nameof(SelectedGraphNodeGroupText));
        OnPropertyChanged(nameof(SelectedGraphNodeRenderPositionText));
        OnPropertyChanged(nameof(SelectedGraphNodeNeighborCountText));
        OnPropertyChanged(nameof(SelectedGraphNodeStrongEdgesText));
        OnPropertyChanged(nameof(SelectedGraphNodeMediumEdgesText));
        OnPropertyChanged(nameof(SelectedGraphNodeWeakEdgesText));
        OnPropertyChanged(nameof(SelectedGraphNodeDegreeSummaryText));
        OnPropertyChanged(nameof(SelectedGraphNodeFireStageText));
        OnPropertyChanged(nameof(SelectedGraphNodeFireIntensityText));
        OnPropertyChanged(nameof(SelectedGraphNodeFireSummaryText));
        OnPropertyChanged(nameof(SelectedGraphNodeCurrentFuelLoadText));
        OnPropertyChanged(nameof(SelectedGraphNodeFuelLoadText));
        OnPropertyChanged(nameof(SelectedGraphNodeFuelRatioText));
        OnPropertyChanged(nameof(SelectedGraphNodeFuelSummaryText));
        OnPropertyChanged(nameof(SelectedGraphNodeAccumulatedHeatText));
        OnPropertyChanged(nameof(SelectedGraphNodeBurningElapsedText));
        OnPropertyChanged(nameof(SelectedGraphNodeThermalSummaryText));
        OnPropertyChanged(nameof(HasNoSelectedGraphNode));
    }

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartSelectedSimulation));
        OnPropertyChanged(nameof(CanExecuteStepSimulation));
        OnPropertyChanged(nameof(CanStartAutoSimulation));
        OnPropertyChanged(nameof(CanPauseAutoSimulation));
        OnPropertyChanged(nameof(CanResumeAutoSimulation));
        OnPropertyChanged(nameof(CanStopAutoSimulation));
        OnPropertyChanged(nameof(CanManageSimulationActions));

        if (SelectedSimulation != null)
            CanResetSimulation = (SelectedSimulationStatus == 2 || SelectedSimulationStatus == 3 || SelectedSimulationStatus == 1) && value;

        RefreshWorkflowStatus();
    }

    partial void OnIsSimulationRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExecuteStepSimulation));
        OnPropertyChanged(nameof(SimulationStatusText));
        OnPropertyChanged(nameof(CanStartAutoSimulation));
        OnPropertyChanged(nameof(CanPauseAutoSimulation));
        OnPropertyChanged(nameof(CanResumeAutoSimulation));
        OnPropertyChanged(nameof(CanStopAutoSimulation));
        OnPropertyChanged(nameof(CanManageSimulationActions));

        if (!value && IsAutoSimulationRunning)
            StopAutoSimulationInternal("Авто-режим остановлен: симуляция завершилась");

        RefreshWorkflowStatus();
    }

    partial void OnSelectedSimulationStatusChanged(int value)
    {
        OnPropertyChanged(nameof(SimulationStatusText));
        OnPropertyChanged(nameof(CanStartSelectedSimulation));
        OnPropertyChanged(nameof(CanExecuteStepSimulation));
        OnPropertyChanged(nameof(CanStartAutoSimulation));
        OnPropertyChanged(nameof(CanPauseAutoSimulation));
        OnPropertyChanged(nameof(CanResumeAutoSimulation));
        OnPropertyChanged(nameof(CanStopAutoSimulation));
        OnPropertyChanged(nameof(CanManageSimulationActions));

        CanResetSimulation = (value == 2 || value == 3 || value == 1) && IsConnected;

        if (value != 1 && IsAutoSimulationRunning)
            StopAutoSimulationInternal("Авто-режим остановлен: симуляция больше не в статусе Running");

        RefreshWorkflowStatus();
    }


    [RelayCommand]
    private async Task RefreshIgnitionSetupAsync()
    {
        if (SelectedSimulation == null || !IsConnected)
            return;

        SetTransientStatus("Обновление стартовых очагов...", false);
        EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Очистка сохранённых очагов...");

        var result = await _apiService.RefreshIgnitionSetupAsync(SelectedSimulation.Id);

        if (!result.Success)
        {
            SetTransientStatus($"Ошибка: {result.Message}", true);
            EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Ошибка обновления очагов: {result.Message}");
            return;
        }

        HasSavedIgnitionPreview = false;
        IsPreparedMapLoaded = false;
        IsIgnitionSelectionEnabled = false;
        SelectedIgnitionMode = IgnitionMode.Random;
        ClearSelectedIgnitionCells();
        ClearSelectedIgnitionNodes();

        if (SelectedSimulationGraphType == GraphType.Grid)
            await LoadSimulationCellsAsync(SelectedSimulation.Id);
        else
            await LoadSimulationGraphAsync(SelectedSimulation.Id);

        IsPreparedMapLoaded = true;
        IsIgnitionSelectionEnabled = IsManualIgnitionMode && CanEditIgnitionSetup;

        SetTransientStatus("Сохранённые очаги очищены", true);
        SimulationInfoText = SelectedSimulationGraphType == GraphType.Grid
            ? "Старые очаги удалены. Карта очищена, можно задать новый старт."
            : "Старые очаги удалены. Граф очищен, можно задать новый старт.";

        EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Сохранённые очаги очищены");

        OnPropertyChanged(nameof(CanStartSelectedSimulation));
        OnPropertyChanged(nameof(IgnitionSelectionSummary));
        OnPropertyChanged(nameof(CanRefreshIgnitionSetup));
        OnPropertyChanged(nameof(CanEditIgnitionSetup));
        OnPropertyChanged(nameof(ShowIgnitionControls));

        RefreshWorkflowStatus();
    }

    [RelayCommand]
    private void SwitchToGridPage()
    {
        CurrentPage = AppPage.Grid;
    }

    [RelayCommand]
    private void SwitchToGraphPage()
    {
        CurrentPage = AppPage.Graph;
    }

    [RelayCommand]
    private void SelectSmallGraphMode()
    {
        SelectedGraphCreationMode = GraphCreationMode.Small;
    }

    [RelayCommand]
    private void SelectMediumGraphMode()
    {
        SelectedGraphCreationMode = GraphCreationMode.Medium;
    }

    [RelayCommand]
    private void SelectLargeGraphMode()
    {
        SelectedGraphCreationMode = GraphCreationMode.Large;
    }
    private GraphScaleType GetSelectedGraphScaleType()
    {
        return SelectedGraphCreationMode switch
        {
            GraphCreationMode.Small => GraphScaleType.Small,
            GraphCreationMode.Medium => GraphScaleType.Medium,
            GraphCreationMode.Large => GraphScaleType.Large,
            _ => GraphScaleType.Medium
        };
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        SetTransientStatus("Проверка подключения...", false);

        var success = await _apiService.TestConnectionAsync();

        if (success)
        {
            IsConnected = true;
            SetTransientStatus("Подключение к API установлено", true);
            await LoadSimulationsAsync();
        }
        else
        {
            IsConnected = false;
            SetTransientStatus("Ошибка подключения к API", true);
        }
    }

    [RelayCommand]
    private async Task ConnectSignalRAsync()
    {
        if (_signalRService != null)
            await _signalRService.DisconnectAsync();

        _signalRService = new SignalRService($"{ApiUrl}/fireHub");

        _signalRService.OnConnected += (s, connectionId) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsSignalRConnected = true;
                SignalRStatus = "Подключено";
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Подключены потоковые данные");
                SetTransientStatus("Потоковые данные подключены", true);
            });

            if (SelectedSimulation != null)
                _ = SubscribeToSelectedSimulationAsync(SelectedSimulation.Id);
        };

        _signalRService.OnDisconnected += (s, reason) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsSignalRConnected = false;
                SignalRStatus = "Не подключено";
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Потоковые данные отключены");
            });
        };

        _signalRService.OnMovingAveragesReceived += (s, data) =>
  {
      Avalonia.Threading.Dispatcher.UIThread.Post(() =>
      {
          MovingAverage3 = data.MovingAverage3;
          MovingAverage5 = data.MovingAverage5;
          MovingAverage10 = data.MovingAverage10;
          StreamSpeed = data.Speed;
          StreamAcceleration = data.Acceleration;

          OnPropertyChanged(nameof(StreamMovingAverageText));
          OnPropertyChanged(nameof(StreamDynamicsText));
      });
  };

        _signalRService.OnTrendReceived += (s, data) =>
   {
       Avalonia.Threading.Dispatcher.UIThread.Post(() =>
       {
           TrendText = data.Trend switch
           {
               "ACCELERATING" => "Ускоряется",
               "DECELERATING" => "Замедляется",
               _ => "Стабильно"
           };

           StreamSpeed = data.Speed;
           StreamAcceleration = data.Acceleration;
           StreamIsCritical = data.IsCritical;

           OnPropertyChanged(nameof(StreamDynamicsText));
           OnPropertyChanged(nameof(StreamCriticalText));
       });
   };
        _signalRService.OnAnomalyReceived += (s, data) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                LastAnomalyText = string.IsNullOrWhiteSpace(data.Reason)
                    ? "Обнаружено отклонение"
                    : data.Reason;

                AnomalyDeviation = data.Deviation;
                AnomalyPreviousAverage = data.PreviousAvg;
                AnomalyCurrentArea = data.CurrentArea;

                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Аномалия: {LastAnomalyText}");
            });
        };

        _signalRService.OnForecastReceived += (s, data) =>
  {
      Avalonia.Threading.Dispatcher.UIThread.Post(() =>
      {
          ForecastNextArea = data.ForecastNextArea;
          ForecastDelta = data.ForecastDelta;
          ForecastMethod = GetForecastMethodName(data.Method);
          LastForecastAbsoluteError = data.LastForecastAbsoluteError;
          MeanAbsoluteError = data.MeanAbsoluteError;
          ForecastErrorCount = data.ForecastErrorCount;

          OnPropertyChanged(nameof(ForecastSummaryText));
          OnPropertyChanged(nameof(ForecastErrorSummaryText));
      });
  };

        await _signalRService.ConnectAsync();
    }

    [RelayCommand]
    private async Task LoadSimulationsAsync()
    {
        if (!IsConnected)
            return;

        SetTransientStatus("Загрузка симуляций...", false);

        var selectedId = SelectedSimulation?.Id;
        var simulations = await _apiService.GetSimulationsAsync();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Simulations.Clear();
            GridSimulations.Clear();
            GraphSimulations.Clear();

            foreach (var sim in simulations)
            {
                Simulations.Add(sim);

                if (sim.GraphType == GraphType.Grid)
                    GridSimulations.Add(sim);
                else
                    GraphSimulations.Add(sim);
            }

            if (selectedId.HasValue)
            {
                var restored = Simulations.FirstOrDefault(s => s.Id == selectedId.Value);
                if (restored != null)
                {
                    var restoredFitsPage =
                        (CurrentPage == AppPage.Grid && restored.GraphType == GraphType.Grid) ||
                        (CurrentPage == AppPage.Graph && restored.GraphType != GraphType.Grid);

                    if (restoredFitsPage)
                    {
                        SelectedSimulation = restored;
                        SelectedSimulationStatus = restored.Status;
                        SelectedSimulationGraphType = restored.GraphType;
                    }
                }
            }

            SetTransientStatus($"Список симуляций обновлён: {Simulations.Count}", true);
        });
    }

    [RelayCommand]
    private async Task CreateSimulationAsync()
    {
        if (!IsConnected)
            return;

        var mainWindow = GetMainWindow();

        if (mainWindow == null)
        {
            SetTransientStatus("Не удалось получить главное окно", true);
            return;
        }

        SimulationCreationResult? creation = null;
        bool result;

        if (CurrentPage == AppPage.Grid)
        {
            var dialog = new CreateGridSimulationDialog();
            result = await dialog.ShowDialog<bool>(mainWindow);

            if (result)
                creation = dialog.GetResult();
        }
        else
        {
            var dialog = new CreateGraphSimulationDialog(SelectedGraphCreationMode);
            result = await dialog.ShowDialog<bool>(mainWindow);

            if (result)
                creation = dialog.GetResult();
        }

        if (!result || creation == null)
            return;

        SetTransientStatus("Создание симуляции...", false);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            WindInfo = $"{creation.WindSpeed} м/с, {GetWindDirectionName(creation.WindDirection)}";
            TemperatureInfo = $"{creation.Temperature} °C";
            HumidityInfo = $"{creation.Humidity}%";
            PrecipitationInfo = $"{creation.Precipitation:F1} мм/ч";
            GridWidth = creation.GridWidth;
            GridHeight = creation.GridHeight;
            InitialFireCells = creation.InitialFireCells;
        });

        var graphType = creation.GraphType;
        var graphScaleType = creation.GraphScaleType;

        var mapModeText = creation.SelectedMapCreationMode switch
        {
            MapCreationMode.Random => "Случайная генерация",
            MapCreationMode.Scenario => "Сценарий",
            MapCreationMode.SemiManual => "Полуручное создание",
            _ => "Случайная генерация"
        };

        var scenarioText = graphType switch
        {
            GraphType.Grid => creation.SelectedScenarioType switch
            {
                MapScenarioType.MixedForest => "Смешанный лес",
                MapScenarioType.DryConiferousMassif => "Сухой хвойный массив",
                MapScenarioType.ForestWithRiver => "Лес с рекой",
                MapScenarioType.ForestWithLake => "Лес с озером",
                MapScenarioType.ForestWithFirebreak => "Лес с просекой",
                MapScenarioType.HillyTerrain => "Холмистая местность",
                MapScenarioType.WetForestAfterRain => "Влажный лес после дождя",
                _ => string.Empty
            },

            GraphType.ClusteredGraph => creation.SelectedClusteredScenarioType switch
            {
                ClusteredScenarioType.DenseDryConiferous => "Плотный сухой граф",
                ClusteredScenarioType.WaterBarrier => "Граф с водным барьером",
                ClusteredScenarioType.FirebreakGap => "Граф с разрывом / просекой",
                ClusteredScenarioType.HillyClusters => "Холмистая графовая структура",
                ClusteredScenarioType.WetAfterRain => "Влажный граф после дождя",
                ClusteredScenarioType.MixedDryHotspots => "Смешанный граф с сухими очагами",
                _ => string.Empty
            },

            _ => string.Empty
        };

        var baseShapeText = graphType switch
        {
            GraphType.Grid =>
                $"Сетка {creation.GridWidth}x{creation.GridHeight}",

            GraphType.ClusteredGraph => graphScaleType switch
            {
                GraphScaleType.Small => $"Малый граф • поле {creation.GridWidth}x{creation.GridHeight}",
                GraphScaleType.Medium => $"Средний граф • поле {creation.GridWidth}x{creation.GridHeight}",
                GraphScaleType.Large => $"Большой граф • поле {creation.GridWidth}x{creation.GridHeight}",
                _ => $"Граф • поле {creation.GridWidth}x{creation.GridHeight}"
            },

            _ => $"{creation.GridWidth}x{creation.GridHeight}"
        };

        var summaryParts = new List<string>
    {
        baseShapeText,
        mapModeText
    };

        if (!string.IsNullOrWhiteSpace(scenarioText))
            summaryParts.Add(scenarioText);

        var createDto = new CreateSimulationDto
        {
            Name = string.IsNullOrWhiteSpace(creation.SimulationName)
                ? $"Симуляция {DateTime.Now:HH:mm:ss}"
                : creation.SimulationName,

            Description = string.Join(" • ", summaryParts),

            GridWidth = creation.GridWidth,
            GridHeight = creation.GridHeight,
            GraphType = (int)graphType,
            GraphScaleType = graphScaleType,

            InitialMoistureMin = creation.MoistureMin,
            InitialMoistureMax = creation.MoistureMax,
            ElevationVariation = creation.ElevationVariation,
            InitialFireCellsCount = creation.InitialFireCells,
            SimulationSteps = creation.SimulationSteps,
            StepDurationSeconds = creation.StepDurationSeconds,
            RandomSeed = creation.RandomSeed,

            MapCreationMode = creation.SelectedMapCreationMode,
            ScenarioType = graphType == GraphType.Grid ? creation.SelectedScenarioType : null,
            ClusteredScenarioType = graphType == GraphType.ClusteredGraph ? creation.SelectedClusteredScenarioType : null,

            MapNoiseStrength = creation.MapNoiseStrength,
            MapDrynessFactor = creation.MapDrynessFactor,
            ReliefStrengthFactor = creation.ReliefStrengthFactor,
            FuelDensityFactor = creation.FuelDensityFactor,
            Precipitation = creation.Precipitation,

            MapRegionObjects = graphType == GraphType.Grid
                ? creation.MapRegionObjects
                : new List<MapRegionObjectDto>(),

            ClusteredBlueprint = graphType == GraphType.ClusteredGraph
                ? creation.ClusteredBlueprint
                : null,

            InitialFirePositions = new List<InitialFirePositionDto>(),

            VegetationDistributions = creation.VegetationDistributions
                .Select(x => new VegetationDistributionDto
                {
                    VegetationType = (VegetationType)x.VegetationType,
                    Probability = x.Probability
                })
                .ToList()
        };

        var simulationId = await _apiService.CreateSimulationAsync(
            createDto,
            creation.Temperature,
            creation.Humidity,
            creation.WindSpeed,
            creation.WindDirection);

        if (!simulationId.HasValue)
        {
            SetTransientStatus("Не удалось создать симуляцию", true);
            return;
        }

        await LoadSimulationsAsync();

        var created = Simulations.FirstOrDefault(x => x.Id == simulationId.Value);
        if (created != null)
        {
            SelectedSimulation = created;
            SelectedSimulationStatus = created.Status;
            SelectedSimulationGraphType = created.GraphType;
        }

        var createdName = graphType == GraphType.Grid
            ? "сеточная симуляция"
            : graphScaleType switch
            {
                GraphScaleType.Small => "малый граф",
                GraphScaleType.Medium => "средний граф",
                GraphScaleType.Large => "большой граф",
                _ => "графовая симуляция"
            };

        SetTransientStatus($"Создана {createdName}", true);
    }

    [RelayCommand]
    private async Task ResetSimulationAsync()
    {
        if (SelectedSimulation == null || !IsConnected)
            return;

        SetTransientStatus("Перезапуск симуляции...", false);
        EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Перезапуск симуляции {SelectedSimulation.Name}...");

        var (success, message, cells, isRunning, fireArea, currentStep, status) =
            await _apiService.ResetSimulationAsync(SelectedSimulation.Id);

        if (success)
        {
            IsSimulationRunning = false;
            FireArea = fireArea;
            CurrentStep = 0;
            SelectedSimulationStatus = 0;
            SelectedGraphNode = null;

            if (SelectedSimulation != null)
                SelectedSimulation.Status = 0;

            ClearSelectedIgnitionCells();
            ClearSelectedIgnitionNodes();

            if (SelectedSimulationGraphType == GraphType.Grid && cells != null)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Cells = new ObservableCollection<GraphCellDto>(cells);

                    var burning = cells.Count(c => c.IsBurning);
                    var burned = cells.Count(c => c.IsBurned);

                    HasSavedIgnitionPreview = burning > 0;
                    IsPreparedMapLoaded = Cells.Count > 0;
                    IsIgnitionSelectionEnabled = false;

                    SimulationInfoText = HasSavedIgnitionPreview
                        ? $"Симуляция сброшена. Показаны сохранённые стартовые очаги: {burning}. Чтобы задать новые, нажмите «Обновить очаги»."
                        : $"Сетка восстановлена: клеток {Cells.Count}, горят {burning}, сгорело {burned}. Можно запускать заново.";

                    EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Симуляция перезапущена");
                });
            }
            else
            {
                await LoadSimulationGraphAsync(SelectedSimulation!.Id);

                IsIgnitionSelectionEnabled = false;

                SimulationInfoText = HasSavedIgnitionPreview
                    ? "Графовая симуляция сброшена. Показаны сохранённые стартовые очаги. Чтобы задать новые, нажмите «Обновить очаги»."
                    : "Графовая симуляция восстановлена. Можно запускать заново.";

                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Графовая симуляция перезапущена");
            }

            SelectedIgnitionMode = IgnitionMode.Random;
            await LoadSimulationMetricsHistoryAsync(SelectedSimulation!.Id);

            OnPropertyChanged(nameof(CanStartSelectedSimulation));
            OnPropertyChanged(nameof(CanExecuteStepSimulation));
            OnPropertyChanged(nameof(CanResetSimulation));
            OnPropertyChanged(nameof(SimulationStatusText));
            OnPropertyChanged(nameof(IsRandomIgnitionMode));
            OnPropertyChanged(nameof(IsManualIgnitionMode));
            OnPropertyChanged(nameof(IgnitionModeText));
            OnPropertyChanged(nameof(IgnitionSelectionSummary));
            OnPropertyChanged(nameof(CanRefreshIgnitionSetup));
            OnPropertyChanged(nameof(CanEditIgnitionSetup));
            OnPropertyChanged(nameof(ShowIgnitionControls));

            SetTransientStatus("Симуляция перезапущена", true);
        }
        else
        {
            SetTransientStatus($"Ошибка: {message}", true);
            EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Ошибка перезапуска: {message}");
        }

        RefreshWorkflowStatus();
    }

    [RelayCommand]
    private void SetRandomIgnitionMode()
    {
        if (!CanEditIgnitionSetup)
            return;

        SelectedIgnitionMode = IgnitionMode.Random;
        RefreshWorkflowStatus();
    }

    [RelayCommand]
    private void SetManualIgnitionMode()
    {
        if (!CanEditIgnitionSetup)
            return;

        SelectedIgnitionMode = IgnitionMode.Manual;

        IsIgnitionSelectionEnabled =
            SelectedSimulation != null &&
            SelectedSimulationStatus == 0 &&
            IsPreparedMapLoaded &&
            CanEditIgnitionSetup;

        OnPropertyChanged(nameof(CanStartSelectedSimulation));
        RefreshWorkflowStatus();
    }

    [RelayCommand]
    private async Task DeleteSimulationAsync()
    {
        StopAutoSimulationInternal();

        if (SelectedSimulation == null || !IsConnected)
            return;

        var result = await _apiService.DeleteSimulationAsync(SelectedSimulation.Id);

        if (result)
        {
            SetTransientStatus("Симуляция удалена", true);
            SimulationInfoText = "Симуляция удалена";
            EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Удалена симуляция");

            SelectedSimulation = null;
            SelectedGraphNode = null;
            Cells.Clear();
            GraphNodes.Clear();
            GraphEdges.Clear();

            await LoadSimulationsAsync();
        }
        else
        {
            SetTransientStatus("Ошибка удаления", true);
        }

        RefreshWorkflowStatus();
    }

    [RelayCommand]
    private async Task StartSimulationAsync()
    {
        if (SelectedSimulation == null || !IsConnected)
            return;

        if (SelectedSimulationStatus != 0)
        {
            SimulationInfoText = "Эту симуляцию нельзя запускать повторно. Для завершённых или прерванных используйте «Перезапуск».";
            return;
        }

        if (IsManualIgnitionMode)
        {
            var hasManualSelection = SelectedSimulationGraphType == GraphType.Grid
                ? SelectedIgnitionCells.Count > 0
                : SelectedIgnitionNodes.Count > 0;

            if (!hasManualSelection)
            {
                SetTransientStatus("Сначала выберите хотя бы один очаг на визуализации", true);
                return;
            }
        }

        SetTransientStatus("Запуск симуляции...", false);

        List<(int X, int Y)>? manualPositions = null;

        if (IsManualIgnitionMode)
        {
            manualPositions = SelectedSimulationGraphType == GraphType.Grid
                ? SelectedIgnitionCells.Select(c => (c.X, c.Y)).ToList()
                : SelectedIgnitionNodes.Select(n => (n.X, n.Y)).ToList();
        }

        var ignitionMode = IsManualIgnitionMode ? "manual" : "saved-or-random";

        var (success, message, cells, isRunning, fireArea, currentStep, status) =
            await _apiService.StartSimulationAsync(SelectedSimulation.Id, ignitionMode, manualPositions);

        if (success)
        {
            IsSimulationRunning = isRunning;
            FireArea = fireArea;
            CurrentStep = currentStep;
            SelectedSimulationStatus = status >= 0 ? status : 1;
            SelectedGraphNode = null;

            IsPreparedMapLoaded = false;
            IsIgnitionSelectionEnabled = false;
            HasSavedIgnitionPreview = false;
            ClearSelectedIgnitionCells();
            ClearSelectedIgnitionNodes();

            if (SelectedSimulation != null)
                SelectedSimulation.Status = SelectedSimulationStatus;

            if (SelectedSimulationGraphType == GraphType.Grid && cells != null)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Cells = new ObservableCollection<GraphCellDto>(cells);

                    var burning = cells.Count(c => c.IsBurning);
                    var burned = cells.Count(c => c.IsBurned);

                    SimulationInfoText = $"Сетка загружена: клеток {Cells.Count}, горят {burning}, сгорело {burned}";
                    EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Симуляция запущена");
                });
            }
            else
            {
                await LoadSimulationGraphAsync(SelectedSimulation.Id);
                SimulationInfoText = "Графовая симуляция запущена";
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Графовая симуляция запущена");
            }

            await LoadSimulationMetricsHistoryAsync(SelectedSimulation.Id);

            OnPropertyChanged(nameof(CanStartSelectedSimulation));
            OnPropertyChanged(nameof(CanExecuteStepSimulation));
            OnPropertyChanged(nameof(SimulationStatusText));
            OnPropertyChanged(nameof(CanResetSimulation));
            OnPropertyChanged(nameof(IgnitionSelectionSummary));
            OnPropertyChanged(nameof(CanRefreshIgnitionSetup));
            OnPropertyChanged(nameof(CanEditIgnitionSetup));
            OnPropertyChanged(nameof(ShowIgnitionControls));

            SetTransientStatus("Симуляция запущена", true);
        }
        else
        {
            SetTransientStatus($"Ошибка: {message}", true);
        }

        RefreshWorkflowStatus();
    }

    [RelayCommand]
    private async Task ExecuteStepAsync()
    {
        if (IsAutoSimulationRunning)
            return;

        if (SelectedSimulation == null || !IsConnected)
            return;

        if (!CanExecuteStepSimulation)
        {
            SimulationInfoText = "Шаги больше выполнять нельзя";
            return;
        }

        await ExecuteSingleStepAsync(startedByAutoMode: false);
    }

    [RelayCommand]
    private async Task StartAutoSimulationAsync()
    {
        if (!CanStartAutoSimulation || SelectedSimulation == null)
            return;

        StopAutoSimulationInternal();

        _autoSimulationCts = new System.Threading.CancellationTokenSource();

        IsAutoSimulationRunning = true;
        IsAutoSimulationPaused = false;
        AutoSimulationStatusText = "Авто-режим: моделирование идёт";

        OnPropertyChanged(nameof(CanStartSelectedSimulation));
        OnPropertyChanged(nameof(CanExecuteStepSimulation));
        OnPropertyChanged(nameof(CanStartAutoSimulation));
        OnPropertyChanged(nameof(CanPauseAutoSimulation));
        OnPropertyChanged(nameof(CanResumeAutoSimulation));
        OnPropertyChanged(nameof(CanStopAutoSimulation));

        EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Запущен авто-режим");
        RefreshWorkflowStatus();

        try
        {
            while (!_autoSimulationCts.IsCancellationRequested)
            {
                if (SelectedSimulation == null || !IsSimulationRunning || SelectedSimulationStatus != 1)
                    break;

                if (IsAutoSimulationPaused)
                {
                    await Task.Delay(150, _autoSimulationCts.Token);
                    continue;
                }

                var executed = await ExecuteSingleStepAsync(startedByAutoMode: true, _autoSimulationCts.Token);
                if (!executed)
                    break;

                if (!IsSimulationRunning || SelectedSimulationStatus != 1)
                    break;

                await Task.Delay(AutoStepDelayMs, _autoSimulationCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            StopAutoSimulationInternal("Авто-режим завершён");
        }
    }

    [RelayCommand]
    private void PauseAutoSimulation()
    {
        if (!CanPauseAutoSimulation)
            return;

        IsAutoSimulationPaused = true;
        AutoSimulationStatusText = "Авто-режим: пауза";

        OnPropertyChanged(nameof(CanPauseAutoSimulation));
        OnPropertyChanged(nameof(CanResumeAutoSimulation));

        EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Авто-режим поставлен на паузу");
        RefreshWorkflowStatus();
    }

    [RelayCommand]
    private void ResumeAutoSimulation()
    {
        if (!CanResumeAutoSimulation)
            return;

        IsAutoSimulationPaused = false;
        AutoSimulationStatusText = "Авто-режим: моделирование идёт";

        OnPropertyChanged(nameof(CanPauseAutoSimulation));
        OnPropertyChanged(nameof(CanResumeAutoSimulation));

        EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Авто-режим продолжен");
        RefreshWorkflowStatus();
    }

    [RelayCommand]
    private void StopAutoSimulation()
    {
        if (!CanStopAutoSimulation)
            return;

        StopAutoSimulationInternal("Авто-режим остановлен пользователем");
        RefreshWorkflowStatus();
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        if (SelectedSimulation != null)
        {
            await LoadSimulationStatusAsync(SelectedSimulation.Id);

            if (SelectedSimulationGraphType == GraphType.Grid)
                await LoadSimulationCellsAsync(SelectedSimulation.Id);
            else
                await LoadSimulationGraphAsync(SelectedSimulation.Id);

            await LoadSimulationMetricsHistoryAsync(SelectedSimulation.Id);
        }

        RefreshWorkflowStatus();
    }

    private GraphType GetCreateGraphType()
    {
        if (CurrentPage == AppPage.Grid)
            return GraphType.Grid;

        return GraphType.ClusteredGraph;
    }

    private async Task SubscribeToSelectedSimulationAsync(Guid simulationId)
    {
        if (_signalRService?.IsConnected != true)
            return;

        var newId = simulationId.ToString();

        if (_subscribedSimulationId == newId)
            return;

        await UnsubscribeFromCurrentSimulationAsync();

        var subscribed = await _signalRService.SubscribeToSimulationAsync(newId);
        if (subscribed)
        {
            _subscribedSimulationId = newId;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Подписка на симуляцию {newId}");
            });
        }
    }

    private async Task UnsubscribeFromCurrentSimulationAsync()
    {
        if (_signalRService?.IsConnected == true && !string.IsNullOrWhiteSpace(_subscribedSimulationId))
        {
            await _signalRService.UnsubscribeFromSimulationAsync(_subscribedSimulationId);
            _subscribedSimulationId = null;
        }
    }

    private async Task LoadSimulationStatusAsync(Guid simulationId)
    {
        var status = await _apiService.GetSimulationStatusAsync(simulationId);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (status != null)
            {
                CurrentStep = status.CurrentStep;
                FireArea = status.FireArea;
                IsSimulationRunning = status.IsRunning;
                SelectedSimulationStatus = status.Status;
                SelectedSimulationGraphType = status.GraphType;

                TemperatureInfo = $"{status.Temperature:F1} °C";
                HumidityInfo = $"{status.Humidity:F1}%";
                WindInfo = $"{status.WindSpeed:F1} м/с, {GetWindDirectionName(status.WindDirectionDegrees)}";
                PrecipitationInfo = $"{status.Precipitation:F1} мм/ч";

                if (SelectedSimulation != null)
                {
                    SelectedSimulation.Status = status.Status;
                    SelectedSimulation.GraphType = status.GraphType;
                }

                SimulationInfoText = !string.IsNullOrWhiteSpace(status.Warning)
                    ? status.Warning
                    : $"Статус: «{SimulationStatusText}», шаг {CurrentStep}, площадь {FireArea:F0} га";

                OnPropertyChanged(nameof(IsGridSelected));
                OnPropertyChanged(nameof(IsClusteredGraphSelected));
                OnPropertyChanged(nameof(VisualizationMeaningText));
                OnPropertyChanged(nameof(StructureScaleText));
                OnPropertyChanged(nameof(SpreadBehaviorText));

                CanResetSimulation = (SelectedSimulationStatus == 2 || SelectedSimulationStatus == 3 || SelectedSimulationStatus == 1) && IsConnected;
            }
        });

        RefreshWorkflowStatus();
    }

    private string GetVegetationDisplayName(string? vegetation)
    {
        return vegetation?.Trim() switch
        {
            "Coniferous" => "Хвойный лес",
            "Deciduous" => "Лиственный лес",
            "Mixed" => "Смешанный лес",
            "Grass" => "Трава",
            "Shrub" => "Кустарник",
            "Water" => "Вода",
            "Bare" => "Пустая поверхность",
            null or "" => "Неизвестно",
            _ => vegetation
        };
    }

    private string GetForecastMethodName(string? method)
    {
        return method switch
        {
            "linear-regression" => "линейная регрессия",
            "recent-average-delta" => "среднее изменение",
            "current-value" => "текущее значение",
            null or "" => "неизвестный метод",
            _ => method
        };
    }

    private async Task LoadSimulationCellsAsync(Guid simulationId)
    {
        var cells = await _apiService.GetSimulationCellsAsync(simulationId);

        if (cells.Count > 0)
        {
            var maxX = cells.Max(c => c.X);
            var maxY = cells.Max(c => c.Y);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                GridWidth = maxX + 1;
                GridHeight = maxY + 1;
            });
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            Cells = new ObservableCollection<GraphCellDto>(cells);

            var burning = cells.Count(c => c.IsBurning);
            var burned = cells.Count(c => c.IsBurned);

            HasSavedIgnitionPreview =
                SelectedSimulationStatus == 0 &&
                burning > 0;

            SimulationInfoText = HasSavedIgnitionPreview
                ? $"Загружено клеток: {cells.Count}. Показаны сохранённые стартовые очаги: {burning}. Чтобы задать новые, нажмите «Обновить очаги»."
                : $"Загружено клеток: {cells.Count}. Горят: {burning}. Сгорело: {burned}.";

            var stats = cells
                .GroupBy(c => GetVegetationDisplayName(c.Vegetation))
                .Select(g => (Name: g.Key, Count: g.Count()))
                .ToList();

            VegetationStats = BuildVegetationStatsText(stats);

            IsPreparedMapLoaded = cells.Count > 0;
            IsIgnitionSelectionEnabled = IsManualIgnitionMode && CanEditIgnitionSetup;
        });

        RefreshWorkflowStatus();
    }

    private async Task LoadSimulationGraphAsync(Guid simulationId)
    {
        var graph = await _apiService.GetSimulationGraphAsync(simulationId);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (graph == null)
            {
                GraphNodes = new ObservableCollection<SimulationGraphNodeDto>();
                GraphEdges = new ObservableCollection<SimulationGraphEdgeDto>();
                SelectedGraphNode = null;
                IsPreparedMapLoaded = false;
                IsIgnitionSelectionEnabled = false;
                HasSavedIgnitionPreview = false;
                SimulationInfoText = "Не удалось загрузить граф симуляции";
                VegetationStats = "—";
                return;
            }

            var previousSelectedId = SelectedGraphNode?.Id;

            GraphLayoutHint = graph.LayoutHint;
            GridWidth = graph.Width;
            GridHeight = graph.Height;

            GraphNodes = new ObservableCollection<SimulationGraphNodeDto>(graph.Nodes);
            GraphEdges = new ObservableCollection<SimulationGraphEdgeDto>(graph.Edges);

            var burning = graph.Nodes.Count(n => n.IsBurning);
            var burned = graph.Nodes.Count(n => n.IsBurned);

            HasSavedIgnitionPreview =
                SelectedSimulationStatus == 0 &&
                burning > 0;

            SimulationInfoText = HasSavedIgnitionPreview
                ? $"Граф загружен: узлов {graph.Nodes.Count}, рёбер {graph.Edges.Count}. Показаны сохранённые стартовые очаги: {burning}. Чтобы задать новые, нажмите «Обновить очаги»."
                : $"Граф загружен: узлов {graph.Nodes.Count}, рёбер {graph.Edges.Count}. Горят: {burning}. Сгорело: {burned}.";

            var stats = graph.Nodes
                .GroupBy(n => GetVegetationDisplayName(n.Vegetation))
                .Select(g => (Name: g.Key, Count: g.Count()))
                .ToList();

            VegetationStats = BuildVegetationStatsText(stats);

            IsPreparedMapLoaded = graph.Nodes.Count > 0;
            IsIgnitionSelectionEnabled = IsManualIgnitionMode && CanEditIgnitionSetup;

            if (previousSelectedId.HasValue)
                SelectedGraphNode = graph.Nodes.FirstOrDefault(n => n.Id == previousSelectedId.Value);
            else
                SelectedGraphNode = null;

            OnPropertyChanged(nameof(SelectedGraphNodeNeighborCountText));
            OnPropertyChanged(nameof(SelectedGraphNodeStrongEdgesText));
            OnPropertyChanged(nameof(SelectedGraphNodeMediumEdgesText));
            OnPropertyChanged(nameof(SelectedGraphNodeWeakEdgesText));
        });

        RefreshWorkflowStatus();
    }
    private int GetNeighborCount(Guid nodeId)
    {
        return GraphEdges.Count(e => e.FromCellId == nodeId || e.ToCellId == nodeId);
    }

    private int GetSelectedEdgeCount(Func<SimulationGraphEdgeDto, bool> predicate)
    {
        if (SelectedGraphNode == null)
            return 0;

        return GraphEdges.Count(e =>
            (e.FromCellId == SelectedGraphNode.Id || e.ToCellId == SelectedGraphNode.Id) &&
            predicate(e));
    }
    private void ResetStreamAnalysisState()
    {
        MovingAverage3 = 0;
        MovingAverage5 = 0;
        MovingAverage10 = 0;

        TrendText = "—";

        StreamSpeed = 0;
        StreamAcceleration = 0;
        StreamIsCritical = false;

        ForecastNextArea = 0;
        ForecastDelta = 0;
        ForecastMethod = "—";
        LastForecastAbsoluteError = 0;
        MeanAbsoluteError = 0;
        ForecastErrorCount = 0;

        LastAnomalyText = "Нет";
        AnomalyDeviation = 0;
        AnomalyPreviousAverage = 0;
        AnomalyCurrentArea = 0;

        OnPropertyChanged(nameof(StreamMovingAverageText));
        OnPropertyChanged(nameof(StreamDynamicsText));
        OnPropertyChanged(nameof(StreamCriticalText));
        OnPropertyChanged(nameof(ForecastSummaryText));
        OnPropertyChanged(nameof(ForecastErrorSummaryText));
    }
    private string GetWindDirectionName(double degrees)
    {
        if (degrees >= 337.5 || degrees < 22.5) return "С";
        if (degrees >= 22.5 && degrees < 67.5) return "СВ";
        if (degrees >= 67.5 && degrees < 112.5) return "В";
        if (degrees >= 112.5 && degrees < 157.5) return "ЮВ";
        if (degrees >= 157.5 && degrees < 202.5) return "Ю";
        if (degrees >= 202.5 && degrees < 247.5) return "ЮЗ";
        if (degrees >= 247.5 && degrees < 292.5) return "З";
        return "СЗ";
    }

    private void ClearSelectedIgnitionCells()
    {
        foreach (var cell in Cells)
            cell.IsSelectedIgnition = false;

        SelectedIgnitionCells.Clear();
        Cells = new ObservableCollection<GraphCellDto>(Cells);
    }

    private void ClearSelectedIgnitionNodes()
    {
        foreach (var node in GraphNodes)
            node.IsSelectedIgnition = false;

        SelectedIgnitionNodes.Clear();
        GraphNodes = new ObservableCollection<SimulationGraphNodeDto>(GraphNodes);
    }

    private bool IsIgnitableGraphNode(SimulationGraphNodeDto node)
    {
        var vegetation = node.Vegetation?.Trim().ToLowerInvariant() ?? string.Empty;
        return vegetation != "water" && vegetation != "bare";
    }

    public void ToggleIgnitionCellSelection(GraphCellDto? cell)
    {
        if (cell == null)
            return;

        if (!IsIgnitionSelectionEnabled || !IsManualIgnitionMode || SelectedSimulationGraphType != GraphType.Grid)
            return;

        if (!cell.IsIgnitable)
            return;

        if (cell.IsSelectedIgnition)
        {
            cell.IsSelectedIgnition = false;
            SelectedIgnitionCells.Remove(cell);
        }
        else
        {
            cell.IsSelectedIgnition = true;
            SelectedIgnitionCells.Add(cell);
        }

        Cells = new ObservableCollection<GraphCellDto>(Cells);

        OnPropertyChanged(nameof(IgnitionSelectionSummary));
        OnPropertyChanged(nameof(CanStartSelectedSimulation));

        RefreshWorkflowStatus();
    }

    public void ToggleIgnitionNodeSelection(SimulationGraphNodeDto? node)
    {
        if (node == null)
            return;

        if (!IsIgnitionSelectionEnabled || !IsManualIgnitionMode || SelectedSimulationGraphType == GraphType.Grid)
            return;

        if (!IsIgnitableGraphNode(node))
            return;

        if (node.IsSelectedIgnition)
        {
            node.IsSelectedIgnition = false;
            SelectedIgnitionNodes.Remove(node);
        }
        else
        {
            node.IsSelectedIgnition = true;
            SelectedIgnitionNodes.Add(node);
        }

        GraphNodes = new ObservableCollection<SimulationGraphNodeDto>(GraphNodes);

        OnPropertyChanged(nameof(IgnitionSelectionSummary));
        OnPropertyChanged(nameof(CanStartSelectedSimulation));

        RefreshWorkflowStatus();
    }
    private async Task LoadSimulationMetricsHistoryAsync(Guid simulationId)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsMetricsHistoryLoading = true;
            MetricsHistorySummary = "Загрузка истории метрик...";
            OnPropertyChanged(nameof(HasMetricsHistory));
            OnPropertyChanged(nameof(MetricsLastFireAreaText));
            OnPropertyChanged(nameof(MetricsMaxFireAreaText));
            OnPropertyChanged(nameof(MetricsAverageSpreadSpeedText));
            OnPropertyChanged(nameof(MetricsLastWeatherText));
            OnPropertyChanged(nameof(CompactMetricsSummaryText));
        });

        var history = await _apiService.GetSimulationMetricsHistoryAsync(simulationId);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            MetricsHistory = new ObservableCollection<FireMetricsHistoryDto>(
                history.OrderByDescending(m => m.Step));

            if (MetricsHistory.Count == 0)
            {
                MetricsHistorySummary = "История метрик пока пуста";
            }
            else
            {
                MetricsHistorySummary = CompactMetricsSummaryText;
            }

            IsMetricsHistoryLoading = false;

            OnPropertyChanged(nameof(HasMetricsHistory));
            OnPropertyChanged(nameof(MetricsLastFireAreaText));
            OnPropertyChanged(nameof(MetricsMaxFireAreaText));
            OnPropertyChanged(nameof(MetricsAverageSpreadSpeedText));
            OnPropertyChanged(nameof(MetricsLastWeatherText));
            OnPropertyChanged(nameof(CompactMetricsSummaryText));
        });
    }
    private async Task ClearSimulationMetricsHistoryAsync()
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            MetricsHistory.Clear();
            IsMetricsHistoryLoading = false;
            MetricsHistorySummary = "История метрик не загружена";

            OnPropertyChanged(nameof(HasMetricsHistory));
            OnPropertyChanged(nameof(MetricsLastFireAreaText));
            OnPropertyChanged(nameof(MetricsMaxFireAreaText));
            OnPropertyChanged(nameof(MetricsAverageSpreadSpeedText));
            OnPropertyChanged(nameof(MetricsLastWeatherText));
            OnPropertyChanged(nameof(CompactMetricsSummaryText));
        });
    }

    private Window? GetMainWindow()
    {
        var app = Avalonia.Application.Current;
        if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;

        return null;
    }
}