using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace WildfireSimulator.Application.Common;

public static class SimulationLogger
{
    public static void LogSimulationInformation(
        this ILogger logger,
        Guid simulationId,
        int step,
        string message,
        params object[] args)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["SimulationId"] = simulationId,
            ["Step"] = step,
            ["CorrelationId"] = Guid.NewGuid().ToString()
        }))
        {
            logger.LogInformation(message, args);
        }
    }

    public static void LogSimulationWarning(
        this ILogger logger,
        Guid simulationId,
        int step,
        string message,
        params object[] args)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["SimulationId"] = simulationId,
            ["Step"] = step
        }))
        {
            logger.LogWarning(message, args);
        }
    }

    public static void LogSimulationError(
        this ILogger logger,
        Guid simulationId,
        int step,
        Exception? ex,
        string message,
        params object[] args)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["SimulationId"] = simulationId,
            ["Step"] = step
        }))
        {
            logger.LogError(ex, message, args);
        }
    }

    public static void LogSimulationDebug(
        this ILogger logger,
        Guid simulationId,
        int step,
        string message,
        params object[] args)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["SimulationId"] = simulationId,
            ["Step"] = step
        }))
        {
            logger.LogDebug(message, args);
        }
    }

    public static void LogSimulationMetrics(
        this ILogger logger,
        Guid simulationId,
        int step,
        int burningCells,
        int burnedCells,
        double fireArea,
        double spreadSpeed)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["SimulationId"] = simulationId,
            ["Step"] = step,
            ["EventType"] = "Metrics"
        }))
        {
            logger.LogInformation(
                " Метрики: Burning={Burning}, Burned={Burned}, Area={Area:F1}га, Speed={Speed:F1}га/день",
                burningCells,
                burnedCells,
                fireArea,
                spreadSpeed);
        }
    }

    public static void LogIgnitionEvent(
        this ILogger logger,
        Guid simulationId,
        int step,
        int x,
        int y,
        string vegetation,
        double probability)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["SimulationId"] = simulationId,
            ["Step"] = step,
            ["EventType"] = "Ignition",
            ["CellX"] = x,
            ["CellY"] = y
        }))
        {
            logger.LogDebug(
                " НОВОЕ ВОЗГОРАНИЕ ({X},{Y}) {Vegetation} (p={Probability:F2})",
                x, y, vegetation, probability);
        }
    }
}
