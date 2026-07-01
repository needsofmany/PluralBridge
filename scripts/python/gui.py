#!/usr/bin/env python3
"""PluralBridge GUI — self-contained tkinter exporter.

Uses pluralbridge modules directly (no subprocess) so it works both as a
standalone script and when bundled into a single .exe via PyInstaller.
"""

from __future__ import annotations

import io
import json
import sys
import threading
import tkinter as tk
import urllib.request
from tkinter import filedialog, messagebox, scrolledtext
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError

# ── Resolve pluralbridge package ─────────────────────────────────
# When running from source the package lives at ../../pluralbridge
# relative to this file.  When frozen by PyInstaller it is in the
# temp extraction dir, so we fall back to a direct import.
_here = Path(__file__).resolve().parent
_src_pkg = _here / "pluralbridge"
if _src_pkg.is_dir() and str(_here) not in sys.path:
    sys.path.insert(0, str(_here))

from pluralbridge.api import get_json  # noqa: E402
from pluralbridge.paths import ensure_dir, write_json  # noqa: E402

DEFAULT_API_BASE = "https://api.apparyllis.com"
REQUEST_DELAY = 0.15


# ── Log capture ──────────────────────────────────────────────────

class LogCapture(io.StringIO):
    """Tee print() output to the GUI log widget."""

    def __init__(self, append_fn) -> None:
        super().__init__()
        self._append = append_fn

    def write(self, s: str) -> int:
        super().write(s)
        if s.strip():
            self._append(s)
        return len(s)

    def flush(self) -> None:
        pass


# ── Export logic ─────────────────────────────────────────────────

def export_json(token: str, output_dir: Path, notes_dir: Path,
                skip_notes: bool, append) -> None:
    """Replicate export_json.py logic with GUI output."""
    import time

    def _fetch(endpoint: str) -> tuple[bool, Any, str]:
        append(f"Fetching {endpoint}\n")
        try:
            data = get_json(DEFAULT_API_BASE, endpoint, token)
            return True, data, ""
        except HTTPError as exc:
            msg = f"HTTP {exc.code} from {endpoint}"
        except URLError as exc:
            msg = f"URL error from {endpoint}: {exc.reason}"
        except Exception as exc:
            msg = f"{type(exc).__name__} from {endpoint}: {exc}"
        append(f"  Skipped: {msg}\n")
        return False, [], msg

    def _export(filename: str, endpoint: str,
                manifest: dict, data_dir: Path) -> Any:
        ok, data, error = _fetch(endpoint)
        if ok:
            write_json(data_dir / filename, data)
        entry: dict[str, Any] = {"endpoint": endpoint, "filename": filename, "ok": ok}
        if error:
            entry["error"] = error
            manifest["errors"].append(entry)
        manifest["files"].append(entry)
        append(f"  saved {endpoint} -> {filename}\n")
        time.sleep(REQUEST_DELAY)
        return data

    output_dir = ensure_dir(output_dir)
    notes_dir = ensure_dir(notes_dir)

    manifest: dict[str, Any] = {"base_url": DEFAULT_API_BASE, "files": [], "errors": []}

    me = _export("me.json", "/v1/me", manifest, output_dir)
    if not me:
        raise RuntimeError("/v1/me returned no data — cannot continue without a user ID.")
    uid = (me.get("id") or me.get("uid") or me.get("_id")
           or (me.get("content", {}) or {}).get("id")
           or (me.get("content", {}) or {}).get("uid"))
    if not uid:
        raise RuntimeError("Could not determine user ID from /v1/me.")
    uid = str(uid)

    specs = [
        ("user.json",             f"/v1/user/{uid}"),
        ("members.json",          f"/v1/members/{uid}"),
        ("groups.json",           f"/v1/groups/{uid}"),
        ("customFronts.json",     f"/v1/customFronts/{uid}"),
        ("frontHistory.json",     "/v1/frontHistory"),
        ("fronthistory_starttime_and_endtime.json",
                                  f"/v1/frontHistory/{uid}?startTime=0&endTime=9999999999999"),
        ("timers__automated.json",f"/v1/timers/automated/{uid}"),
        ("timers__repeated.json", f"/v1/timers/repeated/{uid}"),
        ("polls.json",            f"/v1/polls/{uid}"),
        ("customFields.json",     f"/v1/customFields/{uid}"),
        ("privacyBuckets.json",   "/v1/privacyBuckets"),
        ("filters.json",          "/v1/filters"),
        ("categories.json",       "/v1/chat/categories"),
        ("channels.json",         "/v1/chat/channels"),
        ("friends.json",          "/v1/friends/"),
    ]

    exported: dict[str, Any] = {}
    for filename, endpoint in specs:
        exported[filename] = _export(filename, endpoint, manifest, output_dir)

    if not skip_notes:
        members_json = exported.get("members.json")
        members_list: list[dict] = []
        if isinstance(members_json, list):
            members_list = [m for m in members_json if isinstance(m, dict)]
        elif isinstance(members_json, dict):
            for key in ("members", "data", "results"):
                val = members_json.get(key)
                if isinstance(val, list):
                    members_list = [m for m in val if isinstance(m, dict)]
                    break

        for idx, member in enumerate(members_list, start=1):
            mid = str(member.get("id") or member.get("uid") or member.get("_id") or "")
            if not mid:
                continue
            endpoint = f"/v1/notes/{uid}/{mid}"
            fname = f"{idx}.json"
            ok, data, error = _fetch(endpoint)
            if ok:
                write_json(notes_dir / fname, data)
            entry = {"endpoint": endpoint, "filename": f"notes/{fname}", "ok": ok}
            if error:
                entry["error"] = error
                manifest["errors"].append(entry)
            manifest["files"].append(entry)
            append(f"  saved {endpoint} -> notes/{fname}\n")
            time.sleep(REQUEST_DELAY)

    write_json(output_dir / "manifest.json", manifest)
    append(f"\nJSON export complete: {output_dir}\n")
    append(f"Notes directory: {notes_dir}\n")


