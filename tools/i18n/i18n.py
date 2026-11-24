#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Công cụ đa ngôn ngữ cho InticooInspection.Client (Blazor WASM, từ điển JSON tại wwwroot/i18n/{lang}.json).

Quy ước (xem thêm tools/i18n/README.md):
  - Trang/component kế thừa LocalizedComponentBase (layout: LocalizedLayoutBase) → dùng @T("Key"), T("Key", args).
  - Key phẳng "Nhom.Ten": Nhom = tên file razor (Customers, InspectionQC...) hoặc Common/Menu/Status...
  - en.json là bản gốc; vi/zh/ms phải có đủ key của en.json (thiếu → hiển thị tiếng Anh).

Lệnh:
  python tools/i18n/i18n.py scan [file...]           Liệt kê chữ cứng còn sót trong giao diện (không sửa file)
  python tools/i18n/i18n.py apply [file...]          Tự chuyển chữ cứng trong markup → @T("Key"), thêm key vào en.json
  python tools/i18n/i18n.py literals <file>          Liệt kê chuỗi C# có thể là chữ hiển thị (đánh số #id)
  python tools/i18n/i18n.py wrap <file> <ids>        Bọc các chuỗi C# đã chọn bằng T("Key") (ids: 1,3,5-9 | all)
  python tools/i18n/i18n.py fix                      Build Client và tự sửa lỗi T() trong hàm static / field initializer
  python tools/i18n/i18n.py check                    Kiểm tra key thiếu/thừa giữa code và các file ngôn ngữ
  python tools/i18n/i18n.py sync                     Sắp xếp vi/zh/ms theo thứ tự en.json, xoá key không còn trong en.json
  python tools/i18n/i18n.py export <lang> [out.tsv]  Xuất key chưa dịch của <lang> ra TSV để dịch
  python tools/i18n/i18n.py import <lang> <in.tsv>   Nhập bản dịch từ TSV (key<TAB>en<TAB>bản dịch)
  python tools/i18n/i18n.py set <key> <en> [vi] [zh] [ms]   Thêm/sửa 1 key ở tất cả ngôn ngữ
  python tools/i18n/i18n.py tm                       Điền bản dịch còn thiếu từ tools/i18n/tm/*.tsv (en<TAB>vi<TAB>zh<TAB>ms)

Không truyền file → xử lý mọi *.razor của Client (trừ obj/bin).
"""
import glob
import html
import json
import os
import re
import sys
from collections import OrderedDict, defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
CLIENT = os.path.join(ROOT, 'InticooInspection.Client')
I18N_DIR = os.path.join(CLIENT, 'wwwroot', 'i18n')
LANGS = ['en', 'vi', 'zh', 'ms']

# Thuộc tính HTML / tham số component chứa chữ hiển thị
TEXT_ATTRS = {
    'placeholder', 'title', 'aria-label', 'alt', 'data-label', 'data-title', 'data-tooltip',
    'Placeholder', 'Label', 'Title', 'ButtonLabel', 'Text', 'Header', 'Caption', 'EmptyText',
    'ConfirmText', 'CancelText', 'Message', 'Tooltip', 'SubTitle', 'Subtitle', 'Hint', 'LoadingText',
    'SearchPlaceholder', 'EntityPlural', 'EntitySingular', 'EmptyOptionLabel', 'DefaultLabel',
}
# Chữ không dịch (thương hiệu, ký hiệu kỹ thuật)
NO_TRANSLATE = {'Inticoo', 'INTICOO', 'Inticoo Inspection', 'PDF', 'PNG', 'JPG', 'XLSX', 'DOCX', 'AQL', 'QC', 'QA',
                'ID', 'No', 'N/A', 'OK', 'x', 'X', 'pcs', 'PCS', 'kg', 'cm', 'mm', '%', 'CAPA', 'Email', 'E-mail'}
DATE_FMT_RE = re.compile(r'^[dMyHhmsft/\-.: ]+$')
CODE_KEYWORDS = {'if', 'else', 'foreach', 'for', 'while', 'switch', 'using', 'code', 'functions', 'try', 'catch',
                 'finally', 'lock', 'do', 'await', 'case', 'default', 'section', 'helper', 'inherits', 'model'}


# ─────────────────────────── JSON ngôn ngữ ───────────────────────────
def lang_path(lang):
    return os.path.join(I18N_DIR, f'{lang}.json')


def load_lang(lang):
    p = lang_path(lang)
    if not os.path.exists(p):
        return OrderedDict()
    with open(p, encoding='utf-8-sig') as f:
        return json.load(f, object_pairs_hook=OrderedDict)


def save_lang(lang, data):
    os.makedirs(I18N_DIR, exist_ok=True)
    # Nhóm theo tiền tố: chèn dòng trống giữa các nhóm cho dễ đọc
    lines = ['{']
    items = list(data.items())
    prev = None
    for i, (k, v) in enumerate(items):
        grp = k.split('.', 1)[0]
        if prev is not None and grp != prev:
            lines.append('')
        prev = grp
        comma = ',' if i < len(items) - 1 else ''
        lines.append(f'  {json.dumps(k, ensure_ascii=False)}: {json.dumps(v, ensure_ascii=False)}{comma}')
    lines.append('}')
    with open(lang_path(lang), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')


def insert_key(data, key, value):
    """Chèn key vào cuối nhóm cùng tiền tố (giữ file gọn theo nhóm)."""
    if key in data:
        data[key] = value
        return data
    grp = key.split('.', 1)[0] + '.'
    items = list(data.items())
    pos = None
    for i, (k, _) in enumerate(items):
        if k.startswith(grp):
            pos = i
    if pos is None:
        items.append((key, value))
    else:
        items.insert(pos + 1, (key, value))
    return OrderedDict(items)


# ─────────────────────────── Đọc file razor ───────────────────────────
def read_file(path):
    raw = open(path, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    s = raw.decode('utf-8-sig')
    crlf = '\r\n' in s
    return s.replace('\r\n', '\n'), bom, crlf


def write_file(path, s, bom, crlf):
    if crlf:
        s = s.replace('\n', '\r\n')
    with open(path, 'wb') as f:
        f.write((b'\xef\xbb\xbf' if bom else b'') + s.encode('utf-8'))


def skip_cs_string(s, i):
    """s[i] là đầu chuỗi C# ("..", @"..", $"..", $@"..", '..') → trả về vị trí sau chuỗi."""
    j = i
    verbatim = False
    interp = False
    while j < len(s) and s[j] in '@$':
        if s[j] == '@':
            verbatim = True
        else:
            interp = True
        j += 1
    q = s[j]
    j += 1
    while j < len(s):
        c = s[j]
        if interp and c == '{':
            if s[j + 1:j + 2] == '{':
                j += 2
                continue
            j = match_brace(s, j, '{', '}') + 1   # lỗ nội suy có thể chứa chuỗi lồng "..."
            continue
        if verbatim and c == q:
            if j + 1 < len(s) and s[j + 1] == q:
                j += 2
                continue
            return j + 1
        if not verbatim and c == '\\':
            j += 2
            continue
        if c == q:
            return j + 1
        if c == '\n' and not verbatim:
            return j + 1
        j += 1
    return j


def match_brace(s, i, open_ch='{', close_ch='}'):
    """s[i] == open_ch → vị trí close_ch tương ứng (bỏ qua chuỗi/comment C#)."""
    depth = 0
    j = i
    n = len(s)
    while j < n:
        c = s[j]
        if c == '/' and j + 1 < n and s[j + 1] == '/':
            k = s.find('\n', j)
            j = n if k < 0 else k
            continue
        if c == '/' and j + 1 < n and s[j + 1] == '*':
            k = s.find('*/', j + 2)
            j = n if k < 0 else k + 2
            continue
        if c == '"' or (c in '@$' and j + 1 < n and s[j + 1] in '"$@' and '"' in s[j:j + 3]):
            if c in '@$':
                k = j
                while k < n and s[k] in '@$':
                    k += 1
                if k < n and s[k] == '"':
                    j = skip_cs_string(s, j)
                    continue
            else:
                j = skip_cs_string(s, j)
                continue
        if c == "'":
            # ký tự C# 'x' / '\n'
            if j + 2 < n and s[j + 1] == '\\':
                k = s.find("'", j + 2)
                j = (k + 1) if 0 < k < j + 8 else j + 1
                continue
            if j + 2 < n and s[j + 2] == "'":
                j += 3
                continue
        if c == open_ch:
            depth += 1
        elif c == close_ch:
            depth -= 1
            if depth == 0:
                return j
        j += 1
    return n - 1


