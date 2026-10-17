#!/usr/bin/env python3
"""Build and verify the non-circular 91-row B1 completion matrix.

The matrix classifies evidence.  It never runs Unity, grants training
eligibility, or automatically upgrades historical evidence to current evidence. A row can
become current only through a separate, source-bound current-evidence artifact.
"""

from __future__ import annotations

import argparse
import base64
import binascii
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
from collections import Counter, defaultdict
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Sequence


SCHEMA = "dungeonstory-b1-completion-matrix@1"
CURRENT_EVIDENCE_SCHEMA = "dungeonstory-b1-current-row-evidence@1"
REUSED_EVIDENCE_SCHEMA = "dungeonstory-b1-current-row-evidence@2"
REVIEWED_EVIDENCE_SCHEMA = "dungeonstory-b1-current-row-evidence@3"
REPORT_REVIEWED_EVIDENCE_SCHEMA = "dungeonstory-b1-current-row-evidence@4"
EXPECTED_ROW_COUNT = 91
SHA256_RE = re.compile(r"sha256:[0-9a-f]{64}\Z")
CURRENT_BRANCH_STATES = {
    "covered-by-current-step",
    "covered-by-current-focused",
    "covered-by-current-reuse",
}
BRANCH_STATES = {
    *CURRENT_BRANCH_STATES,
    "historical-only",
    "static-only",
    "unresolved-routing",
    "missing-test-branch",
    "not-implemented",
}
DISPOSITIONS = {
    "current-evidence-complete",
    "implemented-evidence-gap",
    "partial",
    "not-implemented",
    "not-applicable",
    "covered-by",
}
SOURCE_PREFIXES = ("Assets/", "Packages/", "ProjectSettings/", "Tools/")
SOURCE_SUFFIXES = {".cs", ".asmdef", ".asmref"}
ROUTING_BRANCH_KEYS = ("requiredBranches", "requiredBranchMap", "branchCoverage")
HISTORY_KINDS = {"accepted-manifest", "accepted-evidence"}


class MatrixError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise MatrixError(message)


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
    return sha256_bytes(canonical_json({"files": list(descriptors), "schemaVersion": 1}))


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
        raise MatrixError(f"invalid JSON: {error}") from error


def normalize_relative(value: str) -> str:
    require(isinstance(value, str) and value != "", "path must be non-empty")
    require("\\" not in value and "\x00" not in value, f"unsafe path: {value!r}")
    pure = PurePosixPath(value)
    require(not pure.is_absolute(), f"absolute path is forbidden: {value}")
    require(all(part not in {"", ".", ".."} for part in pure.parts), f"unsafe path: {value}")
    require(all(":" not in part for part in pure.parts), f"qualified path is forbidden: {value}")
    return pure.as_posix()


def project_root(path: Path) -> Path:
    root = Path(os.path.abspath(os.fspath(path)))
    require(root.is_dir(), f"project root is missing: {root}")
    require((root / "Assets").is_dir() and (root / "Artifacts" / "QA").is_dir(),
            f"not a DungeonStory project root: {root}")
    return root.resolve(strict=True)


def resolve_input(root: Path, supplied: Path) -> tuple[Path, str]:
    require(".." not in supplied.parts, f"input traversal is forbidden: {supplied}")
    candidate = supplied if supplied.is_absolute() else root / supplied
    candidate = Path(os.path.abspath(os.fspath(candidate)))
    try:
        relative = candidate.relative_to(root).as_posix()
    except ValueError as error:
        raise MatrixError(f"input is outside project root: {candidate}") from error
    require(candidate.is_file(), f"input is missing: {relative}")
    before = candidate.stat(follow_symlinks=False)
    require(stat.S_ISREG(before.st_mode) and not candidate.is_symlink(),
            f"input is not a regular file: {relative}")
    return candidate, normalize_relative(relative)


def resolve_output(root: Path, supplied: Path) -> Path:
    require(".." not in supplied.parts, f"output traversal is forbidden: {supplied}")
    output = supplied if supplied.is_absolute() else root / supplied
    output = Path(os.path.abspath(os.fspath(output)))
    qa_root = root / "Artifacts" / "QA"
    try:
        output.relative_to(qa_root)
    except ValueError as error:
        raise MatrixError(f"output must be below Artifacts/QA: {output}") from error
    require(output.suffix.lower() == ".json", "matrix output must be JSON")
    require(output.parent.is_dir(), f"output parent is missing: {output.parent}")
    require(not os.path.lexists(output), f"create-only output already exists: {output}")
    return output


def load_json_input(root: Path, supplied: Path) -> tuple[Any, dict[str, Any]]:
    path, relative = resolve_input(root, supplied)
    raw = path.read_bytes()
    value = strict_json(raw)
    return value, {
        "path": relative,
        "bytes": len(raw),
        "sha256": sha256_bytes(raw),
    }


def verify_file_descriptor(root: Path, descriptor: dict[str, Any], label: str) -> None:
    require(isinstance(descriptor, dict), f"{label} descriptor is invalid")
    path_value = descriptor.get("path")
    require(isinstance(path_value, str), f"{label} path is missing")
    path, _ = resolve_input(root, Path(path_value))
    require(path.stat().st_size == descriptor.get("bytes"), f"{label} byte length differs: {path_value}")
    require(sha256_file(path) == descriptor.get("sha256"), f"{label} hash differs: {path_value}")


def _inventory_descriptor_map(inventory: dict[str, Any]) -> dict[str, dict[str, Any]]:
    require(isinstance(inventory, dict), "inventory root must be an object")
    require(inventory.get("schemaVersion") == 1, "inventory schemaVersion differs")
    files = inventory.get("files")
    require(isinstance(files, list) and files, "inventory files are missing")
    result: dict[str, dict[str, Any]] = {}
    for descriptor in files:
        require(isinstance(descriptor, dict), "inventory descriptor is invalid")
        path = normalize_relative(descriptor.get("path"))
        require(path not in result, f"duplicate inventory path: {path}")
        require(type(descriptor.get("length")) is int and descriptor["length"] >= 0,
                f"invalid inventory length: {path}")
        require(isinstance(descriptor.get("sha256"), str)
                and SHA256_RE.fullmatch(descriptor["sha256"]),
                f"invalid inventory hash: {path}")
        result[path] = {
            "path": path,
            "length": descriptor["length"],
            "sha256": descriptor["sha256"],
        }
    ordered = [result[path] for path in sorted(result)]
    require([descriptor["path"] for descriptor in files] == [item["path"] for item in ordered],
            "inventory files are not canonically sorted")
    source = [item for item in ordered if PurePosixPath(item["path"]).suffix.lower() in SOURCE_SUFFIXES]
    content = [item for item in ordered if PurePosixPath(item["path"]).suffix.lower() not in SOURCE_SUFFIXES]
    require(inventory.get("dirtySourceSha256") == descriptor_root(source),
            "inventory dirtySourceSha256 differs")
    require(inventory.get("contentSha256") == descriptor_root(content),
            "inventory contentSha256 differs")
    return result


def _walk_paths(value: Any) -> Iterable[str]:
    if isinstance(value, dict):
        for key, child in value.items():
            if key in {"path", "file", "scenarioFile"} and isinstance(child, str):
                normalized = child.replace("\\", "/")
                if normalized.startswith(SOURCE_PREFIXES):
                    yield normalized
            yield from _walk_paths(child)
    elif isinstance(value, list):
        for child in value:
            yield from _walk_paths(child)


def _branch_kind(branch: str) -> str:
    lowered = branch.casefold()
    if any(token in lowered for token in ("저장", "복원", "save", "restore", "재개")):
        return "save-restore"
    if any(token in lowered for token in (
        "실패", "거절", "취소", "중단", "rollback", "fault", "부족", "없", "금지"
    )):
        return "failure"
    return "normal-or-other"


