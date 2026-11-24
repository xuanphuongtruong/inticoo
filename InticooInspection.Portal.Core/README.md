# InticooInspection.Portal.Core — phần dùng chung của Website và Super Admin

Website bán app (`InticooInspection.Website`) và khu quản trị (`InticooInspection.SuperAdmin`) là **2 app riêng**
(deploy riêng, cookie riêng), cùng dùng DB quản trị `InticooInspectionSupper` (schema `sup`) qua thư viện này:

* `Data/` — entity, `PortalDbContext`, migration (lịch sử migration giữ nguyên từ project Portal cũ).
  Thêm migration: `dotnet ef migrations add <Ten> --project InticooInspection.Portal.Core --startup-project InticooInspection.SuperAdmin`.
  Chỉ **Super Admin** tự chạy migration + seed khi khởi động; Website chỉ cảnh báo nếu DB còn migration chưa chạy.
* `Services/` — đăng ký (`RegistrationService`), hóa đơn/thanh toán (`BillingService`, cổng thẻ giả lập, `OverdueWorker`),
  PayPal, cấp DB riêng cho khách (`ProvisioningService`), cài đặt (`SettingsService`), audit, mật khẩu, icon SVG, đa ngôn ngữ
  website (`LocalizationStore`/`Loc` — chỉ Website đăng ký dùng).
* `PortalHosting.cs` — nạp file secret (biến môi trường + tham số dòng lệnh luôn ưu tiên hơn file), đăng ký service chung,
  header bảo mật, log DB đang dùng khi khởi động (server/tên DB, không có mật khẩu).

## Cơ sở dữ liệu và bảo mật dữ liệu

* **DB quản trị trung tâm:** `InticooInspectionSupper` (server 14.225.8.231,1991). DB này vốn đã có vài bảng cũ
  (`Products`, `MailLogs`…) nên mọi bảng mới nằm trong schema **`sup`** — không đụng bảng cũ.
  Migration tự chạy khi khởi động (`Data/Migrations`).
* **Mỗi khách hàng 1 database riêng** (`InticooInspection_<mã công ty>`) + **1 SQL login riêng** chỉ có quyền `db_owner`
  trong DB đó. Chuỗi kết nối của khách được mã hóa AES-256-GCM (khóa `Security:TenantSecretKey`, nằm ngoài DB) rồi lưu ở
  `sup.Companies.ConnectionStringEnc`. Vì vậy khách A không thể đọc dữ liệu khách B kể cả khi lộ 1 chuỗi kết nối, và DB
  trung tâm không chứa dữ liệu nghiệp vụ.
* Mật khẩu admin của khách chỉ được lưu dạng hash Identity (cùng định dạng app chính) từ lúc đăng ký, rồi được chép vào
  DB riêng của khách khi cấp tài khoản. Số thẻ/CVC không bao giờ được lưu hay ghi log (chỉ lưu brand + 4 số cuối).
* Super admin: cookie HttpOnly/SameSite=Strict, kiểm tra security stamp mỗi request (đổi mật khẩu/khóa → đăng xuất ngay),
  khóa tạm sau N lần sai, rate limit đăng nhập/đăng ký/liên hệ, antiforgery mọi POST, CSP `script-src 'self'`,
  Audit Log mọi thao tác nhạy cảm, xuất Excel/CSV chống formula-injection.

## Luồng nghiệp vụ

1. Khách đăng ký → tạo `Company` (PendingPayment) + `Subscription` + `Invoice` (+ `Registration` giữ hash mật khẩu).
2. **Thẻ** → cổng thanh toán → thành công thì hóa đơn `Paid`, thuê bao `Active`, **cấp DB riêng** (`ProvisioningService`):
   tạo DB + login, chạy migration của app chính (`AppDbContext`), tạo role + tài khoản Admin. Thất bại → trang đăng ký cho
   thử lại.
