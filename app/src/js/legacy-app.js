const apiBaseUrl = "";

let selectedSystemId = null;
const membersUiStorageKeyPrefix = "pb.membersUiState.";
let membersUiStorageKey = null;
let membersUiStorageLoaded = false;
let membersUiStorageLoading = false;

const output = document.getElementById("appOutput");
const contractButtons = document.querySelectorAll("[data-contract]");
const sessionButtons = document.querySelectorAll("[data-session-action]");
const loginForm = document.getElementById("loginForm");
const sessionStatus = document.getElementById("sessionStatus");

const endpointBuilders = {
  me: async function() {
    return "/api/me";
  },
  sourceSystems: async function() {
    return "/api/source-systems";
  },
  importBatches: async function() {
    return "/api/import-batches";
  },
  systems: async function() {
    return "/api/systems";
  },
  members: async function() {
    return "/api/systems/" + (await getProofSystemId()) + "/members";
  },
  privacyBuckets: async function() {
    return "/api/systems/" + (await getProofSystemId()) + "/privacy-buckets";
  },
  customFields: async function() {
    return "/api/systems/" + (await getProofSystemId()) + "/custom-fields";
  },
  frontHistory: async function() {
    return "/api/systems/" + (await getProofSystemId()) + "/front-history";
  },
  sourceRecords: async function() {
    return "/api/systems/" + (await getProofSystemId()) + "/source-records";
  },
  sourceIdMappings: async function() {
    return "/api/systems/" + (await getProofSystemId()) + "/source-id-mappings";
  },
  importMetadata: async function() {
    return "/api/systems/" + (await getProofSystemId()) + "/import-metadata";
  }
};

function updateSessionStatus() {
    if (sessionStatus) {
        sessionStatus.textContent = "You are signed in to PluralBridge.";
    }
}

function clearOutput() {
    output.replaceChildren();
}

function renderPayload(payload) {
    clearOutput();

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "json-output";
    jsonBlock.textContent = JSON.stringify(payload, null, 2);

    output.appendChild(jsonBlock);
    updateSessionStatus();
}

function renderSelectionNote(labelText) {
    clearOutput();

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = (labelText || "Item") + " selected.";

    wrapper.appendChild(note);
    output.appendChild(wrapper);
    updateSessionStatus();
}

function readMembersUiStateFromStorage(storageKey) {
    if (!storageKey || typeof window.localStorage === "undefined") {
        return null;
    }

    try {
        const raw = window.localStorage.getItem(storageKey);
        return raw ? JSON.parse(raw) : null;
    } catch (error) {
        return null;
    }
}

function writeMembersUiStateToStorage(state) {
    if (!membersUiStorageKey || typeof window.localStorage === "undefined") {
        return;
    }

    const snapshot = {
        version: 3,
        systemPanelPinned: Boolean(state.systemPanelPinned),
        systemPanelOpen: Boolean(state.systemPanelOpen),
        preferredSelectionKind: state.preferredSelectionKind || "system",
        preferredSystemId: state.preferredSystemId || null,
        preferredGroupKey: state.preferredGroupKey || null,
        expandedSystemTreeNodes: state.expandedSystemTreeNodes || {}
    };

    try {
        window.localStorage.setItem(
            membersUiStorageKey,
            JSON.stringify(snapshot)
        );
    } catch (error) {
        // Ignore storage write failures in restricted environments.
    }
}

function applyPersistedMembersUiState(state, persistedState) {
    if (!persistedState || typeof persistedState !== "object") {
        return;
    }

    const persistedVersion =
        typeof persistedState.version === "number"
            ? persistedState.version
            : 0;

    if (persistedVersion >= 3) {
        if (typeof persistedState.systemPanelPinned === "boolean") {
            state.systemPanelPinned = persistedState.systemPanelPinned;
        }

        if (typeof persistedState.systemPanelOpen === "boolean") {
            state.systemPanelOpen = persistedState.systemPanelOpen;
        } else if (state.systemPanelPinned) {
            state.systemPanelOpen = true;
        }
    }

    if (typeof persistedState.preferredSystemId === "string") {
        state.preferredSystemId = persistedState.preferredSystemId;
    }

    if (
        persistedState.preferredSelectionKind === "group" ||
        persistedState.preferredSelectionKind === "system"
    ) {
        state.preferredSelectionKind = persistedState.preferredSelectionKind;
    }

    if (typeof persistedState.preferredGroupKey === "string") {
        state.preferredGroupKey = persistedState.preferredGroupKey;
    }

    if (
        persistedState.expandedSystemTreeNodes &&
        typeof persistedState.expandedSystemTreeNodes === "object"
    ) {
        state.expandedSystemTreeNodes = Object.assign(
            {
                "systems-root": true
            },
            persistedState.expandedSystemTreeNodes
        );
    }
}

function ensureMembersUiStateLoaded(state, rerender) {
    if (membersUiStorageLoaded || membersUiStorageLoading) {
        return;
    }

    const apiClient =
        window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;

    if (!apiClient || typeof apiClient.fetchJson !== "function") {
        membersUiStorageLoaded = true;
        return;
    }

    membersUiStorageLoading = true;

    apiClient.fetchJson("/api/me")
        .then(function (payload) {
            const accountId =
                payload &&
                payload.currentAccount &&
                payload.currentAccount.accountId
                    ? String(payload.currentAccount.accountId)
                    : "anonymous";

            membersUiStorageKey =
                membersUiStorageKeyPrefix + accountId.toLowerCase();

            const persistedState =
                readMembersUiStateFromStorage(membersUiStorageKey);

            applyPersistedMembersUiState(state, persistedState);
        })
        .catch(function () {
            membersUiStorageKey = membersUiStorageKeyPrefix + "anonymous";
        })
        .finally(function () {
            membersUiStorageLoading = false;
            membersUiStorageLoaded = true;

            if (typeof rerender === "function") {
                rerender();
            }
        });
}

function getSourceSystemsFromPayload(payload) {
    if (Array.isArray(payload)) {
        return payload;
    }

    if (payload && Array.isArray(payload.sourceSystems)) {
        return payload.sourceSystems;
    }

    return [];
}

function getSourceSystemDisplayName(sourceSystem) {
    if (sourceSystem && sourceSystem.displayName) {
        return sourceSystem.displayName;
    }

    if (sourceSystem && sourceSystem.sourceSystemCode) {
        return sourceSystem.sourceSystemCode;
    }

    return "(unnamed source system)";
}

function getSourceSystemCode(sourceSystem) {
    return sourceSystem && sourceSystem.sourceSystemCode ? sourceSystem.sourceSystemCode : "";
}

function createSourceSystemAccordion(sourceSystem) {
    const details = document.createElement("details");
    details.className = "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = getSourceSystemDisplayName(sourceSystem);

    const code = document.createElement("span");
    code.className = "member-pronouns";
    code.textContent = getSourceSystemCode(sourceSystem);

    const chevron = document.createElement("span");
    chevron.className = "member-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    identity.appendChild(name);
    identity.appendChild(code);

    summary.appendChild(identity);
    summary.appendChild(chevron);
    details.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const content = document.createElement("div");
    content.className = "member-content";

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(sourceSystem, null, 2);

    content.appendChild(jsonBlock);
    body.appendChild(content);
    details.appendChild(body);

    return details;
}

function renderSourceSystems(payload) {
    clearOutput();

    const sourceSystems = getSourceSystemsFromPayload(payload);

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Source systems";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = sourceSystems.length + " source systems returned from the read-only API.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    if (sourceSystems.length === 0) {
        const empty = document.createElement("p");
        empty.className = "output-note";
        empty.textContent = "No source systems were returned.";
        wrapper.appendChild(empty);
    } else {
        sourceSystems.forEach(function (sourceSystem) {
            wrapper.appendChild(createSourceSystemAccordion(sourceSystem));
        });
    }

    output.appendChild(wrapper);
    updateSessionStatus();
}


function getSystemsFromPayload(payload) {
    if (Array.isArray(payload)) {
        return payload;
    }

    if (payload && Array.isArray(payload.systems)) {
        return payload.systems;
    }

    return [];
}

function getSystemDisplayName(system) {
    if (system && system.systemName) {
        return system.systemName;
    }

    return "(unnamed system)";
}

function getSystemSubtitle(system) {
    if (system && system.description) {
        return system.description;
    }

    if (system && system.systemId) {
        return system.systemId;
    }

    return "";
}

function createSystemAccordion(system) {
    const details = document.createElement("details");
    details.className = "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = getSystemDisplayName(system);

    const subtitle = document.createElement("span");
    subtitle.className = "member-pronouns";
    subtitle.textContent = getSystemSubtitle(system);

    const chevron = document.createElement("span");
    chevron.className = "member-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    identity.appendChild(name);
    identity.appendChild(subtitle);

    summary.appendChild(identity);
    summary.appendChild(chevron);
    details.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const content = document.createElement("div");
    content.className = "member-content";

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(system, null, 2);

    content.appendChild(jsonBlock);
    body.appendChild(content);
    details.appendChild(body);

    return details;
}

function renderSystems(payload) {
    clearOutput();

    const systems = getSystemsFromPayload(payload);

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Systems";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = systems.length + " systems returned from the read-only API.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    if (systems.length === 0) {
        const empty = document.createElement("p");
        empty.className = "output-note";
        empty.textContent = "No systems were returned.";
        wrapper.appendChild(empty);
    } else {
        systems.forEach(function (system) {
            wrapper.appendChild(createSystemAccordion(system));
        });
    }

    output.appendChild(wrapper);
    updateSessionStatus();
}

function createMeField(labelText, valueText) {
    const item = document.createElement("div");
    item.className = "me-field";

    const label = document.createElement("span");
    label.className = "me-label";
    label.textContent = labelText;

    const value = document.createElement("span");
    value.className = "me-value";
    value.textContent = valueText || "(none)";

    item.appendChild(label);
    item.appendChild(value);

    return item;
}

