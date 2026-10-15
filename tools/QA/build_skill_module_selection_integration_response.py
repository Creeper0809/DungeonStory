#!/usr/bin/env python3
"""Create one explicit source-v9-bound skill candidate response fixture."""

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

SKILL_PROFILE = "CharacterSkillModuleSelection"


def load_binding(root: Path, supplied: Path) -> dict[str, Any]:
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
    mechanics = helper.require_object(helper.strict_json(
        helper.read_descriptor(source, manifest.get("mechanics"),
                               "source mechanics"),
        "source mechanics"), "source mechanics")
    helper.require(mechanics.get("schema") == helper.MECHANICS_SCHEMA,
                   "source mechanics schema differs")
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
               and row.get("profileId") == SKILL_PROFILE]
    helper.require(len(matches) == 1,
                   "source must contain exactly one skill selection")
    selection = matches[0]
    request_id = helper.require_text(selection.get("correlationId"),
                                     "skill request ID")
    selection_ids = helper.require_string_array(
        selection.get("selectionIds"), "skill selection IDs")
    helper.require(len(selection_ids) == 3,
                   "skill source must offer exactly three selections")
    capture = helper.require_object(selection.get("capture"), "skill capture")
    helper.read_descriptor(source, capture.get("officialWorldCheckpoint"),
                           "skill official world checkpoint")
    pending = helper.require_object(helper.strict_json(
        helper.read_descriptor(source, capture.get("pending"), "skill pending"),
        "skill pending"), "skill pending")
    context = helper.require_object(
        pending.get("requestContext"), "frozen skill request context")
    helper.require(context.get("version") == 1,
                   "frozen skill request context version differs")
    character_name = helper.require_text(
        context.get("characterName"), "frozen skill character name")
    offers = pending.get("moduleSelectionOffers")
    helper.require(isinstance(offers, list) and len(offers) == 3,
                   "skill pending must contain three module offers")
    helper.require([offer.get("selectionId") for offer in offers]
                   == list(selection_ids),
                   "skill pending offer order differs from selection source")
    wire = helper.require_object(selection.get("wire"), "skill wire")
    helper.require(wire.get("byteExactReplay") is True,
                   "skill source wire is not byte-exact")
    original_wire = helper.read_descriptor(source, wire.get("original"),
                                           "skill source wire")
    restored_wire = helper.read_descriptor(
        source, wire.get("restoredReplay"), "skill restored wire")
    helper.require(original_wire == restored_wire,
                   "skill restored wire differs")
    return {
        "manifest_sha256": helper.sha256_bytes(manifest_raw),
        "selection_source_id": helper.require_text(
            selection.get("selectionSourceId"), "selection source ID"),
        "request_id": request_id,
        "wire_sha256": helper.sha256_bytes(original_wire),
        "offers": offers,
        "character_name": character_name,
    }


