using Microsoft.Extensions.Logging;
using WildfireSimulator.Application.Interfaces;
using WildfireSimulator.Application.Models.Events;
using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Services;

public class FireSpreadSimulator : IFireSpreadSimulator
{
    private readonly ILogger<FireSpreadSimulator> _logger;
    private readonly IFireSpreadCalculator _calculator;

    private const double InternalPhysicsStepSeconds = 30.0;
    private const double WindJumpMinSpeedMps = 8.0;
    private const double WindJumpFullStrengthSpeedMps = 15.0;

    private const double WindJumpBaseDistanceCells = 2.2;
    private const double WindJumpExtraDistanceCells = 3.2;

    private const double WindJumpAngleToleranceDeg = 30.0;

    private const double WindJumpBaseHeatMultiplier = 0.04;
    private const double WindJumpExtraHeatMultiplier = 0.38;

    private const int WindJumpBaseTargetsPerSource = 1;
    private const int WindJumpExtraTargetsPerSource = 2;

    public FireSpreadSimulator(
        ILogger<FireSpreadSimulator> logger,
        IFireSpreadCalculator calculator)
    {
        _logger = logger;
        _calculator = calculator;
    }

    public async Task<SimulationStepResult> SimulateStepAsync(
        ForestGraph graph,
        WeatherCondition weather,
        int currentStep,
        Guid simulationId,
        double stepDurationSeconds)
    {
        var result = new SimulationStepResult
        {
            Step = currentStep,
            SimulationId = simulationId,
            Timestamp = DateTime.UtcNow
        };

        var events = new List<SimulationEvent>();

        try
        {
            if (stepDurationSeconds < 1.0)
                stepDurationSeconds = 1.0;

            if (stepDurationSeconds > 7200.0)
                stepDurationSeconds = 7200.0;

            double internalStepSeconds = GetInternalPropagationStepSeconds(stepDurationSeconds);
            double remainingSeconds = stepDurationSeconds;
            int internalSubstepIndex = 0;
            int totalNewlyIgnited = 0;

            _logger.LogInformation(
                "🔥 ШАГ {Step}: внешний шаг={StepDuration}s, внутренний подшаг={InternalStep}s",
                currentStep,
                stepDurationSeconds,
                internalStepSeconds);

            while (remainingSeconds > 0.0001)
            {
                internalSubstepIndex++;
                double currentInternalStep = Math.Min(internalStepSeconds, remainingSeconds);

                ExecuteInternalSubstep(
                    graph,
                    weather,
                    currentStep,
                    simulationId,
                    currentInternalStep,
                    internalSubstepIndex,
                    events,
                    ref totalNewlyIgnited);

                remainingSeconds -= currentInternalStep;

                if (!graph.Cells.Any(c => c.State == CellState.Burning))
                {
                    _logger.LogInformation(
                        "   На подшаге {Substep} активных очагов больше нет, завершаем внешний шаг досрочно",
                        internalSubstepIndex);
                    break;
                }
            }

            result.NewlyIgnitedCells = totalNewlyIgnited;

            FillResultAfterStep(graph, result, stepDurationSeconds);

            _logger.LogInformation(
                " Шаг {Step}: горят={Burning}, сгорело={Burned}, новых={New}, площадь={Area:F1} га, скорость={Speed:F2} га/час",
                currentStep,
                result.BurningCellsCount,
                result.BurnedCellsCount,
                result.NewlyIgnitedCells,
                result.FireArea,
                result.SpreadSpeed);

            events.Add(new SimulationStepCompletedEvent
            {
                SimulationId = simulationId,
                Step = currentStep,
                BurningCellsCount = result.BurningCellsCount,
                BurnedCellsCount = result.BurnedCellsCount,
                FireArea = result.FireArea,
                SpreadSpeed = result.SpreadSpeed
            });

            result.Events = events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка в FireSpreadSimulator");
            result.Error = ex.Message;
        }

        return await Task.FromResult(result);
    }

    private double GetInternalPropagationStepSeconds(double externalStepSeconds)
    {
        return externalStepSeconds <= InternalPhysicsStepSeconds
            ? externalStepSeconds
            : InternalPhysicsStepSeconds;
    }

    private void ExecuteInternalSubstep(
      ForestGraph graph,
      WeatherCondition weather,
      int currentStep,
      Guid simulationId,
      double stepDurationSeconds,
      int internalSubstepIndex,
      List<SimulationEvent> events,
      ref int totalNewlyIgnited)
    {
        CoolDownEdgeHeat(graph, stepDurationSeconds);
        CoolDownNormalCells(graph, stepDurationSeconds);

        ApplyMovingPrecipitationFront(
            graph,
            weather,
            currentStep,
            stepDurationSeconds,
            internalSubstepIndex);

        var burningCellsAtStepStart = graph.Cells
            .Where(c => c.State == CellState.Burning)
            .ToList();

        if (burningCellsAtStepStart.Count == 0)
            return;

        var totalIncomingHeat = new Dictionary<Guid, double>();
        var heatSources = new Dictionary<Guid, List<Guid>>();

        foreach (var source in burningCellsAtStepStart)
        {
            var incidentEdges = graph.GetIncidentEdges(source);
            var neighbors = new List<ForestCell>();

            foreach (var edge in incidentEdges)
            {
                var target = graph.GetOppositeCell(edge, source);
                neighbors.Add(target);

                if (target.State != CellState.Normal)
                    continue;

                double heatFlow = _calculator.CalculateHeatFlow(
                    source,
                    target,
                    weather,
                    stepDurationSeconds);

                heatFlow = ApplyEdgeAwareTransferAdjustment(
                    graph,
                    source,
                    target,
                    edge,
                    heatFlow);

                double bridgePressureHeat = CalculateBridgePressureHeat(
                    graph,
                    source,
                    target,
                    edge,
                    weather,
                    stepDurationSeconds);

                heatFlow += bridgePressureHeat;

                heatFlow = ApplyLocalPrecipitationHeatFactor(
                    graph,
                    source,
                    target,
                    weather,
                    currentStep,
                    internalSubstepIndex,
                    heatFlow);

                if (heatFlow <= 0.0)
                    continue;

                totalIncomingHeat[target.Id] =
                    totalIncomingHeat.GetValueOrDefault(target.Id) + heatFlow;

                double edgeMemoryHeat = ConvertToEdgeMemoryHeat(
                    heatFlow,
                    edge,
                    stepDurationSeconds);

                if (edgeMemoryHeat > 0.0)
                    edge.AddAccumulatedHeat(edgeMemoryHeat);

                if (!heatSources.ContainsKey(target.Id))
                    heatSources[target.Id] = new List<Guid>();

                heatSources[target.Id].Add(source.Id);
            }

            ApplyWindDrivenHeatTransfers(
                graph,
                source,
                neighbors,
                weather,
                stepDurationSeconds,
                totalIncomingHeat,
                heatSources);
        }

        var candidates = BuildCandidates(
            graph,
            weather,
            totalIncomingHeat,
            heatSources);

        var sortedCandidates = candidates
            .OrderByDescending(c => c.Probability)
            .ThenByDescending(c => c.Ratio)
            .ToList();

        int substepIgnitions = IgniteCandidates(
            sortedCandidates,
            currentStep,
            simulationId,
            events);

        totalNewlyIgnited += substepIgnitions;

        UpdateBurningCells(
            burningCellsAtStepStart,
            weather,
            stepDurationSeconds);
    }
    private void ApplyMovingPrecipitationFront(
      ForestGraph graph,
      WeatherCondition weather,
      int currentStep,
      double stepDurationSeconds,
      int internalSubstepIndex)
    {
        double precipitationPercent = GetPrecipitationPercent(weather);
        if (precipitationPercent <= 0.0)
            return;

        foreach (var cell in graph.Cells)
        {
            if (cell.State == CellState.Burned)
                continue;

            if (cell.Vegetation == VegetationType.Water ||
                cell.Vegetation == VegetationType.Bare)
            {
                continue;
            }

            double frontRainPercent = GetMovingPrecipitationFrontIntensity(
                graph,
                weather,
                cell,
                currentStep,
                internalSubstepIndex);

            double trailRainPercent = GetMovingPrecipitationTrailIntensity(
                graph,
                weather,
                cell,
                currentStep,
                internalSubstepIndex);

            double localMoistureRainPercent =
                frontRainPercent + trailRainPercent * 0.45;

            if (localMoistureRainPercent > 0.0)
            {
                double rainLevel = localMoistureRainPercent / 100.0;

                double moistureIncrease =
                    rainLevel *
                    stepDurationSeconds *
                    0.00016;

                double maxMoisture = cell.State == CellState.Burning ? 0.84 : 0.96;

                double newMoisture = Math.Clamp(
                    cell.Moisture + moistureIncrease,
                    0.0,
                    maxMoisture);

                cell.UpdateMoisture(newMoisture);
                continue;
            }

            if (cell.State == CellState.Normal)
            {
                double evaporation =
                    weather.CalculateMoistureEvaporation() *
                    stepDurationSeconds *
                    0.0000025;

                if (evaporation > 0.0)
                {
                    double newMoisture = Math.Clamp(
                        cell.Moisture - evaporation,
                        0.0,
                        1.0);

                    cell.UpdateMoisture(newMoisture);
                }
            }
        }
    }

