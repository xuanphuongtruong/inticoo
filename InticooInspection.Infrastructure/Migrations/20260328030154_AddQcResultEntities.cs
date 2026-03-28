using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQcResultEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionQcAqlResult_Inspections_InspectionId",
                table: "InspectionQcAqlResult");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionQcDefect_Inspections_InspectionId",
                table: "InspectionQcDefect");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionQcQuantityConformity_Inspections_InspectionId",
                table: "InspectionQcQuantityConformity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionQcQuantityConformity",
                table: "InspectionQcQuantityConformity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionQcDefect",
                table: "InspectionQcDefect");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionQcAqlResult",
                table: "InspectionQcAqlResult");

            migrationBuilder.RenameTable(
                name: "InspectionQcQuantityConformity",
                newName: "InspectionQcQuantityConformities");

            migrationBuilder.RenameTable(
                name: "InspectionQcDefect",
                newName: "InspectionQcDefects");

            migrationBuilder.RenameTable(
                name: "InspectionQcAqlResult",
                newName: "InspectionQcAqlResults");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionQcQuantityConformity_InspectionId",
                table: "InspectionQcQuantityConformities",
                newName: "IX_InspectionQcQuantityConformities_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionQcDefect_InspectionId",
                table: "InspectionQcDefects",
                newName: "IX_InspectionQcDefects_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionQcAqlResult_InspectionId",
                table: "InspectionQcAqlResults",
                newName: "IX_InspectionQcAqlResults_InspectionId");

            migrationBuilder.AlterColumn<string>(
                name: "Remark",
                table: "InspectionQcDefects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "InspectionQcDefects",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DefectType",
                table: "InspectionQcDefects",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "MinorAql",
                table: "InspectionQcAqlResults",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "MajorAql",
                table: "InspectionQcAqlResults",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "InspectionLevel",
                table: "InspectionQcAqlResults",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CriticalAql",
                table: "InspectionQcAqlResults",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionQcQuantityConformities",
                table: "InspectionQcQuantityConformities",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionQcDefects",
                table: "InspectionQcDefects",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionQcAqlResults",
                table: "InspectionQcAqlResults",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionQcAqlResults_Inspections_InspectionId",
                table: "InspectionQcAqlResults",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionQcDefects_Inspections_InspectionId",
                table: "InspectionQcDefects",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionQcQuantityConformities_Inspections_InspectionId",
                table: "InspectionQcQuantityConformities",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionQcAqlResults_Inspections_InspectionId",
                table: "InspectionQcAqlResults");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionQcDefects_Inspections_InspectionId",
                table: "InspectionQcDefects");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionQcQuantityConformities_Inspections_InspectionId",
                table: "InspectionQcQuantityConformities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionQcQuantityConformities",
                table: "InspectionQcQuantityConformities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionQcDefects",
                table: "InspectionQcDefects");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InspectionQcAqlResults",
                table: "InspectionQcAqlResults");

            migrationBuilder.RenameTable(
                name: "InspectionQcQuantityConformities",
                newName: "InspectionQcQuantityConformity");

            migrationBuilder.RenameTable(
                name: "InspectionQcDefects",
                newName: "InspectionQcDefect");

            migrationBuilder.RenameTable(
                name: "InspectionQcAqlResults",
                newName: "InspectionQcAqlResult");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionQcQuantityConformities_InspectionId",
                table: "InspectionQcQuantityConformity",
                newName: "IX_InspectionQcQuantityConformity_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionQcDefects_InspectionId",
                table: "InspectionQcDefect",
                newName: "IX_InspectionQcDefect_InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionQcAqlResults_InspectionId",
                table: "InspectionQcAqlResult",
                newName: "IX_InspectionQcAqlResult_InspectionId");

            migrationBuilder.AlterColumn<string>(
                name: "Remark",
                table: "InspectionQcDefect",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "InspectionQcDefect",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DefectType",
                table: "InspectionQcDefect",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "MinorAql",
                table: "InspectionQcAqlResult",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "MajorAql",
                table: "InspectionQcAqlResult",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "InspectionLevel",
                table: "InspectionQcAqlResult",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(5)",
                oldMaxLength: 5);

            migrationBuilder.AlterColumn<string>(
                name: "CriticalAql",
                table: "InspectionQcAqlResult",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionQcQuantityConformity",
                table: "InspectionQcQuantityConformity",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionQcDefect",
                table: "InspectionQcDefect",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InspectionQcAqlResult",
                table: "InspectionQcAqlResult",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionQcAqlResult_Inspections_InspectionId",
                table: "InspectionQcAqlResult",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionQcDefect_Inspections_InspectionId",
                table: "InspectionQcDefect",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionQcQuantityConformity_Inspections_InspectionId",
                table: "InspectionQcQuantityConformity",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
