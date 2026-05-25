#!/usr/bin/env python3
"""Guided regular-user launcher for local Simply Plural export."""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
from pathlib import Path

try:
    sys.stdout.reconfigure(line_buffering=True)
except Exception:
    pass


def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Guided regular-user PluralBridge export helper.")
    parser.add_argument("--check-only", action="store_true", help="Check startup requirements without prompting for a token or running export.")
    return parser.parse_args()


def print_header() -> None:
    print("PluralBridge guided local export")
    print("=" * 36)
    print()
    print("This helper exports Simply Plural data to local files on this computer.")
    print("It does not upload your exported data to PluralBridge.")
    print("Your Simply Plural API token is used for this run only.")
    print()


def require_file(path: Path) -> bool:
    if path.exists():
        print(f"OK: found {path.as_posix()}")
        return True
    print(f"ERROR: missing {path.as_posix()}")
    return False


def confirm_repo_files(root: Path) -> bool:
    print("Checking PluralBridge files...")
    needed = [
        root / "scripts" / "python" / "export_json.py",
        root / "scripts" / "python" / "export_avatars.py",
        root / "scripts" / "python" / "pluralbridge" / "config.py",
    ]
    ok = True
    for path in needed:
        ok = require_file(path.relative_to(root)) and ok
    print()
    return ok


def yes_no(prompt: str, default: bool) -> bool:
    suffix = " [Y/n]: " if default else " [y/N]: "
    while True:
        value = input(prompt + suffix).strip().lower()
        if not value:
            return default
        if value in {"y", "yes"}:
            return True
        if value in {"n", "no"}:
            return False
        print("Please answer yes or no.")


def read_token() -> str:
    existing = os.environ.get("SP_TOKEN", "").strip()
    if existing:
        print("SP_TOKEN is already set for this command window.")
        if yes_no("Use the existing SP_TOKEN value", True):
            return existing
        print()

    print("Paste your Simply Plural API token below.")
    print("For compatibility with Git Bash and Windows shells, the token may be visible while you paste it.")
    token = input("Simply Plural API token: ").strip()
    if not token:
        raise RuntimeError("No API token was entered.")

    return token


def run_child(root: Path, args: list[str], token: str | None = None) -> int:
    env = os.environ.copy()
    if token is not None:
        env["SP_TOKEN"] = token
    result = subprocess.run(args, cwd=root, env=env)
    return int(result.returncode)


def count_files(path: Path) -> int:
    if not path.exists():
        return 0
    return sum(1 for item in path.rglob("*") if item.is_file())


def show_summary(root: Path, ran_avatars: bool) -> None:
    json_dir = root / "exports" / "json"
    notes_dir = root / "exports" / "notes"
    avatar_dir = root / "exports" / "member_images"
    manifest_path = root / "exports" / "avatar_manifest.tsv"

    print()
    print("Export locations")
    print("-" * 16)
    print(f"JSON files:   {json_dir}")
    print(f"JSON count:   {count_files(json_dir)}")
    print(f"Notes files:  {notes_dir}")
    print(f"Notes count:  {count_files(notes_dir)}")
    if ran_avatars:
        print(f"Avatar files: {avatar_dir}")
        print(f"Avatar count: {count_files(avatar_dir)}")
        print(f"Avatar list:  {manifest_path}")
    print()
    print("Back up the exports folder and keep it private:")
    print(root / "exports")


def main() -> int:
    args = parse_args()
    root = repo_root()
    os.chdir(root)

    print_header()
    print(f"Python: {sys.executable}")
    print(f"Folder: {root}")
    print()

    if not confirm_repo_files(root):
        print("This launcher must be run from a complete PluralBridge folder.")
        return 1

    if args.check_only:
        print("Check-only mode complete. No token was requested and no export was run.")
        return 0

    try:
        token = read_token()
    except Exception as exc:
        print(f"Token setup failed: {exc}")
        return 1

    print()
    print("Running JSON and notes export...")
    json_code = run_child(root, [sys.executable, "scripts/python/export_json.py"], token)
    if json_code != 0:
        print()
        print("JSON export failed. No avatar export will be run.")
        return json_code

    print()
    ran_avatars = False
    if yes_no("Run avatar export too", True):
        members_json = root / "exports" / "json" / "members.json"
        if not members_json.exists():
            print("Avatar export skipped because exports/json/members.json was not found.")
        else:
            print("Running avatar export...")
            avatar_code = run_child(root, [sys.executable, "scripts/python/export_avatars.py"])
            if avatar_code != 0:
                print("Avatar export failed.")
                return avatar_code
            ran_avatars = True
    else:
        print("Avatar export skipped.")

    show_summary(root, ran_avatars)
    print()
    print("Done.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
