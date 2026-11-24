"""Trích các chuỗi tiếng Anh dùng trong L["..."] / L.F("...") của website để dịch.
Chạy từ thư mục InticooInspection.Portal:  python tools/extract_keys.py
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

# Chuỗi được truyền vào L[...] gián tiếp (biến, mảng, kết quả service) nên regex không bắt được
EXTRA = [
    "Company Information", "Select Plan", "Language & Modules", "Administrator Account", "Payment", "Confirmation",
    "For small teams", "For growing teams", "For larger teams",
    "Product Inspection", "CAPA Management", "Dashboard Analytics", "Loading Container Check (CLC)", "API Integration",
    "Book a Demo", "Request a Quote", "General Enquiry", "Partnership", "Technical Support",
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
    files = glob.glob("Pages/**/*.cshtml", recursive=True) + glob.glob("Pages/*.cs") + glob.glob("Services/*.cs")
    for f in files:
        s = open(f, encoding="utf-8").read()
        for m in PAT_INDEX.finditer(s):
            keys.add(m.group(1))
        for m in PAT_FMT.finditer(s):
            keys.add(m.group(1))
        for m in PAT_ERR.finditer(s):
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
