// wwwroot/js/print-report.js
// Print function for InspectionReport — loaded via <script src> to avoid CSP inline violation

window.printReportOnly = function () {
    // Hide all elements outside .rpt-page temporarily
    const hidden = [];

    document.querySelectorAll('body > *').forEach(el => {
        if (!el.contains(document.querySelector('.rpt-page')) &&
            !el.classList.contains('rpt-page')) {
            hidden.push({ el, display: el.style.display });
            el.style.display = 'none';
        }
    });

    // Also hide nav/header at any level
    document.querySelectorAll(
        'nav, header, .navbar, .sidebar, .top-bar, .nav-bar, ' +
        '[class*="nav-menu"], [class*="app-bar"], [class*="sidebar"]'
    ).forEach(el => {
        if (!el.closest('.rpt-page')) {
            hidden.push({ el, display: el.style.display });
            el.style.display = 'none';
        }
    });

    window.print();

    // Restore after print dialog closes
    setTimeout(() => {
        hidden.forEach(({ el, display }) => {
            el.style.display = display;
        });
    }, 1000);
};
