using Microsoft.EntityFrameworkCore;
using Prometheus;
using WildfireSimulator.API.Middleware;
using WildfireSimulator.API.Hubs;
using WildfireSimulator.API.Services;
using WildfireSimulator.Application;
using WildfireSimulator.Infrastructure;
using WildfireSimulator.Infrastructure.Data;
using System.Text.Json;
using WildfireSimulator.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
   options.TimestampFormat = "HH:mm:ss";
   options.JsonWriterOptions = new JsonWriterOptions
   {
       Indented = false,
       Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
   };
   options.IncludeScopes = true;
});

builder.Services.AddHttpContextAccessor();

builder.Configuration
   .SetBasePath(builder.Environment.ContentRootPath)
   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
   .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
   .AddEnvironmentVariables();

builder.Services.AddSignalR();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<KafkaStreamsService>();
builder.Services.AddHostedService<KafkaSignalRBridgeService>();

builder.Services.AddControllers()
   .AddJsonOptions(options =>
   {
       options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
       options.JsonSerializerOptions.WriteIndented = true;
       options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
   });

builder.Services.AddCors(options =>
{
   options.AddPolicy("AllowAll",
       builder =>
       {
           builder.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
       });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
   c.SwaggerDoc("v1", new() {
       Title = "Wildfire Simulator API",
       Version = "v1",
       Description = "API для имитационного моделирования распространения лесных пожаров"
   });
});

var app = builder.Build();

app.UseHttpMetrics(options =>
{
   options.AddCustomLabel("host", context => context.Request.Host.Host);
});

app.UseMetricServer();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
   app.UseSwagger();
   app.UseSwaggerUI(c =>
   {
       c.SwaggerEndpoint("/swagger/v1/swagger.json", "Wildfire Simulator API v1");
       c.RoutePrefix = "api-docs";
   });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<FireHub>("/fireHub");

app.Map("/health", () => Results.Ok(new
{
   status = "Healthy",
   timestamp = DateTime.UtcNow,
   service = "Wildfire Simulator API"
}));

using (var scope = app.Services.CreateScope())
{
   var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
   var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
  
   try
   {
       var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
       if (pendingMigrations.Any())
       {
           logger.LogInformation("📦 Накатываем миграции: {Migrations}", string.Join(", ", pendingMigrations));
           await dbContext.Database.MigrateAsync();
       }
       else
       {
           logger.LogInformation("✅ База данных в актуальном состоянии");
       }
      
       var canConnect = await dbContext.Database.CanConnectAsync();
       if (canConnect)
       {
           var simulationsCount = await dbContext.Simulations.CountAsync();
           var activeCount = await dbContext.ActiveSimulationRecords.CountAsync();
           logger.LogInformation(" Статистика: {Simulations} симуляций, {Active} активных",
               simulationsCount, activeCount);
       }
   }
   catch (Exception ex)
   {
       logger.LogError(ex, "❌ Ошибка при проверке БД");
   }
}

var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("🚀 Wildfire Simulator API запущен");
startupLogger.LogInformation("📡 Environment: {Environment}", app.Environment.EnvironmentName);
startupLogger.LogInformation("🔗 Swagger UI: http://localhost:5198/api-docs");
startupLogger.LogInformation("🏥 Health check: http://localhost:5198/health");
startupLogger.LogInformation(" Prometheus metrics: http://localhost:5198/metrics");
startupLogger.LogInformation("📡 SignalR Hub: http://localhost:5198/fireHub");
startupLogger.LogInformation("🌐 Web UI: http://localhost:5198");
startupLogger.LogInformation("📋 Kafka Streams и SignalR Bridge активированы");

app.Run();
