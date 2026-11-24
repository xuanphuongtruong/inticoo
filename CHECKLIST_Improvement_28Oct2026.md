# Checklist cải tiến InticooInspection (theo yêu cầu khách 28/10/2026)

Ký hiệu: ✅ đã sửa và build đạt | ⚠️ làm một phần | ❌ chưa làm | 🔍 chưa kiểm tra trên trình duyệt

Ghi chú chung: build solution 0 lỗi, `i18n.py check` ✓ OK. Chưa kiểm tra bằng mắt trên trình duyệt hoặc điện thoại. Migration `AddVendorBusinessType` tự chạy khi API khởi động.

## Hệ thống / điều hướng
- ✅ Home.razor: chuyển trang đầu tiên theo quyền, sửa vòng lặp "No Access" của Dashboard
- ✅ DefectManagement.razor: phụ đề quy tắc CAPA lấy theo cấu hình
- ✅ InspectionNew / InspectionEdit: vô hiệu hóa loại inspection đã tắt trong Settings
- ✅ MainLayout.razor: tab con Directory trên mobile
- ✅ Taxonomy.cs (mới): danh mục Category, Business Type, Language dùng chung

## 4 popup Directory
- ✅ Customers.razor: Business Type nhiều lựa chọn, bỏ Inspection Report Preferences, bố cục 2 cột, Account Status
- ✅ Vendors.razor: bỏ cột Telephone, thêm Business Type, Account Status, bố cục 2 cột
- ✅ Vendor backend: `Vendor.cs`, `VendorController.cs`, migration `AddVendorBusinessType`
- ✅ Products.razor: bố cục Product Information | Product Photo, Reference | Account Status
- ✅ InspectorProfile.razor: Category, Languages từ Taxonomy, bố cục 2 cột
- ✅ PartyReportSettings.razor: bỏ "Scheduled", thêm Preview Email, Save Template
- ✅ party-profile.css: CSS 2 cột dùng chung, về 1 cột khi dưới 900px
- ❌ Import Excel của Vendor chưa có cột Business Type
- ⚠️ Contacts vẫn là bảng nhiều người, chưa làm ô "Main Contact Person"
- ⚠️ Inspector chưa có ô chọn mã vùng điện thoại

## Inspections / Report / QC
- ✅ Inspections.razor: bộ lọc Category, độ rộng ô lọc, mobile mở PDF báo cáo
- ✅ InspectionReport.razor: chữ so sánh lớn hơn, dòng Remark xanh nhạt, bố cục mobile
- ✅ InspectionQC.razor: tách Product Weight thành bước riêng
- ⚠️ Bản nháp QC cũ lưu sau bước C-i có thể khôi phục lệch một bước
- ⚠️ PDF trên mobile chưa thử với server thật

## Khác
- ✅ PerformanceTestMasterAdmin.razor: bỏ icon thừa trên thẻ mobile và nút Add New
- ✅ VendorReview.razor: đổi tiêu đề, thêm 5 thẻ KPI (chưa có icon)
- ✅ i18n: đủ en / vi / zh / ms cho các key mới

## Việc nên làm tiếp
- 🔍 Mở thử 4 popup trên máy bàn và điện thoại (Vendor dễ lệch nhất)
- 🔍 Kiểm tra cột `Vendors.BusinessType` đã có trong DB sau khi API chạy
- Nâng gói `SixLabors.ImageSharp` (đang có cảnh báo bảo mật)
