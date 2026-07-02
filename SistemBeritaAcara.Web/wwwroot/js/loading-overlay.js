// Overlay loading global — dikontrol via JS, tidak bergantung pada Blazor rendering cycle.
// Muncul SEGERA saat dipanggil (sebelum SignalR DOM patch dikirim ke browser).
// Bertahan melewati in-app Blazor navigation karena tidak berada dalam component tree Blazor.
window.appLoading = {
    _el: null,
    show: function (message, subtext) {
        if (!this._el) {
            this._el = document.createElement('div');
            this._el.style.cssText =
                'position:fixed;inset:0;z-index:99999;background:rgba(255,255,255,0.93);' +
                'backdrop-filter:blur(6px);display:none;flex-direction:column;' +
                'align-items:center;justify-content:center;gap:16px;';
            document.body.appendChild(this._el);
        }
        this._el.innerHTML =
            '<div class="spinner" style="width:56px;height:56px;border-width:5px;' +
            'border-color:var(--primary,#2563EB);border-top-color:transparent;"></div>';
        this._el.style.display = 'flex';
    },
    hide: function () {
        if (this._el) this._el.style.display = 'none';
    }
};
