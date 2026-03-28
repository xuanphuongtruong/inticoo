using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQcResultFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FinalResult",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectorComments",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QcInspectionRef",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QcResultJson",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureUrl",
                table: "Inspections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InspectionQcAqlResult",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    InspectionLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriticalAql = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MajorAql = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinorAql = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriticalSampleSize = table.Column<int>(type: "int", nullable: false),
                    MajorSampleSize = table.Column<int>(type: "int", nullable: false),
                    MinorSampleSize = table.Column<int>(type: "int", nullable: false),
                    CriticalAccept = table.Column<int>(type: "int", nullable: false),
                    MajorAccept = table.Column<int>(type: "int", nullable: false),
                    MinorAccept = table.Column<int>(type: "int", nullable: false),
                    CriticalFound = table.Column<int>(type: "int", nullable: false),
                    MajorFound = table.Column<int>(type: "int", nullable: false),
                    MinorFound = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionQcAqlResult", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionQcAqlResult_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionQcDefect",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    DefectType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionQcDefect", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionQcDefect_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionQcQuantityConformity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionId = table.Column<int>(type: "int", nullable: false),
                    ItemPerCarton = table.Column<int>(type: "int", nullable: false),
                    PresentedPacked = table.Column<int>(type: "int", nullable: false),
                    PresentedNotPacked = table.Column<int>(type: "int", nullable: false),
                    CartonsPacked = table.Column<int>(type: "int", nullable: false),
                    CartonsNotPacked = table.Column<int>(type: "int", nullable: false),
                    QtyNotFinished = table.Column<int>(type: "int", nullable: false),
                    PhotosJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionQcQuantityConformity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionQcQuantityConformity_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionQcAqlResult_InspectionId",
                table: "InspectionQcAqlResult",
                column: "InspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionQcDefect_InspectionId",
                table: "InspectionQcDefect",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionQcQuantityConformity_InspectionId",
                table: "InspectionQcQuantityConformity",
                column: "InspectionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionQcAqlResult");

            migrationBuilder.DropTable(
                name: "InspectionQcDefect");

            migrationBuilder.DropTable(
                name: "InspectionQcQuantityConformity");

            migrationBuilder.DropColumn(
                name: "FinalResult",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "InspectorComments",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "QcInspectionRef",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "QcResultJson",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "SignatureUrl",
                table: "Inspections");
        }
    }
}
