using WildfireSimulator.Application.Interfaces;
using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Services
{
    public class FireSpreadCalculator : IFireSpreadCalculator
    {
        private readonly Random _random = new();

        //эффективная активная зона теплопередачи.
        private const double EffectiveHeatingAreaM2 = 220.0;

        //коэффициент эффективности передачи тепла - какая доля накопленной тепловой энергии реально влияет на соседнюю клетку.
        private const double HeatTransferEfficiency = 0.00030;

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

            // Формула (4): B_i = clamp(0.70 + 0.65 * sin(pi * p_i), 0.60, 1.35)
            // Стадийный множитель учитывает изменение тепловыделения в течение горения.
            double burningProgress = GetBurningProgress(source);
            double burningStageFactor = 0.70 + 0.65 * Math.Sin(Math.PI * burningProgress);
            burningStageFactor = Math.Clamp(burningStageFactor, 0.60, 1.35);

            double distance = CalculateDistance(source.X, source.Y, target.X, target.Y);
            if (distance < 0.001)
                distance = 0.001;

            // Формула (8): D_ij = 1 / d_ij^2
            // Ослабление теплового воздействия с ростом расстояния.
            double distanceFactor = 1.0 / (distance * distance);

            double windFactor = CalculateWindFactor(source, target, weather);
            double slopeFactor = CalculateSlopeFactor(source, target);

            double effectiveExposureSeconds = Math.Max(stepDurationSeconds, 1.0);

            // Формула (7): Q_base_ij = I_i * K_unit * A_eff * Δt * D_ij * W_ij * S_ij * η
            // Базовый тепловой поток от горящего участка к целевому.
            // Осадки K_prec применяются отдельно в FireSpreadSimulator.
            double heatFlow =
                intensityKwPerM2 * 1000.0 *
                EffectiveHeatingAreaM2 *
                effectiveExposureSeconds *
                burningStageFactor *
                distanceFactor *
                windFactor *
                slopeFactor *
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

            // Формула (6): I_i = q_i * F_i * v_i * B_i * K_dur
            // Рассчитывает интенсивность горения участка по параметрам топлива,
            // скорости распространения, стадии горения и длительности горения.
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


            // Формула (5): v_i = v_0,i * K_v * K_M * K_p
            // Итоговая скорость распространения зависит от базовой скорости растительности,
            // ветра, влажности участка и стадии горения.
            double rate = baseRate * windEffect * moistureEffect * progressEffect;

            return Math.Max(rate, 0.001);
        }

        // Формула (22): Q_crit(j) = Q_crit,0(veg_j) * β_w(w_j) * β_T(T) * β_H(H)
        // Рассчитывает порог воспламенения участка с учетом типа растительности,
        // влажности топлива, температуры воздуха и влажности воздуха.
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

            // Итоговый порог воспламенения по формуле (22).
            return baseThreshold *
                   fuelMoistureFactor *
                   temperatureFactor *
                   airHumidityFactor;
        }

        // Формула (25): P_ignite = f(R), где R = Q_total / Q_crit
        // Рассчитывает базовую вероятность воспламенения по кусочно-линейной функции.
        // Чем больше накопленное тепло относительно порога, тем выше вероятность.
        public double CalculateIgnitionProbability(double totalHeat, double threshold)
        {
            if (threshold <= 0.0 || totalHeat <= 0.0 || double.IsInfinity(threshold))
                return 0.0;

            // Формула (23): R = Q_total / Q_crit
            // Отношение накопленного тепла к порогу воспламенения.
            double ratio = totalHeat / threshold;

            if (ratio < 0.03)
                return Math.Clamp(ratio / 0.03 * 0.04, 0.0, 0.04);

            if (ratio < 0.10)
                return Math.Clamp(0.04 + (ratio - 0.03) / 0.07 * 0.12, 0.04, 0.16);

            if (ratio < 0.25)
                return Math.Clamp(0.16 + (ratio - 0.10) / 0.15 * 0.24, 0.16, 0.40);

            if (ratio < 0.50)
                return Math.Clamp(0.40 + (ratio - 0.25) / 0.25 * 0.25, 0.40, 0.65);

            if (ratio < 0.75)
                return Math.Clamp(0.65 + (ratio - 0.50) / 0.25 * 0.17, 0.65, 0.82);

            if (ratio < 1.00)
                return Math.Clamp(0.82 + (ratio - 0.75) / 0.25 * 0.10, 0.82, 0.92);

            return 0.96;
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

            // Формула (26): применяем обновление горящего участка за длительность шага.
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

            double humidityHeatFactor = 1.0 - (weather.Humidity / 100.0) * 0.32;
            humidityHeatFactor = Math.Clamp(humidityHeatFactor, 0.62, 1.0);

            double effectiveExposureSeconds = Math.Max(stepDurationSeconds, 1.0);

            double ambientPrecipitationFactor = 1.0 / (1.0 + weather.Precipitation * 0.018);
            ambientPrecipitationFactor = Math.Clamp(ambientPrecipitationFactor, 0.78, 1.0);

            double gridRecoveryFactor = 1.08;

            double totalHeat =
                intensity * 1000.0 *
                EffectiveHeatingAreaM2 *
                effectiveExposureSeconds *
                burningStageFactor *
                distanceFactor *
                windFactor *
                slopeFactor *
                humidityHeatFactor *
                ambientPrecipitationFactor *
                HeatTransferEfficiency *
                gridRecoveryFactor;

            return (intensity, GetBurningTimeSeconds(source), distanceFactor, windFactor, slopeFactor, totalHeat);
        }

        private double GetBurningTimeSeconds(ForestCell cell)
        {
            return Math.Max(0.0, cell.BurningElapsedSeconds);
        }

        // Формула (3): p_i = t_burn,i / T_burn,i
        // Определяет относительный прогресс горения участка от 0 до 1.
        private double GetBurningProgress(ForestCell cell)
        {
            double burningTime = GetBurningTimeSeconds(cell);
            double totalBurnoutTime = FireModelCatalog.Get(cell.Vegetation).BaseBurnDurationSeconds;

            if (totalBurnoutTime <= 0.0 || double.IsInfinity(totalBurnoutTime))
                return 0.0;

            return Math.Min(1.0, burningTime / totalBurnoutTime);
        }

        // Формула (1): d_ij = sqrt((x_i - x_j)^2 + (y_i - y_j)^2)
        // Вычисляет евклидово расстояние между двумя участками модели по их координатам.
        private double CalculateDistance(int x1, int y1, int x2, int y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private double CalculateWindFactor(ForestCell source, ForestCell target, WeatherCondition weather)
        {
            double dx = target.X - source.X;
            double dy = target.Y - source.Y;

            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 0.0001 || weather.WindSpeedMps <= 0.0001)
                return 1.0;

            dx /= distance;
            dy /= distance;

            double windFlowDegrees = (weather.WindDirectionDegrees + 180.0) % 360.0;
            double radians = windFlowDegrees * Math.PI / 180.0;

            double windX = Math.Sin(radians);
            double windY = -Math.Cos(radians);

            double dot = dx * windX + dy * windY;
            dot = Math.Clamp(dot, -1.0, 1.0);

            double windStrength = Math.Clamp(weather.WindSpeedMps / 15.0, 0.0, 1.0);

            // Формула (9):W_ij = clamp(exp(k_w · v_w_norm · cos(θ_ij)) · C_dir, W_min, W_max) 
            // dot является косинусом угла между направлением к цели и направлением ветрового потока.
            // windStrength нормирует влияние скорости ветра.
            double factor = Math.Exp(dot * windStrength * 1.05);

            if (dot > 0.25)
                factor *= 1.0 + dot * 0.18;

            if (dot < -0.25)
                factor *= 1.0 + dot * 0.28;

            return Math.Clamp(factor, 0.32, 2.85);
        }
        private double CalculateSlopeFactor(ForestCell source, ForestCell target)
        {
            double distance = CalculateDistance(source.X, source.Y, target.X, target.Y);
            if (distance < 0.01)
                return 1.0;

            // Формула (2): s_ij = (h_j - h_i) / d_ij
            // Вычисляем уклон от источника огня к целевому участку.
            double elevationDelta = target.Elevation - source.Elevation;
            double slope = elevationDelta / distance;

            // Формулы (10)-(11):
            // normalizedSlope = s_ij / s_norm, где s_norm = 20
            // S_ij = clamp(1.0 + normalizedSlope, 0.55, 1.80)
            // Коэффициент рельефа усиливает распространение вверх по склону и ослабляет вниз.
            double normalizedSlope = slope / 20.0;
            double slopeFactor = 1.0 + normalizedSlope;
            return Math.Clamp(slopeFactor, 0.55, 1.80);
        }
    }
}