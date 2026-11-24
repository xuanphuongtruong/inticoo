(function () {
  // CSP chặn handler inline nên dùng data-attribute
  document.addEventListener('change', function (e) {
    var t = e.target;
    if (t && t.matches && t.matches('[data-autosubmit]') && t.form) t.form.submit();
  });
  document.addEventListener('submit', function (e) {
    var f = e.target, msg = f && f.getAttribute && f.getAttribute('data-confirm');
    if (msg && !window.confirm(msg)) e.preventDefault();
  }, true);
  var pb = document.getElementById('printbtn'); if (pb) pb.addEventListener('click', function () { window.print(); });
  // chọn tất cả
  document.querySelectorAll('[data-checkall]').forEach(function (a) {
    a.addEventListener('change', function () {
      document.querySelectorAll(a.getAttribute('data-checkall')).forEach(function (c) { c.checked = a.checked; });
    });
  });
})();
