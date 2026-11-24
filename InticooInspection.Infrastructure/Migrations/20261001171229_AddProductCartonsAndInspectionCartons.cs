using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCartonsAndInspectionCartons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CartonQuantity",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BurstingTest",
                table: "InspectionPackagings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InspectionCartons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionId = table.Column<int>(type: "int", nullable: false),
                    CartonIndex = table.Column<int>(type: "int", nullable: false),
                    CartonVariant = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PackingQuantity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InnerPackingQty = table.Column<int>(type: "int", nullable: true),
                    InnerSizeL = table.Column<double>(type: "float", nullable: true),
                    InnerSizeW = table.Column<double>(type: "float", nullable: true),
                    InnerSizeH = table.Column<double>(type: "float", nullable: true),
                    InnerWeight = table.Column<double>(type: "float", nullable: true),
                    OuterSizeL = table.Column<double>(type: "float", nullable: true),
                    OuterSizeW = table.Column<double>(type: "float", nullable: true),
                    OuterSizeH = table.Column<double>(type: "float", nullable: true),
                    OuterWeight = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionCartons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionCartons_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductCartons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CartonVariant = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PackagingQuantity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SizeL = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    SizeW = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    SizeH = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(10,3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCartons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductCartons_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionCartons_InspectionId",
                table: "InspectionCartons",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCartons_ProductId",
                table: "ProductCartons",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionCartons");

            migrationBuilder.DropTable(
                name: "ProductCartons");

            migrationBuilder.DropColumn(
                name: "CartonQuantity",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BurstingTest",
                table: "InspectionPackagings");
        }
    }
}
