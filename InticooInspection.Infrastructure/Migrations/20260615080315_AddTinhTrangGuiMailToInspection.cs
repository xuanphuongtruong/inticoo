using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTinhTrangGuiMailToInspection : Migration
    {
        // LƯU Ý: Migration được EF sinh kèm một số thao tác "drift" có sẵn của model
        // (các bảng MailConfigs/MailLogs/PerformanceTestReferences và cột email của Customers)
        // do snapshot trước đó lệch với DB thực tế. Các đối tượng này ĐÃ tồn tại trên DB
        // production nên ở đây CHỈ giữ lại thao tác thêm cột TinhTrangGuiMail để tránh lỗi
        // "object already exists" khi MigrateAsync() chạy lúc khởi động.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TinhTrangGuiMail",
                table: "Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill: đánh dấu MỌI inspection hiện có là đã gửi (cờ = 1) để
            // InspectionDoneMailWorker không gửi loạt mail cho dữ liệu lịch sử.
            // Chỉ inspection hoàn thành SAU khi triển khai (cờ mặc định = 0) mới được job xử lý.
            migrationBuilder.Sql("UPDATE [Inspections] SET [TinhTrangGuiMail] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TinhTrangGuiMail",
                table: "Inspections");
        }
    }
}
