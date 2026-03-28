using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionFullSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionColourSwatch_Inspections_InspectionId",
                table: "InspectionColourSwatch");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionPackaging_Inspections_InspectionId",
                table: "InspectionPackaging");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionPerformanceTest_Inspections_InspectionId",
                table: "InspectionPerformanceTest");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionProductSpec_Inspections_InspectionId",
                table: "InspectionProductSpec");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionReference_Inspections_InspectionId",
                table: "InspectionReference");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionReference",
                table: "InspectionReference");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionProductSpec",
                table: "InspectionProductSpec");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionPerformanceTest",
                table: "InspectionPerformanceTest");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionPackaging",
                table: "InspectionPackaging");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionColourSwatch",
                table: "InspectionColourSwatch");

            migrationBuilder.RenameTable(
                name: "InspectionReference",
                newName: "InspectionReferences");

            migrationBuilder.RenameTable(
                name: "InspectionProductSpec",
                newName: "InspectionProductSpecs");

            migrationBuilder.RenameTable(
                name: "InspectionPerformanceTest",
                newName: "InspectionPerformanceTests");

            migrationBuilder.RenameTable(
                name: "InspectionPackaging",
                newName: "InspectionPackagings");

            migrationBuilder.RenameTable(
                name: "InspectionColourSwatch",
                newName: "InspectionColourSwatches");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionReference_InspectionId",
                table: "InspectionReferences",
                newName: "IX_InspectionReferences_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionProductSpec_InspectionId",
                table: "InspectionProductSpecs",
                newName: "IX_InspectionProductSpecs_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionPerformanceTest_InspectionId",
                table: "InspectionPerformanceTests",
                newName: "IX_InspectionPerformanceTests_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionPackaging_InspectionId",
                table: "InspectionPackagings",
                newName: "IX_InspectionPackagings_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionColourSwatch_InspectionId",
                table: "InspectionColourSwatches",
                newName: "IX_InspectionColourSwatches_InspectionId");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Inspections",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Remark",
                table: "InspectionReferences",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceName",
                table: "InspectionReferences",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "InspectionReferences",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "InspectionReferences",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TestRequirement",
                table: "InspectionPerformanceTests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TestItem",
                table: "InspectionPerformanceTests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Remark",
                table: "InspectionPerformanceTests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "InspectionPerformanceTests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Material",
                table: "InspectionColourSwatches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionReferences",
                table: "InspectionReferences",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionProductSpecs",
                table: "InspectionProductSpecs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionPerformanceTests",
                table: "InspectionPerformanceTests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionPackagings",
                table: "InspectionPackagings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionColourSwatches",
                table: "InspectionColourSwatches",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "InspectionOverallConclusions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Letter = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Compliance = table.Column<int>(type: "int", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionOverallConclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionOverallConclusions_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionOverallConclusions_InspectionId",
                table: "InspectionOverallConclusions",
                column: "InspectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionColourSwatches_Inspections_InspectionId",
                table: "InspectionColourSwatches",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionPackagings_Inspections_InspectionId",
                table: "InspectionPackagings",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionPerformanceTests_Inspections_InspectionId",
                table: "InspectionPerformanceTests",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionProductSpecs_Inspections_InspectionId",
                table: "InspectionProductSpecs",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionReferences_Inspections_InspectionId",
                table: "InspectionReferences",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionColourSwatches_Inspections_InspectionId",
                table: "InspectionColourSwatches");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionPackagings_Inspections_InspectionId",
                table: "InspectionPackagings");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionPerformanceTests_Inspections_InspectionId",
                table: "InspectionPerformanceTests");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionProductSpecs_Inspections_InspectionId",
                table: "InspectionProductSpecs");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionReferences_Inspections_InspectionId",
                table: "InspectionReferences");

            migrationBuilder.DropTable(
                name: "InspectionOverallConclusions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionReferences",
                table: "InspectionReferences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionProductSpecs",
                table: "InspectionProductSpecs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionPerformanceTests",
                table: "InspectionPerformanceTests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionPackagings",
                table: "InspectionPackagings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionColourSwatches",
                table: "InspectionColourSwatches");

            migrationBuilder.RenameTable(
                name: "InspectionReferences",
                newName: "InspectionReference");

            migrationBuilder.RenameTable(
                name: "InspectionProductSpecs",
                newName: "InspectionProductSpec");

            migrationBuilder.RenameTable(
                name: "InspectionPerformanceTests",
                newName: "InspectionPerformanceTest");

            migrationBuilder.RenameTable(
                name: "InspectionPackagings",
                newName: "InspectionPackaging");

            migrationBuilder.RenameTable(
                name: "InspectionColourSwatches",
                newName: "InspectionColourSwatch");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionReferences_InspectionId",
                table: "InspectionReference",
                newName: "IX_InspectionReference_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionProductSpecs_InspectionId",
                table: "InspectionProductSpec",
                newName: "IX_InspectionProductSpec_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionPerformanceTests_InspectionId",
                table: "InspectionPerformanceTest",
                newName: "IX_InspectionPerformanceTest_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionPackagings_InspectionId",
                table: "InspectionPackaging",
                newName: "IX_InspectionPackaging_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionColourSwatches_InspectionId",
                table: "InspectionColourSwatch",
                newName: "IX_InspectionColourSwatch_InspectionId");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Inspections",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Remark",
                table: "InspectionReference",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceName",
                table: "InspectionReference",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "InspectionReference",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "InspectionReference",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TestRequirement",
                table: "InspectionPerformanceTest",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TestItem",
                table: "InspectionPerformanceTest",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Remark",
                table: "InspectionPerformanceTest",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "InspectionPerformanceTest",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Material",
                table: "InspectionColourSwatch",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionReference",
                table: "InspectionReference",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionProductSpec",
                table: "InspectionProductSpec",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionPerformanceTest",
                table: "InspectionPerformanceTest",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionPackaging",
                table: "InspectionPackaging",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionColourSwatch",
                table: "InspectionColourSwatch",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionColourSwatch_Inspections_InspectionId",
                table: "InspectionColourSwatch",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionPackaging_Inspections_InspectionId",
                table: "InspectionPackaging",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionPerformanceTest_Inspections_InspectionId",
                table: "InspectionPerformanceTest",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionProductSpec_Inspections_InspectionId",
                table: "InspectionProductSpec",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionReference_Inspections_InspectionId",
                table: "InspectionReference",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
