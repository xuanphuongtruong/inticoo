(function () {
  // menu mobile
  var b = document.getElementById('burger'), n = document.getElementById('nav');
  if (b && n) b.addEventListener('click', function () { n.classList.toggle('open'); });

  // đóng menu thả xuống (Resources / ngôn ngữ) khi bấm ra ngoài hoặc chọn mục
  document.addEventListener('click', function (e) {
    document.querySelectorAll('details.dd[open], details.lang[open]').forEach(function (d) {
      if (!d.contains(e.target) || e.target.closest('.dd-menu a')) d.removeAttribute('open');
    });
  });

  // đếm ký tự ô message
  var ta = document.querySelector('textarea[maxlength]'), cnt = document.getElementById('msg-count');
  if (ta && cnt) { var f = function () { cnt.textContent = ta.value.length + '/' + ta.maxLength; }; ta.addEventListener('input', f); f(); }
})();
