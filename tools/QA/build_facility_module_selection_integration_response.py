#!/usr/bin/env python3
"""Create one explicit, source-v9-bound facility selection response fixture."""

from __future__ import annotations

import argparse
import errno
import json
import os
import sys
import time
import uuid
from pathlib import Path
from typing import Any, Iterable

import build_module_selection_integration_response as helper


FACILITY_PROFILE = "FacilityEvolutionModuleSelection"


def finalize_create_only(staging: Path, output: Path) -> None:
    for attempt in range(10):
        helper.require(not output.exists(),
                       f"create-only output already exists: {output}")
        try:
            os.replace(staging, output)
            return
        except OSError as error:
            transient_access_denied = (
                getattr(error, "winerror", None) == 5
                or error.errno in (errno.EACCES, errno.EPERM)
            )
            if not transient_access_denied or attempt == 9:
                raise
            time.sleep(0.05 * (attempt + 1))


def load_binding(root: Path, source_directory: Path) -> dict[str, Any]:
    source = helper.require_qa_directory(root, source_directory, "source-v9 package")
    manifest_path = source / helper.MANIFEST_FILE
    manifest_raw = manifest_path.read_bytes()
    manifest = helper.require_object(
        helper.strict_json(manifest_raw, "source manifest"), "source manifest")
    helper.require(manifest.get("schema") == helper.SOURCE_SCHEMA,
                   "source manifest schema differs")
    helper.require(manifest.get("selectionCount") == 1,
                   "facility source must contain one selection")
    for claim in (
        "integrationFixture", "naturalPlayClaimed", "fullAuditClosure",
        "humanApprovalClaimed", "trainingEligible", "dpoPerformed", "releaseReady",
    ):
        helper.require_false(manifest, claim, "source manifest")
    mechanics = helper.require_object(helper.strict_json(
        helper.read_descriptor(source, manifest.get("mechanics"), "source mechanics"),
        "source mechanics"), "source mechanics")
    helper.require(mechanics.get("schema") == helper.MECHANICS_SCHEMA,
                   "source mechanics schema differs")
    helper.require(isinstance(mechanics.get("codeIdentity"), list)
                   and mechanics["codeIdentity"],
                   "source mechanics code identity is missing")
    selection_root = helper.require_object(helper.strict_json(
        helper.read_descriptor(source, manifest.get("selectionSource"),
                               "selection source"),
        "selection source"), "selection source")
    helper.require(selection_root.get("schema") == helper.SELECTION_SOURCE_SCHEMA,
                   "selection source schema differs")
    for claim in (
        "responseDataIncluded", "networkSent", "responseApplied",
        "humanApprovalClaimed", "trainingEligible",
    ):
        helper.require_false(selection_root, claim, "selection source")
    rows = selection_root.get("selections")
    helper.require(isinstance(rows, list) and len(rows) == 1,
                   "selection source must contain exactly one facility selection")
    selection = helper.require_object(rows[0], "facility selection")
    helper.require(selection.get("profileId") == FACILITY_PROFILE,
                   "selection is not the facility module-selection profile")
    helper.require_false(selection, "responseDataIncluded", "facility selection")
    request_id = helper.require_text(selection.get("correlationId"),
                                     "facility request ID")
    helper.require(request_id == helper.require_text(
        selection.get("triggerId"), "facility trigger ID"),
        "facility trigger and correlation identity differ")
    facility_id = helper.require_text(selection.get("targetId"),
                                      "facility target ID")
    helper.require(facility_id == helper.require_text(
        selection.get("ownerId"), "facility owner ID"),
        "facility owner and target differ")
    offered_modules = helper.require_string_array(
        selection.get("selectionIds"), "facility offered module IDs")
    helper.require(selection.get("candidateCount") == len(offered_modules),
                   "facility candidate count differs from offered modules")

    capture = helper.require_object(selection.get("capture"), "facility capture")
    helper.require_object(helper.strict_json(helper.read_descriptor(
        source, capture.get("officialWorldCheckpoint"),
        "facility official world checkpoint"), "facility world"), "facility world")
    pending = helper.require_object(helper.strict_json(helper.read_descriptor(
        source, capture.get("pending"), "facility pending"),
        "facility pending"), "facility pending")
    helper.read_descriptor(source, capture.get("witness"), "facility witness")
    helper.require(pending.get("presentationId") == request_id,
                   "facility pending presentation differs from request")
    helper.require(pending.get("sourceFacilityPersistentId") == facility_id,
                   "facility pending owner differs")
    node = helper.require_object(pending.get("node"), "facility pending node")
    helper.require(node.get("presentationId") == request_id
                   and node.get("moduleSelectionId") == request_id,
                   "facility pending node identity differs")
    offers = node.get("moduleSelectionOffers")
    helper.require(isinstance(offers, list) and offers,
                   "facility pending offers are missing")
    by_id = {
        helper.require_text(offer.get("moduleId"), "facility offer module ID"): offer
        for offer in offers if isinstance(offer, dict)
    }
    helper.require(set(by_id) == set(offered_modules),
                   "facility pending offers differ from the selection source")
    offered_evidence = helper.require_string_array(
        node.get("evidenceIds"), "facility offered evidence IDs")

    wire = helper.require_object(selection.get("wire"), "facility wire")
    helper.require(wire.get("byteExactReplay") is True,
                   "facility source wire is not byte-exact")
    original_wire = helper.read_descriptor(
        source, wire.get("original"), "facility source wire")
    restored_wire = helper.read_descriptor(
        source, wire.get("restoredReplay"), "facility restored source wire")
    helper.require(original_wire == restored_wire,
                   "facility restored source wire differs")
    return {
        "manifest_sha256": helper.sha256_bytes(manifest_raw),
        "selection_source_id": helper.require_text(
            selection.get("selectionSourceId"), "selection source ID"),
        "request_id": request_id,
        "wire_sha256": helper.sha256_bytes(original_wire),
        "offered_modules": offered_modules,
        "offers": by_id,
        "offered_evidence": offered_evidence,
    }


