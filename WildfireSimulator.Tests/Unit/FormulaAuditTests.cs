using System;
using System.Collections.Generic;
using System.Linq;
using WildfireSimulator.Application.Services;
using WildfireSimulator.Domain.Models;
using Xunit;
using Xunit.Abstractions;

namespace WildfireSimulator.Tests.Unit;

public class FormulaAuditTests
{
    private readonly ITestOutputHelper _output;
    private readonly FireSpreadCalculator _calculator;

    public FormulaAuditTests(ITestOutputHelper output)
    {
        _output = output;

        _calculator = new FireSpreadCalculator();
    }

    [Fact]
    public void HeatFlow_IsLinearByStepDuration()
    {
        _output.WriteLine("=== HeatFlow_IsLinearByStepDuration ===");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var target = new ForestCell(10, 11, VegetationType.Grass, 0.20, 50);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);

        var heat300 = _calculator.CalculateHeatFlow(source, target, weather, 300);
        var heat600 = _calculator.CalculateHeatFlow(source, target, weather, 600);
        var heat1800 = _calculator.CalculateHeatFlow(source, target, weather, 1800);

        _output.WriteLine($"heat300  = {heat300:F4}");
        _output.WriteLine($"heat600  = {heat600:F4}");
        _output.WriteLine($"heat1800 = {heat1800:F4}");

