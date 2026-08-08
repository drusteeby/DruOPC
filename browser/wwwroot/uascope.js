window.uascope = {
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
            return JSON.parse(localStorage.getItem("uascope.recentServers") || "[]");
        } catch {
            return [];
        }
    },

    scrollToSelected: function () {
        document.querySelector(".tree-row.selected")?.scrollIntoView({ block: "center", behavior: "smooth" });
    },

    addRecentServer: function (url) {
        try {
            let list = JSON.parse(localStorage.getItem("uascope.recentServers") || "[]");
            list = [url, ...list.filter(u => u !== url)].slice(0, 8);
            localStorage.setItem("uascope.recentServers", JSON.stringify(list));
        } catch { }
    }
};
