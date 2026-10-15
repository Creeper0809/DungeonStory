#!/usr/bin/env python3
"""Create or verify a deterministic DungeonStory ledger source inventory.

The inventory is deliberately narrow in meaning.  It binds repository bytes
and ``git HEAD``; it does not run Unity, prove gameplay rows, or grant training
eligibility.  The snapshot's own SHA-256 is printed by the CLI and is never
embedded in the snapshot.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
import unicodedata
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Sequence


SCHEMA_VERSION = 1
SCAN_ROOTS = ("Assets", "Packages", "ProjectSettings")
SOURCE_SUFFIXES = {".cs", ".asmdef", ".asmref"}
SELF_RELATIVE_PATH = "Tools/Documentation/build_ledger_source_inventory.py"
SNAPSHOT_KEYS = {
    "schemaVersion",
    "files",
    "gameCommit",
    "dirtySourceSha256",
    "contentSha256",
}
DESCRIPTOR_KEYS = {"path", "length", "sha256"}
SHA256_PATTERN = re.compile(r"sha256:[0-9a-f]{64}\Z")
COMMIT_PATTERN = re.compile(r"[0-9a-f]{40,64}\Z")
REPARSE_ATTRIBUTE = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)


class InventoryError(ValueError):
    """Raised when an inventory input or snapshot is unsafe or inconsistent."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise InventoryError(message)


