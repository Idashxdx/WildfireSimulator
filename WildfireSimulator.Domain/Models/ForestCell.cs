using Ardalis.GuardClauses;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Domain.Models;

public enum VegetationType
{
    Grass = 0,
    Shrub = 1,
    Deciduous = 2,
    Coniferous = 3,
    Mixed = 4,
    Water = 5,
    Bare = 6
}

public enum CellState
{
    Normal = 0,
    Burning = 1,
    Burned = 2
}

public enum FireStage
{
    Unburned = 0,
    Ignition = 1,
    Active = 2,
    Intense = 3,
    Smoldering = 4,
    BurnedOut = 5
}

public class VegetationFuelProperties
{
    public double HeatOfCombustion { get; }
    public double BulkDensity { get; }
    public double VegetationHeight { get; }
    public double SurfaceAreaToVolumeRatio { get; }
    public double MineralContent { get; }
    public double MoistureOfExtinction { get; }
    public double BaseSpreadRateMps { get; }
    public double BaseIgnitionCoefficient { get; }

    public VegetationFuelProperties(
        double heatOfCombustion,
        double bulkDensity,
        double vegetationHeight,
        double surfaceAreaToVolumeRatio,
        double mineralContent,
        double moistureOfExtinction,
        double baseSpreadRateMps,
        double baseIgnitionCoefficient)
    {
        HeatOfCombustion = heatOfCombustion;
        BulkDensity = bulkDensity;
        VegetationHeight = vegetationHeight;
        SurfaceAreaToVolumeRatio = surfaceAreaToVolumeRatio;
        MineralContent = mineralContent;
        MoistureOfExtinction = moistureOfExtinction;
        BaseSpreadRateMps = baseSpreadRateMps;
        BaseIgnitionCoefficient = baseIgnitionCoefficient;
    }

    public double CalculateFuelLoad() => BulkDensity * VegetationHeight;

    public double GetMoistureFactor(double moisture)
    {
        if (moisture >= MoistureOfExtinction) return 0.1;
        return 1.0 - (moisture / MoistureOfExtinction);
    }

    public double GetMineralFactor() => 1.0 - (MineralContent / 100.0);

    public double GetIgnitionMoistureFactor(double moisture)
    {
        if (moisture >= MoistureOfExtinction) return 0.2;
        return 1.0 - (moisture / MoistureOfExtinction) * 0.8;
    }
}