def _route_branches(document: dict[str, Any]) -> Iterable[tuple[str, dict[str, Any], dict[str, Any]]]:
    rows = document.get("rows")
    if isinstance(rows, list):
        for row in rows:
            if not isinstance(row, dict) or not isinstance(row.get("rowId"), str):
                continue
            for key in ROUTING_BRANCH_KEYS:
                branches = row.get(key)
                if isinstance(branches, list):
                    for branch in branches:
                        if isinstance(branch, dict) and isinstance(branch.get("branch"), str):
                            yield row["rowId"], branch, row
                    break
    if isinstance(document.get("rowId"), str):
        for key in ROUTING_BRANCH_KEYS:
            branches = document.get(key)
            if isinstance(branches, list):
                for branch in branches:
                    if isinstance(branch, dict) and isinstance(branch.get("branch"), str):
                        yield document["rowId"], branch, document
                break


def _route_state(route: dict[str, Any], document: dict[str, Any]) -> str:
    raw = str(route.get("coverage") or route.get("classification") or route.get("state") or "")
    lowered = raw.casefold()
    if "missing" in lowered:
        return "missing-test-branch"
    if document.get("runnerEvidence") and document.get("sourceIdentity"):
        return "historical-only"
    if document.get("aggregateExecution") and document.get("sourceIdentity"):
        return "historical-only"
    if lowered in {"covered", "static-covered", "static_only", "static-only"}:
        return "static-only"
    if "partial" in lowered:
        return "unresolved-routing"
    if "not-implemented" in lowered:
        return "not-implemented"
    return "unresolved-routing"


def _accepted_history(
    root: Path,
    refs: list[Any],
    mapped_row_id: str,
) -> list[dict[str, Any]]:
    history: list[dict[str, Any]] = []
    for ref in refs:
        if not isinstance(ref, dict) or ref.get("kind") not in HISTORY_KINDS:
            continue
        path_value = ref.get("path")
        if not isinstance(path_value, str):
            continue
        try:
            path, relative = resolve_input(root, Path(path_value))
        except MatrixError:
            history.append({"reference": ref, "artifact": None, "status": "missing-reference"})
            continue
        raw = path.read_bytes()
        manifest = strict_json(raw)
        require(isinstance(manifest, dict), f"accepted manifest is invalid: {relative}")
        manifest_row_id = manifest.get("rowId") or manifest.get("row") or manifest.get("workItem")
        verdict_field = "verdict" if "verdict" in manifest else "status"
        accepted = manifest.get("acceptedEvidence")
        if not isinstance(accepted, list):
            accepted = manifest.get("evidence")
        if not isinstance(accepted, list):
            accepted = []
        failures = manifest.get("preservedFailures")
        if not isinstance(failures, list):
            failures = manifest.get("preservedFailureEvidence")
        if not isinstance(failures, list):
            failures = []
        missing_failure_hashes = sum(
            1 for failure in failures
            if isinstance(failure, dict)
            and not isinstance(failure.get("sha256"), str)
        )
        source_files = manifest.get("sourceFiles")
        history.append({
            "reference": ref,
            "artifact": {"path": relative, "bytes": len(raw), "sha256": sha256_bytes(raw)},
            "status": "historical-only",
            "manifestSchema": manifest.get("schema"),
            "manifestRowId": manifest_row_id,
            "mappedB1RowIds": [mapped_row_id],
            "rawVerdict": {
                "field": verdict_field,
                "value": manifest.get(verdict_field),
            },
            "acceptedEvidenceCount": len(accepted),
            "preservedFailureCount": len(failures),
            "preservedFailureMissingRecordedHashCount": missing_failure_hashes,
            "nonResultNoteCount": len(manifest.get("nonResultsPreserved", []))
                if isinstance(manifest.get("nonResultsPreserved"), list) else 0,
            "sourceFiles": {
                "status": "recorded" if isinstance(source_files, list) else "not-recorded",
                "count": len(source_files) if isinstance(source_files, list) else None,
            },
            "resolvedNow": True,
            "provenanceLimit": "Current existence and matrix sealing do not retroactively prove execution-time bytes.",
        })
    return history


def _json_pointer(value: Any, pointer: str, label: str) -> Any:
    require(pointer.startswith("/"), f"{label} JSON pointer is invalid")
    cursor = value
    for raw_part in pointer[1:].split("/"):
        part = raw_part.replace("~1", "/").replace("~0", "~")
        if isinstance(cursor, dict):
            require(part in cursor, f"{label} JSON pointer is missing: {pointer}")
            cursor = cursor[part]
        elif isinstance(cursor, list):
            require(part.isdigit(), f"{label} JSON pointer index is invalid: {pointer}")
            index = int(part)
            require(index < len(cursor), f"{label} JSON pointer index is missing: {pointer}")
            cursor = cursor[index]
        else:
            raise MatrixError(f"{label} JSON pointer crosses a scalar: {pointer}")
    return cursor


def _is_whole_file_unity_editor_guarded(source: str) -> bool:
    """Recognize only the simple whole-file guard used by these callable probes.

    Extra directives and multiline lexical constructs are deliberately rejected,
    rather than attempting to prove arbitrary C# preprocessing or call graphs.
    """
    if any(token in source for token in ("\x00", "/*", "*/", '@"', '"""')):
        return False
    lines = [line.strip() for line in source.splitlines() if line.strip()]
    return (len(lines) >= 3
            and lines[0] == "#if UNITY_EDITOR"
            and lines[-1] == "#endif"
            and not any(line.startswith("#") for line in lines[1:-1]))


def _recorded_execution_entry_symbols(
    root: Path, original: dict[str, Any], label: str,
) -> set[str]:
    """Read actual entry types from every hash-bound native log, not DTO filenames."""
    report_path, _ = resolve_input(root, Path(original["execution"]["report"]["path"]))
    report = strict_json(report_path.read_bytes())
    logs = {original["execution"]["unityLog"]["path"]:
            original["execution"]["unityLog"]}

    def collect(value: Any) -> None:
        if isinstance(value, dict):
            if (isinstance(value.get("path"), str)
                    and value["path"].endswith("unity.log")
                    and {"bytes", "sha256"}.issubset(value)):
                previous = logs.get(value["path"])
                require(previous is None or previous == value,
                        f"{label} conflicting original native log binding")
                logs[value["path"]] = value
            for child in value.values():
                collect(child)
        elif isinstance(value, list):
            for child in value:
                collect(child)

    collect(original["execution"])
    collect(report)
    symbols: set[str] = set()
    for descriptor in logs.values():
        verify_file_descriptor(root, descriptor, f"{label} original native entry log")
        path, _ = resolve_input(root, Path(descriptor["path"]))
        text = path.read_text(encoding="utf-8-sig")
        methods = re.findall(
            r"(?m)^-executeMethod\s*\r?\n([A-Za-z_][A-Za-z0-9_.]*\.[A-Za-z_][A-Za-z0-9_]*)\s*$",
            text)
        require(len(methods) == 1,
                f"{label} native log lacks one exact recorded entry: {descriptor['path']}")
        symbols.add(methods[0].rsplit(".", 1)[0].rsplit(".", 1)[-1])
    return symbols


