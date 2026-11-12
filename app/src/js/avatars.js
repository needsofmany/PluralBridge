window.PluralBridge = window.PluralBridge || {};

window.PluralBridge.avatars = (function () {
    function createObjectUrl(file) {
        if (!file || typeof URL.createObjectURL !== "function") {
            return "";
        }

        return URL.createObjectURL(file);
    }

    function revokeObjectUrl(url) {
        if (url && typeof URL.revokeObjectURL === "function") {
            URL.revokeObjectURL(url);
        }
    }

    function paintFrame(frame, url, emptyText) {
        if (!frame) {
            return;
        }

        frame.replaceChildren();
        if (!url) {
            const empty = document.createElement("span");
            empty.className = "member-avatar-empty";
            empty.textContent = emptyText || "No avatar selected";
            frame.appendChild(empty);
            return;
        }

        const image = document.createElement("img");
        image.className = "member-avatar-image";
        image.alt = "";
        image.src = url;
        frame.appendChild(image);
    }

    return {
        createObjectUrl: createObjectUrl,
        revokeObjectUrl: revokeObjectUrl,
        paintFrame: paintFrame
    };
})();
