// Trình soạn thảo bài viết đơn giản (contenteditable) cho màn Resources — không dùng thư viện ngoài (CSP script-src 'self').
// HTML được server lọc lại bằng HtmlSanitizer khi lưu, nên ở đây chỉ lo trải nghiệm soạn thảo.
(function () {
  var form = document.getElementById('resform');
  if (!form) return;
  var token = (form.querySelector('input[name="__RequestVerificationToken"]') || {}).value || '';

  var TOOLS = [
    ['P', 'Paragraph', function () { exec('formatBlock', '<p>'); }],
    ['H2', 'Heading', function () { exec('formatBlock', '<h2>'); }],
    ['H3', 'Sub-heading', function () { exec('formatBlock', '<h3>'); }],
    ['|'],
    ['<b>B</b>', 'Bold', function () { exec('bold'); }],
    ['<i>I</i>', 'Italic', function () { exec('italic'); }],
    ['<u>U</u>', 'Underline', function () { exec('underline'); }],
    ['|'],
    ['• List', 'Bulleted list', function () { exec('insertUnorderedList'); }],
    ['1. List', 'Numbered list', function () { exec('insertOrderedList'); }],
    ['❝', 'Quote', function () { exec('formatBlock', '<blockquote>'); }],
    ['—', 'Divider', function () { exec('insertHorizontalRule'); }],
    ['|'],
    ['Link', 'Insert link', function () {
      var u = window.prompt('Link address (https://...)', 'https://');
      if (u && /^(https?:|mailto:|\/)/i.test(u)) exec('createLink', u);
    }],
    ['Unlink', 'Remove link', function () { exec('unlink'); }],
    ['Image', 'Insert image (JPG/PNG/WEBP, max 5 MB)', null, 'image'],
    ['|'],
    ['Clear', 'Clear formatting', function () { exec('removeFormat'); exec('formatBlock', '<p>'); }],
    ['↶', 'Undo', function () { exec('undo'); }],
    ['↷', 'Redo', function () { exec('redo'); }],
    ['HTML', 'Edit HTML source', null, 'source']
  ];

  function exec(cmd, val) { document.execCommand(cmd, false, val || null); }

  document.querySelectorAll('.rte').forEach(function (box) {
    var ta = box.querySelector('textarea');
    var bar = document.createElement('div'); bar.className = 'rte-bar';
    var ed = document.createElement('div'); ed.className = 'rte-ed'; ed.contentEditable = 'true'; ed.innerHTML = ta.value || '<p><br></p>';
    var src = document.createElement('textarea'); src.className = 'rte-src'; src.hidden = true;
    var file = document.createElement('input'); file.type = 'file'; file.accept = '.jpg,.jpeg,.png,.webp'; file.hidden = true;
    var saved = null;

    TOOLS.forEach(function (t) {
      if (t[0] === '|') { var s = document.createElement('span'); s.className = 'sep'; bar.appendChild(s); return; }
      var b = document.createElement('button'); b.type = 'button'; b.innerHTML = t[0]; b.title = t[1];
      b.addEventListener('mousedown', function (e) { e.preventDefault(); });   // giữ vùng chọn trong editor
      b.addEventListener('click', function () {
        if (t[3] === 'source') {
          if (src.hidden) { src.value = ed.innerHTML; src.hidden = false; ed.hidden = true; b.classList.add('on'); }
          else { ed.innerHTML = src.value; src.hidden = true; ed.hidden = false; b.classList.remove('on'); }
          return;
        }
        if (src.hidden === false) return;
        ed.focus();
        if (t[3] === 'image') { var sel = window.getSelection(); saved = sel.rangeCount ? sel.getRangeAt(0).cloneRange() : null; file.click(); return; }
        t[2]();
      });
      bar.appendChild(b);
    });

    file.addEventListener('change', function () {
      var f = file.files[0]; if (!f) return;
      var fd = new FormData(); fd.append('image', f);
      bar.classList.add('busy');
      fetch(box.dataset.upload, { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' })
        .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
        .then(function (x) {
          if (!x.ok || !x.j.url) { window.alert(x.j.error || 'Upload failed.'); return; }
          ed.focus();
          if (saved) { var sel = window.getSelection(); sel.removeAllRanges(); sel.addRange(saved); }
          exec('insertImage', x.j.url);
        })
        .catch(function () { window.alert('Upload failed.'); })
        .finally(function () { bar.classList.remove('busy'); file.value = ''; });
    });

    // Dán: chỉ lấy chữ thuần để tránh mang theo định dạng Word/web lộn xộn
    ed.addEventListener('paste', function (e) {
      var text = (e.clipboardData || window.clipboardData).getData('text/plain');
      if (!text) return;
      e.preventDefault();
      var html = text.split(/\r?\n\r?\n/).map(function (p) {
        return '<p>' + p.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\r?\n/g, '<br>') + '</p>';
      }).join('');
      exec('insertHTML', html);
    });

    box.appendChild(bar); box.appendChild(ed); box.appendChild(src); box.appendChild(file);
    box._sync = function () { ta.value = src.hidden ? ed.innerHTML : src.value; };
  });

  form.addEventListener('submit', function () { document.querySelectorAll('.rte').forEach(function (b) { b._sync && b._sync(); }); });

  // Ẩn/hiện ô theo mục (Section) + lọc Type theo mục
  var sec = document.getElementById('secSel'), kind = document.getElementById('kindSel');
  function secRefresh() {
    var s = sec.value;
    document.querySelectorAll('[data-hide-for]').forEach(function (el) { el.hidden = el.dataset.hideFor.split(' ').indexOf(s) >= 0; });
    document.querySelectorAll('[data-show-for]').forEach(function (el) { el.hidden = el.dataset.showFor.split(' ').indexOf(s) < 0; });
    var first = null, keep = false;
    Array.prototype.forEach.call(kind.options, function (o) {
      var ok = o.dataset.sec === s; o.hidden = !ok; o.disabled = !ok;
      if (ok && !first) first = o; if (ok && o.selected) keep = true;
    });
    if (!keep && first) first.selected = true;
  }
  sec.addEventListener('change', secRefresh); secRefresh();

  // Tab bản dịch
  document.querySelectorAll('#trTabs a').forEach(function (a) {
    a.addEventListener('click', function (e) {
      e.preventDefault();
      document.querySelectorAll('#trTabs a').forEach(function (x) { x.classList.toggle('on', x === a); });
      document.querySelectorAll('.tr-pane').forEach(function (p) { p.hidden = p.id !== 'tr-' + a.dataset.tr; });
    });
  });
})();