3. **Chuyển khoản** → hóa đơn `Pending` kèm thông tin ngân hàng (Settings › Payment & Billing). Super admin bấm
   *Mark as Paid* (Invoices) hoặc *Confirm Payment* (Payments) → kích hoạt + cấp DB.
4. `Require Admin Approval` (Settings) bật → đã trả tiền vẫn chờ `Pending Review` đến khi super admin *Approve*.
5. Hóa đơn quá hạn tự chuyển `Overdue` (worker chạy mỗi giờ + khi mở Dashboard).
6. Xóa công ty đã có thanh toán bị chặn (giữ chứng từ) — hủy thuê bao thay vì xóa. Xóa công ty chưa thanh toán/demo có
   thể xóa luôn DB + login riêng (phải tick xác nhận).

## App chính chạy theo DB riêng của từng khách (đa khách)

`InticooInspection.API` chọn DB theo công ty của người gọi (code ở `InticooInspection.API/Services/Tenancy/`):

* **Đăng nhập** (`AuthController.Login`): tra email/username trong `sup.CompanyUsers` → các công ty có DB riêng
  (`ProvisionStatus=Provisioned`, không phải demo), luôn kèm **DB gốc** (`DefaultConnection`, khách cũ). Thử mật khẩu ở
  từng DB; đúng ở 1 nơi → JWT có claim `inticoo_company` (Id công ty); đúng ở nhiều nơi → trả `needCompany` + danh sách
  công ty, Client hiện ô chọn và gửi lại `companyCode` (DB gốc có mã `inticoo`). Sai mật khẩu chỉ bị tính khi sai ở mọi DB.
* **Mỗi request**: `TenantMiddleware` kiểm chữ ký JWT, đọc claim → `TenantContext` → `AppDbContext` nối chuỗi kết nối
  của công ty (giải mã bằng `Security:TenantSecretKey`). Công ty bị Pause/Cancel → 401 (trạng thái cache 2 phút; mở lại
  có hiệu lực ngay). Không đổi tên claim thành `tid`: thư viện JWT tự đổi `tid` thành claim tenantid của Azure AD.
* **Khởi động API**: migrate DB gốc + mọi DB công ty và bảo đảm đủ role. `DbSeeder` (`admin@inticoo.com`) chỉ chạy cho DB gốc.
* **Worker mail** (`WeeklyMailWorker`, `InspectionDoneMailWorker`) chạy lần lượt cho DB gốc và từng công ty;
  cấu hình mail (`MailConfigProvider`, nay là scoped) cache riêng theo công ty.
* **File upload** của công ty khách nằm ở `wwwroot/uploads/t/{mã công ty}/...` (DB gốc giữ `uploads/...`) vì Id trong các
  DB trùng nhau.
* **Đồng bộ người dùng**: tạo/sửa/khóa/xóa/import user trong app → cập nhật `sup.CompanyUsers` (cột mới `UserName`),
  nên user mới đăng nhập được ngay và hiện ở màn Users của Super Admin.
* Migration `AddMissingSchemaForNewDatabases` bù 3 cột `Customers` + 3 bảng `MailConfigs`, `MailLogs`,
  `PerformanceTestReferences` vốn có trong model nhưng chưa từng có migration (DB gốc được thêm tay) — DB mới thiếu
  nên lỗi "Invalid column name". Viết dạng kiểm tra tồn tại, chạy trên DB gốc không đổi gì.
* Xóa công ty: thanh toán qua cổng giả lập (`Provider = Test`) không chặn xóa (không phải chứng từ thật).

## Chưa làm / cần quyết định

* Lưu ý cấu hình **API khi triển khai**: phải đặt thêm `ConnectionStrings__Super` (DB `InticooInspectionSupper`) và
  `Security__TenantSecretKey` (**cùng khóa** với Portal). Thiếu 2 giá trị này API vẫn chạy nhưng chỉ dùng DB gốc
  (khách đăng ký qua Portal không đăng nhập được). Login SQL cho API chỉ cần đọc `sup.Companies` và đọc/ghi
  `sup.CompanyUsers` — nên tạo login riêng thay vì dùng login quản trị của Portal (Portal cần quyền tạo DB/login).
