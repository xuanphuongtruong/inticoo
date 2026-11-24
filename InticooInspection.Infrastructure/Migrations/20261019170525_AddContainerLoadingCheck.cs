using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContainerLoadingCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContainerChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerContact = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CustomerPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VendorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VendorContact = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    VendorPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VendorEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VendorAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContainerNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ContainerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PoNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShipmentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PlannedLoadingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedStartTime = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PlannedEndTime = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LoadingMethod = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    TruckNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LoadingLocation = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    LoadingAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CargoDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InspectorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    InspectorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InspectionStartTime = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InspectionEndTime = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    JobRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SevenPointResult = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AdditionalResult = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LoadingResult = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SealResult = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    OverallResult = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CheckClean = table.Column<bool>(type: "bit", nullable: true),
                    CheckNoMoisture = table.Column<bool>(type: "bit", nullable: true),
                    CheckNoOdor = table.Column<bool>(type: "bit", nullable: true),
                    CheckSuitable = table.Column<bool>(type: "bit", nullable: true),
                    CheckDoorSealable = table.Column<bool>(type: "bit", nullable: true),
                    AdditionalRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SealNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SealCondition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SealAppliedBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SealPhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SealRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LoadingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LoadingStartTime = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LoadingEndTime = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LoadingMethodActual = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CartonCondition = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MarkingLabel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PackagingProtection = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PalletCondition = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LoadingRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FinalRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InspectorSignatureUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InspectorSignName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    InspectorSignPosition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspectorSignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewerSignatureUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewerSignName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReviewerSignPosition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReviewerSignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerChecks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContainerCheckPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Slot = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerCheckPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContainerCheckPhotos_ContainerChecks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "ContainerChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContainerCheckPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckId = table.Column<int>(type: "int", nullable: false),
                    PointNo = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Findings = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PhotosJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerCheckPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContainerCheckPoints_ContainerChecks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "ContainerChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContainerCheckProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PlannedCartons = table.Column<int>(type: "int", nullable: false),
                    PlannedQty = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ActualCartons = table.Column<int>(type: "int", nullable: true),
                    ActualQty = table.Column<int>(type: "int", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerCheckProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContainerCheckProducts_ContainerChecks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "ContainerChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerCheckPhotos_CheckId",
                table: "ContainerCheckPhotos",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerCheckPoints_CheckId_PointNo",
                table: "ContainerCheckPoints",
                columns: new[] { "CheckId", "PointNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContainerCheckProducts_CheckId",
                table: "ContainerCheckProducts",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerChecks_ContainerNo",
                table: "ContainerChecks",
                column: "ContainerNo");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerChecks_InspectionDate",
                table: "ContainerChecks",
                column: "InspectionDate");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerChecks_JobNumber",
                table: "ContainerChecks",
                column: "JobNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContainerChecks_Status",
                table: "ContainerChecks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContainerCheckPhotos");

            migrationBuilder.DropTable(
                name: "ContainerCheckPoints");

            migrationBuilder.DropTable(
                name: "ContainerCheckProducts");

            migrationBuilder.DropTable(
                name: "ContainerChecks");
        }
    }
}
