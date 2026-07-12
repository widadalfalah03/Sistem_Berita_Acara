
window.ttdUpload = {
    _threshold: 200,
    _dataUrl: null,
    _origSrc: null,
    _canvasId: null,
    _wrapId: null,

    onFileChange: function (inputId, canvasId, wrapId) {
        this._canvasId = canvasId;
        this._wrapId = wrapId;
        var input = document.getElementById(inputId);
        if (!input || !input.files || !input.files[0]) return;
        var file = input.files[0];
        if (file.size > 5 * 1024 * 1024) {
            alert('Ukuran file terlalu besar. Maksimal 5MB.');
            input.value = '';
            return;
        }
        if (!file.type.startsWith('image/')) {
            alert('File harus berupa gambar (JPG, PNG, dll).');
            input.value = '';
            return;
        }
        var reader = new FileReader();
        var self = this;
        reader.onload = function (e) {
            self._origSrc = e.target.result;
            self._reprocess(e.target.result, canvasId, wrapId);
        };
        reader.readAsDataURL(file);
    },

    setThreshold: function (val) {
        this._threshold = parseInt(val);
        if (this._origSrc && this._canvasId) {
            this._reprocess(this._origSrc, this._canvasId, this._wrapId);
        }
    },

    _reprocess: function (imgSrc, canvasId, wrapId) {
        var img = new Image();
        var self = this;
        img.onload = function () {
            var canvas = document.getElementById(canvasId);
            if (!canvas) return;

            var parent = canvas.parentElement;
            var maxW = parent ? Math.max(parent.offsetWidth - 18, 180) : 380;
            var maxH = 160;
            var scale = Math.min(maxW / img.naturalWidth, maxH / img.naturalHeight, 1);
            canvas.width = Math.round(img.naturalWidth * scale);
            canvas.height = Math.round(img.naturalHeight * scale);

            var ctx = canvas.getContext('2d');
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

            var imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            var data = imageData.data;
            var W = canvas.width, H = canvas.height;

            
            
            var bgR = 0, bgG = 0, bgB = 0, samples = 0;
            var r = Math.max(1, Math.min(8, Math.floor(Math.min(W, H) * 0.08)));
            var corners = [[0, 0], [W - 1, 0], [0, H - 1], [W - 1, H - 1]];
            for (var c = 0; c < corners.length; c++) {
                for (var dy = -r; dy <= r; dy++) {
                    for (var dx = -r; dx <= r; dx++) {
                        var px = Math.max(0, Math.min(W - 1, corners[c][0] + dx));
                        var py = Math.max(0, Math.min(H - 1, corners[c][1] + dy));
                        var idx = (py * W + px) * 4;
                        bgR += data[idx]; bgG += data[idx + 1]; bgB += data[idx + 2];
                        samples++;
                    }
                }
            }
            bgR = Math.round(bgR / samples);
            bgG = Math.round(bgG / samples);
            bgB = Math.round(bgB / samples);

            
            
            var removeThreshold = 45;  
            var edgeThreshold = 70;    
            for (var i = 0; i < data.length; i += 4) {
                var dr = data[i] - bgR, dg = data[i + 1] - bgG, db = data[i + 2] - bgB;
                var dist = Math.sqrt(dr * dr + dg * dg + db * db);
                if (dist <= removeThreshold) {
                    data[i + 3] = 0;
                } else if (dist < edgeThreshold) {
                    data[i + 3] = Math.round(data[i + 3] * (dist - removeThreshold) / (edgeThreshold - removeThreshold));
                }
            }
            ctx.putImageData(imageData, 0, 0);
            self._dataUrl = self._autocrop(canvas);

            if (wrapId) {
                var wrap = document.getElementById(wrapId);
                if (wrap) wrap.style.display = 'block';
            }
        };
        img.src = imgSrc;
    },

    _autocrop: function (canvas) {
        var ctx = canvas.getContext('2d');
        var d = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
        var minX = canvas.width, maxX = 0, minY = canvas.height, maxY = 0;
        for (var y = 0; y < canvas.height; y++) {
            for (var x = 0; x < canvas.width; x++) {
                if (d[(y * canvas.width + x) * 4 + 3] > 10) {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX <= minX || maxY <= minY) return canvas.toDataURL('image/png');
        var pad = 8;
        minX = Math.max(0, minX - pad); minY = Math.max(0, minY - pad);
        maxX = Math.min(canvas.width - 1, maxX + pad); maxY = Math.min(canvas.height - 1, maxY + pad);
        var tmp = document.createElement('canvas');
        tmp.width = maxX - minX + 1; tmp.height = maxY - minY + 1;
        tmp.getContext('2d').drawImage(canvas, minX, minY, tmp.width, tmp.height, 0, 0, tmp.width, tmp.height);
        return tmp.toDataURL('image/png');
    },

    getDataUrl: function () { return this._dataUrl; },
    hasImage: function () { return !!this._dataUrl; },
    reset: function () {
        this._dataUrl = null; this._origSrc = null; this._threshold = 200;
        this._canvasId = null; this._wrapId = null;
        ['ttd-ul-input', 'setup-ul-input', 'pj-ul-input'].forEach(function (id) {
            var el = document.getElementById(id); if (el) el.value = '';
        });
        ['ttd-ul-wrap', 'setup-ul-wrap', 'pj-ul-wrap'].forEach(function (id) {
            var el = document.getElementById(id); if (el) el.style.display = 'none';
        });
    }
};

window.setupCanvas = {
    isDrawing: false,
    ctx: null,
    _prevX: 0,
    _prevY: 0,
    init: function () {
        var oldC = document.getElementById('setup-ttd-canvas');
        if (!oldC) return;

        var c = oldC.cloneNode(true);
        oldC.parentNode.replaceChild(c, oldC);

        // Unified DPR-based sizing — same approach as all other canvases
        var dpr = window.devicePixelRatio || 1;
        var rect = c.getBoundingClientRect();
        c.width = Math.round(rect.width * dpr);
        c.height = Math.round(rect.height * dpr);
        this.ctx = c.getContext('2d');
        this.ctx.scale(dpr, dpr);
        this.ctx.lineCap = 'round';
        this.ctx.lineJoin = 'round';
        this.ctx.lineWidth = 2;  // CSS pixel units — identical visual on all canvases
        this.ctx.strokeStyle = '#000000';
        this.clear();

        c.addEventListener('mousedown', (e) => this.start(e));
        c.addEventListener('mousemove', (e) => this.move(e));
        c.addEventListener('mouseup',   (e) => this.stop(e));
        c.addEventListener('mouseout',  (e) => this.stop(e));
        c.addEventListener('touchstart', (e) => { e.preventDefault(); this.start(e); }, { passive: false });
        c.addEventListener('touchmove',  (e) => { e.preventDefault(); this.move(e);  }, { passive: false });
        c.addEventListener('touchend',   (e) => { e.preventDefault(); this.stop(e);  }, { passive: false });
    },
    start: function (e) {
        var c = document.getElementById('setup-ttd-canvas'); if (!c) return;
        var r = c.getBoundingClientRect();
        var src = e.touches ? e.touches[0] : e;
        var x = src.clientX - r.left;
        var y = src.clientY - r.top;
        this.isDrawing = true;
        this._prevX = x;
        this._prevY = y;
        this.ctx.beginPath();
        this.ctx.moveTo(x, y);
    },
    move: function (e) {
        if (!this.isDrawing) return;
        if (e && e.preventDefault) e.preventDefault();
        var c = document.getElementById('setup-ttd-canvas'); if (!c) return;
        var r = c.getBoundingClientRect();
        var src = e.touches ? e.touches[0] : e;
        var x = src.clientX - r.left;
        var y = src.clientY - r.top;
        // Quadratic Bezier midpoint smoothing
        var midX = (this._prevX + x) / 2;
        var midY = (this._prevY + y) / 2;
        this.ctx.quadraticCurveTo(this._prevX, this._prevY, midX, midY);
        this.ctx.stroke();
        this.ctx.beginPath();
        this.ctx.moveTo(midX, midY);
        this._prevX = x;
        this._prevY = y;
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

        // Unified DPR-based sizing — identical to setupCanvas and pjCanvas approach
        const dpr = window.devicePixelRatio || 1;
        const rect = canvas.getBoundingClientRect();
        canvas.width  = Math.round((rect.width  || 600) * dpr);
        canvas.height = Math.round((rect.height || 200) * dpr);

        this.ctx = canvas.getContext('2d');
        this.ctx.scale(dpr, dpr);
        this.ctx.strokeStyle = '#1a1a2e';
        this.ctx.lineWidth = 2;  // CSS pixel units — same visual thickness across all canvases
        this.ctx.lineCap = 'round';
        this.ctx.lineJoin = 'round';
        this.isEmpty = true;
        this._prevX = 0;
        this._prevY = 0;

        const hidePlaceholder = () => {
            const ph = document.getElementById('ttd-placeholder');
            if (ph) ph.style.opacity = '0';
        };

        // Coordinates in CSS pixel space (ctx.scale handles DPR conversion)
        const getXY = (e) => {
            const r = canvas.getBoundingClientRect();
            const src = e.touches ? e.touches[0] : e;
            return { x: src.clientX - r.left, y: src.clientY - r.top };
        };

        const startDraw = (e, hide) => {
            if (hide) hidePlaceholder();
            const p = getXY(e);
            this.isDrawing = true;
            this._prevX = p.x;
            this._prevY = p.y;
            this.ctx.beginPath();
            this.ctx.moveTo(p.x, p.y);
        };

        const moveDraw = (e) => {
            if (!this.isDrawing) return;
            const p = getXY(e);
            // Quadratic Bezier midpoint smoothing
            const midX = (this._prevX + p.x) / 2;
            const midY = (this._prevY + p.y) / 2;
            this.ctx.quadraticCurveTo(this._prevX, this._prevY, midX, midY);
            this.ctx.stroke();
            this.ctx.beginPath();
            this.ctx.moveTo(midX, midY);
            this._prevX = p.x;
            this._prevY = p.y;
            this.isEmpty = false;
        };

        canvas.addEventListener('touchstart', (e) => { e.preventDefault(); startDraw(e, true); }, { passive: false });
        canvas.addEventListener('touchmove',  (e) => { e.preventDefault(); moveDraw(e); },       { passive: false });
        canvas.addEventListener('touchend',   (e) => { e.preventDefault(); this.isDrawing = false; }, { passive: false });

        canvas.addEventListener('mousedown',  (e) => startDraw(e, true));
        canvas.addEventListener('mousemove',  (e) => moveDraw(e));
        canvas.addEventListener('mouseup',    () => { this.isDrawing = false; });
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
