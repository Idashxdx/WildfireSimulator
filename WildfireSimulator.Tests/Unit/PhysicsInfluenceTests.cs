using System;
using System.Collections.Generic;
using System.Linq;
using WildfireSimulator.Application.Services;
using WildfireSimulator.Domain.Models;
using Xunit;
using Xunit.Abstractions;

namespace WildfireSimulator.Tests.Unit;

public class PhysicsInfluenceTests
{
    private readonly ITestOutputHelper _output;
    private readonly FireSpreadCalculator _calculator;

    public PhysicsInfluenceTests(ITestOutputHelper output)
    {
        _output = output;
        _calculator = new FireSpreadCalculator();
    }

    [Fact]
    public void FuelMoisture_IncreasesIgnitionThreshold_AndReducesProbability()
    {
        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);

        var dry = new ForestCell(10, 11, VegetationType.Grass, 0.10, 50);
        var wet = new ForestCell(10, 11, VegetationType.Grass, 0.80, 50);

        var dryThreshold = _calculator.CalculateIgnitionThreshold(dry, weather);
        var wetThreshold = _calculator.CalculateIgnitionThreshold(wet, weather);

        var dryHeat = _calculator.CalculateHeatFlow(source, dry, weather, 3600);
        var wetHeat = _calculator.CalculateHeatFlow(source, wet, weather, 3600);

        var dryProbability = _calculator.CalculateIgnitionProbability(dryHeat, dryThreshold);
        var wetProbability = _calculator.CalculateIgnitionProbability(wetHeat, wetThreshold);

        _output.WriteLine($"dry threshold={dryThreshold:F4}, prob={dryProbability:F6}");
        _output.WriteLine($"wet threshold={wetThreshold:F4}, prob={wetProbability:F6}");