def _static_initializers(source: str) -> list[str]:
    """Capture static field/auto-property initializers in the narrow Editor syntax."""
    result: list[str] = []
    for match in re.finditer(r"\bstatic\b", source):
        start = match.start()
        tail = source[match.end():]
        if re.match(r"\s*(?:partial\s+)?(?:class|struct|interface|record)\b", tail):
            continue
        depth = 0
        quote = ""
        escaped = False
        initialized = False
        index = start
        while index < len(source):
            char = source[index]
            if quote:
                if escaped:
                    escaped = False
                elif char == "\\":
                    escaped = True
                elif char == quote:
                    quote = ""
            elif source[index:index + 2] == "//":
                newline = source.find("\n", index)
                index = len(source) if newline < 0 else newline
                continue
            elif char in ("\"", "'"):
                quote = char
            elif char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0 and not initialized:
                    if not source[index + 1:].lstrip().startswith("="):
                        break  # A property without an auto-property initializer.
            elif char == "(" and depth == 0 and not initialized:
                require(re.search(r"[A-Za-z_]\w*(?:\s*<[^(){};=]+>)?\s*$",
                                  source[match.end():index]),
                        "recorded-entry proof cannot inspect tuple/static declaration syntax")
                break  # A method parameter list, before any initializer.
            elif char == "=" and depth == 0 and not initialized:
                if source[index:index + 2] == "=>":
                    break
                initialized = True
            elif char == ";" and depth == 0:
                if initialized:
                    result.append(source[start:index + 1])
                break
            index += 1
    return sorted(result)


def _validate_manual_snapshot_pair(
    before: str, after: str, owned_symbols: set[str], label: str,
) -> None:
    declarations = []
    for source in (before, after):
        require(_is_whole_file_unity_editor_guarded(source),
                f"{label} recorded-entry proof requires narrow whole-file Editor snapshots")
        require(not re.search(r"\bnamespace\s+[A-Za-z_]", source),
                f"{label} recorded-entry proof supports global callable types only")
        top_level = re.findall(
            r"(?m)^((?:(?:public|internal|sealed|abstract|partial|static)\s+)*)class\s+([A-Za-z_]\w*)",
            source)
        require(top_level and all("static" in modifiers.split()
                                  for modifiers, _ in top_level),
                f"{label} recorded-entry proof requires manual static top-level classes")
        types = re.findall(r"\b(class|struct|enum|interface|record)\s+([A-Za-z_]\w*)", source)
        require(not any(name in owned_symbols for _, name in types),
                f"{label} excluded source declares an owned type")
        require(not any(re.search(r"\bstatic\s+" + re.escape(name) + r"\s*\(", source)
                        for _, name in types),
                f"{label} excluded source contains a static constructor")
        declarations.append(types)
    require(declarations[0] == declarations[1],
            f"{label} recorded-entry proof changed type declarations")
    require(_static_initializers(before) == _static_initializers(after),
            f"{label} recorded-entry proof changed static initialization")


def _validate_no_owned_callers(
    root: Path, owned_sources: Sequence[dict[str, Any]], source: str, label: str,
) -> None:
    """Require the unchanged owned code not to call the excluded manual types.

    Native entry names alone do not prove separation: an entry can reach a
    changed helper in the opposite direction. This optional proof reads the
    hash-bound current owned sources and conservatively rejects even textual
    references. It is not a general C# call-graph analyzer.
    """
    manual_types = set(re.findall(
        r"(?m)^(?:(?:public|internal|sealed|abstract|partial|static)\s+)*class\s+([A-Za-z_]\w*)",
        source))
    require(manual_types, f"{label} manual callable types are missing")
    for descriptor in owned_sources:
        if PurePosixPath(descriptor["path"]).suffix != ".cs":
            continue
        path, _ = resolve_input(root, Path(descriptor["path"]))
        raw = path.read_bytes()
        require(len(raw) == descriptor["length"]
                and sha256_bytes(raw) == descriptor["sha256"],
                f"{label} current owned boundary source differs: {descriptor['path']}")
        try:
            owned_text = raw.decode("utf-8-sig")
        except UnicodeError as error:
            raise MatrixError(f"{label} owned boundary source encoding is invalid") from error
        require(not any(re.search(r"\b" + re.escape(name) + r"\b", owned_text)
                        for name in manual_types),
                f"{label} owned source references an excluded manual type: {descriptor['path']}")


def _validate_report_label_insertion(root: Path, change: dict[str, Any], label: str) -> None:
    """Check exact sealed bytes for one added literal in an Editor coverage array.

    This proves the edit shape, not that the row cannot consume the report.
    The row-specific pinned call review and actual changed-byte regression are
    still required by _validate_reviewed_impact.
    """
    path = change["path"]
    require("/Editor/" in path, f"{label} report edit is outside Editor")
    pin = change.get("sourceSnapshots")
    verify_file_descriptor(root, pin, f"{label} report source snapshots")
    snapshots, _ = load_json_input(root, Path(pin["path"]))
    require(isinstance(snapshots, dict) and snapshots.get("path") == path,
            f"{label} report source snapshot path differs")
    versions = {}
    for field, version in (("beforeSourceBase64", "before"),
                           ("currentSourceBase64", "after")):
        try:
            raw = base64.b64decode(snapshots.get(field, ""), validate=True)
        except (ValueError, TypeError, binascii.Error) as error:
            raise MatrixError(f"{label} invalid report source encoding") from error
        require(len(raw) == change[version]["length"]
                and sha256_bytes(raw) == change[version]["sha256"],
                f"{label} report source bytes differ")
        versions[version] = raw
    insertion = snapshots.get("insertion")
    require(isinstance(insertion, dict), f"{label} report insertion is missing")
    offset, text = insertion.get("offset"), insertion.get("text")
    require(type(offset) is int and 0 <= offset <= len(versions["before"])
            and isinstance(text, str)
            and re.fullmatch(r'[ \t]+"[a-z0-9-]+",\r?\n', text),
            f"{label} report insertion is not a single coverage label")
    raw_label = text.encode("ascii")
    before, after = versions["before"], versions["after"]
    require(after == before[:offset] + raw_label + before[offset:],
            f"{label} report edit contains other byte changes")
    arrays = re.finditer(
        rb'\["coverage"\][ \t]*=[ \t]*new JArray\(\r?\n'
        rb'(?P<labels>[ \t]*"[a-z0-9-]+"(?:,\r?\n[ \t]*"[a-z0-9-]+")*)\)', before)
    require(any(match.start("labels") <= offset < match.end("labels")
                and (offset == 0 or before[offset - 1:offset] == b"\n")
                for match in arrays), f"{label} insertion is not inside a literal coverage array")


