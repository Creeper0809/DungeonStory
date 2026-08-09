#!/usr/bin/env python3
"""Mount the untrained Qwen3-1.7B Q4_K_M base model for local CPU inference.

This is a development integration path, not a release-certification shortcut.
It deliberately refuses training checkpoints and leaves the certified V25
packager/evaluation gate untouched.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import shutil
import subprocess
import sys
import zipfile


LLAMA_TAG = "b10331"
LLAMA_ARCHIVE = f"llama-{LLAMA_TAG}-bin-win-cpu-x64.zip"
LLAMA_URL = (
    f"https://github.com/ggml-org/llama.cpp/releases/download/{LLAMA_TAG}/"
    f"{LLAMA_ARCHIVE}"
)
MODEL_FILE = "Qwen3-1.7B-Q4_K_M.gguf"
MODEL_URL = (
    "https://huggingface.co/ggml-org/Qwen3-1.7B-GGUF/resolve/main/"
    f"{MODEL_FILE}?download=true"
)
MOUNTED_MODEL_FILE = "DungeonStory-Qwen3-1.7B-Q4_K_M.gguf"
MAXIMUM_MODEL_BYTES = 1_500_000_000


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def run_curl(url: str, destination: pathlib.Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    command = [
        "curl.exe" if os.name == "nt" else "curl",
        "-L",
        "--fail",
        "--retry",
        "8",
        "--retry-delay",
        "3",
        "--continue-at",
        "-",
        "--output",
        str(destination),
        url,
    ]
    print(f"Downloading/resuming {destination.name} ...", flush=True)
    subprocess.run(command, check=True)


def copy_if_changed(source: pathlib.Path, destination: pathlib.Path) -> None:
    if destination.is_file() and destination.stat().st_size == source.stat().st_size:
        if sha256(destination) == sha256(source):
            return
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_suffix(destination.suffix + ".partial")
    if temporary.exists():
        temporary.unlink()
    shutil.copy2(source, temporary)
    os.replace(temporary, destination)


def require_gguf(path: pathlib.Path) -> None:
    lowered = str(path).lower()
    if "checkpoint-" in lowered or "adapter_model" in lowered:
        raise SystemExit("Training checkpoints and adapters cannot be mounted as the base GGUF.")
    if not path.is_file() or path.stat().st_size <= 0:
        raise SystemExit(f"Model file is missing: {path}")
    if path.stat().st_size > MAXIMUM_MODEL_BYTES:
        raise SystemExit("Base GGUF exceeds the 1.5 GB runtime contract.")
    with path.open("rb") as stream:
        if stream.read(4) != b"GGUF":
            raise SystemExit("Model does not have a GGUF header.")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--destination",
        type=pathlib.Path,
        default=pathlib.Path("Assets/StreamingAssets/DungeonStoryLlm"),
    )
    parser.add_argument("--cache", type=pathlib.Path)
    parser.add_argument("--model", type=pathlib.Path)
    parser.add_argument("--llama-bin", type=pathlib.Path)
    parser.add_argument("--skip-download", action="store_true")
    args = parser.parse_args()

    if os.name != "nt":
        raise SystemExit("This base-model mount currently packages the Windows CPU host only.")

    default_cache = pathlib.Path(
        os.environ.get("LOCALAPPDATA", pathlib.Path.home() / ".cache")
    ) / "DungeonStory" / "ModelCache"
    cache = (args.cache or default_cache).resolve()
    cache.mkdir(parents=True, exist_ok=True)

    llama_bin = args.llama_bin.resolve() if args.llama_bin else None
    if llama_bin is None:
        previously_downloaded = cache / f"llama-{LLAMA_TAG}-win-cpu-x64" / "bin"
        if (previously_downloaded / "llama-server.exe").is_file():
            llama_bin = previously_downloaded

    if llama_bin is None:
        archive = cache / LLAMA_ARCHIVE
        extracted = cache / f"llama-{LLAMA_TAG}-win-cpu-x64"
        if not archive.is_file() and args.skip_download:
            raise SystemExit(f"Missing llama.cpp archive: {archive}")
        if not archive.is_file():
            run_curl(LLAMA_URL, archive)
        server = extracted / "llama-server.exe"
        if not server.is_file():
            print(f"Extracting {archive.name} ...", flush=True)
            extracted.mkdir(parents=True, exist_ok=True)
            with zipfile.ZipFile(archive) as bundle:
                bundle.extractall(extracted)
        llama_bin = extracted

    server = llama_bin / "llama-server.exe"
    runtime_libraries = sorted(llama_bin.glob("*.dll"), key=lambda path: path.name.lower())
    if not server.is_file() or not runtime_libraries:
        raise SystemExit(f"Incomplete llama.cpp CPU distribution: {llama_bin}")

    model = args.model.resolve() if args.model else cache / MODEL_FILE
    if not model.is_file() and args.skip_download:
        raise SystemExit(f"Missing base GGUF: {model}")
    if not model.is_file():
        run_curl(MODEL_URL, model)
    require_gguf(model)

    destination = args.destination.resolve()
    destination.mkdir(parents=True, exist_ok=True)
    mounted_server = destination / "DungeonStoryLlmHost.exe"
    mounted_model = destination / MOUNTED_MODEL_FILE
    print("Copying verified CPU host ...", flush=True)
    copy_if_changed(server, mounted_server)
    for library in runtime_libraries:
        copy_if_changed(library, destination / library.name)
    print("Copying verified base GGUF ...", flush=True)
    copy_if_changed(model, mounted_model)

    support_files = [
        {"file": library.name, "sha256": sha256(destination / library.name)}
        for library in runtime_libraries
    ]
    manifest = {
        "protocolVersion": 25,
        "hostKind": "LlamaCppServer",
        "hostWindows": mounted_server.name,
        "hostLinux": "",
        "hostWindowsSha256": sha256(mounted_server),
        "hostLinuxSha256": "",
        "supportFiles": support_files,
        "modelFile": mounted_model.name,
        "modelSha256": sha256(mounted_model),
        "maximumModelBytes": MAXIMUM_MODEL_BYTES,
        "modelVersion": f"Qwen3-1.7B-base-Q4_K_M@ggml-org/{LLAMA_TAG}",
        "releaseCertified": False,
        "trainingState": "base-untrained",
    }
    (destination / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    (destination / "THIRD_PARTY_NOTICES.txt").write_text(
        "DungeonStory local narrative development mount\n"
        "\n"
        "Model: ggml-org/Qwen3-1.7B-GGUF (Q4_K_M)\n"
        "Original model: Qwen/Qwen3-1.7B\n"
        "Inference runtime: ggml-org/llama.cpp " + LLAMA_TAG + "\n"
        "This is the untrained base model and is not a V25 release-certified build.\n"
        "See the upstream repositories for their complete license texts and notices.\n",
        encoding="utf-8",
    )
    print(json.dumps(manifest, ensure_ascii=False, indent=2), flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
