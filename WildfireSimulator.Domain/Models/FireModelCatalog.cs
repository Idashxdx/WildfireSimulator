using Ardalis.GuardClauses;

namespace WildfireSimulator.Domain.Models;

public sealed class VegetationModelParameters
{
   public VegetationType VegetationType { get; }
   public double HeatOfCombustion { get; }
   public double BulkDensity { get; }
   public double VegetationHeight { get; }
   public double SurfaceAreaToVolumeRatio { get; }
   public double MineralContent { get; }
   public double MoistureOfExtinction { get; }
   public double BaseSpreadRateMps { get; }
   public double BaseIgnitionCoefficient { get; }
   public double FuelLoadKgPerM2 { get; }
   public double BaseIgnitionThresholdJ { get; }
   public double BaseBurnDurationSeconds { get; }

   public VegetationModelParameters(
       VegetationType vegetationType,
       double heatOfCombustion,
       double bulkDensity,
       double vegetationHeight,
       double surfaceAreaToVolumeRatio,
       double mineralContent,
       double moistureOfExtinction,
       double baseSpreadRateMps,
       double baseIgnitionCoefficient,
       double fuelLoadKgPerM2,
       double baseIgnitionThresholdJ,
       double baseBurnDurationSeconds)
   {
       VegetationType = vegetationType;
       HeatOfCombustion = Guard.Against.Negative(heatOfCombustion, nameof(heatOfCombustion));
       BulkDensity = Guard.Against.Negative(bulkDensity, nameof(bulkDensity));
       VegetationHeight = Guard.Against.Negative(vegetationHeight, nameof(vegetationHeight));
       SurfaceAreaToVolumeRatio = Guard.Against.Negative(surfaceAreaToVolumeRatio, nameof(surfaceAreaToVolumeRatio));
       MineralContent = Guard.Against.Negative(mineralContent, nameof(mineralContent));
       MoistureOfExtinction = Guard.Against.Negative(moistureOfExtinction, nameof(moistureOfExtinction));
       BaseSpreadRateMps = Guard.Against.Negative(baseSpreadRateMps, nameof(baseSpreadRateMps));
       BaseIgnitionCoefficient = Guard.Against.Negative(baseIgnitionCoefficient, nameof(baseIgnitionCoefficient));
       FuelLoadKgPerM2 = Guard.Against.Negative(fuelLoadKgPerM2, nameof(fuelLoadKgPerM2));
       BaseIgnitionThresholdJ = Guard.Against.Negative(baseIgnitionThresholdJ, nameof(baseIgnitionThresholdJ));
       BaseBurnDurationSeconds = Guard.Against.Negative(baseBurnDurationSeconds, nameof(baseBurnDurationSeconds));
   }

   public double CalculateStructuralFuelLoad() => BulkDensity * VegetationHeight;
}

