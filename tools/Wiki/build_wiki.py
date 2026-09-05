#!/usr/bin/env python3
"""Build the player wiki without dropping styles for an already-running server."""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import tempfile
from pathlib import Path


def run(command: list[str], cwd: Path) -> None:
    print("+", " ".join(command), flush=True)
    subprocess.run(command, cwd=cwd, check=True)


def restore_previous_files(snapshot_root: Path, target_root: Path, suffix: str) -> int:
    if not snapshot_root.exists():
        return 0

    restored = 0
    for previous_file in snapshot_root.rglob(f"*{suffix}"):
        target = target_root / previous_file.relative_to(snapshot_root)
        if target.exists():
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(previous_file, target)
        restored += 1
    return restored


def main() -> int:
    parser = argparse.ArgumentParser(description="Build the DungeonStory wiki and retain live-server stylesheet continuity.")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--game-version", required=True)
    arguments = parser.parse_args()

    repo_root = arguments.repo_root.resolve()
    wiki_root = repo_root / "wiki"
    asset_root = wiki_root / "dist" / "client" / "_astro"
    chunk_root = wiki_root / "dist" / "server" / "chunks"

    with tempfile.TemporaryDirectory(prefix="dungeonstory-wiki-assets-") as temporary_directory:
        snapshot_root = Path(temporary_directory) / "_astro"
        chunk_snapshot_root = Path(temporary_directory) / "chunks"
        if asset_root.exists():
            shutil.copytree(asset_root, snapshot_root)
        if chunk_root.exists():
            shutil.copytree(chunk_root, chunk_snapshot_root)

        npm = "npm.cmd" if os.name == "nt" else "npm"
        npx = "npx.cmd" if os.name == "nt" else "npx"
        run([npm, "run", "model"], wiki_root)
        run([npm, "run", "validate:model"], wiki_root)
        run([npm, "run", "validate:authority"], wiki_root)
        run([npx, "astro", "build"], wiki_root)
        restored_stylesheets = restore_previous_files(snapshot_root, asset_root, ".css")
        restored_chunks = restore_previous_files(chunk_snapshot_root, chunk_root, ".mjs")

    print(f"preserved_stylesheets={restored_stylesheets}")
    print(f"preserved_server_chunks={restored_chunks}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