function createMeCount(labelText, valueText) {
    const item = document.createElement("div");
    item.className = "me-count";

    const value = document.createElement("span");
    value.className = "me-count-value";
    value.textContent = String(valueText);

    const label = document.createElement("span");
    label.className = "me-count-label";
    label.textContent = labelText;

    item.appendChild(value);
    item.appendChild(label);

    return item;
}

function renderMe(payload) {
    clearOutput();

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Session";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = "PluralBridge proof context for this session.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    const details = document.createElement("details");
    details.className = "member-card";
    details.open = true;

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = payload && payload.mode ? payload.mode : "read-only proof";

    const subtitle = document.createElement("span");
    subtitle.className = "member-pronouns";
    subtitle.textContent = payload && payload.database ? payload.database : "";

    const chevron = document.createElement("span");
    chevron.className = "member-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    identity.appendChild(name);
    identity.appendChild(subtitle);

    summary.appendChild(identity);
    summary.appendChild(chevron);
    details.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const fields = document.createElement("div");
    fields.className = "me-fields";

    fields.appendChild(createMeField("API", payload && payload.api ? payload.api : ""));
    fields.appendChild(createMeField("Phase", payload && payload.phase ? payload.phase : ""));
    fields.appendChild(createMeField("Write access", payload && payload.canWrite ? "enabled" : "disabled"));

    if (payload && payload.proofSystem) {
        fields.appendChild(createMeField("Proof system ID", payload.proofSystem.systemId || ""));
        fields.appendChild(createMeField("Proof system name", payload.proofSystem.systemName || "(unnamed system)"));
    }

    body.appendChild(fields);

    const countsHeading = document.createElement("h3");
    countsHeading.className = "me-section-heading";
    countsHeading.textContent = "Read-only record counts";
    body.appendChild(countsHeading);

    const countsGrid = document.createElement("div");
    countsGrid.className = "me-count-grid";

    if (payload && payload.counts) {
        Object.keys(payload.counts).forEach(function (key) {
            countsGrid.appendChild(createMeCount(key, payload.counts[key]));
        });
    }

    body.appendChild(countsGrid);

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(payload, null, 2);

    const jsonDetails = document.createElement("details");
    jsonDetails.className = "me-json-details";

    const jsonSummary = document.createElement("summary");
    jsonSummary.textContent = "Raw JSON";

    jsonDetails.appendChild(jsonSummary);
    jsonDetails.appendChild(jsonBlock);
    body.appendChild(jsonDetails);

    details.appendChild(body);
    wrapper.appendChild(details);

    output.appendChild(wrapper);
    updateSessionStatus();
}

function getPrivacyBucketsFromPayload(payload) {
    if (Array.isArray(payload)) {
        return payload;
    }

    if (payload && Array.isArray(payload.privacyBuckets)) {
        return payload.privacyBuckets;
    }

    return [];
}

function getPrivacyBucketName(bucket) {
    return bucket && bucket.bucketName ? bucket.bucketName : "(unnamed privacy bucket)";
}

function getPrivacyBucketSubtitle(bucket) {
    if (bucket && bucket.description) {
        return bucket.description;
    }

    if (bucket && bucket.privacyBucketId) {
        return bucket.privacyBucketId;
    }

    return "";
}

function createPrivacyBucketAccordion(bucket) {
    const details = document.createElement("details");
    details.className = "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = getPrivacyBucketName(bucket);

    const subtitle = document.createElement("span");
    subtitle.className = "member-pronouns";
    subtitle.textContent = getPrivacyBucketSubtitle(bucket);

    const chevron = document.createElement("span");
    chevron.className = "member-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    identity.appendChild(name);
    identity.appendChild(subtitle);

    summary.appendChild(identity);
    summary.appendChild(chevron);
    details.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const content = document.createElement("div");
    content.className = "member-content";

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(bucket, null, 2);

    content.appendChild(jsonBlock);
    body.appendChild(content);
    details.appendChild(body);

    return details;
}

function renderPrivacyBuckets(payload) {
    clearOutput();

    const privacyBuckets = getPrivacyBucketsFromPayload(payload);

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Privacy buckets";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = privacyBuckets.length + " privacy buckets returned from the read-only API.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    if (privacyBuckets.length === 0) {
        const empty = document.createElement("p");
        empty.className = "output-note";
        empty.textContent = "No privacy buckets were returned.";
        wrapper.appendChild(empty);
    } else {
        privacyBuckets.forEach(function (bucket) {
            wrapper.appendChild(createPrivacyBucketAccordion(bucket));
        });
    }

    output.appendChild(wrapper);
    updateSessionStatus();
}

function getCustomFieldsFromPayload(payload) {
    if (Array.isArray(payload)) {
        return payload;
    }

    if (payload && Array.isArray(payload.customFields)) {
        return payload.customFields;
    }

    return [];
}

function getCustomFieldName(customField) {
    return customField && customField.fieldName ? customField.fieldName : "(unnamed custom field)";
}

function getCustomFieldSubtitle(customField) {
    if (customField && customField.description) {
        return customField.description;
    }

    if (customField && customField.fieldTypeCode !== null && customField.fieldTypeCode !== undefined) {
        return "Field type code " + customField.fieldTypeCode;
    }

    if (customField && customField.customFieldId) {
        return customField.customFieldId;
    }

    return "";
}

function createCustomFieldAccordion(customField) {
    const details = document.createElement("details");
    details.className = "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = getCustomFieldName(customField);

    const subtitle = document.createElement("span");
    subtitle.className = "member-pronouns";
    subtitle.textContent = getCustomFieldSubtitle(customField);

    const chevron = document.createElement("span");
    chevron.className = "member-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    identity.appendChild(name);
    identity.appendChild(subtitle);

    summary.appendChild(identity);
    summary.appendChild(chevron);
    details.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const content = document.createElement("div");
    content.className = "member-content";

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(customField, null, 2);

    content.appendChild(jsonBlock);
    body.appendChild(content);
    details.appendChild(body);

    return details;
}

function renderCustomFields(payload) {
    clearOutput();

    const customFields = getCustomFieldsFromPayload(payload);

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Custom fields";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = customFields.length + " custom fields returned from the read-only API.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    if (customFields.length === 0) {
        const empty = document.createElement("p");
        empty.className = "output-note";
        empty.textContent = "No custom fields were returned.";
        wrapper.appendChild(empty);
    } else {
        customFields.forEach(function (customField) {
            wrapper.appendChild(createCustomFieldAccordion(customField));
        });
    }

    output.appendChild(wrapper);
    updateSessionStatus();
}

function getImportBatchesFromPayload(payload) {
    if (Array.isArray(payload)) {
        return payload;
    }

    if (payload && Array.isArray(payload.importBatches)) {
        return payload.importBatches;
    }

    return [];
}

function getImportBatchName(importBatch) {
    if (importBatch && importBatch.sourceExportName) {
        return importBatch.sourceExportName;
    }

    if (importBatch && importBatch.importToolName) {
        return importBatch.importToolName;
    }

    if (importBatch && importBatch.importBatchId) {
        return importBatch.importBatchId;
    }

    return "(unnamed import batch)";
}

function formatImportBatchTime(value) {
    if (!value) {
        return "";
    }

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return value;
    }

    return date.toLocaleString();
}

function getImportBatchSubtitle(importBatch) {
    if (!importBatch) {
        return "";
    }

    const parts = [];

    if (importBatch.sourceSystemCode) {
        parts.push(importBatch.sourceSystemCode);
    }

    if (importBatch.importCompletedAtUtc) {
        parts.push("completed " + formatImportBatchTime(importBatch.importCompletedAtUtc));
    } else if (importBatch.importStartedAtUtc) {
        parts.push("started " + formatImportBatchTime(importBatch.importStartedAtUtc));
    }

    return parts.join(" · ");
}

function createImportBatchAccordion(importBatch) {
    const details = document.createElement("details");
    details.className = "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = getImportBatchName(importBatch);

    const subtitle = document.createElement("span");
    subtitle.className = "member-pronouns";
    subtitle.textContent = getImportBatchSubtitle(importBatch);

    const chevron = document.createElement("span");
    chevron.className = "member-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    identity.appendChild(name);
    identity.appendChild(subtitle);

    summary.appendChild(identity);
    summary.appendChild(chevron);
    details.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const content = document.createElement("div");
    content.className = "member-content";

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(importBatch, null, 2);

    content.appendChild(jsonBlock);
    body.appendChild(content);
    details.appendChild(body);

    return details;
}

function renderImportBatches(payload) {
    clearOutput();

    const importBatches = getImportBatchesFromPayload(payload);

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Import batches";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = importBatches.length + " import batches returned from the read-only API.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    if (importBatches.length === 0) {
        const empty = document.createElement("p");
        empty.className = "output-note";
        empty.textContent = "No import batches were returned.";
        wrapper.appendChild(empty);
    } else {
        importBatches.forEach(function (importBatch) {
            wrapper.appendChild(createImportBatchAccordion(importBatch));
        });
    }

    output.appendChild(wrapper);
    updateSessionStatus();
}

function getSourceRecordsFromPayload(payload) {
    if (Array.isArray(payload)) {
        return payload;
    }

    if (payload && Array.isArray(payload.sourceRecords)) {
        return payload.sourceRecords;
    }

    return [];
}

function getSourceRecordName(sourceRecord) {
    if (!sourceRecord) {
        return "(unknown source record)";
    }

    if (sourceRecord.sourceEntityTypeCode && sourceRecord.sourceId) {
        return sourceRecord.sourceEntityTypeCode + " · " + sourceRecord.sourceId;
    }

    if (sourceRecord.sourceEntityTypeCode) {
        return sourceRecord.sourceEntityTypeCode;
    }

    if (sourceRecord.sourceRecordId) {
        return sourceRecord.sourceRecordId;
    }

    return "(unknown source record)";
}

