using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Domain.Models;

public class ForestGraph
{
    public List<ForestCell> Cells { get; set; } = new();
    public List<ForestEdge> Edges { get; set; } = new();
    public int Width { get; set; }
    public int Height { get; set; }

    public int StepDurationSeconds { get; set; } = 5400;

    [JsonIgnore]
    public ForestCell? this[int x, int y] => GetCell(x, y);

    public ForestCell? GetCell(int x, int y)
    {
        return Cells.FirstOrDefault(c => c.X == x && c.Y == y);
    }

    public List<ForestCell> GetNeighbors(ForestCell cell)
    {
        var neighborEdges = Edges.Where(e => e.FromCellId == cell.Id || e.ToCellId == cell.Id);
        return neighborEdges
            .Select(e =>
            {
                if (e.FromCellId == cell.Id)
                    return Cells.First(c => c.Id == e.ToCellId);
                else
                    return Cells.First(c => c.Id == e.FromCellId);
            })
            .ToList();
    }

    public List<ForestEdge> GetIncidentEdges(ForestCell cell)
    {
        return Edges
            .Where(e => e.FromCellId == cell.Id || e.ToCellId == cell.Id)
            .ToList();
    }

    public ForestCell GetOppositeCell(ForestEdge edge, ForestCell cell)
    {
        if (edge.FromCellId == cell.Id)
            return Cells.First(c => c.Id == edge.ToCellId);

        if (edge.ToCellId == cell.Id)
            return Cells.First(c => c.Id == edge.FromCellId);

        throw new InvalidOperationException("Edge is not incident to the provided cell.");
    }
}