        Assert.InRange(heat600 / heat300, 1.95, 2.05);
        Assert.InRange(heat1800 / heat300, 5.90, 6.10);
    }

    [Fact]
    public void WindDirection_ZeroWind_GivesSymmetricHeat()
    {
        _output.WriteLine("=== WindDirection_ZeroWind_GivesSymmetricHeat ===");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);

        var north = new ForestCell(10, 9, VegetationType.Grass, 0.2, 50);
        var south = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var east = new ForestCell(11, 10, VegetationType.Grass, 0.2, 50);
        var west = new ForestCell(9, 10, VegetationType.Grass, 0.2, 50);

        var hn = _calculator.CalculateHeatFlow(source, north, weather, 3600);
        var hs = _calculator.CalculateHeatFlow(source, south, weather, 3600);
        var he = _calculator.CalculateHeatFlow(source, east, weather, 3600);
        var hw = _calculator.CalculateHeatFlow(source, west, weather, 3600);

        _output.WriteLine($"N={hn:F4}, S={hs:F4}, E={he:F4}, W={hw:F4}");

        Assert.InRange(hn / hs, 0.999, 1.001);
        Assert.InRange(he / hw, 0.999, 1.001);
        Assert.InRange(hn / he, 0.999, 1.001);
    }

    [Fact]
    public void WindDirection_DownwindCrosswindUpwind_OrderIsCorrect()
    {
        _output.WriteLine("=== WindDirection_DownwindCrosswindUpwind_OrderIsCorrect ===");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 12, 0, 0);

        var downwind = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var upwind = new ForestCell(10, 9, VegetationType.Grass, 0.2, 50);
        var cross1 = new ForestCell(11, 10, VegetationType.Grass, 0.2, 50);
        var cross2 = new ForestCell(9, 10, VegetationType.Grass, 0.2, 50);

        var hDown = _calculator.CalculateHeatFlow(source, downwind, weather, 3600);
        var hUp = _calculator.CalculateHeatFlow(source, upwind, weather, 3600);
        var hCross1 = _calculator.CalculateHeatFlow(source, cross1, weather, 3600);
        var hCross2 = _calculator.CalculateHeatFlow(source, cross2, weather, 3600);
        var hCross = (hCross1 + hCross2) / 2.0;

        _output.WriteLine($"downwind = {hDown:F4}");
        _output.WriteLine($"cross    = {hCross:F4}");
        _output.WriteLine($"upwind   = {hUp:F4}");

        Assert.True(hDown > hCross, "По ветру должно быть сильнее, чем поперек");
        Assert.True(hCross > hUp, "Поперек должно быть сильнее, чем против ветра");
    }

    [Fact]
    public void StrongerWind_IncreasesDownwindToUpwindContrast()
    {
        _output.WriteLine("=== StrongerWind_IncreasesDownwindToUpwindContrast ===");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);

        var lowWind = new WeatherCondition(DateTime.UtcNow, 25, 40, 3, 0, 0);
        var highWind = new WeatherCondition(DateTime.UtcNow, 25, 40, 15, 0, 0);

        var downwind = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var upwind = new ForestCell(10, 9, VegetationType.Grass, 0.2, 50);

        var lowDown = _calculator.CalculateHeatFlow(source, downwind, lowWind, 3600);
        var lowUp = _calculator.CalculateHeatFlow(source, upwind, lowWind, 3600);
        var highDown = _calculator.CalculateHeatFlow(source, downwind, highWind, 3600);
        var highUp = _calculator.CalculateHeatFlow(source, upwind, highWind, 3600);

        var lowContrast = lowDown / lowUp;
        var highContrast = highDown / highUp;

        _output.WriteLine($"low contrast  = {lowContrast:F4}");
        _output.WriteLine($"high contrast = {highContrast:F4}");

        Assert.True(highContrast > lowContrast,
            "Сильный ветер должен сильнее разделять downwind и upwind");
    }

    [Fact]
    public void SlopeFactor_IsMonotonic()
    {
        _output.WriteLine("=== SlopeFactor_IsMonotonic ===");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);

        var downhill = new ForestCell(10, 11, VegetationType.Grass, 0.2, 20);
        var flat = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var uphill = new ForestCell(10, 11, VegetationType.Grass, 0.2, 80);

        var dbgDown = _calculator.GetHeatFlowDebugInfo(source, downhill, weather, 3600);
        var dbgFlat = _calculator.GetHeatFlowDebugInfo(source, flat, weather, 3600);
        var dbgUp = _calculator.GetHeatFlowDebugInfo(source, uphill, weather, 3600);

        _output.WriteLine($"downhill slopeFactor = {dbgDown.SlopeFactor:F4}");
        _output.WriteLine($"flat slopeFactor     = {dbgFlat.SlopeFactor:F4}");
        _output.WriteLine($"uphill slopeFactor   = {dbgUp.SlopeFactor:F4}");

        Assert.True(dbgDown.SlopeFactor < dbgFlat.SlopeFactor);
        Assert.True(dbgFlat.SlopeFactor <= dbgUp.SlopeFactor);
        Assert.InRange(dbgFlat.SlopeFactor, 0.999, 1.001);
    }

    [Fact]
    public void Precipitation_MonotonicallyReducesHeatFlow()
    {
        _output.WriteLine("=== Precipitation_MonotonicallyReducesHeatFlow ===");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var target = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);

        var precipitations = new[] { 0.0, 2.0, 5.0, 10.0, 20.0, 40.0 };
        double? prevHeat = null;

        foreach (var p in precipitations)
        {
            var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, p);
            var heat = _calculator.CalculateHeatFlow(source, target, weather, 3600);
            _output.WriteLine($"p={p,5:F1} -> heat={heat:F4}");

            if (prevHeat.HasValue)
                Assert.True(heat <= prevHeat.Value + 1e-9,
                    $"Heat should not increase when precipitation rises: prev={prevHeat}, current={heat}");

            prevHeat = heat;
        }
    }

    [Fact]
    public void Precipitation_MonotonicallyRaisesIgnitionThreshold()
    {
        _output.WriteLine("=== Precipitation_MonotonicallyRaisesIgnitionThreshold ===");

        var target = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);

        var precipitations = new[] { 0.0, 2.0, 5.0, 10.0, 20.0, 40.0 };
        double? prevThreshold = null;

        foreach (var p in precipitations)
        {
            var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, p);
            var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
            _output.WriteLine($"p={p,5:F1} -> threshold={threshold:F4}");

            if (prevThreshold.HasValue)
                Assert.True(threshold >= prevThreshold.Value - 1e-9,
                    $"Threshold should not decrease when precipitation rises: prev={prevThreshold}, current={threshold}");

            prevThreshold = threshold;
        }
    }

    [Fact]
    public void TemperatureEffect_OnThreshold_IsMonotonicInWarmRange()
    {
        _output.WriteLine("=== TemperatureEffect_OnThreshold_IsMonotonicInWarmRange ===");

        var target = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var temps = new[] { 20.0, 25.0, 30.0, 35.0, 40.0 };

        double? prevThreshold = null;

        foreach (var t in temps)
        {
            var weather = new WeatherCondition(DateTime.UtcNow, t, 40, 5, 0, 0);
            var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
            _output.WriteLine($"temp={t,5:F1} -> threshold={threshold:F4}");

            if (prevThreshold.HasValue)
                Assert.True(threshold <= prevThreshold.Value + 1e-9,
                    "В тёплом диапазоне рост температуры не должен повышать threshold");

            prevThreshold = threshold;
        }
    }

    [Fact]
    public void HumidityEffect_OnThreshold_IsMonotonic()
    {
        _output.WriteLine("=== HumidityEffect_OnThreshold_IsMonotonic ===");

        var target = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var humidities = new[] { 10.0, 20.0, 40.0, 60.0, 80.0, 100.0 };

        double? prevThreshold = null;

        foreach (var h in humidities)
        {
            var weather = new WeatherCondition(DateTime.UtcNow, 25, h, 5, 0, 0);
            var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
            _output.WriteLine($"humidity={h,5:F1} -> threshold={threshold:F4}");

            if (prevThreshold.HasValue)
                Assert.True(threshold >= prevThreshold.Value - 1e-9,
                    "Рост влажности воздуха не должен понижать threshold");

            prevThreshold = threshold;
        }
    }

    [Fact]
    public void FireIntensity_HasInteriorPeak_NotMonotonicGrowthForever()
    {
        _output.WriteLine("=== FireIntensity_HasInteriorPeak_NotMonotonicGrowthForever ===");

        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);
        var veg = VegetationType.Coniferous;
        var burnDuration = FireModelCatalog.Get(veg).BaseBurnDurationSeconds;

        var checkpoints = new[]
        {
            0.05 * burnDuration,
            0.20 * burnDuration,
            0.40 * burnDuration,
            0.60 * burnDuration,
            0.80 * burnDuration,
            0.95 * burnDuration
        };

        var intensities = new List<double>();

        foreach (var seconds in checkpoints)
        {
            var source = CreateSource(10, 10, veg, 0.15, 50, (int)seconds);
            var intensity = _calculator.CalculateFireIntensity(source, weather);
            intensities.Add(intensity);
            _output.WriteLine($"burnAge={seconds,8:F0}s -> intensity={intensity:F4}");
        }

        var peakIndex = intensities.IndexOf(intensities.Max());
        _output.WriteLine($"peakIndex={peakIndex}, peak={intensities.Max():F4}");

        Assert.True(peakIndex > 0, "Пик не должен быть в самом начале");
        Assert.True(peakIndex < intensities.Count - 1, "Пик не должен быть в самом конце");
    }

    [Fact]
    public void WaterAndBare_HaveBlockedIgnitionThreshold()
    {
        _output.WriteLine("=== WaterAndBare_HaveBlockedIgnitionThreshold ===");

        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);

        var water = new ForestCell(0, 0, VegetationType.Water, 1.0, 0);
        var bare = new ForestCell(0, 0, VegetationType.Bare, 0.0, 0);

        var waterThreshold = _calculator.CalculateIgnitionThreshold(water, weather);
        var bareThreshold = _calculator.CalculateIgnitionThreshold(bare, weather);

        _output.WriteLine($"waterThreshold = {waterThreshold}");
        _output.WriteLine($"bareThreshold  = {bareThreshold}");

        Assert.True(double.IsInfinity(waterThreshold) || waterThreshold == double.MaxValue);
        Assert.True(double.IsInfinity(bareThreshold) || bareThreshold == double.MaxValue);
    }

    [Fact]
    public void MultipleSources_ProbabilityIsHigherThanSingleSource()
    {
        _output.WriteLine("=== MultipleSources_ProbabilityIsHigherThanSingleSource ===");

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

        var singleProb = _calculator.CalculateIgnitionProbability(singleHeat, threshold);
        var totalProb = _calculator.CalculateIgnitionProbability(totalHeat, threshold);

        _output.WriteLine($"singleHeat = {singleHeat:F4}");
        _output.WriteLine($"totalHeat  = {totalHeat:F4}");
        _output.WriteLine($"singleProb = {singleProb:F6}");
        _output.WriteLine($"totalProb  = {totalProb:F6}");

        Assert.True(totalHeat > singleHeat);
        Assert.True(totalProb > singleProb);
    }

    private ForestCell CreateSource(
        int x,
        int y,
        VegetationType veg,
        double moisture,
        double elevation,
        int burningAgeSeconds)
    {
        var cell = new ForestCell(x, y, veg, moisture, elevation);
        cell.Ignite(DateTime.UtcNow.AddSeconds(-burningAgeSeconds));
        return cell;
    }
}