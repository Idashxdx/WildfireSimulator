using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WildfireSimulator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGraphScaleType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Parameters_GraphScaleType",
                table: "Simulations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Parameters_GraphScaleType",
                table: "Simulations");
        }
    }
}
