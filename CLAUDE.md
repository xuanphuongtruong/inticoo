# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Lệnh thường dùng

Solution `InticooInspection.sln`, .NET 9. Không có project test.

```bash
dotnet build InticooInspection.sln
dotnet run --project InticooInspection.API --launch-profile http      # http://localhost:5034 (Swagger ở /swagger, chỉ Development)
dotnet run --project InticooInspection.Client --launch-profile http    # Blazor WASM dev server (port 5186)
dotnet run --project InticooInspection.Website --launch-profile http     # website bán app (port 5200)
dotnet run --project InticooInspection.SuperAdmin --launch-profile http  # Super Admin (port 5210, chạy trước: migrate DB Super)
dotnet ef migrations add <Ten> --project InticooInspection.Infrastructure --startup-project InticooInspection.API
```

- `.claude/launch.json` đã khai báo `inticoo-api`, `inticoo-client`, `inticoo-website`, `inticoo-superadmin` và bản `*-localdb` (kể cả `inticoo-api-localdb`) (LocalDB `InticooInspectionSupper_Dev`, không đụng DB thật) cho preview.
- API tự chạy `db.Database.MigrateAsync()` + `DbSeeder.SeedAsync` khi khởi động (lỗi chỉ log, không dừng app) — thêm migration là đủ, không cần `database update`.
- API từ chối khởi động nếu thiếu `Jwt:Key` (>= 32 ký tự) hoặc `ConnectionStrings:DefaultConnection`. Hai giá trị này (và `MailSettings:Password`) KHÔNG nằm trong `appsettings.json`; đặt ở `appsettings.Secrets.json` / `appsettings.{Env}.Secrets.json` (gitignore, không publish) hoặc biến môi trường `Jwt__Key`, `ConnectionStrings__DefaultConnection`.
- Client đọc địa chỉ API từ `InticooInspection.Client/wwwroot/appsettings*.json` (`ApiBaseUrl`: dev `http://localhost:5034/`, production `https://inticooapi.thuphuc.com/`).

## Kiến trúc

Blazor WebAssembly (Client) gọi ASP.NET Core Web API (API) bằng JWT; EF Core + SQL Server + ASP.NET Identity.

- `InticooInspection.Domain` — entity (`Entities/`) và `Security/ModulePermissions.cs`. File phân quyền này được **link vào Client** qua `<Compile Include=... Link=...>` trong csproj: API và Client dùng chung 1 nguồn mô hình quyền, chỉ được dùng thư viện chuẩn (không EF/Identity).
- `InticooInspection.Infrastructure` — `AppDbContext`, cấu hình EF trong `Persistence/Configurations`, `Migrations/`, `DbSeeder`.
- `InticooInspection.Application` — hiện gần như rỗng (`Class1.cs`); logic nghiệp vụ nằm thẳng trong controller và `API/Services`.
- `InticooInspection.API` — `Controllers/` (~20), `Services/` chứa logic chính: thống kê chất lượng/CAPA (`QualityStats`, `ClcStats`, `DefectCatalog`, `DefectCapaService`), xuất PDF bằng PuppeteerSharp (`PuppeteerPdfService`), mail (`InspectionMailService`, hai `BackgroundService`: `WeeklyMailWorker` gửi hàng tuần cho Vendor, `InspectionDoneMailWorker` quét 10 phút/lần), lưu file (Azure Blob + `wwwroot/uploads`), cấu hình động (`SettingsStore`). Phạm vi dữ liệu theo người gọi: `UserAccess` (quyền module + phạm vi khách hàng) và `RequireSignedInFilter`. Đăng nhập/đổi mật khẩu bị rate-limit theo IP (policy `auth`). Mẫu Excel import ở `Templates/` được dịch tiêu đề theo ngôn ngữ người dùng (`ImportTemplateLocalizer`).
- `InticooInspection.Client` — `Pages/`, `Layout/`, `Components/`, `Services/` (`AuthService`, `PageAccessService`, `SettingsClient`, `Taxonomy` = danh mục Category/Business Type/Language dùng chung). Quyền module khoá theo `ModuleKeys` để menu và guard chạy cùng một nguồn.
- `InticooInspection.Website`, `InticooInspection.SuperAdmin`, `InticooInspection.Portal.Core` — website bán app + Super Admin (2 app riêng, chung DB Super); xem phần dưới. Thư mục `InticooInspection.Portal` là bản gộp cũ, đã bỏ khỏi solution (chỉ để tham khảo / bản đang chạy production).
- Các thư mục ở gốc (`API/`, `Client/`, `Domain/`, `Application/`, `Infrastructure/`, `{Domain,Application,...}/`) là tàn dư, không nằm trong solution — đừng sửa vào đó.
- `docs/` (gitignore) chứa script dựng bộ hướng dẫn người dùng (`build_guide.py`, `capture_all.py`); `CHECKLIST_Improvement_28Oct2026.md` ghi trạng thái các cải tiến theo yêu cầu khách.

