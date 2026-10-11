document.addEventListener("DOMContentLoaded", async () => {
  const params = new URLSearchParams(window.location.search);
  const file = params.get("file");
  const status = document.getElementById("json-status");
  const output = document.getElementById("json-output");
  const rawLink = document.getElementById("json-raw-link");

  const allowed = new Set([
    "simply_plural_last_export.inferred.schema.json",
    "samsung_smart_switch_simply_db.inferred.schema.json"
  ]);

  const escapeHtml = (text) =>
    text
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");

  const highlightJson = (text) => {
    const escaped = escapeHtml(text);
    return escaped.replace(
      /("(?:\\.|[^"\\])*")\s*:|("(?:\\.|[^"\\])*")|(-?\d+(?:\.\d+)?(?:[eE][+\-]?\d+)?)|\b(true|false|null)\b|([{}\[\],:])/g,
      (match, key, stringVal, numberVal, literalVal, punctVal) => {
        if (key) {
          const cleanKey = key.slice(0, -1);
          return `<span class="json-key">${cleanKey}</span><span class="json-punct">:</span>`;
        }
        if (stringVal) return `<span class="json-string">${stringVal}</span>`;
        if (numberVal) return `<span class="json-number">${numberVal}</span>`;
        if (literalVal) return `<span class="json-literal">${literalVal}</span>`;
        if (punctVal) return `<span class="json-punct">${punctVal}</span>`;
        return match;
      }
    );
  };

  if (!file || !allowed.has(file)) {
    status.textContent = "Invalid JSON file requested.";
    output.textContent = "";
    rawLink.removeAttribute("href");
    return;
  }

  const rawPath = "./" + file;
  rawLink.href = rawPath;

  try {
    const res = await fetch(rawPath, { cache: "no-cache" });
    if (!res.ok) throw new Error("HTTP " + res.status);
    const json = await res.json();
    const pretty = JSON.stringify(json, null, 2);
    output.innerHTML = highlightJson(pretty);
    status.textContent = "Viewing: " + file;
  } catch (error) {
    status.textContent = "Failed to load JSON.";
    output.textContent = String(error);
  }
});
