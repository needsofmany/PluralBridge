window.PluralBridge = window.PluralBridge || {};

window.PluralBridge.members = (function () {
    function createStatusMessage() {
        const status = document.createElement("p");
        status.className = "member-form-status";
        status.setAttribute("aria-live", "polite");
        status.textContent = "";
        return status;
    }

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

    function setFormBusy(form, isBusy) {
        const controls = form.querySelectorAll("input, textarea, button");

        controls.forEach(function (control) {
            control.disabled = isBusy;
        });
    }

    function getMemberId(member) {
        return member && member.memberId ? member.memberId : "";
    }

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

    function treeToggleLabel(isVisible) {
        return isVisible ? "Hide Tree" : "Show Tree";
    }

    let addMenuController = null;

    function releaseAddMenuHandlers() {
        if (addMenuController) {
            addMenuController.abort();
            addMenuController = null;
        }
    }

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
                button.addEventListener("click", function () {
                    clickHandler();
                    details.open = false;
                });
            }

            return button;
        }

        const memberAddEnabled = !(options && options.memberActionsEnabled === false);
        popover.appendChild(createAddOption("Member", "member", memberAddEnabled, function () {
            if (options && typeof options.toggleAddMember === "function") {
                options.toggleAddMember();
            }
        }));
        popover.appendChild(createAddOption("Group", "group", Boolean(options && typeof options.toggleAddGroup === "function"), function () {
            if (options && typeof options.toggleAddGroup === "function") {
                options.toggleAddGroup();
            }
        }));
        popover.appendChild(createAddOption("Place", "place", Boolean(options && typeof options.toggleAddPlace === "function"), function () {
            if (options && typeof options.toggleAddPlace === "function") {
                options.toggleAddPlace();
            }
        }));
        popover.appendChild(createAddOption("System", "system", Boolean(options && typeof options.toggleAddSystem === "function"), function () {
            if (options && typeof options.toggleAddSystem === "function") {
                options.toggleAddSystem();
            }
        }));

        const relationshipSeparator = document.createElement("div");
        relationshipSeparator.className = "member-toolbar-add-separator";
        relationshipSeparator.setAttribute("role", "separator");
        popover.appendChild(relationshipSeparator);
        popover.appendChild(createAddOption("Relationship", "relationship", Boolean(options && typeof options.toggleAddRelationship === "function"), function () {
            if (options && typeof options.toggleAddRelationship === "function") {
                options.toggleAddRelationship();
            }
        }));

        details.appendChild(summary);
        details.appendChild(popover);

        function syncExpanded() {
            summary.setAttribute("aria-expanded", details.open ? "true" : "false");
        }

        function closeMenu() {
            if (details.open) {
                details.open = false;
            }
        }

        details.addEventListener("toggle", syncExpanded, { signal: signal });

        details.addEventListener("focusout", function () {
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

        details.addEventListener("keydown", function (event) {
            if (event.key !== "Escape" || !details.open) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            closeMenu();
            summary.focus();
        }, { signal: signal });

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
            onClick: function () {
                if (options && typeof options.toggleSystemPanel === "function") {
                    options.toggleSystemPanel();
                }
            }
        });
        treeButton.classList.add("member-toolbar-tree-toggle");
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
            onClick: function () {
                if (window.PluralBridge.theme && typeof window.PluralBridge.theme.toggle === "function") {
                    window.PluralBridge.theme.toggle();
                }
            }
        });
        themeButton.classList.add("member-toolbar-theme");

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
            let unsubscribe = function () {};
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

    function databaseWritesAreDisabled() {
        const tools = window.PluralBridge && window.PluralBridge.developerTools
            ? window.PluralBridge.developerTools
            : null;

        return !tools || tools.databaseWritesDisabled !== false;
    }

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

    function previewNounFor(kind) {
        if (kind === "group" || kind === "system" || kind === "place" || kind === "relationship" || kind === "member") {
            return kind;
        }

        return "member";
    }

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

    async function previewAddMember(request) {
        return holdAccountPreview("member", request);
    }

    function profilePreviewKey(value) {
        return value ? String(value).toLowerCase() : "";
    }

    function explicitPreviewColor(value) {
        return typeof value === "string" && /^#[0-9A-Fa-f]{6}$/.test(value) ? value : null;
    }

    function explicitPreviewFronting(value) {
        return value === true || value === false ? value : null;
    }

    function canonicalVisibilityId(value) {
        if (value === 1 || value === 2 || value === 3 || value === 4) {
            return value;
        }

        return null;
    }

    function canonicalPermission(value) {
        if (value === "inherit" || value === "viewer" || value === "editor" || value === "manager" || value === "owner") {
            return value;
        }

        return null;
    }

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

    function explicitPreviewAvatar(value) {
        return typeof value === "string" && value ? value : null;
    }

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

        const remaining = Object.keys(stored).some(function (key) {
            return stored[key] !== undefined;
        });
        if (!remaining) {
            delete accountPreviews[memberKey];
        }
    }

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
            displayName: fields ? fields.displayName : "",
            pronouns: fields ? fields.pronouns : "",
            description: fields ? fields.description : ""
        };
        if (fields && Object.prototype.hasOwnProperty.call(fields, "color")) {
            body.color = fields.color;
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

        cancelButton.addEventListener("click", function () {
            form.reset();
            status.textContent = "";

            if (options && typeof options.cancelAddMember === "function") {
                options.cancelAddMember();
            }
        });

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

        cancelButton.addEventListener("click", function () {
            if (options && typeof options.cancelEdit === "function") {
                options.cancelEdit();
            }
        });

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

    function revokeUncommittedAvatarUrls() {
        addMemberDialogOwnedUrls.forEach(function (url) {
            if (!addMemberDialogCommittedUrls[url]) {
                URL.revokeObjectURL(url);
            }
        });
        addMemberDialogOwnedUrls = [];
    }

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

    function closeAddMemberDialog() {
        addMemberDialogEpoch += 1;
        const opener = addMemberDialogOpener;
        addMemberDialogOpener = null;
        destroyAddMemberDialog();
        focusAddMemberOpener(opener);
    }

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

    function isDestinationAnchor(row) {
        return Boolean(
            row &&
            row.header !== true &&
            (row.objectType === "system" || row.objectType === "group" || row.objectType === "place")
        );
    }

    function containingAnchor(snapshot) {
        const byKey = {};
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

    function destinationDisplayName(row) {
        if (row && row.anchorName) {
            return row.anchorName;
        }
        return row && row.nodeLabel ? row.nodeLabel : "";
    }

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

        function shape(tagName, attributes) {
            const node = document.createElementNS(svgNamespace, tagName);
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

    function createEditPencil() {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-edit-pencil-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "16");
        svg.setAttribute("height", "16");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        function addShape(tagName, attributes) {
            const node = document.createElementNS(svgNamespace, tagName);
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

    function dialogPencil() {
        const icon = createEditPencil();
        icon.classList.add("member-add-dialog-pencil-icon");
        return icon;
    }

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

    function customRelationshipId(label) {
        const slug = String(label || "").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "type";
        return "custom:" + slug + ":" + Math.random().toString(36).slice(2, 8);
    }

    function relationshipTypeChoices(accountId) {
        const choices = supportedRelationshipTypes.map(function (item) {
            return { id: item.id, label: item.label, icon: "link", disabled: false, custom: false };
        });
        const custom = accountId && customRelationshipTypesByAccount[accountId]
            ? customRelationshipTypesByAccount[accountId]
            : [];
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

    function paintInheritanceHint(hint, show, sourceName) {
        hint.textContent = sourceName ? "Inherited from " + sourceName : "\u00a0";
        const visible = Boolean(show && sourceName);
        hint.classList.toggle("is-quiet", !visible);
        hint.setAttribute("aria-hidden", visible ? "false" : "true");
    }

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

        function addShape(tagName, attributes) {
            const node = document.createElementNS(svgNamespace, tagName);
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

        function currentChoice() {
            const current = options && typeof options.getValue === "function" ? String(options.getValue() || "") : "";
            for (let index = 0; index < choices.length; index += 1) {
                if (choices[index].id === current) {
                    return choices[index];
                }
            }
            return choices[0] || { id: "", label: "Not set", disabled: false };
        }

        function paint() {
            const choice = currentChoice();
            value.textContent = choice.label;
            const accessible = (options && options.label ? options.label + ": " : "") + choice.label;
            button.setAttribute("aria-label", accessible);
            button.title = accessible;
            list.replaceChildren();
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

        function setOpen(open) {
            list.hidden = !open;
            button.setAttribute("aria-expanded", open ? "true" : "false");
            paint();
            if (!open) {
                return;
            }
            const selected = optionsEls.findIndex(function (option) {
                return option.getAttribute("aria-selected") === "true";
            });
            const index = selected < 0 ? 0 : selected;
            optionsEls[index].focus();
            place();
        }

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

        function keepLocal(event) {
            event.preventDefault();
            event.stopPropagation();
        }
        root.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        root.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        button.addEventListener("click", function (event) {
            keepLocal(event);
            setOpen(list.hidden);
        });
        button.addEventListener("keydown", function (event) {
            if (event.key === "ArrowDown" || event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                event.stopPropagation();
                setOpen(true);
            }
        });
        list.addEventListener("mousedown", function (event) {
            event.preventDefault();
            event.stopPropagation();
        });
        list.addEventListener("keydown", function (event) {
            event.stopPropagation();
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
        list.addEventListener("click", function (event) {
            const option = event.target.closest(".member-sharing-option");
            event.preventDefault();
            event.stopPropagation();
            if (option) {
                choose(option);
            }
        });
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

    function isModeDestination(row, mode) {
        if (!row || row.header === true) {
            return false;
        }
        if (row.objectType === "account") {
            return mode === "group" || mode === "system" || mode === "place";
        }
        return row.objectType === "system" || row.objectType === "group" || row.objectType === "place";
    }

    function createTreeExpansion(rows) {
        const children = {};
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
        Object.keys(children).forEach(function (key) {
            const list = children[key];
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

    function treeChildren(expansion, pathKey) {
        return pathKey && expansion.children[pathKey] ? expansion.children[pathKey] : [];
    }

    function treeFullyOpen(expansion) {
        const keys = Object.keys(expansion.children);
        for (let index = 0; index < keys.length; index += 1) {
            const key = keys[index];
            const list = expansion.children[key];
            const hasStructure = list.some(function (row) {
                return row.objectType !== "member";
            });
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

    function setTreeFullyOpen(expansion, open) {
        Object.keys(expansion.children).forEach(function (key) {
            expansion.structureOpen[key] = open;
            expansion.membersOpen[key] = open;
        });
    }

    function renderDialogTree(host, rows, expansion, options) {
        const scrollTop = host.scrollTop;
        const active = document.activeElement;
        const restorePath = active && host.contains(active) ? active.getAttribute("data-path-key") : "";
        const restoreToggle = Boolean(active && active.classList.contains("member-system-tree-toggle"));
        host.replaceChildren();
        const canSelect = options && typeof options.canSelect === "function"
            ? options.canSelect
            : function () { return true; };
        const selectedPathKey = options && options.selectedPathKey ? options.selectedPathKey : "";

        function notify() {
            if (options && typeof options.onChange === "function") {
                options.onChange();
            }
        }

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
            toggle.addEventListener("click", function () {
                expansion.membersOpen[parent.pathKey] = !open;
                notify();
            });
            nodeRow.appendChild(toggle);
            host.appendChild(nodeRow);
            if (open) {
                members.forEach(function (child) {
                    appendRow(child, level + 1);
                });
            }
        }

        function appendRow(row, level) {
            const list = treeChildren(expansion, row.pathKey);
            const structural = list.filter(function (child) {
                return child.objectType !== "member";
            });
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
            structural.forEach(function (child) {
                appendRow(child, level + 1);
            });
            if (members.length && structural.length) {
                appendMembersToggle(row, members, level + 1);
            } else if (members.length) {
                members.forEach(function (child) {
                    appendRow(child, level + 1);
                });
            }
        }

        const leading = options && options.leadingRows ? options.leadingRows : [];
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
        nameInput.addEventListener("input", function () {
            anchorNameEdited = true;
        });
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

        function paintFronting() {
            frontingButton.replaceChildren(dialogFrontingIcon(fronting));
            const label = document.createElement("span");
            label.textContent = fronting ? "Fronting" : "Not fronting";
            frontingButton.appendChild(label);
            frontingButton.setAttribute("aria-pressed", fronting ? "true" : "false");
            frontingButton.setAttribute("aria-label", fronting ? "Fronting" : "Not fronting");
        }

        function paintAvatar() {
            avatarFrame.replaceChildren();
            const avatars = window.PluralBridge.avatars;
            if (avatars && typeof avatars.paintFrame === "function") {
                avatars.paintFrame(avatarFrame, draftAvatarUrl, "No avatar selected");
                return;
            }
            avatarFrame.textContent = draftAvatarUrl ? "" : "No avatar selected";
        }

        function currentDestination() {
            return isModeDestination(selectedRow, mode) ? selectedRow : null;
        }

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

        function syncSave() {
            saveButton.disabled = !currentDestination();
        }

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

        function accountChanged() {
            const now = currentAccountId();
            if (openedAccountId && now !== openedAccountId) {
                closeAddMemberDialog();
                return true;
            }
            return false;
        }

        function paintTree() {
            renderDialogTree(tree, snapshot.rows, expansion, {
                leadingRows: accountRow && !memberOnly ? [accountRow] : [],
                selectedPathKey: selectedRow && selectedRow.pathKey ? selectedRow.pathKey : "",
                canSelect: function (row) {
                    return isModeDestination(row, mode);
                },
                onSelect: selectOccurrence,
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
            getValue: function () { return visibilityChoice; },
            setValue: function (next) {
                visibilityChoice = next || "inherit";
                visibilityOverridden = true;
                paintPolicy();
            }
        });
        const permissionMenu = createPolicyDropdown({
            label: "Permissions",
            choices: policyChoices("permission", false),
            getValue: function () { return permissionChoice; },
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

        expandAllButton.addEventListener("click", function () {
            if (accountChanged()) {
                return;
            }
            setTreeFullyOpen(expansion, !treeFullyOpen(expansion));
            syncExpandAll();
            paintTree();
        }, { signal: signal });

        frontingButton.addEventListener("click", function () {
            if (accountChanged()) {
                return;
            }
            fronting = !fronting;
            paintFronting();
        }, { signal: signal });

        closeButton.addEventListener("click", function () {
            closeAddMemberDialog();
        }, { signal: signal });
        cancelButton.addEventListener("click", function () {
            closeAddMemberDialog();
        }, { signal: signal });

        colorPencil.addEventListener("click", function () {
            colorInput.click();
        }, { signal: signal });
        avatarPencil.addEventListener("click", function () {
            fileInput.click();
        }, { signal: signal });
        avatarFrame.addEventListener("click", function () {
            fileInput.click();
        }, { signal: signal });
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

        dialog.addEventListener("cancel", function (event) {
            event.preventDefault();
            closeAddMemberDialog();
        }, { signal: signal });

        window.addEventListener("focus", function () {
            accountChanged();
        }, { signal: signal });

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
        window.setTimeout(function () {
            if (nameInput.isConnected) {
                keepTypedAnchorName();
            }
        }, 0);
    }

    function relationshipObjectKey(row) {
        if (!row || row.header === true || !row.objectType || !row.objectId || row.objectType === "member") {
            return "";
        }
        return String(row.objectType).toLowerCase() + ":" + String(row.objectId).toLowerCase();
    }

    function anchorEndpoint(row) {
        return Boolean(
            row &&
            row.header !== true &&
            row.objectId &&
            (row.objectType === "system" || row.objectType === "group" || row.objectType === "place")
        );
    }

    function parentLinkWouldCycle(snapshot, parentRow, childRow) {
        const parentKey = relationshipObjectKey(parentRow);
        const childKey = relationshipObjectKey(childRow);
        if (!parentKey || !childKey || parentKey === childKey) {
            return parentKey !== "" && parentKey === childKey;
        }

        const byPath = {};
        (snapshot.rows || []).forEach(function (row) {
            if (row.pathKey) {
                byPath[row.pathKey] = row;
            }
        });
        const parentsOf = {};
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
            (parentsOf[key] || []).forEach(function (next) {
                stack.push(next);
            });
        }
        return false;
    }

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

        function endpointName(row) {
            if (!row) {
                return "";
            }
            return row.anchorName || row.nodeLabel || "";
        }

        function paintTree(host, side) {
            const selected = side === "left" ? leftRow : rightRow;
            renderDialogTree(host, snapshot.rows, side === "left" ? leftExpansion : rightExpansion, {
                selectedPathKey: selected && selected.pathKey ? selected.pathKey : "",
                memberTitle: "Members stay visible and cannot be relationship endpoints.",
                headingTitle: "This heading cannot be a relationship endpoint.",
                canSelect: function (row) {
                    return anchorEndpoint(row);
                },
                onSelect: function (row) {
                    if (side === "left") {
                        leftRow = row;
                    } else {
                        rightRow = row;
                    }
                    paint();
                },
                onChange: function () {
                    paintTree(host, side);
                },
                emptyText: "The signed-in account map is not available."
            });
        }

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
            getValue: function () { return relationshipType; },
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
            getValue: function () { return visibilityChoice; },
            setValue: function (next) {
                visibilityChoice = next || "inherit";
                paint();
            }
        });
        const permissionMenu = createPolicyDropdown({
            label: "Permissions",
            choices: policyChoices("permission", false),
            getValue: function () { return permissionChoice; },
            setValue: function (next) {
                permissionChoice = next || "inherit";
                paint();
            }
        });

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

        function paint() {
            paintTree(leftColumn.tree, "left");
            paintTree(rightColumn.tree, "right");
            const sourceName = endpointName(leftRow);
            paintInheritanceHint(visibilityHint, visibilityChoice === "inherit", sourceName);
            paintInheritanceHint(permissionHint, permissionChoice === "inherit", sourceName);
            visibilityMenu.refresh();
            permissionMenu.refresh();
            kindMenu.refresh();
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

        kindPencil.addEventListener("click", function () {
            typeEditor.hidden = false;
            typeStatus.textContent = "";
            typeInput.focus();
        }, { signal: signal });
        typeCancel.addEventListener("click", function () {
            typeEditor.hidden = true;
            typeStatus.textContent = "";
        }, { signal: signal });
        typeAdd.addEventListener("click", function () {
            rememberRelationshipType(typeInput.value);
        }, { signal: signal });
        typeInput.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                rememberRelationshipType(typeInput.value);
            }
        }, { signal: signal });

        closeButton.addEventListener("click", closeAddMemberDialog, { signal: signal });
        cancelButton.addEventListener("click", closeAddMemberDialog, { signal: signal });
        dialog.addEventListener("cancel", function (event) {
            event.preventDefault();
            closeAddMemberDialog();
        }, { signal: signal });
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

    function openAddMemberDialog(options) {
        const opener = options && options.opener instanceof Element ? options.opener : null;
        const requested = options && options.mode ? String(options.mode) : "member";
        const mode = requested === "group" || requested === "system" || requested === "place" || requested === "relationship"
            ? requested
            : "member";
        const scheduledEpoch = addMemberDialogEpoch;
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
        createMemberEditForm: createMemberEditForm
    };
})();
