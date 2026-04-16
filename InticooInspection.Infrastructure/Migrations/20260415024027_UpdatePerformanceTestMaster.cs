using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePerformanceTestMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PerformanceTestMasters_ProductType_IsActive",
                table: "PerformanceTestMasters");

            migrationBuilder.DropColumn(
                name: "Market",
                table: "PerformanceTestMasters");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "PerformanceTestMasters");

            migrationBuilder.DropColumn(
                name: "StandardCode",
                table: "PerformanceTestMasters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Market",
                table: "PerformanceTestMasters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "PerformanceTestMasters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StandardCode",
                table: "PerformanceTestMasters",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceTestMasters_ProductType_IsActive",
                table: "PerformanceTestMasters",
                columns: new[] { "ProductType", "IsActive" });
        }
    }
}
