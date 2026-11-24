using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDefectSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerSignatureUrl",
                table: "DefectCapas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerSignedAt",
                table: "DefectCapas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerSignedBy",
                table: "DefectCapas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FactoryRepSignatureUrl",
                table: "DefectCapas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FactoryRepSignedAt",
                table: "DefectCapas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FactoryRepSignedBy",
                table: "DefectCapas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QaSignatureUrl",
                table: "DefectCapas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QaSignedAt",
                table: "DefectCapas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QaSignedBy",
                table: "DefectCapas",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerSignatureUrl",
                table: "DefectCapas");

            migrationBuilder.DropColumn(
                name: "CustomerSignedAt",
                table: "DefectCapas");

            migrationBuilder.DropColumn(
                name: "CustomerSignedBy",
                table: "DefectCapas");

            migrationBuilder.DropColumn(
                name: "FactoryRepSignatureUrl",
                table: "DefectCapas");

            migrationBuilder.DropColumn(
                name: "FactoryRepSignedAt",
                table: "DefectCapas");

            migrationBuilder.DropColumn(
                name: "FactoryRepSignedBy",
                table: "DefectCapas");

            migrationBuilder.DropColumn(
                name: "QaSignatureUrl",
                table: "DefectCapas");

            migrationBuilder.DropColumn(
                name: "QaSignedAt",
                table: "DefectCapas");

            migrationBuilder.DropColumn(
                name: "QaSignedBy",
                table: "DefectCapas");
        }
    }
}
