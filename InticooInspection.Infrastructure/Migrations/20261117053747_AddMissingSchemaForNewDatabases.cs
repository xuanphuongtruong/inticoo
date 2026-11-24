using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InticooInspection.Infrastructure.Migrations
{
    /// <summary>
    /// Bù phần schema đã có trong model (snapshot) nhưng chưa từng có migration tạo ra: DB gốc được thêm tay
    /// nên chạy bình thường, còn DB mới (DB riêng của công ty khách do Portal cấp) thì thiếu → lỗi "Invalid column name".
    /// Mọi lệnh đều kiểm tra tồn tại trước nên chạy trên DB gốc không thay đổi gì.
    /// </summary>
    public partial class AddMissingSchemaForNewDatabases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Customers', 'AlternateReportEmail') IS NULL
    ALTER TABLE dbo.Customers ADD AlternateReportEmail nvarchar(max) NULL;
IF COL_LENGTH('dbo.Customers', 'ReceiveInspectionReport') IS NULL
    ALTER TABLE dbo.Customers ADD ReceiveInspectionReport bit NOT NULL CONSTRAINT DF_Customers_ReceiveInspectionReport DEFAULT(0);
IF COL_LENGTH('dbo.Customers', 'ReportEmailType') IS NULL
    ALTER TABLE dbo.Customers ADD ReportEmailType nvarchar(max) NULL;

IF OBJECT_ID(N'dbo.MailConfigs', N'U') IS NULL
CREATE TABLE dbo.MailConfigs (
    Id               int NOT NULL CONSTRAINT PK_MailConfigs PRIMARY KEY,   -- giống DB gốc: không IDENTITY, code luôn ghi Id = 1
    SmtpHost         nvarchar(200) NOT NULL,
    SmtpPort         int NOT NULL,
    UseSsl           bit NOT NULL,
    SenderEmail      nvarchar(200) NOT NULL,
    SenderName       nvarchar(100) NOT NULL,
    Username         nvarchar(200) NOT NULL,
    LookAheadDays    int NOT NULL,
    SendDayOfWeek    int NOT NULL,
    SendHour         int NOT NULL,
    SendMinute       int NOT NULL,
    WeeklyJobEnabled bit NOT NULL,
    UpdatedAt        datetime2 NOT NULL,
    UpdatedBy        nvarchar(200) NULL
);

IF OBJECT_ID(N'dbo.MailLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MailLogs (
        Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MailLogs PRIMARY KEY,
        VendorId        int NULL,
        VendorCode      nvarchar(50) NULL,
        ToEmail         nvarchar(200) NOT NULL,
        Subject         nvarchar(300) NOT NULL,
        InspectionCount int NOT NULL,
        IsSuccess       bit NOT NULL,
        ErrorMessage    nvarchar(2000) NULL,
        SentAt          datetime2 NOT NULL
    );
    CREATE INDEX IX_MailLogs_SentAt ON dbo.MailLogs (SentAt);
    CREATE INDEX IX_MailLogs_VendorId ON dbo.MailLogs (VendorId);
END

IF OBJECT_ID(N'dbo.PerformanceTestReferences', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PerformanceTestReferences (
        Id                      int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PerformanceTestReferences PRIMARY KEY,
        PerformanceTestMasterId int NOT NULL,
        FileName                nvarchar(500) NOT NULL,
        ContentType             nvarchar(200) NOT NULL CONSTRAINT DF_PerfTestRef_ContentType DEFAULT(N'application/octet-stream'),
        FileSize                bigint NOT NULL,
        FileData                varbinary(max) NOT NULL,
        SortOrder               int NOT NULL CONSTRAINT DF_PerfTestRef_SortOrder DEFAULT(0),
        UploadedAt              datetime2 NOT NULL CONSTRAINT DF_PerfTestRef_UploadedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_PerformanceTestReferences_PerformanceTestMasters_PerformanceTestMasterId
            FOREIGN KEY (PerformanceTestMasterId) REFERENCES dbo.PerformanceTestMasters (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_PerfTestRef_MasterId ON dbo.PerformanceTestReferences (PerformanceTestMasterId);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cố ý không xóa: trên DB gốc các bảng/cột này có từ trước migration (chứa dữ liệu thật).
        }
    }
}
