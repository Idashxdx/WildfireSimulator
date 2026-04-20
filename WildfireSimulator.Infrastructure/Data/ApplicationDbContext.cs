using Microsoft.EntityFrameworkCore;
using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<WeatherCondition> WeatherConditions { get; set; } = null!;
    public DbSet<Simulation> Simulations { get; set; } = null!;
    public DbSet<FireMetrics> FireMetrics { get; set; } = null!;
    public DbSet<ActiveSimulationRecord> ActiveSimulationRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ActiveSimulationRecord>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SimulationId)
                .IsRequired();

            entity.Property(e => e.SimulationName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.CurrentStep)
                .IsRequired();

            entity.Property(e => e.IsRunning)
                .IsRequired();

            entity.Property(e => e.StartTime)
                .IsRequired();

            entity.Property(e => e.WeatherData)
                .IsRequired()
                .HasColumnType("jsonb");

            entity.Property(e => e.StepResultsData)
                .IsRequired()
                .HasColumnType("jsonb");

            entity.HasOne(e => e.Simulation)
                .WithMany()
                .HasForeignKey(e => e.SimulationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.SimulationId)
                .IsUnique();

            entity.HasIndex(e => e.IsRunning);
        });

        modelBuilder.Entity<WeatherCondition>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Timestamp)
                .IsRequired();

            entity.Property(e => e.Temperature)
                .IsRequired()
                .HasPrecision(5, 2);

            entity.Property(e => e.Humidity)
                .IsRequired()
                .HasPrecision(5, 2);

            entity.Property(e => e.WindSpeed)
                .IsRequired()
                .HasPrecision(5, 2);

            entity.Property(e => e.WindDirection)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.Precipitation)
                .IsRequired()
                .HasPrecision(5, 2);

            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<Simulation>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.SerializedGraph)
                .HasColumnType("jsonb");

            entity.Property(e => e.InitialFirePositions)
                .HasColumnType("jsonb");

            entity.OwnsOne(e => e.Parameters, p =>
            {
                p.Property(pp => pp.GridWidth).IsRequired();
                p.Property(pp => pp.GridHeight).IsRequired();
                p.Property(pp => pp.GraphType).IsRequired().HasConversion<int>();

                p.Property(pp => pp.GraphScaleType)
                    .HasConversion<int?>();

                p.Property(pp => pp.InitialMoistureMin).IsRequired().HasPrecision(3, 2);
                p.Property(pp => pp.InitialMoistureMax).IsRequired().HasPrecision(3, 2);
                p.Property(pp => pp.ElevationVariation).IsRequired().HasPrecision(8, 2);
                p.Property(pp => pp.InitialFireCellsCount).IsRequired();
                p.Property(pp => pp.SimulationSteps).IsRequired();
                p.Property(pp => pp.StepDurationSeconds).IsRequired();
                p.Property(pp => pp.RandomSeed).IsRequired(false);

                p.Property(pp => pp.MapCreationMode)
                    .IsRequired()
                    .HasConversion<int>();

                p.Property(pp => pp.ScenarioType)
                    .HasConversion<int?>();

                p.Property(pp => pp.ClusteredScenarioType)
                    .HasConversion<int?>();

                p.Property(pp => pp.MapNoiseStrength)
                    .IsRequired()
                    .HasPrecision(4, 3);

                p.Property(pp => pp.VegetationDistributionsJson)
                    .HasColumnName("VegetationDistributions")
                    .HasColumnType("jsonb");

                p.Property(pp => pp.MapRegionObjectsJson)
                    .HasColumnName("MapRegionObjects")
                    .HasColumnType("jsonb");

                p.Property(pp => pp.ClusteredBlueprintJson)
                    .HasColumnName("ClusteredBlueprint")
                    .HasColumnType("jsonb");
            });

            entity.HasOne(e => e.WeatherCondition)
                .WithMany()
                .HasForeignKey(e => e.WeatherConditionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<FireMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Step)
                .IsRequired();

            entity.Property(e => e.Timestamp)
                .IsRequired();

            entity.Property(e => e.BurningCellsCount)
                .IsRequired();

            entity.Property(e => e.BurnedCellsCount)
                .IsRequired();

            entity.Property(e => e.FireSpreadSpeed)
                .IsRequired()
                .HasPrecision(6, 2);

            entity.HasOne(e => e.Simulation)
                .WithMany(s => s.Metrics)
                .HasForeignKey(e => e.SimulationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SimulationId, e.Step })
                .IsUnique();

            entity.HasIndex(e => e.Timestamp);
        });
    }
}