const apiBaseUrl = "";

let selectedSystemId = null;

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
        sessionStatus.textContent = "You are signed in to the read-only PluralBridge demo.";
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
    note.textContent = sourceSystems.length + " source systems returned from the read-only demo API.";

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
    note.textContent = systems.length + " systems returned from the read-only demo API.";

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
    heading.textContent = "Demo session";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = "Read-only PluralBridge proof context for the protected demo session.";

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
    note.textContent = privacyBuckets.length + " privacy buckets returned from the read-only demo API.";

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
    note.textContent = customFields.length + " custom fields returned from the read-only demo API.";

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
    note.textContent = importBatches.length + " import batches returned from the read-only demo API.";

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
    note.textContent = sourceRecords.length + " source records returned from the read-only demo API.";

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
    note.textContent = sourceIdMappings.length + " source ID mappings returned from the read-only demo API.";

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
    note.textContent = "Read-only import health and lineage summary for this demo system.";

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

        if (iconName === "description") {
            path.setAttribute("d", "M6 3h9l3 3v15H6V3zm8 1.8V7h2.2L14 4.8zM8 10h8v2H8v-2zm0 4h8v2H8v-2z");
        } else if (iconName === "json") {
            path.setAttribute("d", "M8.6 7.4 4 12l4.6 4.6L7.2 18 1.2 12l6-6 1.4 1.4zm6.8 0L16.8 6l6 6-6 6-1.4-1.4L20 12l-4.6-4.6zM13.2 5h2L10.8 19h-2l4.4-14z");
        } else {
            path.setAttribute("d", "M4 17.3V21h3.7L18.8 9.9l-3.7-3.7L4 17.3zM20.7 8c.4-.4.4-1 0-1.4l-2.3-2.3c-.4-.4-1-.4-1.4 0l-1.2 1.2 3.7 3.7L20.7 8z");
        }

        svg.appendChild(path);
        return svg;
    }

    function createMemberTextButton(iconName, labelText) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "member-toggle-button member-toggle-text-button";

        button.appendChild(createMemberActionIcon(iconName));

        const label = document.createElement("span");
        label.textContent = labelText;

        button.appendChild(label);

        return button;
    }

    function createMemberIconButton(iconName, labelText) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "member-toggle-button member-toggle-icon-button";
        button.setAttribute("aria-label", labelText);
        button.title = labelText;

        button.appendChild(createMemberActionIcon(iconName));

        return button;
    }

    const details = document.createElement("details");
    details.className = "member-card";

    const summary = document.createElement("summary");
    summary.className = "member-summary";

    const summaryMain = document.createElement("span");
    summaryMain.className = "member-summary-main";

    const avatarPlaceholder = document.createElement("span");
    avatarPlaceholder.className = "member-avatar-placeholder";
    avatarPlaceholder.setAttribute("aria-hidden", "true");

    const red = Math.floor(Math.random() * 256);
    const green = Math.floor(Math.random() * 256);
    const blue = Math.floor(Math.random() * 256);

    avatarPlaceholder.style.backgroundColor = "rgb(" + red + ", " + green + ", " + blue + ")";
    avatarPlaceholder.style.borderColor = "rgba(" + red + ", " + green + ", " + blue + ", 0.85)";

    const identity = document.createElement("span");
    identity.className = "member-identity";

    const name = document.createElement("span");
    name.className = "member-name";
    name.textContent = getMemberDisplayName(member);

    const pronouns = document.createElement("span");
    pronouns.className = "member-pronouns";
    pronouns.textContent = getMemberPronouns(member);

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
    details.appendChild(summary);

    const body = document.createElement("div");
    body.className = "member-body";

    const toggleRow = document.createElement("div");
    toggleRow.className = "member-toggle-row";
    toggleRow.setAttribute("aria-label", "Member detail display mode");

    const descriptionButton = createMemberTextButton("description", "Description");
    const jsonButton = createMemberTextButton("json", "JSON");
    const editButton = createMemberTextButton("edit", "Edit");

    const content = document.createElement("div");
    content.className = "member-content";

    const descriptionText = document.createElement("p");
    descriptionText.className = "member-description";
    descriptionText.textContent = getMemberDescription(member);

    const jsonBlock = document.createElement("pre");
    jsonBlock.className = "member-json json-output";
    jsonBlock.textContent = JSON.stringify(member, null, 2);

    function setPressed(selectedButton) {
        descriptionButton.setAttribute("aria-pressed", String(selectedButton === descriptionButton));
        jsonButton.setAttribute("aria-pressed", String(selectedButton === jsonButton));
        editButton.setAttribute("aria-pressed", String(selectedButton === editButton));
    }

    function refreshMembersFromApi() {
        return window.PluralBridge.apiClient.readMembers()
            .then(function (payload) {
                renderMembers(payload);
            });
    }

    function showDescription() {
        setPressed(descriptionButton);
        content.replaceChildren(descriptionText);
    }

    function showJson() {
        setPressed(jsonButton);
        content.replaceChildren(jsonBlock);
    }

    function showEdit() {
        setPressed(editButton);
        details.open = true;

        if (
            window.PluralBridge &&
            window.PluralBridge.members &&
            typeof window.PluralBridge.members.createMemberEditForm === "function"
        ) {
            content.replaceChildren(window.PluralBridge.members.createMemberEditForm(member, {
                cancelEdit: showDescription,
                refreshMembers: refreshMembersFromApi
            }));
            return;
        }

        const unavailable = document.createElement("p");
        unavailable.className = "member-description";
        unavailable.textContent = "Member edit is not available in the current browser session.";
        content.replaceChildren(unavailable);
    }

    descriptionButton.addEventListener("click", showDescription);
    jsonButton.addEventListener("click", showJson);
    editButton.addEventListener("click", showEdit);

    toggleRow.appendChild(descriptionButton);
    toggleRow.appendChild(jsonButton);
    toggleRow.appendChild(editButton);

    body.appendChild(toggleRow);
    body.appendChild(content);
    details.appendChild(body);

    showDescription();

    return details;
}