* Khách tự cấu hình SMTP riêng trong app (Mail Settings) vẫn dùng mật khẩu `MailSettings:Password` chung của server
  (cách cũ của app chính) → hiện chỉ hợp khi mọi khách gửi qua hộp thư Inticoo.
* **Thẻ trên form Portal** (`Payments:Mode=Test`) vẫn là cổng giả lập, không trừ tiền thật.

## Thanh toán PayPal (thẻ Visa/Mastercard không cần tài khoản PayPal)

`Services/PayPal.cs` — PayPal Checkout, Orders API v2, kiểu **chuyển hướng** (không nhúng script PayPal, server không thấy số thẻ):
1. Khách chọn *PayPal / Visa / Mastercard* ở bước 5 (hoặc nút *Pay with PayPal / Card* ở trang hoàn tất khi lần trước thất bại)
   → server tạo order (số tiền = tổng hóa đơn, tiền tệ = `billing.currency`, `landing_page=GUEST_CHECKOUT`) và ghi Payment
   `Pending` với TransactionId = order id → chuyển khách sang PayPal.
2. Khách trả bằng thẻ (*Pay with Debit or Credit Card*) hoặc tài khoản PayPal → PayPal quay về `/pay/paypal/return`
   → server **capture**, kiểm tra số tiền/tiền tệ khớp → TransactionId đổi thành capture id, hóa đơn Paid, kích hoạt + cấp DB.
   Hủy → `/pay/paypal/cancel` → Payment Failed, khách thử lại được. Mở lại link quay về không thu tiền lần 2.
3. Super Admin › Payments › *Refund* với giao dịch PayPal → gọi PayPal hoàn tiền thật rồi mới đổi trạng thái.
Khách duyệt xong mà không quay về → order chưa capture = chưa bị trừ tiền (không cần webhook).

**Cài đặt** (chủ trang tự làm):
1. Có tài khoản **PayPal Business**. Vào developer.paypal.com › Apps & Credentials › tạo app → lấy *Client ID* + *Secret*
   (tab Sandbox để thử với tiền giả, tab Live để nhận tiền thật).
2. Bật nhận thẻ không cần tài khoản PayPal: trong tài khoản PayPal Business › Account Settings › Website payments ›
   *PayPal account optional* = **On** (thiếu bước này khách vẫn phải đăng nhập/tạo tài khoản PayPal; tùy quốc gia PayPal hỗ trợ).
3. Thêm vào `appsettings.Secrets.json` trên server rồi recycle app pool:
   `"PayPal": { "Mode": "Live", "ClientId": "...", "Secret": "..." }` (thử trước bằng `"Mode": "Sandbox"` + key Sandbox).
Thiếu ClientId/Secret → lựa chọn PayPal tự ẩn. Tiền về số dư PayPal của chủ trang, rút về ngân hàng từ trang PayPal.
`PayPal:ApiBase` chỉ dùng khi kiểm thử với PayPal giả lập (không đặt trên server thật).
* **Gửi email** (hóa đơn, chào mừng, xác minh email, thông báo): chưa nối SMTP; template lưu ở Settings › Email Templates.
* Gia hạn tự động: `Auto Renew` mới là cờ; chưa có job tự tạo hóa đơn kỳ kế tiếp (dùng *Create Invoice*).
* Số liệu gói trong thiết kế mâu thuẫn nhau (website: Business 20 user/20 GB; Super Admin: 15 user/200 GB). Đang dùng số
  của Super Admin (5/15/50 user; 100/200/500 GB) — sửa ở Settings › Subscription Plans.
* Nội dung Resources (PDF, bài viết), Terms/Privacy chỉ là khung — cần nội dung và duyệt pháp lý.
* Dung lượng (Storage) đo thật từ kích thước DB riêng; công ty không có DB riêng giữ giá trị cũ.
