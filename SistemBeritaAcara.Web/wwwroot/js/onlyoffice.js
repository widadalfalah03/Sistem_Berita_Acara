window.onlyoffice = {
    editor: null,
    loadScript: function (url) {
        return new Promise((resolve, reject) => {
            if (document.querySelector(`script[src="${url}"]`)) {
                resolve();
                return;
            }
            var script = document.createElement("script");
            script.src = url;
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    },
    init: function (apiScriptUrl, config) {
        this.loadScript(apiScriptUrl).then(() => {
            var placeholder = document.getElementById("onlyoffice-placeholder");
            if (placeholder && typeof DocsAPI !== 'undefined') {
                // Hancurkan editor lama jika ada
                if (this.editor) {
                    this.editor.destroyEditor();
                }
                this.editor = new DocsAPI.DocEditor("onlyoffice-placeholder", config);
            }
        }).catch(err => console.error("Failed to load ONLYOFFICE API", err));
    },
    destroy: function () {
        if (this.editor) {
            this.editor.destroyEditor();
            this.editor = null;
        }
    }
};
