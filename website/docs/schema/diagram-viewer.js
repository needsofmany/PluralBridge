document.addEventListener("DOMContentLoaded", () => {
  const status = document.getElementById("diagram-status");
  const canvas = document.getElementById("diagram-canvas");
  const imagePane = document.getElementById("diagram-image-pane");
  const image = document.getElementById("diagram-image");
  const sourceOut = document.getElementById("diagram-source-text");
  const label = document.getElementById("zoom-label");
  const btnModeGraph = document.getElementById("mode-graph");
  const btnModeImage = document.getElementById("mode-image");
  const btnReset = document.getElementById("zoom-reset");
  const btnIn = document.getElementById("zoom-in");
  const btnOut = document.getElementById("zoom-out");

  const decodeSql = async (res) => {
    const bytes = new Uint8Array(await res.arrayBuffer());
    if (bytes.length >= 2 && bytes[0] === 0xff && bytes[1] === 0xfe) {
      return new TextDecoder("utf-16le").decode(bytes);
    }
    if (bytes.length >= 2 && bytes[0] === 0xfe && bytes[1] === 0xff) {
      return new TextDecoder("utf-16be").decode(bytes);
    }
    return new TextDecoder("utf-8").decode(bytes);
  };

  const parseSchema = (sql) => {
    const tableRegex = /CREATE TABLE\s+\[dbo\]\.\[([^\]]+)\]\(/gi;
    const fkRegex =
      /ALTER TABLE\s+\[dbo\]\.\[([^\]]+)\]\s+WITH CHECK ADD\s+CONSTRAINT\s+\[[^\]]+\]\s+FOREIGN KEY\(\[([^\]]+)\]\)\s*REFERENCES\s+\[dbo\]\.\[([^\]]+)\]\s+\(\[([^\]]+)\]\)/gi;

    const tables = [];
    const seen = new Set();
    let m;
    while ((m = tableRegex.exec(sql)) !== null) {
      const name = m[1];
      if (!seen.has(name)) {
        seen.add(name);
        tables.push(name);
      }
    }

    const foreignKeys = [];
    while ((m = fkRegex.exec(sql)) !== null) {
      foreignKeys.push({
        fromTable: m[1],
        fromColumn: m[2],
        toTable: m[3],
        toColumn: m[4],
      });
    }

    return { tables, foreignKeys };
  };

  const toMermaid = ({ tables, foreignKeys }) => {
    const idByTable = new Map();
    tables.forEach((name, idx) => idByTable.set(name, `T${idx + 1}`));

    const lines = ["graph LR"];
    tables.forEach((name) => {
      lines.push(`  ${idByTable.get(name)}["${name}"]`);
    });

    foreignKeys.forEach((fk) => {
      const fromId = idByTable.get(fk.fromTable);
      const toId = idByTable.get(fk.toTable);
      if (!fromId || !toId) return;
      const edgeLabel = `${fk.fromColumn} -> ${fk.toColumn}`;
      lines.push(`  ${fromId} -->|"${edgeLabel}"| ${toId}`);
    });

    return lines.join("\n");
  };

  let scale = 1;
  let mode = "graph";
  let graphStatus = "Generating graph from script.sql...";
  const minScale = 0.4;
  const maxScale = 2.5;
  const step = 0.15;

  const updateModeButtons = () => {
    btnModeGraph.classList.toggle("is-active", mode === "graph");
    btnModeImage.classList.toggle("is-active", mode === "image");
    canvas.hidden = mode !== "graph";
    imagePane.hidden = mode !== "image";
  };

  const updateStatus = () => {
    if (mode === "image") {
      status.textContent = "Showing original DB diagram image.";
      return;
    }
    status.textContent = graphStatus;
  };

  const applyScale = () => {
    canvas.style.transform = `scale(${scale})`;
    imagePane.style.transform = `scale(${scale})`;
    label.textContent = `${Math.round(scale * 100)}%`;
  };

  btnIn.addEventListener("click", () => {
    scale = Math.min(maxScale, scale + step);
    applyScale();
  });

  btnOut.addEventListener("click", () => {
    scale = Math.max(minScale, scale - step);
    applyScale();
  });

  btnReset.addEventListener("click", () => {
    scale = 1;
    applyScale();
  });

  btnModeGraph.addEventListener("click", () => {
    mode = "graph";
    updateModeButtons();
    updateStatus();
  });

  btnModeImage.addEventListener("click", () => {
    mode = "image";
    updateModeButtons();
    updateStatus();
  });

  const prefersDark =
    window.matchMedia &&
    window.matchMedia("(prefers-color-scheme: dark)").matches;

  const renderFallback = (mermaidSource) => {
    graphStatus = "Graph renderer unavailable; showing Mermaid source below.";
    canvas.innerHTML =
      "<pre style='margin:0;color:#dbeafe;font-family:Consolas,monospace;white-space:pre;'>" +
      mermaidSource.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;") +
      "</pre>";
    updateStatus();
  };

  const buildGraph = async () => {
    try {
      const res = await fetch("./script.sql", { cache: "no-cache" });
      if (!res.ok) throw new Error("HTTP " + res.status);

      const sql = await decodeSql(res);
      const schema = parseSchema(sql);
      const mermaidSource = toMermaid(schema);
      sourceOut.textContent = mermaidSource;
      graphStatus =
        `Generated ${schema.tables.length} tables and ${schema.foreignKeys.length} relationships from script.sql.`;
      updateStatus();

      try {
        const mermaidModule = await import("https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs");
        const mermaid = mermaidModule.default;
        mermaid.initialize({
          startOnLoad: false,
          theme: prefersDark ? "dark" : "default",
          securityLevel: "loose",
        });
        const renderId = "pbSchemaGraph";
        const result = await mermaid.render(renderId, mermaidSource);
        canvas.innerHTML = result.svg;
      } catch (renderError) {
        renderFallback(mermaidSource);
      }
    } catch (error) {
      graphStatus = "Failed to generate schema graph.";
      canvas.textContent = String(error);
      sourceOut.textContent = "";
      updateStatus();
    }
  };

  updateModeButtons();
  updateStatus();
  applyScale();
  buildGraph();
});
