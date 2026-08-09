#!/usr/bin/env python3
"""Fail-closed GGUF packaging gate for StreamingAssets."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil
import sys


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True, type=pathlib.Path)
    parser.add_argument("--host-windows", required=True, type=pathlib.Path)
    parser.add_argument("--host-linux", required=True, type=pathlib.Path)
    parser.add_argument("--evaluation", required=True, type=pathlib.Path)
    parser.add_argument("--model-version", required=True)
    parser.add_argument("--destination", required=True, type=pathlib.Path)
    args = parser.parse_args()

    evaluation = json.loads(args.evaluation.read_text(encoding="utf-8"))
    if not evaluation.get("passed"):
        raise SystemExit("Held-out evaluation did not pass; release packaging refused.")
    if args.model.stat().st_size > 1_500_000_000:
        raise SystemExit("Q4_K_M model exceeds the 1.5 GB contract.")
    for artifact in (args.model, args.host_windows, args.host_linux):
        if not artifact.is_file() or artifact.stat().st_size <= 0:
            raise SystemExit(f"Missing release artifact: {artifact}")

    args.destination.mkdir(parents=True, exist_ok=True)
    model_name = "DungeonStory-Qwen3-1.7B-Q4_K_M.gguf"
    shutil.copy2(args.model, args.destination / model_name)
    shutil.copy2(args.host_windows, args.destination / "DungeonStoryLlmHost.exe")
    shutil.copy2(args.host_linux, args.destination / "DungeonStoryLlmHost")
    manifest = {
        "protocolVersion": 25,
        "hostWindows": "DungeonStoryLlmHost.exe",
        "hostLinux": "DungeonStoryLlmHost",
        "hostWindowsSha256": sha256(args.host_windows),
        "hostLinuxSha256": sha256(args.host_linux),
        "modelFile": model_name,
        "modelSha256": sha256(args.model),
        "maximumModelBytes": 1_500_000_000,
        "modelVersion": args.model_version,
        "evaluationSha256": sha256(args.evaluation),
    }
    (args.destination / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(manifest, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