public static class FireModelCatalog
{
   private static readonly IReadOnlyDictionary<VegetationType, VegetationModelParameters> _catalog =
       new Dictionary<VegetationType, VegetationModelParameters>
       {
           [VegetationType.Grass] = new(
               vegetationType: VegetationType.Grass,
               heatOfCombustion: 18600.0,
               bulkDensity: 0.8,
               vegetationHeight: 0.3,
               surfaceAreaToVolumeRatio: 6000.0,
               mineralContent: 0.05,
               moistureOfExtinction: 0.70,
               baseSpreadRateMps: 0.0142,
               baseIgnitionCoefficient: 0.95,
               fuelLoadKgPerM2: 0.8,
               baseIgnitionThresholdJ: 8000.0 * 10000.0,
               baseBurnDurationSeconds: 5400.0
           ),

           [VegetationType.Shrub] = new(
               vegetationType: VegetationType.Shrub,
               heatOfCombustion: 19200.0,
               bulkDensity: 1.5,
               vegetationHeight: 1.0,
               surfaceAreaToVolumeRatio: 4000.0,
               mineralContent: 0.03,
               moistureOfExtinction: 0.65,
               baseSpreadRateMps: 0.0092,
               baseIgnitionCoefficient: 0.85,
               fuelLoadKgPerM2: 1.5,
               baseIgnitionThresholdJ: 12000.0 * 10000.0,
               baseBurnDurationSeconds: 7200.0
           ),

           [VegetationType.Deciduous] = new(
               vegetationType: VegetationType.Deciduous,
               heatOfCombustion: 18800.0,
               bulkDensity: 3.0,
               vegetationHeight: 15.0,
               surfaceAreaToVolumeRatio: 800.0,
               mineralContent: 0.02,
               moistureOfExtinction: 0.60,
               baseSpreadRateMps: 0.0058,
               baseIgnitionCoefficient: 0.65,
               fuelLoadKgPerM2: 3.0,
               baseIgnitionThresholdJ: 18000.0 * 10000.0,
               baseBurnDurationSeconds: 12600.0
           ),

           [VegetationType.Coniferous] = new(
               vegetationType: VegetationType.Coniferous,
               heatOfCombustion: 20500.0,
               bulkDensity: 4.0,
               vegetationHeight: 20.0,
               surfaceAreaToVolumeRatio: 1200.0,
               mineralContent: 0.02,
               moistureOfExtinction: 0.65,
               baseSpreadRateMps: 0.0100,
               baseIgnitionCoefficient: 0.85,
               fuelLoadKgPerM2: 4.0,
               baseIgnitionThresholdJ: 15000.0 * 10000.0,
               baseBurnDurationSeconds: 10800.0
           ),

           [VegetationType.Mixed] = new(
               vegetationType: VegetationType.Mixed,
               heatOfCombustion: 19600.0,
               bulkDensity: 3.5,
               vegetationHeight: 18.0,
               surfaceAreaToVolumeRatio: 1000.0,
               mineralContent: 0.02,
               moistureOfExtinction: 0.62,
               baseSpreadRateMps: 0.0075,
               baseIgnitionCoefficient: 0.75,
               fuelLoadKgPerM2: 3.5,
               baseIgnitionThresholdJ: 16500.0 * 10000.0,
               baseBurnDurationSeconds: 11700.0
           ),

           [VegetationType.Water] = new(
               vegetationType: VegetationType.Water,
               heatOfCombustion: 0.0,
               bulkDensity: 0.0,
               vegetationHeight: 0.0,
               surfaceAreaToVolumeRatio: 0.0,
               mineralContent: 0.0,
               moistureOfExtinction: 1.0,
               baseSpreadRateMps: 0.0,
               baseIgnitionCoefficient: 0.0,
               fuelLoadKgPerM2: 0.0,
               baseIgnitionThresholdJ: double.MaxValue,
               baseBurnDurationSeconds: double.MaxValue
           ),

           [VegetationType.Bare] = new(
               vegetationType: VegetationType.Bare,
               heatOfCombustion: 5000.0,
               bulkDensity: 0.2,
               vegetationHeight: 0.05,
               surfaceAreaToVolumeRatio: 500.0,
               mineralContent: 0.10,
               moistureOfExtinction: 0.40,
               baseSpreadRateMps: 0.0020,
               baseIgnitionCoefficient: 0.30,
               fuelLoadKgPerM2: 0.2,
               baseIgnitionThresholdJ: double.MaxValue,
               baseBurnDurationSeconds: 7200.0
           )
       };

   public static VegetationModelParameters Get(VegetationType vegetationType)
   {
       if (_catalog.TryGetValue(vegetationType, out var parameters))
           return parameters;

       throw new ArgumentOutOfRangeException(nameof(vegetationType), vegetationType, "Unknown vegetation type");
   }

   public static IReadOnlyDictionary<VegetationType, VegetationModelParameters> GetAll() => _catalog;
}