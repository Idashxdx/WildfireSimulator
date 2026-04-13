using System;
using System.Collections.Generic;
using System.Linq;
using WildfireSimulator.Application.Services;
using WildfireSimulator.Domain.Models;
using Xunit;
using Xunit.Abstractions;

namespace WildfireSimulator.Tests.Unit;

public class RegionPhysicsAuditTests
{
    private readonly ITestOutputHelper _output;
    private readonly FireSpreadCalculator _calculator;

    public RegionPhysicsAuditTests(ITestOutputHelper output)
    {
        _output = output;
        _calculator = new FireSpreadCalculator();
    }

    [Fact]
    public void SingleSource_AccumulatedHeat_GrowsOnLocalNeighbor()
    {
        _output.WriteLine("=== SingleSource_AccumulatedHeat_GrowsOnLocalNeighbor ===");

        var source = CreateBurningCell(
            x: 10,
            y: 10,
            vegetation: VegetationType.Coniferous,
            moisture: 0.15,
            elevation: 50,
            burningAgeSeconds: 1800);

        var target = new ForestCell(
            x: 11,
            y: 10,
            vegetation: VegetationType.Grass,
            moisture: 0.20,
            elevation: 50,
            clusterId: "region-0-0");

        var weather = new WeatherCondition(
            DateTime.UtcNow,
            temperature: 30,
            humidity: 25,
            windSpeedMps: 3,
            windDirectionDegrees: 90,
            precipitation: 0);

        const double stepSeconds = 900.0;

        var heats = new List<double>();
        double accumulated = 0.0;

        for (int step = 1; step <= 5; step++)
        {
            var heat = _calculator.CalculateHeatFlow(source, target, weather, stepSeconds);
            accumulated += heat;
            heats.Add(accumulated);

            _output.WriteLine($"step={step} | heat={heat:F4} | accumulated={accumulated:F4}");
        }

        for (int i = 1; i < heats.Count; i++)
        {
            Assert.True(
                heats[i] > heats[i - 1],
                $"Накопленное тепло должно расти: step {i} -> {i + 1}");
        }
    }

    [Fact]
    public void AccumulatedHeat_Cooldown_DoesNotEraseTooFast()
    {
        _output.WriteLine("=== AccumulatedHeat_Cooldown_DoesNotEraseTooFast ===");

        var cell = new ForestCell(
            x: 0,
            y: 0,
            vegetation: VegetationType.Grass,
            moisture: 0.20,
            elevation: 10,
            clusterId: "region-0-0");

        cell.SetAccumulatedHeatJ(1_000_000.0);

        var values = new List<double> { 1_000_000.0 };

        const double retentionFactor = 0.90;

        for (int i = 1; i <= 5; i++)
        {
            cell.CoolDown(retentionFactor);
            values.Add(cell.AccumulatedHeatJ);
            _output.WriteLine($"cooldown_step={i} | accumulated={cell.AccumulatedHeatJ:F4}");
        }

        for (int i = 1; i < values.Count; i++)
        {
            Assert.True(values[i] < values[i - 1], "При cooling тепло должно уменьшаться");
            Assert.True(values[i] > 0.0, "Тепло не должно мгновенно исчезать");
        }

        Assert.True(
            values[1] >= 800_000.0,
            "После одного охлаждения тепло не должно практически обнуляться");
    }

    [Fact]
    public void IgnitionProbability_FromSingleSource_IsNonDecreasingAcrossSteps()
    {
        _output.WriteLine("=== IgnitionProbability_FromSingleSource_IsNonDecreasingAcrossSteps ===");

        var source = CreateBurningCell(
            x: 10,
            y: 10,
            vegetation: VegetationType.Coniferous,
            moisture: 0.15,
            elevation: 50,
            burningAgeSeconds: 1800);

        var target = new ForestCell(
            x: 11,
            y: 10,
            vegetation: VegetationType.Grass,
            moisture: 0.20,
            elevation: 50,
            clusterId: "region-0-0");

        var weather = new WeatherCondition(
            DateTime.UtcNow,
            temperature: 30,
            humidity: 25,
            windSpeedMps: 3,
            windDirectionDegrees: 90,
            precipitation: 0);

        const double stepSeconds = 900.0;

        var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
        double accumulatedHeat = 0.0;
        var probs = new List<double>();

        for (int step = 1; step <= 5; step++)
        {
            accumulatedHeat += _calculator.CalculateHeatFlow(source, target, weather, stepSeconds);
            var probability = _calculator.CalculateIgnitionProbability(accumulatedHeat, threshold);
            probs.Add(probability);

            _output.WriteLine(
                $"step={step} | accumulatedHeat={accumulatedHeat:F4} | threshold={threshold:F4} | prob={probability:F6}");
        }

        for (int i = 1; i < probs.Count; i++)
        {
            Assert.True(
                probs[i] >= probs[i - 1],
                $"Вероятность должна не убывать: step {i} -> {i + 1}");
        }

        Assert.True(
            probs.Last() > probs.First(),
            "При накоплении тепла вероятность должна заметно вырасти");
    }

