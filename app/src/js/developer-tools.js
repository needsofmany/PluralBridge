/**
 * Page-local developer flags. This file does not read or write browser storage.
 * ensureWorkspaceTreeDisabledByDefault keeps its exported name and sets workspaceTreeEnabled
 * true only when the property is absent. setWorkspaceTreeEnabled(false) is the explicit fallback.
 * Idle timeout and the context budget are process configuration, not flags in this file.
 */
(function () {
    window.PluralBridge = window.PluralBridge || {};
    window.PluralBridge.developerTools = window.PluralBridge.developerTools || {};

    function ensureDatabaseWritesEnabledByDefault(tools) {
        if (!tools || Object.prototype.hasOwnProperty.call(tools, "databaseWritesDisabled")) {
            return;
        }

        tools.databaseWritesDisabled = false;
    }

    /**
     * Turns the new tree path on unless workspaceTreeEnabled is already present.
     * A value already on the object is kept for this page. This file does not read or write browser storage.
     * setWorkspaceTreeEnabled(false) is the explicit fallback to the plural-map path.
     * The idle-expiration setting is process configuration, not a browser flag.
     * @param {object} tools The developer tools object.
     * @returns {void}
     */
    function ensureWorkspaceTreeDisabledByDefault(tools) {
        if (!tools || Object.prototype.hasOwnProperty.call(tools, "workspaceTreeEnabled")) {
            return;
        }

        tools.workspaceTreeEnabled = true;
    }

    /**
     * Selects the workspace tree or the existing plural-map path and asks the navigator to reset.
     * @param {boolean} enabled True uses GET /api/me/workspace-tree. False keeps the existing path.
     * @returns {void}
     */
    function setWorkspaceTreeEnabled(enabled) {
        const tools = window.PluralBridge.developerTools;
        tools.workspaceTreeEnabled = enabled === true;
        const navigatorView = window.PluralBridge.navigator;
        if (navigatorView && typeof navigatorView.applyWorkspacePath === "function") {
            navigatorView.applyWorkspacePath();
        }
    }

    ensureDatabaseWritesEnabledByDefault(window.PluralBridge.developerTools);
    ensureWorkspaceTreeDisabledByDefault(window.PluralBridge.developerTools);
    window.PluralBridge.developerTools.ensureDatabaseWritesEnabledByDefault = ensureDatabaseWritesEnabledByDefault;
    window.PluralBridge.developerTools.ensureWorkspaceTreeDisabledByDefault = ensureWorkspaceTreeDisabledByDefault;
    window.PluralBridge.developerTools.setWorkspaceTreeEnabled = setWorkspaceTreeEnabled;
})();