def canonical_json(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def sha256_bytes(raw: bytes) -> str:
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(chunk)
    return "sha256:" + digest.hexdigest()


def descriptor_root(descriptors: Sequence[dict[str, Any]]) -> str:
    """Hash the complete ordered descriptors for one inventory partition."""

    return sha256_bytes(
        canonical_json({"files": list(descriptors), "schemaVersion": SCHEMA_VERSION})
    )


def _invalid_json_constant(value: str) -> None:
    raise InventoryError(f"non-finite JSON number: {value}")


def strict_json(raw: bytes) -> Any:
    def unique_pairs(items: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in items:
            require(key not in result, f"duplicate JSON key: {key}")
            result[key] = value
        return result

    try:
        return json.loads(
            raw.decode("utf-8"),
            object_pairs_hook=unique_pairs,
            parse_constant=_invalid_json_constant,
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise InventoryError(f"invalid snapshot JSON: {error}") from error


def _is_reparse(path_stat: os.stat_result) -> bool:
    return bool(getattr(path_stat, "st_file_attributes", 0) & REPARSE_ATTRIBUTE)


def _absolute_lexical(path: Path) -> Path:
    return Path(os.path.abspath(os.fspath(path)))


def _relative_lexical(candidate: Path, root: Path, label: str) -> Path:
    try:
        return candidate.relative_to(root)
    except ValueError as error:
        raise InventoryError(f"{label} is outside the repository: {candidate}") from error


def _reject_reparse_components(root: Path, candidate: Path, label: str) -> None:
    relative = _relative_lexical(candidate, root, label)
    current = root
    paths = [root]
    for part in relative.parts:
        current /= part
        paths.append(current)
    for path in paths:
        if not os.path.lexists(path):
            continue
        path_stat = path.lstat()
        require(
            not path.is_symlink() and not _is_reparse(path_stat),
            f"{label} contains a symlink or reparse point: {path}",
        )


def resolve_project_root(project_root: Path) -> Path:
    lexical = _absolute_lexical(project_root)
    require(lexical.is_dir(), f"project root is not a directory: {lexical}")
    require(
        not lexical.is_symlink() and not _is_reparse(lexical.lstat()),
        f"project root is a symlink or reparse point: {lexical}",
    )
    return lexical.resolve(strict=True)


def normalize_relative_path(value: str) -> str:
    require(isinstance(value, str) and value != "", "file path must be non-empty")
    require("\\" not in value and "\x00" not in value, f"unsafe file path: {value!r}")
    normalized = unicodedata.normalize("NFC", value)
    require(value == normalized, f"file path is not NFC-normalized: {value!r}")
    pure = PurePosixPath(normalized)
    require(not pure.is_absolute(), f"absolute file path is forbidden: {value}")
    require(
        all(part not in {"", ".", ".."} for part in pure.parts),
        f"unsafe file path segment: {value}",
    )
    require(pure.parts and all(":" not in part for part in pure.parts),
            f"colon-qualified file path is forbidden: {value}")
    canonical = pure.as_posix()
    require(canonical == normalized, f"file path is not normalized: {value}")
    return canonical


def _descriptor(path: Path, project_root: Path) -> dict[str, Any]:
    before = path.stat(follow_symlinks=False)
    require(stat.S_ISREG(before.st_mode), f"inventory entry is not a regular file: {path}")
    resolved = path.resolve(strict=True)
    try:
        resolved.relative_to(project_root)
    except ValueError as error:
        raise InventoryError(f"inventory file escaped the repository: {path}") from error
    raw_hash = sha256_file(path)
    after = path.stat(follow_symlinks=False)
    require(
        (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
        == (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns),
        f"inventory file changed while hashing: {path}",
    )
    relative = normalize_relative_path(path.relative_to(project_root).as_posix())
    return {"path": relative, "length": before.st_size, "sha256": raw_hash}


def _walk_regular_files(root: Path, project_root: Path) -> Iterable[Path]:
    pending = [root]
    while pending:
        directory = pending.pop()
        with os.scandir(directory) as entries:
            current_entries = sorted(entries, key=lambda entry: entry.name)
        child_directories: list[Path] = []
        for entry in current_entries:
            path = Path(entry.path)
            entry_stat = entry.stat(follow_symlinks=False)
            require(
                not entry.is_symlink() and not _is_reparse(entry_stat),
                f"scan root contains a symlink or reparse point: {path}",
            )
            if stat.S_ISDIR(entry_stat.st_mode):
                child_directories.append(path)
            elif stat.S_ISREG(entry_stat.st_mode):
                resolved = path.resolve(strict=True)
                try:
                    resolved.relative_to(project_root)
                except ValueError as error:
                    raise InventoryError(f"scan file escaped the repository: {path}") from error
                yield path
            else:
                raise InventoryError(f"scan root contains a non-regular entry: {path}")
        pending.extend(reversed(child_directories))


def _contract_path(project_root: Path, supplied: Path) -> Path:
    require(".." not in supplied.parts, f"contract path traversal is forbidden: {supplied}")
    candidate = supplied if supplied.is_absolute() else project_root / supplied
    candidate = _absolute_lexical(candidate)
    _relative_lexical(candidate, project_root, "contract file")
    _reject_reparse_components(project_root, candidate, "contract file")
    require(candidate.suffix.lower() == ".py", f"contract file must be Python: {candidate}")
    require(candidate.is_file(), f"contract file is missing: {candidate}")
    require(
        stat.S_ISREG(candidate.stat(follow_symlinks=False).st_mode),
        f"contract file is not regular: {candidate}",
    )
    relative = normalize_relative_path(candidate.relative_to(project_root).as_posix())
    require(relative != SELF_RELATIVE_PATH, "the inventory utility cannot inventory itself")
    return candidate


def enumerate_files(
    project_root: Path,
    contract_files: Sequence[Path] = (),
) -> list[dict[str, Any]]:
    root = resolve_project_root(project_root)
    paths: list[Path] = []
    for root_name in SCAN_ROOTS:
        scan_root = root / root_name
        require(scan_root.is_dir(), f"required scan root is missing: {root_name}")
        _reject_reparse_components(root, scan_root, "scan root")
        paths.extend(_walk_regular_files(scan_root, root))
    paths.extend(_contract_path(root, supplied) for supplied in contract_files)

    descriptors: list[dict[str, Any]] = []
    normalized_keys: set[str] = set()
    for path in paths:
        descriptor = _descriptor(path, root)
        duplicate_key = unicodedata.normalize("NFC", descriptor["path"]).casefold()
        require(
            duplicate_key not in normalized_keys,
            f"duplicate normalized inventory path: {descriptor['path']}",
        )
        normalized_keys.add(duplicate_key)
        descriptors.append(descriptor)
    return sorted(descriptors, key=lambda descriptor: descriptor["path"])


def git_head(project_root: Path) -> str:
    completed = subprocess.run(
        ["git", "-C", os.fspath(project_root), "rev-parse", "--verify", "HEAD"],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    require(completed.returncode == 0, f"git HEAD unavailable: {completed.stderr.strip()}")
    commit = completed.stdout.strip().lower()
    require(COMMIT_PATTERN.fullmatch(commit) is not None, f"invalid git HEAD: {commit}")
    return commit


def _is_source_descriptor(descriptor: dict[str, Any]) -> bool:
    return PurePosixPath(descriptor["path"]).suffix.lower() in SOURCE_SUFFIXES


def build_snapshot(
    project_root: Path,
    contract_files: Sequence[Path] = (),
) -> dict[str, Any]:
    root = resolve_project_root(project_root)
    descriptors = enumerate_files(root, contract_files)
    source_descriptors = [value for value in descriptors if _is_source_descriptor(value)]
    content_descriptors = [value for value in descriptors if not _is_source_descriptor(value)]
    return {
        "schemaVersion": SCHEMA_VERSION,
        "files": descriptors,
        "gameCommit": git_head(root),
        "dirtySourceSha256": descriptor_root(source_descriptors),
        "contentSha256": descriptor_root(content_descriptors),
    }


def validate_snapshot(snapshot: Any) -> dict[str, int]:
    require(isinstance(snapshot, dict), "snapshot root must be an object")
    require(set(snapshot) == SNAPSHOT_KEYS, "snapshot top-level fields differ")
    require(
        type(snapshot["schemaVersion"]) is int
        and snapshot["schemaVersion"] == SCHEMA_VERSION,
        "snapshot schemaVersion differs",
    )
    commit = snapshot["gameCommit"]
    require(
        isinstance(commit, str) and COMMIT_PATTERN.fullmatch(commit) is not None,
        "snapshot gameCommit is invalid",
    )
    for field in ("dirtySourceSha256", "contentSha256"):
        require(
            isinstance(snapshot[field], str)
            and SHA256_PATTERN.fullmatch(snapshot[field]) is not None,
            f"snapshot {field} is invalid",
        )

    descriptors = snapshot["files"]
    require(isinstance(descriptors, list), "snapshot files must be an array")
    paths: list[str] = []
    duplicate_keys: set[str] = set()
    for index, descriptor in enumerate(descriptors):
        require(isinstance(descriptor, dict), f"file descriptor {index} is not an object")
        require(set(descriptor) == DESCRIPTOR_KEYS, f"file descriptor {index} fields differ")
        relative = normalize_relative_path(descriptor["path"])
        require(relative != SELF_RELATIVE_PATH, "snapshot includes the inventory utility itself")
        pure = PurePosixPath(relative)
        in_scan_root = pure.parts[0] in SCAN_ROOTS
        require(
            in_scan_root or pure.suffix.lower() == ".py",
            f"file descriptor is outside scan roots and is not a Python contract: {relative}",
        )
        length = descriptor["length"]
        require(
            isinstance(length, int) and not isinstance(length, bool) and length >= 0,
            f"file descriptor length is invalid: {relative}",
        )
        digest = descriptor["sha256"]
        require(
            isinstance(digest, str) and SHA256_PATTERN.fullmatch(digest) is not None,
            f"file descriptor SHA-256 is invalid: {relative}",
        )
        duplicate_key = unicodedata.normalize("NFC", relative).casefold()
        require(duplicate_key not in duplicate_keys, f"duplicate normalized path: {relative}")
        duplicate_keys.add(duplicate_key)
        paths.append(relative)
    require(paths == sorted(paths), "snapshot files are not sorted by normalized path")

    source_descriptors = [value for value in descriptors if _is_source_descriptor(value)]
    content_descriptors = [value for value in descriptors if not _is_source_descriptor(value)]
    require(
        snapshot["dirtySourceSha256"] == descriptor_root(source_descriptors),
        "dirtySourceSha256 descriptor root mismatch",
    )
    require(
        snapshot["contentSha256"] == descriptor_root(content_descriptors),
        "contentSha256 descriptor root mismatch",
    )
    return {
        "fileCount": len(descriptors),
        "sourceFileCount": len(source_descriptors),
        "contentFileCount": len(content_descriptors),
    }


def load_snapshot(path: Path) -> tuple[dict[str, Any], bytes, dict[str, int]]:
    raw = path.read_bytes()
    snapshot = strict_json(raw)
    counts = validate_snapshot(snapshot)
    require(
        raw == canonical_json(snapshot) + b"\n",
        "snapshot JSON bytes are not canonical",
    )
    return snapshot, raw, counts


def _validated_output(project_root: Path, supplied: Path) -> Path:
    root = resolve_project_root(project_root)
    require(".." not in supplied.parts, f"output path traversal is forbidden: {supplied}")
    output = supplied if supplied.is_absolute() else root / supplied
    output = _absolute_lexical(output)
    qa_root = root / "Artifacts" / "QA"
    require(qa_root.is_dir(), f"output root is missing: {qa_root}")
    _reject_reparse_components(root, qa_root, "output root")
    _relative_lexical(output, qa_root, "output")
    require(output.suffix.lower() == ".json", f"snapshot output must be JSON: {output}")
    require(output.parent.is_dir(), f"snapshot output parent is missing: {output.parent}")
    _reject_reparse_components(root, output.parent, "output parent")
    require(not os.path.lexists(output), f"snapshot output already exists: {output}")
    return output


def _validated_snapshot_input(project_root: Path, supplied: Path) -> Path:
    root = resolve_project_root(project_root)
    require(".." not in supplied.parts, f"snapshot path traversal is forbidden: {supplied}")
    snapshot = supplied if supplied.is_absolute() else root / supplied
    snapshot = _absolute_lexical(snapshot)
    qa_root = root / "Artifacts" / "QA"
    require(qa_root.is_dir(), f"snapshot root is missing: {qa_root}")
    _relative_lexical(snapshot, qa_root, "snapshot")
    _reject_reparse_components(root, snapshot, "snapshot")
    require(snapshot.suffix.lower() == ".json", f"snapshot must be JSON: {snapshot}")
    require(snapshot.is_file(), f"snapshot is missing: {snapshot}")
    require(
        stat.S_ISREG(snapshot.stat(follow_symlinks=False).st_mode),
        f"snapshot is not a regular file: {snapshot}",
    )
    return snapshot


def create_snapshot(
    project_root: Path,
    output: Path,
    contract_files: Sequence[Path] = (),
) -> dict[str, Any]:
    root = resolve_project_root(project_root)
    destination = _validated_output(root, output)
    snapshot = build_snapshot(root, contract_files)
    validate_snapshot(snapshot)
    raw = canonical_json(snapshot) + b"\n"
    with destination.open("xb") as stream:
        stream.write(raw)
    return {
        "inventoryStatus": "SNAPSHOT_CREATED",
        "trust": "snapshot-bytes-and-git-head-only",
        "currentSourceChecked": True,
        "snapshot": destination.relative_to(root).as_posix(),
        "manifestSha256": sha256_bytes(raw),
        "fileCount": len(snapshot["files"]),
        "dirtySourceSha256": snapshot["dirtySourceSha256"],
        "contentSha256": snapshot["contentSha256"],
        "claims": {
            "gameplayRows": "NOT_EVALUATED",
            "runtime": "NOT_RUN",
            "training": "NOT_EVALUATED",
        },
    }


def verify_snapshot_integrity(snapshot_path: Path) -> dict[str, Any]:
    snapshot, raw, counts = load_snapshot(snapshot_path)
    return {
        "inventoryStatus": "SNAPSHOT_INTEGRITY_VALID",
        "trust": "loaded-snapshot-integrity-only",
        "currentSourceChecked": False,
        "manifestSha256": sha256_bytes(raw),
        "gameCommit": snapshot["gameCommit"],
        **counts,
        "claims": {
            "gameplayRows": "NOT_EVALUATED",
            "runtime": "NOT_RUN",
            "training": "NOT_EVALUATED",
        },
    }


def verify_current(
    project_root: Path,
    snapshot_path: Path,
    contract_files: Sequence[Path] = (),
) -> dict[str, Any]:
    root = resolve_project_root(project_root)
    snapshot_file = _validated_snapshot_input(root, snapshot_path)
    snapshot, raw, counts = load_snapshot(snapshot_file)
    current = build_snapshot(root, contract_files)
    failures: list[str] = []
    if snapshot["gameCommit"] != current["gameCommit"]:
        failures.append(
            f"game-commit-changed:{snapshot['gameCommit']}->{current['gameCommit']}"
        )

    expected_by_path = {value["path"]: value for value in snapshot["files"]}
    current_by_path = {value["path"]: value for value in current["files"]}
    for path in sorted(expected_by_path.keys() - current_by_path.keys()):
        failures.append(f"file-missing:{path}")
    for path in sorted(current_by_path.keys() - expected_by_path.keys()):
        failures.append(f"file-added:{path}")
    for path in sorted(expected_by_path.keys() & current_by_path.keys()):
        expected = expected_by_path[path]
        actual = current_by_path[path]
        if expected["length"] != actual["length"] or expected["sha256"] != actual["sha256"]:
            failures.append(f"file-changed:{path}")
    if snapshot["dirtySourceSha256"] != current["dirtySourceSha256"]:
        failures.append("dirty-source-root-changed")
    if snapshot["contentSha256"] != current["contentSha256"]:
        failures.append("content-root-changed")

    return {
        "inventoryStatus": "CURRENT_MATCH" if not failures else "CURRENT_MISMATCH",
        "trust": "current-disk-and-git-head" if not failures else "current-mismatch",
        "currentSourceChecked": True,
        "manifestSha256": sha256_bytes(raw),
        "snapshotGameCommit": snapshot["gameCommit"],
        "currentGameCommit": current["gameCommit"],
        **counts,
        "failureCount": len(failures),
        "failures": failures,
        "claims": {
            "gameplayRows": "NOT_EVALUATED",
            "runtime": "NOT_RUN",
            "training": "NOT_EVALUATED",
        },
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    create = subparsers.add_parser("create", help="create one new frozen snapshot")
    create.add_argument("--project-root", type=Path, default=Path("."))
    create.add_argument("--output", type=Path, required=True)
    create.add_argument("--contract-file", type=Path, action="append", default=[])

    verify = subparsers.add_parser("verify", help="verify a frozen snapshot")
    verify.add_argument("--project-root", type=Path, default=Path("."))
    verify.add_argument("--snapshot", type=Path, required=True)
    verify.add_argument("--contract-file", type=Path, action="append", default=[])
    verify.add_argument(
        "--snapshot-only",
        action="store_true",
        help="check only loaded descriptor schema and digest roots; do not inspect disk or git",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        if args.command == "create":
            result = create_snapshot(args.project_root, args.output, args.contract_file)
        elif args.snapshot_only:
            result = verify_snapshot_integrity(args.snapshot)
        else:
            result = verify_current(
                args.project_root,
                args.snapshot,
                args.contract_file,
            )
    except (InventoryError, OSError, subprocess.SubprocessError) as error:
        print(
            json.dumps(
                {
                    "inventoryStatus": "ERROR",
                    "trust": "none",
                    "error": str(error),
                    "claims": {
                        "gameplayRows": "NOT_EVALUATED",
                        "runtime": "NOT_RUN",
                        "training": "NOT_EVALUATED",
                    },
                },
                ensure_ascii=False,
                sort_keys=True,
            )
        )
        return 2
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0 if result.get("failureCount", 0) == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
