using System.ComponentModel.DataAnnotations;

namespace InticooInspection.Domain.Entities
{
    /// <summary>
    /// Cấu hình mail lưu trong DB để admin có thể đổi qua UI mà KHÔNG cần restart app.
    /// Bảng này chỉ có 1 record duy nhất (Id = 1, singleton pattern).
    /// 
    /// LƯU Ý BẢO MẬT: Password KHÔNG lưu ở đây - vẫn lấy từ Environment Variables Azure.
    /// </summary>
    public class MailConfig
    {
        public int Id { get; set; }   // luôn = 1

        // ── SMTP server ──
        [MaxLength(200)] public string  SmtpHost    { get; set; } = "smtp.office365.com";
        public int     SmtpPort    { get; set; } = 587;
        public bool    UseSsl      { get; set; } = true;

        // ── Sender info ──
        [MaxLength(200)] public string  SenderEmail { get; set; } = "no-reply@inticoo.com";
        [MaxLength(100)] public string  SenderName  { get; set; } = "Inticoo Inspection";
        [MaxLength(200)] public string  Username    { get; set; } = "no-reply@inticoo.com";

        // ── Lịch gửi tự động ──
        public int  LookAheadDays    { get; set; } = 7;     // số ngày tới sẽ thông báo
        public int  SendDayOfWeek    { get; set; } = 5;     // 0=CN, 1=T2, ..., 5=T6, 6=T7
        public int  SendHour         { get; set; } = 18;    // 0-23 (giờ VN)
        public int  SendMinute       { get; set; } = 0;     // 0-59
        public bool WeeklyJobEnabled { get; set; } = true;

        // ── Audit ──
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [MaxLength(200)] public string? UpdatedBy { get; set; }
    }
}