def explicit_subset(values: Iterable[str], offered: tuple[str, ...], label: str,
                    *, non_empty: bool) -> list[str]:
    return helper.validate_explicit_subset(
        values, offered, label, non_empty=non_empty)


def build_package(root: Path, source_directory: Path, output_directory: Path,
                  positive_module_ids: Iterable[str],
                  drawback_module_ids: Iterable[str],
                  evidence_fact_ids: Iterable[str],
                  display_name: str, narrative_flavor: str) -> dict[str, Any]:
    binding = load_binding(root, source_directory)
    positive = explicit_subset(positive_module_ids, binding["offered_modules"],
                               "positive module IDs", non_empty=True)
    drawback = explicit_subset(drawback_module_ids, binding["offered_modules"],
                               "drawback module IDs", non_empty=False)
    helper.require(len(positive) == 1 and not drawback,
                   "facility response must select one positive module and no drawback")
    helper.require(binding["offers"][positive[0]].get("polarity") == 0,
                   "selected facility module is not a positive offer")
    evidence = explicit_subset(evidence_fact_ids, binding["offered_evidence"],
                               "evidence fact IDs", non_empty=True)
    display, flavor = helper.validate_prose(display_name, narrative_flavor)
    output = helper.require_new_qa_directory(root, output_directory)
    response = {
        "selectionId": binding["request_id"],
        "positiveModuleIds": positive,
        "drawbackModuleIds": drawback,
        "evidenceFactIds": evidence,
        "displayName": display,
        "narrativeFlavor": flavor,
    }
    accepted_canonical = helper.canonical_json(response)
    accepted_raw = accepted_canonical + b"\n"
    accepted_descriptor = {
        "path": helper.ACCEPTED_RESPONSE_FILE,
        "bytes": len(accepted_raw),
        "sha256": helper.sha256_bytes(accepted_raw),
    }
    manifest = {
        "schema": helper.RESPONSE_SCHEMA,
        "sourceManifestSha256": binding["manifest_sha256"],
        "selectionSourceId": binding["selection_source_id"],
        "profileId": FACILITY_PROFILE,
        "requestId": binding["request_id"],
        "wireSha256": binding["wire_sha256"],
        "authoringOrigin": "integration-fixture-explicit",
        "integrationFixture": True,
        "humanApprovalClaimed": False,
        "trainingEligible": False,
        "dpoPerformed": False,
        "releaseReady": False,
        "acceptedResponse": accepted_descriptor,
        "acceptedResponseSha256": helper.sha256_bytes(accepted_canonical),
    }
    manifest_raw = helper.canonical_json(manifest) + b"\n"
    staging = output.parent / f".{output.name}.staging-{uuid.uuid4().hex}"
    try:
        staging.mkdir()
        (staging / helper.ACCEPTED_RESPONSE_FILE).write_bytes(accepted_raw)
        (staging / helper.MANIFEST_FILE).write_bytes(manifest_raw)
        finalize_create_only(staging, output)
    except OSError as error:
        raise helper.ResponseBuildError(
            "could not create facility response package; staging preserved at "
            f"{staging}: {error}") from error
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
    parser.add_argument("--positive-module-id", action="append", required=True)
    parser.add_argument("--drawback-module-id", action="append", default=[])
    parser.add_argument("--evidence-fact-id", action="append", required=True)
    parser.add_argument("--display-name", required=True)
    parser.add_argument("--narrative-flavor", required=True)
    args = parser.parse_args(argv)
    try:
        result = build_package(
            helper.project_root(args.project_root),
            args.source_directory,
            args.output_directory,
            args.positive_module_id,
            args.drawback_module_id,
            args.evidence_fact_id,
            args.display_name,
            args.narrative_flavor)
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        return 0
    except (helper.ResponseBuildError, OSError) as error:
        print(json.dumps({"status": "ERROR", "error": str(error)},
                         ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
