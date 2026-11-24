# Kế hoạch: tách Website và Super Admin + làm sát thiết kế (22/11/2026)

Nguồn yêu cầu: `D:\Data\Zalo\Website.docx` (7 hình) và `D:\Data\Zalo\Super Admin.docx` (9 hình) — chỉ có hình thiết kế, không có chữ.

Hiện trạng: cả hai đang nằm chung trong `InticooInspection.Portal` (website ở `/`, Super Admin ở `/admin`), dùng chung
DB `InticooInspectionSupper` schema `sup`. Chức năng gần đủ, nhưng giao diện website còn dùng icon/emoji thay cho ảnh và
thiếu nhiều khối so với thiết kế.

Đã chốt với anh/chị:
- Ảnh: cắt từ chính hình thiết kế, để ở `wwwroot/img/...` với tên cố định → sau này có ảnh nét chỉ cần chép đè.
- Chỉ làm ở máy dev, chưa deploy lên 14.225.8.231.
- Project cũ `InticooInspection.Portal`: giữ thư mục, bỏ khỏi solution.

## 1. Cấu trúc sau khi tách

| Project | Loại | Nội dung | Port dev |
|---|---|---|---|
| `InticooInspection.Portal.Core` | Class library | `Data/` (entity, `PortalDbContext`, migration — giữ nguyên lịch sử migration, không tạo DB mới), service dùng chung: Settings, Audit, Password, Billing (+cổng Test), PayPal, Provisioning, Registration, Seeder, Icons, Export | — |
| `InticooInspection.Website` | Razor Pages | Home, Features, Pricing, Resources, About, Contact, Terms, Privacy, Register (6 bước) + RegisterComplete, `/pay/paypal/return|cancel`, chế độ bảo trì, 4 ngôn ngữ (`Localization/*.json` + `tools/` dịch) | 5200 |
| `InticooInspection.SuperAdmin` | Razor Pages | Toàn bộ màn `/admin/*` chuyển lên gốc (`/` = Dashboard, `/companies`, `/login`...), cookie đăng nhập, `OverdueWorker`, rate limit login | 5210 |

- Hai app độc lập: cấu hình riêng, cookie riêng, deploy riêng. Super Admin không còn đường nào đi qua website công khai.
- Migration + seed: chạy ở **Super Admin** (chủ của DB). Website chỉ kiểm tra kết nối, không migrate.
- Chỗ Super Admin cần link sang website (Send Payment Link, xem hóa đơn của khách) dùng cấu hình `Portal:PublicUrl`.
- Website bỏ hẳn mọi tham chiếu `/admin`; Super Admin thêm `robots noindex` + CSP như cũ.
- `appsettings.Secrets.json` chép sang cả hai project mới (gitignore, không publish).
- `.claude/launch.json`: thay `inticoo-portal` bằng `inticoo-website` và `inticoo-superadmin`.
- Cập nhật `CLAUDE.md`, README mới cho từng project (thay README cũ của Portal).

## 2. Website — làm sát 7 hình thiết kế

Chung: header (logo, menu Home/Features/Pricing/Resources▾/About Us, chọn ngôn ngữ có cờ, Login, Register & Subscribe),
footer xanh đậm 4 cột (Product / Resources / Support / Contact Us + địa chỉ Kuala Lumpur, LinkedIn, YouTube).

