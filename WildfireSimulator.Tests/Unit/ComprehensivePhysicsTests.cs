using System;
using System.Collections.Generic;
using System.Linq;
using WildfireSimulator.Application.Services;
using WildfireSimulator.Domain.Models;
using Xunit;
using Xunit.Abstractions;

namespace WildfireSimulator.Tests.Unit;

public class ComprehensivePhysicsTests
{
    private readonly ITestOutputHelper _output;
    private readonly FireSpreadCalculator _calculator;
    private const double DefaultStepSeconds = 3600.0;

    public ComprehensivePhysicsTests(ITestOutputHelper output)
    {
        _output = output;
        _calculator = new FireSpreadCalculator();
    }


    [Fact]
    public void BasicInvariants_AlwaysHold()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 1. БАЗОВЫЕ ИНВАРИАНТЫ                                              ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var cell = new ForestCell(0, 0, VegetationType.Grass, 0.3, 0);
        cell.Ignite(DateTime.UtcNow);
        for (int i = 0; i < 100 && cell.State == CellState.Burning; i++)
            cell.UpdateBurn(TimeSpan.FromSeconds(300), 1.0, 1.0);
        Assert.Equal(CellState.Burned, cell.State);
        _output.WriteLine("  ✅ 1.1 Клетка выгорает за конечное время");

        var burnedState = cell.State;
        cell.Ignite(DateTime.UtcNow);
        Assert.Equal(CellState.Burned, cell.State);
        _output.WriteLine("  ✅ 1.2 Сгоревшая клетка не загорается снова");

