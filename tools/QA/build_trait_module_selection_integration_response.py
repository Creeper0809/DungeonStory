#!/usr/bin/env python3
"""Create one explicit source-v9-bound acquired-trait response fixture."""

from __future__ import annotations

import argparse
import importlib.util
import json
import os
import sys
import uuid
from pathlib import Path
from typing import Any


HELPER_PATH = Path(__file__).with_name(
    "build_module_selection_integration_response.py")
SPEC = importlib.util.spec_from_file_location(
    "module_selection_response_builder", HELPER_PATH)
helper = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = helper
SPEC.loader.exec_module(helper)

TRAIT_PROFILE = "AcquiredTraitModuleSelection"


def load_binding(root: Path, supplied: Path, positive_module_id: str) -> dict[str, Any]:
    source = helper.require_qa_directory(root, supplied, "source-v9 package")
    manifest_raw = (source / helper.MANIFEST_FILE).read_bytes()
    manifest = helper.require_object(
        helper.strict_json(manifest_raw, "source manifest"), "source manifest")
    helper.require(manifest.get("schema") == helper.SOURCE_SCHEMA,
                   "source manifest schema differs")
    count = manifest.get("selectionCount")
    helper.require(type(count) is int and count >= 1,
                   "source manifest selection count is invalid")
    for claim in ("humanApprovalClaimed", "trainingEligible",
                  "dpoPerformed", "releaseReady"):
        helper.require_false(manifest, claim, "source manifest")
    selection_root = helper.require_object(helper.strict_json(
        helper.read_descriptor(source, manifest.get("selectionSource"),
                               "selection source"),
        "selection source"), "selection source")
    helper.require(selection_root.get("schema")
                   == helper.SELECTION_SOURCE_SCHEMA,
                   "selection source schema differs")
    for claim in ("responseDataIncluded", "networkSent",
                  "responseApplied", "humanApprovalClaimed",
                  "trainingEligible"):
        helper.require_false(selection_root, claim, "selection source")
    rows = selection_root.get("selections")
    helper.require(isinstance(rows, list) and len(rows) == count,
                   "selection rows differ from the manifest count")
    matches = [row for row in rows if isinstance(row, dict)
               and row.get("profileId") == TRAIT_PROFILE]
    helper.require(len(matches) == 1,
                   "source must contain exactly one acquired-trait selection")
    selection = matches[0]
    request_id = helper.require_text(selection.get("correlationId"),
                                     "trait correlation ID")
    selection_ids = helper.require_string_array(
        selection.get("selectionIds"), "trait selection IDs")
    helper.require(len(selection_ids) == 1,
                   "trait source must contain one selection ID")
    capture = helper.require_object(selection.get("capture"), "trait capture")
    helper.read_descriptor(source, capture.get("officialWorldCheckpoint"),
                           "trait official world checkpoint")
    pending = helper.require_object(helper.strict_json(
        helper.read_descriptor(source, capture.get("pending"), "trait pending"),
        "trait pending"), "trait pending")
    helper.require(pending.get("moduleSelectionId") == selection_ids[0],
                   "trait pending selection ID differs")
    benefits = helper.require_string_array(
        pending.get("offeredBenefitModuleIds"), "trait benefit modules")
    selected_module = helper.require_text(positive_module_id, "trait positive module ID")
    helper.require(selected_module in benefits,
                   "selected trait positive module is not offered")
    evidence = helper.require_string_array(
        pending.get("evidenceFactIds"), "trait evidence")
    bindings = pending.get("evidenceBindings")
    helper.require(isinstance(bindings, list),
                   "trait evidence bindings are missing")
    work_bindings = [binding for binding in bindings
                     if isinstance(binding, dict)
                     and binding.get("sourceKind") == "exact"
                     and binding.get("publicFactId") in evidence]
    helper.require(len(work_bindings) >= 1,
                   "trait source has no exact gameplay-outcome evidence")
    evidence_id = helper.require_text(work_bindings[0].get("publicFactId"),
                                      "trait selected evidence")
    wire = helper.require_object(selection.get("wire"), "trait wire")
    helper.require(wire.get("byteExactReplay") is True,
                   "trait source wire is not byte-exact")
    original_wire = helper.read_descriptor(source, wire.get("original"),
                                           "trait source wire")
    restored_wire = helper.read_descriptor(
        source, wire.get("restoredReplay"), "trait restored wire")
    helper.require(original_wire == restored_wire,
                   "trait restored wire differs")
    return {
        "manifest_sha256": helper.sha256_bytes(manifest_raw),
        "selection_source_id": helper.require_text(
            selection.get("selectionSourceId"), "selection source ID"),
        "request_id": request_id,
        "wire_sha256": helper.sha256_bytes(original_wire),
        "selection_id": selection_ids[0],
        "evidence_id": evidence_id,
        "evidence_ids": sorted(evidence),
        "selected_module_id": selected_module,
    }


