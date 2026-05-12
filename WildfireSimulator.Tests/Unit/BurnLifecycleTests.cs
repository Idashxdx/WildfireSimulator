using System;
using System.Collections.Generic;
using System.Linq;
using WildfireSimulator.Application.Services;
using WildfireSimulator.Domain.Models;
using Xunit;
using Xunit.Abstractions;

namespace WildfireSimulator.Tests.Unit;

public class BurnLifecycleTests
{
    private readonly ITestOutputHelper _output;
    private readonly FireSpreadCalculator _calculator;

    public BurnLifecycleTests(ITestOutputHelper output)
    {
        _output = output;
        _calculator = new FireSpreadCalculator();
    }

    [Fact]
    public void NewCell_StartsAsNormalWithFullFuel()
    {
        var cell = new ForestCell(0, 0, VegetationType.Grass, 0.30, 0);

        Assert.Equal(CellState.Normal, cell.State);
        Assert.Equal(FireStage.Unburned, cell.FireStage);
        Assert.True(cell.FuelLoad > 0.0);
        Assert.Equal(cell.FuelLoad, cell.CurrentFuelLoad);
        Assert.Equal(0.0, cell.BurningElapsedSeconds);
        Assert.Equal(0.0, cell.AccumulatedHeatJ);
    }

    [Fact]
    public void Ignite_SetsBurningStateAndIgnitionStage()
    {
        var cell = new ForestCell(0, 0, VegetationType.Grass, 0.30, 0);

        cell.Ignite(DateTime.UtcNow);

        Assert.Equal(CellState.Burning, cell.State);
        Assert.Equal(FireStage.Ignition, cell.FireStage);
        Assert.NotNull(cell.IgnitionTime);
        Assert.True(cell.FireIntensity >= 0.0);
    }

    [Fact]
    public void WaterAndBare_CannotBeIgnited()
    {
        var water = new ForestCell(0, 0, VegetationType.Water, 1.0, 0);
        var bare = new ForestCell(0, 0, VegetationType.Bare, 0.0, 0);

        water.Ignite(DateTime.UtcNow);
        bare.Ignite(DateTime.UtcNow);

        Assert.Equal(CellState.Normal, water.State);
        Assert.Equal(CellState.Normal, bare.State);
        Assert.Equal(FireStage.Unburned, water.FireStage);
        Assert.Equal(FireStage.Unburned, bare.FireStage);
    }

    [Fact]
    public void BurningCell_ProgressesThroughExpectedFireStages()
    {
        var cell = new ForestCell(0, 0, VegetationType.Grass, 0.20, 0);
        var burnDuration = FireModelCatalog.Get(cell.Vegetation).BaseBurnDurationSeconds;

        cell.Ignite(DateTime.UtcNow);

        cell.SetBurningElapsedSeconds(burnDuration * 0.10);
        cell.UpdateBurn(TimeSpan.FromSeconds(1), windEffect: 1.0, slopeEffect: 1.0);
        Assert.Equal(FireStage.Ignition, cell.FireStage);

        cell.SetBurningElapsedSeconds(burnDuration * 0.20);
        cell.UpdateBurn(TimeSpan.FromSeconds(1), windEffect: 1.0, slopeEffect: 1.0);
        Assert.Equal(FireStage.Active, cell.FireStage);

        cell.SetBurningElapsedSeconds(burnDuration * 0.50);
        cell.UpdateBurn(TimeSpan.FromSeconds(1), windEffect: 1.0, slopeEffect: 1.0);
        Assert.Equal(FireStage.Intense, cell.FireStage);

        cell.SetBurningElapsedSeconds(burnDuration * 0.85);
        cell.UpdateBurn(TimeSpan.FromSeconds(1), windEffect: 1.0, slopeEffect: 1.0);
        Assert.Equal(FireStage.Smoldering, cell.FireStage);
    }

    [Fact]
    public void BurningCell_LosesFuelOverTime()
    {
        var cell = new ForestCell(0, 0, VegetationType.Grass, 0.20, 0);
        cell.Ignite(DateTime.UtcNow);

        var initialFuel = cell.CurrentFuelLoad;

        cell.UpdateBurn(TimeSpan.FromSeconds(600), windEffect: 1.0, slopeEffect: 1.0);

        _output.WriteLine($"initial fuel={initialFuel:F6}");
        _output.WriteLine($"current fuel={cell.CurrentFuelLoad:F6}");

        Assert.True(cell.CurrentFuelLoad < initialFuel);
        Assert.True(cell.CurrentFuelLoad >= 0.0);
    }

