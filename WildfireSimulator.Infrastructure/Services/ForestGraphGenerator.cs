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
        if (nodeCount <= 12) return 4;
        if (nodeCount <= 40) return 5;
        if (nodeCount <= 120) return 5;
        return 6;
    }

    private int GetClusteredMaxDegree(int nodeCount)
    {
        if (nodeCount <= 20) return 5;
        if (nodeCount <= 80) return 6;
        if (nodeCount <= 250) return 6;
        return 7;
    }

    private double GetClusteredCloseRadius(int nodeCount)
    {
        if (nodeCount <= 12) return 2.2;
        if (nodeCount <= 40) return 2.4;
        if (nodeCount <= 120) return 2.6;
        return 2.8;
    }

    private double GetClusteredSupportRadius(int nodeCount)
    {
        if (nodeCount <= 12) return 3.1;
        if (nodeCount <= 40) return 3.4;
        if (nodeCount <= 120) return 3.7;
        return 4.0;
    }

    private double GetClusteredExtendedRadius(int nodeCount)
    {
        if (nodeCount <= 12) return 3.8;
        if (nodeCount <= 40) return 4.3;
        if (nodeCount <= 120) return 4.8;
        return 5.2;
    }

    private int GetClusteredExtendedEdgeBudget(int nodeCount)
    {
        if (nodeCount <= 12) return 1;
        if (nodeCount <= 40) return 2;
        if (nodeCount <= 120) return 4;
        if (nodeCount <= 250) return 8;
        return 12;
    }

    private double GetClusteredPlacementScale(int nodeCount)
    {
        if (nodeCount <= 12) return 1.15;
        if (nodeCount <= 32) return 1.25;
        if (nodeCount <= 72) return 1.35;
        if (nodeCount <= 200) return 1.45;
        return 1.50;
    }

    private int GetClusteredPatchCount(int nodeCount)
    {
        if (nodeCount <= 20) return 3;
        if (nodeCount <= 60) return 4;
        if (nodeCount <= 140) return 5;
        if (nodeCount <= 260) return 6;
        return 7;
    }

    private int GetRegionClusterRegionCount(int width, int height)
    {
        var area = width * height;

        if (area <= 120) return 4;
        if (area <= 240) return 6;
        if (area <= 420) return 7;
        if (area <= 700) return 8;
        return 9;
    }

    public async Task<ForestGraph> GenerateGridAsync(int width, int height, SimulationParameters parameters)
    {
        _logger.LogInformation("Генерация сетки {Width}x{Height}", width, height);

        var random = CreateRandom(parameters);

        var graph = new ForestGraph
        {
            Width = width,
            Height = height,
            StepDurationSeconds = parameters.StepDurationSeconds
        };

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var vegetation = GetRandomVegetation(parameters.VegetationDistributions, random);
                var moisture = GetRandomMoisture(parameters.InitialMoistureMin, parameters.InitialMoistureMax, random);
                var elevation = GetRandomElevation(parameters.ElevationVariation, random);

                var cell = new ForestCell(x, y, vegetation, moisture, elevation);
                graph.Cells.Add(cell);
            }
        }

        CreateEdgesForGrid(graph, width, height);

        _logger.LogInformation("Сгенерировано {Cells} клеток и {Edges} ребер",
            graph.Cells.Count, graph.Edges.Count);

        return await Task.FromResult(graph);
    }

    public async Task<ForestGraph> GenerateClusteredGraphAsync(int nodeCount, SimulationParameters parameters)
    {
        _logger.LogInformation("Генерация кластерного графа с {NodeCount} узлами", nodeCount);

        var random = CreateRandom(parameters);
        var placementScale = GetClusteredPlacementScale(nodeCount);
        var maxDegree = GetClusteredMaxDegree(nodeCount);

        var graph = new ForestGraph
        {
            Width = Math.Max(8, (int)Math.Ceiling(Math.Sqrt(nodeCount) * placementScale)),
            Height = Math.Max(8, (int)Math.Ceiling(Math.Sqrt(nodeCount) * placementScale)),
            StepDurationSeconds = parameters.StepDurationSeconds
        };

        var patches = CreateClusteredPatches(
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

            var vegetation = random.NextDouble() < 0.82
                ? patch.DominantVegetation
                : GetRandomVegetation(parameters.VegetationDistributions, random);

            var moisture = Math.Clamp(
                patch.BaseMoisture + (random.NextDouble() * 0.18 - 0.09),
                parameters.InitialMoistureMin,
                parameters.InitialMoistureMax);

            var elevation = patch.BaseElevation + (random.NextDouble() * 10.0 - 5.0);

            var cell = new ForestCell(
                x,
                y,
                vegetation,
                moisture,
                elevation,
                clusterId: $"patch-{patch.Index}");

            graph.Cells.Add(cell);
        }

        var edgeKeys = new HashSet<(Guid A, Guid B)>();
        var degreeMap = graph.Cells.ToDictionary(c => c.Id, _ => 0);

        CreateEdgesForClusteredGraph(graph, edgeKeys, degreeMap);
        EnsureGraphConnectivity(graph, edgeKeys, degreeMap, maxDegree);

        var closeRadius = GetClusteredCloseRadius(graph.Cells.Count);
        var supportRadius = GetClusteredSupportRadius(graph.Cells.Count);
        var extendedRadius = GetClusteredExtendedRadius(graph.Cells.Count);

        var requiredCloseNeighbors = graph.Cells.Count <= 40 ? 3 : 2;

        EnsureCloseNeighborSupport(
            graph,
            edgeKeys,
            degreeMap,
            closeRadius,
            requiredCloseNeighbors,
            maxDegree);

        EnsureMinimumDegree(
            graph,
            edgeKeys,
            degreeMap,
            minDegree: 3,
            preferredMaxDegree: maxDegree);

        int addedExtendedEdges = AddClusteredExtendedEdges(
            graph,
            edgeKeys,
            degreeMap,
            maxDegree,
            supportRadius,
            extendedRadius);

        _logger.LogInformation(
            "Сгенерирован кластерный граф: {Cells} узлов, {Edges} ребер, поле {Width}x{Height}, avgDegree={AvgDegree:F2}, minDegree={MinDegree}, maxDegreeActual={MaxDegreeActual}, longEdges={LongEdges}, patches={PatchCount}",
            graph.Cells.Count,
            graph.Edges.Count,
            graph.Width,
            graph.Height,
            degreeMap.Count > 0 ? degreeMap.Values.Average() : 0.0,
            degreeMap.Count > 0 ? degreeMap.Values.Min() : 0,
            degreeMap.Count > 0 ? degreeMap.Values.Max() : 0,
            addedExtendedEdges,
            patches.Count);

        return await Task.FromResult(graph);
    }

    public async Task<ForestGraph> GenerateRegionClusterGraphAsync(SimulationParameters parameters)
    {
        _logger.LogInformation("Генерация регионального кластерного графа");

        var random = CreateRandom(parameters);

        var width = Math.Max(12, parameters.GridWidth);
        var height = Math.Max(12, parameters.GridHeight);

        var graph = new ForestGraph
        {
            Width = width,
            Height = height,
            StepDurationSeconds = parameters.StepDurationSeconds
        };

        var regionCount = GetRegionClusterRegionCount(width, height);
        var regions = CreateVoronoiRegions(width, height, regionCount, parameters, random);

        PopulateRegionClusterRegions(graph, regions, parameters, random);

        foreach (var region in regions)
            CreateDenseIntraClusterEdges(graph, region.Cells);

        CreateRegionClusterBridges(graph, regions, random);

        _logger.LogInformation(
            "Региональный кластерный граф создан: {Cells} узлов, {Edges} ребер, областей {Regions}",
            graph.Cells.Count,
            graph.Edges.Count,
            regions.Count);

        return await Task.FromResult(graph);
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

                var dominantVegetation = GetRandomVegetation(parameters.VegetationDistributions, random);

                var baseMoisture = GetRandomMoisture(parameters.InitialMoistureMin, parameters.InitialMoistureMax, random);
                var baseElevation = GetRandomElevation(parameters.ElevationVariation, random);
                var radius = random.NextDouble() * 2.0 + Math.Max(2.6, Math.Min(width, height) / 4.5);
                var weight = 0.8 + random.NextDouble() * 0.9;

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
                Math.Max(2.6, Math.Min(width, height) / 3.0),
                1.0,
                GetRandomVegetation(parameters.VegetationDistributions, random),
                GetRandomMoisture(parameters.InitialMoistureMin, parameters.InitialMoistureMax, random),
                GetRandomElevation(parameters.ElevationVariation, random)));
        }

        return patches;
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
                .Select(c => new
                {
                    Cell = c,
                    Distance = CalculateDistance(cell.X, cell.Y, c.X, c.Y),
                    SamePatch = !string.IsNullOrWhiteSpace(cell.ClusterId) &&
                                !string.IsNullOrWhiteSpace(c.ClusterId) &&
                                cell.ClusterId == c.ClusterId
                })
                .OrderByDescending(x => x.SamePatch)
                .ThenBy(x => x.Distance)
                .ToList();

            var closeNeighbors = rankedNeighbors
                .Where(x => x.Distance <= closeRadius)
                .ToList();

            if (closeNeighbors.Count == 0)
                closeNeighbors = rankedNeighbors.Take(3).ToList();

            foreach (var neighbor in closeNeighbors)
            {
                if (degreeMap[cell.Id] >= 3)
                    break;

                if (degreeMap[neighbor.Cell.Id] >= maxDegree)
                    continue;

                TryAddEdge(graph, edgeKeys, degreeMap, cell, neighbor.Cell);
            }

            var supportNeighbors = rankedNeighbors
                .Where(x => x.Distance <= supportRadius)
                .OrderByDescending(x => x.SamePatch)
                .ThenBy(x => degreeMap[x.Cell.Id])
                .ThenBy(x => x.Distance)
                .ToList();

            foreach (var neighbor in supportNeighbors)
            {
                if (degreeMap[cell.Id] >= localTargetDegree)
                    break;

                if (degreeMap[neighbor.Cell.Id] >= maxDegree)
                    continue;

                if (degreeMap[cell.Id] >= localTargetDegree - 1 &&
                    degreeMap[neighbor.Cell.Id] >= localTargetDegree)
                {
                    continue;
                }

                TryAddEdge(graph, edgeKeys, degreeMap, cell, neighbor.Cell);
            }
        }

        _logger.LogInformation(
            "Созданы базовые ребра кластерного графа: edges={Edges}, localTargetDegree={LocalTargetDegree}, maxDegree={MaxDegree}",
            graph.Edges.Count,
            localTargetDegree,
            maxDegree);
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
                .Select(other => new
                {
                    Cell = other,
                    Degree = degreeMap[other.Id],
                    Distance = CalculateDistance(cell.X, cell.Y, other.X, other.Y),
                    DifferentPatch = !string.IsNullOrWhiteSpace(cell.ClusterId) &&
                                     !string.IsNullOrWhiteSpace(other.ClusterId) &&
                                     cell.ClusterId != other.ClusterId
                })
                .Where(x => x.Distance > supportRadius * 1.10 && x.Distance <= extendedRadius)
                .Where(x => x.Degree < maxDegree - 1)
                .OrderByDescending(x => x.DifferentPatch)
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


    private List<VoronoiRegion> CreateVoronoiRegions(
     int width,
     int height,
     int regionCount,
     SimulationParameters parameters,
     Random random)
    {
        var regions = new List<VoronoiRegion>();
        var minDistance = Math.Max(4.0, Math.Min(width, height) / 3.8);

        for (int i = 0; i < regionCount; i++)
        {
            for (int attempt = 0; attempt < 120; attempt++)
            {
                double centerX = random.NextDouble() * Math.Max(1, width - 1);
                double centerY = random.NextDouble() * Math.Max(1, height - 1);

                if (regions.Any(r => CalculateDistance(r.CenterX, r.CenterY, centerX, centerY) < minDistance))
                    continue;

                var vegetation = GetRandomVegetation(parameters.VegetationDistributions, random);
                var moisture = GetRandomMoisture(parameters.InitialMoistureMin, parameters.InitialMoistureMax, random);
                var elevation = GetRandomElevation(parameters.ElevationVariation, random);
                var density = 0.60 + random.NextDouble() * 0.22;

                regions.Add(new VoronoiRegion(
                    $"region-{i}",
                    centerX,
                    centerY,
                    vegetation,
                    moisture,
                    elevation,
                    density));

                break;
            }
        }

        if (regions.Count == 0)
        {
            regions.Add(new VoronoiRegion(
                "region-0",
                width / 2.0,
                height / 2.0,
                GetRandomVegetation(parameters.VegetationDistributions, random),
                GetRandomMoisture(parameters.InitialMoistureMin, parameters.InitialMoistureMax, random),
                GetRandomElevation(parameters.ElevationVariation, random),
                0.72));
        }

        AssignGeographicRegionIds(regions, width, height);
        return regions;
    }
    private void AssignGeographicRegionIds(
        List<VoronoiRegion> regions,
        int width,
        int height)
    {
        if (regions.Count == 0)
            return;

        double bucketWidth = Math.Max(1.0, width / 3.0);
        double bucketHeight = Math.Max(1.0, height / 3.0);

        var usedIds = new Dictionary<string, int>();

        foreach (var region in regions
                     .OrderBy(r => r.CenterY)
                     .ThenBy(r => r.CenterX))
        {
            int col = ClampToRange((int)Math.Floor(region.CenterX / bucketWidth), 0, 2);
            int row = ClampToRange((int)Math.Floor(region.CenterY / bucketHeight), 0, 2);

            string baseId = $"region-{col}-{row}";

            if (!usedIds.TryGetValue(baseId, out var duplicateIndex))
            {
                usedIds[baseId] = 0;
                region.RegionId = baseId;
                continue;
            }

            duplicateIndex++;
            usedIds[baseId] = duplicateIndex;
            region.RegionId = $"{baseId}-{duplicateIndex}";
        }
    }
    private void PopulateRegionClusterRegions(
      ForestGraph graph,
      List<VoronoiRegion> regions,
      SimulationParameters parameters,
      Random random)
    {
        var assignments = new Dictionary<string, List<(int X, int Y, double InteriorFactor)>>();

        foreach (var region in regions)
            assignments[region.RegionId] = new List<(int X, int Y, double InteriorFactor)>();

        for (int x = 0; x < graph.Width; x++)
        {
            for (int y = 0; y < graph.Height; y++)
            {
                var ordered = regions
                    .Select(r => new
                    {
                        Region = r,
                        Distance = CalculateDistance(r.CenterX, r.CenterY, x, y)
                    })
                    .OrderBy(v => v.Distance)
                    .ToList();

                var best = ordered[0];
                var second = ordered.Count > 1 ? ordered[1] : ordered[0];

                var separation = second.Distance - best.Distance;
                var interiorFactor = Math.Clamp(
                    separation / Math.Max(1.0, Math.Min(graph.Width, graph.Height) / 5.0),
                    0.0,
                    1.0);

                assignments[best.Region.RegionId].Add((x, y, interiorFactor));
            }
        }

        foreach (var region in regions)
        {
            var candidates = assignments[region.RegionId];
            var kept = new List<(int X, int Y, double InteriorFactor)>();

            foreach (var point in candidates)
            {
                var keepChance =
                    region.Density *
                    (0.80 + 0.20 * point.InteriorFactor) *
                    (0.95 + random.NextDouble() * 0.10);

                keepChance = Math.Clamp(keepChance, 0.22, 0.98);

                if (random.NextDouble() <= keepChance)
                    kept.Add(point);
            }

            int minCells = Math.Max(10, candidates.Count / 5);

            if (kept.Count < minCells)
            {
                var additional = candidates
                    .Where(p => kept.All(k => k.X != p.X || k.Y != p.Y))
                    .OrderByDescending(p => p.InteriorFactor)
                    .ThenBy(p => CalculateDistance(region.CenterX, region.CenterY, p.X, p.Y))
                    .Take(minCells - kept.Count)
                    .ToList();

                kept.AddRange(additional);
            }

            var anchorPoint = candidates
                .OrderBy(p => CalculateDistance(region.CenterX, region.CenterY, p.X, p.Y))
                .FirstOrDefault();

            if (anchorPoint != default &&
                kept.All(p => p.X != anchorPoint.X || p.Y != anchorPoint.Y))
            {
                kept.Add(anchorPoint);
            }

            foreach (var point in kept)
            {
                var vegetation = random.NextDouble() < 0.84
                    ? region.DominantVegetation
                    : GetRandomVegetation(parameters.VegetationDistributions, random);

                var moisture = Math.Clamp(
                    region.BaseMoisture + (random.NextDouble() * 0.12 - 0.06),
                    parameters.InitialMoistureMin,
                    parameters.InitialMoistureMax);

                var elevation = region.BaseElevation + (random.NextDouble() * 8.0 - 4.0);

                var cell = new ForestCell(
                    point.X,
                    point.Y,
                    vegetation,
                    moisture,
                    elevation,
                    region.RegionId);

                graph.Cells.Add(cell);
                region.Cells.Add(cell);
            }
        }

        EnsureNoEmptyRegionClusterRegions(graph, regions, parameters, random);
    }
    private void EnsureNoEmptyRegionClusterRegions(
        ForestGraph graph,
        List<VoronoiRegion> regions,
        SimulationParameters parameters,
        Random random)
    {
        foreach (var region in regions.Where(r => r.Cells.Count == 0))
        {
            int cx = ClampToRange((int)Math.Round(region.CenterX), 0, graph.Width - 1);
            int cy = ClampToRange((int)Math.Round(region.CenterY), 0, graph.Height - 1);

            var cell = new ForestCell(
                cx,
                cy,
                region.DominantVegetation,
                region.BaseMoisture,
                region.BaseElevation,
                region.RegionId);

            graph.Cells.Add(cell);
            region.Cells.Add(cell);
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

    private void CreateRegionClusterBridges(
     ForestGraph graph,
     List<VoronoiRegion> regions,
     Random random)
    {
        if (regions.Count <= 1)
            return;

        var regionLinks = BuildRegionLinks(regions);

        if (regionLinks.Count == 0)
            return;

        var connected = new HashSet<string> { regions[0].RegionId };

        while (connected.Count < regions.Count)
        {
            var nextLink = regionLinks
                .Where(link =>
                    (connected.Contains(link.RegionA.RegionId) && !connected.Contains(link.RegionB.RegionId)) ||
                    (connected.Contains(link.RegionB.RegionId) && !connected.Contains(link.RegionA.RegionId)))
                .OrderBy(link => link.MinCellDistance)
                .ThenByDescending(link => link.BoundaryContactScore)
                .FirstOrDefault();

            if (nextLink == null)
                break;

            if (connected.Contains(nextLink.RegionA.RegionId))
            {
                CreateBridgeBetweenRegions(
                    graph,
                    nextLink.RegionA,
                    nextLink.RegionB,
                    random,
                    mandatory: true);

                connected.Add(nextLink.RegionB.RegionId);
            }
            else
            {
                CreateBridgeBetweenRegions(
                    graph,
                    nextLink.RegionB,
                    nextLink.RegionA,
                    random,
                    mandatory: true);

                connected.Add(nextLink.RegionA.RegionId);
            }
        }

        foreach (var region in regions)
        {
            var extraTargets = regionLinks
                .Where(link => link.RegionA.RegionId == region.RegionId || link.RegionB.RegionId == region.RegionId)
                .OrderBy(link => link.MinCellDistance)
                .ThenByDescending(link => link.BoundaryContactScore)
                .Take(3)
                .ToList();

            foreach (var link in extraTargets)
            {
                var target = link.RegionA.RegionId == region.RegionId
                    ? link.RegionB
                    : link.RegionA;

                if (link.MinCellDistance > 4.2 && link.BoundaryContactScore <= 0)
                    continue;

                CreateBridgeBetweenRegions(
                    graph,
                    region,
                    target,
                    random,
                    mandatory: false);
            }
        }
    }


    private void CreateBridgeBetweenRegions(
        ForestGraph graph,
        VoronoiRegion first,
        VoronoiRegion second,
        Random random,
        bool mandatory)
    {
        if (first.Cells.Count == 0 || second.Cells.Count == 0)
            return;

        var candidatePairs = BuildBoundaryBridgeCandidates(graph, first, second, mandatory);

        if (candidatePairs.Count == 0)
            return;

        int desiredBridgeCount = GetDesiredBoundaryBridgeCount(candidatePairs, mandatory);

        var usedFirst = new HashSet<Guid>();
        var usedSecond = new HashSet<Guid>();
        var acceptedPairs = new List<BoundaryBridgeCandidate>();

        foreach (var candidate in candidatePairs)
        {
            if (acceptedPairs.Count >= desiredBridgeCount)
                break;

            if (usedFirst.Contains(candidate.A.Id) || usedSecond.Contains(candidate.B.Id))
                continue;

            bool tooCloseToAccepted = acceptedPairs.Any(existing =>
                CalculateDistance(existing.MidX, existing.MidY, candidate.MidX, candidate.MidY) < 2.2);

            if (tooCloseToAccepted)
                continue;

            if (EdgeExists(graph, candidate.A, candidate.B))
                continue;

            if (!TryAddRegionBridgeEdge(graph, candidate.A, candidate.B))
                continue;

            usedFirst.Add(candidate.A.Id);
            usedSecond.Add(candidate.B.Id);
            acceptedPairs.Add(candidate);
        }

        if (mandatory && acceptedPairs.Count == 0)
        {
            var fallback = candidatePairs.FirstOrDefault(c => !EdgeExists(graph, c.A, c.B));
            if (fallback != null)
                TryAddRegionBridgeEdge(graph, fallback.A, fallback.B);

            return;
        }

        if (!mandatory || acceptedPairs.Count == 0)
            return;

        var anchorPairs = acceptedPairs.ToList();

        foreach (var anchor in anchorPairs)
        {
            if (acceptedPairs.Count >= desiredBridgeCount)
                break;

            var supportCandidates = candidatePairs
                .Where(c => c.A.Id != anchor.A.Id && c.B.Id != anchor.B.Id)
                .Where(c => !usedFirst.Contains(c.A.Id) && !usedSecond.Contains(c.B.Id))
                .Where(c => !EdgeExists(graph, c.A, c.B))
                .Where(c => CalculateDistance(c.MidX, c.MidY, anchor.MidX, anchor.MidY) >= 2.0)
                .Where(c => CalculateDistance(c.MidX, c.MidY, anchor.MidX, anchor.MidY) <= 5.2)
                .OrderBy(c => Math.Abs(c.BoundaryAxisProjection - anchor.BoundaryAxisProjection))
                .ThenBy(c => c.Distance)
                .ThenByDescending(c => c.BoundaryScore)
                .ToList();

            foreach (var support in supportCandidates)
            {
                if (acceptedPairs.Count >= desiredBridgeCount)
                    break;

                if (!TryAddRegionBridgeEdge(graph, support.A, support.B))
                    continue;

                usedFirst.Add(support.A.Id);
                usedSecond.Add(support.B.Id);
                acceptedPairs.Add(support);
                break;
            }
        }
    }
  private bool TryAddRegionBridgeEdge(ForestGraph graph, ForestCell from, ForestCell to)
{
    if (from.Id == to.Id || EdgeExists(graph, from, to))
        return false;

    var geometricDistance = CalculateDistance(from.X, from.Y, to.X, to.Y);

    var effectiveDistance = Math.Clamp(geometricDistance * 0.78, 1.0, 1.45);

    var slope = CalculateSlope(from.Elevation, to.Elevation, geometricDistance);

    var edge = new ForestEdge(from, to, effectiveDistance, slope);

    edge.ApplyBridgeSpreadBonus(1.55);

    graph.Edges.Add(edge);
    return true;
}


    private List<BoundaryBridgeCandidate> BuildBoundaryBridgeCandidates(
      ForestGraph graph,
      VoronoiRegion first,
      VoronoiRegion second,
      bool mandatory)
    {
        double strictMaxDistance = mandatory ? 1.6 : 1.45;
        double relaxedMaxDistance = mandatory ? 2.2 : 1.9;
        int minOwnNeighbors = mandatory ? 2 : 3;

        double dx = second.CenterX - first.CenterX;
        double dy = second.CenterY - first.CenterY;
        double axisLength = Math.Sqrt(dx * dx + dy * dy);

        if (axisLength < 0.001)
        {
            dx = 1.0;
            dy = 0.0;
            axisLength = 1.0;
        }

        double axisX = dx / axisLength;
        double axisY = dy / axisLength;

        List<BoundaryBridgeCandidate> Build(double maxDistance, bool strictOwnNeighborRule)
        {
            return first.Cells
                .SelectMany(a => second.Cells.Select(b => new
                {
                    A = a,
                    B = b,
                    Distance = CalculateDistance(a.X, a.Y, b.X, b.Y),
                    DegreeA = GetNodeDegree(graph, a),
                    DegreeB = GetNodeDegree(graph, b),
                    SameSideNeighborsA = graph.GetNeighbors(a).Count(n => n.ClusterId == a.ClusterId),
                    SameSideNeighborsB = graph.GetNeighbors(b).Count(n => n.ClusterId == b.ClusterId),
                    BoundaryScore =
                        CountNearForeignCells(a, second.Cells, 2.2) +
                        CountNearForeignCells(b, first.Cells, 2.2),
                    MidX = (a.X + b.X) / 2.0,
                    MidY = (a.Y + b.Y) / 2.0
                }))
                .Where(x => x.Distance > 0.0 && x.Distance <= maxDistance)
                .Where(x => x.DegreeA < 6 && x.DegreeB < 6)
                .Where(x => !strictOwnNeighborRule ||
                            (x.SameSideNeighborsA >= minOwnNeighbors && x.SameSideNeighborsB >= minOwnNeighbors))
                .Select(x => new BoundaryBridgeCandidate(
                    x.A,
                    x.B,
                    x.Distance,
                    x.BoundaryScore,
                    x.MidX,
                    x.MidY,
                    x.MidX * axisX + x.MidY * axisY))
                .OrderBy(c => c.Distance)
                .ThenByDescending(c => c.BoundaryScore)
                .ThenBy(c => c.BoundaryAxisProjection)
                .ToList();
        }

        var strictCandidates = Build(strictMaxDistance, strictOwnNeighborRule: true);
        if (strictCandidates.Count > 0)
            return strictCandidates;

        var relaxedCandidates = Build(relaxedMaxDistance, strictOwnNeighborRule: true);
        if (relaxedCandidates.Count > 0)
            return relaxedCandidates;

        if (!mandatory)
            return relaxedCandidates;

        var fallbackCandidates = Build(2.8, strictOwnNeighborRule: false);
        if (fallbackCandidates.Count > 0)
            return fallbackCandidates;

        return first.Cells
            .SelectMany(a => second.Cells.Select(b => new
            {
                A = a,
                B = b,
                Distance = CalculateDistance(a.X, a.Y, b.X, b.Y),
                BoundaryScore =
                    CountNearForeignCells(a, second.Cells, 3.0) +
                    CountNearForeignCells(b, first.Cells, 3.0),
                MidX = (a.X + b.X) / 2.0,
                MidY = (a.Y + b.Y) / 2.0
            }))
            .Where(x => x.Distance > 0.0 && x.Distance <= 3.4)
            .Select(x => new BoundaryBridgeCandidate(
                x.A,
                x.B,
                x.Distance,
                x.BoundaryScore,
                x.MidX,
                x.MidY,
                x.MidX * axisX + x.MidY * axisY))
            .OrderBy(c => c.Distance)
            .ThenByDescending(c => c.BoundaryScore)
            .ThenBy(c => c.BoundaryAxisProjection)
            .ToList();
    }
    private int GetDesiredBoundaryBridgeCount(
      List<BoundaryBridgeCandidate> candidates,
      bool mandatory)
    {
        if (candidates.Count == 0)
            return 0;

        if (!mandatory)
        {
            if (candidates.Count >= 10 && candidates[0].Distance <= 2.2)
                return 2;

            if (candidates.Count >= 18 && candidates[0].Distance <= 2.0)
                return 3;

            return 1;
        }

        int count = 3;

        if (candidates.Count >= 8)
            count = 4;

        if (candidates.Count >= 16 && candidates[0].Distance <= 2.4)
            count = 5;

        return Math.Min(count, candidates.Count);
    }

    private List<RegionLink> BuildRegionLinks(List<VoronoiRegion> regions)
    {
        var links = new List<RegionLink>();

        for (int i = 0; i < regions.Count; i++)
        {
            for (int j = i + 1; j < regions.Count; j++)
            {
                var a = regions[i];
                var b = regions[j];

                if (a.Cells.Count == 0 || b.Cells.Count == 0)
                    continue;

                double minDistance = double.MaxValue;
                int contactScore = 0;

                foreach (var cellA in a.Cells)
                {
                    foreach (var cellB in b.Cells)
                    {
                        double distance = CalculateDistance(cellA.X, cellA.Y, cellB.X, cellB.Y);

                        if (distance < minDistance)
                            minDistance = distance;

                        if (distance <= 2.2)
                            contactScore++;
                    }
                }

                links.Add(new RegionLink(a, b, minDistance, contactScore));
            }
        }

        return links;
    }

    private int CountNearForeignCells(ForestCell source, List<ForestCell> foreignCells, double radius)
    {
        int count = 0;

        foreach (var cell in foreignCells)
        {
            if (CalculateDistance(source.X, source.Y, cell.X, cell.Y) <= radius)
                count++;
        }

        return count;
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

    private double GetRandomElevation(double variation, Random random)
    {
        return random.NextDouble() * variation;
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

    private sealed class RegionLink
    {
        public VoronoiRegion RegionA { get; }
        public VoronoiRegion RegionB { get; }
        public double MinCellDistance { get; }
        public int BoundaryContactScore { get; }

        public RegionLink(
            VoronoiRegion regionA,
            VoronoiRegion regionB,
            double minCellDistance,
            int boundaryContactScore)
        {
            RegionA = regionA;
            RegionB = regionB;
            MinCellDistance = minCellDistance;
            BoundaryContactScore = boundaryContactScore;
        }
    }

    private sealed class VoronoiRegion
    {
        public string RegionId { get; set; }
        public double CenterX { get; }
        public double CenterY { get; }
        public VegetationType DominantVegetation { get; }
        public double BaseMoisture { get; }
        public double BaseElevation { get; }
        public double Density { get; }
        public List<ForestCell> Cells { get; } = new();

        public VoronoiRegion(
            string regionId,
            double centerX,
            double centerY,
            VegetationType dominantVegetation,
            double baseMoisture,
            double baseElevation,
            double density)
        {
            RegionId = regionId;
            CenterX = centerX;
            CenterY = centerY;
            DominantVegetation = dominantVegetation;
            BaseMoisture = baseMoisture;
            BaseElevation = baseElevation;
            Density = density;
        }
    }
    private sealed class BoundaryBridgeCandidate
    {
        public ForestCell A { get; }
        public ForestCell B { get; }
        public double Distance { get; }
        public int BoundaryScore { get; }
        public double MidX { get; }
        public double MidY { get; }
        public double BoundaryAxisProjection { get; }

        public BoundaryBridgeCandidate(
            ForestCell a,
            ForestCell b,
            double distance,
            int boundaryScore,
            double midX,
            double midY,
            double boundaryAxisProjection)
        {
            A = a;
            B = b;
            Distance = distance;
            BoundaryScore = boundaryScore;
            MidX = midX;
            MidY = midY;
            BoundaryAxisProjection = boundaryAxisProjection;
        }
    }
}