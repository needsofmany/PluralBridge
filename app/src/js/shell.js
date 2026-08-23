window.PluralBridge = window.PluralBridge || {};

window.PluralBridge.shell = (function () {

    let currentRoute = null;
    let phoneMenuOpen = false;

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

        profile: {
            title: "Profile",
            message: "Account Profile functionality has not yet been implemented."
        },

        security: {
            title: "Security",
            message: "Account Security functionality has not yet been implemented."
        },

        systems: {
            title: "System(s)",
            message: "System/account-management functionality has not yet been implemented."
        },

        audit: {
            title: "Audit",
            message: "Audit functionality has not yet been implemented."
        }
    };

    function getOutput() {
        return document.getElementById("appOutput");
    }

    function getRouteForButton(button) {

        if (!button) {
            return null;
        }

        if (button.hasAttribute("data-shell-main")) {
            return "main";
        }

        if (button.dataset.contract === "me") {
            return "session";
        }

        if (developerContracts.has(button.dataset.contract)) {
            return "developer:" + button.dataset.contract;
        }

        if (button.dataset.shellPlaceholder) {
            return button.dataset.shellPlaceholder;
        }

        return null;
    }

    function setActiveRoute(route) {

        currentRoute = route;

        const navigationButtons =
            document.querySelectorAll(
                ".application-navigation .application-nav-item"
            );

        navigationButtons.forEach(function (button) {

            const buttonRoute = getRouteForButton(button);

            if (!buttonRoute) {
                button.setAttribute("aria-pressed", "false");
                return;
            }

            button.setAttribute(
                "aria-pressed",
                String(buttonRoute === route)
            );

        });
    }

    function renderPlaceholder(screenKey) {

        const screen = placeholderScreens[screenKey];
        const output = getOutput();

        if (!screen || !output) {
            return;
        }

        output.replaceChildren();

        const wrapper = document.createElement("section");
        wrapper.className = "member-list";
        wrapper.dataset.shellScreen = screenKey;

        const heading = document.createElement("h2");
        heading.className = "output-heading";
        heading.textContent = screen.title;

        const note = document.createElement("p");
        note.className = "output-note";
        note.textContent = screen.message;

        wrapper.appendChild(heading);
        wrapper.appendChild(note);

        output.appendChild(wrapper);
    }

    function handleNavigationClick(event) {

        const button =
            event.target.closest(
                ".application-navigation .application-nav-item"
            );

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

        setActiveRoute(route);
        closePhoneMenu();

        if (route === "main" || route === "session") {
            return;
        }

        if (route.startsWith("developer:")) {
            return;
        }

        renderPlaceholder(route);
    }

    function showMain() {

        const mainButton =
            document.querySelector("[data-shell-main]");

        if (!mainButton) {
            return;
        }

        mainButton.click();
    }

    function initializeNavigation() {

        document.addEventListener(
            "click",
            handleNavigationClick
        );
    }

    function setPhoneMenuOpen(isOpen) {

        phoneMenuOpen = Boolean(isOpen);

        document.body.classList.toggle(
            "shell-menu-open",
            phoneMenuOpen
        );

        const toggle =
            document.querySelector("[data-shell-menu-toggle]");

        if (toggle) {

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
    }

    function closePhoneMenu() {
        setPhoneMenuOpen(false);
    }

    function initializePhoneNavigation() {

        const toggle =
            document.querySelector("[data-shell-menu-toggle]");

        const backdrop =
            document.querySelector("[data-shell-menu-backdrop]");


        if (toggle) {

            toggle.addEventListener("click", function () {
                setPhoneMenuOpen(!phoneMenuOpen);
            });
        }


        if (backdrop) {

            backdrop.addEventListener(
                "click",
                closePhoneMenu
            );
        }


        document.addEventListener("keydown", function (event) {

            if (event.key === "Escape" && phoneMenuOpen) {
                closePhoneMenu();
            }
        });
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