function getSourceRecordSubtitle(sourceRecord) {
    if (!sourceRecord) {
        return "";
    }

    const parts = [];

    if (sourceRecord.sourceSystemCode) {
        parts.push(sourceRecord.sourceSystemCode);
    }

    if (sourceRecord.sourceEndpoint) {
        parts.push(sourceRecord.sourceEndpoint);
    }

    return parts.join(" · ");
}

function createSourceRecordAccordion(sourceRecord) {
    const details = document.createElement("details");
    details.className = "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = getSourceRecordName(sourceRecord);

    const subtitle = document.createElement("span");
    subtitle.className = "member-pronouns";
    subtitle.textContent = getSourceRecordSubtitle(sourceRecord);

    const chevron = document.createElement("span");
    chevron.className = "member-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    identity.appendChild(name);
    identity.appendChild(subtitle);

    summary.appendChild(identity);
    summary.appendChild(chevron);
    details.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const content = document.createElement("div");
    content.className = "member-content";

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(sourceRecord, null, 2);

    content.appendChild(jsonBlock);
    body.appendChild(content);
    details.appendChild(body);

    return details;
}

function renderSourceRecords(payload) {
    clearOutput();

    const sourceRecords = getSourceRecordsFromPayload(payload);

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Source records";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = sourceRecords.length + " source records returned from the read-only API.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    if (sourceRecords.length === 0) {
        const empty = document.createElement("p");
        empty.className = "output-note";
        empty.textContent = "No source records were returned.";
        wrapper.appendChild(empty);
    } else {
        sourceRecords.forEach(function (sourceRecord) {
            wrapper.appendChild(createSourceRecordAccordion(sourceRecord));
        });
    }

    output.appendChild(wrapper);
    updateSessionStatus();
}

function getSourceIdMappingsFromPayload(payload) {
    if (Array.isArray(payload)) {
        return payload;
    }

    if (payload && Array.isArray(payload.sourceIdMappings)) {
        return payload.sourceIdMappings;
    }

    return [];
}

function getSourceIdMappingName(mapping) {
    if (!mapping) {
        return "(unknown source ID mapping)";
    }

    if (mapping.sourceEntityTypeCode && mapping.sourceId) {
        return mapping.sourceEntityTypeCode + " · " + mapping.sourceId;
    }

    if (mapping.sourceEntityTypeCode) {
        return mapping.sourceEntityTypeCode;
    }

    if (mapping.sourceIdMapId) {
        return mapping.sourceIdMapId;
    }

    return "(unknown source ID mapping)";
}

function getSourceIdMappingSubtitle(mapping) {
    if (!mapping) {
        return "";
    }

    const parts = [];

    if (mapping.sourceSystemCode) {
        parts.push(mapping.sourceSystemCode);
    }

    if (mapping.pluralBridgeEntityTypeCode && mapping.pluralBridgeId) {
        parts.push(mapping.pluralBridgeEntityTypeCode + " → " + mapping.pluralBridgeId);
    } else if (mapping.pluralBridgeEntityTypeCode) {
        parts.push(mapping.pluralBridgeEntityTypeCode);
    } else if (mapping.pluralBridgeId) {
        parts.push(mapping.pluralBridgeId);
    }

    return parts.join(" · ");
}

function createSourceIdMappingAccordion(mapping) {
    const details = document.createElement("details");
    details.className = "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = getSourceIdMappingName(mapping);

    const subtitle = document.createElement("span");
    subtitle.className = "member-pronouns";
    subtitle.textContent = getSourceIdMappingSubtitle(mapping);

    const chevron = document.createElement("span");
    chevron.className = "member-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    identity.appendChild(name);
    identity.appendChild(subtitle);

    summary.appendChild(identity);
    summary.appendChild(chevron);
    details.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const content = document.createElement("div");
    content.className = "member-content";

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(mapping, null, 2);

    content.appendChild(jsonBlock);
    body.appendChild(content);
    details.appendChild(body);

    return details;
}

function renderSourceIdMappings(payload) {
    clearOutput();

    const sourceIdMappings = getSourceIdMappingsFromPayload(payload);

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Source ID mappings";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = sourceIdMappings.length + " source ID mappings returned from the read-only  API.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    if (sourceIdMappings.length === 0) {
        const empty = document.createElement("p");
        empty.className = "output-note";
        empty.textContent = "No source ID mappings were returned.";
        wrapper.appendChild(empty);
    } else {
        sourceIdMappings.forEach(function (mapping) {
            wrapper.appendChild(createSourceIdMappingAccordion(mapping));
        });
    }

    output.appendChild(wrapper);
    updateSessionStatus();
}

function renderImportMetadata(payload) {
    clearOutput();

    const metadata = payload && payload.importMetadata ? payload.importMetadata : {};

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Import metadata";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = "Import health and lineage summary for this system.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    const details = document.createElement("details");
    details.className = "member-card";
    details.open = true;

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = "Import status";

    const subtitle = document.createElement("span");
    subtitle.className = "member-pronouns";
    subtitle.textContent = metadata.systemExists === true ? "system exists" : "system missing";

    const chevron = document.createElement("span");
    chevron.className = "member-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    identity.appendChild(name);
    identity.appendChild(subtitle);

    summary.appendChild(identity);
    summary.appendChild(chevron);
    details.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const fields = document.createElement("div");
    fields.className = "me-fields";

    fields.appendChild(createMeField("System ID", payload && payload.systemId ? payload.systemId : ""));
    fields.appendChild(createMeField("System exists", metadata.systemExists === true ? "yes" : "no"));

    body.appendChild(fields);

    const countsHeading = document.createElement("h3");
    countsHeading.className = "me-section-heading";
    countsHeading.textContent = "Import ledger counts";
    body.appendChild(countsHeading);

    const countsGrid = document.createElement("div");
    countsGrid.className = "me-count-grid";

    countsGrid.appendChild(createMeCount("source systems", metadata.sourceSystemCount));
    countsGrid.appendChild(createMeCount("import batches", metadata.importBatchCount));
    countsGrid.appendChild(createMeCount("source records", metadata.sourceRecordCount));
    countsGrid.appendChild(createMeCount("source ID mappings", metadata.sourceIdMappingCount));

    body.appendChild(countsGrid);

    if (metadata.latestImportBatch) {
        const batchHeading = document.createElement("h3");
        batchHeading.className = "me-section-heading";
        batchHeading.textContent = "Latest import batch";
        body.appendChild(batchHeading);

        body.appendChild(createImportBatchAccordion(metadata.latestImportBatch));
    }

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(payload, null, 2);

    const jsonDetails = document.createElement("details");
    jsonDetails.className = "me-json-details";

    const jsonSummary = document.createElement("summary");
    jsonSummary.textContent = "Raw JSON";

    jsonDetails.appendChild(jsonSummary);
    jsonDetails.appendChild(jsonBlock);
    body.appendChild(jsonDetails);

    details.appendChild(body);
    wrapper.appendChild(details);

    output.appendChild(wrapper);
    updateSessionStatus();
}

function getMembersFromPayload(payload) {
    if (Array.isArray(payload)) {
        return payload;
    }

    if (payload && Array.isArray(payload.members)) {
        return payload.members;
    }

    return [];
}

function getMemberDisplayName(member) {
    return member && member.displayName ? member.displayName : "(unnamed member)";
}

function getMemberPronouns(member) {
    return member && member.pronouns ? member.pronouns : "";
}

function getMemberDescription(member) {
    return member && member.description ? member.description : "No description available.";
}

