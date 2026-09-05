#!/usr/bin/env python3
"""Regenerate a snapshot twice and require an identical content digest."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from generate_wiki_model import build_snapshot, repository_root


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify deterministic DungeonStory wiki generation.")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--game-version", required=True)
    args = parser.parse_args()
    try:
        root = repository_root(args.repo_root)
        first = build_snapshot(root, args.game_version)
        second = build_snapshot(root, args.game_version)
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"wiki determinism check failed: {error}", file=sys.stderr)
        return 1
    if first["content_digest"] != second["content_digest"]:
        print("wiki determinism check failed: consecutive snapshot digests differ", file=sys.stderr)
        return 1
    print(json.dumps({"status": "deterministic", "game_version": args.game_version, "content_digest": second["content_digest"]}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