class Regions:
    """Xác định vùng markup / vùng code trong file razor (theo offset)."""

    def __init__(self, s):
        self.s = s
        n = len(s)
        self.excluded = bytearray(n)   # 1 = không phải markup (comment, style, script, @code, @{...}, directive)
        self.code_blocks = []          # (start, end) khối C# thuần
        self._mark(r'@\*[\s\S]*?\*@')
        self._mark(r'<!--[\s\S]*?-->')
        self._mark(r'<style\b[\s\S]*?</style>')
        self._mark(r'<script\b[\s\S]*?</script>')
        for m in re.finditer(r'^[ \t]*@(page|using|inject|attribute|inherits|implements|layout|typeparam|namespace|rendermode|preservewhitespace)\b.*$', s, re.M):
            self._set(m.start(), m.end())
        for m in re.finditer(r'@(code|functions)\s*\{', s):
            if self.excluded[m.start()]:
                continue
            b = s.index('{', m.start())
            e = match_brace(s, b)
            self._set(m.start(), e + 1)
            self.code_blocks.append((b + 1, e))
        for m in re.finditer(r'@\{', s):
            if self.excluded[m.start()]:
                continue
            e = match_brace(s, m.start() + 1)
            self._set(m.start(), e + 1)
            self.code_blocks.append((m.start() + 2, e))

        # Markup viết bên trong code (RenderFragment __builder => { <div>..</div> } hoặc @<b>..</b>) → coi là markup
        for (a, b) in list(self.code_blocks):
            for (ia, ib) in builder_bodies(s, a, b):
                for k in range(ia, ib):
                    self.excluded[k] = 0
                # các khối code lồng trong đảo (@{...}) vẫn loại trừ
                for m in re.finditer(r'@\{', s[ia:ib]):
                    st = ia + m.start()
                    e = match_brace(s, st + 1)
                    self._set(st, e + 1)

    def _mark(self, pat):
        for m in re.finditer(pat, self.s, re.I):
            self._set(m.start(), m.end())

    def _set(self, a, b):
        for k in range(a, min(b, len(self.excluded))):
            self.excluded[k] = 1

    def is_markup(self, i):
        return not self.excluded[i]


HTML_TAGS = set('''a abbr address area article aside audio b bdi bdo blockquote br button canvas caption cite code col
colgroup data datalist dd del details dfn dialog div dl dt em embed fieldset figcaption figure footer form h1 h2 h3 h4 h5 h6
header hr i iframe img input ins kbd label legend li link main map mark menu meter nav noscript object ol optgroup option
output p param picture pre progress q rp rt ruby s samp section select small source span strong sub summary sup table tbody
td template textarea tfoot th thead time tr track u ul var video wbr svg path circle rect line polyline polygon g text
tspan defs title PageTitle'''.split())