function createMemberAccordion(member) {
    function hashStringToInt(value) {
        let hash = 0;

        for (let index = 0; index < value.length; index++) {
            hash = (hash * 31 + value.charCodeAt(index)) | 0;
        }

        return Math.abs(hash);
    }

    function getAvatarColorSeed() {
        if (member && member.memberId) {
            return String(member.memberId);
        }

        if (member && member.displayName) {
            return String(member.displayName);
        }

        return "member";
    }
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
        } else {
            path.setAttribute("d", "M4 17.3V21h3.7L18.8 9.9l-3.7-3.7L4 17.3zM20.7 8c.4-.4.4-1 0-1.4l-2.3-2.3c-.4-.4-1-.4-1.4 0l-1.2 1.2 3.7 3.7L20.7 8z");
        }

        svg.appendChild(path);
        return svg;
    }

    function getMemberIdForSave() {
        return member && member.memberId ? member.memberId : "";
    }

    function getProfilePlaceholder(fieldName) {
        if (fieldName === "displayName") {
            return "(unnamed member)";
        }

        if (fieldName === "pronouns") {
            return "Not set";
        }

        return "No description available.";
    }

    function getProfileDisplayText(fieldName) {
        const value = draftValues[fieldName];

        if (value) {
            return value;
        }

        return getProfilePlaceholder(fieldName);
    }

    function hasDirtyFields() {
        return draftValues.displayName !== persistedValues.displayName ||
            draftValues.pronouns !== persistedValues.pronouns ||
            draftValues.description !== persistedValues.description;
    }

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

    function setGuardStatus(message, statusType) {
        guardStatus.classList.remove("is-error");
        guardStatus.classList.remove("is-success");
        guardStatus.textContent = message || "";

        if (statusType === "error") {
            guardStatus.classList.add("is-error");
        }

        if (statusType === "success") {
            guardStatus.classList.add("is-success");
        }
    }

    function updateSummaryPreview() {
        name.textContent = getProfileDisplayText("displayName");
        pronouns.textContent = draftValues.pronouns || "";
    }

    function updateStaticFieldDisplay(fieldName) {
        const rowInfo = fieldRows[fieldName];

        if (!rowInfo) {
            return;
        }

        rowInfo.value.textContent = getProfileDisplayText(fieldName);
        rowInfo.value.dataset.draftValue = draftValues[fieldName] || "";
    }

    function updateAllStaticFieldDisplays() {
        Object.keys(fieldRows).forEach(function (fieldName) {
            updateStaticFieldDisplay(fieldName);
        });

        updateSummaryPreview();
        updateSaveButtonState();
    }

    function updateSaveButtonState() {
        if (!saveButton) {
            return;
        }

        const isDirty = hasDirtyFields();

        saveButton.disabled = isSaving || !isDirty;
        saveButton.title = isDirty ? "Save profile changes" : "No profile changes to save";
    }

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

        editState.row.classList.remove("is-active");
        editState.editButton.disabled = isSaving;

        updateSummaryPreview();
        updateSaveButtonState();
    }

    function createEditorForField(fieldName, isLongText) {
        let editor = null;

        if (isLongText) {
            editor = document.createElement("textarea");
            editor.rows = 6;
            editor.className = "member-profile-field-editor member-profile-field-textarea";
        } else {
            editor = document.createElement("input");
            editor.type = "text";
            editor.className = "member-profile-field-editor member-profile-field-input";
            editor.autocomplete = "off";
        }

        editor.value = draftValues[fieldName] || "";
        editor.setAttribute("aria-label", "Edit " + fieldLabels[fieldName]);

        editor.addEventListener("blur", function () {
            closeActiveEditor(true);
        });

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

        rowInfo.row.classList.add("is-active");
        rowInfo.editButton.disabled = true;
        rowInfo.value.replaceWith(editor);

        activeEdit = {
            fieldName: fieldName,
            row: rowInfo.row,
            displayElement: rowInfo.value,
            editor: editor,
            editButton: rowInfo.editButton
        };

        window.setTimeout(function () {
            editor.focus();
            editor.select();
        }, 0);
    }

    function createMemberProfileFieldRow(labelText, fieldName, rawValue, isLongText) {
        const row = document.createElement("div");
        row.className = "member-profile-field-row";
        row.dataset.memberProfileField = fieldName;

        const label = document.createElement("span");
        label.className = "member-profile-field-label";
        label.textContent = labelText;

        const value = document.createElement("span");
        value.className = "member-profile-field-value";
        value.dataset.memberProfileValue = fieldName;
        value.dataset.persistedValue = rawValue || "";
        value.dataset.draftValue = rawValue || "";
        value.textContent = rawValue || getProfilePlaceholder(fieldName);

        if (isLongText) {
            value.classList.add("member-profile-field-value-long");
        }

        const editButton = document.createElement("button");
        editButton.type = "button";
        editButton.className = "member-profile-field-edit-button";
        editButton.dataset.memberProfileEdit = fieldName;
        editButton.title = "Edit " + labelText;
        editButton.setAttribute("aria-label", "Edit " + labelText);

        editButton.appendChild(createMemberActionIcon("edit"));

        editButton.addEventListener("click", function () {
            startEditingField(fieldName);
        });

        row.appendChild(label);
        row.appendChild(value);
        row.appendChild(editButton);

        fieldRows[fieldName] = {
            row: row,
            value: value,
            editButton: editButton,
            isLongText: isLongText
        };

        return row;
    }

    function createProfileSaveButton() {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "member-profile-save-button";
        button.disabled = true;
        button.title = "No profile changes to save.";

        button.appendChild(createMemberActionIcon("save"));

        const label = document.createElement("span");
        label.textContent = "Save";

        button.appendChild(label);

        button.addEventListener("click", saveProfileChanges);

        return button;
    }

    function setProfileBusy(isBusy) {
        isSaving = isBusy;

        Object.keys(fieldRows).forEach(function (fieldName) {
            fieldRows[fieldName].editButton.disabled = isBusy;
        });

        if (activeEdit && activeEdit.editor) {
            activeEdit.editor.disabled = isBusy;
        }

        updateSaveButtonState();
    }

    function setGuardBusy(isBusy) {
        saveChangesButton.disabled = isBusy;
        discardChangesButton.disabled = isBusy;
        keepEditingButton.disabled = isBusy;
    }

    function buildMemberEditRequest() {
        return {
            displayName: draftValues.displayName.trim(),
            pronouns: draftValues.pronouns.trim(),
            description: draftValues.description.trim()
        };
    }

    function applySuccessfulSave() {
        persistedValues.displayName = draftValues.displayName;
        persistedValues.pronouns = draftValues.pronouns;
        persistedValues.description = draftValues.description;

        Object.keys(fieldRows).forEach(function (fieldName) {
            fieldRows[fieldName].value.dataset.persistedValue = persistedValues[fieldName] || "";
            fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
            updateStaticFieldDisplay(fieldName);
        });

        if (member) {
            member.displayName = persistedValues.displayName;
            member.pronouns = persistedValues.pronouns;
            member.description = persistedValues.description;
        }

        jsonBlock.textContent = JSON.stringify(member, null, 2);

        updateSummaryPreview();
        updateSaveButtonState();
    }

    async function saveProfileChanges() {
        closeActiveEditor(true);

        if (!hasDirtyFields()) {
            updateSaveButtonState();
            return true;
        }

        const memberId = getMemberIdForSave();

        if (!memberId) {
            setProfileStatus("Member edit is not available because this member has no member ID.", "error");
            return false;
        }

        const apiClient = window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;

        if (!apiClient || typeof apiClient.editMember !== "function") {
            setProfileStatus("Member edit is not available in the current browser session.", "error");
            return false;
        }

        const request = buildMemberEditRequest();

        if (!request.displayName) {
            setProfileStatus("Name is required.", "error");
            updateSaveButtonState();
            return false;
        }

        setProfileBusy(true);
        setProfileStatus("Saving member.");

        try {
            await apiClient.editMember(memberId, request);
            applySuccessfulSave();
            setProfileStatus("Member saved.", "success");
            return true;
        } catch (error) {
            setProfileStatus("Member could not be saved. Confirm the API is running and try again.", "error");
            return false;
        } finally {
            setProfileBusy(false);
        }
    }

    function discardProfileChanges() {
        closeActiveEditor(false);

        draftValues.displayName = persistedValues.displayName;
        draftValues.pronouns = persistedValues.pronouns;
        draftValues.description = persistedValues.description;

        setProfileStatus("");
        updateAllStaticFieldDisplays();
    }

    function showUnsavedDialog() {
        setGuardStatus("");

        guardDialog.hidden = false;
        guardDialog.removeAttribute("hidden");

        if (typeof guardDialog.showModal === "function" && !guardDialog.open) {
            guardDialog.showModal();
        } else {
            guardDialog.setAttribute("open", "open");
        }

        keepEditingButton.focus();
    }

    function hideUnsavedDialog() {
        if (typeof guardDialog.close === "function" && guardDialog.open) {
            guardDialog.close();
        }

        guardDialog.hidden = true;
        guardDialog.removeAttribute("open");
        setGuardStatus("");
        setGuardBusy(false);
    }

    function completePendingLeave(shouldLeave) {
        const resolve = pendingLeaveResolve;
        const action = pendingLeaveAction;

        pendingLeaveResolve = null;
        pendingLeaveAction = null;

        hideUnsavedDialog();

        if (shouldLeave && typeof action === "function") {
            action();
        }

        if (typeof resolve === "function") {
            resolve(shouldLeave);
        }
    }

    function requestProfileLeave(leaveAction) {
        closeActiveEditor(true);

        if (!hasDirtyFields()) {
            if (typeof leaveAction === "function") {
                leaveAction();
            }

            return Promise.resolve(true);
        }

        if (pendingLeaveResolve) {
            return Promise.resolve(false);
        }

        pendingLeaveAction = leaveAction || null;
        showUnsavedDialog();

        return new Promise(function (resolve) {
            pendingLeaveResolve = resolve;
        });
    }

    async function saveChangesAndLeave() {
        setGuardBusy(true);
        setGuardStatus("Saving changes.");

        const didSave = await saveProfileChanges();

        if (didSave) {
            completePendingLeave(true);
            return;
        }

        setGuardBusy(false);
        setGuardStatus("Changes could not be saved. Keep editing or discard changes.", "error");
    }

    function discardChangesAndLeave() {
        discardProfileChanges();
        completePendingLeave(true);
    }

    function returnToCurrentProfile() {
        cardDetails.open = true;

        window.setTimeout(function () {
            cardDetails.scrollIntoView({
                block: "center",
                inline: "nearest"
            });

            try {
                summary.focus({
                    preventScroll: true
                });
            } catch (error) {
                summary.focus();
            }
        }, 0);
    }

    function keepEditing() {
        completePendingLeave(false);
        returnToCurrentProfile();
    }
    function createGuardActionButton(labelText, className) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = className;
        button.textContent = labelText;
        return button;
    }

    const draftValues = {
        displayName: member && member.displayName ? member.displayName : "",
        pronouns: member && member.pronouns ? member.pronouns : "",
        description: member && member.description ? member.description : ""
    };

    const persistedValues = {
        displayName: draftValues.displayName,
        pronouns: draftValues.pronouns,
        description: draftValues.description
    };

    const fieldLabels = {
        displayName: "Name",
        pronouns: "Pronouns",
        description: "Description"
    };

    const fieldRows = {};
    let activeEdit = null;
    let isSaving = false;
    let pendingLeaveResolve = null;
    let pendingLeaveAction = null;

    const cardDetails = document.createElement("details");
    cardDetails.className = "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const summaryMain = document.createElement("span");
    summaryMain.className = "member-summary-main";

    const avatarPlaceholder = document.createElement("span");
    avatarPlaceholder.className = "member-avatar-placeholder";
    avatarPlaceholder.setAttribute("aria-hidden", "true");

    const colorHash = hashStringToInt(getAvatarColorSeed());
    const hue = colorHash % 360;
    const avatarFill = "hsl(" + hue + " 52% 28%)";
    const avatarBorder = "hsl(" + hue + " 56% 48%)";

    avatarPlaceholder.style.backgroundColor = avatarFill;
    avatarPlaceholder.style.borderColor = avatarBorder;

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = getProfileDisplayText("displayName");

    const pronouns = document.createElement("span");
    pronouns.className = "member-pronouns";
    pronouns.textContent = draftValues.pronouns || "";

    const chevron = document.createElement("span");
    chevron.className = "member-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    identity.appendChild(name);
    identity.appendChild(pronouns);

    summaryMain.appendChild(avatarPlaceholder);
    summaryMain.appendChild(identity);

    summary.appendChild(summaryMain);
    summary.appendChild(chevron);

    summary.addEventListener("click", async function (event) {
        const currentGuard = window.PluralBridge && window.PluralBridge.memberProfileGuard
            ? window.PluralBridge.memberProfileGuard
            : null;

        if (
            !cardDetails.open &&
            currentGuard &&
            currentGuard !== profileGuard &&
            typeof currentGuard.hasDirtyChanges === "function" &&
            currentGuard.hasDirtyChanges() &&
            typeof currentGuard.requestLeave === "function"
        ) {
            event.preventDefault();
            event.stopPropagation();

            await currentGuard.requestLeave(function () {
                cardDetails.open = true;
            });

            return;
        }

        if (cardDetails.open && hasDirtyFields()) {
            event.preventDefault();
            event.stopPropagation();

            requestProfileLeave(function () {
                cardDetails.open = false;
            });
        }
    });

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

    const profileActions = document.createElement("div");
    profileActions.className = "member-profile-action-row";

    const saveButton = createProfileSaveButton();
    profileActions.appendChild(saveButton);

    const profileStatus = document.createElement("p");
    profileStatus.className = "member-form-status";
    profileStatus.setAttribute("aria-live", "polite");
    profileStatus.textContent = "";

    const guardDialog = document.createElement("dialog");
    guardDialog.className = "member-profile-unsaved-dialog";
    guardDialog.setAttribute("aria-label", "Unsaved member profile changes");
    guardDialog.hidden = true;

    const guardMessage = document.createElement("p");
    guardMessage.className = "member-profile-unsaved-message";
    guardMessage.textContent = "This member has unsaved profile changes.";

    const guardButtonRow = document.createElement("div");
    guardButtonRow.className = "member-profile-unsaved-actions";

    const saveChangesButton = createGuardActionButton("Save changes", "member-profile-unsaved-button");
    const discardChangesButton = createGuardActionButton("Discard changes", "member-profile-unsaved-button");
    const keepEditingButton = createGuardActionButton("Keep editing", "member-profile-unsaved-button");

    saveChangesButton.addEventListener("click", saveChangesAndLeave);
    discardChangesButton.addEventListener("click", discardChangesAndLeave);
    keepEditingButton.addEventListener("click", keepEditing);

    guardDialog.addEventListener("cancel", function (event) {
        event.preventDefault();
        keepEditing();
    });

    guardButtonRow.appendChild(saveChangesButton);
    guardButtonRow.appendChild(discardChangesButton);
    guardButtonRow.appendChild(keepEditingButton);

    const guardStatus = document.createElement("p");
    guardStatus.className = "member-form-status member-profile-unsaved-status";
    guardStatus.setAttribute("aria-live", "polite");
    guardStatus.textContent = "";

    guardDialog.appendChild(guardMessage);
    guardDialog.appendChild(guardButtonRow);
    guardDialog.appendChild(guardStatus);

    profilePanel.appendChild(profileHeading);
    profilePanel.appendChild(profileFields);
    profilePanel.appendChild(profileActions);
    profilePanel.appendChild(profileStatus);
    profilePanel.appendChild(guardDialog);

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

    const profileGuard = {
        hasDirtyChanges: hasDirtyFields,
        requestLeave: requestProfileLeave
    };

    cardDetails.addEventListener("toggle", function () {
        window.PluralBridge = window.PluralBridge || {};

        if (cardDetails.open) {
            window.PluralBridge.memberProfileGuard = profileGuard;
            return;
        }

        if (window.PluralBridge.memberProfileGuard === profileGuard) {
            window.PluralBridge.memberProfileGuard = null;
        }
    });

    updateSaveButtonState();

    return cardDetails;
}

