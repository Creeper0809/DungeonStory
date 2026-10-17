#!/usr/bin/env python3
"""Create one explicit, source-v9-bound equipment selection response fixture.

The command deliberately does not choose modules or evidence.  Every selected
ID is supplied on the command line and must already be offered by the fixed
source package.  Both source and output packages are restricted to the
project's Artifacts/QA directory.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import stat
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


SOURCE_SCHEMA = "dungeonstory-gameplay-module-selection-source@9"
SELECTION_SOURCE_SCHEMA = "dungeonstory-selection-source-v9@1"
MECHANICS_SCHEMA = "dungeonstory-mechanics-source-v9@1"
RESPONSE_SCHEMA = "dungeonstory-module-selection-authored-response@2"
EQUIPMENT_PROFILE = "EquipmentEvolutionModuleSelection"
MANIFEST_FILE = "manifest.json"
ACCEPTED_RESPONSE_FILE = "accepted-response.json"
SHA256_PREFIX_LENGTH = len("sha256:")
REPARSE_POINT = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x0400)


class ResponseBuildError(ValueError):
    """A source package or requested explicit response is unsafe or invalid."""


@dataclass(frozen=True)
class SourceBinding:
    manifest_sha256: str
    selection_source_id: str
    profile_id: str
    request_id: str
    wire_sha256: str
    offered_module_ids: tuple[str, ...]
    offered_evidence_ids: tuple[str, ...]


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ResponseBuildError(message)


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


def strict_json(raw: bytes, label: str) -> Any:
    def unique_pairs(items: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in items:
            require(key not in result, f"{label} has duplicate JSON key: {key}")
            result[key] = value
        return result

    def reject_constant(value: str) -> None:
        raise ResponseBuildError(f"{label} has non-finite JSON value: {value}")

    try:
        return json.loads(
            raw.decode("utf-8"),
            object_pairs_hook=unique_pairs,
            parse_constant=reject_constant,
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ResponseBuildError(f"{label} is not valid UTF-8 JSON: {error}") from error


def require_object(value: Any, label: str) -> dict[str, Any]:
    require(isinstance(value, dict), f"{label} must be a JSON object")
    return value


def require_text(value: Any, label: str) -> str:
    require(isinstance(value, str) and value.strip() and value == value.strip(),
            f"{label} must be non-empty canonical text")
    return value


def require_false(owner: dict[str, Any], key: str, label: str) -> None:
    require(owner.get(key) is False, f"{label} must be false: {key}")


def require_sha256(value: Any, label: str) -> str:
    digest = require_text(value, label)
    require(len(digest) == SHA256_PREFIX_LENGTH + 64
            and digest.startswith("sha256:")
            and all(character in "0123456789abcdef" for character in digest[7:]),
            f"{label} must be a lower-case sha256 digest")
    return digest


def require_string_array(value: Any, label: str, *, non_empty: bool = True) -> tuple[str, ...]:
    require(isinstance(value, list), f"{label} must be an array")
    items = tuple(require_text(item, f"{label}[{index}]")
                  for index, item in enumerate(value))
    require(not non_empty or items, f"{label} must not be empty")
    require(len(set(items)) == len(items), f"{label} must not contain duplicates")
    return items


def is_reparse_point(path: Path) -> bool:
    metadata = path.lstat()
    attributes = getattr(metadata, "st_file_attributes", 0)
    return path.is_symlink() or bool(attributes & REPARSE_POINT)


def require_not_reparse_path(path: Path, label: str) -> None:
    try:
        if is_reparse_point(path):
            raise ResponseBuildError(f"{label} cannot be a reparse point: {path}")
    except FileNotFoundError as error:
        raise ResponseBuildError(f"{label} is missing: {path}") from error


def path_is_inside(path: Path, ancestor: Path) -> bool:
    try:
        path.relative_to(ancestor)
        return True
    except ValueError:
        return False


def reject_reparse_components(ancestor: Path, candidate: Path, label: str) -> None:
    require(path_is_inside(candidate, ancestor), f"{label} is outside allowed root")
    require_not_reparse_path(ancestor, f"{label} root")
    relative = candidate.relative_to(ancestor)
    current = ancestor
    for component in relative.parts:
        current /= component
        if not current.exists() and not current.is_symlink():
            break
        require_not_reparse_path(current, label)


def project_root(value: Path) -> Path:
    root = Path(os.path.abspath(os.fspath(value)))
    require(root.is_dir(), f"project root is missing: {root}")
    require_not_reparse_path(root, "project root")
    root = root.resolve(strict=True)
    require((root / "Assets").is_dir() and (root / "Artifacts" / "QA").is_dir(),
            f"not a DungeonStory project root: {root}")
    reject_reparse_components(root, root / "Artifacts" / "QA", "project QA directory")
    return root


def qa_root(root: Path) -> Path:
    qa = root / "Artifacts" / "QA"
    reject_reparse_components(root, qa, "project QA directory")
    return qa.resolve(strict=True)


def reject_traversal(supplied: Path, label: str) -> None:
    require(".." not in supplied.parts, f"{label} traversal is forbidden: {supplied}")
    require("\x00" not in os.fspath(supplied), f"{label} contains a NUL byte")


def qa_candidate(root: Path, supplied: Path, label: str) -> tuple[Path, Path]:
    reject_traversal(supplied, label)
    qa = qa_root(root)
    candidate = supplied if supplied.is_absolute() else root / supplied
    candidate = Path(os.path.abspath(os.fspath(candidate)))
    require(path_is_inside(candidate, qa), f"{label} must be inside Artifacts/QA: {candidate}")
    reject_reparse_components(root, candidate, label)
    return candidate, qa


def require_qa_directory(root: Path, supplied: Path, label: str) -> Path:
    candidate, qa = qa_candidate(root, supplied, label)
    require(candidate.is_dir(), f"{label} is missing: {candidate}")
    resolved = candidate.resolve(strict=True)
    require(path_is_inside(resolved, qa), f"{label} resolved outside Artifacts/QA")
    return resolved


def require_new_qa_directory(root: Path, supplied: Path) -> Path:
    candidate, qa = qa_candidate(root, supplied, "output directory")
    require(candidate != qa, "output directory cannot be the project QA root")
    require(not candidate.exists() and not candidate.is_symlink(),
            f"create-only output already exists: {candidate}")
    parent = candidate.parent
    require(parent.is_dir(), f"output parent is missing: {parent}")
    reject_reparse_components(root, parent, "output parent")
    resolved_parent = parent.resolve(strict=True)
    require(path_is_inside(resolved_parent, qa),
            "output parent resolved outside Artifacts/QA")
    return candidate


def normalize_descriptor_path(value: Any, label: str) -> str:
    relative = require_text(value, f"{label} path")
    require("\\" not in relative and "\x00" not in relative,
            f"{label} path is unsafe: {relative!r}")
    pure = PurePosixPath(relative)
    require(not pure.is_absolute() and all(part not in {"", ".", ".."}
                                           for part in pure.parts),
            f"{label} path is unsafe: {relative}")
    require(all(":" not in part for part in pure.parts),
            f"{label} path is qualified: {relative}")
    return pure.as_posix()


def read_descriptor(package: Path, descriptor: Any, label: str) -> bytes:
    value = require_object(descriptor, f"{label} descriptor")
    require(set(value) == {"path", "bytes", "sha256"},
            f"{label} descriptor keys differ")
    relative = normalize_descriptor_path(value["path"], label)
    expected_bytes = value["bytes"]
    require(type(expected_bytes) is int and expected_bytes >= 0,
            f"{label} descriptor byte count is invalid")
    expected_sha = require_sha256(value["sha256"], f"{label} descriptor digest")
    file_path = package.joinpath(*PurePosixPath(relative).parts)
    reject_reparse_components(package, file_path, label)
    require(file_path.is_file(), f"{label} file is missing: {relative}")
    resolved = file_path.resolve(strict=True)
    require(path_is_inside(resolved, package), f"{label} resolved outside source package")
    raw = resolved.read_bytes()
    require(len(raw) == expected_bytes, f"{label} byte count differs")
    require(sha256_bytes(raw) == expected_sha, f"{label} bytes/hash differ")
    return raw


def load_source_binding(root: Path, source_directory: Path) -> SourceBinding:
    source = require_qa_directory(root, source_directory, "source-v9 package")
    manifest_path = source / MANIFEST_FILE
    reject_reparse_components(source, manifest_path, "source manifest")
    require(manifest_path.is_file(), "source manifest is missing")
    manifest_raw = manifest_path.read_bytes()
    manifest = require_object(strict_json(manifest_raw, "source manifest"), "source manifest")
    require(manifest.get("schema") == SOURCE_SCHEMA, "source manifest schema differs")
    require(manifest.get("selectionCount") == 1, "source manifest selection count differs")
    for claim in (
        "integrationFixture", "naturalPlayClaimed", "fullAuditClosure",
        "humanApprovalClaimed", "trainingEligible", "dpoPerformed", "releaseReady",
    ):
        require_false(manifest, claim, "source manifest")
    package_id = require_text(manifest.get("sourcePackageId"), "source package ID")

    mechanics = require_object(strict_json(
        read_descriptor(source, manifest.get("mechanics"), "source mechanics"),
        "source mechanics"), "source mechanics")
    require(mechanics.get("schema") == MECHANICS_SCHEMA, "source mechanics schema differs")
    code_identity = mechanics.get("codeIdentity")
    require(isinstance(code_identity, list) and code_identity,
            "source mechanics code identity is missing")
    for claim in ("humanApprovalClaimed", "trainingEligible"):
        require_false(mechanics, claim, "source mechanics")
    evidence_manifest = require_object(mechanics.get("b1EvidenceManifest"),
                                       "source mechanics evidence manifest")
    require_sha256(evidence_manifest.get("sha256"), "source mechanics evidence digest")

    selection_root = require_object(strict_json(
        read_descriptor(source, manifest.get("selectionSource"), "selection source"),
        "selection source"), "selection source")
    require(selection_root.get("schema") == SELECTION_SOURCE_SCHEMA,
            "selection source schema differs")
    require(require_text(selection_root.get("sourcePackageId"),
                         "selection source package ID") == package_id,
            "selection source package ID differs")
    for claim in (
        "responseDataIncluded", "networkSent", "responseApplied",
        "humanApprovalClaimed", "trainingEligible",
    ):
        require_false(selection_root, claim, "selection source")
    selections = selection_root.get("selections")
    require(isinstance(selections, list) and len(selections) == 1,
            "selection source must contain exactly one equipment selection")
    selection = require_object(selections[0], "equipment selection")
    require(require_text(selection.get("profileId"), "equipment profile ID")
            == EQUIPMENT_PROFILE, "selection is not the equipment module-selection profile")
    selection_source_id = require_text(selection.get("selectionSourceId"),
                                       "selection source ID")
    request_id = require_text(selection.get("correlationId"), "equipment request ID")
    require(request_id == require_text(selection.get("triggerId"), "equipment trigger ID"),
            "equipment trigger and correlation identity differ")
    require_false(selection, "responseDataIncluded", "equipment selection")
    require_text(selection.get("targetId"), "equipment target ID")
    require_text(selection.get("ownerId"), "equipment owner ID")
    offered_modules = require_string_array(selection.get("selectionIds"),
                                           "equipment offered module IDs")
    candidate_count = selection.get("candidateCount")
    require(type(candidate_count) is int and candidate_count == len(offered_modules),
            "equipment candidate count differs from offered modules")

    capture = require_object(selection.get("capture"), "equipment capture")
    world_raw = read_descriptor(source, capture.get("officialWorldCheckpoint"),
                                "equipment official world checkpoint")
    require_object(strict_json(world_raw, "equipment official world checkpoint"),
                   "equipment official world checkpoint")
    pending = require_object(strict_json(
        read_descriptor(source, capture.get("pending"), "equipment pending"),
        "equipment pending"), "equipment pending")
    read_descriptor(source, capture.get("witness"), "equipment witness")
    require(require_text(pending.get("presentationId"), "pending presentation ID")
            == request_id, "pending presentation ID differs from request")
    require(require_text(pending.get("targetPersistentId"), "pending target ID")
            == selection["targetId"], "pending target ID differs from selection")
    require(require_text(pending.get("attunementOwnerPersistentId"), "pending owner ID")
            == selection["ownerId"], "pending owner ID differs from selection")
    require_text(pending.get("nodeId"), "pending node ID")
    offered_evidence = require_string_array(pending.get("evidenceIds"),
                                            "equipment offered evidence IDs")

    wire = require_object(selection.get("wire"), "equipment wire")
    require(wire.get("byteExactReplay") is True,
            "equipment source wire is not byte-exact")
    original_wire = read_descriptor(source, wire.get("original"), "equipment source wire")
    restored_wire = read_descriptor(source, wire.get("restoredReplay"),
                                    "equipment restored source wire")
    require(original_wire == restored_wire, "equipment restored source wire differs")

    return SourceBinding(
        manifest_sha256=sha256_bytes(manifest_raw),
        selection_source_id=selection_source_id,
        profile_id=EQUIPMENT_PROFILE,
        request_id=request_id,
        wire_sha256=sha256_bytes(original_wire),
        offered_module_ids=offered_modules,
        offered_evidence_ids=offered_evidence,
    )


def validate_explicit_subset(
    values: Iterable[str],
    offered: tuple[str, ...],
    label: str,
    *,
    non_empty: bool,
) -> list[str]:
    selected = [require_text(value, f"{label}[{index}]")
                for index, value in enumerate(values)]
    require(not non_empty or selected, f"{label} must be explicitly supplied")
    require(len(set(selected)) == len(selected), f"{label} must not contain duplicates")
    illegal = [value for value in selected if value not in offered]
    require(not illegal, f"{label} contains IDs not offered by source: {', '.join(illegal)}")
    return selected


def validate_prose(display_name: str, narrative_flavor: str) -> tuple[str, str]:
    display = require_text(display_name, "display name")
    flavor = require_text(narrative_flavor, "narrative flavor")
    utf16_length = lambda value: len(value.encode("utf-16-le")) // 2
    require(utf16_length(display) <= 32 and utf16_length(flavor) <= 180,
            "response prose exceeds equipment module-selection limits")
    require(not any(character.isdigit() or character in {"%", "％"}
                    for character in display + flavor),
            "response prose must not contain mechanical numbers or percentages")
    return display, flavor


def build_package(
    root: Path,
    source_directory: Path,
    output_directory: Path,
    positive_module_ids: Iterable[str],
    drawback_module_ids: Iterable[str],
    evidence_fact_ids: Iterable[str],
    display_name: str,
    narrative_flavor: str,
) -> dict[str, Any]:
    binding = load_source_binding(root, source_directory)
    positive = validate_explicit_subset(
        positive_module_ids, binding.offered_module_ids, "positive module IDs", non_empty=True)
    drawback = validate_explicit_subset(
        drawback_module_ids, binding.offered_module_ids, "drawback module IDs", non_empty=False)
    require(not set(positive).intersection(drawback),
            "a module cannot be selected as both positive and drawback")
    evidence = validate_explicit_subset(
        evidence_fact_ids, binding.offered_evidence_ids, "evidence fact IDs", non_empty=True)
    display, flavor = validate_prose(display_name, narrative_flavor)
    output = require_new_qa_directory(root, output_directory)

    response = {
        "selectionId": binding.request_id,
        "positiveModuleIds": positive,
        "drawbackModuleIds": drawback,
        "evidenceFactIds": evidence,
        "displayName": display,
        "narrativeFlavor": flavor,
    }
    accepted_canonical = canonical_json(response)
    accepted_raw = accepted_canonical + b"\n"
    accepted_descriptor = {
        "path": ACCEPTED_RESPONSE_FILE,
        "bytes": len(accepted_raw),
        "sha256": sha256_bytes(accepted_raw),
    }
    manifest = {
        "schema": RESPONSE_SCHEMA,
        "sourceManifestSha256": binding.manifest_sha256,
        "selectionSourceId": binding.selection_source_id,
        "profileId": binding.profile_id,
        "requestId": binding.request_id,
        "wireSha256": binding.wire_sha256,
        "authoringOrigin": "integration-fixture-explicit",
        "integrationFixture": True,
        "humanApprovalClaimed": False,
        "trainingEligible": False,
        "dpoPerformed": False,
        "releaseReady": False,
        "acceptedResponse": accepted_descriptor,
        "acceptedResponseSha256": sha256_bytes(accepted_canonical),
    }
    manifest_raw = canonical_json(manifest) + b"\n"

    staging = output.parent / f".{output.name}.staging-{uuid.uuid4().hex}"
    require(not staging.exists(), f"staging output unexpectedly exists: {staging}")
    try:
        staging.mkdir()
        (staging / ACCEPTED_RESPONSE_FILE).write_bytes(accepted_raw)
        (staging / MANIFEST_FILE).write_bytes(manifest_raw)
        require(not output.exists(), f"create-only output already exists: {output}")
        os.replace(staging, output)
    except OSError as error:
        raise ResponseBuildError(
            "could not create integration response package; staging preserved at "
            f"{staging}: {error}") from error
    # A failed publication is QA evidence, not disposable scratch state.
    # A successful publication already moved staging to the final directory.

    relative_output = output.relative_to(root).as_posix()
    return {
        "status": "INTEGRATION_FIXTURE_CREATED",
        "output": relative_output,
        "sourceManifestSha256": binding.manifest_sha256,
        "acceptedResponseSha256": manifest["acceptedResponseSha256"],
        "responseManifestSha256": sha256_bytes(manifest_raw),
        "trainingEligible": False,
        "humanApprovalClaimed": False,
    }


def parser() -> argparse.ArgumentParser:
    command = argparse.ArgumentParser(description=__doc__)
    command.add_argument("--project-root", type=Path, default=Path.cwd())
    command.add_argument("--source-directory", type=Path, required=True)
    command.add_argument("--output-directory", type=Path, required=True)
    command.add_argument("--positive-module-id", action="append", required=True)
    command.add_argument("--drawback-module-id", action="append", default=[])
    command.add_argument("--evidence-fact-id", action="append", required=True)
    command.add_argument("--display-name", required=True)
    command.add_argument("--narrative-flavor", required=True)
    return command


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    try:
        result = build_package(
            project_root(args.project_root),
            args.source_directory,
            args.output_directory,
            args.positive_module_id,
            args.drawback_module_id,
            args.evidence_fact_id,
            args.display_name,
            args.narrative_flavor,
        )
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        return 0
    except (ResponseBuildError, OSError) as error:
        print(json.dumps({"status": "ERROR", "error": str(error)},
                         ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
