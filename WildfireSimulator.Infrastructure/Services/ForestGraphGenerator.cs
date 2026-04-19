using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using WildfireSimulator.Domain.Models;
using WildfireSimulator.Application.Interfaces;

namespace WildfireSimulator.Infrastructure.Services;

public class ForestGraphGenerator : IForestGraphGenerator
{
    private readonly ILogger<ForestGraphGenerator> _logger;

    public ForestGraphGenerator(ILogger<ForestGraphGenerator> logger)
    {
        _logger = logger;
    }

    private int GetClusteredLocalTargetDegree(int nodeCount)
    {
        if (nodeCount <= 8) return 2;
        if (nodeCount <= 12) return 2;
        if (nodeCount <= 18) return 2;
        if (nodeCount <= 32) return 3;
        if (nodeCount <= 80) return 3;
        if (nodeCount <= 180) return 3;
        return 4;
    }


    private int GetClusteredMaxDegree(int nodeCount)
    {
        if (nodeCount <= 8) return 3;
        if (nodeCount <= 12) return 3;
        if (nodeCount <= 18) return 3;
        if (nodeCount <= 32) return 4;
        if (nodeCount <= 80) return 4;
        if (nodeCount <= 250) return 4;
        return 5;
    }


    private double GetClusteredCloseRadius(int nodeCount)
    {
        if (nodeCount <= 8) return 1.75;
        if (nodeCount <= 12) return 1.95;
        if (nodeCount <= 18) return 2.05;
        if (nodeCount <= 30) return 2.20;
        if (nodeCount <= 80) return 2.40;
        if (nodeCount <= 160) return 2.60;
        return 2.80;
    }
    private double GetClusteredSupportRadius(int nodeCount)
    {
        if (nodeCount <= 8) return 2.35;
        if (nodeCount <= 12) return 2.55;
        if (nodeCount <= 18) return 2.75;
        if (nodeCount <= 30) return 3.00;
        if (nodeCount <= 80) return 3.35;
        if (nodeCount <= 160) return 3.70;
        return 4.00;
    }

    private double GetClusteredExtendedRadius(int nodeCount)
    {
        if (nodeCount <= 8) return 2.90;
        if (nodeCount <= 12) return 3.15;
        if (nodeCount <= 18) return 3.35;
        if (nodeCount <= 30) return 3.70;
        if (nodeCount <= 80) return 4.25;
        if (nodeCount <= 160) return 4.75;
        return 5.20;
    }

    private int GetClusteredExtendedEdgeBudget(int nodeCount)
    {
        if (nodeCount <= 8) return 0;
        if (nodeCount <= 12) return 1;
        if (nodeCount <= 18) return 1;
        if (nodeCount <= 30) return 2;
        if (nodeCount <= 90) return 3;
        if (nodeCount <= 180) return 6;
        if (nodeCount <= 250) return 8;
        return 12;
    }

    private double GetClusteredPlacementScale(int nodeCount)
    {
        if (nodeCount <= 8) return 0.95;
        if (nodeCount <= 12) return 1.00;
        if (nodeCount <= 18) return 1.08;
        if (nodeCount <= 30) return 1.18;
        if (nodeCount <= 72) return 1.32;
        if (nodeCount <= 200) return 1.45;
        return 1.52;
    }

