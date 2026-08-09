#!/usr/bin/env python3
"""Compare two generated V25 corpus directories byte-for-byte."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def inventory(root: Path) -> dict[str, str]:
    return {
        path.relative_to(root).as_posix(): digest(path)
        for path in sorted(root.rglob("*"))
        if path.is_file()
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("left", type=Path)
    parser.add_argument("right", type=Path)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()

    left = inventory(args.left.resolve())
    right = inventory(args.right.resolve())
    differing = sorted(path for path in left.keys() & right.keys() if left[path] != right[path])
    result = {
        "passed": left == right,
        "leftFileCount": len(left),
        "rightFileCount": len(right),
        "missingFromRight": sorted(left.keys() - right.keys()),
        "missingFromLeft": sorted(right.keys() - left.keys()),
        "hashMismatches": differing,
    }
    rendered = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
