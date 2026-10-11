(() => {
  const page = document.querySelector(".doc-page:has(#api-endpoint-reference)");
  if (!page) {
    return;
  }

  const detailByEndpoint = {
    "POST /api/account/register": {
      requestExample:
        '{\n  "username": "string",\n  "password": "string",\n  "email": "name@example.com"\n}',
      responseExample:
        '{\n  "succeeded": true,\n  "outcome": "string",\n  "reasonCode": "string",\n  "message": "string"\n}',
    },
    "POST /api/account/verify-registration": {
      requestExample:
        '{\n  "username": "string",\n  "code": "123456"\n}',
      responseExample:
        '{\n  "succeeded": true,\n  "outcome": "string",\n  "reasonCode": "string",\n  "message": "string"\n}',
    },
    "POST /api/account/login": {
      requestExample: '{\n  "username": "string",\n  "password": "string"\n}',
      responseExample:
        '{\n  "succeeded": true,\n  "outcome": "string",\n  "reasonCode": "string",\n  "message": "string",\n  "account": {\n    "accountId": "guid",\n    "username": "string"\n  }\n}',
    },
    "POST /api/account/forgot-username": {
      requestExample: '{\n  "email": "name@example.com"\n}',
      responseExample:
        '{\n  "succeeded": true,\n  "outcome": "string",\n  "reasonCode": "string",\n  "message": "string"\n}',
    },
    "POST /api/account/forgot-password": {
      requestExample: '{\n  "username": "string"\n}',
      responseExample:
        '{\n  "succeeded": true,\n  "outcome": "string",\n  "reasonCode": "string",\n  "message": "string"\n}',
    },
    "POST /api/account/reset-password": {
      requestExample:
        '{\n  "username": "string",\n  "code": "123456",\n  "newPassword": "string"\n}',
      responseExample:
        '{\n  "succeeded": true,\n  "outcome": "string",\n  "reasonCode": "string",\n  "message": "string"\n}',
    },
    "POST /api/account/change-password": {
      requestExample:
        '{\n  "currentPassword": "string",\n  "newPassword": "string"\n}',
      responseExample:
        '{\n  "succeeded": true,\n  "outcome": "string",\n  "reasonCode": "string",\n  "message": "string"\n}',
    },
    "PUT /api/account/profile": {
      requestExample:
        '{\n  "displayName": "string",\n  "timeZone": "string"\n}',
      responseExample:
        '{\n  "succeeded": true,\n  "outcome": "string",\n  "reasonCode": "string",\n  "message": "string"\n}',
    },
    "PUT /api/account/contact": {
      requestExample:
        '{\n  "destinationType": "email",\n  "destination": "name@example.com"\n}',
      responseExample:
        '{\n  "succeeded": true,\n  "outcome": "string",\n  "reasonCode": "string",\n  "message": "string"\n}',
    },
    "POST /api/account/verify-contact": {
      requestExample:
        '{\n  "destinationType": "email",\n  "code": "123456"\n}',
      responseExample:
        '{\n  "succeeded": true,\n  "outcome": "string",\n  "reasonCode": "string",\n  "message": "string"\n}',
    },
    "GET /api/me": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "mode": "read-only proof",\n  "canWrite": false,\n  "currentAccount": { },\n  "membershipAccess": [ ],\n  "currentSystem": { },\n  "counts": { }\n}',
    },
    "GET /api/source-systems": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/source-systems",\n  "canWrite": false,\n  "count": 0,\n  "sourceSystems": [ ]\n}',
    },
    "GET /api/systems": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems",\n  "canWrite": false,\n  "count": 0,\n  "systems": [ ]\n}',
    },
    "GET /api/systems/{systemId}/members": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/members",\n  "canWrite": false,\n  "systemId": "guid",\n  "count": 0,\n  "members": [ ]\n}',
    },
    "GET /api/systems/{systemId}/members/{memberId}": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/members/{memberId}",\n  "canWrite": false,\n  "systemId": "guid",\n  "memberId": "guid",\n  "member": { }\n}',
    },
    "POST /api/systems/{systemId}/members": {
      requestExample:
        '{\n  "displayName": "string",\n  "pronouns": "string",\n  "description": "string"\n}',
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/members/{memberId}",\n  "canWrite": true,\n  "systemId": "guid",\n  "memberId": "guid",\n  "member": { }\n}',
    },
    "PUT /api/systems/{systemId}/members/{memberId}": {
      requestExample:
        '{\n  "displayName": "string",\n  "pronouns": "string",\n  "description": "string"\n}',
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/members/{memberId}",\n  "canWrite": true,\n  "systemId": "guid",\n  "memberId": "guid",\n  "member": { }\n}',
    },
    "GET /api/systems/{systemId}/front-history": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/front-history",\n  "canWrite": false,\n  "systemId": "guid",\n  "count": 0,\n  "frontHistory": [ ]\n}',
    },
    "GET /api/systems/{systemId}/custom-fields": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/custom-fields",\n  "canWrite": false,\n  "systemId": "guid",\n  "count": 0,\n  "customFields": [ ]\n}',
    },
    "GET /api/systems/{systemId}/privacy-buckets": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/privacy-buckets",\n  "canWrite": false,\n  "systemId": "guid",\n  "count": 0,\n  "privacyBuckets": [ ]\n}',
    },
    "GET /api/systems/{systemId}/import-batches": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/import-batches",\n  "canWrite": false,\n  "systemId": "guid",\n  "count": 0,\n  "importBatches": [ ]\n}',
    },
    "GET /api/systems/{systemId}/import-metadata": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/import-metadata",\n  "canWrite": false,\n  "systemId": "guid",\n  "count": 0,\n  "importMetadata": [ ]\n}',
    },
    "GET /api/systems/{systemId}/source-records": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/source-records",\n  "canWrite": false,\n  "systemId": "guid",\n  "count": 0,\n  "sourceRecords": [ ]\n}',
    },
    "GET /api/systems/{systemId}/source-id-mappings": {
      responseExample:
        '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "/api/systems/{systemId}/source-id-mappings",\n  "canWrite": false,\n  "systemId": "guid",\n  "count": 0,\n  "sourceIdMappings": [ ]\n}',
    },
  };

  function escapeHtml(value) {
    return value
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;");
  }

  function getRouteParams(endpoint) {
    const matches = endpoint.match(/\{[^}]+\}/g);
    if (!matches) {
      return [];
    }
    return matches.map((raw) => raw.slice(1, -1));
  }

  function buildCurlExample(method, endpoint, hasBody) {
    const parts = [`curl -X ${method} "${endpoint}"`];
    if (hasBody) {
      parts.push("-H \"Content-Type: application/json\"");
      parts.push("-d '{...}'");
    }
    return parts.join(" ");
  }

  function formatFieldValue(value) {
    if (value === null) {
      return "null";
    }
    if (Array.isArray(value)) {
      return "[ ... ]";
    }
    if (typeof value === "object") {
      return "{ ... }";
    }
    if (typeof value === "string") {
      return value;
    }
    return String(value);
  }

  function formatResponseFields(responseExample) {
    try {
      const parsed = JSON.parse(responseExample);
      if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
        return responseExample;
      }
      return Object.entries(parsed)
        .map(([key, value]) => `${key}: ${formatFieldValue(value)}`)
        .join("\n");
    } catch {
      return responseExample;
    }
  }

  function buildDetailsHtml(method, endpoint, auth, purpose, detail) {
    const routeParams = getRouteParams(endpoint);
    const hasBody = method !== "GET";
    const requestExample = detail.requestExample || "{\n  \"...\": \"...\"\n}";
    const responseExample =
      detail.responseExample ||
      '{\n  "api": "PluralBridge.Api",\n  "phase": "string",\n  "endpoint": "string"\n}';
    const responseFields = formatResponseFields(responseExample);
    const curlExample = buildCurlExample(method, endpoint, hasBody);

    const parametersHtml =
      routeParams.length === 0
        ? '<div class="api-mini-text">No route parameters.</div>'
        : `<ul class="api-mini-list">${routeParams
            .map(
              (p) =>
                `<li><code>${escapeHtml(p)}</code> - required route identifier.</li>`
            )
            .join("")}</ul>`;

    const requestBodyHtml = hasBody
      ? `<pre><code class="language-json">${escapeHtml(requestExample)}</code></pre>`
      : '<div class="api-mini-text">No request body.</div>';

    const responsesHtml = `<div class="api-mini-text"><strong>200 OK</strong> - ${escapeHtml(
      purpose
    )}</div><pre><code class="language-text">${escapeHtml(responseFields)}</code></pre>`;

    return `
      <div class="api-mini-grid">
        <div class="api-mini-section">
          <div class="api-mini-title">Auth</div>
          <div class="api-mini-text">${escapeHtml(auth)}</div>
        </div>
        <div class="api-mini-section">
          <div class="api-mini-title">Parameters</div>
          ${parametersHtml}
        </div>
        <div class="api-mini-section">
          <div class="api-mini-title">Request body</div>
          ${requestBodyHtml}
        </div>
        <div class="api-mini-section">
          <div class="api-mini-title">Responses</div>
          ${responsesHtml}
        </div>
        <div class="api-mini-section">
          <div class="api-mini-title">Programming</div>
          <pre><code class="language-shell">${escapeHtml(curlExample)}</code></pre>
        </div>
      </div>
    `;
  }

  let activeToggle = null;
  let activeDetailRow = null;
  let activeChevron = null;

  const endpointTables = page.querySelectorAll("table");
  endpointTables.forEach((table) => {
    const rows = table.querySelectorAll("tbody tr");
    rows.forEach((row) => {
      const cells = row.querySelectorAll("td");
      if (cells.length < 4) {
        return;
      }

      const method = (cells[0].textContent || "").trim().toUpperCase();
      const endpoint = (cells[1].textContent || "").trim();
      const auth = (cells[2].textContent || "").trim();
      const purpose = (cells[3].textContent || "").trim();

      const methodChip = document.createElement("span");
      methodChip.className = `api-method-chip api-method-${method.toLowerCase()}`;
      methodChip.textContent = method;
      cells[0].textContent = "";
      cells[0].appendChild(methodChip);

      const authKey = auth.toLowerCase();
      const authChip = document.createElement("span");
      authChip.className = `api-auth-chip api-auth-${authKey}`;
      authChip.textContent = auth;
      cells[2].textContent = "";
      cells[2].appendChild(authChip);

      const key = `${method} ${endpoint}`;
      const detail = detailByEndpoint[key] || {};

      const endpointCell = cells[1];
      const toggle = document.createElement("button");
      toggle.type = "button";
      toggle.className = "api-row-toggle";
      toggle.setAttribute("aria-expanded", "false");

      const chevron = document.createElement("span");
      chevron.className = "api-row-chevron";
      chevron.setAttribute("aria-hidden", "true");
      chevron.textContent = ">";

      const endpointCode = document.createElement("code");
      endpointCode.textContent = endpoint;

      toggle.append(chevron, endpointCode);
      endpointCell.textContent = "";
      endpointCell.appendChild(toggle);

      const detailRow = document.createElement("tr");
      detailRow.className = "api-detail-row";
      detailRow.hidden = true;

      const detailCell = document.createElement("td");
      detailCell.colSpan = 4;
      detailCell.className = "api-detail-cell";
      detailCell.innerHTML = buildDetailsHtml(
        method,
        endpoint,
        auth,
        purpose,
        detail
      );
      detailRow.appendChild(detailCell);
      row.insertAdjacentElement("afterend", detailRow);

      toggle.addEventListener("click", () => {
        const isOpen = toggle.getAttribute("aria-expanded") === "true";
        if (isOpen) {
          toggle.setAttribute("aria-expanded", "false");
          detailRow.hidden = true;
          chevron.textContent = ">";
          if (activeToggle === toggle) {
            activeToggle = null;
            activeDetailRow = null;
            activeChevron = null;
          }
          return;
        }

        if (activeToggle && activeToggle !== toggle) {
          activeToggle.setAttribute("aria-expanded", "false");
        }
        if (activeDetailRow && activeDetailRow !== detailRow) {
          activeDetailRow.hidden = true;
        }
        if (activeChevron && activeChevron !== chevron) {
          activeChevron.textContent = ">";
        }

        toggle.setAttribute("aria-expanded", "true");
        detailRow.hidden = false;
        chevron.textContent = "v";
        activeToggle = toggle;
        activeDetailRow = detailRow;
        activeChevron = chevron;
      });
    });
  });
})();