def export_avatars(members_json_path: Path, output_dir: Path, append) -> None:
    """Replicate export_avatars.py logic with GUI output."""
    output_dir = ensure_dir(output_dir)

    with members_json_path.open("r", encoding="utf-8") as f:
        members = json.load(f)

    manifest_rows: list[str] = []
    count = 0
    skipped = 0
    for row in members:
        mid = row.get("id", "")
        if not mid:
            continue
        content = row.get("content", {}) or {}
        uid = content.get("uid", "")
        avatar_uuid = content.get("avatarUuid", "")
        url = content.get("avatarUrl", "")
        if not url and uid and avatar_uuid:
            url = f"https://spaces.apparyllis.com/avatars/{uid}/{avatar_uuid}"
        if not url:
            continue

        append(f"Downloading avatar for {mid}\n")
        try:
            req = urllib.request.Request(url, headers={"Accept": "image/*",
                                                        "User-Agent": "PluralBridge/0.1"})
            with urllib.request.urlopen(req) as resp:
                data = resp.read()
                ct = resp.headers.get("Content-Type", "").split(";")[0].strip().lower()
        except Exception as exc:
            append(f"  Skipped avatar {mid}: {exc}\n")
            skipped += 1
            continue

        ext = {".png": "image/png", ".jpg": "image/jpeg",
               ".gif": "image/gif", ".webp": "image/webp"}.get(ct, "")
        if not ext:
            for e, m in {".png": "png", ".jpg": "jpeg", ".gif": "gif", ".webp": "webp"}.items():
                if m in ct:
                    ext = e
                    break
            else:
                ext = ".img"

        local = output_dir / f"{mid}{ext}"
        local.write_bytes(data)
        manifest_rows.append(f"{mid}\t{uid}\t{avatar_uuid}\t{url}\t{local.name}\t{local.as_posix()}")
        count += 1

    manifest_path = output_dir.parent / "avatar_manifest.tsv"
    with manifest_path.open("w", encoding="utf-8", newline="\n") as f:
        f.write("member_id\tsystem_uid\tavatar_uuid\tsource_url\tlocal_filename\tlocal_path\n")
        for r in manifest_rows:
            f.write(r + "\n")

    append(f"\nAvatar export complete: {count} saved, {skipped} skipped -> {output_dir}\n")
    append(f"Manifest: {manifest_path}\n")


# ── GUI ──────────────────────────────────────────────────────────

