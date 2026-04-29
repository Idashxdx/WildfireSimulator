using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using WildfireSimulator.Application.Interfaces;
using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Infrastructure.Services;

public class ForestGraphGenerator : IForestGraphGenerator
{
    private readonly ILogger<ForestGraphGenerator> _logger;

    public ForestGraphGenerator(ILogger<ForestGraphGenerator> logger)
    {
        _logger = logger;
    }

    private sealed class TerritoryDraft
    {
        public int Width { get; }
        public int Height { get; }
        public VegetationType[,] VegetationMap { get; }
        public double[,] MoistureMap { get; }
        public double[,] ElevationMap { get; }

        public TerritoryDraft(int width, int height)
        {
            Width = width;
            Height = height;
            VegetationMap = new VegetationType[width, height];
            MoistureMap = new double[width, height];
            ElevationMap = new double[width, height];
        }
    }

    private sealed class ClusteredPatch
    {
        public int Index { get; init; }
        public double CenterX { get; init; }
        public double CenterY { get; init; }
        public double RadiusX { get; init; }
        public double RadiusY { get; init; }
        public VegetationType DominantVegetation { get; init; }
        public double BaseMoisture { get; init; }
        public double BaseElevation { get; init; }
        public string Tag { get; init; } = string.Empty;
    }

    private sealed class ClusteredScaleProfile
    {
        public GraphScaleType Scale { get; init; }
        public int PatchCount { get; init; }
        public int LocalTargetDegree { get; init; }
        public int MaxDegree { get; init; }
        public double CloseRadius { get; init; }
        public double SupportRadius { get; init; }
        public double ExtendedRadius { get; init; }
        public int ExtendedEdgeBudget { get; init; }
        public double PlacementScale { get; init; }
        public int PreferredBridgeCount { get; init; }
        public int CorridorBudget { get; init; }
    }