function getMembersUiState() {
    window.PluralBridge = window.PluralBridge || {};

    if (!window.PluralBridge.membersUiState) {
        window.PluralBridge.membersUiState = {
            systemPanelOpen: true,
            systemPanelPinned: true,
            expandedSystemTreeNodes: {
                "systems-root": true
            },
            activeSystemId: null,
            activeGroupKey: null,
            activeGroupMemberIds: [],
            preferredSystemId: null,
            preferredSelectionKind: "system",
            preferredGroupKey: null,
            restorePreferredSystemAttempted: false,
            systemNameById: {},
            groupByKey: {},
            systemTreeData: null,
            systemTreeLoading: false,
            systemTreeError: null
        };
    }

    return window.PluralBridge.membersUiState;
}

function getSystemsFromPayloadForTree(payload) {
    if (Array.isArray(payload)) {
        return payload;
    }

    if (payload && Array.isArray(payload.systems)) {
        return payload.systems;
    }

    return [];
}

function getSystemParentId(system) {
    if (!system) {
        return null;
    }

    if (system.parentSystemId) {
        return String(system.parentSystemId);
    }

    if (system.systemParentId) {
        return String(system.systemParentId);
    }

    if (system.parentId) {
        return String(system.parentId);
    }

    if (system.parentSystem && system.parentSystem.systemId) {
        return String(system.parentSystem.systemId);
    }

    return null;
}

function getSystemName(system) {
    if (system && system.systemName) {
        return system.systemName;
    }

    return "(unnamed system)";
}

function getSystemGroups(system) {
    if (system && Array.isArray(system.groups)) {
        return system.groups;
    }

    if (system && Array.isArray(system.systemGroups)) {
        return system.systemGroups;
    }

    return [];
}

function getGroupName(group) {
    if (group && group.groupName) {
        return group.groupName;
    }

    if (group && group.name) {
        return group.name;
    }

    return "(unnamed group)";
}

function getGroupParentId(group) {
    if (!group) {
        return null;
    }

    if (group.parentGroupId) {
        return String(group.parentGroupId);
    }

    if (group.parentId) {
        return String(group.parentId);
    }

    return null;
}

function getGroupSourceId(group) {
    if (group && group.sourceGroupId) {
        return String(group.sourceGroupId);
    }

    return null;
}

function getGroupParentSourceId(group) {
    if (group && group.parentSourceGroupId) {
        return String(group.parentSourceGroupId);
    }

    return null;
}

function getGroupMemberIds(group) {
    if (group && Array.isArray(group.memberIds)) {
        return group.memberIds.map(function (memberId) {
            return String(memberId).toLowerCase();
        });
    }

    if (group && Array.isArray(group.members)) {
        return group.members.map(function (member) {
            if (member && member.memberId) {
                return String(member.memberId).toLowerCase();
            }

            return String(member).toLowerCase();
        });
    }

    return [];
}

function toGroupSelectionKey(systemId, groupId) {
    return String(systemId).toLowerCase() + ":" + String(groupId).toLowerCase();
}

function ensureDefaultMembersTreeExpansion(state, activeSystemId) {
    if (!state || !state.systemTreeData) {
        return false;
    }

    if (!state.expandedSystemTreeNodes || typeof state.expandedSystemTreeNodes !== "object") {
        state.expandedSystemTreeNodes = {};
    }

    let changed = false;

    if (state.expandedSystemTreeNodes["systems-root"] !== true) {
        state.expandedSystemTreeNodes["systems-root"] = true;
        changed = true;
    }

    function expandDefaults(node) {
        if (!node) {
            return;
        }

        const children = Array.isArray(node.children) ? node.children : [];
        if (children.length > 0 && !(node.id in state.expandedSystemTreeNodes)) {
            state.expandedSystemTreeNodes[node.id] = true;
            changed = true;
        }

        children.forEach(function (childNode) {
            expandDefaults(childNode);
        });
    }

    expandDefaults(state.systemTreeData);

    if (activeSystemId) {
        const systemExpansionKey = String(activeSystemId);
        if (!(systemExpansionKey in state.expandedSystemTreeNodes)) {
            state.expandedSystemTreeNodes[systemExpansionKey] = true;
            changed = true;
        }
    }

    return changed;
}

function sortSystemTreeNodes(nodes) {
    nodes.sort(function (left, right) {
        return left.name.localeCompare(right.name);
    });

    nodes.forEach(function (node) {
        if (Array.isArray(node.children) && node.children.length > 0) {
            sortSystemTreeNodes(node.children);
        }
    });
}