    private double GetPrecipitationPercent(WeatherCondition weather)
    {
        return Math.Clamp(weather.Precipitation, 0.0, 100.0);
    }

    private double GetMovingPrecipitationFrontIntensity(
      ForestGraph graph,
      WeatherCondition weather,
      ForestCell cell,
      int currentStep,
      int internalSubstepIndex)
    {
        double precipitationPercent = GetPrecipitationPercent(weather);
        if (precipitationPercent <= 0.0)
            return 0.0;

        double width = Math.Max(1.0, graph.Width);
        double height = Math.Max(1.0, graph.Height);

        double centerX = width / 2.0;
        double centerY = height / 2.0;

        double diagonal = Math.Sqrt(width * width + height * height);

        double frontLength = Math.Max(12.0, diagonal * 1.35);
        double frontThickness = Math.Max(5.0, diagonal * 0.24);

        var moveDirection = GetPrecipitationFlowDirection(weather);

        double bandX = -moveDirection.Y;
        double bandY = moveDirection.X;

        double externalStepSeconds = graph.StepDurationSeconds > 0
            ? graph.StepDurationSeconds
            : 900.0;

        double modelTimeSeconds =
            Math.Max(0, currentStep - 1) * externalStepSeconds +
            Math.Max(0, internalSubstepIndex - 1) * InternalPhysicsStepSeconds;

        double speedCellsPerSecond =
            0.00120 + weather.WindSpeedMps * 0.00018;

        speedCellsPerSecond = Math.Clamp(speedCellsPerSecond, 0.00120, 0.00420);

        double travelDistance = diagonal + frontThickness * 2.0;

        double position =
            (modelTimeSeconds * speedCellsPerSecond) % travelDistance
            - diagonal / 2.0
            - frontThickness;

        double frontCenterX = centerX + moveDirection.X * position;
        double frontCenterY = centerY + moveDirection.Y * position;

        double dx = cell.X - frontCenterX;
        double dy = cell.Y - frontCenterY;

        double distanceAlongMove = dx * moveDirection.X + dy * moveDirection.Y;
        double distanceAlongBand = dx * bandX + dy * bandY;

        if (Math.Abs(distanceAlongMove) > frontThickness / 2.0)
            return 0.0;

        if (Math.Abs(distanceAlongBand) > frontLength / 2.0)
            return 0.0;

        double moveFade =
            1.0 - Math.Abs(distanceAlongMove) / (frontThickness / 2.0);

        double bandFade =
            1.0 - Math.Abs(distanceAlongBand) / (frontLength / 2.0);

        double coverage = moveFade * 0.85 + bandFade * 0.15;
        coverage = Math.Clamp(coverage, 0.0, 1.0);

        return precipitationPercent * coverage;
    }
    private double GetMovingPrecipitationTrailIntensity(
        ForestGraph graph,
        WeatherCondition weather,
        ForestCell cell,
        int currentStep,
        int internalSubstepIndex)
    {
        double precipitationPercent = GetPrecipitationPercent(weather);
        if (precipitationPercent <= 0.0)
            return 0.0;

        double width = Math.Max(1.0, graph.Width);
        double height = Math.Max(1.0, graph.Height);

        double centerX = width / 2.0;
        double centerY = height / 2.0;

        double diagonal = Math.Sqrt(width * width + height * height);

        double frontLength = Math.Max(12.0, diagonal * 1.35);
        double frontThickness = Math.Max(5.0, diagonal * 0.24);
        double trailLength = Math.Max(8.0, diagonal * 0.42);

        var moveDirection = GetPrecipitationFlowDirection(weather);

        double bandX = -moveDirection.Y;
        double bandY = moveDirection.X;

        double externalStepSeconds = graph.StepDurationSeconds > 0
            ? graph.StepDurationSeconds
            : 900.0;

        double modelTimeSeconds =
            Math.Max(0, currentStep - 1) * externalStepSeconds +
            Math.Max(0, internalSubstepIndex - 1) * InternalPhysicsStepSeconds;

        double speedCellsPerSecond =
            0.00120 + weather.WindSpeedMps * 0.00018;

        speedCellsPerSecond = Math.Clamp(speedCellsPerSecond, 0.00120, 0.00420);

        double travelDistance = diagonal + frontThickness * 2.0;

        double position =
            (modelTimeSeconds * speedCellsPerSecond) % travelDistance
            - diagonal / 2.0
            - frontThickness;

        double frontCenterX = centerX + moveDirection.X * position;
        double frontCenterY = centerY + moveDirection.Y * position;

        double dx = cell.X - frontCenterX;
        double dy = cell.Y - frontCenterY;

        double distanceAlongMove = dx * moveDirection.X + dy * moveDirection.Y;
        double distanceAlongBand = dx * bandX + dy * bandY;

        double frontBack = -frontThickness / 2.0;

        if (distanceAlongMove >= frontBack)
            return 0.0;

        double trailDistance = Math.Abs(distanceAlongMove - frontBack);

        if (trailDistance > trailLength)
            return 0.0;

        if (Math.Abs(distanceAlongBand) > frontLength / 2.0)
            return 0.0;

        double trailFade = 1.0 - trailDistance / trailLength;
        double bandFade = 1.0 - Math.Abs(distanceAlongBand) / (frontLength / 2.0);

        double coverage = trailFade * 0.75 + bandFade * 0.25;
        coverage = Math.Clamp(coverage, 0.0, 1.0);

        return precipitationPercent * coverage;
    }
    private double ApplyLocalPrecipitationHeatFactor(
      ForestGraph graph,
      ForestCell source,
      ForestCell target,
      WeatherCondition weather,
      int currentStep,
      int internalSubstepIndex,
      double heatFlow)
    {
        if (heatFlow <= 0.0)
            return heatFlow;

        double precipitationPercent = GetPrecipitationPercent(weather);
        if (precipitationPercent <= 0.0)
            return heatFlow;

        double sourceRainPercent = GetMovingPrecipitationFrontIntensity(
            graph,
            weather,
            source,
            currentStep,
            internalSubstepIndex);

        double targetRainPercent = GetMovingPrecipitationFrontIntensity(
            graph,
            weather,
            target,
            currentStep,
            internalSubstepIndex);

        double localRainPercent = Math.Max(sourceRainPercent, targetRainPercent);

        if (localRainPercent <= 0.0)
            return heatFlow;

        double rainLevel = localRainPercent / 100.0;

        double precipitationFactor = 1.0 / (1.0 + rainLevel * 3.2);
        precipitationFactor = Math.Clamp(precipitationFactor, 0.28, 1.0);

        return heatFlow * precipitationFactor;
    }
    private (double X, double Y) GetPrecipitationFlowDirection(WeatherCondition weather)
    {
        double flowDirectionDegrees =
            (weather.WindDirectionDegrees + 180.0) % 360.0;

        double radians = flowDirectionDegrees * Math.PI / 180.0;

        double x = Math.Sin(radians);
        double y = -Math.Cos(radians);

        double length = Math.Sqrt(x * x + y * y);

        if (length < 0.0001)
            return (0.0, 1.0);

        return (x / length, y / length);
    }

