window.pdfViewer = {
    loadPdfJs: function () {
        return new Promise((resolve, reject) => {
            if (window.pdfjsLib) {
                resolve();
                return;
            }
            var script = document.createElement('script');
            script.src = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.min.js';
            script.onload = () => {
                window.pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
                resolve();
            };
            script.onerror = reject;
            document.head.appendChild(script);
        });
    },
    renderPdf: async function (containerId, pdfUrl) {
        try {
            await this.loadPdfJs();
            
            const container = document.getElementById(containerId);
            if (!container) return;
            
            // Tampilkan loading di dalam container
            container.innerHTML = '<div style="padding:40px;text-align:center;color:#6b7280;"><div class="spinner" style="margin-bottom:16px;display:inline-block;"></div><div>Memuat dokumen PDF...</div></div>';
            
            const pdfjsLib = window.pdfjsLib;
            const loadingTask = pdfjsLib.getDocument(pdfUrl);
            const pdf = await loadingTask.promise;
            
            container.innerHTML = ''; // Bersihkan container
            container.style.overflowY = 'auto';
            container.style.padding = '16px';
            container.style.background = '#E8EAF0';
            
            for (let pageNum = 1; pageNum <= pdf.numPages; pageNum++) {
                const page = await pdf.getPage(pageNum);
                
                // Set scale to fit container width
                const containerWidth = container.clientWidth - 32; // Kurangi padding
                const unscaledViewport = page.getViewport({ scale: 1.0 });
                const baseScale = containerWidth / unscaledViewport.width;
                const scale = Math.min(1.5, baseScale);
                const viewport = page.getViewport({ scale: scale });

                // Mendukung High-DPI / Retina display agar PDF tidak pecah
                const dpr = window.devicePixelRatio || 1;
                const canvas = document.createElement('canvas');
                const context = canvas.getContext('2d');
                
                canvas.style.width = `${viewport.width}px`;
                canvas.style.height = `${viewport.height}px`;
                canvas.width = Math.floor(viewport.width * dpr);
                canvas.height = Math.floor(viewport.height * dpr);
                
                context.scale(dpr, dpr);

                canvas.style.display = 'block';
                canvas.style.margin = '0 auto 16px auto';
                canvas.style.borderRadius = '4px';
                canvas.style.boxShadow = '0 2px 8px rgba(0,0,0,0.15)';
                canvas.style.backgroundColor = '#ffffff';

                container.appendChild(canvas);

                const renderContext = {
                    canvasContext: context,
                    viewport: viewport
                };
                
                // Tunggu render page 1 selesai sebelum lanjut ke page 2 dst agar tidak nge-lag
                await page.render(renderContext).promise;
            }
        } catch (e) {
            console.error('Error rendering PDF:', e);
            const container = document.getElementById(containerId);
            if (container) {
                container.innerHTML = '<div style="padding:40px;text-align:center;color:#ef4444;"><i class="bi bi-exclamation-circle" style="font-size:32px;margin-bottom:8px;display:block;"></i><div>Gagal memuat PDF.</div><a href="'+pdfUrl+'" target="_blank" style="margin-top:16px;display:inline-block;padding:8px 16px;background:#ef4444;color:white;border-radius:4px;text-decoration:none;">Buka PDF Manual</a></div>';
            }
        }
    }
};
