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
    },

    // Watch list per server, saved in a cookie like the panel layout.
    // Node ids are URI-encoded so '|' can separate them.
    getWatchList: function (serverUrl) {
        const name = druopc._watchCookieName(serverUrl) + "=";
        const entry = document.cookie.split("; ").find(c => c.startsWith(name));
        if (!entry) { return []; }
        return entry.substring(name.length).split("|")
            .filter(v => v.length > 0).map(decodeURIComponent);
    },

    setWatchList: function (serverUrl, nodeIds) {
        const value = nodeIds.map(encodeURIComponent).join("|");
        // Browsers cap a cookie around 4 KB; drop the overflow rather than lose it all.
        // Dropped entries stay in the live watch list but won't be restored after a reload.
        let kept = value;
        while (kept.length > 3500) {
            kept = kept.substring(0, kept.lastIndexOf("|"));
        }
        const maxAge = kept.length > 0 ? 60 * 60 * 24 * 365 : 0;
        document.cookie = `${druopc._watchCookieName(serverUrl)}=${kept}; path=/; max-age=${maxAge}; samesite=lax`;
    },

    _watchCookieName: function (serverUrl) {
        let h = 5381;
        for (const ch of serverUrl.trim().toLowerCase()) {
            h = ((h * 33) ^ ch.charCodeAt(0)) >>> 0;
        }
        return "druopc.watch." + h.toString(16);
    }
};