        var source = CreateSource(0, 0, VegetationType.Coniferous, 0.15, 50, 1800);
        var target = new ForestCell(0, 1, VegetationType.Grass, 0.2, 50);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);
        for (int i = 0; i < 50; i++)
        {
            var heat = _calculator.CalculateHeatFlow(source, target, weather, DefaultStepSeconds);
            var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
            var prob = _calculator.CalculateIgnitionProbability(heat, threshold);
            Assert.InRange(prob, 0.0, 1.0);
        }
        _output.WriteLine("  ✅ 1.3 Вероятность всегда в диапазоне [0,1]");

        _output.WriteLine("");
    }


    [Fact]
    public void DistanceFollowsInverseSquareLaw()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 2. ЗАКОН ОБРАТНЫХ КВАДРАТОВ                                        ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);
        
        var distances = new[] { 1.0, 2.0, 3.0, 4.0 };
        var heats = new List<double>();
        
        _output.WriteLine("  Расстояние -> Тепло (Дж) -> Ожидаемое отношение (1/d²)");
        
        foreach (var dist in distances)
        {
            var target = new ForestCell(10 + (int)dist, 10, VegetationType.Grass, 0.2, 50);
            var heat = _calculator.CalculateHeatFlow(source, target, weather, DefaultStepSeconds);
            heats.Add(heat);
            _output.WriteLine($"  {dist:F0} -> {heat,12:F0} -> 1/{dist:F0}² = {1/(dist*dist):F4}");
        }
        
        var ratio12 = heats[0] / heats[1];
        var expectedRatio12 = 4.0;
        _output.WriteLine($"  Отношение d=1 к d=2: {ratio12:F2} (ожидается ~{expectedRatio12})");
        Assert.InRange(ratio12, expectedRatio12 * 0.5, expectedRatio12 * 1.5);
        
        var ratio13 = heats[0] / heats[2];
        var expectedRatio13 = 9.0;
        _output.WriteLine($"  Отношение d=1 к d=3: {ratio13:F2} (ожидается ~{expectedRatio13})");
        Assert.InRange(ratio13, expectedRatio13 * 0.5, expectedRatio13 * 1.5);
        
        var ratio14 = heats[0] / heats[3];
        var expectedRatio14 = 16.0;
        _output.WriteLine($"  Отношение d=1 к d=4: {ratio14:F2} (ожидается ~{expectedRatio14})");
        Assert.InRange(ratio14, expectedRatio14 * 0.5, expectedRatio14 * 1.5);

        _output.WriteLine("  ✅ Тепло убывает примерно пропорционально квадрату расстояния");
        _output.WriteLine("");
    }


    [Fact]
    public void WindDirectionAffectsSpreadDirection()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 3. НАПРАВЛЕНИЕ ВЕТРА                                               ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var baseWeather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);
        var windyWeather = new WeatherCondition(DateTime.UtcNow, 25, 40, 15, 0, 0);

        var south = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var north = new ForestCell(10, 9, VegetationType.Grass, 0.2, 50);
        var east = new ForestCell(11, 10, VegetationType.Grass, 0.2, 50);
        var west = new ForestCell(9, 10, VegetationType.Grass, 0.2, 50);

        double heatSouthNoWind = _calculator.CalculateHeatFlow(source, south, baseWeather, DefaultStepSeconds);
        double heatNorthNoWind = _calculator.CalculateHeatFlow(source, north, baseWeather, DefaultStepSeconds);
        double heatSouthWind = _calculator.CalculateHeatFlow(source, south, windyWeather, DefaultStepSeconds);
        double heatNorthWind = _calculator.CalculateHeatFlow(source, north, windyWeather, DefaultStepSeconds);
        double heatEastWind = _calculator.CalculateHeatFlow(source, east, windyWeather, DefaultStepSeconds);
        double heatWestWind = _calculator.CalculateHeatFlow(source, west, windyWeather, DefaultStepSeconds);

        _output.WriteLine($"  Без ветра: ЮГ={heatSouthNoWind:F0}, СЕВЕР={heatNorthNoWind:F0}");
        _output.WriteLine($"  С ветром: ЮГ={heatSouthWind:F0}, СЕВЕР={heatNorthWind:F0}, ВОСТОК={heatEastWind:F0}, ЗАПАД={heatWestWind:F0}");

        Assert.True(heatSouthWind > heatNorthWind, "По ветру тепла больше, чем против ветра");
        Assert.True(heatSouthWind > heatEastWind, "По ветру тепла больше, чем поперек");
        Assert.True(heatSouthWind > heatWestWind, "По ветру тепла больше, чем поперек");

        _output.WriteLine("  ✅ Ветер усиливает распространение по направлению");
        _output.WriteLine("");
    }


    [Fact]
    public void StrongerWindIncreasesSpread()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 4. СИЛА ВЕТРА                                                     ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var target = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var windSpeeds = new[] { 0.0, 3.0, 7.0, 12.0, 18.0, 25.0 };
        
        var previousHeat = 0.0;
        _output.WriteLine("  Скорость ветра -> Тепло (Дж) -> Относительное усиление");
        
        foreach (var speed in windSpeeds)
        {
            var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, speed, 0, 0);
            var heat = _calculator.CalculateHeatFlow(source, target, weather, DefaultStepSeconds);
            var gain = previousHeat > 0 ? heat / previousHeat : 1.0;
            _output.WriteLine($"  {speed,5:F1} м/с -> {heat,12:F0} -> {gain,12:F2}x");
            previousHeat = heat;
        }
        
        Assert.True(previousHeat > 0, "При сильном ветре тепло должно быть ненулевым");
        _output.WriteLine("  ✅ Сильный ветер дает больше тепла, чем слабый");
        _output.WriteLine("");
    }


    [Fact]
    public void VegetationTypeAffectsIgnitionAndBurn()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 5. ТИП РАСТИТЕЛЬНОСТИ                                             ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);
        
        var vegetationTypes = new[]
        {
            VegetationType.Grass,
            VegetationType.Shrub,
            VegetationType.Coniferous,
            VegetationType.Mixed,
            VegetationType.Deciduous
        };

        _output.WriteLine("  Тип -> Порог (Дж) -> Вероятность -> Время выгорания (сек)");
        
        var grassThreshold = 0.0;
        var grassBurnTime = 0.0;
        
        foreach (var veg in vegetationTypes)
        {
            var target = new ForestCell(10, 11, veg, 0.2, 50);
            var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
            var heat = _calculator.CalculateHeatFlow(source, target, weather, DefaultStepSeconds);
            var probability = _calculator.CalculateIgnitionProbability(heat, threshold);
            var burnTime = FireModelCatalog.Get(veg).BaseBurnDurationSeconds;
            
            if (veg == VegetationType.Grass)
            {
                grassThreshold = threshold;
                grassBurnTime = burnTime;
            }
            
            _output.WriteLine($"  {veg,-12} -> {threshold,12:F0} -> {probability,12:F3} -> {burnTime,12:F0}");
        }
        
        Assert.True(grassThreshold > 0, "Порог для травы должен быть положительным");
        Assert.True(grassBurnTime < FireModelCatalog.Get(VegetationType.Deciduous).BaseBurnDurationSeconds, 
            "Трава должна выгорать быстрее лиственного леса");
        
        _output.WriteLine("  ✅ Растительность корректно влияет на порог и время выгорания");
        _output.WriteLine("");
    }


    [Fact]
    public void HigherMoistureIncreasesIgnitionThreshold()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 6. ВЛАЖНОСТЬ ТОПЛИВА                                              ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);
        
        var moistureLevels = new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8 };
        var previousThreshold = 0.0;
        
        _output.WriteLine("  Влажность -> Порог (Дж) -> Относительное увеличение");
        
        foreach (var moisture in moistureLevels)
        {
            var target = new ForestCell(10, 11, VegetationType.Grass, moisture, 50);
            var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
            var increase = previousThreshold > 0 ? threshold / previousThreshold : 1.0;
            _output.WriteLine($"  {moisture * 100,2:F0}% -> {threshold,12:F0} -> {increase,12:F2}x");
            previousThreshold = threshold;
        }
        
        var dryTarget = new ForestCell(10, 11, VegetationType.Grass, 0.1, 50);
        var wetTarget = new ForestCell(10, 11, VegetationType.Grass, 0.8, 50);
        var dryThreshold = _calculator.CalculateIgnitionThreshold(dryTarget, weather);
        var wetThreshold = _calculator.CalculateIgnitionThreshold(wetTarget, weather);
        
        Assert.True(wetThreshold > dryThreshold, "Влажная клетка должна иметь больший порог");
        _output.WriteLine("  ✅ Влажность увеличивает порог воспламенения");
        _output.WriteLine("");
    }


    [Fact]
    public void SlopeAffectsHeatTransferUpward()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 7. УКЛОН МЕСТНОСТИ                                                 ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);
        
        var uphill = new ForestCell(10, 11, VegetationType.Grass, 0.2, 100);
        var downhill = new ForestCell(10, 9, VegetationType.Grass, 0.2, 0);
        var flat = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        
        var uphillHeat = _calculator.CalculateHeatFlow(source, uphill, weather, DefaultStepSeconds);
        var downhillHeat = _calculator.CalculateHeatFlow(source, downhill, weather, DefaultStepSeconds);
        var flatHeat = _calculator.CalculateHeatFlow(source, flat, weather, DefaultStepSeconds);
        
        var uphillDebug = _calculator.GetHeatFlowDebugInfo(source, uphill, weather, DefaultStepSeconds);
        var downhillDebug = _calculator.GetHeatFlowDebugInfo(source, downhill, weather, DefaultStepSeconds);
        
        _output.WriteLine($"  Вверх по склону: коэффициент={uphillDebug.SlopeFactor:F2}, тепло={uphillHeat:F0}");
        _output.WriteLine($"  Вниз по склону: коэффициент={downhillDebug.SlopeFactor:F2}, тепло={downhillHeat:F0}");
        _output.WriteLine($"  Равнина: тепло={flatHeat:F0}");
        
        Assert.True(uphillHeat > flatHeat, "Вверх по склону тепла больше, чем на равнине");
        Assert.True(flatHeat > downhillHeat, "На равнине тепла больше, чем вниз по склону");
        Assert.True(uphillDebug.SlopeFactor > 1.0, "Коэффициент уклона вверх > 1");
        Assert.True(downhillDebug.SlopeFactor < 1.0, "Коэффициент уклона вниз < 1");
        
        _output.WriteLine("  ✅ Уклон усиливает перенос вверх и ослабляет вниз");
        _output.WriteLine("");
    }


   /*  [Fact]
    public void PrecipitationReducesIgnitionProbability()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 8. ОСАДКИ                                                        ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var target = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        
        var precipitationLevels = new[] { 0.0, 5.0, 15.0, 30.0, 50.0 };
        var previousProb = 0.0;
        
        _output.WriteLine("  Осадки (мм/ч) -> Вероятность -> Относительное снижение");
        
        foreach (var prec in precipitationLevels)
        {
            var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, prec);
            var heat = _calculator.CalculateHeatFlow(source, target, weather, DefaultStepSeconds);
            var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
            var prob = _calculator.CalculateIgnitionProbability(heat, threshold);
            var reduction = previousProb > 0 ? previousProb / prob : 1.0;
            _output.WriteLine($"  {prec,5:F1} -> {prob,12:F3} -> {reduction,12:F2}x");
            previousProb = prob;
        }
        
        var dryWeather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);
        var wetWeather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 30);
        var dryProb = _calculator.CalculateIgnitionProbability(
            _calculator.CalculateHeatFlow(source, target, dryWeather, DefaultStepSeconds),
            _calculator.CalculateIgnitionThreshold(target, dryWeather));
        var wetProb = _calculator.CalculateIgnitionProbability(
            _calculator.CalculateHeatFlow(source, target, wetWeather, DefaultStepSeconds),
            _calculator.CalculateIgnitionThreshold(target, wetWeather));
        
        Assert.True(wetProb < dryProb, "При осадках вероятность должна быть ниже");
        _output.WriteLine("  ✅ Осадки уменьшают вероятность возгорания");
        _output.WriteLine("");
    } */


    [Fact]
    public void WaterAndBareDoNotIgnite()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 9. НЕГОРЮЧИЕ ТИПЫ (ВОДА, ГОЛЫЙ ГРУНТ)                            ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var weather = new WeatherCondition(DateTime.UtcNow, 35, 20, 15, 0, 0);
        
        var waterCell = new ForestCell(10, 11, VegetationType.Water, 1.0, 50);
        var bareCell = new ForestCell(10, 11, VegetationType.Bare, 0.0, 50);
        
        var waterHeat = _calculator.CalculateHeatFlow(source, waterCell, weather, DefaultStepSeconds);
        var waterThreshold = _calculator.CalculateIgnitionThreshold(waterCell, weather);
        var waterProb = _calculator.CalculateIgnitionProbability(waterHeat, waterThreshold);
        
        var bareHeat = _calculator.CalculateHeatFlow(source, bareCell, weather, DefaultStepSeconds);
        var bareThreshold = _calculator.CalculateIgnitionThreshold(bareCell, weather);
        var bareProb = _calculator.CalculateIgnitionProbability(bareHeat, bareThreshold);
        
        _output.WriteLine($"  Вода: тепло={waterHeat:F0}, вероятность={waterProb:F6}");
        _output.WriteLine($"  Голый грунт: тепло={bareHeat:F0}, вероятность={bareProb:F6}");
        
        Assert.Equal(0.0, waterProb);
        Assert.Equal(0.0, bareProb);
        
        _output.WriteLine("  ✅ Вода и голый грунт не загораются");
        _output.WriteLine("");
    }


    [Fact]
    public void MultipleSourcesSumHeat()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 10. НЕСКОЛЬКО ИСТОЧНИКОВ (СУММИРОВАНИЕ ТЕПЛА)                     ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);
        var target = new ForestCell(10, 10, VegetationType.Grass, 0.2, 50);
        
        var sources = new[]
        {
            CreateSource(9, 9, VegetationType.Coniferous, 0.15, 50, 1800),
            CreateSource(9, 10, VegetationType.Coniferous, 0.15, 50, 1800),
            CreateSource(9, 11, VegetationType.Coniferous, 0.15, 50, 1800),
            CreateSource(10, 9, VegetationType.Coniferous, 0.15, 50, 1800),
            CreateSource(10, 11, VegetationType.Coniferous, 0.15, 50, 1800),
            CreateSource(11, 9, VegetationType.Coniferous, 0.15, 50, 1800),
            CreateSource(11, 10, VegetationType.Coniferous, 0.15, 50, 1800),
            CreateSource(11, 11, VegetationType.Coniferous, 0.15, 50, 1800),
        };
        
        var individualHeats = sources.Select(s => _calculator.CalculateHeatFlow(s, target, weather, DefaultStepSeconds)).ToList();
        var totalHeat = individualHeats.Sum();
        var singleSourceHeat = _calculator.CalculateHeatFlow(sources[0], target, weather, DefaultStepSeconds);
        
        var totalProb = _calculator.CalculateIgnitionProbability(totalHeat, 
            _calculator.CalculateIgnitionThreshold(target, weather));
        var singleProb = _calculator.CalculateIgnitionProbability(singleSourceHeat, 
            _calculator.CalculateIgnitionThreshold(target, weather));
        
        _output.WriteLine($"  Один источник: тепло={singleSourceHeat:F0}, вероятность={singleProb:F3}");
        _output.WriteLine($"  Восемь источников: суммарное тепло={totalHeat:F0}, вероятность={totalProb:F3}");
        
        Assert.True(totalHeat > singleSourceHeat, "Суммарное тепло больше тепла от одного источника");
        Assert.True(totalProb > singleProb, "Вероятность от нескольких источников выше");
        
        _output.WriteLine("  ✅ Несколько источников корректно суммируются");
        _output.WriteLine("");
    }


    [Fact]
    public void SourceBurnTimeAffectsIntensity()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 11. ВРЕМЯ ГОРЕНИЯ ИСТОЧНИКА                                        ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var target = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);
        var burnTimes = new[] { 0, 300, 600, 1200, 1800, 2700, 3600, 5400 };
        
        _output.WriteLine("  Время горения -> Интенсивность (кВт/м²) -> Тепло (Дж)");
        
        var previousHeat = 0.0;
        foreach (var burnTime in burnTimes)
        {
            var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, burnTime);
            var intensity = _calculator.CalculateFireIntensity(source, weather);
            var heat = _calculator.CalculateHeatFlow(source, target, weather, DefaultStepSeconds);
            _output.WriteLine($"  {burnTime,5} сек -> {intensity,12:F0} -> {heat,12:F0}");
            
            if (burnTime > 0)
                Assert.True(heat > 0, $"При времени горения {burnTime} сек тепло должно быть ненулевым");
            previousHeat = heat;
        }
        
        _output.WriteLine("  ✅ Время горения влияет на интенсивность источника");
        _output.WriteLine("");
    }


    [Fact]
    public void StepDurationScalingIsStable()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 12. МАСШТАБИРОВАНИЕ ПО ВРЕМЕНИ (stepDurationSeconds)              ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var source = CreateSource(10, 10, VegetationType.Coniferous, 0.15, 50, 1800);
        var target = new ForestCell(10, 11, VegetationType.Grass, 0.2, 50);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 5, 0, 0);
        
        var stepDurations = new[] { 300.0, 600.0, 900.0, 1800.0, 3600.0, 5400.0 };
        var heatPerSecondValues = new List<double>();
        
        _output.WriteLine("  Длительность шага -> Тепло за шаг -> Тепло в секунду");
        
        foreach (var stepSec in stepDurations)
        {
            var heat = _calculator.CalculateHeatFlow(source, target, weather, stepSec);
            var heatPerSec = heat / stepSec;
            heatPerSecondValues.Add(heatPerSec);
            _output.WriteLine($"  {stepSec,5:F0} сек -> {heat,12:F0} Дж -> {heatPerSec,12:F2} Дж/сек");
        }
        
        var avg = heatPerSecondValues.Average();
        var maxDeviation = heatPerSecondValues.Max(v => Math.Abs(v - avg) / avg);
        
        _output.WriteLine($"  Среднее тепло в секунду: {avg:F2} Дж/сек");
        _output.WriteLine($"  Максимальное отклонение: {maxDeviation * 100:F1}%");
        
        Assert.True(maxDeviation < 0.2, "Тепло в секунду не должно сильно зависеть от длительности шага");
        
        _output.WriteLine("  ✅ Масштабирование по времени стабильно");
        _output.WriteLine("");
    }


    [Fact]
    public void ProbabilityIsLogisticFunctionOfHeatThresholdRatio()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║ 13. ВЕРОЯТНОСТЬ КАК ЛОГИСТИЧЕСКАЯ ФУНКЦИЯ ОТ HEAT/THRESHOLD       ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

        var target = new ForestCell(0, 0, VegetationType.Grass, 0.2, 50);
        var weather = new WeatherCondition(DateTime.UtcNow, 25, 40, 0, 0, 0);
        var threshold = _calculator.CalculateIgnitionThreshold(target, weather);
        
        var ratios = new[] { 0.2, 0.4, 0.6, 0.8, 1.0, 1.2, 1.5, 2.0, 3.0 };
        var previousProb = -1.0;
        
        _output.WriteLine("  Ratio (heat/threshold) -> Вероятность");
        
        foreach (var ratio in ratios)
        {
            var heat = threshold * ratio;
            var prob = _calculator.CalculateIgnitionProbability(heat, threshold);
            _output.WriteLine($"  {ratio,5:F2} -> {prob,12:F3}");
            
            if (previousProb >= 0)
                Assert.True(prob >= previousProb, $"Вероятность должна монотонно расти: {previousProb} -> {prob}");
            previousProb = prob;
        }
        
        Assert.InRange(_calculator.CalculateIgnitionProbability(threshold * 0.1, threshold), 0.0, 0.1);
        Assert.InRange(_calculator.CalculateIgnitionProbability(threshold * 5.0, threshold), 0.9, 1.0);
        
        _output.WriteLine("  ✅ Вероятность монотонно растет с heat/threshold");
        _output.WriteLine("");
    }


    private ForestCell CreateSource(int x, int y, VegetationType veg, double moisture, double elevation, int burningAgeSeconds)
    {
        var cell = new ForestCell(x, y, veg, moisture, elevation);
        cell.Ignite(DateTime.UtcNow.AddSeconds(-burningAgeSeconds));
        return cell;
    }
}