VOID_TAGS = {'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'source', 'track', 'wbr'}


def builder_bodies(s, a, b):
    """Thân các RenderFragment viết tay trong @code: `__builder => { ...markup... }`."""
    out = []
    for m in re.finditer(r'__builder\s*=>\s*\{', s[a:b]):
        st = a + m.end() - 1
        e = match_brace(s, st)
        out.append((st + 1, e))
    return out


def markup_islands(s, a, b):
    """Tìm các đoạn markup nằm trong khối code [a, b): dòng bắt đầu bằng <tag hoặc biểu thức @<tag."""
    out = []
    i = a
    while i < b:
        m = re.compile(r'(^[ 	]*|@)<([A-Za-z][\w.:-]*)', re.M).search(s, i, b)
        if not m:
            break
        start = m.start(2) - 1
        # bỏ qua so sánh/generic C# kiểu "x <y" — chỉ nhận khi ở đầu dòng hoặc ngay sau @
        depth = 0
        j = start
        end = None
        while j < b:
            if s[j] == '<' and j + 1 < b and (s[j + 1].isalpha() or s[j + 1] == '/'):
                e = scan_tag_end(s, j)
                tag = s[j:e + 1]
                name = re.match(r'</?([A-Za-z][\w.:-]*)', tag)
                nm = name.group(1).lower() if name else ''
                if tag.startswith('</'):
                    depth -= 1
                elif tag.endswith('/>') or nm in VOID_TAGS:
                    pass
                else:
                    depth += 1
                j = e + 1
                if depth <= 0:
                    end = j
                    break
                continue
            if s[j] == '@' and j + 1 < b and s[j + 1] == '(':
                j = match_brace(s, j + 1, '(', ')') + 1
                continue
            j += 1
        if end is None:
            break
        out.append((start, end))
        i = end
    return out


# ─────────────────────────── Phân tích biểu thức razor trong chữ ───────────────────────────
IDENT_START = re.compile(r'[A-Za-z_]')
IDENT_CHAR = re.compile(r'[A-Za-z0-9_]')


def parse_implicit(text, i):
    """text[i] là ký tự đầu identifier sau '@' → (end, expr)."""
    j = i
    n = len(text)
    while j < n and IDENT_CHAR.match(text[j]):
        j += 1
    while j < n:
        c = text[j]
        if c == '(':
            j = match_brace(text, j, '(', ')') + 1
        elif c == '[':
            j = match_brace(text, j, '[', ']') + 1
        elif c == '.' and j + 1 < n and IDENT_START.match(text[j + 1]):
            j += 1
            while j < n and IDENT_CHAR.match(text[j]):
                j += 1
        elif c == '?' and j + 2 < n and text[j + 1] == '.' and IDENT_START.match(text[j + 2]):
            j += 2
            while j < n and IDENT_CHAR.match(text[j]):
                j += 1
        elif c == '!' and j + 1 < n and text[j + 1] == '.':
            j += 1
        else:
            break
    return j, text[i:j]


def tokenize(text):
    """Tách chữ thành [('lit', str) | ('expr', code)]. Trả None nếu chứa lệnh điều khiển razor."""
    out = []
    buf = []
    i = 0
    n = len(text)
    while i < n:
        c = text[i]
        if c == '@':
            if i + 1 < n and text[i + 1] == '@':
                buf.append('@')
                i += 2
                continue
            if i + 1 < n and text[i + 1] == '(':
                e = match_brace(text, i + 1, '(', ')')
                if buf:
                    out.append(('lit', ''.join(buf)))
                    buf = []
                out.append(('expr', text[i + 2:e].strip()))
                i = e + 1
                continue
            if i > 0 and IDENT_CHAR.match(text[i - 1]):      # email: abc@xyz.com
                buf.append('@')
                i += 1
                continue
            if i + 1 < n and IDENT_START.match(text[i + 1]):
                e, expr = parse_implicit(text, i + 1)
                head = re.match(r'[A-Za-z_]+', expr).group(0)
                if head in CODE_KEYWORDS:
                    return None
                if buf:
                    out.append(('lit', ''.join(buf)))
                    buf = []
                out.append(('expr', expr))
                i = e
                continue
            if i + 1 < n and text[i + 1] in '{:':
                return None
            buf.append('@')
            i += 1
            continue
        buf.append(c)
        i += 1
    if buf:
        out.append(('lit', ''.join(buf)))
    return out


def has_letters(s):
    return re.search(r'[A-Za-z]', s) is not None


ENTITY_RE = re.compile(r'&(#\d+|#x[0-9a-fA-F]+|[A-Za-z]+);')


def looks_like_code_text(core):
    core = ENTITY_RE.sub(' ', core)   # &amp; &nbsp; ... không phải dấu ';' của C#
    if re.search(r'[{};]', core):
        return True
    for line in core.split('\n'):
        t = line.strip()
        if not t:
            continue
        if re.match(r'^(else\b|case\b|default:|break\b|return\b|var\b|@if\b|@foreach\b|@for\b|@while\b|@switch\b|@using\b|@try\b|//)', t):
            return True
    return False


def build_template(tokens):
    """→ (english_template, [exprs]) hoặc None nếu không có chữ."""
    parts = []
    exprs = []
    lit_text = ''
    for kind, val in tokens:
        if kind == 'lit':
            v = html.unescape(val)
            v = re.sub(r'[ \t\r\n]+', ' ', v)
            v = v.replace('{', '{{').replace('}', '}}')
            parts.append(v)
            lit_text += v
        else:
            if re.search(r'MarkupString|RenderFragment|ChildContent|\bBody\b|@\(', val):
                return None
            parts.append('{%d}' % len(exprs))
            exprs.append(val)
    tpl = ''.join(parts).strip()
    if not has_letters(lit_text):
        return None
    plain = re.sub(r'\{\d+\}', '', tpl).strip(' :.-–—()|/·,')
    if plain in NO_TRANSLATE or not has_letters(plain) or len(plain) < 2:
        return None
    return tpl, exprs


def slug(text):
    t = re.sub(r'\{\d+\}', ' ', text)
    words = re.findall(r'[A-Za-z0-9]+', t)
    words = [w for w in words if w.lower() not in ('the', 'a', 'an')] or words
    s = ''.join(w[:1].upper() + w[1:] for w in words[:6])
    if not s:
        s = 'Text'
    if s[0].isdigit():
        s = 'N' + s
    return s[:48]


# ─────────────────────────── Tìm chữ cứng trong markup ───────────────────────────
class Hit:
    __slots__ = ('start', 'end', 'kind', 'template', 'exprs', 'lead', 'trail', 'attr', 'quote', 'line')

    def __init__(self, **kw):
        for k, v in kw.items():
            setattr(self, k, v)


def scan_tag_end(s, i):
    """s[i] == '<' (mở thẻ) → vị trí '>' kết thúc thẻ (bỏ qua chuỗi và biểu thức razor)."""
    j = i + 1
    n = len(s)
    quote = None
    while j < n:
        c = s[j]
        if c == '@' and j + 1 < n and s[j + 1] == '(':
            j = match_brace(s, j + 1, '(', ')') + 1
            continue
        if quote:
            if c == quote:
                quote = None
        elif c in '"\'':
            quote = c
        elif c == '>':
            return j
        j += 1
    return n - 1


ATTR_RE = re.compile(r'(?<=[\s])([A-Za-z_:@][\w:\-.@]*)\s*=\s*(["\'])')


def find_markup_hits(s, regions):
    hits = []
    manual = []
    n = len(s)
    i = 0
    text_start = 0

    def handle_text(a, b):
        raw = s[a:b]
        if not raw.strip():
            return
        # cắt bỏ phần không phải markup (vd. directive) — chỉ xử lý nếu toàn đoạn là markup
        if any(regions.excluded[k] for k in range(a, b)):
            # tách theo các khoảng markup liên tục
            k = a
            while k < b:
                if regions.excluded[k]:
                    k += 1
                    continue
                e = k
                while e < b and not regions.excluded[e]:
                    e += 1
                handle_text_piece(k, e)
                k = e
            return
        handle_text_piece(a, b)

    def is_text_line(l):
        l = ENTITY_RE.sub(' ', l)
        return has_letters(l) and not re.search(r'[;{}]|=>|&&|\|\||==|!=|\?\?|\?\.|\s=\s|^[.:?]\s?|\bvar\s|\w\(.*\)\s*$|^"|,\s*$|^\w+:\s', l) \
            and not re.match(r'^(@?(if|else|foreach|for|while|switch|case|default|break|return|var|using|try|catch|finally)\b|//|@\{)', l)

    def handle_text_piece(a, b):
        raw = s[a:b]
        core = raw.strip()
        if not core or not has_letters(core):
            return
        if looks_like_code_text(core):
            # Code razor xen giữa thẻ (vd. "}" / "else {" cạnh chữ) → xử lý từng dòng chữ thuần
            pos = a
            for line in raw.split('\n'):
                st = line.strip()
                if st and is_text_line(st):
                    ls = pos + (len(line) - len(line.lstrip()))
                    make_hit(ls, ls + len(st))
                elif st and has_letters(st) and not re.search(r'[;{}]|=>|&&|\|\|', st) \
                        and not st.startswith(('@', 'else', '//')):
                    manual.append((s.count('\n', 0, pos) + 1, st))
                pos += len(line) + 1
            return
        make_hit(a, b)

    def make_hit(a, b):
        raw = s[a:b]
        core = raw.strip()
        if not core:
            return
        lead = raw[:len(raw) - len(raw.lstrip())]
        trail = raw[len(raw.rstrip()):]
        toks = tokenize(core)
        if toks is None:
            return
        built = build_template(toks)
        if built is None:
            return
        tpl, exprs = built
        hits.append(Hit(start=a, end=b, kind='text', template=tpl, exprs=exprs, lead=lead, trail=trail,
                        attr=None, quote=None, line=s.count('\n', 0, a) + 1))

    while i < n:
        if regions.excluded[i]:
            # đóng đoạn text trước vùng loại trừ
            if text_start < i:
                handle_text(text_start, i)
            while i < n and regions.excluded[i]:
                i += 1
            text_start = i
            continue
        c = s[i]
        # bỏ qua biểu thức @(...) và điều kiện @if/@foreach (...) — có thể chứa '<' của C#
        if c == '@' and i + 1 < n:
            if s[i + 1] == '(':
                i = match_brace(s, i + 1, '(', ')') + 1
                continue
            m = re.match(r'@(if|foreach|for|while|switch|using|lock)\s*\(', s[i:i + 20])
            if m:
                # điều kiện của lệnh điều khiển không phải chữ hiển thị → cắt đoạn text tại đây
                if text_start < i:
                    handle_text(text_start, i)
                i = match_brace(s, i + m.end() - 1, '(', ')') + 1
                text_start = i
                continue
        if c == 'e' and s.startswith('else if', i):
            k = s.find('(', i)
            if 0 <= k < i + 12:
                if text_start < i:
                    handle_text(text_start, i)
                i = match_brace(s, k, '(', ')') + 1
                text_start = i
                continue
        # '<' liền sau định danh mà tên sau nó không phải thẻ HTML → generic C# (List<string>), không phải thẻ
        if c == '<' and i + 1 < n and (s[i + 1].isalpha() or s[i + 1] == '/') \
                and not (i > 0 and IDENT_CHAR.match(s[i - 1]) and s[i + 1] != '/'
                         and re.match(r'[A-Za-z][\w]*', s[i + 1:i + 40]).group(0) not in HTML_TAGS):
            if text_start < i:
                handle_text(text_start, i)
            e = scan_tag_end(s, i)
            tag = s[i:e + 1]
            if not tag.startswith('</'):
                find_attr_hits(s, i, e, hits)
            i = e + 1
            text_start = i
            continue
        i += 1
    if text_start < n:
        handle_text(text_start, n)
    return hits, manual


def find_attr_hits(s, a, b, hits):
    tag = s[a:b + 1]
    for m in ATTR_RE.finditer(tag):
        name = m.group(1)
        if name not in TEXT_ATTRS:
            continue
        q = m.group(2)
        vs = a + m.end()
        # tìm dấu đóng (có thể chứa @(...))
        j = vs
        while j < b:
            if s[j] == '@' and s[j + 1] == '(':
                j = match_brace(s, j + 1, '(', ')') + 1
                continue
            if s[j] == '@' and IDENT_START.match(s[j + 1]):
                j, _ = parse_implicit(s, j + 1)   # @x.ToString("...") có thể chứa dấu "
                continue
            if s[j] == q:
                break
            j += 1
        val = s[vs:j]
        if not val.strip() or val.strip().startswith('@'):
            continue
        if DATE_FMT_RE.match(val.strip()):
            continue
        if name == 'alt' and val.strip() in NO_TRANSLATE:
            continue
        if re.match(r'^[\w\-./:]+$', val.strip()) and not re.match(r'^[A-Z][a-z]', val.strip()):
            # dạng định danh/đường dẫn (vd. "email", "/img/x.png") → bỏ qua
            continue
        toks = tokenize(val)
        if toks is None:
            continue
        built = build_template(toks)
        if built is None:
            continue
        tpl, exprs = built
        hits.append(Hit(start=vs, end=j, kind='attr', template=tpl, exprs=exprs, lead='', trail='',
                        attr=name, quote=q, line=s.count('\n', 0, vs) + 1))


# ─────────────────────────── Chuỗi C# ───────────────────────────
CS_STR_RE = re.compile(r'(\$@|@\$|\$|@)?"')
NON_DISPLAY_CALL = re.compile(
    r'(?:^|\.)(NavigateTo|InvokeVoidAsync|InvokeAsync|GetFromJsonAsync|PostAsJsonAsync|PutAsJsonAsync|PatchAsJsonAsync|DeleteAsync|'
    r'GetAsync|PostAsync|PutAsync|SendAsync|SetItemAsync|GetItemAsync|RemoveItemAsync|GetItemAsStringAsync|SetItemAsStringAsync|'
    r'ContainKeyAsync|TryGetProperty|GetProperty|HasAccess|Can|CanView|CanExport|CanDownload|IsInRole|FindFirst|'
    r'Equals|StartsWith|EndsWith|Contains|IndexOf|Replace|Split|Trim|TrimStart|TrimEnd|ToString|ParseExact|TryParseExact|'
    r'Regex\.\w+|Matches|Match|IsMatch|Join|Add|Headers\.\w+|MediaTypeHeaderValue|StringContent|GetString|Combine|'
    r'HttpRequestMessage|Uri|AddRange|Remove|ContainsKey|TryGetValue|GetValueOrDefault|Format|Log|WriteLine|'
    r'LogInformation|LogWarning|LogError|Debug|Assert|nameof|typeof|Parse|TryParse|Exists|Select|Where|OrderBy|'
    r'T|TE|TA|Get|Enum|Api|Tr|Date)\s*(<[^>]*>)?\s*\($')


def iter_cs_strings(s, a, b):
    """Duyệt chuỗi C# trong s[a:b] → (start, end, prefix, content)."""
    j = a
    while j < b:
        c = s[j]
        if c == '/' and s[j + 1:j + 2] == '/':
            k = s.find('\n', j)
            j = b if k < 0 else k
            continue
        if c == '/' and s[j + 1:j + 2] == '*':
            k = s.find('*/', j + 2)
            j = b if k < 0 else k + 2
            continue
        if c == "'":
            if s[j + 1:j + 2] == '\\':
                k = s.find("'", j + 2)
                j = (k + 1) if 0 < k < j + 8 else j + 1
                continue
            if s[j + 2:j + 3] == "'":
                j += 3
                continue
        m = CS_STR_RE.match(s, j)
        if m and (m.group(1) is None or True):
            if m.group(1) is None or s[j] in '@$':
                start = j
                end = skip_cs_string(s, j)
                prefix = m.group(1) or ''
                content = s[start + len(prefix) + 1:end - 1]
                yield start, end, prefix, content
                j = end
                continue
        j += 1


def code_ranges(s, regions):
    """Vùng có thể chứa chuỗi C#: @code, @{...}, biểu thức @(...) và thuộc tính @xxx="..." trong markup."""
    ranges = list(regions.code_blocks)
    n = len(s)
    i = 0
    while i < n:
        if regions.excluded[i]:
            i += 1
            continue
        if s[i] == '@' and i + 1 < n:
            if s[i + 1] == '(':
                e = match_brace(s, i + 1, '(', ')')
                ranges.append((i + 2, e))
                i = e + 1
                continue
            m = re.match(r'@(if|foreach|for|while|switch|else\s+if)\s*\(', s[i:i + 20])
            if m:
                p = i + m.end() - 1
                e = match_brace(s, p, '(', ')')
                ranges.append((p + 1, e))
                i = e + 1
                continue
            if IDENT_START.match(s[i + 1]) and (i == 0 or not IDENT_CHAR.match(s[i - 1])):
                e, expr = parse_implicit(s, i + 1)
                if '(' in expr:
                    ranges.append((i + 1, e))
                i = max(e, i + 1)
                continue
        # thuộc tính directive: @onclick="..." / @bind="..." / Attr="@(...)" / OnClick="() => ..."
        if s[i] == '=' and i + 1 < n and s[i + 1] == '"':
            k = i - 1
            while k > 0 and (s[k].isalnum() or s[k] in ':-_@'):
                k -= 1
            name = s[k + 1:i]
            q = i + 1
            # tìm dấu " đóng: thuộc tính có lambda có thể chứa "..." lồng nhau → match theo ngoặc
            j = q + 1
            depth = 0
            while j < n:
                ch = s[j]
                if ch in '([{':
                    depth += 1
                elif ch in ')]}':
                    depth -= 1
                elif ch == '"' and depth <= 0:
                    break
                elif ch == '"' and depth > 0:
                    j = skip_cs_string(s, j)
                    continue
                elif ch == '\n' and depth <= 0:
                    break
                j += 1
            if name.startswith('@') or '=>' in s[q:j] or s[q + 1:q + 2] == '@':
                ranges.append((q + 1, j))
            i = j + 1
            continue
        i += 1
    return ranges


def literal_candidates(s, regions):
    out = []
    seen = set()
    for a, b in code_ranges(s, regions):
        for start, end, prefix, content in iter_cs_strings(s, a, b):
            if start in seen:
                continue
            seen.add(start)
            if 'verbatim' and '@' in prefix and '$' not in prefix:
                pass
            text = content
            if not has_letters(re.sub(r'\{[^}]*\}', '', text) if '$' in prefix else text):
                continue
            before = s[max(0, start - 60):start]
            after = s[end:end + 20]
            b_strip = before.rstrip()
            # so sánh / case / chỉ mục / tham số của hàm không hiển thị
            if re.search(r'(==|!=|\bcase|\bis|\bnot|\?\?=|=>\s*\w+\s*==)\s*$', b_strip):
                continue
            if re.match(r'\s*(==|!=|=>|:\s*$)', after) and not re.match(r'\s*:\s*\S', after):
                pass
            if re.match(r'\s*(==|!=|=>)', after):
                continue           # so sánh / nhánh switch "X" => ...
            if '@' in text:
                continue           # thuộc tính razor trong markup viết trong @code
            if re.search(r'\[\s*$', b_strip):
                continue
            call = re.search(r'([\w.]+)\s*(<[^>]*>)?\s*\(\s*([^()]*,\s*)?$', b_strip)
            if call and NON_DISPLAY_CALL.search(call.group(1) + '(') \
                    and not re.search(r'"(alert|confirm|prompt)"', call.group(3) or ''):
                continue           # (riêng JS alert/confirm/prompt: tham số thứ 2 là chữ hiển thị)
            plain = text.strip()
            if re.match(r'^[\w\-./:#?&=%]+$', plain) and not re.match(r'^[A-Z][a-z]+$', plain):
                continue           # định danh, url, class css, key
            if plain.startswith(('/', 'api/', 'http', 'data:', 'image/', 'application/', 'text/', '#', '.', '<', 'js', 'window.')):
                continue
            if DATE_FMT_RE.match(plain) or re.match(r'^[NFCPDXE]\d?$', plain):
                continue
            if re.search(r'(document\.|function\s*\(|=>|\bvar\b|;\s*$|getElementById|querySelector)', plain):
                continue
            if re.match(r'^[a-z][\w-]*(\s+[a-z][\w-]*)*$', plain) and ' ' in plain and '-' in plain:
                continue           # danh sách class css "btn btn-primary"
            if plain in NO_TRANSLATE:
                continue
            line = s.count('\n', 0, start) + 1
            out.append((start, end, prefix, content, line))
    out.sort()
    return out


# ─────────────────────────── Đặt key ───────────────────────────
class KeyMaker:
    def __init__(self, en, prefix, common=None):
        self.en = en
        self.prefix = prefix
        self.common = common or {}
        self.by_value = defaultdict(list)
        for k, v in en.items():
            self.by_value[v].append(k)

    def key_for(self, tpl):
        # Dùng lại key đã có cùng nội dung: ưu tiên Common.*, rồi cùng trang
        keys = self.by_value.get(tpl, [])
        for pref in ('Common.', self.prefix + '.'):
            for k in keys:
                if k.startswith(pref):
                    return k, False
        if tpl in self.common:
            base = 'Common.' + slug(tpl)
        else:
            base = self.prefix + '.' + slug(tpl)
        k = base
        n = 2
        while k in self.en and self.en[k] != tpl:
            k = f'{base}{n}'
            n += 1
        is_new = k not in self.en
        if is_new:
            self.en = insert_key(self.en, k, tpl)
            self.by_value[tpl].append(k)
        return k, is_new


def prefix_of(path):
    return os.path.splitext(os.path.basename(path))[0]


def cs_quote(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n') + '"'


def render_call(key, exprs, fn='T'):
    if exprs:
        return f'{fn}({cs_quote(key)}, {", ".join(exprs)})'
    return f'{fn}({cs_quote(key)})'


def ensure_inherits(s, path):
    """Thêm @inherits LocalizedComponentBase (layout: LocalizedLayoutBase) + đổi Dispose nếu cần."""
    if re.search(r'^\s*@inherits\s+Localized', s, re.M):
        return s
    is_layout = re.search(r'^\s*@inherits\s+LayoutComponentBase', s, re.M) is not None
    base = 'LocalizedLayoutBase' if is_layout else 'LocalizedComponentBase'
    if is_layout:
        s = re.sub(r'^(\s*)@inherits\s+LayoutComponentBase', r'\1@inherits ' + base, s, count=1, flags=re.M)
    elif re.search(r'^\s*@inherits\b', s, re.M):
        print(f'  ! {os.path.basename(path)} đã @inherits lớp khác — cần xử lý tay')
        return s
    else:
        # chèn sau các dòng directive đầu file
        m = None
        for m in re.finditer(r'^@(page|attribute|using|inject|layout|implements|typeparam|rendermode)\b.*\n', s, re.M):
            pass
        pos = m.end() if m else 0
        s = s[:pos] + f'@inherits {base}\n' + s[pos:]
    # Dispose: lớp cha đã cài IDisposable → đổi về override Dispose(bool)
    if re.search(r'^\s*@implements\s+IDisposable\s*$', s, re.M):
        s = re.sub(r'^\s*@implements\s+IDisposable\s*\n', '', s, count=1, flags=re.M)
        m = re.search(r'public\s+void\s+Dispose\s*\(\s*\)\s*=>\s*([^;]+);', s)
        if m:
            s = s[:m.start()] + 'protected override void Dispose(bool disposing) { base.Dispose(disposing); ' + m.group(1).strip() + '; }' + s[m.end():]
        else:
            m = re.search(r'public\s+void\s+Dispose\s*\(\s*\)\s*\{', s)
            if m:
                s = s[:m.start()] + 'protected override void Dispose(bool disposing)\n    {\n        base.Dispose(disposing);' + s[m.end():]
            else:
                print(f'  ! {os.path.basename(path)}: không tìm thấy Dispose() để đổi')
    return s


# ─────────────────────────── Lệnh ───────────────────────────
def razor_files(args):
    if args:
        return [os.path.abspath(a) for a in args]
    files = []
    for p in glob.glob(os.path.join(CLIENT, '**', '*.razor'), recursive=True):
        parts = p.split(os.sep)
        if 'obj' in parts or 'bin' in parts:
            continue
        if os.path.basename(p) in ('_Imports.razor',):
            continue
        files.append(p)
    return sorted(files)


def cmd_scan(args):
    total = 0
    for p in razor_files(args):
        s, _, _ = read_file(p)
        reg = Regions(s)
        hits, manual = find_markup_hits(s, reg)
        lits = literal_candidates(s, reg)
        if not hits and not manual and not lits:
            continue
        rel = os.path.relpath(p, CLIENT)
        print(f'\n== {rel}: {len(hits)} markup, {len(lits)} chuỗi C#, {len(manual)} cần xem tay')
        for h in hits:
            print(f'  L{h.line:<5} {"@" + h.attr if h.attr else "text":<14} {h.template!r}')
        for (_, _, prefix, content, line) in lits:
            print(f'  L{line:<5} {"cs":<14} {prefix}"{content}"')
        for line, t in manual:
            print(f'  L{line:<5} {"?manual":<14} {t!r}')
        total += len(hits) + len(lits) + len(manual)
    print(f'\nTổng: {total}')


def collect_common(files, en):
    counter = defaultdict(set)
    for p in files:
        s, _, _ = read_file(p)
        hits, _ = find_markup_hits(s, Regions(s))
        for h in hits:
            if not h.exprs and len(h.template.split()) <= 4:
                counter[h.template].add(prefix_of(p))
    return {t for t, ps in counter.items() if len(ps) >= 3}


def cmd_apply(args):
    files = razor_files(args)
    en = load_lang('en')
    common = collect_common(razor_files([]), en) if not args or len(files) > 1 else set()
    new_keys = 0
    for p in files:
        s, bom, crlf = read_file(p)
        reg = Regions(s)
        hits, manual = find_markup_hits(s, reg)
        if not hits:
            for line, t in manual:
                print(f'  ? {os.path.basename(p)} L{line}: {t!r}')
            continue
        km = KeyMaker(en, prefix_of(p), common)
        for h in sorted(hits, key=lambda h: -h.start):
            key, is_new = km.key_for(h.template)
            new_keys += is_new
            if h.kind == 'text':
                rep = h.lead + '@' + render_call(key, h.exprs) + h.trail
            else:
                rep = '@' + render_call(key, h.exprs)
                if h.quote == '"':
                    # trong thuộc tính "..." vẫn dùng được "..." lồng nhau với razor
                    pass
            s = s[:h.start] + rep + s[h.end:]
        en = km.en
        s = ensure_inherits(s, p)
        write_file(p, s, bom, crlf)
        print(f'  ✓ {os.path.relpath(p, CLIENT)}: {len(hits)} chỗ')
        for line, t in manual:
            print(f'    ? L{line}: {t!r}')
    save_lang('en', en)
    print(f'Đã thêm {new_keys} key mới vào en.json')


def cmd_literals(args):
    p = os.path.abspath(args[0])
    s, _, _ = read_file(p)
    reg = Regions(s)
    for idx, (start, end, prefix, content, line) in enumerate(literal_candidates(s, reg), 1):
        ctx = s[max(0, start - 40):start].split('\n')[-1].strip()
        print(f'#{idx:<4} L{line:<5} {prefix}"{content}"    ‹ {ctx[-40:]}')


def parse_ids(spec, n):
    if spec == 'all':
        return set(range(1, n + 1))
    ids = set()
    for part in spec.split(','):
        part = part.strip()
        if not part:
            continue
        if '-' in part:
            a, b = part.split('-')
            ids.update(range(int(a), int(b) + 1))
        else:
            ids.add(int(part))
    return ids


def convert_interpolated(content):
    """Nội dung $"..." → (template, [exprs]); None nếu phức tạp."""
    tpl = []
    exprs = []
    i = 0
    n = len(content)
    while i < n:
        c = content[i]
        if c == '{':
            if content[i + 1:i + 2] == '{':
                tpl.append('{{')
                i += 2
                continue
            e = match_brace(content, i, '{', '}')
            inner = content[i + 1:e]
            fmt = ''
            # {expr:format} / {expr,align}
            depth = 0
            split_at = None
            for k, ch in enumerate(inner):
                if ch in '([':
                    depth += 1
                elif ch in ')]':
                    depth -= 1
                elif ch in ':,' and depth == 0 and '?' not in inner[:k]:
                    split_at = k
                    break
            if split_at is not None:
                fmt = inner[split_at:]
                inner = inner[:split_at]
            tpl.append('{%d%s}' % (len(exprs), fmt))
            exprs.append(inner.strip())
            i = e + 1
            continue
        if c == '}' and content[i + 1:i + 2] == '}':
            tpl.append('}}')
            i += 2
            continue
        tpl.append(c)
        i += 1
    return ''.join(tpl), exprs


def unescape_cs(content, verbatim):
    if verbatim:
        return content.replace('""', '"')
    return bytes(content, 'utf-8').decode('unicode_escape').encode('latin-1').decode('utf-8') if '\\' in content else content


def cmd_wrap(args):
    p = os.path.abspath(args[0])
    s, bom, crlf = read_file(p)
    reg = Regions(s)
    cands = literal_candidates(s, reg)
    ids = parse_ids(args[1] if len(args) > 1 else 'all', len(cands))
    en = load_lang('en')
    km = KeyMaker(en, prefix_of(p))
    chosen = [c for idx, c in enumerate(cands, 1) if idx in ids]
    count = 0
    for start, end, prefix, content, line in sorted(chosen, key=lambda c: -c[0]):
        verbatim = '@' in prefix
        if '$' in prefix:
            tpl, exprs = convert_interpolated(content)
            tpl = unescape_cs(tpl, verbatim)
        else:
            tpl, exprs = unescape_cs(content, verbatim).replace('{', '{{').replace('}', '}}'), []
        key, _ = km.key_for(tpl)
        s = s[:start] + render_call(key, exprs) + s[end:]
        count += 1
    write_file(p, ensure_inherits(s, p), bom, crlf)
    save_lang('en', km.en)
    print(f'Đã bọc {count} chuỗi trong {os.path.basename(p)}')


DECL_RE = re.compile(r'^(\s*)((?:private|protected|public|internal)\s+)?(static\s+)?(readonly\s+)?'
                     r'([A-Za-z_][\w<>\[\],?.() ]*?)\s+([A-Za-z_]\w*)\s*=(?!=|>)')
METHOD_RE = re.compile(r'^\s*((?:private|protected|public|internal)\s+)?static\s+(?!readonly)(?!class)[^=;{}]*\(')


def fix_file_errors(path, errors):
    """errors: list (line, code). Sửa CS0236 (field initializer dùng T) và CS0120 (hàm static dùng T)."""
    s, bom, crlf = read_file(path)
    lines = s.split('\n')
    changed = 0
    done_decl = set()
    for line_no, code in sorted(set(errors), key=lambda x: -x[0]):
        idx = line_no - 1
        if code == 'CS0120':
            # tìm khai báo hàm static bao quanh
            for k in range(idx, max(-1, idx - 80), -1):
                if METHOD_RE.match(lines[k]):
                    if k not in done_decl:
                        lines[k] = re.sub(r'\bstatic\s+', '', lines[k], count=1)
                        done_decl.add(k)
                        changed += 1
                    break
                m = DECL_RE.match(lines[k])
                if m and m.group(3):
                    idx = k + 1
                    code = 'CS0236'
                    break
        if code == 'CS0236':
            for k in range(idx, max(-1, idx - 60), -1):
                m = DECL_RE.match(lines[k])
                if not m:
                    continue
                if k in done_decl:
                    break
                if not (m.group(4) or m.group(3)):
                    print(f'  ! {os.path.basename(path)} L{k + 1}: trường không readonly dùng T() — sửa tay (chuyển sang property/khởi tạo trong OnInitialized)')
                    break
                text = '\n'.join(lines[k:])
                eq = m.end() - 1
                # tìm ';' kết thúc ở độ sâu 0
                j = eq + 1
                depth = 0
                while j < len(text):
                    ch = text[j]
                    if ch in '"\'' or (ch in '@$' and text[j + 1:j + 2] in '"$@'):
                        if ch == "'":
                            j += 1
                            continue
                        j = skip_cs_string(text, j)
                        continue
                    if ch in '([{':
                        depth += 1
                    elif ch in ')]}':
                        depth -= 1
                    elif ch == ';' and depth == 0:
                        break
                    j += 1
                init = text[eq + 1:j].strip()
                typ = m.group(5).strip()
                if init.startswith('{'):
                    init = 'new ' + typ + ' ' + init if not typ.endswith('[]') else 'new[] ' + init
                elif init.startswith('new()') or init.startswith('new ()'):
                    init = 'new ' + typ + init[init.index('('):]
                new_decl = f'{m.group(1)}{m.group(2) or ""}{typ} {m.group(6)} => {init};'
                rest = text[j + 1:]
                lines = lines[:k] + (new_decl + rest).split('\n')
                done_decl.add(k)
                changed += 1
                break
    if changed:
        write_file(path, '\n'.join(lines), bom, crlf)
    return changed


def cmd_fix(args):
    import subprocess
    proj = os.path.join(CLIENT, 'InticooInspection.Client.csproj')
    for attempt in range(6):
        r = subprocess.run(['dotnet', 'build', proj, '-nologo', '-v', 'q'], capture_output=True, text=True,
                           encoding='utf-8', errors='replace')
        errs = defaultdict(list)
        other = set()
        for m in re.finditer(r'^(.*?\.razor)\((\d+),\d+\): error (CS\d+): (.*?)(?: \[|$)', r.stdout, re.M):
            f, ln, code, msg = m.group(1), int(m.group(2)), m.group(3), m.group(4)
            if code in ('CS0236', 'CS0120') and 'Localized' in msg:
                errs[f.strip()].append((ln, code))
            else:
                other.add(f'{os.path.basename(f)}({ln}): {code} {msg}')
        if not errs:
            if other:
                print('Còn lỗi build cần sửa tay:')
                for o in sorted(other):
                    print('  ', o)
            else:
                print('✓ Build OK')
            return 0 if not other else 1
        total = 0
        for f, e in errs.items():
            total += fix_file_errors(f, e)
        print(f'Lần {attempt + 1}: đã sửa {total} khai báo')
        if total == 0:
            for o in sorted(other):
                print('  ', o)
            return 1
    return 1


KEY_USE_RE = re.compile(r'\b(?:T|L\.Get|L)\s*\(\s*"([A-Za-z][\w]*\.[^"]+)"|L\[\s*"([^"]+)"')


def cmd_check(args):
    langs = {l: load_lang(l) for l in LANGS}
    en = langs['en']
    used = set()
    for p in glob.glob(os.path.join(CLIENT, '**', '*.*'), recursive=True):
        parts = p.split(os.sep)
        if 'obj' in parts or 'bin' in parts or not p.endswith(('.razor', '.cs')):
            continue
        s = open(p, encoding='utf-8-sig').read()
        for m in KEY_USE_RE.finditer(s):
            used.add(m.group(1) or m.group(2))
    dynamic_prefixes = ('Status.', 'Result.', 'Api.', 'Enum.', 'Role.', 'Opt.', 'Date.', 'Menu.')
    missing_en = sorted(k for k in used if k not in en)
    unused = sorted(k for k in en if k not in used and not k.startswith(dynamic_prefixes))
    ok = True
    if missing_en:
        ok = False
        print(f'✗ {len(missing_en)} key dùng trong code nhưng KHÔNG có trong en.json:')
        for k in missing_en:
            print('   ', k)
    for l in LANGS[1:]:
        miss = [k for k in en if not langs[l].get(k)]
        extra = [k for k in langs[l] if k not in en]
        if miss:
            ok = False
            print(f'✗ {l}.json thiếu {len(miss)} bản dịch (python tools/i18n/i18n.py export {l})')
            for k in miss[:15]:
                print('   ', k)
            if len(miss) > 15:
                print('    ...')
        if extra:
            print(f'! {l}.json có {len(extra)} key không còn trong en.json (python tools/i18n/i18n.py sync)')
        # placeholder {0} phải khớp với bản tiếng Anh
        for k, v in en.items():
            t = langs[l].get(k)
            # bản dịch được phép bỏ bớt placeholder (vd. hậu tố số nhiều "s"), không được thêm placeholder lạ
            if t and not set(re.findall(r'\{(\d+)', t)) <= set(re.findall(r'\{(\d+)', v)):
                ok = False
                print(f'✗ {l}.json "{k}": placeholder không khớp — en: {v!r} / {l}: {t!r}')
    if unused:
        print(f'! {len(unused)} key trong en.json không thấy dùng (có thể dùng động):')
        for k in unused[:20]:
            print('   ', k)
    # chữ cứng còn sót
    left = 0
    for p in razor_files([]):
        s, _, _ = read_file(p)
        hits, _ = find_markup_hits(s, Regions(s))
        if hits:
            left += len(hits)
            print(f'! {os.path.relpath(p, CLIENT)}: còn {len(hits)} chữ cứng trong markup (python tools/i18n/i18n.py apply {os.path.relpath(p, ROOT)})')
    print('✓ OK' if ok and not left else '— xem các mục ở trên')
    return 0 if ok else 1


def cmd_sync(args):
    en = load_lang('en')
    for l in LANGS[1:]:
        cur = load_lang(l)
        save_lang(l, OrderedDict((k, cur.get(k, '')) for k in en))
    save_lang('en', en)
    print('Đã đồng bộ thứ tự key cho', ', '.join(LANGS[1:]))


def cmd_export(args):
    lang = args[0]
    out = args[1] if len(args) > 1 else os.path.join(ROOT, 'tools', 'i18n', f'missing_{lang}.tsv')
    en = load_lang('en')
    cur = load_lang(lang)
    rows = [(k, v) for k, v in en.items() if not cur.get(k)]
    with open(out, 'w', encoding='utf-8', newline='\n') as f:
        for k, v in rows:
            f.write(f'{k}\t{v}\t\n')
    print(f'Đã xuất {len(rows)} dòng → {out}')


def cmd_import(args):
    lang, src = args[0], args[1]
    en = load_lang('en')
    cur = load_lang(lang)
    n = 0
    with open(src, encoding='utf-8-sig') as f:
        for line in f:
            line = line.rstrip('\n')
            if not line.strip():
                continue
            parts = line.split('\t')
            if len(parts) < 3 or not parts[2].strip():
                continue
            k = parts[0].strip()
            if k in en:
                cur[k] = parts[2].strip()
                n += 1
    save_lang(lang, OrderedDict((k, cur.get(k, '')) for k in en))
    print(f'Đã nhập {n} bản dịch vào {lang}.json')


TM_DIR = os.path.join(ROOT, 'tools', 'i18n', 'tm')


def load_tm():
    """tm/*.tsv: en<TAB>vi<TAB>zh<TAB>ms (dịch theo câu tiếng Anh, dùng chung cho mọi key cùng nội dung).
    tm/overrides.tsv: key<TAB>vi<TAB>zh<TAB>ms (ép bản dịch cho từng key cụ thể)."""
    tm, ov = {}, {}
    for p in sorted(glob.glob(os.path.join(TM_DIR, '*.tsv'))):
        target = ov if os.path.basename(p) == 'overrides.tsv' else tm
        with open(p, encoding='utf-8-sig') as f:
            for line in f:
                line = line.rstrip('\r\n')
                if not line.strip():
                    continue
                parts = line.split('\t')
                if len(parts) < 4:
                    print(f'  ! {os.path.basename(p)}: dòng thiếu cột: {line[:60]!r}')
                    continue
                key = parts[0].replace('\\n', '\n')
                target[key] = [x.replace('\\n', '\n') for x in parts[1:4]]
    return tm, ov


def cmd_tm(args):
    """Điền bản dịch còn thiếu cho vi/zh/ms từ translation memory (không ghi đè bản dịch đã có, trừ overrides)."""
    en = load_lang('en')
    tm, ov = load_tm()
    for i, l in enumerate(LANGS[1:]):
        cur = load_lang(l)
        n = 0
        for k, v in en.items():
            if k in ov and ov[k][i]:
                if cur.get(k) != ov[k][i]:
                    cur[k] = ov[k][i]
                    n += 1
            elif not cur.get(k) and v in tm and tm[v][i]:
                cur[k] = tm[v][i]
                n += 1
        save_lang(l, OrderedDict((k, cur.get(k, '')) for k in en))
        missing = sum(1 for k in en if not cur.get(k))
        print(f'{l}: điền {n} bản dịch, còn thiếu {missing}')


def cmd_set(args):
    key, vals = args[0], args[1:]
    for l, v in zip(LANGS, vals):
        d = load_lang(l)
        d = insert_key(d, key, v)
        save_lang(l, d)
    cmd_sync([])


def main():
    if len(sys.argv) < 2 or sys.argv[1] in ('-h', '--help', 'help'):
        print(__doc__)
        return 0
    cmd, args = sys.argv[1], sys.argv[2:]
    fn = {'scan': cmd_scan, 'apply': cmd_apply, 'literals': cmd_literals, 'wrap': cmd_wrap, 'check': cmd_check,
          'sync': cmd_sync, 'export': cmd_export, 'import': cmd_import, 'set': cmd_set, 'fix': cmd_fix, 'tm': cmd_tm}.get(cmd)
    if not fn:
        print(__doc__)
        return 2
    return fn(args) or 0


if __name__ == '__main__':
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
    sys.exit(main())
