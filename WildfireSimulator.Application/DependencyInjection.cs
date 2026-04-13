using Microsoft.Extensions.DependencyInjection;
using WildfireSimulator.Application.Interfaces;
using WildfireSimulator.Application.Services;

namespace WildfireSimulator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IFireSpreadSimulator, FireSpreadSimulator>();
        services.AddSingleton<SimulationManager>();
        services.AddSingleton<KafkaFacade>();
        services.AddSingleton<IFireSpreadCalculator, FireSpreadCalculator>();

        return services;
    }
}
