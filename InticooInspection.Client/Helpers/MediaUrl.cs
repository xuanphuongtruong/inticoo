using System;
using System.Linq;

namespace InticooInspection.Client.Helpers
{
    /// <summary>
    /// Chuẩn hóa URL ảnh/file để hiển thị trên client.
    /// Quy ước:
    ///  - rỗng/null               → ""
    ///  - data: (base64 inline)   → giữ nguyên
    ///  - URL tuyệt đối trỏ host Azure cũ (azurewebsites.net / blob.core.windows.net)
    ///                            → đổi host sang API mới (giữ nguyên path + query)
    ///  - URL tuyệt đối khác       → giữ nguyên
    ///  - đường dẫn tương đối      → ghép với API base (apiBase)
    /// </summary>
    public static class MediaUrl
    {
        // Các host lưu trữ Azure cũ cần chuyển về server/API mới.
        private static readonly string[] LegacyHosts =
        {
            "azurewebsites.net",
            "blob.core.windows.net"
        };

        /// <summary>
        /// Nếu <paramref name="url"/> là URL tuyệt đối trỏ về host Azure cũ thì đổi
        /// sang <paramref name="apiBase"/> (giữ nguyên path + query). Trường hợp khác giữ nguyên.
        /// </summary>
        public static string RewriteLegacyHost(string url, string? apiBase)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var abs) &&
                LegacyHosts.Any(h => abs.Host.EndsWith(h, StringComparison.OrdinalIgnoreCase)))
            {
                return $"{(apiBase ?? string.Empty).TrimEnd('/')}{abs.PathAndQuery}";
            }
            return url;
        }

        /// <summary>
        /// Chuẩn hóa URL ảnh/file để hiển thị (xem mô tả class).
        /// </summary>
        public static string Resolve(string? url, string? apiBase)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            url = url.Trim();
            var baseUrl = (apiBase ?? string.Empty).TrimEnd('/');

            // base64 inline → giữ nguyên
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return url;

            // URL tuyệt đối → đổi host Azure cũ nếu có, còn lại giữ nguyên
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return RewriteLegacyHost(url, baseUrl);

            // Đường dẫn tương đối → ghép API base
            return $"{baseUrl}/{url.TrimStart('/')}";
        }
    }
}
