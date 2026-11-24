"""Trích các chuỗi tiếng Anh dùng trong L["..."] / L.F("...") của website để dịch.
Chạy từ thư mục InticooInspection.Website:  python tools/extract_keys.py
In ra các khóa chưa có trong Localization/{vi,zh,ms}.json."""
import glob
import json
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

PAT_INDEX = re.compile(r'\bL\[' + r'"((?:[^"\\]|\\.)*)"' + r'\]')
PAT_FMT = re.compile(r'\bL\.F\(' + r'"((?:[^"\\]|\\.)*)"')
# Thông báo kiểm tra dữ liệu (DataAnnotations) — được dịch bởi ModelState.Localize
PAT_ERR = re.compile(r'\bErrorMessage\s*=\s*' + r'"((?:[^"\\]|\\.)*)"')

# Chuỗi trong mảng tuple của .cshtml (vd. new[] { ("doc", "Inspection Scheduling", "...") }) rồi đưa vào L[t]:
# lấy literal bắt đầu bằng chữ hoa, có chữ thường/khoảng trắng (bỏ qua tên icon/ảnh viết thường, "N0"...)
PAT_TUPLE = re.compile(r'[(,]\s*"([A-Z][^"\\]*[a-z ][^"\\]*)"')

# Chuỗi được truyền vào L[...] gián tiếp (biến, mảng, kết quả service) nên regex không bắt được
EXTRA = [
    "Company Information", "Select Plan", "Language & Modules", "Administrator Account", "Payment", "Confirmation",
    "For small teams", "For growing teams", "For larger teams",
    "Product Inspection", "CAPA Management", "Dashboard Analytics", "Loading Container Check (CLC)", "API Integration",
    "View Guide", "View Checklists", "Read Articles", "Watch Video", "Read More", "Download PDF",
    # Dòng tính năng mặc định của gói (migration AddUsageTracking → sup.Plans.Features; Super Admin sửa được)
    "Digital inspection forms (PPI, DPI, PST)", "Inspection scheduling", "Photo and PDF reports", "Basic dashboard and analytics",
    "Up to {users} users", "{storage} GB cloud storage", "Multi-language (EN/CN/VI/MS)", "Email support",
    "Everything in Basic", "CAPA management", "Supplier management", "Advanced analytics and reports", "Priority email support",
    "Everything in Business", "Advanced reporting", "Custom inspection templates", "API integration (upon request)", "Dedicated account manager",
    "Book a Demo", "Request a Quote", "General Enquiry", "Partnership", "Technical Support", "Request a Resource",
    "Self-registration is currently disabled. Please contact us.",
    "You must accept the Terms of Service and Privacy Policy.",
    "Please select a valid country / region.", "Selected plan is not available.", "Invalid billing cycle.", "Invalid payment method.",
    "An account with this email already exists. Please sign in or contact support.",
    "Invalid card number.", "Card expired or expiry date invalid.", "Invalid security code.", "Your card was declined.",
    "Online card payment is not enabled.",
    "Online card payment is not available yet. Please choose Bank Transfer.", "Please enter your card details.",
    "Please check the highlighted information.", "Please select a subject.",
]


def collect():
    keys = set(EXTRA)
    # Service dùng chung (thông báo lỗi đăng ký/thanh toán) nằm ở project InticooInspection.Portal.Core
    files = glob.glob("Pages/**/*.cshtml", recursive=True) + glob.glob("Pages/*.cs") + glob.glob("Services/*.cs")         + glob.glob("../InticooInspection.Portal.Core/Services/*.cs")
    for f in files:
        s = open(f, encoding="utf-8").read()
        for m in PAT_INDEX.finditer(s):
            keys.add(m.group(1))
        for m in PAT_FMT.finditer(s):
            keys.add(m.group(1))
        for m in PAT_ERR.finditer(s):
            keys.add(m.group(1))
        if f.endswith(".cshtml"):
            for m in PAT_TUPLE.finditer(s):
                keys.add(m.group(1))
        if f.endswith("Seeder.cs"):
            # nội dung Resources mặc định (Super Admin sửa được; bản gốc tiếng Anh được dịch qua file dịch website)
            for line in s.splitlines():
                if "R(ResourceSection." in line:
                    for m in PAT_TUPLE.finditer(line):
                        keys.add(m.group(1))
    return sorted(keys)


if __name__ == "__main__":
    keys = collect()
    missing = {}
    for lang in ("vi", "zh", "ms"):
        d = json.load(open(f"Localization/{lang}.json", encoding="utf-8"))
        missing[lang] = [k for k in keys if k not in d]
    print(f"{len(keys)} khóa; thiếu: " + ", ".join(f"{l}={len(v)}" for l, v in missing.items()))
    if "--list" in sys.argv:
        for k in keys:
            print(k)
