const contracts = {
  me: {
    endpoint: "GET /api/me",
    contractVersion: "phase-2-readonly-surface",
    data: {
      userId: "demo-user-001",
      displayName: "Phase 2 Demo User",
      activeSystemId: "demo-system-001"
    },
    safety: {
      canWrite: false,
      usesPrivateData: false
    }
  },
  system: {
    endpoint: "GET /api/systems/current",
    contractVersion: "phase-2-readonly-surface",
    data: {
      systemId: "demo-system-001",
      displayName: "Phase 2 Demo System",
      totalCount: 1
    },
    safety: {
      canWrite: false,
      usesPrivateData: false
    }
  },
  members: {
    endpoint: "GET /api/members",
    contractVersion: "phase-2-readonly-surface",
    data: {
      totalCount: 49,
      sampleItems: [
        { memberId: "demo-member-001", displayName: "Demo Member 001" },
        { memberId: "demo-member-002", displayName: "Demo Member 002" },
        { memberId: "demo-member-003", displayName: "Demo Member 003" }
      ]
    },
    safety: {
      canWrite: false,
      usesPrivateData: false
    }
  },
  frontHistory: {
    endpoint: "GET /api/front-history",
    contractVersion: "phase-2-readonly-surface",
    data: {
      totalCount: 886,
      sampleItems: [
        { frontHistoryId: "demo-front-001", memberId: "demo-member-001", startedAtUtc: "2026-01-01T00:00:00Z" },
        { frontHistoryId: "demo-front-002", memberId: "demo-member-002", startedAtUtc: "2026-01-02T00:00:00Z" }
      ]
    },
    safety: {
      canWrite: false,
      usesPrivateData: false
    }
  },
  privacyBuckets: {
    endpoint: "GET /api/privacy-buckets",
    contractVersion: "phase-2-readonly-surface",
    data: {
      totalCount: 2,
      sampleItems: [
        { privacyBucketId: "demo-privacy-001", label: "Demo Public" },
        { privacyBucketId: "demo-privacy-002", label: "Demo Trusted" }
      ]
    },
    safety: {
      canWrite: false,
      usesPrivateData: false
    }
  },
  customFields: {
    endpoint: "GET /api/custom-fields",
    contractVersion: "phase-2-readonly-surface",
    data: {
      totalCount: 7,
      sampleItems: [
        { customFieldId: "demo-field-001", fieldName: "Demo Field 001" },
        { customFieldId: "demo-field-002", fieldName: "Demo Field 002" }
      ]
    },
    safety: {
      canWrite: false,
      usesPrivateData: false
    }
  },
  importMetadata: {
    endpoint: "GET /api/imports/current",
    contractVersion: "phase-2-readonly-surface",
    data: {
      sourceSystems: 1,
      importBatches: 1,
      sourceRecords: 945,
      sourceIdMap: 945
    },
    safety: {
      canWrite: false,
      usesPrivateData: false
    }
  }
};

const output = document.getElementById("appOutput");
const buttons = document.querySelectorAll("[data-contract]");

function renderContract(key) {
  output.textContent = JSON.stringify(contracts[key], null, 2);
  buttons.forEach((button) => {
    button.setAttribute("aria-pressed", String(button.dataset.contract === key));
  });
}

buttons.forEach((button) => {
  button.addEventListener("click", () => renderContract(button.dataset.contract));
});

renderContract("me");
