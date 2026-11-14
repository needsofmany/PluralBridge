window.PluralBridge = window.PluralBridge || {};

window.PluralBridge.apiClient = (function () {
    const client = window.PluralBridge.apiClient || {};

    let currentSystemId = null;

    function apiUrl(endpoint) {
        return endpoint;
    }

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

        const responseText = await response.text();
        let payload = null;

        if (responseText) {
            payload = JSON.parse(responseText);
        }

        if (!response.ok) {
            throw new Error("API request failed with HTTP " + response.status);
        }

        return payload;
    }

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

    async function readMembers() {
        const systemId = await getCurrentSystemId();

        return await fetchJson("/api/systems/" + encodeURIComponent(systemId) + "/members");
    }

    function setCurrentSystemId(systemId) {
        currentSystemId = systemId || null;
    }

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
            const message = payload && payload.error
                ? String(payload.error)
                : "Member could not be saved.";
            const error = new Error(message);
            error.status = response.status;
            throw error;
        }

        return payload;
    }

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
    client.addMember = addMember;
    client.saveAnchorMemberProfile = saveAnchorMemberProfile;
    client.saveMemberFronting = saveMemberFronting;
    client.editMember = editMember;

    return client;
})();