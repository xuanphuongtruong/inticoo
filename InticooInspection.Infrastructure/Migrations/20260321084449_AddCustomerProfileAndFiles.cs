using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerProfileAndFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CvUrl",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CvUrl",
                table: "AspNetUsers");
        }
    }
}
