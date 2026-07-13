


window.appLoading = {
    _el: null,
    show: function (message, subtext) {
        if (!this._el) {
            this._el = document.createElement('div');
            document.body.appendChild(this._el);
        }
        const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
        this._el.style.cssText =
            'position:fixed;inset:0;z-index:99999;' +
            'background:' + (isDark ? 'rgba(3,11,37,0.96)' : 'rgba(255,255,255,0.93)') + ';' +
            'backdrop-filter:blur(6px);display:none;flex-direction:column;' +
            'align-items:center;justify-content:center;gap:16px;' +
            'transition:background 0.2s;';
        this._el.innerHTML =
            '<div class="spinner" style="width:56px;height:56px;border-width:5px;' +
            'border-color:var(--border,#1a2a47);border-top-color:var(--primary,#3B82F6);"></div>';
        this._el.style.display = 'flex';
    },
    hide: function () {
        if (this._el) this._el.style.display = 'none';
    }
};
