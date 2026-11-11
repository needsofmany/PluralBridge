window.PluralBridge = window.PluralBridge || {};
window.PluralBridge.bootstrap = window.PluralBridge.bootstrap || {};

window.PluralBridge.theme = (function () {
    const storageKey = "pluralbridge.theme";
    const listeners = [];

    function normalize(value) {
        return value === "light" || value === "dark" ? value : "dark";
    }

    function readStored() {
        try {
            return normalize(window.localStorage.getItem(storageKey));
        } catch (error) {
            return "dark";
        }
    }

    function apply(theme) {
        document.documentElement.setAttribute("data-theme", theme);
        document.documentElement.style.colorScheme = theme;
    }

    function get() {
        return normalize(document.documentElement.getAttribute("data-theme") || readStored());
    }

    function set(theme) {
        const next = normalize(theme);
        apply(next);
        try {
            window.localStorage.setItem(storageKey, next);
        } catch (error) {
            /* The theme still applies for this page when storage is unavailable. */
        }
        listeners.forEach(function (listener) {
            listener(next);
        });
        return next;
    }

    function toggle() {
        return set(get() === "light" ? "dark" : "light");
    }

    function subscribe(listener) {
        if (typeof listener !== "function") {
            return function () {};
        }
        listeners.push(listener);
        return function () {
            const index = listeners.indexOf(listener);
            if (index >= 0) {
                listeners.splice(index, 1);
            }
        };
    }

    apply(readStored());

    return {
        storageKey: storageKey,
        get: get,
        set: set,
        toggle: toggle,
        subscribe: subscribe
    };
})();