    public async Task<ForestGraph> GenerateGridAsync(int width, int height, SimulationParameters parameters)
    {
        _logger.LogInformation(
            "Генерация сетки {Width}x{Height}. Режим карты: {Mode}, сценарий: {Scenario}, полуручных объектов: {ObjectsCount}",
            width,
            height,
            parameters.MapCreationMode,
            parameters.ScenarioType,
            parameters.MapRegionObjects?.Count ?? 0);

        var random = CreateRandom(parameters);

        var graph = new ForestGraph
        {
            Width = width,
            Height = height,
            StepDurationSeconds = parameters.StepDurationSeconds
        };

        var territoryDraft = BuildTerritoryDraft(width, height, parameters, random);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cell = new ForestCell(
                    x,
                    y,
                    territoryDraft.VegetationMap[x, y],
                    territoryDraft.MoistureMap[x, y],
                    territoryDraft.ElevationMap[x, y]);

                graph.Cells.Add(cell);
            }
        }

        CreateEdgesForGrid(graph, width, height);
        ApplySurfaceBarrierEdgeModifiers(graph);

        _logger.LogInformation(
            "Сгенерирована сетка: {Cells} клеток, {Edges} рёбер, режим={Mode}",
            graph.Cells.Count,
            graph.Edges.Count,
            parameters.MapCreationMode);

        return await Task.FromResult(graph);
    }

    public async Task<ForestGraph> GenerateClusteredGraphAsync(int nodeCount, SimulationParameters parameters)
    {
        var random = CreateRandom(parameters);
        var scaleProfile = GetClusteredScaleProfile(parameters, nodeCount);

        int effectiveWidth = scaleProfile.Scale switch
        {
            GraphScaleType.Small => 24,
            GraphScaleType.Medium => 44,
            GraphScaleType.Large => 64,
            _ => 44
        };

        int effectiveHeight = scaleProfile.Scale switch
        {
            GraphScaleType.Small => 18,
            GraphScaleType.Medium => 34,
            GraphScaleType.Large => 46,
            _ => 34
        };

        var graph = new ForestGraph
        {
            Width = effectiveWidth,
            Height = effectiveHeight,
            StepDurationSeconds = parameters.StepDurationSeconds
        };

        _logger.LogInformation(
            "Генерация графа. Масштаб={Scale}, Узлов={NodeCount}, Размер={Width}x{Height}, Режим={Mode}, Сценарий={Scenario}, ЕстьРедактор={HasBlueprint}",
            scaleProfile.Scale,
            nodeCount,
            graph.Width,
            graph.Height,
            parameters.MapCreationMode,
            parameters.ClusteredScenarioType,
            parameters.ClusteredBlueprint != null && parameters.ClusteredBlueprint.Nodes.Any());

        if (parameters.ClusteredBlueprint != null &&
            parameters.ClusteredBlueprint.Nodes.Any())
        {
            var blueprintGraph = BuildClusteredGraphFromBlueprint(parameters.ClusteredBlueprint, parameters);
            blueprintGraph.StepDurationSeconds = parameters.StepDurationSeconds;

            _logger.LogInformation(
                "Граф построен из редактора: Масштаб={Scale}, Узлов={Cells}, Рёбер={Edges}",
                scaleProfile.Scale,
                blueprintGraph.Cells.Count,
                blueprintGraph.Edges.Count);

            return await Task.FromResult(blueprintGraph);
        }

        bool canUseDemoScenario =
            scaleProfile.Scale == GraphScaleType.Large &&
            (parameters.MapCreationMode == MapCreationMode.Scenario ||
             parameters.ClusteredScenarioType.HasValue);

        if (canUseDemoScenario)
        {
            BuildClusteredScenarioGraph(
                graph,
                nodeCount,
                parameters,
                random,
                scaleProfile.MaxDegree);

            graph.StepDurationSeconds = parameters.StepDurationSeconds;

            _logger.LogInformation(
                "Демо-граф сгенерирован: Масштаб={Scale}, Узлов={Cells}, Рёбер={Edges}",
                scaleProfile.Scale,
                graph.Cells.Count,
                graph.Edges.Count);

            return await Task.FromResult(graph);
        }

        BuildClusteredRandomGraph(
            graph,
            nodeCount,
            parameters,
            random,
            scaleProfile.MaxDegree);

        graph.StepDurationSeconds = parameters.StepDurationSeconds;

        _logger.LogInformation(
            "Случайный граф сгенерирован: Масштаб={Scale}, Узлов={Cells}, Рёбер={Edges}",
            scaleProfile.Scale,
            graph.Cells.Count,
            graph.Edges.Count);

        return await Task.FromResult(graph);
    }
    private TerritoryDraft BuildTerritoryDraft(
        int width,
        int height,
        SimulationParameters parameters,
        Random random)
    {
        var draft = new TerritoryDraft(width, height);
        FillTerritoryDraft(draft, parameters, random);
        return draft;
    }

    private void FillTerritoryDraft(
        TerritoryDraft draft,
        SimulationParameters parameters,
        Random random)
    {
        switch (parameters.MapCreationMode)
        {
            case MapCreationMode.Scenario:
                BuildScenarioGridMaps(
                    draft.Width,
                    draft.Height,
                    parameters,
                    random,
                    draft.VegetationMap,
                    draft.MoistureMap,
                    draft.ElevationMap);
                break;

            case MapCreationMode.SemiManual:
                BuildSemiManualGridMaps(
                    draft.Width,
                    draft.Height,
                    parameters,
                    random,
                    draft.VegetationMap,
                    draft.MoistureMap,
                    draft.ElevationMap);
                break;

            default:
                BuildRandomGridMaps(
                    draft.Width,
                    draft.Height,
                    parameters,
                    random,
                    draft.VegetationMap,
                    draft.MoistureMap,
                    draft.ElevationMap);
                break;
        }
    }

    private void BuildRandomGridMaps(
      int width,
      int height,
      SimulationParameters parameters,
      Random random,
      VegetationType[,] vegetationMap,
      double[,] moistureMap,
      double[,] elevationMap)
    {
        var generatedVegetation = BuildGridVegetationMap(width, height, parameters, random);
        var (effectiveMoistureMin, effectiveMoistureMax) = GetEffectiveMoistureRange(parameters);
        double moistureCenter = (effectiveMoistureMin + effectiveMoistureMax) / 2.0;
        double moistureSpread = Math.Max(0.04, (effectiveMoistureMax - effectiveMoistureMin) * 0.42);
        double effectiveElevationVariation = GetEffectiveElevationVariation(parameters.ElevationVariation, parameters);

        var baseElevationMap = GenerateSmoothField(
            width,
            height,
            random,
            centerValue: 0.0,
            amplitude: Math.Max(4.0, effectiveElevationVariation * 0.22),
            coarseDivisor: 5.5);

        var baseMoistureMap = GenerateSmoothField(
            width,
            height,
            random,
            centerValue: moistureCenter,
            amplitude: Math.Max(0.03, moistureSpread),
            coarseDivisor: 5.0);

        AddDirectionalElevationGradient(
            width,
            height,
            baseElevationMap,
            random,
            Math.Max(3.0, effectiveElevationVariation * 0.12));

        AddDirectionalMoistureGradient(
            width,
            height,
            baseMoistureMap,
            random,
            Math.Max(0.02, (effectiveMoistureMax - effectiveMoistureMin) * 0.16));

        SmoothDoubleMap(width, height, baseElevationMap, iterations: 2, preserveWater: false, vegetationMap: null);
        SmoothDoubleMap(width, height, baseMoistureMap, iterations: 2, preserveWater: false, vegetationMap: null);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                vegetationMap[x, y] = generatedVegetation[x, y];
                elevationMap[x, y] = baseElevationMap[x, y];

                double vegetationBias = GetVegetationMoistureBias(generatedVegetation[x, y]);
                moistureMap[x, y] = ClampMoisture(baseMoistureMap[x, y] + vegetationBias, parameters);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (vegetationMap[x, y] == VegetationType.Water)
                    elevationMap[x, y] -= 10;

                if (vegetationMap[x, y] == VegetationType.Grass)
                    elevationMap[x, y] += 2;

                if (vegetationMap[x, y] == VegetationType.Coniferous)
                    elevationMap[x, y] += 5;
            }
        }

        HarmonizeTerrainAndMoisture(width, height, vegetationMap, moistureMap, elevationMap, parameters);

        ApplyWaterAdjacencyEffects(width, height, vegetationMap, moistureMap, elevationMap, parameters);
        ApplyTerrainNoise(width, height, vegetationMap, moistureMap, elevationMap, parameters, random);

        HarmonizeTerrainAndMoisture(width, height, vegetationMap, moistureMap, elevationMap, parameters);
        SmoothMaps(width, height, vegetationMap, moistureMap, elevationMap, parameters, elevationIterations: 2, moistureIterations: 2);

        NormalizeElevation(width, height, elevationMap, targetMin: -20.0, targetMax: 70.0);
    }

    private void BuildScenarioGridMaps(
     int width,
     int height,
     SimulationParameters parameters,
     Random random,
     VegetationType[,] vegetationMap,
     double[,] moistureMap,
     double[,] elevationMap)
    {
        var scenario = parameters.ScenarioType ?? MapScenarioType.MixedForest;

        switch (scenario)
        {
            case MapScenarioType.DryConiferousMassif:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: Math.Max(0.10, parameters.InitialMoistureMin + 0.03),
                    moistureSpread: 0.04,
                    elevationBaseFactor: 0.16,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Coniferous, 0.76),
                        (VegetationType.Mixed, 0.14),
                        (VegetationType.Shrub, 0.06),
                        (VegetationType.Grass, 0.03),
                        (VegetationType.Deciduous, 0.01)));

                AddDryPatches(width, height, moistureMap, random, intensity: 0.08, patchCount: 4);
                AddOrientedHillFeature(width, height, elevationMap, parameters, random, width * 0.26, height * 0.34, 0.85, 0.22, 0.12);
                AddOrientedHillFeature(width, height, elevationMap, parameters, random, width * 0.63, height * 0.56, 0.70, 0.18, 0.10);
                break;

            case MapScenarioType.ForestWithRiver:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0 + 0.04,
                    moistureSpread: 0.07,
                    elevationBaseFactor: 0.12,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.38),
                        (VegetationType.Deciduous, 0.32),
                        (VegetationType.Coniferous, 0.15),
                        (VegetationType.Shrub, 0.09),
                        (VegetationType.Grass, 0.06)));

                AddWetPatches(width, height, moistureMap, random, intensity: 0.05, patchCount: 2);
                PaintRiver(width, height, vegetationMap, moistureMap, elevationMap, random, parameters);
                break;

            case MapScenarioType.ForestWithLake:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0 + 0.05,
                    moistureSpread: 0.08,
                    elevationBaseFactor: 0.10,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.36),
                        (VegetationType.Deciduous, 0.30),
                        (VegetationType.Coniferous, 0.16),
                        (VegetationType.Shrub, 0.10),
                        (VegetationType.Grass, 0.08)));

                AddWetPatches(width, height, moistureMap, random, intensity: 0.05, patchCount: 2);
                PaintLake(width, height, vegetationMap, moistureMap, elevationMap, random, parameters);
                break;

            case MapScenarioType.ForestWithFirebreak:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0,
                    moistureSpread: 0.07,
                    elevationBaseFactor: 0.09,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.40),
                        (VegetationType.Coniferous, 0.24),
                        (VegetationType.Deciduous, 0.18),
                        (VegetationType.Shrub, 0.10),
                        (VegetationType.Grass, 0.08)));

                AddDryPatches(width, height, moistureMap, random, intensity: 0.04, patchCount: 2);
                PaintFirebreak(width, height, vegetationMap, moistureMap, random, parameters);
                break;

            case MapScenarioType.HillyTerrain:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0,
                    moistureSpread: 0.08,
                    elevationBaseFactor: 0.18,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.34),
                        (VegetationType.Coniferous, 0.24),
                        (VegetationType.Deciduous, 0.18),
                        (VegetationType.Shrub, 0.14),
                        (VegetationType.Grass, 0.10)));

                AddRidgeSystem(width, height, elevationMap, parameters, random);
                AddRidgeSystem(width, height, elevationMap, parameters, random);
                AddValleySystem(width, height, elevationMap, parameters, random);

                AddOrientedHillFeature(
                    width, height, elevationMap, parameters, random,
                    width * 0.30, height * 0.35,
                    strength: 1.15,
                    radiusXFactor: 0.20,
                    radiusYFactor: 0.09);

                AddOrientedHillFeature(
                    width, height, elevationMap, parameters, random,
                    width * 0.68, height * 0.62,
                    strength: 1.00,
                    radiusXFactor: 0.24,
                    radiusYFactor: 0.10);

                AddOrientedValleyFeature(
                    width, height, elevationMap, parameters, random,
                    width * 0.50, height * 0.56,
                    strength: 0.95,
                    radiusXFactor: 0.22,
                    radiusYFactor: 0.08);

                AddWetPatches(width, height, moistureMap, random, intensity: 0.05, patchCount: 2);
                break;

            case MapScenarioType.WetForestAfterRain:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: Math.Min(0.92, parameters.InitialMoistureMax + 0.12),
                    moistureSpread: 0.04,
                    elevationBaseFactor: 0.08,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.38),
                        (VegetationType.Deciduous, 0.32),
                        (VegetationType.Coniferous, 0.10),
                        (VegetationType.Shrub, 0.12),
                        (VegetationType.Grass, 0.08)));

                AddWetPatches(width, height, moistureMap, random, intensity: 0.14, patchCount: 6);
                break;

            case MapScenarioType.MixedForest:
            default:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0,
                    moistureSpread: 0.08,
                    elevationBaseFactor: 0.10,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.40),
                        (VegetationType.Deciduous, 0.24),
                        (VegetationType.Coniferous, 0.18),
                        (VegetationType.Shrub, 0.10),
                        (VegetationType.Grass, 0.08)));

                AddWetPatches(width, height, moistureMap, random, intensity: 0.04, patchCount: 2);
                AddDryPatches(width, height, moistureMap, random, intensity: 0.03, patchCount: 2);
                AddOrientedHillFeature(width, height, elevationMap, parameters, random, width * 0.34, height * 0.42, 0.34, 0.16, 0.10);
                break;
        }

        HarmonizeTerrainAndMoisture(width, height, vegetationMap, moistureMap, elevationMap, parameters);

        ApplyWaterAdjacencyEffects(width, height, vegetationMap, moistureMap, elevationMap, parameters);
        ApplyTerrainNoise(width, height, vegetationMap, moistureMap, elevationMap, parameters, random);

        HarmonizeTerrainAndMoisture(width, height, vegetationMap, moistureMap, elevationMap, parameters);

        int elevationSmoothIterations = scenario == MapScenarioType.HillyTerrain ? 2 : 3;
        int moistureSmoothIterations = 2;

        SmoothMaps(
            width,
            height,
            vegetationMap,
            moistureMap,
            elevationMap,
            parameters,
            elevationIterations: elevationSmoothIterations,
            moistureIterations: moistureSmoothIterations);

        RebalanceVegetationByTerrain(width, height, vegetationMap, moistureMap, elevationMap, parameters, random);
        ClampMaps(width, height, vegetationMap, moistureMap, elevationMap, parameters);

        if (scenario == MapScenarioType.HillyTerrain)
        {
            NormalizeElevation(width, height, elevationMap, targetMin: -40.0, targetMax: 120.0);
            ApplyElevationContrast(width, height, elevationMap, power: 1.6);
            NormalizeElevation(width, height, elevationMap, targetMin: -40.0, targetMax: 120.0);
        }
        else
        {
            NormalizeElevation(width, height, elevationMap, targetMin: -20.0, targetMax: 70.0);
        }
    }
    private void ApplyElevationContrast(
    int width,
    int height,
    double[,] elevationMap,
    double power)
    {
        power = Math.Clamp(power, 1.0, 2.0);

        double center = 0.0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                center += elevationMap[x, y];
        }

        center /= Math.Max(1, width * height);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double value = elevationMap[x, y] - center;
                double sign = Math.Sign(value);
                double normalized = Math.Pow(Math.Abs(value), power);

                elevationMap[x, y] = center + sign * normalized;
            }
        }
    }

    private void BuildSemiManualGridMaps(
      int width,
      int height,
      SimulationParameters parameters,
      Random random,
      VegetationType[,] vegetationMap,
      double[,] moistureMap,
      double[,] elevationMap)
    {
        InitializeBaseLandscape(
            width, height, parameters, random,
            vegetationMap, moistureMap, elevationMap,
            moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0,
            moistureSpread: 0.08,
            elevationBaseFactor: 0.08,
            vegetationPicker: r => GetRandomCombustibleVegetation(parameters.VegetationDistributions, r, parameters));

        var orderedObjects = (parameters.MapRegionObjects ?? new List<MapRegionObject>())
            .OrderBy(o => o.Priority)
            .ToList();

        foreach (var mapObject in orderedObjects)
            ApplyMapObject(width, height, parameters, vegetationMap, moistureMap, elevationMap, mapObject);

        HarmonizeTerrainAndMoisture(width, height, vegetationMap, moistureMap, elevationMap, parameters);

        ApplyWaterAdjacencyEffects(width, height, vegetationMap, moistureMap, elevationMap, parameters);
        ApplyTerrainNoise(width, height, vegetationMap, moistureMap, elevationMap, parameters, random);

        SmoothMaps(width, height, vegetationMap, moistureMap, elevationMap, parameters, elevationIterations: 2, moistureIterations: 1);
        ClampMaps(width, height, vegetationMap, moistureMap, elevationMap, parameters);
    }

    private void InitializeBaseLandscape(
    int width,
    int height,
    SimulationParameters parameters,
    Random random,
    VegetationType[,] vegetationMap,
    double[,] moistureMap,
    double[,] elevationMap,
    double moistureCenter,
    double moistureSpread,
    double elevationBaseFactor,
    Func<Random, VegetationType> vegetationPicker)
    {
        double effectiveElevationVariation = GetEffectiveElevationVariation(parameters.ElevationVariation, parameters);
        double elevationAmplitude = Math.Max(3.0, effectiveElevationVariation * elevationBaseFactor);

        var baseElevationMap = GenerateSmoothField(
            width,
            height,
            random,
            centerValue: 0.0,
            amplitude: elevationAmplitude,
            coarseDivisor: 5.2);

        var baseMoistureMap = GenerateSmoothField(
            width,
            height,
            random,
            centerValue: moistureCenter,
            amplitude: Math.Max(0.025, moistureSpread),
            coarseDivisor: 5.0);

        AddDirectionalElevationGradient(
            width,
            height,
            baseElevationMap,
            random,
            Math.Max(2.0, elevationAmplitude * 0.55));

        AddDirectionalMoistureGradient(
            width,
            height,
            baseMoistureMap,
            random,
            Math.Max(0.015, moistureSpread * 0.55));

        SmoothDoubleMap(width, height, baseElevationMap, iterations: 2, preserveWater: false, vegetationMap: null);
        SmoothDoubleMap(width, height, baseMoistureMap, iterations: 2, preserveWater: false, vegetationMap: null);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var vegetation = vegetationPicker(random);
                vegetationMap[x, y] = vegetation;

                elevationMap[x, y] = baseElevationMap[x, y];
                moistureMap[x, y] = ClampMoisture(
                    baseMoistureMap[x, y] + GetVegetationMoistureBias(vegetation),
                    parameters);
            }
        }

        HarmonizeTerrainAndMoisture(width, height, vegetationMap, moistureMap, elevationMap, parameters);
    }


    private void BuildClusteredRandomGraph(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     int maxDegree)
    {
        var profile = GetClusteredScaleProfile(parameters, nodeCount);

        int effectiveNodeCount = profile.Scale switch
        {
            GraphScaleType.Small => Math.Clamp(nodeCount, 12, 24),
            GraphScaleType.Medium => Math.Clamp(nodeCount, 45, 80),
            GraphScaleType.Large => Math.Clamp(nodeCount, 90, 160),
            _ => Math.Clamp(nodeCount, 45, 80)
        };

        if (profile.Scale == GraphScaleType.Small)
        {
            BuildSmallRandomGraph(graph, effectiveNodeCount, parameters, random, profile);
            return;
        }

        BuildAreaBasedRandomGraph(graph, effectiveNodeCount, parameters, random, profile);
    }
    private void BuildSmallRandomGraph(
        ForestGraph graph,
        int nodeCount,
        SimulationParameters parameters,
        Random random,
        ClusteredScaleProfile profile)
    {
        var used = new HashSet<(int X, int Y)>();

        for (int i = 0; i < nodeCount; i++)
        {
            var position = GetFreeRandomPosition(
                graph.Width,
                graph.Height,
                used,
                random,
                margin: 2);

            var vegetation = GetRandomCombustibleVegetation(
                parameters.VegetationDistributions,
                random,
                parameters);

            double moisture = GetRandomGraphMoisture(vegetation, parameters, random, 0.06);
            double elevation = GetRandomElevation(parameters.ElevationVariation, random, parameters) * 0.35;

            var cell = new ForestCell(
                position.X,
                position.Y,
                vegetation,
                moisture,
                elevation,
                "область-1");

            graph.Cells.Add(cell);
        }

        ConnectGraphLocally(graph, graph.Cells, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree, random);
        EnsureGraphConnectedInsideGroup(graph, graph.Cells, profile.MaxDegree);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
        ApplyRandomGraphEdgeModifiers(graph);
    }
    private void BuildAreaBasedRandomGraph(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     ClusteredScaleProfile profile)
    {
        var patches = CreateSimpleRandomGraphPatches(
            graph.Width,
            graph.Height,
            parameters,
            random,
            profile);

        var surfaceZones = CreateClusteredSurfaceZones(
            graph.Width,
            graph.Height,
            patches,
            profile,
            random,
            parameters);

        var used = new HashSet<(int X, int Y)>();
        var groups = patches.ToDictionary(p => p.Index, _ => new List<ForestCell>());

        var patchOrder = BuildPatchDistributionOrder(
            patches,
            nodeCount,
            profile,
            random);

        foreach (var patch in patchOrder)
        {
            var position = GetFreePositionNearPatch(
                graph.Width,
                graph.Height,
                patch,
                used,
                random);

            double dominantChance = profile.Scale == GraphScaleType.Large
                ? 0.76
                : 0.64;

            var vegetation = random.NextDouble() < dominantChance
                ? patch.DominantVegetation
                : GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters);

            double moistureSpread = profile.Scale == GraphScaleType.Large ? 0.12 : 0.08;

            double moisture = GetRandomGraphMoisture(
                vegetation,
                parameters,
                random,
                moistureSpread);

            moisture = Math.Clamp((moisture * 0.38) + (patch.BaseMoisture * 0.62), 0.02, 0.98);

            double elevationSpread = profile.Scale == GraphScaleType.Large ? 0.24 : 0.12;

            double elevation = patch.BaseElevation +
                GetRandomElevation(parameters.ElevationVariation, random, parameters) * elevationSpread;

            var zone = ResolveClusteredSurfaceZone(position.X, position.Y, surfaceZones);
            if (zone != null)
            {
                vegetation = zone.Value.Type;

                if (vegetation == VegetationType.Water)
                {
                    moisture = 1.0;
                    elevation -= 7.0;
                }
                else if (vegetation == VegetationType.Bare)
                {
                    moisture = Math.Clamp(moisture * 0.42, 0.02, 0.24);
                    elevation -= 1.5;
                }
            }

            var cell = new ForestCell(
                position.X,
                position.Y,
                vegetation,
                moisture,
                elevation,
                $"область-{patch.Index + 1}");

            graph.Cells.Add(cell);
            groups[patch.Index].Add(cell);
        }

        foreach (var group in groups.Values)
        {
            ConnectGraphLocally(graph, group, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree, random);
            EnsureGraphConnectedInsideGroup(graph, group, profile.MaxDegree);
        }

        ConnectRandomGraphAreas(
            graph,
            groups.Values.ToList(),
            profile,
            random);

        AddLimitedSupportEdgesBetweenCloseAreas(
            graph,
            groups.Values.ToList(),
            profile);

        EnsureNoIsolatedNodes(graph, profile.MaxDegree);

        ApplyRandomGraphEdgeModifiers(graph);
        ApplySurfaceBarrierEdgeModifiers(graph);
    }
    private List<ClusteredPatch> BuildPatchDistributionOrder(
        List<ClusteredPatch> patches,
        int nodeCount,
        ClusteredScaleProfile profile,
        Random random)
    {
        var result = new List<ClusteredPatch>();

        if (patches.Count == 0 || nodeCount <= 0)
            return result;

        if (profile.Scale != GraphScaleType.Large)
        {
            for (int i = 0; i < nodeCount; i++)
                result.Add(patches[i % patches.Count]);

            return result;
        }

        double[] weights =
        {
        1.10,
        0.82,
        1.28,
        0.95,
        1.45,
        0.76,
        1.18
    };

        var quotas = new Dictionary<int, int>();
        double totalWeight = 0.0;

        for (int i = 0; i < patches.Count; i++)
            totalWeight += weights[i % weights.Length];

        int assigned = 0;

        for (int i = 0; i < patches.Count; i++)
        {
            int quota = Math.Max(6, (int)Math.Round(nodeCount * weights[i % weights.Length] / totalWeight));
            quotas[patches[i].Index] = quota;
            assigned += quota;
        }

        while (assigned > nodeCount)
        {
            var largest = quotas
                .OrderByDescending(q => q.Value)
                .First();

            if (largest.Value <= 6)
                break;

            quotas[largest.Key]--;
            assigned--;
        }

        while (assigned < nodeCount)
        {
            var patch = patches[random.Next(patches.Count)];
            quotas[patch.Index]++;
            assigned++;
        }

        foreach (var patch in patches)
        {
            int quota = quotas.GetValueOrDefault(patch.Index);

            for (int i = 0; i < quota; i++)
                result.Add(patch);
        }

        return result
            .OrderBy(_ => random.Next())
            .ToList();
    }
    private List<(VegetationType Type, double CenterX, double CenterY, double RadiusX, double RadiusY)> CreateClusteredSurfaceZones(
      int width,
      int height,
      List<ClusteredPatch> patches,
      ClusteredScaleProfile profile,
      Random random,
      SimulationParameters parameters)
    {
        var zones = new List<(VegetationType Type, double CenterX, double CenterY, double RadiusX, double RadiusY)>();

        if (patches.Count == 0)
            return zones;

        double waterProbability = GetVegetationProbability(parameters.VegetationDistributions, VegetationType.Water);
        double bareProbability = GetVegetationProbability(parameters.VegetationDistributions, VegetationType.Bare);

        int waterZones = waterProbability <= 0.000001
            ? 0
            : profile.Scale == GraphScaleType.Large ? 2 : 1;

        int bareZones = bareProbability <= 0.000001
            ? 0
            : profile.Scale == GraphScaleType.Large ? 2 : 1;

        var orderedPatches = patches
            .OrderBy(_ => random.Next())
            .ToList();

        for (int i = 0; i < waterZones && i < orderedPatches.Count; i++)
        {
            var patch = orderedPatches[i];

            zones.Add((
                VegetationType.Water,
                patch.CenterX + (random.NextDouble() * 2.0 - 1.0) * patch.RadiusX * 0.35,
                patch.CenterY + (random.NextDouble() * 2.0 - 1.0) * patch.RadiusY * 0.35,
                Math.Max(2.6, patch.RadiusX * 0.52),
                Math.Max(2.2, patch.RadiusY * 0.46)
            ));
        }

        for (int i = 0; i < bareZones && i < orderedPatches.Count; i++)
        {
            var patch = orderedPatches[(i + waterZones) % orderedPatches.Count];

            zones.Add((
                VegetationType.Bare,
                patch.CenterX + (random.NextDouble() * 2.0 - 1.0) * patch.RadiusX * 0.45,
                patch.CenterY + (random.NextDouble() * 2.0 - 1.0) * patch.RadiusY * 0.45,
                Math.Max(2.4, patch.RadiusX * 0.42),
                Math.Max(2.0, patch.RadiusY * 0.36)
            ));
        }

        return zones
            .Select(z => (
                z.Type,
                Math.Clamp(z.CenterX, 2.0, width - 3.0),
                Math.Clamp(z.CenterY, 2.0, height - 3.0),
                z.RadiusX,
                z.RadiusY))
            .ToList();
    }
    private (VegetationType Type, double CenterX, double CenterY, double RadiusX, double RadiusY)? ResolveClusteredSurfaceZone(
        int x,
        int y,
        List<(VegetationType Type, double CenterX, double CenterY, double RadiusX, double RadiusY)> zones)
    {
        foreach (var zone in zones)
        {
            double dx = (x - zone.CenterX) / Math.Max(0.1, zone.RadiusX);
            double dy = (y - zone.CenterY) / Math.Max(0.1, zone.RadiusY);

            if (dx * dx + dy * dy <= 1.0)
                return zone;
        }

        return null;
    }
    private void ConnectRandomGraphAreas(
       ForestGraph graph,
       List<List<ForestCell>> groups,
       ClusteredScaleProfile profile,
       Random random)
    {
        var nonEmptyGroups = groups
            .Where(g => g.Count > 0)
            .ToList();

        if (nonEmptyGroups.Count <= 1)
            return;

        if (profile.Scale == GraphScaleType.Medium)
        {
            var mediumPairs = BuildNeighborAreaPairs(nonEmptyGroups, profile);

            foreach (var pair in mediumPairs)
            {
                AddAreaContactZoneEdges(
                    graph,
                    pair.A,
                    pair.B,
                    profile.MaxDegree,
                    targetLinks: 2,
                    random,
                    modifierMultiplier: 0.78);
            }

            return;
        }

        if (profile.Scale == GraphScaleType.Large)
        {
            var strongPairs = new List<(int A, int B)>
        {
            (0, 1),
            (1, 6),
            (6, 4),
            (4, 5)
        };

            foreach (var pair in strongPairs)
            {
                if (pair.A >= nonEmptyGroups.Count || pair.B >= nonEmptyGroups.Count)
                    continue;

                AddAreaContactZoneEdges(
                    graph,
                    nonEmptyGroups[pair.A],
                    nonEmptyGroups[pair.B],
                    profile.MaxDegree,
                    targetLinks: 6,
                    random,
                    modifierMultiplier: 0.95,
                    allowSharedEndpoints: true);
            }

            var bridgePairs = new List<(int A, int B)>
        {
            (0, 3),
            (1, 2),
            (2, 6),
            (3, 4),
            (5, 6)
        };

            foreach (var pair in bridgePairs)
            {
                if (pair.A >= nonEmptyGroups.Count || pair.B >= nonEmptyGroups.Count)
                    continue;

                AddAreaContactZoneEdges(
                    graph,
                    nonEmptyGroups[pair.A],
                    nonEmptyGroups[pair.B],
                    profile.MaxDegree,
                    targetLinks: 3,
                    random,
                    modifierMultiplier: 0.76);
            }

            return;
        }

        var defaultPairs = BuildNeighborAreaPairs(nonEmptyGroups, profile);

        foreach (var pair in defaultPairs)
        {
            AddAreaContactZoneEdges(
                graph,
                pair.A,
                pair.B,
                profile.MaxDegree,
                targetLinks: 1,
                random,
                modifierMultiplier: 0.78);
        }
    }


    private List<ClusteredPatch> CreateSimpleRandomGraphPatches(
    int width,
    int height,
    SimulationParameters parameters,
    Random random,
    ClusteredScaleProfile profile)
    {
        int patchCount = Math.Max(1, profile.PatchCount);
        var patches = new List<ClusteredPatch>();

        var layout = profile.Scale switch
        {
            GraphScaleType.Medium => new List<(double X, double Y)>
        {
            (0.28, 0.30),
            (0.72, 0.32),
            (0.30, 0.70),
            (0.72, 0.68)
        },

            GraphScaleType.Large => new List<(double X, double Y)>
        {
            (0.18, 0.28),
            (0.48, 0.18),
            (0.78, 0.30),
            (0.22, 0.72),
            (0.52, 0.55),
            (0.80, 0.72),
            (0.50, 0.38)
        },

            _ => new List<(double X, double Y)>
        {
            (0.50, 0.50)
        }
        };

        for (int i = 0; i < patchCount; i++)
        {
            var point = layout[Math.Min(i, layout.Count - 1)];
            double noise = Math.Clamp(parameters.MapNoiseStrength, 0.0, 1.0);

            double noiseScale = profile.Scale == GraphScaleType.Large ? 0.025 : 0.04;

            double centerX = Math.Clamp(
                width * point.X + (random.NextDouble() * 2.0 - 1.0) * width * noiseScale * noise,
                3,
                width - 4);

            double centerY = Math.Clamp(
                height * point.Y + (random.NextDouble() * 2.0 - 1.0) * height * noiseScale * noise,
                3,
                height - 4);

            var dominantVegetation = profile.Scale == GraphScaleType.Large
                ? i switch
                {
                    0 => VegetationType.Coniferous,
                    1 => VegetationType.Shrub,
                    2 => VegetationType.Mixed,
                    3 => VegetationType.Deciduous,
                    4 => VegetationType.Grass,
                    5 => VegetationType.Mixed,
                    6 => VegetationType.Shrub,
                    _ => GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters)
                }
                : GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters);

            double moistureSpread = profile.Scale == GraphScaleType.Large ? 0.15 : 0.08;

            double baseMoisture = GetRandomGraphMoisture(
                dominantVegetation,
                parameters,
                random,
                moistureSpread);

            if (profile.Scale == GraphScaleType.Large)
            {
                baseMoisture = i switch
                {
                    0 => Math.Clamp(baseMoisture - 0.12, 0.05, 0.95),
                    1 => Math.Clamp(baseMoisture - 0.04, 0.05, 0.95),
                    2 => Math.Clamp(baseMoisture + 0.08, 0.05, 0.95),
                    3 => Math.Clamp(baseMoisture + 0.14, 0.05, 0.95),
                    4 => Math.Clamp(baseMoisture - 0.16, 0.05, 0.95),
                    5 => Math.Clamp(baseMoisture + 0.03, 0.05, 0.95),
                    6 => Math.Clamp(baseMoisture - 0.08, 0.05, 0.95),
                    _ => baseMoisture
                };
            }

            double radiusX = profile.Scale switch
            {
                GraphScaleType.Medium => 6.8,
                GraphScaleType.Large => i switch
                {
                    0 => 7.6,
                    1 => 6.4,
                    2 => 7.2,
                    3 => 8.0,
                    4 => 8.4,
                    5 => 7.0,
                    6 => 5.8,
                    _ => 7.0
                },
                _ => 5.0
            };

            double radiusY = profile.Scale switch
            {
                GraphScaleType.Medium => 5.8,
                GraphScaleType.Large => i switch
                {
                    0 => 5.8,
                    1 => 4.8,
                    2 => 5.8,
                    3 => 6.2,
                    4 => 6.6,
                    5 => 5.8,
                    6 => 5.0,
                    _ => 5.8
                },
                _ => 4.4
            };

            patches.Add(new ClusteredPatch
            {
                Index = i,
                CenterX = centerX,
                CenterY = centerY,
                RadiusX = radiusX,
                RadiusY = radiusY,
                DominantVegetation = dominantVegetation,
                BaseMoisture = baseMoisture,
                BaseElevation = GetRandomElevation(parameters.ElevationVariation, random, parameters) *
                    (profile.Scale == GraphScaleType.Large ? 0.34 : 0.20),
                Tag = $"область-{i + 1}"
            });
        }

        return patches;
    }

    private (int X, int Y) GetFreeRandomPosition(
        int width,
        int height,
        HashSet<(int X, int Y)> used,
        Random random,
        int margin)
    {
        margin = Math.Clamp(margin, 0, Math.Max(0, Math.Min(width, height) / 3));

        for (int attempt = 0; attempt < 300; attempt++)
        {
            int x = random.Next(margin, Math.Max(margin + 1, width - margin));
            int y = random.Next(margin, Math.Max(margin + 1, height - margin));

            if (used.Add((x, y)))
                return (x, y);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (used.Add((x, y)))
                    return (x, y);
            }
        }

        return (0, 0);
    }
    private (int X, int Y) GetFreePositionNearPatch(
        int width,
        int height,
        ClusteredPatch patch,
        HashSet<(int X, int Y)> used,
        Random random)
    {
        for (int attempt = 0; attempt < 400; attempt++)
        {
            double angle = random.NextDouble() * Math.PI * 2.0;
            double radius = Math.Sqrt(random.NextDouble());

            int x = (int)Math.Round(patch.CenterX + Math.Cos(angle) * patch.RadiusX * radius);
            int y = (int)Math.Round(patch.CenterY + Math.Sin(angle) * patch.RadiusY * radius);

            x = Math.Clamp(x, 0, width - 1);
            y = Math.Clamp(y, 0, height - 1);

            if (used.Add((x, y)))
                return (x, y);
        }

        return GetFreeRandomPosition(width, height, used, random, margin: 1);
    }
    private double GetRandomGraphMoisture(
        VegetationType vegetation,
        SimulationParameters parameters,
        Random random,
        double spread)
    {
        var (min, max) = GetEffectiveMoistureRange(parameters);

        double center = (min + max) / 2.0;
        center += GetVegetationMoistureBias(vegetation);

        double value = center + (random.NextDouble() * 2.0 - 1.0) * spread;

        if (vegetation == VegetationType.Water)
            return 1.0;

        if (vegetation == VegetationType.Bare)
            return Math.Clamp(value, 0.02, 0.25);

        return Math.Clamp(value, min, max);
    }
    private void ConnectGraphLocally(
      ForestGraph graph,
      List<ForestCell> group,
      double radius,
      int maxDegree,
      int targetDegree,
      Random random)
    {
        if (group.Count <= 1)
            return;

        foreach (var cell in group)
        {
            var candidates = group
                .Where(other => other.Id != cell.Id)
                .Select(other => new
                {
                    Cell = other,
                    Distance = CalculateDistance(cell.X, cell.Y, other.X, other.Y)
                })
                .Where(x => x.Distance <= radius)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Cell.X)
                .ThenBy(x => x.Cell.Y)
                .Take(targetDegree + 2)
                .ToList();

            int added = 0;

            foreach (var item in candidates)
            {
                if (GetNodeDegree(graph, cell) >= maxDegree)
                    break;

                if (GetNodeDegree(graph, item.Cell) >= maxDegree)
                    continue;

                double probability = item.Distance switch
                {
                    <= 2.5 => 0.85,
                    <= 4.0 => 0.55,
                    <= 6.0 => 0.30,
                    _ => 0.15
                };

                if (random.NextDouble() > probability)
                    continue;

                if (TryAddEdge(graph, cell, item.Cell))
                    added++;

                if (added >= targetDegree)
                    break;
            }
        }
    }
    private void EnsureGraphConnectedInsideGroup(
        ForestGraph graph,
        List<ForestCell> group,
        int maxDegree)
    {
        if (group.Count <= 1)
            return;

        var ordered = group
            .OrderBy(c => c.X)
            .ThenBy(c => c.Y)
            .ToList();

        for (int i = 1; i < ordered.Count; i++)
        {
            var current = ordered[i];

            bool hasConnectionToPrevious = ordered
                .Take(i)
                .Any(previous => EdgeExists(graph, current, previous));

            if (hasConnectionToPrevious)
                continue;

            var nearestPrevious = ordered
                .Take(i)
                .OrderBy(previous => CalculateDistance(current.X, current.Y, previous.X, previous.Y))
                .FirstOrDefault();

            if (nearestPrevious != null)
                TryAddEdge(graph, current, nearestPrevious);
        }
    }


    private List<(List<ForestCell> A, List<ForestCell> B)> BuildNeighborAreaPairs(
     List<List<ForestCell>> groups,
     ClusteredScaleProfile profile)
    {
        var result = new List<(List<ForestCell> A, List<ForestCell> B)>();

        if (groups.Count <= 1)
            return result;

        void AddPair(int firstIndex, int secondIndex)
        {
            if (firstIndex < 0 || secondIndex < 0)
                return;

            if (firstIndex >= groups.Count || secondIndex >= groups.Count)
                return;

            if (groups[firstIndex].Count == 0 || groups[secondIndex].Count == 0)
                return;

            AddAreaPairIfMissing(result, groups[firstIndex], groups[secondIndex]);
        }

        if (profile.Scale == GraphScaleType.Medium)
        {
            AddPair(0, 1);
            AddPair(0, 2);
            AddPair(1, 3);
            AddPair(2, 3);

            return result;
        }

        if (profile.Scale == GraphScaleType.Large)
        {
            AddPair(0, 1);
            AddPair(1, 6);
            AddPair(6, 4);
            AddPair(4, 5);

            AddPair(0, 3);
            AddPair(1, 2);
            AddPair(2, 6);
            AddPair(3, 4);
            AddPair(5, 6);

            return result;
        }

        for (int i = 1; i < groups.Count; i++)
            AddPair(i - 1, i);

        return result;
    }
    private void AddLimitedSupportEdgesBetweenCloseAreas(
     ForestGraph graph,
     List<List<ForestCell>> groups,
     ClusteredScaleProfile profile)
    {
        if (profile.ExtendedEdgeBudget <= 0 || groups.Count <= 1)
            return;

        if (profile.Scale == GraphScaleType.Medium)
            return;

        int added = 0;

        var pairs = new List<(ForestCell From, ForestCell To, double Distance)>();

        for (int i = 0; i < groups.Count; i++)
        {
            for (int j = i + 1; j < groups.Count; j++)
            {
                var candidates = GetAreaContactCandidates(
                        graph,
                        groups[i],
                        groups[j],
                        profile.MaxDegree)
                    .Where(c => c.Distance <= Math.Max(profile.ExtendedRadius, 10.0))
                    .ToList();

                pairs.AddRange(candidates);
            }
        }

        foreach (var pair in pairs.OrderBy(p => p.Distance))
        {
            if (added >= profile.ExtendedEdgeBudget)
                break;

            if (pair.Distance > 12.0)
                continue;

            if (GetNodeDegree(graph, pair.From) >= profile.MaxDegree ||
                GetNodeDegree(graph, pair.To) >= profile.MaxDegree)
            {
                continue;
            }

            if (!TryAddEdge(graph, pair.From, pair.To))
                continue;

            var edge = GetEdge(graph, pair.From, pair.To);
            if (edge != null)
                ApplyTransitionEdgeModifier(edge, 0.66);

            added++;
        }
    }

    private void AddAreaPairIfMissing(
        List<(List<ForestCell> A, List<ForestCell> B)> pairs,
        List<ForestCell> first,
        List<ForestCell> second)
    {
        bool exists = pairs.Any(pair =>
            (ReferenceEquals(pair.A, first) && ReferenceEquals(pair.B, second)) ||
            (ReferenceEquals(pair.A, second) && ReferenceEquals(pair.B, first)));

        if (!exists)
            pairs.Add((first, second));
    }

    private void ApplyRandomGraphEdgeModifiers(ForestGraph graph)
    {
        foreach (var edge in graph.Edges)
        {
            bool sameArea = string.Equals(
                edge.FromCell.ClusterId,
                edge.ToCell.ClusterId,
                StringComparison.Ordinal);

            if (sameArea)
            {
                double factor = 1.08;

                if (edge.FromCell.Vegetation == VegetationType.Coniferous ||
                    edge.ToCell.Vegetation == VegetationType.Coniferous)
                {
                    factor *= 1.07;
                }

                if (edge.FromCell.Vegetation == VegetationType.Grass ||
                    edge.ToCell.Vegetation == VegetationType.Grass)
                {
                    factor *= 1.04;
                }

                double moisture = (edge.FromCell.Moisture + edge.ToCell.Moisture) / 2.0;

                if (moisture < 0.28)
                    factor *= 1.10;
                else if (moisture > 0.65)
                    factor *= 0.82;

                SetEdgeFireSpreadModifier(
                    edge,
                    Math.Clamp(edge.FireSpreadModifier * factor, 0.02, 1.45));

                continue;
            }

            ApplyTransitionEdgeModifier(edge, 0.82);
        }
    }
    private ClusteredScaleProfile GetClusteredScaleProfile(SimulationParameters parameters, int nodeCount)
    {
        var scale = GetEffectiveGraphScale(parameters, nodeCount);

        return scale switch
        {
            GraphScaleType.Small => new ClusteredScaleProfile
            {
                Scale = GraphScaleType.Small,
                PatchCount = 1,
                LocalTargetDegree = 2,
                MaxDegree = 4,
                CloseRadius = 5.0,
                SupportRadius = 6.2,
                ExtendedRadius = 0.0,
                ExtendedEdgeBudget = 0,
                PlacementScale = 1.0,
                PreferredBridgeCount = 0,
                CorridorBudget = 0
            },

            GraphScaleType.Medium => new ClusteredScaleProfile
            {
                Scale = GraphScaleType.Medium,
                PatchCount = 4,
                LocalTargetDegree = 3,
                MaxDegree = 6,
                CloseRadius = 5.2,
                SupportRadius = 7.0,
                ExtendedRadius = 8.5,
                ExtendedEdgeBudget = 1,
                PlacementScale = 1.0,
                PreferredBridgeCount = 4,
                CorridorBudget = 0
            },

            GraphScaleType.Large => new ClusteredScaleProfile
            {
                Scale = GraphScaleType.Large,
                PatchCount = 7,
                LocalTargetDegree = 3,
                MaxDegree = 7,
                CloseRadius = 5.6,
                SupportRadius = 7.6,
                ExtendedRadius = 10.5,
                ExtendedEdgeBudget = 4,
                PlacementScale = 1.0,
                PreferredBridgeCount = 9,
                CorridorBudget = 3
            },

            _ => new ClusteredScaleProfile
            {
                Scale = GraphScaleType.Medium,
                PatchCount = 4,
                LocalTargetDegree = 3,
                MaxDegree = 6,
                CloseRadius = 5.2,
                SupportRadius = 7.0,
                ExtendedRadius = 8.5,
                ExtendedEdgeBudget = 1,
                PlacementScale = 1.0,
                PreferredBridgeCount = 4,
                CorridorBudget = 0
            }
        };
    }

    private List<(List<ForestCell> A, List<ForestCell> B)> BuildFixedNeighborAreaPairs(
   List<List<ForestCell>> groups,
   ClusteredScaleProfile profile)
    {
        var result = new List<(List<ForestCell> A, List<ForestCell> B)>();

        if (groups.Count <= 1)
            return result;

        void AddPair(int firstIndex, int secondIndex)
        {
            if (firstIndex < 0 || secondIndex < 0)
                return;

            if (firstIndex >= groups.Count || secondIndex >= groups.Count)
                return;

            if (groups[firstIndex].Count == 0 || groups[secondIndex].Count == 0)
                return;

            bool exists = result.Any(pair =>
                (ReferenceEquals(pair.A, groups[firstIndex]) && ReferenceEquals(pair.B, groups[secondIndex])) ||
                (ReferenceEquals(pair.A, groups[secondIndex]) && ReferenceEquals(pair.B, groups[firstIndex])));

            if (!exists)
                result.Add((groups[firstIndex], groups[secondIndex]));
        }

        if (profile.Scale == GraphScaleType.Medium)
        {
            AddPair(0, 1);
            AddPair(0, 2);
            AddPair(1, 3);
            AddPair(2, 3);
            return result;
        }

        if (profile.Scale == GraphScaleType.Large)
        {
            AddPair(0, 6);
            AddPair(1, 6);
            AddPair(2, 6);
            AddPair(3, 6);
            AddPair(4, 6);
            AddPair(5, 6);

            AddPair(0, 1);
            AddPair(1, 2);
            AddPair(3, 4);
            AddPair(4, 5);

            return result;
        }

        for (int i = 1; i < groups.Count; i++)
            AddPair(i - 1, i);

        return result;
    }

    private int AddAreaContactZoneEdges(
     ForestGraph graph,
     List<ForestCell> fromGroup,
     List<ForestCell> toGroup,
     int maxDegree,
     int targetLinks,
     Random random,
     double modifierMultiplier = 0.78,
     bool allowSharedEndpoints = false)
    {
        var candidates = GetAreaContactCandidates(graph, fromGroup, toGroup, maxDegree)
            .OrderBy(c => c.Distance)
            .ThenBy(_ => random.Next())
            .ToList();

        if (candidates.Count == 0)
            return 0;

        int added = 0;
        var usedFrom = new HashSet<Guid>();
        var usedTo = new HashSet<Guid>();

        foreach (var candidate in candidates)
        {
            if (added >= targetLinks)
                break;

            if (!allowSharedEndpoints &&
                (usedFrom.Contains(candidate.From.Id) || usedTo.Contains(candidate.To.Id)))
            {
                continue;
            }

            if (GetNodeDegree(graph, candidate.From) >= maxDegree ||
                GetNodeDegree(graph, candidate.To) >= maxDegree)
            {
                continue;
            }

            if (!TryAddEdge(graph, candidate.From, candidate.To))
                continue;

            var edge = GetEdge(graph, candidate.From, candidate.To);
            if (edge != null)
                ApplyTransitionEdgeModifier(edge, modifierMultiplier);

            usedFrom.Add(candidate.From.Id);
            usedTo.Add(candidate.To.Id);
            added++;
        }

        return added;
    }

    private List<(ForestCell From, ForestCell To, double Distance)> GetAreaContactCandidates(
      ForestGraph graph,
      List<ForestCell> fromGroup,
      List<ForestCell> toGroup,
      int maxDegree)
    {
        var result = new List<(ForestCell From, ForestCell To, double Distance)>();

        if (fromGroup.Count == 0 || toGroup.Count == 0)
            return result;

        var centerA = GetGroupCenter(fromGroup);
        var centerB = GetGroupCenter(toGroup);

        double dx = centerB.X - centerA.X;
        double dy = centerB.Y - centerA.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 0.0001)
            return result;

        double dirX = dx / length;
        double dirY = dy / length;

        var fromBoundary = GetFacingBoundaryNodes(
            graph,
            fromGroup,
            maxDegree,
            dirX,
            dirY);

        var toBoundary = GetFacingBoundaryNodes(
            graph,
            toGroup,
            maxDegree,
            -dirX,
            -dirY);

        foreach (var from in fromBoundary)
        {
            foreach (var to in toBoundary)
            {
                if (EdgeExists(graph, from, to))
                    continue;

                double distance = CalculateDistance(from.X, from.Y, to.X, to.Y);

                if (distance > 10.5)
                    continue;

                double edgeDx = to.X - from.X;
                double edgeDy = to.Y - from.Y;
                double edgeLength = Math.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);

                if (edgeLength < 0.0001)
                    continue;

                double alignment = Math.Abs((edgeDx / edgeLength) * dirX + (edgeDy / edgeLength) * dirY);

                if (alignment < 0.52)
                    continue;

                double perpendicularGap = Math.Abs(edgeDx * dirY - edgeDy * dirX);

                if (perpendicularGap > 5.5)
                    continue;

                result.Add((from, to, distance));
            }
        }

        if (result.Count == 0)
        {
            result = (from a in fromBoundary.DefaultIfEmpty().Where(x => x != null).Cast<ForestCell>()
                      where GetNodeDegree(graph, a) < maxDegree
                      from b in toBoundary.DefaultIfEmpty().Where(x => x != null).Cast<ForestCell>()
                      where GetNodeDegree(graph, b) < maxDegree
                      where !EdgeExists(graph, a, b)
                      let d = CalculateDistance(a.X, a.Y, b.X, b.Y)
                      where d <= 11.5
                      orderby d
                      select (a, b, d))
                .Take(12)
                .ToList();
        }

        if (result.Count == 0)
        {
            result = (from a in fromGroup
                      where GetNodeDegree(graph, a) < maxDegree
                      from b in toGroup
                      where GetNodeDegree(graph, b) < maxDegree
                      where !EdgeExists(graph, a, b)
                      let d = CalculateDistance(a.X, a.Y, b.X, b.Y)
                      where d <= 12.0
                      orderby d
                      select (a, b, d))
                .Take(10)
                .ToList();
        }

        if (result.Count == 0)
            return result;

        double minDistance = result.Min(x => x.Distance);
        double maxAllowedDistance = Math.Max(minDistance + 3.0, minDistance * 1.45);

        return result
            .Where(x => x.Distance <= maxAllowedDistance)
            .OrderBy(x => x.Distance)
            .Take(18)
            .ToList();
    }
    private List<ForestCell> GetFacingBoundaryNodes(
    ForestGraph graph,
    List<ForestCell> group,
    int maxDegree,
    double directionX,
    double directionY)
    {
        var available = group
            .Where(c => GetNodeDegree(graph, c) < maxDegree)
            .ToList();

        if (available.Count == 0)
            return new List<ForestCell>();

        double maxProjection = available.Max(c => c.X * directionX + c.Y * directionY);

        return available
            .Where(c => maxProjection - (c.X * directionX + c.Y * directionY) <= 4.0)
            .OrderByDescending(c => c.X * directionX + c.Y * directionY)
            .ThenBy(c => Math.Abs(c.X * -directionY + c.Y * directionX))
            .Take(14)
            .ToList();
    }
    private (double X, double Y) GetGroupCenter(List<ForestCell> group)
    {
        if (group.Count == 0)
            return (0.0, 0.0);

        return (
            group.Average(c => c.X),
            group.Average(c => c.Y));
    }

    private void ApplyTransitionEdgeModifier(ForestEdge edge, double baseMultiplier)
    {
        double vegetationFactor = GetVegetationTransitionFactor(edge.FromCell.Vegetation) *
                                  GetVegetationTransitionFactor(edge.ToCell.Vegetation);

        double averageMoisture = (edge.FromCell.Moisture + edge.ToCell.Moisture) / 2.0;

        double moistureFactor = averageMoisture switch
        {
            < 0.24 => 1.18,
            < 0.35 => 1.08,
            > 0.78 => 0.42,
            > 0.65 => 0.58,
            > 0.52 => 0.76,
            _ => 0.92
        };

        double distanceFactor = edge.Distance switch
        {
            <= 2.5 => 1.05,
            <= 4.0 => 0.92,
            <= 6.0 => 0.78,
            _ => 0.62
        };

        double factor = baseMultiplier * vegetationFactor * moistureFactor * distanceFactor;

        SetEdgeFireSpreadModifier(
            edge,
            Math.Clamp(edge.FireSpreadModifier * factor, 0.02, 0.95));
    }

    private double GetVegetationTransitionFactor(VegetationType vegetation)
    {
        return vegetation switch
        {
            VegetationType.Grass => 1.16,
            VegetationType.Shrub => 1.06,
            VegetationType.Coniferous => 1.08,
            VegetationType.Mixed => 0.98,
            VegetationType.Deciduous => 0.88,
            VegetationType.Bare => 0.35,
            VegetationType.Water => 0.16,
            _ => 1.0
        };
    }


    private void BuildClusteredScenarioGraph(
       ForestGraph graph,
       int nodeCount,
       SimulationParameters parameters,
       Random random,
       int maxDegree)
    {
        var scenario = parameters.ClusteredScenarioType ?? ClusteredScenarioType.MixedForest;
        var profile = GetClusteredScaleProfile(parameters, nodeCount);

        _logger.LogInformation(
            "Генерация демо-графа: сценарий={Scenario}, масштаб={Scale}, узлов={NodeCount}",
            scenario,
            profile.Scale,
            nodeCount);

        if (profile.Scale != GraphScaleType.Large)
        {
            BuildClusteredRandomGraph(graph, nodeCount, parameters, random, profile.MaxDegree);
            return;
        }

        switch (scenario)
        {
            case ClusteredScenarioType.DryConiferousMassif:
                BuildDenseDryConiferousScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.ForestWithRiver:
                BuildWaterBarrierScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.ForestWithLake:
                BuildLakeBarrierScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.ForestWithFirebreak:
                BuildFirebreakGapScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.HillyTerrain:
                BuildHillyClustersScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.WetForestAfterRain:
                BuildWetAfterRainScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.MixedForest:
            default:
                BuildMixedDryHotspotsScenario(graph, nodeCount, parameters, random, profile);
                return;
        }
    }
    private void BuildLakeBarrierScenario(
        ForestGraph graph,
        int nodeCount,
        SimulationParameters parameters,
        Random random,
        ClusteredScaleProfile profile)
    {
        var used = new HashSet<(int X, int Y)>();
        int total = Math.Max(140, nodeCount);

        int westForestCount = Math.Max(30, (int)(total * 0.26));
        int eastForestCount = Math.Max(30, (int)(total * 0.26));
        int southForestCount = Math.Max(22, (int)(total * 0.18));
        int lakeCount = Math.Max(22, (int)(total * 0.16));
        int shoreCount = Math.Max(12, total - westForestCount - eastForestCount - southForestCount - lakeCount);

        var westForest = AddScenarioPatchNodesEllipse(
            graph, used, "западный-смешанный-лес",
            0.24, 0.46, 0.20, 0.24, westForestCount,
            parameters, random,
            VegetationType.Mixed, 0.20, 0.38, 0.0, 14.0);

        var eastForest = AddScenarioPatchNodesEllipse(
            graph, used, "восточный-лиственный-лес",
            0.76, 0.44, 0.19, 0.23, eastForestCount,
            parameters, random,
            VegetationType.Deciduous, 0.26, 0.46, -2.0, 10.0);

        var southForest = AddScenarioPatchNodesEllipse(
            graph, used, "южный-обход-озера",
            0.50, 0.78, 0.24, 0.11, southForestCount,
            parameters, random,
            VegetationType.Grass, 0.16, 0.30, -1.0, 8.0);

        var lake = AddScenarioPatchNodesEllipse(
            graph, used, "озеро",
            0.50, 0.45, 0.16, 0.18, lakeCount,
            parameters, random,
            VegetationType.Water, 1.0, 1.0, -12.0, -6.0);

        var shore = AddScenarioPatchNodesEllipse(
            graph, used, "влажный-берег",
            0.50, 0.62, 0.19, 0.08, shoreCount,
            parameters, random,
            VegetationType.Shrub, 0.34, 0.56, -4.0, 4.0);

        ConnectScenarioNodesLocally(graph, westForest, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, eastForest, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, southForest, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, lake, profile.CloseRadius, profile.MaxDegree, 2);
        ConnectScenarioNodesLocally(graph, shore, profile.CloseRadius, profile.MaxDegree, 2);

        AddScenarioBridge(graph, westForest, shore, 3, 0.72);
        AddScenarioBridge(graph, shore, eastForest, 3, 0.68);
        AddScenarioBridge(graph, westForest, southForest, 2, 0.86);
        AddScenarioBridge(graph, southForest, eastForest, 2, 0.82);

        foreach (var waterNode in lake)
        {
            foreach (var edge in graph.GetIncidentEdges(waterNode))
                SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 0.12, 0.02, 0.22));
        }

        foreach (var shoreNode in shore)
        {
            foreach (var edge in graph.GetIncidentEdges(shoreNode))
                SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 0.68, 0.02, 0.90));
        }

        ApplySurfaceBarrierEdgeModifiers(graph);
        ApplyClusteredBridgeWeakening(graph, 0.70);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }
    private void BuildDenseDryConiferousScenario(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     ClusteredScaleProfile profile)
    {
        var used = new HashSet<(int X, int Y)>();
        int total = Math.Max(profile.Scale == GraphScaleType.Large ? 140 : 70, nodeCount);

        int mainCount = profile.Scale == GraphScaleType.Large ? 42 : 26;
        int eastCount = profile.Scale == GraphScaleType.Large ? 34 : 20;
        int southCount = profile.Scale == GraphScaleType.Large ? 28 : 14;
        int bufferCount = Math.Max(
            profile.Scale == GraphScaleType.Large ? 16 : 10,
            total - mainCount - eastCount - southCount);

        var mainConiferous = AddScenarioPatchNodesEllipse(
            graph, used, "главный-сухой-хвойный-массив",
            0.30, 0.45, 0.24, 0.28, mainCount,
            parameters, random,
            VegetationType.Coniferous, 0.05, 0.14, 8.0, 26.0);

        var eastConiferous = AddScenarioPatchNodesEllipse(
            graph, used, "восточный-сухой-хвойный-массив",
            0.68, 0.42, 0.20, 0.24, eastCount,
            parameters, random,
            VegetationType.Coniferous, 0.06, 0.16, 6.0, 24.0);

        var southConiferous = AddScenarioPatchNodesEllipse(
            graph, used, "южный-хвойный-участок",
            0.48, 0.76, 0.22, 0.13, southCount,
            parameters, random,
            VegetationType.Coniferous, 0.07, 0.18, 2.0, 18.0);

        var mixedBuffer = AddScenarioPatchNodesEllipse(
            graph, used, "смешанная-влажная-кромка",
            0.78, 0.72, 0.15, 0.12, bufferCount,
            parameters, random,
            VegetationType.Mixed, 0.22, 0.42, -2.0, 8.0);

        var groups = new List<List<ForestCell>>
    {
        mainConiferous,
        eastConiferous,
        southConiferous,
        mixedBuffer
    };

        foreach (var group in groups)
        {
            ConnectScenarioNodesLocally(
                graph,
                group,
                profile.SupportRadius,
                profile.MaxDegree,
                profile.LocalTargetDegree + 1);
        }

        AddScenarioBridge(
            graph,
            mainConiferous,
            eastConiferous,
            profile.Scale == GraphScaleType.Large ? 5 : 3,
            1.18);

        AddScenarioBridge(
            graph,
            mainConiferous,
            southConiferous,
            profile.Scale == GraphScaleType.Large ? 4 : 2,
            1.12);

        AddScenarioBridge(
            graph,
            eastConiferous,
            southConiferous,
            profile.Scale == GraphScaleType.Large ? 3 : 2,
            1.08);

        AddScenarioBridge(
            graph,
            eastConiferous,
            mixedBuffer,
            profile.Scale == GraphScaleType.Large ? 2 : 1,
            0.76);

        AddScenarioBridge(
            graph,
            southConiferous,
            mixedBuffer,
            profile.Scale == GraphScaleType.Large ? 2 : 1,
            0.72);

        if (profile.Scale == GraphScaleType.Large)
        {
            var northDryPocket = AddScenarioPatchNodesEllipse(
                graph, used, "северный-сухой-хвойный-карман",
                0.48, 0.16, 0.17, 0.09, Math.Max(10, total / 12),
                parameters, random,
                VegetationType.Coniferous, 0.05, 0.13, 14.0, 32.0);

            var westernShrubEdge = AddScenarioPatchNodesEllipse(
                graph, used, "западная-сухая-кустарниковая-кромка",
                0.12, 0.72, 0.10, 0.13, Math.Max(8, total / 16),
                parameters, random,
                VegetationType.Shrub, 0.07, 0.16, 2.0, 14.0);

            ConnectScenarioNodesLocally(graph, northDryPocket, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
            ConnectScenarioNodesLocally(graph, westernShrubEdge, profile.SupportRadius, profile.MaxDegree, 2);

            AddScenarioBridge(graph, mainConiferous, northDryPocket, 3, 1.14);
            AddScenarioBridge(graph, northDryPocket, eastConiferous, 2, 1.08);
            AddScenarioBridge(graph, mainConiferous, westernShrubEdge, 2, 1.02);
            AddScenarioBridge(graph, westernShrubEdge, southConiferous, 1, 0.92);

            groups.Add(northDryPocket);
            groups.Add(westernShrubEdge);

            CreateLargeScaleCorridors(graph, BuildPatchesFromExistingClusters(groups), parameters, random, profile);
        }

        foreach (var edge in graph.Edges)
        {
            bool coniferousEdge =
                edge.FromCell.Vegetation == VegetationType.Coniferous ||
                edge.ToCell.Vegetation == VegetationType.Coniferous;

            bool bothConiferous =
                edge.FromCell.Vegetation == VegetationType.Coniferous &&
                edge.ToCell.Vegetation == VegetationType.Coniferous;

            bool dryEdge =
                edge.FromCell.Moisture < 0.18 ||
                edge.ToCell.Moisture < 0.18;

            bool bufferEdge =
                edge.FromCell.ClusterId == "смешанная-влажная-кромка" ||
                edge.ToCell.ClusterId == "смешанная-влажная-кромка";

            double factor = 1.0;

            if (bothConiferous)
                factor *= 1.22;
            else if (coniferousEdge)
                factor *= 1.12;

            if (dryEdge)
                factor *= 1.14;

            if (bufferEdge)
                factor *= 0.68;

            SetEdgeFireSpreadModifier(
                edge,
                Math.Clamp(edge.FireSpreadModifier * factor, 0.02, 1.90));
        }

        ApplyClusteredBridgeWeakening(graph, profile.Scale == GraphScaleType.Large ? 0.74 : 0.80);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }
    private void BuildWaterBarrierScenario(
        ForestGraph graph,
        int nodeCount,
        SimulationParameters parameters,
        Random random,
        ClusteredScaleProfile profile)
    {
        var used = new HashSet<(int X, int Y)>();
        int total = Math.Max(140, nodeCount);

        int westCount = Math.Max(34, (int)(total * 0.30));
        int eastCount = Math.Max(34, (int)(total * 0.30));
        int riverCount = Math.Max(24, (int)(total * 0.17));
        int southBypassCount = Math.Max(28, (int)(total * 0.20));
        int northBankCount = Math.Max(10, total - westCount - eastCount - riverCount - southBypassCount);

        var westForest = AddScenarioPatchNodesEllipse(
            graph, used, "западный-смешанный-лес",
            0.22, 0.52, 0.18, 0.25, westCount,
            parameters, random,
            VegetationType.Mixed, 0.20, 0.36, 0.0, 12.0);

        var eastForest = AddScenarioPatchNodesEllipse(
            graph, used, "восточный-лиственный-лес",
            0.78, 0.52, 0.18, 0.25, eastCount,
            parameters, random,
            VegetationType.Deciduous, 0.24, 0.42, -2.0, 9.0);

        var river = AddScenarioPatchNodesEllipse(
            graph, used, "прямая-река-барьер",
            0.50, 0.46, 0.055, 0.39, riverCount,
            parameters, random,
            VegetationType.Water, 1.0, 1.0, -16.0, -8.0);

        var southBypass = AddScenarioPatchNodesEllipse(
            graph, used, "южный-обход-реки",
            0.50, 0.86, 0.31, 0.075, southBypassCount,
            parameters, random,
            VegetationType.Grass, 0.14, 0.27, -1.0, 6.0);

        var northBank = AddScenarioPatchNodesEllipse(
            graph, used, "северный-влажный-берег",
            0.50, 0.12, 0.20, 0.055, northBankCount,
            parameters, random,
            VegetationType.Shrub, 0.34, 0.54, -4.0, 5.0);

        ConnectScenarioNodesLocally(graph, westForest, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, eastForest, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, river, profile.CloseRadius, profile.MaxDegree, 2);
        ConnectScenarioNodesLocally(graph, southBypass, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, northBank, profile.CloseRadius, profile.MaxDegree, 2);

        AddScenarioBridge(graph, westForest, southBypass, 5, 0.92);
        AddScenarioBridge(graph, southBypass, eastForest, 5, 0.86);

        AddScenarioBridge(graph, westForest, northBank, 1, 0.55);
        AddScenarioBridge(graph, northBank, eastForest, 1, 0.50);

        foreach (var waterNode in river)
        {
            var nearestWest = westForest
                .OrderBy(c => CalculateDistance(c.X, c.Y, waterNode.X, waterNode.Y))
                .Take(1)
                .ToList();

            var nearestEast = eastForest
                .OrderBy(c => CalculateDistance(c.X, c.Y, waterNode.X, waterNode.Y))
                .Take(1)
                .ToList();

            foreach (var forestNode in nearestWest.Concat(nearestEast))
            {
                if (!TryAddEdge(graph, waterNode, forestNode))
                    continue;

                var edge = GetEdge(graph, waterNode, forestNode);
                if (edge != null)
                    SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 0.08, 0.02, 0.14));
            }
        }

        foreach (var edge in graph.Edges)
        {
            bool hasWater =
                edge.FromCell.Vegetation == VegetationType.Water ||
                edge.ToCell.Vegetation == VegetationType.Water;

            if (hasWater)
                SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 0.10, 0.02, 0.16));
        }

        ApplySurfaceBarrierEdgeModifiers(graph);
        ApplyClusteredBridgeWeakening(graph, 0.72);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }

    private void BuildFirebreakGapScenario(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     ClusteredScaleProfile profile)
    {
        var used = new HashSet<(int X, int Y)>();
        int total = Math.Max(140, nodeCount);

        int northCount = Math.Max(42, (int)(total * 0.34));
        int southCount = Math.Max(42, (int)(total * 0.34));
        int firebreakCount = Math.Max(16, (int)(total * 0.11));
        int eastBypassCount = Math.Max(26, (int)(total * 0.18));
        int westDryCount = Math.Max(8, total - northCount - southCount - firebreakCount - eastBypassCount);

        var northForest = AddScenarioPatchNodesEllipse(
            graph, used, "северная-хвойная-зона",
            0.40, 0.28, 0.28, 0.16, northCount,
            parameters, random,
            VegetationType.Coniferous, 0.08, 0.17, 4.0, 18.0);

        var southForest = AddScenarioPatchNodesEllipse(
            graph, used, "южная-смешанная-зона",
            0.40, 0.72, 0.28, 0.16, southCount,
            parameters, random,
            VegetationType.Mixed, 0.14, 0.28, 0.0, 12.0);

        var firebreak = AddScenarioPatchNodesEllipse(
            graph, used, "горизонтальная-просека",
            0.42, 0.50, 0.34, 0.035, firebreakCount,
            parameters, random,
            VegetationType.Bare, 0.04, 0.10, -1.0, 3.0);

        var eastBypass = AddScenarioPatchNodesEllipse(
            graph, used, "правый-обход-просеки",
            0.82, 0.50, 0.105, 0.31, eastBypassCount,
            parameters, random,
            VegetationType.Grass, 0.08, 0.18, 0.0, 6.0);

        var westDry = AddScenarioPatchNodesEllipse(
            graph, used, "левая-сухая-кромка",
            0.12, 0.50, 0.08, 0.22, westDryCount,
            parameters, random,
            VegetationType.Shrub, 0.08, 0.18, 1.0, 7.0);

        ConnectScenarioNodesLocally(graph, northForest, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, southForest, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, firebreak, profile.CloseRadius, profile.MaxDegree, 1);
        ConnectScenarioNodesLocally(graph, eastBypass, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, westDry, profile.CloseRadius, profile.MaxDegree, 2);

        AddScenarioBridge(graph, northForest, eastBypass, 4, 0.90);
        AddScenarioBridge(graph, eastBypass, southForest, 4, 0.86);

        AddScenarioBridge(graph, northForest, westDry, 1, 0.48);
        AddScenarioBridge(graph, westDry, southForest, 1, 0.44);

        foreach (var barrierNode in firebreak)
        {
            var nearestNorth = northForest
                .OrderBy(c => CalculateDistance(c.X, c.Y, barrierNode.X, barrierNode.Y))
                .Take(1)
                .ToList();

            var nearestSouth = southForest
                .OrderBy(c => CalculateDistance(c.X, c.Y, barrierNode.X, barrierNode.Y))
                .Take(1)
                .ToList();

            foreach (var forestNode in nearestNorth.Concat(nearestSouth))
            {
                if (!TryAddEdge(graph, barrierNode, forestNode))
                    continue;

                var edge = GetEdge(graph, barrierNode, forestNode);
                if (edge != null)
                    SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 0.14, 0.02, 0.22));
            }
        }

        foreach (var edge in graph.Edges)
        {
            bool hasBare =
                edge.FromCell.Vegetation == VegetationType.Bare ||
                edge.ToCell.Vegetation == VegetationType.Bare;

            if (hasBare)
                SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 0.16, 0.02, 0.26));
        }

        ApplySurfaceBarrierEdgeModifiers(graph);
        ApplyClusteredBridgeWeakening(graph, 0.70);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }
    private void BuildHillyClustersScenario(
       ForestGraph graph,
       int nodeCount,
       SimulationParameters parameters,
       Random random,
       ClusteredScaleProfile profile)
    {
        var used = new HashSet<(int X, int Y)>();
        int total = Math.Max(profile.Scale == GraphScaleType.Large ? 140 : 75, nodeCount);

        var lowWet = AddScenarioPatchNodesEllipse(
            graph, used, "нижняя-влажная-долина",
            0.25, 0.72, 0.22, 0.15, Math.Max(16, (int)(total * 0.24)),
            parameters, random,
            VegetationType.Deciduous, 0.26, 0.48, -14.0, -2.0);

        var centralSlope = AddScenarioPatchNodesEllipse(
            graph, used, "центральный-склон",
            0.50, 0.53, 0.18, 0.18, Math.Max(16, (int)(total * 0.24)),
            parameters, random,
            VegetationType.Mixed, 0.15, 0.30, 12.0, 34.0);

        var dryRidge = AddScenarioPatchNodesEllipse(
            graph, used, "сухая-верхняя-гряда",
            0.45, 0.22, 0.22, 0.10, Math.Max(14, (int)(total * 0.20)),
            parameters, random,
            VegetationType.Shrub, 0.06, 0.16, 42.0, 70.0);

        var rightSlope = AddScenarioPatchNodesEllipse(
            graph, used, "правый-смешанный-склон",
            0.76, 0.58, 0.18, 0.22, Math.Max(14, total - lowWet.Count - centralSlope.Count - dryRidge.Count),
            parameters, random,
            VegetationType.Mixed, 0.14, 0.30, 8.0, 30.0);

        var groups = new List<List<ForestCell>> { lowWet, centralSlope, dryRidge, rightSlope };

        foreach (var group in groups)
            ConnectScenarioNodesLocally(graph, group, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);

        AddScenarioBridge(graph, lowWet, centralSlope, profile.Scale == GraphScaleType.Large ? 3 : 2, 0.94);
        AddScenarioBridge(graph, centralSlope, dryRidge, profile.Scale == GraphScaleType.Large ? 3 : 2, 1.16);
        AddScenarioBridge(graph, centralSlope, rightSlope, profile.Scale == GraphScaleType.Large ? 3 : 2, 1.02);
        AddScenarioBridge(graph, dryRidge, rightSlope, profile.Scale == GraphScaleType.Large ? 2 : 1, 0.90);

        if (profile.Scale == GraphScaleType.Large)
        {
            var upperDryPocket = AddScenarioPatchNodesEllipse(
                graph, used, "верхний-сухой-карман",
                0.72, 0.24, 0.14, 0.10, Math.Max(10, total / 12),
                parameters, random,
                VegetationType.Coniferous, 0.06, 0.15, 36.0, 62.0);

            ConnectScenarioNodesLocally(graph, upperDryPocket, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
            AddScenarioBridge(graph, dryRidge, upperDryPocket, 2, 1.10);
            AddScenarioBridge(graph, upperDryPocket, rightSlope, 1, 0.90);

            groups.Add(upperDryPocket);
            CreateLargeScaleCorridors(graph, BuildPatchesFromExistingClusters(groups), parameters, random, profile);
        }

        double averageElevation = graph.Cells.Count == 0
            ? 0.0
            : graph.Cells.Average(c => c.Elevation);

        foreach (var edge in graph.Edges)
        {
            bool goesUp = edge.ToCell.Elevation > edge.FromCell.Elevation + 8.0 ||
                          edge.FromCell.Elevation > edge.ToCell.Elevation + 8.0;

            bool highArea = edge.FromCell.Elevation > averageElevation + 14.0 ||
                            edge.ToCell.Elevation > averageElevation + 14.0;

            bool lowWetEdge = edge.FromCell.ClusterId == "нижняя-влажная-долина" ||
                              edge.ToCell.ClusterId == "нижняя-влажная-долина";

            double factor = 1.0;

            if (goesUp)
                factor *= 1.08;

            if (highArea)
                factor *= 1.10;

            if (lowWetEdge)
                factor *= 0.76;

            SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * factor, 0.02, 1.70));
        }

        ApplyClusteredBridgeWeakening(graph, profile.Scale == GraphScaleType.Large ? 0.70 : 0.78);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }


    private void BuildWetAfterRainScenario(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     ClusteredScaleProfile profile)
    {
        var used = new HashSet<(int X, int Y)>();
        int total = Math.Max(profile.Scale == GraphScaleType.Large ? 140 : 70, nodeCount);

        var wetNorth = AddScenarioPatchNodesEllipse(
            graph, used, "влажный-север",
            0.33, 0.30, 0.23, 0.18, Math.Max(18, (int)(total * 0.30)),
            parameters, random,
            VegetationType.Deciduous, 0.48, 0.72, -5.0, 5.0);

        var wetEast = AddScenarioPatchNodesEllipse(
            graph, used, "влажный-восток",
            0.74, 0.52, 0.20, 0.24, Math.Max(18, (int)(total * 0.30)),
            parameters, random,
            VegetationType.Mixed, 0.40, 0.66, -3.0, 8.0);

        var dryIsland = AddScenarioPatchNodesEllipse(
            graph, used, "сухой-островок",
            0.48, 0.56, 0.14, 0.14, Math.Max(10, (int)(total * 0.16)),
            parameters, random,
            VegetationType.Coniferous, 0.12, 0.24, 6.0, 18.0);

        var grassSouth = AddScenarioPatchNodesEllipse(
            graph, used, "южная-травяная-кромка",
            0.42, 0.80, 0.24, 0.10, Math.Max(10, total - wetNorth.Count - wetEast.Count - dryIsland.Count),
            parameters, random,
            VegetationType.Grass, 0.22, 0.38, -2.0, 6.0);

        var groups = new List<List<ForestCell>> { wetNorth, wetEast, dryIsland, grassSouth };

        foreach (var group in groups)
            ConnectScenarioNodesLocally(graph, group, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);

        AddScenarioBridge(graph, wetNorth, dryIsland, profile.Scale == GraphScaleType.Large ? 2 : 1, 0.58);
        AddScenarioBridge(graph, dryIsland, wetEast, profile.Scale == GraphScaleType.Large ? 2 : 1, 0.56);
        AddScenarioBridge(graph, dryIsland, grassSouth, profile.Scale == GraphScaleType.Large ? 2 : 1, 0.70);
        AddScenarioBridge(graph, wetNorth, wetEast, profile.Scale == GraphScaleType.Large ? 2 : 1, 0.46);

        if (profile.Scale == GraphScaleType.Large)
        {
            var wetLowland = AddScenarioPatchNodesEllipse(
                graph, used, "низина-после-дождя",
                0.67, 0.78, 0.18, 0.11, Math.Max(10, total / 12),
                parameters, random,
                VegetationType.Deciduous, 0.56, 0.82, -8.0, 0.0);

            ConnectScenarioNodesLocally(graph, wetLowland, profile.SupportRadius, profile.MaxDegree, 2);
            AddScenarioBridge(graph, wetEast, wetLowland, 2, 0.48);
            AddScenarioBridge(graph, grassSouth, wetLowland, 1, 0.54);
        }

        foreach (var edge in graph.Edges)
        {
            double averageMoisture = (edge.FromCell.Moisture + edge.ToCell.Moisture) / 2.0;

            bool dryIslandEdge =
                edge.FromCell.ClusterId == "сухой-островок" ||
                edge.ToCell.ClusterId == "сухой-островок";

            double factor = averageMoisture switch
            {
                > 0.62 => 0.42,
                > 0.48 => 0.56,
                > 0.36 => 0.72,
                _ => 0.92
            };

            if (dryIslandEdge)
                factor *= 1.12;

            SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * factor, 0.02, 1.20));
        }

        ApplyClusteredBridgeWeakening(graph, profile.Scale == GraphScaleType.Large ? 0.68 : 0.76);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }

    private void BuildMixedDryHotspotsScenario(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     ClusteredScaleProfile profile)
    {
        var used = new HashSet<(int X, int Y)>();

        if (profile.Scale == GraphScaleType.Small)
        {
            int smallTotal = Math.Clamp(nodeCount, 12, 24);

            var main = AddScenarioPatchNodesEllipse(
                graph, used, "малый-граф",
                0.50, 0.50, 0.33, 0.30, smallTotal,
                parameters, random,
                VegetationType.Mixed, 0.18, 0.34, 0.0, 12.0);

            ConnectScenarioNodesLocally(graph, main, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree + 1);

            foreach (var edge in graph.Edges)
                SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 1.02, 0.02, 1.35));

            EnsureNoIsolatedNodes(graph, profile.MaxDegree);
            return;
        }

        int total = Math.Max(profile.Scale == GraphScaleType.Large ? 140 : 70, nodeCount);

        var mixedWest = AddScenarioPatchNodesEllipse(
            graph, used, "смешанный-запад",
            0.23, 0.46, 0.19, 0.24, Math.Max(16, (int)(total * 0.24)),
            parameters, random,
            VegetationType.Mixed, 0.18, 0.34, 0.0, 12.0);

        var dryCenter = AddScenarioPatchNodesEllipse(
            graph, used, "сухой-центральный-очаг",
            0.52, 0.34, 0.16, 0.14, Math.Max(14, (int)(total * 0.22)),
            parameters, random,
            VegetationType.Coniferous, 0.06, 0.15, 6.0, 22.0);

        var grassRoute = AddScenarioPatchNodesEllipse(
            graph, used, "травяной-маршрут",
            0.51, 0.70, 0.24, 0.12, Math.Max(12, (int)(total * 0.18)),
            parameters, random,
            VegetationType.Grass, 0.08, 0.20, -1.0, 6.0);

        var wetEast = AddScenarioPatchNodesEllipse(
            graph, used, "влажный-восток",
            0.78, 0.55, 0.18, 0.23, Math.Max(14, total - mixedWest.Count - dryCenter.Count - grassRoute.Count),
            parameters, random,
            VegetationType.Deciduous, 0.30, 0.52, -4.0, 8.0);

        var groups = new List<List<ForestCell>> { mixedWest, dryCenter, grassRoute, wetEast };

        foreach (var group in groups)
            ConnectScenarioNodesLocally(graph, group, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);

        AddScenarioBridge(graph, mixedWest, dryCenter, profile.Scale == GraphScaleType.Large ? 3 : 2, 1.08);
        AddScenarioBridge(graph, dryCenter, grassRoute, profile.Scale == GraphScaleType.Large ? 3 : 2, 1.04);
        AddScenarioBridge(graph, grassRoute, wetEast, profile.Scale == GraphScaleType.Large ? 2 : 1, 0.82);
        AddScenarioBridge(graph, dryCenter, wetEast, profile.Scale == GraphScaleType.Large ? 2 : 1, 0.66);

        if (profile.Scale == GraphScaleType.Large)
        {
            var northMixed = AddScenarioPatchNodesEllipse(
                graph, used, "северная-смешанная-зона",
                0.48, 0.16, 0.18, 0.09, Math.Max(10, total / 12),
                parameters, random,
                VegetationType.Mixed, 0.18, 0.34, 4.0, 18.0);

            var southDry = AddScenarioPatchNodesEllipse(
                graph, used, "южный-сухой-карман",
                0.70, 0.82, 0.17, 0.09, Math.Max(10, total / 12),
                parameters, random,
                VegetationType.Shrub, 0.07, 0.17, 1.0, 12.0);

            ConnectScenarioNodesLocally(graph, northMixed, profile.SupportRadius, profile.MaxDegree, 2);
            ConnectScenarioNodesLocally(graph, southDry, profile.SupportRadius, profile.MaxDegree, 2);

            AddScenarioBridge(graph, mixedWest, northMixed, 2, 0.94);
            AddScenarioBridge(graph, northMixed, dryCenter, 2, 1.02);
            AddScenarioBridge(graph, grassRoute, southDry, 2, 1.00);
            AddScenarioBridge(graph, southDry, wetEast, 1, 0.74);

            groups.Add(northMixed);
            groups.Add(southDry);

            CreateLargeScaleCorridors(graph, BuildPatchesFromExistingClusters(groups), parameters, random, profile);
        }

        foreach (var edge in graph.Edges)
        {
            bool dryEdge =
                edge.FromCell.ClusterId == "сухой-центральный-очаг" ||
                edge.ToCell.ClusterId == "сухой-центральный-очаг" ||
                edge.FromCell.ClusterId == "южный-сухой-карман" ||
                edge.ToCell.ClusterId == "южный-сухой-карман";

            bool wetEdge =
                edge.FromCell.ClusterId == "влажный-восток" ||
                edge.ToCell.ClusterId == "влажный-восток";

            bool grassEdge =
                edge.FromCell.ClusterId == "травяной-маршрут" ||
                edge.ToCell.ClusterId == "травяной-маршрут";

            double factor = 1.0;

            if (dryEdge)
                factor *= 1.14;

            if (grassEdge)
                factor *= 1.06;

            if (wetEdge)
                factor *= 0.74;

            SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * factor, 0.02, 1.65));
        }

        ApplyClusteredBridgeWeakening(graph, profile.Scale == GraphScaleType.Large ? 0.70 : 0.78);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }
    private GraphScaleType GetEffectiveGraphScale(SimulationParameters parameters, int nodeCount)
    {
        if (parameters.GraphScaleType.HasValue)
            return parameters.GraphScaleType.Value;

        if (nodeCount <= 20)
            return GraphScaleType.Small;

        if (nodeCount <= 80)
            return GraphScaleType.Medium;

        return GraphScaleType.Large;
    }

    private void CreateEdgesForGrid(ForestGraph graph, int width, int height)
    {
        var cellMap = graph.Cells.ToDictionary(c => (c.X, c.Y), c => c);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!cellMap.TryGetValue((x, y), out var from))
                    continue;

                foreach (var (nx, ny) in GetGridNeighbors8(x, y, width, height))
                {
                    if (nx < x || (nx == x && ny <= y))
                        continue;

                    if (!cellMap.TryGetValue((nx, ny), out var to))
                        continue;

                    if (EdgeExists(graph, from, to))
                        continue;

                    var distance = CalculateDistance(from.X, from.Y, to.X, to.Y);
                    var slope = distance <= 0.0001 ? 0.0 : (to.Elevation - from.Elevation) / distance;
                    graph.Edges.Add(new ForestEdge(from, to, distance, slope));
                }
            }
        }
    }

    private void ApplySurfaceBarrierEdgeModifiers(ForestGraph graph)
    {
        foreach (var edge in graph.Edges)
        {
            var from = edge.FromCell;
            var to = edge.ToCell;

            if (from == null || to == null)
                continue;

            double factor = 1.0;

            bool fromBarrier = from.Vegetation == VegetationType.Water || from.Vegetation == VegetationType.Bare;
            bool toBarrier = to.Vegetation == VegetationType.Water || to.Vegetation == VegetationType.Bare;

            if (fromBarrier || toBarrier)
                factor *= 0.25;

            if (from.Vegetation == VegetationType.Water || to.Vegetation == VegetationType.Water)
                factor *= 0.35;

            if (from.Vegetation == VegetationType.Bare || to.Vegetation == VegetationType.Bare)
                factor *= 0.55;

            SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * factor, 0.02, 1.15));
        }
    }

    private void CreateLargeScaleCorridors(
        ForestGraph graph,
        List<ClusteredPatch> patches,
        SimulationParameters parameters,
        Random random,
        ClusteredScaleProfile profile)
    {
        if (graph.Cells.Count == 0 || patches.Count < 3 || profile.CorridorBudget <= 0)
            return;

        var cellsByCluster = graph.Cells
            .Where(c => !string.IsNullOrWhiteSpace(c.ClusterId))
            .GroupBy(c => c.ClusterId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var orderedPatches = patches
            .OrderBy(p => p.CenterX)
            .ThenBy(p => p.CenterY)
            .ToList();

        int created = 0;

        int sequentialBudget = Math.Max(1, profile.CorridorBudget / 2);
        int farBudget = Math.Max(1, profile.CorridorBudget - sequentialBudget);

        for (int i = 0; i < orderedPatches.Count - 1 && created < sequentialBudget; i++)
        {
            string clusterA = $"patch-{orderedPatches[i].Index}";
            string clusterB = $"patch-{orderedPatches[i + 1].Index}";

            if (!cellsByCluster.TryGetValue(clusterA, out var clusterACells) || clusterACells.Count == 0)
                continue;

            if (!cellsByCluster.TryGetValue(clusterB, out var clusterBCells) || clusterBCells.Count == 0)
                continue;

            var bestPair = FindClosestPair(graph, clusterACells, clusterBCells, profile.MaxDegree);
            if (bestPair == null)
                continue;

            if (!TryAddEdge(graph, bestPair.Value.From, bestPair.Value.To))
                continue;

            var edge = GetEdge(graph, bestPair.Value.From, bestPair.Value.To);
            if (edge != null)
            {
                SetEdgeFireSpreadModifier(
                    edge,
                    Math.Clamp(edge.FireSpreadModifier * (0.52 + random.NextDouble() * 0.20), 0.08, 0.85));
            }

            created++;
        }

        if (created >= profile.CorridorBudget)
            return;

        var farPatchPairs = new List<(ClusteredPatch A, ClusteredPatch B, double Distance)>();

        for (int i = 0; i < orderedPatches.Count; i++)
        {
            for (int j = i + 2; j < orderedPatches.Count; j++)
            {
                var a = orderedPatches[i];
                var b = orderedPatches[j];

                double dx = a.CenterX - b.CenterX;
                double dy = a.CenterY - b.CenterY;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                farPatchPairs.Add((a, b, distance));
            }
        }

        foreach (var pair in farPatchPairs.OrderByDescending(p => p.Distance))
        {
            if (created >= profile.CorridorBudget || farBudget <= 0)
                break;

            string clusterA = $"patch-{pair.A.Index}";
            string clusterB = $"patch-{pair.B.Index}";

            if (!cellsByCluster.TryGetValue(clusterA, out var clusterACells) || clusterACells.Count == 0)
                continue;

            if (!cellsByCluster.TryGetValue(clusterB, out var clusterBCells) || clusterBCells.Count == 0)
                continue;

            var candidatePairs = clusterACells
                .Where(a => GetNodeDegree(graph, a) < profile.MaxDegree)
                .SelectMany(a => clusterBCells
                    .Where(b => GetNodeDegree(graph, b) < profile.MaxDegree)
                    .Where(b => !EdgeExists(graph, a, b))
                    .Select(b => new
                    {
                        From = a,
                        To = b,
                        Distance = CalculateDistance(a.X, a.Y, b.X, b.Y)
                    }))
                .Where(x => x.Distance >= Math.Max(profile.SupportRadius * 1.35, 3.0))
                .OrderByDescending(x => x.Distance)
                .ToList();

            if (candidatePairs.Count == 0)
                continue;

            var selected = candidatePairs.First();

            if (!TryAddEdge(graph, selected.From, selected.To))
                continue;

            var edge = GetEdge(graph, selected.From, selected.To);
            if (edge != null)
            {
                SetEdgeFireSpreadModifier(
                    edge,
                    Math.Clamp(edge.FireSpreadModifier * (0.42 + random.NextDouble() * 0.16), 0.06, 0.72));
            }

            created++;
            farBudget--;
        }
    }
    private void ConnectScenarioNodesLocally(
       ForestGraph graph,
       List<ForestCell> nodes,
       double radius,
       int maxDegree,
       int localTargetDegree)
    {
        if (nodes.Count == 0)
            return;

        foreach (var source in nodes)
        {
            if (GetNodeDegree(graph, source) >= localTargetDegree)
                continue;

            var candidates = nodes
                .Where(n => n.Id != source.Id)
                .Select(n => new
                {
                    Cell = n,
                    Distance = CalculateDistance(source.X, source.Y, n.X, n.Y)
                })
                .Where(x => x.Distance > 0.0 && x.Distance <= radius)
                .OrderBy(x => x.Distance)
                .ToList();

            foreach (var candidate in candidates)
            {
                if (GetNodeDegree(graph, source) >= localTargetDegree)
                    break;

                if (GetNodeDegree(graph, candidate.Cell) >= maxDegree)
                    continue;

                if (EdgeExists(graph, source, candidate.Cell))
                    continue;

                TryAddEdge(graph, source, candidate.Cell);
            }
        }
    }

    private void ApplyClusteredBridgeWeakening(ForestGraph graph, double bridgeFactor)
    {
        foreach (var edge in graph.Edges)
        {
            var fromCluster = edge.FromCell?.ClusterId;
            var toCluster = edge.ToCell?.ClusterId;

            if (string.IsNullOrWhiteSpace(fromCluster) || string.IsNullOrWhiteSpace(toCluster))
                continue;

            if (string.Equals(fromCluster, toCluster, StringComparison.Ordinal))
                continue;

            double distanceCompensation = edge.Distance switch
            {
                <= 3.0 => 1.10,
                <= 5.0 => 1.25,
                <= 7.5 => 1.45,
                <= 10.0 => 1.65,
                _ => 1.80
            };

            double corridorCompensation = edge.IsCorridor ? 1.18 : 1.08;

            double newModifier =
                edge.FireSpreadModifier *
                bridgeFactor *
                distanceCompensation *
                corridorCompensation;

            SetEdgeFireSpreadModifier(
                edge,
                Math.Clamp(newModifier, 0.04, 1.45));
        }
    }

    private void EnsureNoIsolatedNodes(ForestGraph graph, int maxDegree)
    {
        var isolated = graph.Cells
            .Where(c => graph.GetIncidentEdges(c).Count == 0)
            .ToList();

        foreach (var cell in isolated)
        {
            var nearest = graph.Cells
                .Where(c => c.Id != cell.Id)
                .Where(c => graph.GetIncidentEdges(c).Count < maxDegree)
                .OrderBy(c => CalculateDistance(cell.X, cell.Y, c.X, c.Y))
                .FirstOrDefault();

            if (nearest == null)
                continue;

            TryAddEdge(graph, cell, nearest);
        }
    }

    private List<ForestCell> AddScenarioPatchNodesEllipse(
        ForestGraph graph,
        HashSet<(int X, int Y)> used,
        string clusterId,
        double centerXFactor,
        double centerYFactor,
        double radiusXFactor,
        double radiusYFactor,
        int count,
        SimulationParameters parameters,
        Random random,
        VegetationType vegetation,
        double moistureMin,
        double moistureMax,
        double elevationMin,
        double elevationMax)
    {
        var result = new List<ForestCell>();
        count = Math.Max(0, count);

        double centerX = graph.Width * centerXFactor;
        double centerY = graph.Height * centerYFactor;
        double radiusX = Math.Max(1.5, graph.Width * radiusXFactor);
        double radiusY = Math.Max(1.5, graph.Height * radiusYFactor);

        int guard = count * 100 + 200;

        while (result.Count < count && guard-- > 0)
        {
            double nx = NextGaussian(random) * 0.55;
            double ny = NextGaussian(random) * 0.55;

            double xRaw = centerX + nx * radiusX;
            double yRaw = centerY + ny * radiusY;

            int x = Math.Clamp((int)Math.Round(xRaw), 0, graph.Width - 1);
            int y = Math.Clamp((int)Math.Round(yRaw), 0, graph.Height - 1);

            double ellipse = Math.Pow((x - centerX) / radiusX, 2) + Math.Pow((y - centerY) / radiusY, 2);
            if (ellipse > 1.15)
                continue;

            if (!used.Add((x, y)))
                continue;

            double moisture = Math.Clamp(moistureMin + random.NextDouble() * Math.Max(0.001, moistureMax - moistureMin), 0.02, 0.98);
            double elevation = elevationMin + random.NextDouble() * Math.Max(0.001, elevationMax - elevationMin);

            if (vegetation == VegetationType.Water)
            {
                moisture = 1.0;
                elevation -= 8.0;
            }
            else if (vegetation == VegetationType.Bare)
            {
                moisture = Math.Clamp(moisture, 0.03, 0.18);
                elevation += 1.0;
            }

            var cell = new ForestCell(x, y, vegetation, moisture, elevation, clusterId);
            graph.Cells.Add(cell);
            result.Add(cell);
        }

        return result;
    }

    private List<ForestCell> AddLinearBarrierNodes(
        ForestGraph graph,
        HashSet<(int X, int Y)> used,
        string clusterId,
        double xFactor,
        int totalNodes,
        SimulationParameters parameters,
        Random random,
        VegetationType vegetation,
        double moisture,
        double elevationBase)
    {
        var result = new List<ForestCell>();
        totalNodes = Math.Max(1, totalNodes);

        int centerX = Math.Clamp((int)Math.Round(graph.Width * xFactor), 0, graph.Width - 1);

        for (int i = 0; i < totalNodes; i++)
        {
            double yFactor = totalNodes == 1 ? 0.5 : i / (double)(totalNodes - 1);
            int y = Math.Clamp((int)Math.Round(1 + yFactor * Math.Max(1, graph.Height - 2)), 0, graph.Height - 1);
            int x = Math.Clamp(centerX + random.Next(-1, 2), 0, graph.Width - 1);

            if (!used.Add((x, y)))
                continue;

            var cell = new ForestCell(
                x,
                y,
                vegetation,
                vegetation == VegetationType.Water ? 1.0 : moisture,
                elevationBase + random.NextDouble() * 1.5,
                clusterId);

            graph.Cells.Add(cell);
            result.Add(cell);
        }

        return result;
    }

    private void CreateGapAwareBridge(
        ForestGraph graph,
        List<ForestCell> left,
        List<ForestCell> right,
        List<ForestCell> throughNodes,
        int maxLinks,
        double weakenTo)
    {
        int added = 0;

        foreach (var gapNode in throughNodes)
        {
            if (added >= maxLinks)
                break;

            var leftNearest = left
                .OrderBy(c => CalculateDistance(c.X, c.Y, gapNode.X, gapNode.Y))
                .FirstOrDefault();

            var rightNearest = right
                .OrderBy(c => CalculateDistance(c.X, c.Y, gapNode.X, gapNode.Y))
                .FirstOrDefault();

            if (leftNearest != null && TryAddEdge(graph, leftNearest, gapNode))
            {
                var edge = GetEdge(graph, leftNearest, gapNode);
                if (edge != null)
                    SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * weakenTo, 0.02, 0.45));
            }

            if (rightNearest != null && TryAddEdge(graph, gapNode, rightNearest))
            {
                var edge = GetEdge(graph, gapNode, rightNearest);
                if (edge != null)
                    SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * weakenTo, 0.02, 0.45));
            }

            added++;
        }
    }

    private void AddScenarioBridge(
        ForestGraph graph,
        List<ForestCell> fromCluster,
        List<ForestCell> toCluster,
        int bridgeCount,
        double modifierMultiplier)
    {
        if (fromCluster.Count == 0 || toCluster.Count == 0 || bridgeCount <= 0)
            return;

        int added = 0;
        var pairs = from a in fromCluster
                    from b in toCluster
                    let d = CalculateDistance(a.X, a.Y, b.X, b.Y)
                    orderby d
                    select new { From = a, To = b };

        foreach (var pair in pairs)
        {
            if (added >= bridgeCount)
                break;

            if (!TryAddEdge(graph, pair.From, pair.To))
                continue;

            var edge = GetEdge(graph, pair.From, pair.To);
            if (edge != null)
                SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * modifierMultiplier, 0.02, 1.45));

            added++;
        }
    }

    private (ForestCell From, ForestCell To)? FindClosestPair(
        ForestGraph graph,
        List<ForestCell> fromCluster,
        List<ForestCell> toCluster,
        int maxDegree)
    {
        return (from a in fromCluster
                where GetNodeDegree(graph, a) < maxDegree
                from b in toCluster
                where GetNodeDegree(graph, b) < maxDegree
                where !EdgeExists(graph, a, b)
                let d = CalculateDistance(a.X, a.Y, b.X, b.Y)
                orderby d
                select (a, b)).Cast<(ForestCell, ForestCell)?>().FirstOrDefault();
    }

    private List<ClusteredPatch> BuildPatchesFromExistingClusters(List<List<ForestCell>> clusters)
    {
        var patches = new List<ClusteredPatch>();

        for (int i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            if (cluster.Count == 0)
                continue;

            patches.Add(new ClusteredPatch
            {
                Index = i,
                CenterX = cluster.Average(c => c.X),
                CenterY = cluster.Average(c => c.Y),
                RadiusX = Math.Max(1.0, cluster.Max(c => c.X) - cluster.Min(c => c.X)),
                RadiusY = Math.Max(1.0, cluster.Max(c => c.Y) - cluster.Min(c => c.Y)),
                DominantVegetation = cluster.GroupBy(c => c.Vegetation).OrderByDescending(g => g.Count()).First().Key,
                BaseMoisture = cluster.Average(c => c.Moisture),
                BaseElevation = cluster.Average(c => c.Elevation),
                Tag = "existing"
            });
        }

        return patches;
    }

    private ForestGraph BuildClusteredGraphFromBlueprint(
     ClusteredGraphBlueprint blueprint,
     SimulationParameters parameters)
    {
        var normalizedBlueprint = NormalizeClusteredBlueprint(blueprint, parameters);

        int width = Math.Max(8, normalizedBlueprint.CanvasWidth);
        int height = Math.Max(8, normalizedBlueprint.CanvasHeight);

        var graph = new ForestGraph
        {
            Width = width,
            Height = height,
            StepDurationSeconds = parameters.StepDurationSeconds
        };

        var cellByDraftId = new Dictionary<Guid, ForestCell>();

        foreach (var nodeDraft in normalizedBlueprint.Nodes)
        {
            var clusterId = string.IsNullOrWhiteSpace(nodeDraft.ClusterId)
                ? "manual"
                : nodeDraft.ClusterId.Trim();

            var cell = new ForestCell(
                nodeDraft.X,
                nodeDraft.Y,
                NormalizeBlueprintVegetation(nodeDraft.Vegetation),
                NormalizeBlueprintMoisture(nodeDraft.Moisture, parameters),
                NormalizeBlueprintElevation(nodeDraft.Elevation, parameters),
                clusterId);

            SetBackingField(cell, "<Id>k__BackingField", nodeDraft.Id);

            graph.Cells.Add(cell);
            cellByDraftId[nodeDraft.Id] = cell;
        }

        foreach (var edgeDraft in normalizedBlueprint.Edges)
        {
            if (!cellByDraftId.TryGetValue(edgeDraft.FromNodeId, out var fromCell) ||
                !cellByDraftId.TryGetValue(edgeDraft.ToNodeId, out var toCell))
            {
                continue;
            }

            if (fromCell.Id == toCell.Id)
                continue;

            double dx = toCell.X - fromCell.X;
            double dy = toCell.Y - fromCell.Y;

            double geometricDistance = Math.Sqrt(dx * dx + dy * dy);
            double effectiveDistance = edgeDraft.DistanceOverride.HasValue
                ? Math.Max(0.25, edgeDraft.DistanceOverride.Value)
                : Math.Max(0.25, geometricDistance);

            double slope = 0.0;
            if (effectiveDistance > 0.0001)
                slope = (toCell.Elevation - fromCell.Elevation) / effectiveDistance;

            var edge = new ForestEdge(fromCell, toCell, effectiveDistance, slope);

            SetBackingField(edge, "<Id>k__BackingField", edgeDraft.Id);
            SetEdgeFireSpreadModifier(
                edge,
                Math.Clamp(
                    NormalizeBlueprintFireSpreadModifier(edgeDraft.FireSpreadModifier),
                    0.02,
                    1.85));

            graph.Edges.Add(edge);
        }

        SoftCompleteBlueprintConnectivity(graph, parameters);

        _logger.LogInformation(
            "Blueprint graph normalized and built: Nodes={Nodes}, Edges={Edges}, Canvas={Width}x{Height}",
            graph.Cells.Count,
            graph.Edges.Count,
            graph.Width,
            graph.Height);

        return graph;
    }
    private ClusteredGraphBlueprint NormalizeClusteredBlueprint(
        ClusteredGraphBlueprint blueprint,
        SimulationParameters parameters)
    {
        blueprint ??= new ClusteredGraphBlueprint();

        int canvasWidth = Math.Max(8, blueprint.CanvasWidth > 0 ? blueprint.CanvasWidth : parameters.GridWidth);
        int canvasHeight = Math.Max(8, blueprint.CanvasHeight > 0 ? blueprint.CanvasHeight : parameters.GridHeight);

        var normalized = new ClusteredGraphBlueprint
        {
            CanvasWidth = canvasWidth,
            CanvasHeight = canvasHeight,
            Candidates = (blueprint.Candidates ?? new List<ClusteredCandidateNode>())
                .Where(x => x != null)
                .Select(x => new ClusteredCandidateNode
                {
                    Id = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id,
                    X = Math.Clamp(x.X, 0, canvasWidth - 1),
                    Y = Math.Clamp(x.Y, 0, canvasHeight - 1)
                })
                .GroupBy(x => (x.X, x.Y))
                .Select(g => g.First())
                .ToList()
        };

        var occupiedCoordinates = new HashSet<(int X, int Y)>();
        var normalizedNodes = new List<ClusteredNodeDraft>();

        foreach (var sourceNode in blueprint.Nodes ?? Enumerable.Empty<ClusteredNodeDraft>())
        {
            if (sourceNode == null)
                continue;

            int x = Math.Clamp(sourceNode.X, 0, canvasWidth - 1);
            int y = Math.Clamp(sourceNode.Y, 0, canvasHeight - 1);

            if (!occupiedCoordinates.Add((x, y)))
                continue;

            normalizedNodes.Add(new ClusteredNodeDraft
            {
                Id = sourceNode.Id == Guid.Empty ? Guid.NewGuid() : sourceNode.Id,
                X = x,
                Y = y,
                ClusterId = string.IsNullOrWhiteSpace(sourceNode.ClusterId)
                    ? "manual"
                    : sourceNode.ClusterId.Trim(),
                Vegetation = NormalizeBlueprintVegetation(sourceNode.Vegetation),
                Moisture = NormalizeBlueprintMoisture(sourceNode.Moisture, parameters),
                Elevation = NormalizeBlueprintElevation(sourceNode.Elevation, parameters)
            });
        }

        normalized.Nodes = normalizedNodes;

        var validNodeIds = normalized.Nodes
            .Select(x => x.Id)
            .ToHashSet();

        var seenEdgeKeys = new HashSet<string>();
        var normalizedEdges = new List<ClusteredEdgeDraft>();

        foreach (var sourceEdge in blueprint.Edges ?? Enumerable.Empty<ClusteredEdgeDraft>())
        {
            if (sourceEdge == null)
                continue;

            if (sourceEdge.FromNodeId == Guid.Empty || sourceEdge.ToNodeId == Guid.Empty)
                continue;

            if (!validNodeIds.Contains(sourceEdge.FromNodeId) || !validNodeIds.Contains(sourceEdge.ToNodeId))
                continue;

            if (sourceEdge.FromNodeId == sourceEdge.ToNodeId)
                continue;

            var orderedA = sourceEdge.FromNodeId.CompareTo(sourceEdge.ToNodeId) < 0
                ? sourceEdge.FromNodeId
                : sourceEdge.ToNodeId;

            var orderedB = sourceEdge.FromNodeId.CompareTo(sourceEdge.ToNodeId) < 0
                ? sourceEdge.ToNodeId
                : sourceEdge.FromNodeId;

            var edgeKey = $"{orderedA:N}:{orderedB:N}";
            if (!seenEdgeKeys.Add(edgeKey))
                continue;

            normalizedEdges.Add(new ClusteredEdgeDraft
            {
                Id = sourceEdge.Id == Guid.Empty ? Guid.NewGuid() : sourceEdge.Id,
                FromNodeId = sourceEdge.FromNodeId,
                ToNodeId = sourceEdge.ToNodeId,
                DistanceOverride = sourceEdge.DistanceOverride.HasValue
                    ? Math.Max(0.25, sourceEdge.DistanceOverride.Value)
                    : null,
                FireSpreadModifier = NormalizeBlueprintFireSpreadModifier(sourceEdge.FireSpreadModifier)
            });
        }

        normalized.Edges = normalizedEdges;

        return normalized;
    }
    private void SoftCompleteBlueprintConnectivity(
      ForestGraph graph,
      SimulationParameters parameters)
    {
        if (graph.Cells.Count <= 1)
            return;

        var profile = GetClusteredScaleProfile(parameters, Math.Max(graph.Cells.Count, 1));

        int maxDegree = Math.Max(2, profile.MaxDegree);
        double preferredCompletionDistance = profile.Scale switch
        {
            GraphScaleType.Small => 6.0,
            GraphScaleType.Medium => 7.5,
            _ => 9.5
        };

        var isolatedCells = graph.Cells
            .Where(cell => !graph.GetIncidentEdges(cell).Any())
            .ToList();

        foreach (var isolated in isolatedCells)
        {
            var allCandidates = graph.Cells
                .Where(other => other.Id != isolated.Id)
                .Where(other => graph.GetIncidentEdges(other).Count < maxDegree)
                .Select(other => new
                {
                    Cell = other,
                    Distance = Math.Sqrt(
                        Math.Pow(other.X - isolated.X, 2) +
                        Math.Pow(other.Y - isolated.Y, 2))
                })
                .OrderBy(x => x.Distance)
                .ToList();

            if (allCandidates.Count == 0)
                continue;

            var preferredCandidate = allCandidates
                .FirstOrDefault(x => x.Distance <= preferredCompletionDistance);

            var candidate = preferredCandidate ?? allCandidates.First();

            double effectiveDistance = Math.Max(0.25, candidate.Distance);
            double slope = (candidate.Cell.Elevation - isolated.Elevation) / effectiveDistance;

            var edge = new ForestEdge(isolated, candidate.Cell, effectiveDistance, slope);

            bool sameCluster = string.Equals(
                isolated.ClusterId,
                candidate.Cell.ClusterId,
                StringComparison.Ordinal);

            double completionModifier;

            if (preferredCandidate != null)
            {
                completionModifier = sameCluster ? 0.92 : 0.72;
            }
            else
            {
                completionModifier = sameCluster ? 0.62 : 0.48;
            }

            SetEdgeFireSpreadModifier(
                edge,
                Math.Clamp(edge.FireSpreadModifier * completionModifier, 0.02, 1.20));

            graph.Edges.Add(edge);

            _logger.LogInformation(
                "Blueprint soft-completion: connected isolated node ({X},{Y}) to ({NX},{NY}), distance={Distance:F2}, preferred={Preferred}",
                isolated.X,
                isolated.Y,
                candidate.Cell.X,
                candidate.Cell.Y,
                candidate.Distance,
                preferredCandidate != null);
        }
    }
    private VegetationType NormalizeBlueprintVegetation(VegetationType vegetation)
    {
        return Enum.IsDefined(typeof(VegetationType), vegetation)
            ? vegetation
            : VegetationType.Mixed;
    }
    private double NormalizeBlueprintMoisture(
        double moisture,
        SimulationParameters parameters)
    {
        double fallback = (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0;

        if (double.IsNaN(moisture) || double.IsInfinity(moisture))
            moisture = fallback;

        return Math.Clamp(moisture, 0.0, 1.0);
    }
    private double NormalizeBlueprintElevation(
        double elevation,
        SimulationParameters parameters)
    {
        if (double.IsNaN(elevation) || double.IsInfinity(elevation))
            return 0.0;

        double limit = Math.Max(10.0, parameters.ElevationVariation * 2.0);
        return Math.Clamp(elevation, -limit, limit);
    }
    private double NormalizeBlueprintFireSpreadModifier(double modifier)
    {
        if (double.IsNaN(modifier) || double.IsInfinity(modifier))
            return 1.0;

        if (modifier <= 0.0)
            return 1.0;

        return Math.Clamp(modifier, 0.02, 1.85);
    }
    private static void SetBackingField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (field != null)
            field.SetValue(target, value);
    }

    private bool TryAddEdge(ForestGraph graph, ForestCell from, ForestCell to)
    {
        if (from.Id == to.Id)
            return false;

        if (EdgeExists(graph, from, to))
            return false;

        double distance = CalculateDistance(from.X, from.Y, to.X, to.Y);
        if (distance <= 0.0001)
            return false;

        double slope = (to.Elevation - from.Elevation) / distance;
        graph.Edges.Add(new ForestEdge(from, to, distance, slope));
        return true;
    }

    private bool EdgeExists(ForestGraph graph, ForestCell a, ForestCell b)
    {
        return graph.Edges.Any(e =>
            (e.FromCellId == a.Id && e.ToCellId == b.Id) ||
            (e.FromCellId == b.Id && e.ToCellId == a.Id));
    }

    private ForestEdge? GetEdge(ForestGraph graph, ForestCell a, ForestCell b)
    {
        return graph.Edges.FirstOrDefault(e =>
            (e.FromCellId == a.Id && e.ToCellId == b.Id) ||
            (e.FromCellId == b.Id && e.ToCellId == a.Id));
    }

    private int GetNodeDegree(ForestGraph graph, ForestCell cell)
    {
        return graph.GetIncidentEdges(cell).Count;
    }

    private static double CalculateDistance(int x1, int y1, int x2, int y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private Random CreateRandom(SimulationParameters parameters)
    {
        return parameters.RandomSeed.HasValue
            ? new Random(parameters.RandomSeed.Value)
            : new Random();
    }

    private double GetRandomMoisture(double min, double max, Random random, SimulationParameters parameters)
    {
        var (effectiveMin, effectiveMax) = GetEffectiveMoistureRange(parameters);
        min = Math.Clamp(min, 0.0, 1.0);
        max = Math.Clamp(max, 0.0, 1.0);

        if (max < min)
            (min, max) = (max, min);

        min = Math.Max(min, effectiveMin);
        max = Math.Min(max, effectiveMax);

        if (max < min)
            max = min;

        return min + random.NextDouble() * Math.Max(0.0001, max - min);
    }

    private (double Min, double Max) GetEffectiveMoistureRange(SimulationParameters parameters)
    {
        double drynessFactor = Math.Clamp(parameters.MapDrynessFactor, 0.4, 1.8);

        double min = Math.Clamp(parameters.InitialMoistureMin / drynessFactor, 0.02, 0.95);
        double max = Math.Clamp(parameters.InitialMoistureMax / drynessFactor, min, 0.98);

        return (min, max);
    }

    private double GetRandomElevation(double variation, Random random, SimulationParameters parameters)
    {
        double effective = GetEffectiveElevationVariation(variation, parameters);
        return (random.NextDouble() * 2.0 - 1.0) * effective;
    }

    private double GetEffectiveElevationVariation(double variation, SimulationParameters parameters)
    {
        return Math.Max(1.0, variation * Math.Clamp(parameters.ReliefStrengthFactor, 0.35, 2.2));
    }

    private double GetNormalizedFuelDensity(SimulationParameters parameters)
    {
        return Math.Clamp(parameters.FuelDensityFactor, 0.35, 2.2);
    }

    private double AdjustFuelWeightForDensity(VegetationType type, double baseWeight, double densityFactor)
    {
        if (type == VegetationType.Water || type == VegetationType.Bare)
            return baseWeight;

        double multiplier = type switch
        {
            VegetationType.Coniferous => 1.0 + (densityFactor - 1.0) * 0.35,
            VegetationType.Mixed => 1.0 + (densityFactor - 1.0) * 0.25,
            VegetationType.Deciduous => 1.0 + (densityFactor - 1.0) * 0.15,
            VegetationType.Shrub => 1.0 + (densityFactor - 1.0) * 0.12,
            VegetationType.Grass => 1.0 + (densityFactor - 1.0) * 0.08,
            _ => 1.0
        };

        return Math.Max(0.0, baseWeight * Math.Clamp(multiplier, 0.45, 1.85));
    }

    private VegetationType[,] BuildGridVegetationMap(
        int width,
        int height,
        SimulationParameters parameters,
        Random random)
    {
        var map = new VegetationType[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                map[x, y] = GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters);
        }

        ApplyConnectedSurfaceZones(map, width, height, parameters.VegetationDistributions, VegetationType.Water, random);
        ApplyConnectedSurfaceZones(map, width, height, parameters.VegetationDistributions, VegetationType.Bare, random);

        return map;
    }

    private void ApplyConnectedSurfaceZones(
        VegetationType[,] map,
        int width,
        int height,
        List<VegetationDistribution> distributions,
        VegetationType targetType,
        Random random)
    {
        int totalCells = width * height;
        if (totalCells <= 0)
            return;

        double targetProbability = GetVegetationProbability(distributions, targetType);
        if (targetProbability <= 0.0)
            return;

        int targetCount = (int)Math.Round(totalCells * targetProbability);
        if (targetCount <= 0)
            return;

        int painted = 0;

        if (targetType == VegetationType.Water &&
            width >= 12 &&
            height >= 12 &&
            targetCount >= Math.Max(8, totalCells / 18))
        {
            painted += TryPaintWaterBarrier(map, width, height, targetCount, random);
        }

        int remaining = targetCount - painted;
        if (remaining <= 0)
            return;

        int zoneCount = EstimateGridSurfaceZoneCount(remaining, targetType);

        var seeds = GetAllGridPoints(width, height)
            .Where(p => map[p.X, p.Y] != VegetationType.Water && map[p.X, p.Y] != VegetationType.Bare)
            .OrderBy(_ => random.Next())
            .Take(zoneCount)
            .ToList();

        int zonesLeft = seeds.Count;

        foreach (var seed in seeds)
        {
            if (remaining <= 0)
                break;

            int zoneTarget = Math.Max(1, (int)Math.Ceiling((double)remaining / Math.Max(1, zonesLeft)));

            int zonePainted = PaintConnectedSurfaceZone(
                map,
                width,
                height,
                seed.X,
                seed.Y,
                zoneTarget,
                targetType,
                random);

            painted += zonePainted;
            remaining = targetCount - painted;
            zonesLeft--;
        }
    }

    private int TryPaintWaterBarrier(
        VegetationType[,] map,
        int width,
        int height,
        int targetCount,
        Random random)
    {
        bool vertical = width >= height;
        int painted = 0;

        if (vertical)
        {
            int centerX = (int)Math.Round(width * (0.32 + random.NextDouble() * 0.36));
            int thickness = Math.Max(1, Math.Min(2, width / 20));

            for (int y = 0; y < height && painted < targetCount; y++)
            {
                for (int dx = -thickness; dx <= thickness && painted < targetCount; dx++)
                {
                    int x = centerX + dx;
                    if (x < 0 || x >= width)
                        continue;

                    if (map[x, y] == VegetationType.Water)
                        continue;

                    map[x, y] = VegetationType.Water;
                    painted++;
                }
            }
        }
        else
        {
            int centerY = (int)Math.Round(height * (0.32 + random.NextDouble() * 0.36));
            int thickness = Math.Max(1, Math.Min(2, height / 20));

            for (int x = 0; x < width && painted < targetCount; x++)
            {
                for (int dy = -thickness; dy <= thickness && painted < targetCount; dy++)
                {
                    int y = centerY + dy;
                    if (y < 0 || y >= height)
                        continue;

                    if (map[x, y] == VegetationType.Water)
                        continue;

                    map[x, y] = VegetationType.Water;
                    painted++;
                }
            }
        }

        return painted;
    }

    private int EstimateGridSurfaceZoneCount(int remaining, VegetationType targetType)
    {
        return targetType switch
        {
            VegetationType.Water => Math.Max(1, remaining / 20),
            VegetationType.Bare => Math.Max(1, remaining / 16),
            _ => Math.Max(1, remaining / 24)
        };
    }

    private int PaintConnectedSurfaceZone(
        VegetationType[,] map,
        int width,
        int height,
        int startX,
        int startY,
        int zoneTarget,
        VegetationType targetType,
        Random random)
    {
        if (zoneTarget <= 0)
            return 0;

        var queue = new Queue<(int X, int Y)>();
        var visited = new HashSet<(int X, int Y)>();
        int painted = 0;

        queue.Enqueue((startX, startY));
        visited.Add((startX, startY));

        while (queue.Count > 0 && painted < zoneTarget)
        {
            var point = queue.Dequeue();

            if (map[point.X, point.Y] != VegetationType.Water && map[point.X, point.Y] != VegetationType.Bare)
            {
                map[point.X, point.Y] = targetType;
                painted++;
            }

            foreach (var neighbor in GetGridNeighbors8(point.X, point.Y, width, height).OrderBy(_ => random.Next()))
            {
                if (visited.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        return painted;
    }

    private void AddDryPatches(
     int width,
     int height,
     double[,] moistureMap,
     Random random,
     double intensity,
     int patchCount)
    {
        for (int i = 0; i < patchCount; i++)
        {
            double centerX = random.NextDouble() * Math.Max(1, width - 1);
            double centerY = random.NextDouble() * Math.Max(1, height - 1);

            double radiusX = Math.Max(2.0, width * (0.10 + random.NextDouble() * 0.08));
            double radiusY = Math.Max(2.0, height * (0.10 + random.NextDouble() * 0.08));
            double rotation = random.NextDouble() * Math.PI;

            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double dx = x - centerX;
                    double dy = y - centerY;

                    double localX = dx * cos + dy * sin;
                    double localY = -dx * sin + dy * cos;

                    double normalized =
                        (localX * localX) / (radiusX * radiusX) +
                        (localY * localY) / (radiusY * radiusY);

                    if (normalized > 2.8)
                        continue;

                    double falloff = Math.Exp(-normalized * 1.15);
                    moistureMap[x, y] -= intensity * falloff;
                }
            }
        }
    }

    private void AddWetPatches(
      int width,
      int height,
      double[,] moistureMap,
      Random random,
      double intensity,
      int patchCount)
    {
        for (int i = 0; i < patchCount; i++)
        {
            double centerX = random.NextDouble() * Math.Max(1, width - 1);
            double centerY = random.NextDouble() * Math.Max(1, height - 1);

            double radiusX = Math.Max(2.2, width * (0.11 + random.NextDouble() * 0.10));
            double radiusY = Math.Max(2.2, height * (0.11 + random.NextDouble() * 0.10));
            double rotation = random.NextDouble() * Math.PI;

            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double dx = x - centerX;
                    double dy = y - centerY;

                    double localX = dx * cos + dy * sin;
                    double localY = -dx * sin + dy * cos;

                    double normalized =
                        (localX * localX) / (radiusX * radiusX) +
                        (localY * localY) / (radiusY * radiusY);

                    if (normalized > 3.0)
                        continue;

                    double falloff = Math.Exp(-normalized * 1.05);
                    moistureMap[x, y] += intensity * falloff;
                }
            }
        }
    }

    private void PaintRiver(
     int width,
     int height,
     VegetationType[,] vegetationMap,
     double[,] moistureMap,
     double[,] elevationMap,
     Random random,
     SimulationParameters parameters)
    {
        int minDimension = Math.Min(width, height);
        bool vertical = width >= height;

        int waterHalfWidth = minDimension <= 24 ? 0 : minDimension <= 44 ? 1 : 2;
        int riparianWidth = minDimension <= 24 ? 2 : minDimension <= 44 ? 3 : 4;

        double baseAmplitude = Math.Max(1.1, minDimension * 0.07);
        double secondaryAmplitude = Math.Max(0.6, minDimension * 0.03);
        double phase1 = random.NextDouble() * Math.PI * 2.0;
        double phase2 = random.NextDouble() * Math.PI * 2.0;

        if (vertical)
        {
            double centerX = width * (0.32 + random.NextDouble() * 0.22);

            for (int y = 0; y < height; y++)
            {
                double t = height <= 1 ? 0.0 : (double)y / (height - 1);

                double drift =
                    Math.Sin(t * Math.PI * 1.55 + phase1) * baseAmplitude +
                    Math.Sin(t * Math.PI * 3.30 + phase2) * secondaryAmplitude;

                int riverX = (int)Math.Round(centerX + drift);

                for (int dx = -(riparianWidth + 1); dx <= riparianWidth + 1; dx++)
                {
                    int x = riverX + dx;
                    if (x < 0 || x >= width)
                        continue;

                    int absDx = Math.Abs(dx);

                    if (absDx <= waterHalfWidth)
                    {
                        vegetationMap[x, y] = VegetationType.Water;
                        moistureMap[x, y] = 1.0;
                        elevationMap[x, y] -= 10.0 + (waterHalfWidth - absDx + 1) * 1.8;
                        continue;
                    }

                    double bankDistance = absDx - waterHalfWidth;
                    if (bankDistance > riparianWidth + 0.5)
                        continue;

                    double bankFactor = 1.0 - (bankDistance - 1.0) / Math.Max(1.0, riparianWidth);
                    bankFactor = Math.Clamp(bankFactor, 0.0, 1.0);

                    moistureMap[x, y] = ClampMoisture(moistureMap[x, y] + 0.20 * bankFactor, parameters);
                    elevationMap[x, y] -= 3.5 * bankFactor;

                    if (bankFactor >= 0.68)
                    {
                        if (vegetationMap[x, y] == VegetationType.Coniferous)
                            vegetationMap[x, y] = VegetationType.Deciduous;
                        else if (vegetationMap[x, y] == VegetationType.Grass)
                            vegetationMap[x, y] = VegetationType.Shrub;
                    }
                }
            }
        }
        else
        {
            double centerY = height * (0.32 + random.NextDouble() * 0.22);

            for (int x = 0; x < width; x++)
            {
                double t = width <= 1 ? 0.0 : (double)x / (width - 1);

                double drift =
                    Math.Sin(t * Math.PI * 1.55 + phase1) * baseAmplitude +
                    Math.Sin(t * Math.PI * 3.30 + phase2) * secondaryAmplitude;

                int riverY = (int)Math.Round(centerY + drift);

                for (int dy = -(riparianWidth + 1); dy <= riparianWidth + 1; dy++)
                {
                    int y = riverY + dy;
                    if (y < 0 || y >= height)
                        continue;

                    int absDy = Math.Abs(dy);

                    if (absDy <= waterHalfWidth)
                    {
                        vegetationMap[x, y] = VegetationType.Water;
                        moistureMap[x, y] = 1.0;
                        elevationMap[x, y] -= 10.0 + (waterHalfWidth - absDy + 1) * 1.8;
                        continue;
                    }

                    double bankDistance = absDy - waterHalfWidth;
                    if (bankDistance > riparianWidth + 0.5)
                        continue;

                    double bankFactor = 1.0 - (bankDistance - 1.0) / Math.Max(1.0, riparianWidth);
                    bankFactor = Math.Clamp(bankFactor, 0.0, 1.0);

                    moistureMap[x, y] = ClampMoisture(moistureMap[x, y] + 0.20 * bankFactor, parameters);
                    elevationMap[x, y] -= 3.5 * bankFactor;

                    if (bankFactor >= 0.68)
                    {
                        if (vegetationMap[x, y] == VegetationType.Coniferous)
                            vegetationMap[x, y] = VegetationType.Deciduous;
                        else if (vegetationMap[x, y] == VegetationType.Grass)
                            vegetationMap[x, y] = VegetationType.Shrub;
                    }
                }
            }
        }
    }
    private void PaintLake(
     int width,
     int height,
     VegetationType[,] vegetationMap,
     double[,] moistureMap,
     double[,] elevationMap,
     Random random,
     SimulationParameters parameters)
    {
        double centerX = width * (0.34 + random.NextDouble() * 0.22);
        double centerY = height * (0.34 + random.NextDouble() * 0.22);

        double radiusX = Math.Max(2.3, width * (0.11 + random.NextDouble() * 0.06));
        double radiusY = Math.Max(2.3, height * (0.11 + random.NextDouble() * 0.06));

        double phase1 = random.NextDouble() * Math.PI * 2.0;
        double phase2 = random.NextDouble() * Math.PI * 2.0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double dx = x - centerX;
                double dy = y - centerY;

                double angle = Math.Atan2(dy, dx);
                double shorelineNoise =
                    1.0
                    + 0.10 * Math.Sin(angle * 3.0 + phase1)
                    + 0.06 * Math.Sin(angle * 5.0 + phase2);

                double nx = dx / (radiusX * shorelineNoise);
                double ny = dy / (radiusY * shorelineNoise);
                double distance = nx * nx + ny * ny;

                if (distance <= 1.0)
                {
                    vegetationMap[x, y] = VegetationType.Water;
                    moistureMap[x, y] = 1.0;
                    elevationMap[x, y] -= 11.5 * (1.10 - Math.Min(distance, 1.0));
                    continue;
                }

                if (distance <= 1.70)
                {
                    double shoreFactor = Math.Clamp(1.70 - distance, 0.0, 0.70) / 0.70;

                    moistureMap[x, y] = ClampMoisture(moistureMap[x, y] + 0.24 * shoreFactor, parameters);
                    elevationMap[x, y] -= 4.5 * shoreFactor;

                    if (shoreFactor >= 0.60)
                    {
                        if (vegetationMap[x, y] == VegetationType.Coniferous)
                            vegetationMap[x, y] = VegetationType.Deciduous;
                        else if (vegetationMap[x, y] == VegetationType.Grass)
                            vegetationMap[x, y] = VegetationType.Shrub;
                    }
                }
            }
        }
    }
    private void PaintFirebreak(
     int width,
     int height,
     VegetationType[,] vegetationMap,
     double[,] moistureMap,
     Random random,
     SimulationParameters parameters)
    {
        int minDimension = Math.Min(width, height);
        bool vertical = width >= height;

        int bareHalfWidth = minDimension <= 24 ? 0 : minDimension <= 48 ? 1 : 1;
        int shoulderWidth = minDimension <= 24 ? 1 : 2;

        if (vertical)
        {
            int centerX = (int)Math.Round(width * (0.36 + random.NextDouble() * 0.18));

            for (int x = centerX - (bareHalfWidth + shoulderWidth); x <= centerX + (bareHalfWidth + shoulderWidth); x++)
            {
                if (x < 0 || x >= width)
                    continue;

                int distance = Math.Abs(x - centerX);

                for (int y = 0; y < height; y++)
                {
                    if (distance <= bareHalfWidth)
                    {
                        vegetationMap[x, y] = VegetationType.Bare;
                        moistureMap[x, y] = Math.Min(moistureMap[x, y], 0.10);
                    }
                    else
                    {
                        if (vegetationMap[x, y] != VegetationType.Water)
                        {
                            vegetationMap[x, y] = distance == bareHalfWidth + 1
                                ? VegetationType.Grass
                                : VegetationType.Shrub;

                            moistureMap[x, y] = ClampMoisture(
                                Math.Min(moistureMap[x, y], 0.22) - 0.02,
                                parameters);
                        }
                    }
                }
            }
        }
        else
        {
            int centerY = (int)Math.Round(height * (0.36 + random.NextDouble() * 0.18));

            for (int y = centerY - (bareHalfWidth + shoulderWidth); y <= centerY + (bareHalfWidth + shoulderWidth); y++)
            {
                if (y < 0 || y >= height)
                    continue;

                int distance = Math.Abs(y - centerY);

                for (int x = 0; x < width; x++)
                {
                    if (distance <= bareHalfWidth)
                    {
                        vegetationMap[x, y] = VegetationType.Bare;
                        moistureMap[x, y] = Math.Min(moistureMap[x, y], 0.10);
                    }
                    else
                    {
                        if (vegetationMap[x, y] != VegetationType.Water)
                        {
                            vegetationMap[x, y] = distance == bareHalfWidth + 1
                                ? VegetationType.Grass
                                : VegetationType.Shrub;

                            moistureMap[x, y] = ClampMoisture(
                                Math.Min(moistureMap[x, y], 0.22) - 0.02,
                                parameters);
                        }
                    }
                }
            }
        }
    }

    private void ApplyMapObject(
        int width,
        int height,
        SimulationParameters parameters,
        VegetationType[,] vegetationMap,
        double[,] moistureMap,
        double[,] elevationMap,
        MapRegionObject mapObject)
    {
        if (mapObject.Width <= 0 || mapObject.Height <= 0)
            return;

        double effectiveElevationVariation = GetEffectiveElevationVariation(parameters.ElevationVariation, parameters);
        double baseElevationDelta = Math.Max(4.0, effectiveElevationVariation * 0.55 * Math.Max(0.1, mapObject.Strength));
        double moistureDelta = 0.22 * Math.Max(0.1, mapObject.Strength);

        ForEachObjectCell(width, height, mapObject, (x, y, influence) =>
        {
            switch (mapObject.ObjectType)
            {
                case MapObjectType.ConiferousArea:
                    vegetationMap[x, y] = VegetationType.Coniferous;
                    break;
                case MapObjectType.DeciduousArea:
                    vegetationMap[x, y] = VegetationType.Deciduous;
                    break;
                case MapObjectType.MixedForestArea:
                    vegetationMap[x, y] = VegetationType.Mixed;
                    break;
                case MapObjectType.GrassArea:
                    vegetationMap[x, y] = VegetationType.Grass;
                    break;
                case MapObjectType.ShrubArea:
                    vegetationMap[x, y] = VegetationType.Shrub;
                    break;
                case MapObjectType.WaterBody:
                    vegetationMap[x, y] = VegetationType.Water;
                    elevationMap[x, y] -= 10.0 * influence;
                    moistureMap[x, y] = Math.Max(moistureMap[x, y], 0.92);
                    break;
                case MapObjectType.Firebreak:
                    vegetationMap[x, y] = VegetationType.Bare;
                    moistureMap[x, y] = Math.Min(moistureMap[x, y], 0.18);
                    break;
                case MapObjectType.WetZone:
                    moistureMap[x, y] += moistureDelta * influence;
                    break;
                case MapObjectType.DryZone:
                    moistureMap[x, y] -= moistureDelta * influence;
                    break;
                case MapObjectType.Hill:
                    elevationMap[x, y] += baseElevationDelta * influence;
                    break;
                case MapObjectType.Lowland:
                    elevationMap[x, y] -= baseElevationDelta * influence;
                    moistureMap[x, y] += 0.06 * influence;
                    break;
            }
        });

        ClampMaps(width, height, vegetationMap, moistureMap, elevationMap, parameters);
    }

    private void ForEachObjectCell(
        int width,
        int height,
        MapRegionObject mapObject,
        Action<int, int, double> action)
    {
        int startX = Math.Max(0, mapObject.StartX);
        int startY = Math.Max(0, mapObject.StartY);
        int endX = Math.Min(width - 1, mapObject.StartX + mapObject.Width - 1);
        int endY = Math.Min(height - 1, mapObject.StartY + mapObject.Height - 1);

        if (endX < startX || endY < startY)
            return;

        double centerX = mapObject.StartX + (mapObject.Width - 1) / 2.0;
        double centerY = mapObject.StartY + (mapObject.Height - 1) / 2.0;
        double halfWidth = Math.Max(1.0, mapObject.Width / 2.0);
        double halfHeight = Math.Max(1.0, mapObject.Height / 2.0);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                double influence;


                double dx = Math.Abs(x - centerX) / halfWidth;
                double dy = Math.Abs(y - centerY) / halfHeight;
                double edge = Math.Max(dx, dy);
                influence = Math.Clamp(1.0 - edge * 0.75, 0.20, 1.0);


                action(x, y, influence);
            }
        }
    }

    private void ApplyWaterAdjacencyEffects(
        int width,
        int height,
        VegetationType[,] vegetationMap,
        double[,] moistureMap,
        double[,] elevationMap,
        SimulationParameters parameters)
    {
        var originalMoisture = (double[,])moistureMap.Clone();
        var originalElevation = (double[,])elevationMap.Clone();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (vegetationMap[x, y] == VegetationType.Water)
                {
                    moistureMap[x, y] = 1.0;
                    continue;
                }

                int nearestWaterDistance = FindNearestVegetationDistance(
                    x, y, width, height, vegetationMap, VegetationType.Water, 5);

                if (nearestWaterDistance == int.MaxValue)
                    continue;

                double factor = nearestWaterDistance switch
                {
                    1 => 0.20,
                    2 => 0.13,
                    3 => 0.08,
                    4 => 0.05,
                    5 => 0.02,
                    _ => 0.0
                };

                moistureMap[x, y] = ClampMoisture(originalMoisture[x, y] + factor, parameters);
                elevationMap[x, y] = originalElevation[x, y] - factor * 7.5;

                if (factor >= 0.12 && vegetationMap[x, y] == VegetationType.Coniferous)
                    vegetationMap[x, y] = VegetationType.Deciduous;
            }
        }
    }

    private int FindNearestVegetationDistance(
        int startX,
        int startY,
        int width,
        int height,
        VegetationType[,] vegetationMap,
        VegetationType targetType,
        int maxDistance)
    {
        for (int distance = 1; distance <= maxDistance; distance++)
        {
            for (int dx = -distance; dx <= distance; dx++)
            {
                for (int dy = -distance; dy <= distance; dy++)
                {
                    int x = startX + dx;
                    int y = startY + dy;

                    if (x < 0 || x >= width || y < 0 || y >= height)
                        continue;

                    if (Math.Abs(dx) + Math.Abs(dy) > maxDistance + 1)
                        continue;

                    if (vegetationMap[x, y] == targetType)
                        return distance;
                }
            }
        }

        return int.MaxValue;
    }

    private void ApplyTerrainNoise(
      int width,
      int height,
      VegetationType[,] vegetationMap,
      double[,] moistureMap,
      double[,] elevationMap,
      SimulationParameters parameters,
      Random random)
    {
        double noiseStrength = Math.Clamp(parameters.MapNoiseStrength, 0.0, 1.0);

        if (noiseStrength <= 0.0001)
        {
            ClampMaps(width, height, vegetationMap, moistureMap, elevationMap, parameters);
            return;
        }

        double elevationAmplitude = Math.Max(
            0.8,
            GetEffectiveElevationVariation(parameters.ElevationVariation, parameters) * 0.06 * noiseStrength);

        double moistureAmplitude = Math.Max(0.01, 0.05 * noiseStrength);

        var elevationNoise = GenerateSmoothField(
            width,
            height,
            random,
            centerValue: 0.0,
            amplitude: elevationAmplitude,
            coarseDivisor: 8.0);

        var moistureNoise = GenerateSmoothField(
            width,
            height,
            random,
            centerValue: 0.0,
            amplitude: moistureAmplitude,
            coarseDivisor: 8.5);

        SmoothDoubleMap(width, height, elevationNoise, iterations: 1, preserveWater: false, vegetationMap: null);
        SmoothDoubleMap(width, height, moistureNoise, iterations: 1, preserveWater: false, vegetationMap: null);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (vegetationMap[x, y] == VegetationType.Water)
                    continue;

                elevationMap[x, y] += elevationNoise[x, y];
                moistureMap[x, y] += moistureNoise[x, y];
            }
        }

        ClampMaps(width, height, vegetationMap, moistureMap, elevationMap, parameters);
    }
    private double[,] GenerateSmoothField(
    int width,
    int height,
    Random random,
    double centerValue,
    double amplitude,
    double coarseDivisor)
    {
        int coarseWidth = Math.Max(3, (int)Math.Round(width / coarseDivisor) + 2);
        int coarseHeight = Math.Max(3, (int)Math.Round(height / coarseDivisor) + 2);

        var coarse = new double[coarseWidth, coarseHeight];
        for (int x = 0; x < coarseWidth; x++)
        {
            for (int y = 0; y < coarseHeight; y++)
            {
                coarse[x, y] = centerValue + (random.NextDouble() * 2.0 - 1.0) * amplitude;
            }
        }

        var result = new double[width, height];

        for (int x = 0; x < width; x++)
        {
            double gx = width <= 1 ? 0.0 : (double)x / Math.Max(1, width - 1) * (coarseWidth - 1);
            int x0 = Math.Clamp((int)Math.Floor(gx), 0, coarseWidth - 1);
            int x1 = Math.Clamp(x0 + 1, 0, coarseWidth - 1);
            double tx = gx - x0;

            for (int y = 0; y < height; y++)
            {
                double gy = height <= 1 ? 0.0 : (double)y / Math.Max(1, height - 1) * (coarseHeight - 1);
                int y0 = Math.Clamp((int)Math.Floor(gy), 0, coarseHeight - 1);
                int y1 = Math.Clamp(y0 + 1, 0, coarseHeight - 1);
                double ty = gy - y0;

                double v00 = coarse[x0, y0];
                double v10 = coarse[x1, y0];
                double v01 = coarse[x0, y1];
                double v11 = coarse[x1, y1];

                double top = v00 + (v10 - v00) * tx;
                double bottom = v01 + (v11 - v01) * tx;

                result[x, y] = top + (bottom - top) * ty;
            }
        }

        return result;
    }

    private void AddDirectionalElevationGradient(
        int width,
        int height,
        double[,] elevationMap,
        Random random,
        double amplitude)
    {
        double angle = random.NextDouble() * Math.PI * 2.0;
        double dirX = Math.Cos(angle);
        double dirY = Math.Sin(angle);

        double centerX = (width - 1) / 2.0;
        double centerY = (height - 1) / 2.0;
        double maxDistance = Math.Max(1.0, Math.Sqrt(centerX * centerX + centerY * centerY));

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double px = x - centerX;
                double py = y - centerY;
                double projection = (px * dirX + py * dirY) / maxDistance;
                elevationMap[x, y] += projection * amplitude;
            }
        }
    }

    private void AddDirectionalMoistureGradient(
        int width,
        int height,
        double[,] moistureMap,
        Random random,
        double amplitude)
    {
        double angle = random.NextDouble() * Math.PI * 2.0;
        double dirX = Math.Cos(angle);
        double dirY = Math.Sin(angle);

        double centerX = (width - 1) / 2.0;
        double centerY = (height - 1) / 2.0;
        double maxDistance = Math.Max(1.0, Math.Sqrt(centerX * centerX + centerY * centerY));

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double px = x - centerX;
                double py = y - centerY;
                double projection = (px * dirX + py * dirY) / maxDistance;
                moistureMap[x, y] += projection * amplitude;
            }
        }
    }

    private void SmoothMaps(
        int width,
        int height,
        VegetationType[,] vegetationMap,
        double[,] moistureMap,
        double[,] elevationMap,
        SimulationParameters parameters,
        int elevationIterations,
        int moistureIterations)
    {
        SmoothDoubleMap(width, height, elevationMap, elevationIterations, preserveWater: false, vegetationMap);
        SmoothDoubleMap(width, height, moistureMap, moistureIterations, preserveWater: true, vegetationMap);
        ClampMaps(width, height, vegetationMap, moistureMap, elevationMap, parameters);
    }

    private void SmoothDoubleMap(
        int width,
        int height,
        double[,] map,
        int iterations,
        bool preserveWater,
        VegetationType[,]? vegetationMap)
    {
        iterations = Math.Max(0, iterations);
        if (iterations == 0)
            return;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var copy = (double[,])map.Clone();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (preserveWater &&
                        vegetationMap != null &&
                        vegetationMap[x, y] == VegetationType.Water)
                    {
                        continue;
                    }

                    double weightedSum = copy[x, y] * 4.0;
                    double totalWeight = 4.0;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                                continue;

                            double weight = (dx == 0 || dy == 0) ? 1.0 : 0.7;
                            weightedSum += copy[nx, ny] * weight;
                            totalWeight += weight;
                        }
                    }

                    map[x, y] = weightedSum / totalWeight;
                }
            }
        }
    }

    private void HarmonizeTerrainAndMoisture(
        int width,
        int height,
        VegetationType[,] vegetationMap,
        double[,] moistureMap,
        double[,] elevationMap,
        SimulationParameters parameters)
    {
        var elevationCopy = (double[,])elevationMap.Clone();
        var moistureCopy = (double[,])moistureMap.Clone();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var vegetation = vegetationMap[x, y];

                if (vegetation == VegetationType.Water)
                {
                    moistureMap[x, y] = 1.0;
                    continue;
                }

                var neighbors = GetGridNeighborPoints(x, y, width, height).ToList();
                if (neighbors.Count == 0)
                    continue;

                double similarVegetationElevation = 0.0;
                double similarVegetationMoisture = 0.0;
                double similarWeight = 0.0;

                double generalElevation = 0.0;
                double generalMoisture = 0.0;
                double generalWeight = 0.0;

                foreach (var (nx, ny) in neighbors)
                {
                    double weight = (nx == x || ny == y) ? 1.0 : 0.6;
                    generalElevation += elevationCopy[nx, ny] * weight;
                    generalMoisture += moistureCopy[nx, ny] * weight;
                    generalWeight += weight;

                    if (vegetationMap[nx, ny] == vegetation)
                    {
                        similarVegetationElevation += elevationCopy[nx, ny] * weight;
                        similarVegetationMoisture += moistureCopy[nx, ny] * weight;
                        similarWeight += weight;
                    }
                }

                double neighborElevationMean = generalWeight > 0.0 ? generalElevation / generalWeight : elevationCopy[x, y];
                double neighborMoistureMean = generalWeight > 0.0 ? generalMoisture / generalWeight : moistureCopy[x, y];

                if (similarWeight > 0.0)
                {
                    double sameVegElevationMean = similarVegetationElevation / similarWeight;
                    double sameVegMoistureMean = similarVegetationMoisture / similarWeight;

                    elevationMap[x, y] = elevationCopy[x, y] * 0.72 + sameVegElevationMean * 0.18 + neighborElevationMean * 0.10;
                    moistureMap[x, y] = moistureCopy[x, y] * 0.68 + sameVegMoistureMean * 0.20 + neighborMoistureMean * 0.12;
                }
                else
                {
                    elevationMap[x, y] = elevationCopy[x, y] * 0.78 + neighborElevationMean * 0.22;
                    moistureMap[x, y] = moistureCopy[x, y] * 0.76 + neighborMoistureMean * 0.24;
                }

                moistureMap[x, y] = ClampMoisture(moistureMap[x, y], parameters);
            }
        }
    }

    private IEnumerable<(int X, int Y)> GetGridNeighborPoints(int x, int y, int width, int height)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    continue;

                yield return (nx, ny);
            }
        }
    }

    private double GetVegetationMoistureBias(VegetationType vegetation)
    {
        return vegetation switch
        {
            VegetationType.Coniferous => -0.04,
            VegetationType.Grass => -0.02,
            VegetationType.Shrub => -0.01,
            VegetationType.Deciduous => 0.03,
            VegetationType.Mixed => 0.01,
            VegetationType.Bare => -0.08,
            VegetationType.Water => 0.40,
            _ => 0.0
        };
    }

    private void AddOrientedHillFeature(
     int width,
     int height,
     double[,] elevationMap,
     SimulationParameters parameters,
     Random random,
     double centerX,
     double centerY,
     double strength,
     double radiusXFactor,
     double radiusYFactor)
    {
        double effectiveElevationVariation = GetEffectiveElevationVariation(parameters.ElevationVariation, parameters);
        double radiusX = Math.Max(2.5, width * radiusXFactor);
        double radiusY = Math.Max(2.5, height * radiusYFactor);
        double amplitude = Math.Max(6.0, effectiveElevationVariation * 0.68 * strength);
        double rotation = random.NextDouble() * Math.PI;

        double cos = Math.Cos(rotation);
        double sin = Math.Sin(rotation);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double dx = x - centerX;
                double dy = y - centerY;

                double localX = dx * cos + dy * sin;
                double localY = -dx * sin + dy * cos;

                double normalized =
                    (localX * localX) / (radiusX * radiusX) +
                    (localY * localY) / (radiusY * radiusY);

                if (normalized > 3.6)
                    continue;

                double falloff = Math.Exp(-normalized * 0.92);
                elevationMap[x, y] += amplitude * falloff;
            }
        }
    }

    private void AddOrientedValleyFeature(
     int width,
     int height,
     double[,] elevationMap,
     SimulationParameters parameters,
     Random random,
     double centerX,
     double centerY,
     double strength,
     double radiusXFactor,
     double radiusYFactor)
    {
        double effectiveElevationVariation = GetEffectiveElevationVariation(parameters.ElevationVariation, parameters);
        double radiusX = Math.Max(2.5, width * radiusXFactor);
        double radiusY = Math.Max(2.5, height * radiusYFactor);
        double amplitude = Math.Max(5.0, effectiveElevationVariation * 0.55 * strength);
        double rotation = random.NextDouble() * Math.PI;

        double cos = Math.Cos(rotation);
        double sin = Math.Sin(rotation);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double dx = x - centerX;
                double dy = y - centerY;

                double localX = dx * cos + dy * sin;
                double localY = -dx * sin + dy * cos;

                double normalized =
                    (localX * localX) / (radiusX * radiusX) +
                    (localY * localY) / (radiusY * radiusY);

                if (normalized > 3.8)
                    continue;

                double falloff = Math.Exp(-normalized * 0.90);
                elevationMap[x, y] -= amplitude * falloff;
            }
        }
    }
    private void NormalizeElevation(
     int width,
     int height,
     double[,] elevationMap,
     double targetMin,
     double targetMax)
    {
        double currentMin = double.MaxValue;
        double currentMax = double.MinValue;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double value = elevationMap[x, y];
                if (value < currentMin)
                    currentMin = value;

                if (value > currentMax)
                    currentMax = value;
            }
        }

        double currentRange = currentMax - currentMin;
        if (currentRange < 0.0001)
        {
            double fallback = (targetMin + targetMax) / 2.0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    elevationMap[x, y] = fallback;
            }

            return;
        }

        double targetRange = targetMax - targetMin;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double normalized = (elevationMap[x, y] - currentMin) / currentRange;
                elevationMap[x, y] = targetMin + normalized * targetRange;
            }
        }
    }

    private void AddRidgeSystem(
     int width,
     int height,
     double[,] elevationMap,
     SimulationParameters parameters,
     Random random)
    {
        int ridgeCount = Math.Clamp((int)Math.Round(Math.Max(width, height) / 12.0), 3, 5);

        for (int i = 0; i < ridgeCount; i++)
        {
            double centerX = width * (0.16 + random.NextDouble() * 0.68);
            double centerY = height * (0.16 + random.NextDouble() * 0.68);

            bool elongatedHorizontally = random.NextDouble() < 0.5;

            double radiusXFactor;
            double radiusYFactor;

            if (elongatedHorizontally)
            {
                radiusXFactor = 0.22 + random.NextDouble() * 0.12;
                radiusYFactor = 0.06 + random.NextDouble() * 0.05;
            }
            else
            {
                radiusXFactor = 0.06 + random.NextDouble() * 0.05;
                radiusYFactor = 0.22 + random.NextDouble() * 0.12;
            }

            AddOrientedHillFeature(
                width,
                height,
                elevationMap,
                parameters,
                random,
                centerX,
                centerY,
                strength: 0.95 + random.NextDouble() * 0.55,
                radiusXFactor,
                radiusYFactor);
        }
    }
    private void AddValleySystem(
      int width,
      int height,
      double[,] elevationMap,
      SimulationParameters parameters,
      Random random)
    {
        int valleyCount = Math.Clamp((int)Math.Round(Math.Max(width, height) / 16.0), 2, 4);

        for (int i = 0; i < valleyCount; i++)
        {
            double centerX = width * (0.18 + random.NextDouble() * 0.64);
            double centerY = height * (0.18 + random.NextDouble() * 0.64);

            bool elongatedHorizontally = random.NextDouble() < 0.5;

            double radiusXFactor;
            double radiusYFactor;

            if (elongatedHorizontally)
            {
                radiusXFactor = 0.20 + random.NextDouble() * 0.12;
                radiusYFactor = 0.06 + random.NextDouble() * 0.05;
            }
            else
            {
                radiusXFactor = 0.06 + random.NextDouble() * 0.05;
                radiusYFactor = 0.20 + random.NextDouble() * 0.12;
            }

            AddOrientedValleyFeature(
                width,
                height,
                elevationMap,
                parameters,
                random,
                centerX,
                centerY,
                strength: 0.85 + random.NextDouble() * 0.40,
                radiusXFactor,
                radiusYFactor);
        }
    }

    private void RebalanceVegetationByTerrain(
        int width,
        int height,
        VegetationType[,] vegetationMap,
        double[,] moistureMap,
        double[,] elevationMap,
        SimulationParameters parameters,
        Random random)
    {
        if (!TryGetMapMinMax(width, height, elevationMap, out var minElevation, out var maxElevation))
            return;

        double span = Math.Max(1.0, maxElevation - minElevation);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var current = vegetationMap[x, y];
                if (current == VegetationType.Water || current == VegetationType.Bare)
                    continue;

                double normalizedElevation = (elevationMap[x, y] - minElevation) / span;
                double moisture = moistureMap[x, y];

                if (normalizedElevation >= 0.72 && moisture <= 0.30 && random.NextDouble() < 0.35)
                {
                    vegetationMap[x, y] = current switch
                    {
                        VegetationType.Deciduous => VegetationType.Mixed,
                        VegetationType.Mixed => VegetationType.Coniferous,
                        _ => current
                    };
                }
                else if (normalizedElevation <= 0.30 && moisture >= 0.55 && random.NextDouble() < 0.30)
                {
                    vegetationMap[x, y] = current switch
                    {
                        VegetationType.Coniferous => VegetationType.Mixed,
                        VegetationType.Mixed => VegetationType.Deciduous,
                        VegetationType.Grass => VegetationType.Shrub,
                        _ => current
                    };
                }
            }
        }
    }

    private bool TryGetMapMinMax(int width, int height, double[,] map, out double minValue, out double maxValue)
    {
        minValue = double.MaxValue;
        maxValue = double.MinValue;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                minValue = Math.Min(minValue, map[x, y]);
                maxValue = Math.Max(maxValue, map[x, y]);
            }
        }

        if (minValue == double.MaxValue || maxValue == double.MinValue)
            return false;

        return true;
    }
    private void ClampMaps(
        int width,
        int height,
        VegetationType[,] vegetationMap,
        double[,] moistureMap,
        double[,] elevationMap,
        SimulationParameters parameters)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (vegetationMap[x, y] == VegetationType.Water)
                {
                    moistureMap[x, y] = 1.0;
                }
                else if (vegetationMap[x, y] == VegetationType.Bare)
                {
                    moistureMap[x, y] = Math.Clamp(moistureMap[x, y], 0.02, 0.35);
                }
                else
                {
                    moistureMap[x, y] = ClampMoisture(moistureMap[x, y], parameters);
                }
            }
        }
    }

    private double ClampMoisture(double moisture, SimulationParameters parameters)
    {
        var (min, max) = GetEffectiveMoistureRange(parameters);
        return Math.Clamp(moisture, min, max);
    }

    private double GetVegetationProbability(List<VegetationDistribution> distributions, VegetationType vegetationType)
    {
        var item = distributions?.FirstOrDefault(v => v.VegetationType == vegetationType);
        return item?.Probability ?? 0.0;
    }

    private VegetationType GetRandomCombustibleVegetation(
     List<VegetationDistribution> distributions,
     Random random,
     SimulationParameters parameters)
    {
        var fuelDensityFactor = GetNormalizedFuelDensity(parameters);

        var weighted = (distributions ?? new List<VegetationDistribution>())
            .Where(v => v.VegetationType != VegetationType.Water &&
                        v.VegetationType != VegetationType.Bare)
            .Select(v => new
            {
                v.VegetationType,
                Weight = AdjustFuelWeightForDensity(
                    v.VegetationType,
                    Math.Max(0.0, v.Probability),
                    fuelDensityFactor)
            })
            .Where(x => x.Weight > 0.0)
            .ToList();

        if (weighted.Count == 0)
        {
            return PickByWeights(random, parameters,
                (VegetationType.Mixed, 0.34),
                (VegetationType.Coniferous, 0.24),
                (VegetationType.Deciduous, 0.20),
                (VegetationType.Shrub, 0.12),
                (VegetationType.Grass, 0.10));
        }

        double sum = weighted.Sum(x => x.Weight);
        double roll = random.NextDouble() * sum;
        double cumulative = 0.0;

        foreach (var item in weighted)
        {
            cumulative += item.Weight;
            if (roll <= cumulative)
                return item.VegetationType;
        }

        return weighted[^1].VegetationType;
    }

    private VegetationType PickByWeights(
        Random random,
        SimulationParameters parameters,
        params (VegetationType Type, double Weight)[] items)
    {
        var fuelDensityFactor = GetNormalizedFuelDensity(parameters);

        var adjusted = items
            .Select(item => (
                item.Type,
                Weight: AdjustFuelWeightForDensity(item.Type, Math.Max(0.0, item.Weight), fuelDensityFactor)))
            .ToList();

        double total = adjusted.Sum(i => i.Weight);
        if (total <= 0.000001)
            return VegetationType.Mixed;

        double roll = random.NextDouble() * total;
        double cumulative = 0.0;

        foreach (var item in adjusted)
        {
            cumulative += item.Weight;
            if (roll <= cumulative)
                return item.Type;
        }

        return adjusted[^1].Type;
    }

    private List<(int X, int Y)> GetAllGridPoints(int width, int height)
    {
        var points = new List<(int X, int Y)>(width * height);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                points.Add((x, y));
        }

        return points;
    }

    private List<(int X, int Y)> GetGridNeighbors8(int x, int y, int width, int height)
    {
        var result = new List<(int X, int Y)>(8);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                result.Add((nx, ny));
            }
        }

        return result;
    }

    private static double NextGaussian(Random random)
    {
        double u1 = 1.0 - random.NextDouble();
        double u2 = 1.0 - random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private void SetEdgeFireSpreadModifier(ForestEdge edge, double value)
    {
        SetBackingField(edge, "<FireSpreadModifier>k__BackingField", value);
    }
}