    [Fact]
    public void BurningCell_BurnsOutAfterExpectedDuration()
    {
        var cell = new ForestCell(0, 0, VegetationType.Grass, 0.20, 0);
        var burnDuration = FireModelCatalog.Get(cell.Vegetation).BaseBurnDurationSeconds;

        cell.Ignite(DateTime.UtcNow);

        var step = TimeSpan.FromSeconds(300);
        var maxIterations = (int)Math.Ceiling(burnDuration / step.TotalSeconds) + 5;

        for (var i = 0; i < maxIterations && cell.State == CellState.Burning; i++)
            cell.UpdateBurn(step, windEffect: 1.0, slopeEffect: 1.0);

        _output.WriteLine($"burn duration model={burnDuration:F0}s");
        _output.WriteLine($"elapsed={cell.BurningElapsedSeconds:F0}s");
        _output.WriteLine($"state={cell.State}");
        _output.WriteLine($"stage={cell.FireStage}");

        Assert.Equal(CellState.Burned, cell.State);
        Assert.Equal(FireStage.BurnedOut, cell.FireStage);
        Assert.NotNull(cell.BurnoutTime);
        Assert.Equal(0.0, cell.CurrentFuelLoad);
    }

    [Fact]
    public void BurnedCell_CannotIgniteAgain()
    {
        var cell = new ForestCell(0, 0, VegetationType.Grass, 0.20, 0);
        var burnDuration = FireModelCatalog.Get(cell.Vegetation).BaseBurnDurationSeconds;

        cell.Ignite(DateTime.UtcNow);
        cell.UpdateBurn(TimeSpan.FromSeconds(burnDuration + 1), windEffect: 1.0, slopeEffect: 1.0);

        Assert.Equal(CellState.Burned, cell.State);

        cell.Ignite(DateTime.UtcNow);

        Assert.Equal(CellState.Burned, cell.State);
        Assert.Equal(FireStage.BurnedOut, cell.FireStage);
    }

    [Fact]
    public void FireIntensity_HasPeakInsideBurningPeriod()
    {
        var vegetation = VegetationType.Coniferous;
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);
        var burnDuration = FireModelCatalog.Get(vegetation).BaseBurnDurationSeconds;

        var checkpoints = new[]
        {
            0.05,
            0.20,
            0.40,
            0.60,
            0.80,
            0.95
        };

        var intensities = new List<double>();

        foreach (var progress in checkpoints)
        {
            var source = CreateSource(
                10,
                10,
                vegetation,
                0.15,
                50,
                (int)(burnDuration * progress));

            var intensity = _calculator.CalculateFireIntensity(source, weather);
            intensities.Add(intensity);

            _output.WriteLine($"progress={progress:F2}, intensity={intensity:F6}");
        }

        var peakIndex = intensities.IndexOf(intensities.Max());

        Assert.True(peakIndex > 0);
        Assert.True(peakIndex < intensities.Count - 1);
        Assert.True(intensities.Max() > intensities.First());
        Assert.True(intensities.Max() > intensities.Last());
    }

    [Fact]
    public void BurnDurations_AreReasonableForOneHectareModelCell()
    {
        var grass = FireModelCatalog.Get(VegetationType.Grass).BaseBurnDurationSeconds;
        var shrub = FireModelCatalog.Get(VegetationType.Shrub).BaseBurnDurationSeconds;
        var coniferous = FireModelCatalog.Get(VegetationType.Coniferous).BaseBurnDurationSeconds;
        var mixed = FireModelCatalog.Get(VegetationType.Mixed).BaseBurnDurationSeconds;
        var deciduous = FireModelCatalog.Get(VegetationType.Deciduous).BaseBurnDurationSeconds;

        _output.WriteLine($"Grass      = {grass / 3600.0:F2} h");
        _output.WriteLine($"Shrub      = {shrub / 3600.0:F2} h");
        _output.WriteLine($"Coniferous = {coniferous / 3600.0:F2} h");
        _output.WriteLine($"Mixed      = {mixed / 3600.0:F2} h");
        _output.WriteLine($"Deciduous  = {deciduous / 3600.0:F2} h");

        Assert.InRange(grass, 3600.0, 7200.0);
        Assert.InRange(shrub, 5400.0, 9000.0);
        Assert.InRange(coniferous, 9000.0, 14400.0);
        Assert.InRange(mixed, 9000.0, 14400.0);
        Assert.InRange(deciduous, 9000.0, 14400.0);

        Assert.True(grass < shrub);
        Assert.True(shrub < deciduous);
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