using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace InticooInspection.Client.Services
{
    /// <summary>
    /// Đa ngôn ngữ dạng JSON: wwwroot/i18n/{code}.json (key phẳng "Nhom.Ten": "Chuỗi").
    /// - Từ điển nạp 1 lần rồi giữ trong RAM → đổi ngôn ngữ tức thì, không reload trang.
    /// - Ngôn ngữ chọn ở màn Login, lưu localStorage "app_lang"; các màn sau đọc lại.
    /// - Thiếu key: fallback sang tiếng Anh, rồi tới chính key.
    /// Thêm ngôn ngữ mới: tạo file i18n/{code}.json + thêm 1 dòng vào <see cref="Supported"/>.
    /// KHÔNG đổi CultureInfo: WASM báo lỗi khi đổi culture lúc khởi động (cần BlazorWebAssemblyLoadAllGlobalizationData,
    /// tải nặng thêm) và để không ảnh hưởng parse/định dạng số, ngày hiện có. Culture trong LanguageInfo để dùng khi cần.
    /// </summary>
    public class LanguageService
    {
        public const string StorageKey = "app_lang";
        public const string DefaultCode = "en";

        public record LanguageInfo(string Code, string Name, string Culture);

        public static readonly IReadOnlyList<LanguageInfo> Supported = new List<LanguageInfo>
        {
            new("en", "English",       "en-US"),
            new("vi", "Tiếng Việt",    "vi-VN"),
            new("zh", "中文",          "zh-CN"),
            new("ms", "Bahasa Melayu", "ms-MY"),
        };

        // Đổi theo mỗi lần build → tránh trình duyệt dùng file JSON cũ sau khi deploy
        private static readonly string BuildStamp =
            typeof(LanguageService).Assembly.ManifestModule.ModuleVersionId.ToString("N")[..8];

        private readonly HttpClient _http;   // trỏ về host của Client (không phải API)
        private readonly IJSRuntime _js;
        private readonly Dictionary<string, Dictionary<string, string>> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<Dictionary<string, string>>> _loading = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, string> _current = new();
        private Dictionary<string, string> _fallback = new();

        public string CurrentCode { get; private set; } = DefaultCode;
        public LanguageInfo Current => Supported.FirstOrDefault(l => l.Code == CurrentCode) ?? Supported[0];

        /// <summary>Phát ra sau khi đổi ngôn ngữ — component đang hiển thị tự render lại.</summary>
        public event Action? OnChanged;

        /// <summary>Instance duy nhất (Singleton) — cho helper tĩnh / file .cs không inject được.</summary>
        public static LanguageService? Instance { get; private set; }

        /// <summary>Dịch khi chỉ có ngữ cảnh tĩnh; chưa khởi tạo thì trả fallback (thường là chữ tiếng Anh).</summary>
        public static string Tr(string key, string fallback) => Instance?.Has(key) == true ? Instance.Get(key) : fallback;

        public LanguageService(IWebAssemblyHostEnvironment env, IJSRuntime js)
        {
            _http = new HttpClient { BaseAddress = new Uri(env.BaseAddress) };
            _js = js;
            Instance = this;
        }

        /// <summary>Gọi 1 lần trong Program.cs trước RunAsync → lần render đầu đã đúng ngôn ngữ.</summary>
        public async Task InitializeAsync()
        {
            string? code = null;
            try { code = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey); } catch { }
            code = Normalize(code);

            _fallback = await GetDictionaryAsync(DefaultCode);
            await ApplyAsync(code, persist: false, notify: false);

            // Nạp ngầm các ngôn ngữ còn lại → lần đổi sau không phải chờ mạng
            foreach (var l in Supported.Where(l => l.Code != code))
                _ = GetDictionaryAsync(l.Code);
        }

        public async Task SetLanguageAsync(string code)
        {
            code = Normalize(code);
            if (code == CurrentCode && _current.Count > 0) return;
            await ApplyAsync(code, persist: true, notify: true);
        }

        /// <summary>Lấy chuỗi theo key; args dùng cho placeholder {0}, {1}...</summary>
        public string this[string key] => Get(key);
        public string this[string key, params object?[] args] => Get(key, args);

        public string Get(string key, params object?[] args)
        {
            if (!TryGet(key, out var text))
            {
#if DEBUG
                Console.WriteLine($"[i18n] Missing key '{key}' ({CurrentCode})");
#endif
                text = key;
            }
            if (args is { Length: > 0 })
            {
                try { return string.Format(text, args); } catch (FormatException) { }
            }
            return text;
        }

        public bool Has(string key) => TryGet(key, out _);

        // Chuỗi rỗng trong file ngôn ngữ = chưa dịch → dùng bản tiếng Anh
        private bool TryGet(string key, out string text)
        {
            if (_current.TryGetValue(key, out var t) && !string.IsNullOrEmpty(t)) { text = t; return true; }
            if (_fallback.TryGetValue(key, out t) && !string.IsNullOrEmpty(t)) { text = t; return true; }
            text = key;
            return false;
        }

        /// <summary>
        /// Dịch giá trị dạng danh mục cố định lưu trong DB (trạng thái, kết quả...): key = "{group}.{value}".
        /// Không có key → trả nguyên giá trị (KHÔNG log thiếu key). Chỉ dùng để HIỂN THỊ, không dùng để so sánh/lưu.
        /// </summary>
        public string Enum(string group, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value ?? "";
            return TryGet(group + "." + value.Trim(), out var text) ? text : value;
        }

        /// <summary>
        /// Dịch thông báo tiếng Anh cố định do API trả về: key = "Api.{câu gốc}".
        /// Câu chưa có trong từ điển (hoặc có dữ liệu động) → hiển thị nguyên văn.
        /// </summary>
        public string? Api(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return message;   // null giữ null để dùng được "?? fallback"
            return TryGet("Api." + message.Trim(), out var text) ? text : message;
        }

        // ── Ngày tháng ───────────────────────────────────────────────────────
        private static readonly string[] EnMonthShort = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        private static readonly string[] EnMonth = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        private static readonly string[] EnDayShort = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        private static readonly string[] EnDay = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        public string MonthShort(int month) => Pick($"Date.MonthShort{month}", EnMonthShort[(month + 11) % 12]);
        public string MonthName(int month) => Pick($"Date.Month{month}", EnMonth[(month + 11) % 12]);
        public string DayShort(DayOfWeek d) => Pick($"Date.DayShort{(int)d}", EnDayShort[(int)d]);
        public string DayName(DayOfWeek d) => Pick($"Date.Day{(int)d}", EnDay[(int)d]);
        private string Pick(string key, string fallback) => TryGet(key, out var t) ? t : fallback;

        /// <summary>
        /// Định dạng ngày theo mẫu .NET (vd. "d MMM yyyy", "dddd, d MMMM yyyy") nhưng tên tháng/thứ
        /// (MMM, MMMM, ddd, dddd) lấy theo ngôn ngữ đang chọn; phần số giữ InvariantCulture.
        /// Dùng thay cho x.ToString("d MMM yyyy", EnUs). null → null (giữ được "?? \"—\"").
        /// </summary>
        public string? Date(DateTime? value, string pattern)
        {
            if (value == null) return null;
            var v = value.Value;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < pattern.Length)
            {
                char c = pattern[i];
                if (c == '\'' || c == '"')
                {
                    int j = pattern.IndexOf(c, i + 1);
                    if (j < 0) j = pattern.Length;
                    sb.Append(pattern, i + 1, j - i - 1);
                    i = j + 1;
                    continue;
                }
                if (c == '\\' && i + 1 < pattern.Length) { sb.Append(pattern[i + 1]); i += 2; continue; }
                int k = i;
                while (k < pattern.Length && pattern[k] == c) k++;
                int len = k - i;
                if (c == 'M' && len >= 3) sb.Append(len == 3 ? MonthShort(v.Month) : MonthName(v.Month));
                else if (c == 'd' && len >= 3) sb.Append(len == 3 ? DayShort(v.DayOfWeek) : DayName(v.DayOfWeek));
                else if ("yMdHhmsftzgK".IndexOf(c) >= 0) sb.Append(v.ToString(len == 1 ? "%" + c : new string(c, len), inv));
                else sb.Append(pattern, i, len);
                i = k;
            }
            return sb.ToString();
        }

        private async Task ApplyAsync(string code, bool persist, bool notify)
        {
            _current = await GetDictionaryAsync(code);
            CurrentCode = code;

            if (persist)
            {
                try { await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, code); } catch { }
            }
            try { await _js.InvokeVoidAsync("document.documentElement.setAttribute", "lang", code); } catch { }

            if (notify) OnChanged?.Invoke();
        }

        private Task<Dictionary<string, string>> GetDictionaryAsync(string code)
        {
            if (_cache.TryGetValue(code, out var dict)) return Task.FromResult(dict);
            if (_loading.TryGetValue(code, out var pending)) return pending;

            var task = LoadAsync(code);
            _loading[code] = task;
            return task;
        }

        private async Task<Dictionary<string, string>> LoadAsync(string code)
        {
            Dictionary<string, string> dict;
            try
            {
                dict = await _http.GetFromJsonAsync<Dictionary<string, string>>($"i18n/{code}.json?v={BuildStamp}")
                       ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[i18n] Cannot load '{code}': {ex.Message}");
                dict = new();
            }
            dict = new Dictionary<string, string>(dict, StringComparer.Ordinal);
            _cache[code] = dict;
            _loading.Remove(code);
            return dict;
        }

        private static string Normalize(string? code)
        {
            code = (code ?? "").Trim().Trim('"').ToLowerInvariant();
            if (code.Length > 2) code = code[..2];
            return Supported.Any(l => l.Code == code) ? code : DefaultCode;
        }
    }
}
