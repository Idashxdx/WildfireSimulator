using System;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Client.Models;

public class SignalRMessage
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("data")]
    public object Data { get; set; } = new();
}

public class MovingAverageData
{
    [JsonPropertyName("simulationId")]
    public string SimulationId { get; set; } = string.Empty;

    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("currentArea")]
    public double CurrentArea { get; set; }

    [JsonPropertyName("movingAverage3")]
    public double MovingAverage3 { get; set; }

    [JsonPropertyName("movingAverage5")]
    public double MovingAverage5 { get; set; }

    [JsonPropertyName("movingAverage10")]
    public double MovingAverage10 { get; set; }

    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    [JsonPropertyName("acceleration")]
    public double Acceleration { get; set; }
}

public class TrendData
{
    [JsonPropertyName("simulationId")]
    public string SimulationId { get; set; } = string.Empty;

    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("trend")]
    public string Trend { get; set; } = string.Empty;

    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    [JsonPropertyName("acceleration")]
    public double Acceleration { get; set; }

    [JsonPropertyName("isCritical")]
    public bool IsCritical { get; set; }
}

public class AnomalyData
{
    [JsonPropertyName("simulationId")]
    public string SimulationId { get; set; } = string.Empty;

    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("currentArea")]
    public double CurrentArea { get; set; }

    [JsonPropertyName("previousAvg")]
    public double PreviousAvg { get; set; }

    [JsonPropertyName("deviation")]
    public double Deviation { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
public class ForecastData
{
    [JsonPropertyName("simulationId")]
    public string SimulationId { get; set; } = string.Empty;

    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("currentArea")]
    public double CurrentArea { get; set; }

    [JsonPropertyName("forecastNextArea")]
    public double ForecastNextArea { get; set; }

    [JsonPropertyName("forecastDelta")]
    public double ForecastDelta { get; set; }

    [JsonPropertyName("basedOnPoints")]
    public int BasedOnPoints { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("lastForecastAbsoluteError")]
    public double LastForecastAbsoluteError { get; set; }

    [JsonPropertyName("meanAbsoluteError")]
    public double MeanAbsoluteError { get; set; }

    [JsonPropertyName("forecastErrorCount")]
    public int ForecastErrorCount { get; set; }
}