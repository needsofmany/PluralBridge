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
    const membersApi = window.PluralBridge && window.PluralBridge.members
        ? window.PluralBridge.members
        : null;
    if (membersApi && typeof membersApi.releaseAddMenuHandlers === "function") {
        membersApi.releaseAddMenuHandlers();
    }
    if (membersApi && typeof membersApi.closeAddMemberDialog === "function") {
        membersApi.closeAddMemberDialog();
    }

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

function createMemberAccordion(member, options) {
    const allowProfileEditing = !options || options.allowProfileEditing !== false;
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

    function sharingOutlineIcon(kind) {
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("class", "member-sharing-icon");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "20");
        svg.setAttribute("height", "20");
        svg.setAttribute("fill", "none");
        svg.setAttribute("stroke", "currentColor");
        svg.setAttribute("stroke-width", "1.8");
        svg.setAttribute("stroke-linecap", "round");
        svg.setAttribute("stroke-linejoin", "round");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");

        function addShape(tagName, attributes) {
            const node = document.createElementNS(svgNamespace, tagName);
            Object.keys(attributes).forEach(function (name) {
                node.setAttribute(name, attributes[name]);
            });
            svg.appendChild(node);
        }

        if (kind === "only-me") {
            addShape("path", { d: "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" });
            addShape("circle", { cx: "12", cy: "7", r: "4" });
        } else if (kind === "selected-people") {
            addShape("path", { d: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" });
            addShape("circle", { cx: "9", cy: "7", r: "4" });
            addShape("path", { d: "M22 21v-2a4 4 0 0 0-3-3.87" });
            addShape("path", { d: "M16 3.13a4 4 0 0 1 0 7.75" });
        } else if (kind === "all-connections") {
            addShape("path", { d: "M8.59 13.51 15.42 17.49" });
            addShape("path", { d: "M15.41 6.51 8.59 10.49" });
            addShape("circle", { cx: "18", cy: "5", r: "3" });
            addShape("circle", { cx: "6", cy: "12", r: "3" });
            addShape("circle", { cx: "18", cy: "19", r: "3" });
        } else if (kind === "inherit") {
            addShape("path", { d: "M7 7h8a4 4 0 0 1 0 8H9" });
            addShape("path", { d: "M11 13 8 16l3 3" });
        } else if (kind === "chevron") {
            svg.setAttribute("width", "16");
            svg.setAttribute("height", "16");
            svg.setAttribute("stroke-width", "1.75");
            addShape("path", { d: "M9 5.5 16.5 12 9 18.5" });
        } else {
            addShape("circle", { cx: "12", cy: "12", r: "9" });
            addShape("path", { d: "M8 12h8" });
        }

        return svg;
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
        } else if (window.PluralBridge.members && typeof window.PluralBridge.members.createEditPencil === "function") {
            return window.PluralBridge.members.createEditPencil();
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

    function updateSummaryPreview() {
        name.textContent = allowProfileEditing
            ? getProfileDisplayText("displayName")
            : (draftValues.displayName || "");
        pronouns.textContent = draftValues.pronouns || "";
    }

    function updateStaticFieldDisplay(fieldName) {
        const rowInfo = fieldRows[fieldName];

        if (!rowInfo) {
            return;
        }

        rowInfo.value.textContent = allowProfileEditing
            ? getProfileDisplayText(fieldName)
            : (draftValues[fieldName] || "");
        rowInfo.value.dataset.draftValue = draftValues[fieldName] || "";
    }

    function updateAllStaticFieldDisplays() {
        Object.keys(fieldRows).forEach(function (fieldName) {
            updateStaticFieldDisplay(fieldName);
        });

        updateSummaryPreview();
        updateSaveButtonState();
    }

    function changeTracking() {
        return window.PluralBridge && window.PluralBridge.changeTracking
            ? window.PluralBridge.changeTracking
            : null;
    }

    function cardIsDirty() {
        const changes = changeTracking();
        return !!(changes && changes.dirty(cardDetails));
    }

    function modifiedMarker() {
        const changes = changeTracking();
        return changes ? changes.modifiedClass : "pb-control-modified";
    }

    function notifyEdited(control) {
        const changes = changeTracking();
        if (!changes || !control) {
            return;
        }

        changes.notify(control);
    }

    function renderChangeState() {
        const dirty = cardIsDirty();
        cardDetails.classList.remove("member-card-unsaved");
        if (unsavedIndicator) {
            unsavedIndicator.hidden = !dirty;
        }
        updateSaveButtonState();
    }

    function withoutChangeNotices(work) {
        const changes = changeTracking();
        if (!changes) {
            work();
            return;
        }

        changes.suppress(cardDetails, work);
    }

    let cancelButton = null;

    function updateSaveButtonState() {
        const dirty = cardIsDirty();
        if (cancelButton) {
            cancelButton.hidden = !dirty;
            cancelButton.disabled = isSaving;
        }

        if (!saveButton) {
            return;
        }

        const nameMissing = !String(draftValues.displayName || "").trim();
        saveButton.disabled = isSaving || !dirty || nameMissing;
        saveButton.title = isSaving
            ? "Saving profile changes"
            : nameMissing
                ? "Name is required."
                : dirty
                    ? "Save profile changes"
                    : "No profile changes to save";
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

        if (editState.editor.classList.contains(modifiedMarker())) {
            editState.displayElement.classList.add(modifiedMarker());
        }

        editState.row.classList.remove("is-active");
        editState.editButton.disabled = isSaving;

        updateSummaryPreview();
        updateSaveButtonState();
    }

    function fitLongEditor(editor) {
        if (!editor || editor.tagName !== "TEXTAREA" || !editor.isConnected) {
            return;
        }

        editor.style.height = "auto";
        editor.style.height = editor.scrollHeight + "px";
    }

    function createEditorForField(fieldName, isLongText) {
        let editor = null;

        if (isLongText) {
            editor = document.createElement("textarea");
            editor.className = "member-profile-field-editor member-profile-field-textarea";
        } else {
            editor = document.createElement("input");
            editor.type = "text";
            editor.className = "member-profile-field-editor member-profile-field-input";
            editor.autocomplete = "off";
        }

        editor.value = draftValues[fieldName] || "";
        editor.setAttribute("aria-label", "Edit " + fieldLabels[fieldName]);
        const fieldLabel = fieldRows[fieldName] && fieldRows[fieldName].row.querySelector(".member-profile-field-label");
        if (fieldLabel && fieldLabel.id) {
            editor.setAttribute("aria-labelledby", fieldLabel.id);
        }

        editor.addEventListener("input", function () {
            draftValues[fieldName] = editor.value;
            if (isLongText) {
                fitLongEditor(editor);
            }
            updateSaveButtonState();
        });

        editor.addEventListener("blur", function () {
            if (isLongText) {
                return;
            }
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
        const changes = changeTracking();
        if (changes) {
            changes.rebind(rowInfo.value, editor);
        }
        if (rowInfo.value.classList.contains(modifiedMarker())) {
            editor.classList.add(modifiedMarker());
        }

        rowInfo.row.classList.add("is-active");
        rowInfo.editButton.disabled = true;
        rowInfo.value.replaceWith(editor);
        if (rowInfo.isLongText) {
            fitLongEditor(editor);
        }

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
        label.id = "member-field-" + window.PluralBridge.memberSharingSequence + "-" + fieldName;
        label.textContent = labelText;

        const value = document.createElement("span");
        value.className = "member-profile-field-value";
        value.dataset.memberProfileValue = fieldName;
        value.dataset.persistedValue = rawValue || "";
        value.dataset.draftValue = rawValue || "";
        value.setAttribute("data-change-control", "");
        value.setAttribute("aria-labelledby", label.id);
        value.textContent = allowProfileEditing
            ? (rawValue || getProfilePlaceholder(fieldName))
            : (rawValue || "");

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

        if (allowProfileEditing) {
            value.addEventListener("dblclick", function (event) {
                event.preventDefault();
                event.stopPropagation();
                startEditingField(fieldName);
            });
        }

        row.appendChild(label);
        row.appendChild(value);
        if (allowProfileEditing) {
            row.appendChild(editButton);
        }

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
        button.disabled = false;
        button.title = "Save profile changes";

        button.appendChild(createMemberActionIcon("save"));

        const label = document.createElement("span");
        label.textContent = "Save";

        button.appendChild(label);

        let savePointerHeld = false;

        function rememberOpenCard() {
            pendingSavedCardView = captureCardView();
            keepSavedCardOpen = true;
        }

        function releaseUnusedSaveHold() {
            window.removeEventListener("pointerup", releaseUnusedSaveHold, true);
            window.removeEventListener("pointercancel", releaseUnusedSaveHold, true);
            window.setTimeout(function () {
                if (!savePointerHeld) {
                    return;
                }

                savePointerHeld = false;
                keepSavedCardOpen = false;
                pendingSavedCardView = null;
            }, 0);
        }

        button.addEventListener("pointerdown", function (event) {
            if (event.button !== 0) {
                return;
            }

            event.stopPropagation();
            savePointerHeld = true;
            rememberOpenCard();
            window.addEventListener("pointerup", releaseUnusedSaveHold, true);
            window.addEventListener("pointercancel", releaseUnusedSaveHold, true);
        });

        button.addEventListener("keydown", function (event) {
            if (event.key !== "Enter" && event.key !== " ") {
                return;
            }

            event.stopPropagation();
            savePointerHeld = true;
            rememberOpenCard();
        });

        button.addEventListener("keyup", function (event) {
            if (event.key === "Enter" || event.key === " ") {
                releaseUnusedSaveHold();
            }
        });

        button.addEventListener("click", async function (event) {
            event.preventDefault();
            event.stopPropagation();
            savePointerHeld = false;
            if (isSaving) {
                return;
            }
            const view = pendingSavedCardView || captureCardView();
            const holdToken = ++savedCardHoldToken;
            pendingSavedCardView = null;
            activeSavedCardView = view;
            view.open = true;
            keepSavedCardOpen = true;
            restoreCardView(view);

            try {
                await saveProfileChanges();
            } finally {
                window.setTimeout(function () {
                    if (holdToken !== savedCardHoldToken) {
                        return;
                    }

                    restoreCardView(view);
                    window.setTimeout(function () {
                        if (holdToken !== savedCardHoldToken) {
                            return;
                        }

                        restoreCardView(view);
                        keepSavedCardOpen = false;
                        pendingSavedCardView = null;
                        if (activeSavedCardView === view) {
                            activeSavedCardView = null;
                        }
                    }, 0);
                }, 0);
            }
        });

        return button;
    }

    function createProfileCancelButton() {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "member-profile-cancel-button";
        button.textContent = "Cancel";
        button.hidden = true;
        button.disabled = false;

        button.addEventListener("pointerdown", function (event) {
            event.stopPropagation();
        });

        button.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
            if (isSaving || !cardIsDirty()) {
                return;
            }

            const view = captureCardView();
            view.open = true;
            const changes = changeTracking();
            if (changes) {
                changes.restore(cardDetails);
            }
            cardDetails.open = true;
            restoreCardView(view);
            window.setTimeout(function () {
                restoreCardView(view);
            }, 0);
        });

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

        [frontingButton, sharingButton, colorPencil, colorInput, avatarPencil, avatarFile].forEach(function (control) {
            if (control) {
                control.disabled = isBusy;
            }
        });

        const permissionButton = cardDetails.querySelector(".member-policy-button");
        if (permissionButton) {
            permissionButton.disabled = isBusy;
        }

        const changes = changeTracking();
        if (changes) {
            changes.setSaving(cardDetails, isBusy);
        }

        updateSaveButtonState();
    }

    function buildMemberEditRequest() {
        return {
            displayName: draftValues.displayName.trim(),
            pronouns: draftValues.pronouns.trim(),
            description: draftValues.description.trim()
        };
    }

    function applySuccessfulSave() {
        if (!applySuccessfulSave.guarded) {
            applySuccessfulSave.guarded = true;
            try {
                withoutChangeNotices(function () {
                    applySuccessfulSave();
                });
            } finally {
                applySuccessfulSave.guarded = false;
            }
            return;
        }

        persistedValues.displayName = draftValues.displayName;
        persistedValues.pronouns = draftValues.pronouns;
        persistedValues.description = draftValues.description;
        persistedValues.color = draftValues.color;
        persistedValues.fronting = draftValues.fronting;
        persistedValues.visibilityKnown = draftValues.visibilityKnown;
        persistedValues.visibilityInherit = draftValues.visibilityInherit;
        persistedValues.visibilityTypeId = draftValues.visibilityTypeId;
        persistedValues.permission = draftValues.permission;
        persistedValues.avatarObjectUrl = draftValues.avatarObjectUrl;

        Object.keys(fieldRows).forEach(function (fieldName) {
            fieldRows[fieldName].value.dataset.persistedValue = persistedValues[fieldName] || "";
            fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
            updateStaticFieldDisplay(fieldName);
        });

        if (member) {
            member.displayName = persistedValues.displayName;
            member.pronouns = persistedValues.pronouns;
            member.description = persistedValues.description;
            if (previewHeader) {
                member.color = persistedValues.color;
                member.fronting = persistedValues.fronting;
                member.visibilityKnown = persistedValues.visibilityKnown;
                member.visibilityTypeId = persistedValues.visibilityTypeId;
                member.avatarObjectUrl = persistedValues.avatarObjectUrl;
            }
        }

        jsonBlock.textContent = JSON.stringify(member, null, 2);

        updateSummaryPreview();
        updateSaveButtonState();
        paintPreviewControls();
    }

    function applyPersistedProfileFields(profile) {
        if (!applyPersistedProfileFields.guarded) {
            applyPersistedProfileFields.guarded = true;
            try {
                withoutChangeNotices(function () {
                    applyPersistedProfileFields(profile);
                });
            } finally {
                applyPersistedProfileFields.guarded = false;
            }
            return;
        }

        const displayName = profile && profile.displayName ? String(profile.displayName) : "";
        const pronouns = profile && profile.pronouns ? String(profile.pronouns) : "";
        const description = profile && profile.description ? String(profile.description) : "";
        const color = storedMemberColor(profile && profile.color);

        persistedValues.displayName = displayName;
        persistedValues.pronouns = pronouns;
        persistedValues.description = description;
        persistedValues.color = color;
        draftValues.displayName = displayName;
        draftValues.pronouns = pronouns;
        draftValues.description = description;
        draftValues.color = color;

        ["displayName", "pronouns", "description"].forEach(function (fieldName) {
            if (!fieldRows[fieldName]) {
                return;
            }

            fieldRows[fieldName].value.dataset.persistedValue = persistedValues[fieldName] || "";
            fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
            updateStaticFieldDisplay(fieldName);
        });

        if (member) {
            member.displayName = displayName;
            member.pronouns = pronouns;
            member.description = description;
            member.color = color;
        }

        jsonBlock.textContent = JSON.stringify(member, null, 2);
        updateSummaryPreview();
        updateSaveButtonState();
        paintPreviewControls();
    }

    function markedControls() {
        const changes = changeTracking();
        return changes ? changes.modified(cardDetails) : [];
    }

    function controlIsMarked(control) {
        return !!(control && markedControls().indexOf(control) >= 0);
    }

    function savedProfileControls(before) {
        return before.filter(function (control) {
            return profileControls.indexOf(control) >= 0;
        });
    }

    function publishConfirmed(before, includeColor, includeLatchedExtras) {
        const changes = changeTracking();
        if (!changes) {
            return;
        }

        const fields = {
            displayName: persistedValues.displayName,
            pronouns: persistedValues.pronouns,
            description: persistedValues.description
        };
        if (includeColor) {
            fields.color = persistedValues.color;
        }
        if (includeLatchedExtras && before.indexOf(frontingButton) >= 0) {
            fields.fronting = persistedValues.fronting;
        }
        if (includeLatchedExtras && before.indexOf(sharingButton) >= 0) {
            fields.visibilityKnown = persistedValues.visibilityKnown;
            fields.visibilityInherit = persistedValues.visibilityInherit;
            fields.visibilityTypeId = persistedValues.visibilityTypeId;
        }
        if (includeLatchedExtras && permissionControl && before.indexOf(permissionControl) >= 0) {
            fields.permission = persistedValues.permission;
        }
        if (includeLatchedExtras && before.indexOf(avatarWrap) >= 0) {
            fields.avatarObjectUrl = persistedValues.avatarObjectUrl;
        }
        changes.publishShared(getMemberIdForSave(), fields, cardDetails);
    }

    async function saveProfileChanges() {
        closeActiveEditor(true);

        if (isSaving) {
            return { saved: false };
        }

        if (changeTracking() && !cardIsDirty()) {
            updateSaveButtonState();
            return { saved: false };
        }

        const memberId = getMemberIdForSave();

        if (!memberId) {
            const message = "Member edit is not available because this member has no member ID.";
            setProfileStatus(message, "error");
            return { saved: false, message: message };
        }

        const request = buildMemberEditRequest();

        if (!request.displayName) {
            const message = "Name is required.";
            setProfileStatus(message, "error");
            updateSaveButtonState();
            return { saved: false, message: message };
        }

        const before = markedControls().slice();
        const changes = changeTracking();
        const includeColor = before.indexOf(colorSwatch) >= 0;

        if (options && typeof options.saveProfile === "function") {
            setProfileBusy(true);
            setProfileStatus("Saving member.");

            try {
                const outcome = await options.saveProfile(profilePreviewSaveRequest(memberId, request));

                if (!cardDetails.isConnected) {
                    return { saved: false, message: "Member could not be saved." };
                }

                if (!outcome || outcome.saved !== true) {
                    const message = outcome && outcome.status
                        ? outcome.status
                        : "Member could not be saved. Confirm the API is running and try again.";
                    setProfileStatus(message, "error");
                    return { saved: false, message: message };
                }

                if (outcome.persisted === true) {
                    if (!outcome.profile) {
                        const message = "The member profile save did not return the saved values.";
                        setProfileStatus(message, "error");
                        return { saved: false, message: message };
                    }

                    applyPersistedProfileFields(outcome.profile);
                    if (changes) {
                        changes.commit(cardDetails, savedProfileControls(before));
                    }
                    publishConfirmed(before, true, false);
                } else {
                    applySuccessfulSave();
                    if (changes) {
                        changes.commit(cardDetails, before);
                    }
                    publishConfirmed(before, includeColor, true);
                }
                setProfileStatus(outcome.status || "Member saved.", "success");
                return { saved: true };
            } catch (error) {
                const message = "Member could not be saved. Confirm the API is running and try again.";
                setProfileStatus(message, "error");
                return { saved: false, message: message };
            } finally {
                setProfileBusy(false);
            }
        }

        const apiClient = window.PluralBridge && window.PluralBridge.apiClient
            ? window.PluralBridge.apiClient
            : null;

        if (!apiClient || typeof apiClient.editMember !== "function") {
            const message = "Member edit is not available in the current browser session.";
            setProfileStatus(message, "error");
            return { saved: false, message: message };
        }

        setProfileBusy(true);
        setProfileStatus("Saving member.");

        try {
            await apiClient.editMember(memberId, request);
            withoutChangeNotices(function () {
                persistedValues.displayName = draftValues.displayName;
                persistedValues.pronouns = draftValues.pronouns;
                persistedValues.description = draftValues.description;
                ["displayName", "pronouns", "description"].forEach(function (fieldName) {
                    if (!fieldRows[fieldName]) {
                        return;
                    }
                    fieldRows[fieldName].value.dataset.persistedValue = persistedValues[fieldName] || "";
                    fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
                    updateStaticFieldDisplay(fieldName);
                });
                if (member) {
                    member.displayName = persistedValues.displayName;
                    member.pronouns = persistedValues.pronouns;
                    member.description = persistedValues.description;
                }
                updateSummaryPreview();
            });
            if (changes) {
                changes.commit(cardDetails, savedProfileControls(before).filter(function (control) {
                    return control !== colorSwatch;
                }));
            }
            publishConfirmed(before, false, false);
            setProfileStatus("Member saved.", "success");
            return { saved: true };
        } catch (error) {
            const message = "Member could not be saved. Confirm the API is running and try again.";
            setProfileStatus(message, "error");
            return { saved: false, message: message };
        } finally {
            setProfileBusy(false);
        }
    }

    function captureCardView() {
        const positions = [];
        let node = cardDetails.parentElement;
        while (node) {
            if (node.scrollHeight > node.clientHeight || node.scrollWidth > node.clientWidth) {
                positions.push({
                    node: node,
                    top: node.scrollTop,
                    left: node.scrollLeft
                });
            }
            node = node.parentElement;
        }
        if (document.scrollingElement) {
            positions.push({
                node: document.scrollingElement,
                top: document.scrollingElement.scrollTop,
                left: document.scrollingElement.scrollLeft
            });
        }
        return {
            open: cardDetails.open,
            positions: positions
        };
    }

    function restoreCardView(view) {
        if (!view || !cardDetails.isConnected) {
            return;
        }
        if (view.open) {
            cardDetails.open = true;
        }
        view.positions.forEach(function (position) {
            if (!position.node || !position.node.isConnected) {
                return;
            }
            position.node.scrollTop = position.top;
            position.node.scrollLeft = position.left;
        });
    }

    const previewHeader = Boolean(options && options.previewHeader === true);

    function nullablePreviewValue(value) {
        if (value === true || value === false) {
            return value;
        }

        if (value === undefined || value === null || value === "") {
            return null;
        }

        return value;
    }

    function memberCssColor(value) {
        return typeof value === "string" && /^#[0-9A-Fa-f]{6}$/.test(value) ? value : null;
    }

    function storedMemberColor(value) {
        if (typeof value !== "string") {
            return null;
        }

        const trimmed = value.trim();
        return trimmed ? trimmed : null;
    }

    function generatedMemberColor() {
        const hue = hashStringToInt(getAvatarColorSeed()) % 360;
        return {
            fill: "hsl(" + hue + " 52% 28%)",
            border: "hsl(" + hue + " 56% 48%)"
        };
    }

    function effectiveMemberColor() {
        const explicit = memberCssColor(draftValues.color);
        return explicit
            ? { fill: explicit, border: explicit, explicit: true }
            : Object.assign({ explicit: false }, generatedMemberColor());
    }

    function profilePreviewSaveRequest(memberId, request) {
        const payload = {
            memberId: memberId,
            displayName: request.displayName,
            pronouns: request.pronouns,
            description: request.description
        };
        const changes = changeTracking();
        const marked = markedControls();

        if (changes && marked.indexOf(colorSwatch) >= 0) {
            payload.color = draftValues.color;
        }

        if (changes && marked.indexOf(frontingButton) >= 0) {
            payload.fronting = draftValues.fronting;
        }

        if (changes && marked.indexOf(sharingButton) >= 0) {
            payload.visibilityInherit = draftValues.visibilityInherit === true;
            payload.visibilityTypeId = draftValues.visibilityInherit === true ? null : draftValues.visibilityTypeId;
        }

        if (changes && permissionControl && marked.indexOf(permissionControl) >= 0) {
            payload.permission = draftValues.permission;
        }

        if (changes && marked.indexOf(avatarWrap) >= 0) {
            payload.avatarObjectUrl = draftValues.avatarObjectUrl;
        }

        return payload;
    }

    function canonicalVisibilityId(value) {
        return value === 1 || value === 2 || value === 3 || value === 4 ? value : null;
    }

    const draftValues = {
        displayName: member && member.displayName ? member.displayName : "",
        pronouns: member && member.pronouns ? member.pronouns : "",
        description: member && member.description ? member.description : "",
        color: storedMemberColor(member && member.color),
        fronting: member && (member.fronting === true || member.fronting === false) ? member.fronting : null,
        visibilityKnown: Boolean(member && member.visibilityKnown === true),
        visibilityInherit: Boolean(member && member.visibilityInherit === true),
        visibilityTypeId: canonicalVisibilityId(member && member.visibilityTypeId),
        permission: member && member.permission ? member.permission : null,
        avatarObjectUrl: nullablePreviewValue(member && member.avatarObjectUrl)
    };

    const persistedValues = {
        displayName: draftValues.displayName,
        pronouns: draftValues.pronouns,
        description: draftValues.description,
        color: draftValues.color,
        fronting: draftValues.fronting,
        visibilityKnown: draftValues.visibilityKnown,
        visibilityInherit: draftValues.visibilityInherit,
        visibilityTypeId: draftValues.visibilityTypeId,
        permission: draftValues.permission,
        avatarObjectUrl: draftValues.avatarObjectUrl
    };

    const fieldLabels = {
        displayName: "Name",
        pronouns: "Pronouns",
        description: "Description"
    };

    const fieldRows = {};
    let activeEdit = null;
    let isSaving = false;
    let unsavedIndicator = null;
    let detachChanges = null;
    let profileControls = [];
    let permissionControl = null;

    const cardDetails = document.createElement("details");
    cardDetails.className = previewHeader ? "member-card member-card-preview" : "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const summaryMain = document.createElement("span");
    summaryMain.className = "member-summary-main";

    const colorStrip = document.createElement("span");
    colorStrip.className = "member-color-strip";
    colorStrip.setAttribute("aria-hidden", "true");

    const avatarWrap = document.createElement("span");
    avatarWrap.className = "member-avatar-wrap";
    avatarWrap.setAttribute("data-change-control", "");
    avatarWrap.setAttribute("data-change-name", "Choose an avatar image");

    const avatarPlaceholder = document.createElement("span");
    avatarPlaceholder.className = "member-avatar-placeholder";
    avatarPlaceholder.setAttribute("aria-hidden", "true");

    const avatarPencil = document.createElement("button");
    avatarPencil.type = "button";
    avatarPencil.className = "member-avatar-pencil";
    avatarPencil.setAttribute("aria-label", "Choose an avatar image");
    avatarPencil.title = "Choose an avatar image";
    avatarPencil.appendChild(createMemberActionIcon("edit"));

    const avatarFile = document.createElement("input");
    avatarFile.type = "file";
    avatarFile.accept = "image/*";
    avatarFile.tabIndex = -1;
    avatarFile.className = "member-avatar-file";
    avatarFile.setAttribute("aria-label", "Choose an avatar image");

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = allowProfileEditing
        ? getProfileDisplayText("displayName")
        : (draftValues.displayName || "");

    const pronouns = document.createElement("span");
    pronouns.className = "member-pronouns";
    pronouns.textContent = draftValues.pronouns || "";

    const headerControls = document.createElement("span");
    headerControls.className = "member-summary-controls";

    const sharingLabel = document.createElement("span");
    sharingLabel.className = "member-sharing-label";
    sharingLabel.appendChild(document.createTextNode("Visible to:"));

    const sharingControl = document.createElement("span");
    sharingControl.className = "member-sharing-control";
    const sharingButton = document.createElement("button");
    sharingButton.type = "button";
    sharingButton.className = "member-card-sharing";
    sharingButton.setAttribute("data-change-control", "");
    sharingButton.setAttribute("data-change-name", "Visibility");
    sharingButton.setAttribute("aria-label", "Visibility");
    sharingButton.setAttribute("aria-haspopup", "listbox");
    sharingButton.setAttribute("aria-expanded", "false");
    const sharingList = document.createElement("div");
    sharingList.className = "member-sharing-list";
    sharingList.setAttribute("role", "listbox");
    sharingList.hidden = true;
    window.PluralBridge = window.PluralBridge || {};
    window.PluralBridge.memberSharingSequence = (window.PluralBridge.memberSharingSequence || 0) + 1;
    sharingList.id = "member-sharing-list-" + window.PluralBridge.memberSharingSequence;
    sharingButton.setAttribute("aria-controls", sharingList.id);
    let sharingOptions = [];
    sharingControl.appendChild(sharingButton);
    sharingControl.appendChild(sharingList);

    const frontingButton = document.createElement("button");
    frontingButton.type = "button";
    frontingButton.className = "member-card-fronting";
    frontingButton.setAttribute("data-change-control", "");
    frontingButton.setAttribute("data-change-name", "Fronting");

    const chevron = document.createElement("span");
    chevron.className = "member-card-expand-icon member-tree-chevron";
    chevron.setAttribute("aria-hidden", "true");
    const chevronSvgNamespace = "http://www.w3.org/2000/svg";
    const chevronSvg = document.createElementNS(chevronSvgNamespace, "svg");
    chevronSvg.setAttribute("viewBox", "0 0 24 24");
    chevronSvg.setAttribute("width", "16");
    chevronSvg.setAttribute("height", "16");
    chevronSvg.setAttribute("aria-hidden", "true");
    chevronSvg.setAttribute("focusable", "false");
    const chevronPath = document.createElementNS(chevronSvgNamespace, "path");
    chevronPath.setAttribute("d", "M9 5.5 16.5 12 9 18.5");
    chevronPath.setAttribute("fill", "none");
    chevronPath.setAttribute("stroke", "currentColor");
    chevronPath.setAttribute("stroke-width", "1.75");
    chevronPath.setAttribute("stroke-linecap", "round");
    chevronPath.setAttribute("stroke-linejoin", "round");
    chevronSvg.appendChild(chevronPath);
    chevron.appendChild(chevronSvg);
    if (!previewHeader) {
        chevron.className = "member-chevron";
        chevron.replaceChildren();
        chevron.textContent = "v";
    }

    identity.appendChild(name);
    identity.appendChild(pronouns);
    unsavedIndicator = document.createElement("span");
    unsavedIndicator.className = "member-card-unsaved-indicator";
    unsavedIndicator.textContent = "Unsaved changes";
    unsavedIndicator.hidden = true;
    identity.appendChild(unsavedIndicator);
    avatarWrap.appendChild(avatarPlaceholder);
    if (previewHeader) {
        avatarWrap.appendChild(avatarPencil);
        avatarWrap.appendChild(avatarFile);
    }
    summaryMain.appendChild(avatarWrap);
    summaryMain.appendChild(identity);
    sharingLabel.appendChild(sharingControl);
    headerControls.appendChild(sharingLabel);
    if (previewHeader && window.PluralBridge.members && typeof window.PluralBridge.members.createPolicyDropdown === "function" && typeof window.PluralBridge.members.policyChoices === "function") {
        const permissionLabel = document.createElement("span");
        permissionLabel.className = "member-sharing-label";
        permissionLabel.appendChild(document.createTextNode("Permissions:"));
        const permissionMenu = window.PluralBridge.members.createPolicyDropdown({
            label: "Permissions",
            choices: window.PluralBridge.members.policyChoices("permission", true),
            getValue: function () {
                return draftValues.permission || "";
            },
            setValue: function (next) {
                const permission = next ? String(next) : "";
                if (permission === (draftValues.permission || "")) {
                    return;
                }
                draftValues.permission = permission || null;
                notifyEdited(permissionControl);
                updateSaveButtonState();
            }
        });
        permissionMenu.root.classList.add("member-card-permission");
        permissionControl = permissionMenu.root.matches && permissionMenu.root.matches("input, select, textarea")
            ? permissionMenu.root
            : (permissionMenu.root.querySelector("input, select, textarea, .member-policy-button") || permissionMenu.root);
        permissionControl.setAttribute("data-change-control", "");
        permissionControl.setAttribute("data-change-name", "Permissions");
        permissionControl.setAttribute("aria-label", "Permissions");
        permissionLabel.appendChild(permissionMenu.root);
        permissionLabel.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        permissionLabel.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
        });
        headerControls.appendChild(permissionLabel);
        cardDetails.paintPermission = permissionMenu.refresh;
    }
    headerControls.appendChild(frontingButton);
    headerControls.appendChild(chevron);

    if (previewHeader) {
        cardDetails.appendChild(colorStrip);
    }
    summary.appendChild(summaryMain);
    if (previewHeader) {
        summary.appendChild(headerControls);
    } else {
        summary.appendChild(chevron);
    }

    let keepSavedCardOpen = false;
    let pendingSavedCardView = null;
    let activeSavedCardView = null;
    let savedCardHoldToken = 0;
    let paintingColor = false;
    let colorEcho = 0;
    let acceptColorEdits = false;

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

    const colorRow = document.createElement("div");
    colorRow.className = "member-profile-color-row";
    const colorLabel = document.createElement("span");
    colorLabel.className = "member-profile-field-label";
    colorLabel.textContent = previewHeader ? "Member color" : "Color";
    const colorSwatch = document.createElement("span");
    colorSwatch.className = "member-color-swatch";
    const colorInput = document.createElement("input");
    colorInput.type = "color";
    colorInput.className = "member-color-input";
    colorInput.value = "#94a3b8";
    colorLabel.id = "member-color-label-" + window.PluralBridge.memberSharingSequence;
    colorInput.setAttribute("aria-labelledby", colorLabel.id);
    colorSwatch.setAttribute("data-change-control", "");
    colorSwatch.setAttribute("data-change-name", colorLabel.textContent);
    colorSwatch.appendChild(colorInput);
    const colorPencil = document.createElement("button");
    colorPencil.type = "button";
    colorPencil.className = "member-profile-field-edit-button member-color-pencil";
    colorPencil.setAttribute("aria-label", "Edit color");
    colorPencil.title = "Member color is not loaded. Choosing a color stores a preview only.";
    colorPencil.appendChild(createMemberActionIcon("edit"));
    colorRow.appendChild(colorLabel);
    colorRow.appendChild(colorSwatch);
    colorRow.appendChild(colorPencil);
    if (previewHeader) {
        const colorHelp = document.createElement("p");
        colorHelp.className = "member-color-help";
        colorHelp.textContent = "Used to identify this member throughout the app.";
        colorRow.appendChild(colorHelp);
    }

    const profileActions = document.createElement("div");
    profileActions.className = "member-profile-action-row";

    const saveButton = allowProfileEditing ? createProfileSaveButton() : null;
    cancelButton = allowProfileEditing ? createProfileCancelButton() : null;
    if (cancelButton) {
        profileActions.appendChild(cancelButton);
    }
    if (saveButton) {
        profileActions.appendChild(saveButton);
    }

    const profileStatus = document.createElement("p");
    profileStatus.className = "member-form-status";
    profileStatus.setAttribute("aria-live", "polite");
    profileStatus.textContent = "";

    profilePanel.appendChild(profileHeading);
    profilePanel.appendChild(profileFields);
    if (previewHeader) {
        const colorFooter = document.createElement("div");
        colorFooter.className = "member-profile-color-footer";
        colorFooter.appendChild(colorRow);
        if (allowProfileEditing) {
            colorFooter.appendChild(profileActions);
        }
        profilePanel.appendChild(colorFooter);
    } else if (allowProfileEditing) {
        profilePanel.appendChild(profileActions);
    }
    profilePanel.appendChild(profileStatus);

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

    cardDetails.addEventListener("toggle", function () {
        if (cardDetails.dataset.pbLeave) {
            const holdOpen = cardDetails.dataset.pbLeave === "1";
            if (cardDetails.open !== holdOpen) {
                cardDetails.open = holdOpen;
            }
            return;
        }

        if (cardIsDirty() && !cardDetails.open) {
            cardDetails.open = true;
            return;
        }

        if (keepSavedCardOpen && !cardDetails.open) {
            cardDetails.open = true;
            if (activeSavedCardView) {
                activeSavedCardView.open = true;
                restoreCardView(activeSavedCardView);
            }
            return;
        }

        if (previewHeader) {
            chevron.classList.toggle("is-expanded", cardDetails.open);
        }
    });

    function releaseUnsavedAvatar() {
        if (
            draftValues.avatarObjectUrl &&
            draftValues.avatarObjectUrl !== persistedValues.avatarObjectUrl &&
            typeof URL.revokeObjectURL === "function"
        ) {
            URL.revokeObjectURL(draftValues.avatarObjectUrl);
        }
    }

    function paintAvatar() {
        const image = avatarWrap.querySelector(".member-avatar-image");
        if (image) {
            image.remove();
        }

        if (!previewHeader) {
            const generated = generatedMemberColor();
            avatarPlaceholder.hidden = false;
            avatarPlaceholder.style.backgroundColor = generated.fill;
            avatarPlaceholder.style.borderColor = generated.border;
            return;
        }

        const appearance = effectiveMemberColor();
        avatarPlaceholder.style.backgroundColor = appearance.fill;
        avatarPlaceholder.style.borderColor = appearance.border;
        avatarPlaceholder.classList.remove("is-neutral");
        if (!draftValues.avatarObjectUrl) {
            avatarPlaceholder.hidden = false;
            return;
        }

        avatarPlaceholder.hidden = true;
        const preview = document.createElement("img");
        preview.className = "member-avatar-image";
        preview.alt = "";
        preview.src = draftValues.avatarObjectUrl;
        avatarWrap.insertBefore(preview, avatarPencil);
    }

    function paintColor() {
        const appearance = effectiveMemberColor();
        colorStrip.style.backgroundColor = appearance.fill;
        colorStrip.classList.toggle("is-generated", !appearance.explicit);
        if (appearance.explicit) {
            colorStrip.removeAttribute("title");
            colorStrip.removeAttribute("aria-label");
            colorStrip.setAttribute("aria-hidden", "true");
        } else {
            colorStrip.title = "Generated color—no member color assigned.";
            colorStrip.setAttribute("aria-label", "Generated color—no member color assigned.");
            colorStrip.removeAttribute("aria-hidden");
        }
        colorSwatch.style.backgroundColor = appearance.fill;
        colorSwatch.style.borderColor = appearance.border;
        colorSwatch.title = appearance.explicit
            ? "Edit color"
            : "No stored member color. This color is generated for display. Choosing a color stores a preview only.";
        colorPencil.title = colorSwatch.title;
        if (appearance.explicit && colorInput.value.toLowerCase() !== appearance.fill.toLowerCase()) {
            withoutChangeNotices(function () {
                colorEcho += 1;
                paintingColor = true;
                colorInput.value = appearance.fill;
                paintingColor = false;
            });
            window.setTimeout(function () {
                colorEcho = Math.max(0, colorEcho - 1);
            }, 0);
        }
    }

    function commitPickerColor() {
        if (!acceptColorEdits || paintingColor || colorEcho || isSaving) {
            return;
        }

        const nextColor = memberCssColor(colorInput.value);
        draftValues.color = nextColor;
        paintColor();
        updateSaveButtonState();
    }

    function paintFronting() {
        frontingButton.replaceChildren();
        const svgNamespace = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(svgNamespace, "svg");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("width", "20");
        svg.setAttribute("height", "20");
        svg.setAttribute("fill", "none");
        svg.setAttribute("stroke", "currentColor");
        svg.setAttribute("stroke-width", "1.8");
        svg.setAttribute("stroke-linecap", "round");
        svg.setAttribute("stroke-linejoin", "round");
        svg.setAttribute("aria-hidden", "true");
        const person = document.createElementNS(svgNamespace, "path");
        person.setAttribute("d", "M9 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM4.5 19v-1.2A3.8 3.8 0 0 1 8.3 14h1.4a3.8 3.8 0 0 1 3.8 3.8V19");
        const arrow = document.createElementNS(svgNamespace, "path");
        arrow.setAttribute("d", draftValues.fronting === true ? "M17 19V8M14 11l3-3 3 3" : "M17 5v11M14 13l3 3 3-3");
        svg.appendChild(person);
        svg.appendChild(arrow);
        frontingButton.appendChild(svg);
        const label = document.createElement("span");
        label.className = "member-fronting-label";
        if (draftValues.fronting === null) {
            label.textContent = "Not loaded";
            frontingButton.setAttribute("aria-pressed", "mixed");
            frontingButton.title = "Fronting is not loaded. Activate to mark this member as fronting in this preview.";
        } else if (draftValues.fronting) {
            label.textContent = "Fronting";
            frontingButton.setAttribute("aria-pressed", "true");
            frontingButton.title = "Fronting. This is the imported fronting snapshot.";
        } else {
            label.textContent = "Not fronting";
            frontingButton.setAttribute("aria-pressed", "false");
            frontingButton.title = "Not fronting. This is the imported fronting snapshot.";
        }
        frontingButton.setAttribute("aria-label", frontingButton.title);
        frontingButton.appendChild(label);
    }

    function visibilityChoices() {
        const choices = [];
        if (!draftValues.visibilityKnown) {
            choices.push({ id: null, known: false, label: "Not loaded", icon: "unset", disabled: false });
        } else {
            choices.push({ id: null, known: true, label: "Not set", icon: "unset", inherit: false, disabled: false });
        }

        choices.push(
            { id: null, known: true, label: "Inherit", icon: "inherit", inherit: true, disabled: false },
            { id: 1, known: true, label: "Only you", icon: "only-me", disabled: false },
            { id: 2, known: true, label: "Trusted friends", icon: "selected-people", disabled: false },
            { id: 3, known: true, label: "All friends", icon: "all-connections", disabled: false },
            { id: 4, known: true, label: "Selected people", icon: "selected-people", disabled: true }
        );
        return choices;
    }

    function currentSharingChoice() {
        const choices = visibilityChoices();
        if (!draftValues.visibilityKnown) {
            return choices[0];
        }

        if (draftValues.visibilityInherit === true) {
            return choices.find(function (choice) {
                return choice.inherit === true;
            }) || choices[0];
        }

        const selected = choices.find(function (choice) {
            return choice.known && choice.inherit !== true && choice.id === draftValues.visibilityTypeId;
        });
        return selected || choices[0];
    }

    function paintSharing() {
        const choices = visibilityChoices();
        const choice = currentSharingChoice();
        const selectedIndex = choices.indexOf(choice);
        sharingList.replaceChildren();
        sharingOptions = choices.map(function (item, index) {
            const option = document.createElement("div");
            option.className = "member-sharing-option";
            option.setAttribute("role", "option");
            option.tabIndex = -1;
            option.id = sharingList.id + "-option-" + index;
            option.dataset.visibilityId = item.id === null ? "" : String(item.id);
            option.dataset.visibilityKnown = item.known ? "true" : "false";
            option.dataset.visibilityInherit = item.inherit === true ? "true" : "false";
            option.dataset.sharingIndex = String(index);
            if (item.disabled) {
                option.setAttribute("aria-disabled", "true");
            }
            option.appendChild(sharingOutlineIcon(item.icon));
            const optionLabel = document.createElement("span");
            optionLabel.textContent = item.label;
            option.appendChild(optionLabel);
            const selected = index === selectedIndex;
            option.setAttribute("aria-selected", selected ? "true" : "false");
            option.classList.toggle("is-current", selected);
            sharingList.appendChild(option);
            return option;
        });
        sharingButton.replaceChildren();
        sharingButton.appendChild(sharingOutlineIcon(choice.icon));
        const value = document.createElement("span");
        value.className = "member-sharing-value";
        value.textContent = choice.label;
        sharingButton.appendChild(value);
        const menuChevron = document.createElement("span");
        menuChevron.className = "member-sharing-chevron" + (sharingList.hidden ? "" : " is-open");
        menuChevron.setAttribute("aria-hidden", "true");
        menuChevron.appendChild(sharingOutlineIcon("chevron"));
        sharingButton.appendChild(menuChevron);
        if (!draftValues.visibilityKnown) {
            sharingButton.title = "Visibility is not loaded. Choosing a value stores a preview only.";
        } else if (draftValues.visibilityInherit === true) {
            sharingButton.title = "Preview only. Inherit does not change stored visibility.";
        } else if (draftValues.visibilityTypeId === null) {
            sharingButton.title = "Visibility is not set. Choosing a value stores a preview only.";
        } else {
            sharingButton.title = "Preview only. This does not change who can see the member.";
        }
        sharingButton.setAttribute("aria-label", "Visible to: " + choice.label);
    }

    function placeSharingList() {
        if (sharingList.hidden) {
            return;
        }

        const rect = sharingButton.getBoundingClientRect();
        const listHeight = sharingList.offsetHeight;
        let top = rect.bottom + 4;
        if (top + listHeight > window.innerHeight - 8) {
            top = Math.max(8, rect.top - listHeight - 4);
        }
        sharingList.style.left = Math.max(8, rect.left) + "px";
        sharingList.style.top = top + "px";
        sharingList.style.width = rect.width + "px";
    }

    function setSharingOpen(open) {
        sharingList.hidden = !open;
        sharingButton.setAttribute("aria-expanded", open ? "true" : "false");
        cardDetails.classList.toggle("is-sharing-open", open);
        paintSharing();
        if (!open) {
            sharingList.removeAttribute("aria-activedescendant");
            return;
        }

        const selected = sharingOptions.findIndex(function (option) {
            return option.getAttribute("aria-selected") === "true";
        });
        const index = selected < 0 ? 0 : selected;
        sharingOptions[index].focus();
        sharingList.setAttribute("aria-activedescendant", sharingOptions[index].id);
        placeSharingList();
    }

    function chooseSharing(option) {
        if (!option || option.getAttribute("aria-disabled") === "true") {
            if (option && option.getAttribute("aria-disabled") === "true") {
                setSharingOpen(false);
                sharingButton.focus();
            }
            return;
        }

        if (option.dataset.visibilityKnown !== "true") {
            setSharingOpen(false);
            sharingButton.focus();
            return;
        }

        const raw = option.dataset.visibilityId || "";
        draftValues.visibilityKnown = true;
        draftValues.visibilityInherit = option.dataset.visibilityInherit === "true";
        draftValues.visibilityTypeId = draftValues.visibilityInherit || !raw
            ? null
            : canonicalVisibilityId(Number(raw));
        notifyEdited(sharingButton);
        setSharingOpen(false);
        updateSaveButtonState();
        sharingButton.focus();
    }

    function enabledSharingIndex(start, step) {
        const count = sharingOptions.length;
        if (!count) {
            return 0;
        }

        let index = start;
        for (let stepCount = 0; stepCount < count; stepCount += 1) {
            index = step > 0
                ? Math.min(count - 1, index + step)
                : Math.max(0, index + step);
            if (sharingOptions[index].getAttribute("aria-disabled") !== "true") {
                return index;
            }
            if ((step > 0 && index === count - 1) || (step < 0 && index === 0)) {
                break;
            }
        }

        return start;
    }

    function dismissSharing(event) {
        if (sharingList.hidden || sharingControl.contains(event.target)) {
            return;
        }

        setSharingOpen(false);
    }

    function paintPreviewControls() {
        paintAvatar();
        if (!previewHeader) {
            return;
        }
        paintColor();
        paintFronting();
        paintSharing();
        if (typeof cardDetails.paintPermission === "function") {
            cardDetails.paintPermission();
        }
    }

    function keepHeaderClick(event) {
        event.preventDefault();
        event.stopPropagation();
    }

    if (previewHeader) {
        function openAvatarChooser(event) {
            keepHeaderClick(event);
            avatarFile.click();
        }
        avatarPencil.addEventListener("mousedown", keepHeaderClick);
        avatarPencil.addEventListener("click", openAvatarChooser);
        avatarWrap.addEventListener("mousedown", function (event) {
            if (event.target === avatarFile) {
                return;
            }
            event.stopPropagation();
        });
        avatarWrap.addEventListener("click", function (event) {
            if (event.target === avatarFile || event.target === avatarPencil || avatarPencil.contains(event.target)) {
                return;
            }
            openAvatarChooser(event);
        });
        avatarFile.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        avatarFile.addEventListener("change", function () {
            const file = avatarFile.files && avatarFile.files[0] ? avatarFile.files[0] : null;
            if (!file) {
                return;
            }
            releaseUnsavedAvatar();
            draftValues.avatarObjectUrl = URL.createObjectURL(file);
            notifyEdited(avatarWrap);
            avatarFile.value = "";
            paintAvatar();
            updateSaveButtonState();
        });
        sharingLabel.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        sharingLabel.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        sharingControl.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        sharingControl.addEventListener("click", function (event) {
            event.stopPropagation();
        });
        sharingButton.addEventListener("click", function (event) {
            keepHeaderClick(event);
            setSharingOpen(sharingList.hidden);
        });
        sharingButton.addEventListener("keydown", function (event) {
            if (event.key === "ArrowDown" || event.key === "ArrowUp" || event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                event.stopPropagation();
                if (sharingList.hidden) {
                    setSharingOpen(true);
                }
                return;
            }

            if (event.key === "Escape" && !sharingList.hidden) {
                event.preventDefault();
                event.stopPropagation();
                setSharingOpen(false);
            }
        });
        sharingList.addEventListener("mousedown", function (event) {
            if (event.target.closest(".member-sharing-option")) {
                event.preventDefault();
                event.stopPropagation();
            }
        });
        sharingList.addEventListener("click", function (event) {
            const option = event.target.closest(".member-sharing-option");
            if (!option) {
                return;
            }

            keepHeaderClick(event);
            chooseSharing(option);
        });
        sharingList.addEventListener("keydown", function (event) {
            const current = Number(document.activeElement && document.activeElement.dataset
                ? document.activeElement.dataset.sharingIndex
                : NaN);
            if (event.key === "ArrowDown" || event.key === "ArrowUp" || event.key === "Home" || event.key === "End") {
                event.preventDefault();
                event.stopPropagation();
                const start = Number.isNaN(current) ? 0 : current;
                let next = start;
                if (event.key === "ArrowDown") {
                    next = enabledSharingIndex(next, 1);
                } else if (event.key === "ArrowUp") {
                    next = enabledSharingIndex(next, -1);
                } else if (event.key === "Home") {
                    next = enabledSharingIndex(-1, 1);
                } else {
                    next = enabledSharingIndex(sharingOptions.length, -1);
                }
                if (!sharingOptions[next]) {
                    return;
                }
                sharingOptions[next].focus();
                sharingList.setAttribute("aria-activedescendant", sharingOptions[next].id);
                return;
            }

            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                event.stopPropagation();
                chooseSharing(document.activeElement);
                return;
            }

            if (event.key === "Escape") {
                event.preventDefault();
                event.stopPropagation();
                setSharingOpen(false);
                sharingButton.focus();
                return;
            }

            if (event.key === "Tab") {
                setSharingOpen(false);
            }
        });
        document.addEventListener("pointerdown", dismissSharing, true);
        window.addEventListener("resize", placeSharingList);
        window.addEventListener("scroll", placeSharingList, true);
        frontingButton.addEventListener("mousedown", function (event) {
            event.stopPropagation();
        });
        frontingButton.addEventListener("click", function (event) {
            keepHeaderClick(event);
            draftValues.fronting = draftValues.fronting === null ? true : !draftValues.fronting;
            notifyEdited(frontingButton);
            paintFronting();
            updateSaveButtonState();
        });
        colorPencil.addEventListener("click", function () {
            colorInput.click();
        });
        colorSwatch.addEventListener("click", function () {
            colorInput.click();
        });
        colorInput.addEventListener("input", commitPickerColor);
        colorInput.addEventListener("change", commitPickerColor);

        queueMicrotask(function () {
            if (!cardDetails.isConnected || !document.body) {
                return;
            }

            const removalObserver = new MutationObserver(function () {
                if (!cardDetails.isConnected) {
                    releaseUnsavedAvatar();
                    if (typeof detachChanges === "function") {
                        detachChanges();
                        detachChanges = null;
                    }
                    document.removeEventListener("pointerdown", dismissSharing, true);
                    window.removeEventListener("resize", placeSharingList);
                    window.removeEventListener("scroll", placeSharingList, true);
                    removalObserver.disconnect();
                }
            });
            removalObserver.observe(document.body, { childList: true, subtree: true });
        });
    }

    paintPreviewControls();
    updateSaveButtonState();
    acceptColorEdits = true;

    function surfaceIsModified(control) {
        const marker = modifiedMarker();
        return !!(control && control.classList && control.classList.contains(marker));
    }

    function textFieldIsModified(fieldName) {
        const rowInfo = fieldRows[fieldName];
        if (rowInfo && surfaceIsModified(rowInfo.value)) {
            return true;
        }
        return !!(activeEdit && activeEdit.fieldName === fieldName && surfaceIsModified(activeEdit.editor));
    }

    function applyExternal(fields) {
        if (!fields) {
            return;
        }

        const refreshControls = [];
        ["displayName", "pronouns", "description"].forEach(function (fieldName) {
            if (!Object.prototype.hasOwnProperty.call(fields, fieldName) || !fieldRows[fieldName] || textFieldIsModified(fieldName)) {
                return;
            }

            persistedValues[fieldName] = fields[fieldName];
            draftValues[fieldName] = fields[fieldName];
            fieldRows[fieldName].value.dataset.persistedValue = persistedValues[fieldName] || "";
            fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
            updateStaticFieldDisplay(fieldName);
            if (member && Object.prototype.hasOwnProperty.call(member, fieldName)) {
                member[fieldName] = persistedValues[fieldName];
            }
            if (activeEdit && activeEdit.fieldName === fieldName) {
                activeEdit.editor.value = draftValues[fieldName] || "";
                if (fieldRows[fieldName].isLongText) {
                    fitLongEditor(activeEdit.editor);
                }
                refreshControls.push(activeEdit.editor);
            }
            refreshControls.push(fieldRows[fieldName].value);
        });

        if (Object.prototype.hasOwnProperty.call(fields, "color") && !surfaceIsModified(colorSwatch) && !surfaceIsModified(colorInput)) {
            persistedValues.color = fields.color;
            draftValues.color = fields.color;
            if (member) {
                member.color = persistedValues.color;
            }
            refreshControls.push(colorSwatch, colorInput);
        }

        if (Object.prototype.hasOwnProperty.call(fields, "fronting") && !surfaceIsModified(frontingButton)) {
            persistedValues.fronting = fields.fronting;
            draftValues.fronting = fields.fronting;
            if (member) {
                member.fronting = persistedValues.fronting;
            }
            refreshControls.push(frontingButton);
        }

        if (Object.prototype.hasOwnProperty.call(fields, "permission") && !surfaceIsModified(permissionControl)) {
            persistedValues.permission = fields.permission;
            draftValues.permission = fields.permission;
            if (permissionControl) {
                refreshControls.push(permissionControl);
            }
        }

        if (Object.prototype.hasOwnProperty.call(fields, "avatarObjectUrl") && !surfaceIsModified(avatarWrap) && !surfaceIsModified(avatarFile)) {
            persistedValues.avatarObjectUrl = fields.avatarObjectUrl;
            draftValues.avatarObjectUrl = fields.avatarObjectUrl;
            if (member) {
                member.avatarObjectUrl = persistedValues.avatarObjectUrl;
            }
            refreshControls.push(avatarWrap, avatarFile);
        }

        const visibilitySupplied = ["visibilityKnown", "visibilityInherit", "visibilityTypeId"].some(function (key) {
            return Object.prototype.hasOwnProperty.call(fields, key);
        });
        if (visibilitySupplied && !surfaceIsModified(sharingButton)) {
            ["visibilityKnown", "visibilityInherit", "visibilityTypeId"].forEach(function (key) {
                if (Object.prototype.hasOwnProperty.call(fields, key)) {
                    persistedValues[key] = fields[key];
                    draftValues[key] = fields[key];
                }
            });
            if (member && Object.prototype.hasOwnProperty.call(fields, "visibilityKnown")) {
                member.visibilityKnown = persistedValues.visibilityKnown;
            }
            if (member && Object.prototype.hasOwnProperty.call(fields, "visibilityTypeId")) {
                member.visibilityTypeId = persistedValues.visibilityTypeId;
            }
            refreshControls.push(sharingButton);
        }

        updateSummaryPreview();
        paintPreviewControls();
        const changes = changeTracking();
        if (changes) {
            changes.refreshSnapshots(cardDetails, refreshControls);
        }
    }

    cardDetails.addEventListener("click", function (event) {
        const changes = changeTracking();
        if (!changes) {
            return;
        }

        if (changes.blocks(cardDetails)) {
            event.preventDefault();
            event.stopPropagation();
            const wasOpen = cardDetails.open;
            cardDetails.dataset.pbLeave = wasOpen ? "1" : "0";
            changes.requestLeave(function () {
                delete cardDetails.dataset.pbLeave;
                if (!cardDetails.isConnected) {
                    return;
                }
                cardDetails.open = true;
                window.setTimeout(function () {
                    if (cardDetails.isConnected && !cardDetails.dataset.pbLeave) {
                        cardDetails.open = true;
                    }
                }, 0);
            }).then(function () {
                delete cardDetails.dataset.pbLeave;
            });
            return;
        }

        const avatarSurface = event.target && event.target.closest
            ? event.target.closest(".member-avatar-wrap")
            : null;
        if (avatarSurface && cardDetails.contains(avatarSurface)) {
            const fileTarget = event.target.closest ? event.target.closest("input[type='file']") : null;
            if (!fileTarget) {
                event.preventDefault();
            }
            return;
        }

        const control = event.target && event.target.closest
            ? event.target.closest("button, input, textarea, select, a, [role='option']")
            : null;
        if (control && cardDetails.contains(control)) {
            return;
        }

        const summaryHit = event.target && event.target.closest
            ? event.target.closest("summary")
            : null;
        if (summaryHit && cardDetails.contains(summaryHit) && cardDetails.open && changes.dirty(cardDetails)) {
            event.preventDefault();
            event.stopPropagation();
            changes.requestLeave(function () {
                if (cardDetails.isConnected) {
                    cardDetails.open = true;
                }
            });
        }
    }, true);

    cardDetails.addEventListener("focusin", function (event) {
        const changes = changeTracking();
        if (!changes || !changes.blocks(cardDetails) || changes.hasPending()) {
            return;
        }

        if (event.target && typeof event.target.blur === "function") {
            event.target.blur();
        }
        changes.requestLeave();
    }, true);

    profileControls = [
        fieldRows.displayName.value,
        fieldRows.pronouns.value,
        fieldRows.description.value,
        colorSwatch
    ];

    function profileFieldName(control) {
        if (!control) {
            return "";
        }
        if (control.dataset && control.dataset.memberProfileValue && fieldRows[control.dataset.memberProfileValue]) {
            return control.dataset.memberProfileValue;
        }
        const fieldRow = control.closest ? control.closest("[data-member-profile-field]") : null;
        const fieldName = fieldRow && fieldRow.dataset.memberProfileField;
        return fieldName && fieldRows[fieldName] ? fieldName : "";
    }

    function readTracked(control) {
        const fieldName = profileFieldName(control);
        if (fieldName) {
            return draftValues[fieldName];
        }
        if (control === colorSwatch || control === colorInput) {
            return draftValues.color;
        }
        if (control === frontingButton) {
            return draftValues.fronting;
        }
        if (control === sharingButton) {
            return {
                visibilityKnown: draftValues.visibilityKnown,
                visibilityInherit: draftValues.visibilityInherit,
                visibilityTypeId: draftValues.visibilityTypeId
            };
        }
        if (permissionControl && control === permissionControl) {
            return draftValues.permission;
        }
        if (control === avatarWrap || control === avatarFile) {
            return draftValues.avatarObjectUrl;
        }
        if (control.type === "checkbox" || control.type === "radio") {
            return control.checked;
        }
        return control.value;
    }

    function writeTracked(control, value) {
        const fieldName = profileFieldName(control);
        if (fieldName) {
            draftValues[fieldName] = value || "";
            if (activeEdit && activeEdit.fieldName === fieldName) {
                closeActiveEditor(false);
            }
            fieldRows[fieldName].value.dataset.draftValue = draftValues[fieldName] || "";
            updateStaticFieldDisplay(fieldName);
            if (member && Object.prototype.hasOwnProperty.call(member, fieldName)) {
                member[fieldName] = draftValues[fieldName];
            }
            updateSummaryPreview();
            return;
        }
        if (control === colorSwatch || control === colorInput) {
            draftValues.color = value;
            if (member) {
                member.color = value;
            }
            paintColor();
            return;
        }
        if (control === frontingButton) {
            draftValues.fronting = value === true || value === false ? value : null;
            if (member) {
                member.fronting = draftValues.fronting;
            }
            paintFronting();
            return;
        }
        if (control === sharingButton && value) {
            draftValues.visibilityKnown = value.visibilityKnown === true;
            draftValues.visibilityInherit = value.visibilityInherit === true;
            draftValues.visibilityTypeId = value.visibilityTypeId;
            if (member) {
                member.visibilityKnown = draftValues.visibilityKnown;
                member.visibilityTypeId = draftValues.visibilityTypeId;
            }
            paintSharing();
            return;
        }
        if (permissionControl && control === permissionControl) {
            draftValues.permission = value || null;
            if (typeof cardDetails.paintPermission === "function") {
                cardDetails.paintPermission();
            }
            return;
        }
        if (control === avatarWrap || control === avatarFile) {
            releaseUnsavedAvatar();
            draftValues.avatarObjectUrl = value || null;
            if (member) {
                member.avatarObjectUrl = draftValues.avatarObjectUrl;
            }
            paintAvatar();
            return;
        }
        if (control.type === "checkbox" || control.type === "radio") {
            control.checked = !!value;
            return;
        }
        if (control.type !== "file" && value !== undefined && value !== null) {
            control.value = value;
        }
    }

    const changeOwner = changeTracking();
    if (changeOwner) {
        detachChanges = changeOwner.attach({
            container: cardDetails,
            memberId: getMemberIdForSave(),
            save: saveProfileChanges,
            read: readTracked,
            write: writeTracked,
            persists: function (control) {
                return profileControls.indexOf(control) >= 0 || !!profileFieldName(control);
            },
            applyExternal: applyExternal,
            onChanged: renderChangeState
        });
        changeOwner.capture(cardDetails);
    }

    return cardDetails;
}