    [Fact]
    public void RegionLikeBridgeEdge_IsWeakerThanLocalEdge()
    {
        _output.WriteLine("=== RegionLikeBridgeEdge_IsWeakerThanLocalEdge ===");

        var a = new ForestCell(0, 0, VegetationType.Grass, 0.2, 10, "region-a");
        var b = new ForestCell(1, 0, VegetationType.Grass, 0.2, 10, "region-a");
        var c = new ForestCell(2, 0, VegetationType.Grass, 0.2, 10, "region-b");

        var local = new ForestEdge(a, b, distance: 1.0, slope: 0.0);
        var bridge = new ForestEdge(b, c, distance: 1.35, slope: 0.0);

        bridge.ApplyBridgeSpreadBonus(1.15);

        _output.WriteLine($"local  = {local.FireSpreadModifier:F6}");
        _output.WriteLine($"bridge = {bridge.FireSpreadModifier:F6}");

        Assert.True(
            bridge.FireSpreadModifier < local.FireSpreadModifier,
            "Мост между регионами должен быть слабее короткой локальной связи");
    }

    [Fact]
    public void LocalEdgeStrength_ProducesHigherHeatThanBridgeEdge()
    {
        _output.WriteLine("=== LocalEdgeStrength_ProducesHigherHeatThanBridgeEdge ===");

        var source = CreateBurningCell(
            x: 10,
            y: 10,
            vegetation: VegetationType.Coniferous,
            moisture: 0.15,
            elevation: 50,
            burningAgeSeconds: 1800);

        var localTarget = new ForestCell(11, 10, VegetationType.Grass, 0.2, 50, "region-0-0");
        var bridgeTarget = new ForestCell(12, 10, VegetationType.Grass, 0.2, 50, "region-0-1");

        var weather = new WeatherCondition(
            DateTime.UtcNow,
            temperature: 30,
            humidity: 25,
            windSpeedMps: 0,
            windDirectionDegrees: 0,
            precipitation: 0);

        const double stepSeconds = 900.0;

        var localHeat = _calculator.CalculateHeatFlow(source, localTarget, weather, stepSeconds);
        var bridgeHeat = _calculator.CalculateHeatFlow(source, bridgeTarget, weather, stepSeconds);

        _output.WriteLine($"localHeat  = {localHeat:F4}");
        _output.WriteLine($"bridgeHeat = {bridgeHeat:F4}");

        Assert.True(
            localHeat > bridgeHeat,
            "Локальная близкая связь должна переносить больше тепла, чем более дальняя bridge-like связь");
    }

    [Fact]
    public void DryScenario_SingleSourceHasMeaningfulChanceToWarmNearestNeighbor()
    {
        _output.WriteLine("=== DryScenario_SingleSourceHasMeaningfulChanceToWarmNearestNeighbor ===");

        var source = CreateBurningCell(
            x: 10,
            y: 10,
            vegetation: VegetationType.Coniferous,
            moisture: 0.10,
            elevation: 50,
            burningAgeSeconds: 2400);

        var target = new ForestCell(
            x: 11,
            y: 10,
            vegetation: VegetationType.Grass,
            moisture: 0.10,
            elevation: 50,
            clusterId: "region-0-0");

        var weather = new WeatherCondition(
            DateTime.UtcNow,
            temperature: 35,
            humidity: 20,
            windSpeedMps: 5,
            windDirectionDegrees: 90,
            precipitation: 0);

        const double stepSeconds = 900.0;

        var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
        double accumulatedHeat = 0.0;
        double probability = 0.0;

        for (int step = 1; step <= 5; step++)
        {
            accumulatedHeat += _calculator.CalculateHeatFlow(source, target, weather, stepSeconds);
            probability = _calculator.CalculateIgnitionProbability(accumulatedHeat, threshold);

            _output.WriteLine(
                $"step={step} | accumulatedHeat={accumulatedHeat:F4} | threshold={threshold:F4} | prob={probability:F6}");
        }

        Assert.True(
            probability > 0.02,
            "В сухом локальном сценарии вероятность к 5 шагу должна быть хотя бы заметной");
    }

    private static ForestCell CreateBurningCell(
        int x,
        int y,
        VegetationType vegetation,
        double moisture,
        double elevation,
        int burningAgeSeconds)
    {
        var cell = new ForestCell(x, y, vegetation, moisture, elevation, "region-0-0");
        cell.Ignite(DateTime.UtcNow.AddSeconds(-burningAgeSeconds));
        return cell;
    }
}