def _validate_reviewed_impact(
    root: Path,
    document: dict[str, Any],
    inventory: dict[str, Any],
    inventory_descriptor: dict[str, Any],
    original: dict[str, Any],
    excluded: list[dict[str, Any]],
    label: str,
) -> None:
    """Validate a reviewed non-impact binding, not a new runtime execution.

    The shared reuse checks already require identical content,
    original execution, assertions and examples. This path additionally binds
    every excluded change to a pinned, addressable source review. Changed
    production files need actual regression evidence for those exact bytes.
    Owned bytes remain identical except @4's separately sealed report-label edit.
    Review conclusions remain explicit evidence, not automated call-graph proof.
    """
    proof = document["reuseProof"]
    review_pin = proof.get("impactReview")
    verify_file_descriptor(root, review_pin, f"{label} impact review")
    review, _ = load_json_input(root, Path(review_pin["path"]))
    require(isinstance(review, dict)
            and review.get("schema") == "dungeonstory-b1-row-impact-review@1",
            f"{label} impact review schema differs")
    require(review.get("rowId") == original["rowId"]
            and review.get("originalEvidence") == proof["originalEvidence"]
            and review.get("currentInventory") == inventory_descriptor,
            f"{label} impact review identity differs")
    require(review.get("requiredBranches") == [x["branch"] for x in original["branches"]]
            and review.get("unresolvedChanges") == []
            and review.get("humanApprovalClaimed") is False
            and review.get("trainingEligible") is False,
            f"{label} impact review scope is incomplete")
    require(review.get("excludedChanges") == excluded,
            f"{label} impact review delta differs")

    compilation_pin = document["currentBinding"].get("currentCompilationEvidence")
    verify_file_descriptor(root, compilation_pin, f"{label} current compilation")
    compilation, _ = load_json_input(root, Path(compilation_pin["path"]))
    require(isinstance(compilation, dict) and compilation.get("schema") == CURRENT_EVIDENCE_SCHEMA,
            f"{label} compilation must be an original current execution")
    _validate_current_evidence_artifact(
        root, compilation, compilation_pin, inventory, inventory_descriptor)
    log_path, _ = resolve_input(root, Path(compilation["execution"]["unityLog"]["path"]))
    log = log_path.read_text(encoding="utf-8-sig")
    require(re.search(r"CompileScripts:\s*[0-9.]+ms", log)
            and not re.search(r"error CS[0-9]+", log),
            f"{label} current compilation is unproven")

    for change in excluded:
        path = change["path"]
        require(path.startswith("Assets/") and PurePosixPath(path).suffix == ".cs",
                f"{label} reviewed change is not C#: {path}")
        require(isinstance(change.get("rationale"), str) and change["rationale"].strip(),
                f"{label} change rationale is missing: {path}")
        references = change.get("reviewReferences")
        require(isinstance(references, list) and references,
                f"{label} change has no review references: {path}")
        for reference in references:
            require(isinstance(reference, dict), f"{label} review reference is invalid")
            pin = reference.get("artifact")
            verify_file_descriptor(root, pin, f"{label} review reference")
            source_review, _ = load_json_input(root, Path(pin["path"]))
            pointer = reference.get("pointer")
            require(isinstance(pointer, str), f"{label} review pointer is missing")
            value = _json_pointer(source_review, pointer, label)
            require(value not in (None, "", [], {}),
                    f"{label} review reference is empty: {path}")
            row = value if isinstance(value, dict) else source_review
            require(isinstance(row, dict)
                    and row.get("rowId", row.get("row")) == original["rowId"],
                    f"{label} review reference belongs to another row: {path}")
        classification = change.get("classification")
        require(classification in {"editor-only", "guarded-editor-only", "production-non-impact",
                                   "editor-report-label-only"},
                f"{label} change classification is invalid: {path}")
        if classification == "editor-report-label-only":
            require(document["schema"] == REPORT_REVIEWED_EVIDENCE_SCHEMA,
                    f"{label} report edit needs explicit report-review schema")
            _validate_report_label_insertion(root, change, label)
            # Unlike ordinary Editor exclusions, require an actual @1 run of
            # these changed bytes as well as the separate row call review.
        if classification == "editor-only":
            require("/Editor/" in path,
                    f"{label} editor classification outside Editor: {path}")
            continue
        if classification == "guarded-editor-only":
            snapshot_pin = change.get("sourceSnapshots")
            verify_file_descriptor(root, snapshot_pin, f"{label} guarded source snapshots")
            snapshots, _ = load_json_input(root, Path(snapshot_pin["path"]))
            require(isinstance(snapshots, dict) and snapshots.get("path") == path,
                    f"{label} guarded source snapshot path differs: {path}")
            for field, version in (("beforeSourceBase64", "before"),
                                   ("currentSourceBase64", "after")):
                require(isinstance(snapshots.get(field), str),
                        f"{label} guarded source bytes missing: {path}")
                try:
                    raw = base64.b64decode(snapshots[field], validate=True)
                    text = raw.decode("utf-8-sig")
                except (ValueError, binascii.Error, UnicodeError) as error:
                    raise MatrixError(f"{label} guarded source encoding invalid: {path}") from error
                require(len(raw) == change[version]["length"]
                        and sha256_bytes(raw) == change[version]["sha256"]
                        and _is_whole_file_unity_editor_guarded(text),
                        f"{label} guarded source does not match a whole-file guard: {path}")
            continue
        regression_pin = change.get("regressionEvidence")
        verify_file_descriptor(root, regression_pin, f"{label} changed production regression")
        regression, _ = load_json_input(root, Path(regression_pin["path"]))
        require(isinstance(regression, dict) and regression.get("schema") == CURRENT_EVIDENCE_SCHEMA,
                f"{label} regression must preserve an original execution")
        regression_inventory_pin = regression.get("sourceIdentity", {}).get("inventory")
        verify_file_descriptor(root, regression_inventory_pin, f"{label} regression inventory")
        regression_inventory, _ = load_json_input(root, Path(regression_inventory_pin["path"]))
        _validate_current_evidence_artifact(
            root, regression, regression_pin, regression_inventory, regression_inventory_pin)
        require(change["after"] in regression["sourceFiles"],
                f"{label} regression did not execute changed production bytes: {path}")