window.PluralBridge = window.PluralBridge || {};
window.PluralBridge.createMemberAccordion = createMemberAccordion;

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
    heading.textContent = "Plural map navigator";

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
        if (state.pluralMapNavigator) {
            const visible = state.systemPanelPinned || state.systemPanelOpen;
            panel.hidden = !visible;
            panel.classList.toggle("is-pinned", state.systemPanelPinned);
            panel.classList.toggle("is-overlay", !state.systemPanelPinned);
            pinButton.setAttribute("aria-pressed", String(state.systemPanelPinned));
            pinButton.setAttribute("aria-label", state.systemPanelPinned ? "Unpin panel" : "Pin panel");
            pinButton.title = state.systemPanelPinned ? "Unpin panel" : "Pin panel";
            pinButton.replaceChildren(createPinIcon(state.systemPanelPinned));
            const systemsButton = document.querySelector(".member-toolbar .member-toolbar-tree-toggle");
            const membersApi = window.PluralBridge && window.PluralBridge.members
                ? window.PluralBridge.members
                : null;
            if (systemsButton && membersApi && typeof membersApi.setTreeToggleVisible === "function") {
                membersApi.setTreeToggleVisible(systemsButton, visible);
            }
            return;
        }

        writeMembersUiStateToStorage(state);
        rerenderMembers();
    });

    header.appendChild(heading);

    const headerActions = document.createElement("div");
    headerActions.className = "member-systems-header-actions";

    if (state.pluralMapNavigator) {
        const expandAllButton = document.createElement("button");
        expandAllButton.type = "button";
        expandAllButton.className = "member-systems-expand-all";
        expandAllButton.setAttribute("aria-label", "Expand all");
        expandAllButton.title = "Expand all";
        const initialIcon = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        initialIcon.setAttribute("class", "member-tree-chevron");
        initialIcon.setAttribute("viewBox", "0 0 24 24");
        initialIcon.setAttribute("aria-hidden", "true");
        initialIcon.setAttribute("focusable", "false");
        const initialPath = document.createElementNS("http://www.w3.org/2000/svg", "path");
        initialPath.setAttribute("d", "M9 5.5 16.5 12 9 18.5");
        initialPath.setAttribute("fill", "none");
        initialPath.setAttribute("stroke", "currentColor");
        initialPath.setAttribute("stroke-width", "1.75");
        initialPath.setAttribute("stroke-linecap", "round");
        initialPath.setAttribute("stroke-linejoin", "round");
        initialIcon.appendChild(initialPath);
        expandAllButton.appendChild(initialIcon);
        expandAllButton.addEventListener("click", function () {
            const navigatorView = window.PluralBridge && window.PluralBridge.navigator
                ? window.PluralBridge.navigator
                : null;
            if (navigatorView && typeof navigatorView.toggleAllExpansion === "function") {
                navigatorView.toggleAllExpansion();
            }
        });
        headerActions.appendChild(expandAllButton);
    }

    headerActions.appendChild(pinButton);
    header.appendChild(headerActions);
    panel.appendChild(header);

    const tree = document.createElement("div");
    tree.className = "member-systems-tree";

    if (state.pluralMapNavigator) {
        panel.appendChild(tree);
        return panel;
    }

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
            expandButton.setAttribute("aria-label", isExpanded ? "Collapse" : "Expand");
            const chevron = document.createElementNS("http://www.w3.org/2000/svg", "svg");
            chevron.setAttribute("class", "member-tree-chevron" + (isExpanded ? " is-expanded" : ""));
            chevron.setAttribute("viewBox", "0 0 24 24");
            chevron.setAttribute("aria-hidden", "true");
            chevron.setAttribute("focusable", "false");
            const chevronPath = document.createElementNS("http://www.w3.org/2000/svg", "path");
            chevronPath.setAttribute("d", "M9 5.5 16.5 12 9 18.5");
            chevronPath.setAttribute("fill", "none");
            chevronPath.setAttribute("stroke", "currentColor");
            chevronPath.setAttribute("stroke-width", "1.75");
            chevronPath.setAttribute("stroke-linecap", "round");
            chevronPath.setAttribute("stroke-linejoin", "round");
            chevron.appendChild(chevronPath);
            expandButton.appendChild(chevron);

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

