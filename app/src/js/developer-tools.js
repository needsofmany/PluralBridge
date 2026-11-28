/**
 * Page-local developer flags. This file does not read or write browser storage.
 * ensureWorkspaceTreeDisabledByDefault keeps its exported name and sets workspaceTreeEnabled
 * true only when the property is absent. setWorkspaceTreeEnabled(false) is the explicit fallback.
 * ensureWorkspaceLoadProfileDisabledByDefault sets workspaceLoadProfile false only when that
 * property is absent. workspaceLoadProfileEnabled is true only when the value is strictly true.
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

    /**
     * Leaves workspace-load profiling off unless workspaceLoadProfile is already present.
     * A value already on the object is kept, including a non-boolean value. This file does not read or write browser storage.
     * @param {object} tools The developer tools object.
     * @returns {void}
     */
    function ensureWorkspaceLoadProfileDisabledByDefault(tools) {
        if (!tools || Object.prototype.hasOwnProperty.call(tools, "workspaceLoadProfile")) {
            return;
        }

        tools.workspaceLoadProfile = false;
    }

    /**
     * Reports whether workspace-load profiling is enabled.
     * Missing tools, a missing setting, and every value other than strict true are disabled.
     * @param {object|null|undefined} [tools] Developer tools object. Omitted reads the page object. Null and a missing object are disabled.
     * @returns {boolean} True only when workspaceLoadProfile is strictly true.
     */
    function workspaceLoadProfileEnabled(tools) {
        const source = arguments.length === 0
            ? (window.PluralBridge && window.PluralBridge.developerTools)
            : tools;
        return Boolean(source && source.workspaceLoadProfile === true);
    }

    ensureDatabaseWritesEnabledByDefault(window.PluralBridge.developerTools);
    ensureWorkspaceTreeDisabledByDefault(window.PluralBridge.developerTools);
    ensureWorkspaceLoadProfileDisabledByDefault(window.PluralBridge.developerTools);
    window.PluralBridge.developerTools.ensureDatabaseWritesEnabledByDefault = ensureDatabaseWritesEnabledByDefault;
    window.PluralBridge.developerTools.ensureWorkspaceTreeDisabledByDefault = ensureWorkspaceTreeDisabledByDefault;
    window.PluralBridge.developerTools.ensureWorkspaceLoadProfileDisabledByDefault = ensureWorkspaceLoadProfileDisabledByDefault;
    window.PluralBridge.developerTools.setWorkspaceTreeEnabled = setWorkspaceTreeEnabled;
    window.PluralBridge.developerTools.workspaceLoadProfileEnabled = workspaceLoadProfileEnabled;
})();