def _validate_historical_reuse(
    root: Path,
    document: dict[str, Any],
    inventory: dict[str, Any],
    inventory_descriptor: dict[str, Any],
    label: str,
) -> None:
    """Validate explicit reuse without rewriting original execution identity.

    @2 permits only traced unrelated Editor changes; @3 adds pinned domain
    non-impact review and regression. @4 additionally permits the exact sealed
    owned report-label edit, retaining old and current source pins separately.
    All versions reject changed content/path sets, commits and reuse chains.
    """
    report_reviewed = document["schema"] == REPORT_REVIEWED_EVIDENCE_SCHEMA
    reviewed = document["schema"] in {REVIEWED_EVIDENCE_SCHEMA, REPORT_REVIEWED_EVIDENCE_SCHEMA}
    mode = ("reviewed-editor-report-only" if report_reviewed else
            "reviewed-domain-non-impact" if reviewed else "historical-non-impact")
    require(document.get("bindingMode") == mode,
            f"{label} reuse binding mode differs")
    binding = document.get("currentBinding")
    require(isinstance(binding, dict) and binding.get("inventory") == inventory_descriptor,
            f"{label} current binding inventory differs")
    for key in ("gameCommit", "dirtySourceSha256", "contentSha256"):
        require(binding.get(key) == inventory.get(key),
                f"{label} current binding differs: {key}")
    require(binding.get("verifiedCurrentAtBinding") is True,
            f"{label} lacks current-source binding verification")
    proof = document.get("reuseProof")
    require(isinstance(proof, dict), f"{label} reuse proof is missing")
    original_descriptor = proof.get("originalEvidence")
    verify_file_descriptor(root, original_descriptor, f"{label} original evidence")
    original, _ = load_json_input(root, Path(original_descriptor["path"]))
    require(isinstance(original, dict) and original.get("schema") == CURRENT_EVIDENCE_SCHEMA,
            f"{label} reuse must anchor an original execution, not a reuse chain")
    old_identity = original.get("sourceIdentity")
    require(isinstance(old_identity, dict), f"{label} original identity is missing")
    old_descriptor = old_identity.get("inventory")
    verify_file_descriptor(root, old_descriptor, f"{label} original inventory")
    old_inventory, _ = load_json_input(root, Path(old_descriptor["path"]))
    _validate_current_evidence_artifact(
        root, original, original_descriptor, old_inventory, old_descriptor)
    for key in ("rowId", "sourceIdentity", "sourceFiles", "execution", "exportExamples",
                "scope", "exportLimitations", "wholeWorldRestoreClaimed",
                "coldProcessRestoreClaimed", "naturalGameplay"):
        require(document.get(key) == original.get(key),
                f"{label} must preserve original execution field: {key}")
    new_branches = document.get("branches")
    require(isinstance(new_branches, list), f"{label} reuse branches are missing")
    require([{key: value for key, value in item.items() if key != "state"}
             for item in new_branches if isinstance(item, dict)]
            == [{key: value for key, value in item.items() if key != "state"}
                for item in original["branches"]],
            f"{label} must preserve original branch assertions")
    before = _inventory_descriptor_map(old_inventory)
    after = _inventory_descriptor_map(inventory)
    require(before.keys() == after.keys(), f"{label} reuse inventory path set changed")
    require(old_inventory.get("gameCommit") == inventory.get("gameCommit")
            and old_inventory.get("contentSha256") == inventory.get("contentSha256"),
            f"{label} reuse game commit or content changed")
    owned = {item["path"] for item in original["sourceFiles"]}
    changed_owned = {path for path in owned if before.get(path) != after.get(path)}
    if report_reviewed:
        require(changed_owned and document.get("newExecutionClaimed") is False,
                f"{label} report reuse must name an owned edit, not a new execution")
        require(binding.get("sourceFiles") == [after[item["path"]] for item in original["sourceFiles"]],
                f"{label} current report source binding differs")
    else:
        require(not changed_owned, f"{label} reuse dependency changed")
    delta = [{"path": path, "before": before[path], "after": after[path]}
             for path in sorted(before) if before[path] != after[path]]
    excluded = proof.get("excludedChanges")
    require(isinstance(excluded, list) and delta
            and all(isinstance(item, dict) for item in excluded),
            f"{label} reuse requires a nonempty exact delta")
    require([{key: item.get(key) for key in ("path", "before", "after")}
             for item in excluded if isinstance(item, dict)] == delta,
            f"{label} reuse excluded changes differ from full delta")
    if report_reviewed:
        require({item["path"] for item in excluded
                 if item.get("classification") == "editor-report-label-only"} == changed_owned,
                f"{label} changed owned sources are not exact report edits")
    if reviewed:
        _validate_reviewed_impact(
            root, document, inventory, inventory_descriptor, original, excluded, label)
        return
    owned_symbols = {PurePosixPath(path).stem for path in owned}
    recorded_symbols: set[str] | None = None
    forbidden = ("InitializeOnLoad", "InitializeOnLoadMethod",
                 "RuntimeInitializeOnLoadMethod", "InitializeOnEnterPlayMode",
                 "DidReloadScripts", "AssetPostprocessor", "ScriptedImporter")
    for item in excluded:
        path = item["path"]
        require(path.startswith("Assets/")
                and PurePosixPath(path).suffix == ".cs" and path not in owned,
                f"{label} reuse delta is not unrelated Editor C#: {path}")
        require(isinstance(item.get("rationale"), str) and item["rationale"].strip(),
                f"{label} reuse entry separation rationale is missing: {path}")
        guarded_file = "/Editor/" not in path
        mode = item.get("entrySeparationMode")
        require(mode in (None, "recorded-native-entrypoints"),
                f"{label} unknown entry separation mode")
        recorded_mode = mode == "recorded-native-entrypoints"
        entry_symbols = owned_symbols
        if recorded_mode:
            compilation_descriptor = binding.get("currentCompilationEvidence")
            verify_file_descriptor(root, compilation_descriptor,
                                   f"{label} current compilation evidence")
            compilation, _ = load_json_input(root, Path(compilation_descriptor["path"]))
            require(isinstance(compilation, dict)
                    and compilation.get("schema") == CURRENT_EVIDENCE_SCHEMA,
                    f"{label} current compilation must anchor an actual current execution")
            _validate_current_evidence_artifact(
                root, compilation, compilation_descriptor, inventory, inventory_descriptor)
            compile_path, _ = resolve_input(root, Path(compilation["execution"]["unityLog"]["path"]))
            compile_log = compile_path.read_text(encoding="utf-8-sig")
            require(re.search(r"CompileScripts:\s*[0-9.]+ms", compile_log)
                    and not re.search(r"error CS[0-9]+", compile_log),
                    f"{label} current source compilation is unproven")
            if recorded_symbols is None:
                recorded_symbols = _recorded_execution_entry_symbols(root, original, label)
            entry_symbols = recorded_symbols
        versions = [("currentSourceBase64", "after")]
        if guarded_file or recorded_mode:
            versions.append(("beforeSourceBase64", "before"))
        texts = {}
        for field, version in versions:
            require(isinstance(item.get(field), str),
                    f"{label} reuse {version} source bytes are missing: {path}")
            try:
                raw = base64.b64decode(item[field], validate=True)
                text = raw.decode("utf-8-sig")
            except (ValueError, binascii.Error, UnicodeError) as error:
                raise MatrixError(
                    f"{label} reuse {version} source encoding is invalid: {path}") from error
            require(len(raw) == item[version]["length"]
                    and sha256_bytes(raw) == item[version]["sha256"],
                    f"{label} reuse {version} sealed source differs: {path}")
            texts[version] = text
            if guarded_file:
                require(_is_whole_file_unity_editor_guarded(text),
                        f"{label} reuse {version} source is not whole-file Editor-only: {path}")
            require(not any(re.search(r"\b" + re.escape(symbol) + r"\b", text)
                            for symbol in set(forbidden) | entry_symbols),
                    f"{label} reuse {version} source references entry or automatic startup: {path}")
        if recorded_mode:
            _validate_manual_snapshot_pair(texts["before"], texts["after"], owned_symbols, label)
            _validate_no_owned_callers(
                root, [after[path] for path in sorted(owned)], texts["after"], label)


