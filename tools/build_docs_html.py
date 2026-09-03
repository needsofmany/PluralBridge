from pathlib import Path
import html
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
WEBSITE = ROOT / "website"
DOCS = WEBSITE / "docs"
DOCS_SOURCE = ROOT / "docs"

NAV = """<header class="site-header">
    <div class="header-inner">
        <a class="brand" href="/index.html">
            <img class="brand-logo" src="/assets/images/pluralbridge-nav-logo.png" alt="" aria-hidden="true">
            <span class="brand-text">
                <span class="brand-title">PluralBridge</span>
                <span class="brand-subtitle">Needs of the Many</span>
            </span>
        </a>
        <nav class="nav" aria-label="Main navigation">
            <a href="/index.html">Home</a>
            <a href="/start-here.html">Start Here</a>
            <a href="/export-now.html">Data Recovery</a>
            <details class="nav-menu">
                <summary data-current="section">Guides</summary>
                <div class="nav-menu-panel">
                    <a href="/docs.html">Documentation Home</a>
                    <a href="/safety.html">Safety</a>
                    <a href="/developer-workflow.html">Help Build</a>
                    <a href="/simply-plural-shutdown.html">Shutdown Info</a>
                </div>
            </details>
            <details class="nav-menu">
                <summary>Projects</summary>
                <div class="nav-menu-panel">
                    <a href="/browser-app.html">What PluralBridge Is Building</a>
                </div>
            </details>
            <details class="nav-menu">
                <summary>Community</summary>
                <div class="nav-menu-panel">
                    <a href="https://github.com/needsofmany/PluralBridge/discussions">Discussions</a>
                    <a href="https://pluralpedia.org/w/PluralBridge">Pluralpedia</a>
                    <a href="https://mastodon.social/@needsofthemany">Mastodon</a>
                    <a class="github-link" href="https://github.com/needsofmany/PluralBridge">GitHub</a>
                </div>
            </details>
            <details class="nav-menu">
                <summary>About</summary>
                <div class="nav-menu-panel">
                    <a href="/about.html">About PluralBridge</a>
                    <a href="/contact.html">Contact Us</a>
                </div>
            </details>
            <span class="nav-preview-disabled" aria-disabled="true" title="Preview coming after account setup, import review, and member profiles are ready">Preview</span>
        </nav>
    </div>
</header>"""

def slugify(text):
    text = text.lower()
    text = re.sub(r"[^a-z0-9]+", "-", text)
    return text.strip("-") or "section"

def should_preserve_md_link(href):
    normalized = href.split("?", 1)[0].split("#", 1)[0].lstrip("./")
    return normalized.startswith("schema/") or normalized.startswith("docs/schema/")

def inline_markup(text):
    text = html.escape(text, quote=False)

    def image_repl(match):
        alt = html.escape(match.group(1), quote=True)
        src = match.group(2)
        if (
            src.endswith(".md")
            and not re.match(r"^[a-z]+://", src, re.IGNORECASE)
            and not should_preserve_md_link(src)
        ):
            src = src[:-3] + ".html"
        return f'<img src="{html.escape(src, quote=True)}" alt="{alt}">'

    def link_repl(match):
        label = match.group(1)
        href = match.group(2)
        if (
            href.endswith(".md")
            and not re.match(r"^[a-z]+://", href, re.IGNORECASE)
            and not should_preserve_md_link(href)
        ):
            href = href[:-3] + ".html"
        return f'<a href="{html.escape(href, quote=True)}">{label}</a>'

    text = re.sub(r"!\[([^\]]*)\]\(([^)]+)\)", image_repl, text)
    text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", link_repl, text)
    text = re.sub(r"`([^`]+)`", lambda m: "<code>" + html.escape(m.group(1)) + "</code>", text)
    return text

