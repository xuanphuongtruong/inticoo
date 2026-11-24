using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDefectSignerPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerPosition",
                table: "DefectCapas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FactoryRepPosition",
                table: "DefectCapas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QaPosition",
                table: "DefectCapas",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerPosition",
                table: "DefectCapas");

            migrationBuilder.DropColumn(
                name: "FactoryRepPosition",
                table: "DefectCapas");

            migrationBuilder.DropColumn(
                name: "QaPosition",
                table: "DefectCapas");
        }
    }
}
