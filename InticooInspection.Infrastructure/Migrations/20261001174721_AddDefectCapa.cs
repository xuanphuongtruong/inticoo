using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDefectCapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DefectCapas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DefectNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CarNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InspectionId = table.Column<int>(type: "int", nullable: true),
                    JobNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PoNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InspectorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefectCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DefectCategory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefectType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    InspectionArea = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AqlClassification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductQtyChecked = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationX = table.Column<double>(type: "float", nullable: true),
                    LocationY = table.Column<double>(type: "float", nullable: true),
                    LocationLabel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhotosJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhotoNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterPhotosJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RootCauseType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinalRootCause = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Why1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Why2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Why3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Why4 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Why5 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsibleDepartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsiblePerson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrectiveActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreventiveActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CapaRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectivenessResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerificationComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferencesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FactoryRepName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QaVerifierName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerApproverName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpenDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CloseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AutoCreated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefectCapas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DefectCapas_DefectNo",
                table: "DefectCapas",
                column: "DefectNo");

            migrationBuilder.CreateIndex(
                name: "IX_DefectCapas_InspectionId",
                table: "DefectCapas",
                column: "InspectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DefectCapas");
        }
    }
}
