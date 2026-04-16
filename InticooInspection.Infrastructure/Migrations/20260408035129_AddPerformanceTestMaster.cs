using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceTestMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerformanceTestMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StandardCode = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    StandardName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProductType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Market = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProtocolName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Requirements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceTestMasters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceTestMasters_Category_IsActive",
                table: "PerformanceTestMasters",
                columns: new[] { "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceTestMasters_ProductType_IsActive",
                table: "PerformanceTestMasters",
                columns: new[] { "ProductType", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerformanceTestMasters");
        }
    }
}
