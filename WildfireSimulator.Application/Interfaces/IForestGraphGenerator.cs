using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Interfaces;

public interface IForestGraphGenerator
{
   Task<ForestGraph> GenerateGridAsync(int width, int height, SimulationParameters parameters);
   Task<ForestGraph> GenerateClusteredGraphAsync(int nodeCount, SimulationParameters parameters);
   Task<ForestGraph> GenerateRegionClusterGraphAsync(SimulationParameters parameters);
}