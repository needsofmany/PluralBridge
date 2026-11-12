window.PluralBridge = window.PluralBridge || {};

window.PluralBridge.navigator = (function () {
    const pluralMapEndpoint = "/api/me/plural-map";
    const anchorTypes = {
        system: true,
        group: true,
        place: true
    };

    let requestGeneration = 0;
    let selectionGeneration = 0;
    let activeController = null;
    let tree = null;
    let detail = null;
    let rows = [];
    let expandedPathKeys = {};
    let expandedAccountId = null;
    let heldExpansion = null;
    let heldAccountId = null;
    let selectedPathKey = null;

    function isAnchor(row) {
        return Boolean(
            row &&
            row.header !== true &&
            row.parentPathKey == null &&
            anchorTypes[row.objectType] === true
        );
    }

    function isSelectable(row) {
        return Boolean(
            row &&
            row.header !== true &&
            anchorTypes[row.objectType] === true
        );
    }

    function firstTopLevelAnchor(sourceRows) {
        for (let index = 0; index < sourceRows.length; index += 1) {
            if (isAnchor(sourceRows[index])) {
                return sourceRows[index];
            }
        }

        return null;
    }

    function immediateChildren(sourceRows, pathKey) {
        return sourceRows.filter(function (row) {
            return row.parentPathKey === pathKey;
        });
    }

    function mapAccountId(payload) {
        const context = payload && Array.isArray(payload.context) ? payload.context : [];
        const row = context.length > 0 ? context[0] : null;
        if (!row || !row.accountId) {
            return "";
        }

        return String(row.accountId).toLowerCase();
    }

    function getAccountId() {
        return expandedAccountId ? String(expandedAccountId) : "";
    }

    function getMapSnapshot() {
        return {
            accountId: getAccountId(),
            selectedPathKey: selectedPathKey ? String(selectedPathKey) : "",
            rows: rows.map(function (row) {
                return {
                    pathKey: row && row.pathKey ? String(row.pathKey) : "",
                    parentPathKey: row && row.parentPathKey ? String(row.parentPathKey) : null,
                    objectType: row && row.objectType ? String(row.objectType) : "",
                    objectId: row && row.objectId ? String(row.objectId) : "",
                    anchorName: row && typeof row.anchorName === "string" ? row.anchorName : "",
                    nodeLabel: row && row.nodeLabel ? String(row.nodeLabel) : "",
                    header: Boolean(row && row.header === true)
                };
            })
        };
    }

    function expandAllOccurrences(sourceRows) {
        const expanded = {};
        sourceRows.forEach(function (row) {
            if (row && row.parentPathKey) {
                expanded[row.parentPathKey] = true;
            }
        });
        return expanded;
    }

    function cloneExpansion(source) {
        const copy = {};
        const keys = source || {};
        Object.keys(keys).forEach(function (pathKey) {
            copy[pathKey] = keys[pathKey] === true;
        });
        return copy;
    }

    function holdExpansion() {
        heldExpansion = cloneExpansion(expandedPathKeys);
        heldAccountId = expandedAccountId;
    }

    function initializeExpansionForAccount(accountId, sourceRows) {
        if (accountId === expandedAccountId) {
            return;
        }

        expandedPathKeys = expandAllOccurrences(sourceRows);
        expandedAccountId = accountId;
        heldExpansion = null;
        heldAccountId = null;
        holdExpansion();
    }

    function restoreHeldExpansion() {
        if (heldExpansion && heldAccountId === expandedAccountId) {
            expandedPathKeys = cloneExpansion(heldExpansion);
        }

        if (tree && rows.length > 0) {
            renderTree();
        }
    }

    function allOccurrencesExpanded() {
        const expanded = expandAllOccurrences(rows);
        const pathKeys = Object.keys(expanded);
        for (let index = 0; index < pathKeys.length; index += 1) {
            if (expandedPathKeys[pathKeys[index]] !== true) {
                return false;
            }
        }

        return true;
    }

    function createTreeChevron(expanded) {
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

    function syncExpandAllButton() {
        if (!tree || !tree.parentElement) {
            return;
        }

        const button = tree.parentElement.querySelector(".member-systems-expand-all");
        if (!button) {
            return;
        }

        const collapseAll = allOccurrencesExpanded();
        const label = collapseAll ? "Collapse all" : "Expand all";
        button.setAttribute("aria-label", label);
        button.title = label;

        let icon = button.querySelector(".member-tree-chevron");
        if (!icon) {
            button.replaceChildren(createTreeChevron(collapseAll));
            return;
        }

        icon.classList.toggle("is-expanded", collapseAll);
    }

    function toggleAllExpansion() {
        if (allOccurrencesExpanded()) {
            expandedPathKeys = {};
        } else {
            expandedPathKeys = expandAllOccurrences(rows);
        }

        holdExpansion();
        renderTree();
    }

    function beginRequest() {
        requestGeneration += 1;

        if (activeController) {
            activeController.abort();
        }

        activeController = new AbortController();
        return {
            generation: requestGeneration,
            signal: activeController.signal
        };
    }

    function clearDetail() {
        if (detail) {
            detail.replaceChildren();
        }
    }

    function renderStatus(text, isError) {
        if (!tree) {
            return;
        }

        tree.replaceChildren();

        const status = document.createElement("p");
        status.className = isError
            ? "navigator-status navigator-status-error"
            : "navigator-status";
        status.textContent = text;
        tree.appendChild(status);
    }

    function normalizeGuid(value) {
        if (!value) {
            return "";
        }

        return String(value).toLowerCase();
    }

    function createDetailSection(row) {
        const section = document.createElement("section");
        section.className = "member-map-detail";

        const label = document.createElement("h3");
        label.className = "output-heading";
        label.textContent = row.nodeLabel || "";

        const type = document.createElement("p");
        type.className = "output-note";
        type.textContent = row.objectType || "";

        section.appendChild(label);
        section.appendChild(type);
        return section;
    }

    function renderDetailStatus(row, text, isError) {
        if (!detail) {
            return;
        }

        detail.replaceChildren();
        const section = createDetailSection(row);
        const status = document.createElement("p");
        status.className = isError
            ? "output-note navigator-status-error"
            : "output-note";
        status.textContent = text;
        section.appendChild(status);
        detail.appendChild(section);
    }

    function resolveCreateMemberAccordion() {
        const exported = window.PluralBridge && window.PluralBridge.createMemberAccordion;
        if (typeof exported === "function") {
            return exported;
        }

        if (typeof createMemberAccordion === "function") {
            return createMemberAccordion;
        }

        return null;
    }

    function reportProfileFailure(explanation, error) {
        if (error !== undefined) {
            console.error(explanation, error);
            return;
        }

        console.error(explanation);
    }

    function memberCssColor(value) {
        return typeof value === "string" && /^#[0-9A-Fa-f]{6}$/.test(value) ? value : null;
    }

    function canonicalVisibilityId(value) {
        return value === 1 || value === 2 || value === 3 || value === 4 ? value : null;
    }

    function profileForCard(profile, memberId, accountId) {
        const membersApi = window.PluralBridge && window.PluralBridge.members
            ? window.PluralBridge.members
            : null;
        const preview = membersApi && typeof membersApi.readMemberProfilePreview === "function"
            ? membersApi.readMemberProfilePreview(accountId, memberId)
            : null;
        const hasPreviewField = function (key) {
            return Boolean(preview) && Object.prototype.hasOwnProperty.call(preview, key);
        };

        return {
            memberId: profile ? profile.memberId : memberId,
            displayName: preview ? preview.displayName : profile.displayName,
            pronouns: preview ? preview.pronouns : profile.pronouns,
            description: preview ? preview.description : profile.description,
            color: hasPreviewField("color")
                ? memberCssColor(preview.color)
                : memberCssColor(profile && profile.color),
            fronting: hasPreviewField("fronting")
                ? preview.fronting
                : (profile && (profile.isFronting === true || profile.isFronting === false)
                    ? profile.isFronting
                    : null),
            visibilityKnown: Boolean(preview && preview.visibilityExplicit === true)
                || Boolean(profile && Object.prototype.hasOwnProperty.call(profile, "visibilityTypeId")),
            visibilityInherit: Boolean(preview && preview.visibilityExplicit === true && preview.visibilityInherit === true),
            visibilityTypeId: preview && preview.visibilityExplicit === true
                ? (preview.visibilityInherit === true ? null : canonicalVisibilityId(preview.visibilityTypeId))
                : (profile && Object.prototype.hasOwnProperty.call(profile, "visibilityTypeId")
                    ? canonicalVisibilityId(profile.visibilityTypeId)
                    : null),
            permission: preview && preview.permissionExplicit === true ? preview.permission : null,
            avatarObjectUrl: hasPreviewField("avatarObjectUrl") ? preview.avatarObjectUrl : null
        };
    }

    function renderProfiles(row, profiles) {
        if (!detail) {
            return;
        }

        detail.replaceChildren();
        const section = createDetailSection(row);
        const createCard = resolveCreateMemberAccordion();
        const children = immediateChildren(rows, row.pathKey);
        const profilesById = {};
        const profileIds = [];
        const profilesWithoutId = [];

        profiles.forEach(function (profile) {
            const id = normalizeGuid(profile && profile.memberId);
            if (!id) {
                profilesWithoutId.push("(profile without member id)");
                return;
            }

            if (!profilesById[id]) {
                profilesById[id] = profile;
                profileIds.push(id);
            }
        });

        const matched = {};
        const missing = [];
        const list = document.createElement("div");
        list.className = "member-map-children";
        const renderedAccountId = getAccountId();
        const memberChildren = children.filter(function (child) {
            return child.objectType === "member";
        });

        memberChildren.forEach(function (child) {
            const id = normalizeGuid(child.objectId);
            const profile = id ? profilesById[id] : null;
            if (!profile) {
                missing.push(id || "(missing member id)");
                return;
            }

            if (typeof createCard !== "function") {
                throw new Error(
                    "Cannot create member cards: createMemberAccordion is not accessible from the navigator."
                );
            }

            matched[id] = true;
            list.appendChild(createCard(profileForCard(profile, id, renderedAccountId), {
                allowProfileEditing: true,
                previewHeader: true,
                saveProfile: function (saveRequest) {
                    const membersApi = window.PluralBridge && window.PluralBridge.members
                        ? window.PluralBridge.members
                        : null;
                    if (!membersApi || typeof membersApi.saveMemberProfilePreview !== "function") {
                        return {
                            saved: false,
                            status: "Member profile saving is not connected to an anchor-specific endpoint yet."
                        };
                    }

                    return membersApi.saveMemberProfilePreview(renderedAccountId, id, saveRequest);
                }
            }));
        });

        const extra = profileIds.filter(function (id) {
            return matched[id] !== true;
        }).concat(profilesWithoutId);

        if (memberChildren.length === 0) {
            const empty = document.createElement("p");
            empty.className = "output-note";
            empty.textContent = "No member profiles were returned.";
            section.appendChild(empty);
        } else if (list.childNodes.length > 0) {
            section.appendChild(list);
        }

        if (missing.length > 0 || extra.length > 0) {
            const mismatch = document.createElement("p");
            mismatch.className = "output-note navigator-status-error";
            const parts = ["Member profiles do not match the displayed members."];
            if (missing.length > 0) {
                parts.push("Missing: " + missing.join(", ") + ".");
            }
            if (extra.length > 0) {
                parts.push("Extra: " + extra.join(", ") + ".");
            }
            mismatch.textContent = parts.join(" ");
            section.appendChild(mismatch);
        }

        detail.appendChild(section);
    }

    function profileResponseIsCurrent(request, pathKey) {
        return request.generation === requestGeneration && selectedPathKey === pathKey;
    }

    function loadAnchorProfiles(row) {
        const pathKey = row.pathKey;
        const anchorName = typeof row.anchorName === "string" ? row.anchorName : "";
        const request = beginRequest();

        if (!row.objectType || anchorName.trim() === "") {
            if (!profileResponseIsCurrent(request, pathKey)) {
                return;
            }

            renderDetailStatus(row, "Cannot load member profiles: the selected anchor has no stored name.", true);
            return;
        }

        renderDetailStatus(row, "Loading member profiles.", false);

        const apiClient = window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;

        if (!apiClient || typeof apiClient.readAnchorMemberProfiles !== "function") {
            if (!profileResponseIsCurrent(request, pathKey)) {
                return;
            }

            reportProfileFailure(
                "Member profile loading failed because readAnchorMemberProfiles is not available on the API client."
            );
            renderDetailStatus(row, "The member profiles could not be loaded.", true);
            return;
        }

        apiClient.readAnchorMemberProfiles(row.objectType, anchorName, request.signal)
            .then(function (profiles) {
                if (!profileResponseIsCurrent(request, pathKey)) {
                    return;
                }

                if (!Array.isArray(profiles)) {
                    reportProfileFailure(
                        "Member profile loading failed because the profile response was not an array.",
                        profiles
                    );
                    renderDetailStatus(row, "The member profiles could not be loaded.", true);
                    return;
                }

                renderProfiles(row, profiles);
            })
            .catch(function (error) {
                if (error && error.name === "AbortError") {
                    return;
                }

                if (!profileResponseIsCurrent(request, pathKey)) {
                    return;
                }

                reportProfileFailure(
                    "Member profile loading failed before the cards could be shown.",
                    error
                );
                renderDetailStatus(row, "The member profiles could not be loaded.", true);
            });
    }

    function appendShape(svg, tagName, attributes) {
        const shape = document.createElementNS("http://www.w3.org/2000/svg", tagName);
        Object.keys(attributes).forEach(function (name) {
            shape.setAttribute(name, attributes[name]);
        });
        svg.appendChild(shape);
    }

    function createObjectIcon(objectType) {
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

        if (objectType === "system") {
            appendShape(svg, "rect", { x: "16", y: "16", width: "6", height: "6", rx: "1" });
            appendShape(svg, "rect", { x: "2", y: "16", width: "6", height: "6", rx: "1" });
            appendShape(svg, "rect", { x: "9", y: "2", width: "6", height: "6", rx: "1" });
            appendShape(svg, "path", { d: "M5 16v-3a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v3" });
            appendShape(svg, "path", { d: "M12 12V8" });
        } else if (objectType === "group") {
            appendShape(svg, "path", { d: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" });
            appendShape(svg, "circle", { cx: "9", cy: "7", r: "4" });
            appendShape(svg, "path", { d: "M22 21v-2a4 4 0 0 0-3-3.87" });
            appendShape(svg, "path", { d: "M16 3.13a4 4 0 0 1 0 7.75" });
        } else if (objectType === "place") {
            appendShape(svg, "path", { d: "M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8" });
            appendShape(svg, "path", { d: "M3 10a2 2 0 0 1 .709-1.528l7-5.999a2 2 0 0 1 2.582 0l7 5.999A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" });
        } else if (objectType === "member") {
            appendShape(svg, "path", { d: "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" });
            appendShape(svg, "circle", { cx: "12", cy: "7", r: "4" });
        } else {
            return null;
        }

        return svg;
    }

    function createNodeLabel(objectType, labelText) {
        const wrapper = document.createElement("span");
        wrapper.className = "member-tree-node-content";
        const icon = createObjectIcon(objectType);
        if (icon) {
            wrapper.appendChild(icon);
        }

        const label = document.createElement("span");
        label.className = "member-tree-node-text";
        label.textContent = labelText;
        wrapper.appendChild(label);
        return wrapper;
    }

    function iconFor(row) {
        if (!row) {
            return "";
        }

        if (row.objectType === "system" || row.objectType === "group" || row.objectType === "place" || row.objectType === "member") {
            return row.objectType;
        }

        return "";
    }

    function appendNode(row, level) {
        const children = immediateChildren(rows, row.pathKey);
        const hasChildren = children.length > 0;
        const isExpanded = expandedPathKeys[row.pathKey] === true;
        const nodeRow = document.createElement("div");
        nodeRow.className = "member-system-node-row";
        nodeRow.style.setProperty("--system-node-level", String(level));

        if (hasChildren) {
            const expandButton = document.createElement("button");
            expandButton.type = "button";
            expandButton.className = "member-system-tree-toggle";
            expandButton.setAttribute("aria-expanded", String(isExpanded));
            expandButton.setAttribute("aria-label", isExpanded ? "Collapse" : "Expand");
            expandButton.appendChild(createTreeChevron(isExpanded));
            expandButton.addEventListener("click", function () {
                const expanded = !isExpanded;
                expandedPathKeys[row.pathKey] = expanded;
                holdExpansion();
                const chevron = expandButton.querySelector(".member-tree-chevron");
                if (chevron) {
                    chevron.classList.toggle("is-expanded", expanded);
                }
                const reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
                window.setTimeout(function () {
                    renderTree();
                }, reduceMotion ? 0 : 150);
            });
            nodeRow.appendChild(expandButton);
        } else {
            const spacer = document.createElement("span");
            spacer.className = "member-system-tree-spacer";
            nodeRow.appendChild(spacer);
        }

        const labelText = row.nodeLabel || "";
        if (isSelectable(row)) {
            const selectButton = document.createElement("button");
            selectButton.type = "button";
            selectButton.className = "member-system-node-button";
            selectButton.appendChild(createNodeLabel(iconFor(row), labelText));

            if (selectedPathKey === row.pathKey) {
                selectButton.classList.add("is-active");
                selectButton.setAttribute("aria-current", "true");
            }

            selectButton.addEventListener("click", function () {
                selectAnchor(row);
            });
            nodeRow.appendChild(selectButton);
        } else {
            const label = document.createElement("span");
            label.className = "member-system-node-label";
            label.appendChild(createNodeLabel(iconFor(row), labelText));
            nodeRow.appendChild(label);
        }

        tree.appendChild(nodeRow);

        if (hasChildren && isExpanded) {
            children.forEach(function (child) {
                appendNode(child, level + 1);
            });
        }
    }

    function renderTree() {
        if (!tree) {
            return;
        }

        tree.replaceChildren();
        rows.forEach(function (row) {
            if (row.parentPathKey == null) {
                appendNode(row, 0);
            }
        });
        syncExpandAllButton();
    }

    async function selectAnchor(row) {
        if (!isSelectable(row) || selectedPathKey === row.pathKey) {
            return;
        }

        const selection = selectionGeneration + 1;
        selectionGeneration = selection;

        let allowed = true;
        if (typeof allowMemberProfileLeave === "function") {
            allowed = await allowMemberProfileLeave();
        }

        if (!allowed || selection !== selectionGeneration || !isSelectable(row)) {
            return;
        }

        selectedPathKey = row.pathKey;
        renderTree();
        loadAnchorProfiles(row);
    }

    function clear() {
        selectionGeneration += 1;
        beginRequest();
        activeController = null;
        rows = [];
        selectedPathKey = null;
        clearDetail();

        if (tree) {
            tree.replaceChildren();
        }
    }

    function mount(containers) {
        selectionGeneration += 1;
        tree = containers && containers.tree ? containers.tree : null;
        detail = containers && containers.detail ? containers.detail : null;
        rows = [];
        selectedPathKey = null;
        clearDetail();

        if (!tree || !detail) {
            beginRequest();
            activeController = null;
            return;
        }

        const request = beginRequest();
        renderStatus("Loading plural map.");

        fetch(pluralMapEndpoint, {
            headers: {
                Accept: "application/json"
            },
            credentials: "same-origin",
            signal: request.signal
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("The plural map could not be loaded.");
                }

                return response.json();
            })
            .then(function (payload) {
                if (request.generation !== requestGeneration) {
                    return;
                }

                if (!payload || !Array.isArray(payload.hierarchyPreview)) {
                    throw new Error("The plural map could not be loaded.");
                }

                rows = payload.hierarchyPreview;
                initializeExpansionForAccount(mapAccountId(payload), rows);
                if (rows.length === 0) {
                    selectedPathKey = null;
                    clearDetail();
                    renderStatus("No plural map rows were returned.");
                    syncExpandAllButton();
                    return;
                }

                renderTree();
                const anchor = firstTopLevelAnchor(rows);
                if (anchor) {
                    selectAnchor(anchor);
                }
            })
            .catch(function (error) {
                if (error && error.name === "AbortError") {
                    return;
                }

                if (request.generation !== requestGeneration) {
                    return;
                }

                rows = [];
                selectedPathKey = null;
                clearDetail();
                renderStatus("The plural map could not be loaded.", true);
            });
    }

    return {
        mount: mount,
        clear: clear,
        getAccountId: getAccountId,
        getMapSnapshot: getMapSnapshot,
        toggleAllExpansion: toggleAllExpansion,
        holdExpansion: holdExpansion,
        restoreHeldExpansion: restoreHeldExpansion
    };
}());
