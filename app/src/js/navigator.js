/**
 * Plural-map navigator. Loads /api/me/plural-map, draws the occurrence tree, and loads anchor member profiles for the selected anchor.
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
    function mapAccountId(payload) {
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
     * @param {string} text Status text.
     * @param {boolean} isError True uses the error status class.
     * @returns {void}
     */
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
     * Builds the detail heading from the row label and object type.
     * @param {object} row Selected row. A missing label or type is left blank.
     * @returns {HTMLElement} The section.
     */
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

    /**
     * Replaces the detail pane with the row heading and one status line.
     * @param {object} row Selected row.
     * @param {string} text Status text.
     * @param {boolean} isError True uses the error class.
     * @returns {void}
     */
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
     * @returns {object} The member passed to the card.
     */
    function profileForCard(profile, memberId, accountId) {
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
            avatarObjectUrl: hasPreviewField("avatarObjectUrl")
                ? preview.avatarObjectUrl
                : (profile && profile.avatarUrl ? profile.avatarUrl : null),
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
     * Draws member cards for member children of the selected anchor and reports profiles that do not match those children.
     * Each card receives the selected anchor and branch for both profile Save and immediate Fronting.
     * @param {object} row Selected anchor.
     * @param {Array<object>} profiles Profile rows. Rows without a member id are reported as extra.
     * @returns {void}
     * @throws {Error} When a matched member cannot find createMemberAccordion.
     */
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
        const renderedAccountId = getAccountId();
        /**
         * Keeps member children of the selected anchor.
         * @param {object} child Map child.
         * @returns {boolean} True when the child type is member.
         */
        const memberChildren = children.filter(function (child) {
            return child.objectType === "member";
        });

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

            if (typeof createCard !== "function") {
                throw new Error(
                    "Cannot create member cards: createMemberAccordion is not accessible from the navigator."
                );
            }

            matched[id] = true;
            list.appendChild(createCard(profileForCard(profile, id, renderedAccountId), {
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
            }));
        });

        /**
         * Keeps a profile id that was not matched to a displayed member child.
         * @param {string} id Normalized profile member id.
         * @returns {boolean} True when this id has no card.
         */
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

        apiClient.readAnchorMemberProfiles(row.objectType, anchorName, request.signal, row.objectId, selectedBranch(row))
            /**
             * Paints the profiles when this response is still current. A non-array body is a load failure.
             * @param {*} profiles Response body.
             * @returns {void}
             */
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
            /**
             * Ignores an abort. A later failure is shown only when this request is still the visible selection.
             * @param {*} error Rejection from the profile request. AbortError is ignored.
             * @returns {void}
             */
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
    }

    /**
     * Selects an anchor after the shared leave guard, then loads its profiles.
     * A repeated call for the current path does nothing. The proceed callback does nothing if another selection changed the generation.
     * @param {object} row Anchor row. A non-selectable row does nothing.
     * @returns {void}
     */
    function selectAnchor(row) {
        if (!isSelectable(row) || selectedPathKey === row.pathKey) {
            return;
        }

        const generationAtRequest = selectionGeneration;
        /**
         * Applies the selection when no newer selection request has started.
         * @returns {void}
         */
        const proceed = function () {
            if (selectionGeneration !== generationAtRequest || !isSelectable(row)) {
                return;
            }

            selectionGeneration += 1;
            selectedPathKey = row.pathKey;
            renderTree();
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
     * Drops the current map, cancels the active request, and clears the tree and detail.
     * @returns {void}
     */
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
            /**
             * Rejects a non-OK map response and otherwise parses its JSON.
             * @param {Response} response Fetch response.
             * @returns {Promise<object>} The parsed body.
             * @throws {Error} When the status is not OK. Invalid JSON rejects through response.json.
             */
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("The plural map could not be loaded.");
                }

                return response.json();
            })
            /**
             * Stores the hierarchy when this load is still current, expands it, and selects the first top-level anchor.
             * An empty hierarchy shows a status and does not select an anchor. A missing hierarchyPreview rejects into the following catch.
             * @param {object} payload Map response.
             * @returns {void}
             * @throws {Error} When the payload has no hierarchyPreview array.
             */
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
            /**
             * Ignores an abort or a stale map load. Any other failure clears the rows and shows the load error.
             * @param {*} error Rejection from the map request.
             * @returns {void}
             */
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
