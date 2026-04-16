using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WildfireSimulator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClusteredBlueprint",
                table: "Simulations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Parameters_ClusteredScenarioType",
                table: "Simulations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClusteredBlueprint",
                table: "Simulations");

            migrationBuilder.DropColumn(
                name: "Parameters_ClusteredScenarioType",
                table: "Simulations");
        }
    }
}
