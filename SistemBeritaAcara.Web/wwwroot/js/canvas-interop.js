window.setupCanvas = {
    isDrawing: false,
    ctx: null,
    init: function () {
        var oldC = document.getElementById('setup-ttd-canvas');
        if (!oldC) return;
        
        // Mencegah duplicate listeners
        var c = oldC.cloneNode(true);
        oldC.parentNode.replaceChild(c, oldC);

        var rect = c.getBoundingClientRect();
        c.width = rect.width * 2;
        c.height = rect.height * 2;
        this.ctx = c.getContext('2d');
        this.ctx.scale(2, 2);
        this.ctx.lineCap = 'round';
        this.ctx.lineJoin = 'round';
        this.ctx.lineWidth = 3;
        this.ctx.strokeStyle = '#000000';
        this.clear();

        c.addEventListener('mousedown', (e) => this.start(e));
        c.addEventListener('mousemove', (e) => this.move(e));
        c.addEventListener('mouseup', (e) => this.stop(e));
        c.addEventListener('mouseout', (e) => this.stop(e));
        c.addEventListener('touchstart', (e) => { e.preventDefault(); this.start(e); }, { passive: false });
        c.addEventListener('touchmove', (e) => { e.preventDefault(); this.move(e); }, { passive: false });
        c.addEventListener('touchend', (e) => { e.preventDefault(); this.stop(e); }, { passive: false });
    },
    start: function (e) {
        var c = document.getElementById('setup-ttd-canvas'); if (!c) return;
        var rect = c.getBoundingClientRect();
        var x = (e.clientX || (e.touches && e.touches[0].clientX)) - rect.left;
        var y = (e.clientY || (e.touches && e.touches[0].clientY)) - rect.top;
        this.isDrawing = true;
        this.ctx.beginPath();
        this.ctx.moveTo(x, y);
    },
    move: function (e) {
        if (!this.isDrawing) return;
        if (e && e.preventDefault) e.preventDefault();
        var c = document.getElementById('setup-ttd-canvas'); if (!c) return;
        var rect = c.getBoundingClientRect();
        var x = (e.clientX || (e.touches && e.touches[0].clientX)) - rect.left;
        var y = (e.clientY || (e.touches && e.touches[0].clientY)) - rect.top;
        this.ctx.lineTo(x, y);
        this.ctx.stroke();
    },
    stop: function () {
        this.isDrawing = false;
    },
    clear: function () {
        var c = document.getElementById('setup-ttd-canvas'); if (!c) return;
        this.ctx.clearRect(0, 0, c.width, c.height);
    },
    isEmpty: function () {
        var c = document.getElementById('setup-ttd-canvas'); if (!c) return true;
        var p = this.ctx.getImageData(0, 0, c.width, c.height).data;
        for (var i = 0; i < p.length; i += 4) { if (p[i + 3] > 0) return false; }
        return true;
    },
    getDataUrl: function () {
        var c = document.getElementById('setup-ttd-canvas');
        return c ? c.toDataURL("image/png") : "";
    }
};