        Assert.True(wetThreshold > dryThreshold);
        Assert.True(wetProbability < dryProbability);
    }

    [Fact]
    public void Temperature_InWarmRange_ReducesIgnitionThreshold()
    {
        var target = new ForestCell(10, 11, VegetationType.Grass, 0.25, 50);
        var temperatures = new[] { 20.0, 25.0, 30.0, 35.0, 40.0 };

        double? previousThreshold = null;

        foreach (var temperature in temperatures)
        {
            var weather = new WeatherCondition(DateTime.UtcNow, temperature, 40, 5, 0, 0);
            var threshold = _calculator.CalculateIgnitionThreshold(target, weather);

            _output.WriteLine($"temperature={temperature:F1}, threshold={threshold:F4}");

            if (previousThreshold.HasValue)
                Assert.True(threshold <= previousThreshold.Value + 1e-9);

            previousThreshold = threshold;
        }
    }

    [Fact]
    public void AirHumidity_IncreasesIgnitionThreshold()
    {
        var target = new ForestCell(10, 11, VegetationType.Grass, 0.25, 50);
        var humidities = new[] { 10.0, 20.0, 40.0, 60.0, 80.0, 100.0 };

        double? previousThreshold = null;

        foreach (var humidity in humidities)
        {
            var weather = new WeatherCondition(DateTime.UtcNow, 25, humidity, 5, 0, 0);
            var threshold = _calculator.CalculateIgnitionThreshold(target, weather);

            _output.WriteLine($"humidity={humidity:F1}, threshold={threshold:F4}");

            if (previousThreshold.HasValue)
                Assert.True(threshold >= previousThreshold.Value - 1e-9);

            previousThreshold = threshold;
        }
    }

    [Fact]
    public void WindDirection_ZeroWind_GivesSymmetricHeat()
    {
        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);

        var north = new ForestCell(10, 9, VegetationType.Grass, 0.2, 50);
        var south = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var east = new ForestCell(11, 10, VegetationType.Grass, 0.2, 50);
        var west = new ForestCell(9, 10, VegetationType.Grass, 0.2, 50);

        var northHeat = _calculator.CalculateHeatFlow(source, north, weather, 3600);
        var southHeat = _calculator.CalculateHeatFlow(source, south, weather, 3600);
        var eastHeat = _calculator.CalculateHeatFlow(source, east, weather, 3600);
        var westHeat = _calculator.CalculateHeatFlow(source, west, weather, 3600);

        _output.WriteLine($"N={northHeat:F4}, S={southHeat:F4}, E={eastHeat:F4}, W={westHeat:F4}");

        Assert.InRange(northHeat / southHeat, 0.999, 1.001);
        Assert.InRange(eastHeat / westHeat, 0.999, 1.001);
        Assert.InRange(northHeat / eastHeat, 0.999, 1.001);
    }

    [Fact]
    public void WindDirection_DownwindCrosswindUpwind_OrderIsCorrect()
    {
        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 12, 0, 0);

        var downwind = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var upwind = new ForestCell(10, 9, VegetationType.Grass, 0.2, 50);
        var cross1 = new ForestCell(11, 10, VegetationType.Grass, 0.2, 50);
        var cross2 = new ForestCell(9, 10, VegetationType.Grass, 0.2, 50);

        var downwindHeat = _calculator.CalculateHeatFlow(source, downwind, weather, 3600);
        var upwindHeat = _calculator.CalculateHeatFlow(source, upwind, weather, 3600);
        var crosswindHeat = (
            _calculator.CalculateHeatFlow(source, cross1, weather, 3600) +
            _calculator.CalculateHeatFlow(source, cross2, weather, 3600)
        ) / 2.0;

        _output.WriteLine($"downwind={downwindHeat:F4}");
        _output.WriteLine($"crosswind={crosswindHeat:F4}");
        _output.WriteLine($"upwind={upwindHeat:F4}");

        Assert.True(downwindHeat > crosswindHeat);
        Assert.True(crosswindHeat > upwindHeat);
    }

    [Fact]
    public void StrongerWind_IncreasesDownwindHeat_AndDownwindUpwindContrast()
    {
        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);

        var downwind = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var upwind = new ForestCell(10, 9, VegetationType.Grass, 0.2, 50);

        var lowWind = new WeatherCondition(DateTime.UtcNow, 25, 40, 3, 0, 0);
        var highWind = new WeatherCondition(DateTime.UtcNow, 25, 40, 15, 0, 0);

        var lowDownwindHeat = _calculator.CalculateHeatFlow(source, downwind, lowWind, 3600);
        var highDownwindHeat = _calculator.CalculateHeatFlow(source, downwind, highWind, 3600);

        var lowUpwindHeat = _calculator.CalculateHeatFlow(source, upwind, lowWind, 3600);
        var highUpwindHeat = _calculator.CalculateHeatFlow(source, upwind, highWind, 3600);

        var lowContrast = lowDownwindHeat / lowUpwindHeat;
        var highContrast = highDownwindHeat / highUpwindHeat;

        _output.WriteLine($"low downwind heat={lowDownwindHeat:F4}");
        _output.WriteLine($"high downwind heat={highDownwindHeat:F4}");
        _output.WriteLine($"low contrast={lowContrast:F4}");
        _output.WriteLine($"high contrast={highContrast:F4}");

        Assert.True(highDownwindHeat > lowDownwindHeat);
        Assert.True(highContrast > lowContrast);
    }

    [Fact]
    public void Slope_UpwardIncreasesHeat_DownwardReducesHeat()
    {
        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);

        var downhill = new ForestCell(10, 11, VegetationType.Grass, 0.2, 20);
        var flat = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var uphill = new ForestCell(10, 11, VegetationType.Grass, 0.2, 80);

        var downhillHeat = _calculator.CalculateHeatFlow(source, downhill, weather, 3600);
        var flatHeat = _calculator.CalculateHeatFlow(source, flat, weather, 3600);
        var uphillHeat = _calculator.CalculateHeatFlow(source, uphill, weather, 3600);

        var downhillDebug = _calculator.GetHeatFlowDebugInfo(source, downhill, weather, 3600);
        var flatDebug = _calculator.GetHeatFlowDebugInfo(source, flat, weather, 3600);
        var uphillDebug = _calculator.GetHeatFlowDebugInfo(source, uphill, weather, 3600);

        _output.WriteLine($"downhill factor={downhillDebug.SlopeFactor:F4}, heat={downhillHeat:F4}");
        _output.WriteLine($"flat factor={flatDebug.SlopeFactor:F4}, heat={flatHeat:F4}");
        _output.WriteLine($"uphill factor={uphillDebug.SlopeFactor:F4}, heat={uphillHeat:F4}");

        Assert.True(downhillDebug.SlopeFactor < flatDebug.SlopeFactor);
        Assert.True(flatDebug.SlopeFactor <= uphillDebug.SlopeFactor);
        Assert.True(downhillHeat < flatHeat);
        Assert.True(flatHeat <= uphillHeat);
    }

    [Fact]
    public void VegetationType_AffectsThresholdAndBurnDuration()
    {
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);

        var grass = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var shrub = new ForestCell(10, 11, VegetationType.Shrub, 0.2, 50);
        var coniferous = new ForestCell(10, 11, VegetationType.Coniferous, 0.2, 50);
        var deciduous = new ForestCell(10, 11, VegetationType.Deciduous, 0.2, 50);

        var grassThreshold = _calculator.CalculateIgnitionThreshold(grass, weather);
        var shrubThreshold = _calculator.CalculateIgnitionThreshold(shrub, weather);
        var coniferousThreshold = _calculator.CalculateIgnitionThreshold(coniferous, weather);
        var deciduousThreshold = _calculator.CalculateIgnitionThreshold(deciduous, weather);

        _output.WriteLine($"grass threshold={grassThreshold:F4}");
        _output.WriteLine($"shrub threshold={shrubThreshold:F4}");
        _output.WriteLine($"coniferous threshold={coniferousThreshold:F4}");
        _output.WriteLine($"deciduous threshold={deciduousThreshold:F4}");

        Assert.True(grassThreshold < shrubThreshold);
        Assert.True(shrubThreshold < deciduousThreshold);
        Assert.True(FireModelCatalog.Get(VegetationType.Grass).BaseBurnDurationSeconds <
                    FireModelCatalog.Get(VegetationType.Deciduous).BaseBurnDurationSeconds);
        Assert.True(FireModelCatalog.Get(VegetationType.Grass).FuelLoadKgPerM2 <
                    FireModelCatalog.Get(VegetationType.Coniferous).FuelLoadKgPerM2);
    }

    [Fact]
    public void WaterAndBare_DoNotReceiveHeat_AndHaveZeroIgnitionProbability()
    {
        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 35, 20, 15, 0, 0);

        var water = new ForestCell(10, 11, VegetationType.Water, 1.0, 50);
        var bare = new ForestCell(10, 11, VegetationType.Bare, 0.0, 50);

        var waterHeat = _calculator.CalculateHeatFlow(source, water, weather, 3600);
        var bareHeat = _calculator.CalculateHeatFlow(source, bare, weather, 3600);

        var waterProbability = _calculator.CalculateIgnitionProbability(
            waterHeat,
            _calculator.CalculateIgnitionThreshold(water, weather));

        var bareProbability = _calculator.CalculateIgnitionProbability(
            bareHeat,
            _calculator.CalculateIgnitionThreshold(bare, weather));

        _output.WriteLine($"water heat={waterHeat:F4}, probability={waterProbability:F6}");
        _output.WriteLine($"bare heat={bareHeat:F4}, probability={bareProbability:F6}");

        Assert.Equal(0.0, waterHeat);
        Assert.Equal(0.0, bareHeat);
        Assert.Equal(0.0, waterProbability);
        Assert.Equal(0.0, bareProbability);
    }

    [Fact]
    public void MultipleSources_IncreaseTotalHeatAndIgnitionProbability()
    {
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);
        var target = new ForestCell(10, 10, VegetationType.Grass, 0.2, 50);

        var sources = new[]
        {
            CreateSource(9, 10, VegetationType.Coniferous, 0.15, 50, 1800),
            CreateSource(11, 10, VegetationType.Coniferous, 0.15, 50, 1800),
            CreateSource(10, 9, VegetationType.Coniferous, 0.15, 50, 1800),
            CreateSource(10, 11, VegetationType.Coniferous, 0.15, 50, 1800)
        };

        var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
        var singleHeat = _calculator.CalculateHeatFlow(sources[0], target, weather, 3600);
        var totalHeat = sources.Sum(s => _calculator.CalculateHeatFlow(s, target, weather, 3600));

        var singleProbability = _calculator.CalculateIgnitionProbability(singleHeat, threshold);
        var totalProbability = _calculator.CalculateIgnitionProbability(totalHeat, threshold);

        _output.WriteLine($"single heat={singleHeat:F4}, probability={singleProbability:F6}");
        _output.WriteLine($"total heat={totalHeat:F4}, probability={totalProbability:F6}");

        Assert.True(totalHeat > singleHeat);
        Assert.True(totalProbability > singleProbability);
    }

private static ForestCell CreateSource(
    int x,
    int y,
    VegetationType vegetation,
    double moisture,
    double elevation,
    int burningAgeSeconds)
{
    var cell = new ForestCell(x, y, vegetation, moisture, elevation);
    cell.Ignite(DateTime.UtcNow);
    cell.SetBurningElapsedSeconds(burningAgeSeconds);
    return cell;
}
}