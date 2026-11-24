using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActualSizeToProductSpec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ActualSizeH",
                table: "InspectionProductSpecs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ActualSizeL",
                table: "InspectionProductSpecs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ActualSizeW",
                table: "InspectionProductSpecs",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualSizeH",
                table: "InspectionProductSpecs");

            migrationBuilder.DropColumn(
                name: "ActualSizeL",
                table: "InspectionProductSpecs");

            migrationBuilder.DropColumn(
                name: "ActualSizeW",
                table: "InspectionProductSpecs");
        }
    }
}
