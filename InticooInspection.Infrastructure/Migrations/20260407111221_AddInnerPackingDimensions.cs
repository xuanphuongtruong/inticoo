using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInnerPackingDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "InnerSizeH",
                table: "InspectionPackagings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "InnerSizeL",
                table: "InspectionPackagings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "InnerSizeW",
                table: "InspectionPackagings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "InnerWeight",
                table: "InspectionPackagings",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InnerSizeH",
                table: "InspectionPackagings");

            migrationBuilder.DropColumn(
                name: "InnerSizeL",
                table: "InspectionPackagings");

            migrationBuilder.DropColumn(
                name: "InnerSizeW",
                table: "InspectionPackagings");

            migrationBuilder.DropColumn(
                name: "InnerWeight",
                table: "InspectionPackagings");
        }
    }
}
