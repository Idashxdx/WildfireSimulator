using System.Collections.Generic;
using WildfireSimulator.Client.Models;

namespace WildfireSimulator.Client.Views;

public partial class CreateGridSimulationDialog : CreateGridSimulationDialogBase
{
    public CreateGridSimulationDialog()
    {
        InitializeComponent();
        InitializeGridDialog();
        Title = "Создание сеточной симуляции";
    }

    public SimulationCreationResult GetResult()
    {
        return new SimulationCreationResult
        {
            GraphType = GraphType.Grid,
            GraphScaleType = null,

            SimulationName = SimulationName,

            GridWidth = GridWidth,
            GridHeight = GridHeight,
            InitialFireCells = InitialFireCells,

            MoistureMin = MoistureMin,
            MoistureMax = MoistureMax,
            ElevationVariation = ElevationVariation,

            SimulationSteps = SimulationSteps,
            StepDurationSeconds = StepDurationSeconds,

            Temperature = Temperature,
            Humidity = Humidity,
            WindSpeed = WindSpeed,
            WindDirection = WindDirection,
            Precipitation = Precipitation,

            RandomSeed = RandomSeed,

            SelectedMapCreationMode = SelectedMapCreationMode,
            SelectedScenarioType = SelectedScenarioType,
            SelectedClusteredScenarioType = null,

            MapNoiseStrength = MapNoiseStrength,
            MapDrynessFactor = MapDrynessFactor,
            ReliefStrengthFactor = ReliefStrengthFactor,
            FuelDensityFactor = FuelDensityFactor,

            MapRegionObjects = new List<MapRegionObjectDto>(MapRegionObjects),
            VegetationDistributions = new List<(int VegetationType, double Probability)>(VegetationDistributions),
            ClusteredBlueprint = null
        };
    }
}