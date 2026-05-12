using System;
using WildfireSimulator.Application.Services;
using WildfireSimulator.Domain.Models;
using Xunit;
using Xunit.Abstractions;

namespace WildfireSimulator.Tests.Unit;

public class PhysicsSummaryTests
{
    private readonly ITestOutputHelper _output;
    private readonly FireSpreadCalculator _calculator;

    public PhysicsSummaryTests(ITestOutputHelper output)
    {
        _output = output;
        _calculator = new FireSpreadCalculator();
    }

    [Fact]
    public void Print_Physics_Model_Summary()
    {
        _output.WriteLine("==================================================");
        _output.WriteLine(" WILDFIRE PHYSICS MODEL SUMMARY");
        _output.WriteLine("==================================================");

        var weather = new WeatherCondition(
            DateTime.UtcNow,
            temperature: 25,
            humidity: 45,
            windSpeedMps: 4,
            windDirectionDegrees: 0,
            precipitation: 0);

        var source = new ForestCell(
            10,
            10,
            VegetationType.Coniferous,
            0.15,
            50);

        source.Ignite(DateTime.UtcNow.AddHours(-1));

        var target = new ForestCell(
            10,
            11,
            VegetationType.Grass,
            0.35,
            55);

        var spreadRate = _calculator.CalculateSpreadRate(source, weather);
        var intensity = _calculator.CalculateFireIntensity(source, weather);
        var heat = _calculator.CalculateHeatFlow(source, target, weather, 3600);
        var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
        var probability = _calculator.CalculateIgnitionProbability(heat, threshold);

        _output.WriteLine("");
        _output.WriteLine("=== SOURCE CELL ===");
        _output.WriteLine($"Vegetation          : {source.Vegetation}");
        _output.WriteLine($"Moisture            : {source.Moisture:F2}");
        _output.WriteLine($"Burning age         : {source.BurningElapsedSeconds:F0} s");

        _output.WriteLine("");
        _output.WriteLine("=== WEATHER ===");
        _output.WriteLine($"Temperature         : {weather.Temperature:F1} °C");
        _output.WriteLine($"Humidity            : {weather.Humidity:F1} %");
        _output.WriteLine($"Wind speed          : {weather.WindSpeedMps:F1} m/s");
        _output.WriteLine($"Wind direction      : {weather.WindDirectionDegrees:F0}°");

        _output.WriteLine("");
        _output.WriteLine("=== TARGET CELL ===");
        _output.WriteLine($"Vegetation          : {target.Vegetation}");
        _output.WriteLine($"Moisture            : {target.Moisture:F2}");
        _output.WriteLine($"Elevation           : {target.Elevation:F1}");

        _output.WriteLine("");
        _output.WriteLine("=== RESULTS ===");
        _output.WriteLine($"Spread rate         : {spreadRate:F6} m/s");
        _output.WriteLine($"Fire intensity      : {intensity:F3}");
        _output.WriteLine($"Heat flow           : {heat:F3} J");
        _output.WriteLine($"Ignition threshold  : {threshold:F3} J");
        _output.WriteLine($"Ignition probability: {probability:F6}");

        _output.WriteLine("");
        _output.WriteLine("=== BURN DURATIONS ===");

        foreach (var vegetation in Enum.GetValues<VegetationType>())
        {
            if (vegetation == VegetationType.Water ||
                vegetation == VegetationType.Bare)
                continue;

            var model = FireModelCatalog.Get(vegetation);

            _output.WriteLine(
                $"{vegetation,-12} -> " +
                $"{model.BaseBurnDurationSeconds / 3600.0:F2} h");
        }

        _output.WriteLine("");
        _output.WriteLine("==================================================");
    }
}