function renderMembers(payload) {
    clearOutput();

    const members = getMembersFromPayload(payload);

    const wrapper = document.createElement("section");
    wrapper.className = "member-list";

    const heading = document.createElement("h2");
    heading.className = "output-heading";
    heading.textContent = "Members";

    const note = document.createElement("p");
    note.className = "output-note";
    note.textContent = members.length + " members returned.";

    wrapper.appendChild(heading);
    wrapper.appendChild(note);

    const memberAddContainer = document.createElement("div");
    memberAddContainer.className = "member-add-container";
    memberAddContainer.hidden = true;

    function refreshMembersFromApi() {
        return window.PluralBridge.apiClient.readMembers()
            .then(function (payload) {
                renderMembers(payload);
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
        wrapper.appendChild(window.PluralBridge.members.createMemberToolbar({
            toggleAddMember: toggleAddMemberForm
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

        wrapper.appendChild(memberAddContainer);
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

    wrapper.appendChild(memberScrollList);

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
    note.textContent = records.length + " front history records returned from the read-only demo API.";

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

  if (!payload || !payload.proofSystem || !payload.proofSystem.systemId) {
    throw new Error("No proof system was returned by /api/me.");
  }

  selectedSystemId = payload.proofSystem.systemId;
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

  renderPayload({
    phase: "Phase 2B",
    canWrite: false,
    action: key,
    status: "loading"
  });

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

document.addEventListener("click", function (event) {
    const contractButton = event.target.closest("[data-contract]");

    if (contractButton) {
        renderContract(contractButton.dataset.contract);
        return;
    }

    const sessionButton = event.target.closest("[data-session-action]");

    if (sessionButton) {
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
