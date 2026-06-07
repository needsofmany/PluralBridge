const meResponse = Object.freeze({
  endpoint: "GET /api/me",
  contractVersion: "phase-1-me-proof",
  mode: "static-browser-contract",
  authenticated: true,
  user: {
    userId: "phase1-demo-user",
    displayName: "Phase 1 Demo User"
  },
  system: {
    systemId: "phase1-demo-system",
    displayName: "Phase 1 Demo System",
    sourceSystem: "APPARYLLIS",
    mappingSource: "phase-1-mock-contract"
  },
  permissions: {
    readOnly: true,
    canReadMembers: true,
    canReadFrontHistory: true,
    canWrite: false
  },
  dataScope: {
    source: "mock-contract",
    usesPrivateData: false
  },
  links: {
    members: "/api/members",
    frontHistory: "/api/front-history"
  }
});

function renderMeResponse() {
  const output = document.getElementById("meOutput");
  output.textContent = JSON.stringify(meResponse, null, 2);
}

document.getElementById("meButton").addEventListener("click", renderMeResponse);
