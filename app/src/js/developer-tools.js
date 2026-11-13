(function () {
    window.PluralBridge = window.PluralBridge || {};
    window.PluralBridge.developerTools = window.PluralBridge.developerTools || {};

    function ensureDatabaseWritesEnabledByDefault(tools) {
        if (!tools || Object.prototype.hasOwnProperty.call(tools, "databaseWritesDisabled")) {
            return;
        }

        tools.databaseWritesDisabled = false;
    }

    ensureDatabaseWritesEnabledByDefault(window.PluralBridge.developerTools);
    window.PluralBridge.developerTools.ensureDatabaseWritesEnabledByDefault = ensureDatabaseWritesEnabledByDefault;
})();
