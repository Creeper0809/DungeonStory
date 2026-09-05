#!/usr/bin/env python3
"""Verify a locally staged DungeonStory static-site release bundle."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import zipfile
from pathlib import Path, PurePosixPath


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def site_files(root: Path) -> dict[str, Path]:
    return {
        path.relative_to(root).as_posix(): path
        for path in sorted(root.rglob("*"))
        if path.is_file()
    }


def valid_zip_name(name: str) -> bool:
    path = PurePosixPath(name)
    return not path.is_absolute() and ".." not in path.parts and path.as_posix() != "."


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify a Wiki release bundle before NAS upload.")
    parser.add_argument("--release", type=Path, required=True)
    args = parser.parse_args()
    release = args.release.resolve()
    failures: list[str] = []
    manifest_path = release / "release-manifest.json"
    if not manifest_path.is_file():
        print("release verification failed: release-manifest.json is missing", file=sys.stderr)
        return 1
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        payload = manifest["payload"]
        game_version = manifest["game_version"]
        release_id = manifest["release_id"]
        site_origin = manifest["site_origin"]
    except (OSError, KeyError, TypeError, json.JSONDecodeError) as error:
        print(f"release verification failed: invalid manifest: {error}", file=sys.stderr)
        return 1

    if manifest.get("schema_version") != 1:
        failures.append("unsupported release manifest schema")
    if not isinstance(game_version, str) or not release_id.startswith(f"{game_version}-"):
        failures.append("release id and game version disagree")
    if not isinstance(site_origin, str) or not site_origin.startswith("https://") or site_origin.rstrip("/") != site_origin:
        failures.append("site origin must be a normalized HTTPS origin")
    site = release / payload.get("directory", "")
    archive = release / payload.get("archive", "")
    checksum_file = release / f"{payload.get('archive', '')}.sha256"
    if not site.is_dir():
        failures.append("site payload directory is missing")
    if not archive.is_file():
        failures.append("payload archive is missing")
    if not checksum_file.is_file():
        failures.append("payload checksum file is missing")
    if failures:
        for failure in failures:
            print(f"release verification failed: {failure}", file=sys.stderr)
        return 1

    actual_hash = sha256(archive)
    if actual_hash != payload.get("archive_sha256"):
        failures.append("manifest archive SHA-256 does not match")
    if checksum_file.read_text(encoding="ascii").strip().lower() != actual_hash:
        failures.append("detached archive SHA-256 does not match")

    files = site_files(site)
    if len(files) != payload.get("file_count"):
        failures.append("payload file count does not match manifest")
    if sum(path.stat().st_size for path in files.values()) != payload.get("byte_count"):
        failures.append("payload byte count does not match manifest")
    if "index.html" not in files or "pagefind/pagefind.js" not in files:
        failures.append("payload is missing an HTML root or Pagefind")
    if "index.html" in files:
        index = files["index.html"].read_text(encoding="utf-8")
        canonical = f'<link rel="canonical" href="{site_origin}/">'
        if canonical not in index:
            failures.append("root canonical URL does not match release origin")

    try:
        with zipfile.ZipFile(archive) as bundle:
            entries = {entry.filename: entry for entry in bundle.infolist() if not entry.is_dir()}
            if any(not valid_zip_name(name) for name in entries):
                failures.append("archive contains an unsafe path")
            if set(entries) != set(files):
                failures.append("archive file set does not match site payload")
            else:
                for relative, disk_path in files.items():
                    if hashlib.sha256(bundle.read(entries[relative])).hexdigest() != sha256(disk_path):
                        failures.append(f"archive content differs from site payload: {relative}")
                        break
    except (OSError, zipfile.BadZipFile) as error:
        failures.append(f"archive cannot be read: {error}")

    if failures:
        for failure in failures:
            print(f"release verification failed: {failure}", file=sys.stderr)
        return 1
    print(json.dumps({
        "status": "valid",
        "release_id": release_id,
        "game_version": game_version,
        "site_files": len(files),
        "archive_sha256": actual_hash,
    }, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
