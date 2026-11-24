using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Portal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Features",
                schema: "sup",
                table: "Plans",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActiveUserCount",
                schema: "sup",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "DbSizeGb",
                schema: "sup",
                table: "Companies",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "FileCount",
                schema: "sup",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "FileSizeGb",
                schema: "sup",
                table: "Companies",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                schema: "sup",
                table: "Companies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LimitOverrideUntil",
                schema: "sup",
                table: "Companies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserCount",
                schema: "sup",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CompanyUsageSnapshots",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DbDataMb = table.Column<double>(type: "float", nullable: false),
                    DbLogMb = table.Column<double>(type: "float", nullable: false),
                    FilesMb = table.Column<double>(type: "float", nullable: false),
                    FileCount = table.Column<int>(type: "int", nullable: false),
                    Users = table.Column<int>(type: "int", nullable: false),
                    ActiveUsers = table.Column<int>(type: "int", nullable: false),
                    LockedUsers = table.Column<int>(type: "int", nullable: false),
                    ActiveUsers7d = table.Column<int>(type: "int", nullable: false),
                    ActiveUsers30d = table.Column<int>(type: "int", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Inspections = table.Column<int>(type: "int", nullable: false),
                    InspectionsThisMonth = table.Column<int>(type: "int", nullable: false),
                    ContainerChecks = table.Column<int>(type: "int", nullable: false),
                    OpenCapa = table.Column<int>(type: "int", nullable: false),
                    Vendors = table.Column<int>(type: "int", nullable: false),
                    Products = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyUsageSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyUsageSnapshots_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "sup",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlatformDiskSnapshots",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Drive = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalGb = table.Column<double>(type: "float", nullable: false),
                    FreeGb = table.Column<double>(type: "float", nullable: false),
                    UploadsGb = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformDiskSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUsageSnapshots_At",
                schema: "sup",
                table: "CompanyUsageSnapshots",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUsageSnapshots_CompanyId_At",
                schema: "sup",
                table: "CompanyUsageSnapshots",
                columns: new[] { "CompanyId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformDiskSnapshots_At",
                schema: "sup",
                table: "PlatformDiskSnapshots",
                column: "At");

            // Số đo cũ chỉ là kích thước DB → chuyển sang cột DbSizeGb
            migrationBuilder.Sql("UPDATE sup.Companies SET DbSizeGb = StorageUsedGb WHERE DbSizeGb = 0 AND StorageUsedGb > 0;");

            // Nội dung thẻ gói trên website theo thiết kế Website.docx (chạy 1 lần; Super Admin sửa được ở Settings › Subscription Plans).
            // {users} / {storage} được thay bằng MaxUsers / StorageGb của gói; dòng bắt đầu bằng * được tô nổi bật.
            void Features(string code, params string[] lines) => migrationBuilder.Sql(
                $"UPDATE sup.Plans SET Features = N'{string.Join("\n", lines).Replace("'", "''")}' WHERE Code = '{code}' AND Features IS NULL;");
            Features("basic", "Digital inspection forms (PPI, DPI, PST)", "Inspection scheduling", "Photo and PDF reports", "Basic dashboard and analytics",
                "Up to {users} users", "{storage} GB cloud storage", "Multi-language (EN/CN/VI/MS)", "Email support");
            Features("business", "Everything in Basic", "CAPA management", "Supplier management", "Advanced analytics and reports",
                "Up to {users} users", "{storage} GB cloud storage", "Multi-language (EN/CN/VI/MS)", "Priority email support");
            Features("professional", "Everything in Business", "*Loading Container Check (CLC)", "Advanced reporting", "Custom inspection templates",
                "API integration (upon request)", "Up to {users} users", "{storage} GB cloud storage", "Multi-language (EN/CN/VI/MS)", "Dedicated account manager");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyUsageSnapshots",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "PlatformDiskSnapshots",
                schema: "sup");

            migrationBuilder.DropColumn(
                name: "Features",
                schema: "sup",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "ActiveUserCount",
                schema: "sup",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "DbSizeGb",
                schema: "sup",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "FileCount",
                schema: "sup",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "FileSizeGb",
                schema: "sup",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                schema: "sup",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LimitOverrideUntil",
                schema: "sup",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "UserCount",
                schema: "sup",
                table: "Companies");
        }
    }
}
