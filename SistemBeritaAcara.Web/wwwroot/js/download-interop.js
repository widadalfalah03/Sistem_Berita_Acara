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
