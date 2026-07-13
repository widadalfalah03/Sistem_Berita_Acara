window.downloadFileFromBytes = function (fileName, contentType, byteArray) {
    const blob = new Blob([new Uint8Array(byteArray)], { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

// Drag-and-drop interop untuk upload dokumen (DOCX/PDF) — assign ke file input lalu dispatch change
window.docUploadDrop = {
    _h: {},
    init: function (dropZoneId, inputId, dotNetRef) {
        const zone  = document.getElementById(dropZoneId);
        const input = document.getElementById(inputId);
        if (!zone || !input) return;
        this.destroy(dropZoneId);

        const onOver = function (e) {
            e.preventDefault(); e.stopPropagation();
            dotNetRef.invokeMethodAsync('OnDocDragOver');
        };
        const onLeave = function (e) {
            if (!zone.contains(e.relatedTarget))
                dotNetRef.invokeMethodAsync('OnDocDragLeave');
        };
        const onDrop = function (e) {
            e.preventDefault(); e.stopPropagation();
            dotNetRef.invokeMethodAsync('OnDocDragLeave');
            var files = e.dataTransfer && e.dataTransfer.files;
            if (!files || files.length === 0) return;
            try {
                var dt = new DataTransfer();
                dt.items.add(files[0]);
                input.files = dt.files;
                // Trigger Blazor InputFile.OnChange via native change event
                input.dispatchEvent(new Event('change', { bubbles: true }));
            } catch (err) {
                console.error('[docUploadDrop] drop error:', err);
            }
        };

        zone.addEventListener('dragover',  onOver);
        zone.addEventListener('dragleave', onLeave);
        zone.addEventListener('drop',      onDrop);
        this._h[dropZoneId] = { zone: zone, onOver: onOver, onLeave: onLeave, onDrop: onDrop };
    },
    destroy: function (dropZoneId) {
        var h = this._h[dropZoneId];
        if (!h) return;
        h.zone.removeEventListener('dragover',  h.onOver);
        h.zone.removeEventListener('dragleave', h.onLeave);
        h.zone.removeEventListener('drop',      h.onDrop);
        delete this._h[dropZoneId];
    }
};

// Drag-and-drop interop untuk area upload Excel
window.excelDropZone = {
    init: function (dropZoneId, dotnetRef) {
        const zone = document.getElementById(dropZoneId);
        if (!zone) return;

        zone.addEventListener('dragover', function (e) {
            e.preventDefault();
            e.stopPropagation();
            dotnetRef.invokeMethodAsync('OnDragOver');
        });

        zone.addEventListener('dragleave', function (e) {
            // Hanya trigger jika benar-benar keluar dari zona (bukan child element)
            if (!zone.contains(e.relatedTarget)) {
                dotnetRef.invokeMethodAsync('OnDragLeave');
            }
        });

        zone.addEventListener('drop', function (e) {
            e.preventDefault();
            e.stopPropagation();
            const files = e.dataTransfer.files;
            if (!files || files.length === 0) return;
            const file = files[0];
            if (!file.name.endsWith('.xlsx')) {
                dotnetRef.invokeMethodAsync('OnDropInvalidFile');
                return;
            }
            // Baca file sebagai base64 lalu kirim ke Blazor
            const reader = new FileReader();
            reader.onload = function (ev) {
                const base64 = ev.target.result.split(',')[1];
                dotnetRef.invokeMethodAsync('OnDropFile', file.name, file.size, base64);
            };
            reader.readAsDataURL(file);
        });
    }
};
