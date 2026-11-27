/**
 * Member-card editor. One card shows drafts, tracks modified controls, and saves or restores them.
 * Depends on window.PluralBridge.changeTracking for markers, leave, and shared updates. The navigator supplies saveProfile and saveFronting.
 * Direct Save and Cancel keep the card expanded. Fronting, when saveFronting is supplied, persists immediately and is not part of the profile body. Avatar bytes stay a local preview until Save confirms them.
 * @param {object} member Initial member fields. Missing text is treated as empty. A missing member id is not invented.
 * @param {object} [options] Editing, preview, saveProfile, and saveFronting callbacks. A missing saveProfile uses the legacy edit path when that exists.
 * @returns {HTMLDetailsElement} The card element.
 */
function createMemberAccordion(member, options) {
    const allowProfileEditing = !options || options.allowProfileEditing !== false;
    /**
     * Folds a seed string into a 32-bit integer for the generated avatar hue.
     *
     * @param {string} value Seed text. Every character is mixed into the hash.
     * @returns {number} Absolute value of the hash.
     */
    function hashStringToInt(value) {
        let hash = 0;

        for (let index = 0; index < value.length; index++) {
            hash = (hash * 31 + value.charCodeAt(index)) | 0;
        }

        return Math.abs(hash);
    }

    /**
     * Chooses the text that generated avatar colors are derived from.
     *
     * @returns {string} The member id when present, otherwise the display name, otherwise the literal member.
     */
    function getAvatarColorSeed() {
        if (member && member.memberId) {
            return String(member.memberId);
        }

        if (member && member.displayName) {
            return String(member.displayName);
        }

        return "member";
    }

    /**
     * Draws one visibility-menu outline icon.
     *
     * @param {string} kind only-me, selected-people, all-connections, inherit, chevron, or any other value, which draws the unset mark.
     * @returns {SVGElement} The icon.
     */
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

        /**
         * Appends one SVG shape to the icon currently being drawn.
         *
         * @param {string} tagName SVG element name, such as path or circle.
         * @param {object} attributes Attribute names and values copied onto the shape.
         * @returns {void} The shape is appended to the icon.
         */
        function addShape(tagName, attributes) {
            const node = document.createElementNS(svgNamespace, tagName);
            /**
             * Copies one attribute onto the shape being built.
             *
             * @param {string} name Attribute name.
             * @returns {void} Nothing is returned.
             */
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
    /**
     * Draws a card action icon. The edit icon uses the shared pencil when members.createEditPencil exists.
     *
     * @param {string} iconName json, save, or any other name, which draws the pencil.
     * @returns {SVGElement} The icon.
     */
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

    /**
     * Reads the member id used by Save and shared updates.
     *
     * @returns {string} The member id, or an empty string when the member has none.
     */
    function getMemberIdForSave() {
        return member && member.memberId ? member.memberId : "";
    }

    /**
     * Supplies the empty-state text for a profile field.
     *
     * @param {string} fieldName displayName, pronouns, or any other field, which uses the description placeholder.
     * @returns {string} The placeholder.
     */
    function getProfilePlaceholder(fieldName) {
        if (fieldName === "displayName") {
            return "(unnamed member)";
        }

        if (fieldName === "pronouns") {
            return "Not set";
        }

        return "No description available.";
    }

    /**
     * Shows the draft text, or the empty-state placeholder when the draft is empty.
     *
     * @param {string} fieldName Draft field name.
     * @returns {string} The visible text.
     */
    function getProfileDisplayText(fieldName) {
        const value = draftValues[fieldName];

        if (value) {
            return value;
        }

        return getProfilePlaceholder(fieldName);
    }

    /**
     * Replaces the profile status line and its error or success class.
     *
     * @param {string} message Status text. A missing message clears the line.
     * @param {string} statusType error, success, or any other value, which leaves the line neutral.
     * @returns {void} Nothing is returned.
     */
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

    /**
     * Copies the current name and pronouns drafts into the card summary.
     *
     * @returns {void} Nothing is returned.
     */
    function updateSummaryPreview() {
        name.textContent = allowProfileEditing
            ? getProfileDisplayText("displayName")
            : (draftValues.displayName || "");
        pronouns.textContent = draftValues.pronouns || "";
    }

    /**
     * Copies one field draft into its static display.
     *
     * @param {string} fieldName Field whose row is updated. A missing row is left alone.
     * @returns {void} Nothing is returned.
     */
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

    /**
     * Refreshes every static field, the summary, and the Save button.
     *
     * @returns {void} Nothing is returned.
     */
    function updateAllStaticFieldDisplays() {
        /**
         * Refreshes the static display for one profile field.
         *
         * @param {string} fieldName Field row name.
         * @returns {void} Nothing is returned.
         */
        Object.keys(fieldRows).forEach(function (fieldName) {
            updateStaticFieldDisplay(fieldName);
        });

        updateSummaryPreview();
        updateSaveButtonState();
    }

    /**
     * Reads the shared change-tracking module.
     *
     * @returns {object|null} window.PluralBridge.changeTracking, or null when it is missing.
     */
    function changeTracking() {
        return window.PluralBridge && window.PluralBridge.changeTracking
            ? window.PluralBridge.changeTracking
            : null;
    }

    /**
     * Reports whether this card has a marked control.
     *
     * @returns {boolean} True when change tracking says the card is dirty.
     */
    function cardIsDirty() {
        const changes = changeTracking();
        return !!(changes && changes.dirty(cardDetails));
    }

    /**
     * Reads the CSS class used to mark a modified control.
     *
     * @returns {string} The tracker class, or pb-control-modified when tracking is missing.
     */
    function modifiedMarker() {
        const changes = changeTracking();
        return changes ? changes.modifiedClass : "pb-control-modified";
    }

    /**
     * Tells change tracking that a control was edited.
     *
     * @param {Element} control Control that changed. A missing tracker or control does nothing.
     * @returns {void} Nothing is returned.
     */
    function notifyEdited(control) {
        const changes = changeTracking();
        if (!changes || !control) {
            return;
        }

        changes.notify(control);
    }

    /**
     * Shows or hides the Unsaved changes indicator and refreshes Save.
     *
     * @returns {void} The member-card-unsaved class is removed. Visibility follows the dirty state.
     */
    function renderChangeState() {
        const dirty = cardIsDirty();
        cardDetails.classList.remove("member-card-unsaved");
        if (unsavedIndicator) {
            unsavedIndicator.classList.toggle("is-visible", dirty);
        }
        updateSaveButtonState();
    }

    /**
     * Runs a draft update without letting it mark controls.
     *
     * @param {function} work Update to run. It is called immediately, inside suppress when tracking exists.
     * @returns {void} Nothing is returned.
     */
    function withoutChangeNotices(work) {
        const changes = changeTracking();
        if (!changes) {
            work();
            return;
        }

        changes.suppress(cardDetails, work);
    }

    let cancelButton = null;

    /**
     * Enables Save only when the card is dirty, not already saving, and the name draft is not blank. Cancel is shown only while dirty.
     *
     * @returns {void} Nothing is returned.
     */
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

    /**
     * Closes the open text editor and puts the static display back.
     *
     * @param {boolean} applyChange True stores the trimmed editor text in the draft. False leaves the draft as it was.
     * @returns {void} A missing editor does nothing. The modified class moves from the editor back to the display.
     */
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

    /**
     * Sizes a description textarea to its content.
     *
     * @param {HTMLTextAreaElement} editor Editor to measure. Other elements and disconnected textareas are ignored.
     * @returns {void} Nothing is returned.
     */
    function fitLongEditor(editor) {
        if (!editor || editor.tagName !== "TEXTAREA" || !editor.isConnected) {
            return;
        }

        editor.style.height = "auto";
        editor.style.height = editor.scrollHeight + "px";
    }

    /**
     * Creates the input or textarea for one profile field and wires its editing keys.
     *
     * @param {string} fieldName Draft field the editor writes.
     * @param {boolean} isLongText True creates a textarea that stays open on blur.
     * @returns {HTMLInputElement|HTMLTextAreaElement} The editor, already filled with the current draft.
     */
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

        /**
         * Writes each keystroke into the draft and grows a description editor.
         *
         * @returns {void} Save is refreshed. This does not close the editor.
         */
        editor.addEventListener("input", function () {
            draftValues[fieldName] = editor.value;
            if (isLongText) {
                fitLongEditor(editor);
            }
            updateSaveButtonState();
        });

        /**
         * Commits a short field when it loses focus. A description editor stays open.
         *
         * @returns {void} Nothing is returned.
         */
        editor.addEventListener("blur", function () {
            if (isLongText) {
                return;
            }
            closeActiveEditor(true);
        });

        /**
         * Closes the editor from the keyboard.
         *
         * @param {KeyboardEvent} event Escape restores the display without applying. Enter applies a short field. A description ignores Enter.
         * @returns {void} Nothing is returned.
         */
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

    /**
     * Opens the editor for one field, replacing the static display.
     *
     * @param {string} fieldName Field to edit. A missing row or a card that is already saving does nothing. The same field only focuses its editor.
     * @returns {void} Any other open editor is committed first. Change tracking is rebound from the display to the editor.
     */
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

        /**
         * Focuses and selects the editor after the current turn, once it is in the document.
         *
         * @returns {void} Nothing is returned.
         */
        window.setTimeout(function () {
            editor.focus();
            editor.select();
        }, 0);
    }

    /**
     * Builds one profile field row, its display, and its edit button.
     *
     * @param {string} labelText Visible label.
     * @param {string} fieldName Draft and dataset name.
     * @param {string} rawValue Initial display and persisted text.
     * @param {boolean} isLongText True uses the long description surface.
     * @returns {HTMLDivElement} The row. It is also stored in fieldRows.
     */
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
        } else {
            value.classList.add("member-profile-field-value-short");
        }

        const editButton = document.createElement("button");
        editButton.type = "button";
        editButton.className = "member-profile-field-edit-button";
        editButton.dataset.memberProfileEdit = fieldName;
        editButton.title = "Edit " + labelText;
        editButton.setAttribute("aria-label", "Edit " + labelText);

        editButton.appendChild(createMemberActionIcon("edit"));

        /**
         * Opens the editor for this field from the pencil.
         *
         * @returns {void} Nothing is returned.
         */
        editButton.addEventListener("click", function () {
            startEditingField(fieldName);
        });

        if (allowProfileEditing) {
            /**
             * Opens the editor from a double-click and stops that click from toggling the card.
             *
             * @param {MouseEvent} event Double-click on the field value.
             * @returns {void} Nothing is returned.
             */
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

    /**
     * Builds the direct Save button.
     * The factory leaves the button enabled. updateSaveButtonState later disables it unless the card is dirty, not saving, and the name draft is not blank. Pointer and keyboard activation capture the card view before the click runs. The click calls saveProfileChanges and schedules restoration of that view.
     * @returns {HTMLButtonElement} The Save button, created with disabled set to false.
     */
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

        /**
         * Captures the open card and its scroll positions for the duration of this Save.
         * Save preserves that captured expansion and scroll state.
         * @returns {void}
         */
        function rememberOpenCard() {
            pendingSavedCardView = captureCardView();
            keepSavedCardOpen = true;
        }

        /**
         * Drops a Save pointer or key hold that never became a click.
         *
         * @returns {void} The hold flags clear on the next turn only if the pointer is still recorded as held.
         */
        function releaseUnusedSaveHold() {
            window.removeEventListener("pointerup", releaseUnusedSaveHold, true);
            window.removeEventListener("pointercancel", releaseUnusedSaveHold, true);
            /**
             * Clears the unused Save hold after pointerup has finished dispatching.
             *
             * @returns {void} Nothing is returned.
             */
            window.setTimeout(function () {
                if (!savePointerHeld) {
                    return;
                }

                savePointerHeld = false;
                keepSavedCardOpen = false;
                pendingSavedCardView = null;
            }, 0);
        }

        /**
         * Starts a Save hold from the primary pointer so the card view is captured before the click.
         *
         * @param {PointerEvent} event Other buttons are ignored. The event does not continue to the summary.
         * @returns {void} Nothing is returned.
         */
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

        /**
         * Starts the same Save hold from Enter or Space.
         *
         * @param {KeyboardEvent} event Other keys are ignored.
         * @returns {void} Nothing is returned.
         */
        button.addEventListener("keydown", function (event) {
            if (event.key !== "Enter" && event.key !== " ") {
                return;
            }

            event.stopPropagation();
            savePointerHeld = true;
            rememberOpenCard();
        });

        /**
         * Ends a keyboard Save hold when Enter or Space is released.
         *
         * @param {KeyboardEvent} event Other keys are ignored.
         * @returns {void} Nothing is returned.
         */
        button.addEventListener("keyup", function (event) {
            if (event.key === "Enter" || event.key === " ") {
                releaseUnusedSaveHold();
            }
        });

        /**
         * Runs profile Save and schedules restoration of the captured expansion and scroll.
         * The handler awaits saveProfileChanges, schedules the later restoration, and completes without waiting for those timers.
         * @param {MouseEvent} event The click is stopped. A card that is already saving returns without a second save.
         * @returns {Promise<void>} Fulfills after saveProfileChanges settles, or immediately when a save is already in progress. The restoration timers are not awaited. A save failure is handled inside saveProfileChanges.
         */
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
                /**
                 * Restores the captured card view after Save, ignoring a hold that a newer Save replaced.
                 *
                 * @returns {void} Nothing is returned.
                 */
                window.setTimeout(function () {
                    if (holdToken !== savedCardHoldToken) {
                        return;
                    }

                    restoreCardView(view);
                    /**
                     * Restores the captured view once more and then releases this Save hold.
                     *
                     * @returns {void} Nothing is returned.
                     */
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

    /**
     * Builds the Cancel button. It stays hidden while the card is clean.
     * @returns {HTMLButtonElement} The Cancel button.
     */
    function createProfileCancelButton() {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "member-profile-cancel-button";
        button.textContent = "Cancel";
        button.hidden = true;
        button.disabled = false;

        /**
         * Stops a Cancel pointerdown from reaching the summary.
         *
         * @param {PointerEvent} event Pointer event on Cancel.
         * @returns {void} Nothing is returned.
         */
        button.addEventListener("pointerdown", function (event) {
            event.stopPropagation();
        });

        /**
         * Restores the last accepted values and keeps the card expanded. It does not ask for confirmation.
         * A clean card or a card that is already saving does nothing. The click is stopped so it does not continue to another handler.
         * @param {Event} event Click event.
         * @returns {void}
         */
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
            /**
             * Restores the captured expansion and scroll after Cancel has applied the accepted values.
             *
             * @returns {void} Nothing is returned.
             */
            window.setTimeout(function () {
                restoreCardView(view);
            }, 0);
        });

        return button;
    }

    /**
     * Locks or unlocks the card controls for the duration of Save.
     *
     * @param {boolean} isBusy True disables editors and preview controls and tells change tracking the card is saving.
     * @returns {void} Nothing is returned.
     */
    function setProfileBusy(isBusy) {
        isSaving = isBusy;

        /**
         * Disables or enables one field pencil while the card is busy.
         *
         * @param {string} fieldName Field row name.
         * @returns {void} Nothing is returned.
         */
        Object.keys(fieldRows).forEach(function (fieldName) {
            fieldRows[fieldName].editButton.disabled = isBusy;
        });

        if (activeEdit && activeEdit.editor) {
            activeEdit.editor.disabled = isBusy;
        }

        /**
         * Disables or enables one preview control while the card is busy.
         *
         * @param {Element|null} control A missing control is skipped.
         * @returns {void} Nothing is returned.
         */
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

    /**
     * Reads the trimmed name, pronouns, and description for a legacy edit.
     *
     * @returns {object} displayName, pronouns, and description.
     */
    function buildMemberEditRequest() {
        return {
            displayName: draftValues.displayName.trim(),
            pronouns: draftValues.pronouns.trim(),
            description: draftValues.description.trim()
        };
    }

    /**
     * Copies every current draft into the accepted baseline and the member object.
     *
     * @returns {void} The first call re-enters through withoutChangeNotices so the copy does not mark controls.
     */
    function applySuccessfulSave() {
        if (!applySuccessfulSave.guarded) {
            applySuccessfulSave.guarded = true;
            try {
                /**
                 * Re-enters the baseline copy with change notices suppressed.
                 *
                 * @returns {void} Nothing is returned.
                 */
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

        /**
         * Stores the accepted and draft text on one field display.
         *
         * @param {string} fieldName Field row name.
         * @returns {void} Nothing is returned.
         */
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

    /**
     * Replaces the text and color baseline with values from a persisted profile response.
     *
     * @param {object} profile Returned profile. Missing text becomes empty. Color keeps only a non-blank string.
     * @returns {void} The first call re-enters with change notices suppressed.
     */
    function applyPersistedProfileFields(profile) {
        if (!applyPersistedProfileFields.guarded) {
            applyPersistedProfileFields.guarded = true;
            try {
                /**
                 * Re-enters the persisted profile copy with change notices suppressed.
                 *
                 * @returns {void} Nothing is returned.
                 */
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

        /**
         * Stores one persisted text field on its display.
         *
         * @param {string} fieldName displayName, pronouns, or description. A missing row is skipped.
         * @returns {void} Nothing is returned.
         */
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

    /**
     * Copies returned placement visibility and permission into the accepted baseline.
     *
     * @param {object} profile A missing profile does nothing. A side is copied only when its mode is present.
     * @returns {void} Nothing is returned.
     */
    function applyPersistedPlacement(profile) {
        if (!profile) {
            return;
        }

        /**
         * Applies the returned placement values without marking those controls.
         *
         * @returns {void} Nothing is returned.
         */
        withoutChangeNotices(function () {
            if (profile.placementVisibilityMode) {
                persistedValues.visibilityKnown = true;
                persistedValues.visibilityInherit = profile.placementVisibilityMode === "inherit";
                persistedValues.visibilityTypeId = profile.placementVisibilityMode === "explicit"
                    ? canonicalVisibilityId(profile.placementVisibilityTypeId)
                    : null;
                draftValues.visibilityKnown = persistedValues.visibilityKnown;
                draftValues.visibilityInherit = persistedValues.visibilityInherit;
                draftValues.visibilityTypeId = persistedValues.visibilityTypeId;
            }

            if (profile.placementPermissionMode) {
                persistedValues.permission = profile.placementPermissionMode === "explicit"
                    ? profile.placementPermissionCode
                    : (profile.placementPermissionMode === "inherit" ? "inherit" : null);
                draftValues.permission = persistedValues.permission;
            }

            paintPreviewControls();
        });
    }

    /**
     * Lists the controls change tracking currently marks on this card.
     *
     * @returns {Array<Element>} An empty list when tracking is missing.
     */
    function markedControls() {
        const changes = changeTracking();
        return changes ? changes.modified(cardDetails) : [];
    }

    /**
     * Reports whether one control is in the marked set.
     *
     * @param {Element} control Control to test. A missing control is not marked.
     * @returns {boolean} True when the control is marked.
     */
    function controlIsMarked(control) {
        return !!(control && markedControls().indexOf(control) >= 0);
    }

    /**
     * Keeps only marked controls that belong to the profile text and color set.
     *
     * @param {Array<Element>} before Controls marked before the request.
     * @returns {Array<Element>} The profile controls from that list.
     */
    function savedProfileControls(before) {
        /**
         * Keeps a marked control when it is one of the profile controls.
         *
         * @param {Element} control Control from the before list.
         * @returns {boolean} True when the control is in profileControls.
         */
        return before.filter(function (control) {
            return profileControls.indexOf(control) >= 0;
        });
    }

    /**
     * Publishes the accepted profile text, plus color and latched extras when the arguments include them.
     * Name, Pronouns, and Description are always published. Color is published only when includeColor is true. Fronting, visibility, permission, and avatar are published only when includeLatchedExtras is true and that control is in the pre-save marked list.
     * @param {Array<Element>} before Controls marked before the request. This list selects the latched extras. It does not select Name, Pronouns, or Description.
     * @param {boolean} includeColor True publishes the accepted color.
     * @param {boolean} includeLatchedExtras True allows Fronting, visibility, permission, and avatar when those controls are in before. A persisted profile save passes false.
     * @returns {void} A missing tracker does nothing.
     */
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

    /**
     * Saves the marked profile controls.
     * An in-flight Fronting request is awaited first. A disconnected card does not apply the response. persisted true clears only controls the response confirms. A missing avatar confirmation keeps the preview and its marker. If Fronting is still marked and saveFronting exists, it is saved after the profile commit and a failure leaves that marker. A result that is not persisted updates the local baseline and does not claim the database stored it. A thrown failure leaves the markers in place.
     * @returns {Promise<{saved: boolean, message?: string}>} saved true when the accepted path finished. Fronting failure after a persisted profile returns saved false. Rejection from the profile request is caught and returned as saved false.
     */
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

        if (frontingRequest) {
            const pendingFront = await frontingRequest;
            if (!pendingFront || pendingFront.saved !== true) {
                return {
                    saved: false,
                    message: pendingFront && pendingFront.message
                        ? pendingFront.message
                        : "Fronting could not be saved."
                };
            }
        }

        const before = markedControls().slice();
        const changes = changeTracking();
        const includeColor = before.indexOf(colorSwatch) >= 0;
        if (before.indexOf(avatarWrap) >= 0 && !pendingAvatar) {
            const message = "Choose a PNG, JPEG, or WebP image before saving.";
            setProfileStatus(message, "error");
            return { saved: false, message: message };
        }

        if (options && typeof options.saveProfile === "function") {
            setProfileBusy(true);
            setProfileStatus("Saving member.");

            try {
                const outcome = await options.saveProfile(profilePreviewSaveRequest(memberId, request));

                // A card removed while the request was in flight must not apply
                // the response to a replacement card.
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

                // persisted means the server stored the profile. Only controls
                // the response confirms are cleared. A missing avatar
                // confirmation keeps the preview and its marker. Fronting,
                // when this card saves it immediately, is committed after
                // the profile and keeps its marker if that call fails.
                // Anything else is a local preview: the draft becomes the
                // card baseline, but it was not stored.
                if (outcome.persisted === true) {
                    if (!outcome.profile) {
                        const message = "The member profile save did not return the saved values.";
                        setProfileStatus(message, "error");
                        return { saved: false, message: message };
                    }

                    applyPersistedProfileFields(outcome.profile);
                    applyPersistedPlacement(outcome.profile);
                    if (changes) {
                        const saved = savedProfileControls(before);
                        if (Object.prototype.hasOwnProperty.call(outcome.profile, "placementVisibilityMode")
                            && outcome.profile.placementVisibilityMode) {
                            saved.push(sharingButton);
                        }
                        if (permissionControl
                            && Object.prototype.hasOwnProperty.call(outcome.profile, "placementPermissionMode")
                            && outcome.profile.placementPermissionMode) {
                            saved.push(permissionControl);
                        }
                        if (outcome.profile.avatarSaved === true && outcome.profile.avatarUrl) {
                            const previousPreview = pendingAvatar && pendingAvatar.objectUrl;
                            pendingAvatar = null;
                            if (previousPreview && previousPreview !== outcome.profile.avatarUrl && typeof URL.revokeObjectURL === "function") {
                                URL.revokeObjectURL(previousPreview);
                            }
                            persistedValues.avatarObjectUrl = outcome.profile.avatarUrl;
                            draftValues.avatarObjectUrl = outcome.profile.avatarUrl;
                            if (member) {
                                member.avatarObjectUrl = outcome.profile.avatarUrl;
                            }
                            paintAvatar();
                            saved.push(avatarWrap);
                        }
                        changes.commit(cardDetails, saved);
                    }
                    publishConfirmed(before, true, false);
                    if (outcome.profile.avatarSaved === true && outcome.profile.avatarUrl && changes) {
                        changes.publishShared(getMemberIdForSave(), {
                            avatarObjectUrl: outcome.profile.avatarUrl
                        }, cardDetails);
                    }
                    if (before.indexOf(avatarWrap) >= 0 && !(outcome.profile.avatarSaved === true && outcome.profile.avatarUrl)) {
                        const message = "The avatar was not saved.";
                        setProfileStatus(message, "error");
                        return { saved: false, message: message };
                    }
                    if (before.indexOf(frontingButton) >= 0 && options && typeof options.saveFronting === "function") {
                        const front = await saveFrontingNow(draftValues.fronting);
                        if (!front || front.saved !== true) {
                            return {
                                saved: false,
                                message: front && front.message ? front.message : "Fronting could not be saved."
                            };
                        }
                    }
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
            /**
             * Copies the legacy edit's text into the baseline without marking the fields.
             *
             * @returns {void} Nothing is returned.
             */
            withoutChangeNotices(function () {
                persistedValues.displayName = draftValues.displayName;
                persistedValues.pronouns = draftValues.pronouns;
                persistedValues.description = draftValues.description;
                /**
                 * Stores one legacy-saved text field on its display.
                 *
                 * @param {string} fieldName displayName, pronouns, or description. A missing row is skipped.
                 * @returns {void} Nothing is returned.
                 */
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
                /**
                 * Leaves the color swatch marked after the legacy edit, which does not send color.
                 *
                 * @param {Element} control Marked profile control.
                 * @returns {boolean} False for the color swatch.
                 */
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

    /**
     * Records whether the card is open and the scroll positions of its scrolling ancestors.
     *
     * @returns {object} open and a positions list of node, top, and left.
     */
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

    /**
     * Puts back a captured card expansion and scroll position.
     *
     * @param {object} view Value from captureCardView. A missing view or a disconnected card does nothing.
     * @returns {void} Nothing is returned.
     */
    function restoreCardView(view) {
        if (!view || !cardDetails.isConnected) {
            return;
        }
        if (view.open) {
            cardDetails.open = true;
        }
        /**
         * Restores one captured scroll position.
         *
         * @param {object} position node, top, and left. A missing or disconnected node is skipped.
         * @returns {void} Nothing is returned.
         */
        view.positions.forEach(function (position) {
            if (!position.node || !position.node.isConnected) {
                return;
            }
            position.node.scrollTop = position.top;
            position.node.scrollLeft = position.left;
        });
    }

    const previewHeader = Boolean(options && options.previewHeader === true);

    /**
     * Turns an empty preview value into null and keeps a boolean.
     *
     * @param {*} value Preview field. Undefined, null, and an empty string become null.
     * @returns {*} The boolean or other value, or null.
     */
    function nullablePreviewValue(value) {
        if (value === true || value === false) {
            return value;
        }

        if (value === undefined || value === null || value === "") {
            return null;
        }

        return value;
    }

    /**
     * Accepts only a #RRGGBB color for CSS painting.
     *
     * @param {*} value Candidate color.
     * @returns {string|null} The matching color, or null.
     */
    function memberCssColor(value) {
        return typeof value === "string" && /^#[0-9A-Fa-f]{6}$/.test(value) ? value : null;
    }

    /**
     * Keeps a stored color string, including values that are not #RRGGBB.
     *
     * @param {*} value Stored color. A non-string or blank string becomes null.
     * @returns {string|null} The trimmed color.
     */
    function storedMemberColor(value) {
        if (typeof value !== "string") {
            return null;
        }

        const trimmed = value.trim();
        return trimmed ? trimmed : null;
    }

    /**
     * Builds the generated avatar fill and border from the member seed.
     *
     * @returns {object} fill and border HSL colors.
     */
    function generatedMemberColor() {
        const hue = hashStringToInt(getAvatarColorSeed()) % 360;
        return {
            fill: "hsl(" + hue + " 52% 28%)",
            border: "hsl(" + hue + " 56% 48%)"
        };
    }

    /**
     * Uses the draft color when it is #RRGGBB, and the generated color otherwise.
     *
     * @returns {object} fill, border, and explicit.
     */
    function effectiveMemberColor() {
        const explicit = memberCssColor(draftValues.color);
        return explicit
            ? { fill: explicit, border: explicit, explicit: true }
            : Object.assign({ explicit: false }, generatedMemberColor());
    }

    /**
     * Builds the profile request from the current draft.
     * Name, Pronouns, and Description are always included. Color, fronting, visibility, permission, and avatar bytes are included only when that control is marked. Color must not be sent merely because the card knows the current value.
     * @param {string} memberId Member id copied onto the request.
     * @param {object} request Text fields already read from the card. displayName, pronouns, and description are copied even when unmarked.
     * @returns {object} The request. Unmarked optional fields are absent, not null.
     */
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
            if (draftValues.visibilityInherit === true) {
                payload.visibilityMode = "inherit";
                payload.visibilityTypeId = null;
            } else if (draftValues.visibilityTypeId === 1 || draftValues.visibilityTypeId === 2 || draftValues.visibilityTypeId === 3) {
                payload.visibilityMode = "explicit";
                payload.visibilityTypeId = draftValues.visibilityTypeId;
            } else {
                payload.visibilityMode = "unset";
                payload.visibilityTypeId = null;
            }
            payload.visibilityInherit = payload.visibilityMode === "inherit";
        }

        if (changes && permissionControl && marked.indexOf(permissionControl) >= 0) {
            payload.permission = draftValues.permission;
            if (draftValues.permission === "inherit") {
                payload.permissionMode = "inherit";
                payload.permissionCode = null;
            } else if (draftValues.permission === "viewer" || draftValues.permission === "editor" || draftValues.permission === "manager" || draftValues.permission === "owner") {
                payload.permissionMode = "explicit";
                payload.permissionCode = draftValues.permission;
            } else {
                payload.permissionMode = "unset";
                payload.permissionCode = null;
            }
        }

        if (changes && marked.indexOf(avatarWrap) >= 0 && pendingAvatar) {
            payload.avatarContentType = pendingAvatar.contentType;
            payload.avatarImageBase64 = pendingAvatar.base64;
            payload.avatarObjectUrl = draftValues.avatarObjectUrl;
        }

        return payload;
    }

    /**
     * Keeps visibility type ids 1 through 4.
     *
     * @param {*} value Candidate id.
     * @returns {number|null} The id, or null.
     */
    function canonicalVisibilityId(value) {
        return value === 1 || value === 2 || value === 3 || value === 4 ? value : null;
    }

    /**
     * Drafts are what the card shows. The following persistedValues object is the last accepted baseline.
     * Markers compare controls with their change-tracking snapshots. Typing a field back to the original text does not clear its marker.
     */
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
    avatarFile.accept = "image/png,image/jpeg,image/webp";
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
            /**
             * Reads the permission draft for the permissions menu.
             *
             * @returns {string} The permission, or an empty string when it is unset.
             */
            getValue: function () {
                return draftValues.permission || "";
            },
            /**
             * Stores a permissions-menu choice in the draft and marks the control.
             *
             * @param {*} next Selected id. An empty value stores null. The same value does not notify.
             * @returns {void} Nothing is returned.
             */
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
        /**
         * Stops a permissions-label pointerdown from toggling the card.
         *
         * @param {MouseEvent} event Pointerdown on the label.
         * @returns {void} Nothing is returned.
         */
        permissionLabel.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        /**
         * Stops a permissions-label click from toggling the card.
         *
         * @param {MouseEvent} event Click on the label.
         * @returns {void} Nothing is returned.
         */
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
    profileActions.appendChild(unsavedIndicator);
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

    /**
     * Keeps a dirty card open and preserves a Save hold. A leave guard restores the expansion it recorded.
     *
     * @returns {void} A preview card also toggles its chevron when none of those holds apply.
     */
    cardDetails.addEventListener("toggle", function () {
        if (cardDetails.dataset.pbLeave) {
            const holdOpen = cardDetails.dataset.pbLeave === "1";
            if (cardDetails.open !== holdOpen) {
                cardDetails.open = holdOpen;
            }
            return;
        }

        // A dirty card is kept open. While a Save hold is active, the held
        // expansion and scroll state are preserved.
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

    let pendingAvatar = null;
    let frontingRequest = null;
    /**
     * Placeholder Fronting save used until the preview card replaces it.
     *
     * @returns {Promise<{saved: boolean, message: string}>} Always fulfills with saved false.
     */
    let saveFrontingNow = function () {
        return Promise.resolve({
            saved: false,
            message: "Fronting could not be saved."
        });
    };

    /**
     * Revokes a blob preview that is not the accepted avatar.
     *
     * @returns {void} pendingAvatar is cleared. A non-blob or accepted URL is left in place.
     */
    function releaseUnsavedAvatar() {
        const previewUrl = pendingAvatar && pendingAvatar.objectUrl
            ? pendingAvatar.objectUrl
            : draftValues.avatarObjectUrl;
        if (
            previewUrl &&
            previewUrl !== persistedValues.avatarObjectUrl &&
            typeof URL.revokeObjectURL === "function" &&
            String(previewUrl).indexOf("blob:") === 0
        ) {
            URL.revokeObjectURL(previewUrl);
        }
        pendingAvatar = null;
    }

    /**
     * Reads a blob as base64 without the data-URL prefix.
     *
     * @param {Blob} blob Image bytes.
     * @returns {Promise<string>} Fulfills with the base64 text. Rejects when the reader fails.
     */
    function blobToBase64(blob) {
        /**
         * Reads the file and resolves the base64 payload.
         *
         * @param {function} resolve Called with the base64 text.
         * @param {function} reject Called when the reader fails.
         * @returns {void} Nothing is returned.
         */
        return new Promise(function (resolve, reject) {
            const reader = new FileReader();
            /**
             * Strips the data-URL prefix from the reader result.
             *
             * @returns {void} Nothing is returned.
             */
            reader.onload = function () {
                const text = String(reader.result || "");
                const comma = text.indexOf(",");
                resolve(comma >= 0 ? text.slice(comma + 1) : text);
            };
            /**
             * Rejects the base64 read when the file reader fails.
             *
             * @returns {void} Nothing is returned.
             */
            reader.onerror = function () {
                reject(new Error("The image could not be read."));
            };
            reader.readAsDataURL(blob);
        });
    }

    /**
     * Loads an image URL so its decoded size can be measured.
     *
     * @param {string} url Object URL or other image source.
     * @returns {Promise<HTMLImageElement>} Fulfills with the loaded image. Rejects when the image fails to load.
     */
    function loadAvatarImage(url) {
        /**
         * Starts the image load.
         *
         * @param {function} resolve Called with the image.
         * @param {function} reject Called when the image fails.
         * @returns {void} Nothing is returned.
         */
        return new Promise(function (resolve, reject) {
            const image = new Image();
            /**
             * Resolves the image load.
             *
             * @returns {void} Nothing is returned.
             */
            image.onload = function () {
                resolve(image);
            };
            /**
             * Rejects the image load.
             *
             * @returns {void} Nothing is returned.
             */
            image.onerror = function () {
                reject(new Error("Choose a PNG, JPEG, or WebP image."));
            };
            image.src = url;
        });
    }

    /**
     * Encodes a canvas to an image blob.
     *
     * @param {HTMLCanvasElement} canvas Scaled image.
     * @param {string} type MIME type passed to toBlob.
     * @param {number} quality Encoder quality.
     * @returns {Promise<Blob>} Fulfills with the blob. Rejects when the browser returns no blob.
     */
    function canvasToBlob(canvas, type, quality) {
        /**
         * Receives the canvas blob.
         *
         * @param {function} resolve Called with the blob.
         * @param {function} reject Called when encoding fails.
         * @returns {void} Nothing is returned.
         */
        return new Promise(function (resolve, reject) {
            /**
             * Rejects an empty canvas result and resolves a blob.
             *
             * @param {Blob|null} blob Encoded image, or null when encoding failed.
             * @returns {void} Nothing is returned.
             */
            canvas.toBlob(function (blob) {
                if (!blob) {
                    reject(new Error("The image could not be read."));
                    return;
                }
                resolve(blob);
            }, type, quality);
        });
    }

    /**
     * Accepts a PNG, JPEG, or WebP file and returns bytes that fit a 2048 pixel edge and 2 MiB.
     * An already fitting file is returned unchanged. A larger file is drawn to a canvas. An oversized PNG is encoded as WebP. The source object URL is revoked.
     *
     * @param {File} file Chosen file. Another MIME type rejects.
     * @returns {Promise<object>} Fulfills with contentType, base64, and blob. Rejects when the type, decode, canvas, or final size fails.
     */
    async function normalizeAvatarFile(file) {
        const allowed = {
            "image/png": true,
            "image/jpeg": true,
            "image/webp": true
        };
        if (!file || !allowed[file.type]) {
            throw new Error("Choose a PNG, JPEG, or WebP image.");
        }

        const maxEdge = 2048;
        const maxBytes = 2 * 1024 * 1024;
        const sourceUrl = URL.createObjectURL(file);
        try {
            const image = await loadAvatarImage(sourceUrl);
            const sourceWidth = image.naturalWidth || image.width;
            const sourceHeight = image.naturalHeight || image.height;
            if (!sourceWidth || !sourceHeight) {
                throw new Error("The image could not be read.");
            }

            if (sourceWidth <= maxEdge && sourceHeight <= maxEdge && file.size <= maxBytes) {
                return {
                    contentType: file.type,
                    base64: await blobToBase64(file),
                    blob: file
                };
            }

            const scale = Math.min(1, maxEdge / Math.max(sourceWidth, sourceHeight));
            const canvas = document.createElement("canvas");
            canvas.width = Math.max(1, Math.round(sourceWidth * scale));
            canvas.height = Math.max(1, Math.round(sourceHeight * scale));
            const context = canvas.getContext("2d");
            if (!context) {
                throw new Error("The image could not be read.");
            }
            context.drawImage(image, 0, 0, canvas.width, canvas.height);
            const type = file.type === "image/png" ? "image/webp" : file.type;
            let quality = 0.92;
            let blob = await canvasToBlob(canvas, type, quality);
            while (blob.size > maxBytes && quality > 0.5) {
                quality -= 0.08;
                blob = await canvasToBlob(canvas, type, quality);
            }
            if (blob.size > maxBytes) {
                throw new Error("The image is still larger than 2 MiB after resizing.");
            }
            return {
                contentType: blob.type || type,
                base64: await blobToBase64(blob),
                blob: blob
            };
        } finally {
            URL.revokeObjectURL(sourceUrl);
        }
    }

    /**
     * Paints the avatar image or the generated placeholder.
     *
     * @returns {void} A card without a preview header paints only the generated placeholder.
     */
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

    /**
     * Paints the color strip and swatch from the effective color.
     *
     * @returns {void} A generated color is labeled. An explicit #RRGGBB color is copied into the picker without marking it.
     */
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
            /**
             * Copies the explicit color into the picker without treating that write as an edit.
             *
             * @returns {void} Nothing is returned.
             */
            withoutChangeNotices(function () {
                colorEcho += 1;
                paintingColor = true;
                colorInput.value = appearance.fill;
                paintingColor = false;
            });
            /**
             * Drops the color-echo guard after the picker has applied the programmatic value.
             *
             * @returns {void} Nothing is returned.
             */
            window.setTimeout(function () {
                colorEcho = Math.max(0, colorEcho - 1);
            }, 0);
        }
    }

    /**
     * Stores a user color-picker change in the draft.
     *
     * @returns {void} Ignored before the card accepts edits, during a programmatic paint, or while saving.
     */
    function commitPickerColor() {
        if (!acceptColorEdits || paintingColor || colorEcho || isSaving) {
            return;
        }

        const nextColor = memberCssColor(colorInput.value);
        draftValues.color = nextColor;
        paintColor();
        updateSaveButtonState();
    }

    /**
     * Redraws the Fronting button from the current draft.
     *
     * @returns {void} Null is Not loaded, true is Fronting, and false is Not fronting. The pressed state matches that value.
     */
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

    /**
     * Builds the visibility menu choices for the current draft.
     *
     * @returns {Array<object>} Not loaded or Not set, Inherit, Only you, Trusted friends, All friends, and the disabled Selected people choice.
     */
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

    /**
     * Finds the visibility choice that matches the draft.
     *
     * @returns {object} The matching choice, or the first choice.
     */
    function currentSharingChoice() {
        const choices = visibilityChoices();
        if (!draftValues.visibilityKnown) {
            return choices[0];
        }

        if (draftValues.visibilityInherit === true) {
            /**
             * Selects the Inherit choice.
             *
             * @param {object} choice Menu choice.
             * @returns {boolean} True when the choice is Inherit.
             */
            return choices.find(function (choice) {
                return choice.inherit === true;
            }) || choices[0];
        }

        /**
         * Selects the explicit visibility choice with the draft type id.
         *
         * @param {object} choice Menu choice.
         * @returns {boolean} True when the choice is the current explicit type.
         */
        const selected = choices.find(function (choice) {
            return choice.known && choice.inherit !== true && choice.id === draftValues.visibilityTypeId;
        });
        return selected || choices[0];
    }

    /**
     * Redraws the visibility button and its option list from the draft.
     *
     * @returns {void} The button title describes loaded, inherit, unset, or saved visibility.
     */
    function paintSharing() {
        const choices = visibilityChoices();
        const choice = currentSharingChoice();
        const selectedIndex = choices.indexOf(choice);
        sharingList.replaceChildren();
        /**
         * Builds one visibility option.
         *
         * @param {object} item Choice id, label, icon, known, inherit, and disabled.
         * @param {number} index Option index.
         * @returns {HTMLDivElement} The option element.
         */
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
            sharingButton.title = "Visibility is not loaded.";
        } else if (draftValues.visibilityInherit === true) {
            sharingButton.title = "Visibility is Inherit. " + resolvedVisibilityText();
        } else if (draftValues.visibilityTypeId === null) {
            sharingButton.title = "Visibility is not set.";
        } else {
            sharingButton.title = "Visibility is saved for this member on the selected anchor.";
        }
        sharingButton.setAttribute("aria-label", "Visible to: " + choice.label);
    }

    /**
     * Places the open visibility list under the button, or above it when there is no room.
     *
     * @returns {void} A hidden list is left alone.
     */
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

    /**
     * Opens or closes the visibility list and moves focus into it.
     *
     * @param {boolean} open True opens the list.
     * @returns {void} Nothing is returned.
     */
    function setSharingOpen(open) {
        sharingList.hidden = !open;
        sharingButton.setAttribute("aria-expanded", open ? "true" : "false");
        cardDetails.classList.toggle("is-sharing-open", open);
        paintSharing();
        if (!open) {
            sharingList.removeAttribute("aria-activedescendant");
            return;
        }

        /**
         * Finds the option that should receive focus when the list opens.
         *
         * @param {HTMLElement} option Option element.
         * @returns {boolean} True when the option is selected.
         */
        const selected = sharingOptions.findIndex(function (option) {
            return option.getAttribute("aria-selected") === "true";
        });
        const index = selected < 0 ? 0 : selected;
        sharingOptions[index].focus();
        sharingList.setAttribute("aria-activedescendant", sharingOptions[index].id);
        placeSharingList();
    }

    /**
     * Stores a visibility choice and marks the control.
     *
     * @param {HTMLElement} option Chosen option. A disabled or not-loaded option only closes the list.
     * @returns {void} Nothing is returned.
     */
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

    /**
     * Moves to the next enabled visibility option, stopping at the list ends.
     *
     * @param {number} start Current index.
     * @param {number} step 1 moves down and -1 moves up.
     * @returns {number} The next enabled index, or start when none is available.
     */
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

    /**
     * Closes the visibility list when a pointer goes outside it.
     *
     * @param {PointerEvent} event Document pointerdown. A target inside the control is ignored.
     * @returns {void} Nothing is returned.
     */
    function dismissSharing(event) {
        if (sharingList.hidden || sharingControl.contains(event.target)) {
            return;
        }

        setSharingOpen(false);
    }

    /**
     * Describes how the loaded branch resolves an Inherit visibility value.
     *
     * @returns {string} The resolved label, Not set, or the note that Inherit stays Inherit at the top anchor.
     */
    function resolvedVisibilityText() {
        const mode = member && member.resolvedVisibilityMode;
        if (mode === "explicit") {
            const labels = { 1: "Only you", 2: "Trusted friends", 3: "All friends" };
            const label = labels[member.resolvedVisibilityTypeId];
            return label
                ? "In this branch that resolves to " + label + "."
                : "In this branch that resolves to an explicit visibility.";
        }
        if (mode === "unset") {
            return "In this branch that resolves to Not set.";
        }
        return "This branch ends at the top anchor, so Inherit stays Inherit.";
    }

    /**
     * Repaints avatar, color, Fronting, visibility, and permissions.
     *
     * @returns {void} A card without a preview header paints only the avatar placeholder.
     */
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
        if (permissionControl && draftValues.permission === "inherit") {
            const mode = member && member.resolvedPermissionMode;
            let resolved = "This branch ends at the top anchor, so Inherit stays Inherit.";
            if (mode === "explicit" && member.resolvedPermissionCode) {
                resolved = "In this branch that resolves to " + member.resolvedPermissionCode + ".";
            } else if (mode === "unset") {
                resolved = "In this branch that resolves to Not set.";
            }
            permissionControl.title = "Permissions are Inherit. " + resolved;
        }
    }

    /**
     * Stops a header control click from toggling the card.
     *
     * @param {Event} event Click or pointer event.
     * @returns {void} Nothing is returned.
     */
    function keepHeaderClick(event) {
        event.preventDefault();
        event.stopPropagation();
    }

    if (previewHeader) {
        /**
         * Opens the avatar file chooser without toggling the card.
         *
         * @param {Event} event Click on the pencil or avatar frame.
         * @returns {void} Nothing is returned.
         */
        function openAvatarChooser(event) {
            keepHeaderClick(event);
            avatarFile.click();
        }
        avatarPencil.addEventListener("mousedown", keepHeaderClick);
        avatarPencil.addEventListener("click", openAvatarChooser);
        /**
         * Stops an avatar pointerdown, except the file input itself, from toggling the card.
         *
         * @param {MouseEvent} event Pointerdown on the avatar.
         * @returns {void} Nothing is returned.
         */
        avatarWrap.addEventListener("mousedown", function (event) {
            if (event.target === avatarFile) {
                return;
            }
            event.stopPropagation();
        });
        /**
         * Opens the avatar chooser from the frame, leaving the pencil and file input to their own handlers.
         *
         * @param {MouseEvent} event Click on the avatar.
         * @returns {void} Nothing is returned.
         */
        avatarWrap.addEventListener("click", function (event) {
            if (event.target === avatarFile || event.target === avatarPencil || avatarPencil.contains(event.target)) {
                return;
            }
            openAvatarChooser(event);
        });
        /**
         * Stops the file input click from also toggling the card.
         *
         * @param {MouseEvent} event Click on the file input.
         * @returns {void} Nothing is returned.
         */
        avatarFile.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        /**
         * Stores a chosen avatar as a local preview and marks the avatar.
         * An empty selection, including chooser cancel, returns without changing the image or marker. A decode that finishes after a newer choice or after the card is removed is ignored.
         *
         * @returns {void} Nothing is returned.
         */
        avatarFile.addEventListener("change", function () {
            const file = avatarFile.files && avatarFile.files[0] ? avatarFile.files[0] : null;
            avatarFile.value = "";
            // Cancelling the chooser leaves the current image and marker.
            // The token drops a decode that finished after a newer choice.
            if (!file) {
                return;
            }
            const requestToken = String(Date.now()) + ":" + file.name + ":" + file.size;
            avatarFile.dataset.normalizeToken = requestToken;
            /**
             * Installs the normalized preview when this choice is still the latest one.
             *
             * @param {object} normalized contentType, base64, and blob from normalizeAvatarFile.
             * @returns {void} The previous unaccepted blob is released.
             */
            normalizeAvatarFile(file).then(function (normalized) {
                if (!cardDetails.isConnected || avatarFile.dataset.normalizeToken !== requestToken) {
                    return;
                }
                releaseUnsavedAvatar();
                const objectUrl = URL.createObjectURL(normalized.blob);
                pendingAvatar = {
                    contentType: normalized.contentType,
                    base64: normalized.base64,
                    objectUrl: objectUrl
                };
                draftValues.avatarObjectUrl = objectUrl;
                notifyEdited(avatarWrap);
                paintAvatar();
                updateSaveButtonState();
            /**
             * Shows the avatar error on a card that is still connected.
             *
             * @param {Error} error Rejection from normalizeAvatarFile.
             * @returns {void} Nothing is returned.
             */
            }, function (error) {
                if (!cardDetails.isConnected) {
                    return;
                }
                setProfileStatus(error && error.message ? error.message : "Choose a PNG, JPEG, or WebP image.", "error");
            });
        });
        /**
         * Stops a visibility-label pointerdown from toggling the card.
         *
         * @param {MouseEvent} event Pointerdown on the label.
         * @returns {void} Nothing is returned.
         */
        sharingLabel.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        /**
         * Stops a visibility-label click from toggling the card.
         *
         * @param {MouseEvent} event Click on the label.
         * @returns {void} Nothing is returned.
         */
        sharingLabel.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        /**
         * Stops a visibility-control pointerdown from toggling the card.
         *
         * @param {MouseEvent} event Pointerdown on the control.
         * @returns {void} Nothing is returned.
         */
        sharingControl.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        /**
         * Stops a visibility-control click from toggling the card.
         *
         * @param {MouseEvent} event Click on the control.
         * @returns {void} Nothing is returned.
         */
        sharingControl.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        /**
         * Toggles the visibility list.
         *
         * @param {MouseEvent} event Click on the visibility button.
         * @returns {void} Nothing is returned.
         */
        sharingButton.addEventListener("click", function (event) {
            keepHeaderClick(event);
            setSharingOpen(sharingList.hidden);
        });
        /**
         * Opens the visibility list from the arrow keys, Enter, or Space, and closes it on Escape.
         *
         * @param {KeyboardEvent} event Keydown on the visibility button.
         * @returns {void} Nothing is returned.
         */
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
        /**
         * Keeps a pointer on an option from moving focus before the click chooses it.
         *
         * @param {MouseEvent} event Pointerdown in the list.
         * @returns {void} Nothing is returned.
         */
        sharingList.addEventListener("mousedown", function (event) {
            if (event.target.closest(".member-sharing-option")) {
                event.preventDefault();
                event.stopPropagation();
            }
        });
        /**
         * Chooses the visibility option under the pointer.
         *
         * @param {MouseEvent} event Click in the list. A click outside an option returns.
         * @returns {void} Nothing is returned.
         */
        sharingList.addEventListener("click", function (event) {
            const option = event.target.closest(".member-sharing-option");
            if (!option) {
                return;
            }

            keepHeaderClick(event);
            chooseSharing(option);
        });
        /**
         * Moves, chooses, or closes the visibility list from the keyboard.
         *
         * @param {KeyboardEvent} event Arrow keys, Home, End, Enter, Space, Escape, and Tab.
         * @returns {void} Nothing is returned.
         */
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
        /**
         * Stops a Fronting pointerdown from toggling the card.
         *
         * @param {MouseEvent} event Pointerdown on Fronting.
         * @returns {void} Nothing is returned.
         */
        frontingButton.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        /**
         * Persists Fronting immediately, separately from profile Save. Only one request runs at a time. A second call returns the in-flight promise.
         * Success clears only this marker and publishes the returned boolean. Failure leaves the draft and the marker so Discard or another click can retry. A removed card does not apply the response. The promise fulfills with saved true or saved false. It does not reject for a failed save.
         * @param {boolean} next Requested fronting state.
         * @returns {Promise<{saved: boolean, message?: string}>} The outcome. A missing saveFronting callback fulfills with saved false.
         */
        saveFrontingNow = function (next) {
            if (!options || typeof options.saveFronting !== "function") {
                return Promise.resolve({
                    saved: false,
                    message: "Fronting could not be saved."
                });
            }

            if (frontingRequest) {
                return frontingRequest;
            }

            frontingButton.disabled = true;
            /**
             * Applies a Fronting response to a card that is still connected.
             *
             * @param {object} outcome saved and fronting from the save. Success clears only the Fronting marker and publishes the boolean.
             * @returns {object} saved true, or saved false with a message. A failed response keeps the draft and marker.
             */
            frontingRequest = Promise.resolve(options.saveFronting(next === true)).then(function (outcome) {
                if (!cardDetails.isConnected) {
                    return { saved: false, message: "Fronting could not be saved." };
                }

                if (outcome && outcome.saved === true && (outcome.fronting === true || outcome.fronting === false)) {
                    /**
                     * Stores the confirmed Fronting value without marking the button.
                     *
                     * @returns {void} Nothing is returned.
                     */
                    withoutChangeNotices(function () {
                        persistedValues.fronting = outcome.fronting;
                        draftValues.fronting = outcome.fronting;
                        if (member) {
                            member.fronting = outcome.fronting;
                        }
                        paintFronting();
                    });
                    const changes = changeTracking();
                    if (changes) {
                        changes.commit(cardDetails, [frontingButton]);
                        changes.publishShared(getMemberIdForSave(), {
                            fronting: outcome.fronting
                        }, cardDetails);
                    }
                    return { saved: true };
                }

                const message = outcome && outcome.status ? outcome.status : "Fronting could not be saved.";
                setProfileStatus(message, "error");
                paintFronting();
                return { saved: false, message: message };
            /**
             * Turns a rejected Fronting request into a saved-false result.
             *
             * @returns {object} saved false and the error message shown on the card.
             */
            }, function () {
                const message = "Fronting could not be saved.";
                setProfileStatus(message, "error");
                return { saved: false, message: message };
            });
            /**
             * Clears the in-flight Fronting request and enables the button again.
             *
             * @returns {void} Nothing is returned.
             */
            frontingRequest.finally(function () {
                frontingRequest = null;
                frontingButton.disabled = false;
                updateSaveButtonState();
            });
            return frontingRequest;
        }

        /**
         * Toggles the Fronting draft and marks the button.
         * Null becomes true. When saveFronting exists, the new value is persisted immediately. A card that is saving or already has a Fronting request returns.
         *
         * @param {MouseEvent} event Click on Fronting.
         * @returns {void} Nothing is returned.
         */
        frontingButton.addEventListener("click", function (event) {
            keepHeaderClick(event);
            if (isSaving || frontingRequest) {
                return;
            }
            draftValues.fronting = draftValues.fronting === null ? true : !draftValues.fronting;
            notifyEdited(frontingButton);
            paintFronting();
            updateSaveButtonState();
            if (options && typeof options.saveFronting === "function") {
                saveFrontingNow(draftValues.fronting);
            }
        });
        /**
         * Opens the color picker from the pencil.
         *
         * @returns {void} Nothing is returned.
         */
        colorPencil.addEventListener("click", function () {
            colorInput.click();
        });
        /**
         * Opens the color picker from the swatch.
         *
         * @returns {void} Nothing is returned.
         */
        colorSwatch.addEventListener("click", function () {
            colorInput.click();
        });
        colorInput.addEventListener("input", commitPickerColor);
        colorInput.addEventListener("change", commitPickerColor);

        /**
         * Watches for card removal and releases the preview and listeners.
         *
         * @returns {void} A card that is already disconnected does nothing.
         */
        queueMicrotask(function () {
            if (!cardDetails.isConnected || !document.body) {
                return;
            }

            /**
             * Releases the avatar preview, change tracking, and visibility listeners after the card leaves the document.
             *
             * @returns {void} Nothing is returned.
             */
            const removalObserver = new MutationObserver(function () {
                if (!cardDetails.isConnected) {
                    releaseUnsavedAvatar();
                    // A loaded image is a blob URL stored as the persisted value. The unsaved-preview release leaves that URL in place.
                    if (
                        persistedValues.avatarObjectUrl
                        && String(persistedValues.avatarObjectUrl).indexOf("blob:") === 0
                        && typeof URL.revokeObjectURL === "function"
                    ) {
                        URL.revokeObjectURL(persistedValues.avatarObjectUrl);
                    }
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

    /**
     * Reports whether a control currently carries the modified class.
     *
     * @param {Element} control Control to test.
     * @returns {boolean} True when the marker class is present.
     */
    function surfaceIsModified(control) {
        const marker = modifiedMarker();
        return !!(control && control.classList && control.classList.contains(marker));
    }

    /**
     * Reports whether a text field display or its open editor is marked.
     *
     * @param {string} fieldName Profile field name.
     * @returns {boolean} True when either surface is marked.
     */
    function textFieldIsModified(fieldName) {
        const rowInfo = fieldRows[fieldName];
        if (rowInfo && surfaceIsModified(rowInfo.value)) {
            return true;
        }
        return !!(activeEdit && activeEdit.fieldName === fieldName && surfaceIsModified(activeEdit.editor));
    }

    /**
     * Applies a shared publish to this card.
     * A marked control keeps its draft. Clean controls take the published values, and their snapshots move so a later Cancel restores the published value.
     * @param {object} fields Fields supplied by the publisher. Only keys present on this object are considered.
     * @returns {void}
     */
    function applyExternal(fields) {
        if (!fields) {
            return;
        }

        const refreshControls = [];
        /**
         * Copies one published text field onto a clean control.
         *
         * @param {string} fieldName displayName, pronouns, or description. A missing key, missing row, or marked field is skipped.
         * @returns {void} Nothing is returned.
         */
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
            const previousAvatar = persistedValues.avatarObjectUrl;
            if (
                previousAvatar
                && previousAvatar !== fields.avatarObjectUrl
                && String(previousAvatar).indexOf("blob:") === 0
                && typeof URL.revokeObjectURL === "function"
            ) {
                URL.revokeObjectURL(previousAvatar);
            }
            persistedValues.avatarObjectUrl = fields.avatarObjectUrl;
            draftValues.avatarObjectUrl = fields.avatarObjectUrl;
            if (member) {
                member.avatarObjectUrl = persistedValues.avatarObjectUrl;
            }
            refreshControls.push(avatarWrap, avatarFile);
        }

        /**
         * Reports whether the published fields include a visibility key.
         *
         * @param {string} key visibilityKnown, visibilityInherit, or visibilityTypeId.
         * @returns {boolean} True when that key is present.
         */
        const visibilitySupplied = ["visibilityKnown", "visibilityInherit", "visibilityTypeId"].some(function (key) {
            return Object.prototype.hasOwnProperty.call(fields, key);
        });
        if (visibilitySupplied && !surfaceIsModified(sharingButton)) {
            /**
             * Copies one published visibility key onto the clean draft and baseline.
             *
             * @param {string} key Visibility key present on the published fields.
             * @returns {void} Nothing is returned.
             */
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

    /**
     * Handles a click that would leave this card or activate one of its controls.
     * A blocked card starts the leave dialog and holds its expansion. A dirty open card starts leave when the summary is clicked. Avatar clicks are kept on the avatar. Other controls are left to their own handlers.
     *
     * @param {MouseEvent} event Click inside the card, observed during capture.
     * @returns {void} Nothing is returned.
     */
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
            /**
             * Reopens the card after the leave dialog releases a blocked click.
             *
             * @returns {void} A disconnected card is left alone.
             */
            changes.requestLeave(function () {
                delete cardDetails.dataset.pbLeave;
                if (!cardDetails.isConnected) {
                    return;
                }
                cardDetails.open = true;
                /**
                 * Keeps the card open on the next turn unless a newer leave guard is active.
                 *
                 * @returns {void} Nothing is returned.
                 */
                window.setTimeout(function () {
                    if (cardDetails.isConnected && !cardDetails.dataset.pbLeave) {
                        cardDetails.open = true;
                    }
                }, 0);
            /**
             * Clears the leave guard when the leave request settles.
             *
             * @returns {void} Nothing is returned.
             */
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
            /**
             * Reopens a dirty card after the leave dialog returns from a summary click.
             *
             * @returns {void} Nothing is returned.
             */
            changes.requestLeave(function () {
                if (cardDetails.isConnected) {
                    cardDetails.open = true;
                }
            });
        }
    }, true);

    /**
     * Moves focus back out of a card whose leave dialog is blocking it.
     *
     * @param {FocusEvent} event Focus moving into the card. A card that is not blocked, or a pending dialog, is ignored.
     * @returns {void} Nothing is returned.
     */
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

    /**
     * Finds the profile field name owned by a control.
     *
     * @param {Element} control Display, editor, or another element inside a field row.
     * @returns {string} The field name, or an empty string.
     */
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

    /**
     * Reads the draft value change tracking snapshots for a control.
     *
     * @param {Element} control Profile field, color, Fronting, visibility, permission, avatar, or another form control.
     * @returns {*} The draft value for that control.
     */
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

    /**
     * Writes a restored or published value back through the matching card control.
     * Text restores the display and closes an open editor without applying it. Avatar restoration releases the unsaved preview.
     *
     * @param {Element} control Control whose draft is replaced.
     * @param {*} value Value previously read from that control.
     * @returns {void} Nothing is returned.
     */
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
            /**
             * Reports whether a control belongs to the set this card can persist.
             * Profile fields always do. Visibility, permission, and avatar do when saveProfile exists. Fronting does only when saveFronting exists. Otherwise its marker is a local draft that profile Save does not clear.
             * @param {Element} control Control being classified.
             * @returns {boolean} True when the control is one this card can persist.
             */
            persists: function (control) {
                if (profileControls.indexOf(control) >= 0 || profileFieldName(control)) {
                    return true;
                }
                if (!(options && typeof options.saveProfile === "function")) {
                    return false;
                }
                if (control === sharingButton || control === permissionControl || control === avatarWrap) {
                    return true;
                }
                return control === frontingButton && typeof options.saveFronting === "function";
            },
            applyExternal: applyExternal,
            onChanged: renderChangeState
        });
        changeOwner.capture(cardDetails);
    }

    /**
     * Publishes one loaded avatar. A pending avatar edit keeps its preview and this method returns false.
     * Other edits, expansion, and scroll position are left alone.
     * @param {string|null} objectUrl Object URL for the image. Null leaves the placeholder when the avatar is clean.
     * @returns {boolean} True when the card accepted the URL. False when a pending edit kept the current preview.
     */
    cardDetails.applyWorkspaceAvatar = function (objectUrl) {
        if (surfaceIsModified(avatarWrap) || surfaceIsModified(avatarFile)) {
            return false;
        }

        applyExternal({
            avatarObjectUrl: objectUrl
        });
        return true;
    };

    return cardDetails;
}

window.PluralBridge = window.PluralBridge || {};
window.PluralBridge.createMemberAccordion = createMemberAccordion;
