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
        popover.appendChild(createAddOption("Place", "place", false, null));
        popover.appendChild(createAddOption("System", "system", false, null));

        const relationshipSeparator = document.createElement("div");
        relationshipSeparator.className = "member-toolbar-add-separator";
        relationshipSeparator.setAttribute("role", "separator");
        popover.appendChild(relationshipSeparator);
        popover.appendChild(createAddOption("Relationship", "relationship", false, null));

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

    async function holdAccountPreview(kind, request) {
        const isGroup = kind === "group";
        const previewNoun = isGroup ? "group" : "member";

        if (!databaseWritesAreDisabled()) {
            return {
                status: isGroup
                    ? "Group creation is not connected to an anchor-specific save endpoint yet."
                    : "Member creation is not connected to an anchor-specific save endpoint yet."
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
        const record = isGroup
            ? {
                previewKind: "group",
                objectType: "group",
                displayName: request.displayName,
                color: request.color ? String(request.color) : "",
                description: request.description || "",
                destination: destination
            }
            : {
                previewKind: "member",
                objectType: "member",
                displayName: request.displayName,
                pronouns: request.pronouns || "",
                description: request.description || "",
                color: request.color ? String(request.color) : "",
                fronting: request.fronting === true,
                avatarObjectUrl: request.avatarObjectUrl ? String(request.avatarObjectUrl) : "",
                destination: destination
            };

        memberPreviewsByAccount[accountId].push(record);

        console.info(
            isGroup
                ? "Suppressed add-group database write. Group details were held in browser memory for the signed-in account. No profile fields were written to the log."
                : "Suppressed add-member database write. Member details were held in browser memory for the signed-in account. No profile fields were written to the log."
        );

        return {
            status: isGroup
                ? "Preview only: group details held in browser memory. Nothing was saved to the database."
                : "Preview only: member details held in browser memory. Nothing was saved to the database.",
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

    function assignVisibilityPreview(record, previous, fields) {
        const incoming = Boolean(fields)
            && Object.prototype.hasOwnProperty.call(fields, "visibilityTypeId");
        const retained = !incoming && Boolean(previous) && previous.visibilityExplicit === true;
        if (!incoming && !retained) {
            return;
        }

        const source = incoming ? fields : previous;
        record.visibilityExplicit = true;
        record.visibilityTypeId = canonicalVisibilityId(source.visibilityTypeId);
    }

    function explicitPreviewAvatar(value) {
        return typeof value === "string" && value ? value : null;
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

        const record = {
            displayName: preview.displayName,
            pronouns: preview.pronouns,
            description: preview.description
        };
        assignExplicitPreview(record, preview, null, "color", explicitPreviewColor);
        assignExplicitPreview(record, preview, null, "fronting", explicitPreviewFronting);
        assignVisibilityPreview(record, preview, null);
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
        assignExplicitPreview(record, previous, fields, "color", explicitPreviewColor);
        assignExplicitPreview(record, previous, fields, "fronting", explicitPreviewFronting);
        assignVisibilityPreview(record, previous, fields);
        assignExplicitPreview(record, previous, fields, "avatarObjectUrl", explicitPreviewAvatar);
        memberProfilePreviewsByAccount[accountKey][memberKey] = record;

        console.info("Suppressed member-profile database write. Profile details were held in browser memory for the signed-in account. No profile fields were written to the log.");

        return {
            saved: true,
            status: "Preview only: member profile held in browser memory. Nothing was saved to the database."
        };
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

    function dialogPencil() {
        const icon = dialogSvg("M4 17.3V21h3.7L18.8 9.9l-3.7-3.7L4 17.3zM20.7 8c.4-.4.4-1 0-1.4l-2.3-2.3c-.4-.4-1-.4-1.4 0l-1.2 1.2 3.7 3.7L20.7 8z");
        icon.setAttribute("class", "member-add-dialog-pencil-icon");
        icon.setAttribute("width", "16");
        icon.setAttribute("height", "16");
        return icon;
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

    function presentAddMemberDialog(opener, addMode) {
        const isGroup = addMode === "group";
        const snapshot = readMapSnapshot();
        const openedAccountId = snapshot.accountId || currentAccountId();
        addMemberDialogOpener = opener;
        const controller = new AbortController();
        addMemberDialogController = controller;
        const signal = controller.signal;
        const expanded = {};
        snapshot.rows.forEach(function (row) {
            if (row.parentPathKey) {
                expanded[row.parentPathKey] = true;
            }
        });

        let selectedRow = containingAnchor(snapshot);
        let fronting = false;
        let draftAvatarUrl = "";

        const dialog = document.createElement("dialog");
        dialog.className = "member-add-dialog";
        dialog.setAttribute("data-add-mode", isGroup ? "group" : "member");
        dialog.setAttribute("aria-labelledby", "member-add-overlay-title");
        addMemberDialog = dialog;

        const title = document.createElement("h2");
        title.id = "member-add-overlay-title";
        title.className = "member-add-dialog-title";
        title.textContent = isGroup ? "Add Group" : "Add Member";

        const layout = document.createElement("div");
        layout.className = "member-add-dialog-layout";

        const side = document.createElement("div");
        side.className = "member-add-dialog-side";

        const sideHeading = document.createElement("div");
        sideHeading.className = "member-add-dialog-side-heading";

        const sideLabel = document.createElement("p");
        sideLabel.className = "member-add-dialog-side-label";
        sideLabel.textContent = isGroup ? "Add group to:" : "Add member to:";

        const expandAllButton = document.createElement("button");
        expandAllButton.type = "button";
        expandAllButton.className = "member-add-dialog-expand-all member-systems-expand-all";
        sideHeading.appendChild(sideLabel);
        sideHeading.appendChild(expandAllButton);

        const tree = document.createElement("div");
        tree.className = "member-add-dialog-tree member-systems-tree";
        tree.setAttribute("role", "group");
        tree.setAttribute("aria-label", "Add member to");

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
        side.appendChild(actions);

        const main = document.createElement("div");
        main.className = "member-add-dialog-main";

        const toolbar = document.createElement("div");
        toolbar.className = "member-add-dialog-toolbar";

        const frontingButton = document.createElement("button");
        frontingButton.type = "button";
        frontingButton.className = "member-add-dialog-fronting member-add-dialog-member-only";

        const closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "member-add-dialog-close";
        closeButton.setAttribute("aria-label", "Close");
        closeButton.textContent = "×";

        toolbar.appendChild(frontingButton);
        toolbar.appendChild(closeButton);

        const typeLine = document.createElement("p");
        typeLine.className = "member-add-dialog-type";
        typeLine.appendChild(document.createTextNode("Type : "));
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
        nameInput.placeholder = "Use @ for root";

        const pronounsInput = document.createElement("input");
        pronounsInput.id = "member-add-overlay-pronouns";
        pronounsInput.type = "text";
        pronounsInput.autocomplete = "off";
        pronounsInput.placeholder = "Use @ for root";

        const descriptionInput = document.createElement("textarea");
        descriptionInput.id = "member-add-overlay-description";
        descriptionInput.rows = 6;

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
            if (!draftAvatarUrl) {
                const empty = document.createElement("span");
                empty.className = "member-add-dialog-avatar-empty";
                empty.textContent = "No avatar selected";
                avatarFrame.appendChild(empty);
                return;
            }

            const image = document.createElement("img");
            image.className = "member-add-dialog-avatar-image";
            image.alt = "";
            image.src = draftAvatarUrl;
            avatarFrame.appendChild(image);
        }

        function currentDestination() {
            return isDestinationAnchor(selectedRow) ? selectedRow : null;
        }

        function syncSave() {
            saveButton.disabled = !currentDestination();
        }

        function expandablePathKeys() {
            const keys = {};
            snapshot.rows.forEach(function (row) {
                if (row.parentPathKey) {
                    keys[row.parentPathKey] = true;
                }
            });
            return Object.keys(keys);
        }

        function allOccurrencesExpanded() {
            const keys = expandablePathKeys();
            for (let index = 0; index < keys.length; index += 1) {
                if (expanded[keys[index]] !== true) {
                    return false;
                }
            }
            return true;
        }

        function syncExpandAll() {
            const collapseAll = allOccurrencesExpanded();
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
            if (selectedRow && selectedRow.pathKey === row.pathKey) {
                syncSave();
                return;
            }
            selectedRow = row;
            paintDestination();
            syncSave();
            paintTree();
        }

        function accountChanged() {
            const now = currentAccountId();
            if (openedAccountId && now !== openedAccountId) {
                closeAddMemberDialog();
                return true;
            }
            return false;
        }

        function childRows(pathKey) {
            return snapshot.rows.filter(function (row) {
                return row.parentPathKey === pathKey;
            });
        }

        function appendChoice(row, level) {
            const children = childRows(row.pathKey);
            const hasChildren = children.length > 0;
            const isExpanded = expanded[row.pathKey] === true;
            const nodeRow = document.createElement("div");
            nodeRow.className = "member-system-node-row";
            nodeRow.style.setProperty("--system-node-level", String(level));

            if (hasChildren) {
                const expandButton = document.createElement("button");
                expandButton.type = "button";
                expandButton.className = "member-system-tree-toggle";
                expandButton.setAttribute("data-path-key", row.pathKey);
                expandButton.setAttribute("aria-expanded", String(isExpanded));
                expandButton.setAttribute("aria-label", isExpanded ? "Collapse" : "Expand");
                expandButton.appendChild(dialogChevron(isExpanded));
                expandButton.addEventListener("click", function () {
                    if (accountChanged()) {
                        return;
                    }
                    expanded[row.pathKey] = !isExpanded;
                    syncExpandAll();
                    paintTree();
                });
                nodeRow.appendChild(expandButton);
            } else {
                const spacer = document.createElement("span");
                spacer.className = "member-system-tree-spacer";
                nodeRow.appendChild(spacer);
            }

            const iconType = row.objectType === "system" || row.objectType === "group" || row.objectType === "place" || row.objectType === "member"
                ? row.objectType
                : "";
            const content = document.createElement("span");
            content.className = "member-tree-node-content";
            const icon = dialogOutlineIcon(iconType);
            if (icon) {
                content.appendChild(icon);
            }
            const text = document.createElement("span");
            text.className = "member-tree-node-text";
            text.textContent = row.nodeLabel || "";
            content.appendChild(text);

            const selectButton = document.createElement("button");
            selectButton.type = "button";
            selectButton.className = "member-system-node-button";
            selectButton.setAttribute("data-path-key", row.pathKey);
            if (selectedRow && selectedRow.pathKey === row.pathKey) {
                selectButton.classList.add("is-active");
            }
            if (isDestinationAnchor(row)) {
                if (selectedRow && selectedRow.pathKey === row.pathKey) {
                    selectButton.setAttribute("aria-current", "true");
                }
            } else {
                selectButton.classList.add("is-not-destination");
                selectButton.title = row.header
                    ? "This heading cannot be the destination."
                    : "Members stay visible and cannot be the destination.";
            }
            selectButton.appendChild(content);
            selectButton.addEventListener("focus", function () {
                selectOccurrence(row);
            });
            selectButton.addEventListener("click", function () {
                selectOccurrence(row);
            });
            nodeRow.appendChild(selectButton);

            tree.appendChild(nodeRow);
            if (hasChildren && isExpanded) {
                children.forEach(function (child) {
                    appendChoice(child, level + 1);
                });
            }
        }

        function paintTree() {
            const scrollTop = tree.scrollTop;
            const active = document.activeElement;
            const restorePath = active && tree.contains(active) ? active.getAttribute("data-path-key") : "";
            const restoreToggle = Boolean(active && active.classList.contains("member-system-tree-toggle"));
            tree.replaceChildren();
            if (snapshot.rows.length === 0) {
                const empty = document.createElement("p");
                empty.className = "member-add-dialog-tree-empty";
                empty.textContent = "The signed-in account map is not available.";
                tree.appendChild(empty);
                syncExpandAll();
                return;
            }

            snapshot.rows.forEach(function (row) {
                if (row.parentPathKey == null) {
                    appendChoice(row, 0);
                }
            });
            tree.scrollTop = scrollTop;
            syncExpandAll();
            if (!restorePath) {
                return;
            }

            const selector = restoreToggle
                ? ".member-system-tree-toggle"
                : ".member-system-node-button";
            const buttons = tree.querySelectorAll(selector);
            for (let index = 0; index < buttons.length; index += 1) {
                if (buttons[index].getAttribute("data-path-key") === restorePath) {
                    buttons[index].focus();
                    break;
                }
            }
        }

        const form = document.createElement("form");
        form.className = "member-add-dialog-form";
        form.appendChild(title);
        form.appendChild(layout);
        layout.appendChild(side);
        layout.appendChild(main);
        const identity = document.createElement("div");
        identity.className = "member-add-dialog-identity";
        identity.appendChild(destinationName);
        identity.appendChild(media);

        main.appendChild(toolbar);
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
        dialog.appendChild(form);

        paintFronting();
        paintAvatar();
        paintDestination();
        syncSave();
        paintTree();

        expandAllButton.addEventListener("click", function () {
            if (accountChanged()) {
                return;
            }
            const keys = expandablePathKeys();
            const expand = !allOccurrencesExpanded();
            keys.forEach(function (pathKey) {
                expanded[pathKey] = expand;
            });
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
            draftAvatarUrl = URL.createObjectURL(file);
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
                status.textContent = isGroup
                    ? "Select a system, group, or place before saving this group."
                    : "Select a system, group, or place before saving this member.";
                syncSave();
                return;
            }

            if (!currentAccountId()) {
                status.textContent = "The signed-in account is not available, so this " + (isGroup ? "group" : "member") + " preview was not stored.";
                return;
            }

            saveButton.disabled = true;
            const previewRequest = {
                displayName: displayName,
                description: descriptionInput.value.trim(),
                color: colorInput.value,
                destination: {
                    objectType: destination.objectType,
                    objectId: destination.objectId,
                    pathKey: destination.pathKey,
                    anchorName: destination.anchorName || ""
                }
            };
            if (!isGroup) {
                previewRequest.pronouns = pronounsInput.value.trim();
                previewRequest.fronting = fronting;
                previewRequest.avatarObjectUrl = draftAvatarUrl;
            }
            try {
                const outcome = await holdAccountPreview(isGroup ? "group" : "member", previewRequest);
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
        if (currentDestination()) {
            nameInput.focus();
        }
    }

    function openAddMemberDialog(options) {
        const opener = options && options.opener instanceof Element ? options.opener : null;
        const mode = options && options.mode === "group" ? "group" : "member";
        const scheduledEpoch = addMemberDialogEpoch;
        window.setTimeout(function () {
            if (scheduledEpoch !== addMemberDialogEpoch) {
                return;
            }
            destroyAddMemberDialog();
            presentAddMemberDialog(opener, mode);
        }, 0);
    }

    return {
        createMemberToolbar: createMemberToolbar,
        releaseAddMenuHandlers: releaseAddMenuHandlers,
        setTreeToggleVisible: setTreeToggleVisible,
        createMemberAddForm: createMemberAddForm,
        openAddMemberDialog: openAddMemberDialog,
        closeAddMemberDialog: closeAddMemberDialog,
        previewAddMember: previewAddMember,
        readMemberProfilePreview: readMemberProfilePreview,
        saveMemberProfilePreview: saveMemberProfilePreview,
        createMemberEditForm: createMemberEditForm
    };
})();
