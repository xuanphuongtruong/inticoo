// Trình soạn email đơn giản (contenteditable) cho Report Settings của Customer / Vendor.
window.partyEditor = {
    set: function (el, html) { if (el && el.innerHTML !== html) el.innerHTML = html || ""; },
    get: function (el) { return el ? el.innerHTML : ""; },
    exec: function (el, cmd) {
        if (!el) return "";
        el.focus();
        document.execCommand(cmd, false, null);
        return el.innerHTML;
    },
    link: function (el) {
        if (!el) return "";
        var url = window.prompt("URL", "https://");
        if (url) { el.focus(); document.execCommand("createLink", false, url); }
        return el.innerHTML;
    },
    insert: function (el, text) {
        if (!el) return "";
        el.focus();
        document.execCommand("insertText", false, text);
        return el.innerHTML;
    }
};