def _validate_current_evidence_artifact(
    root: Path,
    document: Any,
    descriptor: dict[str, Any],
    inventory: dict[str, Any],
    inventory_descriptor: dict[str, Any],
) -> dict[str, Any]:
    label = f"current evidence {descriptor.get('path', '<unknown>')}"
    require(isinstance(document, dict), f"{label} root is invalid")
    require(document.get("schema") in {
        CURRENT_EVIDENCE_SCHEMA, REUSED_EVIDENCE_SCHEMA, REVIEWED_EVIDENCE_SCHEMA,
        REPORT_REVIEWED_EVIDENCE_SCHEMA},
            f"{label} schema differs")
    reused = document["schema"] in {
        REUSED_EVIDENCE_SCHEMA, REVIEWED_EVIDENCE_SCHEMA, REPORT_REVIEWED_EVIDENCE_SCHEMA}
    row_id = document.get("rowId")
    require(isinstance(row_id, str) and row_id, f"{label} rowId is missing")
    require(document.get("status") in {
        "CURRENT_STEP_EVIDENCE_COMPLETE",
        "CURRENT_FOCUSED_EVIDENCE_COMPLETE",
        "CURRENT_REUSED_EVIDENCE_COMPLETE",
    }, f"{label} status is not current-complete")
    require((document.get("status") == "CURRENT_REUSED_EVIDENCE_COMPLETE") == reused,
            f"{label} status and reuse schema differ")
    require(document.get("rowCurrentEvidenceComplete") is True,
            f"{label} row completion flag differs")
    require(document.get("fullAuditClosure") is False,
            f"{label} must not claim full audit closure")
    require(document.get("trainingEligible") is False,
            f"{label} must not grant training eligibility")
    require(document.get("humanApprovalClaimed") is False,
            f"{label} must not claim human approval")

    source_identity = document.get("sourceIdentity")
    require(isinstance(source_identity, dict), f"{label} source identity is missing")
    if reused:
        _validate_historical_reuse(root, document, inventory, inventory_descriptor, label)
    else:
        require(document.get("bindingMode") is None,
                f"{label} original execution must not declare a reuse mode")
        require(source_identity.get("inventory") == inventory_descriptor,
                f"{label} inventory descriptor differs")
        for key in ("gameCommit", "dirtySourceSha256", "contentSha256"):
            require(source_identity.get(key) == inventory.get(key),
                    f"{label} source identity differs: {key}")
    require(source_identity.get("verifiedCurrentBeforeExecution") is True,
            f"{label} lacks pre-execution current-source verification")
    require(source_identity.get("verifiedCurrentAfterExecution") is True,
            f"{label} lacks post-execution current-source verification")

    inventory_by_path = _inventory_descriptor_map(inventory)
    source_files = (document["currentBinding"]["sourceFiles"]
                    if document["schema"] == REPORT_REVIEWED_EVIDENCE_SCHEMA
                    else document.get("sourceFiles"))
    require(isinstance(source_files, list) and source_files,
            f"{label} source files are missing")
    source_paths: set[str] = set()
    for source in source_files:
        require(isinstance(source, dict), f"{label} source descriptor is invalid")
        path = source.get("path")
        require(isinstance(path, str) and path not in source_paths,
                f"{label} source path is invalid or duplicate: {path}")
        source_paths.add(path)
        require(inventory_by_path.get(path) == source,
                f"{label} source descriptor differs from inventory: {path}")

    execution = document.get("execution")
    require(isinstance(execution, dict), f"{label} execution is missing")
    artifact_keys = ("report", "unityLog", "isolationProbe", "launcherExit")
    for key in artifact_keys:
        verify_file_descriptor(root, execution.get(key), f"{label} {key}")
    require(execution.get("unityExit") == 0, f"{label} Unity exit differs")
    require(execution.get("noninteractiveIsolation") is True,
            f"{label} is not noninteractive-isolated")
    require(execution.get("compileErrorLines") == 0,
            f"{label} contains compile errors")
    require(type(execution.get("compileWarningLines")) is int
            and execution["compileWarningLines"] >= 0,
            f"{label} compile warning count is invalid")

    report_path, _ = resolve_input(root, Path(execution["report"]["path"]))
    report = strict_json(report_path.read_bytes())
    require(isinstance(report, dict) and report.get("passed") is True,
            f"{label} execution report did not pass")
    require(report.get("fullAuditClosure") is False,
            f"{label} report must not claim full audit closure")
    require(report.get("trainingEligible") is False,
            f"{label} report must not grant training eligibility")
    require(report.get("humanApprovalClaimed") is False,
            f"{label} report must not claim human approval")

    branches = document.get("branches")
    require(isinstance(branches, list) and branches, f"{label} branches are missing")
    normalized_branches: list[dict[str, Any]] = []
    seen_branches: set[str] = set()
    for branch in branches:
        require(isinstance(branch, dict), f"{label} branch is invalid")
        branch_name = branch.get("branch")
        require(isinstance(branch_name, str) and branch_name not in seen_branches,
                f"{label} branch is invalid or duplicate: {branch_name}")
        seen_branches.add(branch_name)
        state = branch.get("state")
        require(state == "covered-by-current-reuse" if reused
                else state in CURRENT_BRANCH_STATES - {"covered-by-current-reuse"},
                f"{label} branch is not current-covered: {branch_name}")
        assertions = branch.get("assertions")
        require(isinstance(assertions, list) and assertions
                and all(isinstance(item, str) and item for item in assertions),
                f"{label} branch assertions are missing: {branch_name}")
        normalized_branches.append(branch)

    export_examples = document.get("exportExamples")
    require(isinstance(export_examples, list) and export_examples,
            f"{label} export examples are missing")
    report_relative = execution["report"]["path"]
    for index, example in enumerate(export_examples):
        example_label = f"{label} export example {index}"
        require(isinstance(example, dict), f"{example_label} is invalid")
        source = example.get("source")
        require(isinstance(source, str) and "#/" in source,
                f"{example_label} source is invalid")
        source_path, pointer_tail = source.split("#", 1)
        require(source_path == report_relative,
                f"{example_label} must bind to the accepted report")
        require(_json_pointer(report, pointer_tail, example_label) == example.get("value"),
                f"{example_label} value differs from the report")

    return {
        "rowId": row_id,
        "artifact": descriptor,
        "branches": normalized_branches,
        "sourceFiles": source_files,
        "exportExamples": export_examples,
        **({"bindingMode": document["bindingMode"],
            "executionSourceIdentity": source_identity} if reused else {}),
    }


