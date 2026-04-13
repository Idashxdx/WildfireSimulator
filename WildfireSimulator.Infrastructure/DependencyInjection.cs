using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using WildfireSimulator.Application.Interfaces;
using WildfireSimulator.Infrastructure.Data;
using WildfireSimulator.Infrastructure.Repositories;
using WildfireSimulator.Infrastructure.Services;
using WildfireSimulator.Infrastructure.Kafka;

namespace WildfireSimulator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
        
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ISimulationRepository, SimulationRepository>();
        services.AddScoped<IActiveSimulationRepository, ActiveSimulationRepository>();
        services.AddScoped<IForestGraphGenerator, ForestGraphGenerator>();
        services.AddScoped<IFireMetricsRepository, FireMetricsRepository>();
        
        var kafkaConfig = new KafkaConfig();
        configuration.GetSection("Kafka").Bind(kafkaConfig);
        services.AddSingleton(kafkaConfig);
        
        services.AddSingleton<IKafkaProducerService, RealKafkaProducerService>();
        
        return services;
    }
}
