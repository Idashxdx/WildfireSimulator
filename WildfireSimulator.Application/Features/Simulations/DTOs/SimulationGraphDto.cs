using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Features.Simulations.DTOs;

public class SimulationGraphDto
{
    public Guid SimulationId { get; set; }
    public string SimulationName { get; set; } = string.Empty;
    public GraphType GraphType { get; set; }
    public GraphScaleType? GraphScaleType { get; set; }
    public string LayoutHint { get; set; } = "grid";
    public int Width { get; set; }
    public int Height { get; set; }
    public int StepDurationSeconds { get; set; }

    public List<SimulationGraphNodeDto> Nodes { get; set; } = new();
    public List<SimulationGraphEdgeDto> Edges { get; set; } = new();
}

public class SimulationGraphNodeDto
{
    public Guid Id { get; set; }

    public int X { get; set; }
    public int Y { get; set; }

    public double RenderX { get; set; }
    public double RenderY { get; set; }

    public string GroupKey { get; set; } = string.Empty;

    public string Vegetation { get; set; } = string.Empty;
    public double Moisture { get; set; }
    public double Elevation { get; set; }
    public string State { get; set; } = string.Empty;
    public double BurnProbability { get; set; }
    public DateTime? IgnitionTime { get; set; }
    public DateTime? BurnoutTime { get; set; }

    public string FireStage { get; set; } = string.Empty;
    public double FireIntensity { get; set; }
    public double CurrentFuelLoad { get; set; }
    public double FuelLoad { get; set; }
    public double BurningElapsedSeconds { get; set; }
    public double AccumulatedHeatJ { get; set; }
    public bool IsIgnitable { get; set; } = true;
    public double PrecipitationIntensity { get; set; }
    public bool IsInPrecipitationFront { get; set; }
}

public class SimulationGraphEdgeDto
{
    public Guid Id { get; set; }

    public Guid FromCellId { get; set; }
    public int FromX { get; set; }
    public int FromY { get; set; }
    public double FromRenderX { get; set; }
    public double FromRenderY { get; set; }

    public Guid ToCellId { get; set; }
    public int ToX { get; set; }
    public int ToY { get; set; }
    public double ToRenderX { get; set; }
    public double ToRenderY { get; set; }

    public double Distance { get; set; }
    public double Slope { get; set; }
    public double FireSpreadModifier { get; set; }
    public double AccumulatedHeat { get; set; }
    public bool IsCorridor { get; set; }
}