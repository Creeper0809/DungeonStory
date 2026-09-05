#!/usr/bin/env python3
"""Fail when a generated DungeonStory knowledge artifact is stale or mutated."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from knowledge_manifest import verify_generation_manifest


ROOT = Path(__file__).resolve().parents[2]


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "roots",
        nargs="+",
        help="Generated artifact roots, relative to the project root or absolute.",
    )
    return parser.parse_args(argv)


def resolve_root(value: str) -> Path:
    candidate = Path(value)
    resolved = candidate.resolve() if candidate.is_absolute() else (ROOT / candidate).resolve()
    resolved.relative_to(ROOT.resolve())
    return resolved


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    results = [verify_generation_manifest(ROOT, resolve_root(value)) for value in args.roots]
    summary = {
        "artifact_count": len(results),
        "failure_count": sum(result["failure_count"] for result in results),
        "artifacts": results,
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if summary["failure_count"] == 0 else 2


if __name__ == "__main__":
    sys.exit(main())
