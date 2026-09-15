window.PluralBridge = window.PluralBridge || {};

window.PluralBridge.shell = (function () {

    const selectors = {
        output: "#appOutput",
        navigationButton: ".application-navigation .application-nav-item",
        mainButton: "[data-shell-main]",
        phoneMenuToggle: "[data-shell-menu-toggle]",
        phoneMenuBackdrop: "[data-shell-menu-backdrop]"
    };

    const cssClasses = {
        phoneMenuOpen: "shell-menu-open"
    };

    const routes = {
        main: "main",
        session: "session"
    };

    const developerContracts = new Set([
        "sourceSystems",
        "privacyBuckets",
        "sourceRecords",
        "sourceIdMappings",
        "importMetadata"
    ]);

    const placeholderScreens = {
        fronting: {
            title: "Fronting",
            message: "Current Fronting functionality has not yet been implemented."
        },

        groups: {
            title: "Groups",
            message: "Groups functionality has not yet been implemented."
        },

        import: {
            title: "Import",
            message: "Import functionality has not yet been implemented."
        },

        "manage-fields": {
            title: "Manage Fields",
            message: "Field-definition management has not yet been implemented."
        },

        history: {
            title: "Fronting History",
            message: "Fronting History functionality has not yet been implemented."
        },

        "saved-views": {
            title: "Saved Views",
            message: "Saved Views functionality has not yet been implemented."
        },

        audit: {
            title: "Audit",
            message: "Audit functionality has not yet been implemented."
        }
    };

    let currentRoute = null;
    let phoneMenuOpen = false;

    function query(selector) {
        return document.querySelector(selector);
    }

    function queryAll(selector) {
        return document.querySelectorAll(selector);
    }

    function getOutput() {
        return query(selectors.output);
    }

    function isDeveloperContract(contractName) {
        return developerContracts.has(contractName);
    }

    function getDeveloperRoute(contractName) {
        return "developer:" + contractName;
    }

    function getRouteForButton(button) {

        if (!button) {
            return null;
        }

        if (button.hasAttribute("data-shell-main")) {
            return routes.main;
        }

        if (button.dataset.contract === "me") {
            return routes.session;
        }

        if (isDeveloperContract(button.dataset.contract)) {
            return getDeveloperRoute(button.dataset.contract);
        }

        if (button.dataset.shellPlaceholder) {
            return button.dataset.shellPlaceholder;
        }

        return null;
    }

    function isDeveloperRoute(route) {
        return typeof route === "string" && route.startsWith("developer:");
    }

    function isNativeAppRoute(route) {
        return route === routes.main || route === routes.session;
    }

    function setActiveRoute(route) {

        currentRoute = route;

        const navigationButtons = queryAll(selectors.navigationButton);

        navigationButtons.forEach(function (button) {

            const buttonRoute = getRouteForButton(button);

            button.setAttribute(
                "aria-pressed",
                String(Boolean(buttonRoute) && buttonRoute === route)
            );
        });
    }

    function createElement(tagName, className, textContent) {

        const element = document.createElement(tagName);

        if (className) {
            element.className = className;
        }

        if (textContent !== undefined && textContent !== null) {
            element.textContent = textContent;
        }

        return element;
    }

    function clearOutput() {

        const output = getOutput();

        if (!output) {
            return null;
        }

        output.replaceChildren();

        return output;
    }

    function renderPlaceholder(screenKey) {

        const screen = placeholderScreens[screenKey];
        const output = clearOutput();

        if (!screen || !output) {
            return;
        }

        const wrapper = createElement("section", "member-list");
        wrapper.dataset.shellScreen = screenKey;

        wrapper.appendChild(
            createElement("h2", "output-heading", screen.title)
        );

        wrapper.appendChild(
            createElement("p", "output-note", screen.message)
        );

        output.appendChild(wrapper);
    }

    function tryRenderExternalScreen(route) {

        const accountScreens =
            window.PluralBridge && window.PluralBridge.accountScreens
                ? window.PluralBridge.accountScreens
                : null;

        if (
            accountScreens &&
            typeof accountScreens.render === "function" &&
            accountScreens.render(route)
        ) {
            return true;
        }

        return false;
    }

    function handleResolvedRoute(route) {

        setActiveRoute(route);
        closePhoneMenu();

        if (isNativeAppRoute(route)) {
            return;
        }

        if (isDeveloperRoute(route)) {
            return;
        }

        if (tryRenderExternalScreen(route)) {
            return;
        }

        renderPlaceholder(route);
    }

    function handleNavigationClick(event) {

        const button =
            event.target.closest(selectors.navigationButton);

        if (!button) {
            return;
        }

        if (button.dataset.sessionAction === "logout") {
            return;
        }

        const route = getRouteForButton(button);

        if (!route) {
            return;
        }

        handleResolvedRoute(route);
    }

    function showMain() {

        const mainButton = query(selectors.mainButton);

        if (!mainButton) {
            return;
        }

        mainButton.click();
    }

    function setPhoneMenuOpen(isOpen) {

        phoneMenuOpen = Boolean(isOpen);

        document.body.classList.toggle(
            cssClasses.phoneMenuOpen,
            phoneMenuOpen
        );

        const toggle = query(selectors.phoneMenuToggle);

        if (!toggle) {
            return;
        }

        toggle.setAttribute(
            "aria-expanded",
            String(phoneMenuOpen)
        );

        toggle.setAttribute(
            "aria-label",
            phoneMenuOpen
                ? "Close application navigation"
                : "Open application navigation"
        );
    }

    function closePhoneMenu() {
        setPhoneMenuOpen(false);
    }

    function togglePhoneMenu() {
        setPhoneMenuOpen(!phoneMenuOpen);
    }

    function handlePhoneMenuKeydown(event) {

        if (event.key === "Escape" && phoneMenuOpen) {
            closePhoneMenu();
        }
    }

    function initializeNavigation() {

        document.addEventListener(
            "click",
            handleNavigationClick
        );
    }

    function initializePhoneNavigation() {

        const toggle = query(selectors.phoneMenuToggle);
        const backdrop = query(selectors.phoneMenuBackdrop);

        if (toggle) {
            toggle.addEventListener("click", togglePhoneMenu);
        }

        if (backdrop) {
            backdrop.addEventListener("click", closePhoneMenu);
        }

        document.addEventListener("keydown", handlePhoneMenuKeydown);
    }

    function initialize() {

        initializeNavigation();
        initializePhoneNavigation();
        showMain();
    }

    return {

        initialize: initialize,

        getCurrentRoute: function () {
            return currentRoute;
        },

        showMain: showMain
    };

})();

window.PluralBridge.shell.initialize();