function createMembersScreenFrame() {
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

    const nameplate = document.createElement("img");
    nameplate.className = "member-brand-nameplate";
    nameplate.src = "/app/images/pluralbridge-nameplate.png";
    nameplate.alt = "The Plural Bridge";
    nameplate.addEventListener("error", function () {
        nameplate.hidden = true;
    });

    const brand = document.createElement("div");
    brand.className = "member-screen-brand";
    brand.appendChild(nameplate);
    brand.appendChild(logo);

    const header = document.createElement("div");
    header.className = "member-screen-header";
    header.appendChild(heading);
    header.appendChild(brand);

    const memberAddContainer = document.createElement("div");
    memberAddContainer.className = "member-add-container";
    memberAddContainer.hidden = true;

    const memberScrollList = document.createElement("div");
    memberScrollList.className = "member-scroll-list";

    const membersWorkspace = document.createElement("div");
    membersWorkspace.className = "member-workspace";

    const workspacePhoto = document.createElement("img");
    workspacePhoto.className = "member-workspace-photo";
    workspacePhoto.src = "/app/images/members-workspace-background.jpg";
    workspacePhoto.alt = "";
    workspacePhoto.setAttribute("aria-hidden", "true");
    membersWorkspace.appendChild(workspacePhoto);

    const memberContent = document.createElement("div");
    memberContent.className = "member-content-stack";
    memberContent.appendChild(memberAddContainer);
    memberContent.appendChild(memberScrollList);
    membersWorkspace.appendChild(memberContent);

    return {
        wrapper: wrapper,
        header: header,
        heading: heading,
        note: note,
        logo: logo,
        memberAddContainer: memberAddContainer,
        memberScrollList: memberScrollList,
        membersWorkspace: membersWorkspace,
        memberContent: memberContent
    };
}

