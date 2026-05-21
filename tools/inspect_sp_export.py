#!/usr/bin/env python3
"""Inspect the structure of an official Simply Plural export without printing private values.

This script reports container types, counts, and keys only. It is intended as a safe first pass
for deciding whether an official Simply Plural export can be normalized into the PluralBridge
JSON tree shape.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


EXPECTED_PLURALBRIDGE_AREAS = {
    "members.json": ["members"],
    "groups.json": ["groups"],
    "frontHistory.json": ["frontHistory", "front_history", "fronthistory"],
    "customFields.json": ["customFields", "custom_fields", "customfields"],
    "customFronts.json": ["customFronts", "custom_fronts", "customfronts"],
    "privacyBuckets.json": ["privacyBuckets", "privacy_buckets", "privacybuckets"],
    "polls.json": ["polls"],
    "timers__automated.json": ["automatedReminders", "automatedTimers", "timersAutomated", "timers__automated"],
    "timers__repeated.json": ["repeatedReminders", "repeatedRemidners", "timersRepeated", "timers__repeated"],
    "friends.json": ["friends"],
    "categories.json": ["channelCategories", "categories", "chatCategories"],
    "channels.json": ["channels", "chatChannels"],
    "filters.json": ["filters"],
    "me.json": ["me"],
    "user.json": ["users", "user"],
    "member notes": ["notes", "memberNotes", "member_notes"],
    "avatar manifest": ["avatarExports", "avatars", "avatar", "avatarUrl", "avatarUuid", "avatarURL"],
}

SENSITIVE_TOP_LEVEL_KEYS = {
    "tokens",
    "securityLogs",
    "chatMessages",
    "messages",
    "friends",
    "notes",
    "private",
    "privateFront",
    "sharedFront",
    "usage",
    "reports",
    "dataExports",
    "avatarExports",
}

SENSITIVE_NESTED_KEYS = {
    "token",
    "ip",
    "message",
    "note",
    "url",
    "avatarUrl",
    "avatarUuid",
    "notificationToken",
}


def describe_type(value: Any) -> str:
    if isinstance(value, dict):
        return f"dict, keys {len(value)}"
    if isinstance(value, list):
        return f"list, count {len(value)}"
    if value is None:
        return "null"
    return type(value).__name__


def sample_keys_from_item(value: Any) -> list[str]:
    if isinstance(value, dict):
        return sorted(str(key) for key in value.keys())
    if isinstance(value, list) and value and isinstance(value[0], dict):
        return sorted(str(key) for key in value[0].keys())
    return []


def find_keys_recursive(value: Any, wanted: set[str], prefix: str = "") -> dict[str, list[str]]:
    found: dict[str, list[str]] = {}

    if isinstance(value, dict):
        for key, child in value.items():
            key_text = str(key)
            path = f"{prefix}.{key_text}" if prefix else key_text
            if key_text in wanted:
                found.setdefault(key_text, []).append(path)
            child_found = find_keys_recursive(child, wanted, path)
            for child_key, paths in child_found.items():
                found.setdefault(child_key, []).extend(paths)
    elif isinstance(value, list):
        for index, child in enumerate(value[:5]):
            path = f"{prefix}[{index}]" if prefix else f"[{index}]"
            child_found = find_keys_recursive(child, wanted, path)
            for child_key, paths in child_found.items():
                found.setdefault(child_key, []).extend(paths)

    return found


def inspect_export(input_path: Path) -> str:
    raw_text = input_path.read_text(encoding="utf-8-sig")
    data = json.loads(raw_text)

    lines: list[str] = []
    lines.append(f"Input file: {input_path}")
    lines.append(f"File size: {input_path.stat().st_size} bytes")
    lines.append(f"Top-level type: {describe_type(data)}")
    lines.append("")

    if isinstance(data, dict):
        top_keys = sorted(str(key) for key in data.keys())
        lines.append("Top-level keys:")
        for key in top_keys:
            lines.append(f"  - {key}: {describe_type(data[key])}")
        lines.append("")

        lines.append("Top-level sections and sample nested keys:")
        for key in top_keys:
            nested_keys = sample_keys_from_item(data[key])
            if nested_keys:
                lines.append(f"  - {key}: sample keys {nested_keys}")
            else:
                lines.append(f"  - {key}: no nested object keys sampled")
        lines.append("")
    elif isinstance(data, list):
        lines.append(f"Top-level list count: {len(data)}")
        lines.append(f"Top-level list sample keys: {sample_keys_from_item(data)}")
        lines.append("")

    wanted_keys = {candidate for candidates in EXPECTED_PLURALBRIDGE_AREAS.values() for candidate in candidates}
    recursive_hits = find_keys_recursive(data, wanted_keys)

    lines.append("Likely PluralBridge mappings:")
    for area, candidates in EXPECTED_PLURALBRIDGE_AREAS.items():
        matched_paths: list[str] = []
        for candidate in candidates:
            matched_paths.extend(recursive_hits.get(candidate, []))
        if matched_paths:
            shown_paths = matched_paths[:8]
            suffix = "" if len(matched_paths) <= 8 else f" ... plus {len(matched_paths) - 8} more"
            lines.append(f"  - {area}: present at {shown_paths}{suffix}")
        else:
            lines.append(f"  - {area}: not found by key-name probe")
    lines.append("")

    if isinstance(data, dict):
        known_top_level = set()
        for candidates in EXPECTED_PLURALBRIDGE_AREAS.values():
            known_top_level.update(candidates)
        unknown = [key for key in sorted(str(key) for key in data.keys()) if key not in known_top_level]
        lines.append("Top-level keys not directly recognized by current probe:")
        if unknown:
            for key in unknown:
                lines.append(f"  - {key}")
        else:
            lines.append("  - none")
        lines.append("")

        lines.append("Sensitive or private-bearing top-level sections present:")
        sensitive_present = sorted(key for key in data.keys() if str(key) in SENSITIVE_TOP_LEVEL_KEYS)
        if sensitive_present:
            for key in sensitive_present:
                lines.append(f"  - {key}: {describe_type(data[key])}")
        else:
            lines.append("  - none detected by key-name probe")
        lines.append("")

    sensitive_hits = find_keys_recursive(data, SENSITIVE_NESTED_KEYS)
    lines.append("Sensitive nested key names detected:")
    if sensitive_hits:
        for key in sorted(sensitive_hits.keys()):
            paths = sensitive_hits[key]
            shown_paths = paths[:8]
            suffix = "" if len(paths) <= 8 else f" ... plus {len(paths) - 8} more"
            lines.append(f"  - {key}: present at {shown_paths}{suffix}")
    else:
        lines.append("  - none detected by key-name probe")
    lines.append("")

    lines.append("Preliminary conclusion:")
    lines.append("  - Official export appears suitable for a PluralBridge normalizer probe if the mapped sections contain usable records.")
    lines.append("  - Official export must be treated as secret-bearing because token/security/private sections may be present.")
    lines.append("  - Next useful test is a no-values normalizer dry run that writes section files from top-level arrays only.")

    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description="Safely inspect official Simply Plural export structure.")
    parser.add_argument("input_file", help="Path to the official Simply Plural export JSON file.")
    parser.add_argument("--report", help="Optional path to write the structural report.")
    args = parser.parse_args()

    input_path = Path(args.input_file)
    report = inspect_export(input_path)

    if args.report:
        report_path = Path(args.report)
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(report + "\n", encoding="utf-8")

    print(report)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