    private int GetClusteredPatchCount(int nodeCount)
    {
        if (nodeCount <= 8) return 2;
        if (nodeCount <= 12) return 2;
        if (nodeCount <= 18) return 3;
        if (nodeCount <= 30) return 3;
        if (nodeCount <= 60) return 4;
        if (nodeCount <= 140) return 5;
        if (nodeCount <= 260) return 6;
        return 7;
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
            "Сгенерирована сетка: {Cells} клеток, {Edges} ребер, режим={Mode}",
            graph.Cells.Count,
            graph.Edges.Count,
            parameters.MapCreationMode);

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
            {
                map[x, y] = GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters);
            }
        }

        ApplyConnectedSurfaceZones(
            map,
            width,
            height,
            parameters.VegetationDistributions,
            VegetationType.Water,
            random);

        ApplyConnectedSurfaceZones(
            map,
            width,
            height,
            parameters.VegetationDistributions,
            VegetationType.Bare,
            random);

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
            remaining -= zonePainted;
            zonesLeft--;
        }

        if (remaining > 0)
        {
            var fallback = GetAllGridPoints(width, height)
                .Where(p => map[p.X, p.Y] != VegetationType.Water && map[p.X, p.Y] != VegetationType.Bare)
                .OrderBy(_ => random.Next())
                .Take(remaining)
                .ToList();

            foreach (var point in fallback)
                map[point.X, point.Y] = targetType;
        }
    }
    private int PaintConnectedSurfaceZone(
        VegetationType[,] map,
        int width,
        int height,
        int startX,
        int startY,
        int targetCount,
        VegetationType targetType,
        Random random)
    {
        if (targetCount <= 0)
            return 0;

        if (startX < 0 || startX >= width || startY < 0 || startY >= height)
            return 0;

        if (map[startX, startY] == VegetationType.Water || map[startX, startY] == VegetationType.Bare)
            return 0;

        int painted = 0;

        var queue = new Queue<(int X, int Y)>();
        var visited = new HashSet<(int X, int Y)>();

        queue.Enqueue((startX, startY));
        visited.Add((startX, startY));

        double biasX = random.NextDouble() - random.NextDouble();
        double biasY = random.NextDouble() - random.NextDouble();

        while (queue.Count > 0 && painted < targetCount)
        {
            var current = queue.Dequeue();

            if (map[current.X, current.Y] != VegetationType.Water &&
                map[current.X, current.Y] != VegetationType.Bare)
            {
                map[current.X, current.Y] = targetType;
                painted++;
            }

            if (painted >= targetCount)
                break;

            var neighbors = GetGridNeighbors8(current.X, current.Y, width, height)
                .Where(n => !visited.Contains(n))
                .Where(n => map[n.X, n.Y] != VegetationType.Water && map[n.X, n.Y] != VegetationType.Bare)
                .Select(n =>
                {
                    double dx = n.X - current.X;
                    double dy = n.Y - current.Y;
                    double directionalBias = dx * biasX + dy * biasY;
                    double score = random.NextDouble() + directionalBias * 0.20;
                    return (n.X, n.Y, Score: score);
                })
                .OrderByDescending(n => n.Score)
                .ToList();

            foreach (var neighbor in neighbors)
            {
                if (!visited.Add((neighbor.X, neighbor.Y)))
                    continue;

                if (random.NextDouble() < 0.88 || queue.Count == 0)
                    queue.Enqueue((neighbor.X, neighbor.Y));
            }
        }

        return painted;
    }
    private int TryPaintWaterBarrier(
        VegetationType[,] map,
        int width,
        int height,
        int targetCount,
        Random random)
    {
        bool vertical = width >= height;
        int thickness = targetCount >= 24 ? 2 : 1;
        int painted = 0;

        if (vertical)
        {
            int x = random.Next(1, Math.Max(2, width - thickness));

            for (int y = 0; y < height && painted < targetCount / 2; y++)
            {
                for (int dx = 0; dx < thickness && painted < targetCount / 2; dx++)
                {
                    int px = Math.Min(width - 1, x + dx);

                    if (map[px, y] == VegetationType.Water || map[px, y] == VegetationType.Bare)
                        continue;

                    map[px, y] = VegetationType.Water;
                    painted++;
                }
            }
        }
        else
        {
            int y = random.Next(1, Math.Max(2, height - thickness));

            for (int x = 0; x < width && painted < targetCount / 2; x++)
            {
                for (int dy = 0; dy < thickness && painted < targetCount / 2; dy++)
                {
                    int py = Math.Min(height - 1, y + dy);

                    if (map[x, py] == VegetationType.Water || map[x, py] == VegetationType.Bare)
                        continue;

                    map[x, py] = VegetationType.Water;
                    painted++;
                }
            }
        }

        return painted;
    }
    private int EstimateGridSurfaceZoneCount(int targetCount, VegetationType targetType)
    {
        if (targetType == VegetationType.Water)
        {
            if (targetCount <= 8) return 1;
            if (targetCount <= 24) return 2;
            if (targetCount <= 60) return 3;
            return 4;
        }

        if (targetCount <= 6) return 1;
        if (targetCount <= 18) return 2;
        if (targetCount <= 45) return 3;
        return 4;
    }
    private VegetationType GetRandomCombustibleVegetation(
    List<VegetationDistribution> distributions,
    Random random)
    {
        return GetRandomCombustibleVegetation(distributions, random, null);
    }

    private VegetationType GetRandomCombustibleVegetation(
        List<VegetationDistribution> distributions,
        Random random,
        SimulationParameters? parameters)
    {
        var combustibleTypes = new[]
        {
        VegetationType.Grass,
        VegetationType.Shrub,
        VegetationType.Deciduous,
        VegetationType.Coniferous,
        VegetationType.Mixed
    };

        var fuelDensityFactor = GetNormalizedFuelDensity(parameters);

        if (distributions == null || distributions.Count == 0)
        {
            var weightedDefaults = new List<(VegetationType Type, double Weight)>
        {
            (VegetationType.Coniferous, AdjustFuelWeightForDensity(VegetationType.Coniferous, 0.30, fuelDensityFactor)),
            (VegetationType.Deciduous, AdjustFuelWeightForDensity(VegetationType.Deciduous, 0.20, fuelDensityFactor)),
            (VegetationType.Mixed, AdjustFuelWeightForDensity(VegetationType.Mixed, 0.20, fuelDensityFactor)),
            (VegetationType.Shrub, AdjustFuelWeightForDensity(VegetationType.Shrub, 0.15, fuelDensityFactor)),
            (VegetationType.Grass, AdjustFuelWeightForDensity(VegetationType.Grass, 0.15, fuelDensityFactor))
        };

            double total = weightedDefaults.Sum(x => x.Weight);
            double roll = random.NextDouble() * total;
            double cumulative = 0.0;

            foreach (var item in weightedDefaults)
            {
                cumulative += item.Weight;
                if (roll <= cumulative)
                    return item.Type;
            }

            return weightedDefaults[^1].Type;
        }

        var weighted = combustibleTypes
            .Select(type => new
            {
                Type = type,
                Weight = AdjustFuelWeightForDensity(
                    type,
                    Math.Max(0.0, GetVegetationProbability(distributions, type)),
                    fuelDensityFactor)
            })
            .ToList();

        double totalWeight = weighted.Sum(x => x.Weight);

        if (totalWeight <= 0.000001)
            return VegetationType.Mixed;

        double randomValue = random.NextDouble() * totalWeight;
        double cumulativeWeight = 0.0;

        foreach (var item in weighted)
        {
            cumulativeWeight += item.Weight;
            if (randomValue <= cumulativeWeight)
                return item.Type;
        }

        return weighted[^1].Type;
    }
    private (double Min, double Max) GetEffectiveMoistureRange(SimulationParameters parameters)
    {
        double baseMin = Math.Clamp(parameters.InitialMoistureMin, 0.0, 1.0);
        double baseMax = Math.Clamp(parameters.InitialMoistureMax, 0.0, 1.0);

        if (baseMax < baseMin)
            (baseMin, baseMax) = (baseMax, baseMin);

        double dryness = Math.Clamp(parameters.MapDrynessFactor, 0.5, 1.5);
        double offset = (dryness - 1.0) * 0.18;

        double min = Math.Clamp(baseMin - offset, 0.0, 1.0);
        double max = Math.Clamp(baseMax - offset, 0.0, 1.0);

        if (max < min)
            max = min;

        return (min, max);
    }

    private double GetEffectiveElevationVariation(double baseVariation, SimulationParameters parameters)
    {
        double relief = Math.Clamp(parameters.ReliefStrengthFactor, 0.5, 1.5);
        return Math.Max(1.0, baseVariation * relief);
    }

    private double GetNormalizedFuelDensity(SimulationParameters? parameters)
    {
        if (parameters == null)
            return 1.0;

        return Math.Clamp(parameters.FuelDensityFactor, 0.5, 1.5);
    }

    private double AdjustFuelWeightForDensity(VegetationType type, double baseWeight, double fuelDensityFactor)
    {
        if (baseWeight <= 0.0)
            return 0.0;

        double adjusted = type switch
        {
            VegetationType.Coniferous => baseWeight * (0.72 + 0.56 * fuelDensityFactor),
            VegetationType.Mixed => baseWeight * (0.80 + 0.40 * fuelDensityFactor),
            VegetationType.Shrub => baseWeight * (0.85 + 0.30 * fuelDensityFactor),
            VegetationType.Deciduous => baseWeight * (1.18 - 0.18 * fuelDensityFactor),
            VegetationType.Grass => baseWeight * (1.22 - 0.22 * fuelDensityFactor),
            _ => baseWeight
        };

        return Math.Max(0.0001, adjusted);
    }


    private double GetVegetationProbability(
        List<VegetationDistribution> distributions,
        VegetationType vegetationType)
    {
        if (distributions == null || distributions.Count == 0)
            return 0.0;

        var item = distributions.FirstOrDefault(v => v.VegetationType == vegetationType);
        return item?.Probability ?? 0.0;
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

    public async Task<ForestGraph> GenerateClusteredGraphAsync(int nodeCount, SimulationParameters parameters)
    {
        _logger.LogInformation(
            "Генерация ClusteredGraph. RequestedNodeCount={NodeCount}, Mode={Mode}, Scenario={Scenario}, HasBlueprint={HasBlueprint}",
            nodeCount,
            parameters.MapCreationMode,
            parameters.ClusteredScenarioType,
            parameters.ClusteredBlueprint != null);

        if (parameters.MapCreationMode == MapCreationMode.SemiManual &&
            parameters.ClusteredBlueprint != null &&
            parameters.ClusteredBlueprint.Nodes != null &&
            parameters.ClusteredBlueprint.Nodes.Count > 0)
        {
            _logger.LogInformation(
                "ClusteredGraph: используется strict blueprint pipeline. Nodes={Nodes}, Edges={Edges}",
                parameters.ClusteredBlueprint.Nodes.Count,
                parameters.ClusteredBlueprint.Edges?.Count ?? 0);

            var blueprintGraph = BuildClusteredGraphFromBlueprint(parameters.ClusteredBlueprint, parameters);

            _logger.LogInformation(
                "ClusteredGraph blueprint построен без автогенерации: Cells={Cells}, Edges={Edges}",
                blueprintGraph.Cells.Count,
                blueprintGraph.Edges.Count);

            return await Task.FromResult(blueprintGraph);
        }

        var generatedGraph = await GenerateClusteredGraphByScenarioAsync(nodeCount, parameters);

        _logger.LogInformation(
            "ClusteredGraph сгенерирован сценарием: Cells={Cells}, Edges={Edges}",
            generatedGraph.Cells.Count,
            generatedGraph.Edges.Count);

        return generatedGraph;
    }

    private void BuildClusteredRandomGraph(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     int maxDegree)
    {
        var patches = CreateClusteredPatchesRandomOnly(
            GetClusteredPatchCount(nodeCount),
            graph.Width,
            graph.Height,
            parameters,
            random);

        var coordinates = GeneratePatchDrivenClusteredCoordinates(
            nodeCount,
            graph.Width,
            graph.Height,
            patches,
            random);

        foreach (var (x, y) in coordinates)
        {
            var patch = GetBestPatchForPoint(x, y, patches);

            var vegetation = random.NextDouble() < 0.78
                ? patch.DominantVegetation
                : GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters);

            var moisture = Math.Clamp(
                patch.BaseMoisture + (random.NextDouble() * 2.0 - 1.0) * 0.08,
                0.02,
                0.98);

            var elevation = patch.BaseElevation
                + (random.NextDouble() * 2.0 - 1.0) * Math.Max(2.0, parameters.ElevationVariation * 0.10);

            var cell = new ForestCell(
                x,
                y,
                vegetation,
                moisture,
                elevation,
                $"patch-{patch.Index}");

            graph.Cells.Add(cell);
        }

        CreateDenseLocalEdges(graph, maxDegree);
        CreateClusterBridges(graph, patches, random, maxDegree);
        ApplyClusteredBridgeWeakening(graph);
        ApplySurfaceBarrierEdgeModifiers(graph);
        EnsureNoIsolatedNodes(graph, maxDegree);
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

            double dist = CalculateDistance(cell.X, cell.Y, nearest.X, nearest.Y);
            if (dist <= 0.001)
                continue;

            double slope = (nearest.Elevation - cell.Elevation) / dist;

            var edge = new ForestEdge(cell, nearest, dist, slope);
            SetEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * 0.55, 0.02, 0.85));
            graph.Edges.Add(edge);
        }
    }
    private void CreateClusterBridges(
     ForestGraph graph,
     List<ClusteredPatch> patches,
     Random random,
     int maxDegree)
    {
        if (graph.Cells.Count <= 1 || patches.Count <= 1)
            return;

        int bridgeDegreeLimit = Math.Max(2, maxDegree);

        var cellsByCluster = graph.Cells
            .Where(c => !string.IsNullOrWhiteSpace(c.ClusterId))
            .GroupBy(c => c.ClusterId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var patchByClusterId = patches.ToDictionary(
            p => $"patch-{p.Index}",
            p => p);

        var orderedPatchPairs = new List<(ClusteredPatch A, ClusteredPatch B, double Distance)>();

        for (int i = 0; i < patches.Count; i++)
        {
            for (int j = i + 1; j < patches.Count; j++)
            {
                var a = patches[i];
                var b = patches[j];

                var distance = CalculateDistance(a.CenterX, a.CenterY, b.CenterX, b.CenterY);
                orderedPatchPairs.Add((a, b, distance));
            }
        }

        orderedPatchPairs = orderedPatchPairs
            .OrderBy(x => x.Distance)
            .ToList();

        var connectedClusters = new HashSet<string>();

        foreach (var pair in orderedPatchPairs)
        {
            string clusterA = $"patch-{pair.A.Index}";
            string clusterB = $"patch-{pair.B.Index}";

            if (!cellsByCluster.TryGetValue(clusterA, out var firstCells) ||
                !cellsByCluster.TryGetValue(clusterB, out var secondCells))
            {
                continue;
            }

            var limitedA = firstCells
                .OrderBy(c => CalculateDistance(c.X, c.Y, pair.A.CenterX, pair.A.CenterY))
                .Take(6)
                .ToList();

            var limitedB = secondCells
                .OrderBy(c => CalculateDistance(c.X, c.Y, pair.B.CenterX, pair.B.CenterY))
                .Take(6)
                .ToList();

            var bestPair = limitedA
                .Where(a => GetNodeDegree(graph, a) < bridgeDegreeLimit)
                .SelectMany(a => limitedB
                    .Where(b => GetNodeDegree(graph, b) < bridgeDegreeLimit)
                    .Select(b => new
                    {
                        A = a,
                        B = b,
                        Distance = CalculateDistance(a.X, a.Y, b.X, b.Y)
                    }))
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (bestPair == null)
                continue;

            if (!EdgeExists(graph, bestPair.A, bestPair.B))
            {
                TryAddEdge(graph, bestPair.A, bestPair.B);

                var createdEdge = graph.Edges.LastOrDefault(e =>
                    (e.FromCellId == bestPair.A.Id && e.ToCellId == bestPair.B.Id) ||
                    (e.FromCellId == bestPair.B.Id && e.ToCellId == bestPair.A.Id));

                if (createdEdge != null)
                {
                    double bridgeScale = 0.36 + random.NextDouble() * 0.14;
                    SetEdgeFireSpreadModifier(
                        createdEdge,
                        Math.Clamp(createdEdge.FireSpreadModifier * bridgeScale, 0.02, 0.55));
                }
            }

            connectedClusters.Add(clusterA);
            connectedClusters.Add(clusterB);
        }

        var isolatedClusters = cellsByCluster.Keys
            .Where(clusterId => !connectedClusters.Contains(clusterId))
            .ToList();

        foreach (var isolatedCluster in isolatedClusters)
        {
            if (!cellsByCluster.TryGetValue(isolatedCluster, out var isolatedCells) || isolatedCells.Count == 0)
                continue;

            if (!patchByClusterId.TryGetValue(isolatedCluster, out var isolatedPatch))
                continue;

            var nearestOtherPatch = patches
                .Where(p => $"patch-{p.Index}" != isolatedCluster)
                .OrderBy(p => CalculateDistance(isolatedPatch.CenterX, isolatedPatch.CenterY, p.CenterX, p.CenterY))
                .FirstOrDefault();

            if (nearestOtherPatch == null)
                continue;

            string targetCluster = $"patch-{nearestOtherPatch.Index}";
            if (!cellsByCluster.TryGetValue(targetCluster, out var targetCells) || targetCells.Count == 0)
                continue;

            var bestPair = isolatedCells
                .Take(6)
                .SelectMany(a => targetCells
                    .Take(6)
                    .Select(b => new
                    {
                        A = a,
                        B = b,
                        Distance = CalculateDistance(a.X, a.Y, b.X, b.Y)
                    }))
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (bestPair == null || EdgeExists(graph, bestPair.A, bestPair.B))
                continue;

            TryAddEdge(graph, bestPair.A, bestPair.B);

            var createdEdge = graph.Edges.LastOrDefault(e =>
                (e.FromCellId == bestPair.A.Id && e.ToCellId == bestPair.B.Id) ||
                (e.FromCellId == bestPair.B.Id && e.ToCellId == bestPair.A.Id));

            if (createdEdge != null)
            {
                SetEdgeFireSpreadModifier(
                    createdEdge,
                    Math.Clamp(createdEdge.FireSpreadModifier * 0.34, 0.02, 0.45));
            }
        }
    }
    private void BuildClusteredScenarioGraph(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     int maxDegree)
    {
        var scenario = parameters.ClusteredScenarioType ?? ClusteredScenarioType.DenseDryConiferous;

        _logger.LogInformation(
            "Clustered scenario generation: Scenario={Scenario}, NodeCount={NodeCount}, Width={Width}, Height={Height}",
            scenario,
            nodeCount,
            graph.Width,
            graph.Height);

        switch (scenario)
        {
            case ClusteredScenarioType.DenseDryConiferous:
                BuildDenseDryConiferousScenario(
                    graph,
                    nodeCount,
                    parameters,
                    random,
                    maxDegree);
                return;

            case ClusteredScenarioType.WaterBarrier:
                BuildWaterBarrierScenario(
                    graph,
                    nodeCount,
                    parameters,
                    random,
                    maxDegree);
                return;

            case ClusteredScenarioType.FirebreakGap:
                BuildFirebreakGapScenario(
                    graph,
                    nodeCount,
                    parameters,
                    random,
                    maxDegree);
                return;

            default:
                var patches = CreateScenarioDrivenClusteredPatches(
                    scenario,
                    nodeCount,
                    graph.Width,
                    graph.Height,
                    parameters,
                    random);

                var coordinates = GeneratePatchDrivenClusteredCoordinates(
                    nodeCount,
                    graph.Width,
                    graph.Height,
                    patches,
                    random);

                foreach (var (x, y) in coordinates)
                {
                    var patch = GetBestPatchForPoint(x, y, patches);

                    var vegetation = SelectScenarioVegetationForPatch(
                        patch,
                        scenario,
                        parameters,
                        random);

                    var moisture = Math.Clamp(
                        patch.BaseMoisture + (random.NextDouble() * 2.0 - 1.0) * 0.08,
                        0.02,
                        0.98);

                    var elevation = patch.BaseElevation
                        + (random.NextDouble() * 2.0 - 1.0) * Math.Max(2.0, parameters.ElevationVariation * 0.12);

                    var cell = new ForestCell(
                        x,
                        y,
                        vegetation,
                        moisture,
                        elevation,
                        $"patch-{patch.Index}");

                    graph.Cells.Add(cell);
                }

                CreateDenseLocalEdges(graph, maxDegree);
                ApplyClusteredScenarioBridgePolicy(graph, patches, scenario, random);
                ApplyClusteredBridgeWeakening(graph);
                ApplyClusteredScenarioNodeAdjustments(graph, scenario, parameters, random);
                ApplySurfaceBarrierEdgeModifiers(graph);
                EnsureNoIsolatedNodes(graph, maxDegree);
                return;
        }
    }


    private void BuildDenseDryConiferousScenario(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     int maxDegree)
    {
        var used = new HashSet<(int X, int Y)>();
        int total = Math.Max(8, nodeCount);

        int[] BuildCounts(int totalNodes, double[] shares, int minPerCluster)
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

        if (total <= 15)
        {
            var counts = BuildCounts(total, new[] { 0.38, 0.34, 0.28 }, 2);

            var c1 = AddScenarioPatchNodesEllipse(
                graph, used, "dense-small-1",
                0.25, 0.34, 0.22, 0.20, counts[0],
                parameters, random,
                VegetationType.Coniferous, 0.10, 0.18, -8.0, 6.0);

            var c2 = AddScenarioPatchNodesEllipse(
                graph, used, "dense-small-2",
                0.70, 0.32, 0.22, 0.20, counts[1],
                parameters, random,
                VegetationType.Coniferous, 0.10, 0.19, -5.0, 8.0);

            var c3 = AddScenarioPatchNodesEllipse(
                graph, used, "dense-small-3",
                0.48, 0.72, 0.24, 0.22, counts[2],
                parameters, random,
                VegetationType.Mixed, 0.11, 0.20, -3.0, 10.0);

            ConnectScenarioNodesLocally(graph, c1, 2.65, maxDegree);
            ConnectScenarioNodesLocally(graph, c2, 2.65, maxDegree);
            ConnectScenarioNodesLocally(graph, c3, 2.75, maxDegree);

            AddScenarioBridge(graph, c1, c2, 1, 1.24);
            AddScenarioBridge(graph, c1, c3, 1, 1.18);
            AddScenarioBridge(graph, c2, c3, 1, 1.18);

            ApplySurfaceBarrierEdgeModifiers(graph);
            return;
        }

        if (total <= 30)
        {
            var counts = BuildCounts(total, new[] { 0.28, 0.24, 0.22, 0.26 }, 4);

            var c1 = AddScenarioPatchNodesEllipse(
                graph, used, "dense-mid-1",
                0.22, 0.28, 0.20, 0.18, counts[0],
                parameters, random,
                VegetationType.Coniferous, 0.10, 0.18, -8.0, 6.0);

            var c2 = AddScenarioPatchNodesEllipse(
                graph, used, "dense-mid-2",
                0.66, 0.26, 0.21, 0.18, counts[1],
                parameters, random,
                VegetationType.Coniferous, 0.10, 0.19, -5.0, 8.0);

            var c3 = AddScenarioPatchNodesEllipse(
                graph, used, "dense-mid-3",
                0.28, 0.72, 0.21, 0.19, counts[2],
                parameters, random,
                VegetationType.Coniferous, 0.11, 0.20, -3.0, 10.0);

            var c4 = AddScenarioPatchNodesEllipse(
                graph, used, "dense-mid-4",
                0.72, 0.70, 0.23, 0.21, counts[3],
                parameters, random,
                VegetationType.Mixed, 0.12, 0.22, -2.0, 12.0);

            ConnectScenarioNodesLocally(graph, c1, 2.45, maxDegree);
            ConnectScenarioNodesLocally(graph, c2, 2.45, maxDegree);
            ConnectScenarioNodesLocally(graph, c3, 2.45, maxDegree);
            ConnectScenarioNodesLocally(graph, c4, 2.55, maxDegree);

            AddScenarioBridge(graph, c1, c2, 1, 1.26);
            AddScenarioBridge(graph, c1, c3, 1, 1.20);
            AddScenarioBridge(graph, c2, c4, 1, 1.20);
            AddScenarioBridge(graph, c3, c4, 1, 1.22);

            ApplySurfaceBarrierEdgeModifiers(graph);
            return;
        }

        var largeCounts = BuildCounts(total, new[] { 0.24, 0.20, 0.18, 0.20, 0.18 }, 6);

        var l1 = AddScenarioPatchNodesEllipse(
            graph, used, "dense-1",
            0.22, 0.26, 0.20, 0.18, largeCounts[0],
            parameters, random,
            VegetationType.Coniferous, 0.10, 0.18, -8.0, 6.0);

        var l2 = AddScenarioPatchNodesEllipse(
            graph, used, "dense-2",
            0.48, 0.22, 0.22, 0.18, largeCounts[1],
            parameters, random,
            VegetationType.Coniferous, 0.10, 0.19, -5.0, 8.0);

        var l3 = AddScenarioPatchNodesEllipse(
            graph, used, "dense-3",
            0.73, 0.28, 0.20, 0.18, largeCounts[2],
            parameters, random,
            VegetationType.Coniferous, 0.11, 0.20, -3.0, 10.0);

        var l4 = AddScenarioPatchNodesEllipse(
            graph, used, "dense-4",
            0.34, 0.68, 0.22, 0.20, largeCounts[3],
            parameters, random,
            VegetationType.Coniferous, 0.12, 0.20, -10.0, 4.0);

        var l5 = AddScenarioPatchNodesEllipse(
            graph, used, "dense-5",
            0.70, 0.68, 0.24, 0.22, largeCounts[4],
            parameters, random,
            VegetationType.Mixed, 0.12, 0.22, -2.0, 12.0);

        ConnectScenarioNodesLocally(graph, l1, 2.40, maxDegree);
        ConnectScenarioNodesLocally(graph, l2, 2.45, maxDegree);
        ConnectScenarioNodesLocally(graph, l3, 2.35, maxDegree);
        ConnectScenarioNodesLocally(graph, l4, 2.45, maxDegree);
        ConnectScenarioNodesLocally(graph, l5, 2.55, maxDegree);

        AddScenarioBridge(graph, l1, l2, 2, 1.28);
        AddScenarioBridge(graph, l2, l3, 2, 1.24);
        AddScenarioBridge(graph, l1, l4, 2, 1.22);
        AddScenarioBridge(graph, l2, l5, 2, 1.20);
        AddScenarioBridge(graph, l4, l5, 2, 1.24);
        AddScenarioBridge(graph, l3, l5, 2, 1.18);

        ApplySurfaceBarrierEdgeModifiers(graph);
    }

    private void BuildWaterBarrierScenario(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     int maxDegree)
    {
        var used = new HashSet<(int X, int Y)>();
        int total = Math.Max(8, nodeCount);

        int[] BuildCounts(int totalNodes, double[] shares, int minPerCluster)
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

        double leftInnerMin = total <= 15 ? 0.44 : 0.40;
        double leftInnerMax = total <= 15 ? 0.49 : 0.47;
        double rightInnerMin = total <= 15 ? 0.51 : 0.53;
        double rightInnerMax = total <= 15 ? 0.56 : 0.60;

        VegetationType WaterEdgeSelector(int x, int y, VegetationType fallback)
        {
            double nx = graph.Width <= 1 ? 0.5 : (double)x / (graph.Width - 1);

            if ((nx >= leftInnerMin && nx <= leftInnerMax) ||
                (nx >= rightInnerMin && nx <= rightInnerMax))
            {
                return VegetationType.Water;
            }

            return fallback;
        }

        if (total <= 15)
        {
            var counts = BuildCounts(total, new[] { 0.50, 0.50 }, 3);

            var left = AddScenarioPatchNodesEllipse(
                graph, used, "water-small-left",
                0.24, 0.52, 0.24, 0.32, counts[0],
                parameters, random,
                VegetationType.Deciduous,
                0.34, 0.50,
                -6.0, 6.0,
                WaterEdgeSelector);

            var right = AddScenarioPatchNodesEllipse(
                graph, used, "water-small-right",
                0.76, 0.50, 0.24, 0.32, counts[1],
                parameters, random,
                VegetationType.Mixed,
                0.30, 0.46,
                -4.0, 8.0,
                WaterEdgeSelector);

            ConnectScenarioNodesLocally(graph, left, 2.55, maxDegree);
            ConnectScenarioNodesLocally(graph, right, 2.55, maxDegree);

            AddScenarioBridge(graph, left, right, 1, 0.18);

            ApplySurfaceBarrierEdgeModifiers(graph);
            return;
        }

        if (total <= 30)
        {
            var counts = BuildCounts(total, new[] { 0.26, 0.24, 0.26, 0.24 }, 4);

            var leftTop = AddScenarioPatchNodesEllipse(
                graph, used, "water-mid-left-1",
                0.25, 0.30, 0.18, 0.20, counts[0],
                parameters, random,
                VegetationType.Deciduous,
                0.34, 0.50,
                -6.0, 6.0,
                WaterEdgeSelector);

            var leftBottom = AddScenarioPatchNodesEllipse(
                graph, used, "water-mid-left-2",
                0.28, 0.72, 0.18, 0.18, counts[1],
                parameters, random,
                VegetationType.Mixed,
                0.32, 0.48,
                -8.0, 4.0,
                WaterEdgeSelector);

            var rightTop = AddScenarioPatchNodesEllipse(
                graph, used, "water-mid-right-1",
                0.72, 0.32, 0.18, 0.20, counts[2],
                parameters, random,
                VegetationType.Mixed,
                0.30, 0.46,
                -4.0, 8.0,
                WaterEdgeSelector);

            var rightBottom = AddScenarioPatchNodesEllipse(
                graph, used, "water-mid-right-2",
                0.70, 0.70, 0.18, 0.18, counts[3],
                parameters, random,
                VegetationType.Deciduous,
                0.32, 0.48,
                -5.0, 5.0,
                WaterEdgeSelector);

            ConnectScenarioNodesLocally(graph, leftTop, 2.35, maxDegree);
            ConnectScenarioNodesLocally(graph, leftBottom, 2.35, maxDegree);
            ConnectScenarioNodesLocally(graph, rightTop, 2.35, maxDegree);
            ConnectScenarioNodesLocally(graph, rightBottom, 2.35, maxDegree);

            AddScenarioBridge(graph, leftTop, leftBottom, 1, 1.02);
            AddScenarioBridge(graph, rightTop, rightBottom, 1, 1.04);
            AddScenarioBridge(graph, leftTop, rightTop, 1, 0.20);
            AddScenarioBridge(graph, leftBottom, rightBottom, 1, 0.18);

            ApplySurfaceBarrierEdgeModifiers(graph);
            return;
        }

        var largeCounts = BuildCounts(total, new[] { 0.24, 0.18, 0.28, 0.30 }, 6);

        var largeLeftTop = AddScenarioPatchNodesEllipse(
            graph, used, "water-left-1",
            0.24, 0.30, 0.20, 0.20, largeCounts[0],
            parameters, random,
            VegetationType.Deciduous,
            0.34, 0.50,
            -6.0, 6.0,
            WaterEdgeSelector);

        var largeLeftBottom = AddScenarioPatchNodesEllipse(
            graph, used, "water-left-2",
            0.28, 0.73, 0.18, 0.18, largeCounts[1],
            parameters, random,
            VegetationType.Mixed,
            0.32, 0.48,
            -8.0, 4.0,
            WaterEdgeSelector);

        var largeRightTop = AddScenarioPatchNodesEllipse(
            graph, used, "water-right-1",
            0.72, 0.32, 0.20, 0.22, largeCounts[2],
            parameters, random,
            VegetationType.Mixed,
            0.30, 0.46,
            -4.0, 8.0,
            WaterEdgeSelector);

        var largeRightBottom = AddScenarioPatchNodesEllipse(
            graph, used, "water-right-2",
            0.70, 0.72, 0.20, 0.20, largeCounts[3],
            parameters, random,
            VegetationType.Deciduous,
            0.32, 0.48,
            -5.0, 5.0,
            WaterEdgeSelector);

        ConnectScenarioNodesLocally(graph, largeLeftTop, 2.30, maxDegree);
        ConnectScenarioNodesLocally(graph, largeLeftBottom, 2.30, maxDegree);
        ConnectScenarioNodesLocally(graph, largeRightTop, 2.35, maxDegree);
        ConnectScenarioNodesLocally(graph, largeRightBottom, 2.35, maxDegree);

        AddScenarioBridge(graph, largeLeftTop, largeLeftBottom, 2, 1.02);
        AddScenarioBridge(graph, largeRightTop, largeRightBottom, 2, 1.04);
        AddScenarioBridge(graph, largeLeftTop, largeRightTop, 1, 0.22);
        AddScenarioBridge(graph, largeLeftBottom, largeRightBottom, 1, 0.20);

        ApplySurfaceBarrierEdgeModifiers(graph);
    }
    private void BuildFirebreakGapScenario(
     ForestGraph graph,
     int nodeCount,
     SimulationParameters parameters,
     Random random,
     int maxDegree)
    {
        var used = new HashSet<(int X, int Y)>();
        int total = Math.Max(8, nodeCount);

        int[] BuildCounts(int totalNodes, double[] shares, int minPerCluster)
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

        double leftInnerMin = total <= 15 ? 0.45 : 0.43;
        double leftInnerMax = total <= 15 ? 0.49 : 0.49;
        double rightInnerMin = total <= 15 ? 0.51 : 0.51;
        double rightInnerMax = total <= 15 ? 0.55 : 0.57;

        VegetationType BareEdgeSelector(int x, int y, VegetationType fallback)
        {
            double nx = graph.Width <= 1 ? 0.5 : (double)x / (graph.Width - 1);

            if ((nx >= leftInnerMin && nx <= leftInnerMax) ||
                (nx >= rightInnerMin && nx <= rightInnerMax))
            {
                return VegetationType.Bare;
            }

            return fallback;
        }

        if (total <= 15)
        {
            var counts = BuildCounts(total, new[] { 0.52, 0.48 }, 3);

            var left = AddScenarioPatchNodesEllipse(
                graph, used, "gap-small-left",
                0.24, 0.52, 0.24, 0.30, counts[0],
                parameters, random,
                VegetationType.Coniferous,
                0.18, 0.30,
                -4.0, 8.0,
                BareEdgeSelector);

            var right = AddScenarioPatchNodesEllipse(
                graph, used, "gap-small-right",
                0.76, 0.50, 0.24, 0.30, counts[1],
                parameters, random,
                VegetationType.Mixed,
                0.18, 0.30,
                -3.0, 9.0,
                BareEdgeSelector);

            ConnectScenarioNodesLocally(graph, left, 2.55, maxDegree);
            ConnectScenarioNodesLocally(graph, right, 2.55, maxDegree);

            AddScenarioBridge(graph, left, right, 1, 0.12);

            ApplySurfaceBarrierEdgeModifiers(graph);
            return;
        }

        if (total <= 30)
        {
            var counts = BuildCounts(total, new[] { 0.26, 0.24, 0.26, 0.24 }, 4);

            var leftTop = AddScenarioPatchNodesEllipse(
                graph, used, "gap-mid-left-1",
                0.25, 0.30, 0.18, 0.20, counts[0],
                parameters, random,
                VegetationType.Coniferous,
                0.18, 0.30,
                -4.0, 8.0,
                BareEdgeSelector);

            var leftBottom = AddScenarioPatchNodesEllipse(
                graph, used, "gap-mid-left-2",
                0.30, 0.72, 0.18, 0.18, counts[1],
                parameters, random,
                VegetationType.Mixed,
                0.20, 0.32,
                -6.0, 6.0,
                BareEdgeSelector);

            var rightTop = AddScenarioPatchNodesEllipse(
                graph, used, "gap-mid-right-1",
                0.72, 0.30, 0.18, 0.20, counts[2],
                parameters, random,
                VegetationType.Mixed,
                0.18, 0.30,
                -3.0, 9.0,
                BareEdgeSelector);

            var rightBottom = AddScenarioPatchNodesEllipse(
                graph, used, "gap-mid-right-2",
                0.70, 0.70, 0.18, 0.18, counts[3],
                parameters, random,
                VegetationType.Coniferous,
                0.18, 0.30,
                -4.0, 7.0,
                BareEdgeSelector);

            ConnectScenarioNodesLocally(graph, leftTop, 2.35, maxDegree);
            ConnectScenarioNodesLocally(graph, leftBottom, 2.35, maxDegree);
            ConnectScenarioNodesLocally(graph, rightTop, 2.35, maxDegree);
            ConnectScenarioNodesLocally(graph, rightBottom, 2.35, maxDegree);

            AddScenarioBridge(graph, leftTop, leftBottom, 1, 1.05);
            AddScenarioBridge(graph, rightTop, rightBottom, 1, 1.05);
            AddScenarioBridge(graph, leftTop, rightTop, 1, 0.14);
            AddScenarioBridge(graph, leftBottom, rightBottom, 1, 0.12);

            ApplySurfaceBarrierEdgeModifiers(graph);
            return;
        }

        var largeCounts = BuildCounts(total, new[] { 0.25, 0.20, 0.25, 0.30 }, 6);

        var largeLeftTop = AddScenarioPatchNodesEllipse(
            graph, used, "gap-left-1",
            0.25, 0.30, 0.19, 0.20, largeCounts[0],
            parameters, random,
            VegetationType.Coniferous,
            0.18, 0.30,
            -4.0, 8.0,
            BareEdgeSelector);

        var largeLeftBottom = AddScenarioPatchNodesEllipse(
            graph, used, "gap-left-2",
            0.30, 0.73, 0.18, 0.19, largeCounts[1],
            parameters, random,
            VegetationType.Mixed,
            0.20, 0.32,
            -6.0, 6.0,
            BareEdgeSelector);

        var largeRightTop = AddScenarioPatchNodesEllipse(
            graph, used, "gap-right-1",
            0.72, 0.30, 0.19, 0.20, largeCounts[2],
            parameters, random,
            VegetationType.Mixed,
            0.18, 0.30,
            -3.0, 9.0,
            BareEdgeSelector);

        var largeRightBottom = AddScenarioPatchNodesEllipse(
            graph, used, "gap-right-2",
            0.70, 0.72, 0.19, 0.19, largeCounts[3],
            parameters, random,
            VegetationType.Coniferous,
            0.18, 0.30,
            -4.0, 7.0,
            BareEdgeSelector);

        ConnectScenarioNodesLocally(graph, largeLeftTop, 2.35, maxDegree);
        ConnectScenarioNodesLocally(graph, largeLeftBottom, 2.35, maxDegree);
        ConnectScenarioNodesLocally(graph, largeRightTop, 2.35, maxDegree);
        ConnectScenarioNodesLocally(graph, largeRightBottom, 2.35, maxDegree);

        AddScenarioBridge(graph, largeLeftTop, largeLeftBottom, 2, 1.05);
        AddScenarioBridge(graph, largeRightTop, largeRightBottom, 2, 1.05);
        AddScenarioBridge(graph, largeLeftTop, largeRightTop, 1, 0.16);
        AddScenarioBridge(graph, largeLeftBottom, largeRightBottom, 1, 0.14);

        ApplySurfaceBarrierEdgeModifiers(graph);
    }

    private List<ForestCell> AddScenarioPatchNodesEllipse(
     ForestGraph graph,
     HashSet<(int X, int Y)> used,
     string clusterId,
     double centerXNorm,
     double centerYNorm,
     double radiusXNorm,
     double radiusYNorm,
     int requestedCount,
     SimulationParameters parameters,
     Random random,
     VegetationType dominantVegetation,
     double moistureMin,
     double moistureMax,
     double elevationMin,
     double elevationMax,
     Func<int, int, VegetationType, VegetationType>? vegetationSelector = null)
    {
        var result = new List<ForestCell>();

        int width = Math.Max(8, graph.Width);
        int height = Math.Max(8, graph.Height);

        double centerX = centerXNorm * (width - 1);
        double centerY = centerYNorm * (height - 1);
        double radiusX = Math.Max(2.0, radiusXNorm * width);
        double radiusY = Math.Max(2.0, radiusYNorm * height);

        var candidates = new List<(int X, int Y, double Score)>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (used.Contains((x, y)))
                    continue;

                double nx = (x - centerX) / radiusX;
                double ny = (y - centerY) / radiusY;
                double d = nx * nx + ny * ny;

                if (d > 1.0)
                    continue;

                double score = d + random.NextDouble() * 0.18;
                candidates.Add((x, y, score));
            }
        }

        foreach (var candidate in candidates.OrderBy(c => c.Score).Take(requestedCount))
        {
            VegetationType vegetation = dominantVegetation switch
            {
                VegetationType.Coniferous => random.NextDouble() < 0.82 ? VegetationType.Coniferous :
                                             random.NextDouble() < 0.65 ? VegetationType.Mixed :
                                             VegetationType.Shrub,

                VegetationType.Deciduous => random.NextDouble() < 0.78 ? VegetationType.Deciduous :
                                            random.NextDouble() < 0.60 ? VegetationType.Mixed :
                                            VegetationType.Shrub,

                VegetationType.Mixed => random.NextDouble() < 0.68 ? VegetationType.Mixed :
                                        random.NextDouble() < 0.55 ? VegetationType.Deciduous :
                                        VegetationType.Coniferous,

                _ => dominantVegetation
            };

            if (vegetationSelector != null)
                vegetation = vegetationSelector(candidate.X, candidate.Y, vegetation);

            double moisture = moistureMin + random.NextDouble() * Math.Max(0.01, moistureMax - moistureMin);
            moisture = Math.Clamp(
                moisture * Math.Clamp(parameters.MapDrynessFactor <= 0 ? 1.0 : (2.0 - parameters.MapDrynessFactor), 0.65, 1.35),
                0.02,
                0.98);

            double elevation = elevationMin + random.NextDouble() * Math.Max(0.1, elevationMax - elevationMin);
            elevation *= Math.Clamp(parameters.ReliefStrengthFactor, 0.5, 1.5);

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

            var cell = new ForestCell(
                candidate.X,
                candidate.Y,
                vegetation,
                moisture,
                elevation,
                clusterId);

            graph.Cells.Add(cell);
            result.Add(cell);
            used.Add((candidate.X, candidate.Y));
        }

        return result;
    }

    private void ConnectScenarioNodesLocally(
      ForestGraph graph,
      List<ForestCell> nodes,
      double radius,
      int maxDegree)
    {
        if (nodes == null || nodes.Count == 0)
            return;

        int localTargetDegree = Math.Min(maxDegree, GetClusteredLocalTargetDegree(nodes.Count));

        foreach (var source in nodes)
        {
            int sourceDegree = graph.GetIncidentEdges(source).Count;
            if (sourceDegree >= localTargetDegree)
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
                if (graph.GetIncidentEdges(source).Count >= localTargetDegree)
                    break;

                if (graph.GetIncidentEdges(candidate.Cell).Count >= maxDegree)
                    continue;

                if (EdgeExists(graph, source, candidate.Cell))
                    continue;

                TryAddEdge(graph, source, candidate.Cell);
            }
        }
    }
    private void ApplyClusteredBridgeWeakening(ForestGraph graph)
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
                Math.Clamp(edge.FireSpreadModifier * 0.72, 0.02, 1.10));
        }
    }
    private void AddScenarioBridge(
      ForestGraph graph,
      List<ForestCell> fromCluster,
      List<ForestCell> toCluster,
      int bridgeCount,
      double modifierMultiplier)
    {
        if (fromCluster == null || toCluster == null || fromCluster.Count == 0 || toCluster.Count == 0 || bridgeCount <= 0)
            return;

        int bridgeDegreeLimit = 6;

        var pairs = from a in fromCluster
                    from b in toCluster
                    let d = CalculateDistance(a.X, a.Y, b.X, b.Y)
                    orderby d
                    select new { From = a, To = b, Distance = d };

        int added = 0;
        var used = new HashSet<string>();

        foreach (var pair in pairs)
        {
            if (added >= bridgeCount)
                break;

            if (graph.GetIncidentEdges(pair.From).Count >= bridgeDegreeLimit)
                continue;

            if (graph.GetIncidentEdges(pair.To).Count >= bridgeDegreeLimit)
                continue;

            string key = pair.From.Id.CompareTo(pair.To.Id) < 0
                ? $"{pair.From.Id}:{pair.To.Id}"
                : $"{pair.To.Id}:{pair.From.Id}";

            if (!used.Add(key))
                continue;

            int beforeCount = graph.Edges.Count;
            TryAddEdge(graph, pair.From, pair.To);

            if (graph.Edges.Count == beforeCount)
                continue;

            var edge = graph.Edges.LastOrDefault(e =>
                (e.FromCellId == pair.From.Id && e.ToCellId == pair.To.Id) ||
                (e.FromCellId == pair.To.Id && e.ToCellId == pair.From.Id));

            if (edge != null)
            {
                SetEdgeFireSpreadModifier(
                    edge,
                    Math.Clamp(edge.FireSpreadModifier * modifierMultiplier * 0.72, 0.02, 1.10));
            }

            added++;
        }
    }


    private ForestGraph BuildClusteredGraphFromBlueprint(
      ClusteredGraphBlueprint blueprint,
      SimulationParameters parameters)
    {
        if (blueprint == null)
            throw new InvalidOperationException("Clustered blueprint не задан.");

        if (blueprint.Nodes == null || blueprint.Nodes.Count == 0)
            throw new InvalidOperationException("Clustered blueprint не содержит узлов.");

        var graph = new ForestGraph
        {
            Width = blueprint.CanvasWidth > 0 ? blueprint.CanvasWidth : Math.Max(1, parameters.GridWidth),
            Height = blueprint.CanvasHeight > 0 ? blueprint.CanvasHeight : Math.Max(1, parameters.GridHeight),
            StepDurationSeconds = parameters.StepDurationSeconds
        };

        var cellById = new Dictionary<Guid, ForestCell>();
        var occupiedPositions = new HashSet<(int X, int Y)>();

        foreach (var draftNode in blueprint.Nodes)
        {
            if (draftNode.Id == Guid.Empty)
                throw new InvalidOperationException("Blueprint содержит узел с пустым Id.");

            if (cellById.ContainsKey(draftNode.Id))
                throw new InvalidOperationException($"Blueprint содержит дублирующийся node id: {draftNode.Id}");

            if (!occupiedPositions.Add((draftNode.X, draftNode.Y)))
            {
                _logger.LogWarning(
                    "Blueprint содержит несколько узлов в одной позиции ({X},{Y}). Позиции сохраняются как есть.",
                    draftNode.X,
                    draftNode.Y);
            }

            var cell = new ForestCell(
                draftNode.X,
                draftNode.Y,
                draftNode.Vegetation,
                Math.Clamp(draftNode.Moisture, 0.0, 1.0),
                draftNode.Elevation,
                draftNode.ClusterId);

            SetBackingField(cell, "<Id>k__BackingField", draftNode.Id);
            cell.SetClusterId(draftNode.ClusterId);
            cell.ResetAccumulatedHeat();
            cell.SetBurnProbability(cell.BurnProbability);

            graph.Cells.Add(cell);
            cellById[draftNode.Id] = cell;
        }

        var blueprintEdges = blueprint.Edges ?? new List<ClusteredEdgeDraft>();
        var edgePairs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var draftEdge in blueprintEdges)
        {
            if (draftEdge.Id == Guid.Empty)
                throw new InvalidOperationException("Blueprint содержит ребро с пустым Id.");

            if (draftEdge.FromNodeId == Guid.Empty || draftEdge.ToNodeId == Guid.Empty)
                throw new InvalidOperationException($"Blueprint edge {draftEdge.Id} содержит пустой endpoint id.");

            if (draftEdge.FromNodeId == draftEdge.ToNodeId)
                throw new InvalidOperationException($"Blueprint edge {draftEdge.Id} является self-loop.");

            if (!cellById.TryGetValue(draftEdge.FromNodeId, out var fromCell))
                throw new InvalidOperationException(
                    $"Blueprint edge {draftEdge.Id} ссылается на отсутствующий узел FromNodeId={draftEdge.FromNodeId}");

            if (!cellById.TryGetValue(draftEdge.ToNodeId, out var toCell))
                throw new InvalidOperationException(
                    $"Blueprint edge {draftEdge.Id} ссылается на отсутствующий узел ToNodeId={draftEdge.ToNodeId}");

            var canonicalPair = BuildCanonicalEdgeKey(draftEdge.FromNodeId, draftEdge.ToNodeId);
            if (!edgePairs.Add(canonicalPair))
                throw new InvalidOperationException(
                    $"Blueprint содержит дублирующееся ребро между {draftEdge.FromNodeId} и {draftEdge.ToNodeId}");

            double distance = (draftEdge.DistanceOverride.HasValue && draftEdge.DistanceOverride.Value > 0.0)
     ? draftEdge.DistanceOverride.Value
     : CalculateEuclideanDistance(fromCell.X, fromCell.Y, toCell.X, toCell.Y);
            double slope = CalculateSlopeBetween(fromCell, toCell);

            var edge = new ForestEdge(fromCell, toCell, distance, slope);

            SetBackingField(edge, "<Id>k__BackingField", draftEdge.Id);

            if (draftEdge.FireSpreadModifier > 0.0)
            {
                SetBackingField(
                    edge,
                    "<FireSpreadModifier>k__BackingField",
                    Math.Clamp(draftEdge.FireSpreadModifier, 0.02, 1.85));
            }

            graph.Edges.Add(edge);
        }

        _logger.LogInformation(
            "Strict clustered blueprint applied: nodes={Nodes}, edges={Edges}, canvas={Width}x{Height}",
            graph.Cells.Count,
            graph.Edges.Count,
            graph.Width,
            graph.Height);

        return graph;
    }
    private async Task<ForestGraph> GenerateClusteredGraphByScenarioAsync(int nodeCount, SimulationParameters parameters)
    {
        var effectiveNodeCount = Math.Max(1, nodeCount);

        _logger.LogInformation(
            "Генерация ClusteredGraph по сценарию/рандому. NodeCount={NodeCount}, Mode={Mode}, ClusteredScenario={ClusteredScenario}, BlueprintNodes={BlueprintNodes}, BlueprintEdges={BlueprintEdges}",
            effectiveNodeCount,
            parameters.MapCreationMode,
            parameters.ClusteredScenarioType,
            parameters.ClusteredBlueprint?.Nodes.Count ?? 0,
            parameters.ClusteredBlueprint?.Edges.Count ?? 0);

        var random = CreateRandom(parameters);
        var placementScale = GetClusteredPlacementScale(effectiveNodeCount);
        var maxDegree = GetClusteredMaxDegree(effectiveNodeCount);

        var graph = new ForestGraph
        {
            Width = Math.Max(8, (int)Math.Ceiling(Math.Sqrt(effectiveNodeCount) * placementScale)),
            Height = Math.Max(8, (int)Math.Ceiling(Math.Sqrt(effectiveNodeCount) * placementScale)),
            StepDurationSeconds = parameters.StepDurationSeconds
        };

        switch (parameters.MapCreationMode)
        {
            case MapCreationMode.Scenario:
                BuildClusteredScenarioGraph(
                    graph,
                    effectiveNodeCount,
                    parameters,
                    random,
                    maxDegree);
                break;

            case MapCreationMode.Random:
            default:
                BuildClusteredRandomGraph(
                    graph,
                    effectiveNodeCount,
                    parameters,
                    random,
                    maxDegree);
                break;
        }

        _logger.LogInformation(
            "ClusteredGraph сгенерирован: nodes={Nodes}, edges={Edges}, mode={Mode}, clusteredScenario={Scenario}",
            graph.Cells.Count,
            graph.Edges.Count,
            parameters.MapCreationMode,
            parameters.ClusteredScenarioType);

        return await Task.FromResult(graph);
    }
    private static string BuildCanonicalEdgeKey(Guid a, Guid b)
    {
        return a.CompareTo(b) < 0
            ? $"{a:D}|{b:D}"
            : $"{b:D}|{a:D}";
    }

    private static double CalculateEuclideanDistance(int x1, int y1, int x2, int y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double CalculateSlopeBetween(ForestCell fromCell, ForestCell toCell)
    {
        var distance = CalculateEuclideanDistance(fromCell.X, fromCell.Y, toCell.X, toCell.Y);
        if (distance <= 0.000001)
            return 0.0;

        return (toCell.Elevation - fromCell.Elevation) / distance;
    }

    private static void SetBackingField<TTarget, TValue>(TTarget target, string fieldName, TValue value)
    {
        var field = typeof(TTarget).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        if (field == null)
            throw new InvalidOperationException(
                $"Не найден backing field '{fieldName}' у типа {typeof(TTarget).Name}");

        field.SetValue(target, value);
    }


    private void CreateDenseLocalEdges(ForestGraph graph, int maxDegree)
    {
        if (graph.Cells.Count <= 1)
            return;

        int localTargetDegree = GetClusteredLocalTargetDegree(graph.Cells.Count);
        int effectiveMaxDegree = Math.Max(localTargetDegree, maxDegree);

        double closeRadius = GetClusteredCloseRadius(graph.Cells.Count);
        double supportRadius = GetClusteredSupportRadius(graph.Cells.Count);
        double extendedRadius = GetClusteredExtendedRadius(graph.Cells.Count);
        int extendedBudget = GetClusteredExtendedEdgeBudget(graph.Cells.Count);

        var degreeMap = graph.Cells.ToDictionary(c => c.Id, _ => 0);

        foreach (var edge in graph.Edges)
        {
            if (degreeMap.ContainsKey(edge.FromCellId))
                degreeMap[edge.FromCellId]++;

            if (degreeMap.ContainsKey(edge.ToCellId))
                degreeMap[edge.ToCellId]++;
        }

        var edgeKeys = new HashSet<(Guid A, Guid B)>(
            graph.Edges.Select(e => NormalizeEdgeKey(e.FromCellId, e.ToCellId)));

        foreach (var source in graph.Cells)
        {
            if (degreeMap[source.Id] >= localTargetDegree)
                continue;

            var candidates = graph.Cells
                .Where(c => c.Id != source.Id)
                .Where(c => !EdgeExists(edgeKeys, source, c))
                .Where(c =>
                {
                    double dx = Math.Abs(c.X - source.X);
                    double dy = Math.Abs(c.Y - source.Y);
                    double limit = Math.Max(closeRadius, supportRadius);
                    return dx <= limit && dy <= limit;
                })
                .Select(target => new
                {
                    Cell = target,
                    Distance = CalculateDistance(source.X, source.Y, target.X, target.Y),
                    SameCluster = string.Equals(source.ClusterId, target.ClusterId, StringComparison.Ordinal)
                })
                .Where(x =>
                    (x.SameCluster && x.Distance <= supportRadius) ||
                    (!x.SameCluster && x.Distance <= closeRadius))
                .OrderByDescending(x => x.SameCluster)
                .ThenBy(x => x.Distance)
                .ToList();

            foreach (var candidate in candidates)
            {
                if (degreeMap[source.Id] >= localTargetDegree)
                    break;

                if (degreeMap[candidate.Cell.Id] >= effectiveMaxDegree)
                    continue;

                if (!TryAddEdge(graph, edgeKeys, degreeMap, source, candidate.Cell))
                    continue;
            }
        }

        if (extendedBudget <= 0)
            return;

        foreach (var source in graph.Cells)
        {
            if (degreeMap[source.Id] >= effectiveMaxDegree)
                continue;

            int addedForSource = 0;

            var candidates = graph.Cells
                .Where(c => c.Id != source.Id)
                .Where(c => !EdgeExists(edgeKeys, source, c))
                .Where(c =>
                {
                    double dx = Math.Abs(c.X - source.X);
                    double dy = Math.Abs(c.Y - source.Y);
                    return dx <= extendedRadius && dy <= extendedRadius;
                })
                .Select(target => new
                {
                    Cell = target,
                    Distance = CalculateDistance(source.X, source.Y, target.X, target.Y),
                    SameCluster = string.Equals(source.ClusterId, target.ClusterId, StringComparison.Ordinal)
                })
                .Where(x => x.Distance <= extendedRadius)
                .OrderByDescending(x => x.SameCluster)
                .ThenBy(x => x.Distance)
                .ToList();

            foreach (var candidate in candidates)
            {
                if (addedForSource >= extendedBudget)
                    break;

                if (degreeMap[source.Id] >= effectiveMaxDegree)
                    break;

                if (degreeMap[candidate.Cell.Id] >= effectiveMaxDegree)
                    continue;

                if (!TryAddEdge(graph, edgeKeys, degreeMap, source, candidate.Cell))
                    continue;

                addedForSource++;
            }
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
        var minDistance = Math.Max(3.0, Math.Min(width, height) / 4.2);

        for (int i = 0; i < patchCount; i++)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                var centerX = random.Next(1, Math.Max(2, width - 1));
                var centerY = random.Next(1, Math.Max(2, height - 1));

                var ok = patches.All(p => CalculateDistance(p.CenterX, p.CenterY, centerX, centerY) >= minDistance);
                if (!ok)
                    continue;

                var dominantVegetation = GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters);

                var (minMoisture, maxMoisture) = GetEffectiveMoistureRange(parameters);
                var baseMoisture = minMoisture + random.NextDouble() * Math.Max(0.02, maxMoisture - minMoisture);

                var effectiveElevationVariation = GetEffectiveElevationVariation(parameters.ElevationVariation, parameters);
                var baseElevation = (random.NextDouble() * 2.0 - 1.0) * Math.Max(4.0, effectiveElevationVariation * 0.35);

                var radius = Math.Clamp(
                    2.4 + random.NextDouble() * 1.6,
                    2.2,
                    Math.Max(3.4, Math.Min(width, height) / 2.4));

                var weight = Math.Clamp(
                    0.75 + random.NextDouble() * 0.70,
                    0.45,
                    1.85);

                patches.Add(new ClusteredPatch(
                    i,
                    centerX,
                    centerY,
                    radius,
                    weight,
                    dominantVegetation,
                    baseMoisture,
                    baseElevation));

                break;
            }
        }

        if (patches.Count == 0)
        {
            patches.Add(new ClusteredPatch(
                0,
                width / 2.0,
                height / 2.0,
                3.2,
                1.0,
                VegetationType.Mixed,
                0.45,
                0.0));
        }

        return patches;
    }
    private List<ClusteredPatch> CreateScenarioDrivenClusteredPatches(
        ClusteredScenarioType scenario,
        int nodeCount,
        int width,
        int height,
        SimulationParameters parameters,
        Random random)
    {
        var patches = new List<ClusteredPatch>();

        int patchCount = scenario switch
        {
            ClusteredScenarioType.DenseDryConiferous => Math.Max(4, GetClusteredPatchCount(nodeCount)),
            ClusteredScenarioType.WaterBarrier => Math.Max(5, GetClusteredPatchCount(nodeCount)),
            ClusteredScenarioType.FirebreakGap => Math.Max(4, GetClusteredPatchCount(nodeCount)),
            ClusteredScenarioType.HillyClusters => Math.Max(5, GetClusteredPatchCount(nodeCount)),
            ClusteredScenarioType.WetAfterRain => Math.Max(4, GetClusteredPatchCount(nodeCount)),
            ClusteredScenarioType.MixedDryHotspots => Math.Max(5, GetClusteredPatchCount(nodeCount)),
            _ => GetClusteredPatchCount(nodeCount)
        };

        for (int i = 0; i < patchCount; i++)
        {
            var sideBias = scenario switch
            {
                ClusteredScenarioType.WaterBarrier => i < patchCount / 2 ? 0.28 : 0.72,
                ClusteredScenarioType.FirebreakGap => i < patchCount / 2 ? 0.30 : 0.70,
                _ => 0.18 + random.NextDouble() * 0.64
            };

            double centerX = width * sideBias + (random.NextDouble() * 2.0 - 1.0) * Math.Max(1.5, width * 0.08);
            double centerY = height * (0.18 + random.NextDouble() * 0.64);

            centerX = ClampToRange((int)Math.Round(centerX), 1, Math.Max(1, width - 2));
            centerY = ClampToRange((int)Math.Round(centerY), 1, Math.Max(1, height - 2));

            var dominantVegetation = scenario switch
            {
                ClusteredScenarioType.DenseDryConiferous => random.NextDouble() < 0.82 ? VegetationType.Coniferous : VegetationType.Mixed,
                ClusteredScenarioType.WaterBarrier => random.NextDouble() < 0.45 ? VegetationType.Deciduous : VegetationType.Mixed,
                ClusteredScenarioType.FirebreakGap => random.NextDouble() < 0.55 ? VegetationType.Mixed : VegetationType.Coniferous,
                ClusteredScenarioType.HillyClusters => random.NextDouble() < 0.45 ? VegetationType.Coniferous : VegetationType.Shrub,
                ClusteredScenarioType.WetAfterRain => random.NextDouble() < 0.50 ? VegetationType.Deciduous : VegetationType.Mixed,
                ClusteredScenarioType.MixedDryHotspots => i % 3 == 0 ? VegetationType.Coniferous : VegetationType.Mixed,
                _ => VegetationType.Mixed
            };

            var baseMoisture = scenario switch
            {
                ClusteredScenarioType.DenseDryConiferous => 0.12 + random.NextDouble() * 0.10,
                ClusteredScenarioType.WaterBarrier => 0.32 + random.NextDouble() * 0.18,
                ClusteredScenarioType.FirebreakGap => 0.24 + random.NextDouble() * 0.16,
                ClusteredScenarioType.HillyClusters => 0.20 + random.NextDouble() * 0.18,
                ClusteredScenarioType.WetAfterRain => 0.62 + random.NextDouble() * 0.16,
                ClusteredScenarioType.MixedDryHotspots => i % 3 == 0
                    ? 0.12 + random.NextDouble() * 0.10
                    : 0.34 + random.NextDouble() * 0.16,
                _ => 0.40
            };

            var baseElevation = scenario switch
            {
                ClusteredScenarioType.HillyClusters => (random.NextDouble() * 2.0 - 1.0) * Math.Max(20.0, parameters.ElevationVariation * 0.75),
                _ => (random.NextDouble() * 2.0 - 1.0) * Math.Max(6.0, parameters.ElevationVariation * 0.20)
            };

            var radius = scenario switch
            {
                ClusteredScenarioType.DenseDryConiferous => 3.1 + random.NextDouble() * 0.8,
                ClusteredScenarioType.WaterBarrier => 2.6 + random.NextDouble() * 0.9,
                ClusteredScenarioType.FirebreakGap => 2.8 + random.NextDouble() * 0.8,
                ClusteredScenarioType.HillyClusters => 2.7 + random.NextDouble() * 1.0,
                ClusteredScenarioType.WetAfterRain => 2.8 + random.NextDouble() * 0.9,
                ClusteredScenarioType.MixedDryHotspots => 2.5 + random.NextDouble() * 1.1,
                _ => 3.0
            };

            var weight = scenario switch
            {
                ClusteredScenarioType.DenseDryConiferous => 1.25 + random.NextDouble() * 0.35,
                ClusteredScenarioType.WetAfterRain => 0.95 + random.NextDouble() * 0.25,
                _ => 0.90 + random.NextDouble() * 0.45
            };

            patches.Add(new ClusteredPatch(
                i,
                centerX,
                centerY,
                radius,
                weight,
                dominantVegetation,
                Math.Clamp(baseMoisture, 0.02, 0.98),
                baseElevation));
        }

        return patches;
    }
    private VegetationType SelectScenarioVegetationForPatch(
    ClusteredPatch patch,
    ClusteredScenarioType scenario,
    SimulationParameters parameters,
    Random random)
    {
        return scenario switch
        {
            ClusteredScenarioType.DenseDryConiferous =>
                random.NextDouble() < 0.86 ? patch.DominantVegetation : VegetationType.Mixed,

            ClusteredScenarioType.WaterBarrier =>
                random.NextDouble() < 0.72 ? patch.DominantVegetation : VegetationType.Deciduous,

            ClusteredScenarioType.FirebreakGap =>
                random.NextDouble() < 0.76 ? patch.DominantVegetation : GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters),

            ClusteredScenarioType.HillyClusters =>
                random.NextDouble() < 0.72 ? patch.DominantVegetation : VegetationType.Shrub,

            ClusteredScenarioType.WetAfterRain =>
                random.NextDouble() < 0.74 ? patch.DominantVegetation : VegetationType.Deciduous,

            ClusteredScenarioType.MixedDryHotspots =>
                random.NextDouble() < 0.68 ? patch.DominantVegetation : GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters),

            _ =>
                patch.DominantVegetation
        };
    }
    private void ApplyClusteredScenarioBridgePolicy(
     ForestGraph graph,
     List<ClusteredPatch> patches,
     ClusteredScenarioType scenario,
     Random random)
    {
        int maxDegree = GetClusteredMaxDegree(graph.Cells.Count);

        switch (scenario)
        {
            case ClusteredScenarioType.WaterBarrier:
            case ClusteredScenarioType.FirebreakGap:
                CreateClusterBridges(graph, patches, random, Math.Max(2, maxDegree - 1));
                break;

            case ClusteredScenarioType.HillyClusters:
                CreateClusterBridges(graph, patches, random, maxDegree);
                break;

            case ClusteredScenarioType.WetAfterRain:
                CreateClusterBridges(graph, patches, random, Math.Max(2, maxDegree - 1));
                break;

            case ClusteredScenarioType.MixedDryHotspots:
                CreateClusterBridges(graph, patches, random, maxDegree);
                break;

            case ClusteredScenarioType.DenseDryConiferous:
            default:
                CreateClusterBridges(graph, patches, random, maxDegree);
                break;
        }
    }

    private void ApplyClusteredScenarioNodeAdjustments(
        ForestGraph graph,
        ClusteredScenarioType scenario,
        SimulationParameters parameters,
        Random random)
    {
        foreach (var cell in graph.Cells)
        {
            switch (scenario)
            {
                case ClusteredScenarioType.DenseDryConiferous:
                    if (cell.Vegetation == VegetationType.Coniferous || cell.Vegetation == VegetationType.Mixed)
                        cell.UpdateMoisture(Math.Clamp(cell.Moisture - 0.06, 0.02, 0.98));
                    break;

                case ClusteredScenarioType.WetAfterRain:
                    cell.UpdateMoisture(Math.Clamp(cell.Moisture + 0.10, 0.02, 0.98));
                    break;

                case ClusteredScenarioType.MixedDryHotspots:
                    if (cell.ClusterId != null && cell.ClusterId.EndsWith("0", StringComparison.Ordinal))
                        cell.UpdateMoisture(Math.Clamp(cell.Moisture - 0.10, 0.02, 0.98));
                    break;
            }
        }
    }



    private void ApplySurfaceBarrierEdgeModifiers(ForestGraph graph)
    {
        if (graph.Edges.Count == 0 || graph.Cells.Count == 0)
            return;

        var cellMap = graph.Cells.ToDictionary(c => c.Id, c => c);

        foreach (var edge in graph.Edges)
        {
            if (!cellMap.TryGetValue(edge.FromCellId, out var fromCell) ||
                !cellMap.TryGetValue(edge.ToCellId, out var toCell))
            {
                continue;
            }

            double modifier = edge.FireSpreadModifier;
            double adjustedModifier = modifier;

            bool touchesWater =
                fromCell.Vegetation == VegetationType.Water ||
                toCell.Vegetation == VegetationType.Water;

            bool touchesBare =
                fromCell.Vegetation == VegetationType.Bare ||
                toCell.Vegetation == VegetationType.Bare;

            bool nearWater =
                IsNearBarrierCell(graph, fromCell, VegetationType.Water) ||
                IsNearBarrierCell(graph, toCell, VegetationType.Water);

            bool nearBare =
                IsNearBarrierCell(graph, fromCell, VegetationType.Bare) ||
                IsNearBarrierCell(graph, toCell, VegetationType.Bare);

            if (touchesWater)
            {
                adjustedModifier *= 0.08;
            }
            else if (touchesBare)
            {
                adjustedModifier *= 0.18;
            }
            else
            {
                if (nearWater)
                    adjustedModifier *= 0.52;

                if (nearBare)
                    adjustedModifier *= 0.72;
            }

            adjustedModifier = Math.Clamp(adjustedModifier, 0.02, 1.35);

            SetEdgeFireSpreadModifier(edge, adjustedModifier);
        }
    }
    private bool IsNearBarrierCell(
        ForestGraph graph,
        ForestCell cell,
        VegetationType barrierType)
    {
        foreach (var neighbor in graph.GetNeighbors(cell))
        {
            if (neighbor.Vegetation == barrierType)
                return true;
        }

        return false;
    }
    private void SetEdgeFireSpreadModifier(ForestEdge edge, double value)
    {
        var modifierField = typeof(ForestEdge).GetField("<FireSpreadModifier>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        modifierField?.SetValue(edge, value);
    }

    private void CreateEdgesForGrid(ForestGraph graph, int width, int height)
    {
        foreach (var cell in graph.Cells)
        {
            var neighbors = new[]
            {
                (cell.X - 1, cell.Y),
                (cell.X + 1, cell.Y),
                (cell.X, cell.Y - 1),
                (cell.X, cell.Y + 1)
            };

            foreach (var (nx, ny) in neighbors)
            {
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                var neighbor = graph.GetCell(nx, ny);
                if (neighbor != null)
                    TryAddEdge(graph, cell, neighbor);
            }
        }
    }


    private List<ClusteredPatch> CreateClusteredPatches(
     int patchCount,
     int width,
     int height,
     SimulationParameters parameters,
     Random random,
     TerritoryDraft territoryDraft)
    {
        var patches = new List<ClusteredPatch>();
        var minDistance = Math.Max(3.0, Math.Min(width, height) / 4.2);

        for (int i = 0; i < patchCount; i++)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                var centerX = random.Next(1, Math.Max(2, width - 1));
                var centerY = random.Next(1, Math.Max(2, height - 1));

                var ok = patches.All(p => CalculateDistance(p.CenterX, p.CenterY, centerX, centerY) >= minDistance);
                if (!ok)
                    continue;

                var profile = BuildClusteredPatchProfileFromTerritory(
                    width,
                    height,
                    parameters,
                    random,
                    territoryDraft,
                    centerX,
                    centerY,
                    patchCount);

                patches.Add(new ClusteredPatch(
                    i,
                    centerX,
                    centerY,
                    profile.Radius,
                    profile.Weight,
                    profile.DominantVegetation,
                    profile.BaseMoisture,
                    profile.BaseElevation));

                break;
            }
        }

        if (patches.Count == 0)
        {
            var fallbackCenterX = width / 2.0;
            var fallbackCenterY = height / 2.0;

            var profile = BuildClusteredPatchProfileFromTerritory(
                width,
                height,
                parameters,
                random,
                territoryDraft,
                fallbackCenterX,
                fallbackCenterY,
                patchCount);

            patches.Add(new ClusteredPatch(
                0,
                fallbackCenterX,
                fallbackCenterY,
                profile.Radius,
                profile.Weight,
                profile.DominantVegetation,
                profile.BaseMoisture,
                profile.BaseElevation));
        }

        return patches;
    }
    private ClusteredPatchSeedProfile BuildClusteredPatchProfileFromTerritory(
     int width,
     int height,
     SimulationParameters parameters,
     Random random,
     TerritoryDraft territoryDraft,
     double centerX,
     double centerY,
     int patchCount)
    {
        int radiusCells = Math.Max(3, (int)Math.Round(Math.Min(width, height) / Math.Max(4.5, Math.Sqrt(patchCount) + 1.8)));

        var vegetationWeights = new Dictionary<VegetationType, double>();
        double weightedMoisture = 0.0;
        double weightedElevation = 0.0;
        double totalWeight = 0.0;

        int waterCount = 0;
        int bareCount = 0;
        int sampleCount = 0;

        int minX = Math.Max(0, (int)Math.Floor(centerX) - radiusCells);
        int maxX = Math.Min(width - 1, (int)Math.Ceiling(centerX) + radiusCells);
        int minY = Math.Max(0, (int)Math.Floor(centerY) - radiusCells);
        int maxY = Math.Min(height - 1, (int)Math.Ceiling(centerY) + radiusCells);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                double distance = CalculateDistance(centerX, centerY, x, y);
                if (distance > radiusCells * 1.30)
                    continue;

                double sampleWeight = 1.0 / (1.0 + distance * distance);

                var vegetation = territoryDraft.VegetationMap[x, y];
                var moisture = territoryDraft.MoistureMap[x, y];
                var elevation = territoryDraft.ElevationMap[x, y];

                if (!vegetationWeights.ContainsKey(vegetation))
                    vegetationWeights[vegetation] = 0.0;

                vegetationWeights[vegetation] += sampleWeight;
                weightedMoisture += moisture * sampleWeight;
                weightedElevation += elevation * sampleWeight;
                totalWeight += sampleWeight;
                sampleCount++;

                if (vegetation == VegetationType.Water)
                    waterCount++;

                if (vegetation == VegetationType.Bare)
                    bareCount++;
            }
        }

        if (sampleCount == 0 || totalWeight <= 0.0)
        {
            var fallbackVegetation = GetRandomCombustibleVegetation(parameters.VegetationDistributions, random, parameters);
            var fallbackMoisture = GetRandomMoisture(parameters.InitialMoistureMin, parameters.InitialMoistureMax, random, parameters);
            var fallbackElevation = GetRandomElevation(parameters.ElevationVariation, random, parameters);

            return new ClusteredPatchSeedProfile(
                fallbackVegetation,
                fallbackMoisture,
                fallbackElevation,
                Math.Max(2.6, Math.Min(width, height) / 4.5),
                1.0);
        }

        var dominantVegetation = vegetationWeights
            .Where(x => x.Key != VegetationType.Water && x.Key != VegetationType.Bare)
            .OrderByDescending(x => x.Value)
            .Select(x => x.Key)
            .FirstOrDefault();

        if (dominantVegetation == default && !vegetationWeights.ContainsKey(dominantVegetation))
            dominantVegetation = VegetationType.Mixed;

        double averageMoisture = weightedMoisture / totalWeight;
        double averageElevation = weightedElevation / totalWeight;

        double waterFraction = (double)waterCount / sampleCount;
        double bareFraction = (double)bareCount / sampleCount;

        double radius =
            Math.Max(2.4, Math.Min(width, height) / 5.0) +
            random.NextDouble() * 1.8 +
            waterFraction * 0.9 -
            bareFraction * 0.4;

        if (parameters.MapCreationMode == MapCreationMode.Scenario && parameters.ScenarioType.HasValue)
        {
            switch (parameters.ScenarioType.Value)
            {
                case MapScenarioType.DryConiferousMassif:
                    if (dominantVegetation == VegetationType.Coniferous)
                        radius += 0.35;
                    break;

                case MapScenarioType.ForestWithRiver:
                case MapScenarioType.ForestWithLake:
                    radius += waterFraction * 0.55;
                    break;

                case MapScenarioType.ForestWithFirebreak:
                    radius -= bareFraction * 0.45;
                    break;

                case MapScenarioType.HillyTerrain:
                    radius += Math.Min(0.45, Math.Abs(averageElevation) / Math.Max(25.0, parameters.ElevationVariation) * 0.45);
                    break;

                case MapScenarioType.WetForestAfterRain:
                    radius += Math.Max(0.0, averageMoisture - 0.55) * 0.35;
                    break;
            }
        }

        radius = Math.Clamp(radius, 2.2, Math.Max(3.4, Math.Min(width, height) / 2.4));

        double patchWeight = 0.78;

        patchWeight += dominantVegetation switch
        {
            VegetationType.Coniferous => 0.22,
            VegetationType.Mixed => 0.16,
            VegetationType.Deciduous => 0.08,
            VegetationType.Shrub => 0.05,
            VegetationType.Grass => -0.03,
            _ => 0.0
        };

        patchWeight += (0.55 - averageMoisture) * 0.40;
        patchWeight -= waterFraction * 0.24;
        patchWeight -= bareFraction * 0.16;

        patchWeight = Math.Clamp(patchWeight, 0.45, 1.85);

        return new ClusteredPatchSeedProfile(
            dominantVegetation,
            averageMoisture,
            averageElevation,
            radius,
            patchWeight);
    }
    private List<(int X, int Y)> GeneratePatchDrivenClusteredCoordinates(
        int nodeCount,
        int width,
        int height,
        List<ClusteredPatch> patches,
        Random random)
    {
        var result = new List<(int X, int Y)>();
        var used = new HashSet<(int X, int Y)>();

        while (result.Count < nodeCount)
        {
            var patch = PickPatchWeighted(patches, random);

            int x = ClampToRange(
                (int)Math.Round(patch.CenterX + NextTriangular(random) * patch.Radius * 1.15),
                0,
                width - 1);

            int y = ClampToRange(
                (int)Math.Round(patch.CenterY + NextTriangular(random) * patch.Radius * 1.15),
                0,
                height - 1);

            var point = (x, y);

            if (used.Contains(point))
            {
                point = FindNearestFreePointAround(x, y, width, height, used) ?? FindAnyFreePoint(width, height, used) ?? point;
            }

            if (used.Contains(point))
                continue;

            used.Add(point);
            result.Add(point);
        }

        return result;
    }

    private ClusteredPatch PickPatchWeighted(List<ClusteredPatch> patches, Random random)
    {
        var totalWeight = patches.Sum(p => p.Weight);
        var roll = random.NextDouble() * totalWeight;
        double acc = 0.0;

        foreach (var patch in patches)
        {
            acc += patch.Weight;
            if (roll <= acc)
                return patch;
        }

        return patches[^1];
    }

    private ClusteredPatch GetBestPatchForPoint(int x, int y, List<ClusteredPatch> patches)
    {
        return patches
            .OrderBy(p => CalculateDistance(p.CenterX, p.CenterY, x, y) / Math.Max(1.0, p.Radius))
            .First();
    }


    private void CreateEdgesForClusteredGraph(
     ForestGraph graph,
     HashSet<(Guid A, Guid B)> edgeKeys,
     Dictionary<Guid, int> degreeMap)
    {
        int localTargetDegree = GetClusteredLocalTargetDegree(graph.Cells.Count);
        int maxDegree = GetClusteredMaxDegree(graph.Cells.Count);
        double closeRadius = GetClusteredCloseRadius(graph.Cells.Count);
        double supportRadius = GetClusteredSupportRadius(graph.Cells.Count);

        foreach (var cell in graph.Cells)
        {
            var rankedNeighbors = graph.Cells
                .Where(c => c.Id != cell.Id)
                .Select(c =>
                {
                    double distance = CalculateDistance(cell.X, cell.Y, c.X, c.Y);

                    bool samePatch =
                        !string.IsNullOrWhiteSpace(cell.ClusterId) &&
                        !string.IsNullOrWhiteSpace(c.ClusterId) &&
                        cell.ClusterId == c.ClusterId;

                    double barrierStrength = EstimateClusteredBarrierStrength(cell, c);
                    double compatibility = CalculateClusteredPairCompatibility(cell, c, samePatch);
                    double distanceScore = 1.0 / Math.Max(1.0, distance);

                    double priority =
                        (samePatch ? 1.35 : 0.88) +
                        compatibility * 1.20 +
                        distanceScore * 0.85 -
                        barrierStrength * 1.45;

                    return new
                    {
                        Cell = c,
                        Distance = distance,
                        SamePatch = samePatch,
                        BarrierStrength = barrierStrength,
                        Compatibility = compatibility,
                        Priority = priority
                    };
                })
                .OrderByDescending(x => x.Priority)
                .ThenByDescending(x => x.SamePatch)
                .ThenBy(x => x.Distance)
                .ToList();

            var closeNeighbors = rankedNeighbors
                .Where(x => x.Distance <= closeRadius)
                .Where(x => x.BarrierStrength < 0.82)
                .Where(x => x.Compatibility > 0.18)
                .ToList();

            if (closeNeighbors.Count == 0)
            {
                closeNeighbors = rankedNeighbors
                    .Where(x => x.BarrierStrength < 0.90)
                    .Take(3)
                    .ToList();
            }

            foreach (var neighbor in closeNeighbors)
            {
                if (degreeMap[cell.Id] >= 3)
                    break;

                if (degreeMap[neighbor.Cell.Id] >= maxDegree)
                    continue;

                if (neighbor.BarrierStrength >= 0.92)
                    continue;

                if (neighbor.Compatibility < 0.12)
                    continue;

                TryAddEdge(graph, edgeKeys, degreeMap, cell, neighbor.Cell);
            }

            var supportNeighbors = rankedNeighbors
                .Where(x => x.Distance <= supportRadius)
                .Where(x => x.BarrierStrength < 0.78)
                .Where(x => x.Compatibility > 0.20)
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => degreeMap[x.Cell.Id])
                .ThenBy(x => x.Distance)
                .ToList();

            foreach (var neighbor in supportNeighbors)
            {
                if (degreeMap[cell.Id] >= localTargetDegree)
                    break;

                if (degreeMap[neighbor.Cell.Id] >= maxDegree)
                    continue;

                TryAddEdge(graph, edgeKeys, degreeMap, cell, neighbor.Cell);
            }
        }
    }


    private void EnsureCloseNeighborSupport(
        ForestGraph graph,
        HashSet<(Guid A, Guid B)> edgeKeys,
        Dictionary<Guid, int> degreeMap,
        double closeRadius,
        int requiredCloseNeighbors,
        int preferredMaxDegree)
    {
        if (graph.Cells.Count <= 2)
            return;

        bool changed;

        do
        {
            changed = false;

            foreach (var cell in graph.Cells)
            {
                if (degreeMap[cell.Id] >= preferredMaxDegree)
                    continue;

                int closeNeighborCount = graph.Cells
                    .Where(other => other.Id != cell.Id)
                    .Where(other => EdgeExists(edgeKeys, cell, other))
                    .Count(other => CalculateDistance(cell.X, cell.Y, other.X, other.Y) <= closeRadius);

                while (closeNeighborCount < requiredCloseNeighbors && degreeMap[cell.Id] < preferredMaxDegree)
                {
                    var bestCandidate = graph.Cells
                        .Where(other => other.Id != cell.Id)
                        .Where(other => !EdgeExists(edgeKeys, cell, other))
                        .Select(other => new
                        {
                            Cell = other,
                            Degree = degreeMap[other.Id],
                            Distance = CalculateDistance(cell.X, cell.Y, other.X, other.Y),
                            SamePatch = !string.IsNullOrWhiteSpace(cell.ClusterId) &&
                                        !string.IsNullOrWhiteSpace(other.ClusterId) &&
                                        cell.ClusterId == other.ClusterId
                        })
                        .Where(x => x.Distance <= closeRadius * 1.35)
                        .Where(x => x.Degree < preferredMaxDegree)
                        .OrderByDescending(x => x.SamePatch)
                        .ThenBy(x => x.Distance)
                        .ThenBy(x => x.Degree)
                        .FirstOrDefault();

                    if (bestCandidate == null)
                        break;

                    if (TryAddEdge(graph, edgeKeys, degreeMap, cell, bestCandidate.Cell))
                        changed = true;

                    closeNeighborCount = graph.Cells
                        .Where(other => other.Id != cell.Id)
                        .Where(other => EdgeExists(edgeKeys, cell, other))
                        .Count(other => CalculateDistance(cell.X, cell.Y, other.X, other.Y) <= closeRadius);
                }
            }
        }
        while (changed);
    }

    private void EnsureMinimumDegree(
        ForestGraph graph,
        HashSet<(Guid A, Guid B)> edgeKeys,
        Dictionary<Guid, int> degreeMap,
        int minDegree,
        int preferredMaxDegree)
    {
        if (graph.Cells.Count <= 2)
            return;

        bool changed;

        do
        {
            changed = false;

            var weakCells = graph.Cells
                .Where(c => degreeMap[c.Id] < minDegree)
                .OrderBy(c => degreeMap[c.Id])
                .ThenBy(c => c.X)
                .ThenBy(c => c.Y)
                .ToList();

            foreach (var cell in weakCells)
            {
                while (degreeMap[cell.Id] < minDegree)
                {
                    var bestCandidate = graph.Cells
                        .Where(other => other.Id != cell.Id)
                        .Where(other => !EdgeExists(edgeKeys, cell, other))
                        .Select(other => new
                        {
                            Cell = other,
                            Degree = degreeMap[other.Id],
                            Distance = CalculateDistance(cell.X, cell.Y, other.X, other.Y),
                            SamePatch = !string.IsNullOrWhiteSpace(cell.ClusterId) &&
                                        !string.IsNullOrWhiteSpace(other.ClusterId) &&
                                        cell.ClusterId == other.ClusterId
                        })
                        .Where(x => x.Degree < preferredMaxDegree)
                        .OrderByDescending(x => x.SamePatch)
                        .ThenBy(x => x.Degree)
                        .ThenBy(x => x.Distance)
                        .FirstOrDefault();

                    if (bestCandidate == null)
                        break;

                    if (TryAddEdge(graph, edgeKeys, degreeMap, cell, bestCandidate.Cell))
                        changed = true;
                    else
                        break;
                }
            }
        }
        while (changed);
    }

    private int AddClusteredExtendedEdges(
     ForestGraph graph,
     HashSet<(Guid A, Guid B)> edgeKeys,
     Dictionary<Guid, int> degreeMap,
     int maxDegree,
     double supportRadius,
     double extendedRadius)
    {
        int added = 0;
        int budget = GetClusteredExtendedEdgeBudget(graph.Cells.Count);

        if (budget <= 0)
            return 0;

        var candidateSources = graph.Cells
            .OrderBy(c => degreeMap[c.Id])
            .ThenBy(c => c.X)
            .ThenBy(c => c.Y)
            .ToList();

        foreach (var cell in candidateSources)
        {
            if (added >= budget)
                break;

            if (degreeMap[cell.Id] >= maxDegree - 1)
                continue;

            bool alreadyHasLongEdge = graph.Edges.Any(e =>
            {
                if (e.FromCellId != cell.Id && e.ToCellId != cell.Id)
                    return false;

                return e.Distance > supportRadius * 1.10;
            });

            if (alreadyHasLongEdge)
                continue;

            var candidate = graph.Cells
                .Where(other => other.Id != cell.Id)
                .Where(other => !EdgeExists(edgeKeys, cell, other))
                .Select(other =>
                {
                    double distance = CalculateDistance(cell.X, cell.Y, other.X, other.Y);

                    bool differentPatch =
                        !string.IsNullOrWhiteSpace(cell.ClusterId) &&
                        !string.IsNullOrWhiteSpace(other.ClusterId) &&
                        cell.ClusterId != other.ClusterId;

                    double barrierStrength = EstimateClusteredBarrierStrength(cell, other);
                    double compatibility = CalculateClusteredPairCompatibility(cell, other, samePatch: !differentPatch);

                    double bridgePriority =
                        (differentPatch ? 1.20 : 0.72) +
                        compatibility * 1.05 -
                        barrierStrength * 1.30 -
                        Math.Max(0.0, distance - supportRadius) * 0.12;

                    return new
                    {
                        Cell = other,
                        Degree = degreeMap[other.Id],
                        Distance = distance,
                        DifferentPatch = differentPatch,
                        BarrierStrength = barrierStrength,
                        Compatibility = compatibility,
                        BridgePriority = bridgePriority
                    };
                })
                .Where(x => x.Distance > supportRadius * 1.10 && x.Distance <= extendedRadius)
                .Where(x => x.Degree < maxDegree - 1)
                .Where(x => x.BarrierStrength < 0.72)
                .Where(x => x.Compatibility > 0.22)
                .OrderByDescending(x => x.BridgePriority)
                .ThenByDescending(x => x.DifferentPatch)
                .ThenBy(x => x.Distance)
                .ThenBy(x => x.Degree)
                .FirstOrDefault();

            if (candidate == null)
                continue;

            if (TryAddEdge(graph, edgeKeys, degreeMap, cell, candidate.Cell))
                added++;
        }

        return added;
    }
    private double EstimateClusteredBarrierStrength(ForestCell a, ForestCell b)
    {
        double barrier = 0.0;

        if (a.Vegetation == VegetationType.Water || b.Vegetation == VegetationType.Water)
            barrier += 0.95;

        if (a.Vegetation == VegetationType.Bare || b.Vegetation == VegetationType.Bare)
            barrier += 0.72;

        double avgMoisture = (a.Moisture + b.Moisture) * 0.5;
        if (avgMoisture > 0.62)
            barrier += (avgMoisture - 0.62) * 0.90;

        double slopePenalty = Math.Abs(a.Elevation - b.Elevation) / Math.Max(8.0, CalculateDistance(a.X, a.Y, b.X, b.Y) * 14.0);
        barrier += Math.Min(0.28, slopePenalty);

        return Math.Clamp(barrier, 0.0, 1.0);
    }

    private double CalculateClusteredPairCompatibility(ForestCell a, ForestCell b, bool samePatch)
    {
        double vegetationCompatibility = a.Vegetation == b.Vegetation
            ? 1.0
            : (a.Vegetation, b.Vegetation) switch
            {
                (VegetationType.Coniferous, VegetationType.Mixed) => 0.88,
                (VegetationType.Mixed, VegetationType.Coniferous) => 0.88,
                (VegetationType.Deciduous, VegetationType.Mixed) => 0.84,
                (VegetationType.Mixed, VegetationType.Deciduous) => 0.84,
                (VegetationType.Grass, VegetationType.Shrub) => 0.76,
                (VegetationType.Shrub, VegetationType.Grass) => 0.76,
                (VegetationType.Water, _) => 0.06,
                (_, VegetationType.Water) => 0.06,
                (VegetationType.Bare, _) => 0.22,
                (_, VegetationType.Bare) => 0.22,
                _ => 0.62
            };

        double moistureGap = Math.Abs(a.Moisture - b.Moisture);
        double elevationGap = Math.Abs(a.Elevation - b.Elevation);

        double moistureCompatibility = 1.0 - Math.Min(0.78, moistureGap * 1.25);
        double elevationCompatibility = 1.0 - Math.Min(0.52, elevationGap / 90.0);

        double combustibilityA = GetClusteredCombustibility(a);
        double combustibilityB = GetClusteredCombustibility(b);

        double result =
            vegetationCompatibility * 0.42 +
            ((combustibilityA + combustibilityB) * 0.5) * 0.24 +
            moistureCompatibility * 0.18 +
            elevationCompatibility * 0.10 +
            (samePatch ? 0.12 : 0.0);

        return Math.Clamp(result, 0.0, 1.0);
    }

    private double GetClusteredCombustibility(ForestCell cell)
    {
        double value = cell.Vegetation switch
        {
            VegetationType.Coniferous => 1.00,
            VegetationType.Mixed => 0.86,
            VegetationType.Deciduous => 0.72,
            VegetationType.Shrub => 0.70,
            VegetationType.Grass => 0.62,
            VegetationType.Bare => 0.16,
            VegetationType.Water => 0.04,
            _ => 0.50
        };

        value -= Math.Max(0.0, cell.Moisture - 0.55) * 0.48;
        return Math.Clamp(value, 0.0, 1.0);
    }

    private void EnsureGraphConnectivity(
       ForestGraph graph,
       HashSet<(Guid A, Guid B)> edgeKeys,
       Dictionary<Guid, int> degreeMap,
       int maxAllowedDegree)
    {
        if (graph.Cells.Count <= 1)
            return;

        while (true)
        {
            var components = GetConnectedComponents(graph);
            if (components.Count <= 1)
                return;

            var strictPair = components
                .SelectMany((component, index) =>
                    components.Skip(index + 1).SelectMany(otherComponent =>
                        component.SelectMany(a => otherComponent.Select(b => new
                        {
                            A = a,
                            B = b,
                            Distance = CalculateDistance(a.X, a.Y, b.X, b.Y),
                            MaxEndpointDegree = Math.Max(degreeMap[a.Id], degreeMap[b.Id]),
                            DegreeSum = degreeMap[a.Id] + degreeMap[b.Id],
                            Overflow =
                                Math.Max(0, degreeMap[a.Id] + 1 - maxAllowedDegree) +
                                Math.Max(0, degreeMap[b.Id] + 1 - maxAllowedDegree)
                        }))))
                .Where(x => degreeMap[x.A.Id] < maxAllowedDegree && degreeMap[x.B.Id] < maxAllowedDegree)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.MaxEndpointDegree)
                .ThenBy(x => x.DegreeSum)
                .FirstOrDefault();

            if (strictPair != null)
            {
                TryAddEdge(graph, edgeKeys, degreeMap, strictPair.A, strictPair.B);
                continue;
            }

            var relaxedPair = components
                .SelectMany((component, index) =>
                    components.Skip(index + 1).SelectMany(otherComponent =>
                        component.SelectMany(a => otherComponent.Select(b => new
                        {
                            A = a,
                            B = b,
                            Distance = CalculateDistance(a.X, a.Y, b.X, b.Y),
                            MaxEndpointDegree = Math.Max(degreeMap[a.Id], degreeMap[b.Id]),
                            DegreeSum = degreeMap[a.Id] + degreeMap[b.Id],
                            Overflow =
                                Math.Max(0, degreeMap[a.Id] + 1 - maxAllowedDegree) +
                                Math.Max(0, degreeMap[b.Id] + 1 - maxAllowedDegree)
                        }))))
                .OrderBy(x => x.Overflow)
                .ThenBy(x => x.Distance)
                .ThenBy(x => x.MaxEndpointDegree)
                .ThenBy(x => x.DegreeSum)
                .FirstOrDefault();

            if (relaxedPair == null)
            {
                throw new InvalidOperationException("Не удалось связать компоненты ClusteredGraph.");
            }

            TryAddEdge(graph, edgeKeys, degreeMap, relaxedPair.A, relaxedPair.B);
        }
    }
    private List<List<ForestCell>> GetConnectedComponents(ForestGraph graph)
    {
        var result = new List<List<ForestCell>>();
        var visited = new HashSet<Guid>();
        var adjacency = BuildAdjacency(graph);

        foreach (var cell in graph.Cells)
        {
            if (visited.Contains(cell.Id))
                continue;

            var component = new List<ForestCell>();
            var queue = new Queue<ForestCell>();
            queue.Enqueue(cell);
            visited.Add(cell.Id);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                if (!adjacency.TryGetValue(current.Id, out var neighborIds))
                    continue;

                foreach (var neighborId in neighborIds)
                {
                    if (!visited.Add(neighborId))
                        continue;

                    var neighbor = graph.Cells.FirstOrDefault(c => c.Id == neighborId);
                    if (neighbor != null)
                        queue.Enqueue(neighbor);
                }
            }

            result.Add(component);
        }

        return result;
    }

    private Dictionary<Guid, List<Guid>> BuildAdjacency(ForestGraph graph)
    {
        var adjacency = graph.Cells.ToDictionary(c => c.Id, _ => new List<Guid>());

        foreach (var edge in graph.Edges)
        {
            adjacency[edge.FromCellId].Add(edge.ToCellId);
            adjacency[edge.ToCellId].Add(edge.FromCellId);
        }

        return adjacency;
    }



    private void ApplyConnectedGraphSurfaceZones(
        ForestGraph graph,
        SimulationParameters parameters,
        Random random,
        Func<ForestCell, string?> groupSelector)
    {
        if (graph.Cells.Count == 0)
            return;

        ApplyConnectedGraphSurfaceZone(
            graph,
            parameters,
            random,
            VegetationType.Water,
            groupSelector);

        ApplyConnectedGraphSurfaceZone(
            graph,
            parameters,
            random,
            VegetationType.Bare,
            groupSelector);
    }
    private void ApplyConnectedGraphSurfaceZone(
        ForestGraph graph,
        SimulationParameters parameters,
        Random random,
        VegetationType targetType,
        Func<ForestCell, string?> groupSelector)
    {
        double probability = GetVegetationProbability(parameters.VegetationDistributions, targetType);
        if (probability <= 0.0)
            return;

        int totalCells = graph.Cells.Count;
        int targetCount = (int)Math.Round(totalCells * probability);
        if (targetCount <= 0)
            return;

        int maxAllowed = Math.Max(1, totalCells / 6);
        targetCount = Math.Min(targetCount, maxAllowed);

        var eligibleCells = graph.Cells
            .Where(IsCombustibleVegetationCell)
            .ToList();

        if (eligibleCells.Count == 0)
            return;

        targetCount = Math.Min(targetCount, eligibleCells.Count);
        if (targetCount <= 0)
            return;

        var grouped = eligibleCells
            .GroupBy(cell => string.IsNullOrWhiteSpace(groupSelector(cell)) ? "__none__" : groupSelector(cell)!)
            .OrderByDescending(g => g.Count())
            .ToList();

        int zoneCount = EstimateGraphSurfaceZoneCount(targetCount, targetType, totalCells);
        var chosenSeeds = new List<ForestCell>();

        foreach (var group in grouped)
        {
            if (chosenSeeds.Count >= zoneCount)
                break;

            var bestSeed = group
                .OrderByDescending(c => graph.GetNeighbors(c).Count(n => n.ClusterId == c.ClusterId))
                .ThenBy(_ => random.Next())
                .FirstOrDefault();

            if (bestSeed != null)
                chosenSeeds.Add(bestSeed);
        }

        if (chosenSeeds.Count == 0)
        {
            var fallback = eligibleCells.OrderBy(_ => random.Next()).FirstOrDefault();
            if (fallback != null)
                chosenSeeds.Add(fallback);
        }

        int remaining = targetCount;
        int zonesLeft = chosenSeeds.Count;

        foreach (var seed in chosenSeeds)
        {
            if (remaining <= 0)
                break;

            int zoneTarget = Math.Max(1, (int)Math.Ceiling((double)remaining / Math.Max(1, zonesLeft)));

            int painted = PaintConnectedGraphZone(
                graph,
                seed,
                zoneTarget,
                targetType,
                random);

            remaining -= painted;
            zonesLeft--;
        }

        if (remaining > 0)
        {
            var fallbackCells = eligibleCells
                .Where(IsCombustibleVegetationCell)
                .OrderBy(c => GetCellReplacementPriority(graph, c))
                .ThenBy(_ => random.Next())
                .Take(remaining)
                .ToList();

            foreach (var cell in fallbackCells)
                ReplaceCellVegetation(cell, targetType);
        }
    }
    private int PaintConnectedGraphZone(
    ForestGraph graph,
    ForestCell start,
    int targetCount,
    VegetationType targetType,
    Random random)
    {
        if (targetCount <= 0)
            return 0;

        if (!IsCombustibleVegetationCell(start))
            return 0;

        int painted = 0;

        var queue = new Queue<ForestCell>();
        var visited = new HashSet<Guid>();

        queue.Enqueue(start);
        visited.Add(start.Id);

        string? preferredCluster = start.ClusterId;

        while (queue.Count > 0 && painted < targetCount)
        {
            var current = queue.Dequeue();

            if (IsCombustibleVegetationCell(current))
            {
                ReplaceCellVegetation(current, targetType);
                painted++;
            }

            if (painted >= targetCount)
                break;

            var neighbors = graph.GetNeighbors(current)
                .Where(n => !visited.Contains(n.Id))
                .Where(IsCombustibleVegetationCell)
                .Select(n => new
                {
                    Cell = n,
                    SameCluster = string.Equals(n.ClusterId, preferredCluster, StringComparison.Ordinal),
                    Degree = graph.GetNeighbors(n).Count(),
                    Score = random.NextDouble()
                })
                .OrderByDescending(x => x.SameCluster)
                .ThenByDescending(x => x.Degree)
                .ThenByDescending(x => x.Score)
                .ToList();

            foreach (var neighbor in neighbors)
            {
                if (!visited.Add(neighbor.Cell.Id))
                    continue;

                if (random.NextDouble() < 0.90 || queue.Count == 0)
                    queue.Enqueue(neighbor.Cell);
            }
        }

        return painted;
    }

    private int EstimateGraphSurfaceZoneCount(int targetCount, VegetationType targetType, int totalCells)
    {
        if (targetType == VegetationType.Water)
        {
            if (targetCount <= 4) return 1;
            if (targetCount <= 10) return 2;
            if (targetCount <= 20) return 3;
            return totalCells >= 180 ? 4 : 3;
        }

        if (targetCount <= 4) return 1;
        if (targetCount <= 12) return 2;
        if (targetCount <= 24) return 3;
        return totalCells >= 180 ? 4 : 3;
    }
    private bool IsCombustibleVegetationCell(ForestCell cell)
    {
        return cell.Vegetation != VegetationType.Water &&
               cell.Vegetation != VegetationType.Bare;
    }
    private int GetCellReplacementPriority(ForestGraph graph, ForestCell cell)
    {
        int sameClusterNeighbors = graph.GetNeighbors(cell).Count(n => n.ClusterId == cell.ClusterId);
        int totalNeighbors = graph.GetNeighbors(cell).Count();

        if (sameClusterNeighbors <= 1)
            return 0;

        if (sameClusterNeighbors == 2)
            return 1;

        if (totalNeighbors <= 2)
            return 2;

        return 3;
    }
    private void ReplaceCellVegetation(ForestCell cell, VegetationType vegetationType)
    {
        var vegetationField = typeof(ForestCell).GetField("<Vegetation>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        vegetationField?.SetValue(cell, vegetationType);

        if (vegetationType == VegetationType.Water || vegetationType == VegetationType.Bare)
        {
            var fuelLoadField = typeof(ForestCell).GetField("<FuelLoad>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            fuelLoadField?.SetValue(cell, 0.0);

            var currentFuelLoadField = typeof(ForestCell).GetField("<CurrentFuelLoad>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            currentFuelLoadField?.SetValue(cell, 0.0);
        }
    }


    private void CreateDenseIntraClusterEdges(ForestGraph graph, List<ForestCell> clusterCells)
    {
        if (clusterCells.Count <= 1)
            return;

        int targetDegree = clusterCells.Count <= 9 ? 4 : 5;
        int hardMaxDegree = clusterCells.Count <= 9 ? 5 : 6;

        foreach (var cell in clusterCells)
        {
            var rankedCandidates = clusterCells
                .Where(c => c.Id != cell.Id)
                .Select(c => new
                {
                    Cell = c,
                    Distance = CalculateDistance(cell.X, cell.Y, c.X, c.Y)
                })
                .Where(x => x.Distance > 0.0)
                .OrderBy(x => x.Distance)
                .ToList();

            foreach (var candidate in rankedCandidates)
            {
                if (GetNodeDegree(graph, cell) >= 3)
                    break;

                if (GetNodeDegree(graph, candidate.Cell) >= hardMaxDegree)
                    continue;

                if (candidate.Distance > 1.7)
                    continue;

                TryAddEdge(graph, cell, candidate.Cell);
            }

            foreach (var candidate in rankedCandidates)
            {
                if (GetNodeDegree(graph, cell) >= targetDegree)
                    break;

                if (GetNodeDegree(graph, candidate.Cell) >= hardMaxDegree)
                    continue;

                if (candidate.Distance > 2.7)
                    continue;

                TryAddEdge(graph, cell, candidate.Cell);
            }
        }

        bool changed;
        do
        {
            changed = false;

            var weakCells = clusterCells
                .Where(c => GetNodeDegree(graph, c) < 4)
                .OrderBy(c => GetNodeDegree(graph, c))
                .ThenBy(c => c.X)
                .ThenBy(c => c.Y)
                .ToList();

            foreach (var cell in weakCells)
            {
                var bestCandidate = clusterCells
                    .Where(other => other.Id != cell.Id)
                    .Where(other => !EdgeExists(graph, cell, other))
                    .Select(other => new
                    {
                        Cell = other,
                        Degree = GetNodeDegree(graph, other),
                        Distance = CalculateDistance(cell.X, cell.Y, other.X, other.Y)
                    })
                    .Where(x => x.Distance <= 3.0)
                    .Where(x => x.Degree < hardMaxDegree || GetNodeDegree(graph, cell) < 2)
                    .OrderBy(x => x.Distance)
                    .ThenBy(x => x.Degree)
                    .FirstOrDefault();

                if (bestCandidate == null)
                    continue;

                int before = graph.Edges.Count;
                TryAddEdge(graph, cell, bestCandidate.Cell);

                if (graph.Edges.Count > before)
                    changed = true;
            }
        }
        while (changed);
    }







    private bool TryAddEdge(
        ForestGraph graph,
        HashSet<(Guid A, Guid B)> edgeKeys,
        Dictionary<Guid, int> degreeMap,
        ForestCell from,
        ForestCell to)
    {
        if (from.Id == to.Id)
            return false;

        var key = NormalizeEdgeKey(from.Id, to.Id);
        if (!edgeKeys.Add(key))
            return false;

        var distance = CalculateDistance(from.X, from.Y, to.X, to.Y);
        var slope = CalculateSlope(from.Elevation, to.Elevation, distance);

        graph.Edges.Add(new ForestEdge(from, to, distance, slope));
        degreeMap[from.Id]++;
        degreeMap[to.Id]++;

        return true;
    }

    private void TryAddEdge(ForestGraph graph, ForestCell from, ForestCell to)
    {
        if (from.Id == to.Id || EdgeExists(graph, from, to))
            return;

        var distance = CalculateDistance(from.X, from.Y, to.X, to.Y);
        var slope = CalculateSlope(from.Elevation, to.Elevation, distance);

        var edge = new ForestEdge(from, to, distance, slope);
        graph.Edges.Add(edge);
    }

    private (Guid A, Guid B) NormalizeEdgeKey(Guid a, Guid b)
    {
        return a.CompareTo(b) < 0 ? (a, b) : (b, a);
    }

    private bool EdgeExists(HashSet<(Guid A, Guid B)> edgeKeys, ForestCell from, ForestCell to)
    {
        return edgeKeys.Contains(NormalizeEdgeKey(from.Id, to.Id));
    }

    private bool EdgeExists(ForestGraph graph, ForestCell from, ForestCell to)
    {
        return graph.Edges.Any(e =>
            (e.FromCellId == from.Id && e.ToCellId == to.Id) ||
            (e.FromCellId == to.Id && e.ToCellId == from.Id));
    }

    private int GetNodeDegree(ForestGraph graph, ForestCell cell)
    {
        return graph.Edges.Count(e => e.FromCellId == cell.Id || e.ToCellId == cell.Id);
    }

    private Random CreateRandom(SimulationParameters parameters)
    {
        return parameters.RandomSeed.HasValue
            ? new Random(parameters.RandomSeed.Value)
            : new Random();
    }

    private VegetationType GetRandomVegetation(List<VegetationDistribution> distributions, Random random)
    {
        if (distributions == null || distributions.Count == 0)
        {
            return random.NextDouble() switch
            {
                < 0.30 => VegetationType.Coniferous,
                < 0.50 => VegetationType.Deciduous,
                < 0.70 => VegetationType.Mixed,
                < 0.85 => VegetationType.Shrub,
                _ => VegetationType.Grass
            };
        }

        var randomValue = random.NextDouble();
        var cumulative = 0.0;

        foreach (var distribution in distributions)
        {
            cumulative += distribution.Probability;
            if (randomValue <= cumulative)
                return distribution.VegetationType;
        }

        return distributions.Last().VegetationType;
    }

    private double GetRandomMoisture(double min, double max, Random random)
    {
        return min + (random.NextDouble() * (max - min));
    }

    private double GetRandomMoisture(double min, double max, Random random, SimulationParameters parameters)
    {
        var (effectiveMin, effectiveMax) = GetEffectiveMoistureRange(parameters);
        return effectiveMin + (random.NextDouble() * (effectiveMax - effectiveMin));
    }

    private double GetRandomElevation(double variation, Random random)
    {
        return random.NextDouble() * variation;
    }

    private double GetRandomElevation(double variation, Random random, SimulationParameters parameters)
    {
        var effectiveVariation = GetEffectiveElevationVariation(variation, parameters);
        return random.NextDouble() * effectiveVariation;
    }


    private double CalculateDistance(int x1, int y1, int x2, int y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private double CalculateDistance(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private double CalculateSlope(double elevation1, double elevation2, double distance)
    {
        if (distance <= 0.0)
            return 0.0;

        return (elevation2 - elevation1) / distance;
    }

    private int ClampToRange(int value, int min, int max)
    {
        return Math.Min(max, Math.Max(min, value));
    }

    private double NextTriangular(Random random)
    {
        return random.NextDouble() - random.NextDouble();
    }

    private (int X, int Y)? FindNearestFreePointAround(
        int centerX,
        int centerY,
        int width,
        int height,
        HashSet<(int X, int Y)> used)
    {
        int maxRadius = Math.Max(width, height);

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;

                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (x < 0 || x >= width || y < 0 || y >= height)
                        continue;

                    if (!used.Contains((x, y)))
                        return (x, y);
                }
            }
        }

        return null;
    }

    private (int X, int Y)? FindAnyFreePoint(
        int width,
        int height,
        HashSet<(int X, int Y)> used)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!used.Contains((x, y)))
                    return (x, y);
            }
        }

        return null;
    }


    private sealed class ClusteredPatch
    {
        public int Index { get; }
        public double CenterX { get; }
        public double CenterY { get; }
        public double Radius { get; }
        public double Weight { get; }
        public VegetationType DominantVegetation { get; }
        public double BaseMoisture { get; }
        public double BaseElevation { get; }

        public ClusteredPatch(
            int index,
            double centerX,
            double centerY,
            double radius,
            double weight,
            VegetationType dominantVegetation,
            double baseMoisture,
            double baseElevation)
        {
            Index = index;
            CenterX = centerX;
            CenterY = centerY;
            Radius = radius;
            Weight = weight;
            DominantVegetation = dominantVegetation;
            BaseMoisture = baseMoisture;
            BaseElevation = baseElevation;
        }
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
    private sealed class ClusteredPatchSeedProfile
    {
        public VegetationType DominantVegetation { get; }
        public double BaseMoisture { get; }
        public double BaseElevation { get; }
        public double Radius { get; }
        public double Weight { get; }

        public ClusteredPatchSeedProfile(
            VegetationType dominantVegetation,
            double baseMoisture,
            double baseElevation,
            double radius,
            double weight)
        {
            DominantVegetation = dominantVegetation;
            BaseMoisture = baseMoisture;
            BaseElevation = baseElevation;
            Radius = radius;
            Weight = weight;
        }
    }
}