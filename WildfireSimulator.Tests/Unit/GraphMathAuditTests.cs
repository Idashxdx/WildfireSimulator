using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using WildfireSimulator.Application.Services;
using WildfireSimulator.Domain.Models;
using Xunit;
using Xunit.Abstractions;

namespace WildfireSimulator.Tests.Unit;

public class GraphMathAuditTests
{
    private readonly ITestOutputHelper _output;

    public GraphMathAuditTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ForestEdge_ShorterDistance_GivesHigherSpreadModifier()
    {
        _output.WriteLine("=== ForestEdge_ShorterDistance_GivesHigherSpreadModifier ===");

        var a = new ForestCell(0, 0, VegetationType.Grass, 0.2, 10);
        var b1 = new ForestCell(1, 0, VegetationType.Grass, 0.2, 10);
        var b2 = new ForestCell(2, 0, VegetationType.Grass, 0.2, 10);
        var b3 = new ForestCell(4, 0, VegetationType.Grass, 0.2, 10);

        var e1 = new ForestEdge(a, b1, 1.0, 0.0);
        var e2 = new ForestEdge(a, b2, 2.0, 0.0);
        var e3 = new ForestEdge(a, b3, 4.0, 0.0);

        _output.WriteLine($"d=1 -> {e1.FireSpreadModifier:F6}");
        _output.WriteLine($"d=2 -> {e2.FireSpreadModifier:F6}");
        _output.WriteLine($"d=4 -> {e3.FireSpreadModifier:F6}");

        Assert.True(e1.FireSpreadModifier > e2.FireSpreadModifier);
        Assert.True(e2.FireSpreadModifier > e3.FireSpreadModifier);
    }

    [Fact]
    public void ForestEdge_UphillSlope_IncreasesSpreadModifier()
    {
        _output.WriteLine("=== ForestEdge_UphillSlope_IncreasesSpreadModifier ===");

        var from = new ForestCell(0, 0, VegetationType.Grass, 0.2, 10);
        var to = new ForestCell(1, 0, VegetationType.Grass, 0.2, 20);

        var flat = new ForestEdge(from, to, 1.0, 0.0);
        var uphill = new ForestEdge(from, to, 1.0, 0.3);
        var downhill = new ForestEdge(from, to, 1.0, -0.3);

        _output.WriteLine($"downhill = {downhill.FireSpreadModifier:F6}");
        _output.WriteLine($"flat     = {flat.FireSpreadModifier:F6}");
        _output.WriteLine($"uphill   = {uphill.FireSpreadModifier:F6}");

        Assert.True(uphill.FireSpreadModifier > flat.FireSpreadModifier);
        Assert.True(flat.FireSpreadModifier > downhill.FireSpreadModifier);
    }

    [Fact]
    public void BridgeSpreadBonus_ReallyChangesEdgeModifier()
    {
        _output.WriteLine("=== BridgeSpreadBonus_ReallyChangesEdgeModifier ===");

        var a = new ForestCell(0, 0, VegetationType.Grass, 0.2, 10);
        var b = new ForestCell(1, 0, VegetationType.Grass, 0.2, 10);

        var edge = new ForestEdge(a, b, 1.5, 0.0);
        var before = edge.FireSpreadModifier;

        edge.ApplyBridgeSpreadBonus(1.55);
        var after = edge.FireSpreadModifier;

        _output.WriteLine($"before = {before:F6}");
        _output.WriteLine($"after  = {after:F6}");

        Assert.True(after > before);
    }

    [Fact]
    public void ClusterIds_ArePreservedInCells()
    {
        _output.WriteLine("=== ClusterIds_ArePreservedInCells ===");

        var cell = new ForestCell(3, 4, VegetationType.Mixed, 0.4, 20, "region-1-0");
        Assert.Equal("region-1-0", cell.ClusterId);

        cell.SetClusterId("region-2-2");
        Assert.Equal("region-2-2", cell.ClusterId);
    }

    [Fact]
    public void RegionBridgeEdge_IsUsuallyWeakerThanLocalShortEdgeBeforeBonusCap()
    {
        _output.WriteLine("=== RegionBridgeEdge_IsUsuallyWeakerThanLocalShortEdgeBeforeBonusCap ===");

        var localA = new ForestCell(0, 0, VegetationType.Grass, 0.2, 10, "region-A");
        var localB = new ForestCell(1, 0, VegetationType.Grass, 0.2, 10, "region-A");

        var bridgeA = new ForestCell(0, 0, VegetationType.Grass, 0.2, 10, "region-A");
        var bridgeB = new ForestCell(2, 0, VegetationType.Grass, 0.2, 10, "region-B");

        var localEdge = new ForestEdge(localA, localB, 1.0, 0.0);
        var bridgeEdge = new ForestEdge(bridgeA, bridgeB, 1.45, 0.0);
        bridgeEdge.ApplyBridgeSpreadBonus(1.55);

        _output.WriteLine($"localEdge  = {localEdge.FireSpreadModifier:F6}");
        _output.WriteLine($"bridgeEdge = {bridgeEdge.FireSpreadModifier:F6}");

        Assert.True(bridgeEdge.FireSpreadModifier <= localEdge.FireSpreadModifier * 2.0);
    }
}