function createMemberAccordion(member, options) {
    const allowProfileEditing = !options || options.allowProfileEditing !== false;
    function hashStringToInt(value) {
        let hash = 0;

        for (let index = 0; index < value.length; index++) {
            hash = (hash * 31 + value.charCodeAt(index)) | 0;
        }

        return Math.abs(hash);
    }

    function getAvatarColorSeed() {
        if (member && member.memberId) {
            return String(member.memberId);
        }

        if (member && member.displayName) {
            return String(member.displayName);
        }

        return "member";
    }

    function sharingOutlineIcon(kind) {
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

        if (kind === "only-me") {
            addShape("path", { d: "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" });
            addShape("circle", { cx: "12", cy: "7", r: "4" });
        } else if (kind === "selected-people") {
            addShape("path", { d: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" });
            addShape("circle", { cx: "9", cy: "7", r: "4" });
            addShape("path", { d: "M22 21v-2a4 4 0 0 0-3-3.87" });
            addShape("path", { d: "M16 3.13a4 4 0 0 1 0 7.75" });
        } else if (kind === "all-connections") {
            addShape("path", { d: "M8.59 13.51 15.42 17.49" });
            addShape("path", { d: "M15.41 6.51 8.59 10.49" });
            addShape("circle", { cx: "18", cy: "5", r: "3" });
            addShape("circle", { cx: "6", cy: "12", r: "3" });
            addShape("circle", { cx: "18", cy: "19", r: "3" });
        } else if (kind === "inherit") {
            addShape("path", { d: "M7 7h8a4 4 0 0 1 0 8H9" });
            addShape("path", { d: "M11 13 8 16l3 3" });
        } else if (kind === "chevron") {
            svg.setAttribute("width", "16");
            svg.setAttribute("height", "16");
            svg.setAttribute("stroke-width", "1.75");
            addShape("path", { d: "M9 5.5 16.5 12 9 18.5" });
        } else {
            addShape("circle", { cx: "12", cy: "12", r: "9" });
            addShape("path", { d: "M8 12h8" });
        }

        return svg;
    }
    function createMemberActionIcon(iconName) {
        const svgNamespace = "http://www.w3.org/2000/svg";

        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-toggle-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "16");
        svg.setAttribute("height", "16");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        const path = document.createElementNS(svgNamespace, "path");
        path.setAttribute("fill", "currentColor");

        if (iconName === "json") {
            path.setAttribute("d", "M8.6 7.4 4 12l4.6 4.6L7.2 18 1.2 12l6-6 1.4 1.4zm6.8 0L16.8 6l6 6-6 6-1.4-1.4L20 12l-4.6-4.6zM13.2 5h2L10.8 19h-2l4.4-14z");
        } else if (iconName === "save") {
            path.setAttribute("d", "M17 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V7l-4-4zM7 5h8v5H7V5zm10 14H7v-6h10v6z");
        } else if (window.PluralBridge.members && typeof window.PluralBridge.members.createEditPencil === "function") {
            return window.PluralBridge.members.createEditPencil();
        } else {
            path.setAttribute("d", "M4 17.3V21h3.7L18.8 9.9l-3.7-3.7L4 17.3zM20.7 8c.4-.4.4-1 0-1.4l-2.3-2.3c-.4-.4-1-.4-1.4 0l-1.2 1.2 3.7 3.7L20.7 8z");
        }

        svg.appendChild(path);
        return svg;
    }

    function getMemberIdForSave() {
        return member && member.memberId ? member.memberId : "";
    }

    function getProfilePlaceholder(fieldName) {
        if (fieldName === "displayName") {
            return "(unnamed member)";
        }

        if (fieldName === "pronouns") {
            return "Not set";
        }

        return "No description available.";
    }

    function getProfileDisplayText(fieldName) {
        const value = draftValues[fieldName];

        if (value) {
            return value;
        }

        return getProfilePlaceholder(fieldName);
    }

    function setProfileStatus(message, statusType) {
        profileStatus.classList.remove("is-error");
        profileStatus.classList.remove("is-success");
        profileStatus.textContent = message || "";

        if (statusType === "error") {
            profileStatus.classList.add("is-error");
        }

        if (statusType === "success") {
            profileStatus.classList.add("is-success");
        }
    }

    function updateSummaryPreview() {
        name.textContent = allowProfileEditing
            ? getProfileDisplayText("displayName")
            : (draftValues.displayName || "");
        pronouns.textContent = draftValues.pronouns || "";
    }

    function updateStaticFieldDisplay(fieldName) {
        const rowInfo = fieldRows[fieldName];

        if (!rowInfo) {
            return;
        }

        rowInfo.value.textContent = allowProfileEditing
            ? getProfileDisplayText(fieldName)
            : (draftValues[fieldName] || "");
        rowInfo.value.dataset.draftValue = draftValues[fieldName] || "";
    }

    function updateAllStaticFieldDisplays() {
        Object.keys(fieldRows).forEach(function (fieldName) {
            updateStaticFieldDisplay(fieldName);
        });

        updateSummaryPreview();
        updateSaveButtonState();
    }

    function changeTracking() {
        return window.PluralBridge && window.PluralBridge.changeTracking
            ? window.PluralBridge.changeTracking
            : null;
    }

    function cardIsDirty() {
        const changes = changeTracking();
        return !!(changes && changes.dirty(cardDetails));
    }

    function modifiedMarker() {
        const changes = changeTracking();
        return changes ? changes.modifiedClass : "pb-control-modified";
    }

    function notifyEdited(control) {
        const changes = changeTracking();
        if (!changes || !control) {
            return;
        }

        changes.notify(control);
    }

    function renderChangeState() {
        const dirty = cardIsDirty();
        cardDetails.classList.remove("member-card-unsaved");
        if (unsavedIndicator) {
            unsavedIndicator.hidden = !dirty;
        }
        updateSaveButtonState();
    }

    function withoutChangeNotices(work) {
        const changes = changeTracking();
        if (!changes) {
            work();
            return;
        }

        changes.suppress(cardDetails, work);
    }

    let cancelButton = null;

    function updateSaveButtonState() {
        const dirty = cardIsDirty();
        if (cancelButton) {
            cancelButton.hidden = !dirty;
            cancelButton.disabled = isSaving;
        }

        if (!saveButton) {
            return;
        }

        const nameMissing = !String(draftValues.displayName || "").trim();
        saveButton.disabled = isSaving || !dirty || nameMissing;
        saveButton.title = isSaving
            ? "Saving profile changes"
            : nameMissing
                ? "Name is required."
                : dirty
                    ? "Save profile changes"
                    : "No profile changes to save";
    }

    function closeActiveEditor(applyChange) {
        if (!activeEdit) {
            return;
        }

        const editState = activeEdit;
        activeEdit = null;

        if (applyChange) {
            draftValues[editState.fieldName] = editState.editor.value.trim();
            setProfileStatus("");
        }

        updateStaticFieldDisplay(editState.fieldName);

        if (editState.editor.parentNode) {
            editState.editor.replaceWith(editState.displayElement);
        }

        if (editState.editor.classList.contains(modifiedMarker())) {
            editState.displayElement.classList.add(modifiedMarker());
        }

        editState.row.classList.remove("is-active");
        editState.editButton.disabled = isSaving;

        updateSummaryPreview();
        updateSaveButtonState();
    }

    function fitLongEditor(editor) {
        if (!editor || editor.tagName !== "TEXTAREA" || !editor.isConnected) {
            return;
        }

        editor.style.height = "auto";
        editor.style.height = editor.scrollHeight + "px";
    }

    function createEditorForField(fieldName, isLongText) {
        let editor = null;

        if (isLongText) {
            editor = document.createElement("textarea");
            editor.className = "member-profile-field-editor member-profile-field-textarea";
        } else {
            editor = document.createElement("input");
            editor.type = "text";
            editor.className = "member-profile-field-editor member-profile-field-input";
            editor.autocomplete = "off";
        }

        editor.value = draftValues[fieldName] || "";
        editor.setAttribute("aria-label", "Edit " + fieldLabels[fieldName]);
        const fieldLabel = fieldRows[fieldName] && fieldRows[fieldName].row.querySelector(".member-profile-field-label");
        if (fieldLabel && fieldLabel.id) {
            editor.setAttribute("aria-labelledby", fieldLabel.id);
        }

        editor.addEventListener("input", function () {
            draftValues[fieldName] = editor.value;
            if (isLongText) {
                fitLongEditor(editor);
            }
            updateSaveButtonState();
        });

        editor.addEventListener("blur", function () {
            if (isLongText) {
                return;
            }
            closeActiveEditor(true);
        });

        editor.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                event.preventDefault();
                closeActiveEditor(false);
                return;
            }

            if (!isLongText && event.key === "Enter") {
                event.preventDefault();
                closeActiveEditor(true);
            }
        });

        return editor;
    }

    function startEditingField(fieldName) {
        const rowInfo = fieldRows[fieldName];

        if (!rowInfo || isSaving) {
            return;
        }

        if (activeEdit && activeEdit.fieldName === fieldName) {
            activeEdit.editor.focus();
            return;
        }

        closeActiveEditor(true);

        const editor = createEditorForField(fieldName, rowInfo.isLongText);
        const changes = changeTracking();
        if (changes) {
            changes.rebind(rowInfo.value, editor);
        }
        if (rowInfo.value.classList.contains(modifiedMarker())) {
            editor.classList.add(modifiedMarker());
        }

        rowInfo.row.classList.add("is-active");
        rowInfo.editButton.disabled = true;
        rowInfo.value.replaceWith(editor);
        if (rowInfo.isLongText) {
            fitLongEditor(editor);
        }

        activeEdit = {
            fieldName: fieldName,
            row: rowInfo.row,
            displayElement: rowInfo.value,
            editor: editor,
            editButton: rowInfo.editButton
        };

        window.setTimeout(function () {
            editor.focus();
            editor.select();
        }, 0);
    }

    function createMemberProfileFieldRow(labelText, fieldName, rawValue, isLongText) {
        const row = document.createElement("div");
        row.className = "member-profile-field-row";
        row.dataset.memberProfileField = fieldName;

        const label = document.createElement("span");
        label.className = "member-profile-field-label";
        label.id = "member-field-" + window.PluralBridge.memberSharingSequence + "-" + fieldName;
        label.textContent = labelText;

        const value = document.createElement("span");
        value.className = "member-profile-field-value";
        value.dataset.memberProfileValue = fieldName;
        value.dataset.persistedValue = rawValue || "";
        value.dataset.draftValue = rawValue || "";
        value.setAttribute("data-change-control", "");
        value.setAttribute("aria-labelledby", label.id);
        value.textContent = allowProfileEditing
            ? (rawValue || getProfilePlaceholder(fieldName))
            : (rawValue || "");

        if (isLongText) {
            value.classList.add("member-profile-field-value-long");
        }

        const editButton = document.createElement("button");
        editButton.type = "button";
        editButton.className = "member-profile-field-edit-button";
        editButton.dataset.memberProfileEdit = fieldName;
        editButton.title = "Edit " + labelText;
        editButton.setAttribute("aria-label", "Edit " + labelText);

        editButton.appendChild(createMemberActionIcon("edit"));

        editButton.addEventListener("click", function () {
            startEditingField(fieldName);
        });

        if (allowProfileEditing) {
            value.addEventListener("dblclick", function (event) {
                event.preventDefault();
                event.stopPropagation();
                startEditingField(fieldName);
            });
        }

        row.appendChild(label);
        row.appendChild(value);
        if (allowProfileEditing) {
            row.appendChild(editButton);
        }

        fieldRows[fieldName] = {
            row: row,
            value: value,
            editButton: editButton,
            isLongText: isLongText
        };

        return row;
    }

    function createProfileSaveButton() {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "member-profile-save-button";
        button.disabled = false;
        button.title = "Save profile changes";

        button.appendChild(createMemberActionIcon("save"));

        const label = document.createElement("span");
        label.textContent = "Save";

        button.appendChild(label);

        let savePointerHeld = false;

        function rememberOpenCard() {
            pendingSavedCardView = captureCardView();
            keepSavedCardOpen = true;
        }

        function releaseUnusedSaveHold() {
            window.removeEventListener("pointerup", releaseUnusedSaveHold, true);
            window.removeEventListener("pointercancel", releaseUnusedSaveHold, true);
            window.setTimeout(function () {
                if (!savePointerHeld) {
                    return;
                }

                savePointerHeld = false;
                keepSavedCardOpen = false;
                pendingSavedCardView = null;
            }, 0);
        }

        button.addEventListener("pointerdown", function (event) {
            if (event.button !== 0) {
                return;
            }

            event.stopPropagation();
            savePointerHeld = true;
            rememberOpenCard();
            window.addEventListener("pointerup", releaseUnusedSaveHold, true);
            window.addEventListener("pointercancel", releaseUnusedSaveHold, true);
        });

        button.addEventListener("keydown", function (event) {
            if (event.key !== "Enter" && event.key !== " ") {
                return;
            }

            event.stopPropagation();
            savePointerHeld = true;
            rememberOpenCard();
        });

        button.addEventListener("keyup", function (event) {
            if (event.key === "Enter" || event.key === " ") {
                releaseUnusedSaveHold();
            }
        });

        button.addEventListener("click", async function (event) {
            event.preventDefault();
            event.stopPropagation();
            savePointerHeld = false;
            if (isSaving) {
                return;
            }
            const view = pendingSavedCardView || captureCardView();
            const holdToken = ++savedCardHoldToken;
            pendingSavedCardView = null;
            activeSavedCardView = view;
            view.open = true;
            keepSavedCardOpen = true;
            restoreCardView(view);

            try {
                await saveProfileChanges();
            } finally {
                window.setTimeout(function () {
                    if (holdToken !== savedCardHoldToken) {
                        return;
                    }

                    restoreCardView(view);
                    window.setTimeout(function () {
                        if (holdToken !== savedCardHoldToken) {
                            return;
                        }

                        restoreCardView(view);
                        keepSavedCardOpen = false;
                        pendingSavedCardView = null;
                        if (activeSavedCardView === view) {
                            activeSavedCardView = null;
                        }
                    }, 0);
                }, 0);
            }
        });

        return button;
    }

    function createProfileCancelButton() {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "member-profile-cancel-button";
        button.textContent = "Cancel";
        button.hidden = true;
        button.disabled = false;

        button.addEventListener("pointerdown", function (event) {
            event.stopPropagation();
        });

        button.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
            if (isSaving || !cardIsDirty()) {
                return;
            }

            const view = captureCardView();
            view.open = true;
            const changes = changeTracking();
            if (changes) {
                changes.restore(cardDetails);
            }
            cardDetails.open = true;
            restoreCardView(view);
            window.setTimeout(function () {
                restoreCardView(view);
            }, 0);
        });

        return button;
    }

    function setProfileBusy(isBusy) {
        isSaving = isBusy;

        Object.keys(fieldRows).forEach(function (fieldName) {
            fieldRows[fieldName].editButton.disabled = isBusy;
        });

        if (activeEdit && activeEdit.editor) {
            activeEdit.editor.disabled = isBusy;
        }

        [frontingButton, sharingButton, colorPencil, colorInput, avatarPencil, avatarFile].forEach(function (control) {
            if (control) {
                control.disabled = isBusy;
            }
        });

        const permissionButton = cardDetails.querySelector(".member-policy-button");
        if (permissionButton) {
            permissionButton.disabled = isBusy;
        }

        const changes = changeTracking();
        if (changes) {
            changes.setSaving(cardDetails, isBusy);
        }

        updateSaveButtonState();
    }

    function buildMemberEditRequest() {
        return {
            displayName: draftValues.displayName.trim(),
            pronouns: draftValues.pronouns.trim(),
            description: draftValues.description.trim()
        };
    }

    function applySuccessfulSave() {
        if (!applySuccessfulSave.guarded) {
            applySuccessfulSave.guarded = true;
            try {
                withoutChangeNotices(function () {
                    applySuccessfulSave();
                });
            } finally {
                applySuccessfulSave.guarded = false;
            }
            return;
        }

        persistedValues.displayName = draftValues.displayName;
        persistedValues.pronouns = draftValues.pronouns;
        persistedValues.description = draftValues.description;
        persistedValues.color = draftValues.color;
        persistedValues.fronting = draftValues.fronting;
        persistedValues.visibilityKnown = draftValues.visibilityKnown;
        persistedValues.visibilityInherit = draftValues.visibilityInherit;
        persistedValues.visibilityTypeId = draftValues.visibilityTypeId;
        persistedValues.permission = draftValues.permission;
        persistedValues.avatarObjectUrl = draftValues.avatarObjectUrl;

        Object.keys(fieldRows).forEach(function (fieldName) {
            fieldRows[fieldName].value.dataset.persistedValue = persistedValues[fieldName] || "";
            fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
            updateStaticFieldDisplay(fieldName);
        });

        if (member) {
            member.displayName = persistedValues.displayName;
            member.pronouns = persistedValues.pronouns;
            member.description = persistedValues.description;
            if (previewHeader) {
                member.color = persistedValues.color;
                member.fronting = persistedValues.fronting;
                member.visibilityKnown = persistedValues.visibilityKnown;
                member.visibilityTypeId = persistedValues.visibilityTypeId;
                member.avatarObjectUrl = persistedValues.avatarObjectUrl;
            }
        }

        jsonBlock.textContent = JSON.stringify(member, null, 2);

        updateSummaryPreview();
        updateSaveButtonState();
        paintPreviewControls();
    }

    function applyPersistedProfileFields(profile) {
        if (!applyPersistedProfileFields.guarded) {
            applyPersistedProfileFields.guarded = true;
            try {
                withoutChangeNotices(function () {
                    applyPersistedProfileFields(profile);
                });
            } finally {
                applyPersistedProfileFields.guarded = false;
            }
            return;
        }

        const displayName = profile && profile.displayName ? String(profile.displayName) : "";
        const pronouns = profile && profile.pronouns ? String(profile.pronouns) : "";
        const description = profile && profile.description ? String(profile.description) : "";
        const color = storedMemberColor(profile && profile.color);

        persistedValues.displayName = displayName;
        persistedValues.pronouns = pronouns;
        persistedValues.description = description;
        persistedValues.color = color;
        draftValues.displayName = displayName;
        draftValues.pronouns = pronouns;
        draftValues.description = description;
        draftValues.color = color;

        ["displayName", "pronouns", "description"].forEach(function (fieldName) {
            if (!fieldRows[fieldName]) {
                return;
            }

            fieldRows[fieldName].value.dataset.persistedValue = persistedValues[fieldName] || "";
            fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
            updateStaticFieldDisplay(fieldName);
        });

        if (member) {
            member.displayName = displayName;
            member.pronouns = pronouns;
            member.description = description;
            member.color = color;
        }

        jsonBlock.textContent = JSON.stringify(member, null, 2);
        updateSummaryPreview();
        updateSaveButtonState();
        paintPreviewControls();
    }

    function markedControls() {
        const changes = changeTracking();
        return changes ? changes.modified(cardDetails) : [];
    }

    function controlIsMarked(control) {
        return !!(control && markedControls().indexOf(control) >= 0);
    }

    function savedProfileControls(before) {
        return before.filter(function (control) {
            return profileControls.indexOf(control) >= 0;
        });
    }

    function publishConfirmed(before, includeColor, includeLatchedExtras) {
        const changes = changeTracking();
        if (!changes) {
            return;
        }

        const fields = {
            displayName: persistedValues.displayName,
            pronouns: persistedValues.pronouns,
            description: persistedValues.description
        };
        if (includeColor) {
            fields.color = persistedValues.color;
        }
        if (includeLatchedExtras && before.indexOf(frontingButton) >= 0) {
            fields.fronting = persistedValues.fronting;
        }
        if (includeLatchedExtras && before.indexOf(sharingButton) >= 0) {
            fields.visibilityKnown = persistedValues.visibilityKnown;
            fields.visibilityInherit = persistedValues.visibilityInherit;
            fields.visibilityTypeId = persistedValues.visibilityTypeId;
        }
        if (includeLatchedExtras && permissionControl && before.indexOf(permissionControl) >= 0) {
            fields.permission = persistedValues.permission;
        }
        if (includeLatchedExtras && before.indexOf(avatarWrap) >= 0) {
            fields.avatarObjectUrl = persistedValues.avatarObjectUrl;
        }
        changes.publishShared(getMemberIdForSave(), fields, cardDetails);
    }

    async function saveProfileChanges() {
        closeActiveEditor(true);

        if (isSaving) {
            return { saved: false };
        }

        if (changeTracking() && !cardIsDirty()) {
            updateSaveButtonState();
            return { saved: false };
        }

        const memberId = getMemberIdForSave();

        if (!memberId) {
            const message = "Member edit is not available because this member has no member ID.";
            setProfileStatus(message, "error");
            return { saved: false, message: message };
        }

        const request = buildMemberEditRequest();

        if (!request.displayName) {
            const message = "Name is required.";
            setProfileStatus(message, "error");
            updateSaveButtonState();
            return { saved: false, message: message };
        }

        const before = markedControls().slice();
        const changes = changeTracking();
        const includeColor = before.indexOf(colorSwatch) >= 0;

        if (options && typeof options.saveProfile === "function") {
            setProfileBusy(true);
            setProfileStatus("Saving member.");

            try {
                const outcome = await options.saveProfile(profilePreviewSaveRequest(memberId, request));

                if (!cardDetails.isConnected) {
                    return { saved: false, message: "Member could not be saved." };
                }

                if (!outcome || outcome.saved !== true) {
                    const message = outcome && outcome.status
                        ? outcome.status
                        : "Member could not be saved. Confirm the API is running and try again.";
                    setProfileStatus(message, "error");
                    return { saved: false, message: message };
                }

                if (outcome.persisted === true) {
                    if (!outcome.profile) {
                        const message = "The member profile save did not return the saved values.";
                        setProfileStatus(message, "error");
                        return { saved: false, message: message };
                    }

                    applyPersistedProfileFields(outcome.profile);
                    if (changes) {
                        changes.commit(cardDetails, savedProfileControls(before));
                    }
                    publishConfirmed(before, true, false);
                } else {
                    applySuccessfulSave();
                    if (changes) {
                        changes.commit(cardDetails, before);
                    }
                    publishConfirmed(before, includeColor, true);
                }
                setProfileStatus(outcome.status || "Member saved.", "success");
                return { saved: true };
            } catch (error) {
                const message = "Member could not be saved. Confirm the API is running and try again.";
                setProfileStatus(message, "error");
                return { saved: false, message: message };
            } finally {
                setProfileBusy(false);
            }
        }

        const apiClient = window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;

        if (!apiClient || typeof apiClient.editMember !== "function") {
            const message = "Member edit is not available in the current browser session.";
            setProfileStatus(message, "error");
            return { saved: false, message: message };
        }

        setProfileBusy(true);
        setProfileStatus("Saving member.");

        try {
            await apiClient.editMember(memberId, request);
            withoutChangeNotices(function () {
                persistedValues.displayName = draftValues.displayName;
                persistedValues.pronouns = draftValues.pronouns;
                persistedValues.description = draftValues.description;
                ["displayName", "pronouns", "description"].forEach(function (fieldName) {
                    if (!fieldRows[fieldName]) {
                        return;
                    }
                    fieldRows[fieldName].value.dataset.persistedValue = persistedValues[fieldName] || "";
                    fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
                    updateStaticFieldDisplay(fieldName);
                });
                if (member) {
                    member.displayName = persistedValues.displayName;
                    member.pronouns = persistedValues.pronouns;
                    member.description = persistedValues.description;
                }
                updateSummaryPreview();
            });
            if (changes) {
                changes.commit(cardDetails, savedProfileControls(before).filter(function (control) {
                    return control !== colorSwatch;
                }));
            }
            publishConfirmed(before, false, false);
            setProfileStatus("Member saved.", "success");
            return { saved: true };
        } catch (error) {
            const message = "Member could not be saved. Confirm the API is running and try again.";
            setProfileStatus(message, "error");
            return { saved: false, message: message };
        } finally {
            setProfileBusy(false);
        }
    }

    function captureCardView() {
        const positions = [];
        let node = cardDetails.parentElement;
        while (node) {
            if (node.scrollHeight > node.clientHeight || node.scrollWidth > node.clientWidth) {
                positions.push({
                    node: node,
                    top: node.scrollTop,
                    left: node.scrollLeft
                });
            }
            node = node.parentElement;
        }
        if (document.scrollingElement) {
            positions.push({
                node: document.scrollingElement,
                top: document.scrollingElement.scrollTop,
                left: document.scrollingElement.scrollLeft
            });
        }
        return {
            open: cardDetails.open,
            positions: positions
        };
    }

    function restoreCardView(view) {
        if (!view || !cardDetails.isConnected) {
            return;
        }
        if (view.open) {
            cardDetails.open = true;
        }
        view.positions.forEach(function (position) {
            if (!position.node || !position.node.isConnected) {
                return;
            }
            position.node.scrollTop = position.top;
            position.node.scrollLeft = position.left;
        });
    }

    const previewHeader = Boolean(options && options.previewHeader === true);

    function nullablePreviewValue(value) {
        if (value === true || value === false) {
            return value;
        }

        if (value === undefined || value === null || value === "") {
            return null;
        }

        return value;
    }

    function memberCssColor(value) {
        return typeof value === "string" && /^#[0-9A-Fa-f]{6}$/.test(value) ? value : null;
    }

    function storedMemberColor(value) {
        if (typeof value !== "string") {
            return null;
        }

        const trimmed = value.trim();
        return trimmed ? trimmed : null;
    }

    function generatedMemberColor() {
        const hue = hashStringToInt(getAvatarColorSeed()) % 360;
        return {
            fill: "hsl(" + hue + " 52% 28%)",
            border: "hsl(" + hue + " 56% 48%)"
        };
    }

    function effectiveMemberColor() {
        const explicit = memberCssColor(draftValues.color);
        return explicit
            ? { fill: explicit, border: explicit, explicit: true }
            : Object.assign({ explicit: false }, generatedMemberColor());
    }

    function profilePreviewSaveRequest(memberId, request) {
        const payload = {
            memberId: memberId,
            displayName: request.displayName,
            pronouns: request.pronouns,
            description: request.description
        };
        const changes = changeTracking();
        const marked = markedControls();

        if (changes && marked.indexOf(colorSwatch) >= 0) {
            payload.color = draftValues.color;
        }

        if (changes && marked.indexOf(frontingButton) >= 0) {
            payload.fronting = draftValues.fronting;
        }

        if (changes && marked.indexOf(sharingButton) >= 0) {
            payload.visibilityInherit = draftValues.visibilityInherit === true;
            payload.visibilityTypeId = draftValues.visibilityInherit === true ? null : draftValues.visibilityTypeId;
        }

        if (changes && permissionControl && marked.indexOf(permissionControl) >= 0) {
            payload.permission = draftValues.permission;
        }

        if (changes && marked.indexOf(avatarWrap) >= 0) {
            payload.avatarObjectUrl = draftValues.avatarObjectUrl;
        }

        return payload;
    }

    function canonicalVisibilityId(value) {
        return value === 1 || value === 2 || value === 3 || value === 4 ? value : null;
    }

    const draftValues = {
        displayName: member && member.displayName ? member.displayName : "",
        pronouns: member && member.pronouns ? member.pronouns : "",
        description: member && member.description ? member.description : "",
        color: storedMemberColor(member && member.color),
        fronting: member && (member.fronting === true || member.fronting === false) ? member.fronting : null,
        visibilityKnown: Boolean(member && member.visibilityKnown === true),
        visibilityInherit: Boolean(member && member.visibilityInherit === true),
        visibilityTypeId: canonicalVisibilityId(member && member.visibilityTypeId),
        permission: member && member.permission ? member.permission : null,
        avatarObjectUrl: nullablePreviewValue(member && member.avatarObjectUrl)
    };

    const persistedValues = {
        displayName: draftValues.displayName,
        pronouns: draftValues.pronouns,
        description: draftValues.description,
        color: draftValues.color,
        fronting: draftValues.fronting,
        visibilityKnown: draftValues.visibilityKnown,
        visibilityInherit: draftValues.visibilityInherit,
        visibilityTypeId: draftValues.visibilityTypeId,
        permission: draftValues.permission,
        avatarObjectUrl: draftValues.avatarObjectUrl
    };

    const fieldLabels = {
        displayName: "Name",
        pronouns: "Pronouns",
        description: "Description"
    };

    const fieldRows = {};
    let activeEdit = null;
    let isSaving = false;
    let unsavedIndicator = null;
    let detachChanges = null;
    let profileControls = [];
    let permissionControl = null;

    const cardDetails = document.createElement("details");
    cardDetails.className = previewHeader ? "member-card member-card-preview" : "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const summaryMain = document.createElement("span");
    summaryMain.className = "member-summary-main";

    const colorStrip = document.createElement("span");
    colorStrip.className = "member-color-strip";
    colorStrip.setAttribute("aria-hidden", "true");

    const avatarWrap = document.createElement("span");
    avatarWrap.className = "member-avatar-wrap";
    avatarWrap.setAttribute("data-change-control", "");
    avatarWrap.setAttribute("data-change-name", "Choose an avatar image");

    const avatarPlaceholder = document.createElement("span");
    avatarPlaceholder.className = "member-avatar-placeholder";
    avatarPlaceholder.setAttribute("aria-hidden", "true");

    const avatarPencil = document.createElement("button");
    avatarPencil.type = "button";
    avatarPencil.className = "member-avatar-pencil";
    avatarPencil.setAttribute("aria-label", "Choose an avatar image");
    avatarPencil.title = "Choose an avatar image";
    avatarPencil.appendChild(createMemberActionIcon("edit"));

    const avatarFile = document.createElement("input");
    avatarFile.type = "file";
    avatarFile.accept = "image/*";
    avatarFile.tabIndex = -1;
    avatarFile.className = "member-avatar-file";
    avatarFile.setAttribute("aria-label", "Choose an avatar image");

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = allowProfileEditing
        ? getProfileDisplayText("displayName")
        : (draftValues.displayName || "");

    const pronouns = document.createElement("span");
    pronouns.className = "member-pronouns";
    pronouns.textContent = draftValues.pronouns || "";

    const headerControls = document.createElement("span");
    headerControls.className = "member-summary-controls";

    const sharingLabel = document.createElement("span");
    sharingLabel.className = "member-sharing-label";
    sharingLabel.appendChild(document.createTextNode("Visible to:"));

    const sharingControl = document.createElement("span");
    sharingControl.className = "member-sharing-control";
    const sharingButton = document.createElement("button");
    sharingButton.type = "button";
    sharingButton.className = "member-card-sharing";
    sharingButton.setAttribute("data-change-control", "");
    sharingButton.setAttribute("data-change-name", "Visibility");
    sharingButton.setAttribute("aria-label", "Visibility");
    sharingButton.setAttribute("aria-haspopup", "listbox");
    sharingButton.setAttribute("aria-expanded", "false");
    const sharingList = document.createElement("div");
    sharingList.className = "member-sharing-list";
    sharingList.setAttribute("role", "listbox");
    sharingList.hidden = true;
    window.PluralBridge = window.PluralBridge || {};
    window.PluralBridge.memberSharingSequence = (window.PluralBridge.memberSharingSequence || 0) + 1;
    sharingList.id = "member-sharing-list-" + window.PluralBridge.memberSharingSequence;
    sharingButton.setAttribute("aria-controls", sharingList.id);
    let sharingOptions = [];
    sharingControl.appendChild(sharingButton);
    sharingControl.appendChild(sharingList);

    const frontingButton = document.createElement("button");
    frontingButton.type = "button";
    frontingButton.className = "member-card-fronting";
    frontingButton.setAttribute("data-change-control", "");
    frontingButton.setAttribute("data-change-name", "Fronting");

    const chevron = document.createElement("span");
    chevron.className = "member-card-expand-icon member-tree-chevron";
    chevron.setAttribute("aria-hidden", "true");
    const chevronSvgNamespace = "http://www.w3.org/2000/svg";
    const chevronSvg = document.createElementNS(chevronSvgNamespace, "svg");
    chevronSvg.setAttribute("viewBox", "0 0 24 24");
    chevronSvg.setAttribute("width", "16");
    chevronSvg.setAttribute("height", "16");
    chevronSvg.setAttribute("aria-hidden", "true");
    chevronSvg.setAttribute("focusable", "false");
    const chevronPath = document.createElementNS(chevronSvgNamespace, "path");
    chevronPath.setAttribute("d", "M9 5.5 16.5 12 9 18.5");
    chevronPath.setAttribute("fill", "none");
    chevronPath.setAttribute("stroke", "currentColor");
    chevronPath.setAttribute("stroke-width", "1.75");
    chevronPath.setAttribute("stroke-linecap", "round");
    chevronPath.setAttribute("stroke-linejoin", "round");
    chevronSvg.appendChild(chevronPath);
    chevron.appendChild(chevronSvg);
    if (!previewHeader) {
        chevron.className = "member-chevron";
        chevron.replaceChildren();
        chevron.textContent = "v";
    }

    identity.appendChild(name);
    identity.appendChild(pronouns);
    unsavedIndicator = document.createElement("span");
    unsavedIndicator.className = "member-card-unsaved-indicator";
    unsavedIndicator.textContent = "Unsaved changes";
    unsavedIndicator.hidden = true;
    identity.appendChild(unsavedIndicator);
    avatarWrap.appendChild(avatarPlaceholder);
    if (previewHeader) {
        avatarWrap.appendChild(avatarPencil);
        avatarWrap.appendChild(avatarFile);
    }
    summaryMain.appendChild(avatarWrap);
    summaryMain.appendChild(identity);
    sharingLabel.appendChild(sharingControl);
    headerControls.appendChild(sharingLabel);
    if (previewHeader && window.PluralBridge.members && typeof window.PluralBridge.members.createPolicyDropdown === "function" && typeof window.PluralBridge.members.policyChoices === "function") {
        const permissionLabel = document.createElement("span");
        permissionLabel.className = "member-sharing-label";
        permissionLabel.appendChild(document.createTextNode("Permissions:"));
        const permissionMenu = window.PluralBridge.members.createPolicyDropdown({
            label: "Permissions",
            choices: window.PluralBridge.members.policyChoices("permission", true),
            getValue: function () {
                return draftValues.permission || "";
            },
            setValue: function (next) {
                const permission = next ? String(next) : "";
                if (permission === (draftValues.permission || "")) {
                    return;
                }
                draftValues.permission = permission || null;
                notifyEdited(permissionControl);
                updateSaveButtonState();
            }
        });
        permissionMenu.root.classList.add("member-card-permission");
        permissionControl = permissionMenu.root.matches && permissionMenu.root.matches("input, select, textarea")
            ? permissionMenu.root
            : (permissionMenu.root.querySelector("input, select, textarea, .member-policy-button") || permissionMenu.root);
        permissionControl.setAttribute("data-change-control", "");
        permissionControl.setAttribute("data-change-name", "Permissions");
        permissionControl.setAttribute("aria-label", "Permissions");
        permissionLabel.appendChild(permissionMenu.root);
        permissionLabel.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        permissionLabel.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
        });
        headerControls.appendChild(permissionLabel);
        cardDetails.paintPermission = permissionMenu.refresh;
    }
    headerControls.appendChild(frontingButton);
    headerControls.appendChild(chevron);

    if (previewHeader) {
        cardDetails.appendChild(colorStrip);
    }
    summary.appendChild(summaryMain);
    if (previewHeader) {
        summary.appendChild(headerControls);
    } else {
        summary.appendChild(chevron);
    }

    let keepSavedCardOpen = false;
    let pendingSavedCardView = null;
    let activeSavedCardView = null;
    let savedCardHoldToken = 0;
    let paintingColor = false;
    let colorEcho = 0;
    let acceptColorEdits = false;

    cardDetails.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const profilePanel = document.createElement("section");
    profilePanel.className = "member-profile-panel";
    profilePanel.setAttribute("aria-label", "Member profile fields");

    const profileHeading = document.createElement("h3");
    profileHeading.className = "me-section-heading";
    profileHeading.textContent = "Profile";

    const profileFields = document.createElement("div");
    profileFields.className = "member-profile-fields";

    profileFields.appendChild(createMemberProfileFieldRow("Name", "displayName", draftValues.displayName, false));
    profileFields.appendChild(createMemberProfileFieldRow("Pronouns", "pronouns", draftValues.pronouns, false));
    profileFields.appendChild(createMemberProfileFieldRow("Description", "description", draftValues.description, true));

    const colorRow = document.createElement("div");
    colorRow.className = "member-profile-color-row";
    const colorLabel = document.createElement("span");
    colorLabel.className = "member-profile-field-label";
    colorLabel.textContent = previewHeader ? "Member color" : "Color";
    const colorSwatch = document.createElement("span");
    colorSwatch.className = "member-color-swatch";
    const colorInput = document.createElement("input");
    colorInput.type = "color";
    colorInput.className = "member-color-input";
    colorInput.value = "#94a3b8";
    colorLabel.id = "member-color-label-" + window.PluralBridge.memberSharingSequence;
    colorInput.setAttribute("aria-labelledby", colorLabel.id);
    colorSwatch.setAttribute("data-change-control", "");
    colorSwatch.setAttribute("data-change-name", colorLabel.textContent);
    colorSwatch.appendChild(colorInput);
    const colorPencil = document.createElement("button");
    colorPencil.type = "button";
    colorPencil.className = "member-profile-field-edit-button member-color-pencil";
    colorPencil.setAttribute("aria-label", "Edit color");
    colorPencil.title = "Member color is not loaded. Choosing a color stores a preview only.";
    colorPencil.appendChild(createMemberActionIcon("edit"));
    colorRow.appendChild(colorLabel);
    colorRow.appendChild(colorSwatch);
    colorRow.appendChild(colorPencil);
    if (previewHeader) {
        const colorHelp = document.createElement("p");
        colorHelp.className = "member-color-help";
        colorHelp.textContent = "Used to identify this member throughout the app.";
        colorRow.appendChild(colorHelp);
    }

    const profileActions = document.createElement("div");
    profileActions.className = "member-profile-action-row";

    const saveButton = allowProfileEditing ? createProfileSaveButton() : null;
    cancelButton = allowProfileEditing ? createProfileCancelButton() : null;
    if (cancelButton) {
        profileActions.appendChild(cancelButton);
    }
    if (saveButton) {
        profileActions.appendChild(saveButton);
    }

    const profileStatus = document.createElement("p");
    profileStatus.className = "member-form-status";
    profileStatus.setAttribute("aria-live", "polite");
    profileStatus.textContent = "";

    profilePanel.appendChild(profileHeading);
    profilePanel.appendChild(profileFields);
    if (previewHeader) {
        const colorFooter = document.createElement("div");
        colorFooter.className = "member-profile-color-footer";
        colorFooter.appendChild(colorRow);
        if (allowProfileEditing) {
            colorFooter.appendChild(profileActions);
        }
        profilePanel.appendChild(colorFooter);
    } else if (allowProfileEditing) {
        profilePanel.appendChild(profileActions);
    }
    profilePanel.appendChild(profileStatus);

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(member, null, 2);

    const rawJsonDetails = document.createElement("details");
    rawJsonDetails.className = "member-profile-json-details";

    const rawJsonSummary = document.createElement("summary");
    rawJsonSummary.textContent = "Raw JSON";

    rawJsonDetails.appendChild(rawJsonSummary);
    rawJsonDetails.appendChild(jsonBlock);

    body.appendChild(profilePanel);
    body.appendChild(rawJsonDetails);
    cardDetails.appendChild(body);

    cardDetails.addEventListener("toggle", function () {
        if (cardDetails.dataset.pbLeave) {
            const holdOpen = cardDetails.dataset.pbLeave === "1";
            if (cardDetails.open !== holdOpen) {
                cardDetails.open = holdOpen;
            }
            return;
        }

        if (cardIsDirty() && !cardDetails.open) {
            cardDetails.open = true;
            return;
        }

        if (keepSavedCardOpen && !cardDetails.open) {
            cardDetails.open = true;
            if (activeSavedCardView) {
                activeSavedCardView.open = true;
                restoreCardView(activeSavedCardView);
            }
            return;
        }

        if (previewHeader) {
            chevron.classList.toggle("is-expanded", cardDetails.open);
        }
    });

    function releaseUnsavedAvatar() {
        if (
            draftValues.avatarObjectUrl &&
            draftValues.avatarObjectUrl !== persistedValues.avatarObjectUrl &&
            typeof URL.revokeObjectURL === "function"
        ) {
            URL.revokeObjectURL(draftValues.avatarObjectUrl);
        }
    }

    function paintAvatar() {
        const image = avatarWrap.querySelector(".member-avatar-image");
        if (image) {
            image.remove();
        }

        if (!previewHeader) {
            const generated = generatedMemberColor();
            avatarPlaceholder.hidden = false;
            avatarPlaceholder.style.backgroundColor = generated.fill;
            avatarPlaceholder.style.borderColor = generated.border;
            return;
        }

        const appearance = effectiveMemberColor();
        avatarPlaceholder.style.backgroundColor = appearance.fill;
        avatarPlaceholder.style.borderColor = appearance.border;
        avatarPlaceholder.classList.remove("is-neutral");
        if (!draftValues.avatarObjectUrl) {
            avatarPlaceholder.hidden = false;
            return;
        }

        avatarPlaceholder.hidden = true;
        const preview = document.createElement("img");
        preview.className = "member-avatar-image";
        preview.alt = "";
        preview.src = draftValues.avatarObjectUrl;
        avatarWrap.insertBefore(preview, avatarPencil);
    }

    function paintColor() {
        const appearance = effectiveMemberColor();
        colorStrip.style.backgroundColor = appearance.fill;
        colorStrip.classList.toggle("is-generated", !appearance.explicit);
        if (appearance.explicit) {
            colorStrip.removeAttribute("title");
            colorStrip.removeAttribute("aria-label");
            colorStrip.setAttribute("aria-hidden", "true");
        } else {
            colorStrip.title = "Generated color—no member color assigned.";
            colorStrip.setAttribute("aria-label", "Generated color—no member color assigned.");
            colorStrip.removeAttribute("aria-hidden");
        }
        colorSwatch.style.backgroundColor = appearance.fill;
        colorSwatch.style.borderColor = appearance.border;
        colorSwatch.title = appearance.explicit
            ? "Edit color"
            : "No stored member color. This color is generated for display. Choosing a color stores a preview only.";
        colorPencil.title = colorSwatch.title;
        if (appearance.explicit && colorInput.value.toLowerCase() !== appearance.fill.toLowerCase()) {
            withoutChangeNotices(function () {
                colorEcho += 1;
                paintingColor = true;
                colorInput.value = appearance.fill;
                paintingColor = false;
            });
            window.setTimeout(function () {
                colorEcho = Math.max(0, colorEcho - 1);
            }, 0);
        }
    }

    function commitPickerColor() {
        if (!acceptColorEdits || paintingColor || colorEcho || isSaving) {
            return;
        }

        const nextColor = memberCssColor(colorInput.value);
        draftValues.color = nextColor;
        paintColor();
        updateSaveButtonState();
    }

    function paintFronting() {
        frontingButton.replaceChildren();
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "20");
        svg.setAttribute("height", "20");
        svg.setAttribute("fill", "none");
        svg.setAttribute("stroke", "currentColor");
        svg.setAttribute("stroke-width", "1.8");
        svg.setAttribute("stroke-linecap", "round");
        svg.setAttribute("stroke-linejoin", "round");
        svg.setAttribute("aria-hidden", "true");
        const person = document.createElementNS(svgNamespace, "path");
        person.setAttribute("d", "M9 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM4.5 19v-1.2A3.8 3.8 0 0 1 8.3 14h1.4a3.8 3.8 0 0 1 3.8 3.8V19");
        const arrow = document.createElementNS(svgNamespace, "path");
        arrow.setAttribute("d", draftValues.fronting === true ? "M17 19V8M14 11l3-3 3 3" : "M17 5v11M14 13l3 3 3-3");
        svg.appendChild(person);
        svg.appendChild(arrow);
        frontingButton.appendChild(svg);
        const label = document.createElement("span");
        label.className = "member-fronting-label";
        if (draftValues.fronting === null) {
            label.textContent = "Not loaded";
            frontingButton.setAttribute("aria-pressed", "mixed");
            frontingButton.title = "Fronting is not loaded. Activate to mark this member as fronting in this preview.";
        } else if (draftValues.fronting) {
            label.textContent = "Fronting";
            frontingButton.setAttribute("aria-pressed", "true");
            frontingButton.title = "Fronting. This is the imported fronting snapshot.";
        } else {
            label.textContent = "Not fronting";
            frontingButton.setAttribute("aria-pressed", "false");
            frontingButton.title = "Not fronting. This is the imported fronting snapshot.";
        }
        frontingButton.setAttribute("aria-label", frontingButton.title);
        frontingButton.appendChild(label);
    }

    function visibilityChoices() {
        const choices = [];
        if (!draftValues.visibilityKnown) {
            choices.push({ id: null, known: false, label: "Not loaded", icon: "unset", disabled: false });
        } else {
            choices.push({ id: null, known: true, label: "Not set", icon: "unset", inherit: false, disabled: false });
        }

        choices.push(
            { id: null, known: true, label: "Inherit", icon: "inherit", inherit: true, disabled: false },
            { id: 1, known: true, label: "Only you", icon: "only-me", disabled: false },
            { id: 2, known: true, label: "Trusted friends", icon: "selected-people", disabled: false },
            { id: 3, known: true, label: "All friends", icon: "all-connections", disabled: false },
            { id: 4, known: true, label: "Selected people", icon: "selected-people", disabled: true }
        );
        return choices;
    }

    function currentSharingChoice() {
        const choices = visibilityChoices();
        if (!draftValues.visibilityKnown) {
            return choices[0];
        }

        if (draftValues.visibilityInherit === true) {
            return choices.find(function (choice) {
                return choice.inherit === true;
            }) || choices[0];
        }

        const selected = choices.find(function (choice) {
            return choice.known && choice.inherit !== true && choice.id === draftValues.visibilityTypeId;
        });
        return selected || choices[0];
    }

    function paintSharing() {
        const choices = visibilityChoices();
        const choice = currentSharingChoice();
        const selectedIndex = choices.indexOf(choice);
        sharingList.replaceChildren();
        sharingOptions = choices.map(function (item, index) {
            const option = document.createElement("div");
            option.className = "member-sharing-option";
            option.setAttribute("role", "option");
            option.tabIndex = -1;
            option.id = sharingList.id + "-option-" + index;
            option.dataset.visibilityId = item.id === null ? "" : String(item.id);
            option.dataset.visibilityKnown = item.known ? "true" : "false";
            option.dataset.visibilityInherit = item.inherit === true ? "true" : "false";
            option.dataset.sharingIndex = String(index);
            if (item.disabled) {
                option.setAttribute("aria-disabled", "true");
            }
            option.appendChild(sharingOutlineIcon(item.icon));
            const optionLabel = document.createElement("span");
            optionLabel.textContent = item.label;
            option.appendChild(optionLabel);
            const selected = index === selectedIndex;
            option.setAttribute("aria-selected", selected ? "true" : "false");
            option.classList.toggle("is-current", selected);
            sharingList.appendChild(option);
            return option;
        });
        sharingButton.replaceChildren();
        sharingButton.appendChild(sharingOutlineIcon(choice.icon));
        const value = document.createElement("span");
        value.className = "member-sharing-value";
        value.textContent = choice.label;
        sharingButton.appendChild(value);
        const menuChevron = document.createElement("span");
        menuChevron.className = "member-sharing-chevron" + (sharingList.hidden ? "" : " is-open");
        menuChevron.setAttribute("aria-hidden", "true");
        menuChevron.appendChild(sharingOutlineIcon("chevron"));
        sharingButton.appendChild(menuChevron);
        if (!draftValues.visibilityKnown) {
            sharingButton.title = "Visibility is not loaded. Choosing a value stores a preview only.";
        } else if (draftValues.visibilityInherit === true) {
            sharingButton.title = "Preview only. Inherit does not change stored visibility.";
        } else if (draftValues.visibilityTypeId === null) {
            sharingButton.title = "Visibility is not set. Choosing a value stores a preview only.";
        } else {
            sharingButton.title = "Preview only. This does not change who can see the member.";
        }
        sharingButton.setAttribute("aria-label", "Visible to: " + choice.label);
    }

    function placeSharingList() {
        if (sharingList.hidden) {
            return;
        }

        const rect = sharingButton.getBoundingClientRect();
        const listHeight = sharingList.offsetHeight;
        let top = rect.bottom + 4;
        if (top + listHeight > window.innerHeight - 8) {
            top = Math.max(8, rect.top - listHeight - 4);
        }
        sharingList.style.left = Math.max(8, rect.left) + "px";
        sharingList.style.top = top + "px";
        sharingList.style.width = rect.width + "px";
    }

    function setSharingOpen(open) {
        sharingList.hidden = !open;
        sharingButton.setAttribute("aria-expanded", open ? "true" : "false");
        cardDetails.classList.toggle("is-sharing-open", open);
        paintSharing();
        if (!open) {
            sharingList.removeAttribute("aria-activedescendant");
            return;
        }

        const selected = sharingOptions.findIndex(function (option) {
            return option.getAttribute("aria-selected") === "true";
        });
        const index = selected < 0 ? 0 : selected;
        sharingOptions[index].focus();
        sharingList.setAttribute("aria-activedescendant", sharingOptions[index].id);
        placeSharingList();
    }

    function chooseSharing(option) {
        if (!option || option.getAttribute("aria-disabled") === "true") {
            if (option && option.getAttribute("aria-disabled") === "true") {
                setSharingOpen(false);
                sharingButton.focus();
            }
            return;
        }

        if (option.dataset.visibilityKnown !== "true") {
            setSharingOpen(false);
            sharingButton.focus();
            return;
        }

        const raw = option.dataset.visibilityId || "";
        draftValues.visibilityKnown = true;
        draftValues.visibilityInherit = option.dataset.visibilityInherit === "true";
        draftValues.visibilityTypeId = draftValues.visibilityInherit || !raw
            ? null
            : canonicalVisibilityId(Number(raw));
        notifyEdited(sharingButton);
        setSharingOpen(false);
        updateSaveButtonState();
        sharingButton.focus();
    }

    function enabledSharingIndex(start, step) {
        const count = sharingOptions.length;
        if (!count) {
            return 0;
        }

        let index = start;
        for (let stepCount = 0; stepCount < count; stepCount += 1) {
            index = step > 0
                ? Math.min(count - 1, index + step)
                : Math.max(0, index + step);
            if (sharingOptions[index].getAttribute("aria-disabled") !== "true") {
                return index;
            }
            if ((step > 0 && index === count - 1) || (step < 0 && index === 0)) {
                break;
            }
        }

        return start;
    }

    function dismissSharing(event) {
        if (sharingList.hidden || sharingControl.contains(event.target)) {
            return;
        }

        setSharingOpen(false);
    }

    function paintPreviewControls() {
        paintAvatar();
        if (!previewHeader) {
            return;
        }
        paintColor();
        paintFronting();
        paintSharing();
        if (typeof cardDetails.paintPermission === "function") {
            cardDetails.paintPermission();
        }
    }

    function keepHeaderClick(event) {
        event.preventDefault();
        event.stopPropagation();
    }

    if (previewHeader) {
        function openAvatarChooser(event) {
            keepHeaderClick(event);
            avatarFile.click();
        }
        avatarPencil.addEventListener("mousedown", keepHeaderClick);
        avatarPencil.addEventListener("click", openAvatarChooser);
        avatarWrap.addEventListener("mousedown", function (event) {
            if (event.target === avatarFile) {
                return;
            }
            event.stopPropagation();
        });
        avatarWrap.addEventListener("click", function (event) {
            if (event.target === avatarFile || event.target === avatarPencil || avatarPencil.contains(event.target)) {
                return;
            }
            openAvatarChooser(event);
        });
        avatarFile.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        avatarFile.addEventListener("change", function () {
            const file = avatarFile.files && avatarFile.files[0] ? avatarFile.files[0] : null;
            if (!file) {
                return;
            }
            releaseUnsavedAvatar();
            draftValues.avatarObjectUrl = URL.createObjectURL(file);
            notifyEdited(avatarWrap);
            avatarFile.value = "";
            paintAvatar();
            updateSaveButtonState();
        });
        sharingLabel.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        sharingLabel.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        sharingControl.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        sharingControl.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        sharingButton.addEventListener("click", function (event) {
            keepHeaderClick(event);
            setSharingOpen(sharingList.hidden);
        });
        sharingButton.addEventListener("keydown", function (event) {
            if (event.key === "ArrowDown" || event.key === "ArrowUp" || event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                event.stopPropagation();
                if (sharingList.hidden) {
                    setSharingOpen(true);
                }
                return;
            }

            if (event.key === "Escape" && !sharingList.hidden) {
                event.preventDefault();
                event.stopPropagation();
                setSharingOpen(false);
            }
        });
        sharingList.addEventListener("mousedown", function (event) {
            if (event.target.closest(".member-sharing-option")) {
                event.preventDefault();
                event.stopPropagation();
            }
        });
        sharingList.addEventListener("click", function (event) {
            const option = event.target.closest(".member-sharing-option");
            if (!option) {
                return;
            }

            keepHeaderClick(event);
            chooseSharing(option);
        });
        sharingList.addEventListener("keydown", function (event) {
            const current = Number(document.activeElement && document.activeElement.dataset
                ? document.activeElement.dataset.sharingIndex
                : NaN);
            if (event.key === "ArrowDown" || event.key === "ArrowUp" || event.key === "Home" || event.key === "End") {
                event.preventDefault();
                event.stopPropagation();
                const start = Number.isNaN(current) ? 0 : current;
                let next = start;
                if (event.key === "ArrowDown") {
                    next = enabledSharingIndex(next, 1);
                } else if (event.key === "ArrowUp") {
                    next = enabledSharingIndex(next, -1);
                } else if (event.key === "Home") {
                    next = enabledSharingIndex(-1, 1);
                } else {
                    next = enabledSharingIndex(sharingOptions.length, -1);
                }
                if (!sharingOptions[next]) {
                    return;
                }
                sharingOptions[next].focus();
                sharingList.setAttribute("aria-activedescendant", sharingOptions[next].id);
                return;
            }

            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                event.stopPropagation();
                chooseSharing(document.activeElement);
                return;
            }

            if (event.key === "Escape") {
                event.preventDefault();
                event.stopPropagation();
                setSharingOpen(false);
                sharingButton.focus();
                return;
            }

            if (event.key === "Tab") {
                setSharingOpen(false);
            }
        });
        document.addEventListener("pointerdown", dismissSharing, true);
        window.addEventListener("resize", placeSharingList);
        window.addEventListener("scroll", placeSharingList, true);
        frontingButton.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        frontingButton.addEventListener("click", function (event) {
            keepHeaderClick(event);
            draftValues.fronting = draftValues.fronting === null ? true : !draftValues.fronting;
            notifyEdited(frontingButton);
            paintFronting();
            updateSaveButtonState();
        });
        colorPencil.addEventListener("click", function () {
            colorInput.click();
        });
        colorSwatch.addEventListener("click", function () {
            colorInput.click();
        });
        colorInput.addEventListener("input", commitPickerColor);
        colorInput.addEventListener("change", commitPickerColor);

        queueMicrotask(function () {
            if (!cardDetails.isConnected || !document.body) {
                return;
            }

            const removalObserver = new MutationObserver(function () {
                if (!cardDetails.isConnected) {
                    releaseUnsavedAvatar();
                    if (typeof detachChanges === "function") {
                        detachChanges();
                        detachChanges = null;
                    }
                    document.removeEventListener("pointerdown", dismissSharing, true);
                    window.removeEventListener("resize", placeSharingList);
                    window.removeEventListener("scroll", placeSharingList, true);
                    removalObserver.disconnect();
                }
            });
            removalObserver.observe(document.body, { childList: true, subtree: true });
        });
    }

    paintPreviewControls();
    updateSaveButtonState();
    acceptColorEdits = true;

    function surfaceIsModified(control) {
        const marker = modifiedMarker();
        return !!(control && control.classList && control.classList.contains(marker));
    }

    function textFieldIsModified(fieldName) {
        const rowInfo = fieldRows[fieldName];
        if (rowInfo && surfaceIsModified(rowInfo.value)) {
            return true;
        }
        return !!(activeEdit && activeEdit.fieldName === fieldName && surfaceIsModified(activeEdit.editor));
    }

    function applyExternal(fields) {
        if (!fields) {
            return;
        }

        const refreshControls = [];
        ["displayName", "pronouns", "description"].forEach(function (fieldName) {
            if (!Object.prototype.hasOwnProperty.call(fields, fieldName) || !fieldRows[fieldName] || textFieldIsModified(fieldName)) {
                return;
            }

            persistedValues[fieldName] = fields[fieldName];
            draftValues[fieldName] = fields[fieldName];
            fieldRows[fieldName].value.dataset.persistedValue = persistedValues[fieldName] || "";
            fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
            updateStaticFieldDisplay(fieldName);
            if (member && Object.prototype.hasOwnProperty.call(member, fieldName)) {
                member[fieldName] = persistedValues[fieldName];
            }
            if (activeEdit && activeEdit.fieldName === fieldName) {
                activeEdit.editor.value = draftValues[fieldName] || "";
                if (fieldRows[fieldName].isLongText) {
                    fitLongEditor(activeEdit.editor);
                }
                refreshControls.push(activeEdit.editor);
            }
            refreshControls.push(fieldRows[fieldName].value);
        });

        if (Object.prototype.hasOwnProperty.call(fields, "color") && !surfaceIsModified(colorSwatch) && !surfaceIsModified(colorInput)) {
            persistedValues.color = fields.color;
            draftValues.color = fields.color;
            if (member) {
                member.color = persistedValues.color;
            }
            refreshControls.push(colorSwatch, colorInput);
        }

        if (Object.prototype.hasOwnProperty.call(fields, "fronting") && !surfaceIsModified(frontingButton)) {
            persistedValues.fronting = fields.fronting;
            draftValues.fronting = fields.fronting;
            if (member) {
                member.fronting = persistedValues.fronting;
            }
            refreshControls.push(frontingButton);
        }

        if (Object.prototype.hasOwnProperty.call(fields, "permission") && !surfaceIsModified(permissionControl)) {
            persistedValues.permission = fields.permission;
            draftValues.permission = fields.permission;
            if (permissionControl) {
                refreshControls.push(permissionControl);
            }
        }

        if (Object.prototype.hasOwnProperty.call(fields, "avatarObjectUrl") && !surfaceIsModified(avatarWrap) && !surfaceIsModified(avatarFile)) {
            persistedValues.avatarObjectUrl = fields.avatarObjectUrl;
            draftValues.avatarObjectUrl = fields.avatarObjectUrl;
            if (member) {
                member.avatarObjectUrl = persistedValues.avatarObjectUrl;
            }
            refreshControls.push(avatarWrap, avatarFile);
        }

        const visibilitySupplied = ["visibilityKnown", "visibilityInherit", "visibilityTypeId"].some(function (key) {
            return Object.prototype.hasOwnProperty.call(fields, key);
        });
        if (visibilitySupplied && !surfaceIsModified(sharingButton)) {
            ["visibilityKnown", "visibilityInherit", "visibilityTypeId"].forEach(function (key) {
                if (Object.prototype.hasOwnProperty.call(fields, key)) {
                    persistedValues[key] = fields[key];
                    draftValues[key] = fields[key];
                }
            });
            if (member && Object.prototype.hasOwnProperty.call(fields, "visibilityKnown")) {
                member.visibilityKnown = persistedValues.visibilityKnown;
            }
            if (member && Object.prototype.hasOwnProperty.call(fields, "visibilityTypeId")) {
                member.visibilityTypeId = persistedValues.visibilityTypeId;
            }
            refreshControls.push(sharingButton);
        }

        updateSummaryPreview();
        paintPreviewControls();
        const changes = changeTracking();
        if (changes) {
            changes.refreshSnapshots(cardDetails, refreshControls);
        }
    }

    cardDetails.addEventListener("click", function (event) {
        const changes = changeTracking();
        if (!changes) {
            return;
        }

        if (changes.blocks(cardDetails)) {
            event.preventDefault();
            event.stopPropagation();
            const wasOpen = cardDetails.open;
            cardDetails.dataset.pbLeave = wasOpen ? "1" : "0";
            changes.requestLeave(function () {
                delete cardDetails.dataset.pbLeave;
                if (!cardDetails.isConnected) {
                    return;
                }
                cardDetails.open = true;
                window.setTimeout(function () {
                    if (cardDetails.isConnected && !cardDetails.dataset.pbLeave) {
                        cardDetails.open = true;
                    }
                }, 0);
            }).then(function () {
                delete cardDetails.dataset.pbLeave;
            });
            return;
        }

        const avatarSurface = event.target && event.target.closest
            ? event.target.closest(".member-avatar-wrap")
            : null;
        if (avatarSurface && cardDetails.contains(avatarSurface)) {
            const fileTarget = event.target.closest ? event.target.closest("input[type='file']") : null;
            if (!fileTarget) {
                event.preventDefault();
            }
            return;
        }

        const control = event.target && event.target.closest
            ? event.target.closest("button, input, textarea, select, a, [role='option']")
            : null;
        if (control && cardDetails.contains(control)) {
            return;
        }

        const summaryHit = event.target && event.target.closest
            ? event.target.closest("summary")
            : null;
        if (summaryHit && cardDetails.contains(summaryHit) && cardDetails.open && changes.dirty(cardDetails)) {
            event.preventDefault();
            event.stopPropagation();
            changes.requestLeave(function () {
                if (cardDetails.isConnected) {
                    cardDetails.open = true;
                }
            });
        }
    }, true);

    cardDetails.addEventListener("focusin", function (event) {
        const changes = changeTracking();
        if (!changes || !changes.blocks(cardDetails) || changes.hasPending()) {
            return;
        }

        if (event.target && typeof event.target.blur === "function") {
            event.target.blur();
        }
        changes.requestLeave();
    }, true);

    profileControls = [
        fieldRows.displayName.value,
        fieldRows.pronouns.value,
        fieldRows.description.value,
        colorSwatch
    ];

    function profileFieldName(control) {
        if (!control) {
            return "";
        }
        if (control.dataset && control.dataset.memberProfileValue && fieldRows[control.dataset.memberProfileValue]) {
            return control.dataset.memberProfileValue;
        }
        const fieldRow = control.closest ? control.closest("[data-member-profile-field]") : null;
        const fieldName = fieldRow && fieldRow.dataset.memberProfileField;
        return fieldName && fieldRows[fieldName] ? fieldName : "";
    }

    function readTracked(control) {
        const fieldName = profileFieldName(control);
        if (fieldName) {
            return draftValues[fieldName];
        }
        if (control === colorSwatch || control === colorInput) {
            return draftValues.color;
        }
        if (control === frontingButton) {
            return draftValues.fronting;
        }
        if (control === sharingButton) {
            return {
                visibilityKnown: draftValues.visibilityKnown,
                visibilityInherit: draftValues.visibilityInherit,
                visibilityTypeId: draftValues.visibilityTypeId
            };
        }
        if (permissionControl && control === permissionControl) {
            return draftValues.permission;
        }
        if (control === avatarWrap || control === avatarFile) {
            return draftValues.avatarObjectUrl;
        }
        if (control.type === "checkbox" || control.type === "radio") {
            return control.checked;
        }
        return control.value;
    }

    function writeTracked(control, value) {
        const fieldName = profileFieldName(control);
        if (fieldName) {
            draftValues[fieldName] = value || "";
            if (activeEdit && activeEdit.fieldName === fieldName) {
                closeActiveEditor(false);
            }
            fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
            updateStaticFieldDisplay(fieldName);
            if (member && Object.prototype.hasOwnProperty.call(member, fieldName)) {
                member[fieldName] = draftValues[fieldName];
            }
            updateSummaryPreview();
            return;
        }
        if (control === colorSwatch || control === colorInput) {
            draftValues.color = value;
            if (member) {
                member.color = value;
            }
            paintColor();
            return;
        }
        if (control === frontingButton) {
            draftValues.fronting = value === true || value === false ? value : null;
            if (member) {
                member.fronting = draftValues.fronting;
            }
            paintFronting();
            return;
        }
        if (control === sharingButton && value) {
            draftValues.visibilityKnown = value.visibilityKnown === true;
            draftValues.visibilityInherit = value.visibilityInherit === true;
            draftValues.visibilityTypeId = value.visibilityTypeId;
            if (member) {
                member.visibilityKnown = draftValues.visibilityKnown;
                member.visibilityTypeId = draftValues.visibilityTypeId;
            }
            paintSharing();
            return;
        }
        if (permissionControl && control === permissionControl) {
            draftValues.permission = value || null;
            if (typeof cardDetails.paintPermission === "function") {
                cardDetails.paintPermission();
            }
            return;
        }
        if (control === avatarWrap || control === avatarFile) {
            releaseUnsavedAvatar();
            draftValues.avatarObjectUrl = value || null;
            if (member) {
                member.avatarObjectUrl = draftValues.avatarObjectUrl;
            }
            paintAvatar();
            return;
        }
        if (control.type === "checkbox" || control.type === "radio") {
            control.checked = !!value;
            return;
        }
        if (control.type !== "file" && value !== undefined && value !== null) {
            control.value = value;
        }
    }

    const changeOwner = changeTracking();
    if (changeOwner) {
        detachChanges = changeOwner.attach({
            container: cardDetails,
            memberId: getMemberIdForSave(),
            save: saveProfileChanges,
            read: readTracked,
            write: writeTracked,
            persists: function (control) {
                return profileControls.indexOf(control) >= 0 || !!profileFieldName(control);
            },
            applyExternal: applyExternal,
            onChanged: renderChangeState
        });
        changeOwner.capture(cardDetails);
    }

    return cardDetails;
}

window.PluralBridge = window.PluralBridge || {};
window.PluralBridge.createMemberAccordion = createMemberAccordion;
