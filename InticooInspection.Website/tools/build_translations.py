"""Ghép tr_a.py + tr_b.py thành Localization/{vi,zh,ms}.json và báo khóa còn thiếu.
Chạy từ thư mục InticooInspection.Website:  python tools/build_translations.py"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
sys.stdout.reconfigure(encoding="utf-8")

import extract_keys  # noqa: E402
import tr_a  # noqa: E402
import tr_b  # noqa: E402
import tr_c  # noqa: E402

T = {}
T.update(tr_a.T)
T.update(tr_b.T)
T.update(tr_c.T)
keys = extract_keys.collect()

missing = [k for k in keys if k not in T]
for i, lang in enumerate(("vi", "zh", "ms")):
    # chỉ giữ các khóa đang thực sự dùng; sắp xếp để diff ổn định
    d = {k: T[k][i] for k in keys if k in T}
    with open(f"Localization/{lang}.json", "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=1, sort_keys=True)
print(f"{len(keys)} khóa, đã dịch {len(keys) - len(missing)}")
if missing:
    print("THIẾU:")
    for k in missing:
        print("  ", k)
    sys.exit(1)
# cảnh báo placeholder {n} không khớp giữa các ngôn ngữ
import re  # noqa: E402
for k in keys:
    ph = sorted(re.findall(r"\{\d\}", k))
    for i, lang in enumerate(("vi", "zh", "ms")):
        if sorted(re.findall(r"\{\d\}", T[k][i])) != ph:
            print("PLACEHOLDER LỆCH", lang, k)
