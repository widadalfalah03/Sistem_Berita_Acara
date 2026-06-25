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
            if (!placeholder) {
                console.error("ONLYOFFICE: #onlyoffice-placeholder not found in DOM");
                return;
            }
            if (typeof DocsAPI === 'undefined') {
                console.error("ONLYOFFICE: DocsAPI is undefined — API script may have failed to load from " + apiScriptUrl);
                return;
            }
            if (this.editor) {
                this.editor.destroyEditor();
                this.editor = null;
            }
            if (window.innerWidth <= 768) {
                config.type = 'mobile';
            }
            this.editor = new DocsAPI.DocEditor("onlyoffice-placeholder", config);
        }).catch(err => {
            console.error("ONLYOFFICE: Failed to load API script from " + apiScriptUrl, err);
        });
    },
    forceSave: function () {
        if (this.editor) {
            this.editor.forceSave();
        }
    },
    destroy: function () {
        if (this.editor) {
            this.editor.destroyEditor();
            this.editor = null;
        }
    }
};
