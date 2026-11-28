/**
 * Browser calls for the legacy current-system member routes and the signed-in account's anchor member routes.
 * Depends on window.PluralBridge and fetch. Save failures reject with the server's error text when the body can be read. This file does not track card drafts.
 */
window.PluralBridge = window.PluralBridge || {};

/**
 * Builds the shared API client and returns the same object if one was already installed.
 * @returns {object} The client, with the route functions assigned.
 */
window.PluralBridge.apiClient = (function () {
    const client = window.PluralBridge.apiClient || {};

    let currentSystemId = null;

    /**
     * Returns the endpoint unchanged. Callers pass a path that already includes its query.
     * @param {string} endpoint Path beginning with /api.
     * @returns {string} The same endpoint.
     */
    function apiUrl(endpoint) {
        return endpoint;
    }

    /**
     * Sends one same-origin JSON request.
     * A non-OK status rejects. A non-empty body is parsed as JSON, so invalid JSON also rejects. An empty body fulfills with null.
     * @param {string} endpoint Path passed through apiUrl.
     * @param {object} [options] method, headers, body, and signal. Omitted options use GET and no body. A signal abort rejects the fetch.
     * @returns {Promise<object|null>} The parsed JSON body when the response is OK.
     * @throws {Error} When the status is not OK. The message includes the status and does not read payload.error.
     * @throws {SyntaxError} When a non-empty body is not JSON.
     */
    async function fetchJson(endpoint, options) {
        const requestOptions = options || {};
        const headers = requestOptions.headers || {};

        const response = await fetch(apiUrl(endpoint), {
            method: requestOptions.method || "GET",
            credentials: "same-origin",
            headers: Object.assign({
                Accept: "application/json"
            }, headers),
            body: requestOptions.body,
            signal: requestOptions.signal
        });

        if (typeof requestOptions.onResponse === "function") {
            requestOptions.onResponse(response);
        }

        const responseText = await response.text();
        if (typeof requestOptions.onBody === "function") {
            requestOptions.onBody();
        }

        let payload = null;

        if (responseText) {
            payload = JSON.parse(responseText);
        }

        if (typeof requestOptions.onParsed === "function") {
            requestOptions.onParsed();
        }

        if (!response.ok) {
            throw new Error("API request failed with HTTP " + response.status);
        }

        return payload;
    }

    /**
     * Returns the cached current-system id, or reads it once from the legacy proof endpoint.
     * The first id found on currentSystem is kept. proofSystem is used only when currentSystem has no id. Later calls do not read /api/me again.
     * @returns {Promise<string>} The system id.
     * @throws {Error} When neither source has a system id, or when the proof request fails.
     */
    async function getCurrentSystemId() {
        if (currentSystemId) {
            return currentSystemId;
        }

        const payload = await fetchJson("/api/me");

        if (payload && payload.currentSystem && payload.currentSystem.systemId) {
            currentSystemId = payload.currentSystem.systemId;
            return currentSystemId;
        }

        if (payload && payload.proofSystem && payload.proofSystem.systemId) {
            currentSystemId = payload.proofSystem.systemId;
            return currentSystemId;
        }

        throw new Error("No current system was returned by /api/me.");
    }

    /**
     * Reads members for the cached current system. This is the legacy list, not the selected anchor.
     * @returns {Promise<object|null>} The members payload from the current-system route.
     * @throws {Error} When no current system can be resolved or the request fails.
     */
    async function readMembers() {
        const systemId = await getCurrentSystemId();

        return await fetchJson("/api/systems/" + encodeURIComponent(systemId) + "/members");
    }

    /**
     * Replaces the cached current-system id used by the legacy member routes.
     * @param {string} systemId The id to cache. A falsy value clears the cache.
     * @returns {void}
     */
    function setCurrentSystemId(systemId) {
        currentSystemId = systemId || null;
    }

    /**
     * Creates a member through the legacy current-system route.
     * @param {object} [memberRequest] JSON body. Null or omitted is sent as an empty object.
     * @returns {Promise<object|null>} The create response.
     * @throws {Error} When no current system can be resolved or the request fails.
     */
    async function addMember(memberRequest) {
        const systemId = await getCurrentSystemId();

        return await fetchJson("/api/systems/" + encodeURIComponent(systemId) + "/members", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(memberRequest || {})
        });
    }

    /**
     * Reads member profiles for one named anchor on the signed-in account.
     * objectId asks for placement, fronting, and avatar fields. branch is the selected occurrence chain. signal drops a load that a newer selection has already replaced.
     * A missing objectId or branch is omitted from the query. The server still receives a branch on its own default when this client sends one.
     * @param {string} objectType Anchor type sent as the objectType query.
     * @param {string} anchorName Anchor display name.
     * @param {AbortSignal} [signal] Abort signal for this load. Abort rejects the returned promise.
     * @param {string} [objectId] Selected anchor id. Falsy values are not sent.
     * @param {string} [branch] Selected occurrence chain. Falsy values are not sent.
     * @returns {Promise<object|Array|null>} The profile response. The caller checks that a successful overlay is an array.
     * @throws {Error} When the HTTP status is not OK.
     */
    async function readAnchorMemberProfiles(objectType, anchorName, signal, objectId, branch) {
        const query = new URLSearchParams();
        query.set("objectType", objectType);
        query.set("anchorName", anchorName);
        if (objectId) {
            query.set("objectId", objectId);
        }
        if (branch) {
            query.set("branch", branch);
        }

        return await fetchJson("/api/me/anchor-member-profiles?" + query.toString(), {
            signal: signal
        });
    }

    /**
     * Reads direct member cards for one anchor occurrence in a retained workspace context.
     * The server derives the anchor and branch. This request does not send an avatar route and does not include one in the response.
     * @param {string} contextId Opaque context id from the workspace-tree response.
     * @param {string} occurrence Selected anchor path.
     * @param {AbortSignal} [signal] Abort signal for this load. Abort rejects the returned promise.
     * @param {number} [skip] Unique direct members to pass over. Omitted or zero requests the first window.
     * @param {string|null} [profileId] Run id for the workspace open's first window. A later window does not send it.
     * @param {{onResponse: function, bodyReceived: function, jsonParsed: function}|null} [profileMarks] First-page timing hooks. A later window ignores them.
     * @returns {Promise<Array|object|null>} The card array. The caller checks that a successful body is an array.
     * @throws {Error} When the HTTP status is not OK.
     */
    async function readWorkspaceMemberDetails(contextId, occurrence, signal, skip, profileId, profileMarks) {
        const query = new URLSearchParams();
        query.set("occurrence", occurrence || "");
        const memberSkip = skip > 0 ? skip : 0;
        query.set("skip", String(memberSkip));
        const headers = {};
        const marks = memberSkip === 0 ? profileMarks : null;
        if (memberSkip === 0 && profileId) {
            headers["X-PB-Workspace-Profile-Id"] = profileId;
        }

        return await fetchJson(
            "/api/me/workspace-tree/" + encodeURIComponent(contextId) + "/member-details?" + query.toString(),
            {
                signal: signal,
                headers: headers,
                onResponse: marks && marks.onResponse,
                onBody: marks && marks.bodyReceived,
                onParsed: marks && marks.jsonParsed
            }
        );
    }

    /**
     * Reads one page of avatar bytes for the same occurrence and skip as the member cards.
     * The server authorizes that page before it returns images. A null image leaves the card placeholder.
     * @param {string} contextId Opaque context id from the workspace-tree response.
     * @param {string} occurrence Selected anchor path.
     * @param {AbortSignal} [signal] Abort signal for this load. Abort rejects the returned promise.
     * @param {string|null} [profileId] Run id captured for the workspace open. Omitted sends no profiling header. A later page omits it.
     * @param {{onResponse: function, bodyReceived: function, jsonParsed: function}|null} [profileMarks] First-batch timing hooks. Omitted sends the request without them.
     * @param {number} [skip] Unique direct members to pass over. Omitted or zero requests the first window.
     * @returns {Promise<Array|object|null>} The image array. The caller checks that a successful body is an array.
     * @throws {Error} When the HTTP status is not OK.
     */
    async function readWorkspaceMemberAvatars(contextId, occurrence, signal, profileId, profileMarks, skip) {
        const query = new URLSearchParams();
        query.set("occurrence", occurrence || "");
        const avatarSkip = skip > 0 ? skip : 0;
        query.set("skip", String(avatarSkip));
        const headers = {};
        if (profileId) {
            headers["X-PB-Workspace-Profile-Id"] = profileId;
        }

        return await fetchJson(
            "/api/me/workspace-tree/" + encodeURIComponent(contextId) + "/member-avatars?" + query.toString(),
            {
                signal: signal,
                headers: headers,
                onResponse: profileMarks && profileMarks.onResponse,
                onBody: profileMarks && profileMarks.bodyReceived,
                onParsed: profileMarks && profileMarks.jsonParsed
            }
        );
    }

    /**
     * Puts one anchor profile save. Failure rejects. It is not converted into a saved:false object.
     * An unreadable error body still rejects, using the generic message. A readable payload.error is preserved, including an Owner or branch failure.
     * @param {string} memberId Member id in the route. An empty id rejects before the request.
     * @param {object} [memberRequest] Save body. Null or omitted is sent as an empty object. The caller decides which fields are present.
     * @returns {Promise<object|null>} The parsed success body.
     * @throws {Error} When memberId is missing or the status is not OK. error.status is the HTTP status when the response was received.
     */
    async function saveAnchorMemberProfile(memberId, memberRequest) {
        if (!memberId) {
            throw new Error("Member ID is required for member edit.");
        }

        const response = await fetch(apiUrl("/api/me/anchor-member-profiles/" + encodeURIComponent(memberId)), {
            method: "PUT",
            credentials: "same-origin",
            headers: {
                Accept: "application/json",
                "Content-Type": "application/json"
            },
            body: JSON.stringify(memberRequest || {})
        });

        const responseText = await response.text();
        let payload = null;

        if (responseText) {
            try {
                payload = JSON.parse(responseText);
            } catch (parseError) {
                payload = null;
            }
        }

        if (!response.ok) {
            // Keep the server's authorization or validation text. A generic
            // message would hide a missing Owner grant.
            const message = payload && payload.error
                ? String(payload.error)
                : "Member could not be saved.";
            const error = new Error(message);
            error.status = response.status;
            throw error;
        }

        return payload;
    }

    /**
     * Puts an immediate Fronting change on its own route. It is not part of the profile Save body.
     * Failure rejects with payload.error when that text is present. The card turns that rejection into a saved:false outcome and keeps the marker.
     * @param {string} memberId Member id in the route. An empty id rejects before the request.
     * @param {object} [frontingRequest] Fronting body, including the selected anchor, branch, and boolean state. Null or omitted is sent as an empty object.
     * @returns {Promise<object|null>} The parsed success body. The caller requires a boolean fronting field.
     * @throws {Error} When memberId is missing or the status is not OK. error.status is the HTTP status when the response was received.
     */
    async function saveMemberFronting(memberId, frontingRequest) {
        if (!memberId) {
            throw new Error("Member ID is required for fronting.");
        }

        const response = await fetch(apiUrl("/api/me/members/" + encodeURIComponent(memberId) + "/fronting"), {
            method: "PUT",
            credentials: "same-origin",
            headers: {
                Accept: "application/json",
                "Content-Type": "application/json"
            },
            body: JSON.stringify(frontingRequest || {})
        });
        const responseText = await response.text();
        let payload = null;
        if (responseText) {
            try {
                payload = JSON.parse(responseText);
            } catch (parseError) {
                payload = null;
            }
        }

        if (!response.ok) {
            const message = payload && payload.error
                ? String(payload.error)
                : "Fronting could not be saved.";
            const error = new Error(message);
            error.status = response.status;
            throw error;
        }

        return payload;
    }

    /**
     * Updates a member through the legacy current-system route.
     * This path is separate from anchor profile Save. The caller supplies the body. This function does not add a branch.
     * @param {string} memberId Member id in the route. An empty id rejects before the request.
     * @param {object} [memberRequest] JSON body. Null or omitted is sent as an empty object.
     * @returns {Promise<object|null>} The edit response.
     * @throws {Error} When no current system can be resolved, memberId is missing, or the request fails.
     */
    async function editMember(memberId, memberRequest) {
        const systemId = await getCurrentSystemId();

        if (!memberId) {
            throw new Error("Member ID is required for member edit.");
        }

        return await fetchJson("/api/systems/" + encodeURIComponent(systemId) + "/members/" + encodeURIComponent(memberId), {
            method: "PUT",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(memberRequest || {})
        });
    }

    client.fetchJson = fetchJson;
    client.getCurrentSystemId = getCurrentSystemId;
    client.setCurrentSystemId = setCurrentSystemId;
    client.readMembers = readMembers;
    client.readAnchorMemberProfiles = readAnchorMemberProfiles;
    client.readWorkspaceMemberDetails = readWorkspaceMemberDetails;
    client.readWorkspaceMemberAvatars = readWorkspaceMemberAvatars;
    client.addMember = addMember;
    client.saveAnchorMemberProfile = saveAnchorMemberProfile;
    client.saveMemberFronting = saveMemberFronting;
    client.editMember = editMember;

    return client;
})();