function buildMembersSystemTree(systems) {
    const nodeById = new Map();
    const systemNameById = {};
    const groupByKey = {};

    systems.forEach(function (system) {
        if (!system || !system.systemId) {
            return;
        }

        const nodeId = String(system.systemId);
        const nodeName = getSystemName(system);
        systemNameById[nodeId.toLowerCase()] = nodeName;

        nodeById.set(nodeId, {
            id: nodeId,
            type: "system",
            name: nodeName,
            system: system,
            children: []
        });
    });

    const roots = [];

    systems.forEach(function (system) {
        if (!system || !system.systemId) {
            return;
        }

        const nodeId = String(system.systemId);
        const node = nodeById.get(nodeId);
        const parentId = getSystemParentId(system);

        if (parentId && nodeById.has(parentId)) {
            nodeById.get(parentId).children.push(node);
        } else {
            roots.push(node);
        }

        const groups = getSystemGroups(system);
        if (groups.length === 0) {
            return;
        }

        const groupNodeById = new Map();
        const groupIdBySourceId = new Map();
        const rootGroups = [];

        groups.forEach(function (group) {
            if (!group || !group.groupId) {
                return;
            }

            const groupId = String(group.groupId);
            const sourceGroupId = getGroupSourceId(group);
            if (sourceGroupId) {
                groupIdBySourceId.set(sourceGroupId.toLowerCase(), groupId);
            }
        });

        groups.forEach(function (group) {
            if (!group || !group.groupId) {
                return;
            }

            const groupId = String(group.groupId);
            const normalizedGroupId = groupId.toLowerCase();
            const groupSelectionKey = toGroupSelectionKey(nodeId, groupId);

            const groupNode = {
                id: "group:" + groupSelectionKey,
                type: "group",
                name: getGroupName(group),
                groupId: groupId,
                systemId: nodeId,
                groupSelectionKey: groupSelectionKey,
                memberIds: getGroupMemberIds(group),
                children: []
            };

            groupNodeById.set(normalizedGroupId, groupNode);
            groupByKey[groupSelectionKey] = groupNode;
        });

        groups.forEach(function (group) {
            if (!group || !group.groupId) {
                return;
            }

            const groupId = String(group.groupId);
            const normalizedGroupId = groupId.toLowerCase();
            const groupNode = groupNodeById.get(normalizedGroupId);
            if (!groupNode) {
                return;
            }
            let parentGroupId = getGroupParentId(group);

            if (!parentGroupId) {
                const parentSourceGroupId = getGroupParentSourceId(group);
                if (parentSourceGroupId) {
                    parentGroupId = groupIdBySourceId.get(parentSourceGroupId.toLowerCase()) || null;
                }
            }

            const normalizedParentGroupId = parentGroupId
                ? String(parentGroupId).toLowerCase()
                : null;

            if (normalizedParentGroupId && groupNodeById.has(normalizedParentGroupId)) {
                groupNodeById.get(normalizedParentGroupId).children.push(groupNode);
            } else {
                rootGroups.push(groupNode);
            }
        });

        sortSystemTreeNodes(rootGroups);

        rootGroups.forEach(function (groupNode) {
            node.children.push(groupNode);
        });
    });

    sortSystemTreeNodes(roots);

    return {
        id: "systems-root",
        type: "root",
        name: "Systems",
        systemNameById: systemNameById,
        groupByKey: groupByKey,
        children: roots
    };
}

function requestMembersSystemTree(state, onComplete) {
    if (state.systemTreeData || state.systemTreeLoading) {
        return;
    }

    const apiClient =
        window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;

    if (!apiClient || typeof apiClient.fetchJson !== "function") {
        state.systemTreeError = "System list could not be loaded in this session.";
        return;
    }

    state.systemTreeLoading = true;
    state.systemTreeError = null;

    apiClient.fetchJson("/api/systems")
        .then(function (payload) {
            const systems = getSystemsFromPayloadForTree(payload);
            state.systemTreeData = buildMembersSystemTree(systems);
            state.systemNameById = state.systemTreeData.systemNameById || {};
            state.groupByKey = state.systemTreeData.groupByKey || {};
            state.systemTreeLoading = false;

            if (typeof onComplete === "function") {
                onComplete();
            }
        })
        .catch(function (error) {
            state.systemTreeLoading = false;
            state.systemTreeError = error && error.message
                ? "System list could not be loaded right now (" + error.message + ")."
                : "System list could not be loaded right now.";

            if (typeof onComplete === "function") {
                onComplete();
            }
        });
}

function createMembersSystemPanel(state, rerenderMembers, selectSystem, selectGroup) {
    const panel = document.createElement("aside");
    panel.className = "member-systems-panel";

    const isVisible = state.systemPanelPinned || state.systemPanelOpen;

    panel.hidden = !isVisible;

    if (!state.systemPanelPinned) {
        panel.classList.add("is-overlay");
    } else {
        panel.classList.add("is-pinned");
    }

    const header = document.createElement("div");
    header.className = "member-systems-header";

    const heading = document.createElement("h3");
    heading.className = "member-systems-title";
    heading.textContent = "System navigator";

    function createPinIcon(isPinned) {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-systems-pin-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        if (!isPinned) {
            svg.classList.add("is-unpinned");
        }

        const path = document.createElementNS(svgNamespace, "path");
        path.setAttribute("fill", "currentColor");
        path.setAttribute("d", "M16 3c.6 0 1 .4 1 1v1.5l1.7 1.7c.4.4.4 1 0 1.4L17 10.3V14l2 2v1H13v4l-1 1-1-1v-4H5v-1l2-2v-3.7L5.3 8.6a1 1 0 0 1 0-1.4L7 5.5V4c0-.6.4-1 1-1h8z");
        svg.appendChild(path);

        return svg;
    }

    const pinButton = document.createElement("button");
    pinButton.type = "button";
    pinButton.className = "member-systems-pin-button";
    pinButton.setAttribute("aria-pressed", String(state.systemPanelPinned));
    pinButton.setAttribute("aria-label", state.systemPanelPinned ? "Unpin panel" : "Pin panel");
    pinButton.title = state.systemPanelPinned ? "Unpin panel" : "Pin panel";
    pinButton.appendChild(createPinIcon(state.systemPanelPinned));

    pinButton.addEventListener("click", function () {
        if (state.systemPanelPinned) {
            state.systemPanelPinned = false;
            state.systemPanelOpen = false;
        } else {
            state.systemPanelPinned = true;
            state.systemPanelOpen = true;
        }
        writeMembersUiStateToStorage(state);
        rerenderMembers();
    });

    header.appendChild(heading);
    header.appendChild(pinButton);
    panel.appendChild(header);

    const tree = document.createElement("div");
    tree.className = "member-systems-tree";

    if (state.systemTreeLoading) {
        const loading = document.createElement("p");
        loading.className = "output-note";
        loading.textContent = "Loading systems.";
        tree.appendChild(loading);
        panel.appendChild(tree);
        return panel;
    }

    if (state.systemTreeError) {
        const error = document.createElement("p");
        error.className = "output-note";
        error.textContent = state.systemTreeError;
        tree.appendChild(error);
        panel.appendChild(tree);
        return panel;
    }

    if (!state.systemTreeData) {
        const empty = document.createElement("p");
        empty.className = "output-note";
        empty.textContent = "No system structure available yet.";
        tree.appendChild(empty);
        panel.appendChild(tree);
        return panel;
    }

    function createNodeLabel(iconText, labelText) {
        const wrapper = document.createElement("span");
        wrapper.className = "member-tree-node-content";

        const icon = document.createElement("span");
        icon.className = "member-tree-node-icon";
        icon.setAttribute("aria-hidden", "true");
        icon.textContent = iconText;

        const label = document.createElement("span");
        label.className = "member-tree-node-text";
        label.textContent = labelText;

        wrapper.appendChild(icon);
        wrapper.appendChild(label);
        return wrapper;
    }

    function appendNode(node, level) {
        const row = document.createElement("div");
        row.className = "member-system-node-row";
        row.style.setProperty("--system-node-level", String(level));

        const children = Array.isArray(node.children) ? node.children : [];
        const hasChildren = children.length > 0;
        const isExpanded = Boolean(state.expandedSystemTreeNodes[node.id]);

        if (hasChildren) {
            const expandButton = document.createElement("button");
            expandButton.type = "button";
            expandButton.className = "member-system-tree-toggle";
            expandButton.setAttribute("aria-expanded", String(isExpanded));
            expandButton.textContent = isExpanded ? "−" : "+";

            expandButton.addEventListener("click", function () {
                state.expandedSystemTreeNodes[node.id] = !isExpanded;
                writeMembersUiStateToStorage(state);
                rerenderMembers();
            });

            row.appendChild(expandButton);
        } else {
            const spacer = document.createElement("span");
            spacer.className = "member-system-tree-spacer";
            spacer.textContent = "";
            row.appendChild(spacer);
        }

        if (node.type === "system" && node.id !== "systems-root") {
            const selectButton = document.createElement("button");
            selectButton.type = "button";
            selectButton.className = "member-system-node-button";
            selectButton.appendChild(createNodeLabel("◈", node.name));

            if (
                !state.activeGroupKey &&
                state.activeSystemId &&
                String(state.activeSystemId).toLowerCase() === String(node.id).toLowerCase()
            ) {
                selectButton.classList.add("is-active");
                selectButton.setAttribute("aria-current", "true");
            }

            if (typeof selectSystem === "function") {
                selectButton.addEventListener("click", function () {
                    selectSystem(node.id);
                });
            }

            row.appendChild(selectButton);
        } else if (node.type === "group") {
            const selectButton = document.createElement("button");
            selectButton.type = "button";
            selectButton.className = "member-system-node-button member-group-node-button";
            selectButton.appendChild(createNodeLabel("▣", node.name));

            if (
                state.activeGroupKey &&
                String(state.activeGroupKey).toLowerCase() === String(node.groupSelectionKey).toLowerCase()
            ) {
                selectButton.classList.add("is-active");
                selectButton.setAttribute("aria-current", "true");
            }

            if (typeof selectGroup === "function") {
                selectButton.addEventListener("click", function () {
                    selectGroup(node.groupSelectionKey);
                });
            }

            row.appendChild(selectButton);
        } else {
            const label = document.createElement("span");
            label.className = "member-system-node-label";
            label.appendChild(createNodeLabel("☰", node.name));
            row.appendChild(label);
        }

        tree.appendChild(row);

        if (hasChildren && isExpanded) {
            children.forEach(function (childNode) {
                appendNode(childNode, level + 1);
            });
        }
    }

    appendNode(state.systemTreeData, 0);
    panel.appendChild(tree);
    return panel;
}