window.ttdCanvas = {
    ctx: null,
    isDrawing: false,
    isEmpty: true,

    init: function () {
        const canvas = document.getElementById('ttd-canvas');
        if (!canvas) return;
        this.ctx = canvas.getContext('2d');
        this.ctx.strokeStyle = '#1a1a2e';
        this.ctx.lineWidth = 2.5;
        this.ctx.lineCap = 'round';
        this.ctx.lineJoin = 'round';
        this.isEmpty = true;

        const hidePlaceholder = () => {
            const ph = document.getElementById('ttd-placeholder');
            if (ph) ph.style.opacity = '0';
        };

        canvas.addEventListener('touchstart', (e) => {
            e.preventDefault();
            hidePlaceholder();
            const t = e.touches[0];
            const r = canvas.getBoundingClientRect();
            const scaleX = canvas.width / r.width;
            const scaleY = canvas.height / r.height;
            this.isDrawing = true;
            this.ctx.beginPath();
            this.ctx.moveTo((t.clientX - r.left) * scaleX, (t.clientY - r.top) * scaleY);
        }, { passive: false });
        canvas.addEventListener('touchmove', (e) => {
            e.preventDefault();
            if (!this.isDrawing) return;
            const t = e.touches[0];
            const r = canvas.getBoundingClientRect();
            const scaleX = canvas.width / r.width;
            const scaleY = canvas.height / r.height;
            this.ctx.lineTo((t.clientX - r.left) * scaleX, (t.clientY - r.top) * scaleY);
            this.ctx.stroke();
            this.isEmpty = false;
        }, { passive: false });
        canvas.addEventListener('touchend', (e) => { e.preventDefault(); this.isDrawing = false; }, { passive: false });

        // Add mouse events for desktop
        canvas.addEventListener('mousedown', (e) => {
            hidePlaceholder();
            const r = canvas.getBoundingClientRect();
            const scaleX = canvas.width / r.width;
            const scaleY = canvas.height / r.height;
            this.isDrawing = true;
            this.ctx.beginPath();
            this.ctx.moveTo((e.clientX - r.left) * scaleX, (e.clientY - r.top) * scaleY);
        });
        canvas.addEventListener('mousemove', (e) => {
            if (!this.isDrawing) return;
            const r = canvas.getBoundingClientRect();
            const scaleX = canvas.width / r.width;
            const scaleY = canvas.height / r.height;
            this.ctx.lineTo((e.clientX - r.left) * scaleX, (e.clientY - r.top) * scaleY);
            this.ctx.stroke();
            this.isEmpty = false;
        });
        canvas.addEventListener('mouseup', () => { this.isDrawing = false; });
        canvas.addEventListener('mouseleave', () => { this.isDrawing = false; });
    },

    startDraw: function (x, y) {
        if (!this.ctx) return;
        const canvas = document.getElementById('ttd-canvas');
        if (!canvas) return;
        const r = canvas.getBoundingClientRect();
        const scaleX = canvas.width / r.width;
        const scaleY = canvas.height / r.height;
        this.isDrawing = true;
        this.ctx.beginPath();
        this.ctx.moveTo((x - r.left) * scaleX, (y - r.top) * scaleY);
    },

    draw: function (x, y) {
        if (!this.isDrawing || !this.ctx) return;
        const canvas = document.getElementById('ttd-canvas');
        if (!canvas) return;
        const r = canvas.getBoundingClientRect();
        const scaleX = canvas.width / r.width;
        const scaleY = canvas.height / r.height;
        this.ctx.lineTo((x - r.left) * scaleX, (y - r.top) * scaleY);
        this.ctx.stroke();
        this.isEmpty = false;
    },

    stopDraw: function () { this.isDrawing = false; },

    clear: function () {
        if (!this.ctx) return;
        const canvas = document.getElementById('ttd-canvas');
        if (!canvas) return;
        this.ctx.clearRect(0, 0, canvas.width, canvas.height);
        this.isEmpty = true;
        const ph = document.getElementById('ttd-placeholder');
        if (ph) ph.style.opacity = '1';
    },

    isEmpty_check: function () { return this.isEmpty; },

    getDataUrl: function () {
        const canvas = document.getElementById('ttd-canvas');
        if (!canvas) return null;
        const ctx = canvas.getContext('2d');
        const data = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
        let minX = canvas.width, maxX = 0, minY = canvas.height, maxY = 0;
        for (let y = 0; y < canvas.height; y++) {
            for (let x = 0; x < canvas.width; x++) {
                const alpha = data[(y * canvas.width + x) * 4 + 3];
                if (alpha > 10) {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX <= minX || maxY <= minY) return canvas.toDataURL('image/png');
        const pad = 4;
        minX = Math.max(0, minX - pad);
        minY = Math.max(0, minY - pad);
        maxX = Math.min(canvas.width - 1, maxX + pad);
        maxY = Math.min(canvas.height - 1, maxY + pad);
        const w = maxX - minX + 1;
        const h = maxY - minY + 1;
        const tmp = document.createElement('canvas');
        tmp.width = w;
        tmp.height = h;
        tmp.getContext('2d').drawImage(canvas, minX, minY, w, h, 0, 0, w, h);
        return tmp.toDataURL('image/png');
    }
};
