(function () {
  // menu mobile
  var b = document.getElementById('burger'), n = document.getElementById('nav');
  if (b && n) b.addEventListener('click', function () { n.classList.toggle('open'); });

  // chuyển giá tháng/năm
  var sw = document.querySelectorAll('.cycle-switch');
  sw.forEach(function (box) {
    box.querySelectorAll('button').forEach(function (btn) {
      btn.addEventListener('click', function () {
        box.querySelectorAll('button').forEach(function (x) { x.classList.remove('on'); });
        btn.classList.add('on');
        var annual = btn.dataset.cycle === 'Annual';
        document.querySelectorAll('.plans .price').forEach(function (p) {
          var v = Number(annual ? p.dataset.a : p.dataset.m);
          p.querySelector('.amt').textContent = 'US$ ' + v.toLocaleString('en-US');
          var per = p.querySelector('.per');
          if (per) per.textContent = annual ? per.dataset.y : per.dataset.m;
        });
      });
    });
  });

  // đếm ký tự ô message
  var ta = document.querySelector('textarea[maxlength]'), cnt = document.getElementById('msg-count');
  if (ta && cnt) { var f = function () { cnt.textContent = ta.value.length + '/' + ta.maxLength; }; ta.addEventListener('input', f); f(); }
})();
