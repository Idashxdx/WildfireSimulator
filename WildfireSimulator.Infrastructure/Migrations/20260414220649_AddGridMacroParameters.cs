using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WildfireSimulator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGridMacroParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Parameters_FuelDensityFactor",
                table: "Simulations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Parameters_MapDrynessFactor",
                table: "Simulations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Parameters_ReliefStrengthFactor",
                table: "Simulations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Parameters_FuelDensityFactor",
                table: "Simulations");

            migrationBuilder.DropColumn(
                name: "Parameters_MapDrynessFactor",
                table: "Simulations");

            migrationBuilder.DropColumn(
                name: "Parameters_ReliefStrengthFactor",
                table: "Simulations");
        }
    }
}
