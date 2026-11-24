using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Portal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanColorModuleIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                schema: "sup",
                table: "Plans",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                schema: "sup",
                table: "Modules",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // Dữ liệu có sẵn: giữ đúng màu/icon trước đây vốn viết cứng theo mã trong Super Admin
            migrationBuilder.Sql(@"
UPDATE sup.Plans SET Color = CASE Code WHEN 'basic' THEN '#2563eb' WHEN 'business' THEN '#10b981' WHEN 'professional' THEN '#8b5cf6' END WHERE Color IS NULL;
UPDATE sup.Modules SET Icon = CASE Code WHEN 'inspection' THEN 'clipboard' WHEN 'capa' THEN 'shield' WHEN 'analytics' THEN 'chart' WHEN 'clc' THEN 'container' ELSE 'link' END WHERE Icon IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                schema: "sup",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Icon",
                schema: "sup",
                table: "Modules");
        }
    }
}
