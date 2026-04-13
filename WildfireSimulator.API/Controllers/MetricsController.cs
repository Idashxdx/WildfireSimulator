using Microsoft.AspNetCore.Mvc;
using Prometheus;
using WildfireSimulator.Application.Services;

namespace WildfireSimulator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly SimulationManager _simulationManager;
    private readonly ILogger<MetricsController> _logger;
    
    private static readonly Counter SimulationStarted = Metrics
        .CreateCounter("wildfire_simulations_started_total", "Total number of started simulations");
    
    private static readonly Counter SimulationStepsExecuted = Metrics
        .CreateCounter("wildfire_simulation_steps_total", "Total number of executed steps");
    
    private static readonly Gauge ActiveSimulations = Metrics
        .CreateGauge("wildfire_active_simulations", "Number of currently active simulations");
    
    private static readonly Gauge TotalBurnedCells = Metrics
        .CreateGauge("wildfire_burned_cells_total", "Total number of burned cells across all simulations");
    
    private static readonly Histogram StepExecutionTime = Metrics
        .CreateHistogram("wildfire_step_execution_seconds", "Histogram of step execution time");
    
    private static readonly Counter KafkaMessagesSent = Metrics
        .CreateCounter("wildfire_kafka_messages_total", "Total Kafka messages sent", new CounterConfiguration
        {
            LabelNames = new[] { "topic" }
        });

    public MetricsController(
        SimulationManager simulationManager,
        ILogger<MetricsController> logger)
    {
        _simulationManager = simulationManager;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var simulations = await _simulationManager.GetAllSimulations();
        
        return Ok(new
        {
            success = true,
            timestamp = DateTime.UtcNow,
            metrics = new
            {
                activeSimulations = simulations.Count,
                totalBurnedCells = simulations.Sum(s => s.TotalBurnedCells),
                totalBurningCells = simulations.Sum(s => s.TotalBurningCells),
                totalFireArea = simulations.Sum(s => s.FireArea),
                streamProcessing = new
                {
                    topics = new[]
                    {
                        "fire-moving-averages",
                        "fire-trends",
                        "fire-anomalies"
                    }
                }
            }
        });
    }

    public static void OnSimulationStarted()
    {
        SimulationStarted.Inc();
        ActiveSimulations.Inc();
    }

    public static void OnSimulationStopped()
    {
        ActiveSimulations.Dec();
    }

    public static void OnStepExecuted(int burnedCells, double executionTimeMs)
    {
        SimulationStepsExecuted.Inc();
        TotalBurnedCells.Inc(burnedCells);
        StepExecutionTime.Observe(executionTimeMs / 1000.0);
    }

    public static void OnKafkaMessageSent(string topic)
    {
        KafkaMessagesSent.WithLabels(topic).Inc();
    }
}
