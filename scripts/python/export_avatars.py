#!/usr/bin/env python3
"""Export Simply Plural member avatar images from an existing members.json file."""

from __future__ import annotations

import argparse
import json
import sys
import urllib.request
from pathlib import Path

from pluralbridge.paths import ensure_dir


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Download member avatars referenced by members.json."
    )
    parser.add_argument(
        "--members-json",
        default="exports/json/members.json",
        help="Path to exported members.json. Default: exports/json/members.json",
    )
    parser.add_argument(
        "--output-dir",
        default="exports/member_images",
        help="Directory for avatar images. Default: exports/member_images",
    )
    parser.add_argument(
        "--manifest",
        default="exports/avatar_manifest.tsv",
        help="Avatar manifest TSV path. Default: exports/avatar_manifest.tsv",
    )
    return parser.parse_args()


def avatar_url_for_member(row: dict) -> str:
    content = row.get("content", {}) or {}

    avatar_url = content.get("avatarUrl", "")
    if avatar_url:
        return avatar_url

    system_uid = content.get("uid", "")
    avatar_uuid = content.get("avatarUuid", "")

    if system_uid and avatar_uuid:
        return f"https://spaces.apparyllis.com/avatars/{system_uid}/{avatar_uuid}"

    return ""


def extension_from_content_type(content_type: str) -> str:
    content_type = content_type.split(";")[0].strip().lower()

    if content_type == "image/png":
        return ".png"
    if content_type == "image/jpeg":
        return ".jpg"
    if content_type == "image/gif":
        return ".gif"
    if content_type == "image/webp":
        return ".webp"

    return ".img"


def main() -> int:
    args = parse_args()

    members_path = Path(args.members_json)
    output_dir = ensure_dir(args.output_dir)
    manifest_path = Path(args.manifest)
    ensure_dir(manifest_path.parent)

    manifest_rows: list[tuple[str, str, str, str, str, str]] = []

    try:
        with members_path.open("r", encoding="utf-8") as f:
            members = json.load(f)

        count = 0

        for row in members:
            member_id = row.get("id", "")
            if not member_id:
                continue

            content = row.get("content", {}) or {}
            system_uid = content.get("uid", "")
            avatar_uuid = content.get("avatarUuid", "")

            url = avatar_url_for_member(row)
            if not url:
                continue

            print(f"Downloading avatar for {member_id}", flush=True)

            request = urllib.request.Request(
                url,
                headers={
                    "Accept": "image/*",
                    "User-Agent": "PluralBridge/0.1",
                },
                method="GET",
            )

            with urllib.request.urlopen(request) as response:
                data = response.read()
                extension = extension_from_content_type(
                    response.headers.get("Content-Type", "")
                )

            local_filename = f"{member_id}{extension}"
            target = output_dir / local_filename
            target.write_bytes(data)

            manifest_rows.append(
                (
                    member_id,
                    system_uid,
                    avatar_uuid,
                    url,
                    local_filename,
                    str(target.as_posix()),
                )
            )

            count += 1

        with manifest_path.open("w", encoding="utf-8", newline="\n") as f:
            f.write(
                "member_id\tsystem_uid\tavatar_uuid\tsource_url\tlocal_filename\tlocal_path\n"
            )
            for row in manifest_rows:
                f.write("\t".join(row) + "\n")

    except Exception as exc:
        print(f"Avatar export failed: {exc}", file=sys.stderr)
        return 1

    print(f"Avatar export complete: {count} files written to {output_dir}")
    print(f"Avatar manifest written to: {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
