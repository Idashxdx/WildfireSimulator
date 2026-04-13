using Ardalis.GuardClauses;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Domain.Models;

public class ForestEdge
{
    public Guid Id { get; private set; }
    public Guid FromCellId { get; private set; }
    public Guid ToCellId { get; private set; }
    public double Distance { get; private set; }
    public double Slope { get; private set; }
    public double FireSpreadModifier { get; private set; }

    [JsonIgnore]
    public virtual ForestCell FromCell { get; private set; } = null!;

    [JsonIgnore]
    public virtual ForestCell ToCell { get; private set; } = null!;

    private ForestEdge() { }

    public ForestEdge(ForestCell fromCell, ForestCell toCell, double distance, double slope)
    {
        Id = Guid.NewGuid();
        FromCellId = Guard.Against.Default(fromCell.Id, nameof(fromCell.Id));
        ToCellId = Guard.Against.Default(toCell.Id, nameof(toCell.Id));
        Distance = Guard.Against.Negative(distance, nameof(distance));
        Slope = slope;
        FromCell = fromCell;
        ToCell = toCell;
        FireSpreadModifier = CalculateSpreadModifier();
    }

    private double CalculateSpreadModifier()
    {
        double safeDistance = Math.Max(1.0, Distance);
        double distanceModifier = 1.0 / Math.Pow(safeDistance, 1.65);

        double clampedSlope = Math.Clamp(Slope, -0.5, 0.5);
        double slopeModifier = 1.0 + clampedSlope * 0.5;
        slopeModifier = Math.Clamp(slopeModifier, 0.75, 1.25);

        double modifier = distanceModifier * slopeModifier;
        return Math.Clamp(modifier, 0.02, 1.35);
    }

    public void ApplyBridgeSpreadBonus(double multiplier)
    {
        if (multiplier <= 0.0)
            return;

        FireSpreadModifier = Math.Clamp(FireSpreadModifier * multiplier, 0.02, 1.85);
    }
    private double CalculateWindDirectionModifier(WindDirection windDirection)
    {
        if (FromCell == null || ToCell == null)
            return 1.0;

        double dx = ToCell.X - FromCell.X;
        double dy = ToCell.Y - FromCell.Y;

        if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)
            return 1.0;

        double edgeDirectionDegrees = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        if (edgeDirectionDegrees < 0)
            edgeDirectionDegrees += 360.0;

        double windFlowDirectionDegrees = ((double)windDirection + 180.0) % 360.0;

        double angleDiff = Math.Abs(edgeDirectionDegrees - windFlowDirectionDegrees);
        angleDiff = Math.Min(angleDiff, 360.0 - angleDiff);

        double angleRad = angleDiff * Math.PI / 180.0;
        double cosine = Math.Cos(angleRad);

        double modifier = Math.Exp(0.9 * cosine);

        return Math.Clamp(modifier, 0.40, 2.50);
    }
}

public enum WindDirection
{
    North = 0,
    Northeast = 45,
    East = 90,
    Southeast = 135,
    South = 180,
    Southwest = 225,
    West = 270,
    Northwest = 315
}