    private double CalculateBridgePressureHeat(
       ForestGraph graph,
       ForestCell source,
       ForestCell target,
       ForestEdge edge,
       WeatherCondition weather,
       double stepDurationSeconds)
    {
        if (source.State != CellState.Burning || target.State != CellState.Normal)
            return 0.0;

        if (!edge.IsCorridor)
            return 0.0;

        if (string.IsNullOrWhiteSpace(source.ClusterId) ||
            string.IsNullOrWhiteSpace(target.ClusterId) ||
            string.Equals(source.ClusterId, target.ClusterId, StringComparison.Ordinal))
        {
            return 0.0;
        }

        if (target.Vegetation == VegetationType.Water ||
            target.Vegetation == VegetationType.Bare)
        {
            return 0.0;
        }

        int burningPressureCells = graph.Cells.Count(c =>
            c.ClusterId == source.ClusterId &&
            c.State == CellState.Burning &&
            CalculateDistance(source.X, source.Y, c.X, c.Y) <= 2.6);

        int affectedPressureCells = graph.Cells.Count(c =>
            c.ClusterId == source.ClusterId &&
            (c.State == CellState.Burning || c.State == CellState.Burned) &&
            CalculateDistance(source.X, source.Y, c.X, c.Y) <= 2.8);

        if (burningPressureCells < 3 || affectedPressureCells < 3)
            return 0.0;

        double threshold = _calculator.CalculateIgnitionThreshold(target, weather);
        if (double.IsInfinity(threshold) || threshold <= 0.0)
            return 0.0;

        double sourceBurnDuration = FireModelCatalog.Get(source.Vegetation).BaseBurnDurationSeconds;
        double sourceBurnProgress =
            sourceBurnDuration > 0.0 && !double.IsInfinity(sourceBurnDuration)
                ? Math.Clamp(source.BurningElapsedSeconds / sourceBurnDuration, 0.0, 1.0)
                : 0.0;

        if (sourceBurnProgress < 0.20)
            return 0.0;

        double pressure =
            0.006 +
            (burningPressureCells - 3) * 0.004 +
            Math.Min(affectedPressureCells - 3, 4) * 0.002;

        pressure = Math.Clamp(pressure, 0.0, 0.030);

        double distanceFactor = edge.Distance switch
        {
            <= 3.0 => 0.85,
            <= 5.0 => 0.62,
            <= 7.5 => 0.44,
            <= 10.0 => 0.30,
            _ => 0.20
        };

        double durationFactor = Math.Clamp(stepDurationSeconds / 900.0, 0.20, 0.85);

        double maturityFactor = sourceBurnProgress switch
        {
            < 0.30 => 0.55,
            < 0.55 => 0.80,
            < 0.75 => 1.00,
            _ => 0.75
        };

        return threshold *
               pressure *
               distanceFactor *
               durationFactor *
               maturityFactor;
    }
    private void CoolDownEdgeHeat(ForestGraph graph, double stepDurationSeconds)
    {
        double retentionFactor = Math.Exp(-stepDurationSeconds / 300.0);
        retentionFactor = Math.Clamp(retentionFactor, 0.0, 1.0);

        foreach (var edge in graph.Edges)
            edge.CoolDownHeat(retentionFactor);
    }
    private List<(ForestCell Cell, double Probability, double AccumulatedHeat, double Threshold, double Ratio)> BuildCandidates(
      ForestGraph graph,
      WeatherCondition weather,
      Dictionary<Guid, double> totalIncomingHeat,
      Dictionary<Guid, List<Guid>> heatSources)
    {
        var candidates = new List<(ForestCell Cell, double Probability, double AccumulatedHeat, double Threshold, double Ratio)>();
        var topology = DetectTopology(graph);
        bool isGrid = topology == SpreadTopology.Grid;

        foreach (var target in graph.Cells.Where(c => c.State == CellState.Normal))
        {
            double transientHeat = totalIncomingHeat.GetValueOrDefault(target.Id);
            double residualEdgeHeat = GetResidualEdgeHeatForTarget(graph, target);

            if (transientHeat <= 0.0 &&
                residualEdgeHeat <= 0.0 &&
                target.AccumulatedHeatJ <= 0.0)
            {
                continue;
            }

            int sourceCount = heatSources.TryGetValue(target.Id, out var sources)
                ? sources.Distinct().Count()
                : 0;

            double threshold = _calculator.CalculateIgnitionThreshold(target, weather);
            if (threshold <= 0.0 || double.IsInfinity(threshold))
            {
                target.SetBurnProbability(0.0);
                continue;
            }

            double accumulatedHeat = target.AccumulatedHeatJ + transientHeat + residualEdgeHeat;
            double ratio = accumulatedHeat / threshold;

            if (isGrid)
            {
                if (sourceCount >= 2 && ratio >= 0.50)
                {
                    double multiSourceFactor = 1.0 + Math.Min(0.26, (sourceCount - 1) * 0.09);
                    accumulatedHeat *= multiSourceFactor;
                    ratio = accumulatedHeat / threshold;
                }

                if (ratio >= 0.56 && ratio < 1.0)
                {
                    double boost = ratio switch
                    {
                        >= 0.92 => 1.13,
                        >= 0.82 => 1.09,
                        >= 0.72 => 1.05,
                        _ => 1.02
                    };

                    accumulatedHeat *= boost;
                    ratio = accumulatedHeat / threshold;
                }

                target.SetAccumulatedHeatJ(accumulatedHeat);

                double probability = _calculator.CalculateIgnitionProbability(accumulatedHeat, threshold);

                if (transientHeat > 0.0)
                {
                    double diagnosticProbability = Math.Clamp(ratio * 0.012, 0.001, 0.030);

                    if (sourceCount >= 1)
                        probability = Math.Max(probability, diagnosticProbability);
                }

                if (ratio < 0.56)
                {
                    probability = Math.Min(probability, 0.020);
                    target.SetBurnProbability(probability);
                    continue;
                }

                if (ratio < 0.66)
                {
                    probability = Math.Min(probability * 0.30, 0.035);
                }
                else if (ratio < 0.76)
                {
                    probability = Math.Min(probability * 0.42, 0.065);
                }
                else if (ratio < 0.86)
                {
                    probability = Math.Min(probability * 0.56, 0.105);
                }
                else if (ratio < 0.96)
                {
                    probability = Math.Min(probability * 0.70, 0.155);
                }
                else if (ratio < 1.0)
                {
                    probability = Math.Min(probability * 0.82, 0.210);
                }
                else
                {
                    probability = Math.Max(probability, 0.38);
                }

                if (sourceCount >= 2 && ratio >= 0.70)
                    probability = Math.Max(probability, 0.060);

                if (sourceCount >= 3 && ratio >= 0.64)
                    probability = Math.Max(probability, 0.085);

                target.SetBurnProbability(probability);

                if (probability < 0.004 && ratio < 0.68)
                    continue;

                candidates.Add((target, probability, accumulatedHeat, threshold, ratio));
                continue;
            }

            if (sourceCount >= 2)
            {
                double multiSourceFactor = 1.0 + Math.Min(0.42, (sourceCount - 1) * 0.14);
                accumulatedHeat *= multiSourceFactor;
                ratio = accumulatedHeat / threshold;
            }

            if (ratio >= 0.55 && ratio < 1.0)
            {
                double boost = ratio switch
                {
                    >= 0.90 => 1.25,
                    >= 0.80 => 1.18,
                    >= 0.70 => 1.12,
                    _ => 1.07
                };

                accumulatedHeat *= boost;
                ratio = accumulatedHeat / threshold;
            }

            target.SetAccumulatedHeatJ(accumulatedHeat);

            double graphProbability = _calculator.CalculateIgnitionProbability(accumulatedHeat, threshold);

            if (transientHeat > 0.0)
                graphProbability = Math.Max(graphProbability, Math.Clamp(ratio * 0.010, 0.001, 0.025));

            if (sourceCount >= 2)
                graphProbability = Math.Max(graphProbability, 0.035 + sourceCount * 0.015);

            if (ratio >= 0.85)
                graphProbability = Math.Max(graphProbability, 0.20);
            else if (ratio >= 0.75)
                graphProbability = Math.Max(graphProbability, 0.13);
            else if (ratio >= 0.65)
                graphProbability = Math.Max(graphProbability, 0.070);
            else if (ratio >= 0.55)
                graphProbability = Math.Max(graphProbability, 0.030);

            target.SetBurnProbability(graphProbability);

            bool hasMeaningfulTransientHeat = transientHeat >= 35_000.0;
            bool hasMeaningfulResidualHeat = residualEdgeHeat >= 80_000.0;
            bool hasStoredCellHeat = target.AccumulatedHeatJ >= 100_000.0;
            bool hasMultipleSources = sourceCount >= 2;

            if (graphProbability < 0.002 &&
                ratio < 0.52 &&
                !hasMeaningfulTransientHeat &&
                !hasMeaningfulResidualHeat &&
                !hasStoredCellHeat &&
                !hasMultipleSources)
            {
                continue;
            }

            candidates.Add((target, graphProbability, accumulatedHeat, threshold, ratio));
        }

        return candidates;
    }
    private double GetResidualEdgeHeatForTarget(ForestGraph graph, ForestCell target)
    {
        var orderedEdgeHeat = graph.GetIncidentEdges(target)
            .Select(e => e.AccumulatedHeat)
            .Where(h => h > 0.0)
            .OrderByDescending(h => h)
            .ToList();

        if (orderedEdgeHeat.Count == 0)
            return 0.0;

        double primaryContribution = orderedEdgeHeat.Take(1).Sum() * 0.65;
        double secondaryContribution = orderedEdgeHeat.Skip(1).Take(2).Sum() * 0.25;
        double tertiaryContribution = orderedEdgeHeat.Skip(3).Sum() * 0.05;

        double coupledHeat = primaryContribution + secondaryContribution + tertiaryContribution;
        return Math.Clamp(coupledHeat, 0.0, 2_500_000.0);
    }
    private double ConvertToEdgeMemoryHeat(
     double heatFlow,
     ForestEdge edge,
     double stepDurationSeconds)
    {
        if (heatFlow <= 0.0)
            return 0.0;

        bool isBridge = edge.FromCell != null &&
                        edge.ToCell != null &&
                        !string.Equals(edge.FromCell.ClusterId, edge.ToCell.ClusterId, StringComparison.Ordinal);

        double distanceFactor = edge.Distance switch
        {
            <= 1.45 => 1.00,
            <= 2.10 => 0.82,
            <= 2.80 => 0.64,
            <= 3.60 => 0.48,
            <= 5.50 => isBridge ? 0.46 : 0.32,
            <= 8.00 => isBridge ? 0.40 : 0.24,
            _ => isBridge ? 0.34 : 0.18
        };

        double modifierFactor = Math.Clamp(edge.FireSpreadModifier, 0.05, 1.60);
        double durationFactor = Math.Clamp(stepDurationSeconds / 300.0, 0.10, 1.00);

        double bridgeMemoryBonus = isBridge
            ? edge.IsCorridor ? 2.60 : 2.10
            : 1.00;

        double memoryFraction =
            0.022 *
            distanceFactor *
            (0.78 + 0.22 * modifierFactor) *
            durationFactor *
            bridgeMemoryBonus;

        return heatFlow * memoryFraction;
    }
    private double ApplyEdgeAwareTransferAdjustment(
     ForestGraph graph,
     ForestCell source,
     ForestCell target,
     ForestEdge edge,
     double baseHeatFlow)
    {
        if (baseHeatFlow <= 0.0)
            return 0.0;

        double edgeModifier = Math.Clamp(edge.FireSpreadModifier, 0.02, 1.60);
        var topology = DetectTopology(graph);

        if (topology == SpreadTopology.Grid)
        {
            double adjustedGridHeat = baseHeatFlow * edgeModifier;
            return Math.Min(adjustedGridHeat, baseHeatFlow * 1.35);
        }

        int sourceDegree = graph.GetIncidentEdges(source).Count;
        int targetDegree = graph.GetIncidentEdges(target).Count;
        int minDegree = Math.Max(1, Math.Min(sourceDegree, targetDegree));

        double directionalFactor = GetDirectionalEdgeFactor(source, target, edge, graph, topology);

        if (topology == SpreadTopology.ClusteredArea)
        {
            bool sameCluster = string.Equals(source.ClusterId, target.ClusterId, StringComparison.Ordinal);

            if (sameCluster)
            {
                int sameClusterNeighborsSource = graph.GetNeighbors(source).Count(n => n.ClusterId == source.ClusterId);
                int sameClusterNeighborsTarget = graph.GetNeighbors(target).Count(n => n.ClusterId == target.ClusterId);

                int interClusterNeighborsSource = graph.GetNeighbors(source).Count(n => n.ClusterId != source.ClusterId);
                int interClusterNeighborsTarget = graph.GetNeighbors(target).Count(n => n.ClusterId != target.ClusterId);

                double intraClusterSupport = edge.Distance switch
                {
                    <= 1.05 => 2.25,
                    <= 1.45 => 2.85,
                    <= 2.20 => 3.55,
                    <= 3.00 => 3.95,
                    _ => 4.15
                };
                double sparseConnectivityFactor = 1.0 + 0.26 / Math.Sqrt(minDegree);
                sparseConnectivityFactor = Math.Clamp(sparseConnectivityFactor, 1.0, 1.26);

                double similarityFactor = 1.0;

                if (source.Vegetation == target.Vegetation)
                    similarityFactor += 0.10;

                double moistureGap = Math.Abs(source.Moisture - target.Moisture);

                if (moistureGap <= 0.08)
                    similarityFactor += 0.05;
                else if (moistureGap <= 0.16)
                    similarityFactor += 0.02;

                similarityFactor = Math.Clamp(similarityFactor, 1.0, 1.15);

                double neighborhoodSupport =
                    1.0 +
                    Math.Max(0, Math.Min(sameClusterNeighborsSource, sameClusterNeighborsTarget) - 2) * 0.09;

                neighborhoodSupport = Math.Clamp(neighborhoodSupport, 1.0, 1.32);

                double interiorBonus = 1.0;

                if (interClusterNeighborsSource == 0)
                    interiorBonus += 0.12;

                if (interClusterNeighborsTarget == 0)
                    interiorBonus += 0.12;

                if (sameClusterNeighborsSource >= 4 && sameClusterNeighborsTarget >= 4)
                    interiorBonus += 0.10;
                else if (sameClusterNeighborsSource >= 3 && sameClusterNeighborsTarget >= 3)
                    interiorBonus += 0.06;

                interiorBonus = Math.Clamp(interiorBonus, 1.0, 1.34);

                double adjusted =
                    baseHeatFlow *
                    edgeModifier *
                    intraClusterSupport *
                    sparseConnectivityFactor *
                    similarityFactor *
                    neighborhoodSupport *
                    interiorBonus *
                    directionalFactor *
                    1.10;

                return Math.Min(adjusted, baseHeatFlow * 9.20);
            }

            var sourceNeighbors = graph.GetNeighbors(source);

            int sameClusterBurningNeighbors = sourceNeighbors.Count(n =>
                n.ClusterId == source.ClusterId &&
                n.State == CellState.Burning);

            int sameClusterAffectedNeighbors = sourceNeighbors.Count(n =>
                n.ClusterId == source.ClusterId &&
                (n.State == CellState.Burning || n.State == CellState.Burned));

            int sameClusterNormalNeighbors = sourceNeighbors.Count(n =>
                n.ClusterId == source.ClusterId &&
                n.State == CellState.Normal);

            int bridgeNeighborCount = sourceNeighbors.Count(n =>
                n.ClusterId != source.ClusterId);

            double sourceBurnDuration = FireModelCatalog.Get(source.Vegetation).BaseBurnDurationSeconds;
            double sourceBurnProgress =
                sourceBurnDuration > 0.0 && !double.IsInfinity(sourceBurnDuration)
                    ? Math.Clamp(source.BurningElapsedSeconds / sourceBurnDuration, 0.0, 1.0)
                    : 0.0;

            double maturityFactor = sourceBurnProgress switch
            {
                < 0.08 => 0.85,
                < 0.15 => 1.05,
                < 0.25 => 1.25,
                < 0.40 => 1.45,
                < 0.60 => 1.65,
                _ => 1.80
            };

            double bridgeDistanceFactor = edge.Distance switch
            {
                <= 2.00 => 1.65,
                <= 3.00 => 1.55,
                <= 4.50 => 1.42,
                <= 6.50 => 1.30,
                <= 8.50 => 1.18,
                _ => 1.05
            };

            double localFirePressure = 1.0;

            if (sameClusterBurningNeighbors >= 1)
                localFirePressure += 0.35;

            if (sameClusterBurningNeighbors >= 2)
                localFirePressure += 0.45;

            if (sameClusterBurningNeighbors >= 3)
                localFirePressure += 0.45;

            if (sameClusterAffectedNeighbors >= 3)
                localFirePressure += 0.35;

            if (sameClusterAffectedNeighbors >= 5)
                localFirePressure += 0.35;

            if (sameClusterNormalNeighbors <= 2)
                localFirePressure += 0.20;

            localFirePressure = Math.Clamp(localFirePressure, 1.0, 3.20);

            double bridgeNodePressure = bridgeNeighborCount > 0
                ? 1.35
                : 1.0;

            double corridorFactor = edge.IsCorridor
                ? edge.Distance switch
                {
                    >= 7.0 => 2.20,
                    >= 5.0 => 2.00,
                    >= 3.5 => 1.80,
                    _ => 1.60
                }
                : 1.45;

            double bridgeMoistureGap = Math.Abs(source.Moisture - target.Moisture);
            double bridgeSimilarityFactor = bridgeMoistureGap switch
            {
                <= 0.08 => 1.12,
                <= 0.16 => 1.06,
                <= 0.28 => 1.00,
                _ => 0.92
            };

            if (source.Vegetation == target.Vegetation)
                bridgeSimilarityFactor += 0.06;

            bridgeSimilarityFactor = Math.Clamp(bridgeSimilarityFactor, 0.85, 1.18);

            double bridgeSupport = edge.Distance switch
            {
                <= 3.0 => 1.80,
                <= 5.0 => 2.20,
                <= 7.5 => 2.70,
                <= 10.0 => 3.10,
                _ => 3.40
            };

            double adjustedBridgeHeat =
                baseHeatFlow *
                edgeModifier *
                bridgeDistanceFactor *
                bridgeSimilarityFactor *
                maturityFactor *
                localFirePressure *
                bridgeNodePressure *
                corridorFactor *
                bridgeSupport *
                directionalFactor;

            return Math.Min(adjustedBridgeHeat, baseHeatFlow * 9.50);
        }

        return baseHeatFlow * edgeModifier;
    }