def markdown_to_html(markdown):
    lines = markdown.splitlines()
    out = []
    in_code = False
    code_lang = "text"
    code_lines = []
    in_ul = False
    in_ol = False
    in_table = False

    def split_table_row(line):
        cells = line.strip().strip("|").split("|")
        return [cell.strip() for cell in cells]

    def is_table_separator(line):
        cells = split_table_row(line)
        return bool(cells) and all(re.match(r"^:?-{3,}:?$", cell) for cell in cells)

    def close_lists():
        nonlocal in_ul, in_ol
        if in_ul:
            out.append("</ul>")
            in_ul = False
        if in_ol:
            out.append("</ol>")
            in_ol = False

    def close_table():
        nonlocal in_table
        if in_table:
            out.append("</tbody></table>")
            in_table = False

    def close_blocks():
        close_lists()
        close_table()

    i = 0
    while i < len(lines):
        line = lines[i]
        next_line = lines[i + 1] if i + 1 < len(lines) else ""

        fence = re.match(r"^```(\w+)?\s*$", line)
        if fence:
            if in_code:
                out.append(
                    f'<pre><code class="language-{html.escape(code_lang)}">'
                    + html.escape("\n".join(code_lines))
                    + "</code></pre>"
                )
                in_code = False
                code_lang = "text"
                code_lines = []
            else:
                close_blocks()
                in_code = True
                code_lang = fence.group(1) or "text"
                code_lines = []
            i += 1
            continue

        if in_code:
            code_lines.append(line)
            i += 1
            continue

        if not line.strip():
            close_blocks()
            i += 1
            continue

        if (
            "|" in line
            and line.strip().startswith("|")
            and line.strip().endswith("|")
            and is_table_separator(next_line)
        ):
            close_blocks()
            header_cells = split_table_row(line)
            out.append("<table><thead><tr>")
            for cell in header_cells:
                out.append("<th>" + inline_markup(cell) + "</th>")
            out.append("</tr></thead><tbody>")
            in_table = True
            i += 2
            continue

        if (
            in_table
            and "|" in line
            and line.strip().startswith("|")
            and line.strip().endswith("|")
        ):
            row_cells = split_table_row(line)
            out.append("<tr>")
            for cell in row_cells:
                out.append("<td>" + inline_markup(cell) + "</td>")
            out.append("</tr>")
            i += 1
            continue

        if in_table:
            close_table()

        heading = re.match(r"^(#{1,6})\s+(.*)$", line)
        if heading:
            close_blocks()
            level = len(heading.group(1))
            text = heading.group(2).strip()
            ident = slugify(text)
            out.append(f'<h{level} id="{ident}">{inline_markup(text)}</h{level}>')
            i += 1
            continue

        bullet = re.match(r"^\s*-\s+(.*)$", line)
        if bullet:
            if not in_ul:
                close_blocks()
                out.append("<ul>")
                in_ul = True
            out.append("<li>" + inline_markup(bullet.group(1)) + "</li>")
            i += 1
            continue

        numbered = re.match(r"^\s*\d+\.\s+(.*)$", line)
        if numbered:
            if not in_ol:
                close_blocks()
                out.append("<ol>")
                in_ol = True
            out.append("<li>" + inline_markup(numbered.group(1)) + "</li>")
            i += 1
            continue

        close_blocks()
        out.append("<p>" + inline_markup(line) + "</p>")
        i += 1

    if in_code:
        out.append(
            f'<pre><code class="language-{html.escape(code_lang)}">'
            + html.escape("\n".join(code_lines))
            + "</code></pre>"
        )

    close_blocks()
    return "\n".join(out)

def page_title(markdown, fallback):
    for line in markdown.splitlines():
        m = re.match(r"^#\s+(.*)$", line)
        if m:
            return m.group(1).strip()
    return fallback

def render_page(md_path):
    markdown = md_path.read_text(encoding="utf-8")
    title = page_title(markdown, md_path.stem.replace("-", " ").title())
    body = markdown_to_html(markdown)
    html_path = md_path.with_suffix(".html")
    html_path.write_text(f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{html.escape(title)} — PluralBridge Documentation</title>
  <meta name="description" content="{html.escape(title)}. PluralBridge documentation published by Needs of the Many.">
  <link rel="stylesheet" href="/style.css">
<link rel="stylesheet" href="/vendor/prism/prism.css">
<link rel="stylesheet" href="/vendor/prism/pluralbridge-code-theme.css">
</head>
<body>
{NAV}
<main class="page">
  <article class="card doc-page">
{body}
  </article>
</main>
<script src="/vendor/prism/prism.js"></script>
<script src="/vendor/prism/prism-autoloader.js"></script>
<script>
if (window.Prism && window.Prism.plugins && window.Prism.plugins.autoloader) {{
    window.Prism.plugins.autoloader.languages_path = "/vendor/prism/components/";
}}
</script>
</body>
</html>
""", encoding="utf-8")
    print(f"generated {html_path.relative_to(ROOT)}")

def update_docs_index():
    path = WEBSITE / "docs.html"
    text = path.read_text(encoding="utf-8")

    def repl(match):
        href = match.group(1)
        if re.match(r"^[a-z]+://", href, re.IGNORECASE):
            return f'href="{href}"'
        if href.endswith(".md") and not should_preserve_md_link(href):
            href = href[:-3] + ".html"
        return f'href="{href}"'

    text = re.sub(r'href="([^"]+)"', repl, text)
    path.write_text(text, encoding="utf-8")
    print("updated website/docs.html links")

def sync_public_docs_sources():
    sync_pairs = [
        (DOCS_SOURCE / "schema", DOCS / "schema"),
        (DOCS_SOURCE / "system-modeling", DOCS / "system-modeling"),
    ]

    copied = 0
    for source_dir, target_dir in sync_pairs:
        if not source_dir.exists():
            continue

        for source_file in source_dir.rglob("*"):
            if not source_file.is_file():
                continue

            rel = source_file.relative_to(source_dir)
            target_file = target_dir / rel
            target_file.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source_file, target_file)
            copied += 1

    print(f"synced {copied} public docs files into website/docs")

def main():
    sync_public_docs_sources()
    for md_path in sorted(DOCS.rglob("*.md")):
        render_page(md_path)
    update_docs_index()

if __name__ == "__main__":
    main()
