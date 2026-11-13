(function () {
    var modifiedClass = "pb-control-modified";
    var changeEventName = "pb-control-change";
    var standardSelector = "input, textarea, select";

    var byContainer = typeof WeakMap === "function" ? new WeakMap() : null;
    var attached = [];
    var editing = null;
    var pending = null;
    var finishing = false;
    var dialog = null;
    var summary = null;
    var note = null;
    var status = null;
    var saveButton = null;
    var discardButton = null;
    var keepButton = null;
    var ignoreDialogClicks = false;
    var armedButton = null;
    var dialogReopens = 0;

    function sessionFor(container) {
        if (!container || !byContainer) {
            return null;
        }

        return byContainer.get(container) || null;
    }

    function sessionContaining(node) {
        var found = null;
        attached.forEach(function (session) {
            if (!node || !session.container || !session.container.contains(node)) {
                return;
            }
            if (!found || found.container.contains(session.container)) {
                found = session;
            }
        });
        return found;
    }

    function isStandardControl(element) {
        if (!element || !element.matches) {
            return false;
        }
        if (!element.matches(standardSelector)) {
            return false;
        }
        var type = (element.type || "").toLowerCase();
        return type !== "button" && type !== "submit" && type !== "reset" && type !== "hidden" && type !== "image";
    }

    function eventNode(event) {
        var target = event.target;
        if (target && target.nodeType === 3) {
            target = target.parentElement;
        }
        if (event.type === changeEventName && event.detail && event.detail.control) {
            target = event.detail.control;
        }
        return target || null;
    }

    function trackingControl(node, container) {
        if (!node || !node.closest || !container || !container.contains(node)) {
            return null;
        }

        var stable = node.closest("[data-change-control]");
        if (stable && container.contains(stable)) {
            return stable;
        }

        return isStandardControl(node) ? node : null;
    }

    function textOf(element) {
        return element && element.textContent ? element.textContent.replace(/\s+/g, " ").trim() : "";
    }

    function controlName(control) {
        if (!control) {
            return "";
        }

        var explicit = control.getAttribute && control.getAttribute("data-change-name");
        if (explicit) {
            return explicit.trim();
        }

        var labelledBy = control.getAttribute && control.getAttribute("aria-labelledby");
        if (labelledBy && control.ownerDocument) {
            var names = labelledBy.split(/\s+/).map(function (id) {
                return textOf(control.ownerDocument.getElementById(id));
            }).filter(Boolean);
            if (names.length) {
                return names.join(" ");
            }
        }

        if (control.labels && control.labels.length) {
            var labelText = textOf(control.labels[0]);
            if (labelText) {
                return labelText;
            }
        }

        var rowLabel = control.querySelector && control.querySelector(".member-profile-field-label, label");
        if (rowLabel) {
            var rowText = textOf(rowLabel);
            if (rowText) {
                return rowText;
            }
        }

        var wrappingLabel = control.closest && control.closest("label");
        if (wrappingLabel) {
            var wrapped = textOf(wrappingLabel);
            if (wrapped) {
                return wrapped;
            }
        }

        var aria = control.getAttribute && control.getAttribute("aria-label");
        if (aria && aria.trim()) {
            return aria.trim();
        }

        return control.getAttribute && control.getAttribute("name") ? control.getAttribute("name") : "Field";
    }

    function modifiedControls(container) {
        var session = sessionFor(container);
        if (!session) {
            return [];
        }

        return Array.prototype.filter.call(container.querySelectorAll("." + modifiedClass), function (element) {
            var stable = element.closest("[data-change-control]");
            if (stable && stable !== element && stable.classList.contains(modifiedClass)) {
                return false;
            }
            return true;
        });
    }

    function cloneValue(value) {
        if (value && typeof value === "object") {
            return JSON.parse(JSON.stringify(value));
        }
        return value;
    }

    function readControl(session, control) {
        if (session.read) {
            return cloneValue(session.read(control));
        }
        if (!control) {
            return null;
        }
        if (control.type === "checkbox" || control.type === "radio") {
            return control.checked;
        }
        if (control.type === "file") {
            return null;
        }
        return control.value;
    }

    function rebind(from, to) {
        var session = sessionContaining(to) || sessionContaining(from);
        if (!session || !to) {
            return;
        }
        if (from && session.snapshots.has(from)) {
            session.snapshots.set(to, cloneValue(session.snapshots.get(from)));
            return;
        }
        rememberSnapshot(session, to);
    }

    function rememberSnapshot(session, control) {
        if (!session || !control || session.snapshots.has(control)) {
            return;
        }
        session.snapshots.set(control, readControl(session, control));
    }

    function capture(container, controls) {
        var session = sessionFor(container);
        if (!session) {
            return;
        }

        var list = controls && controls.length
            ? controls
            : container.querySelectorAll("[data-change-control], " + standardSelector);
        Array.prototype.forEach.call(list, function (control) {
            if (!container.contains(control)) {
                return;
            }
            session.snapshots.set(control, readControl(session, control));
        });
    }

    function isDirty(container) {
        return modifiedControls(container).length > 0;
    }

    function names(container) {
        return modifiedControls(container).map(controlName).filter(Boolean);
    }

    function canSave(session) {
        return modifiedControls(session.container).some(function (control) {
            return !session.persists || session.persists(control);
        });
    }

    function rememberEditing(session) {
        if (session && isDirty(session.container)) {
            editing = session;
            return;
        }
        if (editing === session) {
            editing = null;
        }
    }

    function notifyChanged(session) {
        if (session && typeof session.onChanged === "function") {
            session.onChanged(modifiedControls(session.container));
        }
        if (pending && editing === session) {
            renderDialog();
        }
    }

    function unmark(control) {
        if (!control || !control.classList) {
            return;
        }
        control.classList.remove(modifiedClass);
        if (control.querySelectorAll) {
            Array.prototype.forEach.call(control.querySelectorAll("." + modifiedClass), function (inner) {
                inner.classList.remove(modifiedClass);
            });
        }
    }

    function clearMarkers(container, controls) {
        var session = sessionFor(container);
        if (!session) {
            return [];
        }

        var list = controls ? Array.prototype.slice.call(controls) : modifiedControls(container);
        list.forEach(unmark);
        rememberEditing(session);
        notifyChanged(session);
        return modifiedControls(container);
    }

    function commit(container, controls) {
        var session = sessionFor(container);
        if (!session) {
            return [];
        }

        Array.prototype.forEach.call(controls || [], function (control) {
            if (!control || !control.classList || !control.classList.contains(modifiedClass)) {
                return;
            }
            session.snapshots.set(control, readControl(session, control));
            unmark(control);
        });
        rememberEditing(session);
        notifyChanged(session);
        return modifiedControls(container);
    }

    function restoreMarked(container) {
        var session = sessionFor(container);
        if (!session) {
            return;
        }

        var marked = modifiedControls(container);
        session.suppress += 1;
        try {
            marked.forEach(function (control) {
                if (!session.snapshots.has(control)) {
                    return;
                }
                var value = cloneValue(session.snapshots.get(control));
                if (session.write) {
                    session.write(control, value);
                    return;
                }
                if (control.type === "checkbox" || control.type === "radio") {
                    control.checked = !!value;
                    return;
                }
                if (control.type !== "file" && value !== undefined && value !== null) {
                    control.value = value;
                }
            });
        } finally {
            session.suppress -= 1;
        }
        clearMarkers(container);
    }

    function mark(session, control, source) {
        if (!session || !control || session.suppress || session.saving) {
            return false;
        }
        if (editing && editing !== session && isDirty(editing.container)) {
            return false;
        }

        rememberSnapshot(session, control);
        control.classList.add(modifiedClass);
        rememberEditing(session);
        notifyChanged(session);
        return true;
    }

    function onEditEvent(event) {
        var node = eventNode(event);
        var session = sessionContaining(node);
        if (!session) {
            return;
        }

        var control = trackingControl(node, session.container);
        if (!control) {
            return;
        }

        if (event.type !== changeEventName) {
            var fileInput = event.target && event.target.type === "file" ? event.target : null;
            if (fileInput && (!fileInput.files || !fileInput.files.length)) {
                return;
            }
        }

        mark(session, control, node);
    }

    function onFocusIn(event) {
        var node = eventNode(event);
        var session = sessionContaining(node);
        if (!session || session.suppress) {
            return;
        }
        var control = trackingControl(node, session.container);
        if (control) {
            rememberSnapshot(session, control);
        }
    }

    function ensureDialog() {
        if (dialog) {
            return;
        }

        dialog = document.createElement("dialog");
        dialog.className = "member-change-dialog";
        dialog.setAttribute("aria-label", "Unsaved member changes");
        dialog.tabIndex = -1;

        summary = document.createElement("div");
        summary.className = "member-change-summary";

        note = document.createElement("p");
        note.className = "member-change-note";
        note.hidden = true;

        var actions = document.createElement("div");
        actions.className = "member-change-actions";

        saveButton = document.createElement("button");
        saveButton.type = "button";
        saveButton.className = "member-change-button";
        saveButton.textContent = "Save changes";

        discardButton = document.createElement("button");
        discardButton.type = "button";
        discardButton.className = "member-change-button";
        discardButton.textContent = "Discard changes";

        keepButton = document.createElement("button");
        keepButton.type = "button";
        keepButton.className = "member-change-button";
        keepButton.textContent = "Keep editing";

        status = document.createElement("p");
        status.className = "member-change-status";
        status.setAttribute("aria-live", "polite");

        actions.appendChild(saveButton);
        actions.appendChild(discardButton);
        actions.appendChild(keepButton);
        dialog.appendChild(summary);
        dialog.appendChild(note);
        dialog.appendChild(actions);
        dialog.appendChild(status);

        function armDialogButton(button) {
            button.addEventListener("pointerdown", function () {
                armedButton = button;
            });
            button.addEventListener("keydown", function (event) {
                if (event.key === "Enter" || event.key === " ") {
                    armedButton = button;
                }
            });
        }

        function acceptDialogClick(button, event) {
            if (ignoreDialogClicks || armedButton !== button) {
                event.preventDefault();
                event.stopPropagation();
                return false;
            }
            armedButton = null;
            return true;
        }

        armDialogButton(saveButton);
        armDialogButton(discardButton);
        armDialogButton(keepButton);

        saveButton.addEventListener("click", function (event) {
            if (!acceptDialogClick(saveButton, event) || (editing && editing.saving)) {
                return;
            }
            saveAndContinue();
        });
        discardButton.addEventListener("click", function (event) {
            if (!acceptDialogClick(discardButton, event) || (editing && editing.saving)) {
                return;
            }
            discardAndContinue();
        });
        keepButton.addEventListener("click", function (event) {
            if (!acceptDialogClick(keepButton, event) || (editing && editing.saving) || !dialog.open) {
                return;
            }
            finish(false);
        });
        dialog.addEventListener("cancel", function (event) {
            event.preventDefault();
            if (editing && editing.saving) {
                return;
            }
            finish(false);
        });
        dialog.addEventListener("close", function () {
            if (!pending || finishing || dialogReopens > 2) {
                return;
            }

            dialogReopens += 1;
            var token = pending;
            window.setTimeout(function () {
                if (pending === token && dialog && !dialog.open) {
                    showDialog(false);
                }
            }, 0);
        });
    }

    function renderChangedFields(summary, changed) {
        summary.replaceChildren();
        var heading = document.createElement("p");
        heading.className = "member-change-heading";
        heading.textContent = "Changed:";
        var list = document.createElement("ul");
        list.className = "member-change-list";
        changed.forEach(function (name) {
            var item = document.createElement("li");
            item.textContent = name;
            list.appendChild(item);
        });
        summary.appendChild(heading);
        summary.appendChild(list);
    }

    function renderDialog() {
        if (!dialog || !editing) {
            return;
        }

        var changed = names(editing.container);
        if (editing.saving && dialog.contains(document.activeElement)) {
            dialog.focus();
        }
        renderChangedFields(summary, changed);
        var savable = canSave(editing);
        if (changed.length && !savable) {
            note.hidden = false;
            note.textContent = "Not saved yet: " + changed.join(", ") + ". Discard changes or keep editing.";
        } else if (note.dataset.hold === "partial") {
            note.hidden = false;
        } else {
            note.hidden = true;
            note.textContent = "";
        }
        saveButton.disabled = Boolean(editing.saving) || !savable;
        discardButton.disabled = Boolean(editing.saving);
        keepButton.disabled = false;
    }

    function showDialog(ignoreClick) {
        ensureDialog();
        status.textContent = "";
        status.classList.remove("is-error");
        if (!note.dataset.hold) {
            note.dataset.hold = "";
        }
        renderDialog();
        if (dialog.parentNode !== document.body) {
            document.body.appendChild(dialog);
        }
        dialog.hidden = false;
        armedButton = null;
        if (!dialog.open) {
            try {
                if (typeof dialog.showModal === "function") {
                    dialog.showModal();
                } else if (typeof dialog.show === "function") {
                    dialog.show();
                } else {
                    dialog.setAttribute("open", "");
                }
            } catch (error) {
                dialog.setAttribute("open", "");
            }
        }
        if (ignoreClick) {
            ignoreDialogClicks = true;
            window.setTimeout(function () {
                ignoreDialogClicks = false;
            }, 0);
        }
        try {
            dialog.focus();
        } catch (error) {
            /* The dialog can still be used without a focused control. */
        }
    }

    function hideDialog() {
        if (!dialog) {
            return;
        }

        if (typeof dialog.close === "function" && dialog.open) {
            dialog.close();
        }
        dialog.hidden = true;
        status.textContent = "";
        status.classList.remove("is-error");
        note.dataset.hold = "";
    }

    function finish(allowed) {
        if (!pending || finishing) {
            return;
        }

        finishing = true;
        var current = pending;
        pending = null;
        dialogReopens = 0;
        try {
            hideDialog();
            if (allowed && typeof current.action === "function") {
                current.action();
            }
            current.resolve(!!allowed);
        } finally {
            finishing = false;
        }
    }

    function discardAndContinue() {
        if (!editing || editing.saving || !dialog || !dialog.open) {
            return;
        }

        var session = editing;
        restoreMarked(session.container);
        finish(true);
    }

    function saveAndContinue() {
        if (!editing || editing.saving || typeof editing.save !== "function" || !dialog || !dialog.open) {
            return;
        }

        if (!canSave(editing)) {
            renderDialog();
            return;
        }

        var session = editing;
        status.classList.remove("is-error");
        status.textContent = "Saving changes.";
        Promise.resolve(session.save()).then(function (result) {
            if (!result || result.saved !== true) {
                status.textContent = result && result.message
                    ? result.message
                    : "Changes could not be saved.";
                status.classList.add("is-error");
                renderDialog();
                return;
            }

            if (isDirty(session.container)) {
                note.dataset.hold = "partial";
                note.hidden = false;
                note.textContent = "Not saved yet: " + names(session.container).join(", ") + ". Discard changes or keep editing.";
                status.textContent = "";
                renderDialog();
                if (dialog && !dialog.open) {
                    showDialog(false);
                }
                return;
            }

            finish(true);
        }, function () {
            status.textContent = "Changes could not be saved.";
            status.classList.add("is-error");
            renderDialog();
        });
    }

    function requestLeave(action) {
        if (!editing || !isDirty(editing.container)) {
            if (typeof action === "function") {
                action();
            }
            return Promise.resolve(true);
        }

        if (pending) {
            if (typeof action === "function") {
                pending.action = action;
            }
            if (!dialog || !dialog.open) {
                var openToken = pending;
                window.setTimeout(function () {
                    if (pending === openToken) {
                        showDialog(false);
                    }
                }, 0);
            }
            return pending.promise;
        }

        var resolveLeave = null;
        var promise = new Promise(function (resolve) {
            resolveLeave = resolve;
        });
        pending = {
            action: typeof action === "function" ? action : null,
            resolve: resolveLeave,
            promise: promise
        };
        var token = pending;
        window.setTimeout(function () {
            if (pending !== token) {
                return;
            }
            showDialog(true);
        }, 0);
        return promise;
    }

    function attach(registration) {
        var container = registration.container || registration.card;
        var session = {
            container: container,
            memberId: registration.memberId ? String(registration.memberId).toLowerCase() : "",
            suppress: 0,
            saving: false,
            snapshots: typeof WeakMap === "function" ? new WeakMap() : {
                has: function () { return false; },
                get: function () { return undefined; },
                set: function () {}
            },
            save: registration.save,
            read: registration.read,
            write: registration.write,
            persists: registration.persists,
            applyExternal: registration.applyExternal,
            onChanged: registration.onChanged || registration.onMaskChanged
        };
        if (byContainer && container) {
            byContainer.set(container, session);
        }
        attached.push(session);
        if (container) {
            container.addEventListener("input", onEditEvent);
            container.addEventListener("change", onEditEvent);
            container.addEventListener(changeEventName, onEditEvent);
            container.addEventListener("focusin", onFocusIn);
        }
        if (typeof session.onChanged === "function") {
            session.onChanged([]);
        }

        return function detach() {
            var index = attached.indexOf(session);
            if (index >= 0) {
                attached.splice(index, 1);
            }
            if (byContainer && container) {
                byContainer.delete(container);
            }
            if (container) {
                container.removeEventListener("input", onEditEvent);
                container.removeEventListener("change", onEditEvent);
                container.removeEventListener(changeEventName, onEditEvent);
                container.removeEventListener("focusin", onFocusIn);
            }
            if (editing === session) {
                editing = null;
                if (pending) {
                    finish(false);
                }
            }
        };
    }

    function notify(control) {
        if (!control || !control.dispatchEvent) {
            return false;
        }

        return control.dispatchEvent(new CustomEvent(changeEventName, {
            bubbles: true,
            detail: { control: control }
        }));
    }

    function suppress(container, work) {
        var session = sessionFor(container);
        if (!session) {
            work();
            return;
        }

        session.suppress += 1;
        try {
            work();
        } finally {
            session.suppress -= 1;
        }
    }

    function publishShared(memberId, fields, source) {
        var id = memberId ? String(memberId).toLowerCase() : "";
        if (!id || !fields) {
            return;
        }

        attached.forEach(function (session) {
            if (session.container === source || session.memberId !== id || isDirty(session.container)) {
                return;
            }
            if (typeof session.applyExternal !== "function") {
                return;
            }
            session.suppress += 1;
            try {
                session.applyExternal(fields);
            } finally {
                session.suppress -= 1;
            }
        });
    }

    function setSaving(container, isSaving) {
        var session = sessionFor(container);
        if (!session) {
            return;
        }
        session.saving = !!isSaving;
        if (pending && editing === session && dialog) {
            if (isSaving) {
                try {
                    dialog.focus();
                } catch (error) {
                    /* Saving still blocks the profile controls. */
                }
            }
            renderDialog();
        }
    }

    var api = {
        modifiedClass: modifiedClass,
        changeEvent: changeEventName,
        attach: attach,
        notify: notify,
        modified: modifiedControls,
        names: names,
        nameOf: controlName,
        dirty: isDirty,
        clear: clearMarkers,
        commit: commit,
        capture: capture,
        restore: restoreMarked,
        rebind: rebind,
        suppress: suppress,
        publishShared: publishShared,
        setSaving: setSaving,
        requestLeave: requestLeave,
        hasPending: function () {
            return !!pending;
        },
        blocks: function (container) {
            return !!(editing && isDirty(editing.container) && editing.container !== container);
        }
    };

    window.PluralBridge = window.PluralBridge || {};
    window.PluralBridge.changeTracking = api;
    window.PluralBridge.memberChanges = api;
})();
