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

    // Saved in a cookie (not localStorage) so the server can render the
    // collapsed panels while prerendering; see PanelLayoutState.
    setCollapsedPanels: function (panelKeys) {
        const maxAge = panelKeys.length > 0 ? 60 * 60 * 24 * 365 : 0;
        document.cookie = `druopc.panelsCollapsed=${panelKeys.join("|")}; path=/; max-age=${maxAge}; samesite=lax`;
    }
};
