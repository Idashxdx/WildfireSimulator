using System.Text.Json.Serialization;

namespace WildfireSimulator.Domain.Models;

public class GraphSerializationDto
{
    public List<CellSerializationDto> Cells { get; set; } = new();
    public List<EdgeSerializationDto> Edges { get; set; } = new();
    public int Width { get; set; }
    public int Height { get; set; }
    public int StepDurationSeconds { get; set; }
}

public class CellSerializationDto
{
    public Guid Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string? ClusterId { get; set; }
    public VegetationType Vegetation { get; set; }
    public double Moisture { get; set; }
    public double Elevation { get; set; }
    public CellState State { get; set; }
    public double BurnProbability { get; set; }
    public DateTime? IgnitionTime { get; set; }
    public DateTime? BurnoutTime { get; set; }
    public double FuelLoad { get; set; }
    public double CurrentFuelLoad { get; set; }
    public double BurnRate { get; set; }
    public FireStage FireStage { get; set; }
    public double FireIntensity { get; set; }
    public double BurningElapsedSeconds { get; set; }
    public double AccumulatedHeatJ { get; set; }
}

public class EdgeSerializationDto
{
    public Guid Id { get; set; }
    public Guid FromCellId { get; set; }
    public Guid ToCellId { get; set; }
    public double Distance { get; set; }
    public double Slope { get; set; }
    public double FireSpreadModifier { get; set; }
    public double AccumulatedHeat { get; set; }
    public bool IsCorridor { get; set; }
}