    private double GetDirectionalEdgeFactor(
     ForestCell source,
     ForestCell target,
     ForestEdge edge,
     ForestGraph graph,
     SpreadTopology topology)
    {
        if (topology == SpreadTopology.Grid)
            return 1.0;

        double dx = target.X - source.X;
        double dy = target.Y - source.Y;

        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 0.0001)
            return 1.0;

        dx /= length;
        dy /= length;

        var neighbors = graph.GetNeighbors(source);
        if (neighbors.Count <= 1)
            return 1.0;

        double avgDx = 0.0;
        double avgDy = 0.0;

        foreach (var n in neighbors)
        {
            double ndx = n.X - source.X;
            double ndy = n.Y - source.Y;
            double nl = Math.Sqrt(ndx * ndx + ndy * ndy);
            if (nl < 0.0001)
                continue;

            avgDx += ndx / nl;
            avgDy += ndy / nl;
        }

        double avgLen = Math.Sqrt(avgDx * avgDx + avgDy * avgDy);
        if (avgLen < 0.0001)
            return 1.0;

        avgDx /= avgLen;
        avgDy /= avgLen;

        double alignment = dx * avgDx + dy * avgDy;
        alignment = Math.Clamp(alignment, -1.0, 1.0);

        double factor;

        if (edge.IsCorridor)
        {
            factor = 1.0 + alignment * 0.22;
        }
        else
        {
            factor = 1.0 + alignment * 0.08;
        }

