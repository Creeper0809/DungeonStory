#!/usr/bin/env python3
"""Deterministic source/output manifests for generated DungeonStory knowledge."""

from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path
from typing import Any, Iterable


MANIFEST_NAME = "generation-manifest.json"
SOURCE_FILES_NAME = "source-files.csv"
OUTPUT_FILES_NAME = "output-files.csv"
CONTROL_FILES = {MANIFEST_NAME, OUTPUT_FILES_NAME}


def project_relative(path: Path, project_root: Path) -> str:
    return path.resolve().relative_to(project_root.resolve()).as_posix()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def aggregate_digest(rows: Iterable[dict[str, Any]], fields: tuple[str, ...]) -> str:
    digest = hashlib.sha256()
    for row in sorted(rows, key=lambda value: tuple(str(value[field]) for field in fields)):
        payload = "\0".join(str(row[field]) for field in fields) + "\n"
        digest.update(payload.encode("utf-8"))
    return digest.hexdigest()


def write_csv(path: Path, rows: Iterable[dict[str, Any]], fieldnames: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, extrasaction="ignore", lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def expand_source_specs(project_root: Path, source_specs: list[dict[str, Any]]) -> list[dict[str, Any]]:
    records: dict[str, dict[str, Any]] = {}
    for spec in source_specs:
        source_set = str(spec["name"])
        source_root = (project_root / str(spec["root"])).resolve()
        source_root.relative_to(project_root.resolve())
        for pattern in spec["patterns"]:
            for path in source_root.glob(str(pattern)):
                if not path.is_file():
                    continue
                relative_path = project_relative(path, project_root)
                record = {
                    "source_set": source_set,
                    "path": relative_path,
                    "size": path.stat().st_size,
                    "sha256": sha256_file(path),
                }
                existing = records.get(relative_path)
                if existing and existing["source_set"] != source_set:
                    existing["source_set"] = "+".join(sorted({existing["source_set"], source_set}))
                else:
                    records[relative_path] = record
    return sorted(records.values(), key=lambda value: value["path"])


def inventory_outputs(output_root: Path) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for path in sorted(output_root.rglob("*")):
        if not path.is_file():
            continue
        relative_path = path.relative_to(output_root).as_posix()
        if relative_path in CONTROL_FILES:
            continue
        records.append(
            {
                "path": relative_path,
                "size": path.stat().st_size,
                "sha256": sha256_file(path),
            }
        )
    return records


def write_generation_manifest(
    *,
    project_root: Path,
    output_root: Path,
    generator_path: Path,
    source_specs: list[dict[str, Any]],
    schema_version: int,
    artifact_kind: str,
    statistics: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Write deterministic manifests after all ordinary generated files exist."""

    output_root = output_root.resolve()
    output_root.relative_to(project_root.resolve())
    for control_name in CONTROL_FILES:
        control_path = output_root / control_name
        if control_path.exists():
            control_path.unlink()

    source_records = expand_source_specs(project_root, source_specs)
    source_file_path = output_root / SOURCE_FILES_NAME
    write_csv(source_file_path, source_records, ["source_set", "path", "size", "sha256"])

    output_records = inventory_outputs(output_root)
    output_file_path = output_root / OUTPUT_FILES_NAME
    write_csv(output_file_path, output_records, ["path", "size", "sha256"])

    manifest = {
        "schema_version": schema_version,
        "artifact_kind": artifact_kind,
        "generator": project_relative(generator_path, project_root),
        "output_root": project_relative(output_root, project_root),
        "source_specs": source_specs,
        "source_file_count": len(source_records),
        "source_digest": aggregate_digest(
            source_records,
            ("source_set", "path", "size", "sha256"),
        ),
        "source_files_manifest_sha256": sha256_file(source_file_path),
        "output_file_count": len(output_records),
        "output_digest": aggregate_digest(output_records, ("path", "size", "sha256")),
        "output_files_manifest_sha256": sha256_file(output_file_path),
        "statistics": statistics or {},
    }
    (output_root / MANIFEST_NAME).write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    return manifest


def verify_generation_manifest(project_root: Path, output_root: Path) -> dict[str, Any]:
    output_root = output_root.resolve()
    manifest_path = output_root / MANIFEST_NAME
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    failures: list[str] = []

    expected_output_root = project_relative(output_root, project_root)
    if manifest.get("output_root") != expected_output_root:
        failures.append(
            f"output-root:{manifest.get('output_root')}!={expected_output_root}"
        )

    source_file_path = output_root / SOURCE_FILES_NAME
    output_file_path = output_root / OUTPUT_FILES_NAME
    if sha256_file(source_file_path) != manifest.get("source_files_manifest_sha256"):
        failures.append("source-files-manifest-mutated")
    if sha256_file(output_file_path) != manifest.get("output_files_manifest_sha256"):
        failures.append("output-files-manifest-mutated")

    expected_sources = {row["path"]: row for row in read_csv(source_file_path)}
    current_source_rows = expand_source_specs(project_root, list(manifest["source_specs"]))
    current_sources = {row["path"]: row for row in current_source_rows}
    for path in sorted(expected_sources.keys() - current_sources.keys()):
        failures.append(f"source-missing:{path}")
    for path in sorted(current_sources.keys() - expected_sources.keys()):
        failures.append(f"source-added:{path}")
    for path in sorted(expected_sources.keys() & current_sources.keys()):
        expected = expected_sources[path]
        current = current_sources[path]
        if (
            expected["source_set"] != current["source_set"]
            or int(expected["size"]) != int(current["size"])
            or expected["sha256"] != current["sha256"]
        ):
            failures.append(f"source-changed:{path}")

    current_source_digest = aggregate_digest(
        current_source_rows,
        ("source_set", "path", "size", "sha256"),
    )
    if current_source_digest != manifest.get("source_digest"):
        failures.append("source-digest-mismatch")

    expected_outputs = {row["path"]: row for row in read_csv(output_file_path)}
    current_output_rows = inventory_outputs(output_root)
    current_outputs = {row["path"]: row for row in current_output_rows}
    for path in sorted(expected_outputs.keys() - current_outputs.keys()):
        failures.append(f"output-missing:{path}")
    for path in sorted(current_outputs.keys() - expected_outputs.keys()):
        failures.append(f"output-added:{path}")
    for path in sorted(expected_outputs.keys() & current_outputs.keys()):
        expected = expected_outputs[path]
        current = current_outputs[path]
        if int(expected["size"]) != int(current["size"]) or expected["sha256"] != current["sha256"]:
            failures.append(f"output-changed:{path}")

    current_output_digest = aggregate_digest(current_output_rows, ("path", "size", "sha256"))
    if current_output_digest != manifest.get("output_digest"):
        failures.append("output-digest-mismatch")

    return {
        "artifact_kind": manifest.get("artifact_kind", ""),
        "output_root": expected_output_root,
        "source_files": len(current_source_rows),
        "output_files": len(current_output_rows),
        "source_digest": current_source_digest,
        "output_digest": current_output_digest,
        "failure_count": len(failures),
        "failures": failures,
    }