def build_matrix(
    root: Path,
    draft: dict[str, Any],
    draft_descriptor: dict[str, Any],
    inventory: dict[str, Any],
    inventory_descriptor: dict[str, Any],
    routing_inputs: list[tuple[dict[str, Any], dict[str, Any]]],
    supplements: list[tuple[dict[str, Any], dict[str, Any]]],
    current_evidence_inputs: list[tuple[dict[str, Any], dict[str, Any]]] | None = None,
) -> dict[str, Any]:
    rows = draft.get("rows")
    require(isinstance(rows, list) and len(rows) == EXPECTED_ROW_COUNT,
            f"draft row count must be {EXPECTED_ROW_COUNT}")
    ids = [row.get("rowId") for row in rows if isinstance(row, dict)]
    require(len(ids) == len(set(ids)) == EXPECTED_ROW_COUNT, "draft row IDs differ or duplicate")
    inventory_by_path = _inventory_descriptor_map(inventory)
    current_evidence_inputs = current_evidence_inputs or []
    current_by_row: dict[str, dict[str, Any]] = {}
    for document, descriptor in current_evidence_inputs:
        current = _validate_current_evidence_artifact(
            root,
            document,
            descriptor,
            inventory,
            inventory_descriptor,
        )
        require(current["rowId"] not in current_by_row,
                f"duplicate current evidence row: {current['rowId']}")
        current_by_row[current["rowId"]] = current
    require(set(current_by_row).issubset(set(ids)),
            "current evidence references an unknown row")

    routed: dict[tuple[str, str], list[tuple[dict[str, Any], dict[str, Any], dict[str, Any]]]] = defaultdict(list)
    for document, descriptor in routing_inputs:
        for row_id, branch, row_document in _route_branches(document):
            routed[(row_id, branch["branch"])].append((branch, row_document, descriptor))

    supplemental: dict[tuple[str, str], list[tuple[dict[str, Any], dict[str, Any]]]] = defaultdict(list)
    for document, descriptor in supplements:
        row_id = document.get("rowId")
        branch = document.get("branch")
        if isinstance(row_id, str) and isinstance(branch, str):
            supplemental[(row_id, branch)].append((document, descriptor))

    matrix_rows: list[dict[str, Any]] = []
    all_input_descriptors = [draft_descriptor, inventory_descriptor]
    all_input_descriptors += [descriptor for _, descriptor in routing_inputs]
    all_input_descriptors += [descriptor for _, descriptor in supplements]
    all_input_descriptors += [descriptor for _, descriptor in current_evidence_inputs]
    for draft_row in rows:
        row_id = draft_row["rowId"]
        required = draft_row.get("requiredBranches")
        require(isinstance(required, list) and required, f"requiredBranches missing: {row_id}")
        require(len(required) == len(set(required)), f"duplicate required branch: {row_id}")
        row_route_artifacts: set[str] = set()
        branch_coverage: list[dict[str, Any]] = []
        source_paths: set[str] = set()
        gaps: set[str] = set()
        current = current_by_row.get(row_id)
        current_branches = {
            branch["branch"]: branch
            for branch in current["branches"]
        } if current is not None else {}
        if current is not None:
            require([branch["branch"] for branch in current["branches"]] == required,
                    f"current evidence branch denominator differs: {row_id}")

        for branch_name in required:
            require(isinstance(branch_name, str) and branch_name, f"invalid branch: {row_id}")
            route_entries = routed.get((row_id, branch_name), [])
            supplement_entries = supplemental.get((row_id, branch_name), [])
            variants: list[dict[str, Any]] = []
            states: list[str] = []
            for route, route_document, descriptor in route_entries:
                state = _route_state(route, route_document)
                states.append(state)
                variants.append({
                    "state": state,
                    "routingArtifact": descriptor,
                    "route": route,
                })
                row_route_artifacts.add(descriptor["path"])
                source_paths.update(_walk_paths(route))
                source_paths.update(_walk_paths(route_document))
            for supplement, descriptor in supplement_entries:
                states.append("historical-only")
                variants.append({
                    "state": "historical-only",
                    "routingArtifact": descriptor,
                    "route": supplement,
                })
                source_paths.update(_walk_paths(supplement))
                row_route_artifacts.add(descriptor["path"])

            if current is not None:
                current_branch = current_branches[branch_name]
                state = current_branch["state"]
            elif not states:
                state = "unresolved-routing"
                gaps.add("branch-assertion-unmapped")
            elif "not-implemented" in states:
                state = "not-implemented"
            elif "missing-test-branch" in states and "historical-only" not in states:
                state = "missing-test-branch"
                gaps.add("missing-test-branch")
            elif "unresolved-routing" in states:
                state = "unresolved-routing"
                gaps.add("branch-assertion-partial")
            elif "historical-only" in states:
                state = "historical-only"
                gaps.add("current-execution-unbound")
            else:
                state = "static-only"
                gaps.add("current-execution-missing")

            branch_coverage.append({
                "branch": branch_name,
                "kind": _branch_kind(branch_name),
                "state": state,
                "stepName": (draft_row.get("runnerSteps") or [None])[0],
                "testRef": variants,
                "executionRef": current["artifact"] if current is not None else None,
                "note": (
                    "Current coverage is accepted only from the source-bound current-evidence artifact."
                    if current is not None else
                    "No historical evidence is promoted to current evidence by this matrix."
                ),
            })

        if current is not None:
            source_files = current["sourceFiles"]
            export_examples = current["exportExamples"]
            gaps.clear()
        else:
            source_files = []
            for path in sorted(source_paths):
                descriptor = inventory_by_path.get(path)
                if descriptor is None:
                    gaps.add("source-reference-not-in-inventory")
                    continue
                source_files.append(descriptor)
            if not source_files:
                gaps.add("source-files-unmapped")
            gaps.add("export-example-unmapped")
            export_examples = []

        states = {branch["state"] for branch in branch_coverage}
        if states and states.issubset(CURRENT_BRANCH_STATES):
            disposition = "current-evidence-complete"
        elif "not-implemented" in states:
            disposition = "not-implemented"
        elif "missing-test-branch" in states:
            disposition = "partial"
        else:
            disposition = "implemented-evidence-gap"
        matrix_rows.append({
            "rowId": row_id,
            "requirement": draft_row.get("requirement", ""),
            "disposition": disposition,
            "entryPoints": draft_row.get("entryPoints", []),
            "requiredBranches": required,
            "branchCoverage": branch_coverage,
            "sourceIdentity": {
                "inventoryPath": inventory_descriptor["path"],
                "inventorySha256": inventory_descriptor["sha256"],
                "gameCommit": inventory.get("gameCommit"),
                "dirtySourceSha256": inventory.get("dirtySourceSha256"),
                "contentSha256": inventory.get("contentSha256"),
                "inventorySchemaVersion": inventory.get("schemaVersion"),
            },
            "sourceFiles": source_files,
            **({"bindingMode": current["bindingMode"],
                "executionSourceIdentity": current["executionSourceIdentity"]}
               if current is not None and "bindingMode" in current else {}),
            "evidenceRefs": draft_row.get("evidenceRefs", []),
            "acceptedHistory": _accepted_history(
                root,
                draft_row.get("evidenceRefs", []),
                row_id,
            ),
            "faultSaveCoverage": [
                {"branch": branch["branch"], "kind": branch["kind"], "state": branch["state"]}
                for branch in branch_coverage
            ],
            "runnerSteps": draft_row.get("runnerSteps", []),
            "exportExamples": export_examples,
            "currentEvidenceArtifacts": [current["artifact"]] if current is not None else [],
            "supersedes": sorted(row_route_artifacts),
            "rationale": draft_row.get("rationale", ""),
            "gaps": sorted(gaps),
            "b0Priority": bool(draft_row.get("b0Priority", False)),
        })

    disposition_counts = Counter(row["disposition"] for row in matrix_rows)
    branch_state_counts = Counter(
        branch["state"] for row in matrix_rows for branch in row["branchCoverage"]
    )
    matrix = {
        "schema": SCHEMA,
        "status": "CLASSIFICATION_COMPLETE_CURRENT_CLOSURE_PENDING",
        "authority": {
            "unityRoot": root.as_posix(),
            "remediationContract": "docs/dev/ledger-training-audit-remediation.md",
            "trainingEligible": False,
            "humanApprovalClaimed": False,
        },
        "sourceIdentity": {
            "inventory": inventory_descriptor,
            "gameCommit": inventory.get("gameCommit"),
            "dirtySourceSha256": inventory.get("dirtySourceSha256"),
            "contentSha256": inventory.get("contentSha256"),
            "schemaVersion": inventory.get("schemaVersion"),
        },
        "inputs": sorted(all_input_descriptors, key=lambda value: value["path"]),
        "counts": {
            "rows": len(matrix_rows),
            "branches": sum(len(row["branchCoverage"]) for row in matrix_rows),
            "unclassifiedRows": 0,
            "currentEvidenceComplete": disposition_counts["current-evidence-complete"],
            "dispositions": dict(sorted(disposition_counts.items())),
            "branchStates": dict(sorted(branch_state_counts.items())),
        },
        "completionClaimed": False,
        "trainingEligible": False,
        "humanApprovalClaimed": False,
        "rows": matrix_rows,
    }
    validate_matrix(matrix, inventory, inventory_descriptor)
    return matrix


def validate_matrix(
    matrix: Any,
    inventory: dict[str, Any],
    inventory_descriptor: dict[str, Any],
) -> dict[str, Any]:
    require(isinstance(matrix, dict) and matrix.get("schema") == SCHEMA, "matrix schema differs")
    require(matrix.get("completionClaimed") is False, "completion must remain false")
    require(matrix.get("trainingEligible") is False, "training eligibility must remain false")
    require(matrix.get("humanApprovalClaimed") is False, "human approval must remain false")
    rows = matrix.get("rows")
    require(isinstance(rows, list) and len(rows) == EXPECTED_ROW_COUNT,
            f"matrix must contain {EXPECTED_ROW_COUNT} rows")
    row_ids = [row.get("rowId") for row in rows if isinstance(row, dict)]
    require(len(row_ids) == len(set(row_ids)) == EXPECTED_ROW_COUNT, "matrix row IDs differ")
    inventory_by_path = _inventory_descriptor_map(inventory)
    require(matrix.get("sourceIdentity", {}).get("inventory") == inventory_descriptor,
            "matrix inventory descriptor differs")
    require(matrix.get("sourceIdentity", {}).get("gameCommit") == inventory.get("gameCommit"),
            "matrix game commit differs")
    covered_by: dict[str, str] = {}
    branch_count = 0
    for row in rows:
        row_id = row["rowId"]
        require(row.get("disposition") in DISPOSITIONS, f"invalid disposition: {row_id}")
        required = row.get("requiredBranches")
        coverage = row.get("branchCoverage")
        require(isinstance(required, list) and isinstance(coverage, list),
                f"branch arrays missing: {row_id}")
        require([item.get("branch") for item in coverage] == required,
                f"branch coverage denominator differs: {row_id}")
        branch_count += len(coverage)
        for branch in coverage:
            require(branch.get("state") in BRANCH_STATES,
                    f"invalid branch state: {row_id}:{branch.get('branch')}")
        for descriptor in row.get("sourceFiles", []):
            path = descriptor.get("path")
            require(path in inventory_by_path and inventory_by_path[path] == descriptor,
                    f"source descriptor differs: {row_id}:{path}")
        require(isinstance(row.get("gaps"), list), f"gaps missing: {row_id}")
        if row["disposition"] == "current-evidence-complete":
            require(all(branch["state"].startswith("covered-by-current-") for branch in coverage),
                    f"current row has non-current branch: {row_id}")
            require(row.get("sourceFiles") and row.get("exportExamples"),
                    f"current row lacks source/export binding: {row_id}")
            require(not row["gaps"], f"current row still has gaps: {row_id}")
            artifacts = row.get("currentEvidenceArtifacts")
            require(isinstance(artifacts, list) and len(artifacts) == 1,
                    f"current row evidence artifact differs: {row_id}")
            require(all(branch.get("executionRef") == artifacts[0] for branch in coverage),
                    f"current row branch execution binding differs: {row_id}")
        else:
            require(not any(branch["state"] in CURRENT_BRANCH_STATES for branch in coverage),
                    f"non-current row contains current branch: {row_id}")
        if row["disposition"] == "covered-by":
            targets = row.get("coveredBy")
            require(isinstance(targets, list) and len(targets) == 1,
                    f"covered-by target missing: {row_id}")
            covered_by[row_id] = targets[0]
    for origin in covered_by:
        seen: set[str] = set()
        cursor = origin
        while cursor in covered_by:
            require(cursor not in seen, f"covered-by cycle: {origin}")
            seen.add(cursor)
            cursor = covered_by[cursor]
        require(cursor in set(row_ids), f"covered-by target missing: {origin}->{cursor}")

    counts = matrix.get("counts")
    require(isinstance(counts, dict), "matrix counts missing")
    require(counts.get("rows") == EXPECTED_ROW_COUNT, "matrix row count summary differs")
    require(counts.get("branches") == branch_count, "matrix branch count summary differs")
    require(counts.get("unclassifiedRows") == 0, "matrix has unclassified rows")
    actual_dispositions = Counter(row["disposition"] for row in rows)
    actual_branch_states = Counter(
        branch["state"] for row in rows for branch in row["branchCoverage"]
    )
    require(counts.get("currentEvidenceComplete") == actual_dispositions["current-evidence-complete"],
            "matrix current-evidence count differs")
    require(counts.get("dispositions") == dict(sorted(actual_dispositions.items())),
            "matrix disposition counts differ")
    require(counts.get("branchStates") == dict(sorted(actual_branch_states.items())),
            "matrix branch-state counts differ")
    return {
        "status": "MATRIX_VALID",
        "rows": len(rows),
        "branches": branch_count,
        "currentEvidenceComplete": counts.get("currentEvidenceComplete", 0),
        "completionClaimed": False,
        "trainingEligible": False,
        "humanApprovalClaimed": False,
    }