public class ForestCell
{
    public Guid Id { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public string? ClusterId { get; private set; }
    public VegetationType Vegetation { get; private set; }
    public double Moisture { get; private set; }
    public double Elevation { get; private set; }
    public CellState State { get; private set; }
    public double BurnProbability { get; private set; }
    public DateTime? IgnitionTime { get; private set; }
    public DateTime? BurnoutTime { get; private set; }

    public double FuelLoad { get; private set; }
    public double CurrentFuelLoad { get; private set; }
    public double BurnRate { get; private set; }
    public FireStage FireStage { get; private set; }
    public double FireIntensity { get; private set; }

    public double BurningElapsedSeconds { get; private set; }
    public double AccumulatedHeatJ { get; private set; }

    [JsonIgnore]
    public virtual ICollection<ForestEdge> OutgoingEdges { get; private set; } = new List<ForestEdge>();

    [JsonIgnore]
    public virtual ICollection<ForestEdge> IncomingEdges { get; private set; } = new List<ForestEdge>();

    private VegetationFuelProperties _fuelProps = null!;
    private VegetationModelParameters _modelParameters = null!;

    private ForestCell() { }

    public ForestCell(
        int x,
        int y,
        VegetationType vegetation,
        double moisture,
        double elevation,
        string? clusterId = null)
    {
        Id = Guid.NewGuid();
        X = Guard.Against.Negative(x, nameof(x));
        Y = Guard.Against.Negative(y, nameof(y));
        ClusterId = string.IsNullOrWhiteSpace(clusterId) ? null : clusterId;
        Vegetation = vegetation;
        Moisture = Guard.Against.OutOfRange(moisture, nameof(moisture), 0, 1);
        Elevation = elevation;
        State = CellState.Normal;
        FireStage = FireStage.Unburned;
        BurningElapsedSeconds = 0.0;
        AccumulatedHeatJ = 0.0;

        InitializeFuelProperties();
        BurnProbability = CalculateBaseBurnProbability();
    }

    public void SetClusterId(string? clusterId)
    {
        ClusterId = string.IsNullOrWhiteSpace(clusterId) ? null : clusterId;
    }

    private void InitializeFuelProperties()
    {
        _modelParameters = FireModelCatalog.Get(Vegetation);

        _fuelProps = new VegetationFuelProperties(
            heatOfCombustion: _modelParameters.HeatOfCombustion,
            bulkDensity: _modelParameters.BulkDensity,
            vegetationHeight: _modelParameters.VegetationHeight,
            surfaceAreaToVolumeRatio: _modelParameters.SurfaceAreaToVolumeRatio,
            mineralContent: _modelParameters.MineralContent,
            moistureOfExtinction: _modelParameters.MoistureOfExtinction,
            baseSpreadRateMps: _modelParameters.BaseSpreadRateMps,
            baseIgnitionCoefficient: _modelParameters.BaseIgnitionCoefficient
        );

        FuelLoad = _fuelProps.CalculateFuelLoad();
        CurrentFuelLoad = FuelLoad;

        double mineralFactor = _fuelProps.GetMineralFactor();
        double moistureFactor = _fuelProps.GetMoistureFactor(Moisture);
        double baseBurnRate = 0.0005 * (_fuelProps.SurfaceAreaToVolumeRatio / 1000.0) * moistureFactor * mineralFactor;
        BurnRate = Math.Clamp(baseBurnRate, 0.0001, 0.01);

        FireIntensity = 0.0;
    }

    public void UpdateMoisture(double newMoisture)
    {
        Moisture = Guard.Against.OutOfRange(newMoisture, nameof(newMoisture), 0, 1);
        BurnProbability = CalculateBaseBurnProbability();

        double mineralFactor = _fuelProps.GetMineralFactor();
        double moistureFactor = _fuelProps.GetMoistureFactor(Moisture);
        double baseBurnRate = 0.0005 * (_fuelProps.SurfaceAreaToVolumeRatio / 1000.0) * moistureFactor * mineralFactor;
        BurnRate = Math.Clamp(baseBurnRate, 0.0001, 0.01);
    }

    public void SetBurnProbability(double probability)
    {
        BurnProbability = Math.Clamp(probability, 0.0, 1.0);
    }

    public void SetBurningElapsedSeconds(double burningElapsedSeconds)
    {
        BurningElapsedSeconds = Math.Max(0.0, burningElapsedSeconds);
    }



    public void CoolDown(double retentionFactor)
    {
        if (State != CellState.Normal)
            return;

        retentionFactor = Math.Clamp(retentionFactor, 0.0, 1.0);
        AccumulatedHeatJ *= retentionFactor;

        if (AccumulatedHeatJ < 1.0)
            AccumulatedHeatJ = 0.0;
    }

    public void ResetAccumulatedHeat()
    {
        AccumulatedHeatJ = 0.0;
    }

    public void SetAccumulatedHeatJ(double heatJ)
    {
        AccumulatedHeatJ = Math.Max(0.0, heatJ);
    }

    public void Ignite(DateTime ignitionTime)
    {
        if (State != CellState.Normal || CurrentFuelLoad <= 0.0)
            return;

        if (Vegetation == VegetationType.Water || Vegetation == VegetationType.Bare)
            return;

        State = CellState.Burning;
        IgnitionTime = ignitionTime;
        FireStage = FireStage.Ignition;
        AccumulatedHeatJ = 0.0;

        var initialElapsed = 0.0;
        if (ignitionTime.Kind != DateTimeKind.Unspecified)
        {
            initialElapsed = Math.Max(0.0, (DateTime.UtcNow - ignitionTime).TotalSeconds);
        }

        BurningElapsedSeconds = initialElapsed;

        double initialBurnRateMps = Math.Max(0.001, BurnRate / Math.Max(0.1, _fuelProps.BulkDensity));
        FireIntensity = _fuelProps.HeatOfCombustion * CurrentFuelLoad * initialBurnRateMps / 1000.0;
        FireIntensity = Math.Clamp(FireIntensity, 0.0, 50000.0);
    }

    public void UpdateBurn(TimeSpan elapsedTime, double windEffect, double slopeEffect = 1.0)
    {
        if (State != CellState.Burning || CurrentFuelLoad <= 0.0)
            return;

        double totalBurnoutTimeSeconds = GetExpectedBurnoutTimeSeconds();
        if (totalBurnoutTimeSeconds <= 0.0 || double.IsInfinity(totalBurnoutTimeSeconds))
            return;

        double elapsedSeconds = Math.Max(0.0, elapsedTime.TotalSeconds);
        BurningElapsedSeconds += elapsedSeconds;

        double progress = BurningElapsedSeconds / totalBurnoutTimeSeconds;
        progress = Math.Clamp(progress, 0.0, 1.2);

        if (BurningElapsedSeconds >= totalBurnoutTimeSeconds)
        {
            Extinguish(DateTime.UtcNow);
            return;
        }

        if (progress < 0.15)
            FireStage = FireStage.Ignition;
        else if (progress < 0.45)
            FireStage = FireStage.Active;
        else if (progress < 0.80)
            FireStage = FireStage.Intense;
        else
            FireStage = FireStage.Smoldering;

        double stageFactor = FireStage switch
        {
            FireStage.Ignition => 0.75,
            FireStage.Active => 1.00,
            FireStage.Intense => 1.20,
            FireStage.Smoldering => 0.65,
            _ => 0.90
        };

        double externalFactor = Math.Clamp(windEffect * slopeEffect, 0.85, 1.35);

        double normalizedBurn = (elapsedSeconds / totalBurnoutTimeSeconds) * stageFactor * externalFactor;
        normalizedBurn = Math.Max(0.0, normalizedBurn);

        double burnFraction = 1.0 - Math.Exp(-normalizedBurn);
        burnFraction = Math.Clamp(burnFraction, 0.0, 0.98);

        double burnedFuel = CurrentFuelLoad * burnFraction;
        CurrentFuelLoad = Math.Max(0.0, CurrentFuelLoad - burnedFuel);

        double fuelRatio = FuelLoad > 0.0 ? CurrentFuelLoad / FuelLoad : 0.0;

        if (CurrentFuelLoad > 0.0)
        {
            double currentBurnRateMps = Math.Max(0.001, _fuelProps.BaseSpreadRateMps * externalFactor);

            double progressIntensityFactor = Math.Sin(Math.Min(progress, 1.0) * Math.PI);
            progressIntensityFactor = Math.Max(0.25, progressIntensityFactor);

            FireIntensity = _fuelProps.HeatOfCombustion
                            * CurrentFuelLoad
                            * currentBurnRateMps
                            * progressIntensityFactor
                            / 1000.0;

            FireIntensity = Math.Clamp(FireIntensity, 0.0, 50000.0);
        }
        else
        {
            FireIntensity = 0.0;
        }

        if (fuelRatio <= 0.01 || BurningElapsedSeconds >= totalBurnoutTimeSeconds)
        {
            Extinguish(DateTime.UtcNow);
        }
    }

    public void Extinguish(DateTime burnoutTime)
    {
        if (State == CellState.Burning)
        {
            State = CellState.Burned;
            BurnoutTime = burnoutTime;
            FireStage = FireStage.BurnedOut;
            FireIntensity = 0.0;
            CurrentFuelLoad = 0.0;
            BurnProbability = 0.0;
            AccumulatedHeatJ = 0.0;
        }
    }

    private double GetExpectedBurnoutTimeSeconds()
    {
        return _modelParameters.BaseBurnDurationSeconds;
    }

    private double CalculateBaseBurnProbability()
    {
        if (State != CellState.Normal || CurrentFuelLoad <= 0.0)
            return 0.0;

        if (Vegetation == VegetationType.Water || Vegetation == VegetationType.Bare)
            return 0.0;

        double baseProb = _fuelProps.BaseIgnitionCoefficient;
        double moistureEffect = _fuelProps.GetIgnitionMoistureFactor(Moisture);
        double fuelEffect = Math.Min(1.0, CurrentFuelLoad / Math.Max(0.0001, FuelLoad));

        double probability = baseProb * moistureEffect * fuelEffect;
        return Math.Clamp(probability, 0.0, 0.95);
    }


    public double GetBaseSpreadRate() => _fuelProps.BaseSpreadRateMps;

    public VegetationFuelProperties GetFuelProperties() => _fuelProps;

    public VegetationModelParameters GetModelParameters() => _modelParameters;
}