using System.Text.Json;
using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Services
{
    /// <summary>Đọc cài đặt đã lưu ở màn Settings để các nghiệp vụ dùng (hiện: ngưỡng tự mở CAPA).</summary>
    public static class SettingsStore
    {
        public const string InspectionDefect = "inspection-defect";
        public const string SystemSection = "system";
        public const string Billing = "billing";

        /// <summary>Ngưỡng mở CAPA; mặc định Critical ≥ 1 · Major ≥ 3 · Minor ≥ 10, bật.</summary>
        public record CapaRules(bool Enabled, int Critical, int Major, int Minor)
        {
            public static readonly CapaRules Default = new(true, 1, 3, 10);

            public int For(string severity) => severity switch
            {
                "Critical" => Critical,
                "Major"    => Major,
                _          => Minor
            };
        }

        public static async Task<CapaRules> GetCapaRulesAsync(AppDbContext db)
        {
            try
            {
                var json = await db.AppSettings.AsNoTracking()
                    .Where(x => x.Section == InspectionDefect).Select(x => x.Json).FirstOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(json)) return CapaRules.Default;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                bool enabled = !root.TryGetProperty("capaEnabled", out var en) || en.ValueKind != JsonValueKind.False;
                int Read(string name, int fallback) =>
                    root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= 1 ? n : fallback;
                return new CapaRules(enabled, Read("capaCritical", 1), Read("capaMajor", 3), Read("capaMinor", 10));
            }
            catch
            {
                return CapaRules.Default;
            }
        }
    }
}
