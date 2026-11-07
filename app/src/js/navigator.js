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
            list.appendChild(createCard(profile, {
                allowProfileEditing: false
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

    function createNodeLabel(iconText, labelText) {
        const wrapper = document.createElement("span");
        wrapper.className = "member-tree-node-content";

        if (iconText) {
            const icon = document.createElement("span");
            icon.className = "member-tree-node-icon";
            icon.setAttribute("aria-hidden", "true");
            icon.textContent = iconText;
            wrapper.appendChild(icon);
        }

        const label = document.createElement("span");
        label.className = "member-tree-node-text";
        label.textContent = labelText;
        wrapper.appendChild(label);
        return wrapper;
    }

    function iconFor(row) {
        if (row.objectType === "system") {
            return "◈";
        }

        if (row.objectType === "group") {
            return "▣";
        }

        if (row.objectType === "place") {
            return "⌂";
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
            expandButton.textContent = isExpanded ? "−" : "+";
            expandButton.addEventListener("click", function () {
                expandedPathKeys[row.pathKey] = !isExpanded;
                renderTree();
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
        expandedPathKeys = {};
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
        expandedPathKeys = {};
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
                if (rows.length === 0) {
                    selectedPathKey = null;
                    clearDetail();
                    renderStatus("No plural map rows were returned.");
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
        clear: clear
    };
}());
