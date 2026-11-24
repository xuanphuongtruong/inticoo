using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Portal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyUserUserName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserName",
                schema: "sup",
                table: "CompanyUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUsers_UserName",
                schema: "sup",
                table: "CompanyUsers",
                column: "UserName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyUsers_UserName",
                schema: "sup",
                table: "CompanyUsers");

            migrationBuilder.DropColumn(
                name: "UserName",
                schema: "sup",
                table: "CompanyUsers");
        }
    }
}
