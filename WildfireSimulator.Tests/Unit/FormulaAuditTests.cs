using System;
using WildfireSimulator.Application.Services;
using WildfireSimulator.Domain.Models;
using Xunit;
using Xunit.Abstractions;

namespace WildfireSimulator.Tests.Unit;

public class FormulaAuditTests
{
    private readonly ITestOutputHelper _output;
    private readonly FireSpreadCalculator _calculator;

    private const double EffectiveHeatingAreaM2 = 220.0;
    private const double HeatTransferEfficiency = 0.00030;

    public FormulaAuditTests(ITestOutputHelper output)
    {
        _output = output;
        _calculator = new FireSpreadCalculator();
    }

    [Fact]
    public void Formula_5_SpreadRate_EqualsExpectedCalculation()
    {
        var vegetation = VegetationType.Coniferous;
        var moisture = 0.20;
        var burnAgeSeconds = 1800;
        var weather = new WeatherCondition(DateTime.UtcNow, 30, 40, 8, 0, 0);
        var source = CreateSource(10, 10, vegetation, moisture, 50, burnAgeSeconds);

        var actual = _calculator.CalculateSpreadRate(source, weather);

        var parameters = FireModelCatalog.Get(vegetation);
        var progress = burnAgeSeconds / parameters.BaseBurnDurationSeconds;

        var windEffect = Math.Clamp(1.0 + weather.WindSpeedMps * 0.07, 0.7, 2.2);
        var moistureEffect = Math.Max(1.0 - moisture * 0.5, 0.3);
        var progressEffect = Math.Max(Math.Sin(progress * Math.PI), 0.60);

        var expected = Math.Max(
            parameters.BaseSpreadRateMps * windEffect * moistureEffect * progressEffect,
            0.001);

        _output.WriteLine($"expected spread rate = {expected:F8}");
        _output.WriteLine($"actual spread rate   = {actual:F8}");
        Assert.True(
    Math.Abs(actual - expected) < 0.0001,
    $"Expected={expected}, Actual={actual}, Diff={Math.Abs(actual - expected)}");
    }

    [Fact]
    public void Formula_6_FireIntensity_EqualsExpectedCalculation()
    {
        var vegetation = VegetationType.Coniferous;
        var moisture = 0.15;
        var burnAgeSeconds = 1800;
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);
        var source = CreateSource(10, 10, vegetation, moisture, 50, burnAgeSeconds);

        var actual = _calculator.CalculateFireIntensity(source, weather);

        var parameters = FireModelCatalog.Get(vegetation);
        var spreadRate = _calculator.CalculateSpreadRate(source, weather);
        var progress = burnAgeSeconds / parameters.BaseBurnDurationSeconds;

        var intensityFactor = Math.Max(Math.Sin(progress * Math.PI), 0.35);
        var durationFactor = 5400.0 / Math.Max(5400.0, parameters.BaseBurnDurationSeconds);
        durationFactor = Math.Clamp(durationFactor, 0.45, 1.0);

        var expected = parameters.HeatOfCombustion *
                       parameters.FuelLoadKgPerM2 *
                       spreadRate *
                       intensityFactor *
                       durationFactor;

        expected = Math.Min(expected, 3500.0);

