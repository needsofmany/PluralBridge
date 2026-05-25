#!/usr/bin/env python3
"""Export Simply Plural JSON data through the public Apparyllis REST API."""

from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError

from pluralbridge.api import get_json
from pluralbridge.config import DEFAULT_API_BASE, PluralBridgeConfig
from pluralbridge.paths import ensure_dir, write_json


REQUEST_DELAY_SECONDS = 0.15


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Export Simply Plural JSON data to local files."
    )
    parser.add_argument(
        "--api-base",
        default=DEFAULT_API_BASE,
        help="API base URL. Default: https://api.apparyllis.com",
    )
    parser.add_argument(
        "--output-dir",
        default="exports/json",
        help="Directory for exported JSON files. Default: exports/json",
    )
    parser.add_argument(
        "--notes-dir",
        default="exports/notes",
        help="Directory for exported note JSON files. Default: exports/notes",
    )
    parser.add_argument(
        "--skip-notes",
        action="store_true",
        help="Skip per-member notes export.",
    )
    return parser.parse_args()


def fetch_json(
    config: PluralBridgeConfig,
    endpoint: str,
    *,
    required: bool,
) -> tuple[bool, Any, str]:
    print(f"Fetching {endpoint}", flush=True)

    try:
        data = get_json(config.api_base, endpoint, config.token)
        return True, data, ""
    except HTTPError as exc:
        message = f"HTTP {exc.code} from {endpoint}"
    except URLError as exc:
        message = f"URL error from {endpoint}: {exc.reason}"
    except Exception as exc:
        message = f"{type(exc).__name__} from {endpoint}: {exc}"

    if required:
        raise RuntimeError(message)

    print(f"Warning: skipped optional endpoint: {message}", file=sys.stderr)
    return False, [], message


def extract_uid(me_json: Any) -> str:
    if not isinstance(me_json, dict):
        raise RuntimeError("Could not determine user ID from /v1/me response.")

    value = (
        me_json.get("id")
        or me_json.get("uid")
        or me_json.get("_id")
        or me_json.get("content", {}).get("id")
        or me_json.get("content", {}).get("uid")
    )

    if not value:
        raise RuntimeError("Could not determine user ID from /v1/me response.")

    return str(value)


def extract_member_id(member: dict[str, Any]) -> str:
    return str(
        member.get("id")
        or member.get("uid")
        or member.get("_id")
        or member.get("uuid")
        or ""
    )


def member_list_from_members_json(members_json: Any) -> list[dict[str, Any]]:
    if isinstance(members_json, list):
        return [m for m in members_json if isinstance(m, dict)]

    if isinstance(members_json, dict):
        for key in ("members", "data", "results"):
            value = members_json.get(key)
            if isinstance(value, list):
                return [m for m in value if isinstance(m, dict)]

    return []


def export_named_endpoint(
    config: PluralBridgeConfig,
    output_dir: Path,
    manifest: dict[str, Any],
    *,
    filename: str,
    endpoint: str,
    required: bool,
) -> Any:
    ok, data, error = fetch_json(config, endpoint, required=required)
    write_json(output_dir / filename, data)

    entry = {
        "endpoint": endpoint,
        "filename": filename,
        "required": required,
        "ok": ok,
    }

    if error:
        entry["error"] = error
        manifest["errors"].append(entry)

    manifest["files"].append(entry)
    print(f"saved {endpoint} -> {filename}", flush=True)

    time.sleep(REQUEST_DELAY_SECONDS)
    return data


def main() -> int:
    args = parse_args()

    config = PluralBridgeConfig(
        api_base=args.api_base,
        output_dir=args.output_dir,
    )

    output_dir = ensure_dir(config.output_dir)
    notes_dir = ensure_dir(args.notes_dir)

    manifest: dict[str, Any] = {
        "base_url": config.api_base,
        "files": [],
        "errors": [],
    }

    try:
        me = export_named_endpoint(
            config,
            output_dir,
            manifest,
            filename="me.json",
            endpoint="/v1/me",
            required=True,
        )

        uid = extract_uid(me)

        endpoint_specs = [
            ("user.json", f"/v1/user/{uid}", False),
            ("members.json", f"/v1/members/{uid}", True),
            ("groups.json", f"/v1/groups/{uid}", False),
            ("customFronts.json", f"/v1/customFronts/{uid}", False),
            ("frontHistory.json", "/v1/frontHistory", False),
            (
                "fronthistory_starttime_and_endtime.json",
                f"/v1/frontHistory/{uid}?startTime=0&endTime=9999999999999",
                True,
            ),
            ("timers__automated.json", f"/v1/timers/automated/{uid}", False),
            ("timers__repeated.json", f"/v1/timers/repeated/{uid}", False),
            ("polls.json", f"/v1/polls/{uid}", False),
            ("customFields.json", f"/v1/customFields/{uid}", True),
            ("privacyBuckets.json", "/v1/privacyBuckets", False),
            ("filters.json", "/v1/filters", False),
            ("categories.json", "/v1/chat/categories", False),
            ("channels.json", "/v1/chat/channels", False),
            ("friends.json", "/v1/friends/", False),
        ]

        exported: dict[str, Any] = {}

        for filename, endpoint, required in endpoint_specs:
            exported[filename] = export_named_endpoint(
                config,
                output_dir,
                manifest,
                filename=filename,
                endpoint=endpoint,
                required=required,
            )

        if not args.skip_notes:
            members_json = exported.get("members.json")
            member_list = member_list_from_members_json(members_json)

            for index, member in enumerate(member_list, start=1):
                member_id = extract_member_id(member)
                if not member_id:
                    continue

                endpoint = f"/v1/notes/{uid}/{member_id}"
                filename = f"{index}.json"

                ok, data, error = fetch_json(config, endpoint, required=False)
                write_json(notes_dir / filename, data)

                entry = {
                    "endpoint": endpoint,
                    "filename": f"notes/{filename}",
                    "required": False,
                    "ok": ok,
                }

                if error:
                    entry["error"] = error
                    manifest["errors"].append(entry)

                manifest["files"].append(entry)
                print(f"saved {endpoint} -> notes/{filename}", flush=True)

                time.sleep(REQUEST_DELAY_SECONDS)

        write_json(output_dir / "manifest.json", manifest)

    except Exception as exc:
        print(f"Export failed: {exc}", file=sys.stderr)
        return 1

    print(f"Export complete: {output_dir}")
    print(f"Notes directory: {notes_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
