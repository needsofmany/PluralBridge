#!/usr/bin/env python3
"""Export basic Simply Plural JSON data through the public Apparyllis REST API."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from pluralbridge.api import get_json
from pluralbridge.config import DEFAULT_API_BASE, PluralBridgeConfig
from pluralbridge.paths import write_json


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
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    config = PluralBridgeConfig(
        api_base=args.api_base,
        output_dir=args.output_dir,
    )

    output_dir = Path(config.output_dir)

    try:
        me = get_json(config.api_base, "/v1/me", config.token)
        write_json(output_dir / "me.json", me)

        uid = (
            me.get("id")
            or me.get("uid")
            or me.get("content", {}).get("id")
            or me.get("content", {}).get("uid")
        )

        if not uid:
            raise RuntimeError("Could not determine user ID from /v1/me response.")

        endpoints = {
            "members.json": f"/v1/members/{uid}",
            "frontHistory.json": f"/v1/fronters/{uid}/history",
            "customFields.json": f"/v1/customFields/{uid}",
            "privacyBuckets.json": f"/v1/privacyBuckets/{uid}",
            "friends.json": f"/v1/friends/{uid}",
        }

        for filename, endpoint in endpoints.items():
            data = get_json(config.api_base, endpoint, config.token)
            write_json(output_dir / filename, data)

    except Exception as exc:
        print(f"Export failed: {exc}", file=sys.stderr)
        return 1

    print(f"Export complete: {output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