function renderMembers(payload) {
    const membersUiState = getMembersUiState();
    ensureMembersUiStateLoaded(membersUiState, function () {
        renderMembers(payload);
    });

    // Keep the initial static loading shell visible until saved state is loaded.
    if (!membersUiStorageLoaded) {
        updateSessionStatus();
        return;
    }

    clearOutput();

    const sourceMembers = getMembersFromPayload(payload);

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Members";

    const note = document.createElement("p");
    note.className = "output-note";

    const logo = document.createElement("img");
    logo.className = "member-brand-logo";
    logo.src = "/app/images/pluralbridge-about-logo-smaller.png";
    logo.alt = "PluralBridge logo";
    logo.addEventListener("error", function () {
        logo.hidden = true;
    });

    const payloadSystemId =
        payload && payload.systemId
            ? String(payload.systemId)
            : null;

    if (payloadSystemId) {
        membersUiState.activeSystemId = payloadSystemId;
    }

    const payloadSelectionKind =
        payload && payload.selectionKind === "group" ? "group" : "system";
    const payloadGroupKey =
        payload && payload.groupSelectionKey
            ? String(payload.groupSelectionKey).toLowerCase()
            : null;
    const payloadGroupMemberIds = Array.isArray(payload && payload.groupMemberIds)
        ? payload.groupMemberIds.map(function (memberId) {
            return String(memberId).toLowerCase();
        })
        : null;

    if (payloadSelectionKind === "group" && payloadGroupKey) {
        membersUiState.activeGroupKey = payloadGroupKey;
        membersUiState.preferredSelectionKind = "group";
        membersUiState.preferredGroupKey = payloadGroupKey;
    } else if (payloadSelectionKind === "system") {
        membersUiState.activeGroupKey = null;
    }

    if (
        membersUiStorageLoaded &&
        !membersUiState.restorePreferredSystemAttempted &&
        membersUiState.preferredSelectionKind === "group" &&
        membersUiState.preferredGroupKey
    ) {
        const preferredGroupKey = String(membersUiState.preferredGroupKey).toLowerCase();
        const hasPreferredGroupNode =
            membersUiState.groupByKey &&
            membersUiState.groupByKey[preferredGroupKey];

        if (hasPreferredGroupNode) {
            membersUiState.restorePreferredSystemAttempted = true;
            selectMembersGroup(preferredGroupKey);
            return;
        }

        if (
            !membersUiState.systemTreeData &&
            !membersUiState.systemTreeLoading &&
            !membersUiState.systemTreeError
        ) {
            requestMembersSystemTree(membersUiState, function () {
                renderMembers(payload);
            });
            return;
        }

        membersUiState.restorePreferredSystemAttempted = true;
        if (membersUiState.preferredSystemId) {
            selectMembersSystem(membersUiState.preferredSystemId);
            return;
        }

        return;
    }

    if (
        membersUiStorageLoaded &&
        !membersUiState.restorePreferredSystemAttempted &&
        membersUiState.preferredSelectionKind !== "group" &&
        membersUiState.preferredSystemId &&
        payloadSystemId &&
        membersUiState.preferredSystemId.toLowerCase() !== payloadSystemId.toLowerCase()
    ) {
        membersUiState.restorePreferredSystemAttempted = true;
        selectMembersSystem(membersUiState.preferredSystemId);
        return;
    }

    if (
        membersUiStorageLoaded &&
        payloadSystemId &&
        !membersUiState.preferredSystemId
    ) {
        membersUiState.preferredSystemId = payloadSystemId;
        writeMembersUiStateToStorage(membersUiState);
    }

    let members = sourceMembers;

    if (payloadSelectionKind === "group" && payloadGroupMemberIds) {
        const selectedMemberIdSet = new Set(payloadGroupMemberIds);
        members = sourceMembers.filter(function (member) {
            return (
                member &&
                member.memberId &&
                selectedMemberIdSet.has(String(member.memberId).toLowerCase())
            );
        });
    }

    note.textContent = members.length + " members returned.";

    const focusedSystemId = payloadSystemId || membersUiState.activeSystemId || null;
    const focusedSystemName =
        focusedSystemId && membersUiState.systemNameById
            ? membersUiState.systemNameById[String(focusedSystemId).toLowerCase()] || "(unknown system)"
            : null;
    const focusedGroupName =
        payloadSelectionKind === "group" &&
        payloadGroupKey &&
        membersUiState.groupByKey &&
        membersUiState.groupByKey[payloadGroupKey]
            ? membersUiState.groupByKey[payloadGroupKey].name || "(unnamed group)"
            : null;

    const focusNotes = document.createElement("div");
    focusNotes.className = "member-focus-notes";

    const focusSystemNote = document.createElement("p");
    focusSystemNote.className = "output-note member-focus-note";
    focusSystemNote.textContent = focusedSystemName
        ? "Focused system: " + focusedSystemName
        : "Focused system:";
    if (!focusedSystemName) {
        focusSystemNote.classList.add("is-placeholder");
    }
    focusNotes.appendChild(focusSystemNote);

    const focusGroupNote = document.createElement("p");
    focusGroupNote.className = "output-note member-focus-note";
    focusGroupNote.textContent = focusedGroupName
        ? "Focused group: " + focusedGroupName
        : "Focused group:";
    if (!focusedGroupName) {
        focusGroupNote.classList.add("is-placeholder");
    }
    focusNotes.appendChild(focusGroupNote);

    const showSystemsPanel =
        membersUiState.systemPanelPinned ||
        membersUiState.systemPanelOpen;

    if (
        showSystemsPanel &&
        !membersUiState.systemTreeData &&
        !membersUiState.systemTreeLoading &&
        !membersUiState.systemTreeError
    ) {
        requestMembersSystemTree(membersUiState, function () {
            renderMembers(payload);
        });
    }

    if (membersUiState.systemTreeData) {
        const expansionChanged = ensureDefaultMembersTreeExpansion(
            membersUiState,
            payloadSystemId || membersUiState.activeSystemId
        );

        if (expansionChanged) {
            writeMembersUiStateToStorage(membersUiState);
        }
    }

    wrapper.appendChild(heading);
    wrapper.appendChild(note);
    wrapper.appendChild(logo);
    wrapper.appendChild(focusNotes);

    const memberAddContainer = document.createElement("div");
    memberAddContainer.className = "member-add-container";
    memberAddContainer.hidden = true;

    function refreshMembersFromApi() {
        return window.PluralBridge.apiClient.readMembers()
            .then(function (payload) {
                renderMembers(payload);
                return payload;
            });
    }

    function setMembersSystemContext(systemId) {
        selectedSystemId = systemId;

        if (
            window.PluralBridge &&
            window.PluralBridge.apiClient &&
            typeof window.PluralBridge.apiClient.setCurrentSystemId === "function"
        ) {
            window.PluralBridge.apiClient.setCurrentSystemId(systemId);
        }

        membersUiState.activeSystemId = String(systemId);
    }

    function selectMembersSystem(systemId) {
        if (!systemId) {
            return;
        }

        setMembersSystemContext(systemId);

        if (!membersUiState.systemPanelPinned) {
            membersUiState.systemPanelOpen = false;
        }

        writeMembersUiStateToStorage(membersUiState);

        refreshMembersFromApi()
            .then(function (membersPayload) {
                renderMembers(Object.assign({}, membersPayload, {
                    selectionKind: "system"
                }));
            })
            .catch(function (error) {
                renderPayload({
                    phase: "Phase 2B",
                    canWrite: false,
                    action: "members",
                    error: error && error.name ? error.name : "Error",
                    message: error && error.message ? error.message : "System switch failed."
                });
            });
    }

    function selectMembersGroup(groupSelectionKey) {
        if (!groupSelectionKey || !membersUiState.groupByKey) {
            return;
        }

        const normalizedGroupKey = String(groupSelectionKey).toLowerCase();
        const groupNode = membersUiState.groupByKey[normalizedGroupKey];

        if (!groupNode || !groupNode.systemId) {
            return;
        }

        const normalizedSystemId = String(groupNode.systemId);
        const groupMemberIds = Array.isArray(groupNode.memberIds)
            ? groupNode.memberIds
            : [];

        setMembersSystemContext(normalizedSystemId);
        membersUiState.activeGroupKey = normalizedGroupKey;
        membersUiState.activeGroupMemberIds = groupMemberIds;
        membersUiState.preferredSystemId = normalizedSystemId;
        membersUiState.preferredSelectionKind = "group";
        membersUiState.preferredGroupKey = normalizedGroupKey;
        membersUiState.restorePreferredSystemAttempted = true;

        if (!membersUiState.systemPanelPinned) {
            membersUiState.systemPanelOpen = false;
        }

        writeMembersUiStateToStorage(membersUiState);

        refreshMembersFromApi()
            .then(function (membersPayload) {
                renderMembers(Object.assign({}, membersPayload, {
                    selectionKind: "group",
                    groupSelectionKey: normalizedGroupKey,
                    groupMemberIds: groupMemberIds
                }));
            })
            .catch(function (error) {
                renderPayload({
                    phase: "Phase 2B",
                    canWrite: false,
                    action: "members",
                    error: error && error.name ? error.name : "Error",
                    message: error && error.message ? error.message : "Group switch failed."
                });
            });
    }

    function hideAddMemberForm() {
        memberAddContainer.hidden = true;
    }

    function toggleAddMemberForm() {
        memberAddContainer.hidden = !memberAddContainer.hidden;
    }

    if (
        window.PluralBridge &&
        window.PluralBridge.members &&
        typeof window.PluralBridge.members.createMemberToolbar === "function"
    ) {
        function rerenderMembers() {
            renderMembers(payload);
        }

        function toggleSystemPanel() {
            if (membersUiState.systemPanelPinned) {
                membersUiState.systemPanelPinned = false;
                membersUiState.systemPanelOpen = false;
            } else {
                membersUiState.systemPanelOpen = !membersUiState.systemPanelOpen;
            }

            writeMembersUiStateToStorage(membersUiState);
            rerenderMembers();
        }

        wrapper.appendChild(window.PluralBridge.members.createMemberToolbar({
            toggleAddMember: toggleAddMemberForm,
            toggleSystemPanel: toggleSystemPanel,
            isSystemPanelOpen: showSystemsPanel
        }));
    }

    if (
        window.PluralBridge &&
        window.PluralBridge.members &&
        typeof window.PluralBridge.members.createMemberAddForm === "function"
    ) {
        memberAddContainer.appendChild(window.PluralBridge.members.createMemberAddForm({
            refreshMembers: refreshMembersFromApi,
            cancelAddMember: hideAddMemberForm
        }));
    }

    const memberScrollList = document.createElement("div");
    memberScrollList.className = "member-scroll-list";

    if (members.length === 0) {
        const empty = document.createElement("p");
        empty.className = "output-note";
        empty.textContent = "No members were returned.";
        memberScrollList.appendChild(empty);
    } else {
        members.forEach(function (member) {
            memberScrollList.appendChild(createMemberAccordion(member));
        });
    }

    const membersWorkspace = document.createElement("div");
    membersWorkspace.className = "member-workspace";

    const memberContent = document.createElement("div");
    memberContent.className = "member-content-stack";
    memberContent.appendChild(memberAddContainer);
    memberContent.appendChild(memberScrollList);

    membersWorkspace.appendChild(
        createMembersSystemPanel(
            membersUiState,
            function () {
                renderMembers(payload);
            },
            selectMembersSystem,
            selectMembersGroup
        )
    );
    membersWorkspace.appendChild(memberContent);
    wrapper.appendChild(membersWorkspace);

    output.appendChild(wrapper);
    updateSessionStatus();
}

