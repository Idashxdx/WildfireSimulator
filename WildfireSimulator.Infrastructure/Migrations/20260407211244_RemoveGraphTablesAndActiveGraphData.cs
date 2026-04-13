using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WildfireSimulator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGraphTablesAndActiveGraphData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeatherConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    Humidity = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    WindSpeed = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    WindDirectionDegrees = table.Column<double>(type: "double precision", nullable: false),
                    WindSpeedMps = table.Column<double>(type: "double precision", nullable: false),
                    Precipitation = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    WindDirection = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherConditions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Simulations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Parameters_GridWidth = table.Column<int>(type: "integer", nullable: false),
                    Parameters_GridHeight = table.Column<int>(type: "integer", nullable: false),
                    Parameters_GraphType = table.Column<int>(type: "integer", nullable: false),
                    VegetationDistributions = table.Column<string>(type: "jsonb", nullable: false),
                    Parameters_InitialMoistureMin = table.Column<double>(type: "double precision", precision: 3, scale: 2, nullable: false),
                    Parameters_InitialMoistureMax = table.Column<double>(type: "double precision", precision: 3, scale: 2, nullable: false),
                    Parameters_ElevationVariation = table.Column<double>(type: "double precision", precision: 8, scale: 2, nullable: false),
                    Parameters_InitialFireCellsCount = table.Column<int>(type: "integer", nullable: false),
                    Parameters_SimulationSteps = table.Column<int>(type: "integer", nullable: false),
                    Parameters_StepDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    Parameters_RandomSeed = table.Column<int>(type: "integer", nullable: true),
                    WeatherConditionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SerializedGraph = table.Column<string>(type: "jsonb", nullable: true),
                    InitialFirePositions = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Simulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Simulations_WeatherConditions_WeatherConditionId",
                        column: x => x.WeatherConditionId,
                        principalTable: "WeatherConditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ActiveSimulationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    IsRunning = table.Column<bool>(type: "boolean", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalBurnedCells = table.Column<int>(type: "integer", nullable: false),
                    TotalBurningCells = table.Column<int>(type: "integer", nullable: false),
                    WeatherData = table.Column<string>(type: "jsonb", nullable: false),
                    StepResultsData = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveSimulationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActiveSimulationRecords_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FireMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BurningCellsCount = table.Column<int>(type: "integer", nullable: false),
                    BurnedCellsCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCellsAffected = table.Column<int>(type: "integer", nullable: false),
                    FireSpreadSpeed = table.Column<double>(type: "double precision", precision: 6, scale: 2, nullable: false),
                    AverageTemperature = table.Column<double>(type: "double precision", nullable: false),
                    AverageWindSpeed = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FireMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FireMetrics_Simulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "Simulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveSimulationRecords_IsRunning",
                table: "ActiveSimulationRecords",
                column: "IsRunning");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveSimulationRecords_SimulationId",
                table: "ActiveSimulationRecords",
                column: "SimulationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FireMetrics_SimulationId_Step",
                table: "FireMetrics",
                columns: new[] { "SimulationId", "Step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FireMetrics_Timestamp",
                table: "FireMetrics",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Simulations_CreatedAt",
                table: "Simulations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Simulations_Status",
                table: "Simulations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Simulations_WeatherConditionId",
                table: "Simulations",
                column: "WeatherConditionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeatherConditions_Timestamp",
                table: "WeatherConditions",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveSimulationRecords");

            migrationBuilder.DropTable(
                name: "FireMetrics");

            migrationBuilder.DropTable(
                name: "Simulations");

            migrationBuilder.DropTable(
                name: "WeatherConditions");
        }
    }
}
