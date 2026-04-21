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

        int effectiveWidth;
        int effectiveHeight;

        if (scaleProfile.Scale == GraphScaleType.Small)
        {
            effectiveWidth = Math.Max(10, parameters.GridWidth);
            effectiveHeight = Math.Max(10, parameters.GridHeight);
        }
        else if (scaleProfile.Scale == GraphScaleType.Medium)
        {
            effectiveWidth = Math.Max(20, parameters.GridWidth);
            effectiveHeight = Math.Max(20, parameters.GridHeight);
        }
        else
        {
            effectiveWidth = Math.Max(34, parameters.GridWidth);
            effectiveHeight = Math.Max(34, parameters.GridHeight);
        }

        var graph = new ForestGraph
        {
            Width = effectiveWidth,
            Height = effectiveHeight,
            StepDurationSeconds = parameters.StepDurationSeconds
        };

        _logger.LogInformation(
            "Генерация Graph. Scale={Scale}, RequestedNodeCount={NodeCount}, Width={Width}, Height={Height}, Mode={Mode}, Scenario={Scenario}, HasBlueprint={HasBlueprint}",
            scaleProfile.Scale,
            nodeCount,
            graph.Width,
            graph.Height,
            parameters.MapCreationMode,
            parameters.ClusteredScenarioType,
            parameters.ClusteredBlueprint != null && parameters.ClusteredBlueprint.Nodes.Any());

        if (parameters.MapCreationMode == MapCreationMode.SemiManual &&
            parameters.ClusteredBlueprint != null &&
            parameters.ClusteredBlueprint.Nodes.Any())
        {
            var blueprintGraph = BuildClusteredGraphFromBlueprint(parameters.ClusteredBlueprint, parameters);
            blueprintGraph.StepDurationSeconds = parameters.StepDurationSeconds;

            _logger.LogInformation(
                "Blueprint graph построен без автогенерации: Scale={Scale}, Cells={Cells}, Edges={Edges}",
                scaleProfile.Scale,
                blueprintGraph.Cells.Count,
                blueprintGraph.Edges.Count);

            return await Task.FromResult(blueprintGraph);
        }

        if (parameters.MapCreationMode == MapCreationMode.Scenario)
        {
            BuildClusteredScenarioGraph(
                graph,
                nodeCount,
                parameters,
                random,
                scaleProfile.MaxDegree);

            graph.StepDurationSeconds = parameters.StepDurationSeconds;

            _logger.LogInformation(
                "Сценарный graph сгенерирован: Scale={Scale}, Cells={Cells}, Edges={Edges}",
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
            "Случайный graph сгенерирован: Scale={Scale}, Cells={Cells}, Edges={Edges}",
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

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                vegetationMap[x, y] = generatedVegetation[x, y];
                moistureMap[x, y] = GetRandomMoisture(parameters.InitialMoistureMin, parameters.InitialMoistureMax, random, parameters);
                elevationMap[x, y] = GetRandomElevation(parameters.ElevationVariation, random, parameters);
            }
        }

        ApplyWaterAdjacencyEffects(width, height, vegetationMap, moistureMap, elevationMap, parameters);
        ApplyTerrainNoise(width, height, vegetationMap, moistureMap, elevationMap, parameters, random);
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
                    moistureCenter: Math.Max(0.08, parameters.InitialMoistureMin + 0.02),
                    moistureSpread: 0.05,
                    elevationBaseFactor: 0.18,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Coniferous, 0.72),
                        (VegetationType.Mixed, 0.16),
                        (VegetationType.Shrub, 0.08),
                        (VegetationType.Grass, 0.03),
                        (VegetationType.Deciduous, 0.01)));

                AddDryPatches(width, height, moistureMap, random, intensity: 0.12, patchCount: 4);
                AddHillFeature(width, height, elevationMap, parameters, width * 0.35, height * 0.40, 0.75);
                AddHillFeature(width, height, elevationMap, parameters, width * 0.68, height * 0.62, 0.60);
                break;

            case MapScenarioType.ForestWithRiver:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0 + 0.03,
                    moistureSpread: 0.09,
                    elevationBaseFactor: 0.14,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.40),
                        (VegetationType.Deciduous, 0.30),
                        (VegetationType.Coniferous, 0.18),
                        (VegetationType.Shrub, 0.07),
                        (VegetationType.Grass, 0.05)));

                PaintRiver(width, height, vegetationMap, moistureMap, elevationMap, random, parameters);
                break;

            case MapScenarioType.ForestWithLake:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0 + 0.05,
                    moistureSpread: 0.10,
                    elevationBaseFactor: 0.12,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.38),
                        (VegetationType.Deciduous, 0.30),
                        (VegetationType.Coniferous, 0.18),
                        (VegetationType.Shrub, 0.08),
                        (VegetationType.Grass, 0.06)));

                PaintLake(width, height, vegetationMap, moistureMap, elevationMap, random, parameters);
                break;

            case MapScenarioType.ForestWithFirebreak:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0,
                    moistureSpread: 0.09,
                    elevationBaseFactor: 0.12,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.38),
                        (VegetationType.Coniferous, 0.26),
                        (VegetationType.Deciduous, 0.18),
                        (VegetationType.Shrub, 0.10),
                        (VegetationType.Grass, 0.08)));

                PaintFirebreak(width, height, vegetationMap, moistureMap, random, parameters);
                break;

            case MapScenarioType.HillyTerrain:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0,
                    moistureSpread: 0.11,
                    elevationBaseFactor: 0.22,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.34),
                        (VegetationType.Coniferous, 0.26),
                        (VegetationType.Deciduous, 0.18),
                        (VegetationType.Shrub, 0.12),
                        (VegetationType.Grass, 0.10)));

                AddHillFeature(width, height, elevationMap, parameters, width * 0.22, height * 0.28, 1.20);
                AddHillFeature(width, height, elevationMap, parameters, width * 0.70, height * 0.40, 1.05);
                AddHillFeature(width, height, elevationMap, parameters, width * 0.52, height * 0.76, 0.95);
                AddHillFeature(width, height, elevationMap, parameters, width * 0.82, height * 0.70, 0.75);
                AddWetPatches(width, height, moistureMap, random, intensity: 0.06, patchCount: 2);
                break;

            case MapScenarioType.WetForestAfterRain:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: Math.Min(0.94, parameters.InitialMoistureMax + 0.14),
                    moistureSpread: 0.05,
                    elevationBaseFactor: 0.10,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.40),
                        (VegetationType.Deciduous, 0.30),
                        (VegetationType.Coniferous, 0.12),
                        (VegetationType.Shrub, 0.10),
                        (VegetationType.Grass, 0.08)));

                AddWetPatches(width, height, moistureMap, random, intensity: 0.18, patchCount: 6);
                break;

            case MapScenarioType.MixedForest:
            default:
                InitializeBaseLandscape(
                    width, height, parameters, random,
                    vegetationMap, moistureMap, elevationMap,
                    moistureCenter: (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0,
                    moistureSpread: 0.10,
                    elevationBaseFactor: 0.12,
                    vegetationPicker: r => PickByWeights(r, parameters,
                        (VegetationType.Mixed, 0.42),
                        (VegetationType.Deciduous, 0.24),
                        (VegetationType.Coniferous, 0.18),
                        (VegetationType.Shrub, 0.09),
                        (VegetationType.Grass, 0.07)));
                break;
        }

        ApplyWaterAdjacencyEffects(width, height, vegetationMap, moistureMap, elevationMap, parameters);
        ApplyTerrainNoise(width, height, vegetationMap, moistureMap, elevationMap, parameters, random);
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
            moistureSpread: 0.10,
            elevationBaseFactor: 0.10,
            vegetationPicker: r => GetRandomCombustibleVegetation(parameters.VegetationDistributions, r, parameters));

        var orderedObjects = (parameters.MapRegionObjects ?? new List<MapRegionObject>())
            .OrderBy(o => o.Priority)
            .ToList();

        foreach (var mapObject in orderedObjects)
            ApplyMapObject(width, height, parameters, vegetationMap, moistureMap, elevationMap, mapObject);

        ApplyWaterAdjacencyEffects(width, height, vegetationMap, moistureMap, elevationMap, parameters);
        ApplyTerrainNoise(width, height, vegetationMap, moistureMap, elevationMap, parameters, random);
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
        double elevationRange = Math.Max(1.0, effectiveElevationVariation * elevationBaseFactor);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                vegetationMap[x, y] = vegetationPicker(random);

                var moisture = moistureCenter + (random.NextDouble() * 2.0 - 1.0) * moistureSpread;
                moistureMap[x, y] = ClampMoisture(moisture, parameters);

                elevationMap[x, y] = (random.NextDouble() * 2.0 - 1.0) * elevationRange;
            }
        }
    }

    private void BuildClusteredRandomGraph(
        ForestGraph graph,
        int nodeCount,
        SimulationParameters parameters,
        Random random,
        int maxDegree)
    {
        var profile = GetClusteredScaleProfile(parameters, nodeCount);

        var patches = CreateClusteredPatchesRandomOnly(
            profile.PatchCount,
            graph.Width,
            graph.Height,
            parameters,
            random);

        var coordinates = GeneratePatchDrivenClusteredCoordinates(
            nodeCount,
            graph.Width,
            graph.Height,
            patches,
            random,
            profile);

        foreach (var (x, y) in coordinates)
        {
            var patch = GetBestPatchForPoint(x, y, patches);

            var vegetation = random.NextDouble() < 0.80
                ? patch.DominantVegetation
                : GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters);

            var moistureSpread = profile.Scale switch
            {
                GraphScaleType.Small => 0.05,
                GraphScaleType.Medium => 0.08,
                _ => 0.11
            };

            var elevationSpread = profile.Scale switch
            {
                GraphScaleType.Small => Math.Max(1.5, parameters.ElevationVariation * 0.07),
                GraphScaleType.Medium => Math.Max(2.0, parameters.ElevationVariation * 0.10),
                _ => Math.Max(3.5, parameters.ElevationVariation * 0.16)
            };

            var moisture = Math.Clamp(
                patch.BaseMoisture + (random.NextDouble() * 2.0 - 1.0) * moistureSpread,
                0.02,
                0.98);

            var elevation = patch.BaseElevation
                + (random.NextDouble() * 2.0 - 1.0) * elevationSpread;

            var cell = new ForestCell(
                x,
                y,
                vegetation,
                moisture,
                elevation,
                $"patch-{patch.Index}");

            graph.Cells.Add(cell);
        }

        CreateDenseLocalEdges(graph, profile);

        if (profile.Scale == GraphScaleType.Small)
        {
            CreateClusterBridges(graph, patches, random, profile.PreferredBridgeCount, profile.MaxDegree);
            ApplyClusteredBridgeWeakening(graph, bridgeFactor: 0.82);
            EnsureNoIsolatedNodes(graph, profile.MaxDegree);
            return;
        }

        if (profile.Scale == GraphScaleType.Medium)
        {
            CreateClusterBridges(graph, patches, random, profile.PreferredBridgeCount, profile.MaxDegree);
            AddExtendedSupportEdges(graph, profile);
            ApplyClusteredBridgeWeakening(graph, bridgeFactor: 0.72);
            ApplySurfaceBarrierEdgeModifiers(graph);
            EnsureNoIsolatedNodes(graph, profile.MaxDegree);
            return;
        }

        CreateClusterBridges(graph, patches, random, profile.PreferredBridgeCount, profile.MaxDegree);
        AddExtendedSupportEdges(graph, profile);
        CreateLargeScaleCorridors(graph, patches, parameters, random, profile);
        ApplyClusteredBridgeWeakening(graph, bridgeFactor: 0.64);
        ApplySurfaceBarrierEdgeModifiers(graph);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }

    private void BuildClusteredScenarioGraph(
        ForestGraph graph,
        int nodeCount,
        SimulationParameters parameters,
        Random random,
        int maxDegree)
    {
        var scenario = parameters.ClusteredScenarioType ?? ClusteredScenarioType.DenseDryConiferous;
        var profile = GetClusteredScaleProfile(parameters, nodeCount);

        _logger.LogInformation(
            "Graph scenario generation: Scenario={Scenario}, Scale={Scale}, NodeCount={NodeCount}",
            scenario,
            profile.Scale,
            nodeCount);

        switch (scenario)
        {
            case ClusteredScenarioType.DenseDryConiferous:
                BuildDenseDryConiferousScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.WaterBarrier:
                BuildWaterBarrierScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.FirebreakGap:
                BuildFirebreakGapScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.HillyClusters:
                BuildHillyClustersScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.WetAfterRain:
                BuildWetAfterRainScenario(graph, nodeCount, parameters, random, profile);
                return;

            case ClusteredScenarioType.MixedDryHotspots:
            default:
                BuildMixedDryHotspotsScenario(graph, nodeCount, parameters, random, profile);
                return;
        }
    }

    private void BuildDenseDryConiferousScenario(
        ForestGraph graph,
        int nodeCount,
        SimulationParameters parameters,
        Random random,
        ClusteredScaleProfile profile)
    {
        var used = new HashSet<(int X, int Y)>();
        int total = Math.Max(8, nodeCount);

        if (profile.Scale == GraphScaleType.Small)
        {
            var left = AddScenarioPatchNodesEllipse(
                graph, used, "small-a",
                0.26, 0.42, 0.16, 0.18, Math.Max(3, total / 2),
                parameters, random,
                VegetationType.Coniferous, 0.10, 0.18, -6.0, 5.0);

            var right = AddScenarioPatchNodesEllipse(
                graph, used, "small-b",
                0.72, 0.42, 0.16, 0.18, total - left.Count,
                parameters, random,
                VegetationType.Coniferous, 0.11, 0.19, -4.0, 6.0);

            ConnectScenarioNodesLocally(graph, left, profile.CloseRadius + 0.35, profile.MaxDegree, profile.LocalTargetDegree);
            ConnectScenarioNodesLocally(graph, right, profile.CloseRadius + 0.35, profile.MaxDegree, profile.LocalTargetDegree);

            AddScenarioBridge(graph, left, right, 1, 1.18);
            ApplyClusteredBridgeWeakening(graph, 0.88);
            EnsureNoIsolatedNodes(graph, profile.MaxDegree);
            return;
        }

        var shares = profile.Scale == GraphScaleType.Medium
            ? new[] { 0.40, 0.34, 0.26 }
            : new[] { 0.26, 0.21, 0.20, 0.18, 0.15 };

        var counts = BuildCounts(total, shares, 3);

        var clusters = new List<List<ForestCell>>();

        if (profile.Scale == GraphScaleType.Medium)
        {
            clusters.Add(AddScenarioPatchNodesEllipse(graph, used, "dense-m-1", 0.23, 0.33, 0.18, 0.18, counts[0], parameters, random, VegetationType.Coniferous, 0.10, 0.18, -6.0, 7.0));
            clusters.Add(AddScenarioPatchNodesEllipse(graph, used, "dense-m-2", 0.68, 0.34, 0.18, 0.18, counts[1], parameters, random, VegetationType.Coniferous, 0.09, 0.18, -4.0, 8.0));
            clusters.Add(AddScenarioPatchNodesEllipse(graph, used, "dense-m-3", 0.48, 0.74, 0.22, 0.20, counts[2], parameters, random, VegetationType.Mixed, 0.11, 0.22, -2.0, 10.0));
        }
        else
        {
            clusters.Add(AddScenarioPatchNodesEllipse(graph, used, "dense-l-1", 0.18, 0.28, 0.16, 0.16, counts[0], parameters, random, VegetationType.Coniferous, 0.09, 0.17, -7.0, 7.0));
            clusters.Add(AddScenarioPatchNodesEllipse(graph, used, "dense-l-2", 0.44, 0.24, 0.16, 0.16, counts[1], parameters, random, VegetationType.Coniferous, 0.10, 0.18, -5.0, 8.0));
            clusters.Add(AddScenarioPatchNodesEllipse(graph, used, "dense-l-3", 0.73, 0.30, 0.16, 0.16, counts[2], parameters, random, VegetationType.Coniferous, 0.10, 0.18, -4.0, 9.0));
            clusters.Add(AddScenarioPatchNodesEllipse(graph, used, "dense-l-4", 0.32, 0.72, 0.18, 0.18, counts[3], parameters, random, VegetationType.Mixed, 0.12, 0.22, -1.0, 10.0));
            clusters.Add(AddScenarioPatchNodesEllipse(graph, used, "dense-l-5", 0.70, 0.74, 0.20, 0.20, counts[4], parameters, random, VegetationType.Mixed, 0.12, 0.23, 1.0, 11.0));
        }

        foreach (var cluster in clusters)
            ConnectScenarioNodesLocally(graph, cluster, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);

        for (int i = 0; i < clusters.Count - 1; i++)
            AddScenarioBridge(graph, clusters[i], clusters[i + 1], 1, 1.16);

        if (profile.Scale == GraphScaleType.Large)
        {
            if (clusters.Count >= 4)
                AddScenarioBridge(graph, clusters[0], clusters[3], 1, 1.10);

            var patches = BuildPatchesFromExistingClusters(clusters);
            CreateLargeScaleCorridors(graph, patches, parameters, random, profile);
        }

        ApplyClusteredBridgeWeakening(graph, profile.Scale == GraphScaleType.Large ? 0.66 : 0.74);
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
        int total = Math.Max(8, nodeCount);

        double barrierX = profile.Scale switch
        {
            GraphScaleType.Small => 0.50,
            GraphScaleType.Medium => 0.48,
            _ => 0.50
        };

        int leftCount = Math.Max(3, (int)Math.Round(total * 0.48));
        int rightCount = Math.Max(3, total - leftCount);

        var left = AddScenarioPatchNodesEllipse(
            graph, used, "water-left",
            0.24, 0.48, 0.18, 0.30, leftCount,
            parameters, random,
            VegetationType.Mixed, 0.22, 0.35, -6.0, 5.0);

        var right = AddScenarioPatchNodesEllipse(
            graph, used, "water-right",
            0.76, 0.48, 0.18, 0.30, rightCount,
            parameters, random,
            VegetationType.Deciduous, 0.24, 0.38, -4.0, 5.0);

        ConnectScenarioNodesLocally(graph, left, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, right, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);

        var barrierNodes = AddLinearBarrierNodes(
            graph, used, "water-barrier", barrierX, totalNodes: profile.Scale == GraphScaleType.Small ? 2 : profile.Scale == GraphScaleType.Medium ? 4 : 7,
            parameters, random,
            vegetation: VegetationType.Water,
            moisture: 1.0,
            elevationBase: -12.0);

        if (profile.Scale == GraphScaleType.Small)
        {
            AddScenarioBridge(graph, left, right, 1, 0.92);
        }
        else if (profile.Scale == GraphScaleType.Medium)
        {
            CreateGapAwareBridge(graph, left, right, throughNodes: barrierNodes, maxLinks: 2, weakenTo: 0.34);
        }
        else
        {
            CreateGapAwareBridge(graph, left, right, throughNodes: barrierNodes, maxLinks: 3, weakenTo: 0.28);

            var topAux = AddScenarioPatchNodesEllipse(
                graph, used, "water-top",
                0.50, 0.16, 0.12, 0.12, Math.Max(4, total / 7),
                parameters, random,
                VegetationType.Shrub, 0.20, 0.30, -3.0, 4.0);

            var bottomAux = AddScenarioPatchNodesEllipse(
                graph, used, "water-bottom",
                0.52, 0.82, 0.12, 0.12, Math.Max(4, total / 7),
                parameters, random,
                VegetationType.Shrub, 0.21, 0.30, -2.0, 4.0);

            ConnectScenarioNodesLocally(graph, topAux, profile.CloseRadius + 0.3, profile.MaxDegree, profile.LocalTargetDegree);
            ConnectScenarioNodesLocally(graph, bottomAux, profile.CloseRadius + 0.3, profile.MaxDegree, profile.LocalTargetDegree);

            AddScenarioBridge(graph, left, topAux, 1, 1.02);
            AddScenarioBridge(graph, topAux, right, 1, 0.96);
            AddScenarioBridge(graph, left, bottomAux, 1, 1.02);
            AddScenarioBridge(graph, bottomAux, right, 1, 0.96);
        }

        ApplySurfaceBarrierEdgeModifiers(graph);
        ApplyClusteredBridgeWeakening(graph, profile.Scale == GraphScaleType.Large ? 0.60 : 0.70);
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
        int total = Math.Max(8, nodeCount);

        int leftCount = Math.Max(3, (int)Math.Round(total * 0.44));
        int rightCount = Math.Max(3, (int)Math.Round(total * 0.44));
        int corridorCount = Math.Max(2, total - leftCount - rightCount);

        var left = AddScenarioPatchNodesEllipse(
            graph, used, "firebreak-left",
            0.24, 0.50, 0.17, 0.27, leftCount,
            parameters, random,
            VegetationType.Coniferous, 0.11, 0.19, -3.0, 6.0);

        var right = AddScenarioPatchNodesEllipse(
            graph, used, "firebreak-right",
            0.76, 0.50, 0.17, 0.27, rightCount,
            parameters, random,
            VegetationType.Mixed, 0.13, 0.22, -1.0, 7.0);

        ConnectScenarioNodesLocally(graph, left, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);
        ConnectScenarioNodesLocally(graph, right, profile.SupportRadius, profile.MaxDegree, profile.LocalTargetDegree);

        var corridor = AddScenarioPatchNodesEllipse(
            graph, used, "firebreak-gap",
            0.50, 0.52, 0.07, 0.10, corridorCount,
            parameters, random,
            VegetationType.Grass, 0.08, 0.14, 1.0, 3.0);

        ConnectScenarioNodesLocally(graph, corridor, profile.CloseRadius + 0.25, profile.MaxDegree, Math.Max(2, profile.LocalTargetDegree - 1));

        if (profile.Scale == GraphScaleType.Small)
        {
            AddScenarioBridge(graph, left, corridor, 1, 1.06);
            AddScenarioBridge(graph, corridor, right, 1, 1.02);
        }
        else if (profile.Scale == GraphScaleType.Medium)
        {
            AddScenarioBridge(graph, left, corridor, 2, 1.04);
            AddScenarioBridge(graph, corridor, right, 2, 1.00);
        }
        else
        {
            AddScenarioBridge(graph, left, corridor, 2, 1.04);
            AddScenarioBridge(graph, corridor, right, 2, 1.00);

            var north = AddScenarioPatchNodesEllipse(
                graph, used, "firebreak-north",
                0.50, 0.20, 0.10, 0.10, Math.Max(4, total / 10),
                parameters, random,
                VegetationType.Shrub, 0.10, 0.18, 2.0, 4.0);

            var south = AddScenarioPatchNodesEllipse(
                graph, used, "firebreak-south",
                0.50, 0.82, 0.10, 0.10, Math.Max(4, total / 10),
                parameters, random,
                VegetationType.Shrub, 0.10, 0.18, 2.0, 4.0);

            ConnectScenarioNodesLocally(graph, north, profile.CloseRadius + 0.25, profile.MaxDegree, 2);
            ConnectScenarioNodesLocally(graph, south, profile.CloseRadius + 0.25, profile.MaxDegree, 2);

            AddScenarioBridge(graph, left, north, 1, 0.96);
            AddScenarioBridge(graph, north, right, 1, 0.92);
            AddScenarioBridge(graph, left, south, 1, 0.96);
            AddScenarioBridge(graph, south, right, 1, 0.92);
        }

        var firebreakNodes = AddLinearBarrierNodes(
            graph, used, "firebreak-line", 0.50, profile.Scale == GraphScaleType.Small ? 2 : profile.Scale == GraphScaleType.Medium ? 4 : 6,
            parameters, random,
            vegetation: VegetationType.Bare,
            moisture: 0.08,
            elevationBase: 1.5);

        foreach (var barrierNode in firebreakNodes)
        {
            var edges = graph.GetIncidentEdges(barrierNode);
            foreach (var edge in edges)
                SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 0.38, 0.02, 0.38));
        }

        ApplySurfaceBarrierEdgeModifiers(graph);
        ApplyClusteredBridgeWeakening(graph, profile.Scale == GraphScaleType.Large ? 0.66 : 0.74);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }

    private void BuildHillyClustersScenario(
        ForestGraph graph,
        int nodeCount,
        SimulationParameters parameters,
        Random random,
        ClusteredScaleProfile profile)
    {
        var patches = CreateScenarioDrivenClusteredPatches(
            ClusteredScenarioType.HillyClusters,
            nodeCount,
            graph.Width,
            graph.Height,
            parameters,
            random,
            profile);

        PopulateGraphFromPatches(graph, nodeCount, parameters, random, profile, patches);
        CreateDenseLocalEdges(graph, profile);
        CreateClusterBridges(graph, patches, random, profile.PreferredBridgeCount, profile.MaxDegree);

        foreach (var cell in graph.Cells.Where(c => c.Elevation > graph.Cells.Average(x => x.Elevation) + 2.5))
        {
            foreach (var edge in graph.GetIncidentEdges(cell))
                SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 1.08, 0.02, 1.60));
        }

        if (profile.Scale == GraphScaleType.Large)
            CreateLargeScaleCorridors(graph, patches, parameters, random, profile);

        ApplyClusteredBridgeWeakening(graph, profile.Scale == GraphScaleType.Large ? 0.68 : 0.76);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }

    private void BuildWetAfterRainScenario(
        ForestGraph graph,
        int nodeCount,
        SimulationParameters parameters,
        Random random,
        ClusteredScaleProfile profile)
    {
        var patches = CreateScenarioDrivenClusteredPatches(
            ClusteredScenarioType.WetAfterRain,
            nodeCount,
            graph.Width,
            graph.Height,
            parameters,
            random,
            profile);

        PopulateGraphFromPatches(graph, nodeCount, parameters, random, profile, patches);
        CreateDenseLocalEdges(graph, profile);
        CreateClusterBridges(graph, patches, random, Math.Max(1, profile.PreferredBridgeCount - 1), profile.MaxDegree);

        foreach (var edge in graph.Edges)
            SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 0.92, 0.02, 1.15));

        ApplyClusteredBridgeWeakening(graph, profile.Scale == GraphScaleType.Large ? 0.72 : 0.80);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }

    private void BuildMixedDryHotspotsScenario(
        ForestGraph graph,
        int nodeCount,
        SimulationParameters parameters,
        Random random,
        ClusteredScaleProfile profile)
    {
        var patches = CreateScenarioDrivenClusteredPatches(
            ClusteredScenarioType.MixedDryHotspots,
            nodeCount,
            graph.Width,
            graph.Height,
            parameters,
            random,
            profile);

        PopulateGraphFromPatches(graph, nodeCount, parameters, random, profile, patches);
        CreateDenseLocalEdges(graph, profile);
        CreateClusterBridges(graph, patches, random, profile.PreferredBridgeCount, profile.MaxDegree);
        AddExtendedSupportEdges(graph, profile);

        if (profile.Scale == GraphScaleType.Large)
            CreateLargeScaleCorridors(graph, patches, parameters, random, profile);

        ApplyClusteredBridgeWeakening(graph, profile.Scale == GraphScaleType.Large ? 0.66 : 0.74);
        ApplySurfaceBarrierEdgeModifiers(graph);
        EnsureNoIsolatedNodes(graph, profile.MaxDegree);
    }

    private void PopulateGraphFromPatches(
        ForestGraph graph,
        int nodeCount,
        SimulationParameters parameters,
        Random random,
        ClusteredScaleProfile profile,
        List<ClusteredPatch> patches)
    {
        var coordinates = GeneratePatchDrivenClusteredCoordinates(
            nodeCount,
            graph.Width,
            graph.Height,
            patches,
            random,
            profile);

        foreach (var (x, y) in coordinates)
        {
            var patch = GetBestPatchForPoint(x, y, patches);
            var vegetation = SelectScenarioVegetationForPatch(patch, parameters, random);

            double moistureSpread = profile.Scale switch
            {
                GraphScaleType.Small => 0.05,
                GraphScaleType.Medium => 0.08,
                _ => 0.12
            };

            double elevationSpread = profile.Scale switch
            {
                GraphScaleType.Small => Math.Max(1.8, parameters.ElevationVariation * 0.08),
                GraphScaleType.Medium => Math.Max(2.2, parameters.ElevationVariation * 0.12),
                _ => Math.Max(4.0, parameters.ElevationVariation * 0.18)
            };

            var moisture = Math.Clamp(
                patch.BaseMoisture + (random.NextDouble() * 2.0 - 1.0) * moistureSpread,
                0.02,
                0.98);

            var elevation = patch.BaseElevation + (random.NextDouble() * 2.0 - 1.0) * elevationSpread;

            graph.Cells.Add(new ForestCell(
                x,
                y,
                vegetation,
                moisture,
                elevation,
                $"patch-{patch.Index}"));
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
                PatchCount = Math.Clamp(Math.Max(2, (int)Math.Round(nodeCount / 6.0)), 2, 4),
                LocalTargetDegree = 2,
                MaxDegree = 3,
                CloseRadius = 2.10,
                SupportRadius = 2.75,
                ExtendedRadius = 3.30,
                ExtendedEdgeBudget = Math.Clamp(nodeCount / 10, 0, 2),
                PlacementScale = 0.96,
                PreferredBridgeCount = 1,
                CorridorBudget = 0
            },

            GraphScaleType.Large => new ClusteredScaleProfile
            {
                Scale = GraphScaleType.Large,
                PatchCount = Math.Clamp(Math.Max(6, (int)Math.Round(nodeCount / 24.0)), 6, 10),
                LocalTargetDegree = 4,
                MaxDegree = 5,
                CloseRadius = 3.10,
                SupportRadius = 4.80,
                ExtendedRadius = 7.80,
                ExtendedEdgeBudget = Math.Clamp(nodeCount / 16, 10, 24),
                PlacementScale = 2.15,
                PreferredBridgeCount = Math.Clamp(nodeCount / 40, 4, 8),
                CorridorBudget = Math.Clamp(nodeCount / 22, 5, 10)
            },

            _ => new ClusteredScaleProfile
            {
                Scale = GraphScaleType.Medium,
                PatchCount = Math.Clamp(Math.Max(3, (int)Math.Round(nodeCount / 18.0)), 3, 6),
                LocalTargetDegree = 3,
                MaxDegree = 4,
                CloseRadius = 2.35,
                SupportRadius = 3.25,
                ExtendedRadius = 4.20,
                ExtendedEdgeBudget = Math.Clamp(nodeCount / 24, 2, 6),
                PlacementScale = 1.28,
                PreferredBridgeCount = Math.Clamp(nodeCount / 35, 2, 4),
                CorridorBudget = 1
            }
        };
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

    private void CreateDenseLocalEdges(ForestGraph graph, ClusteredScaleProfile profile)
    {
        foreach (var source in graph.Cells)
        {
            int sourceDegree = GetNodeDegree(graph, source);
            if (sourceDegree >= profile.LocalTargetDegree)
                continue;

            var candidates = graph.Cells
                .Where(n => n.Id != source.Id)
                .Select(n => new
                {
                    Cell = n,
                    Distance = CalculateDistance(source.X, source.Y, n.X, n.Y)
                })
                .Where(x => x.Distance > 0.0 && x.Distance <= profile.SupportRadius)
                .OrderBy(x => x.Distance)
                .ToList();

            foreach (var candidate in candidates)
            {
                if (GetNodeDegree(graph, source) >= profile.LocalTargetDegree)
                    break;

                if (GetNodeDegree(graph, candidate.Cell) >= profile.MaxDegree)
                    continue;

                if (EdgeExists(graph, source, candidate.Cell))
                    continue;

                bool sameCluster = string.Equals(source.ClusterId, candidate.Cell.ClusterId, StringComparison.Ordinal);
                if (!sameCluster && candidate.Distance > profile.CloseRadius)
                    continue;

                TryAddEdge(graph, source, candidate.Cell);
            }
        }
    }

    private void AddExtendedSupportEdges(ForestGraph graph, ClusteredScaleProfile profile)
    {
        if (profile.ExtendedEdgeBudget <= 0)
            return;

        int added = 0;

        foreach (var source in graph.Cells.OrderBy(c => c.ClusterId).ThenBy(c => c.X).ThenBy(c => c.Y))
        {
            if (added >= profile.ExtendedEdgeBudget)
                break;

            if (GetNodeDegree(graph, source) >= profile.MaxDegree)
                continue;

            var candidate = graph.Cells
                .Where(x => x.Id != source.Id)
                .Where(x => !EdgeExists(graph, source, x))
                .Where(x => GetNodeDegree(graph, x) < profile.MaxDegree)
                .Select(x => new
                {
                    Cell = x,
                    Distance = CalculateDistance(source.X, source.Y, x.X, x.Y),
                    SameCluster = string.Equals(source.ClusterId, x.ClusterId, StringComparison.Ordinal)
                })
                .Where(x => x.Distance > profile.SupportRadius * 0.85 && x.Distance <= profile.ExtendedRadius)
                .OrderByDescending(x => x.SameCluster)
                .ThenBy(x => x.Distance)
                .FirstOrDefault();

            if (candidate == null)
                continue;

            if (!TryAddEdge(graph, source, candidate.Cell))
                continue;

            added++;
        }
    }

    private void CreateClusterBridges(
        ForestGraph graph,
        List<ClusteredPatch> patches,
        Random random,
        int preferredBridgeCount,
        int maxDegree)
    {
        if (patches.Count < 2 || preferredBridgeCount <= 0)
            return;

        var clusterGroups = graph.Cells
            .Where(c => !string.IsNullOrWhiteSpace(c.ClusterId))
            .GroupBy(c => c.ClusterId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var ordered = patches.OrderBy(p => p.CenterX).ThenBy(p => p.CenterY).ToList();
        int created = 0;

        for (int i = 0; i < ordered.Count - 1 && created < preferredBridgeCount; i++)
        {
            var aKey = $"patch-{ordered[i].Index}";
            var bKey = $"patch-{ordered[i + 1].Index}";

            if (!clusterGroups.TryGetValue(aKey, out var fromCluster) || fromCluster.Count == 0)
                continue;

            if (!clusterGroups.TryGetValue(bKey, out var toCluster) || toCluster.Count == 0)
                continue;

            var pair = FindClosestPair(
                graph,
                fromCluster,
                toCluster,
                maxDegree);

            if (pair == null)
                continue;

            if (!TryAddEdge(graph, pair.Value.From, pair.Value.To))
                continue;

            var edge = GetEdge(graph, pair.Value.From, pair.Value.To);
            if (edge != null)
                SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * (0.96 + random.NextDouble() * 0.24), 0.02, 1.35));

            created++;
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

            SetEdgeFireSpreadModifier(
                edge,
                Math.Clamp(edge.FireSpreadModifier * bridgeFactor, 0.02, 1.10));
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

    private List<ClusteredPatch> CreateClusteredPatchesRandomOnly(
        int patchCount,
        int width,
        int height,
        SimulationParameters parameters,
        Random random)
    {
        var patches = new List<ClusteredPatch>();
        var fuelDensityFactor = GetNormalizedFuelDensity(parameters);

        for (int i = 0; i < patchCount; i++)
        {
            double centerX = width * (0.14 + random.NextDouble() * 0.72);
            double centerY = height * (0.14 + random.NextDouble() * 0.72);

            double radiusX = Math.Max(2.0, width * (0.09 + random.NextDouble() * 0.08));
            double radiusY = Math.Max(2.0, height * (0.09 + random.NextDouble() * 0.08));

            var vegetation = PickByWeights(random, parameters,
                (VegetationType.Coniferous, AdjustFuelWeightForDensity(VegetationType.Coniferous, 0.28, fuelDensityFactor)),
                (VegetationType.Mixed, AdjustFuelWeightForDensity(VegetationType.Mixed, 0.26, fuelDensityFactor)),
                (VegetationType.Deciduous, AdjustFuelWeightForDensity(VegetationType.Deciduous, 0.20, fuelDensityFactor)),
                (VegetationType.Shrub, AdjustFuelWeightForDensity(VegetationType.Shrub, 0.14, fuelDensityFactor)),
                (VegetationType.Grass, AdjustFuelWeightForDensity(VegetationType.Grass, 0.12, fuelDensityFactor)));

            patches.Add(new ClusteredPatch
            {
                Index = i,
                CenterX = centerX,
                CenterY = centerY,
                RadiusX = radiusX,
                RadiusY = radiusY,
                DominantVegetation = vegetation,
                BaseMoisture = GetRandomMoisture(parameters.InitialMoistureMin, parameters.InitialMoistureMax, random, parameters),
                BaseElevation = GetRandomElevation(parameters.ElevationVariation, random, parameters),
                Tag = "random"
            });
        }

        return patches;
    }

    private List<ClusteredPatch> CreateScenarioDrivenClusteredPatches(
        ClusteredScenarioType scenario,
        int nodeCount,
        int width,
        int height,
        SimulationParameters parameters,
        Random random,
        ClusteredScaleProfile profile)
    {
        var patches = new List<ClusteredPatch>();

        switch (scenario)
        {
            case ClusteredScenarioType.HillyClusters:
                for (int i = 0; i < profile.PatchCount; i++)
                {
                    patches.Add(new ClusteredPatch
                    {
                        Index = i,
                        CenterX = width * (0.12 + (0.76 * i / Math.Max(1, profile.PatchCount - 1))),
                        CenterY = height * (0.22 + random.NextDouble() * 0.56),
                        RadiusX = Math.Max(2.0, width * 0.10),
                        RadiusY = Math.Max(2.0, height * 0.10),
                        DominantVegetation = i % 2 == 0 ? VegetationType.Coniferous : VegetationType.Mixed,
                        BaseMoisture = Math.Clamp(parameters.InitialMoistureMin + 0.10 + random.NextDouble() * 0.08, 0.04, 0.94),
                        BaseElevation = GetEffectiveElevationVariation(parameters.ElevationVariation, parameters) * (0.28 + random.NextDouble() * 0.35),
                        Tag = "hill"
                    });
                }
                break;

            case ClusteredScenarioType.WetAfterRain:
                for (int i = 0; i < profile.PatchCount; i++)
                {
                    patches.Add(new ClusteredPatch
                    {
                        Index = i,
                        CenterX = width * (0.15 + random.NextDouble() * 0.70),
                        CenterY = height * (0.15 + random.NextDouble() * 0.70),
                        RadiusX = Math.Max(2.0, width * (0.08 + random.NextDouble() * 0.05)),
                        RadiusY = Math.Max(2.0, height * (0.08 + random.NextDouble() * 0.05)),
                        DominantVegetation = i % 3 == 0 ? VegetationType.Deciduous : VegetationType.Mixed,
                        BaseMoisture = Math.Clamp(parameters.InitialMoistureMax + 0.10 + random.NextDouble() * 0.08, 0.30, 0.98),
                        BaseElevation = GetRandomElevation(parameters.ElevationVariation * 0.55, random, parameters),
                        Tag = "wet"
                    });
                }
                break;

            case ClusteredScenarioType.MixedDryHotspots:
            default:
                for (int i = 0; i < profile.PatchCount; i++)
                {
                    bool hot = i % 2 == 0;

                    patches.Add(new ClusteredPatch
                    {
                        Index = i,
                        CenterX = width * (0.12 + random.NextDouble() * 0.76),
                        CenterY = height * (0.12 + random.NextDouble() * 0.76),
                        RadiusX = Math.Max(2.0, width * (0.08 + random.NextDouble() * 0.06)),
                        RadiusY = Math.Max(2.0, height * (0.08 + random.NextDouble() * 0.06)),
                        DominantVegetation = hot ? VegetationType.Coniferous : VegetationType.Mixed,
                        BaseMoisture = hot
                            ? Math.Clamp(parameters.InitialMoistureMin + 0.02 + random.NextDouble() * 0.06, 0.02, 0.45)
                            : Math.Clamp((parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0 + random.NextDouble() * 0.10, 0.08, 0.92),
                        BaseElevation = GetRandomElevation(parameters.ElevationVariation * (hot ? 0.75 : 0.55), random, parameters),
                        Tag = hot ? "hotspot" : "mixed"
                    });
                }
                break;
        }

        return patches;
    }

    private List<(int X, int Y)> GeneratePatchDrivenClusteredCoordinates(
        int nodeCount,
        int width,
        int height,
        List<ClusteredPatch> patches,
        Random random,
        ClusteredScaleProfile profile)
    {
        var used = new HashSet<(int X, int Y)>();
        var result = new List<(int X, int Y)>();

        int guard = nodeCount * 80 + 500;

        while (result.Count < nodeCount && guard-- > 0)
        {
            var patch = patches[random.Next(patches.Count)];

            double scaleX = patch.RadiusX * profile.PlacementScale;
            double scaleY = patch.RadiusY * profile.PlacementScale;

            double px = patch.CenterX + NextGaussian(random) * scaleX;
            double py = patch.CenterY + NextGaussian(random) * scaleY;

            int x = Math.Clamp((int)Math.Round(px), 0, width - 1);
            int y = Math.Clamp((int)Math.Round(py), 0, height - 1);

            if (!used.Add((x, y)))
                continue;

            result.Add((x, y));
        }

        if (result.Count < nodeCount)
        {
            foreach (var point in GetAllGridPoints(width, height).OrderBy(_ => random.Next()))
            {
                if (used.Add(point))
                    result.Add(point);

                if (result.Count >= nodeCount)
                    break;
            }
        }

        return result;
    }

    private ClusteredPatch GetBestPatchForPoint(int x, int y, List<ClusteredPatch> patches)
    {
        return patches
            .OrderBy(p =>
            {
                double dx = (x - p.CenterX) / Math.Max(0.1, p.RadiusX);
                double dy = (y - p.CenterY) / Math.Max(0.1, p.RadiusY);
                return dx * dx + dy * dy;
            })
            .First();
    }

    private VegetationType SelectScenarioVegetationForPatch(
        ClusteredPatch patch,
        SimulationParameters parameters,
        Random random)
    {
        if (patch.Tag == "wet")
        {
            return random.NextDouble() < 0.65
                ? patch.DominantVegetation
                : PickByWeights(random, parameters,
                    (VegetationType.Deciduous, 0.42),
                    (VegetationType.Mixed, 0.36),
                    (VegetationType.Shrub, 0.12),
                    (VegetationType.Grass, 0.10));
        }

        if (patch.Tag == "hill")
        {
            return random.NextDouble() < 0.75
                ? patch.DominantVegetation
                : PickByWeights(random, parameters,
                    (VegetationType.Coniferous, 0.34),
                    (VegetationType.Mixed, 0.28),
                    (VegetationType.Deciduous, 0.18),
                    (VegetationType.Shrub, 0.12),
                    (VegetationType.Grass, 0.08));
        }

        if (patch.Tag == "hotspot")
        {
            return random.NextDouble() < 0.78
                ? VegetationType.Coniferous
                : PickByWeights(random, parameters,
                    (VegetationType.Mixed, 0.32),
                    (VegetationType.Shrub, 0.18),
                    (VegetationType.Grass, 0.10));
        }

        return random.NextDouble() < 0.78
            ? patch.DominantVegetation
            : GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters);
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

    private ForestGraph BuildClusteredGraphFromBlueprint(ClusteredGraphBlueprint blueprint, SimulationParameters parameters)
    {
        var graph = new ForestGraph
        {
            Width = Math.Max(8, blueprint.CanvasWidth),
            Height = Math.Max(8, blueprint.CanvasHeight),
            StepDurationSeconds = parameters.StepDurationSeconds
        };

        var nodes = new Dictionary<Guid, ForestCell>();

        foreach (var nodeDraft in blueprint.Nodes)
        {
            var cell = new ForestCell(
                Math.Clamp(nodeDraft.X, 0, graph.Width - 1),
                Math.Clamp(nodeDraft.Y, 0, graph.Height - 1),
                nodeDraft.Vegetation,
                Math.Clamp(nodeDraft.Moisture, 0.02, 0.98),
                nodeDraft.Elevation,
                string.IsNullOrWhiteSpace(nodeDraft.ClusterId) ? null : nodeDraft.ClusterId);

            nodes[nodeDraft.Id] = cell;
            graph.Cells.Add(cell);

            SetBackingField(cell, "<Id>k__BackingField", nodeDraft.Id);
        }

        foreach (var edgeDraft in blueprint.Edges)
        {
            if (!nodes.TryGetValue(edgeDraft.FromNodeId, out var from))
                continue;

            if (!nodes.TryGetValue(edgeDraft.ToNodeId, out var to))
                continue;

            if (from.Id == to.Id || EdgeExists(graph, from, to))
                continue;

            double distance = edgeDraft.DistanceOverride.HasValue && edgeDraft.DistanceOverride.Value > 0
     ? edgeDraft.DistanceOverride.Value
     : CalculateDistance(from.X, from.Y, to.X, to.Y);

            double slope = distance <= 0.0001 ? 0.0 : (to.Elevation - from.Elevation) / distance;
            var edge = new ForestEdge(from, to, distance, slope);

            SetBackingField(edge, "<Id>k__BackingField", edgeDraft.Id == Guid.Empty ? Guid.NewGuid() : edgeDraft.Id);

            if (edgeDraft.FireSpreadModifier > 0)
                SetEdgeFireSpreadModifier(edge, Math.Clamp(edgeDraft.FireSpreadModifier, 0.02, 1.85));

            graph.Edges.Add(edge);
        }

        EnsureNoIsolatedNodes(graph, GetClusteredScaleProfile(parameters, graph.Cells.Count).MaxDegree);
        ApplySurfaceBarrierEdgeModifiers(graph);

        return graph;
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
            double radius = Math.Max(2.6, Math.Min(width, height) * (0.08 + random.NextDouble() * 0.08));

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double dx = x - centerX;
                    double dy = y - centerY;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    if (distance > radius * 1.7)
                        continue;

                    double normalized = distance / radius;
                    double falloff = Math.Exp(-(normalized * normalized) * 1.25);
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
            double radius = Math.Max(2.8, Math.Min(width, height) * (0.10 + random.NextDouble() * 0.10));

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double dx = x - centerX;
                    double dy = y - centerY;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    if (distance > radius * 1.8)
                        continue;

                    double normalized = distance / radius;
                    double falloff = Math.Exp(-(normalized * normalized) * 1.15);
                    moistureMap[x, y] += intensity * falloff;
                }
            }
        }
    }

    private void AddHillFeature(
        int width,
        int height,
        double[,] elevationMap,
        SimulationParameters parameters,
        double centerX,
        double centerY,
        double strength)
    {
        double radius = Math.Max(3.5, Math.Min(width, height) * 0.20);
        double effectiveElevationVariation = GetEffectiveElevationVariation(parameters.ElevationVariation, parameters);
        double amplitude = Math.Max(5.0, effectiveElevationVariation * 0.75 * strength);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double dx = x - centerX;
                double dy = y - centerY;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance > radius * 1.9)
                    continue;

                double normalized = distance / radius;
                double falloff = Math.Exp(-(normalized * normalized) * 1.20);
                elevationMap[x, y] += amplitude * falloff;
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
        bool vertical = width >= height;
        int thickness = Math.Max(1, Math.Min(4, Math.Min(width, height) / 10));

        if (vertical)
        {
            double centerX = width * (0.28 + random.NextDouble() * 0.44);

            for (int y = 0; y < height; y++)
            {
                double drift =
                    Math.Sin((double)y / Math.Max(4.0, height / 6.0)) * (1.8 + random.NextDouble() * 1.6) +
                    Math.Cos((double)y / Math.Max(5.0, height / 7.0)) * 0.9;

                int riverX = (int)Math.Round(centerX + drift);

                for (int dx = -thickness; dx <= thickness; dx++)
                {
                    int x = riverX + dx;
                    if (x < 0 || x >= width)
                        continue;

                    vegetationMap[x, y] = VegetationType.Water;
                    moistureMap[x, y] = 1.0;
                    elevationMap[x, y] -= 10.0 + Math.Abs(dx) * 2.2;
                }

                for (int dx = -(thickness + 2); dx <= thickness + 2; dx++)
                {
                    int x = riverX + dx;
                    if (x < 0 || x >= width)
                        continue;

                    if (vegetationMap[x, y] == VegetationType.Water)
                        continue;

                    double bankFactor = 1.0 - (Math.Abs(dx) - thickness) / 3.0;
                    bankFactor = Math.Clamp(bankFactor, 0.0, 1.0);

                    moistureMap[x, y] = ClampMoisture(moistureMap[x, y] + 0.18 * bankFactor, parameters);
                    elevationMap[x, y] -= 3.0 * bankFactor;

                    if (bankFactor >= 0.65 && vegetationMap[x, y] == VegetationType.Coniferous)
                        vegetationMap[x, y] = VegetationType.Deciduous;
                }
            }
        }
        else
        {
            double centerY = height * (0.28 + random.NextDouble() * 0.44);

            for (int x = 0; x < width; x++)
            {
                double drift =
                    Math.Sin((double)x / Math.Max(4.0, width / 6.0)) * (1.8 + random.NextDouble() * 1.6) +
                    Math.Cos((double)x / Math.Max(5.0, width / 7.0)) * 0.9;

                int riverY = (int)Math.Round(centerY + drift);

                for (int dy = -thickness; dy <= thickness; dy++)
                {
                    int y = riverY + dy;
                    if (y < 0 || y >= height)
                        continue;

                    vegetationMap[x, y] = VegetationType.Water;
                    moistureMap[x, y] = 1.0;
                    elevationMap[x, y] -= 10.0 + Math.Abs(dy) * 2.2;
                }

                for (int dy = -(thickness + 2); dy <= thickness + 2; dy++)
                {
                    int y = riverY + dy;
                    if (y < 0 || y >= height)
                        continue;

                    if (vegetationMap[x, y] == VegetationType.Water)
                        continue;

                    double bankFactor = 1.0 - (Math.Abs(dy) - thickness) / 3.0;
                    bankFactor = Math.Clamp(bankFactor, 0.0, 1.0);

                    moistureMap[x, y] = ClampMoisture(moistureMap[x, y] + 0.18 * bankFactor, parameters);
                    elevationMap[x, y] -= 3.0 * bankFactor;

                    if (bankFactor >= 0.65 && vegetationMap[x, y] == VegetationType.Coniferous)
                        vegetationMap[x, y] = VegetationType.Deciduous;
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
        double centerX = width * (0.35 + random.NextDouble() * 0.30);
        double centerY = height * (0.35 + random.NextDouble() * 0.30);
        double radiusX = Math.Max(2.5, width * (0.14 + random.NextDouble() * 0.09));
        double radiusY = Math.Max(2.5, height * (0.14 + random.NextDouble() * 0.09));

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double nx = (x - centerX) / radiusX;
                double ny = (y - centerY) / radiusY;
                double distance = nx * nx + ny * ny;

                if (distance <= 1.0)
                {
                    vegetationMap[x, y] = VegetationType.Water;
                    moistureMap[x, y] = 1.0;
                    elevationMap[x, y] -= 12.0 * (1.08 - distance);
                    continue;
                }

                if (distance <= 1.55)
                {
                    double shoreFactor = Math.Clamp(1.55 - distance, 0.0, 0.55) / 0.55;
                    moistureMap[x, y] = ClampMoisture(moistureMap[x, y] + 0.24 * shoreFactor, parameters);
                    elevationMap[x, y] -= 4.0 * shoreFactor;

                    if (shoreFactor >= 0.50 && vegetationMap[x, y] == VegetationType.Coniferous)
                        vegetationMap[x, y] = VegetationType.Deciduous;

                    if (shoreFactor >= 0.70 && vegetationMap[x, y] == VegetationType.Grass)
                        vegetationMap[x, y] = VegetationType.Shrub;
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
        bool vertical = width >= height;
        int thickness = Math.Max(1, Math.Min(3, Math.Min(width, height) / 14));

        if (vertical)
        {
            int centerX = (int)Math.Round(width * (0.34 + random.NextDouble() * 0.32));

            for (int x = centerX - thickness; x <= centerX + thickness; x++)
            {
                if (x < 0 || x >= width)
                    continue;

                for (int y = 0; y < height; y++)
                {
                    vegetationMap[x, y] = VegetationType.Bare;
                    moistureMap[x, y] = Math.Min(moistureMap[x, y], 0.12);
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int grassX = centerX + side * (thickness + 1);
                if (grassX < 0 || grassX >= width)
                    continue;

                for (int y = 0; y < height; y++)
                {
                    if (vegetationMap[grassX, y] != VegetationType.Water)
                    {
                        vegetationMap[grassX, y] = VegetationType.Grass;
                        moistureMap[grassX, y] = ClampMoisture(moistureMap[grassX, y] - 0.05, parameters);
                    }
                }
            }
        }
        else
        {
            int centerY = (int)Math.Round(height * (0.34 + random.NextDouble() * 0.32));

            for (int y = centerY - thickness; y <= centerY + thickness; y++)
            {
                if (y < 0 || y >= height)
                    continue;

                for (int x = 0; x < width; x++)
                {
                    vegetationMap[x, y] = VegetationType.Bare;
                    moistureMap[x, y] = Math.Min(moistureMap[x, y], 0.12);
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int grassY = centerY + side * (thickness + 1);
                if (grassY < 0 || grassY >= height)
                    continue;

                for (int x = 0; x < width; x++)
                {
                    if (vegetationMap[x, grassY] != VegetationType.Water)
                    {
                        vegetationMap[x, grassY] = VegetationType.Grass;
                        moistureMap[x, grassY] = ClampMoisture(moistureMap[x, grassY] - 0.05, parameters);
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

                if (mapObject.Shape == MapObjectShape.Ellipse)
                {
                    double nx = (x - centerX) / halfWidth;
                    double ny = (y - centerY) / halfHeight;
                    double d = nx * nx + ny * ny;

                    if (d > 1.0)
                        continue;

                    influence = Math.Clamp(1.0 - d, 0.15, 1.0);
                }
                else
                {
                    double dx = Math.Abs(x - centerX) / halfWidth;
                    double dy = Math.Abs(y - centerY) / halfHeight;
                    double edge = Math.Max(dx, dy);
                    influence = Math.Clamp(1.0 - edge * 0.75, 0.20, 1.0);
                }

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
        double noiseStrength = Math.Clamp(parameters.MapNoiseStrength, 0.0, 0.30);

        if (noiseStrength <= 0.0001)
        {
            ClampMaps(width, height, vegetationMap, moistureMap, elevationMap, parameters);
            return;
        }

        double moistureAmplitude = 0.12 * noiseStrength;
        double elevationAmplitude = Math.Max(1.0, GetEffectiveElevationVariation(parameters.ElevationVariation, parameters) * 0.12 * noiseStrength);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (vegetationMap[x, y] != VegetationType.Water)
                {
                    moistureMap[x, y] += (random.NextDouble() * 2.0 - 1.0) * moistureAmplitude;
                    elevationMap[x, y] += (random.NextDouble() * 2.0 - 1.0) * elevationAmplitude;
                }
            }
        }

        ClampMaps(width, height, vegetationMap, moistureMap, elevationMap, parameters);
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
            .Select(v => new
            {
                v.VegetationType,
                Weight = AdjustFuelWeightForDensity(v.VegetationType, Math.Max(0.0, v.Probability), fuelDensityFactor)
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

    private static int[] BuildCounts(int totalNodes, double[] shares, int minPerCluster)
    {
        var counts = new int[shares.Length];
        int assigned = 0;

        for (int i = 0; i < shares.Length; i++)
        {
            counts[i] = Math.Max(minPerCluster, (int)Math.Round(totalNodes * shares[i]));
            assigned += counts[i];
        }

        while (assigned > totalNodes)
        {
            int index = Array.IndexOf(counts, counts.Max());
            if (counts[index] <= minPerCluster)
                break;

            counts[index]--;
            assigned--;
        }

        while (assigned < totalNodes)
        {
            int index = Array.IndexOf(counts, counts.Min());
            counts[index]++;
            assigned++;
        }

        return counts;
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

    private void SetBackingField(object target, string backingFieldName, object? value)
    {
        var field = target.GetType().GetField(backingFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}