def build_package(root: Path, source_directory: Path, output_directory: Path,
                  display_name: str, narrative_flavor: str,
                  positive_module_id: str) -> dict[str, Any]:
    binding = load_binding(root, source_directory, positive_module_id)
    display = helper.require_text(display_name, "display name")
    flavor = helper.require_text(narrative_flavor, "narrative flavor")
    helper.require(len(display) <= 32 and len(flavor) <= 180,
                   "trait presentation text exceeds its limits")
    helper.require("\n" not in display + flavor and "\r" not in display + flavor,
                   "trait presentation text must be single-line")
    helper.require(not any(character.isdigit() or character in {"%", "％"}
                           for character in display + flavor),
                   "trait presentation text contains mechanical numbers")
    accepted = {
        "selectionId": binding["selection_id"],
        "positiveModuleIds": [binding["selected_module_id"]],
        "drawbackModuleIds": [],
        # Keep the same evidence authority that produced the pending offer.
        # FreezeSelected recalculates the authored module budget from the cited
        # subset, so dropping facts here can make an offered module unaffordable.
        "evidenceFactIds": binding["evidence_ids"],
        "displayName": display,
        "narrativeFlavor": flavor,
    }
    output = helper.require_new_qa_directory(root, output_directory)
    accepted_raw = helper.canonical_json(accepted) + b"\n"
    accepted_descriptor = {
        "path": helper.ACCEPTED_RESPONSE_FILE,
        "bytes": len(accepted_raw),
        "sha256": helper.sha256_bytes(accepted_raw),
    }
    manifest = {
        "schema": helper.RESPONSE_SCHEMA,
        "sourceManifestSha256": binding["manifest_sha256"],
        "selectionSourceId": binding["selection_source_id"],
        "profileId": TRAIT_PROFILE,
        "requestId": binding["request_id"],
        "wireSha256": binding["wire_sha256"],
        "authoringOrigin": "integration-fixture-explicit",
        "integrationFixture": True,
        "humanApprovalClaimed": False,
        "trainingEligible": False,
        "dpoPerformed": False,
        "releaseReady": False,
        "acceptedResponse": accepted_descriptor,
        "acceptedResponseSha256": helper.sha256_bytes(accepted_raw[:-1]),
    }
    manifest_raw = helper.canonical_json(manifest) + b"\n"
    staging = output.parent / f".{output.name}.staging-{uuid.uuid4().hex}"
    try:
        staging.mkdir()
        (staging / helper.ACCEPTED_RESPONSE_FILE).write_bytes(accepted_raw)
        (staging / helper.MANIFEST_FILE).write_bytes(manifest_raw)
        helper.require(not output.exists(),
                       f"create-only output already exists: {output}")
        os.replace(staging, output)
    except OSError as error:
        raise helper.ResponseBuildError(
            f"could not create trait response package: {error}") from error
    # Failed create-only staging is QA evidence. Preserve it for diagnosis;
    # successful publication already moved it to the final directory.
    return {
        "status": "INTEGRATION_FIXTURE_CREATED",
        "output": output.relative_to(root).as_posix(),
        "sourceManifestSha256": binding["manifest_sha256"],
        "acceptedResponseSha256": manifest["acceptedResponseSha256"],
        "responseManifestSha256": helper.sha256_bytes(manifest_raw),
        "selectedModuleId": binding["selected_module_id"],
        "selectedEvidenceFactIds": binding["evidence_ids"],
        "trainingEligible": False,
        "humanApprovalClaimed": False,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--source-directory", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--positive-module-id", required=True)
    parser.add_argument("--display-name", default="레온의 일의 리듬")
    parser.add_argument(
        "--narrative-flavor",
        default="레온은 반복한 일을 자신만의 호흡으로 이어 가는 법을 익혔다.")
    args = parser.parse_args(argv)
    try:
        result = build_package(
            helper.project_root(args.project_root),
            args.source_directory,
            args.output_directory,
            args.display_name,
            args.narrative_flavor,
            args.positive_module_id)
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        return 0
    except (helper.ResponseBuildError, OSError) as error:
        print(json.dumps({"status": "ERROR", "error": str(error)},
                         ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