def parse_candidate(raw: str, offer: dict[str, Any], index: int) -> dict[str, Any]:
    candidate = helper.require_object(
        helper.strict_json(raw.encode("utf-8"), f"candidate {index}"),
        f"candidate {index}")
    expected_keys = {
        "selectionId", "positiveModuleIds", "drawbackModuleIds",
        "evidenceFactIds", "displayName", "narrativeFlavor",
    }
    helper.require(set(candidate) == expected_keys,
                   f"candidate {index} keys differ")
    helper.require(candidate["selectionId"] == offer.get("selectionId"),
                   f"candidate {index} selection ID differs")
    positive = helper.require_string_array(candidate["positiveModuleIds"],
                                           f"candidate {index} positive modules")
    drawback = helper.require_string_array(candidate["drawbackModuleIds"],
                                           f"candidate {index} drawbacks",
                                           non_empty=False)
    evidence = helper.require_string_array(candidate["evidenceFactIds"],
                                           f"candidate {index} evidence")
    helper.require(1 <= len(positive) <= 3 and len(drawback) <= 1
                   and 1 <= len(evidence) <= 2,
                   f"candidate {index} cardinality is invalid")
    offered_positive = helper.require_string_array(
        offer.get("positiveModuleIds"), f"offer {index} positive modules")
    offered_drawback = helper.require_string_array(
        offer.get("drawbackModuleIds"), f"offer {index} drawbacks",
        non_empty=False)
    offered_evidence = helper.require_string_array(
        offer.get("evidenceFactIds"), f"offer {index} evidence")
    helper.require(all(value in offered_positive for value in positive),
                   f"candidate {index} selected an unoffered positive module")
    helper.require(all(value in offered_drawback for value in drawback),
                   f"candidate {index} selected an unoffered drawback")
    helper.require(all(value in offered_evidence for value in evidence),
                   f"candidate {index} selected unoffered evidence")
    helper.require(not set(positive).intersection(drawback),
                   f"candidate {index} repeats a positive as a drawback")
    display = helper.require_text(candidate["displayName"],
                                  f"candidate {index} display name")
    flavor = helper.require_text(candidate["narrativeFlavor"],
                                 f"candidate {index} narrative flavor")
    utf16_length = lambda value: len(value.encode("utf-16-le")) // 2
    helper.require(utf16_length(display) <= 14
                   and utf16_length(flavor) <= 48,
                   f"candidate {index} prose exceeds skill limits")
    helper.require("\n" not in display + flavor and "\r" not in display + flavor,
                   f"candidate {index} prose must be single-line")
    helper.require(not any(character.isdigit() or character in {"%", "％"}
                           for character in display + flavor),
                   f"candidate {index} prose contains mechanical numbers")
    return candidate


def build_package(root: Path, source_directory: Path, output_directory: Path,
                  candidate_json: list[str]) -> dict[str, Any]:
    binding = load_binding(root, source_directory)
    helper.require(len(candidate_json) == 3,
                   "exactly three explicit candidates are required")
    candidates = [parse_candidate(raw, binding["offers"][index], index)
                  for index, raw in enumerate(candidate_json)]
    for index, candidate in enumerate(candidates):
        # Mirror the deterministic production prose gate before starting Unity.
        # Do not insert a name or rewrite an explicitly authored response.
        helper.require(
            binding["character_name"] in candidate["displayName"]
            or binding["character_name"] in candidate["narrativeFlavor"],
            f"candidate {index} must mention the frozen character name "
            f"'{binding['character_name']}'")
    combinations = [tuple(candidate["positiveModuleIds"]
                          + candidate["drawbackModuleIds"])
                    for candidate in candidates]
    helper.require(len(set(combinations)) == 3,
                   "skill candidates must use distinct module combinations")
    output = helper.require_new_qa_directory(root, output_directory)
    accepted_raw = helper.canonical_json({"candidates": candidates}) + b"\n"
    accepted_descriptor = {
        "path": helper.ACCEPTED_RESPONSE_FILE,
        "bytes": len(accepted_raw),
        "sha256": helper.sha256_bytes(accepted_raw),
    }
    manifest = {
        "schema": helper.RESPONSE_SCHEMA,
        "sourceManifestSha256": binding["manifest_sha256"],
        "selectionSourceId": binding["selection_source_id"],
        "profileId": SKILL_PROFILE,
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
            f"could not create skill response package: {error}") from error
    return {
        "status": "INTEGRATION_FIXTURE_CREATED",
        "output": output.relative_to(root).as_posix(),
        "sourceManifestSha256": binding["manifest_sha256"],
        "acceptedResponseSha256": manifest["acceptedResponseSha256"],
        "responseManifestSha256": helper.sha256_bytes(manifest_raw),
        "trainingEligible": False,
        "humanApprovalClaimed": False,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--source-directory", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--candidate-json", action="append", required=True)
    args = parser.parse_args(argv)
    try:
        result = build_package(
            helper.project_root(args.project_root),
            args.source_directory,
            args.output_directory,
            args.candidate_json)
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        return 0
    except (helper.ResponseBuildError, OSError) as error:
        print(json.dumps({"status": "ERROR", "error": str(error)},
                         ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
