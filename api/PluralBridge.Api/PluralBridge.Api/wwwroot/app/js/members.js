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

        if (iconName === "groups") {
            path.setAttribute("d", "M4 5h6l2 2h8v12H4V5zm2 4v8h12V9H6z");
        } else if (iconName === "addGroup") {
            path.setAttribute("d", "M4 5h6l2 2h8v5h-2V9H6v8h6v2H4V5zm13 8h2v3h3v2h-3v3h-2v-3h-3v-2h3v-3z");
        } else if (iconName === "addMember") {
            path.setAttribute("d", "M9 11a4 4 0 1 1 0-8 4 4 0 0 1 0 8zm0 2c-4 0-7 2-7 5v1h10.5a5.5 5.5 0 0 1 .6-6H9zm8 0h2v3h3v2h-3v3h-2v-3h-3v-2h3v-3z");
        } else if (iconName === "expanded") {
            path.setAttribute("d", "M4 5h16v4H4V5zm0 5.5h16v4H4v-4zM4 16h16v3H4v-3z");
        } else if (iconName === "compact") {
            path.setAttribute("d", "M5 6h14v2H5V6zm0 5h14v2H5v-2zm0 5h14v2H5v-2z");
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

    function createMemberToolbar(options) {
        const toolbar = document.createElement("div");
        toolbar.className = "member-toolbar";
        toolbar.setAttribute("aria-label", "Member toolbar");

        const primaryGroup = document.createElement("div");
        primaryGroup.className = "member-toolbar-group";

        const viewGroup = document.createElement("div");
        viewGroup.className = "member-toolbar-group member-toolbar-view-group";

        primaryGroup.appendChild(createToolbarButton("groups", "Groups", {
            disabled: true,
            title: "Groups view planned"
        }));

        primaryGroup.appendChild(createToolbarButton("addGroup", "Add group", {
            disabled: true,
            title: "Add group planned"
        }));

        primaryGroup.appendChild(createToolbarButton("addMember", "Add member", {
            onClick: function () {
                if (options && typeof options.toggleAddMember === "function") {
                    options.toggleAddMember();
                }
            }
        }));

        viewGroup.appendChild(createToolbarButton("expanded", "Expanded", {
            disabled: true,
            title: "Expanded member view planned"
        }));

        viewGroup.appendChild(createToolbarButton("compact", "Compact", {
            disabled: true,
            title: "Compact member view planned"
        }));

        viewGroup.appendChild(createToolbarButton("details", "Details", {
            disabled: true,
            title: "Member details view planned"
        }));

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

            const apiClient = window.PluralBridge && window.PluralBridge.apiClient
                ? window.PluralBridge.apiClient
                : null;

            if (!apiClient || typeof apiClient.addMember !== "function") {
                status.textContent = "Member add is not available in the current browser session.";
                return;
            }

            const request = buildMemberRequest(displayNameField.input, pronounsField.input, descriptionField.input);

            if (!request.displayName) {
                status.textContent = "Display name is required.";
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

    return {
        createMemberToolbar: createMemberToolbar,
        createMemberAddForm: createMemberAddForm,
        createMemberEditForm: createMemberEditForm
    };
})();