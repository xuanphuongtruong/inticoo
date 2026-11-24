using System.ComponentModel.DataAnnotations;

namespace InticooInspection.Domain.Entities
{
    /// <summary>
    /// Cài đặt toàn hệ thống theo từng nhóm (inspection-defect, system, billing).
    /// Mỗi nhóm một dòng, nội dung lưu dạng JSON để thêm trường mới không cần migration.
    /// </summary>
    public class AppSetting
    {
        [Key, MaxLength(64)] public string Section { get; set; } = "";
        public string Json { get; set; } = "{}";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [MaxLength(200)] public string? UpdatedBy { get; set; }
    }
}
