const apiBaseUrl = "https://localhost:7275";

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
  sessionStatus.textContent = "Phase 2B: read-only database surface";
}

function renderPayload(payload) {
  output.textContent = JSON.stringify(payload, null, 2);
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
    renderPayload(payload);
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

loginForm.addEventListener("submit", function(event) {
  event.preventDefault();
  renderFrozenSessionAction("login");
});

sessionButtons.forEach(function(button) {
  if (button.dataset.sessionAction !== "login") {
    button.addEventListener("click", function() {
      renderFrozenSessionAction(button.dataset.sessionAction);
    });
  }
});

contractButtons.forEach(function(button) {
  button.addEventListener("click", function() {
    renderContract(button.dataset.contract);
  });
});

updateSessionStatus();