class ExportGUI(tk.Tk):
    def __init__(self) -> None:
        super().__init__()
        self.title("PluralBridge Exporter")
        self.resizable(True, True)
        self.minsize(580, 500)
        self._running = False

        root = Path(__file__).resolve().parents[2] if not getattr(sys, 'frozen', False) else Path(sys.executable).parent
        self._default_output = str(root / "exports")

        self._build_ui()
        self._center(640, 540)

    def _build_ui(self) -> None:
        pad = {"padx": 8, "pady": 4}

        # Token
        tk.Label(self, text="Simply Plural API Token:").pack(anchor="w", **pad)
        tf = tk.Frame(self)
        tf.pack(fill="x", **pad)
        self._token_var = tk.StringVar()
        self._token_entry = tk.Entry(tf, textvariable=self._token_var, show="•", width=60)
        self._token_entry.pack(side="left", fill="x", expand=True)
        self._show_var = tk.BooleanVar()
        tk.Checkbutton(tf, text="Show", variable=self._show_var,
                        command=self._toggle_vis).pack(side="left", padx=(4, 0))

        # Output folder
        tk.Label(self, text="Output Folder:").pack(anchor="w", **pad)
        ff = tk.Frame(self)
        ff.pack(fill="x", **pad)
        self._folder_var = tk.StringVar(value=self._default_output)
        tk.Entry(ff, textvariable=self._folder_var, width=50).pack(side="left", fill="x", expand=True)
        tk.Button(ff, text="Browse…", command=self._browse).pack(side="left", padx=(4, 0))

        # Options
        of = tk.LabelFrame(self, text="Export Options", padx=8, pady=4)
        of.pack(fill="x", **pad)
        self._json_var = tk.BooleanVar(value=True)
        self._avatar_var = tk.BooleanVar(value=True)
        self._notes_var = tk.BooleanVar(value=True)
        tk.Checkbutton(of, text="JSON data (members, groups, front history…)",
                        variable=self._json_var).pack(anchor="w")
        tk.Checkbutton(of, text="Per-member notes",
                        variable=self._notes_var).pack(anchor="w")
        tk.Checkbutton(of, text="Avatar images",
                        variable=self._avatar_var).pack(anchor="w")

        # Export button
        bf = tk.Frame(self)
        bf.pack(fill="x", **pad)
        self._export_btn = tk.Button(bf, text="▶  Export", command=self._start,
                                      bg="#4CAF50", fg="white", font=("Segoe UI", 11, "bold"),
                                      padx=16, pady=4)
        self._export_btn.pack(side="left")
        self._status_var = tk.StringVar(value="Ready")
        tk.Label(bf, textvariable=self._status_var, anchor="w").pack(side="left", padx=12)

        # Log
        tk.Label(self, text="Log:").pack(anchor="w", **pad)
        self._log = scrolledtext.ScrolledText(self, height=14, state="disabled",
                                               font=("Consolas", 9), wrap="word")
        self._log.pack(fill="both", expand=True, padx=8, pady=(0, 8))

    def _center(self, w: int, h: int) -> None:
        sw, sh = self.winfo_screenwidth(), self.winfo_screenheight()
        self.geometry(f"{w}x{h}+{(sw - w) // 2}+{(sh - h) // 2}")

    def _toggle_vis(self) -> None:
        self._token_entry.config(show="" if self._show_var.get() else "•")

    def _browse(self) -> None:
        d = filedialog.askdirectory(initialdir=self._folder_var.get())
        if d:
            self._folder_var.set(d)

    def _append_log(self, text: str) -> None:
        self._log.config(state="normal")
        self._log.insert("end", text)
        self._log.see("end")
        self._log.config(state="disabled")

    def _clear_log(self) -> None:
        self._log.config(state="normal")
        self._log.delete("1.0", "end")
        self._log.config(state="disabled")

    def _set_status(self, msg: str) -> None:
        self._status_var.set(msg)

    def _after(self, fn) -> None:
        self.after(0, fn)

    def _start(self) -> None:
        token = self._token_var.get().strip()
        if not token:
            messagebox.showwarning("Missing Token", "Please paste your Simply Plural API token.")
            return
        if self._running:
            return
        self._running = True
        self._export_btn.config(state="disabled")
        self._clear_log()
        self._set_status("Exporting…")
        threading.Thread(target=self._run, args=(token,), daemon=True).start()

    def _run(self, token: str) -> None:
        output_dir = Path(self._folder_var.get().strip())
        notes_dir = output_dir / "notes"
        avatar_dir = output_dir.parent / "member_images"
        log = lambda t: self._after(lambda t=t: self._append_log(t))

        try:
            if self._json_var.get() or self._notes_var.get():
                log("Starting JSON/notes export…\n")
                old_stdout, old_stderr = sys.stdout, sys.stderr
                sys.stdout = LogCapture(log)
                sys.stderr = sys.stdout
                try:
                    export_json(token, output_dir, notes_dir,
                                skip_notes=not self._notes_var.get(), append=log)
                finally:
                    sys.stdout, sys.stderr = old_stdout, old_stderr

            if self._avatar_var.get():
                log("\nStarting avatar export…\n")
                members_json = output_dir / "members.json"
                if not members_json.exists():
                    log("Skipped: members.json not found (run JSON export first).\n")
                else:
                    old_stdout, old_stderr = sys.stdout, sys.stderr
                    sys.stdout = LogCapture(log)
                    sys.stderr = sys.stdout
                    try:
                        export_avatars(members_json, avatar_dir, append=log)
                    finally:
                        sys.stdout, sys.stderr = old_stdout, old_stderr

            log("\n✓ Export complete!\n")
            self._after(lambda: self._set_status("Done"))
            self._after(lambda: messagebox.showinfo(
                "Export Complete",
                f"Files saved to:\n{output_dir}\n\nBack up this folder and keep it private."))

        except Exception as exc:
            log(f"\n✗ Error: {exc}\n")
            self._after(lambda: self._set_status("Failed"))
        finally:
            self._running = False
            self._after(lambda: self._export_btn.config(state="normal"))


def main() -> int:
    app = ExportGUI()
    app.mainloop()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
