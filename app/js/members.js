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
        textarea.rows = 3;

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

        const submitButton = document.createElement("button");
        submitButton.type = "submit";
        submitButton.textContent = "Add member";

        const status = createStatusMessage();

        buttonRow.appendChild(submitButton);

        form.appendChild(displayNameField.row);
        form.appendChild(pronounsField.row);
        form.appendChild(descriptionField.row);
        form.appendChild(buttonRow);
        form.appendChild(status);

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

        const saveButton = document.createElement("button");
        saveButton.type = "submit";
        saveButton.textContent = "Save";

        const cancelButton = document.createElement("button");
        cancelButton.type = "button";
        cancelButton.textContent = "Cancel";

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
        createMemberAddForm: createMemberAddForm,
        createMemberEditForm: createMemberEditForm
    };
})();