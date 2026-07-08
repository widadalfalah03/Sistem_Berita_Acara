window.cropperInterop = {
    cropperInstance: null,

    handleFileSelect: function(inputElement, containerId) {
        if (!inputElement.files || inputElement.files.length === 0) return;
        const file = inputElement.files[0];
        
        
        
        const url = URL.createObjectURL(file);
        
        
        const btn = document.getElementById('hidden-show-modal');
        if (btn) btn.click();
        
        
        setTimeout(() => {
            this.initCropper(containerId, url);
        }, 150);
        
        
        inputElement.value = '';
    },

    initCropper: function (containerId, imageUrl) {
        if (typeof Cropper === 'undefined') {
            alert("Library Cropper belum termuat. Mohon lakukan Hard Refresh (Ctrl + F5).");
            return false;
        }

        const container = document.getElementById(containerId);
        if (!container) {
            console.error("Container element not found for cropper: " + containerId);
            return false;
        }

        
        container.innerHTML = '';
        
        
        const image = document.createElement('img');
        image.id = 'cropper-image';
        image.src = imageUrl;
        image.style.display = 'block';
        image.style.maxWidth = '100%';
        image.style.maxHeight = '360px';
        image.style.margin = '0 auto';
        
        container.appendChild(image);

        const startCropper = () => {
            try {
                
                this.destroyCropper();

                
                this.cropperInstance = new Cropper(image, {
                    aspectRatio: 1, 
                    viewMode: 1,
                    dragMode: 'move',
                    autoCropArea: 0.8,
                    restore: false,
                    guides: true,
                    center: true,
                    highlight: false,
                    cropBoxMovable: true,
                    cropBoxResizable: true,
                    toggleDragModeOnDblclick: false,
                });
            } catch (err) {
                console.error("Failed to init Cropper:", err);
            }
        };

        if (image.complete && image.naturalHeight !== 0) {
            startCropper();
        } else {
            image.onload = startCropper;
            
            setTimeout(startCropper, 300);
        }

        return true;
    },

    getCroppedBase64: function () {
        if (!this.cropperInstance) return null;

        
        const canvas = this.cropperInstance.getCroppedCanvas({
            width: 500,
            height: 500,
            imageSmoothingEnabled: true,
            imageSmoothingQuality: 'high',
        });

        if (!canvas) return null;

        
        
        const dataUrl = canvas.toDataURL('image/jpeg', 0.9);
        return dataUrl;
    },

    destroyCropper: function () {
        if (this.cropperInstance) {
            this.cropperInstance.destroy();
            this.cropperInstance = null;
        }
    }
};
