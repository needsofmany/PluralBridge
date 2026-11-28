/**
 * Plural-map navigator. Loads /api/me/plural-map, draws the occurrence tree, and loads anchor member profiles for the selected anchor.
 * When developerTools.workspaceTreeEnabled is strictly true, the same view loads /api/me/workspace-tree, keeps that context id, and loads direct member cards for the selected anchor. The first window uses skip 0 and then requests that page's avatars. Later windows append cards and do not request avatars. The developer-tools initializer sets that flag to true when it is absent. Strict false keeps the plural-map path and its profile route.
 * Depends on window.PluralBridge.apiClient, window.PluralBridge.members, window.PluralBridge.memberChanges, and createMemberAccordion.
 * A newer selection aborts the previous profile request. A response is painted only when its generation and selected path still match.
 */
window.PluralBridge = window.PluralBridge || {};

/**
 * Creates the navigator and returns its public mount, clear, account, snapshot, and expansion functions.
 * @returns {object} The navigator API.
 */
window.PluralBridge.navigator = (function () {
    const pluralMapEndpoint = "/api/me/plural-map";
    const workspaceTreeEndpoint = "/api/me/workspace-tree";
    const memberDetailBatchSize = 20;
    const emptyMemberId = "00000000-0000-0000-0000-000000000000";
    const anchorTypes = {
        system: true,
        group: true,
        place: true
    };

    let requestGeneration = 0;
    let selectionGeneration = 0;
    let activeController = null;
    let memberDetailController = null;
    let memberAvatarController = null;
    let memberDetailSerial = 0;
    let memberDetailInFlight = false;
    let memberDetailSkip = 0;
    let memberDetailExhausted = false;
    let memberDetailPath = "";
    let memberDetailContext = "";
    let shownMemberIds = {};
    let tree = null;
    let detail = null;
    let rows = [];
    let expandedPathKeys = {};
    let expandedAccountId = null;
    let heldExpansion = null;
    let heldAccountId = null;
    let selectedPathKey = null;
    // Opaque server context id for this view. Cleared before release on clear, path switch, and sign-out.
    let contextId = null;
    let rowTooltip = null;
    let tooltipAnchor = null;
    let tooltipDismissed = false;
    let touchRevealPath = null;
    let lastPointerType = "mouse";
    let tooltipDocumentBound = false;
    let tooltipTree = null;
    let panelResizeObserver = null;
    let panelAttributeObserver = null;

    /**
     * Reports whether a row is a top-level system, group, or place anchor.
     * @param {object} row Map row. A header, a nested row, or a non-anchor type is not an anchor.
     * @returns {boolean} True for a non-header row with no parent and an anchor type.
     */
    function isAnchor(row) {
        return Boolean(
            row &&
            row.header !== true &&
            row.parentPathKey == null &&
            anchorTypes[row.objectType] === true
        );
    }

    /**
     * Reports whether a row can be selected. Nested anchors are selectable. Headers are not.
     * @param {object} row Map row.
     * @returns {boolean} True for a non-header system, group, or place.
     */
    function isSelectable(row) {
        return Boolean(
            row &&
            row.header !== true &&
            anchorTypes[row.objectType] === true
        );
    }

    /**
     * Finds the first top-level anchor in returned map order. That row is the initial focus.
     * @param {Array<object>} sourceRows Hierarchy rows. An empty list has no anchor.
     * @returns {object|null} The first anchor, or null.
     */
    function firstTopLevelAnchor(sourceRows) {
        for (let index = 0; index < sourceRows.length; index += 1) {
            if (isAnchor(sourceRows[index])) {
                return sourceRows[index];
            }
        }

        return null;
    }

    /**
     * Returns the rows whose parent path is the given path.
     * @param {Array<object>} sourceRows Rows to search.
     * @param {string} pathKey Parent path. Rows with a different parent are excluded.
     * @returns {Array<object>} The immediate children, in source order.
     */
    function immediateChildren(sourceRows, pathKey) {
        /**
         * Keeps a row when its parent path equals the requested path.
         * @param {object} row Map row.
         * @returns {boolean} True when row.parentPathKey is pathKey.
         */
        return sourceRows.filter(function (row) {
            return row.parentPathKey === pathKey;
        });
    }

    /**
     * Reads the account id from the first map context row.
     * @param {object} payload Plural-map response. A missing context or account id returns an empty string.
     * @returns {string} The lower-case account id, or an empty string.
     */
    /**
     * Reports whether the developer flag selects the new tree path.
     * @returns {boolean} True only when workspaceTreeEnabled is strictly true.
     */
    function workspaceTreeEnabled() {
        const tools = window.PluralBridge && window.PluralBridge.developerTools
            ? window.PluralBridge.developerTools
            : null;
        return Boolean(tools && tools.workspaceTreeEnabled === true);
    }

    /**
     * Reports whether this page should mark one workspace open with a profiling run id.
     * The shared predicate is the only switch. A missing predicate stays off.
     * @returns {boolean} True only when workspaceLoadProfileEnabled returns strict true.
     */
    function workspaceLoadProfileEnabled() {
        const tools = window.PluralBridge && window.PluralBridge.developerTools
            ? window.PluralBridge.developerTools
            : null;
        return Boolean(tools
            && typeof tools.workspaceLoadProfileEnabled === "function"
            && tools.workspaceLoadProfileEnabled() === true);
    }

    /**
     * Creates the run id for one workspace open. The same id is sent on that open's tree, first member page, and first avatar page.
     * @returns {string} A new id. This function does not record a time.
     */
    function newWorkspaceProfileId() {
        if (window.crypto && typeof window.crypto.randomUUID === "function") {
            return window.crypto.randomUUID();
        }

        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (character) {
            const value = Math.random() * 16 | 0;
            const digit = character === "x" ? value : (value & 0x3 | 0x8);
            return digit.toString(16);
        });
    }

    function mapAccountId(payload) {
        if (payload && payload.accountId) {
            return String(payload.accountId).toLowerCase();
        }

        const context = payload && Array.isArray(payload.context) ? payload.context : [];
        const row = context.length > 0 ? context[0] : null;
        if (!row || !row.accountId) {
            return "";
        }

        return String(row.accountId).toLowerCase();
    }

    /**
     * Returns the account id captured when the current map was expanded.
     * @returns {string} The account id, or an empty string before a map is loaded.
     */
    function getAccountId() {
        return expandedAccountId ? String(expandedAccountId) : "";
    }

    /**
     * Copies the current map rows, selected path, and account id for dialogs.
     * @returns {object} A snapshot. Row text fields are strings. A missing parent path is null.
     */
    function getMapSnapshot() {
        return {
            accountId: getAccountId(),
            selectedPathKey: selectedPathKey ? String(selectedPathKey) : "",
            /**
             * Copies one map row into the snapshot. Missing text becomes an empty string.
             * @param {object} row Source row. A null row still produces an object with empty fields.
             * @returns {object} The copied row.
             */
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

    /**
     * Marks every parent path that has a child, which expands the whole tree.
     * @param {Array<object>} sourceRows Map rows.
     * @returns {object} Path keys set to true.
     */
    function expandAllOccurrences(sourceRows) {
        const expanded = {};
        /**
         * Records a row's parent path as expanded.
         * @param {object} row Map row. A row without a parent path is skipped.
         * @returns {void}
         */
        sourceRows.forEach(function (row) {
            if (row && row.parentPathKey) {
                expanded[row.parentPathKey] = true;
            }
        });
        return expanded;
    }

    /**
     * Copies an expansion map so a later toggle does not change the held copy.
     * @param {object} [source] Path keys to copy. Null or omitted copies nothing.
     * @returns {object} The copy. Only true values stay true.
     */
    function cloneExpansion(source) {
        const copy = {};
        const keys = source || {};
        /**
         * Copies one expansion flag.
         * @param {string} pathKey Path being copied.
         * @returns {void}
         */
        Object.keys(keys).forEach(function (pathKey) {
            copy[pathKey] = keys[pathKey] === true;
        });
        return copy;
    }

    /**
     * Stores the current expansion for the current account so it can be restored.
     * @returns {void}
     */
    function holdExpansion() {
        heldExpansion = cloneExpansion(expandedPathKeys);
        heldAccountId = expandedAccountId;
    }

    /**
     * Expands every occurrence when the account changes. The same account keeps its expansion.
     * @param {string} accountId Account that owns the rows.
     * @param {Array<object>} sourceRows Rows used to find parent paths.
     * @returns {void}
     */
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

    /**
     * Restores the held expansion when it belongs to the current account, then redraws the tree.
     * @returns {void}
     */
    function restoreHeldExpansion() {
        if (heldExpansion && heldAccountId === expandedAccountId) {
            expandedPathKeys = cloneExpansion(heldExpansion);
        }

        if (tree && rows.length > 0) {
            renderTree();
        }
    }

    /**
     * Reports whether every parent that has children is currently expanded.
     * @returns {boolean} True when no expandable path is collapsed. An empty map is treated as expanded.
     */
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

    /**
     * Builds the tree chevron. The expanded class rotates it through CSS.
     * @param {boolean} expanded True when the chevron should use the expanded class.
     * @returns {SVGElement} The chevron.
     */
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

    /**
     * Sets the Expand all button to Collapse all when every occurrence is already expanded.
     * @returns {void}
     */
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

    /**
     * Expands every occurrence, or collapses every occurrence when they are already expanded, and remembers that choice.
     * @returns {void}
     */
    function toggleAllExpansion() {
        if (allOccurrencesExpanded()) {
            expandedPathKeys = {};
        } else {
            expandedPathKeys = expandAllOccurrences(rows);
        }

        holdExpansion();
        renderTree();
    }

    /**
     * Starts a profile or map request and aborts the previous one.
     * The generation still has to match, because an abort does not cover every return path.
     * @returns {{generation: number, signal: AbortSignal}} The generation and signal for this request.
     */
    function beginRequest() {
        requestGeneration += 1;
        memberDetailSerial += 1;
        memberDetailInFlight = false;

        if (memberDetailController) {
            memberDetailController.abort();
            memberDetailController = null;
        }

        if (memberAvatarController) {
            memberAvatarController.abort();
            memberAvatarController = null;
        }

        if (activeController) {
            activeController.abort();
        }

        activeController = new AbortController();
        return {
            generation: requestGeneration,
            signal: activeController.signal
        };
    }

    /**
     * Starts one member-detail window without aborting the tree load.
     * A newer selection or tree load aborts this signal through beginRequest.
     * @returns {AbortSignal} The signal for this window.
     */
    function openMemberDetailSignal() {
        if (memberDetailController) {
            memberDetailController.abort();
        }

        memberDetailController = new AbortController();
        return memberDetailController.signal;
    }

    /**
     * Starts the first-page avatar read. A later member page does not abort it. A new selection does, through beginRequest.
     * @returns {AbortSignal} The signal for this avatar read.
     */
    function openMemberAvatarSignal() {
        if (memberAvatarController) {
            memberAvatarController.abort();
        }

        memberAvatarController = new AbortController();
        return memberAvatarController.signal;
    }

    /**
     * Removes the current detail content.
     * @returns {void}
     */
    function clearDetail() {
        if (detail) {
            detail.replaceChildren();
        }
    }

    /**
     * Replaces the tree with one status line.
     * A workspace load also shows the indeterminate bar under that line. Replacing the tree removes the bar.
     * @param {string} text Status text.
     * @param {boolean} isError True uses the error status class.
     * @param {boolean} [showProgress] True adds the loading bar. Error and empty results omit it.
     * @returns {void}
     */
    function renderStatus(text, isError, showProgress) {
        if (!tree) {
            return;
        }

        tree.replaceChildren();
        hideRowTooltip();

        const status = document.createElement("p");
        status.className = isError
            ? "navigator-status navigator-status-error"
            : "navigator-status";
        status.textContent = text;
        tree.appendChild(status);
        if (showProgress === true) {
            const progress = document.createElement("div");
            progress.className = "navigator-load-progress";
            progress.setAttribute("aria-hidden", "true");
            progress.appendChild(document.createElement("span"));
            tree.appendChild(progress);
        }
    }

    /**
     * Normalizes an id for comparison.
     * @param {*} value Candidate id. A falsy value returns an empty string.
     * @returns {string} The lower-case id, or an empty string.
     */
    function normalizeGuid(value) {
        if (!value) {
            return "";
        }

        return String(value).toLowerCase();
    }

    /**
     * Builds the anchor heading and the full direct-member count.
     * The count uses the same preview order, direct-child filter, and member-id deduplication as paging.
     * Loaded cards and missing profiles are not part of the count.
     * @param {object} row Selected anchor. A missing label is left blank. A missing path counts as zero.
     * @returns {string} The label, then the count in parentheses.
     */
    function detailHeadingText(row) {
        const label = row && row.nodeLabel ? String(row.nodeLabel) : "";
        const pathKey = row && row.pathKey ? row.pathKey : "";
        return label + " (" + String(directMemberIds(pathKey).length) + ")";
    }

    /**
     * Builds the detail section. The heading stays outside the card scroller.
     * @param {object} row Selected row. A missing label is left blank.
     * @returns {HTMLElement} The section, with an empty card body under the heading.
     */
    function createDetailSection(row) {
        const section = document.createElement("section");
        section.className = "member-map-detail";

        const label = document.createElement("h3");
        label.className = "output-heading";
        label.textContent = detailHeadingText(row);

        const body = document.createElement("div");
        body.className = "member-detail-body";

        section.appendChild(label);
        section.appendChild(body);
        return section;
    }

    /**
     * Returns the element that scrolls member cards. The heading is not inside it.
     * @param {ParentNode} [section] Detail section. Omitted uses the mounted pane.
     * @returns {HTMLElement|null} The card body, or the pane when the body is not present.
     */
    function memberCardScroller(section) {
        const root = section || detail;
        if (!root || typeof root.querySelector !== "function") {
            return detail;
        }

        return root.querySelector(".member-detail-body") || detail;
    }

    /**
     * Builds the indeterminate bar used under a loading line and under a later member page.
     * @returns {HTMLElement} The bar. It is hidden from assistive technology because the status text names the load.
     */
    function createMemberLoadProgress() {
        const progress = document.createElement("div");
        progress.className = "navigator-load-progress";
        progress.setAttribute("aria-hidden", "true");
        progress.appendChild(document.createElement("span"));
        return progress;
    }

    /**
     * Removes one request's bar. Another request's bar is a different element and stays.
     * @param {HTMLElement|null} progress The bar this request added. A missing or detached bar does nothing.
     * @returns {void}
     */
    function removeMemberLoadProgress(progress) {
        if (progress && typeof progress.remove === "function") {
            progress.remove();
        }
    }

    /**
     * Replaces the detail pane with the row heading and one status line.
     * The initial member-profile load also places the bar under that line. Replacing the pane removes it.
     * @param {object} row Selected row.
     * @param {string} text Status text.
     * @param {boolean} isError True uses the error class.
     * @param {boolean} [showProgress] True adds the loading bar. Errors and empty results omit it.
     * @returns {HTMLElement|null} The bar, when one was added.
     */
    function renderDetailStatus(row, text, isError, showProgress) {
        if (!detail) {
            return null;
        }

        detail.replaceChildren();
        const section = createDetailSection(row);
        const status = document.createElement("p");
        status.className = isError
            ? "output-note navigator-status-error"
            : "output-note";
        status.textContent = text;
        const body = memberCardScroller(section);
        body.appendChild(status);
        let progress = null;
        if (showProgress === true) {
            progress = createMemberLoadProgress();
            body.appendChild(progress);
        }
        detail.appendChild(section);
        return progress;
    }

    /**
     * Places the loading bar after the cards already on the pane.
     * @returns {HTMLElement|null} The bar. Null when the pane is missing.
     */
    function showPagedMemberProgress() {
        const body = memberCardScroller();
        if (!body) {
            return null;
        }

        const progress = createMemberLoadProgress();
        const list = body.querySelector(".member-map-children");
        if (list) {
            list.after(progress);
        } else {
            body.appendChild(progress);
        }
        return progress;
    }

    /**
     * Finds the member-card constructor on PluralBridge, then as a global.
     * @returns {Function|null} The constructor, or null when neither is a function.
     */
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

    /**
     * Writes a profile-load failure to the console. It does not change the tree.
     * @param {string} explanation Text written first.
     * @param {*} [error] Optional second console value. Undefined writes the explanation alone.
     * @returns {void}
     */
    function reportProfileFailure(explanation, error) {
        if (error !== undefined) {
            console.error(explanation, error);
            return;
        }

        console.error(explanation);
    }

    /**
     * Keeps a non-empty color string, including a color that is not #RRGGBB.
     * @param {*} value Candidate color. A non-string, blank string, or whitespace returns null.
     * @returns {string|null} The trimmed color, or null.
     */
    function storedMemberColor(value) {
        if (typeof value !== "string") {
            return null;
        }

        const trimmed = value.trim();
        return trimmed ? trimmed : null;
    }

    /**
     * Keeps visibility type ids 1 through 4.
     * @param {*} value Candidate id.
     * @returns {number|null} The id, or null for any other value.
     */
    function canonicalVisibilityId(value) {
        return value === 1 || value === 2 || value === 3 || value === 4 ? value : null;
    }

    /**
     * Builds the card member from the profile response, letting an unsaved local preview replace fields it actually stores.
     * A preview field is used only when that key is present. Visibility and permission prefer an explicit preview, then the placement fields.
     * @param {object} profile Profile row. Missing optional fields stay null or false as assigned below.
     * @param {string} memberId Member id used when the profile has none.
     * @param {string} accountId Account whose local preview is read.
     * @param {boolean} [includeAvatar] False keeps avatarObjectUrl null, including a local preview. The workspace detail route uses false.
     * @returns {object} The member passed to the card.
     */
    function profileForCard(profile, memberId, accountId, includeAvatar) {
        const membersApi = window.PluralBridge && window.PluralBridge.members
            ? window.PluralBridge.members
            : null;
        const preview = membersApi && typeof membersApi.readMemberProfilePreview === "function"
            ? membersApi.readMemberProfilePreview(accountId, memberId)
            : null;
        /**
         * Reports whether the local preview stored this field. A missing preview does not count as storing it.
         * @param {string} key Preview field name.
         * @returns {boolean} True when the preview object has the key.
         */
        const hasPreviewField = function (key) {
            return Boolean(preview) && Object.prototype.hasOwnProperty.call(preview, key);
        };

        return {
            memberId: profile ? profile.memberId : memberId,
            displayName: preview && Object.prototype.hasOwnProperty.call(preview, "displayName")
                ? preview.displayName
                : profile.displayName,
            pronouns: preview && Object.prototype.hasOwnProperty.call(preview, "pronouns")
                ? preview.pronouns
                : profile.pronouns,
            description: preview && Object.prototype.hasOwnProperty.call(preview, "description")
                ? preview.description
                : profile.description,
            color: hasPreviewField("color")
                ? storedMemberColor(preview.color)
                : storedMemberColor(profile && profile.color),
            fronting: hasPreviewField("fronting")
                ? preview.fronting
                : (profile && (profile.isFronting === true || profile.isFronting === false)
                    ? profile.isFronting
                    : null),
            visibilityKnown: Boolean(preview && preview.visibilityExplicit === true)
                || Boolean(profile && Object.prototype.hasOwnProperty.call(profile, "placementVisibilityMode"))
                || Boolean(profile && Object.prototype.hasOwnProperty.call(profile, "visibilityTypeId")),
            visibilityInherit: Boolean(preview && preview.visibilityExplicit === true)
                ? preview.visibilityInherit === true
                : (profile && Object.prototype.hasOwnProperty.call(profile, "placementVisibilityMode")
                    ? profile.placementVisibilityMode === "inherit"
                    : false),
            visibilityTypeId: preview && preview.visibilityExplicit === true
                ? (preview.visibilityInherit === true ? null : canonicalVisibilityId(preview.visibilityTypeId))
                : (profile && Object.prototype.hasOwnProperty.call(profile, "placementVisibilityMode")
                    ? (profile.placementVisibilityMode === "explicit"
                        ? canonicalVisibilityId(profile.placementVisibilityTypeId)
                        : null)
                    : (profile && Object.prototype.hasOwnProperty.call(profile, "visibilityTypeId")
                        ? canonicalVisibilityId(profile.visibilityTypeId)
                        : null)),
            permission: preview && preview.permissionExplicit === true
                ? preview.permission
                : (profile && Object.prototype.hasOwnProperty.call(profile, "placementPermissionMode")
                    ? (profile.placementPermissionMode === "explicit"
                        ? profile.placementPermissionCode
                        : (profile.placementPermissionMode === "inherit" ? "inherit" : null))
                    : null),
            avatarObjectUrl: includeAvatar === false
                ? null
                : (hasPreviewField("avatarObjectUrl")
                    ? preview.avatarObjectUrl
                    : (profile && profile.avatarUrl ? profile.avatarUrl : null)),
            resolvedVisibilityMode: profile && profile.resolvedVisibilityMode ? profile.resolvedVisibilityMode : null,
            resolvedVisibilityTypeId: profile ? canonicalVisibilityId(profile.resolvedVisibilityTypeId) : null,
            resolvedPermissionMode: profile && profile.resolvedPermissionMode ? profile.resolvedPermissionMode : null,
            resolvedPermissionCode: profile && profile.resolvedPermissionCode ? profile.resolvedPermissionCode : null
        };
    }

    /**
     * Builds the selected occurrence chain from this row through parentPathKey, at most 32 nodes.
     * A repeated path stops the walk. A row without a type or id stops it. The server validates this chain. The browser does not choose another parent.
     * @param {object} row Selected row.
     * @returns {string} Comma-separated type:id pairs from the selected row upward. An unusable row contributes nothing.
     */
    function selectedBranch(row) {
        const chain = [];
        const seen = {};
        let current = row;
        while (current && chain.length < 32) {
            if (!current.objectType || !current.objectId) {
                break;
            }
            if (current.pathKey && seen[current.pathKey]) {
                break;
            }
            if (current.pathKey) {
                seen[current.pathKey] = true;
            }
            chain.push(String(current.objectType) + ":" + String(current.objectId));
            if (!current.parentPathKey) {
                break;
            }
            const parentKey = current.parentPathKey;
            current = null;
            for (let index = 0; index < rows.length; index += 1) {
                if (rows[index] && rows[index].pathKey === parentKey) {
                    current = rows[index];
                    break;
                }
            }
        }
        return chain.join(",");
    }

    /**
     * Appends one member card. The card keeps its own Save and Fronting callbacks for the anchor selected now.
     * @param {HTMLElement} list Card list.
     * @param {object} row Selected anchor.
     * @param {object} profile Profile row for this member.
     * @param {string} id Normalized member id.
     * @param {boolean} includeAvatar False leaves the card image empty.
     * @returns {void}
     * @throws {Error} When createMemberAccordion is not a function.
     */
    function appendMemberCard(list, row, profile, id, includeAvatar) {
        const createCard = resolveCreateMemberAccordion();
        if (typeof createCard !== "function") {
            throw new Error(
                "Cannot create member cards: createMemberAccordion is not accessible from the navigator."
            );
        }

        const renderedAccountId = getAccountId();
        const card = createCard(profileForCard(profile, id, renderedAccountId, includeAvatar), {
            allowProfileEditing: true,
            previewHeader: true,
            /**
             * Saves Fronting for this card through the members API, using the anchor that was selected when the card was created.
             * A missing members API returns a saved:false outcome instead of rejecting.
             * @param {boolean} fronting Next fronting state. The members API treats only true as fronting.
             * @returns {Promise<object>} The members API outcome, or a saved:false object when the API is missing.
             */
            saveFronting: async function (fronting) {
                const membersApi = window.PluralBridge && window.PluralBridge.members
                    ? window.PluralBridge.members
                    : null;
                if (!membersApi || typeof membersApi.saveMemberFronting !== "function") {
                    return {
                        saved: false,
                        status: "Fronting could not be saved."
                    };
                }

                // Fronting uses the same selected occurrence as profile Save.
                return membersApi.saveMemberFronting(renderedAccountId, id, {
                    objectType: row.objectType,
                    objectId: row.objectId,
                    branch: selectedBranch(row)
                }, fronting);
            },
            /**
             * Saves this card's profile through the members API, using the same selected anchor and branch as Fronting.
             * A persisted name is copied onto matching member rows in the tree. Failure stays in the returned outcome.
             * @param {object} saveRequest Fields prepared by the card.
             * @returns {Promise<object>} The members API outcome, or a saved:false object when the API is missing.
             */
            saveProfile: async function (saveRequest) {
                const membersApi = window.PluralBridge && window.PluralBridge.members
                    ? window.PluralBridge.members
                    : null;
                if (!membersApi || typeof membersApi.saveMemberProfile !== "function") {
                    return {
                        saved: false,
                        status: "Member profile saving is not connected to an anchor-specific endpoint yet."
                    };
                }

                const outcome = await membersApi.saveMemberProfile(renderedAccountId, id, {
                    objectType: row.objectType,
                    objectId: row.objectId,
                    branch: selectedBranch(row)
                }, saveRequest);
                if (outcome && outcome.persisted === true && outcome.profile) {
                    reflectSharedMemberName(id, outcome.profile.displayName);
                }
                return outcome;
            }
        });
        if (card && typeof card.setAttribute === "function") {
            card.setAttribute("data-member-id", id);
        }
        list.appendChild(card);
    }

    /**
     * Draws member cards for member children of the selected anchor and reports profiles that do not match those children.
     * Each card receives the selected anchor and branch for both profile Save and immediate Fronting.
     * @param {object} row Selected anchor.
     * @param {Array<object>} profiles Profile rows. Rows without a member id are reported as extra.
     * @param {{omitAvatar?: boolean, batchLimited?: boolean}} [options] omitAvatar leaves the card image empty. batchLimited compares only the first 20 direct member children.
     * @returns {void}
     * @throws {Error} When a matched member cannot find createMemberAccordion.
     */
    function renderProfiles(row, profiles, options) {
        if (!detail) {
            return;
        }

        detail.replaceChildren();
        const section = createDetailSection(row);
        const children = immediateChildren(rows, row.pathKey);
        const profilesById = {};
        const profileIds = [];
        const profilesWithoutId = [];

        /**
         * Indexes a profile by member id. The first row for an id is kept. A row without an id is recorded separately.
         * @param {object} profile Profile row.
         * @returns {void}
         */
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
        /**
         * Keeps member children of the selected anchor.
         * @param {object} child Map child.
         * @returns {boolean} True when the child type is member.
         */
        const directMembers = children.filter(function (child) {
            return child.objectType === "member";
        });
        const memberChildren = options && options.batchLimited === true
            ? directMembers.slice(0, 20)
            : directMembers;
        const includeAvatar = !(options && options.omitAvatar === true);

        /**
         * Creates the card for one member child. A child without a profile is recorded as missing and is not given a card.
         * @param {object} child Member row.
         * @returns {void}
         */
        memberChildren.forEach(function (child) {
            const id = normalizeGuid(child.objectId);
            const profile = id ? profilesById[id] : null;
            if (!profile) {
                missing.push(id || "(missing member id)");
                return;
            }

            matched[id] = true;
            appendMemberCard(list, row, profile, id, includeAvatar);
        });

        /**
         * Keeps a profile id that was not matched to a displayed member child.
         * @param {string} id Normalized profile member id.
         * @returns {boolean} True when this id has no card.
         */
        const extra = profileIds.filter(function (id) {
            return matched[id] !== true;
        }).concat(profilesWithoutId);

        const body = memberCardScroller(section);
        if (directMembers.length === 0) {
            const empty = document.createElement("p");
            empty.className = "output-note";
            empty.textContent = "No member profiles were returned.";
            body.appendChild(empty);
        } else if (list.childNodes.length > 0) {
            body.appendChild(list);
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
            body.appendChild(mismatch);
        }

        detail.appendChild(section);
    }

    /**
     * Reports whether a profile response still belongs to the visible selection.
     * Both checks are required. A response can belong to the latest request and still be for an anchor the user has since left.
     * @param {{generation: number}} request Request returned by beginRequest.
     * @param {string} pathKey Path key captured when the load started.
     * @returns {boolean} True when the generation and selected path both still match.
     */
    function profileResponseIsCurrent(request, pathKey) {
        return request.generation === requestGeneration && selectedPathKey === pathKey;
    }

    /**
     * Loads profiles for the selected anchor and ignores a response that is no longer current.
     * An abort is ignored. Any other failure is reported and shown in the detail pane.
     * @param {object} row Selected anchor. A missing type or blank name shows an error when this request is still current.
     * @returns {void}
     */
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

        const progress = renderDetailStatus(row, "Loading member profiles.", false, true);

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

        apiClient.readAnchorMemberProfiles(row.objectType, anchorName, request.signal, row.objectId, selectedBranch(row))
            /**
             * Paints the profiles when this response is still current. A non-array body is a load failure.
             * @param {*} profiles Response body.
             * @returns {void}
             */
            .then(function (profiles) {
                if (!profileResponseIsCurrent(request, pathKey)) {
                    removeMemberLoadProgress(progress);
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
            /**
             * Ignores an abort. A later failure is shown only when this request is still the visible selection.
             * @param {*} error Rejection from the profile request. AbortError is ignored.
             * @returns {void}
             */
            .catch(function (error) {
                if (error && error.name === "AbortError") {
                    removeMemberLoadProgress(progress);
                    return;
                }

                if (!profileResponseIsCurrent(request, pathKey)) {
                    removeMemberLoadProgress(progress);
                    return;
                }

                reportProfileFailure(
                    "Member profile loading failed before the cards could be shown.",
                    error
                );
                renderDetailStatus(row, "The member profiles could not be loaded.", true);
            });
    }

    /**
     * Unique direct member ids for one occurrence, in preview order.
     * A repeated id is kept at its first row. Another parent, a non-member, and an empty id are omitted.
     * @param {string} pathKey Selected occurrence path.
     * @returns {Array<string>} The ids later windows skip through.
     */
    function directMemberIds(pathKey) {
        const seen = {};
        const ids = [];
        rows.forEach(function (row) {
            if (!row || row.parentPathKey !== pathKey || row.objectType !== "member") {
                return;
            }

            const id = normalizeGuid(row.objectId);
            if (!id || id === emptyMemberId || seen[id]) {
                return;
            }

            seen[id] = true;
            ids.push(id);
        });
        return ids;
    }

    /**
     * Finds one retained row by path.
     * @param {string} pathKey Path key.
     * @returns {object|null} The row, or null.
     */
    function rowByPath(pathKey) {
        for (let index = 0; index < rows.length; index += 1) {
            if (rows[index] && rows[index].pathKey === pathKey) {
                return rows[index];
            }
        }

        return null;
    }

    /**
     * Reports whether the member pane should request another window.
     * A pane that does not overflow still needs more. A pane near its bottom needs the next window before the user reaches it.
     * An unmeasured pane does not request, so a missing layout cannot walk every window by itself.
     * @returns {boolean} True when another window should start.
     */
    function memberPaneNeedsAnotherBatch() {
        const scroller = memberCardScroller();
        if (!scroller || scroller.clientHeight <= 0) {
            return false;
        }

        const remaining = scroller.scrollHeight - scroller.scrollTop - scroller.clientHeight;
        return remaining <= scroller.clientHeight;
    }

    /**
     * Shows a member-detail failure. Existing cards stay. The first failure, with no cards yet, replaces the pane.
     * @param {object} row Selected anchor.
     * @param {string} text Error text.
     * @returns {void}
     */
    function showWorkspaceDetailError(row, text) {
        if (!detail || !detail.querySelector(".member-card")) {
            renderDetailStatus(row, text, true);
            return;
        }

        const host = memberCardScroller() || detail;
        let note = detail.querySelector(".member-detail-page-error");
        if (!note) {
            note = document.createElement("p");
            note.className = "output-note navigator-status-error member-detail-page-error";
            host.appendChild(note);
        }

        note.textContent = text;
    }

    /**
     * Appends one window of cards. Cards already on the pane are left in place, including their edits and expansion.
     * A profile outside this window, or for a member already shown, is not added.
     * @param {object} row Selected anchor.
     * @param {Array<object>} profiles Profile rows for this window.
     * @param {number} skip Unique direct members this window passed over.
     * @returns {void}
     */
    function paintWorkspaceMemberPage(row, profiles, skip) {
        if (!detail) {
            return;
        }

        const profilesById = {};
        profiles.forEach(function (profile) {
            const id = normalizeGuid(profile && profile.memberId);
            if (id && !profilesById[id]) {
                profilesById[id] = profile;
            }
        });

        const windowIds = directMemberIds(row.pathKey).slice(skip, skip + memberDetailBatchSize);
        let section = detail.querySelector(".member-map-detail");
        let list = section ? section.querySelector(".member-map-children") : null;
        if (!section || !list) {
            detail.replaceChildren();
            shownMemberIds = {};
            section = createDetailSection(row);
            list = document.createElement("div");
            list.className = "member-map-children";
            memberCardScroller(section).appendChild(list);
            detail.appendChild(section);
        }

        const pageError = detail.querySelector(".member-detail-page-error");
        if (pageError) {
            pageError.remove();
        }

        windowIds.forEach(function (id) {
            if (shownMemberIds[id] || !profilesById[id]) {
                return;
            }

            shownMemberIds[id] = true;
            appendMemberCard(list, row, profilesById[id], id, false);
        });

        const body = memberCardScroller(section);
        let empty = section.querySelector(".member-detail-empty");
        if (directMemberIds(row.pathKey).length === 0) {
            list.remove();
            if (!empty) {
                empty = document.createElement("p");
                empty.className = "output-note member-detail-empty";
                empty.textContent = "No member profiles were returned.";
                body.appendChild(empty);
            }
        } else if (empty) {
            empty.remove();
        }
    }

    /**
     * The cards currently on the detail pane. Each card owns its loading bar. This does not choose a page.
     * @param {ParentNode|null} root Detail pane. A missing pane returns an empty list.
     * @returns {Array<HTMLElement>} Every member card in that pane, in document order.
     */
    function avatarCardsIn(root) {
        if (!root || typeof root.querySelectorAll !== "function") {
            return [];
        }

        return Array.prototype.slice.call(root.querySelectorAll(".member-card"));
    }

    /**
     * Shows or hides the avatar bar on each supplied card. Cards without the method are skipped.
     * @param {Array<HTMLElement>} cards The cards this avatar request started. A later page's cards are a different list.
     * @param {boolean} loading True when those cards are waiting. False removes the bars this request showed.
     * @returns {void}
     */
    function setCardsAvatarLoading(cards, loading) {
        cards.forEach(function (card) {
            if (card && typeof card.setWorkspaceAvatarLoading === "function") {
                card.setWorkspaceAvatarLoading(loading);
            }
        });
    }

    /**
     * Requests avatars for the cards just painted on the first page.
     * A later page does not call this. Failure leaves those cards in place and does not request again.
     * @param {object} row Selected anchor.
     * @param {{generation: number, profileId: string|null}} request Generation captured for this selection, and the workspace-open run id when this page belongs to that open.
     * @param {number} serial Detail serial captured when the card request started.
     * @returns {void}
     */
    function requestFirstBatchAvatars(row, request, serial) {
        if (!detail || !detail.querySelector(".member-card")) {
            publishWorkspaceLoadReport(request);
            return;
        }

        const pathKey = row.pathKey;
        const capturedContext = memberDetailContext;
        if (serial !== memberDetailSerial || !profileResponseIsCurrent(request, pathKey) || contextId !== capturedContext) {
            endOpenedProfile(request, "avatars", "incomplete");
            return;
        }

        const apiClient = window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;
        if (!apiClient || typeof apiClient.readWorkspaceMemberAvatars !== "function") {
            endOpenedProfile(request, "avatars", "failed");
            return;
        }

        if (request.treeProfile) {
            markAvatarProfile(request, "avatarRequestStart");
        }

        const loadingCards = avatarCardsIn(detail);
        setCardsAvatarLoading(loadingCards, true);
        apiClient.readWorkspaceMemberAvatars(
            capturedContext,
            pathKey,
            openMemberAvatarSignal(),
            request.profileId,
            request.treeProfile ? {
                onResponse: function (response) {
                    if (request.treeProfile.avatarBrowser && response.headers && typeof response.headers.get === "function") {
                        request.treeProfile.avatarBrowser.serverHeader = response.headers.get("X-PB-Workspace-Profile");
                    }
                },
                bodyReceived: function () {
                    markAvatarProfile(request, "avatarBodyReceived");
                },
                jsonParsed: function () {
                    markAvatarProfile(request, "avatarJsonParsed");
                }
            } : null
        )
            /**
             * Paints images onto the cards already shown. A stale response is dropped before it creates an object URL.
             * @param {*} images Avatar rows. A non-array body is a failed read.
             * @returns {void}
             */
            .then(function (images) {
                if (serial !== memberDetailSerial || !profileResponseIsCurrent(request, pathKey) || contextId !== capturedContext) {
                    setCardsAvatarLoading(loadingCards, false);
                    if (request.treeProfile && request.treeProfile.avatarBrowser) {
                        request.treeProfile.avatarBrowser.phase = "incomplete";
                    }
                    endOpenedProfile(request, "avatars", "incomplete");
                    return;
                }

                if (!Array.isArray(images)) {
                    setCardsAvatarLoading(loadingCards, false);
                    if (request.treeProfile && request.treeProfile.avatarBrowser) {
                        request.treeProfile.avatarBrowser.phase = "incomplete";
                    }
                    reportProfileFailure(
                        "Member avatar loading failed because the avatar response was not an array.",
                        images
                    );
                    endOpenedProfile(request, "avatars", "failed");
                    return;
                }

                applyFirstBatchAvatars(images, request, loadingCards);
            })
            /**
             * Leaves the cards usable. Abort and a later failure do not start another avatar read.
             * @param {*} error Rejection from the avatar request. AbortError is ignored.
             * @returns {void}
             */
            .catch(function (error) {
                setCardsAvatarLoading(loadingCards, false);
                if (request.treeProfile && request.treeProfile.avatarBrowser) {
                    request.treeProfile.avatarBrowser.phase = "incomplete";
                }

                if (serial !== memberDetailSerial) {
                    endOpenedProfile(request, "avatars", "incomplete");
                    return;
                }

                if (error && error.name === "AbortError") {
                    endOpenedProfile(request, "avatars", "cancelled");
                    return;
                }

                if (!profileResponseIsCurrent(request, pathKey) || contextId !== capturedContext) {
                    endOpenedProfile(request, "avatars", "incomplete");
                    return;
                }

                reportProfileFailure(
                    "Member avatar loading failed after the cards were shown.",
                    error
                );
                endOpenedProfile(request, "avatars", "failed");
            });
    }

    /**
     * Applies one first-page avatar response to the matching cards.
     * A null image leaves the placeholder. The card decides whether a pending avatar edit keeps its preview.
     * @param {Array<object>} images Rows with memberId, contentType, and imageBase64.
     * @param {object} [profileRequest] The member request carrying the workspace-open profile. Omitted applies the images without timings.
     * @param {Array<HTMLElement>} [loadingCards] Cards whose bars this request showed. A card the response does not name loses its bar.
     * @returns {void}
     */
    function applyFirstBatchAvatars(images, profileRequest, loadingCards) {
        const notes = [];
        const handled = [];
        images.forEach(function (image) {
            const id = normalizeGuid(image && image.memberId);
            if (!id || !detail) {
                return;
            }

            const card = detail.querySelector('[data-member-id="' + id + '"]');
            const decoded = objectUrlForAvatar(image, profileRequest);
            const objectUrl = decoded.url;
            if (!card || typeof card.applyWorkspaceAvatar !== "function") {
                revokeAvatarObjectUrl(objectUrl);
                return;
            }

            handled.push(card);

            if (!objectUrl) {
                const kept = card.applyWorkspaceAvatar(null);
                if (!kept) {
                    if (profileRequest && profileRequest.treeProfile && profileRequest.treeProfile.avatarBrowser) {
                        profileRequest.treeProfile.avatarBrowser.pendingEdits += 1;
                    }
                    notes.push({ memberId: id, settle: "notApplied" });
                    return;
                }

                if (profileRequest && profileRequest.treeProfile && profileRequest.treeProfile.avatarBrowser) {
                    profileRequest.treeProfile.avatarBrowser.placeholders += 1;
                }
                notes.push({ memberId: id, settle: "placeholder" });
                return;
            }

            const applied = card.applyWorkspaceAvatar(objectUrl);
            if (!applied) {
                if (profileRequest && profileRequest.treeProfile && profileRequest.treeProfile.avatarBrowser) {
                    profileRequest.treeProfile.avatarBrowser.pendingEdits += 1;
                }
                revokeAvatarObjectUrl(objectUrl);
                notes.push({ memberId: id, decodeMs: decoded.elapsedMs, settle: "notApplied" });
                return;
            }

            const preview = card.querySelector(".member-avatar-image");
            notes.push({
                memberId: id,
                decodeMs: decoded.elapsedMs,
                settle: preview ? "pending" : "placeholder",
                image: preview
            });
            if (!preview && profileRequest && profileRequest.treeProfile && profileRequest.treeProfile.avatarBrowser) {
                profileRequest.treeProfile.avatarBrowser.placeholders += 1;
            }
        });
        (loadingCards || []).forEach(function (card) {
            if (handled.indexOf(card) >= 0 || typeof card.setWorkspaceAvatarLoading !== "function") {
                return;
            }

            card.setWorkspaceAvatarLoading(false);
        });
        markAvatarProfile(profileRequest, "avatarApplyComplete");
        settleAppliedAvatars(profileRequest, notes);
    }

    /**
     * Waits for each applied image to decode or fail. Placeholders and pending edits are already counted.
     * Completion here is image decode or a load/error event. It is not screen paint.
     * @param {object} profileRequest The member request. A request without a profile returns immediately.
     * @param {Array<object>} notes Per-image notes from applyFirstBatchAvatars.
     * @returns {void}
     */
    function settleAppliedAvatars(profileRequest, notes) {
        const browser = profileRequest && profileRequest.treeProfile
            ? profileRequest.treeProfile.avatarBrowser
            : null;
        if (!browser) {
            publishWorkspaceLoadReport(profileRequest);
            return;
        }

        browser.images = notes.map(function (note) {
            return {
                memberId: note.memberId,
                decodeMs: note.decodeMs,
                settle: note.settle
            };
        });
        // Image decode or a load/error event settles the image. It is not screen paint.
        const waiting = notes.filter(function (note) {
            return note.settle === "pending" && note.image;
        });
        if (waiting.length === 0) {
            browser.phase = "complete";
            markAvatarProfile(profileRequest, "displayComplete");
            if (profileRequest.treeProfile) {
                profileRequest.treeProfile.avatarStatus = "complete";
            }
            publishWorkspaceLoadReport(profileRequest);
            return;
        }

        let remaining = waiting.length;
        /**
         * Records one settled image. The phase stays incomplete until every applied image has settled.
         * @param {object} note The image note.
         * @param {string} settle loaded or error.
         * @returns {void}
         */
        function finishOne(note, settle) {
            note.settle = settle;
            browser.images.forEach(function (image) {
                if (image.memberId === note.memberId && image.settle === "pending") {
                    image.settle = settle;
                }
            });
            remaining -= 1;
            if (remaining === 0) {
                browser.phase = "complete";
                markAvatarProfile(profileRequest, "displayComplete");
                if (profileRequest.treeProfile) {
                    profileRequest.treeProfile.avatarStatus = "complete";
                }
                publishWorkspaceLoadReport(profileRequest);
            }
        }

        waiting.forEach(function (note) {
            const image = note.image;
            if (typeof image.decode === "function") {
                image.decode().then(function () {
                    finishOne(note, "loaded");
                }, function () {
                    finishOne(note, "error");
                });
                return;
            }

            if (image.complete) {
                finishOne(note, image.naturalWidth > 0 ? "loaded" : "error");
                return;
            }

            image.addEventListener("load", function () {
                finishOne(note, "loaded");
            });
            image.addEventListener("error", function () {
                finishOne(note, "error");
            });
        });
    }

    /**
     * Builds an object URL for one returned image. Null image fields stay null and create no URL.
     * @param {object} image One avatar row.
     * @param {object} [profileRequest] The profiled request. Present only while the first batch is being timed.
     * @returns {{url: string|null, elapsedMs: number|null}} The object URL and the decode duration. A null image has no duration.
     */
    function objectUrlForAvatar(image, profileRequest) {
        if (!image || image.imageBase64 == null || image.imageBase64 === "" || typeof URL.createObjectURL !== "function") {
            return { url: null, elapsedMs: null };
        }

        const started = profileRequest && profileRequest.treeProfile && window.performance && typeof window.performance.now === "function"
            ? window.performance.now()
            : null;
        try {
            const binary = atob(image.imageBase64);
            const bytes = new Uint8Array(binary.length);
            for (let index = 0; index < binary.length; index += 1) {
                bytes[index] = binary.charCodeAt(index);
            }
            return {
                url: URL.createObjectURL(new Blob([bytes], {
                    type: image.contentType || "application/octet-stream"
                })),
                elapsedMs: started == null ? null : window.performance.now() - started
            };
        } catch (error) {
            return {
                url: null,
                elapsedMs: started == null ? null : window.performance.now() - started
            };
        }
    }

    /**
     * Releases an object URL this load created and then did not keep.
     * @param {string|null} objectUrl URL returned by objectUrlForAvatar. A route or a null URL is left alone.
     * @returns {void}
     */
    function revokeAvatarObjectUrl(objectUrl) {
        if (objectUrl && String(objectUrl).indexOf("blob:") === 0 && typeof URL.revokeObjectURL === "function") {
            URL.revokeObjectURL(objectUrl);
        }
    }

    /**
     * Requests one member window for the captured context and occurrence.
     * Success advances skip by the window size. Failure leaves skip where it was and does not request again.
     * @param {object} row Selected anchor.
     * @param {number} skip Unique direct members to pass over.
     * @param {{generation: number}} request Generation captured for this selection.
     * @param {AbortSignal} signal Abort signal for this window.
     * @param {HTMLElement|null} progress The bar shown for this window. Finishing this window removes that element only.
     * @returns {void}
     */
    function startMemberDetailRequest(row, skip, request, signal, progress) {
        const pathKey = row.pathKey;
        const capturedContext = memberDetailContext;
        if (memberDetailInFlight || memberDetailExhausted || !capturedContext || !profileResponseIsCurrent(request, pathKey)) {
            removeMemberLoadProgress(progress);
            endOpenedProfile(request, "members", "incomplete");
            return;
        }

        if (skip > 0 && skip >= directMemberIds(pathKey).length) {
            memberDetailExhausted = true;
            removeMemberLoadProgress(progress);
            return;
        }

        const apiClient = window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;
        if (!apiClient || typeof apiClient.readWorkspaceMemberDetails !== "function") {
            removeMemberLoadProgress(progress);
            reportProfileFailure(
                "Member profile loading failed because readWorkspaceMemberDetails is not available on the API client."
            );
            showWorkspaceDetailError(row, "The member profiles could not be loaded.");
            endOpenedProfile(request, "members", "failed");
            return;
        }

        const serial = memberDetailSerial;
        memberDetailInFlight = true;
        if (skip === 0 && request.treeProfile) {
            markMemberProfile(request, "memberRequestStart");
        }

        apiClient.readWorkspaceMemberDetails(
            capturedContext,
            pathKey,
            signal,
            skip,
            skip === 0 ? request.profileId : null,
            skip === 0 && request.treeProfile ? {
                onResponse: function (response) {
                    if (response.headers && typeof response.headers.get === "function") {
                        request.treeProfile.memberServerHeader = response.headers.get("X-PB-Workspace-Profile");
                    }
                },
                bodyReceived: function () {
                    markMemberProfile(request, "memberBodyReceived");
                },
                jsonParsed: function () {
                    markMemberProfile(request, "memberJsonParsed");
                }
            } : null
        )
            /**
             * Appends the window when this selection is still visible. A non-array body is a load failure.
             * @param {*} profiles Response body.
             * @returns {void}
             */
            .then(function (profiles) {
                removeMemberLoadProgress(progress);
                if (serial !== memberDetailSerial) {
                    endOpenedProfile(request, "members", "incomplete");
                    return;
                }

                memberDetailInFlight = false;
                if (!profileResponseIsCurrent(request, pathKey) || contextId !== capturedContext) {
                    endOpenedProfile(request, "members", "incomplete");
                    return;
                }

                if (!Array.isArray(profiles)) {
                    reportProfileFailure(
                        "Member profile loading failed because the profile response was not an array.",
                        profiles
                    );
                    showWorkspaceDetailError(row, "The member profiles could not be loaded.");
                    endOpenedProfile(request, "members", "failed");
                    return;
                }

                paintWorkspaceMemberPage(row, profiles, skip);
                if (skip === 0) {
                    if (request.treeProfile) {
                        request.treeProfile.cardCount = profiles.length;
                        request.treeProfile.membersStatus = "complete";
                    }

                    markMemberProfile(request, "memberCardsDom");
                    requestFirstBatchAvatars(row, request, serial);
                }
                memberDetailSkip = skip + memberDetailBatchSize;
                if (memberDetailSkip >= directMemberIds(pathKey).length) {
                    memberDetailExhausted = true;
                } else if (memberPaneNeedsAnotherBatch()) {
                    requestNextWorkspaceMemberDetails();
                }
            })
            /**
             * Ignores an abort. A later failure leaves the cards and the next skip unchanged.
             * Removing this window's bar does not remove the bar a newer selection added.
             * @param {*} error Rejection from the detail request. AbortError is ignored.
             * @returns {void}
             */
            .catch(function (error) {
                removeMemberLoadProgress(progress);
                if (serial !== memberDetailSerial) {
                    endOpenedProfile(request, "members", "incomplete");
                    return;
                }

                memberDetailInFlight = false;
                if (error && error.name === "AbortError") {
                    endOpenedProfile(request, "members", "cancelled");
                    return;
                }

                if (!profileResponseIsCurrent(request, pathKey) || contextId !== capturedContext) {
                    endOpenedProfile(request, "members", "incomplete");
                    return;
                }

                reportProfileFailure(
                    "Member profile loading failed before the cards could be shown.",
                    error
                );
                showWorkspaceDetailError(row, "The member profiles could not be loaded.");
                endOpenedProfile(request, "members", "failed");
            });
    }

    /**
     * Requests the next window for the current selection. A window already in flight is left alone.
     * @returns {void}
     */
    function requestNextWorkspaceMemberDetails() {
        if (memberDetailInFlight || memberDetailExhausted || !workspaceTreeEnabled()) {
            return;
        }

        if (!contextId || contextId !== memberDetailContext || selectedPathKey !== memberDetailPath) {
            return;
        }

        const row = rowByPath(selectedPathKey);
        if (!row) {
            return;
        }

        const progress = memberDetailSkip === 0 && (!detail || !detail.querySelector(".member-card"))
            ? renderDetailStatus(row, "Loading member profiles.", false, true)
            : showPagedMemberProgress();
        startMemberDetailRequest(row, memberDetailSkip, {
            generation: requestGeneration
        }, openMemberDetailSignal(), progress);
    }

    /**
     * Loads the first member window when the pane is scrolled near its bottom, or when a short pane receives a wheel.
     * A short pane does not emit scroll, so the wheel is the retry after a failed window.
     * @returns {void}
     */
    function onMemberPaneNeedsMore() {
        if (!workspaceTreeEnabled() || !memberPaneNeedsAnotherBatch()) {
            return;
        }

        requestNextWorkspaceMemberDetails();
    }

    /**
     * Listens once on the member pane. Later mounts of the same element do not add another listener.
     * @returns {void}
     */
    function bindMemberPaneScroll() {
        if (!detail || detail.getAttribute("data-member-detail-scroll") === "true") {
            return;
        }

        detail.setAttribute("data-member-detail-scroll", "true");
        // Capture hears the card body. The heading is outside that scroller, and the navigator tree scrolls on its own element.
        detail.addEventListener("scroll", onMemberPaneNeedsMore, true);
        detail.addEventListener("wheel", function () {
            const scroller = memberCardScroller();
            if (!scroller || scroller.scrollHeight > scroller.clientHeight) {
                return;
            }

            requestNextWorkspaceMemberDetails();
        });
    }

    /**
     * Loads the first direct-member window for the selected anchor in the retained workspace context.
     * A late response is ignored. Later windows append cards. The first window requests avatars after its cards are shown.
     * @param {object} row Selected anchor. A missing context id shows an error when this request is still current.
     * @param {string|null} [profileId] Run id captured by the workspace open that is selecting its first anchor. A later selection omits it.
     * @param {object} [profileRequest] The workspace-open request. Its profile supplies the origin for the first member page.
     * @returns {void}
     */
    function loadWorkspaceMemberDetails(row, profileId, profileRequest) {
        const pathKey = row.pathKey;
        const request = beginRequest();
        request.profileId = profileId || null;
        request.treeProfile = profileRequest && profileRequest.treeProfile && request.profileId === profileRequest.treeProfile.runId
            ? profileRequest.treeProfile
            : null;
        memberDetailSkip = 0;
        memberDetailExhausted = false;
        memberDetailPath = pathKey;
        memberDetailContext = contextId || "";
        shownMemberIds = {};
        if (!contextId) {
            if (!profileResponseIsCurrent(request, pathKey)) {
                endOpenedProfile(request, "members", "incomplete");
                return;
            }

            renderDetailStatus(row, "The member profiles could not be loaded.", true);
            endOpenedProfile(request, "members", "failed");
            return;
        }

        const progress = renderDetailStatus(row, "Loading member profiles.", false, true);
        startMemberDetailRequest(row, 0, request, openMemberDetailSignal(), progress);
    }

    /**
     * Appends one SVG shape and copies every supplied attribute.
     * @param {SVGElement} svg Parent icon.
     * @param {string} tagName SVG tag name.
     * @param {object} attributes Attribute names and values.
     * @returns {void}
     */
    function appendShape(svg, tagName, attributes) {
        const shape = document.createElementNS("http://www.w3.org/2000/svg", tagName);
        /**
         * Sets one attribute on the shape being appended.
         * @param {string} name Attribute name.
         * @returns {void}
         */
        Object.keys(attributes).forEach(function (name) {
            shape.setAttribute(name, attributes[name]);
        });
        svg.appendChild(shape);
    }

    /**
     * Builds the tree icon for a system, group, place, or member.
     * @param {string} objectType Object type. Any other type returns null.
     * @returns {SVGElement|null} The icon, or null.
     */
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

    /**
     * Builds the icon and text for a tree row. An unknown type has text without an icon.
     * @param {string} objectType Icon type.
     * @param {string} labelText Visible label.
     * @returns {HTMLElement} The content element.
     */
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

    /**
     * Returns the icon type for a known object type.
     * @param {object} row Map row. Null or an unknown type returns an empty string.
     * @returns {string} system, group, place, member, or an empty string.
     */
    function iconFor(row) {
        if (!row) {
            return "";
        }

        if (row.objectType === "system" || row.objectType === "group" || row.objectType === "place" || row.objectType === "member") {
            return row.objectType;
        }

        return "";
    }

    /**
     * Finds the navigator row that contains an event target.
     * @param {EventTarget} target Event target. A non-element, or a target outside the tree, returns null.
     * @returns {HTMLElement|null} The row button or label.
     */
    function rowHostFromTarget(target) {
        if (!(target instanceof Element) || !tree || !tree.contains(target)) {
            return null;
        }

        const host = target.closest(".member-system-node-button, .member-system-node-label");
        if (!host || !tree.contains(host)) {
            return null;
        }

        return host;
    }

    /**
     * Reports whether a row label is cut off by the width available to it.
     * @param {Element|null} textEl Label text. A missing element is not truncated.
     * @returns {boolean} True when the content is wider than the visible box.
     */
    function labelIsTruncated(textEl) {
        return Boolean(textEl && textEl.scrollWidth > textEl.clientWidth + 1);
    }

    /**
     * Creates the single navigator tooltip. It is plain text and is not a native title.
     * @returns {HTMLElement} The tooltip element.
     */
    function ensureRowTooltip() {
        if (rowTooltip) {
            return rowTooltip;
        }

        rowTooltip = document.createElement("div");
        rowTooltip.className = "navigator-row-tooltip";
        rowTooltip.setAttribute("role", "tooltip");
        rowTooltip.setAttribute("aria-hidden", "true");
        rowTooltip.hidden = true;
        /**
         * Hides the tooltip when the pointer leaves it and does not return to the row.
         * @param {PointerEvent} event Pointer leave event.
         * @returns {void}
         */
        rowTooltip.addEventListener("pointerout", function (event) {
            const next = event.relatedTarget instanceof Element ? event.relatedTarget : null;
            if (next && (rowTooltip.contains(next) || (tooltipAnchor && tooltipAnchor.contains(next)))) {
                return;
            }

            tooltipDismissed = false;
            hideRowTooltip();
        });
        document.body.appendChild(rowTooltip);
        return rowTooltip;
    }

    /**
     * Hides the navigator row tooltip.
     * @returns {void}
     */
    function hideRowTooltip() {
        tooltipAnchor = null;
        if (!rowTooltip) {
            return;
        }

        rowTooltip.hidden = true;
        rowTooltip.textContent = "";
    }

    /**
     * Places the tooltip below the row, or above it when that stays inside the viewport.
     * @param {HTMLElement} host Row button or label.
     * @returns {void}
     */
    function positionRowTooltip(host) {
        const tip = ensureRowTooltip();
        const margin = 8;
        tip.style.left = "0px";
        tip.style.top = "0px";
        const hostRect = host.getBoundingClientRect();
        const tipRect = tip.getBoundingClientRect();
        let left = hostRect.left;
        let top = hostRect.bottom + 4;
        if (left + tipRect.width > window.innerWidth - margin) {
            left = window.innerWidth - margin - tipRect.width;
        }

        if (left < margin) {
            left = margin;
        }

        if (top + tipRect.height > window.innerHeight - margin) {
            top = hostRect.top - tipRect.height - 4;
        }

        if (top < margin) {
            top = margin;
        }

        tip.style.left = left + "px";
        tip.style.top = top + "px";
    }

    /**
     * Shows the full label when that row's text is truncated. An untruncated row does not show it.
     * @param {HTMLElement|null} host Row button or label.
     * @returns {void}
     */
    function showRowTooltip(host) {
        if (!host || tooltipDismissed) {
            return;
        }

        const textEl = host.querySelector(".member-tree-node-text");
        if (!labelIsTruncated(textEl)) {
            if (tooltipAnchor === host) {
                hideRowTooltip();
            }
            return;
        }

        const tip = ensureRowTooltip();
        tip.textContent = textEl.textContent || "";
        tip.hidden = false;
        tooltipAnchor = host;
        positionRowTooltip(host);
    }

    /**
     * Drops native title attributes on navigator row labels and repositions a visible tooltip.
     * @returns {void}
     */
    function syncRowTruncation() {
        if (!tree) {
            hideRowTooltip();
            return;
        }

        const labels = tree.querySelectorAll(".member-tree-node-text");
        /**
         * Removes a native title from one row label so it cannot duplicate the tooltip.
         * @param {Element} textEl Label text element.
         * @returns {void}
         */
        labels.forEach(function (textEl) {
            textEl.removeAttribute("title");
            const host = textEl.closest(".member-system-node-button, .member-system-node-label");
            if (host && tree.contains(host)) {
                host.removeAttribute("title");
            }
        });

        if (!tooltipAnchor || !tree.contains(tooltipAnchor) || !labelIsTruncated(tooltipAnchor.querySelector(".member-tree-node-text"))) {
            if (tooltipAnchor) {
                hideRowTooltip();
            }
            return;
        }

        if (rowTooltip && !rowTooltip.hidden) {
            positionRowTooltip(tooltipAnchor);
        }
    }

    /**
     * Rechecks truncation after the browser has applied the current layout.
     * @returns {void}
     */
    function scheduleTruncationCheck() {
        window.requestAnimationFrame(function () {
            syncRowTruncation();
        });
    }

    /**
     * Shows the tooltip for a touch selection after the tree has been redrawn.
     * @param {string} pathKey Path that was tapped. A different pending path does nothing.
     * @returns {void}
     */
    function revealTouchSelection(pathKey) {
        if (!tree || touchRevealPath !== pathKey) {
            return;
        }

        touchRevealPath = null;
        const active = tree.querySelector(".member-system-node-button.is-active");
        if (active) {
            showRowTooltip(active);
        }
    }

    /**
     * Records the pointer kind and allows a later hover after Escape.
     * @param {PointerEvent} event Pointer down event.
     * @returns {void}
     */
    function onRowPointerDown(event) {
        lastPointerType = event.pointerType || "mouse";
        tooltipDismissed = false;
    }

    /**
     * Shows the tooltip while the mouse is over a truncated row label.
     * @param {PointerEvent} event Pointer enter event. Touch pointers are ignored.
     * @returns {void}
     */
    function onRowPointerOver(event) {
        if (event.pointerType === "touch") {
            return;
        }

        showRowTooltip(rowHostFromTarget(event.target));
    }

    /**
     * Hides the tooltip when the pointer leaves the row and the tooltip.
     * @param {PointerEvent} event Pointer leave event.
     * @returns {void}
     */
    function onRowPointerOut(event) {
        if (!tooltipAnchor) {
            return;
        }

        const target = event.target instanceof Element ? event.target : null;
        if (!target || !tooltipAnchor.contains(target)) {
            return;
        }

        const next = event.relatedTarget instanceof Element ? event.relatedTarget : null;
        if (next && (tooltipAnchor.contains(next) || (rowTooltip && rowTooltip.contains(next)))) {
            return;
        }

        tooltipDismissed = false;
        hideRowTooltip();
    }

    /**
     * Shows the tooltip when keyboard focus reaches a truncated row.
     * @param {FocusEvent} event Focus event.
     * @returns {void}
     */
    function onRowFocusIn(event) {
        showRowTooltip(rowHostFromTarget(event.target));
    }

    /**
     * Hides the tooltip when focus leaves the row.
     * @param {FocusEvent} event Focus event.
     * @returns {void}
     */
    function onRowFocusOut(event) {
        const next = event.relatedTarget instanceof Element ? event.relatedTarget : null;
        if (tooltipAnchor && next && tooltipAnchor.contains(next)) {
            return;
        }

        tooltipDismissed = false;
        hideRowTooltip();
    }

    /**
     * Hides the tooltip when the pointer starts outside the row and the tooltip.
     * @param {PointerEvent} event Pointer down event.
     * @returns {void}
     */
    function onDocumentPointerDown(event) {
        if (!rowTooltip || rowTooltip.hidden) {
            return;
        }

        const target = event.target instanceof Element ? event.target : null;
        if (target && (rowTooltip.contains(target) || (tooltipAnchor && tooltipAnchor.contains(target)))) {
            return;
        }

        tooltipDismissed = false;
        hideRowTooltip();
    }

    /**
     * Dismisses a visible tooltip on Escape without removing the row's accessible name.
     * @param {KeyboardEvent} event Key event.
     * @returns {void}
     */
    function onTooltipKeyDown(event) {
        if (event.key !== "Escape" || !rowTooltip || rowTooltip.hidden) {
            return;
        }

        tooltipDismissed = true;
        hideRowTooltip();
        event.preventDefault();
        event.stopPropagation();
    }

    /**
     * Dismisses a visible tooltip when any element scrolls.
     * @returns {void}
     */
    function onAnyScroll() {
        if (!rowTooltip || rowTooltip.hidden) {
            return;
        }

        tooltipDismissed = false;
        hideRowTooltip();
    }

    /**
     * Listens for hover, focus, resize, scroll, and panel close on the mounted navigator tree.
     * @returns {void}
     */
    function bindRowTooltips() {
        if (!tooltipDocumentBound) {
            document.addEventListener("pointerdown", onDocumentPointerDown, true);
            document.addEventListener("keydown", onTooltipKeyDown, true);
            window.addEventListener("scroll", onAnyScroll, true);
            window.addEventListener("resize", scheduleTruncationCheck);
            tooltipDocumentBound = true;
        }

        if (!tree || tree === tooltipTree) {
            return;
        }

        tree.addEventListener("pointerdown", onRowPointerDown);
        tree.addEventListener("pointerover", onRowPointerOver);
        tree.addEventListener("pointerout", onRowPointerOut);
        tree.addEventListener("focusin", onRowFocusIn);
        tree.addEventListener("focusout", onRowFocusOut);
        tooltipTree = tree;

        const panel = tree.closest(".member-systems-panel");
        if (panelResizeObserver) {
            panelResizeObserver.disconnect();
        }

        if (panelAttributeObserver) {
            panelAttributeObserver.disconnect();
        }

        if (!panel) {
            return;
        }

        panelResizeObserver = new ResizeObserver(function () {
            scheduleTruncationCheck();
        });
        panelResizeObserver.observe(panel);
        panelAttributeObserver = new MutationObserver(function () {
            if (panel.hidden) {
                tooltipDismissed = false;
                hideRowTooltip();
                return;
            }

            scheduleTruncationCheck();
        });
        panelAttributeObserver.observe(panel, {
            attributes: true,
            attributeFilter: ["hidden", "class"]
        });
    }

    /**
     * Appends one tree row and, when it is expanded, its children.
     * A selectable row selects that anchor. Expanding or collapsing updates the held expansion and redraws the tree.
     * @param {object} row Map row.
     * @param {number} level Nesting level stored as a CSS variable.
     * @returns {void}
     */
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
            /**
             * Toggles this row's expansion, stores it, and redraws the tree after a short delay unless reduced motion is requested.
             * @returns {void}
             */
            expandButton.addEventListener("click", function () {
                const expanded = !isExpanded;
                expandedPathKeys[row.pathKey] = expanded;
                holdExpansion();
                const chevron = expandButton.querySelector(".member-tree-chevron");
                if (chevron) {
                    chevron.classList.toggle("is-expanded", expanded);
                }
                const reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
                /**
                 * Redraws the tree after the expansion change.
                 * @returns {void}
                 */
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

            /**
             * Selects this anchor. selectAnchor ignores a row that is already selected.
             * @returns {void}
             */
            /**
             * Selects this anchor. A touch on a truncated label also shows its full text after the redraw.
             * @param {MouseEvent} event Click event. A keyboard click has detail 0.
             * @returns {void}
             */
            selectButton.addEventListener("click", function (event) {
                const fromKeyboard = event.detail === 0;
                const fromTouch = !fromKeyboard && lastPointerType === "touch";
                if (fromTouch) {
                    const textEl = selectButton.querySelector(".member-tree-node-text");
                    if (!labelIsTruncated(textEl)) {
                        touchRevealPath = null;
                    } else if (selectedPathKey === row.pathKey) {
                        touchRevealPath = null;
                        showRowTooltip(selectButton);
                    } else {
                        touchRevealPath = row.pathKey;
                    }
                } else {
                    touchRevealPath = null;
                }

                selectAnchor(row);
            });
            nodeRow.appendChild(selectButton);
        } else {
            const label = document.createElement("span");
            label.className = "member-system-node-label";
            label.appendChild(createNodeLabel(iconFor(row), labelText));
            /**
             * Shows the full header label when a touch taps a truncated row. Selection does not apply to headers.
             * @param {PointerEvent} event Pointer up event.
             * @returns {void}
             */
            label.addEventListener("pointerup", function (event) {
                if (event.pointerType !== "touch") {
                    return;
                }

                showRowTooltip(label);
            });
            nodeRow.appendChild(label);
        }

        tree.appendChild(nodeRow);

        if (hasChildren && isExpanded) {
            /**
             * Draws one expanded child below this row.
             * @param {object} child Child map row.
             * @returns {void}
             */
            children.forEach(function (child) {
                appendNode(child, level + 1);
            });
        }
    }

    /**
     * Updates visible member labels that use this member id, then redraws the tree without moving its scroll position.
     * A label that does not already start with "Member: " is left unchanged.
     * @param {string} memberId Member id. A blank id does nothing.
     * @param {string} displayName Name to show. A missing name leaves the label prefix with nothing after it.
     * @returns {void}
     */
    function reflectSharedMemberName(memberId, displayName) {
        const id = normalizeGuid(memberId);
        if (!id) {
            return;
        }

        const nextLabel = "Member: " + (displayName ? String(displayName) : "");
        let changed = false;
        /**
         * Replaces one matching member label.
         * @param {object} row Map row. Non-members and other ids are skipped.
         * @returns {void}
         */
        rows.forEach(function (row) {
            if (!row || row.objectType !== "member" || normalizeGuid(row.objectId) !== id) {
                return;
            }

            if (typeof row.nodeLabel !== "string" || row.nodeLabel.indexOf("Member: ") !== 0 || row.nodeLabel === nextLabel) {
                return;
            }

            row.nodeLabel = nextLabel;
            changed = true;
        });

        if (!changed || !tree) {
            return;
        }

        const top = tree.scrollTop;
        renderTree();
        tree.scrollTop = top;
    }

    /**
     * Redraws every top-level row and refreshes the Expand all button.
     * @returns {void}
     */
    function renderTree() {
        if (!tree) {
            return;
        }

        hideRowTooltip();
        tree.replaceChildren();
        /**
         * Draws a top-level row. Nested rows are drawn by their parent.
         * @param {object} row Map row.
         * @returns {void}
         */
        rows.forEach(function (row) {
            if (row.parentPathKey == null) {
                appendNode(row, 0);
            }
        });
        syncExpandAllButton();
        scheduleTruncationCheck();
    }

    /**
     * Selects an anchor after the shared leave guard, then loads its profiles.
     * A repeated call for the current path does nothing. The proceed callback does nothing if another selection changed the generation.
     * @param {object} row Anchor row. A non-selectable row does nothing.
     * @param {string|null} [profileId] Run id for the workspace open that is selecting its first anchor. A click omits it.
     * @param {object} [profileRequest] The workspace-open request whose tree profile receives the selection mark. A click omits it.
     * @returns {void}
     */
    function selectAnchor(row, profileId, profileRequest) {
        if (!isSelectable(row) || selectedPathKey === row.pathKey) {
            finishTreeProfile(profileRequest, "complete");
            return;
        }

        const generationAtRequest = selectionGeneration;
        /**
         * Applies the selection when no newer selection request has started.
         * @returns {void}
         */
        const proceed = function () {
            if (selectionGeneration !== generationAtRequest || !isSelectable(row)) {
                finishTreeProfile(profileRequest, "incomplete");
                return;
            }

            selectionGeneration += 1;
            selectedPathKey = row.pathKey;
            renderTree();
            markTreeProfile(profileRequest, "selectionDom");
            finishTreeProfile(profileRequest, "complete", true);
            revealTouchSelection(row.pathKey);
            if (workspaceTreeEnabled()) {
                loadWorkspaceMemberDetails(row, profileId, profileRequest);
                return;
            }

            loadAnchorProfiles(row);
        };
        const changes = window.PluralBridge && window.PluralBridge.memberChanges;
        if (changes && typeof changes.requestLeave === "function") {
            changes.requestLeave(proceed);
            return;
        }

        proceed();
    }

    /**
     * Asks the server to drop one context. A repeated or rejected release does not throw to the caller.
     * @param {string} id The context id to release.
     * @returns {Promise<void>} Resolves when the request finishes or fails.
     */
    function releaseContext(id) {
        if (!id) {
            return Promise.resolve();
        }

        return fetch(workspaceTreeEndpoint + "/" + encodeURIComponent(id), {
            method: "DELETE",
            credentials: "same-origin"
        }).then(function () {
            return undefined;
        }, function () {
            return undefined;
        });
    }

    /**
     * Shows an error in the detail pane without clearing the tree.
     * @param {string} text The error text.
     * @returns {void}
     */
    function showDetailError(text) {
        clearDetail();
        if (!detail) {
            return;
        }

        const status = document.createElement("p");
        status.className = "navigator-status navigator-status-error";
        status.textContent = text;
        detail.appendChild(status);
    }

    /**
     * Drops the current map, cancels the active request, and clears the tree and detail.
     * A held context id is released without blocking the clear.
     * @returns {void}
     */
    function clear() {
        selectionGeneration += 1;
        beginRequest();
        activeController = null;
        const previousContextId = contextId;
        contextId = null;
        rows = [];
        selectedPathKey = null;
        clearDetail();

        if (tree) {
            tree.replaceChildren();
        }

        hideRowTooltip();

        if (previousContextId) {
            releaseContext(previousContextId);
        }
    }

    /**
     * Loads the plural map into the supplied tree and detail containers.
     * A missing container cancels the previous request and does not fetch. Abort and stale responses are ignored. Any other failure clears the map and shows an error.
     * @param {object} containers tree and detail elements. A missing either property does not fetch.
     * @returns {void}
     */
    function mount(containers) {
        selectionGeneration += 1;
        tree = containers && containers.tree ? containers.tree : null;
        detail = containers && containers.detail ? containers.detail : null;
        bindMemberPaneScroll();
        const previousContextId = contextId;
        rows = [];
        selectedPathKey = null;
        contextId = null;
        clearDetail();
        if (previousContextId) {
            releaseContext(previousContextId);
        }

        startLoad(false);
        bindRowTooltips();
    }

    /**
     * Reloads the current path. A workspace replacement keeps the previous tree when the new load fails.
     * @returns {void}
     */
    function refresh() {
        startLoad(workspaceTreeEnabled() && rows.length > 0 && !!contextId);
    }

    /**
     * Clears this view and loads whichever path the developer flag now selects.
     * The previous path is not used to fill a failed load.
     * @returns {void}
     */
    function applyWorkspacePath() {
        const previousContextId = contextId;
        selectionGeneration += 1;
        contextId = null;
        rows = [];
        selectedPathKey = null;
        clearDetail();
        if (previousContextId) {
            releaseContext(previousContextId);
        }

        startLoad(false);
    }

    /**
     * Releases the context held by this view while the session is still authenticated.
     * @returns {Promise<void>} Resolves after the release attempt, including when it fails.
     */
    function releaseWorkspaceContext() {
        const previousContextId = contextId;
        contextId = null;
        return releaseContext(previousContextId);
    }

    /**
     * Records one first-batch avatar mark since the workspace-open origin.
     * Decode durations are byte conversion and blob-URL creation. They are not screen paint.
     * A request without that open's profile records nothing. A profiling error does not change the load.
     * @param {object} request The member request that still points at the workspace-open profile.
     * @param {string} name avatarRequestStart, avatarBodyReceived, avatarJsonParsed, or avatarApplyComplete.
     * @returns {void}
     */
    function markAvatarProfile(request, name) {
        try {
            if (!request || !request.treeProfile || typeof request.treeProfile.origin !== "number") {
                return;
            }

            if (!window.performance || typeof window.performance.now !== "function") {
                return;
            }

            if (!request.treeProfile.avatarBrowser) {
                request.treeProfile.avatarBrowser = {
                    elapsedMs: {},
                    images: [],
                    placeholders: 0,
                    pendingEdits: 0,
                    serverHeader: null,
                    phase: "incomplete"
                };
            }

            if (typeof request.treeProfile.avatarBrowser.elapsedMs[name] === "number") {
                return;
            }

            request.treeProfile.avatarBrowser.elapsedMs[name] = window.performance.now() - request.treeProfile.origin;
        } catch (error) {
            return;
        }
    }

    /**
     * Records one first-page member mark since the workspace-open origin.
     * memberCardsDom means paintWorkspaceMemberPage has returned. It does not mean the screen has painted.
     * A request without that open's profile records nothing, and a later page does not call this.
     * @param {object} request The member request that still points at the workspace-open profile.
     * @param {string} name memberRequestStart, memberBodyReceived, memberJsonParsed, or memberCardsDom.
     * @returns {void}
     */
    function markMemberProfile(request, name) {
        if (!request || !request.treeProfile || typeof request.treeProfile.origin !== "number") {
            return;
        }

        if (!window.performance || typeof window.performance.now !== "function") {
            return;
        }

        if (!request.treeProfile.memberBrowserElapsedMs) {
            request.treeProfile.memberBrowserElapsedMs = {};
        }

        if (typeof request.treeProfile.memberBrowserElapsedMs[name] === "number") {
            return;
        }

        request.treeProfile.memberBrowserElapsedMs[name] = window.performance.now() - request.treeProfile.origin;
    }

    /**
     * Records one tree mark as elapsed time since this run's origin.
     * domComplete and selectionDom mean renderTree has returned. They do not mean the screen has painted.
     * @param {object} request The workspace-open request. A request without a tree profile is ignored.
     * @param {string} name responseReceived, jsonParsed, domComplete, or selectionDom.
     * @returns {void}
     */
    function markTreeProfile(request, name) {
        if (!request || !request.treeProfile || request.treeProfile.finished) {
            return;
        }

        if (!window.performance || typeof window.performance.now !== "function") {
            return;
        }

        request.treeProfile.browserElapsedMs[name] = window.performance.now() - request.treeProfile.origin;
    }

    /**
     * Reads one profiling response header. The durations in it stay on the server clock.
     * @param {string|null} header The X-PB-Workspace-Profile value.
     * @returns {object|null} Outcome, serverMs, and member lines. Null when the header is missing or unreadable.
     */
    function readProfileHeader(header) {
        if (!header || typeof header !== "string") {
            return null;
        }

        try {
            var parsed = JSON.parse(header);
            if (!parsed || typeof parsed !== "object") {
                return null;
            }

            return {
                outcome: parsed.outcome,
                serverMs: parsed.serverMs || null,
                members: parsed.members || null
            };
        } catch (error) {
            return null;
        }
    }

    /**
     * Builds the one workspace-load report for a run. Browser elapsed times and server durations stay in separate fields.
     * A missing duration is omitted. It is not stored as zero. DOM and decode marks are not screen paint.
     * The returned object is a copy, so later changes to the run do not change a report already handed to the console.
     * @param {object|null} profile Marks and parsed headers for one run. A missing profile produces no report.
     * @returns {object|null} The report snapshot, or null when this run is not profiled.
     */
    function assembleWorkspaceLoadReport(profile) {
        if (!profile || !profile.runId) {
            return null;
        }

        /**
         * Copies one report value so the console snapshot does not keep the live run.
         * @param {*} value A report field.
         * @returns {*} The copied value.
         */
        function copyReportValue(value) {
            var copy;
            var index;
            var key;
            if (value === null || typeof value !== "object") {
                return value;
            }

            if (Object.prototype.toString.call(value) === "[object Array]") {
                copy = [];
                for (index = 0; index < value.length; index++) {
                    copy[index] = copyReportValue(value[index]);
                }

                return copy;
            }

            copy = {};
            for (key in value) {
                if (Object.prototype.hasOwnProperty.call(value, key)) {
                    copy[key] = copyReportValue(value[key]);
                }
            }

            return copy;
        }

        /**
         * Orders browser marks by elapsed time. Marks that were not taken are left out.
         * @param {Array<{name: string, elapsedMs: number}>} entries Marks that were present.
         * @returns {Array<{name: string, elapsedMs: number}>} The same array, earliest first.
         */
        function orderMarks(entries) {
            var index;
            var previous;
            var current;
            for (index = 1; index < entries.length; index++) {
                current = entries[index];
                previous = index - 1;
                while (previous >= 0 && entries[previous].elapsedMs > current.elapsedMs) {
                    entries[previous + 1] = entries[previous];
                    previous -= 1;
                }

                entries[previous + 1] = current;
            }

            return entries;
        }

        /**
         * Collects the named browser marks that have a numeric elapsed time.
         * @param {object|null} source Stored mark names and elapsed milliseconds.
         * @param {Array<{from: string, name: string}>} names Stored name and report name.
         * @param {string} request Tree, members, or avatars.
         * @returns {Array<{request: string, name: string, elapsedMs: number}>} Marks in elapsed order.
         */
        function browserMarks(source, names, request) {
            var entries = [];
            var index;
            for (index = 0; index < names.length; index++) {
                if (source && typeof source[names[index].from] === "number") {
                    entries.push({
                        request: request,
                        name: names[index].name,
                        elapsedMs: source[names[index].from]
                    });
                }
            }

            return orderMarks(entries);
        }

        /**
         * Copies server durations that were measured. Null header fields stay absent.
         * @param {object|null} serverMs Durations from one response header.
         * @param {string[]} names Duration names for that request.
         * @returns {object|null} The measured durations, or null when none were measured.
         */
        function serverDurations(serverMs, names) {
            var copy = {};
            var count = 0;
            var index;
            if (!serverMs) {
                return null;
            }

            for (index = 0; index < names.length; index++) {
                if (typeof serverMs[names[index]] === "number") {
                    copy[names[index]] = serverMs[names[index]];
                    count += 1;
                }
            }

            return count === 0 ? null : copy;
        }

        /**
         * Chooses the phase status. A browser failure, cancellation, or supersession is kept.
         * A completed browser phase still reports a failed or cancelled server outcome.
         * @param {string|null} browserStatus complete, failed, cancelled, or incomplete.
         * @param {object|null} header The parsed response header.
         * @returns {string|null} The phase status, or null when the phase did not run.
         */
        function phaseStatus(browserStatus, header) {
            var outcome = header ? header.outcome : null;
            if (browserStatus === "failed" || browserStatus === "cancelled" || browserStatus === "incomplete") {
                return browserStatus;
            }

            if (outcome === "cancelled") {
                return "cancelled";
            }

            if (outcome === "failed" || outcome === "unauthorized" || outcome === "unavailable" || outcome === "rejected") {
                return "failed";
            }

            if (browserStatus === "complete" || outcome === "completed") {
                return browserStatus === "complete" || outcome === "completed" ? "complete" : null;
            }

            return null;
        }

        var treeMarks = browserMarks(profile.browserElapsedMs, [
            { from: "responseReceived", name: "responseReceived" },
            { from: "jsonParsed", name: "jsonParsed" },
            { from: "domComplete", name: "domComplete" },
            { from: "selectionDom", name: "selectionDom" }
        ], "tree");
        var memberMarks = browserMarks(profile.memberBrowserElapsedMs, [
            { from: "memberRequestStart", name: "requestStart" },
            { from: "memberBodyReceived", name: "bodyReceived" },
            { from: "memberJsonParsed", name: "jsonParsed" },
            { from: "memberCardsDom", name: "cardsDomComplete" }
        ], "members");
        var avatarSource = profile.avatarBrowser ? profile.avatarBrowser.elapsedMs : null;
        var avatarMarks = browserMarks(avatarSource, [
            { from: "avatarRequestStart", name: "requestStart" },
            { from: "avatarBodyReceived", name: "bodyReceived" },
            { from: "avatarJsonParsed", name: "jsonParsed" },
            { from: "avatarApplyComplete", name: "scriptComplete" },
            { from: "displayComplete", name: "displayComplete" }
        ], "avatars");
        var milestones = orderMarks(treeMarks.concat(memberMarks).concat(avatarMarks));
        var report = {
            label: "workspace load",
            runId: profile.runId,
            browserMilestones: milestones
        };
        var treeStatus = phaseStatus(profile.treeStatus, profile.treeHeader);
        var memberStatus = phaseStatus(profile.membersStatus, profile.memberHeader);
        var avatarStatus = phaseStatus(profile.avatarStatus, profile.avatarHeader);
        var treeServer = serverDurations(profile.treeHeader && profile.treeHeader.serverMs, [
            "actionTotal",
            "connectionOpen",
            "accountResolution",
            "openingBuild",
            "createContext",
            "publish"
        ]);
        var memberServer = serverDurations(profile.memberHeader && profile.memberHeader.serverMs, [
            "actionTotal",
            "resolveConnectionOpen",
            "accountResolution",
            "prepareOccurrence",
            "profileConnectionOpen",
            "profileAuthorization",
            "responsePreparation"
        ]);
        // The image connection and graph stages belong to the one miss read. They are not repeated on each member.
        var avatarServer = serverDurations(profile.avatarHeader && profile.avatarHeader.serverMs, [
            "actionTotal",
            "resolveConnectionOpen",
            "accountResolution",
            "profileConnectionOpen",
            "profileAuthorization",
            "imageLoop",
            "imageConnectionOpen",
            "graphTables",
            "topologySeed",
            "topologyWalker",
            "graphExpansion",
            "avatarQuery",
            "base64",
            "responsePreparation"
        ]);

        if (treeStatus || treeMarks.length > 0 || treeServer) {
            report.tree = { status: treeStatus || "incomplete" };
            if (treeMarks.length > 0) {
                report.tree.browserElapsedMs = {};
                putElapsed(report.tree.browserElapsedMs, treeMarks);
            }

            if (treeServer) {
                report.tree.serverMs = treeServer;
            }
        }

        if (memberStatus || memberMarks.length > 0 || memberServer || typeof profile.cardCount === "number") {
            report.members = { status: memberStatus || "incomplete" };
            if (memberMarks.length > 0) {
                report.members.browserElapsedMs = {};
                putElapsed(report.members.browserElapsedMs, memberMarks);
            }

            if (memberServer) {
                report.members.serverMs = memberServer;
            }

            if (typeof profile.cardCount === "number") {
                report.members.cardCount = profile.cardCount;
            }
        }

        var avatarLines = avatarMemberLines(profile);
        if (avatarStatus || avatarMarks.length > 0 || avatarServer || avatarLines) {
            report.avatars = { status: avatarStatus || "incomplete" };
            if (avatarMarks.length > 0) {
                report.avatars.browserElapsedMs = {};
                putElapsed(report.avatars.browserElapsedMs, avatarMarks);
            }

            if (avatarServer) {
                report.avatars.serverMs = avatarServer;
            }

            if (avatarLines) {
                if (typeof avatarLines.retentionHits === "number") {
                    report.avatars.retentionHits = avatarLines.retentionHits;
                    report.avatars.retentionMisses = avatarLines.retentionMisses;
                    report.avatars.nullImages = avatarLines.nullImages;
                }

                report.avatars.members = avatarLines.members;
            }
        }

        return copyReportValue(report);

        /**
         * Stores one request's browser marks. The names are the report names, already in elapsed order.
         * @param {object} target The browserElapsedMs object being filled.
         * @param {Array<{name: string, elapsedMs: number}>} marks Marks for one request.
         * @returns {void}
         */
        function putElapsed(target, marks) {
            var index;
            for (index = 0; index < marks.length; index++) {
                target[marks[index].name] = marks[index].elapsedMs;
            }
        }

        /**
         * Builds per-image lines from the avatar header and the browser image notes.
         * Counts are present only when the server returned member lines. Image bytes are not copied.
         * @param {object} source The run profile.
         * @returns {object|null} Counts and lines, or null when neither side recorded an image.
         */
        function avatarMemberLines(source) {
            var headerMembers = source.avatarHeader && source.avatarHeader.members;
            var browserImages = source.avatarBrowser && source.avatarBrowser.images;
            var browserById = {};
            var lines = [];
            var index;
            var seen = {};
            var hits = 0;
            var misses = 0;
            var nullImages = 0;
            var counted = false;
            if (!headerMembers && (!browserImages || !browserImages.length)) {
                return null;
            }

            if (browserImages) {
                for (index = 0; index < browserImages.length; index++) {
                    if (browserImages[index] && browserImages[index].memberId) {
                        browserById[browserImages[index].memberId] = browserImages[index];
                    }
                }
            }

            if (headerMembers) {
                counted = true;
                for (index = 0; index < headerMembers.length; index++) {
                    if (!headerMembers[index] || !headerMembers[index].memberId) {
                        continue;
                    }

                    if (headerMembers[index].retention === "hit") {
                        hits += 1;
                    }

                    if (headerMembers[index].retention === "miss") {
                        misses += 1;
                    }

                    if (headerMembers[index].image === "null") {
                        nullImages += 1;
                    }

                    lines.push(oneAvatarLine(headerMembers[index], browserById[headerMembers[index].memberId]));
                    seen[headerMembers[index].memberId] = true;
                }
            }

            if (browserImages) {
                for (index = 0; index < browserImages.length; index++) {
                    if (!browserImages[index] || !browserImages[index].memberId || seen[browserImages[index].memberId]) {
                        continue;
                    }

                    lines.push(oneAvatarLine(null, browserImages[index]));
                }
            }

            return {
                retentionHits: counted ? hits : null,
                retentionMisses: counted ? misses : null,
                nullImages: counted ? nullImages : null,
                members: lines
            };
        }

        /**
         * Copies one image line. Only the id, retention, outcome, timings, and byte length are kept.
         * @param {object|null} serverLine The header line. Null when the browser alone recorded the image.
         * @param {object|null} browserLine The decode note. Null when the image was not applied in this page.
         * @returns {object} The report line.
         */
        function oneAvatarLine(serverLine, browserLine) {
            var line = {};
            var serverNames = [
                ["connectionOpen", "connectionOpenMs"],
                ["graphTables", "graphTablesMs"],
                ["topologySeed", "topologySeedMs"],
                ["topologyWalker", "topologyWalkerMs"],
                ["graphExpansion", "graphExpansionMs"],
                ["avatarQuery", "avatarQueryMs"],
                ["byteLength", "byteLength"]
            ];
            var index;
            if (serverLine && serverLine.memberId) {
                line.memberId = serverLine.memberId;
            } else if (browserLine && browserLine.memberId) {
                line.memberId = browserLine.memberId;
            }

            if (browserLine && browserLine.settle === "notApplied") {
                line.image = "notApplied";
            } else if (serverLine && (serverLine.image === "bytes" || serverLine.image === "null")) {
                line.image = serverLine.image;
            }

            if (serverLine && (serverLine.retention === "hit" || serverLine.retention === "miss")) {
                line.retention = serverLine.retention;
            }

            if (serverLine) {
                for (index = 0; index < serverNames.length; index++) {
                    if (typeof serverLine[serverNames[index][0]] === "number") {
                        line[serverNames[index][1]] = serverLine[serverNames[index][0]];
                    }
                }
            }

            if (browserLine && typeof browserLine.decodeMs === "number") {
                line.browserDecodeMs = browserLine.decodeMs;
            }

            if (browserLine && typeof browserLine.settle === "string" && browserLine.settle) {
                line.displaySettled = browserLine.settle;
            }

            return line;
        }
    }

    /**
     * Logs the workspace-load snapshot once. A run without a profile, including a disabled open, logs nothing.
     * @param {object} request The workspace-open request.
     * @returns {void}
     */
    function publishWorkspaceLoadReport(request) {
        try {
            if (!request || !request.treeProfile || request.treeProfile.reported) {
                return;
            }

            request.treeProfile.reported = true;
            request.treeProfile.treeHeader = readProfileHeader(request.treeProfile.serverHeader);
            request.treeProfile.memberHeader = readProfileHeader(request.treeProfile.memberServerHeader);
            request.treeProfile.avatarHeader = readProfileHeader(
                request.treeProfile.avatarBrowser ? request.treeProfile.avatarBrowser.serverHeader : null
            );
            var report = assembleWorkspaceLoadReport(request.treeProfile);
            if (report && window.console && typeof window.console.info === "function") {
                window.console.info(report);
            }
        } catch (error) {
            return;
        }
    }

    /**
     * Records a terminal member or avatar status and logs the run. A second call does not log again.
     * @param {object} request The request that carries the workspace-open profile.
     * @param {string} phase members or avatars.
     * @param {string} status complete, failed, cancelled, or incomplete.
     * @returns {void}
     */
    function endOpenedProfile(request, phase, status) {
        try {
            if (!request || !request.treeProfile || request.treeProfile.reported) {
                return;
            }

            if (phase === "members" && !request.treeProfile.membersStatus) {
                request.treeProfile.membersStatus = status;
            }

            if (phase === "avatars" && !request.treeProfile.avatarStatus) {
                request.treeProfile.avatarStatus = status;
            }

            publishWorkspaceLoadReport(request);
        } catch (error) {
            return;
        }
    }

    /**
     * Records the tree outcome. The console report waits until members and avatars finish, fail, or are superseded.
     * @param {object} request The workspace-open request. A request without a tree profile writes nothing.
     * @param {string} outcome complete, failed, cancelled, or incomplete.
     * @param {boolean} [openContinues] True when the first member page still follows this tree outcome.
     * @returns {void}
     */
    function finishTreeProfile(request, outcome, openContinues) {
        if (!request || !request.treeProfile || request.treeProfile.finished) {
            return;
        }

        request.treeProfile.finished = true;
        request.treeProfile.treeStatus = outcome;
        if (openContinues === true) {
            return;
        }

        publishWorkspaceLoadReport(request);
    }

    /**
     * Starts one tree load. The captured mode and generation decide whether a response may paint.
     * @param {boolean} replacing True when a failed workspace load must keep the current tree and context.
     * @returns {void}
     */
    function startLoad(replacing) {
        if (!tree || !detail) {
            beginRequest();
            activeController = null;
            return;
        }

        const request = beginRequest();
        request.workspaceMode = workspaceTreeEnabled();
        request.replacing = replacing === true;
        request.profileId = request.workspaceMode && workspaceLoadProfileEnabled()
            ? newWorkspaceProfileId()
            : null;
        if (!request.replacing) {
            renderStatus(
                request.workspaceMode ? "Loading workspace tree." : "Loading plural map.",
                false,
                request.workspaceMode === true
            );
        }

        const headers = {
            Accept: "application/json"
        };
        if (request.profileId) {
            headers["X-PB-Workspace-Profile-Id"] = request.profileId;
        }

        // The origin is this run's clock. Later marks subtract it. They are not compared with the server clock.
        if (request.profileId && window.performance && typeof window.performance.now === "function") {
            request.treeProfile = {
                runId: request.profileId,
                origin: window.performance.now(),
                browserElapsedMs: {},
                serverHeader: null,
                finished: false
            };
        }

        fetch(request.workspaceMode ? workspaceTreeEndpoint : pluralMapEndpoint, {
            headers: headers,
            credentials: "same-origin",
            signal: request.signal
        })
            /**
             * Rejects a non-OK response and otherwise parses its JSON.
             * @param {Response} response Fetch response.
             * @returns {Promise<object>} The parsed body.
             * @throws {Error} When the status is not OK.
             */
            .then(function (response) {
                markTreeProfile(request, "responseReceived");
                if (request.treeProfile && response.headers && typeof response.headers.get === "function") {
                    request.treeProfile.serverHeader = response.headers.get("X-PB-Workspace-Profile");
                }

                if (!response.ok) {
                    throw new Error("The tree could not be loaded.");
                }

                return response.json();
            })
            /**
             * Stores the hierarchy when this load is still the visible mode and generation.
             * Workspace mode requires a context id. The following anchor selection loads direct member cards. A failed check falls through to the catch.
             * @param {object} payload Map or workspace-tree response.
             * @returns {void}
             */
            .then(function (payload) {
                markTreeProfile(request, "jsonParsed");
                if (!loadIsCurrent(request)) {
                    finishTreeProfile(request, "incomplete");
                    return;
                }

                if (!payload || !Array.isArray(payload.hierarchyPreview)) {
                    throw new Error("The tree could not be loaded.");
                }

                if (request.workspaceMode && !payload.contextId) {
                    throw new Error("The tree could not be loaded.");
                }

                const previousContextId = contextId;
                rows = payload.hierarchyPreview;
                contextId = request.workspaceMode ? String(payload.contextId) : null;
                if (previousContextId && previousContextId !== contextId) {
                    releaseContext(previousContextId);
                }

                initializeExpansionForAccount(mapAccountId(payload), rows);
                if (rows.length === 0) {
                    selectedPathKey = null;
                    clearDetail();
                    renderStatus("No plural map rows were returned.");
                    syncExpandAllButton();
                    finishTreeProfile(request, "complete");
                    return;
                }

                renderTree();
                markTreeProfile(request, "domComplete");
                const anchor = firstTopLevelAnchor(rows);
                if (anchor) {
                    selectAnchor(anchor, request.profileId, request);
                    return;
                }

                finishTreeProfile(request, "complete");
            })
            /**
             * Ignores an abort or a stale load. A replacement failure keeps the previous tree and context.
             * An aborted or stale load does not clear the tree, so a newer workspace load keeps its progress bar.
             * @param {*} error Rejection from the tree request.
             * @returns {void}
             */
            .catch(function (error) {
                if (error && error.name === "AbortError") {
                    finishTreeProfile(request, "cancelled");
                    return;
                }

                if (!loadIsCurrent(request)) {
                    finishTreeProfile(request, "incomplete");
                    return;
                }

                finishTreeProfile(request, "failed");

                if (request.replacing) {
                    showDetailError("The workspace tree could not be replaced. The previous tree is still shown.");
                    return;
                }

                rows = [];
                selectedPathKey = null;
                contextId = null;
                clearDetail();
                renderStatus(request.workspaceMode ? "The workspace tree could not be loaded." : "The plural map could not be loaded.", true);
            });
    }

    /**
     * Reports whether a tree response still belongs to the visible generation and path mode.
     * @param {{generation: number, workspaceMode: boolean}} request The load that produced the response.
     * @returns {boolean} True when both the generation and the developer flag still match.
     */
    function loadIsCurrent(request) {
        return request.generation === requestGeneration && request.workspaceMode === workspaceTreeEnabled();
    }

    return {
        mount: mount,
        clear: clear,
        refresh: refresh,
        applyWorkspacePath: applyWorkspacePath,
        releaseWorkspaceContext: releaseWorkspaceContext,
        getAccountId: getAccountId,
        getMapSnapshot: getMapSnapshot,
        toggleAllExpansion: toggleAllExpansion,
        holdExpansion: holdExpansion,
        restoreHeldExpansion: restoreHeldExpansion
    };
}());
