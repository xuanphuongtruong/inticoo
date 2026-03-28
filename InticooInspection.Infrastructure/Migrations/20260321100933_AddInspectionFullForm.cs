using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionFullForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AqlInspectionLevel",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CriticalAccept",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CriticalAql",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CriticalReject",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CriticalSampleSize",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CustomerId",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionDate",
                table: "Inspections",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "InspectionLocation",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InspectionReference",
                table: "Inspections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "InspectionType",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InspectorId",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectorName",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemNumber",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobNumber",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MajorAccept",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MajorAql",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MajorReject",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MajorSampleSize",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinorAccept",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinorAql",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinorReject",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinorSampleSize",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfficeDate",
                table: "Inspections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Photo1Url",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Photo2Url",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoNumber",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductCategory",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TotalCartonBoxes",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalShipmentQty",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VendorId",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorName",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "InspectionColourSwatch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Material = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionColourSwatch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionColourSwatch_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionPackaging",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionId = table.Column<int>(type: "int", nullable: false),
                    ItemNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CartonNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PackagingType = table.Column<int>(type: "int", nullable: true),
                    CartonColor = table.Column<int>(type: "int", nullable: true),
                    CardboardType = table.Column<int>(type: "int", nullable: true),
                    ShippingMark = table.Column<int>(type: "int", nullable: true),
                    HasBarcode = table.Column<bool>(type: "bit", nullable: false),
                    InnerPackingQty = table.Column<int>(type: "int", nullable: true),
                    InnerPackingRemark = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OuterSizeL = table.Column<double>(type: "float", nullable: true),
                    OuterSizeW = table.Column<double>(type: "float", nullable: true),
                    OuterSizeH = table.Column<double>(type: "float", nullable: true),
                    OuterWeight = table.Column<double>(type: "float", nullable: true),
                    AssemblyInstruction = table.Column<bool>(type: "bit", nullable: false),
                    Hardware = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPackaging", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionPackaging_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionPerformanceTest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TestItem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TestRequirement = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPerformanceTest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionPerformanceTest_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionProductSpec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionId = table.Column<int>(type: "int", nullable: false),
                    SizeL = table.Column<double>(type: "float", nullable: true),
                    SizeW = table.Column<double>(type: "float", nullable: true),
                    SizeH = table.Column<double>(type: "float", nullable: true),
                    Weight = table.Column<double>(type: "float", nullable: true),
                    CompareGoldenSample = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionProductSpec", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionProductSpec_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionReference",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    ReferenceName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionReference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionReference_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionColourSwatch_InspectionId",
                table: "InspectionColourSwatch",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPackaging_InspectionId",
                table: "InspectionPackaging",
                column: "InspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPerformanceTest_InspectionId",
                table: "InspectionPerformanceTest",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionProductSpec_InspectionId",
                table: "InspectionProductSpec",
                column: "InspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionReference_InspectionId",
                table: "InspectionReference",
                column: "InspectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionColourSwatch");

            migrationBuilder.DropTable(
                name: "InspectionPackaging");

            migrationBuilder.DropTable(
                name: "InspectionPerformanceTest");

            migrationBuilder.DropTable(
                name: "InspectionProductSpec");

            migrationBuilder.DropTable(
                name: "InspectionReference");

            migrationBuilder.DropColumn(
                name: "AqlInspectionLevel",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "CriticalAccept",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "CriticalAql",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "CriticalReject",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "CriticalSampleSize",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "InspectionDate",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "InspectionLocation",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "InspectionReference",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "InspectionType",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "InspectorId",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "InspectorName",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "ItemNumber",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "JobNumber",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "MajorAccept",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "MajorAql",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "MajorReject",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "MajorSampleSize",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "MinorAccept",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "MinorAql",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "MinorReject",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "MinorSampleSize",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "OfficeDate",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "Photo1Url",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "Photo2Url",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "PoNumber",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "ProductCategory",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "TotalCartonBoxes",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "TotalShipmentQty",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "VendorName",
                table: "Inspections");
        }
    }
}
