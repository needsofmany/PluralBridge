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
            body: requestOptions.body
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
    client.readMembers = readMembers;
    client.addMember = addMember;
    client.editMember = editMember;

    return client;
})();