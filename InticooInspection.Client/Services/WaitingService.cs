namespace InticooInspection.Client.Services
{
    /// <summary>
    /// Service toàn cục để hiển thị/ẩn waiting overlay từ bất kỳ trang nào.
    /// Đăng ký: builder.Services.AddScoped&lt;WaitingService&gt;();
    /// </summary>
    public class WaitingService
    {
        public bool   IsVisible { get; private set; }
        public string Message   { get; private set; } = "";

        // MainLayout lắng nghe event này để re-render
        public event Action? OnChange;

        public void Show(string message = "Please wait...")
        {
            IsVisible = true;
            Message   = message;
            OnChange?.Invoke();
        }

        public void Hide()
        {
            IsVisible = false;
            Message   = "";
            OnChange?.Invoke();
        }

        /// <summary>
        /// Tiện ích: tự động Show trước, Hide sau khi task hoàn thành.
        /// Dùng: await _waiting.RunAsync("Saving...", () => SaveData());
        /// </summary>
        public async Task RunAsync(string message, Func<Task> action)
        {
            Show(message);
            try   { await action(); }
            finally { Hide(); }
        }

        /// <summary>
        /// Tiện ích: tự động Show trước, Hide sau, trả về kết quả.
        /// Dùng: var result = await _waiting.RunAsync("Loading...", () => FetchData());
        /// </summary>
        public async Task<T> RunAsync<T>(string message, Func<Task<T>> action)
        {
            Show(message);
            try   { return await action(); }
            finally { Hide(); }
        }
    }
}
