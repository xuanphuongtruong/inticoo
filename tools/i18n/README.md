# Đa ngôn ngữ (i18n) — InticooInspection

Ngôn ngữ hỗ trợ: **English (en)**, **Tiếng Việt (vi)**, **中文 (zh)**, **Bahasa Melayu (ms)**.

## Cách hoạt động

| Thành phần | Vị trí | Vai trò |
|---|---|---|
| Từ điển | `InticooInspection.Client/wwwroot/i18n/{en,vi,zh,ms}.json` | Key phẳng `"Nhom.Ten": "Chuỗi"`. `en.json` là bản gốc |
| `LanguageService` | `Client/Services/LanguageService.cs` | Nạp từ điển 1 lần, giữ trong RAM → đổi ngôn ngữ tức thì, không reload |
| `LocalizedComponentBase` | `Client/Components/LocalizedComponentBase.cs` | Lớp cha của trang/component: `T()`, `TE()`, `TA()`, tự render lại khi đổi ngôn ngữ |
| `LanguagePicker` | `Client/Shared/LanguagePicker.razor` | Chọn ngôn ngữ ở màn Login (menu thả) và sidebar (dải nút) |
| Lưu theo tài khoản | cột `AspNetUsers.UiLanguage`, `PUT api/me/language` | Đăng nhập máy khác vẫn giữ ngôn ngữ |
| File mẫu Excel | `API/Templates/*.xlsx` + `API/Services/ImportTemplateLocalizer.cs` | Tiêu đề cột dịch theo `?lang=`, dạng `Tên công ty* (CompanyName)` |
| Công cụ | `tools/i18n/i18n.py` | Trích chữ, bọc chuỗi, kiểm tra, dịch hàng loạt |

Luồng chọn ngôn ngữ: màn Login → `localStorage["app_lang"]` → mọi màn sau đọc lại. Người dùng chủ động chọn ở Login
thì lưu vào tài khoản khi đăng nhập; không chọn thì dùng ngôn ngữ đã lưu của tài khoản. Đổi ở sidebar cũng lưu vào tài khoản.

## Viết giao diện mới — quy tắc bắt buộc

1. Trang/component có chữ hiển thị: `@inherits LocalizedComponentBase` (layout: `LocalizedLayoutBase`).
   Nếu cần Dispose: **override** `Dispose(bool disposing)` và gọi `base.Dispose(disposing)` — không tự viết `public void Dispose()`.
2. **Không viết chữ cứng.** Dùng:
   - `@T("Trang.Key")`, `@T("Trang.Key", arg0, arg1)` — chuỗi có `{0}`, `{1}`.
   - `@TE("Status", x.Status)` / `@TE("Opt", x.Category)` — hiển thị giá trị danh mục lưu trong DB (giá trị DB giữ tiếng Anh!).
   - `TA(err?.Message)` — dịch thông báo cố định do API trả về (key `Api.{câu gốc}`).
   - `L.Date(ngay, "d MMM yyyy")` — thay cho `ngay.ToString("d MMM yyyy", EnUs)` (tên tháng/thứ theo ngôn ngữ).
   - Trong hàm `static` / file `.cs` không kế thừa: `LanguageService.Tr("Key", "fallback tiếng Anh")`.
3. **Không** dịch giá trị dùng để so sánh / lưu DB / làm class CSS (vd. `Status = "Completed"`, `case "Critical"`).
   Chỉ dịch ở chỗ hiển thị.
4. Không dùng `T()` trong field initializer hoặc hàm `static` — đổi sang property `=>` (lệnh `fix` tự làm).
5. Thêm key xong phải có đủ bản dịch vi/zh/ms (lệnh `check` báo thiếu).

## Quy trình cho màn hình mới / sửa giao diện

```bash
# 1. Viết giao diện như bình thường (có thể viết chữ tiếng Anh trước), rồi tự chuyển chữ cứng trong markup → @T():
python tools/i18n/i18n.py apply InticooInspection.Client/Components/Pages/MyPage.razor

# 2. Chuỗi trong code C# (thông báo, Waiting.Show, alert...): liệt kê rồi chọn số thứ tự cần bọc
python tools/i18n/i18n.py literals InticooInspection.Client/Components/Pages/MyPage.razor
python tools/i18n/i18n.py wrap InticooInspection.Client/Components/Pages/MyPage.razor 1,3,5-9

# 3. Build + tự sửa lỗi T() trong static/field initializer
python tools/i18n/i18n.py fix

# 4. Dịch: thêm dòng vào tools/i18n/tm/*.tsv (en<TAB>vi<TAB>zh<TAB>ms) rồi điền vào các file ngôn ngữ
python tools/i18n/i18n.py export vi            # xem các câu còn thiếu
python tools/i18n/i18n.py tm

# 5. Kiểm tra trước khi commit (key thiếu, bản dịch thiếu, placeholder lệch, chữ cứng còn sót)
python tools/i18n/i18n.py check
```

Thêm/sửa nhanh 1 key: `python tools/i18n/i18n.py set Trang.Key "English" "Tiếng Việt" "中文" "Bahasa Melayu"`.

Key cùng câu tiếng Anh nhưng cần dịch khác nhau (vd. nút "Cancel" = "Hủy" nhưng trạng thái = "Đã hủy"):
ghi vào `tools/i18n/tm/overrides.tsv` (`key<TAB>vi<TAB>zh<TAB>ms`).

## Thêm ngôn ngữ mới

1. Thêm 1 dòng vào `LanguageService.Supported` (Client) và `AuthController.UiLanguages` (API).
2. Tạo `wwwroot/i18n/{code}.json` (chạy `python tools/i18n/i18n.py sync` sau khi thêm code vào `LANGS` trong `i18n.py`).
3. Thêm cột dịch vào `tools/i18n/tm/*.tsv`, nhãn cột Excel vào `ImportTemplateLocalizer`.

## Ghi chú

- `CultureInfo` **không** đổi theo ngôn ngữ (Blazor WASM báo lỗi khi đổi culture lúc khởi động và để không ảnh hưởng nhập số/ngày). Định dạng số giữ nguyên; tên tháng/thứ dịch qua `L.Date()`.
- Báo cáo kiểm hàng (`/inspection-report/{id}`) và báo cáo đóng lỗi in theo ngôn ngữ đang chọn. Xuất PDF phía server (`GET api/pdf/inspection/{id}?lang=vi`) truyền ngôn ngữ cho Puppeteer.
- Email thông báo gửi khách hàng/nhà cung cấp (MailController, WeeklyMailWorker) vẫn bằng tiếng Anh.
- Dữ liệu người dùng nhập (tên sản phẩm, mô tả lỗi...) không dịch.
