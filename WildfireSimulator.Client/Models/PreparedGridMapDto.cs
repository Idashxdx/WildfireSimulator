using System;
using System.Collections.Generic;

namespace WildfireSimulator.Client.Models;

public class PreparedGridMapDto
{
    public int Width { get; set; }
    public int Height { get; set; }

    public List<PreparedGridCellDto> Cells { get; set; } = new();
}

public class PreparedGridCellDto
{
    public int X { get; set; }
    public int Y { get; set; }

    public string Vegetation { get; set; } = string.Empty;
    public double Moisture { get; set; }
    public double Elevation { get; set; }
}