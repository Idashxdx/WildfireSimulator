using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WildfireSimulator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMapCreationModesAndRegionObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MapRegionObjects",
                table: "Simulations",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Parameters_MapCreationMode",
                table: "Simulations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Parameters_MapNoiseStrength",
                table: "Simulations",
                type: "double precision",
                precision: 4,
                scale: 3,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Parameters_ScenarioType",
                table: "Simulations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MapRegionObjects",
                table: "Simulations");

            migrationBuilder.DropColumn(
                name: "Parameters_MapCreationMode",
                table: "Simulations");

            migrationBuilder.DropColumn(
                name: "Parameters_MapNoiseStrength",
                table: "Simulations");

            migrationBuilder.DropColumn(
                name: "Parameters_ScenarioType",
                table: "Simulations");
        }
    }
}
