using System;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Client.Models;

public class SimulationStatusDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("currentStep")]
    public int CurrentStep { get; set; }

    [JsonPropertyName("isRunning")]
    public bool IsRunning { get; set; }

    [JsonPropertyName("fireArea")]
    public double FireArea { get; set; }

    [JsonPropertyName("totalBurnedCells")]
    public int TotalBurnedCells { get; set; }

    [JsonPropertyName("totalBurningCells")]
    public int TotalBurningCells { get; set; }

    [JsonPropertyName("graphType")]
    public GraphType GraphType { get; set; } = GraphType.Grid;

    [JsonPropertyName("precipitation")]
    public double Precipitation { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("humidity")]
    public double Humidity { get; set; }

    [JsonPropertyName("windSpeed")]
    public double WindSpeed { get; set; }

    [JsonPropertyName("windDirection")]
    public string WindDirection { get; set; } = "—";

    [JsonPropertyName("windDirectionDegrees")]
    public double WindDirectionDegrees { get; set; }

    [JsonPropertyName("warning")]
    public string? Warning { get; set; }

    public string StatusText => Status switch
    {
        0 => "Создана",
        1 => "Запущена",
        2 => "Завершена",
        3 => "Отменена",
        _ => "Неизвестно"
    };
}