        _output.WriteLine($"expected intensity = {expected:F8}");
        _output.WriteLine($"actual intensity   = {actual:F8}");
        Assert.True(
            Math.Abs(actual - expected) < 0.0001,
            $"Expected={expected}, Actual={actual}");
    }

    [Fact]
    public void Formula_7_HeatFlow_IsLinearByStepDuration()
    {
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
    public void Formula_7_HeatFlow_EqualsExpectedCalculation_ForSimpleFlatCase()
    {
        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var target = new ForestCell(10, 11, VegetationType.Grass, 0.20, 50);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);
        var stepSeconds = 3600.0;

        var actual = _calculator.CalculateHeatFlow(source, target, weather, stepSeconds);

        var parameters = FireModelCatalog.Get(source.Vegetation);
        var progress = source.BurningElapsedSeconds / parameters.BaseBurnDurationSeconds;

        var burningStageFactor = 0.70 + 0.65 * Math.Sin(Math.PI * progress);
        burningStageFactor = Math.Clamp(burningStageFactor, 0.60, 1.35);

        var intensity = _calculator.CalculateFireIntensity(source, weather);
        var distanceFactor = 1.0;
        var windFactor = 1.0;
        var slopeFactor = 1.0;

        var expected =
            intensity * 1000.0 *
            EffectiveHeatingAreaM2 *
            stepSeconds *
            burningStageFactor *
            distanceFactor *
            windFactor *
            slopeFactor *
            HeatTransferEfficiency;

        expected = Math.Max(0.0, Math.Min(expected, 1e10));

        _output.WriteLine($"expected heat = {expected:F4}");
        _output.WriteLine($"actual heat   = {actual:F4}");

        Assert.Equal(expected, actual, precision: 6);
    }

    [Fact]
    public void Formula_8_DistanceFactor_FollowsInverseSquareLaw()
    {
        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);

        var target1 = new ForestCell(11, 10, VegetationType.Grass, 0.2, 50);
        var target2 = new ForestCell(12, 10, VegetationType.Grass, 0.2, 50);
        var target3 = new ForestCell(13, 10, VegetationType.Grass, 0.2, 50);
        var target4 = new ForestCell(14, 10, VegetationType.Grass, 0.2, 50);

        var heat1 = _calculator.CalculateHeatFlow(source, target1, weather, 3600);
        var heat2 = _calculator.CalculateHeatFlow(source, target2, weather, 3600);
        var heat3 = _calculator.CalculateHeatFlow(source, target3, weather, 3600);
        var heat4 = _calculator.CalculateHeatFlow(source, target4, weather, 3600);

        _output.WriteLine($"d=1 heat={heat1:F4}");
        _output.WriteLine($"d=2 heat={heat2:F4}");
        _output.WriteLine($"d=3 heat={heat3:F4}");
        _output.WriteLine($"d=4 heat={heat4:F4}");

        Assert.InRange(heat1 / heat2, 3.95, 4.05);
        Assert.InRange(heat1 / heat3, 8.95, 9.05);
        Assert.InRange(heat1 / heat4, 15.95, 16.05);
    }

    [Fact]
    public void Formula_22_IgnitionThreshold_EqualsExpectedCalculation()
    {
        var vegetation = VegetationType.Grass;
        var moisture = 0.30;
        var target = new ForestCell(10, 11, vegetation, moisture, 50);
        var weather = new WeatherCondition(DateTime.UtcNow, 30, 60, 5, 0, 0);

        var actual = _calculator.CalculateIgnitionThreshold(target, weather);

        var parameters = FireModelCatalog.Get(vegetation);

        var fuelMoistureFactor = Math.Clamp(1.0 + moisture * 0.9, 1.0, 1.9);

        var temperatureFactor = 1.0;
        if (weather.Temperature > 20.0)
        {
            temperatureFactor = 1.0 - (weather.Temperature - 20.0) * 0.03;
            temperatureFactor = Math.Clamp(temperatureFactor, 0.45, 1.0);
        }
        else if (weather.Temperature < 10.0)
        {
            temperatureFactor = 1.0 + (10.0 - weather.Temperature) * 0.02;
            temperatureFactor = Math.Clamp(temperatureFactor, 1.0, 1.4);
        }

        var airHumidityFactor = 1.0 + (weather.Humidity / 100.0) * 0.8;
        airHumidityFactor = Math.Clamp(airHumidityFactor, 1.0, 1.8);

        var expected = parameters.BaseIgnitionThresholdJ *
                       fuelMoistureFactor *
                       temperatureFactor *
                       airHumidityFactor;

        _output.WriteLine($"expected threshold = {expected:F4}");
        _output.WriteLine($"actual threshold   = {actual:F4}");

        Assert.Equal(expected, actual, precision: 5);
    }

    [Theory]
    [InlineData(0.00, 0.00)]
    [InlineData(0.015, 0.02)]
    [InlineData(0.03, 0.04)]
    [InlineData(0.10, 0.16)]
    [InlineData(0.25, 0.40)]
    [InlineData(0.50, 0.65)]
    [InlineData(0.75, 0.82)]
    [InlineData(1.00, 0.96)]
    [InlineData(2.00, 0.96)]
    public void Formula_23_25_IgnitionProbability_FollowsPiecewiseFunction(double ratio, double expectedProbability)
    {
        var threshold = 1000.0;
        var totalHeat = ratio * threshold;

        var actual = _calculator.CalculateIgnitionProbability(totalHeat, threshold);

        _output.WriteLine($"ratio={ratio:F3}, expected={expectedProbability:F6}, actual={actual:F6}");

        Assert.Equal(expectedProbability, actual, precision: 6);
        Assert.InRange(actual, 0.0, 1.0);
    }

    [Fact]
    public void Formula_22_WaterAndBare_HaveBlockedIgnitionThreshold()
    {
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);

        var water = new ForestCell(0, 0, VegetationType.Water, 1.0, 0);
        var bare = new ForestCell(0, 0, VegetationType.Bare, 0.0, 0);

        var waterThreshold = _calculator.CalculateIgnitionThreshold(water, weather);
        var bareThreshold = _calculator.CalculateIgnitionThreshold(bare, weather);

        Assert.Equal(double.MaxValue, waterThreshold);
        Assert.Equal(double.MaxValue, bareThreshold);
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