/**
 * Application shell navigation.
 * On each page load, the MAIN section is expanded and every other sidebar section is collapsed.
 * That initial state is applied in the page. Stored expansion is not read and cannot replace it.
 * A heading toggles its section for the rest of the page. The chevron and the content height follow that
 * state directly, so a second toggle during an animation reverses from the current position.
 * Each heading carries a decorative icon. An expanded section draws a guide beside its items.
 * The shell owns sidebar layout state for the current page: section expansion and whether the
 * sidebar itself is open. Section defaults are applied on load and are not read from storage.
 * The account row uses the existing person icon beside the signed-in name. That row and the
 * PluralBridge girder and bridge artwork share one header band. The sidebar begins below it.
 * One sidebar visibility control is shown at a time: collapse beside search while open, and a
 * right-pointing double chevron at the left edge while closed.
 * The Members route is unchanged.
 */
window.PluralBridge = window.PluralBridge || {};

window.PluralBridge.shell = (function () {

    const selectors = {
        output: "#appOutput",
        navigationButton: ".application-navigation .application-nav-item",
        navigation: ".application-navigation",
        navigationSection: ".application-navigation .application-nav-section",
        navigationHeading: ".application-nav-heading",
        navigationPanel: ".application-nav-panel",
        mainButton: "[data-shell-main]",
        phoneMenuToggle: "[data-shell-menu-toggle]",
        phoneMenuBackdrop: "[data-shell-menu-backdrop]"
    };

    const cssClasses = {
        phoneMenuOpen: "shell-menu-open",
        sectionCollapsed: "is-collapsed",
        sectionExpanded: "is-expanded",
        accordionReady: "is-accordion-ready",
        accordionSettling: "is-accordion-settling"
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

    let currentRoute = null;
    let phoneMenuOpen = false;

    function query(selector) {
        return document.querySelector(selector);
    }

    function queryAll(selector) {
        return document.querySelectorAll(selector);
    }

    function getEventTargetElement(event) {
        if (!event) {
            return null;
        }

        if (event.target instanceof Element) {
            return event.target;
        }

        if (event.target && event.target.parentElement instanceof Element) {
            return event.target.parentElement;
        }

        return null;
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

    function renderSelectionMessage(labelText) {
        const output = clearOutput();

        if (!output) {
            return;
        }

        const wrapper = createElement("section", "member-list");

        const normalizedLabel =
            String(labelText || "").trim() || "Item";

        wrapper.appendChild(
            createElement("p", "output-note", normalizedLabel + " selected.")
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

    function handleResolvedRoute(route, selectedLabel) {

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

        renderSelectionMessage(selectedLabel);
    }

    function handleNavigationClick(event) {

        const targetElement = getEventTargetElement(event);
        const button =
            targetElement
                ? targetElement.closest(selectors.navigationButton)
                : null;

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

        const selectedLabel = button.textContent;

        handleResolvedRoute(route, selectedLabel);
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

    /**
     * Chooses the decorative icon for a sidebar heading. The visible label remains the accessible name.
     * @param {Element} heading The application-nav-heading element, before its text is wrapped.
     * @returns {string} main, data, account, administration, developer, or folder.
     */
    function sectionIconKind(heading) {
        const id = heading.id || "";
        if (id === "nav-main-heading") {
            return "main";
        }

        if (id === "nav-data-heading") {
            return "data";
        }

        if (id === "nav-account-heading") {
            return "account";
        }

        if (id === "nav-administration-heading") {
            return "administration";
        }

        if (id === "nav-developer-heading") {
            return "developer";
        }

        const label = heading.textContent.trim().toLowerCase();
        if (label === "main" || label === "data" || label === "account" || label === "administration" || label === "developer") {
            return label;
        }

        return "folder";
    }

    /**
     * Draws one outline icon in the same 24-unit stroke language as the member tree icons.
     * @param {string} kind The section kind from sectionIconKind.
     * @returns {SVGElement} A decorative icon.
     */
    function createSectionIcon(kind) {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "application-nav-heading-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        /**
         * Appends one shape to the icon being drawn.
         * @param {string} tagName SVG element name.
         * @param {object} attributes Attribute names and values.
         * @returns {void}
         */
        function shape(tagName, attributes) {
            const node = document.createElementNS(svgNamespace, tagName);
            Object.keys(attributes).forEach(function (name) {
                node.setAttribute(name, attributes[name]);
            });
            svg.appendChild(node);
        }

        if (kind === "main") {
            shape("path", { d: "M3 10a2 2 0 0 1 .709-1.528l7-5.999a2 2 0 0 1 2.582 0l7 5.999A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" });
            shape("path", { d: "M9 22V12h6v10" });
        } else if (kind === "data") {
            shape("ellipse", { cx: "12", cy: "5", rx: "9", ry: "3" });
            shape("path", { d: "M3 5v14c0 1.66 4.03 3 9 3s9-1.34 9-3V5" });
            shape("path", { d: "M3 12c0 1.66 4.03 3 9 3s9-1.34 9-3" });
        } else if (kind === "account") {
            shape("path", { d: "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" });
            shape("circle", { cx: "12", cy: "7", r: "4" });
        } else if (kind === "administration") {
            shape("path", { d: "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z" });
        } else if (kind === "developer") {
            shape("path", { d: "m16 18 6-6-6-6" });
            shape("path", { d: "m8 6-6 6 6 6" });
        } else {
            shape("path", { d: "M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2z" });
        }

        return svg;
    }

    /**
     * Draws the sidebar chevron. CSS rotates the same right-pointing shape down when the section is expanded.
     * @returns {SVGElement} The chevron.
     */
    function createSectionChevron() {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "application-nav-chevron");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");
        const path = document.createElementNS(svgNamespace, "path");
        path.setAttribute("d", "M9 5.5 16.5 12 9 18.5");
        path.setAttribute("fill", "none");
        path.setAttribute("stroke", "currentColor");
        path.setAttribute("stroke-width", "1.75");
        path.setAttribute("stroke-linecap", "round");
        path.setAttribute("stroke-linejoin", "round");
        svg.appendChild(path);
        return svg;
    }

    /**
     * Turns a heading into the section toggle and moves its items into one animated region.
     * @param {Element} section The application-nav-section element.
     * @param {number} index A stable fallback used when the heading has no id.
     * @returns {void}
     */
    function prepareSection(section, index) {
        const heading = section.querySelector(selectors.navigationHeading);
        if (!heading || section.querySelector(selectors.navigationPanel)) {
            return;
        }

        const labelText = formatSectionLabel(heading.textContent.trim());
        const icon = createSectionIcon(sectionIconKind(heading));
        const label = document.createElement("span");
        label.className = "application-nav-heading-label";
        label.textContent = labelText;
        heading.textContent = "";
        heading.appendChild(icon);
        heading.appendChild(label);
        heading.appendChild(createSectionChevron());
        heading.setAttribute("role", "button");
        heading.tabIndex = 0;

        const panel = document.createElement("div");
        panel.className = "application-nav-panel";
        panel.id = heading.id
            ? heading.id.replace(/-heading$/, "-panel")
            : "nav-section-" + String(index) + "-panel";
        heading.setAttribute("aria-controls", panel.id);

        const inner = document.createElement("div");
        inner.className = "application-nav-panel-inner";
        Array.from(section.children).forEach(function (child) {
            if (child !== heading) {
                inner.appendChild(child);
            }
        });
        panel.appendChild(inner);
        section.appendChild(panel);
    }

    /**
     * Sets one sidebar section open or closed without changing the selected page.
     * The class, chevron, aria-expanded value, and content region change together.
     * @param {Element} section The application-nav-section element.
     * @param {boolean} expanded True slides the section open.
     * @returns {void}
     */
    function setSectionExpanded(section, expanded) {
        section.classList.toggle(cssClasses.sectionCollapsed, !expanded);
        section.classList.toggle(cssClasses.sectionExpanded, expanded);

        const heading = section.querySelector(selectors.navigationHeading);
        const panel = section.querySelector(selectors.navigationPanel);
        if (heading) {
            heading.setAttribute("aria-expanded", String(expanded));
        }

        if (panel) {
            panel.inert = !expanded;
        }
    }

    /**
     * Expands the section whose heading is MAIN and collapses every other section.
     * Storage is intentionally unread. A saved value must not replace this load.
     * The first paint is applied without a transition so the load state does not animate.
     * @returns {void}
     */
    function applyInitialSectionExpansion() {
        const navigation = query(selectors.navigation);
        const sections = queryAll(selectors.navigationSection);
        if (navigation) {
            navigation.classList.add(cssClasses.accordionSettling);
        }

        sections.forEach(function (section, index) {
            const heading = section.querySelector(selectors.navigationHeading);
            const isMain = Boolean(heading && heading.id === "nav-main-heading");
            prepareSection(section, index);
            setSectionExpanded(section, isMain);
        });

        if (!navigation) {
            return;
        }

        navigation.classList.add(cssClasses.accordionReady);
        updateSectionToggle();
        window.requestAnimationFrame(function () {
            navigation.classList.remove(cssClasses.accordionSettling);
        });
    }

    /**
     * Toggles the sidebar section whose heading received the click.
     * @param {MouseEvent} event The click event.
     * @returns {void}
     */
    function handleSectionHeadingClick(event) {
        const targetElement = getEventTargetElement(event);
        const heading = targetElement
            ? targetElement.closest(selectors.navigationHeading)
            : null;

        if (!heading || heading.closest(selectors.navigationButton)) {
            return;
        }

        const section = heading.closest(selectors.navigationSection);
        if (!section) {
            return;
        }

        setSectionExpanded(
            section,
            section.classList.contains(cssClasses.sectionCollapsed)
        );
        updateSectionToggle();
    }

    /**
     * Toggles the focused sidebar heading from the keyboard.
     * @param {KeyboardEvent} event The key event.
     * @returns {void}
     */
    function handleSectionHeadingKeydown(event) {
        if (event.key !== "Enter" && event.key !== " ") {
            return;
        }

        const targetElement = getEventTargetElement(event);
        const heading = targetElement
            ? targetElement.closest(selectors.navigationHeading)
            : null;

        if (!heading || document.activeElement !== heading) {
            return;
        }

        event.preventDefault();
        handleSectionHeadingClick(event);
    }

    /**
     * Turns an all-uppercase heading into sentence case. Mixed-case text is kept so an acronym stays intact.
     * @param {string} text The heading text from the page.
     * @returns {string} The label to show.
     */
    function formatSectionLabel(text) {
        const known = {
            MAIN: "Main",
            DATA: "Data",
            ACCOUNT: "Account",
            ADMINISTRATION: "Administration",
            DEVELOPER: "Developer"
        };
        if (Object.prototype.hasOwnProperty.call(known, text)) {
            return known[text];
        }

        if (text !== text.toUpperCase()) {
            return text;
        }

        const lower = text.toLowerCase();
        return lower.charAt(0).toUpperCase() + lower.slice(1);
    }

    /**
     * Reads the signed-in account display name. A missing payload leaves the label empty.
     * @param {HTMLElement} name The account name element.
     * @returns {void}
     */
    function loadAccountDisplayName(name) {
        const apiClient = window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;
        if (!apiClient || typeof apiClient.fetchJson !== "function") {
            name.textContent = "";
            name.removeAttribute("title");
            return;
        }

        name.setAttribute("aria-busy", "true");
        apiClient.fetchJson("/api/me")
            .then(function (payload) {
                const account = payload && payload.currentAccount ? payload.currentAccount : null;
                const displayName = account && typeof account.displayName === "string"
                    ? account.displayName.trim()
                    : "";
                name.textContent = displayName;
                if (displayName) {
                    name.title = displayName;
                } else {
                    name.removeAttribute("title");
                }
            })
            .catch(function () {
                name.textContent = "";
                name.removeAttribute("title");
            })
            .finally(function () {
                name.removeAttribute("aria-busy");
            });
    }

    /**
 * Draws a small outline icon for a shell control.
 * Sidebar visibility uses a double chevron. Section expand-all uses a separate vertical pair.
 * Header actions use the question-circle, bell, and person shapes.
 * @param {string} kind search, sections-expand, sections-collapse, sidebar-collapse, sidebar-expand, support, notifications, or account.
     * @returns {SVGElement} A decorative icon.
     */
    function createControlIcon(kind) {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "application-nav-control-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        /**
         * Appends one shape to the control icon.
         * @param {string} tagName SVG element name.
         * @param {object} attributes Attribute names and values.
         * @returns {void}
         */
        function shape(tagName, attributes) {
            const node = document.createElementNS(svgNamespace, tagName);
            Object.keys(attributes).forEach(function (name) {
                node.setAttribute(name, attributes[name]);
            });
            svg.appendChild(node);
        }

        if (kind === "search") {
            shape("circle", { cx: "11", cy: "11", r: "7" });
            shape("path", { d: "m20 20-3.5-3.5" });
        } else if (kind === "sections-expand") {
            shape("path", { d: "m7 9 5-5 5 5" });
            shape("path", { d: "m7 15 5 5 5-5" });
        } else if (kind === "sections-collapse") {
            shape("path", { d: "m7 4 5 5 5-5" });
            shape("path", { d: "m7 20 5-5 5 5" });
        } else if (kind === "sidebar-expand") {
            shape("path", { d: "m6 17 5-5-5-5" });
            shape("path", { d: "m13 17 5-5-5-5" });
        } else if (kind === "support") {
            shape("circle", { cx: "12", cy: "12", r: "9" });
            shape("path", { d: "M9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3" });
            shape("path", { d: "M12 17h.01" });
        } else if (kind === "notifications") {
            shape("path", { d: "M10.3 21a1.9 1.9 0 0 0 3.4 0" });
            shape("path", { d: "M4 16a1 1 0 0 0 .8 1.6h14.4A1 1 0 0 0 20 16C18.7 14.6 18 13.2 18 9a6 6 0 0 0-12 0c0 4.2-1.4 5.6-2 7" });
        } else if (kind === "account") {
            shape("path", { d: "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" });
            shape("circle", { cx: "12", cy: "7", r: "4" });
        } else {
            shape("path", { d: "m11 17-5-5 5-5" });
            shape("path", { d: "m18 17-5-5 5-5" });
        }

        return svg;
    }

    /**
     * True when at least one sidebar section is collapsed.
     * @returns {boolean} True when expand-all is the next section action.
     */
    function anySectionCollapsed() {
        return Array.from(queryAll(selectors.navigationSection)).some(function (section) {
            return section.classList.contains(cssClasses.sectionCollapsed);
        });
    }

    /**
     * Sets the expand-all control to the action it will perform next.
     * @returns {void}
     */
    function updateSectionToggle() {
        const button = query("[data-shell-sections]");
        if (!button) {
            return;
        }

        const expand = anySectionCollapsed();
        const label = expand ? "Expand all sections" : "Collapse all sections";
        button.replaceChildren(createControlIcon(expand ? "sections-expand" : "sections-collapse"));
        button.title = label;
        button.setAttribute("aria-label", label);
    }

    /**
     * Expands every section when any is collapsed, and collapses every section when all are open.
     * @returns {void}
     */
    function toggleAllSections() {
        const expanded = anySectionCollapsed();
        queryAll(selectors.navigationSection).forEach(function (section) {
            setSectionExpanded(section, expanded);
        });
        updateSectionToggle();
    }

    /**
     * Shows or hides the whole sidebar. Section expansion is left as it is.
     * Focus moves to the control that remains visible.
     * @param {boolean} collapsed True leaves only the restore control.
     * @returns {void}
     */
    function setSidebarCollapsed(collapsed) {
        const shell = query(".app-shell");
        if (!shell) {
            return;
        }

        shell.classList.toggle("is-sidebar-collapsed", collapsed);
        const restore = query("[data-shell-restore]");
        const collapse = query("[data-shell-collapse]");
        if (collapsed && restore) {
            restore.focus();
            return;
        }

        if (collapse) {
            collapse.focus();
        }
    }

    /**
     * Builds one branding image for the shared header.
     * A failed load hides that image and leaves the other artwork in place.
     * @param {string} className Existing brand class, so the proportions stay with the artwork.
     * @param {string} src Image path under the served app.
     * @param {string} alt Accessible name for the artwork.
     * @returns {HTMLImageElement} The image.
     */
    function createBrandImage(className, src, alt) {
        const image = document.createElement("img");
        image.className = className;
        image.src = src;
        image.alt = alt;
        image.addEventListener("error", function () {
            image.hidden = true;
        });
        return image;
    }

    /**
     * Builds one header action that has no destination yet.
     * The control is disabled. Its wrapper carries the tooltip because a disabled button does not.
     * @param {string} kind support, notifications, or account.
     * @param {string} label The visible or accessible name.
     * @param {string} tooltip Why the action cannot be used.
     * @param {boolean} iconOnly True when the label is not shown as text.
     * @returns {HTMLElement} The disabled action.
     */
    function createHeaderPlaceholder(kind, label, tooltip, iconOnly) {
        const wrap = document.createElement("span");
        wrap.className = "application-header-action";
        wrap.title = tooltip;

        const button = document.createElement("button");
        button.type = "button";
        button.className = iconOnly
            ? "application-header-action-button application-header-action-icon"
            : "application-header-action-button";
        button.disabled = true;
        button.setAttribute("aria-disabled", "true");
        button.appendChild(createControlIcon(kind));
        if (iconOnly) {
            button.setAttribute("aria-label", label);
        } else {
            const text = document.createElement("span");
            text.textContent = label;
            button.appendChild(text);
        }

        wrap.appendChild(button);
        return wrap;
    }

    /**
     * Places the account row and the existing girder and bridge artwork in one band.
     * Support, notifications, and the header account action have no destination, so they stay disabled.
     * The band uses the shell's sidebar column, so its right edge matches the navigation.
     * @param {HTMLElement} account The signed-in account row.
     * @param {HTMLElement} restore The control that returns the sidebar after it collapses.
     * @returns {void}
     */
    function mountApplicationHeader(account, restore) {
        const shell = query(".app-shell");
        if (!shell || shell.querySelector(".application-header")) {
            return;
        }

        const header = document.createElement("header");
        header.className = "application-header";

        const accountSlot = document.createElement("div");
        accountSlot.className = "application-header-account";
        accountSlot.appendChild(account);
        accountSlot.appendChild(restore);

        const brand = document.createElement("div");
        brand.className = "application-header-brand";
        const artwork = document.createElement("div");
        artwork.className = "application-header-artwork";
        artwork.appendChild(createBrandImage(
            "member-brand-nameplate",
            "/app/images/pluralbridge-nameplate.png",
            "The Plural Bridge"
        ));
        artwork.appendChild(createBrandImage(
            "member-brand-logo",
            "/app/images/pluralbridge-about-logo-smaller.png",
            "PluralBridge logo"
        ));
        const actions = document.createElement("div");
        actions.className = "application-header-actions";
        actions.appendChild(createHeaderPlaceholder("support", "Support", "Support is not available", false));
        actions.appendChild(createHeaderPlaceholder("notifications", "Notifications", "Notifications are not available", true));
        actions.appendChild(createHeaderPlaceholder("account", "Account", "Account is not available", true));
        brand.appendChild(artwork);
        brand.appendChild(actions);

        header.appendChild(accountSlot);
        header.appendChild(brand);
        shell.insertBefore(header, shell.firstChild);
    }

    /**
     * Builds the account row, the disabled search preview, and the sidebar controls.
     * The account mark is the same decorative person icon used by the Account section.
     * This function owns the current-page layout chrome. It does not choose a navigation route.
     * @returns {void}
     */
    function mountSidebarChrome() {
        const navigation = query(selectors.navigation);
        if (!navigation || navigation.querySelector(".application-nav-body")) {
            return;
        }

        const restore = document.createElement("button");
        restore.type = "button";
        restore.className = "application-nav-icon-button application-nav-restore";
        restore.setAttribute("data-shell-restore", "");
        restore.title = "Expand sidebar";
        restore.setAttribute("aria-label", "Expand sidebar");
        restore.setAttribute("aria-expanded", "false");
        restore.appendChild(createControlIcon("sidebar-expand"));
        restore.addEventListener("click", function () {
            setSidebarCollapsed(false);
        });

        const body = document.createElement("div");
        body.className = "application-nav-body";
        Array.from(navigation.childNodes).forEach(function (child) {
            body.appendChild(child);
        });

        const account = document.createElement("div");
        account.className = "application-nav-account";
        const accountIcon = createSectionIcon("account");
        accountIcon.classList.add("application-nav-account-icon");
        const accountName = document.createElement("span");
        accountName.className = "application-nav-account-name";
        account.appendChild(accountIcon);
        account.appendChild(accountName);

        const tools = document.createElement("div");
        tools.className = "application-nav-tools";
        const searchWrap = document.createElement("div");
        searchWrap.className = "application-nav-search";
        searchWrap.appendChild(createControlIcon("search"));
        const search = document.createElement("input");
        search.type = "search";
        search.disabled = true;
        search.placeholder = "Search…";
        search.setAttribute("aria-label", "Search, unavailable");
        searchWrap.appendChild(search);

        const sectionsButton = document.createElement("button");
        sectionsButton.type = "button";
        sectionsButton.className = "application-nav-icon-button";
        sectionsButton.setAttribute("data-shell-sections", "");
        sectionsButton.addEventListener("click", toggleAllSections);

        const collapse = document.createElement("button");
        collapse.type = "button";
        collapse.className = "application-nav-icon-button";
        collapse.setAttribute("data-shell-collapse", "");
        collapse.title = "Collapse sidebar";
        collapse.setAttribute("aria-label", "Collapse sidebar");
        collapse.setAttribute("aria-expanded", "true");
        collapse.appendChild(createControlIcon("sidebar-collapse"));
        collapse.addEventListener("click", function () {
            setSidebarCollapsed(true);
        });

        tools.appendChild(searchWrap);
        tools.appendChild(sectionsButton);
        tools.appendChild(collapse);
        body.insertBefore(tools, body.firstChild);
        navigation.appendChild(body);
        mountApplicationHeader(account, restore);
        updateSectionToggle();
        loadAccountDisplayName(accountName);
    }

    function initializeNavigation() {

        applyInitialSectionExpansion();

        document.addEventListener(
            "click",
            handleNavigationClick
        );
        document.addEventListener(
            "click",
            handleSectionHeadingClick
        );
        document.addEventListener(
            "keydown",
            handleSectionHeadingKeydown
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

    /**
     * Builds the current page shell. The sidebar starts expanded, Main starts open, and
     * the other sections start collapsed. Those defaults are not read from storage.
     * @returns {void}
     */
    function initialize() {

        mountSidebarChrome();
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