        return Math.Clamp(factor, 0.82, 1.35);
    }

    private double GetGeometricAxisBias(
        ForestCell source,
        ForestCell target,
        ForestGraph graph)
    {
        if (graph.Width <= 0 || graph.Height <= 0)
            return 1.0;

        double centerX = graph.Width / 2.0;
        double centerY = graph.Height / 2.0;

        double sourceRadius = CalculateDistance(source.X, source.Y, centerX, centerY);
        double targetRadius = CalculateDistance(target.X, target.Y, centerX, centerY);

        double radialDelta = targetRadius - sourceRadius;

        if (graph.Cells.Count >= 80)
        {
            if (radialDelta > 0.75)
                return 1.04;

            if (radialDelta < -0.75)
                return 0.98;
        }

        return 1.0;
    }
    private int CountInterClusterNeighbors(ForestGraph graph, ForestCell cell)
    {
        if (string.IsNullOrWhiteSpace(cell.ClusterId))
            return 0;

        return graph.GetNeighbors(cell).Count(n => n.ClusterId != cell.ClusterId);
    }
    private SpreadTopology DetectTopology(ForestGraph graph)
    {
        bool hasGroupedGraphNodes = graph.Cells.Any(c => !string.IsNullOrWhiteSpace(c.ClusterId));
        if (hasGroupedGraphNodes)
            return SpreadTopology.ClusteredArea;

        if (graph.Cells.Count == graph.Width * graph.Height)
            return SpreadTopology.Grid;

        return SpreadTopology.ClusteredArea;
    }

    private void ApplyWindDrivenHeatTransfers(
        ForestGraph graph,
        ForestCell source,
        List<ForestCell> regularNeighbors,
        WeatherCondition weather,
        double stepDurationSeconds,
        Dictionary<Guid, double> totalIncomingHeat,
        Dictionary<Guid, List<Guid>> heatSources)
    {
        if (weather.WindSpeedMps < WindJumpMinSpeedMps)
            return;

        if (source.BurningElapsedSeconds < 30.0)
            return;

        double windStrength = CalculateWindJumpStrength(weather.WindSpeedMps);
        if (windStrength <= 0.0)
            return;

        if (regularNeighbors.Count == 0)
            return;

        var regularNeighborIds = regularNeighbors
            .Select(n => n.Id)
            .ToHashSet();

        double windToDirection = (weather.WindDirectionDegrees + 180.0) % 360.0;

        double maxJumpDistance =
            WindJumpBaseDistanceCells +
            WindJumpExtraDistanceCells * windStrength;

        int maxTargets =
            WindJumpBaseTargetsPerSource +
            (int)Math.Round(WindJumpExtraTargetsPerSource * windStrength);

        if (maxTargets <= 0)
            return;

        double localBaselineHeat = GetLocalWindAlignedBaselineHeat(
            graph,
            source,
            regularNeighbors,
            weather,
            stepDurationSeconds,
            windToDirection);

        if (localBaselineHeat <= 0.0)
            return;

        double averageNeighborDistance = GetAverageNeighborDistance(source, regularNeighbors);

        double minJumpDistance = Math.Max(
            2.4,
            averageNeighborDistance * (2.00 + 0.30 * windStrength));

        var candidates = graph.Cells
            .Where(c => c.State == CellState.Normal)
            .Where(c => c.Id != source.Id)
            .Where(c => !regularNeighborIds.Contains(c.Id))
            .Select(c => new
            {
                Cell = c,
                Distance = CalculateDistance(source.X, source.Y, c.X, c.Y),
                Direction = GetDirectionToTarget(source, c)
            })
            .Where(x => x.Distance >= minJumpDistance && x.Distance <= maxJumpDistance)
            .Select(x => new
            {
                x.Cell,
                x.Distance,
                AngleDiff = CalculateAngleDifferenceDeg(x.Direction, windToDirection)
            })
            .Where(x => x.AngleDiff <= WindJumpAngleToleranceDeg)
            .OrderBy(x => x.AngleDiff)
            .ThenByDescending(x => x.Distance)
            .Take(maxTargets)
            .ToList();

        if (candidates.Count == 0)
            return;

        foreach (var candidate in candidates)
        {
            double alignmentFactor = 1.0 - (candidate.AngleDiff / WindJumpAngleToleranceDeg);
            alignmentFactor = Math.Clamp(alignmentFactor, 0.0, 1.0);

            double normalizedDistance =
                (candidate.Distance - minJumpDistance) /
                Math.Max(0.001, maxJumpDistance - minJumpDistance);

            normalizedDistance = Math.Clamp(normalizedDistance, 0.0, 1.0);

            double strengthBoost = 0.55 + 1.35 * windStrength * windStrength;
            double distanceBoost = 0.82 + 0.42 * normalizedDistance;
            double alignmentBoost = 0.58 + 0.42 * alignmentFactor;

            double rawJumpHeat =
                localBaselineHeat *
                (WindJumpBaseHeatMultiplier + WindJumpExtraHeatMultiplier * windStrength) *
                strengthBoost *
                distanceBoost *
                alignmentBoost;

            double maxAllowedJumpHeat =
                localBaselineHeat * (0.42 + 0.95 * windStrength);

            double jumpHeat = Math.Min(rawJumpHeat, maxAllowedJumpHeat);

            if (jumpHeat <= 0.0)
                continue;

            totalIncomingHeat[candidate.Cell.Id] =
                totalIncomingHeat.GetValueOrDefault(candidate.Cell.Id) + jumpHeat;

            if (!heatSources.ContainsKey(candidate.Cell.Id))
                heatSources[candidate.Cell.Id] = new List<Guid>();

            heatSources[candidate.Cell.Id].Add(source.Id);
        }
    }

    private double GetAverageNeighborDistance(ForestCell source, List<ForestCell> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0)
            return 1.0;

        double sum = 0.0;
        int count = 0;

        foreach (var neighbor in neighbors)
        {
            double distance = CalculateDistance(source.X, source.Y, neighbor.X, neighbor.Y);
            if (distance <= 0.0)
                continue;

            sum += distance;
            count++;
        }

        if (count == 0)
            return 1.0;

        return sum / count;
    }

    private double GetLocalWindAlignedBaselineHeat(
        ForestGraph graph,
        ForestCell source,
        List<ForestCell> regularNeighbors,
        WeatherCondition weather,
        double stepDurationSeconds,
        double windToDirection)
    {
        double bestDownwindHeat = 0.0;
        double bestAnyHeat = 0.0;

        foreach (var neighbor in regularNeighbors)
        {
            if (neighbor.State != CellState.Normal)
                continue;

            double heat = _calculator.CalculateHeatFlow(
                source,
                neighbor,
                weather,
                stepDurationSeconds);

            var edge = graph.GetIncidentEdges(source)
                .FirstOrDefault(e => e.FromCellId == neighbor.Id || e.ToCellId == neighbor.Id);

            if (edge != null)
                heat = ApplyEdgeAwareTransferAdjustment(graph, source, neighbor, edge, heat);

            if (heat > bestAnyHeat)
                bestAnyHeat = heat;

            double direction = GetDirectionToTarget(source, neighbor);
            double angleDiff = CalculateAngleDifferenceDeg(direction, windToDirection);

            if (angleDiff <= 65.0 && heat > bestDownwindHeat)
                bestDownwindHeat = heat;
        }

        if (bestDownwindHeat > 0.0)
            return bestDownwindHeat;

        return bestAnyHeat;
    }

    private double CalculateWindJumpStrength(double windSpeedMps)
    {
        if (windSpeedMps < WindJumpMinSpeedMps)
            return 0.0;

        if (windSpeedMps >= WindJumpFullStrengthSpeedMps)
            return 1.0;

        double normalized =
            (windSpeedMps - WindJumpMinSpeedMps) /
            (WindJumpFullStrengthSpeedMps - WindJumpMinSpeedMps);

        return Math.Clamp(normalized * normalized, 0.0, 1.0);
    }

    private void UpdateBurningCells(
        List<ForestCell> burningCells,
        WeatherCondition weather,
        double stepDurationSeconds)
    {
        foreach (var cell in burningCells)
        {
            if (cell.State != CellState.Burning)
                continue;

            _calculator.UpdateBurningCell(cell, weather, stepDurationSeconds);
        }
    }

    private void CoolDownNormalCells(ForestGraph graph, double stepDurationSeconds)
    {
        double retentionFactor = Math.Exp(-stepDurationSeconds / 21600.0);
        retentionFactor = Math.Clamp(retentionFactor, 0.0, 1.0);

        foreach (var normalCell in graph.Cells.Where(c => c.State == CellState.Normal))
        {
            normalCell.CoolDown(retentionFactor);

            if (normalCell.AccumulatedHeatJ <= 0.0)
                normalCell.SetBurnProbability(0.0);
        }
    }


    private int IgniteCandidates(
        List<(ForestCell Cell, double Probability, double AccumulatedHeat, double Threshold, double Ratio)> sortedCandidates,
        int currentStep,
        Guid simulationId,
        List<SimulationEvent> events)
    {
        int newlyIgnited = 0;

        foreach (var candidate in sortedCandidates)
        {
            if (candidate.Cell.State != CellState.Normal)
                continue;

            bool shouldIgnite = ShouldIgniteDeterministically(
                candidate.Cell,
                candidate.Probability,
                candidate.Ratio);

            if (!shouldIgnite)
                continue;

            IgniteFromAccumulatedHeat(candidate.Cell);
            candidate.Cell.SetBurnProbability(candidate.Probability);
            candidate.Cell.ResetAccumulatedHeat();

            newlyIgnited++;

            events.Add(new CellIgnitedEvent
            {
                SimulationId = simulationId,
                Step = currentStep,
                CellId = candidate.Cell.Id,
                X = candidate.Cell.X,
                Y = candidate.Cell.Y,
                IgnitionProbability = candidate.Probability,
                Vegetation = candidate.Cell.Vegetation
            });
        }

        return newlyIgnited;
    }

    private bool ShouldIgniteDeterministically(
      ForestCell cell,
      double probability,
      double ratio)
    {
        if (probability <= 0.0 || ratio <= 0.0)
            return false;

        if (ratio >= 1.0)
            return true;

        if (probability >= 0.995)
            return true;

        if (ratio >= 0.85 && probability >= 0.20)
            return true;

        if (ratio >= 0.70 && probability >= 0.10)
            return true;

        if (ratio >= 0.58 && probability >= 0.16)
            return true;

        if (ratio >= 0.48 && probability >= 0.24)
            return true;

        double ignitionRoll = GetDeterministicRoll(cell.X, cell.Y);

        return probability >= ignitionRoll;
    }
    private double GetDeterministicRoll(int x, int y)
    {
        ulong hash = 1469598103934665603UL;

        Mix(ref hash, x);
        Mix(ref hash, y);
        Mix(ref hash, 104729);

        ulong value = (hash >> 11) & ((1UL << 53) - 1);
        return value / (double)(1UL << 53);
    }

    private void Mix(ref ulong hash, int value)
    {
        unchecked
        {
            uint v = (uint)value;

            hash ^= (byte)(v & 0xFF);
            hash *= 1099511628211UL;

            hash ^= (byte)((v >> 8) & 0xFF);
            hash *= 1099511628211UL;

            hash ^= (byte)((v >> 16) & 0xFF);
            hash *= 1099511628211UL;

            hash ^= (byte)((v >> 24) & 0xFF);
            hash *= 1099511628211UL;
        }
    }

    private void FillResultAfterStep(
        ForestGraph graph,
        SimulationStepResult result,
        double stepDurationSeconds)
    {
        result.BurnedCellsCount = graph.Cells.Count(c => c.State == CellState.Burned);
        result.BurningCellsCount = graph.Cells.Count(c => c.State == CellState.Burning);
        result.TotalCellsAffected = result.BurnedCellsCount + result.BurningCellsCount;
        result.FireArea = result.TotalCellsAffected * 1.0;

        double stepHours = stepDurationSeconds / 3600.0;
        result.SpreadSpeed = stepHours > 0.0
            ? (result.NewlyIgnitedCells * 1.0) / stepHours
            : 0.0;
    }

    public async Task<ForestGraph> InitializeFireAsync(
       ForestGraph graph,
       int initialFireCellsCount,
       WeatherCondition weather,
       List<(int X, int Y)>? fixedPositions = null)
    {
        _logger.LogInformation("🔥 Инициализация пожара: запрошено {Count} очагов", initialFireCellsCount);

        if (initialFireCellsCount <= 0)
            return await Task.FromResult(graph);

        var normalCells = graph.Cells
            .Where(c => c.State == CellState.Normal)
            .Where(c => c.Vegetation != VegetationType.Water && c.Vegetation != VegetationType.Bare)
            .ToList();

        if (normalCells.Count == 0)
            return await Task.FromResult(graph);

        var selectedCells = new List<ForestCell>();

        if (fixedPositions != null && fixedPositions.Any())
        {
            foreach (var pos in fixedPositions)
            {
                var cell = graph.GetCell(pos.X, pos.Y);
                if (cell == null)
                    continue;

                if (cell.State != CellState.Normal)
                    continue;

                if (cell.Vegetation == VegetationType.Water || cell.Vegetation == VegetationType.Bare)
                    continue;

                selectedCells.Add(cell);
            }
        }
        else
        {
            int fireCount = Math.Min(initialFireCellsCount, normalCells.Count);

            var rankedCells = RankInitialIgnitionCandidates(graph, normalCells, weather);
            for (int i = 0; i < fireCount && i < rankedCells.Count; i++)
                selectedCells.Add(rankedCells[i]);
        }

        foreach (var cell in selectedCells)
            IgniteAsInitialSource(cell);

        return await Task.FromResult(graph);
    }
    private List<ForestCell> RankInitialIgnitionCandidates(
     ForestGraph graph,
     List<ForestCell> normalCells,
     WeatherCondition weather)
    {
        if (normalCells.Count == 0)
            return new List<ForestCell>();

        double centerX = graph.Width / 2.0;
        double centerY = graph.Height / 2.0;
        double evaluationStepSeconds = graph.StepDurationSeconds > 0
            ? graph.StepDurationSeconds
            : 900.0;

        var topology = DetectTopology(graph);

        var clusterMap = graph.Cells
            .Where(c => !string.IsNullOrWhiteSpace(c.ClusterId))
            .GroupBy(c => c.ClusterId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        return normalCells
            .Select(cell =>
            {
                var neighbors = graph.GetNeighbors(cell)
                    .Where(n => n.State == CellState.Normal)
                    .ToList();

                int degree = neighbors.Count;

                double dx = cell.X - centerX;
                double dy = cell.Y - centerY;
                double distanceToCenter = Math.Sqrt(dx * dx + dy * dy);
                double centerScore = 1.0 / (1.0 + distanceToCenter);

                double vegetationScore = cell.Vegetation switch
                {
                    VegetationType.Grass => 1.00,
                    VegetationType.Shrub => 0.95,
                    VegetationType.Coniferous => 0.90,
                    VegetationType.Mixed => 0.85,
                    VegetationType.Deciduous => 0.60,
                    VegetationType.Bare => 0.10,
                    VegetationType.Water => 0.00,
                    _ => 0.50
                };

                double moisturePenalty = cell.Moisture;

                double bestNeighborRatio = 0.0;
                double averageNeighborRatio = 0.0;
                double totalNeighborRatio = 0.0;
                double ignitionReadyNeighbors = 0.0;
                double nearNeighborBonus = 0.0;

                int sameVegetationNeighbors = 0;
                int closeNeighbors = 0;
                int veryCloseNeighbors = 0;
                int longOnlyPenalty = 0;

                var virtualSource = CreateVirtualInitialSource(cell);

                foreach (var neighbor in neighbors)
                {
                    double heat = _calculator.CalculateHeatFlow(
                        virtualSource,
                        neighbor,
                        weather,
                        evaluationStepSeconds);

                    var edge = graph.GetIncidentEdges(cell)
                        .FirstOrDefault(e => e.FromCellId == neighbor.Id || e.ToCellId == neighbor.Id);

                    if (edge != null)
                    {
                        heat = ApplyEdgeAwareTransferAdjustment(graph, virtualSource, neighbor, edge, heat);

                        if (edge.Distance <= 2.20)
                            closeNeighbors++;

                        if (edge.Distance <= 1.50)
                            veryCloseNeighbors++;

                        if (edge.Distance > 3.20)
                            longOnlyPenalty++;
                    }

                    if (neighbor.Vegetation == cell.Vegetation)
                        sameVegetationNeighbors++;

                    double threshold = _calculator.CalculateIgnitionThreshold(neighbor, weather);
                    double ratio = threshold > 0.0 ? heat / threshold : 0.0;

                    totalNeighborRatio += ratio;
                    if (ratio > bestNeighborRatio)
                        bestNeighborRatio = ratio;

                    if (ratio >= 0.85)
                        ignitionReadyNeighbors += 1.0;
                    else if (ratio >= 0.60)
                        ignitionReadyNeighbors += 0.5;

                    double distance = CalculateDistance(cell.X, cell.Y, neighbor.X, neighbor.Y);
                    nearNeighborBonus += 1.0 / Math.Max(1.0, distance);
                }

                if (neighbors.Count > 0)
                    averageNeighborRatio = totalNeighborRatio / neighbors.Count;

                double secondRingReach = neighbors
                    .SelectMany(n => graph.GetNeighbors(n))
                    .Where(n => n.State == CellState.Normal && n.Id != cell.Id)
                    .Select(n => n.Id)
                    .Distinct()
                    .Count();

                double regionClusterInteriorBonus = 0.0;
                double regionClusterBoundaryPenalty = 0.0;
                double regionClusterBridgeCorridorBonus = 0.0;
                double clusteredInteriorBonus = 0.0;

                if (topology == SpreadTopology.ClusteredArea)
                {
                    clusteredInteriorBonus =
                        closeNeighbors * 9.0 +
                        veryCloseNeighbors * 8.0 +
                        sameVegetationNeighbors * 4.0 -
                        longOnlyPenalty * 3.5;
                }

                if (!string.IsNullOrWhiteSpace(cell.ClusterId) &&
                    clusterMap.TryGetValue(cell.ClusterId!, out var clusterCells))
                {
                    int interClusterNeighborCount = CountInterClusterNeighbors(graph, cell);
                    int sameClusterNeighborCount = neighbors.Count(n => n.ClusterId == cell.ClusterId);

                    double clusterCenterX = clusterCells.Average(c => c.X);
                    double clusterCenterY = clusterCells.Average(c => c.Y);

                    double distanceToClusterCenter = CalculateDistance(
                        cell.X,
                        cell.Y,
                        clusterCenterX,
                        clusterCenterY);

                    double clusterRadius = Math.Sqrt(clusterCells.Count) * 0.85;
                    double normalizedClusterDistance = clusterRadius > 0.0
                        ? distanceToClusterCenter / clusterRadius
                        : 0.0;

                    normalizedClusterDistance = Math.Clamp(normalizedClusterDistance, 0.0, 2.0);

                    regionClusterInteriorBonus = (1.05 - normalizedClusterDistance) * 4.0;

                    regionClusterBoundaryPenalty =
                        interClusterNeighborCount * 14.0 +
                        Math.Max(0, 2 - sameClusterNeighborCount) * 6.0;
                }

                double score =
                    degree * 2.5 +
                    centerScore * 1.5 +
                    vegetationScore * 1.5 -
                    moisturePenalty * 1.5 +
                    nearNeighborBonus * 2.0 +
                    bestNeighborRatio * 30.0 +
                    averageNeighborRatio * 18.0 +
                    ignitionReadyNeighbors * 20.0 +
                    secondRingReach * 3.0 +
                    clusteredInteriorBonus +
                    regionClusterInteriorBonus +
                    regionClusterBridgeCorridorBonus -
                    regionClusterBoundaryPenalty;

                return new
                {
                    Cell = cell,
                    Score = score,
                    TieBreaker = GetDeterministicSelectionOrder(cell.X, cell.Y)
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TieBreaker)
            .Select(x => x.Cell)
            .ToList();
    }
    private int FindNearestBridgeHopDistance(ForestGraph graph, ForestCell start)
    {
        if (string.IsNullOrWhiteSpace(start.ClusterId))
            return int.MaxValue;

        var visited = new HashSet<Guid> { start.Id };
        var queue = new Queue<(ForestCell Cell, int Depth)>();
        queue.Enqueue((start, 0));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (IsBridgeSupportNode(graph, current.Cell))
                return current.Depth;

            foreach (var neighbor in graph.GetNeighbors(current.Cell))
            {
                if (neighbor.State != CellState.Normal)
                    continue;

                if (neighbor.ClusterId != start.ClusterId)
                    continue;

                if (!visited.Add(neighbor.Id))
                    continue;

                queue.Enqueue((neighbor, current.Depth + 1));
            }
        }

        return int.MaxValue;
    }
    private bool IsBridgeSupportNode(ForestGraph graph, ForestCell cell)
    {
        if (string.IsNullOrWhiteSpace(cell.ClusterId))
            return false;

        int sameClusterNeighbors = 0;
        int interClusterNeighbors = 0;

        foreach (var neighbor in graph.GetNeighbors(cell))
        {
            if (neighbor.ClusterId == cell.ClusterId)
                sameClusterNeighbors++;
            else
                interClusterNeighbors++;
        }

        return interClusterNeighbors > 0 && sameClusterNeighbors >= 2;
    }
    private ForestCell CreateVirtualInitialSource(ForestCell original)
    {
        var source = new ForestCell(
            original.X,
            original.Y,
            original.Vegetation,
            original.Moisture,
            original.Elevation);

        source.Ignite(DateTime.UtcNow);
        source.SetBurningElapsedSeconds(GetInitialEstablishedBurnSeconds(original));
        source.ResetAccumulatedHeat();

        return source;
    }

    private double GetDeterministicSelectionOrder(int x, int y)
    {
        return GetDeterministicRoll(x, y);
    }

    private void IgniteAsInitialSource(ForestCell cell)
    {
        cell.Ignite(DateTime.UtcNow);

        double initialEstablishedSeconds = GetInitialEstablishedBurnSeconds(cell);
        cell.SetBurningElapsedSeconds(initialEstablishedSeconds);
        cell.ResetAccumulatedHeat();
    }

    private double GetInitialEstablishedBurnSeconds(ForestCell cell)
    {
        double totalBurnDuration = FireModelCatalog.Get(cell.Vegetation).BaseBurnDurationSeconds;

        if (double.IsInfinity(totalBurnDuration) || totalBurnDuration <= 0.0)
            return 0.0;

        double establishedFraction = cell.Vegetation switch
        {
            VegetationType.Grass => 0.020,
            VegetationType.Shrub => 0.025,
            VegetationType.Coniferous => 0.030,
            VegetationType.Mixed => 0.030,
            VegetationType.Deciduous => 0.025,
            _ => 0.025
        };

        double established = totalBurnDuration * establishedFraction;

        double minSeconds = cell.Vegetation switch
        {
            VegetationType.Grass => 15.0,
            VegetationType.Shrub => 25.0,
            VegetationType.Coniferous => 45.0,
            VegetationType.Mixed => 45.0,
            VegetationType.Deciduous => 30.0,
            _ => 20.0
        };

        double maxAllowed = totalBurnDuration * 0.06;
        established = Math.Clamp(established, minSeconds, maxAllowed);

        return Math.Max(0.0, established);
    }

    private void IgniteFromAccumulatedHeat(ForestCell cell)
    {
        cell.Ignite(DateTime.UtcNow);

        double establishedSeconds = GetSecondaryIgnitionEstablishedBurnSeconds(cell);
        cell.SetBurningElapsedSeconds(establishedSeconds);

        cell.ResetAccumulatedHeat();
    }

    private double GetSecondaryIgnitionEstablishedBurnSeconds(ForestCell cell)
    {
        double totalBurnDuration = FireModelCatalog.Get(cell.Vegetation).BaseBurnDurationSeconds;

        if (double.IsInfinity(totalBurnDuration) || totalBurnDuration <= 0.0)
            return 0.0;

        double establishedFraction = cell.Vegetation switch
        {
            VegetationType.Grass => 0.006,
            VegetationType.Shrub => 0.008,
            VegetationType.Coniferous => 0.010,
            VegetationType.Mixed => 0.010,
            VegetationType.Deciduous => 0.008,
            _ => 0.008
        };

        double established = totalBurnDuration * establishedFraction;

        double minSeconds = cell.Vegetation switch
        {
            VegetationType.Grass => 8.0,
            VegetationType.Shrub => 12.0,
            VegetationType.Coniferous => 20.0,
            VegetationType.Mixed => 20.0,
            VegetationType.Deciduous => 15.0,
            _ => 10.0
        };

        double maxAllowed = totalBurnDuration * 0.02;
        established = Math.Clamp(established, minSeconds, maxAllowed);

        return Math.Max(0.0, established);
    }

    private double CalculateDistance(int x1, int y1, int x2, int y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private double CalculateDistance(double x1, double y1, double x2, double y2)
    {
        double dx = x1 - x2;
        double dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private double GetDirectionToTarget(ForestCell source, ForestCell target)
    {
        double dx = target.X - source.X;
        double dy = target.Y - source.Y;

        double angleRad = Math.Atan2(dy, dx);
        double angleDeg = angleRad * 180.0 / Math.PI;
        if (angleDeg < 0.0)
            angleDeg += 360.0;

        double correctedAngle = (90.0 + angleDeg) % 360.0;
        if (correctedAngle < 0.0)
            correctedAngle += 360.0;

        return correctedAngle;
    }

    private double CalculateAngleDifferenceDeg(double a, double b)
    {
        double diff = Math.Abs(a - b);
        return Math.Min(diff, 360.0 - diff);
    }

    private enum SpreadTopology
    {
        Grid,
        ClusteredArea
    }
}