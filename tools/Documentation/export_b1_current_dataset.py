#!/usr/bin/env python3
"""Export review-only B1 examples from current-complete matrix rows.

This tool is intentionally narrower than a training pipeline. It accepts only
source-bound current evidence already validated by the B1 completion matrix and
keeps all approval, training, DPO, release, and full-audit claims false. Multiple
matrices may be combined when their current-complete row IDs are disjoint; every
sample retains the descriptor of the matrix that supplied its source identity.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import re
import sys
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


_MATRIX_SPEC = importlib.util.spec_from_file_location(
    "b1_export_matrix_validation", Path(__file__).with_name("build_b1_completion_matrix.py"))
if _MATRIX_SPEC is None or _MATRIX_SPEC.loader is None:
    raise ImportError("B1 completion matrix validator is unavailable")
_matrix_validation = importlib.util.module_from_spec(_MATRIX_SPEC)
_MATRIX_SPEC.loader.exec_module(_matrix_validation)


MATRIX_SCHEMA = "dungeonstory-b1-completion-matrix@1"
SAMPLE_SCHEMA = "dungeonstory-b1-limited-dataset-sample@1"
MANIFEST_SCHEMA = "dungeonstory-b1-limited-dataset-manifest@1"
DATASET_FILE = "samples.jsonl"
MANIFEST_FILE = "manifest.json"
SHA256_RE = re.compile(r"sha256:[0-9a-f]{64}\Z")


class ExportError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ExportError(message)


def canonical_json(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def pretty_json(value: Any) -> bytes:
    return (json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        indent=2,
        allow_nan=False,
    ) + "\n").encode("utf-8")


def sha256_bytes(raw: bytes) -> str:
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def strict_json(raw: bytes) -> Any:
    def unique_pairs(items: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in items:
            require(key not in result, f"duplicate JSON key: {key}")
            result[key] = value
        return result

    try:
        return json.loads(raw.decode("utf-8"), object_pairs_hook=unique_pairs)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ExportError(f"invalid JSON: {error}") from error


def normalize_relative(value: str) -> str:
    require(isinstance(value, str) and value, "path must be non-empty")
    require("\\" not in value and "\x00" not in value, f"unsafe path: {value!r}")
    pure = PurePosixPath(value)
    require(not pure.is_absolute(), f"absolute path is forbidden: {value}")
    require(all(part not in {"", ".", ".."} for part in pure.parts),
            f"unsafe path: {value}")
    require(all(":" not in part for part in pure.parts),
            f"qualified path is forbidden: {value}")
    return pure.as_posix()


def project_root(path: Path) -> Path:
    root = Path(os.path.abspath(os.fspath(path)))
    require(root.is_dir(), f"project root is missing: {root}")
    require((root / "Assets").is_dir() and (root / "Artifacts" / "QA").is_dir(),
            f"not a DungeonStory project root: {root}")
    return root.resolve(strict=True)


def resolve_file(root: Path, supplied: Path) -> tuple[Path, str]:
    require(".." not in supplied.parts, f"input traversal is forbidden: {supplied}")
    candidate = supplied if supplied.is_absolute() else root / supplied
    candidate = Path(os.path.abspath(os.fspath(candidate)))
    try:
        relative = candidate.relative_to(root).as_posix()
    except ValueError as error:
        raise ExportError(f"input is outside project root: {candidate}") from error
    require(candidate.is_file(), f"input is missing: {relative}")
    return candidate.resolve(strict=True), normalize_relative(relative)


def descriptor(path: Path, relative: str) -> dict[str, Any]:
    raw = path.read_bytes()
    return {"path": relative, "bytes": len(raw), "sha256": sha256_bytes(raw)}


def validate_matrix(matrix: Any) -> list[dict[str, Any]]:
    require(isinstance(matrix, dict), "matrix root is invalid")
    require(matrix.get("schema") == MATRIX_SCHEMA, "matrix schema differs")
    require(matrix.get("completionClaimed") is False,
            "matrix must not claim full completion")
    require(matrix.get("trainingEligible") is False,
            "matrix must not grant training eligibility")
    require(matrix.get("humanApprovalClaimed") is False,
            "matrix must not claim human approval")
    source_identity = matrix.get("sourceIdentity")
    require(isinstance(source_identity, dict), "matrix source identity is missing")
    inventory = source_identity.get("inventory")
    require(isinstance(inventory, dict), "matrix inventory descriptor is missing")
    require(SHA256_RE.fullmatch(str(inventory.get("sha256"))) is not None,
            "matrix inventory digest is invalid")

    rows = matrix.get("rows")
    require(isinstance(rows, list) and rows, "matrix rows are missing")
    current = [row for row in rows if isinstance(row, dict)
               and row.get("disposition") == "current-evidence-complete"]
    require(current, "matrix has no current-complete rows")
    expected_count = matrix.get("counts", {}).get("currentEvidenceComplete")
    require(expected_count == len(current), "matrix current row count differs")

    for row in current:
        row_id = row.get("rowId")
        require(isinstance(row_id, str) and row_id, "current row ID is missing")
        require(isinstance(row.get("requirement"), str) and row["requirement"],
                f"current row requirement is missing: {row_id}")
        require(isinstance(row.get("requiredBranches"), list)
                and row["requiredBranches"],
                f"current row branches are missing: {row_id}")
        require(isinstance(row.get("exportExamples"), list)
                and row["exportExamples"],
                f"current row export examples are missing: {row_id}")
        require(isinstance(row.get("currentEvidenceArtifacts"), list)
                and row["currentEvidenceArtifacts"],
                f"current row evidence descriptor is missing: {row_id}")
    return current


def verify_matrix_evidence(
    root: Path, matrix_path: Path, matrix: dict[str, Any], *, require_current_source: bool,
) -> None:
    """Reuse the authoritative matrix reader; never trust copied row flags alone."""
    try:
        _matrix_validation.verify(argparse.Namespace(
            project_root=root,
            matrix=matrix_path,
            inventory=Path(matrix["sourceIdentity"]["inventory"]["path"]),
            require_current_source=require_current_source,
        ))
    except (_matrix_validation.MatrixError, OSError, KeyError, TypeError) as error:
        raise ExportError(f"matrix evidence validation failed: {error}") from error


def build_samples(
    matrix: dict[str, Any],
    rows: Iterable[dict[str, Any]],
    matrix_descriptor: dict[str, Any] | None = None,
) -> list[dict[str, Any]]:
    samples: list[dict[str, Any]] = []
    source_identity = matrix["sourceIdentity"]
    for row in sorted(rows, key=lambda item: item["rowId"]):
        for index, example in enumerate(row["exportExamples"]):
            require(isinstance(example, dict),
                    f"export example is invalid: {row['rowId']}:{index}")
            source = example.get("source")
            require(isinstance(source, str) and "#/" in source,
                    f"export example source is invalid: {row['rowId']}:{index}")
            require("value" in example,
                    f"export example value is missing: {row['rowId']}:{index}")
            samples.append({
                "schema": SAMPLE_SCHEMA,
                "sampleId": f"{row['rowId']}:{index:03d}",
                "rowId": row["rowId"],
                "requirement": row["requirement"],
                "requiredBranches": row["requiredBranches"],
                "entryPoints": row.get("entryPoints", []),
                "evidence": example["value"],
                "provenance": {
                    "exampleSource": source,
                    "currentEvidenceArtifacts": row["currentEvidenceArtifacts"],
                    "gameCommit": source_identity.get("gameCommit"),
                    "dirtySourceSha256": source_identity.get("dirtySourceSha256"),
                    "inventory": source_identity["inventory"],
                    **({"sourceBindingMode": row["bindingMode"],
                        "executionSourceIdentity": row["executionSourceIdentity"]}
                       if row.get("bindingMode") in {
                           "historical-non-impact", "reviewed-domain-non-impact",
                           "reviewed-editor-report-only"} else {}),
                    **({"matrix": matrix_descriptor}
                       if matrix_descriptor is not None else {}),
                },
                "claims": {
                    "naturalPlay": False,
                    "fullAuditClosure": False,
                    "humanApprovalClaimed": False,
                    "trainingEligible": False,
                    "dpoPerformed": False,
                    "releaseReady": False,
                },
            })
    require(samples, "no samples were produced")
    return samples


def build_package(
    root: Path, matrix_path: Path, output_directory: Path, *, require_current_source: bool = False,
) -> dict[str, Any]:
    return build_bundle_package(
        root, [matrix_path], output_directory, require_current_source=require_current_source)


def build_bundle_package(
    root: Path,
    matrix_paths: Iterable[Path],
    output_directory: Path,
    *, require_current_source: bool = False,
) -> dict[str, Any]:
    supplied = list(matrix_paths)
    require(supplied, "at least one matrix is required")

    matrix_descriptors: list[dict[str, Any]] = []
    samples: list[dict[str, Any]] = []
    seen_rows: set[str] = set()
    for supplied_path in supplied:
        matrix_file, matrix_relative = resolve_file(root, supplied_path)
        matrix_raw = matrix_file.read_bytes()
        matrix = strict_json(matrix_raw)
        rows = validate_matrix(matrix)
        verify_matrix_evidence(
            root, matrix_file, matrix, require_current_source=require_current_source)
        row_ids = {row["rowId"] for row in rows}
        duplicates = sorted(seen_rows.intersection(row_ids))
        require(not duplicates,
                "current-complete row appears in multiple matrices: "
                + ", ".join(duplicates))
        seen_rows.update(row_ids)
        matrix_descriptor = {
            "path": matrix_relative,
            "bytes": len(matrix_raw),
            "sha256": sha256_bytes(matrix_raw),
        }
        matrix_descriptors.append(matrix_descriptor)
        samples.extend(build_samples(matrix, rows, matrix_descriptor))

    output = output_directory if output_directory.is_absolute() else root / output_directory
    output = Path(os.path.abspath(os.fspath(output)))
    try:
        output.relative_to(root)
    except ValueError as error:
        raise ExportError(f"output is outside project root: {output}") from error
    require(not output.exists(), f"create-only output already exists: {output}")

    jsonl = b"".join(canonical_json(sample) + b"\n" for sample in samples)
    sample_rows = sorted({sample["rowId"] for sample in samples})
    dataset_descriptor = {
        "path": DATASET_FILE,
        "bytes": len(jsonl),
        "sha256": sha256_bytes(jsonl),
    }
    manifest = {
        "schema": MANIFEST_SCHEMA,
        "status": "REVIEW_ONLY_CURRENT_EVIDENCE_EXPORT",
        "purpose": "Limited grounded examples from source-bound current B1 rows.",
        "matrices": matrix_descriptors,
        "matrixCount": len(matrix_descriptors),
        "dataset": dataset_descriptor,
        "rowCount": len(sample_rows),
        "sampleCount": len(samples),
        "includedRows": sample_rows,
        "excludedDispositionPolicy": "Only current-evidence-complete rows are exported.",
        "matrixVerificationMode": "require-current-source" if require_current_source else "snapshot-integrity",
        "fullAuditClosure": False,
        "humanApprovalClaimed": False,
        "trainingEligible": False,
        "dpoPerformed": False,
        "releaseReady": False,
    }
    manifest_raw = pretty_json(manifest)

    output.mkdir(parents=False)
    (output / DATASET_FILE).write_bytes(jsonl)
    (output / MANIFEST_FILE).write_bytes(manifest_raw)
    return {
        "status": manifest["status"],
        "output": output.relative_to(root).as_posix(),
        "rows": len(sample_rows),
        "samples": len(samples),
        "datasetSha256": dataset_descriptor["sha256"],
        "manifestSha256": sha256_bytes(manifest_raw),
        "matrixVerificationMode": manifest["matrixVerificationMode"],
        "trainingEligible": False,
        "humanApprovalClaimed": False,
    }


def verify_package(
    root: Path, output_directory: Path, *, require_current_source: bool = False,
) -> dict[str, Any]:
    output = output_directory if output_directory.is_absolute() else root / output_directory
    output = Path(os.path.abspath(os.fspath(output)))
    try:
        relative_output = output.relative_to(root).as_posix()
    except ValueError as error:
        raise ExportError(f"output is outside project root: {output}") from error
    require(output.is_dir(), f"output directory is missing: {relative_output}")
    manifest_path = output / MANIFEST_FILE
    dataset_path = output / DATASET_FILE
    require(manifest_path.is_file(), "manifest is missing")
    require(dataset_path.is_file(), "dataset is missing")
    manifest = strict_json(manifest_path.read_bytes())
    require(isinstance(manifest, dict) and manifest.get("schema") == MANIFEST_SCHEMA,
            "manifest schema differs")
    for key in ("fullAuditClosure", "humanApprovalClaimed", "trainingEligible",
                "dpoPerformed", "releaseReady"):
        require(manifest.get(key) is False, f"manifest claim must remain false: {key}")

    matrix_descriptors = manifest.get("matrices")
    if matrix_descriptors is None:
        legacy_matrix = manifest.get("matrix")
        require(isinstance(legacy_matrix, dict), "matrix descriptors are missing")
        matrix_descriptors = [legacy_matrix]
    require(isinstance(matrix_descriptors, list) and matrix_descriptors,
            "matrix descriptors are missing")
    require(manifest.get("matrixCount", len(matrix_descriptors))
            == len(matrix_descriptors), "matrix count differs")
    known_matrix_digests: set[tuple[str, str]] = set()
    expected_samples: list[dict[str, Any]] = []
    expected_rows: set[str] = set()
    for index, matrix in enumerate(matrix_descriptors):
        require(isinstance(matrix, dict), f"matrix descriptor is invalid: {index}")
        path_value = normalize_relative(str(matrix.get("path", "")))
        matrix_path, _ = resolve_file(root, Path(path_value))
        matrix_raw = matrix_path.read_bytes()
        require(matrix.get("bytes") == len(matrix_raw),
                f"matrix byte count differs: {path_value}")
        require(matrix.get("sha256") == sha256_bytes(matrix_raw),
                f"matrix digest differs: {path_value}")
        matrix_document = strict_json(matrix_raw)
        matrix_rows = validate_matrix(matrix_document)
        verify_matrix_evidence(
            root, matrix_path, matrix_document, require_current_source=require_current_source)
        row_ids = {row["rowId"] for row in matrix_rows}
        require(not expected_rows.intersection(row_ids),
                "current-complete row appears in multiple sealed matrices")
        expected_rows.update(row_ids)
        expected_samples.extend(build_samples(
            matrix_document, matrix_rows, matrix if manifest.get("matrices") is not None else None))
        known_matrix_digests.add((path_value, matrix["sha256"]))
    dataset_raw = dataset_path.read_bytes()
    expected = manifest.get("dataset")
    require(isinstance(expected, dict), "dataset descriptor is missing")
    require(expected.get("path") == DATASET_FILE, "dataset path differs")
    require(expected.get("bytes") == len(dataset_raw), "dataset byte count differs")
    require(expected.get("sha256") == sha256_bytes(dataset_raw), "dataset digest differs")

    lines = dataset_raw.splitlines()
    require(lines, "dataset is empty")
    samples = [strict_json(line) for line in lines]
    ids: set[str] = set()
    rows: set[str] = set()
    for sample in samples:
        require(isinstance(sample, dict) and sample.get("schema") == SAMPLE_SCHEMA,
                "sample schema differs")
        sample_id = sample.get("sampleId")
        require(isinstance(sample_id, str) and sample_id not in ids,
                f"sample ID is invalid or duplicate: {sample_id}")
        ids.add(sample_id)
        row_id = sample.get("rowId")
        require(isinstance(row_id, str) and row_id, "sample row ID is missing")
        rows.add(row_id)
        claims = sample.get("claims")
        require(isinstance(claims, dict), f"sample claims are missing: {sample_id}")
        require(all(claims.get(key) is False for key in (
            "naturalPlay", "fullAuditClosure", "humanApprovalClaimed",
            "trainingEligible", "dpoPerformed", "releaseReady")),
            f"sample claim must remain false: {sample_id}")
        provenance = sample.get("provenance")
        require(isinstance(provenance, dict),
                f"sample provenance is missing: {sample_id}")
        sample_matrix = provenance.get("matrix")
        if manifest.get("matrices") is not None:
            require(isinstance(sample_matrix, dict),
                    f"sample matrix provenance is missing: {sample_id}")
            matrix_key = (str(sample_matrix.get("path")),
                          str(sample_matrix.get("sha256")))
            require(matrix_key in known_matrix_digests,
                    f"sample matrix provenance differs: {sample_id}")
    require(manifest.get("sampleCount") == len(samples), "sample count differs")
    require(manifest.get("rowCount") == len(rows), "row count differs")
    require(manifest.get("includedRows") == sorted(rows), "included rows differ")
    grounded_jsonl = b"".join(canonical_json(sample) + b"\n" for sample in expected_samples)
    require(dataset_raw == grounded_jsonl, "dataset differs from sealed matrix examples")
    return {
        "status": "LIMITED_DATASET_VALID",
        "output": relative_output,
        "rows": len(rows),
        "samples": len(samples),
        "datasetSha256": sha256_bytes(dataset_raw),
        "matrixVerificationMode": "require-current-source" if require_current_source else "snapshot-integrity",
        "trainingEligible": False,
        "humanApprovalClaimed": False,
    }


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(description=__doc__)
    sub = root.add_subparsers(dest="command", required=True)
    create = sub.add_parser("create")
    create.add_argument("--project-root", type=Path, default=Path.cwd())
    create.add_argument("--matrix", type=Path, action="append", required=True)
    create.add_argument("--output-directory", type=Path, required=True)
    create.add_argument("--require-current-source", action="store_true",
                        help="Also compare each matrix's inventory with current files and Git HEAD.")
    verify = sub.add_parser("verify")
    verify.add_argument("--project-root", type=Path, default=Path.cwd())
    verify.add_argument("--output-directory", type=Path, required=True)
    verify.add_argument("--require-current-source", action="store_true",
                        help="Also compare each matrix's inventory with current files and Git HEAD.")
    return root


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    try:
        root = project_root(args.project_root)
        if args.command == "create":
            result = build_bundle_package(
                root, args.matrix, args.output_directory,
                require_current_source=args.require_current_source)
        else:
            result = verify_package(
                root, args.output_directory, require_current_source=args.require_current_source)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (ExportError, OSError) as error:
        print(json.dumps({"status": "ERROR", "error": str(error)},
                         ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