function renderMembers(payload) {
    const membersUiState = getMembersUiState();
    membersUiState.pluralMapNavigator = false;
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
    const screen = createMembersScreenFrame();
    const wrapper = screen.wrapper;
    const note = screen.note;
    const memberAddContainer = screen.memberAddContainer;
    const memberScrollList = screen.memberScrollList;
    const membersWorkspace = screen.membersWorkspace;
    const memberContent = screen.memberContent;

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

    wrapper.appendChild(screen.header);
    wrapper.appendChild(note);
    wrapper.appendChild(focusNotes);

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
            const treeWasVisible = Boolean(
                membersUiState.systemPanelPinned || membersUiState.systemPanelOpen
            );
            if (treeWasVisible) {
                membersUiState.systemPanelPinned = false;
                membersUiState.systemPanelOpen = false;
            } else {
                membersUiState.systemPanelOpen = true;
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

    membersWorkspace.insertBefore(
        createMembersSystemPanel(
            membersUiState,
            function () {
                renderMembers(payload);
            },
            selectMembersSystem,
            selectMembersGroup
        ),
        memberContent
    );
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

function clearPluralMapDisplay() {
    const navigatorView = window.PluralBridge && window.PluralBridge.navigator
        ? window.PluralBridge.navigator
        : null;

    if (navigatorView && typeof navigatorView.clear === "function") {
        navigatorView.clear();
    }
}

function showMembersWorkspace() {
    const membersUiState = getMembersUiState();
    membersUiState.pluralMapNavigator = true;
    setContractButtonState("members");
    setSessionButtonState(null);
    clearPluralMapDisplay();
    clearOutput();

    const screen = createMembersScreenFrame();
    screen.note.hidden = true;

    const panel = createMembersSystemPanel(
        membersUiState,
        function () {},
        null,
        null
    );
    const tree = panel.querySelector(".member-systems-tree");
    screen.membersWorkspace.insertBefore(panel, screen.memberContent);

    let systemsButton = null;
    let toolbar = null;
    if (
        window.PluralBridge &&
        window.PluralBridge.members &&
        typeof window.PluralBridge.members.createMemberToolbar === "function"
    ) {
        function toggleSystemPanel() {
            const treeWasVisible = Boolean(
                membersUiState.systemPanelPinned || membersUiState.systemPanelOpen
            );
            const navigatorView = window.PluralBridge && window.PluralBridge.navigator
                ? window.PluralBridge.navigator
                : null;
            if (treeWasVisible) {
                if (navigatorView && typeof navigatorView.holdExpansion === "function") {
                    navigatorView.holdExpansion();
                }
                membersUiState.systemPanelPinned = false;
                membersUiState.systemPanelOpen = false;
                writeMembersUiStateToStorage(membersUiState);
            } else {
                membersUiState.systemPanelOpen = true;
            }

            const visible = Boolean(
                membersUiState.systemPanelPinned || membersUiState.systemPanelOpen
            );
            panel.hidden = !visible;
            panel.classList.toggle("is-pinned", membersUiState.systemPanelPinned === true);
            panel.classList.toggle("is-overlay", membersUiState.systemPanelPinned !== true);

            const pinButton = panel.querySelector(".member-systems-pin-button");
            if (pinButton) {
                const pinned = membersUiState.systemPanelPinned === true;
                pinButton.setAttribute("aria-pressed", String(pinned));
                pinButton.setAttribute("aria-label", pinned ? "Unpin panel" : "Pin panel");
                pinButton.title = pinned ? "Unpin panel" : "Pin panel";
                const pinIcon = pinButton.querySelector(".member-systems-pin-icon");
                if (pinIcon) {
                    pinIcon.classList.toggle("is-unpinned", !pinned);
                }
            }

            const membersApi = window.PluralBridge && window.PluralBridge.members
                ? window.PluralBridge.members
                : null;
            if (systemsButton && membersApi && typeof membersApi.setTreeToggleVisible === "function") {
                membersApi.setTreeToggleVisible(systemsButton, visible);
            }

            if (!treeWasVisible && navigatorView && typeof navigatorView.restoreHeldExpansion === "function") {
                navigatorView.restoreHeldExpansion();
            }
        }

        function openAddDialog(mode) {
            const opener = document.activeElement;
            if (typeof window.PluralBridge.members.openAddMemberDialog === "function") {
                window.PluralBridge.members.openAddMemberDialog({
                    opener: opener,
                    mode: mode
                });
            }
        }

        toolbar = window.PluralBridge.members.createMemberToolbar({
            toggleSystemPanel: toggleSystemPanel,
            toggleAddMember: function () {
                openAddDialog("member");
            },
            toggleAddGroup: function () {
                openAddDialog("group");
            },
            toggleAddPlace: function () {
                openAddDialog("place");
            },
            toggleAddSystem: function () {
                openAddDialog("system");
            },
            toggleAddRelationship: function () {
                openAddDialog("relationship");
            },
            isSystemPanelOpen: membersUiState.systemPanelPinned || membersUiState.systemPanelOpen
        });
        systemsButton = toolbar.querySelector(".member-toolbar-tree-toggle");
    }

    screen.memberAddContainer.hidden = true;
    screen.memberAddContainer.replaceChildren();

    screen.wrapper.appendChild(screen.header);
    if (toolbar) {
        screen.wrapper.appendChild(toolbar);
    }
    screen.wrapper.appendChild(screen.membersWorkspace);
    output.appendChild(screen.wrapper);
    updateSessionStatus();

    const navigatorView = window.PluralBridge && window.PluralBridge.navigator
        ? window.PluralBridge.navigator
        : null;

    if (!navigatorView || typeof navigatorView.mount !== "function" || !tree) {
        if (tree) {
            const unavailable = document.createElement("p");
            unavailable.className = "navigator-status navigator-status-error";
            unavailable.textContent = "The plural map display is not available.";
            tree.appendChild(unavailable);
        }
        return;
    }

    navigatorView.mount({
        tree: tree,
        detail: screen.memberScrollList
    });
}

async function renderContract(key) {
  const endpointBuilder = endpointBuilders[key];

  setContractButtonState(key);
  setSessionButtonState(null);

  if (key === "members") {
      showMembersWorkspace();
      return;
  }

  clearPluralMapDisplay();

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
  const membersApi = window.PluralBridge && window.PluralBridge.members
    ? window.PluralBridge.members
    : null;
  if (membersApi && typeof membersApi.closeAddMemberDialog === "function") {
    membersApi.closeAddMemberDialog();
  }
  clearPluralMapDisplay();

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

document.addEventListener("click", function (event) {
    const targetElement = getEventTargetElement(event);
    const placeholderButton = targetElement
        ? targetElement.closest("[data-shell-placeholder]")
        : null;

    if (placeholderButton) {
        clearPluralMapDisplay();

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
        renderContract(contractButton.dataset.contract);
        return;
    }

    const sessionButton = targetElement
        ? targetElement.closest("[data-session-action]")
        : null;

    if (sessionButton) {
        event.preventDefault();

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
