using InticooInspection.Client.Services;
using Microsoft.AspNetCore.Components;

namespace InticooInspection.Client.Components
{
    /// <summary>
    /// Lớp cha cho trang/component cần đa ngôn ngữ: <c>@inherits LocalizedComponentBase</c>
    /// rồi dùng <c>@T("Key")</c> hoặc <c>@T("Key", arg0)</c> trong razor.
    /// Tự render lại khi người dùng đổi ngôn ngữ.
    /// Trang đã có @implements IDisposable thì override <see cref="Dispose(bool)"/> thay vì tự viết Dispose().
    /// </summary>
    public abstract class LocalizedComponentBase : ComponentBase, IDisposable
    {
        [Inject] protected LanguageService L { get; set; } = default!;

        protected string T(string key) => L.Get(key);
        protected string T(string key, params object?[] args) => L.Get(key, args);
        /// <summary>Hiển thị giá trị danh mục lưu DB (Status, Result...) — xem LanguageService.Enum.</summary>
        protected string TE(string group, string? value) => L.Enum(group, value);
        /// <summary>Dịch thông báo cố định từ API — xem LanguageService.Api.</summary>
        protected string? TA(string? message) => L.Api(message);

        private bool _subscribed;

        // Đăng ký ở SetParametersAsync (luôn chạy) để trang override OnInitialized quên gọi base vẫn hoạt động
        public override Task SetParametersAsync(ParameterView parameters)
        {
            if (!_subscribed) { L.OnChanged += HandleLanguageChanged; _subscribed = true; }
            return base.SetParametersAsync(parameters);
        }

        private void HandleLanguageChanged() => InvokeAsync(StateHasChanged);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) L.OnChanged -= HandleLanguageChanged;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>Bản dành cho Layout (kế thừa LayoutComponentBase để có @Body).</summary>
    public abstract class LocalizedLayoutBase : LayoutComponentBase, IDisposable
    {
        [Inject] protected LanguageService L { get; set; } = default!;

        protected string T(string key) => L.Get(key);
        protected string T(string key, params object?[] args) => L.Get(key, args);
        /// <summary>Hiển thị giá trị danh mục lưu DB (Status, Result...) — xem LanguageService.Enum.</summary>
        protected string TE(string group, string? value) => L.Enum(group, value);
        /// <summary>Dịch thông báo cố định từ API — xem LanguageService.Api.</summary>
        protected string? TA(string? message) => L.Api(message);

        private bool _subscribed;

        // Đăng ký ở SetParametersAsync (luôn chạy) để trang override OnInitialized quên gọi base vẫn hoạt động
        public override Task SetParametersAsync(ParameterView parameters)
        {
            if (!_subscribed) { L.OnChanged += HandleLanguageChanged; _subscribed = true; }
            return base.SetParametersAsync(parameters);
        }

        private void HandleLanguageChanged() => InvokeAsync(StateHasChanged);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) L.OnChanged -= HandleLanguageChanged;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
