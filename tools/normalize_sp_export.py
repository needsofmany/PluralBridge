#!/usr/bin/env python3
"""Normalize selected top-level sections from an official Simply Plural export.

This first probe writes PluralBridge-like JSON files from top-level arrays only.
It does not print private values. Console output is limited to file names, counts, and warnings.
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


SECTION_MAP = {
    "members.json": "members",
    "groups.json": "groups",
    "frontHistory.json": "frontHistory",
    "customFields.json": "customFields",
    "privacyBuckets.json": "privacyBuckets",
    "polls.json": "polls",
    "timers__automated.json": "automatedReminders",
    "timers__repeated.json": "repeatedReminders",
    "friends.json": "friends",
    "categories.json": "channelCategories",
    "channels.json": "channels",
}


SENSITIVE_TOP_LEVEL_SECTIONS = [
    "tokens",
    "securityLogs",
    "chatMessages",
    "messages",
    "private",
    "privateFront",
    "sharedFront",
    "usage",
    "reports",
    "dataExports",
    "avatarExports",
]


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def item_count(value: Any) -> int:
    if isinstance(value, list):
        return len(value)
    if isinstance(value, dict):
        return len(value)
    if value is None:
        return 0
    return 1


def normalize_user_files(source: dict[str, Any], output_dir: Path, manifest_entries: list[dict[str, Any]]) -> None:
    users = source.get("users")
    if isinstance(users, list) and users:
        write_json(output_dir / "user.json", users[0])
        manifest_entries.append({"file": "user.json", "source": "users[0]", "count": 1})
        write_json(output_dir / "me.json", users[0])
        manifest_entries.append({"file": "me.json", "source": "users[0]", "count": 1})
    elif isinstance(users, list):
        write_json(output_dir / "user.json", {})
        manifest_entries.append({"file": "user.json", "source": "users", "count": 0})
        write_json(output_dir / "me.json", {})
        manifest_entries.append({"file": "me.json", "source": "users", "count": 0})


def normalize_notes(source: dict[str, Any], output_dir: Path, manifest_entries: list[dict[str, Any]]) -> None:
    notes = source.get("notes")
    notes_dir = output_dir / "notes"
    notes_dir.mkdir(parents=True, exist_ok=True)

    if not isinstance(notes, list):
        manifest_entries.append({"file": "notes/", "source": "notes", "count": 0})
        return

    for index, note in enumerate(notes, start=1):
        write_json(notes_dir / f"{index}.json", note)

    manifest_entries.append({"file": "notes/", "source": "notes", "count": len(notes)})


def normalize_export(input_path: Path, output_root: Path) -> list[str]:
    source = load_json(input_path)
    if not isinstance(source, dict):
        raise ValueError("Official export root must be a JSON object for this probe.")

    json_dir = output_root / "json"
    json_dir.mkdir(parents=True, exist_ok=True)

    lines: list[str] = []
    manifest_entries: list[dict[str, Any]] = []

    lines.append(f"Input file: {input_path}")
    lines.append(f"Output root: {output_root}")
    lines.append("")
    lines.append("Written PluralBridge-shaped files:")

    for output_name, source_key in SECTION_MAP.items():
        value = source.get(source_key, [])
        write_json(json_dir / output_name, value)
        count = item_count(value)
        manifest_entries.append({"file": output_name, "source": source_key, "count": count})
        lines.append(f"  - {output_name}: source {source_key}, count {count}")

    normalize_user_files(source, json_dir, manifest_entries)
    lines.append("  - user.json: source users[0] when present")
    lines.append("  - me.json: source users[0] when present")

    normalize_notes(source, output_root, manifest_entries)
    notes_count = len(source.get("notes", [])) if isinstance(source.get("notes"), list) else 0
    lines.append(f"  - notes/: source notes, count {notes_count}")

    manifest = {
        "created_at_utc": datetime.now(timezone.utc).isoformat(),
        "input_file_name": input_path.name,
        "source_format": "official_simply_plural_export_probe",
        "entries": manifest_entries,
    }
    write_json(json_dir / "manifest.json", manifest)
    lines.append("  - manifest.json: generated probe manifest")
    lines.append("")

    lines.append("Sensitive top-level sections intentionally not copied by this first probe:")
    copied_sources = set(SECTION_MAP.values()) | {"users", "notes"}
    for key in SENSITIVE_TOP_LEVEL_SECTIONS:
        if key in source and key not in copied_sources:
            lines.append(f"  - {key}: present, not copied")
    lines.append("")

    lines.append("Probe result: wrote files for shape comparison only. Console output contains counts, not private values.")
    return lines


def main() -> int:
    parser = argparse.ArgumentParser(description="Normalize official Simply Plural export into a PluralBridge-like folder shape.")
    parser.add_argument("input_file", help="Path to the official Simply Plural export JSON file.")
    parser.add_argument("--output-root", default="exports/from_sp_export", help="Output root folder.")
    args = parser.parse_args()

    lines = normalize_export(Path(args.input_file), Path(args.output_root))
    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