function getFrontHistoryFromPayload(payload) {
    if (Array.isArray(payload)) {
        return payload;
    }

    if (payload && Array.isArray(payload.frontHistory)) {
        return payload.frontHistory;
    }

    return [];
}

function getFrontHistoryName(record) {
    return record && record.memberDisplayName ? record.memberDisplayName : "(unknown member)";
}

function formatFrontHistoryTime(value) {
    if (!value) {
        return "";
    }

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return "";
    }

    return date.toLocaleString();
}

function getFrontHistoryTimeRange(record) {
    const start = formatFrontHistoryTime(record.startTimeMs);

    if (record.isLive) {
        return start ? start + " – Live" : "Live";
    }

    const end = formatFrontHistoryTime(record.endTimeMs);

    if (start && end) {
        return start + " – " + end;
    }

    if (start) {
        return start;
    }

    return "No time recorded";
}

function createFrontHistoryAccordion(record) {
    const details = document.createElement("details");
    details.className = "front-history-card";

    const summary = document.createElement("summary");
    summary.className = "front-history-summary";

    const name = document.createElement("span");
    name.className = "front-history-name";
    name.textContent = getFrontHistoryName(record);

    const timeRange = document.createElement("span");
    timeRange.className = "front-history-time";
    timeRange.textContent = getFrontHistoryTimeRange(record);

    const chevron = document.createElement("span");
    chevron.className = "front-history-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "v";

    summary.appendChild(name);
    summary.appendChild(timeRange);
    summary.appendChild(chevron);
    details.appendChild(summary);

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "front-history-json json-output";
    jsonBlock.textContent = JSON.stringify(record, null, 2);

    details.appendChild(jsonBlock);

    return details;
}

function renderFrontHistory(payload) {
    clearOutput();

    const records = getFrontHistoryFromPayload(payload);

    const wrapper = document.createElement("section");
    wrapper.className = "front-history-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Front History";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = records.length + " front history records returned from the read-only API.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    if (records.length === 0) {
        const empty = document.createElement("p");
        empty.className = "output-note";
        empty.textContent = "No front history records were returned.";
        wrapper.appendChild(empty);
    } else {
        records.forEach(function (record) {
            wrapper.appendChild(createFrontHistoryAccordion(record));
        });
    }

    output.appendChild(wrapper);
    updateSessionStatus();
}

function apiUrl(endpoint) {
  const base = apiBaseUrl.endsWith("/") ? apiBaseUrl.slice(0, -1) : apiBaseUrl;
  return base + endpoint;
}

async function fetchJson(endpoint) {
  const response = await fetch(apiUrl(endpoint), {
    headers: {
      Accept: "application/json"
    }
  });

  const responseText = await response.text();
  const payload = responseText ? JSON.parse(responseText) : null;

  if (!response.ok) {
    throw new Error("API request failed with HTTP " + response.status);
  }

  return payload;
}

async function getProofSystemId() {
  if (selectedSystemId) {
    return selectedSystemId;
  }

  const payload = await fetchJson("/api/me");

  const currentSystemId =
    payload &&
    payload.currentSystem &&
    payload.currentSystem.systemId
      ? payload.currentSystem.systemId
      : null;

  const proofSystemId =
    payload &&
    payload.proofSystem &&
    payload.proofSystem.systemId
      ? payload.proofSystem.systemId
      : null;

  if (!currentSystemId && !proofSystemId) {
    throw new Error("No system context was returned by /api/me.");
  }

  // The account's current-system membership context is the authoritative route scope.
  selectedSystemId = currentSystemId || proofSystemId;
  return selectedSystemId;
}

function setContractButtonState(selectedKey) {
  contractButtons.forEach(function(button) {
    button.setAttribute("aria-pressed", String(button.dataset.contract === selectedKey));
  });
}

function setSessionButtonState(selectedAction) {
  sessionButtons.forEach(function(button) {
    button.setAttribute("aria-pressed", String(button.dataset.sessionAction === selectedAction));
  });
}

function getEventTargetElement(event) {
  if (!event) {
    return null;
  }

  if (event.target instanceof Element) {
    return event.target;
  }

  if (event.target && event.target.parentElement instanceof Element) {
    return event.target.parentElement;
  }

  return null;
}

async function allowMemberProfileLeave() {
    const guard = window.PluralBridge && window.PluralBridge.memberProfileGuard
        ? window.PluralBridge.memberProfileGuard
        : null;

    if (!guard || typeof guard.hasDirtyChanges !== "function" || !guard.hasDirtyChanges()) {
        return true;
    }

    if (typeof guard.requestLeave !== "function") {
        return false;
    }

    return await guard.requestLeave();
}

async function renderContract(key) {
  const endpointBuilder = endpointBuilders[key];

  setContractButtonState(key);
  setSessionButtonState(null);

  if (!endpointBuilder) {
    renderPayload({
      phase: "Phase 2B",
      canWrite: false,
      error: "Unknown read-only action",
      action: key
    });
    return;
  }

  // Keep loading payload hidden during normal use; enable only for explicit debugging.
  if (window.PluralBridge && window.PluralBridge.showDebugLoading === true) {
    renderPayload({
      phase: "Phase 2B",
      canWrite: false,
      action: key,
      status: "loading"
    });
  }

  try {
      const endpoint = await endpointBuilder();
      const payload = await fetchJson(endpoint);

      if (key === "me") {
          renderMe(payload);
      } else if (key === "sourceSystems") {
          renderSourceSystems(payload);
      } else if (key === "importBatches") {
          renderImportBatches(payload);
      } else if (key === "systems") {
          renderSystems(payload);
      } else if (key === "privacyBuckets") {
          renderPrivacyBuckets(payload);
      } else if (key === "customFields") {
          renderCustomFields(payload);
      } else if (key === "members") {
          renderMembers(payload);
      } else if (key === "frontHistory") {
          renderFrontHistory(payload);
      } else if (key === "sourceRecords") {
          renderSourceRecords(payload);
      } else if (key === "sourceIdMappings") {
          renderSourceIdMappings(payload);
      } else if (key === "importMetadata") {
          renderImportMetadata(payload);
      } else {
          renderPayload(payload);
      }
  } catch (error) {
    renderPayload({
      phase: "Phase 2B",
      canWrite: false,
      action: key,
      error: error.name || "Error",
      message: error.message
    });
  }
}

function renderFrozenSessionAction(action) {
  setContractButtonState(null);
  setSessionButtonState(action);
  renderPayload({
    phase: "Phase 2B",
    action: action,
    canWrite: false,
    status: "Phase 3A login/session contract is frozen while Phase 2B completes the database-backed read-only surface."
  });
}

async function logout() {
  try {
    const response = await fetch("/logout", {
      method: "POST",
      credentials: "same-origin"
    });

    if (response.redirected) {
      window.location.href = response.url;
      return;
    }

    window.location.href = "/login";
  } catch (error) {
    renderPayload({
      phase: "Phase 2B",
      action: "logout",
      canWrite: false,
      error: error.name || "Error",
      message: error.message
    });
  }
}

if (loginForm) {
    loginForm.addEventListener("submit", function (event) {
        event.preventDefault();
        renderFrozenSessionAction("login");
    });
}

document.addEventListener("click", async function (event) {
    const targetElement = getEventTargetElement(event);
    const placeholderButton = targetElement
        ? targetElement.closest("[data-shell-placeholder]")
        : null;

    if (placeholderButton) {
        const placeholderRoute = placeholderButton.dataset.shellPlaceholder || "";
        const accountScreens =
            window.PluralBridge && window.PluralBridge.accountScreens
                ? window.PluralBridge.accountScreens
                : null;

        if (
            accountScreens &&
            typeof accountScreens.render === "function" &&
            accountScreens.render(placeholderRoute)
        ) {
            return;
        }

        renderSelectionNote((placeholderButton.textContent || "").trim());
        return;
    }

    const contractButton = targetElement
        ? targetElement.closest("[data-contract]")
        : null;

    if (contractButton) {
        event.preventDefault();

        const canLeave = await allowMemberProfileLeave();

        if (!canLeave) {
            return;
        }

        renderContract(contractButton.dataset.contract);
        return;
    }

    const sessionButton = targetElement
        ? targetElement.closest("[data-session-action]")
        : null;

    if (sessionButton) {
        event.preventDefault();

        const canLeave = await allowMemberProfileLeave();

        if (!canLeave) {
            return;
        }

        const action = sessionButton.dataset.sessionAction;

        if (action === "logout") {
            logout();
            return;
        }

        if (action !== "login") {
            renderFrozenSessionAction(action);
        }
    }
}); 

updateSessionStatus();