## Deploy

Hai kênh, xem `.github/workflows/`: push `master` → GitHub Actions đẩy lên Azure (Web App cho API, Static Web Apps cho Client; vài workflow dùng nhánh `main` hoặc `app_location` cũ nên không phải cái nào cũng chạy); hoặc publish thủ công lên VPS Windows/IIS. Client và API phải cùng phiên bản — lệch bản gây 404 khi Client gọi endpoint mới. `wwwroot/uploads` và `*.Secrets.json` không được publish (cố ý, để khỏi đè dữ liệu thật trên server).

## Đa ngôn ngữ (bắt buộc với mọi giao diện mới hoặc chỉnh sửa giao diện)

Ứng dụng hỗ trợ en / vi / zh / ms. Chi tiết: [tools/i18n/README.md](tools/i18n/README.md).

- Không viết chữ hiển thị cứng trong `.razor`. Trang/component kế thừa `LocalizedComponentBase` và dùng `@T("Trang.Key")`.
- Giá trị danh mục lưu DB (Status, Severity, Category...) giữ tiếng Anh khi lưu/so sánh; chỉ hiển thị qua `TE("Status"|"Opt", value)`.
- Thông báo cố định từ API: `TA(message)`. Ngày có tên tháng/thứ: `L.Date(date, "d MMM yyyy")`.
- Sau khi sửa giao diện, chạy lần lượt:
  `python tools/i18n/i18n.py apply <file.razor>` → `literals`/`wrap` cho chuỗi C# → `fix` →
  bổ sung bản dịch vào `tools/i18n/tm/*.tsv` → `tm` → `check` (phải ra "✓ OK").
- Thêm key mới phải có đủ bản dịch ở cả 4 file `InticooInspection.Client/wwwroot/i18n/*.json`.
- Email gửi đi vẫn tiếng Anh (chưa đa ngôn ngữ).

## Website bán app + Super Admin (InticooInspection.Website / .SuperAdmin / .Portal.Core)

Hai app riêng biệt (deploy riêng, cookie riêng) theo 2 tài liệu khách `Website.docx` và `Super Admin.docx`; phần dùng chung
(DB quản trị `InticooInspectionSupper` schema `sup`, entity + migration, đăng ký, thanh toán/PayPal, cấp DB riêng cho từng khách)
ở thư viện `InticooInspection.Portal.Core`. Chỉ Super Admin chạy migration; thêm migration với
`--project InticooInspection.Portal.Core --startup-project InticooInspection.SuperAdmin`.
- Website KHÔNG dùng i18n của Client: dịch bằng `L["câu tiếng Anh"]` + `tools/tr_a.py`/`tr_b.py`/`tr_c.py` →
  `python tools/build_translations.py` (trong thư mục Website, phải dịch đủ N/N). Super Admin chỉ tiếng Anh.
- Ảnh website (`wwwroot/img/photos`) cắt từ hình thiết kế — thay ảnh nét hơn bằng cách chép đè cùng tên.
- Dung lượng / người dùng / hạn mức gói: API đo (`Services/Usage/UsageWorker`) ghi vào `sup.CompanyUsageSnapshots`; API chặn thêm user /
  upload khi vượt gói (`PlanLimitService`, 403 `plan_limit`). Mọi chỗ ghi file mới trong API phải gọi `PlanLimitService.EnsureStorageAsync`
  (hoặc đi qua `AzureBlobService.UploadAsync`), tạo/kích hoạt user phải gọi `EnsureUsersAsync`. Chi tiết: SuperAdmin/README.md.
- Chi tiết: [Portal.Core/README.md](InticooInspection.Portal.Core/README.md), [Website/README.md](InticooInspection.Website/README.md),
  [SuperAdmin/README.md](InticooInspection.SuperAdmin/README.md).

**API chạy đa khách** (`InticooInspection.API/Services/Tenancy/`): `AppDbContext` nối DB theo `TenantContext` của
request (claim JWT `inticoo_company`; không có = DB gốc `DefaultConnection`). Khi viết code mới:
- Tạo scope DI riêng (`Task.Run`, worker, `IServiceScopeFactory`) phải gắn tenant: `TenantMaintenance.CreateScope(sp, tenant)`
  hoặc `scope...GetRequiredService<TenantContext>().Set(tenant)` TRƯỚC khi lấy `AppDbContext` — nếu không sẽ nối nhầm DB gốc.
- Cache static/singleton chứa dữ liệu DB phải có khóa theo `TenantContext.Key`.
- Lưu file vào `wwwroot/uploads` qua `AzureBlobService` hoặc `TenantContext.UploadsDir/UploadsUrl` (tự tách `uploads/t/{mã}`).
- Thêm cột/bảng phải có migration thật — DB khách được tạo mới từ migration, không thêm tay được.
