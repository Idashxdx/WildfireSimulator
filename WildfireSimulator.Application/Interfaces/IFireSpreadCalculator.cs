using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Interfaces
{
    public interface IFireSpreadCalculator
    {
        double CalculateHeatFlow(
            ForestCell source,
            ForestCell target,
            WeatherCondition weather,
            double stepDurationSeconds);

        double CalculateFireIntensity(ForestCell cell, WeatherCondition weather);

        double CalculateSpreadRate(ForestCell cell, WeatherCondition weather);

        double CalculateIgnitionThreshold(ForestCell target, WeatherCondition weather);

        double CalculateIgnitionProbability(double totalHeat, double threshold);

        bool ShouldIgnite(double probability);

        void UpdateBurningCell(ForestCell cell, WeatherCondition weather, double stepDurationSeconds);

        (double Intensity, double BurningTime, double DistanceFactor, double WindFactor, double SlopeFactor, double TotalHeat)
            GetHeatFlowDebugInfo(
                ForestCell source,
                ForestCell target,
                WeatherCondition weather,
                double stepDurationSeconds);
    }
}