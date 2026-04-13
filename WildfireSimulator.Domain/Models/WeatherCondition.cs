using Ardalis.GuardClauses;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Domain.Models;

public class WindVector
{
    public double DirectionDegrees { get; }
    public double SpeedMps { get; }

    public double X => -SpeedMps * Math.Sin(DirectionDegrees * Math.PI / 180.0);
    public double Y => -SpeedMps * Math.Cos(DirectionDegrees * Math.PI / 180.0);

    public WindVector(double directionDegrees, double speedMps)
    {
        DirectionDegrees = directionDegrees % 360;
        SpeedMps = speedMps;
    }

    public double GetAngleTo(WindVector other)
    {
        var diff = Math.Abs(DirectionDegrees - other.DirectionDegrees);
        return Math.Min(diff, 360 - diff);
    }

    public double GetProjectionOnDirection(double targetDirectionDegrees)
    {
        var angleRad = (targetDirectionDegrees - DirectionDegrees) * Math.PI / 180.0;
        return SpeedMps * Math.Cos(angleRad);
    }

    public override string ToString()
    {
        return $"{SpeedMps:F1} м/с, {DirectionDegrees:F0}°";
    }
}

public class WeatherCondition
{
    public Guid Id { get; private set; }
    public DateTime Timestamp { get; private set; }
    public double Temperature { get; private set; }
    public double Humidity { get; private set; }
    public double WindSpeed { get; private set; }
    public double WindDirectionDegrees { get; private set; }
    public double WindSpeedMps { get; set; }
    public double Precipitation { get; private set; }

    [JsonIgnore]
    public WindVector WindVector => new WindVector(WindDirectionDegrees, WindSpeedMps);

    public WeatherCondition() { }

    public WeatherCondition(
        DateTime timestamp,
        double temperature,
        double humidity,
        double windSpeedMps,
        double windDirectionDegrees,
        double precipitation)
    {
        Id = Guid.NewGuid();
        Timestamp = timestamp;
        Temperature = Guard.Against.OutOfRange(temperature, nameof(temperature), -50, 60);
        Humidity = Guard.Against.OutOfRange(humidity, nameof(humidity), 0, 100);
        WindSpeedMps = windSpeedMps < 0 ? 0 : windSpeedMps;
        WindDirectionDegrees = Guard.Against.OutOfRange(windDirectionDegrees, nameof(windDirectionDegrees), 0, 360);
        Precipitation = Guard.Against.Negative(precipitation, nameof(precipitation));
        WindSpeed = WindSpeedMps;
        WindDirection = ConvertDegreesToDirection(windDirectionDegrees);
    }

    [JsonIgnore]
    public WindDirection WindDirection { get; private set; }

    private WindDirection ConvertDegreesToDirection(double degrees)
    {
        return degrees switch
        {
            >= 337.5 or < 22.5 => WindDirection.North,
            >= 22.5 and < 67.5 => WindDirection.Northeast,
            >= 67.5 and < 112.5 => WindDirection.East,
            >= 112.5 and < 157.5 => WindDirection.Southeast,
            >= 157.5 and < 202.5 => WindDirection.South,
            >= 202.5 and < 247.5 => WindDirection.Southwest,
            >= 247.5 and < 292.5 => WindDirection.West,
            _ => WindDirection.Northwest
        };
    }



    public double CalculateMoistureEvaporation()
    {
        double temperatureEffect = Math.Max(0, Temperature - 15) * 0.01;
        double windEffect = WindSpeedMps * 0.005;
        double humidityEffect = (100 - Humidity) * 0.001;

        return temperatureEffect + windEffect + humidityEffect;
    }


}