| Trang | Khối cần có theo thiết kế |
|---|---|
| Home | Hero + mockup laptop/điện thoại + Start Free Trial / Watch Video + 4 badge; 5 thẻ Key Features có ảnh/khung mini (bảng Supplier điểm, CAPA Open/In Progress/Closed, Pass Rate 96.1%); 4 ngôn ngữ có cờ + dropdown minh họa; 4 bước có số + mũi tên; 6 ngành hàng có ảnh; 3 gói giá (Business "Most Popular") |
| Features | Hero + mockup; 9 thẻ chức năng chính kèm khung UI mini (checklist, Defect Details, CAPA list, User Permissions, chọn ngôn ngữ, khiên bảo mật...); 7 thẻ Additional Features |
| Pricing | Hero + 4 badge + mockup; bảng so sánh tính năng 3 gói (✓ / — / chữ), giá tháng + năm "Save 15%"; 4 ô cuối (All Plans Include, Enterprise-Grade Security, 4-Language Support, Need Help Choosing?) |
| Resources | Hero + ảnh; 4 Featured Resources có ảnh; 5 Standards & References (Download PDF); 4 bài Learning Center (ARTICLE/VIDEO); 4 ô Support & FAQ |
| About | Hero ảnh kỹ sư + bản đồ US/EU; Our Story + ảnh văn phòng; Mission/Vision/Values; Experience + 3 ảnh; Commitment + ảnh laptop |
| Contact | Form (Full Name, Company, Email, Country, Subject, Message 0/500); Contact Information; How Can We Help (4 ô); 4 cam kết cuối trang |
| Register | Thanh trên nền tối (logo, khẩu hiệu, ngôn ngữ, "Already have an account? Sign In"); stepper 6 bước; 6 khối form; cột phải **Order Summary** cập nhật theo gói/kỳ thanh toán + khối Secure Payment + nút "Subscribe & Create Account" |

Lưu ý dữ liệu: số liệu gói giữa các hình mâu thuẫn (Website: Business 20 user/20 GB; Super Admin + Register: 15 user/200 GB).
Giá/hạn mức vẫn lấy từ DB (Settings › Subscription Plans) nên chỉnh được, không sửa cứng; giữ số của Super Admin như hiện tại.

PDF/bài viết của Resources, Terms/Privacy: vẫn là khung (chưa có nội dung từ khách).

## 3. Super Admin — đối chiếu 9 hình

Dashboard, Companies, Subscriptions, Payments, Invoices, Users, Modules, Reports, Settings: đối chiếu từng màn với hình
(thẻ KPI, tab trạng thái có số đếm, bộ lọc, bảng + cột, panel chi tiết bên phải, Quick Actions, phân trang, biểu đồ),
sửa phần lệch. Giữ thêm các màn đã có ngoài thiết kế (Languages, Storage, Audit Log, Messages, Account).
Header có ô tìm kiếm, khoảng ngày, chuông thông báo, avatar — như hình.

## 4. Kiểm tra (tự làm, không nhờ anh/chị kiểm)

- Chạy thử trên **LocalDB** (DB `InticooInspectionSupper_Dev` + `Seed:Demo=true`, `Provisioning:Mode=Skip`) — không
  nối DB thật. Xác nhận chuỗi kết nối trước khi chạy (bẫy file Secrets đè cấu hình đã gặp trước đây).
- `dotnet build InticooInspection.sln` sạch lỗi.
- Mở từng trang trong trình duyệt, chụp màn hình so với hình thiết kế, ở 1440px và 375px (điện thoại); 4 ngôn ngữ.
- Chạy trọn luồng: đăng ký → thanh toán thẻ test → hóa đơn Paid → hiện trong Super Admin; chuyển khoản → Mark as Paid;
  form Contact → Messages; đăng nhập/đăng xuất Super Admin; xuất Excel/CSV.
- `build_translations.py` không còn khóa thiếu.
- Xóa DB LocalDB thử nghiệm sau khi xong.

## 5. Thứ tự làm

1. Tạo Portal.Core, chuyển Data + service dùng chung, build.
2. Tạo SuperAdmin, chuyển Areas/Admin lên gốc, build + chạy thử.
3. Tạo Website, chuyển trang công khai + đăng ký + PayPal, build + chạy thử.
4. Cắt ảnh từ hình thiết kế.
5. Làm lại giao diện website theo từng hình (Home → Features → Pricing → Resources → About → Contact → Register).
6. Đối chiếu và chỉnh Super Admin theo 9 hình.
7. Dịch 4 ngôn ngữ cho chữ mới.
8. Kiểm tra toàn bộ (mục 4), sửa lỗi, cập nhật tài liệu.

Không commit, không deploy khi chưa được yêu cầu.
