window.PluralBridge = window.PluralBridge || {};

window.PluralBridge.navigator = (function () {
    const pluralMapEndpoint = "/api/me/plural-map";
    const anchorTypes = {
        system: true,
        group: true,
        place: true
    };

    let requestGeneration = 0;
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

    function renderDetail(row) {
        if (!detail) {
            return;
        }

        detail.replaceChildren();

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

        const children = immediateChildren(rows, row.pathKey);
        if (children.length === 0) {
            const empty = document.createElement("p");
            empty.className = "output-note";
            empty.textContent = "No child rows were returned.";
            section.appendChild(empty);
        } else {
            const list = document.createElement("ul");
            list.className = "member-map-children";

            children.forEach(function (child) {
                const item = document.createElement("li");
                item.className = "member-map-child";

                const childType = document.createElement("span");
                childType.className = "member-map-child-type";
                childType.textContent = child.objectType || "";

                const childLabel = document.createElement("span");
                childLabel.textContent = child.nodeLabel || "";

                item.appendChild(childType);
                item.appendChild(childLabel);
                list.appendChild(item);
            });

            section.appendChild(list);
        }

        detail.appendChild(section);
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

    function selectAnchor(row) {
        if (!isSelectable(row)) {
            return;
        }

        selectedPathKey = row.pathKey;
        renderTree();
        renderDetail(row);
    }

    function clear() {
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