def create(args: argparse.Namespace) -> dict[str, Any]:
    root = project_root(args.project_root)
    draft, draft_descriptor = load_json_input(root, args.draft)
    inventory, inventory_descriptor = load_json_input(root, args.inventory)
    routing_inputs = [load_json_input(root, path) for path in args.routing]
    supplements = [load_json_input(root, path) for path in args.supplement]
    current_evidence_inputs = [load_json_input(root, path) for path in args.current_evidence]
    matrix = build_matrix(
        root,
        draft,
        draft_descriptor,
        inventory,
        inventory_descriptor,
        routing_inputs,
        supplements,
        current_evidence_inputs,
    )
    output = resolve_output(root, args.output)
    raw = canonical_json(matrix) + b"\n"
    with output.open("xb") as stream:
        stream.write(raw)
    return {
        "status": "MATRIX_CREATED",
        "path": output.relative_to(root).as_posix(),
        "sha256": sha256_bytes(raw),
        **validate_matrix(matrix, inventory, inventory_descriptor),
    }


def verify(args: argparse.Namespace) -> dict[str, Any]:
    root = project_root(args.project_root)
    matrix_path, _ = resolve_input(root, args.matrix)
    inventory, inventory_descriptor = load_json_input(root, args.inventory)
    raw = matrix_path.read_bytes()
    matrix = strict_json(raw)
    require(raw == canonical_json(matrix) + b"\n", "matrix JSON is not canonical")
    result = validate_matrix(matrix, inventory, inventory_descriptor)
    for index, descriptor in enumerate(matrix.get("inputs", [])):
        verify_file_descriptor(root, descriptor, f"matrix input {index}")
    for row in matrix.get("rows", []):
        for index, history in enumerate(row.get("acceptedHistory", [])):
            artifact = history.get("artifact") if isinstance(history, dict) else None
            if artifact is not None:
                verify_file_descriptor(root, artifact, f"accepted history {row['rowId']}:{index}")
        current_artifacts = row.get("currentEvidenceArtifacts", [])
        for index, artifact in enumerate(current_artifacts):
            path, _ = resolve_input(root, Path(artifact["path"]))
            raw_current = path.read_bytes()
            require(len(raw_current) == artifact.get("bytes"),
                    f"current evidence byte length differs: {row['rowId']}:{index}")
            require(sha256_bytes(raw_current) == artifact.get("sha256"),
                    f"current evidence hash differs: {row['rowId']}:{index}")
            current = _validate_current_evidence_artifact(
                root,
                strict_json(raw_current),
                artifact,
                inventory,
                inventory_descriptor,
            )
            require(current["rowId"] == row["rowId"],
                    f"current evidence row differs: {row['rowId']}:{index}")
            require(current["sourceFiles"] == row.get("sourceFiles"),
                    f"current evidence source binding differs: {row['rowId']}:{index}")
            require(current["exportExamples"] == row.get("exportExamples"),
                    f"current evidence export binding differs: {row['rowId']}:{index}")
            for key in ("bindingMode", "executionSourceIdentity"):
                require(current.get(key) == row.get(key),
                        f"current evidence execution provenance differs: {row['rowId']}:{key}")
            require(
                [(item["branch"], item["state"]) for item in current["branches"]]
                == [(item["branch"], item["state"]) for item in row["branchCoverage"]],
                f"current evidence branch binding differs: {row['rowId']}:{index}",
            )
    result["matrixSha256"] = sha256_bytes(raw)
    result["inventoryCurrentCheck"] = "NOT_REQUESTED"
    if args.require_current_source:
        for descriptor in inventory.get("files", []):
            path, _ = resolve_input(root, Path(descriptor["path"]))
            require(path.stat().st_size == descriptor["length"],
                    f"current file length differs: {descriptor['path']}")
            require(sha256_file(path) == descriptor["sha256"],
                    f"current file hash differs: {descriptor['path']}")
        completed = subprocess.run(
            ["git", "-C", os.fspath(root), "rev-parse", "--verify", "HEAD"],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        require(completed.returncode == 0, "current git HEAD is unavailable")
        require(completed.stdout.strip().lower() == inventory.get("gameCommit"),
                "current git HEAD differs from inventory")
        result["inventoryCurrentCheck"] = "CURRENT_MATCH"
    return result


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(description=__doc__)
    commands = root.add_subparsers(dest="command", required=True)
    create_parser = commands.add_parser("create")
    create_parser.add_argument("--project-root", type=Path, default=Path("."))
    create_parser.add_argument("--draft", type=Path, required=True)
    create_parser.add_argument("--inventory", type=Path, required=True)
    create_parser.add_argument("--routing", type=Path, action="append", default=[])
    create_parser.add_argument("--supplement", type=Path, action="append", default=[])
    create_parser.add_argument("--current-evidence", type=Path, action="append", default=[])
    create_parser.add_argument("--output", type=Path, required=True)
    verify_parser = commands.add_parser("verify")
    verify_parser.add_argument("--project-root", type=Path, default=Path("."))
    verify_parser.add_argument("--matrix", type=Path, required=True)
    verify_parser.add_argument("--inventory", type=Path, required=True)
    verify_parser.add_argument("--require-current-source", action="store_true")
    return root


def main(argv: Sequence[str] | None = None) -> int:
    args = parser().parse_args(argv)
    try:
        result = create(args) if args.command == "create" else verify(args)
    except (MatrixError, OSError) as error:
        print(json.dumps({"status": "FAIL", "error": str(error)}, ensure_ascii=False))
        return 1
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
