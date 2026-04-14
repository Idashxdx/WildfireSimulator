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
    Clustered = 0,
    RegionCluster = 1
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
    private GraphCreationMode _selectedGraphCreationMode = GraphCreationMode.Clustered;

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
    public bool IsRegionClusterGraphSelected => SelectedSimulationGraphType == GraphType.RegionClusterGraph;

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

    public string VisualizationMeaningText => SelectedSimulationGraphType switch
    {
        GraphType.Grid =>
            "Регулярная карта: каждая клетка — участок поверхности, огонь распространяется по соседям и формирует фронт.",
        GraphType.ClusteredGraph =>
            "Кластерный граф: один связный граф с локальной кластеризацией вершин без явного разделения на регионы.",
        GraphType.RegionClusterGraph =>
            "Региональный граф: территория разделена на регионы, внутри которых связи плотные, а между регионами — редкие мосты.",
        _ =>
            "Модель не выбрана."
    };

    public string StructureScaleText => SelectedSimulationGraphType switch
    {
        GraphType.Grid =>
            "Масштаб: 1 клетка = 1 гектар.",
        GraphType.ClusteredGraph =>
            "Масштаб: узел — отдельный объект графа; важнее локальная связность и кластеры, чем сплошное покрытие пространства.",
        GraphType.RegionClusterGraph =>
            "Масштаб: 1 узел ≈ 1 гектар, группа узлов = регион с высокой внутренней связностью.",
        _ =>
            "Масштаб не определён."
    };

    public string SpreadBehaviorText => SelectedSimulationGraphType switch
    {
        GraphType.Grid =>
            "Ожидаемое поведение: компактный фронт, локальное распространение, сдвиг по ветру.",
        GraphType.ClusteredGraph =>
            "Ожидаемое поведение: распространение по локальным кластерам и близким связям, возможны тупики и ветвления.",
        GraphType.RegionClusterGraph =>
            "Ожидаемое поведение: локальный рост внутри региона и более поздний переход между регионами по мостам.",
        _ =>
            "Поведение не определено."
    };

    public string SimulationStatusText => SelectedSimulationStatus switch
    {
        0 => "Создана",
        1 => "Запущена",
        2 => "Завершена",
        3 => "Отменена",
        _ => "Неизвестно"
    };

    public string GraphTypeText => SelectedSimulationGraphType switch
    {
        GraphType.Grid => "Сетка",
        GraphType.ClusteredGraph => "Кластерный граф",
        GraphType.RegionClusterGraph => "Региональный граф",
        _ => "Неизвестно"
    };

    public string SelectedGraphCreationModeText => SelectedGraphCreationMode switch
    {
        GraphCreationMode.Clustered => "Кластерный граф",
        GraphCreationMode.RegionCluster => "Региональный граф",
        _ => "Кластерный граф"
    };

    public bool IsGridPage => CurrentPage == AppPage.Grid;
    public bool IsGraphPage => CurrentPage == AppPage.Graph;

    public bool IsClusteredGraphCreationMode => SelectedGraphCreationMode == GraphCreationMode.Clustered;
    public bool IsRegionClusterGraphCreationMode => SelectedGraphCreationMode == GraphCreationMode.RegionCluster;

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
            ? "Выберите симуляцию типа «Сетка»"
            : "Выберите графовую симуляцию";

        RefreshWorkflowStatus();
    }

    partial void OnSelectedGraphCreationModeChanged(GraphCreationMode value)
    {
        OnPropertyChanged(nameof(SelectedGraphCreationModeText));
        OnPropertyChanged(nameof(IsClusteredGraphCreationMode));
        OnPropertyChanged(nameof(IsRegionClusterGraphCreationMode));
    }

    partial void OnSelectedSimulationChanged(SimulationDto? value)
    {
        if (value != null)
        {
            SelectedSimulationStatus = value.Status;
            SelectedSimulationGraphType = value.GraphType;
            SelectedGraphNode = null;

            IsPreparedMapLoaded = false;
            IsIgnitionSelectionEnabled = false;
            HasSavedIgnitionPreview = false;
            SelectedIgnitionMode = IgnitionMode.Random;
            ClearSelectedIgnitionCells();
            ClearSelectedIgnitionNodes();

            OnPropertyChanged(nameof(SimulationStatusText));
            OnPropertyChanged(nameof(GraphTypeText));
            OnPropertyChanged(nameof(CanStartSelectedSimulation));
            OnPropertyChanged(nameof(CanExecuteStepSimulation));
            OnPropertyChanged(nameof(IsGridSelected));
            OnPropertyChanged(nameof(IsClusteredGraphSelected));
            OnPropertyChanged(nameof(IsRegionClusterGraphSelected));
            OnPropertyChanged(nameof(VisualizationMeaningText));
            OnPropertyChanged(nameof(StructureScaleText));
            OnPropertyChanged(nameof(SpreadBehaviorText));
            OnPropertyChanged(nameof(IsRandomIgnitionMode));
            OnPropertyChanged(nameof(IsManualIgnitionMode));
            OnPropertyChanged(nameof(IgnitionModeText));
            OnPropertyChanged(nameof(IgnitionSelectionSummary));
            OnPropertyChanged(nameof(CanRefreshIgnitionSetup));
            OnPropertyChanged(nameof(CanEditIgnitionSetup));
            OnPropertyChanged(nameof(ShowIgnitionControls));

            CanResetSimulation = (value.Status == 2 || value.Status == 3 || value.Status == 1) && IsConnected;

            SimulationInfoText = $"Загрузка данных симуляции «{value.Name}»...";

            Task.Run(async () =>
            {
                await LoadSimulationStatusAsync(value.Id);

                if (value.GraphType == GraphType.Grid)
                    await LoadSimulationCellsAsync(value.Id);
                else
                    await LoadSimulationGraphAsync(value.Id);

                await SubscribeToSelectedSimulationAsync(value.Id);
            });
        }
        else
        {
            Task.Run(async () =>
            {
                await UnsubscribeFromCurrentSimulationAsync();
            });

            SelectedSimulationStatus = 0;
            SelectedSimulationGraphType = GraphType.Grid;
            IsSimulationRunning = false;
            CurrentStep = 0;
            FireArea = 0;
            SelectedGraphNode = null;

            IsPreparedMapLoaded = false;
            IsIgnitionSelectionEnabled = false;
            HasSavedIgnitionPreview = false;
            SelectedIgnitionMode = IgnitionMode.Random;
            ClearSelectedIgnitionCells();
            ClearSelectedIgnitionNodes();

            Cells.Clear();
            GraphNodes.Clear();
            GraphEdges.Clear();

            WindInfo = "—";
            TemperatureInfo = "—";
            HumidityInfo = "—";
            VegetationStats = "—";
            PrecipitationInfo = "—";
            MovingAverage3 = 0;
            MovingAverage5 = 0;
            MovingAverage10 = 0;
            TrendText = "—";
            ForecastNextArea = 0;
            ForecastDelta = 0;
            ForecastMethod = "—";
            LastForecastAbsoluteError = 0;
            MeanAbsoluteError = 0;
            ForecastErrorCount = 0;
            SignalRStatus = IsSignalRConnected ? "Подключено" : "Не подключено";

            CanResetSimulation = false;
            SimulationInfoText = CurrentPage == AppPage.Grid
                ? "Выберите симуляцию типа «Сетка»"
                : "Выберите графовую симуляцию";

            OnPropertyChanged(nameof(SimulationStatusText));
            OnPropertyChanged(nameof(GraphTypeText));
            OnPropertyChanged(nameof(CanStartSelectedSimulation));
            OnPropertyChanged(nameof(CanExecuteStepSimulation));
            OnPropertyChanged(nameof(IsGridSelected));
            OnPropertyChanged(nameof(IsClusteredGraphSelected));
            OnPropertyChanged(nameof(IsRegionClusterGraphSelected));
            OnPropertyChanged(nameof(VisualizationMeaningText));
            OnPropertyChanged(nameof(StructureScaleText));
            OnPropertyChanged(nameof(SpreadBehaviorText));
            OnPropertyChanged(nameof(IsRandomIgnitionMode));
            OnPropertyChanged(nameof(IsManualIgnitionMode));
            OnPropertyChanged(nameof(IgnitionModeText));
            OnPropertyChanged(nameof(IgnitionSelectionSummary));
            OnPropertyChanged(nameof(CanRefreshIgnitionSetup));
            OnPropertyChanged(nameof(CanEditIgnitionSetup));
            OnPropertyChanged(nameof(ShowIgnitionControls));

            RefreshWorkflowStatus();
        }
    }

    private void SetPersistentStatus(string message)
    {
        _persistentStatusText = message;
        SetTransientStatus(message, false);
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
            WorkflowStatusText = "Выберите симуляцию";
            return;
        }

        if (IsAutoSimulationRunning && IsAutoSimulationPaused)
        {
            WorkflowStatusText = "Авто-режим: пауза";
            return;
        }

        if (IsAutoSimulationRunning)
        {
            WorkflowStatusText = "Авто-режим: моделирование идёт";
            return;
        }

        if (IsSimulationRunning && SelectedSimulationStatus == 1)
        {
            WorkflowStatusText = "Симуляция идёт";
            return;
        }

        if (SelectedSimulationStatus == 2)
        {
            WorkflowStatusText = "Симуляция завершена";
            return;
        }

        if (SelectedSimulationStatus == 3)
        {
            WorkflowStatusText = "Симуляция отменена";
            return;
        }

        if (SelectedSimulationStatus == 0)
        {
            if (HasSavedIgnitionPreview)
            {
                WorkflowStatusText = "Показаны сохранённые стартовые очаги";
                return;
            }

            if (IsManualIgnitionMode)
            {
                var selectedCount = SelectedSimulationGraphType == GraphType.Grid
                    ? SelectedIgnitionCells.Count
                    : SelectedIgnitionNodes.Count;

                WorkflowStatusText = selectedCount > 0
                    ? "Очаги выбраны, можно запускать"
                    : "Выберите стартовые очаги";
                return;
            }

            WorkflowStatusText = "Готово к запуску";
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
                await LoadSimulationStatusAsync(SelectedSimulation.Id);

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

    partial void OnSelectedGraphNodeChanged(SimulationGraphNodeDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedGraphNode));
        OnPropertyChanged(nameof(SelectedGraphNodeTitle));
        OnPropertyChanged(nameof(SelectedGraphNodeStateText));
        OnPropertyChanged(nameof(SelectedGraphNodeVegetationText));
        OnPropertyChanged(nameof(SelectedGraphNodeMoistureText));
        OnPropertyChanged(nameof(SelectedGraphNodeElevationText));
        OnPropertyChanged(nameof(SelectedGraphNodeProbabilityText));
        OnPropertyChanged(nameof(SelectedGraphNodeGroupText));
        OnPropertyChanged(nameof(SelectedGraphNodeRenderPositionText));
        OnPropertyChanged(nameof(SelectedGraphNodeNeighborCountText));
        OnPropertyChanged(nameof(SelectedGraphNodeStrongEdgesText));
        OnPropertyChanged(nameof(SelectedGraphNodeMediumEdgesText));
        OnPropertyChanged(nameof(SelectedGraphNodeWeakEdgesText));
        OnPropertyChanged(nameof(SelectedGraphNodeFireStageText));
        OnPropertyChanged(nameof(SelectedGraphNodeFireIntensityText));
        OnPropertyChanged(nameof(SelectedGraphNodeCurrentFuelLoadText));
        OnPropertyChanged(nameof(SelectedGraphNodeFuelLoadText));
        OnPropertyChanged(nameof(SelectedGraphNodeFuelRatioText));
        OnPropertyChanged(nameof(SelectedGraphNodeAccumulatedHeatText));
        OnPropertyChanged(nameof(SelectedGraphNodeBurningElapsedText));
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
    private void SelectClusteredGraphMode()
    {
        SelectedGraphCreationMode = GraphCreationMode.Clustered;
    }

    [RelayCommand]
    private void SelectRegionClusterGraphMode()
    {
        SelectedGraphCreationMode = GraphCreationMode.RegionCluster;
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
            });
        };

        _signalRService.OnAnomalyReceived += (s, data) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Аномалия: {data.Reason}");
            });
        };

        _signalRService.OnForecastReceived += (s, data) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ForecastNextArea = data.ForecastNextArea;
                ForecastDelta = data.ForecastDelta;
                ForecastMethod = $"Прогноз на следующий шаг ({GetForecastMethodName(data.Method)})";
                LastForecastAbsoluteError = data.LastForecastAbsoluteError;
                MeanAbsoluteError = data.MeanAbsoluteError;
                ForecastErrorCount = data.ForecastErrorCount;
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

        var dialog = new CreateSimulationDialog(CurrentPage, SelectedGraphCreationMode);
        var mainWindow = GetMainWindow();

        if (mainWindow == null)
        {
            SetTransientStatus("Не удалось получить главное окно", true);
            return;
        }

        var result = await dialog.ShowDialog<bool>(mainWindow);
        if (!result)
            return;

        SetTransientStatus("Создание симуляции...", false);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            WindInfo = $"{dialog.WindSpeed} м/с, {GetWindDirectionName(dialog.WindDirection)}";
            TemperatureInfo = $"{dialog.Temperature} °C";
            HumidityInfo = $"{dialog.Humidity}%";
            PrecipitationInfo = $"{dialog.Precipitation:F1} мм/ч";
            GridWidth = dialog.GridWidth;
            GridHeight = dialog.GridHeight;
            InitialFireCells = dialog.InitialFireCells;
        });

        var graphType = GetCreateGraphType();
        var graphTypeText = graphType switch
        {
            GraphType.Grid => "Сетка",
            GraphType.ClusteredGraph => "Кластерный граф",
            GraphType.RegionClusterGraph => "Региональный граф",
            _ => "Сетка"
        };

        var dto = new CreateSimulationDto
        {
            Name = dialog.SimulationName,
            Description = $"{graphTypeText} {dialog.GridWidth}x{dialog.GridHeight} | Ветер: {dialog.WindSpeed} м/с, {GetWindDirectionName(dialog.WindDirection)} | Осадки: {dialog.Precipitation} мм/ч" +
                          (dialog.RandomSeed.HasValue ? $" | Seed: {dialog.RandomSeed.Value}" : string.Empty),
            GridWidth = dialog.GridWidth,
            GridHeight = dialog.GridHeight,
            GraphType = (int)graphType,
            InitialMoistureMin = dialog.MoistureMin,
            InitialMoistureMax = dialog.MoistureMax,
            ElevationVariation = dialog.ElevationVariation,
            InitialFireCellsCount = dialog.InitialFireCells,
            SimulationSteps = dialog.SimulationSteps,
            StepDurationSeconds = dialog.StepDurationSeconds,
            RandomSeed = dialog.RandomSeed,
            Precipitation = dialog.Precipitation,
            VegetationDistributions = dialog.VegetationDistributions
                .Select(v => new VegetationDistributionDto
                {
                    VegetationType = v.VegetationType,
                    Probability = v.Probability
                })
                .ToList()
        };

        var id = await _apiService.CreateSimulationAsync(
            dto,
            dialog.Temperature,
            dialog.Humidity,
            dialog.WindSpeed,
            dialog.WindDirection);

        if (id.HasValue)
        {
            SetTransientStatus($"Симуляция создана: {dialog.SimulationName}", true);
            SimulationInfoText = $"Создана новая симуляция «{dialog.SimulationName}»";
            EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Создана симуляция: {dialog.SimulationName}");
            await LoadSimulationsAsync();

            var newSim = Simulations.FirstOrDefault(s => s.Id == id.Value);
            if (newSim != null)
            {
                SelectedSimulation = newSim;
                SelectedSimulationStatus = newSim.Status;
                SelectedSimulationGraphType = newSim.GraphType;
            }
        }
        else
        {
            SetTransientStatus("Ошибка создания симуляции", true);
        }

        RefreshWorkflowStatus();
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
        }

        RefreshWorkflowStatus();
    }

    private GraphType GetCreateGraphType()
    {
        if (CurrentPage == AppPage.Grid)
            return GraphType.Grid;

        return SelectedGraphCreationMode == GraphCreationMode.RegionCluster
            ? GraphType.RegionClusterGraph
            : GraphType.ClusteredGraph;
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
                OnPropertyChanged(nameof(IsRegionClusterGraphSelected));
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
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();

            VegetationStats = stats.Count > 0
                ? string.Join(", ", stats)
                : "—";

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
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();

            VegetationStats = stats.Count > 0
                ? string.Join(", ", stats)
                : "—";

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

    private Window? GetMainWindow()
    {
        var app = Avalonia.Application.Current;
        if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;

        return null;
    }
}