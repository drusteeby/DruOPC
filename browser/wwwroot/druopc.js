const PANEL_COLLAPSED_PREFIX = "druopc.panelCollapsed.";

window.druopc = {
    copyText: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            // Clipboard API unavailable (e.g. non-secure context): fall back.
            const el = document.createElement("textarea");
            el.value = text;
            el.style.position = "fixed";
            el.style.opacity = "0";
            document.body.appendChild(el);
            el.select();
            let ok = false;
            try { ok = document.execCommand("copy"); } catch { }
            document.body.removeChild(el);
            return ok;
        }
    },

    downloadFile: function (fileName, mimeType, content) {
        const blob = new Blob([content], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },

    getRecentServers: function () {
        try {
            return JSON.parse(localStorage.getItem("druopc.recentServers") || "[]");
        } catch {
            return [];
        }
    },

    timezoneOffsetMinutes: function () {
        // Minutes to ADD to browser-local wall time to get UTC.
        return new Date().getTimezoneOffset();
    },

    scrollToSelected: function () {
        document.querySelector(".tree-row.selected")?.scrollIntoView({ block: "center", behavior: "smooth" });
    },

    addRecentServer: function (url) {
        try {
            let list = JSON.parse(localStorage.getItem("druopc.recentServers") || "[]");
            list = [url, ...list.filter(u => u !== url)].slice(0, 8);
            localStorage.setItem("druopc.recentServers", JSON.stringify(list));
        } catch { }
    },

    // Keys of every panel saved as collapsed, e.g. ["right.attributes"].
    getCollapsedPanels: function () {
        try {
            return Object.keys(localStorage)
                .filter(k => k.startsWith(PANEL_COLLAPSED_PREFIX) && localStorage.getItem(k) === "1")
                .map(k => k.slice(PANEL_COLLAPSED_PREFIX.length));
        } catch {
            return [];
        }
    },

    setPanelCollapsed: function (panelKey, collapsed) {
        try {
            localStorage.setItem(PANEL_COLLAPSED_PREFIX + panelKey, collapsed ? "1" : "0");
        } catch { }
    },

    resetPanelLayout: function () {
        try {
            Object.keys(localStorage)
                .filter(k => k.startsWith(PANEL_COLLAPSED_PREFIX))
                .forEach(k => localStorage.removeItem(k));
        } catch { }
    }
};
