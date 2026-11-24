using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InticooInspection.API.Services
{
    /// <summary>
    /// Đánh dấu action/controller được gọi KHÔNG cần đăng nhập (vd: api/auth/login).
    /// Lưu ý: [AllowAnonymous] trong dự án được rải khắp nơi từ trước nên KHÔNG dùng nó làm ngoại lệ —
    /// chỉ [PublicApi] mới được bỏ qua kiểm tra đăng nhập.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class PublicApiAttribute : Attribute { }

    /// <summary>
    /// Filter toàn cục: mọi API đều phải có JWT hợp lệ của user đang hoạt động (tự validate qua UserAccess,
    /// không dựa vào middleware JwtBearer), trừ action/controller gắn [PublicApi].
    /// Quyền chi tiết theo module/khách hàng vẫn do từng action kiểm tra như cũ.
    /// </summary>
    public sealed class RequireSignedInFilter : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<PublicApiAttribute>().Any()) return;

            var access = context.HttpContext.RequestServices.GetRequiredService<UserAccess>();
            if (await access.GetAsync() == null)
                context.Result = new UnauthorizedObjectResult(new { success = false, message = "Please sign in again." });
        }
    }

    /// <summary>Chỉ Admin được gọi (gắn lên controller/action: mail, cấu hình mail...).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AdminApiAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var access = context.HttpContext.RequestServices.GetRequiredService<UserAccess>();
            var ctx = await access.GetAsync();
            if (ctx == null) context.Result = new UnauthorizedObjectResult(new { success = false, message = "Please sign in again." });
            else if (!ctx.IsAdmin) context.Result = new ObjectResult(new { success = false, message = "You do not have permission to perform this action." }) { StatusCode = 403 };
        }
    }
}
