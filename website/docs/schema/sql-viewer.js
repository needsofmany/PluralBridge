document.addEventListener("DOMContentLoaded", async () => {
  const status = document.getElementById("sql-status");
  const output = document.getElementById("sql-output");

  const escapeHtml = (text) =>
    text
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");

  const highlightSql = (sqlText) => {
    const keywords = [
      "ADD", "ALTER", "AND", "AS", "ASC", "BEGIN", "BY", "CHECK", "CLUSTERED",
      "COLLATE", "COLUMN", "CONSTRAINT", "CREATE", "DATABASE", "DEFAULT", "DESC",
      "DROP", "ELSE", "END", "EXEC", "EXISTS", "FOREIGN", "FROM", "GO", "GROUP",
      "IF", "IN", "INDEX", "INNER", "INSERT", "INTO", "IS", "JOIN", "KEY", "LEFT",
      "NOT", "NULL", "ON", "OR", "ORDER", "OUTER", "PRIMARY", "PROCEDURE", "REFERENCES",
      "RIGHT", "SCHEMA", "SELECT", "SET", "TABLE", "TOP", "TRIGGER", "UNIQUE", "UPDATE",
      "VALUES", "VIEW", "WHERE", "WITH"
    ];

    let html = escapeHtml(sqlText);
    const placeholders = [];
    const keep = (className, value) => {
      const id = `__SQLTOK_${placeholders.length}__`;
      placeholders.push({ id, html: `<span class="${className}">${value}</span>` });
      return id;
    };

    html = html.replace(/\/\*[\s\S]*?\*\//g, (m) => keep("sql-comment", m));
    html = html.replace(/--[^\n]*/g, (m) => keep("sql-comment", m));
    html = html.replace(/'(?:''|[^'])*'/g, (m) => keep("sql-string", m));

    const kwPattern = new RegExp(`\\b(${keywords.join("|")})\\b`, "gi");
    html = html.replace(kwPattern, '<span class="sql-keyword">$1</span>');
    html = html.replace(/\b\d+(?:\.\d+)?\b/g, '<span class="sql-number">$&</span>');

    placeholders.forEach((entry) => {
      html = html.replace(entry.id, entry.html);
    });

    return html;
  };

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

  try {
    const res = await fetch("./script.sql", { cache: "no-cache" });
    if (!res.ok) throw new Error("HTTP " + res.status);

    const text = await decodeSql(res);
    output.innerHTML = highlightSql(text.replace(/\r\n/g, "\n"));
    status.textContent = "Viewing: script.sql";
  } catch (error) {
    status.textContent = "Failed to load SQL.";
    output.textContent = String(error);
  }
});
