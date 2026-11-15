/**
 * Member forms, local profile previews, and the account-scoped profile and Fronting saves used by the card.
 * Depends on window.PluralBridge.apiClient, the navigator account and map snapshot, and window.PluralBridge.developerTools for the write switch.
 * When database writes are disabled, profile Save returns the local preview and does not report persisted true. Fronting then returns saved false. Failures from the API are caught and returned as saved false. They do not reject.
 */
window.PluralBridge = window.PluralBridge || {};

/**
 * Creates the members module.
 * @returns {object} The members API, including saveMemberProfile and saveMemberFronting.
 */
window.PluralBridge.members = (function () {
    /**
     * Creates the polite status line used by the add and edit forms.
     *
     * @returns {HTMLParagraphElement} An empty status element.
     */
    function createStatusMessage() {
        const status = document.createElement("p");
        status.className = "member-form-status";
        status.setAttribute("aria-live", "polite");
        status.textContent = "";
        return status;
    }

    /**
     * Creates a labeled text input.
     *
     * @param {string} id Input id and name.
     * @param {string} labelText Label text.
     * @param {boolean} required True marks the input required.
     * @returns {object} row and input.
     */
    function createTextField(id, labelText, required) {
        const row = document.createElement("div");
        row.className = "field-row";

        const label = document.createElement("label");
        label.setAttribute("for", id);
        label.textContent = labelText;

        const input = document.createElement("input");
        input.id = id;
        input.name = id;
        input.type = "text";
        input.autocomplete = "off";

        if (required) {
            input.required = true;
        }

        row.appendChild(label);
        row.appendChild(input);

        return {
            row: row,
            input: input
        };
    }

    /**
     * Creates a labeled description textarea.
     *
     * @param {string} id Textarea id and name.
     * @param {string} labelText Label text.
     * @returns {object} row and input.
     */
    function createDescriptionField(id, labelText) {
        const row = document.createElement("div");
        row.className = "field-row";

        const label = document.createElement("label");
        label.setAttribute("for", id);
        label.textContent = labelText;

        const textarea = document.createElement("textarea");
        textarea.id = id;
        textarea.name = id;
        textarea.rows = 6;

        row.appendChild(label);
        row.appendChild(textarea);

        return {
            row: row,
            input: textarea
        };
    }

    /**
     * Disables or enables every input, textarea, and button in a form.
     *
     * @param {HTMLFormElement} form Form whose controls change.
     * @param {boolean} isBusy True disables the controls.
     * @returns {void} Nothing is returned.
     */
    function setFormBusy(form, isBusy) {
        const controls = form.querySelectorAll("input, textarea, button");

        /**
         * Disables or enables one form control.
         *
         * @param {Element} control Input, textarea, or button.
         * @returns {void} Nothing is returned.
         */
        controls.forEach(function (control) {
            control.disabled = isBusy;
        });
    }

    /**
     * Reads a member id.
     *
     * @param {object} member Member record. A missing member or id yields an empty string.
     * @returns {string} The member id.
     */
    function getMemberId(member) {
        return member && member.memberId ? member.memberId : "";
    }

    /**
     * Builds an add or edit body from the form fields.
     *
     * @param {HTMLInputElement} displayNameInput Name field. The value is trimmed and always included.
     * @param {HTMLInputElement} pronounsInput Pronouns field. A blank value is omitted.
     * @param {HTMLTextAreaElement} descriptionInput Description field. A blank value is omitted.
     * @returns {object} displayName, and pronouns or description when those values are present.
     */
    function buildMemberRequest(displayNameInput, pronounsInput, descriptionInput) {
        const request = {
            displayName: displayNameInput.value.trim()
        };

        const pronouns = pronounsInput.value.trim();
        const description = descriptionInput.value.trim();

        if (pronouns) {
            request.pronouns = pronouns;
        }

        if (description) {
            request.description = description;
        }

        return request;
    }

    /**
     * Draws a toolbar icon.
     *
     * @param {string} iconName tree, add, addGroup, addMember, expanded, compact, sun, moon, or any other name, which draws the fallback mark.
     * @returns {SVGElement} The icon.
     */
    function createToolbarIcon(iconName) {
        const svgNamespace = "http://www.w3.org/2000/svg";

        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-toolbar-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "18");
        svg.setAttribute("height", "18");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        const path = document.createElementNS(svgNamespace, "path");
        path.setAttribute("fill", "currentColor");

        if (iconName === "tree") {
            path.setAttribute("d", "M4 3h7v3.5H4V3zm2.6 3.5h1.4V10H6.6V6.5zM6.6 9h5v1.4h-5V9zM11.2 7.2H20v3.6h-8.8V7.2zM6.6 10.4h1.4V15H6.6v-4.6zM6.6 13.8h5v1.4h-5v-1.4zM11.2 12.4H20v3.6h-8.8v-3.6z");
        } else if (iconName === "add") {
            path.setAttribute("d", "M11 4h2v7h7v2h-7v7h-2v-7H4v-2h7V4z");
        } else if (iconName === "addGroup") {
            path.setAttribute("d", "M4 5h6l2 2h8v5h-2V9H6v8h6v2H4V5zm13 8h2v3h3v2h-3v3h-2v-3h-3v-2h3v-3z");
        } else if (iconName === "addMember") {
            path.setAttribute("d", "M9 11a4 4 0 1 1 0-8 4 4 0 0 1 0 8zm0 2c-4 0-7 2-7 5v1h10.5a5.5 5.5 0 0 1 .6-6H9zm8 0h2v3h3v2h-3v3h-2v-3h-3v-2h3v-3z");
        } else if (iconName === "expanded") {
            path.setAttribute("d", "M4 5h16v4H4V5zm0 5.5h16v4H4v-4zM4 16h16v3H4v-3z");
        } else if (iconName === "compact") {
            path.setAttribute("d", "M5 6h14v2H5V6zm0 5h14v2H5v-2zm0 5h14v2H5v-2z");
        } else if (iconName === "sun") {
            path.setAttribute("fill", "none");
            path.setAttribute("stroke", "currentColor");
            path.setAttribute("stroke-width", "1.8");
            path.setAttribute("stroke-linecap", "round");
            path.setAttribute("d", "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8zM12 2.5v2.2M12 19.3v2.2M4.8 4.8l1.6 1.6M17.6 17.6l1.6 1.6M2.5 12h2.2M19.3 12h2.2M4.8 19.2l1.6-1.6M17.6 6.4l1.6-1.6");
        } else if (iconName === "moon") {
            path.setAttribute("d", "M16.5 3.2a8.2 8.2 0 1 0 4.3 14.8A7 7 0 0 1 16.5 3.2z");
        } else {
            path.setAttribute("d", "M11 10h2v8h-2v-8zm0-4h2v2h-2V6zm1-4a10 10 0 1 0 0 20 10 10 0 0 0 0-20z");
        }

        svg.appendChild(path);
        return svg;
    }

    /**
     * Builds a toolbar button and attaches its click handler when one is supplied.
     *
     * @param {string} iconName Icon passed to createToolbarIcon.
     * @param {string} labelText Visible label.
     * @param {object} options Optional disabled, title, ariaLabel, pressed, and onClick. A disabled button uses the planned title.
     * @returns {HTMLButtonElement} The button.
     */
    function createToolbarButton(iconName, labelText, options) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "member-toolbar-button";

        if (options && options.disabled) {
            button.disabled = true;
            button.title = options.title || labelText + " planned";
        } else if (options && options.title) {
            button.title = options.title;
        }

        if (options && options.ariaLabel) {
            button.setAttribute("aria-label", options.ariaLabel);
        }

        if (options && options.pressed !== undefined) {
            button.setAttribute("aria-pressed", String(options.pressed));
        }

        button.appendChild(createToolbarIcon(iconName));

        const label = document.createElement("span");
        label.className = "member-toolbar-label";
        label.textContent = labelText;

        button.appendChild(label);

        if (options && typeof options.onClick === "function") {
            button.addEventListener("click", options.onClick);
        }

        return button;
    }

    /**
     * Chooses the tree-toggle label.
     *
     * @param {boolean} isVisible True when the tree panel is open.
     * @returns {string} Hide Tree or Show Tree.
     */
    function treeToggleLabel(isVisible) {
        return isVisible ? "Hide Tree" : "Show Tree";
    }

    let addMenuController = null;

    /**
     * Aborts the listeners owned by the current Add menu.
     *
     * @returns {void} A missing controller does nothing.
     */
    function releaseAddMenuHandlers() {
        if (addMenuController) {
            addMenuController.abort();
            addMenuController = null;
        }
    }

    /**
     * Updates the tree toggle to the current panel visibility.
     *
     * @param {HTMLButtonElement} button Tree button. A missing button does nothing.
     * @param {boolean} isVisible True when the panel is open.
     * @returns {void} Nothing is returned.
     */
    function setTreeToggleVisible(button, isVisible) {
        if (!button) {
            return;
        }

        const labelText = treeToggleLabel(isVisible);
        const label = button.querySelector(".member-toolbar-label");

        button.setAttribute("aria-pressed", String(isVisible));
        button.setAttribute("aria-label", labelText);
        button.title = labelText;

        if (label) {
            label.textContent = labelText;
        }
    }

    /**
     * Builds the Add menu and replaces any previous menu listeners.
     *
     * @param {object} options Callbacks toggleAddMember, toggleAddGroup, toggleAddPlace, toggleAddSystem, and toggleAddRelationship. memberActionsEnabled false disables Member.
     * @returns {HTMLDetailsElement} The menu. A missing callback leaves that option disabled.
     */
    function createAddMenu(options) {
        releaseAddMenuHandlers();
        const controller = new AbortController();
        addMenuController = controller;
        const signal = controller.signal;

        const details = document.createElement("details");
        details.className = "member-toolbar-add-menu";

        const summary = document.createElement("summary");
        summary.className = "member-toolbar-button member-toolbar-add-trigger is-primary";
        summary.setAttribute("aria-expanded", "false");
        summary.appendChild(createToolbarIcon("add"));

        const label = document.createElement("span");
        label.className = "member-toolbar-label";
        label.textContent = "Add";
        summary.appendChild(label);

        const chevron = document.createElement("span");
        chevron.className = "member-toolbar-add-chevron";
        chevron.textContent = "v";
        summary.appendChild(chevron);

        const popover = document.createElement("div");
        popover.className = "member-toolbar-add-popover";

        /**
         * Builds one Add-menu option.
         *
         * @param {string} labelText Option label.
         * @param {string} iconType Icon name passed to dialogOutlineIcon.
         * @param {boolean} isEnabled False disables the option and marks it planned.
         * @param {function} clickHandler Called when an enabled option is chosen. A missing handler leaves the button without a click.
         * @returns {HTMLButtonElement} The option.
         */
        function createAddOption(labelText, iconType, isEnabled, clickHandler) {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "member-toolbar-add-option";

            const icon = dialogOutlineIcon(iconType);
            if (icon) {
                button.appendChild(icon);
            }

            const label = document.createElement("span");
            label.textContent = labelText;
            button.appendChild(label);

            if (!isEnabled) {
                button.disabled = true;
                button.title = labelText + " planned";
            } else if (typeof clickHandler === "function") {
                /**
                 * Runs the option handler and closes the menu.
                 *
                 * @returns {void} Nothing is returned.
                 */
                button.addEventListener("click", function () {
                    clickHandler();
                    details.open = false;
                });
            }

            return button;
        }

        const memberAddEnabled = !(options && options.memberActionsEnabled === false);
        /**
         * Opens the member add action when that callback exists.
         *
         * @returns {void} Nothing is returned.
         */
        popover.appendChild(createAddOption("Member", "member", memberAddEnabled, function () {
            if (options && typeof options.toggleAddMember === "function") {
                options.toggleAddMember();
            }
        }));
        /**
         * Opens the group add action when that callback exists.
         *
         * @returns {void} Nothing is returned.
         */
        popover.appendChild(createAddOption("Group", "group", Boolean(options && typeof options.toggleAddGroup === "function"), function () {
            if (options && typeof options.toggleAddGroup === "function") {
                options.toggleAddGroup();
            }
        }));
        /**
         * Opens the place add action when that callback exists.
         *
         * @returns {void} Nothing is returned.
         */
        popover.appendChild(createAddOption("Place", "place", Boolean(options && typeof options.toggleAddPlace === "function"), function () {
            if (options && typeof options.toggleAddPlace === "function") {
                options.toggleAddPlace();
            }
        }));
        /**
         * Opens the system add action when that callback exists.
         *
         * @returns {void} Nothing is returned.
         */
        popover.appendChild(createAddOption("System", "system", Boolean(options && typeof options.toggleAddSystem === "function"), function () {
            if (options && typeof options.toggleAddSystem === "function") {
                options.toggleAddSystem();
            }
        }));

        const relationshipSeparator = document.createElement("div");
        relationshipSeparator.className = "member-toolbar-add-separator";
        relationshipSeparator.setAttribute("role", "separator");
        popover.appendChild(relationshipSeparator);
        /**
         * Opens the relationship add action when that callback exists.
         *
         * @returns {void} Nothing is returned.
         */
        popover.appendChild(createAddOption("Relationship", "relationship", Boolean(options && typeof options.toggleAddRelationship === "function"), function () {
            if (options && typeof options.toggleAddRelationship === "function") {
                options.toggleAddRelationship();
            }
        }));

        details.appendChild(summary);
        details.appendChild(popover);

        /**
         * Copies the menu open state into aria-expanded.
         *
         * @returns {void} Nothing is returned.
         */
        function syncExpanded() {
            summary.setAttribute("aria-expanded", details.open ? "true" : "false");
        }

        /**
         * Closes the Add menu when it is open.
         *
         * @returns {void} Nothing is returned.
         */
        function closeMenu() {
            if (details.open) {
                details.open = false;
            }
        }

        details.addEventListener("toggle", syncExpanded, { signal: signal });

        /**
         * Closes the menu after focus leaves it.
         *
         * @returns {void} The check waits one turn so the new focus is known.
         */
        details.addEventListener("focusout", function () {
            /**
             * Closes the menu unless focus is still inside it or the menu was aborted.
             *
             * @returns {void} Nothing is returned.
             */
            window.setTimeout(function () {
                if (signal.aborted || !details.isConnected || !details.open) {
                    return;
                }

                const active = document.activeElement;
                if (active && details.contains(active)) {
                    return;
                }

                closeMenu();
            }, 0);
        }, { signal: signal });

        /**
         * Closes the menu on Escape and returns focus to its summary.
         *
         * @param {KeyboardEvent} event Escape while the menu is open. Other keys are ignored.
         * @returns {void} Nothing is returned.
         */
        details.addEventListener("keydown", function (event) {
            if (event.key !== "Escape" || !details.open) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            closeMenu();
            summary.focus();
        }, { signal: signal });

        /**
         * Closes the menu on a pointer outside it, and releases its listeners after it leaves the document.
         *
         * @param {PointerEvent} event Document pointerdown.
         * @returns {void} Nothing is returned.
         */
        document.addEventListener("pointerdown", function (event) {
            if (!details.isConnected) {
                releaseAddMenuHandlers();
                return;
            }

            if (details.contains(event.target)) {
                return;
            }

            closeMenu();
        }, { capture: true, signal: signal });

        return details;
    }

    /**
     * Builds the member toolbar, including the tree toggle, Add menu, planned view buttons, and theme button.
     *
     * @param {object} options isSystemPanelOpen and the Add-menu callbacks. toggleSystemPanel runs from the tree button.
     * @returns {HTMLDivElement} The toolbar.
     */
    function createMemberToolbar(options) {
        const toolbar = document.createElement("div");
        toolbar.className = "member-toolbar";
        toolbar.setAttribute("aria-label", "Member toolbar");

        const primaryGroup = document.createElement("div");
        primaryGroup.className = "member-toolbar-group";

        const viewGroup = document.createElement("div");
        viewGroup.className = "member-toolbar-group member-toolbar-view-group";

        const treeVisible = options && options.isSystemPanelOpen === true;
        const treeButton = createToolbarButton("tree", treeToggleLabel(treeVisible), {
            pressed: treeVisible,
            title: treeToggleLabel(treeVisible),
            ariaLabel: treeToggleLabel(treeVisible),
            /**
             * Toggles the system panel when that callback exists.
             *
             * @returns {void} Nothing is returned.
             */
            onClick: function () {
                if (options && typeof options.toggleSystemPanel === "function") {
                    options.toggleSystemPanel();
                }
            }
        });
        treeButton.classList.add("member-toolbar-tree-toggle");
        /**
         * Rewrites the tree-toggle label after its pressed state changes.
         *
         * @returns {void} Nothing is returned.
         */
        treeButton.addEventListener("click", function () {
            setTreeToggleVisible(treeButton, treeButton.getAttribute("aria-pressed") === "true");
        });
        primaryGroup.appendChild(treeButton);

        primaryGroup.appendChild(createAddMenu(options));

        const presentation = "expanded";
        const presentationButton = createToolbarButton(
            presentation === "compact" ? "compact" : "expanded",
            presentation === "compact" ? "Compact" : "Expanded",
            {
                disabled: true,
                title: "Card presentation switching is planned"
            }
        );
        presentationButton.classList.add("member-toolbar-presentation");
        viewGroup.appendChild(presentationButton);

        viewGroup.appendChild(createToolbarButton("details", "Details", {
            disabled: true,
            title: "Member details view planned"
        }));

        const themeButton = createToolbarButton("moon", "Dark", {
            /**
             * Switches the theme when window.PluralBridge.theme.toggle exists.
             *
             * @returns {void} Nothing is returned.
             */
            onClick: function () {
                if (window.PluralBridge.theme && typeof window.PluralBridge.theme.toggle === "function") {
                    window.PluralBridge.theme.toggle();
                }
            }
        });
        themeButton.classList.add("member-toolbar-theme");

        /**
         * Paints the theme button for the active light or dark theme.
         *
         * @returns {void} A missing theme reader is treated as dark.
         */
        function paintThemeButton() {
            const active = window.PluralBridge.theme && typeof window.PluralBridge.theme.get === "function"
                ? window.PluralBridge.theme.get()
                : "dark";
            const isLight = active === "light";
            const label = themeButton.querySelector(".member-toolbar-label");
            const icon = themeButton.querySelector(".member-toolbar-icon");
            const nextIcon = createToolbarIcon(isLight ? "sun" : "moon");
            const action = isLight ? "Switch to dark theme" : "Switch to light theme";
            if (icon) {
                icon.replaceWith(nextIcon);
            } else {
                themeButton.insertBefore(nextIcon, label);
            }
            if (label) {
                label.textContent = isLight ? "Light" : "Dark";
            }
            themeButton.setAttribute("aria-label", action);
            themeButton.title = action;
        }

        paintThemeButton();
        if (window.PluralBridge.theme && typeof window.PluralBridge.theme.subscribe === "function") {
            /**
             * Placeholder unsubscribe used until the theme subscription returns the real one.
             *
             * @returns {void} Nothing is returned.
             */
            let unsubscribe = function () {};
            /**
             * Repaints the theme button, and unsubscribes after the button leaves the document.
             *
             * @returns {void} Nothing is returned.
             */
            unsubscribe = window.PluralBridge.theme.subscribe(function () {
                if (!themeButton.isConnected) {
                    unsubscribe();
                    return;
                }
                paintThemeButton();
            });
        }
        viewGroup.appendChild(themeButton);

        toolbar.appendChild(primaryGroup);
        toolbar.appendChild(viewGroup);

        return toolbar;
    }

    /**
     * Draws a form action icon.
     *
     * @param {string} iconName save, add, or any other name, which draws the cancel mark.
     * @returns {SVGElement} The icon.
     */
    function createFormActionIcon(iconName) {
        const svgNamespace = "http://www.w3.org/2000/svg";

        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-form-action-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "16");
        svg.setAttribute("height", "16");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        const path = document.createElementNS(svgNamespace, "path");
        path.setAttribute("fill", "currentColor");

        if (iconName === "save") {
            path.setAttribute("d", "M17 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V7l-4-4zM7 5h8v5H7V5zm10 14H7v-6h10v6z");
        } else if (iconName === "add") {
            path.setAttribute("d", "M9 11a4 4 0 1 1 0-8 4 4 0 0 1 0 8zm0 2c-4 0-7 2-7 5v1h10.5a5.5 5.5 0 0 1 .6-6H9zm8 0h2v3h3v2h-3v3h-2v-3h-3v-2h3v-3z");
        } else {
            path.setAttribute("d", "M6.4 5 5 6.4 10.6 12 5 17.6 6.4 19 12 13.4 17.6 19 19 17.6 13.4 12 19 6.4 17.6 5 12 10.6 6.4 5z");
        }

        svg.appendChild(path);
        return svg;
    }

    /**
     * Builds a labeled form action button.
     *
     * @param {string} iconName Icon name.
     * @param {string} labelText Button label.
     * @param {string} buttonType Button type, such as submit or button.
     * @returns {HTMLButtonElement} The button.
     */
    function createFormActionButton(iconName, labelText, buttonType) {
        const button = document.createElement("button");
        button.type = buttonType;
        button.className = "member-form-action-button";

        button.appendChild(createFormActionIcon(iconName));

        const label = document.createElement("span");
        label.textContent = labelText;

        button.appendChild(label);

        return button;
    }

    const memberPreviewsByAccount = {};
    const memberProfilePreviewsByAccount = {};

    /**
     * Reports whether profile and Fronting writes must stay local.
     * Writes are disabled unless developerTools exists and databaseWritesDisabled is strictly false.
     * @returns {boolean} True when writes must not be sent.
     */
    function databaseWritesAreDisabled() {
        const tools = window.PluralBridge && window.PluralBridge.developerTools
            ? window.PluralBridge.developerTools
            : null;

        return !tools || tools.databaseWritesDisabled !== false;
    }

    /**
     * Reads the signed-in account id from the navigator.
     *
     * @returns {string} The account id, or an empty string when the navigator cannot supply one.
     */
    function currentAccountId() {
        const navigatorView = window.PluralBridge && window.PluralBridge.navigator
            ? window.PluralBridge.navigator
            : null;

        if (!navigatorView || typeof navigatorView.getAccountId !== "function") {
            return "";
        }

        const accountId = navigatorView.getAccountId();
        return accountId ? String(accountId) : "";
    }

    /**
     * Copies the destination fields from an add request.
     *
     * @param {object} request Request with a destination object. A missing destination returns null.
     * @returns {object|null} objectType, objectId, pathKey, and anchorName.
     */
    function previewDestination(request) {
        if (!request || !request.destination || typeof request.destination !== "object") {
            return null;
        }

        return {
            objectType: request.destination.objectType ? String(request.destination.objectType) : "",
            objectId: request.destination.objectId ? String(request.destination.objectId) : "",
            pathKey: request.destination.pathKey ? String(request.destination.pathKey) : "",
            anchorName: typeof request.destination.anchorName === "string" ? request.destination.anchorName : ""
        };
    }

    /**
     * Keeps a known preview kind.
     *
     * @param {string} kind group, system, place, relationship, member, or any other value, which becomes member.
     * @returns {string} The preview noun.
     */
    function previewNounFor(kind) {
        if (kind === "group" || kind === "system" || kind === "place" || kind === "relationship" || kind === "member") {
            return kind;
        }

        return "member";
    }

    /**
     * Holds an add-member, anchor, or relationship draft in memory for the current account.
     * When writes are enabled, this returns the not-connected status and stores nothing. A missing or changed account stores nothing. A second outgoing relationship from the same source is refused.
     *
     * @param {string} kind Preview kind passed to previewNounFor.
     * @param {object} request Draft fields, destination, and relationship endpoints when present.
     * @returns {Promise<object>} Fulfills with status and, for a stored preview, reset true. It does not reject.
     */
    async function holdAccountPreview(kind, request) {
        const previewNoun = previewNounFor(kind);
        const isMember = previewNoun === "member";

        if (!databaseWritesAreDisabled()) {
            return {
                status: previewNoun.charAt(0).toUpperCase() + previewNoun.slice(1) + " creation is not connected to an anchor-specific save endpoint yet."
            };
        }

        const accountId = currentAccountId();
        if (!accountId) {
            return {
                status: "The signed-in account is not available, so this " + previewNoun + " preview was not stored."
            };
        }

        await Promise.resolve();

        if (currentAccountId() !== accountId) {
            return {
                status: "The signed-in account changed, so this " + previewNoun + " preview was not stored."
            };
        }

        if (!memberPreviewsByAccount[accountId]) {
            memberPreviewsByAccount[accountId] = [];
        }

        const destination = previewDestination(request);
        const record = {
            previewKind: previewNoun,
            objectType: previewNoun === "relationship" ? "relationship" : previewNoun,
            displayName: request && request.displayName ? String(request.displayName) : "",
            description: request && request.description ? String(request.description) : "",
            color: request && request.color ? String(request.color) : "",
            visibility: request && request.visibility ? String(request.visibility) : "",
            permission: request && request.permission ? String(request.permission) : "",
            destination: destination
        };
        if (isMember) {
            record.pronouns = request && request.pronouns ? String(request.pronouns) : "";
            record.fronting = Boolean(request && request.fronting === true);
            record.avatarObjectUrl = request && request.avatarObjectUrl ? String(request.avatarObjectUrl) : "";
        }
        if (previewNoun === "relationship") {
            record.relationshipType = request && request.relationshipType ? String(request.relationshipType) : "";
            record.source = request && request.source ? request.source : null;
            record.target = request && request.target ? request.target : null;
            record.previewLabel = request && request.previewLabel ? String(request.previewLabel) : "";
            const sourceKey = relationshipObjectKey(record.source);
            /**
             * Reports whether this account already has an outgoing relationship preview from the same source.
             *
             * @param {object} stored One held preview record.
             * @returns {boolean} True for an existing outgoing relationship from that source.
             */
            const existingOutgoing = sourceKey && memberPreviewsByAccount[accountId].some(function (stored) {
                return stored &&
                    stored.previewKind === "relationship" &&
                    relationshipObjectKey(stored.source) === sourceKey;
            });
            if (existingOutgoing) {
                return {
                    status: "This source already has an outgoing relationship in this preview. The draft was kept."
                };
            }
        }

        memberPreviewsByAccount[accountId].push(record);

        console.info(
            "Suppressed add-" + previewNoun + " database write. Details were held in browser memory for the signed-in account. No fields were written to the log."
        );

        return {
            status: "Preview only: " + previewNoun + " details held in browser memory. Nothing was saved to the database.",
            reset: true
        };
    }

    /**
     * Holds a member add draft through the account preview.
     *
     * @param {object} request Member draft passed to holdAccountPreview.
     * @returns {Promise<object>} The preview result. It fulfills and does not reject.
     */
    async function previewAddMember(request) {
        return holdAccountPreview("member", request);
    }

    /**
     * Builds the account or member key used by profile previews.
     *
     * @param {*} value Account or member id. A missing value becomes an empty string.
     * @returns {string} The lowercased key.
     */
    function profilePreviewKey(value) {
        return value ? String(value).toLowerCase() : "";
    }

    /**
     * Accepts only a #RRGGBB preview color.
     *
     * @param {*} value Candidate color.
     * @returns {string|null} The color, or null.
     */
    function explicitPreviewColor(value) {
        return typeof value === "string" && /^#[0-9A-Fa-f]{6}$/.test(value) ? value : null;
    }

    /**
     * Keeps only a boolean Fronting preview value.
     *
     * @param {*} value Candidate Fronting value.
     * @returns {boolean|null} true, false, or null.
     */
    function explicitPreviewFronting(value) {
        return value === true || value === false ? value : null;
    }

    /**
     * Keeps visibility type ids 1 through 4.
     *
     * @param {*} value Candidate id.
     * @returns {number|null} The id, or null.
     */
    function canonicalVisibilityId(value) {
        if (value === 1 || value === 2 || value === 3 || value === 4) {
            return value;
        }

        return null;
    }

    /**
     * Keeps inherit, viewer, editor, manager, and owner.
     *
     * @param {*} value Candidate permission.
     * @returns {string|null} The permission, or null.
     */
    function canonicalPermission(value) {
        if (value === "inherit" || value === "viewer" || value === "editor" || value === "manager" || value === "owner") {
            return value;
        }

        return null;
    }

    /**
     * Copies visibility onto a profile preview when the new fields include it or the previous preview already marked it explicit.
     *
     * @param {object} record Preview being written.
     * @param {object} previous Previous preview, or null.
     * @param {object} fields Incoming fields, or null when reading the stored preview back.
     * @returns {void} A preview with neither source is left unchanged.
     */
    function assignVisibilityPreview(record, previous, fields) {
        const incoming = Boolean(fields) && (
            Object.prototype.hasOwnProperty.call(fields, "visibilityTypeId")
            || Object.prototype.hasOwnProperty.call(fields, "visibilityInherit")
        );
        const retained = !incoming && Boolean(previous) && previous.visibilityExplicit === true;
        if (!incoming && !retained) {
            return;
        }

        const source = incoming ? fields : previous;
        record.visibilityExplicit = true;
        record.visibilityInherit = source.visibilityInherit === true;
        record.visibilityTypeId = record.visibilityInherit ? null : canonicalVisibilityId(source.visibilityTypeId);
    }

    /**
     * Copies permission onto a profile preview when the new fields include it or the previous preview already marked it explicit.
     *
     * @param {object} record Preview being written.
     * @param {object} previous Previous preview.
     * @param {object} fields Incoming fields, or null when reading the stored preview back.
     * @returns {void} A preview with neither source is left unchanged.
     */
    function assignPermissionPreview(record, previous, fields) {
        const incoming = Boolean(fields) && Object.prototype.hasOwnProperty.call(fields, "permission");
        const retained = !incoming && Boolean(previous) && previous.permissionExplicit === true;
        if (!incoming && !retained) {
            return;
        }

        const source = incoming ? fields : previous;
        record.permissionExplicit = true;
        record.permission = canonicalPermission(source.permission);
    }

    /**
     * Keeps a non-empty avatar preview URL.
     *
     * @param {*} value Candidate URL.
     * @returns {string|null} The URL, or null.
     */
    function explicitPreviewAvatar(value) {
        return typeof value === "string" && value ? value : null;
    }

    /**
     * Copies color onto a profile preview. An incoming null clears it. A non-hex incoming color leaves the stored color unchanged.
     *
     * @param {object} record Preview being written.
     * @param {object} previous Previous preview used when color is absent.
     * @param {object} fields Incoming fields, or null when retaining the previous color.
     * @returns {void} Nothing is returned.
     */
    function assignColorPreview(record, previous, fields) {
        const incoming = Boolean(fields) && Object.prototype.hasOwnProperty.call(fields, "color");
        if (incoming) {
            if (fields.color === null) {
                record.color = null;
                return;
            }

            const hex = explicitPreviewColor(fields.color);
            if (hex) {
                record.color = hex;
            }
            return;
        }

        if (previous && Object.prototype.hasOwnProperty.call(previous, "color")) {
            record.color = previous.color;
        }
    }

    /**
     * Copies one explicit preview value when the incoming fields or the previous preview contain that key.
     *
     * @param {object} record Preview being written.
     * @param {object} previous Previous preview.
     * @param {object} fields Incoming fields. A present key wins over previous.
     * @param {string} key Field name.
     * @param {function} explicitValue Converts the source value. Null means the key is not copied.
     * @returns {void} Nothing is returned.
     */
    function assignExplicitPreview(record, previous, fields, key, explicitValue) {
        const source = fields && Object.prototype.hasOwnProperty.call(fields, key)
            ? fields
            : previous;
        if (!source || !Object.prototype.hasOwnProperty.call(source, key)) {
            return;
        }

        const value = explicitValue(source[key]);
        if (value !== null) {
            record[key] = value;
        }
    }

    /**
     * Reads the in-memory profile preview for one member.
     *
     * @param {string} accountId Account key.
     * @param {string} memberId Member key.
     * @returns {object|null} The stored fields, or null when this account has no preview for the member.
     */
    function readMemberProfilePreview(accountId, memberId) {
        const accountKey = profilePreviewKey(accountId);
        const memberKey = profilePreviewKey(memberId);
        const accountPreviews = accountKey ? memberProfilePreviewsByAccount[accountKey] : null;
        const preview = accountPreviews && memberKey ? accountPreviews[memberKey] : null;

        if (!preview) {
            return null;
        }

        const record = {};
        if (Object.prototype.hasOwnProperty.call(preview, "displayName")) {
            record.displayName = preview.displayName;
        }
        if (Object.prototype.hasOwnProperty.call(preview, "pronouns")) {
            record.pronouns = preview.pronouns;
        }
        if (Object.prototype.hasOwnProperty.call(preview, "description")) {
            record.description = preview.description;
        }
        assignColorPreview(record, preview, null);
        assignExplicitPreview(record, preview, null, "fronting", explicitPreviewFronting);
        assignVisibilityPreview(record, preview, null);
        assignPermissionPreview(record, preview, null);
        assignExplicitPreview(record, preview, null, "avatarObjectUrl", explicitPreviewAvatar);
        return record;
    }

    /**
     * Stores a profile preview in memory while database writes are disabled.
     * When writes are enabled, it returns saved false and the not-connected status. A missing or changed account also returns saved false.
     *
     * @param {string} accountId Account that owns the preview.
     * @param {string} memberId Member being previewed.
     * @param {object} fields Name, pronouns, and description are stored from the fields. Color and the other explicit values are merged with the previous preview.
     * @returns {Promise<object>} Fulfills with saved true for a stored preview, or saved false. It does not reject.
     */
    async function saveMemberProfilePreview(accountId, memberId, fields) {
        if (!databaseWritesAreDisabled()) {
            return {
                saved: false,
                status: "Member profile saving is not connected to an anchor-specific endpoint yet."
            };
        }

        const accountKey = profilePreviewKey(accountId);
        const memberKey = profilePreviewKey(memberId);
        if (!accountKey || !memberKey || currentAccountId() !== accountKey) {
            return {
                saved: false,
                status: "The signed-in account is not available, so this member profile preview was not stored."
            };
        }

        await Promise.resolve();

        if (currentAccountId() !== accountKey) {
            return {
                saved: false,
                status: "The signed-in account changed, so this member profile preview was not stored."
            };
        }

        if (!memberProfilePreviewsByAccount[accountKey]) {
            memberProfilePreviewsByAccount[accountKey] = {};
        }

        const previous = memberProfilePreviewsByAccount[accountKey][memberKey] || {};
        const record = {
            displayName: fields && fields.displayName ? String(fields.displayName) : "",
            pronouns: fields && fields.pronouns ? String(fields.pronouns) : "",
            description: fields && fields.description ? String(fields.description) : ""
        };
        assignColorPreview(record, previous, fields);
        assignExplicitPreview(record, previous, fields, "fronting", explicitPreviewFronting);
        assignVisibilityPreview(record, previous, fields);
        assignPermissionPreview(record, previous, fields);
        assignExplicitPreview(record, previous, fields, "avatarObjectUrl", explicitPreviewAvatar);
        memberProfilePreviewsByAccount[accountKey][memberKey] = record;

        console.info("Suppressed member-profile database write. Profile details were held in browser memory for the signed-in account. No profile fields were written to the log.");

        return {
            saved: true,
            status: "Preview only: member profile held in browser memory. Nothing was saved to the database."
        };
    }

    /**
     * Removes the persisted text and color from a stored profile preview after a database save.
     *
     * @param {string} accountId Account key.
     * @param {string} memberId Member key. A missing preview does nothing.
     * @returns {void} The member entry is removed when no remaining key is defined.
     */
    function forgetPersistedProfilePreview(accountId, memberId) {
        const accountKey = profilePreviewKey(accountId);
        const memberKey = profilePreviewKey(memberId);
        const accountPreviews = accountKey ? memberProfilePreviewsByAccount[accountKey] : null;
        const stored = accountPreviews && memberKey ? accountPreviews[memberKey] : null;
        if (!stored) {
            return;
        }

        delete stored.displayName;
        delete stored.pronouns;
        delete stored.description;
        delete stored.color;

        /**
         * Reports whether a preview key is still defined.
         *
         * @param {string} key Stored preview key.
         * @returns {boolean} True when the value is not undefined.
         */
        const remaining = Object.keys(stored).some(function (key) {
            return stored[key] !== undefined;
        });
        if (!remaining) {
            delete accountPreviews[memberKey];
        }
    }

    /**
     * Saves one member profile for the selected anchor, or keeps a local preview when database writes are disabled.
     * The disabled path must not report the change as persisted. The branch is the selected occurrence chain the server uses for inherited Owner authority. A missing anchor type or id returns saved false without a request. API rejection is returned as saved false.
     * @param {string} accountId Account that owns the preview and the save.
     * @param {string} memberId Member to update.
     * @param {object} anchor Selected anchor. objectType must be system, group, or place, and objectId must be present. branch may be empty. The server rejects an empty or invalid branch.
     * @param {object} fields Profile fields. Name, pronouns, and description are always sent. Color, placement, and avatar are sent only when those keys are present.
     * @returns {Promise<object>} saved true and persisted true when the API returns a profile. Otherwise saved false and a status string. The disabled-write path returns the preview result.
     */
    async function saveMemberProfile(accountId, memberId, anchor, fields) {
        if (databaseWritesAreDisabled()) {
            return saveMemberProfilePreview(accountId, memberId, fields);
        }

        const objectType = anchor && anchor.objectType ? String(anchor.objectType) : "";
        const objectId = anchor && anchor.objectId ? String(anchor.objectId) : "";
        if (objectType !== "system" && objectType !== "group" && objectType !== "place") {
            return {
                saved: false,
                status: "The selected anchor cannot authorize this member profile save."
            };
        }

        if (!objectId) {
            return {
                saved: false,
                status: "The selected anchor has no id, so this member profile was not saved."
            };
        }

        const apiClient = window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;
        if (!apiClient || typeof apiClient.saveAnchorMemberProfile !== "function") {
            return {
                saved: false,
                status: "Member profile saving is not available in the current browser session."
            };
        }

        const body = {
            anchorObjectType: objectType,
            anchorObjectId: objectId,
            branch: anchor && anchor.branch ? String(anchor.branch) : "",
            displayName: fields ? fields.displayName : "",
            pronouns: fields ? fields.pronouns : "",
            description: fields ? fields.description : ""
        };
        // Color is included only when the card marked it. Sending a missing
        // color would clear a stored value the user did not edit.
        if (fields && Object.prototype.hasOwnProperty.call(fields, "color")) {
            body.color = fields.color;
        }
        if (fields && Object.prototype.hasOwnProperty.call(fields, "visibilityMode")) {
            body.visibilityMode = fields.visibilityMode;
            body.visibilityTypeId = Object.prototype.hasOwnProperty.call(fields, "visibilityTypeId")
                ? fields.visibilityTypeId
                : null;
        }
        if (fields && Object.prototype.hasOwnProperty.call(fields, "permissionMode")) {
            body.permissionMode = fields.permissionMode;
            body.permissionCode = Object.prototype.hasOwnProperty.call(fields, "permissionCode")
                ? fields.permissionCode
                : null;
        }
        if (fields && Object.prototype.hasOwnProperty.call(fields, "avatarContentType")) {
            body.avatarContentType = fields.avatarContentType;
            body.avatarImageBase64 = fields.avatarImageBase64;
        }

        try {
            const payload = await apiClient.saveAnchorMemberProfile(memberId, body);
            const profile = payload && payload.profile ? payload.profile : null;
            if (!profile) {
                return {
                    saved: false,
                    status: "The member profile save did not return the saved values."
                };
            }

            forgetPersistedProfilePreview(accountId, memberId);
            return {
                saved: true,
                persisted: true,
                profile: profile,
                status: "Member saved."
            };
        } catch (error) {
            return {
                saved: false,
                status: error && error.message
                    ? error.message
                    : "Member could not be saved."
            };
        }
    }

    /**
     * Saves Fronting immediately and independently of profile Save. The same anchor and branch authorize it.
     * Database writes disabled returns saved false and does not change the server. A missing anchor, missing API, or rejection also returns saved false. The card keeps the marked draft.
     * @param {string} accountId Accepted by the signature and unused by this function.
     * @param {string} memberId Member whose open interval changes.
     * @param {object} anchor Selected anchor. Type and id are required. branch is sent as an empty string when absent.
     * @param {boolean} fronting Requested state. The sent body always includes fronting as a boolean: strict true becomes true, and every other value becomes false.
     * @returns {Promise<object>} saved true and the boolean fronting value from the response, or saved false and a status string.
     */
    async function saveMemberFronting(accountId, memberId, anchor, fronting) {
        if (databaseWritesAreDisabled()) {
            return {
                saved: false,
                status: "Fronting was not saved because database writes are disabled."
            };
        }

        const objectType = anchor && anchor.objectType ? String(anchor.objectType) : "";
        const objectId = anchor && anchor.objectId ? String(anchor.objectId) : "";
        if ((objectType !== "system" && objectType !== "group" && objectType !== "place") || !objectId) {
            return {
                saved: false,
                status: "The selected anchor cannot authorize this fronting change."
            };
        }

        const apiClient = window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;
        if (!apiClient || typeof apiClient.saveMemberFronting !== "function") {
            return {
                saved: false,
                status: "Fronting could not be saved."
            };
        }

        try {
            const payload = await apiClient.saveMemberFronting(memberId, {
                anchorObjectType: objectType,
                anchorObjectId: objectId,
                branch: anchor && anchor.branch ? String(anchor.branch) : "",
                fronting: fronting === true
            });
            if (!payload || (payload.fronting !== true && payload.fronting !== false)) {
                return {
                    saved: false,
                    status: "Fronting could not be saved."
                };
            }

            return {
                saved: true,
                fronting: payload.fronting
            };
        } catch (error) {
            return {
                saved: false,
                status: error && error.message
                    ? error.message
                    : "Fronting could not be saved."
            };
        }
    }

    /**
     * Builds the legacy add-member form.
     *
     * @param {object} options cancelAddMember, submitMember, and refreshMembers callbacks. A missing submitMember uses apiClient.addMember.
     * @returns {HTMLElement} The form section.
     */
    function createMemberAddForm(options) {
        const section = document.createElement("section");
        section.className = "member-add-panel";
        section.setAttribute("aria-labelledby", "member-add-heading");

        const heading = document.createElement("h3");
        heading.id = "member-add-heading";
        heading.className = "me-section-heading";
        heading.textContent = "Add member";

        const form = document.createElement("form");
        form.className = "member-add-form";

        const displayNameField = createTextField("member-add-display-name", "Display name", true);
        const pronounsField = createTextField("member-add-pronouns", "Pronouns", false);
        const descriptionField = createDescriptionField("member-add-description", "Description");

        const buttonRow = document.createElement("div");
        buttonRow.className = "button-row member-form-button-row";

        const submitButton = createFormActionButton("add", "Add member", "submit");
        const cancelButton = createFormActionButton("cancel", "Cancel", "button");

        const status = createStatusMessage();

        buttonRow.appendChild(submitButton);
        buttonRow.appendChild(cancelButton);

        form.appendChild(displayNameField.row);
        form.appendChild(pronounsField.row);
        form.appendChild(descriptionField.row);
        form.appendChild(buttonRow);
        form.appendChild(status);

        /**
         * Clears the add form and calls cancelAddMember when that callback exists.
         *
         * @returns {void} Nothing is returned.
         */
        cancelButton.addEventListener("click", function () {
            form.reset();
            status.textContent = "";

            if (options && typeof options.cancelAddMember === "function") {
                options.cancelAddMember();
            }
        });

        /**
         * Submits the legacy add form.
         * A blank name sets the status and returns. submitMember is used when supplied. Otherwise apiClient.addMember runs and refreshMembers follows a successful add. A rejection sets the status and does not leave the form busy.
         *
         * @param {SubmitEvent} event Form submission. The browser submit is cancelled.
         * @returns {Promise<void>} The promise settles after the status is written. A failed add is caught.
         */
        form.addEventListener("submit", async function (event) {
            event.preventDefault();

            const request = buildMemberRequest(displayNameField.input, pronounsField.input, descriptionField.input);

            if (!request.displayName) {
                status.textContent = "Display name is required.";
                return;
            }

            if (options && typeof options.submitMember === "function") {
                setFormBusy(form, true);
                status.textContent = "";

                try {
                    const outcome = await options.submitMember(request);
                    if (outcome && outcome.reset) {
                        form.reset();
                    }
                    status.textContent = outcome && outcome.status ? outcome.status : "";
                } catch (error) {
                    status.textContent = "Member could not be added. Confirm the API is running and try again.";
                } finally {
                    setFormBusy(form, false);
                }

                return;
            }

            const apiClient = window.PluralBridge && window.PluralBridge.apiClient
                ? window.PluralBridge.apiClient
                : null;

            if (!apiClient || typeof apiClient.addMember !== "function") {
                status.textContent = "Member add is not available in the current browser session.";
                return;
            }

            setFormBusy(form, true);
            status.textContent = "Adding member.";

            let addSucceeded = false;

            try {
                await apiClient.addMember(request);
                addSucceeded = true;
                form.reset();
                status.textContent = "Member added. Refreshing members.";

                if (options && typeof options.refreshMembers === "function") {
                    await options.refreshMembers();
                }
            } catch (error) {
                if (addSucceeded) {
                    status.textContent = "Member was added, but the member list could not refresh. Click members to reload.";
                } else {
                    status.textContent = "Member could not be added. Confirm the API is running and try again.";
                }
            } finally {
                setFormBusy(form, false);
            }
        });

        section.appendChild(heading);
        section.appendChild(form);

        return section;
    }

    /**
     * Builds the legacy edit-member form and fills it from the member.
     *
     * @param {object} member Member being edited. A missing member id is reported on submit.
     * @param {object} options cancelEdit and refreshMembers callbacks.
     * @returns {HTMLElement} The form section.
     */
    function createMemberEditForm(member, options) {
        const section = document.createElement("section");
        section.className = "member-edit-panel";

        const memberId = getMemberId(member);

        const heading = document.createElement("h3");
        heading.className = "me-section-heading";
        heading.textContent = "Edit member";

        const form = document.createElement("form");
        form.className = "member-edit-form";

        const displayNameField = createTextField("member-edit-display-name-" + memberId, "Display name", true);
        const pronounsField = createTextField("member-edit-pronouns-" + memberId, "Pronouns", false);
        const descriptionField = createDescriptionField("member-edit-description-" + memberId, "Description");

        displayNameField.input.value = member && member.displayName ? member.displayName : "";
        pronounsField.input.value = member && member.pronouns ? member.pronouns : "";
        descriptionField.input.value = member && member.description ? member.description : "";

        const buttonRow = document.createElement("div");
        buttonRow.className = "button-row member-form-button-row";

        const saveButton = createFormActionButton("save", "Save", "submit");
        const cancelButton = createFormActionButton("cancel", "Cancel", "button");

        const status = createStatusMessage();

        buttonRow.appendChild(saveButton);
        buttonRow.appendChild(cancelButton);

        form.appendChild(displayNameField.row);
        form.appendChild(pronounsField.row);
        form.appendChild(descriptionField.row);
        form.appendChild(buttonRow);
        form.appendChild(status);

        /**
         * Calls cancelEdit when that callback exists.
         *
         * @returns {void} Nothing is returned.
         */
        cancelButton.addEventListener("click", function () {
            if (options && typeof options.cancelEdit === "function") {
                options.cancelEdit();
            }
        });

        /**
         * Saves the legacy edit through apiClient.editMember.
         * A missing member id, missing client, or blank name sets the status and returns. refreshMembers runs after a successful edit. A rejection sets the status.
         *
         * @param {SubmitEvent} event Form submission. The browser submit is cancelled.
         * @returns {Promise<void>} The promise settles after the status is written. A failed edit is caught.
         */
        form.addEventListener("submit", async function (event) {
            event.preventDefault();

            const apiClient = window.PluralBridge && window.PluralBridge.apiClient
                ? window.PluralBridge.apiClient
                : null;

            if (!memberId) {
                status.textContent = "Member edit is not available because this member has no member ID.";
                return;
            }

            if (!apiClient || typeof apiClient.editMember !== "function") {
                status.textContent = "Member edit is not available in the current browser session.";
                return;
            }

            const request = buildMemberRequest(displayNameField.input, pronounsField.input, descriptionField.input);

            if (!request.displayName) {
                status.textContent = "Display name is required.";
                return;
            }

            setFormBusy(form, true);
            status.textContent = "Saving member.";

            let editSucceeded = false;

            try {
                await apiClient.editMember(memberId, request);
                editSucceeded = true;
                status.textContent = "Member saved. Refreshing members.";

                if (options && typeof options.refreshMembers === "function") {
                    await options.refreshMembers();
                }
            } catch (error) {
                if (editSucceeded) {
                    status.textContent = "Member was saved, but the member list could not refresh. Click members to reload.";
                } else {
                    status.textContent = "Member could not be saved. Confirm the API is running and try again.";
                }
            } finally {
                setFormBusy(form, false);
            }
        });

        section.appendChild(heading);
        section.appendChild(form);

        return section;
    }

    let addMemberDialogEpoch = 0;
    let addMemberDialog = null;
    let addMemberDialogController = null;
    let addMemberDialogOpener = null;
    let addMemberDialogOwnedUrls = [];
    const addMemberDialogCommittedUrls = {};

    /**
     * Returns focus to the Add-menu summary, or to the opener when that summary is gone.
     *
     * @param {Element} opener Element that opened the dialog.
     * @returns {void} Nothing is returned.
     */
    function focusAddMemberOpener(opener) {
        const details = opener && typeof opener.closest === "function"
            ? opener.closest(".member-toolbar-add-menu")
            : null;
        const summary = details ? details.querySelector(".member-toolbar-add-trigger") : null;
        if (summary && summary.isConnected) {
            summary.focus();
            return;
        }

        if (opener && opener.isConnected) {
            opener.focus();
        }
    }

    /**
     * Revokes avatar object URLs created by the add dialog and not kept by a stored preview.
     *
     * @returns {void} The owned-url list is cleared.
     */
    function revokeUncommittedAvatarUrls() {
        /**
         * Revokes one uncommitted avatar URL.
         *
         * @param {string} url Object URL. A URL recorded as committed is kept.
         * @returns {void} Nothing is returned.
         */
        addMemberDialogOwnedUrls.forEach(function (url) {
            if (!addMemberDialogCommittedUrls[url]) {
                URL.revokeObjectURL(url);
            }
        });
        addMemberDialogOwnedUrls = [];
    }

    /**
     * Aborts the dialog listeners, revokes uncommitted avatar URLs, and removes the dialog.
     *
     * @returns {void} Nothing is returned.
     */
    function destroyAddMemberDialog() {
        if (addMemberDialogController) {
            addMemberDialogController.abort();
            addMemberDialogController = null;
        }

        revokeUncommittedAvatarUrls();

        const dialog = addMemberDialog;
        addMemberDialog = null;
        if (!dialog) {
            return;
        }

        if (dialog.open) {
            dialog.close();
        }
        dialog.remove();
    }

    /**
     * Closes the add dialog and returns focus to its opener.
     *
     * @returns {void} The dialog epoch advances so a queued open from the previous dialog does not continue.
     */
    function closeAddMemberDialog() {
        addMemberDialogEpoch += 1;
        const opener = addMemberDialogOpener;
        addMemberDialogOpener = null;
        destroyAddMemberDialog();
        focusAddMemberOpener(opener);
    }

    /**
     * Reads the navigator map snapshot.
     *
     * @returns {object} The snapshot, or an empty rows list when the navigator cannot supply one.
     */
    function readMapSnapshot() {
        const navigatorView = window.PluralBridge && window.PluralBridge.navigator
            ? window.PluralBridge.navigator
            : null;
        if (!navigatorView || typeof navigatorView.getMapSnapshot !== "function") {
            return {
                accountId: "",
                selectedPathKey: "",
                rows: []
            };
        }

        return navigatorView.getMapSnapshot();
    }

    /**
     * Reports whether a row can contain a new member.
     *
     * @param {object} row Map row. Headers are excluded. system, group, and place are destinations.
     * @returns {boolean} True for a destination anchor.
     */
    function isDestinationAnchor(row) {
        return Boolean(
            row &&
            row.header !== true &&
            (row.objectType === "system" || row.objectType === "group" || row.objectType === "place")
        );
    }

    /**
     * Walks from the selected row to the nearest destination anchor.
     *
     * @param {object} snapshot Map snapshot with rows and selectedPathKey.
     * @returns {object|null} The anchor row, or null when the walk does not find one.
     */
    function containingAnchor(snapshot) {
        const byKey = {};
        /**
         * Indexes one snapshot row by its path key.
         *
         * @param {object} row Map row. A row without pathKey is skipped.
         * @returns {void} Nothing is returned.
         */
        snapshot.rows.forEach(function (row) {
            if (row.pathKey) {
                byKey[row.pathKey] = row;
            }
        });

        let row = snapshot.selectedPathKey ? byKey[snapshot.selectedPathKey] : null;
        const seen = {};
        while (row && row.pathKey && !seen[row.pathKey]) {
            seen[row.pathKey] = true;
            if (isDestinationAnchor(row)) {
                return row;
            }
            row = row.parentPathKey ? byKey[row.parentPathKey] : null;
        }

        return null;
    }

    /**
     * Labels a destination type.
     *
     * @param {string} objectType system, group, place, or another type, which returns an empty string.
     * @returns {string} The label.
     */
    function destinationTypeLabel(objectType) {
        if (objectType === "system") {
            return "System";
        }
        if (objectType === "group") {
            return "Group";
        }
        if (objectType === "place") {
            return "Place";
        }
        return "";
    }

    /**
     * Reads the visible name of a destination row.
     *
     * @param {object} row Map row. anchorName wins over nodeLabel.
     * @returns {string} The name, or an empty string.
     */
    function destinationDisplayName(row) {
        if (row && row.anchorName) {
            return row.anchorName;
        }
        return row && row.nodeLabel ? row.nodeLabel : "";
    }

    /**
     * Draws a small dialog SVG from one path.
     *
     * @param {string} pathData SVG path data.
     * @returns {SVGElement} The icon.
     */
    function dialogSvg(pathData) {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");
        const path = document.createElementNS(svgNamespace, "path");
        path.setAttribute("fill", "currentColor");
        path.setAttribute("d", pathData);
        svg.appendChild(path);
        return svg;
    }

    /**
     * Draws the outline icon for a dialog object type.
     *
     * @param {string} objectType account, system, group, place, member, relationship, or another type, which draws the unset mark.
     * @returns {SVGElement} The icon.
     */
    function dialogOutlineIcon(objectType) {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-tree-node-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "20");
        svg.setAttribute("height", "20");
        svg.setAttribute("fill", "none");
        svg.setAttribute("stroke", "currentColor");
        svg.setAttribute("stroke-width", "2.5");
        svg.setAttribute("stroke-linecap", "round");
        svg.setAttribute("stroke-linejoin", "round");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        /**
         * Appends one SVG shape to the dialog icon being drawn.
         *
         * @param {string} tagName SVG element name.
         * @param {object} attributes Attribute names and values.
         * @returns {void} Nothing is returned.
         */
        function shape(tagName, attributes) {
            const node = document.createElementNS(svgNamespace, tagName);
            /**
             * Copies one attribute onto that shape.
             *
             * @param {string} name Attribute name.
             * @returns {void} Nothing is returned.
             */
            Object.keys(attributes).forEach(function (name) {
                node.setAttribute(name, attributes[name]);
            });
            svg.appendChild(node);
        }

        if (objectType === "system") {
            shape("rect", { x: "16", y: "16", width: "6", height: "6", rx: "1" });
            shape("rect", { x: "2", y: "16", width: "6", height: "6", rx: "1" });
            shape("rect", { x: "9", y: "2", width: "6", height: "6", rx: "1" });
            shape("path", { d: "M5 16v-3a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v3" });
            shape("path", { d: "M12 12V8" });
        } else if (objectType === "group") {
            shape("path", { d: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" });
            shape("circle", { cx: "9", cy: "7", r: "4" });
            shape("path", { d: "M22 21v-2a4 4 0 0 0-3-3.87" });
            shape("path", { d: "M16 3.13a4 4 0 0 1 0 7.75" });
        } else if (objectType === "place") {
            shape("path", { d: "M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8" });
            shape("path", { d: "M3 10a2 2 0 0 1 .709-1.528l7-5.999a2 2 0 0 1 2.582 0l7 5.999A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" });
        } else if (objectType === "member") {
            shape("path", { d: "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" });
            shape("circle", { cx: "12", cy: "7", r: "4" });
        } else if (objectType === "account") {
            shape("circle", { cx: "12", cy: "8", r: "3" });
            shape("path", { d: "M5 19a7 7 0 0 1 14 0" });
        } else if (objectType === "relationship") {
            shape("path", { d: "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" });
            shape("path", { d: "M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" });
        } else {
            return null;
        }

        return svg;
    }

    /**
     * Draws an expanded or collapsed tree chevron.
     *
     * @param {boolean} expanded True adds the expanded class.
     * @returns {SVGElement} The chevron.
     */
    function dialogChevron(expanded) {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-tree-chevron" + (expanded ? " is-expanded" : ""));
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
     * Draws the shared edit pencil.
     *
     * @returns {SVGElement} The pencil icon.
     */
    function createEditPencil() {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-edit-pencil-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "16");
        svg.setAttribute("height", "16");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        /**
         * Appends one SVG shape to the pencil.
         *
         * @param {string} tagName SVG element name.
         * @param {object} attributes Attribute names and values.
         * @returns {void} Nothing is returned.
         */
        function addShape(tagName, attributes) {
            const node = document.createElementNS(svgNamespace, tagName);
            /**
             * Copies one attribute onto the pencil shape.
             *
             * @param {string} name Attribute name.
             * @returns {void} Nothing is returned.
             */
            Object.keys(attributes).forEach(function (name) {
                node.setAttribute(name, attributes[name]);
            });
            svg.appendChild(node);
        }

        addShape("path", {
            d: "M3.1 18.2 6.8 21.2 5.2 22.4 2.2 20.2Z",
            fill: "#F3D7A1",
            stroke: "#8A6A12",
            "stroke-width": "0.6",
            "stroke-linejoin": "round"
        });
        addShape("path", {
            d: "M2.4 20.4 3.5 22.2 4.8 21.4 3.6 19.6Z",
            fill: "#5C4A32"
        });
        addShape("path", {
            d: "M6.2 17.6 18.4 5.4 15.6 2.6 3.4 14.8Z",
            fill: "#F6C445",
            stroke: "#8A6A12",
            "stroke-width": "0.8",
            "stroke-linejoin": "round"
        });
        addShape("path", {
            d: "M15.5 2.7 18.3 5.5 19.6 4.2 16.8 1.4Z",
            fill: "#E4E4E4",
            stroke: "#6E6E6E",
            "stroke-width": "0.45",
            "stroke-linejoin": "round"
        });
        addShape("path", {
            d: "M16.9 1.5 21.2 5.8 19.7 7.3 15.4 3Z",
            fill: "#F48FB1",
            stroke: "#9D4E6C",
            "stroke-width": "0.55",
            "stroke-linejoin": "round"
        });
        return svg;
    }

    /**
     * Draws the pencil used inside the add dialog.
     *
     * @returns {SVGElement} The pencil icon.
     */
    function dialogPencil() {
        const icon = createEditPencil();
        icon.classList.add("member-add-dialog-pencil-icon");
        return icon;
    }

    /**
     * Draws the relationship arrow.
     *
     * @returns {SVGElement} The arrow icon.
     */
    function relationshipArrow() {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-relationship-arrow-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "40");
        svg.setAttribute("height", "40");
        svg.setAttribute("fill", "none");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");
        const path = document.createElementNS(svgNamespace, "path");
        path.setAttribute("d", "M4 12h14M14 6l6 6-6 6");
        path.setAttribute("fill", "none");
        path.setAttribute("stroke", "currentColor");
        path.setAttribute("stroke-width", "1.75");
        path.setAttribute("stroke-linecap", "round");
        path.setAttribute("stroke-linejoin", "round");
        svg.appendChild(path);
        return svg;
    }

    /**
     * Draws the Fronting icon for the add dialog.
     *
     * @param {boolean} isFronting True draws the upward arrow. False draws the downward arrow.
     * @returns {SVGElement} The icon.
     */
    function dialogFrontingIcon(isFronting) {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-add-dialog-fronting-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "20");
        svg.setAttribute("height", "20");
        svg.setAttribute("fill", "none");
        svg.setAttribute("stroke", "currentColor");
        svg.setAttribute("stroke-width", "1.8");
        svg.setAttribute("stroke-linecap", "round");
        svg.setAttribute("stroke-linejoin", "round");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");
        const person = document.createElementNS(svgNamespace, "path");
        person.setAttribute("d", "M9 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM4.5 19v-1.2A3.8 3.8 0 0 1 8.3 14h1.4a3.8 3.8 0 0 1 3.8 3.8V19");
        const arrow = document.createElementNS(svgNamespace, "path");
        arrow.setAttribute("d", isFronting ? "M17 19V8M14 11l3-3 3 3" : "M17 5v11M14 13l3 3 3-3");
        svg.appendChild(person);
        svg.appendChild(arrow);
        return svg;
    }

    const supportedRelationshipTypes = [
        { id: "parent", label: "Parent" },
        { id: "sibling", label: "Sibling" },
        { id: "related", label: "Related" },
        { id: "associated", label: "Associated" },
        { id: "subsystem", label: "Subsystem" },
        { id: "evolving", label: "Evolving" }
    ];
    const customRelationshipTypesByAccount = {};

    /**
     * Builds an id for a custom relationship label.
     *
     * @param {string} label Typed label. Non-letters become hyphens, and a random suffix keeps repeated labels distinct.
     * @returns {string} The custom id.
     */
    function customRelationshipId(label) {
        const slug = String(label || "").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "type";
        return "custom:" + slug + ":" + Math.random().toString(36).slice(2, 8);
    }

    /**
     * Lists the built-in relationship types and the custom types stored for one account.
     *
     * @param {string} accountId Account whose custom types are appended. A missing account adds none.
     * @returns {Array<object>} Choice objects with id, label, icon, disabled, and custom.
     */
    function relationshipTypeChoices(accountId) {
        /**
         * Copies one built-in relationship type into a choice.
         *
         * @param {object} item Supported type with id and label.
         * @returns {object} The choice.
         */
        const choices = supportedRelationshipTypes.map(function (item) {
            return { id: item.id, label: item.label, icon: "link", disabled: false, custom: false };
        });
        const custom = accountId && customRelationshipTypesByAccount[accountId]
            ? customRelationshipTypesByAccount[accountId]
            : [];
        /**
         * Appends one custom relationship type.
         *
         * @param {object} item Custom type with id and label.
         * @returns {void} Nothing is returned.
         */
        custom.forEach(function (item) {
            choices.push({
                id: item.id,
                label: item.label,
                icon: "link",
                disabled: false,
                custom: true
            });
        });
        return choices;
    }

    /**
     * Shows or hides an inheritance hint.
     *
     * @param {HTMLElement} hint Hint element.
     * @param {boolean} show True only shows the hint when sourceName is also present.
     * @param {string} sourceName Name of the source the value inherits from.
     * @returns {void} Nothing is returned.
     */
    function paintInheritanceHint(hint, show, sourceName) {
        hint.textContent = sourceName ? "Inherited from " + sourceName : "\u00a0";
        const visible = Boolean(show && sourceName);
        hint.classList.toggle("is-quiet", !visible);
        hint.setAttribute("aria-hidden", visible ? "false" : "true");
    }

    /**
     * Builds visibility or permission choices.
     *
     * @param {string} kind permission returns the permission list. Any other kind returns the visibility list.
     * @param {boolean} includeUnset True adds Not set as the first choice.
     * @returns {Array<object>} The choices. Selected people is present and disabled.
     */
    function policyChoices(kind, includeUnset) {
        const choices = [];
        if (includeUnset) {
            choices.push({ id: "", label: "Not set", icon: "unset", disabled: false });
        }
        if (kind === "permission") {
            choices.push(
                { id: "inherit", label: "Inherit", icon: "inherit", disabled: false },
                { id: "viewer", label: "Viewer", icon: "viewer", disabled: false },
                { id: "editor", label: "Editor", icon: "editor", disabled: false },
                { id: "manager", label: "Manager", icon: "manager", disabled: false },
                { id: "owner", label: "Owner", icon: "owner", disabled: false }
            );
            return choices;
        }

        choices.push(
            { id: "inherit", label: "Inherit", icon: "inherit", disabled: false },
            { id: "1", label: "Only you", icon: "only-you", disabled: false },
            { id: "2", label: "Trusted friends", icon: "trusted", disabled: false },
            { id: "3", label: "All friends", icon: "all-friends", disabled: false },
            { id: "4", label: "Selected people", icon: "trusted", disabled: true }
        );
        return choices;
    }

    /**
     * Draws a policy or relationship icon.
     *
     * @param {string} kind inherit, viewer, editor, manager, owner, only-you, trusted, all-friends, link, or any other kind, which draws the unset mark.
     * @returns {SVGElement} The icon.
     */
    function policyOutlineIcon(kind) {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-sharing-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "20");
        svg.setAttribute("height", "20");
        svg.setAttribute("fill", "none");
        svg.setAttribute("stroke", "currentColor");
        svg.setAttribute("stroke-width", "1.8");
        svg.setAttribute("stroke-linecap", "round");
        svg.setAttribute("stroke-linejoin", "round");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        /**
         * Appends one SVG shape to the policy icon.
         *
         * @param {string} tagName SVG element name.
         * @param {object} attributes Attribute names and values.
         * @returns {void} Nothing is returned.
         */
        function addShape(tagName, attributes) {
            const node = document.createElementNS(svgNamespace, tagName);
            /**
             * Copies one attribute onto the policy shape.
             *
             * @param {string} name Attribute name.
             * @returns {void} Nothing is returned.
             */
            Object.keys(attributes).forEach(function (name) {
                node.setAttribute(name, attributes[name]);
            });
            svg.appendChild(node);
        }

        if (kind === "inherit") {
            addShape("path", { d: "M7 7h8a4 4 0 0 1 0 8H9" });
            addShape("path", { d: "M11 13 8 16l3 3" });
        } else if (kind === "viewer") {
            addShape("path", { d: "M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12z" });
            addShape("circle", { cx: "12", cy: "12", r: "3" });
        } else if (kind === "editor") {
            addShape("path", { d: "M4 20h4l10.5-10.5a2.1 2.1 0 0 0-3-3L5 17v3z" });
            addShape("path", { d: "M13.5 6.5l3 3" });
        } else if (kind === "manager") {
            addShape("path", { d: "M12 3 5 6v5c0 4.2 2.8 7.2 7 8.5 4.2-1.3 7-4.3 7-8.5V6l-7-3z" });
            addShape("path", { d: "M9 12l2 2 4-4" });
        } else if (kind === "owner") {
            addShape("path", { d: "M12 3.5 14.2 8l4.8.7-3.5 3.4.8 4.9L12 14.8 7.7 17l.8-4.9L5 8.7 9.8 8 12 3.5z" });
        } else if (kind === "only-you") {
            addShape("path", { d: "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" });
            addShape("circle", { cx: "12", cy: "7", r: "4" });
        } else if (kind === "trusted") {
            addShape("path", { d: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" });
            addShape("circle", { cx: "9", cy: "7", r: "4" });
            addShape("path", { d: "M22 21v-2a4 4 0 0 0-3-3.87" });
            addShape("path", { d: "M16 3.13a4 4 0 0 1 0 7.75" });
        } else if (kind === "all-friends") {
            addShape("path", { d: "M8.59 13.51 15.42 17.49" });
            addShape("path", { d: "M15.41 6.51 8.59 10.49" });
            addShape("circle", { cx: "18", cy: "5", r: "3" });
            addShape("circle", { cx: "6", cy: "12", r: "3" });
            addShape("circle", { cx: "18", cy: "19", r: "3" });
        } else if (kind === "link") {
            addShape("path", { d: "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" });
            addShape("path", { d: "M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" });
        } else {
            addShape("circle", { cx: "12", cy: "12", r: "9" });
            addShape("path", { d: "M8 12h8" });
        }

        return svg;
    }

    /**
     * Chooses an icon kind from a choice's icon or id.
     *
     * @param {object} choice Choice with an optional icon and id.
     * @returns {string} The icon kind. An unrecognized id returns unset.
     */
    function policyIconKind(choice) {
        if (choice && choice.icon) {
            return choice.icon;
        }
        const id = choice && choice.id ? String(choice.id) : "";
        if (id === "1") {
            return "only-you";
        }
        if (id === "2") {
            return "trusted";
        }
        if (id === "3") {
            return "all-friends";
        }
        if (id === "4") {
            return "trusted";
        }
        if (
            id === "parent" ||
            id === "sibling" ||
            id === "related" ||
            id === "associated" ||
            id === "subsystem" ||
            id === "evolving" ||
            id.indexOf("custom:") === 0
        ) {
            return "link";
        }
        return "unset";
    }

    /**
     * Draws the policy-menu chevron.
     *
     * @param {boolean} open True adds the open class.
     * @returns {HTMLSpanElement} The chevron.
     */
    function policyChevron(open) {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const chevron = document.createElement("span");
        chevron.className = "member-sharing-chevron" + (open ? " is-open" : "");
        chevron.setAttribute("aria-hidden", "true");
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "16");
        svg.setAttribute("height", "16");
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
        chevron.appendChild(svg);
        return chevron;
    }

    /**
     * Builds a policy dropdown bound to getValue and setValue.
     *
     * @param {object} options label, choices, getValue, and setValue. A missing choices list is empty.
     * @returns {object} root and refresh. refresh repaints the button from getValue.
     */
    function createPolicyDropdown(options) {
        const choices = options && options.choices ? options.choices : [];
        const root = document.createElement("div");
        root.className = "member-policy-control";
        const button = document.createElement("button");
        button.type = "button";
        button.className = "member-policy-button";
        button.setAttribute("aria-haspopup", "listbox");
        button.setAttribute("aria-expanded", "false");
        const value = document.createElement("span");
        value.className = "member-policy-value";
        const list = document.createElement("div");
        list.className = "member-policy-list member-sharing-list";
        list.setAttribute("role", "listbox");
        list.hidden = true;
        const listId = "member-policy-" + Math.random().toString(36).slice(2);
        list.id = listId;
        button.setAttribute("aria-controls", listId);
        let optionsEls = [];

        /**
         * Finds the choice matching the current value.
         *
         * @returns {object} The matching choice, or the first choice, or a Not set fallback when the list is empty.
         */
        function currentChoice() {
            const current = options && typeof options.getValue === "function" ? String(options.getValue() || "") : "";
            for (let index = 0; index < choices.length; index += 1) {
                if (choices[index].id === current) {
                    return choices[index];
                }
            }
            return choices[0] || { id: "", label: "Not set", disabled: false };
        }

        /**
         * Repaints the policy button and rebuilds its option list.
         *
         * @returns {void} Nothing is returned.
         */
        function paint() {
            const choice = currentChoice();
            value.textContent = choice.label;
            const accessible = (options && options.label ? options.label + ": " : "") + choice.label;
            button.setAttribute("aria-label", accessible);
            button.title = accessible;
            list.replaceChildren();
            /**
             * Builds one policy option.
             *
             * @param {object} item Choice id, label, and disabled.
             * @param {number} index Option index.
             * @returns {HTMLDivElement} The option.
             */
            optionsEls = choices.map(function (item, index) {
                const option = document.createElement("div");
                option.className = "member-sharing-option";
                option.setAttribute("role", "option");
                option.tabIndex = -1;
                option.id = listId + "-option-" + index;
                option.dataset.policyId = item.id;
                if (item.disabled) {
                    option.setAttribute("aria-disabled", "true");
                }
                option.appendChild(policyOutlineIcon(policyIconKind(item)));
                const optionLabel = document.createElement("span");
                optionLabel.textContent = item.label;
                option.appendChild(optionLabel);
                if (item.custom) {
                    const badge = document.createElement("span");
                    badge.className = "member-policy-custom";
                    badge.textContent = "Custom";
                    option.appendChild(badge);
                }
                const selected = item.id === choice.id;
                option.setAttribute("aria-selected", selected ? "true" : "false");
                option.classList.toggle("is-current", selected);
                list.appendChild(option);
                return option;
            });
            button.replaceChildren(policyOutlineIcon(policyIconKind(choice)), value, policyChevron(!list.hidden));
        }

        /**
         * Places the open policy list under the button, or above it when there is no room.
         *
         * @returns {void} Nothing is returned.
         */
        function place() {
            const rect = button.getBoundingClientRect();
            const height = list.offsetHeight;
            let top = rect.bottom + 4;
            if (top + height > window.innerHeight - 8) {
                top = Math.max(8, rect.top - height - 4);
            }
            list.style.left = Math.max(8, rect.left) + "px";
            list.style.top = top + "px";
            list.style.width = Math.max(rect.width, 180) + "px";
        }

        /**
         * Opens or closes the policy list.
         *
         * @param {boolean} open True opens the list and focuses the selected option.
         * @returns {void} Nothing is returned.
         */
        function setOpen(open) {
            list.hidden = !open;
            button.setAttribute("aria-expanded", open ? "true" : "false");
            paint();
            if (!open) {
                return;
            }
            /**
             * Finds the option that should receive focus.
             *
             * @param {HTMLElement} option Option element.
             * @returns {boolean} True when the option is selected.
             */
            const selected = optionsEls.findIndex(function (option) {
                return option.getAttribute("aria-selected") === "true";
            });
            const index = selected < 0 ? 0 : selected;
            optionsEls[index].focus();
            place();
        }

        /**
         * Stores the chosen policy value and closes the list.
         *
         * @param {HTMLElement} option Chosen option. A missing or disabled option only closes the list.
         * @returns {void} setValue receives the option id.
         */
        function choose(option) {
            if (!option || option.getAttribute("aria-disabled") === "true") {
                setOpen(false);
                button.focus();
                return;
            }
            if (options && typeof options.setValue === "function") {
                options.setValue(option.dataset.policyId || "");
            }
            setOpen(false);
            button.focus();
        }

        /**
         * Stops a policy event from reaching the surrounding dialog or card.
         *
         * @param {Event} event Event to cancel and stop.
         * @returns {void} Nothing is returned.
         */
        function keepLocal(event) {
            event.preventDefault();
            event.stopPropagation();
        }
        /**
         * Stops a policy pointerdown from reaching the surrounding control.
         *
         * @param {MouseEvent} event Pointerdown on the policy root.
         * @returns {void} Nothing is returned.
         */
        root.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        /**
         * Stops a policy click from reaching the surrounding control.
         *
         * @param {MouseEvent} event Click on the policy root.
         * @returns {void} Nothing is returned.
         */
        root.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        /**
         * Toggles the policy list.
         *
         * @param {MouseEvent} event Click on the policy button.
         * @returns {void} Nothing is returned.
         */
        button.addEventListener("click", function (event) {
            keepLocal(event);
            setOpen(list.hidden);
        });
        /**
         * Opens the policy list from ArrowDown, Enter, or Space.
         *
         * @param {KeyboardEvent} event Keydown on the policy button.
         * @returns {void} Nothing is returned.
         */
        button.addEventListener("keydown", function (event) {
            if (event.key === "ArrowDown" || event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                event.stopPropagation();
                setOpen(true);
            }
        });
        /**
         * Keeps a list pointerdown from moving focus before the click chooses an option.
         *
         * @param {MouseEvent} event Pointerdown in the list.
         * @returns {void} Nothing is returned.
         */
        list.addEventListener("mousedown", function (event) {
            event.preventDefault();
            event.stopPropagation();
        });
        /**
         * Moves, chooses, or closes the policy list from the keyboard.
         *
         * @param {KeyboardEvent} event Arrow keys, Home, End, Enter, Space, and Escape.
         * @returns {void} Nothing is returned.
         */
        list.addEventListener("keydown", function (event) {
            event.stopPropagation();
            /**
             * Finds the index of the focused policy option.
             *
             * @param {HTMLElement} option Option element.
             * @returns {boolean} True when the option is the active element.
             */
            const current = optionsEls.findIndex(function (option) {
                return option === document.activeElement;
            });
            if (event.key === "Escape") {
                event.preventDefault();
                setOpen(false);
                button.focus();
                return;
            }
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                choose(optionsEls[current]);
                return;
            }
            if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                event.preventDefault();
                const step = event.key === "ArrowDown" ? 1 : -1;
                let index = current < 0 ? 0 : current + step;
                index = Math.max(0, Math.min(optionsEls.length - 1, index));
                optionsEls[index].focus();
            }
        });
        /**
         * Chooses the policy option under the pointer.
         *
         * @param {MouseEvent} event Click in the list.
         * @returns {void} Nothing is returned.
         */
        list.addEventListener("click", function (event) {
            const option = event.target.closest(".member-sharing-option");
            event.preventDefault();
            event.stopPropagation();
            if (option) {
                choose(option);
            }
        });
        /**
         * Closes the policy list on a pointer outside it, and removes itself after the control leaves the document.
         *
         * @param {PointerEvent} event Document pointerdown.
         * @returns {void} Nothing is returned.
         */
        function onPointerDown(event) {
            if (!root.isConnected) {
                document.removeEventListener("pointerdown", onPointerDown, true);
                return;
            }
            if (root.contains(event.target) || list.contains(event.target)) {
                return;
            }
            if (!list.hidden) {
                setOpen(false);
            }
        }
        document.addEventListener("pointerdown", onPointerDown, true);

        root.appendChild(button);
        root.appendChild(list);
        paint();
        return {
            root: root,
            refresh: paint
        };
    }

    /**
     * Builds the This account row used as a standalone destination.
     *
     * @param {string} accountId Account id. A missing id returns null.
     * @returns {object|null} The synthetic account row.
     */
    function accountDestinationRow(accountId) {
        if (!accountId) {
            return null;
        }

        return {
            pathKey: "account:" + accountId,
            parentPathKey: null,
            objectType: "account",
            objectId: accountId,
            anchorName: "This account",
            nodeLabel: "This account",
            header: false,
            accountDestination: true
        };
    }

    /**
     * Reports whether a row can be chosen for this add mode.
     *
     * @param {object} row Map row. Headers cannot be chosen.
     * @param {string} mode group, system, and place can choose the account row. Every mode can choose a system, group, or place.
     * @returns {boolean} True when the row is a valid destination.
     */
    function isModeDestination(row, mode) {
        if (!row || row.header === true) {
            return false;
        }
        if (row.objectType === "account") {
            return mode === "group" || mode === "system" || mode === "place";
        }
        return row.objectType === "system" || row.objectType === "group" || row.objectType === "place";
    }

    /**
     * Groups map rows under their parent path and opens structural branches.
     *
     * @param {Array<object>} rows Map rows. Rows without a parent path are roots.
     * @returns {object} children, structureOpen, and membersOpen. Member lists start closed.
     */
    function createTreeExpansion(rows) {
        const children = {};
        /**
         * Adds one child row to its parent list.
         *
         * @param {object} row Map row. A row without parentPathKey is skipped.
         * @returns {void} Nothing is returned.
         */
        rows.forEach(function (row) {
            if (!row.parentPathKey) {
                return;
            }
            if (!children[row.parentPathKey]) {
                children[row.parentPathKey] = [];
            }
            children[row.parentPathKey].push(row);
        });
        const structureOpen = {};
        const membersOpen = {};
        /**
         * Opens a parent that has a non-member child and leaves its member list closed.
         *
         * @param {string} key Parent path key.
         * @returns {void} Nothing is returned.
         */
        Object.keys(children).forEach(function (key) {
            const list = children[key];
            /**
             * Reports whether a child list contains a structural row.
             *
             * @param {object} row Child row.
             * @returns {boolean} True when the row is not a member.
             */
            const hasStructure = list.some(function (row) {
                return row.objectType !== "member";
            });
            if (hasStructure) {
                structureOpen[key] = true;
            }
            membersOpen[key] = false;
        });
        return {
            children: children,
            structureOpen: structureOpen,
            membersOpen: membersOpen
        };
    }

    /**
     * Reads the children stored for one path.
     *
     * @param {object} expansion Expansion from createTreeExpansion.
     * @param {string} pathKey Parent path. A missing key returns an empty list.
     * @returns {Array<object>} The child rows.
     */
    function treeChildren(expansion, pathKey) {
        return pathKey && expansion.children[pathKey] ? expansion.children[pathKey] : [];
    }

    /**
     * Reports whether every structural branch and member list is open.
     *
     * @param {object} expansion Tree expansion state.
     * @returns {boolean} True when nothing that can open is still closed.
     */
    function treeFullyOpen(expansion) {
        const keys = Object.keys(expansion.children);
        for (let index = 0; index < keys.length; index += 1) {
            const key = keys[index];
            const list = expansion.children[key];
            /**
             * Reports whether a child list contains a structural row.
             *
             * @param {object} row Child row.
             * @returns {boolean} True when the row is not a member.
             */
            const hasStructure = list.some(function (row) {
                return row.objectType !== "member";
            });
            /**
             * Reports whether a child list contains a member.
             *
             * @param {object} row Child row.
             * @returns {boolean} True when the row is a member.
             */
            const hasMembers = list.some(function (row) {
                return row.objectType === "member";
            });
            if ((hasStructure || hasMembers) && expansion.structureOpen[key] !== true) {
                return false;
            }
            if (hasStructure && hasMembers && expansion.membersOpen[key] !== true) {
                return false;
            }
        }
        return true;
    }

    /**
     * Opens or closes every structural branch and member list.
     *
     * @param {object} expansion Tree expansion state.
     * @param {boolean} open True opens both kinds of list.
     * @returns {void} Nothing is returned.
     */
    function setTreeFullyOpen(expansion, open) {
        /**
         * Sets one parent branch and its member list.
         *
         * @param {string} key Parent path key.
         * @returns {void} Nothing is returned.
         */
        Object.keys(expansion.children).forEach(function (key) {
            expansion.structureOpen[key] = open;
            expansion.membersOpen[key] = open;
        });
    }

    /**
     * Redraws a dialog tree and restores its scroll and focused row.
     *
     * @param {HTMLElement} host Tree container.
     * @param {Array<object>} rows Map rows.
     * @param {object} expansion Open and closed state.
     * @param {object} options leadingRows, selectedPathKey, canSelect, onSelect, onChange, memberTitle, and emptyText.
     * @returns {void} Nothing is returned.
     */
    function renderDialogTree(host, rows, expansion, options) {
        const scrollTop = host.scrollTop;
        const active = document.activeElement;
        const restorePath = active && host.contains(active) ? active.getAttribute("data-path-key") : "";
        const restoreToggle = Boolean(active && active.classList.contains("member-system-tree-toggle"));
        host.replaceChildren();
        const canSelect = options && typeof options.canSelect === "function"
            ? options.canSelect
            /**
             * Default selection rule used when the tree options do not supply canSelect.
             *
             * @returns {boolean} Always true.
             */
            : function () { return true; };
        const selectedPathKey = options && options.selectedPathKey ? options.selectedPathKey : "";

        /**
         * Tells the dialog that the tree changed.
         *
         * @returns {void} A missing onChange does nothing.
         */
        function notify() {
            if (options && typeof options.onChange === "function") {
                options.onChange();
            }
        }

        /**
         * Draws the member-list toggle under a structural row.
         *
         * @param {object} parent Row that owns the members.
         * @param {Array<object>} members Member rows shown when the list is open.
         * @param {number} level Indent level.
         * @returns {void} Nothing is returned.
         */
        function appendMembersToggle(parent, members, level) {
            const open = expansion.membersOpen[parent.pathKey] === true;
            const nodeRow = document.createElement("div");
            nodeRow.className = "member-system-node-row";
            nodeRow.style.setProperty("--system-node-level", String(level));
            const toggle = document.createElement("button");
            toggle.type = "button";
            toggle.className = "member-system-tree-toggle member-member-list-toggle";
            toggle.setAttribute("data-path-key", "members:" + parent.pathKey);
            toggle.setAttribute("aria-expanded", String(open));
            toggle.setAttribute("aria-label", open ? "Collapse members" : "Expand members");
            toggle.appendChild(dialogChevron(open));
            const text = document.createElement("span");
            text.textContent = "Members";
            toggle.appendChild(text);
            /**
             * Toggles this member list and notifies the dialog.
             *
             * @returns {void} Nothing is returned.
             */
            toggle.addEventListener("click", function () {
                expansion.membersOpen[parent.pathKey] = !open;
                notify();
            });
            nodeRow.appendChild(toggle);
            host.appendChild(nodeRow);
            if (open) {
                /**
                 * Draws one member under the open member list.
                 *
                 * @param {object} child Member row.
                 * @returns {void} Nothing is returned.
                 */
                members.forEach(function (child) {
                    appendRow(child, level + 1);
                });
            }
        }

        /**
         * Draws one tree row, its children, and its selection button.
         *
         * @param {object} row Map row.
         * @param {number} level Indent level.
         * @returns {void} Nothing is returned.
         */
        function appendRow(row, level) {
            const list = treeChildren(expansion, row.pathKey);
            /**
             * Keeps the structural children of a row.
             *
             * @param {object} child Child row.
             * @returns {boolean} True when the child is not a member.
             */
            const structural = list.filter(function (child) {
                return child.objectType !== "member";
            });
            /**
             * Keeps the member children of a row.
             *
             * @param {object} child Child row.
             * @returns {boolean} True when the child is a member.
             */
            const members = list.filter(function (child) {
                return child.objectType === "member";
            });
            const structureShown = expansion.structureOpen[row.pathKey] === true;
            const nodeRow = document.createElement("div");
            nodeRow.className = "member-system-node-row";
            nodeRow.style.setProperty("--system-node-level", String(level));
            if (structural.length || members.length) {
                const toggle = document.createElement("button");
                toggle.type = "button";
                toggle.className = "member-system-tree-toggle";
                toggle.setAttribute("data-path-key", row.pathKey);
                toggle.setAttribute("aria-expanded", String(structureShown));
                toggle.setAttribute("aria-label", structureShown ? "Collapse" : "Expand");
                toggle.appendChild(dialogChevron(structureShown));
                /**
                 * Toggles this structural branch and notifies the dialog.
                 *
                 * @returns {void} Nothing is returned.
                 */
                toggle.addEventListener("click", function () {
                    expansion.structureOpen[row.pathKey] = !structureShown;
                    notify();
                });
                nodeRow.appendChild(toggle);
            } else {
                const spacer = document.createElement("span");
                spacer.className = "member-system-tree-spacer";
                nodeRow.appendChild(spacer);
            }

            const selectable = canSelect(row) === true;
            const selectButton = document.createElement("button");
            selectButton.type = "button";
            selectButton.className = "member-system-node-button";
            selectButton.setAttribute("data-path-key", row.pathKey);
            if (selectedPathKey && selectedPathKey === row.pathKey) {
                selectButton.classList.add("is-active");
                selectButton.setAttribute("aria-current", "true");
            }
            if (!selectable) {
                selectButton.classList.add("is-not-destination");
                selectButton.setAttribute("aria-disabled", "true");
                selectButton.title = row.objectType === "member"
                    ? (options && options.memberTitle ? options.memberTitle : "Members stay visible and cannot be the destination.")
                    : (options && options.headingTitle ? options.headingTitle : "This heading cannot be the destination.");
            }
            const icon = dialogOutlineIcon(row.objectType);
            if (icon) {
                selectButton.appendChild(icon);
            }
            const text = document.createElement("span");
            text.className = "member-tree-node-text";
            text.textContent = row.nodeLabel || "";
            selectButton.appendChild(text);
            /**
             * Selects this row when canSelect allows it.
             *
             * @returns {void} A row that cannot be selected does nothing.
             */
            selectButton.addEventListener("click", function () {
                if (!selectable || !options || typeof options.onSelect !== "function") {
                    return;
                }
                options.onSelect(row);
            });
            nodeRow.appendChild(selectButton);
            host.appendChild(nodeRow);
            if (!structureShown) {
                return;
            }
            /**
             * Draws one open structural child.
             *
             * @param {object} child Structural child row.
             * @returns {void} Nothing is returned.
             */
            structural.forEach(function (child) {
                appendRow(child, level + 1);
            });
            if (members.length && structural.length) {
                appendMembersToggle(row, members, level + 1);
            } else if (members.length) {
                /**
                 * Draws one member child when the row has no separate member toggle.
                 *
                 * @param {object} child Member row.
                 * @returns {void} Nothing is returned.
                 */
                members.forEach(function (child) {
                    appendRow(child, level + 1);
                });
            }
        }

        const leading = options && options.leadingRows ? options.leadingRows : [];
        /**
         * Keeps a row that has no parent, so it is a root.
         *
         * @param {object} row Map row.
         * @returns {boolean} True when parentPathKey is null or undefined.
         */
        const roots = leading.concat(rows.filter(function (row) {
            return row.parentPathKey == null;
        }));
        if (!roots.length) {
            const empty = document.createElement("p");
            empty.className = "member-add-dialog-tree-empty";
            empty.textContent = options && options.emptyText
                ? options.emptyText
                : "The signed-in account map is not available.";
            host.appendChild(empty);
        } else {
            /**
             * Draws one root row.
             *
             * @param {object} row Root row.
             * @returns {void} Nothing is returned.
             */
            roots.forEach(function (row) {
                appendRow(row, 0);
            });
        }
        host.scrollTop = scrollTop;
        if (!restorePath) {
            return;
        }
        const selector = restoreToggle ? ".member-system-tree-toggle" : ".member-system-node-button";
        const buttons = host.querySelectorAll(selector);
        for (let index = 0; index < buttons.length; index += 1) {
            if (buttons[index].getAttribute("data-path-key") === restorePath) {
                buttons[index].focus();
                break;
            }
        }
    }

    /**
     * Opens the add dialog for a member, group, system, or place and holds the result as a preview.
     *
     * @param {Element} opener Element that opened the dialog.
     * @param {string} addMode group, system, place, or any other value, which opens the member dialog.
     * @returns {void} The dialog is modal. Closing it returns focus to the opener.
     */
    function presentAddMemberDialog(opener, addMode) {
        const mode = addMode === "group" || addMode === "system" || addMode === "place" ? addMode : "member";
        const memberOnly = mode === "member";
        const isGroup = !memberOnly;
        const modeTitle = mode === "group" ? "Add Group" : mode === "system" ? "Add System" : mode === "place" ? "Add Place" : "Add Member";
        const modeTreeLabel = "Add " + mode + " to:";
        const snapshot = readMapSnapshot();
        const openedAccountId = snapshot.accountId || currentAccountId();
        addMemberDialogOpener = opener;
        const controller = new AbortController();
        addMemberDialogController = controller;
        const signal = controller.signal;
        const expansion = createTreeExpansion(snapshot.rows);

        const accountRow = accountDestinationRow(openedAccountId);
        let selectedRow = containingAnchor(snapshot);
        if (!isModeDestination(selectedRow, mode)) {
            selectedRow = memberOnly ? null : accountRow;
        }
        let visibilityChoice = selectedRow && selectedRow.objectType === "account" ? "1" : "inherit";
        let permissionChoice = selectedRow && selectedRow.objectType === "account" ? "owner" : "inherit";
        let visibilityOverridden = false;
        let permissionOverridden = false;
        let fronting = false;
        let draftAvatarUrl = "";

        const dialog = document.createElement("dialog");
        dialog.className = "member-add-dialog";
        dialog.setAttribute("data-add-mode", mode);
        dialog.setAttribute("aria-labelledby", "member-add-overlay-title");
        addMemberDialog = dialog;

        const title = document.createElement("h2");
        title.id = "member-add-overlay-title";
        title.className = "member-add-dialog-title";
        title.textContent = modeTitle;

        const layout = document.createElement("div");
        layout.className = "member-add-dialog-layout";

        const side = document.createElement("div");
        side.className = "member-add-dialog-side";

        const sideHeading = document.createElement("div");
        sideHeading.className = "member-add-dialog-side-heading";

        const sideLabel = document.createElement("p");
        sideLabel.className = "member-add-dialog-side-label";
        sideLabel.textContent = modeTreeLabel;

        const expandAllButton = document.createElement("button");
        expandAllButton.type = "button";
        expandAllButton.className = "member-add-dialog-expand-all member-systems-expand-all";
        sideHeading.appendChild(sideLabel);
        sideHeading.appendChild(expandAllButton);

        const tree = document.createElement("div");
        tree.className = "member-add-dialog-tree member-systems-tree";
        tree.setAttribute("role", "group");
        tree.setAttribute("aria-label", modeTreeLabel);

        const actions = document.createElement("div");
        actions.className = "member-add-dialog-actions";

        const saveButton = document.createElement("button");
        saveButton.type = "submit";
        saveButton.className = "member-add-dialog-save";
        saveButton.textContent = "Save";

        const cancelButton = document.createElement("button");
        cancelButton.type = "button";
        cancelButton.className = "member-add-dialog-cancel";
        cancelButton.textContent = "Cancel";

        actions.appendChild(saveButton);
        actions.appendChild(cancelButton);
        side.appendChild(sideHeading);
        side.appendChild(tree);

        const main = document.createElement("div");
        main.className = "member-add-dialog-main";

        const toolbar = document.createElement("div");
        toolbar.className = "member-add-dialog-toolbar";

        const frontingButton = document.createElement("button");
        frontingButton.type = "button";
        frontingButton.className = "member-add-dialog-fronting member-add-dialog-member-only";
        frontingButton.hidden = true;

        const closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "member-add-dialog-close";
        closeButton.setAttribute("aria-label", "Close");
        closeButton.textContent = "×";

        toolbar.appendChild(frontingButton);

        const typeLine = document.createElement("p");
        typeLine.className = "member-add-dialog-type";
        typeLine.appendChild(document.createTextNode(memberOnly ? "Type : " : "Parent: "));
        const typeValue = document.createElement("span");
        typeLine.appendChild(typeValue);

        const destinationName = document.createElement("div");
        destinationName.className = "member-add-dialog-destination-name";

        const media = document.createElement("div");
        media.className = "member-add-dialog-media";

        const colorField = document.createElement("div");
        colorField.className = "member-add-dialog-field member-add-dialog-color-field";
        const colorInput = document.createElement("input");
        colorInput.type = "color";
        colorInput.className = "member-add-dialog-color";
        colorInput.value = "#3aa655";
        colorInput.setAttribute("aria-label", "Select your color");
        const colorCopy = document.createElement("span");
        colorCopy.className = "member-add-dialog-color-label";
        colorCopy.textContent = "Select your color";
        const colorPencil = document.createElement("button");
        colorPencil.type = "button";
        colorPencil.className = "member-add-dialog-pencil";
        colorPencil.setAttribute("aria-label", "Edit color");
        colorPencil.appendChild(dialogPencil());
        colorField.appendChild(colorInput);
        colorField.appendChild(colorCopy);
        colorField.appendChild(colorPencil);

        const avatarField = document.createElement("div");
        avatarField.className = "member-add-dialog-field member-add-dialog-avatar-field member-add-dialog-member-only";
        const fileInput = document.createElement("input");
        fileInput.type = "file";
        fileInput.accept = "image/*";
        fileInput.tabIndex = -1;
        fileInput.className = "member-add-dialog-file";
        fileInput.setAttribute("aria-label", "Choose an avatar image");
        const avatarFrame = document.createElement("div");
        avatarFrame.className = "member-add-dialog-avatar";
        const avatarPencil = document.createElement("button");
        avatarPencil.type = "button";
        avatarPencil.className = "member-add-dialog-pencil";
        avatarPencil.setAttribute("aria-label", "Choose an avatar image");
        avatarPencil.appendChild(dialogPencil());
        avatarField.appendChild(avatarFrame);
        avatarField.appendChild(avatarPencil);
        avatarField.appendChild(fileInput);

        media.appendChild(colorField);
        media.appendChild(avatarField);

        /**
         * Wraps a dialog control with its label and pencil.
         *
         * @param {string} fieldId Label and control id.
         * @param {string} labelText Visible label.
         * @param {HTMLElement} control Input or textarea.
         * @param {object} fieldOptions mode add hides the pencil.
         * @returns {HTMLDivElement} The field.
         */
        function labeledField(fieldId, labelText, control, fieldOptions) {
            const field = document.createElement("div");
            field.className = "member-add-dialog-field";
            const label = document.createElement("label");
            label.className = "member-add-dialog-label";
            label.setAttribute("for", fieldId);
            label.textContent = labelText;
            const pencil = document.createElement("button");
            pencil.type = "button";
            pencil.className = "member-add-dialog-pencil";
            pencil.setAttribute("aria-label", "Edit " + labelText);
            pencil.appendChild(dialogPencil());
            /**
             * Moves focus to the labeled control.
             *
             * @returns {void} Nothing is returned.
             */
            pencil.addEventListener("click", function () {
                control.focus();
            });
            if (fieldOptions && fieldOptions.mode === "add") {
                pencil.hidden = true;
                pencil.tabIndex = -1;
                pencil.setAttribute("aria-hidden", "true");
            }
            field.appendChild(label);
            field.appendChild(control);
            field.appendChild(pencil);
            return field;
        }

        const nameInput = document.createElement("input");
        nameInput.id = "member-add-overlay-name";
        nameInput.type = "text";
        nameInput.autocomplete = "off";
        nameInput.placeholder = mode === "group"
            ? "Enter group name"
            : mode === "system"
                ? "Enter system name"
                : mode === "place"
                    ? "Enter place name"
                    : "Enter member name";
        nameInput.value = "";
        let anchorNameEdited = false;
        /**
         * Remembers that the user typed an anchor name, so later destination changes do not clear it.
         *
         * @returns {void} Nothing is returned.
         */
        nameInput.addEventListener("input", function () {
            anchorNameEdited = true;
        });
        /**
         * Clears a generated anchor name until the user types one.
         *
         * @returns {void} A member dialog, or a name the user already edited, is left as typed.
         */
        function keepTypedAnchorName() {
            if (memberOnly || anchorNameEdited) {
                return;
            }
            nameInput.value = "";
        }

        const pronounsInput = document.createElement("input");
        pronounsInput.id = "member-add-overlay-pronouns";
        pronounsInput.type = "text";
        pronounsInput.autocomplete = "off";
        pronounsInput.placeholder = "Enter pronouns";

        const descriptionInput = document.createElement("textarea");
        descriptionInput.id = "member-add-overlay-description";
        descriptionInput.rows = 6;
        descriptionInput.placeholder = "Enter description";

        const status = createStatusMessage();
        status.classList.add("member-add-dialog-status");

        /**
         * Redraws the add-dialog Fronting button from the local draft.
         *
         * @returns {void} Nothing is returned.
         */
        function paintFronting() {
            frontingButton.replaceChildren(dialogFrontingIcon(fronting));
            const label = document.createElement("span");
            label.textContent = fronting ? "Fronting" : "Not fronting";
            frontingButton.appendChild(label);
            frontingButton.setAttribute("aria-pressed", fronting ? "true" : "false");
            frontingButton.setAttribute("aria-label", fronting ? "Fronting" : "Not fronting");
        }

        /**
         * Paints the add-dialog avatar through the avatars helper when it exists.
         *
         * @returns {void} Without that helper, an empty draft shows No avatar selected.
         */
        function paintAvatar() {
            avatarFrame.replaceChildren();
            const avatars = window.PluralBridge.avatars;
            if (avatars && typeof avatars.paintFrame === "function") {
                avatars.paintFrame(avatarFrame, draftAvatarUrl, "No avatar selected");
                return;
            }
            avatarFrame.textContent = draftAvatarUrl ? "" : "No avatar selected";
        }

        /**
         * Reads the selected destination when it is valid for this mode.
         *
         * @returns {object|null} The selected row, or null.
         */
        function currentDestination() {
            return isModeDestination(selectedRow, mode) ? selectedRow : null;
        }

        /**
         * Sets visibility and permission defaults for the current destination unless the user already changed them.
         *
         * @returns {void} An account destination uses Only you and owner. Another destination uses inherit.
         */
        function applyDestinationDefaults() {
            const destination = currentDestination();
            const standalone = Boolean(destination && destination.objectType === "account");
            if (!visibilityOverridden) {
                visibilityChoice = standalone ? "1" : "inherit";
            }
            if (!permissionOverridden) {
                permissionChoice = standalone ? "owner" : "inherit";
            }
        }

        /**
         * Enables Save only when a valid destination is selected.
         *
         * @returns {void} Nothing is returned.
         */
        function syncSave() {
            saveButton.disabled = !currentDestination();
        }

        /**
         * Updates the expand-all button for the current tree state.
         *
         * @returns {void} Nothing is returned.
         */
        function syncExpandAll() {
            const collapseAll = treeFullyOpen(expansion);
            const label = collapseAll ? "Collapse all" : "Expand all";
            expandAllButton.setAttribute("aria-label", label);
            expandAllButton.title = label;
            let icon = expandAllButton.querySelector(".member-tree-chevron");
            if (!icon) {
                expandAllButton.replaceChildren(dialogChevron(collapseAll));
                return;
            }
            icon.classList.toggle("is-expanded", collapseAll);
        }

        /**
         * Shows the selected destination type and name.
         *
         * @returns {void} No selection shows None and Select a destination.
         */
        function paintDestination() {
            const destination = currentDestination();
            if (!destination) {
                typeValue.textContent = "None";
                destinationName.textContent = "Select a destination";
                destinationName.classList.add("is-empty");
                return;
            }

            typeValue.textContent = destinationTypeLabel(destination.objectType);
            destinationName.textContent = destinationDisplayName(destination);
            destinationName.classList.remove("is-empty");
        }

        /**
         * Selects a destination row and refreshes the defaults, policy, tree, and name.
         *
         * @param {object} row Clicked row. An account change closes the dialog. A row that is not valid for the mode is ignored.
         * @returns {void} Nothing is returned.
         */
        function selectOccurrence(row) {
            if (accountChanged()) {
                return;
            }
            if (!isModeDestination(row, mode)) {
                return;
            }
            if (selectedRow && selectedRow.pathKey === row.pathKey) {
                syncSave();
                return;
            }
            selectedRow = row;
            applyDestinationDefaults();
            paintDestination();
            paintPolicy();
            syncSave();
            paintTree();
            keepTypedAnchorName();
        }

        /**
         * Closes the dialog when the signed-in account is no longer the account that opened it.
         *
         * @returns {boolean} True when the account changed and the dialog was closed.
         */
        function accountChanged() {
            const now = currentAccountId();
            if (openedAccountId && now !== openedAccountId) {
                closeAddMemberDialog();
                return true;
            }
            return false;
        }

        /**
         * Redraws the destination tree for the current account snapshot.
         *
         * @returns {void} Nothing is returned.
         */
        function paintTree() {
            renderDialogTree(tree, snapshot.rows, expansion, {
                leadingRows: accountRow && !memberOnly ? [accountRow] : [],
                selectedPathKey: selectedRow && selectedRow.pathKey ? selectedRow.pathKey : "",
                /**
                 * Allows selection of rows that are valid destinations for this add mode.
                 *
                 * @param {object} row Tree row.
                 * @returns {boolean} True when the row can be the destination.
                 */
                canSelect: function (row) {
                    return isModeDestination(row, mode);
                },
                onSelect: selectOccurrence,
                /**
                 * Repaints the tree after an expansion change, unless the account changed.
                 *
                 * @returns {void} Nothing is returned.
                 */
                onChange: function () {
                    if (accountChanged()) {
                        return;
                    }
                    syncExpandAll();
                    paintTree();
                },
                emptyText: "The signed-in account map is not available."
            });
            syncExpandAll();
        }

        const form = document.createElement("form");
        form.className = "member-add-dialog-form";
        const heading = document.createElement("div");
        heading.className = "member-add-dialog-heading";
        heading.appendChild(title);
        heading.appendChild(closeButton);
        form.appendChild(heading);
        form.appendChild(layout);
        layout.appendChild(side);
        layout.appendChild(main);
        const identity = document.createElement("div");
        identity.className = "member-add-dialog-identity";
        identity.appendChild(destinationName);
        identity.appendChild(media);

        const policyRow = document.createElement("div");
        policyRow.className = "member-add-dialog-policy";
        const visibilityHint = document.createElement("p");
        visibilityHint.className = "member-policy-hint";
        const permissionHint = document.createElement("p");
        permissionHint.className = "member-policy-hint";
        const visibilityMenu = createPolicyDropdown({
            label: "Visible to",
            choices: policyChoices("visibility", false),
            /**
             * Reads the add-dialog visibility choice.
             *
             * @returns {string} The current visibility id.
             */
            getValue: function () { return visibilityChoice; },
            /**
             * Stores a visibility choice and marks it as chosen by the user.
             *
             * @param {string} next Choice id. A missing value becomes inherit.
             * @returns {void} Nothing is returned.
             */
            setValue: function (next) {
                visibilityChoice = next || "inherit";
                visibilityOverridden = true;
                paintPolicy();
            }
        });
        const permissionMenu = createPolicyDropdown({
            label: "Permissions",
            choices: policyChoices("permission", false),
            /**
             * Reads the add-dialog permission choice.
             *
             * @returns {string} The current permission id.
             */
            getValue: function () { return permissionChoice; },
            /**
             * Stores a permission choice and marks it as chosen by the user.
             *
             * @param {string} next Choice id. A missing value becomes inherit.
             * @returns {void} Nothing is returned.
             */
            setValue: function (next) {
                permissionChoice = next || "inherit";
                permissionOverridden = true;
                paintPolicy();
            }
        });
        const visibilityField = document.createElement("div");
        visibilityField.className = "member-add-dialog-policy-field";
        const visibilityLabel = document.createElement("span");
        visibilityLabel.className = "member-add-dialog-label";
        visibilityLabel.textContent = "Visible to:";
        visibilityField.appendChild(visibilityLabel);
        visibilityField.appendChild(visibilityMenu.root);
        visibilityField.appendChild(visibilityHint);
        const permissionField = document.createElement("div");
        permissionField.className = "member-add-dialog-policy-field";
        const permissionLabel = document.createElement("span");
        permissionLabel.className = "member-add-dialog-label";
        permissionLabel.textContent = "Permissions:";
        permissionField.appendChild(permissionLabel);
        permissionField.appendChild(permissionMenu.root);
        permissionField.appendChild(permissionHint);
        policyRow.appendChild(visibilityField);
        policyRow.appendChild(permissionField);

        /**
         * Refreshes the inheritance hints and both policy menus.
         *
         * @returns {void} An account destination supplies no inheritance source name.
         */
        function paintPolicy() {
            const destination = currentDestination();
            const sourceName = destination && destination.objectType !== "account"
                ? destinationDisplayName(destination)
                : "";
            paintInheritanceHint(visibilityHint, visibilityChoice === "inherit", sourceName);
            paintInheritanceHint(permissionHint, permissionChoice === "inherit", sourceName);
            visibilityMenu.refresh();
            permissionMenu.refresh();
        }

        main.appendChild(policyRow);
        main.appendChild(typeLine);
        main.appendChild(identity);
        const nameField = labeledField("member-add-overlay-name", "Name", nameInput, { mode: "add" });
        nameField.classList.add("member-add-dialog-name-field");
        main.appendChild(nameField);
        const pronounsField = labeledField("member-add-overlay-pronouns", "Pronouns", pronounsInput, { mode: "add" });
        pronounsField.classList.add("member-add-dialog-member-only");
        if (isGroup) {
            pronounsField.hidden = true;
            frontingButton.hidden = true;
            avatarField.hidden = true;
        }
        main.appendChild(pronounsField);
        const descriptionField = labeledField("member-add-overlay-description", "Description", descriptionInput, { mode: "add" });
        descriptionField.classList.add("member-add-dialog-description-field");
        main.appendChild(descriptionField);
        main.appendChild(status);
        form.appendChild(actions);
        dialog.appendChild(form);

        paintFronting();
        paintAvatar();
        applyDestinationDefaults();
        paintDestination();
        paintPolicy();
        syncSave();
        paintTree();
        keepTypedAnchorName();

        /**
         * Expands or collapses the whole destination tree.
         *
         * @returns {void} An account change closes the dialog instead.
         */
        expandAllButton.addEventListener("click", function () {
            if (accountChanged()) {
                return;
            }
            setTreeFullyOpen(expansion, !treeFullyOpen(expansion));
            syncExpandAll();
            paintTree();
        }, { signal: signal });

        /**
         * Toggles the local Fronting draft. This dialog does not persist it.
         *
         * @returns {void} An account change closes the dialog instead.
         */
        frontingButton.addEventListener("click", function () {
            if (accountChanged()) {
                return;
            }
            fronting = !fronting;
            paintFronting();
        }, { signal: signal });

        /**
         * Closes the add dialog from the close button.
         *
         * @returns {void} Nothing is returned.
         */
        closeButton.addEventListener("click", function () {
            closeAddMemberDialog();
        }, { signal: signal });
        /**
         * Closes the add dialog from Cancel.
         *
         * @returns {void} Nothing is returned.
         */
        cancelButton.addEventListener("click", function () {
            closeAddMemberDialog();
        }, { signal: signal });

        /**
         * Opens the add-dialog color picker.
         *
         * @returns {void} Nothing is returned.
         */
        colorPencil.addEventListener("click", function () {
            colorInput.click();
        }, { signal: signal });
        /**
         * Opens the add-dialog avatar chooser from the pencil.
         *
         * @returns {void} Nothing is returned.
         */
        avatarPencil.addEventListener("click", function () {
            fileInput.click();
        }, { signal: signal });
        /**
         * Opens the add-dialog avatar chooser from the frame.
         *
         * @returns {void} Nothing is returned.
         */
        avatarFrame.addEventListener("click", function () {
            fileInput.click();
        }, { signal: signal });
        /**
         * Replaces the add-dialog avatar preview with the chosen file.
         *
         * @returns {void} An empty selection returns. The previous uncommitted object URL is revoked.
         */
        fileInput.addEventListener("change", function () {
            const file = fileInput.files && fileInput.files[0] ? fileInput.files[0] : null;
            if (!file) {
                return;
            }
            if (draftAvatarUrl) {
                if (!addMemberDialogCommittedUrls[draftAvatarUrl]) {
                    URL.revokeObjectURL(draftAvatarUrl);
                }
            }
            const avatars = window.PluralBridge.avatars;
            draftAvatarUrl = avatars && typeof avatars.createObjectUrl === "function"
                ? avatars.createObjectUrl(file)
                : URL.createObjectURL(file);
            addMemberDialogOwnedUrls.push(draftAvatarUrl);
            paintAvatar();
        }, { signal: signal });

        /**
         * Closes the add dialog when the browser cancel event fires.
         *
         * @param {Event} event Dialog cancel event. The browser close is prevented so this dialog can clean up.
         * @returns {void} Nothing is returned.
         */
        dialog.addEventListener("cancel", function (event) {
            event.preventDefault();
            closeAddMemberDialog();
        }, { signal: signal });

        /**
         * Closes the add dialog if the account changed while the window was unfocused.
         *
         * @returns {void} Nothing is returned.
         */
        window.addEventListener("focus", function () {
            accountChanged();
        }, { signal: signal });

        /**
         * Holds the add dialog as an account preview.
         * A blank name, missing destination, missing account, or account change returns without storing. A stored member preview keeps its avatar URL.
         *
         * @param {SubmitEvent} event Form submission. The browser submit is cancelled.
         * @returns {Promise<void>} The promise settles after the status is written. holdAccountPreview does not reject.
         */
        form.addEventListener("submit", async function (event) {
            event.preventDefault();
            if (accountChanged()) {
                return;
            }

            const displayName = nameInput.value.trim();
            if (!displayName) {
                status.textContent = "Name is required.";
                nameInput.focus();
                return;
            }

            const destination = currentDestination();
            if (!destination) {
                status.textContent = memberOnly
                    ? "Select a system, group, or place before saving this member."
                    : "Select a parent, or This account for a standalone " + mode + ", before saving.";
                syncSave();
                return;
            }

            if (!currentAccountId()) {
                status.textContent = "The signed-in account is not available, so this " + mode + " preview was not stored.";
                return;
            }

            saveButton.disabled = true;
            const previewRequest = {
                displayName: displayName,
                description: descriptionInput.value.trim(),
                color: colorInput.value,
                visibility: visibilityChoice,
                permission: permissionChoice,
                destination: {
                    objectType: destination.objectType,
                    objectId: destination.objectId,
                    pathKey: destination.pathKey,
                    anchorName: destination.anchorName || ""
                }
            };
            if (memberOnly) {
                previewRequest.pronouns = pronounsInput.value.trim();
                previewRequest.fronting = fronting;
                previewRequest.avatarObjectUrl = draftAvatarUrl;
            }
            try {
                const outcome = await holdAccountPreview(mode, previewRequest);
                if (accountChanged()) {
                    return;
                }
                if (outcome && outcome.reset && draftAvatarUrl) {
                    addMemberDialogCommittedUrls[draftAvatarUrl] = true;
                }
                status.textContent = outcome && outcome.status ? outcome.status : "";
            } finally {
                if (saveButton.isConnected) {
                    syncSave();
                }
            }
        }, { signal: signal });

        document.body.appendChild(dialog);
        dialog.showModal();
        keepTypedAnchorName();
        if (currentDestination()) {
            nameInput.focus();
        }
        /**
         * Clears an untouched generated anchor name after the dialog has opened.
         *
         * @returns {void} A disconnected input is ignored.
         */
        window.setTimeout(function () {
            if (nameInput.isConnected) {
                keepTypedAnchorName();
            }
        }, 0);
    }

    /**
     * Builds the type and id key used to compare relationship endpoints.
     *
     * @param {object} row Map or endpoint row. A header, member, or row without type and id returns an empty string.
     * @returns {string} The lowercased type:id key.
     */
    function relationshipObjectKey(row) {
        if (!row || row.header === true || !row.objectType || !row.objectId || row.objectType === "member") {
            return "";
        }
        return String(row.objectType).toLowerCase() + ":" + String(row.objectId).toLowerCase();
    }

    /**
     * Reports whether a row can be a relationship endpoint.
     *
     * @param {object} row Map row. Headers and rows without an id are excluded. system, group, and place qualify.
     * @returns {boolean} True for an endpoint.
     */
    function anchorEndpoint(row) {
        return Boolean(
            row &&
            row.header !== true &&
            row.objectId &&
            (row.objectType === "system" || row.objectType === "group" || row.objectType === "place")
        );
    }

    /**
     * Reports whether a parent relationship would point a child at one of its own ancestors.
     *
     * @param {object} snapshot Map snapshot whose rows supply parent links.
     * @param {object} parentRow Proposed parent, the left endpoint.
     * @param {object} childRow Proposed child, the right endpoint.
     * @returns {boolean} True for the same endpoint or when the proposed parent is already under the proposed child.
     */
    function parentLinkWouldCycle(snapshot, parentRow, childRow) {
        const parentKey = relationshipObjectKey(parentRow);
        const childKey = relationshipObjectKey(childRow);
        if (!parentKey || !childKey || parentKey === childKey) {
            return parentKey !== "" && parentKey === childKey;
        }

        const byPath = {};
        /**
         * Indexes one snapshot row by path key for the cycle walk.
         *
         * @param {object} row Map row. A row without pathKey is skipped.
         * @returns {void} Nothing is returned.
         */
        (snapshot.rows || []).forEach(function (row) {
            if (row.pathKey) {
                byPath[row.pathKey] = row;
            }
        });
        const parentsOf = {};
        /**
         * Records one parent key for a child key.
         *
         * @param {string} child Child object key.
         * @param {string} parent Parent object key. Equal or empty keys are ignored.
         * @returns {void} Nothing is returned.
         */
        function addParent(child, parent) {
            if (!child || !parent || child === parent) {
                return;
            }
            if (!parentsOf[child]) {
                parentsOf[child] = [];
            }
            if (parentsOf[child].indexOf(parent) < 0) {
                parentsOf[child].push(parent);
            }
        }
        /**
         * Adds the nearest keyed ancestor of one map row.
         *
         * @param {object} row Map row. A row without an object key or parent path is skipped.
         * @returns {void} Nothing is returned.
         */
        (snapshot.rows || []).forEach(function (row) {
            const child = relationshipObjectKey(row);
            if (!child || !row.parentPathKey) {
                return;
            }
            let current = byPath[row.parentPathKey];
            const seen = {};
            while (current && current.pathKey && !seen[current.pathKey]) {
                seen[current.pathKey] = true;
                const parent = relationshipObjectKey(current);
                if (parent) {
                    addParent(child, parent);
                    break;
                }
                current = current.parentPathKey ? byPath[current.parentPathKey] : null;
            }
        });
        const accountId = currentAccountId();
        const previews = accountId && memberPreviewsByAccount[accountId] ? memberPreviewsByAccount[accountId] : [];
        /**
         * Adds a stored parent-relationship preview to the cycle graph.
         *
         * @param {object} record Preview record. Other kinds and relationship types are skipped.
         * @returns {void} Nothing is returned.
         */
        previews.forEach(function (record) {
            if (!record || record.previewKind !== "relationship" || record.relationshipType !== "parent") {
                return;
            }
            addParent(relationshipObjectKey(record.target), relationshipObjectKey(record.source));
        });

        const seen = {};
        const stack = (parentsOf[parentKey] || []).slice();
        while (stack.length) {
            const key = stack.pop();
            if (!key || seen[key]) {
                continue;
            }
            if (key === childKey) {
                return true;
            }
            seen[key] = true;
            /**
             * Continues the ancestor walk through one parent.
             *
             * @param {string} next Parent object key.
             * @returns {void} Nothing is returned.
             */
            (parentsOf[key] || []).forEach(function (next) {
                stack.push(next);
            });
        }
        return false;
    }

    /**
     * Opens the relationship dialog and holds a chosen relationship as a preview.
     *
     * @param {Element} opener Element that opened the dialog.
     * @returns {void} The dialog is modal.
     */
    function presentRelationshipDialog(opener) {
        const snapshot = readMapSnapshot();
        const openedAccountId = snapshot.accountId || currentAccountId();
        addMemberDialogOpener = opener;
        const controller = new AbortController();
        addMemberDialogController = controller;
        const signal = controller.signal;
        const leftExpansion = createTreeExpansion(snapshot.rows);
        const rightExpansion = createTreeExpansion(snapshot.rows);
        const relationshipChoices = relationshipTypeChoices(openedAccountId);
        let leftRow = containingAnchor(snapshot);
        let rightRow = null;
        let relationshipType = "sibling";
        let visibilityChoice = "inherit";
        let permissionChoice = "inherit";

        const dialog = document.createElement("dialog");
        dialog.className = "member-add-dialog member-relationship-dialog";
        dialog.setAttribute("aria-labelledby", "member-relationship-title");
        addMemberDialog = dialog;

        const title = document.createElement("h2");
        title.id = "member-relationship-title";
        title.className = "member-add-dialog-title";
        title.textContent = "Add Relationship";

        const closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "member-add-dialog-close";
        closeButton.setAttribute("aria-label", "Close");
        closeButton.textContent = "×";

        const layout = document.createElement("div");
        layout.className = "member-relationship-layout";

        /**
         * Reads the visible name of a relationship endpoint.
         *
         * @param {object} row Endpoint row. A missing row returns an empty string.
         * @returns {string} anchorName, otherwise nodeLabel.
         */
        function endpointName(row) {
            if (!row) {
                return "";
            }
            return row.anchorName || row.nodeLabel || "";
        }

        /**
         * Redraws one side of the relationship tree.
         *
         * @param {HTMLElement} host Tree container for this side.
         * @param {string} side left uses the source selection. Any other value uses the target selection.
         * @returns {void} Nothing is returned.
         */
        function paintTree(host, side) {
            const selected = side === "left" ? leftRow : rightRow;
            renderDialogTree(host, snapshot.rows, side === "left" ? leftExpansion : rightExpansion, {
                selectedPathKey: selected && selected.pathKey ? selected.pathKey : "",
                memberTitle: "Members stay visible and cannot be relationship endpoints.",
                headingTitle: "This heading cannot be a relationship endpoint.",
                /**
                 * Allows selection of system, group, and place endpoints.
                 *
                 * @param {object} row Tree row.
                 * @returns {boolean} True when the row can be an endpoint.
                 */
                canSelect: function (row) {
                    return anchorEndpoint(row);
                },
                /**
                 * Stores the selected endpoint for this side.
                 *
                 * @param {object} row Selected row. The left side is the source and the other side is the target.
                 * @returns {void} Nothing is returned.
                 */
                onSelect: function (row) {
                    if (side === "left") {
                        leftRow = row;
                    } else {
                        rightRow = row;
                    }
                    paint();
                },
                /**
                 * Redraws this side after its expansion changes.
                 *
                 * @returns {void} Nothing is returned.
                 */
                onChange: function () {
                    paintTree(host, side);
                },
                emptyText: "The signed-in account map is not available."
            });
        }

        /**
         * Builds one labeled relationship column and its tree.
         *
         * @param {string} labelText Column label.
         * @param {string} side left or the target side.
         * @returns {object} The column element and its tree host.
         */
        function column(labelText, side) {
            const wrap = document.createElement("div");
            wrap.className = "member-relationship-column";
            const label = document.createElement("p");
            label.className = "member-add-dialog-side-label member-relationship-column-label";
            label.textContent = labelText;
            const tree = document.createElement("div");
            tree.className = "member-add-dialog-tree member-relationship-tree";
            tree.setAttribute("role", "group");
            tree.setAttribute("aria-label", labelText);
            wrap.appendChild(label);
            wrap.appendChild(tree);
            wrap.tree = tree;
            wrap.side = side;
            return wrap;
        }

        const leftColumn = column("Who?", "left");
        const rightColumn = column("With who?", "right");
        const center = document.createElement("div");
        center.className = "member-relationship-center";
        const kindLabel = document.createElement("p");
        kindLabel.className = "member-add-dialog-label member-relationship-column-label";
        kindLabel.textContent = "What kind of relationship?";
        const kindRow = document.createElement("div");
        kindRow.className = "member-relationship-kind";
        const kindMenu = createPolicyDropdown({
            label: "Relationship type",
            choices: relationshipChoices,
            /**
             * Reads the selected relationship type.
             *
             * @returns {string} The type id.
             */
            getValue: function () { return relationshipType; },
            /**
             * Stores the selected relationship type.
             *
             * @param {string} next Type id. A missing value becomes sibling.
             * @returns {void} Nothing is returned.
             */
            setValue: function (next) {
                relationshipType = next || "sibling";
                paint();
            }
        });
        const kindPencil = document.createElement("button");
        kindPencil.type = "button";
        kindPencil.className = "member-add-dialog-pencil is-revealed";
        kindPencil.setAttribute("aria-label", "Add a relationship type");
        kindPencil.title = "Add a relationship type";
        kindPencil.appendChild(dialogPencil());
        const arrow = document.createElement("span");
        arrow.className = "member-relationship-arrow";
        arrow.setAttribute("aria-hidden", "true");
        arrow.appendChild(relationshipArrow());
        kindRow.appendChild(kindMenu.root);
        kindRow.appendChild(kindPencil);
        kindRow.appendChild(arrow);
        const typeEditor = document.createElement("div");
        typeEditor.className = "member-relationship-type-editor";
        typeEditor.hidden = true;
        const typeInput = document.createElement("input");
        typeInput.type = "text";
        typeInput.className = "member-relationship-type-input";
        typeInput.placeholder = "Enter relationship type";
        typeInput.setAttribute("aria-label", "Relationship type name");
        typeInput.autocomplete = "off";
        const typeAdd = document.createElement("button");
        typeAdd.type = "button";
        typeAdd.className = "member-relationship-type-add";
        typeAdd.textContent = "Add";
        const typeCancel = document.createElement("button");
        typeCancel.type = "button";
        typeCancel.className = "member-relationship-type-cancel";
        typeCancel.textContent = "Cancel";
        const typeStatus = document.createElement("p");
        typeStatus.className = "member-relationship-type-status";
        const typeActions = document.createElement("div");
        typeActions.className = "member-relationship-type-actions";
        typeActions.appendChild(typeAdd);
        typeActions.appendChild(typeCancel);
        typeEditor.appendChild(typeInput);
        typeEditor.appendChild(typeActions);
        typeEditor.appendChild(typeStatus);

        const visibilityHint = document.createElement("p");
        visibilityHint.className = "member-policy-hint";
        const permissionHint = document.createElement("p");
        permissionHint.className = "member-policy-hint";
        const visibilityMenu = createPolicyDropdown({
            label: "Visible to",
            choices: policyChoices("visibility", false),
            /**
             * Reads the relationship visibility choice.
             *
             * @returns {string} The visibility id.
             */
            getValue: function () { return visibilityChoice; },
            /**
             * Stores the relationship visibility choice.
             *
             * @param {string} next Choice id. A missing value becomes inherit.
             * @returns {void} Nothing is returned.
             */
            setValue: function (next) {
                visibilityChoice = next || "inherit";
                paint();
            }
        });
        const permissionMenu = createPolicyDropdown({
            label: "Permissions",
            choices: policyChoices("permission", false),
            /**
             * Reads the relationship permission choice.
             *
             * @returns {string} The permission id.
             */
            getValue: function () { return permissionChoice; },
            /**
             * Stores the relationship permission choice.
             *
             * @param {string} next Choice id. A missing value becomes inherit.
             * @returns {void} Nothing is returned.
             */
            setValue: function (next) {
                permissionChoice = next || "inherit";
                paint();
            }
        });

        /**
         * Wraps a relationship policy control with its label and hint.
         *
         * @param {string} text Label text.
         * @param {HTMLElement} control Policy dropdown root.
         * @param {HTMLElement} hint Inheritance hint.
         * @returns {HTMLDivElement} The field.
         */
        function labeled(text, control, hint) {
            const field = document.createElement("div");
            field.className = "member-add-dialog-policy-field";
            const label = document.createElement("span");
            label.className = "member-add-dialog-label";
            label.textContent = text;
            field.appendChild(label);
            field.appendChild(control);
            field.appendChild(hint);
            return field;
        }

        const kindBlock = document.createElement("div");
        kindBlock.className = "member-relationship-kind-block";
        kindBlock.appendChild(kindRow);
        kindBlock.appendChild(typeEditor);
        const centerBody = document.createElement("div");
        centerBody.className = "member-relationship-center-body";
        centerBody.appendChild(kindBlock);
        centerBody.appendChild(labeled("Visible to:", visibilityMenu.root, visibilityHint));
        centerBody.appendChild(labeled("Permissions:", permissionMenu.root, permissionHint));
        center.appendChild(kindLabel);
        center.appendChild(centerBody);

        const status = createStatusMessage();
        status.classList.add("member-add-dialog-status");
        const preview = document.createElement("p");
        preview.className = "member-relationship-preview";
        const actions = document.createElement("div");
        actions.className = "member-add-dialog-actions";
        const saveButton = document.createElement("button");
        saveButton.type = "submit";
        saveButton.className = "member-add-dialog-save";
        saveButton.textContent = "Save";
        const cancelButton = document.createElement("button");
        cancelButton.type = "button";
        cancelButton.className = "member-add-dialog-cancel";
        cancelButton.textContent = "Cancel";
        actions.appendChild(saveButton);
        actions.appendChild(cancelButton);

        /**
         * Repaints both relationship trees, the inheritance hints, and the preview sentence.
         *
         * @returns {void} Nothing is returned.
         */
        function paint() {
            paintTree(leftColumn.tree, "left");
            paintTree(rightColumn.tree, "right");
            const sourceName = endpointName(leftRow);
            paintInheritanceHint(visibilityHint, visibilityChoice === "inherit", sourceName);
            paintInheritanceHint(permissionHint, permissionChoice === "inherit", sourceName);
            visibilityMenu.refresh();
            permissionMenu.refresh();
            kindMenu.refresh();
            /**
             * Finds the choice for the selected relationship type.
             *
             * @param {object} item Relationship choice.
             * @returns {boolean} True when the choice id is the selected type.
             */
            const typeLabel = relationshipChoices.filter(function (item) {
                return item.id === relationshipType;
            })[0];
            const typeText = typeLabel ? (typeLabel.custom ? typeLabel.label : typeLabel.id) : relationshipType;
            preview.textContent = sourceName && endpointName(rightRow)
                ? "Relationship: " + sourceName + " (" + typeText + " → " + endpointName(rightRow) + ")"
                : "Relationship: Select who the relationship starts from and who it points to.";
        }

        const form = document.createElement("form");
        form.className = "member-add-dialog-form";
        const headingRow = document.createElement("div");
        headingRow.className = "member-relationship-heading";
        headingRow.appendChild(title);
        headingRow.appendChild(closeButton);
        form.appendChild(headingRow);
        layout.appendChild(leftColumn);
        layout.appendChild(center);
        layout.appendChild(rightColumn);
        form.appendChild(layout);
        const footer = document.createElement("div");
        footer.className = "member-relationship-footer";
        footer.appendChild(actions);
        footer.appendChild(preview);
        form.appendChild(footer);
        form.appendChild(status);
        dialog.appendChild(form);
        paint();

        /**
         * Adds a custom relationship type for the current account and selects it.
         *
         * @param {string} label Typed label. A blank or duplicate label sets the status and does not add a type.
         * @returns {void} A missing account sets the status and stores nothing.
         */
        function rememberRelationshipType(label) {
            const name = label.trim();
            if (!name) {
                typeStatus.textContent = "Enter a relationship type.";
                typeInput.focus();
                return;
            }
            const accountId = currentAccountId();
            if (!accountId || accountId !== openedAccountId) {
                typeStatus.textContent = "The signed-in account is not available, so this relationship type was not stored.";
                return;
            }
            /**
             * Reports whether a relationship label is already in the choice list.
             *
             * @param {object} item Relationship choice.
             * @returns {boolean} True when the labels match, ignoring case.
             */
            const duplicate = relationshipChoices.some(function (item) {
                return item.label.toLowerCase() === name.toLowerCase();
            });
            if (duplicate) {
                typeStatus.textContent = "That relationship type is already available.";
                typeInput.focus();
                return;
            }
            if (!customRelationshipTypesByAccount[accountId]) {
                customRelationshipTypesByAccount[accountId] = [];
            }
            const record = {
                id: customRelationshipId(name),
                label: name,
                custom: true
            };
            customRelationshipTypesByAccount[accountId].push(record);
            relationshipChoices.push({
                id: record.id,
                label: record.label,
                icon: "link",
                disabled: false,
                custom: true
            });
            relationshipType = record.id;
            typeInput.value = "";
            typeStatus.textContent = "";
            typeEditor.hidden = true;
            paint();
        }

        /**
         * Shows the custom relationship-type editor.
         *
         * @returns {void} Nothing is returned.
         */
        kindPencil.addEventListener("click", function () {
            typeEditor.hidden = false;
            typeStatus.textContent = "";
            typeInput.focus();
        }, { signal: signal });
        /**
         * Hides the custom relationship-type editor.
         *
         * @returns {void} Nothing is returned.
         */
        typeCancel.addEventListener("click", function () {
            typeEditor.hidden = true;
            typeStatus.textContent = "";
        }, { signal: signal });
        /**
         * Adds the typed relationship type.
         *
         * @returns {void} Nothing is returned.
         */
        typeAdd.addEventListener("click", function () {
            rememberRelationshipType(typeInput.value);
        }, { signal: signal });
        /**
         * Adds the typed relationship type when Enter is pressed.
         *
         * @param {KeyboardEvent} event Enter is prevented so the dialog form does not submit.
         * @returns {void} Nothing is returned.
         */
        typeInput.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                rememberRelationshipType(typeInput.value);
            }
        }, { signal: signal });

        closeButton.addEventListener("click", closeAddMemberDialog, { signal: signal });
        cancelButton.addEventListener("click", closeAddMemberDialog, { signal: signal });
        /**
         * Closes the relationship dialog when the browser cancel event fires.
         *
         * @param {Event} event Dialog cancel event.
         * @returns {void} Nothing is returned.
         */
        dialog.addEventListener("cancel", function (event) {
            event.preventDefault();
            closeAddMemberDialog();
        }, { signal: signal });
        /**
         * Holds the relationship as an account preview.
         * Missing endpoints, a parent cycle, the same anchor on both sides, or a missing account set the status and store nothing.
         *
         * @param {SubmitEvent} event Form submission. The browser submit is cancelled.
         * @returns {Promise<void>} The promise settles after the status is written. holdAccountPreview does not reject.
         */
        form.addEventListener("submit", async function (event) {
            event.preventDefault();
            if (!anchorEndpoint(leftRow) || !anchorEndpoint(rightRow)) {
                status.textContent = "Choose a system, group, or place for both sides of the relationship.";
                return;
            }
            if (relationshipType === "parent" && parentLinkWouldCycle(snapshot, leftRow, rightRow)) {
                status.textContent = "This would create a circular parent relationship.";
                return;
            }
            if (relationshipObjectKey(leftRow) === relationshipObjectKey(rightRow)) {
                status.textContent = "Choose two different anchors. An anchor cannot be related to itself in this preview.";
                return;
            }
            if (!currentAccountId()) {
                status.textContent = "The signed-in account is not available, so this relationship preview was not stored.";
                return;
            }
            const label = endpointName(leftRow) + " (" + relationshipType + "-> " + endpointName(rightRow) + ")";
            saveButton.disabled = true;
            try {
                const outcome = await holdAccountPreview("relationship", {
                    displayName: label,
                    previewLabel: label,
                    relationshipType: relationshipType,
                    visibility: visibilityChoice,
                    permission: permissionChoice,
                    source: {
                        objectType: leftRow.objectType,
                        objectId: leftRow.objectId,
                        pathKey: leftRow.pathKey,
                        anchorName: leftRow.anchorName || ""
                    },
                    target: {
                        objectType: rightRow.objectType,
                        objectId: rightRow.objectId,
                        pathKey: rightRow.pathKey,
                        anchorName: rightRow.anchorName || ""
                    },
                    destination: {
                        objectType: leftRow.objectType,
                        objectId: leftRow.objectId,
                        pathKey: leftRow.pathKey,
                        anchorName: leftRow.anchorName || ""
                    }
                });
                status.textContent = outcome && outcome.status ? outcome.status : "";
            } finally {
                if (saveButton.isConnected) {
                    saveButton.disabled = false;
                }
            }
        }, { signal: signal });

        document.body.appendChild(dialog);
        dialog.showModal();
    }

    /**
     * Schedules the add or relationship dialog for the next turn.
     *
     * @param {object} options opener and mode. An unrecognized mode opens the member dialog.
     * @returns {void} A dialog closed before the turn runs is not opened again.
     */
    function openAddMemberDialog(options) {
        const opener = options && options.opener instanceof Element ? options.opener : null;
        const requested = options && options.mode ? String(options.mode) : "member";
        const mode = requested === "group" || requested === "system" || requested === "place" || requested === "relationship"
            ? requested
            : "member";
        const scheduledEpoch = addMemberDialogEpoch;
        /**
         * Opens the scheduled dialog unless a newer close advanced the dialog epoch.
         *
         * @returns {void} Nothing is returned.
         */
        window.setTimeout(function () {
            if (scheduledEpoch !== addMemberDialogEpoch) {
                return;
            }
            destroyAddMemberDialog();
            if (mode === "relationship") {
                presentRelationshipDialog(opener);
                return;
            }
            presentAddMemberDialog(opener, mode);
        }, 0);
    }

    return {
        createMemberToolbar: createMemberToolbar,
        releaseAddMenuHandlers: releaseAddMenuHandlers,
        setTreeToggleVisible: setTreeToggleVisible,
        createMemberAddForm: createMemberAddForm,
        openAddMemberDialog: openAddMemberDialog,
        createPolicyDropdown: createPolicyDropdown,
        policyChoices: policyChoices,
        createEditPencil: createEditPencil,
        closeAddMemberDialog: closeAddMemberDialog,
        previewAddMember: previewAddMember,
        readMemberProfilePreview: readMemberProfilePreview,
        saveMemberProfilePreview: saveMemberProfilePreview,
        saveMemberProfile: saveMemberProfile,
        saveMemberFronting: saveMemberFronting,
        createMemberEditForm: createMemberEditForm
    };
})();
