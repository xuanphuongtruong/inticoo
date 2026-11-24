(function () {
  var form = document.getElementById('regform');
  if (!form) return;
  var $ = function (id) { return document.getElementById(id); };
  var cycleVal = $('cycleVal');
  var radios = form.querySelectorAll('input[name="Input.PlanCode"]');
  var mods = form.querySelectorAll('#mods .mod-opt');
  var money = function (v) { return '$' + Number(v).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); };

  function annual() { return cycleVal.value === 'Annual'; }

  // ── Order Summary + module theo gói ─────────────────────────
  function refresh() {
    var sel = form.querySelector('input[name="Input.PlanCode"]:checked');
    if (!sel) return;
    var a = annual(), price = Number(a ? sel.dataset.a : sel.dataset.m);
    var per = document.querySelector('.plan-pick .per');
    var perTxt = per ? (a ? per.dataset.y : per.dataset.m) : '';
    $('s-plan').textContent = sel.dataset.name;
    $('s-pop').style.display = sel.dataset.pop ? '' : 'none';
    var d = document.querySelector('[data-desc="' + sel.value + '"]'); $('s-desc').textContent = d ? d.textContent : '';
    var ic = sel.closest('.plan-opt').querySelector('.ic');
    if (ic) { var cs = getComputedStyle(ic); $('s-ic').innerHTML = ic.innerHTML; $('s-ic').style.background = cs.backgroundColor; $('s-ic').style.color = cs.color; }
    $('s-price').textContent = '$' + price.toLocaleString('en-US');
    $('s-per').textContent = perTxt; $('s-per2').textContent = perTxt;
    $('s-cycle').textContent = a ? $('s-cycle').dataset.a : $('s-cycle').dataset.m;
    $('s-base').textContent = money(price);
    $('s-total').textContent = money(price);
    var h = $('s-incl-h'); h.textContent = h.dataset.tpl.replace('{0}', sel.dataset.name);
    document.querySelectorAll('ul.incl').forEach(function (u) { u.hidden = u.dataset.plan !== sel.value; });
    var note = $('s-note'); note.textContent = note.dataset.tpl.replace('{0}', money(price)).replace('{1}', a ? note.dataset.a : note.dataset.m);
    var mh = $('modsHint'); if (mh) mh.textContent = mh.dataset.tpl.replace('{0}', sel.dataset.name);

    var allowed = (sel.dataset.mods || '').split(',');
    mods.forEach(function (m) {
      var cb = m.querySelector('input'), ok = allowed.indexOf(m.dataset.code) >= 0;
      m.classList.toggle('locked', !ok);
      cb.disabled = !ok;
      if (!ok) cb.checked = false; else if (!m.dataset.touched) cb.checked = true;
    });
    document.querySelectorAll('.plan-pick .p').forEach(function (p) {
      p.querySelector('.amt').textContent = '$' + Number(a ? p.dataset.a : p.dataset.m).toLocaleString('en-US');
      var pr = p.querySelector('.per'); if (pr) pr.textContent = a ? pr.dataset.y : pr.dataset.m;
    });
  }
  mods.forEach(function (m) { m.querySelector('input').addEventListener('change', function () { m.dataset.touched = '1'; }); });
  radios.forEach(function (r) { r.addEventListener('change', refresh); });
  document.querySelectorAll('#cycle button').forEach(function (b) {
    b.addEventListener('click', function () {
      document.querySelectorAll('#cycle button').forEach(function (x) { x.classList.remove('on'); });
      b.classList.add('on'); cycleVal.value = b.dataset.cycle; refresh();
    });
  });

  // ── Phương thức thanh toán: hiện ô thẻ / ghi chú PayPal / chuyển khoản ──
  var pay = form.querySelectorAll('input[name="Input.PaymentMethod"]');
  function payRefresh() {
    var v = form.querySelector('input[name="Input.PaymentMethod"]:checked'), m = v ? v.value : '';
    [['cardbox', 'Card'], ['ppbox', 'PayPal'], ['bankbox', 'BankTransfer']].forEach(function (x) { var e = $(x[0]); if (e) e.style.display = m === x[1] ? '' : 'none'; });
  }
  pay.forEach(function (p) { p.addEventListener('change', payRefresh); });

  // ── Review & Confirm ────────────────────────────────────────
  function val(n) { var e = form.querySelector('[name="' + n + '"]'); return e ? e.value.trim() : ''; }
  function review() {
    var co = val('Input.CompanyName'), em = val('Input.WorkEmail');
    $('r-co').textContent = co ? co + (em ? ' · ' + em : '') : '-';
    var an = val('Input.AdminName'), ae = val('Input.AdminEmail');
    $('r-admin').textContent = an || ae ? [an, ae].filter(Boolean).join(' · ') : '-';
    var langs = Array.prototype.map.call(form.querySelectorAll('input[name="Input.Languages"]:checked'), function (c) { return c.parentNode.querySelector('span').firstChild.textContent.trim(); });
    $('r-lang').textContent = langs.length ? langs.join(', ') : '-';
  }
  form.addEventListener('input', review); form.addEventListener('change', review);

  // ── Điện thoại: mã vùng theo nước ───────────────────────────
  var country = $('country'), dial = $('dial'), local = $('phoneLocal');
  if (country && dial) country.addEventListener('change', function () {
    var o = country.options[country.selectedIndex], d = o && o.dataset.dial;
    if (d) dial.value = d;
  });

  // ── Hiện/ẩn mật khẩu ────────────────────────────────────────
  var eye = $('pwEye'), pw = $('pw');
  if (eye && pw) eye.addEventListener('click', function () { pw.type = pw.type === 'password' ? 'text' : 'password'; });

  // định dạng nhập thẻ / hạn thẻ
  var num = form.querySelector('input[name="Input.CardNumber"]');
  if (num) num.addEventListener('input', function () { var d = num.value.replace(/\D/g, '').slice(0, 19); num.value = d.replace(/(.{4})/g, '$1 ').trim(); });
  var exp = form.querySelector('input[name="Input.CardExpiry"]');
  if (exp) exp.addEventListener('input', function () { var d = exp.value.replace(/\D/g, '').slice(0, 4); exp.value = d.length > 2 ? d.slice(0, 2) + ' / ' + d.slice(2) : d; });

  // stepper theo vị trí cuộn
  var steps = document.querySelectorAll('#stepper .st');
  if ('IntersectionObserver' in window) {
    var io = new IntersectionObserver(function (es) {
      es.forEach(function (e) {
        if (e.isIntersecting && window.scrollY >= 60) {
          var idx = Number(e.target.id.replace('sec', '')) - 1;
          steps.forEach(function (s, i) { s.classList.toggle('on', i === idx); });
        }
      });
    }, { rootMargin: '-30% 0px -60% 0px' });
    for (var i = 1; i <= 6; i++) { var s = $('sec' + i); if (s) io.observe(s); }
    // ở đầu trang luôn sáng bước 1 (khối 1 nằm trên vùng quan sát của IntersectionObserver)
    var top1 = function () { if (window.scrollY < 60) steps.forEach(function (s, i) { s.classList.toggle('on', i === 0); }); };
    window.addEventListener('scroll', top1, { passive: true }); setTimeout(top1, 50);
  }

  // Gửi: ghép mã vùng + số; server mong Expiry dạng "MM/YY"; chống gửi 2 lần
  form.addEventListener('submit', function () {
    var l = local ? local.value.trim() : '';
    $('phoneFull').value = !l ? '' : (l.charAt(0) === '+' || !dial ? l : dial.value + ' ' + l);
    if (exp) exp.value = exp.value.replace(/\s/g, '');
    var btn = form.querySelector('button[type=submit]'); if (btn) { btn.disabled = true; setTimeout(function () { btn.disabled = false; }, 8000); }
  });

  refresh(); payRefresh(); review();
})();
