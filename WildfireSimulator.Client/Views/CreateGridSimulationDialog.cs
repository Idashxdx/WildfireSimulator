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

            SelectedDemoPreset = SelectedDemoPreset,
            PreparedMap = PreparedMap,

            // legacy-совместимость для существующего API и остального клиента
            SelectedMapCreationMode = PreparedMap != null
                ? MapCreationMode.Random
                : string.IsNullOrWhiteSpace(SelectedDemoPreset)
                    ? MapCreationMode.Random
                    : MapCreationMode.Scenario,

            SelectedScenarioType = ParseDemoPresetToScenarioType(SelectedDemoPreset),
            SelectedClusteredScenarioType = null,

            MapNoiseStrength = MapNoiseStrength,
            MapDrynessFactor = MapDrynessFactor,
            ReliefStrengthFactor = ReliefStrengthFactor,
            FuelDensityFactor = FuelDensityFactor,

            MapRegionObjects = new(),
            VegetationDistributions = new(VegetationDistributions),
            ClusteredBlueprint = null
        };
    }
}