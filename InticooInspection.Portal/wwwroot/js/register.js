(function () {
  var form = document.getElementById('regform');
  if (!form) return;
  var cycleVal = document.getElementById('cycleVal');
  var radios = form.querySelectorAll('input[name="Input.PlanCode"]');
  var mods = form.querySelectorAll('#mods .mod-opt');

  function cycle() { return cycleVal.value === 'Annual' ? 'Annual' : 'Monthly'; }

  function refresh() {
    var sel = form.querySelector('input[name="Input.PlanCode"]:checked');
    if (!sel) return;
    var annual = cycle() === 'Annual';
    var price = Number(annual ? sel.dataset.a : sel.dataset.m);
    document.getElementById('s-plan').textContent = sel.dataset.name;
    document.getElementById('s-cycle').textContent = annual ? 'Annual' : 'Monthly';
    document.getElementById('s-users').textContent = sel.dataset.users;
    document.getElementById('s-gb').textContent = sel.dataset.gb + ' GB';
    document.getElementById('s-total').textContent = '$' + price.toLocaleString('en-US', { minimumFractionDigits: 2 }) + (annual ? ' / year' : ' / month');
    var allowed = (sel.dataset.mods || '').split(',');
    mods.forEach(function (m) {
      var cb = m.querySelector('input'), ok = allowed.indexOf(m.dataset.code) >= 0;
      m.classList.toggle('locked', !ok);
      cb.disabled = !ok;
      if (!ok) cb.checked = false; else if (!m.dataset.touched) cb.checked = true;
      cb.addEventListener('change', function () { m.dataset.touched = '1'; });
    });
    document.querySelectorAll('.plan-pick .p').forEach(function (p) {
      p.querySelector('.amt').textContent = '$' + Number(annual ? p.dataset.a : p.dataset.m).toLocaleString('en-US');
      var per = p.querySelector('.per'); if (per) per.textContent = annual ? per.dataset.y : per.dataset.m;
    });
  }

  radios.forEach(function (r) { r.addEventListener('change', refresh); });
  document.querySelectorAll('#cycle button').forEach(function (b) {
    b.addEventListener('click', function () {
      document.querySelectorAll('#cycle button').forEach(function (x) { x.classList.remove('on'); });
      b.classList.add('on'); cycleVal.value = b.dataset.cycle; refresh();
    });
  });

  // ô thanh toán thẻ chỉ hiện khi chọn Card
  var pay = form.querySelectorAll('input[name="Input.PaymentMethod"]'), cardbox = document.getElementById('cardbox');
  function payRefresh() {
    var v = form.querySelector('input[name="Input.PaymentMethod"]:checked');
    if (cardbox) cardbox.style.display = v && v.value === 'Card' ? '' : 'none';
    var ppbox = document.getElementById('ppbox');
    if (ppbox) ppbox.style.display = v && v.value === 'PayPal' ? '' : 'none';
  }
  pay.forEach(function (p) { p.addEventListener('change', payRefresh); });

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
        if (e.isIntersecting) {
          var idx = Number(e.target.id.replace('sec', '')) - 1;
          steps.forEach(function (s, i) { s.classList.toggle('on', i === idx); });
        }
      });
    }, { rootMargin: '-30% 0px -60% 0px' });
    for (var i = 1; i <= 6; i++) { var s = document.getElementById('sec' + i); if (s) io.observe(s); }
  }

  // Server mong "Expiry" dạng "MM/YY" — bỏ khoảng trắng trước khi gửi, chống gửi 2 lần
  form.addEventListener('submit', function () {
    if (exp) exp.value = exp.value.replace(/\s/g, '');
    var btn = form.querySelector('button[type=submit]'); if (btn) { btn.disabled = true; setTimeout(function () { btn.disabled = false; }, 8000); }
  });

  refresh(); payRefresh();
})();
