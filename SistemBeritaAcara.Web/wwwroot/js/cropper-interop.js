window.cropperInterop = {
    cropperInstance: null,

    handleFileSelect: function(inputElement, containerId) {
        if (!inputElement.files || inputElement.files.length === 0) return;
        const file = inputElement.files[0];
        
        // Create an object URL directly in the browser. 
        // This avoids sending huge base64 strings over SignalR.
        const url = URL.createObjectURL(file);
        
        // Trigger Blazor to show the modal by clicking the hidden button
        const btn = document.getElementById('hidden-show-modal');
        if (btn) btn.click();
        
        // Wait for Blazor to render the modal, then initialize cropper
        setTimeout(() => {
            this.initCropper(containerId, url);
        }, 150);
        
        // Clear input so selecting the same file again triggers onchange
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

        // Clear container to prevent duplicate images if called multiple times
        container.innerHTML = '';
        
        // Create the image dynamically so Blazor doesn't track it
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
                // Destroy existing instance if any
                this.destroyCropper();

                // Initialize Cropper
                this.cropperInstance = new Cropper(image, {
                    aspectRatio: 1, // 1:1 for profile picture
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
            // Fallback in case onload doesn't fire for some data URIs
            setTimeout(startCropper, 300);
        }

        return true;
    },

    getCroppedBase64: function () {
        if (!this.cropperInstance) return null;

        // Get the cropped canvas with fixed standard size (500x500 is good for profile)
        const canvas = this.cropperInstance.getCroppedCanvas({
            width: 500,
            height: 500,
            imageSmoothingEnabled: true,
            imageSmoothingQuality: 'high',
        });

        if (!canvas) return null;

        // Get Base64 data URL
        // We use JPEG for smaller size, but PNG is fine too. Let's use JPEG 0.9 quality.
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
