using WildfireSimulator.Application.Interfaces;
using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Services
{
    public class FireSpreadCalculator : IFireSpreadCalculator
    {
        private readonly Random _random = new();

        private const double EffectiveHeatingAreaM2 = 160.0;
        private const double HeatTransferEfficiency = 0.00050;
        private const double MinWindFactor = 0.25;
        private const double MaxWindFactor = 4.5;
        private const double LogisticSteepness = 5.0;

        public double CalculateHeatFlow(
      ForestCell source,
      ForestCell target,
      WeatherCondition weather,
      double stepDurationSeconds)
        {
            if (source.State != CellState.Burning || target.State != CellState.Normal)
                return 0.0;

            if (target.Vegetation == VegetationType.Water || target.Vegetation == VegetationType.Bare)
                return 0.0;

            double intensityKwPerM2 = CalculateFireIntensity(source, weather);
            if (intensityKwPerM2 <= 0.0)
                return 0.0;

            double burningProgress = GetBurningProgress(source);
            double burningStageFactor = 0.70 + 0.65 * Math.Sin(Math.PI * burningProgress);
            burningStageFactor = Math.Clamp(burningStageFactor, 0.60, 1.35);

            double distance = CalculateDistance(source.X, source.Y, target.X, target.Y);
            if (distance < 0.001)
                distance = 0.001;

            double distanceFactor = 1.0 / (distance * distance);
            double windFactor = CalculateWindFactor(source, target, weather);
            double slopeFactor = CalculateSlopeFactor(source, target);

            double effectiveExposureSeconds = Math.Max(stepDurationSeconds, 1.0);

            double precipitationFactor = 1.0 / (1.0 + weather.Precipitation * 0.35);
            precipitationFactor = Math.Clamp(precipitationFactor, 0.30, 1.0);

            double heatFlow =
                intensityKwPerM2 * 1000.0 *
                EffectiveHeatingAreaM2 *
                effectiveExposureSeconds *
                burningStageFactor *
                distanceFactor *
                windFactor *
                slopeFactor *
                precipitationFactor *
                HeatTransferEfficiency;

            return Math.Max(0.0, Math.Min(heatFlow, 1e10));
        }
        public double CalculateFireIntensity(ForestCell cell, WeatherCondition weather)
        {
            if (cell.State != CellState.Burning)
                return 0.0;

            var parameters = FireModelCatalog.Get(cell.Vegetation);

            double heatOfCombustion = parameters.HeatOfCombustion;
            double fuelLoad = parameters.FuelLoadKgPerM2;
            double spreadRate = CalculateSpreadRate(cell, weather);
            double progress = GetBurningProgress(cell);

            double intensityFactor = Math.Sin(progress * Math.PI);
            intensityFactor = Math.Max(0.35, intensityFactor);

            double durationFactor = 5400.0 / Math.Max(5400.0, parameters.BaseBurnDurationSeconds);
            durationFactor = Math.Clamp(durationFactor, 0.45, 1.0);

            double intensityKwPerM2 = heatOfCombustion * fuelLoad * spreadRate * intensityFactor * durationFactor;
            return Math.Min(intensityKwPerM2, 3500.0);
        }

        public double CalculateSpreadRate(ForestCell cell, WeatherCondition weather)
        {
            var parameters = FireModelCatalog.Get(cell.Vegetation);

            double baseRate = parameters.BaseSpreadRateMps;

            double windEffect = 1.0 + weather.WindSpeedMps * 0.07;
            windEffect = Math.Clamp(windEffect, 0.7, 2.2);

            double moistureEffect = 1.0 - cell.Moisture * 0.5;
            moistureEffect = Math.Max(moistureEffect, 0.3);

            double progress = GetBurningProgress(cell);
            double progressEffect = Math.Sin(progress * Math.PI);

            progressEffect = Math.Max(0.60, progressEffect);

            double rate = baseRate * windEffect * moistureEffect * progressEffect;
            return Math.Max(rate, 0.001);
        }

        public double CalculateIgnitionThreshold(ForestCell target, WeatherCondition weather)
        {
            if (target.Vegetation == VegetationType.Water || target.Vegetation == VegetationType.Bare)
                return double.MaxValue;

            var parameters = FireModelCatalog.Get(target.Vegetation);

            double baseThreshold = parameters.BaseIgnitionThresholdJ;

            double fuelMoistureFactor = 1.0 + target.Moisture * 0.9;
            fuelMoistureFactor = Math.Clamp(fuelMoistureFactor, 1.0, 1.9);

            double temperatureFactor = 1.0;
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

            double airHumidityFactor = 1.0 + (weather.Humidity / 100.0) * 0.8;
            airHumidityFactor = Math.Clamp(airHumidityFactor, 1.0, 1.8);

            double precipitationFactor = 1.0 + weather.Precipitation * 0.45;
            precipitationFactor = Math.Clamp(precipitationFactor, 1.0, 3.0);

            return baseThreshold * fuelMoistureFactor * temperatureFactor * airHumidityFactor * precipitationFactor;
        }

        public double CalculateIgnitionProbability(double totalHeat, double threshold)
        {
            if (threshold <= 0.0 || totalHeat <= 0.0 || double.IsInfinity(threshold))
                return 0.0;

            double ratio = totalHeat / threshold;

            double probability = 1.0 / (1.0 + Math.Exp(-LogisticSteepness * (ratio - 1.0)));

            return Math.Clamp(probability, 0.0, 0.9995);
        }

        public bool ShouldIgnite(double probability)
        {
            if (probability >= 0.9995)
                return true;

            return _random.NextDouble() < probability;
        }

        public void UpdateBurningCell(ForestCell cell, WeatherCondition weather, double stepDurationSeconds)
        {
            if (cell.State != CellState.Burning)
                return;

            if (stepDurationSeconds <= 0.0)
                return;

            double windEffect = 1.0 + weather.WindSpeedMps * 0.02;
            windEffect = Math.Clamp(windEffect, 0.95, 1.20);

            cell.UpdateBurn(TimeSpan.FromSeconds(stepDurationSeconds), windEffect, 1.0);
        }

        public (double Intensity, double BurningTime, double DistanceFactor, double WindFactor, double SlopeFactor, double TotalHeat)
            GetHeatFlowDebugInfo(
                ForestCell source,
                ForestCell target,
                WeatherCondition weather,
                double stepDurationSeconds)
        {
            double intensity = CalculateFireIntensity(source, weather);

            double burningProgress = GetBurningProgress(source);
            double burningStageFactor = 0.70 + 0.65 * Math.Sin(Math.PI * burningProgress);
            burningStageFactor = Math.Clamp(burningStageFactor, 0.60, 1.35);

            double distance = CalculateDistance(source.X, source.Y, target.X, target.Y);
            if (distance < 0.001)
                distance = 0.001;

            double distanceFactor = 1.0 / (distance * distance);
            double windFactor = CalculateWindFactor(source, target, weather);
            double slopeFactor = CalculateSlopeFactor(source, target);

            double effectiveExposureSeconds = Math.Max(stepDurationSeconds, 1.0);

            double totalHeat =
                intensity * 1000.0 *
                EffectiveHeatingAreaM2 *
                effectiveExposureSeconds *
                burningStageFactor *
                distanceFactor *
                windFactor *
                slopeFactor *
                HeatTransferEfficiency;

            return (intensity, GetBurningTimeSeconds(source), distanceFactor, windFactor, slopeFactor, totalHeat);
        }

        private double GetBurningTimeSeconds(ForestCell cell)
        {
            return Math.Max(0.0, cell.BurningElapsedSeconds);
        }

        private double GetBurningProgress(ForestCell cell)
        {
            double burningTime = GetBurningTimeSeconds(cell);
            double totalBurnoutTime = FireModelCatalog.Get(cell.Vegetation).BaseBurnDurationSeconds;

            if (totalBurnoutTime <= 0.0 || double.IsInfinity(totalBurnoutTime))
                return 0.0;

            return Math.Min(1.0, burningTime / totalBurnoutTime);
        }

        private double CalculateDistance(int x1, int y1, int x2, int y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private double GetDirectionToTarget(ForestCell source, ForestCell target)
        {
            double dx = target.X - source.X;
            double dy = target.Y - source.Y;

            double angleRad = Math.Atan2(dy, dx);
            double angleDeg = angleRad * 180.0 / Math.PI;
            if (angleDeg < 0.0)
                angleDeg += 360.0;

            double correctedAngle = (90.0 + angleDeg) % 360.0;
            if (correctedAngle < 0.0)
                correctedAngle += 360.0;

            return correctedAngle;
        }

        private double CalculateWindFactor(ForestCell source, ForestCell target, WeatherCondition weather)
        {
            double windSpeed = weather.WindSpeedMps;
            if (windSpeed < 0.1)
                return 1.0;

            double spreadDirection = GetDirectionToTarget(source, target);
            double windToDirection = (weather.WindDirectionDegrees + 180.0) % 360.0;

            double angleDiff = Math.Abs(spreadDirection - windToDirection);
            angleDiff = Math.Min(angleDiff, 360.0 - angleDiff);

            double cosAngle = Math.Cos(angleDiff * Math.PI / 180.0);
            double normalizedWind = Math.Min(windSpeed / 20.0, 1.0);

            double windFactor = Math.Exp(1.2 * normalizedWind * cosAngle);
            return Math.Clamp(windFactor, MinWindFactor, MaxWindFactor);
        }

        private double CalculateSlopeFactor(ForestCell source, ForestCell target)
        {
            double distance = CalculateDistance(source.X, source.Y, target.X, target.Y);
            if (distance < 0.01)
                return 1.0;

            double elevationDelta = target.Elevation - source.Elevation;
            double slope = elevationDelta / distance;

            double normalizedSlope = slope / 20.0;

            double slopeFactor = 1.0 + normalizedSlope;

            return Math.Clamp(slopeFactor, 0.55, 1.80);
        }
    }
}