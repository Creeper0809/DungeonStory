#!/usr/bin/env python3
"""Fail-closed verifier for the Phase80 producer closure manifest.

The lexical inventory is deliberately conservative.  This verifier requires a
reviewed disposition for every pre-existing candidate and refuses intermediate
states such as planned, blocked, or source-only.  It is independent of Unity so
the same evidence can be checked again after the final frozen source scan.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any


SCHEMA = "gameplay-outcome-ledger-final-closure@1"
FINAL_INVENTORY_CLAIM = "complete-lexical-scan-with-listed-limitations"
DISPOSITIONS = {"Record", "ExactSubstitute", "NonResult"}
NON_RESULT_CATEGORIES = {
    "debug-diagnostic",
    "presentation-ui-wrapper",
    "pure-query-or-projection",
    "preview-or-candidate",
    "persistence-maintenance",
    "lifecycle-or-progress-signal",
    "contract-or-interface",
    "duplicate-derived-notification",
}
STABLE_ID = re.compile(r"^[a-z0-9][a-z0-9._:-]*$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
EVIDENCE_KINDS = {
    "source-owner",
    "source-audit",
    "runtime-test",
    "ui-proof",
    "save-fault-test",
    "player-measurement",
}


def load_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: root must be an object")
    return value


def text(value: Any) -> str:
    return value.strip() if isinstance(value, str) else ""


def require_text(row: dict[str, Any], key: str, errors: list[str], minimum: int = 1) -> str:
    value = text(row.get(key))
    if len(value) < minimum:
        errors.append(f"{row.get('candidate', '<unknown>')}: {key} is missing or too short")
    return value


def verify_evidence(
    candidate: str,
    entries: Any,
    source_root: Path,
    errors: list[str],
) -> set[str]:
    kinds: set[str] = set()
    if not isinstance(entries, list) or not entries:
        errors.append(f"{candidate}: verificationEvidence must be a non-empty array")
        return kinds
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            errors.append(f"{candidate}: verificationEvidence[{index}] must be an object")
            continue
        relative = text(entry.get("path")).replace("\\", "/")
        expected = text(entry.get("sha256"))
        proof = text(entry.get("proof"))
        kind = text(entry.get("kind"))
        if kind not in EVIDENCE_KINDS:
            errors.append(f"{candidate}: invalid evidence kind {kind!r} for {relative!r}")
        else:
            kinds.add(kind)
            if kind == "source-owner" and not relative.startswith("Assets/Scripts/"):
                errors.append(f"{candidate}: source-owner evidence must be under Assets/Scripts: {relative}")
            if kind in {"runtime-test", "ui-proof", "save-fault-test", "player-measurement"} \
                    and not relative.startswith("Artifacts/QA/"):
                errors.append(f"{candidate}: {kind} evidence must be under Artifacts/QA: {relative}")
        if not relative or relative.startswith("/") or ":" in relative.split("/")[0]:
            errors.append(f"{candidate}: evidence path must be repository-relative: {relative!r}")
            continue
        if not SHA256.fullmatch(expected):
            errors.append(f"{candidate}: evidence SHA-256 is invalid for {relative}")
            continue
        if len(proof) < 12:
            errors.append(f"{candidate}: evidence proof is missing for {relative}")
        path = (source_root / relative).resolve()
        try:
            path.relative_to(source_root)
        except ValueError:
            errors.append(
                f"{candidate}: evidence path escapes the repository root: {relative}"
            )
            continue
        if not path.is_file():
            errors.append(f"{candidate}: evidence file does not exist: {relative}")
            continue
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != expected:
            errors.append(
                f"{candidate}: evidence hash mismatch for {relative}: expected {expected}, got {actual}"
            )
    return kinds


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--closure", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, required=True)
    args = parser.parse_args()

    inventory = load_json(args.inventory.resolve())
    closure = load_json(args.closure.resolve())
    source_root = args.source_root.resolve()
    errors: list[str] = []

    metadata = inventory.get("metadata")
    if not isinstance(metadata, dict):
        errors.append("inventory metadata is missing")
        metadata = {}
    if metadata.get("run_mode") != "final-frozen":
        errors.append("inventory was not generated with --final-frozen")
    if metadata.get("source_snapshot_stable_during_scan") is not True:
        errors.append("inventory source digest changed during the scan")
    if metadata.get("completeness_claim") != FINAL_INVENTORY_CLAIM:
        errors.append("inventory does not carry the final completeness claim")
    if metadata.get("unresolved_publish_event_type_count") != 0:
        errors.append("inventory still contains unresolved Publish<T> calls")
    if metadata.get("unknown_publish_receiver_count") != 0:
        errors.append("inventory still contains unknown Publish receivers")

    if closure.get("schema") != SCHEMA:
        errors.append(f"closure schema must be {SCHEMA}")
    if closure.get("humanApprovalClaimed") is not False:
        errors.append("humanApprovalClaimed must remain false")
    if closure.get("trainingEligible") is not False:
        errors.append("trainingEligible must remain false")
    if closure.get("sourceScanSha256") != metadata.get("source_scan_sha256"):
        errors.append("closure sourceScanSha256 does not match the final inventory")
    if closure.get("sourceCommit") != metadata.get("source_commit"):
        errors.append("closure sourceCommit does not match the final inventory")

    definitions = inventory.get("definitions")
    if not isinstance(definitions, list):
        errors.append("inventory definitions is not an array")
        definitions = []
    candidates: dict[str, dict[str, Any]] = {}
    for definition in definitions:
        if not isinstance(definition, dict):
            errors.append("inventory contains a non-object definition")
            continue
        if definition.get("inventory_scope") == "phase80-implementation-infrastructure":
            continue
        name = text(definition.get("base_type"))
        if not name:
            errors.append("inventory definition has no base_type")
            continue
        if name in candidates:
            errors.append(f"inventory contains duplicate candidate {name}")
        candidates[name] = definition

    rows = closure.get("rows")
    if not isinstance(rows, list):
        errors.append("closure rows is not an array")
        rows = []
    by_name: dict[str, dict[str, Any]] = {}
    record_outcome_types: set[str] = set()
    record_count = 0
    for row in rows:
        if not isinstance(row, dict):
            errors.append("closure contains a non-object row")
            continue
        candidate = text(row.get("candidate"))
        if not candidate:
            errors.append("closure row has no candidate")
            continue
        if candidate in by_name:
            errors.append(f"closure contains duplicate row {candidate}")
            continue
        by_name[candidate] = row
        definition = candidates.get(candidate)
        if definition is None:
            errors.append(f"closure row is not in final inventory: {candidate}")
            continue
        if row.get("status") != "VERIFIED":
            errors.append(f"{candidate}: status must be VERIFIED")
        if text(row.get("definitionPath")) != text(definition.get("definition_path")):
            errors.append(f"{candidate}: definitionPath does not match inventory")
        disposition = row.get("disposition")
        if disposition not in DISPOSITIONS:
            errors.append(f"{candidate}: invalid disposition {disposition!r}")
            continue
        require_text(row, "reason", errors, minimum=20)
        require_text(row, "sourceOwner", errors, minimum=3)
        require_text(row, "authoritySymbol", errors, minimum=3)
        evidence_kinds = verify_evidence(
            candidate, row.get("verificationEvidence"), source_root, errors)

        if disposition == "NonResult":
            category = row.get("nonResultCategory")
            if category not in NON_RESULT_CATEGORIES:
                errors.append(f"{candidate}: invalid nonResultCategory {category!r}")
            if "source-audit" not in evidence_kinds:
                errors.append(f"{candidate}: NonResult requires source-audit evidence")
            continue

        if "source-owner" not in evidence_kinds:
            errors.append(f"{candidate}: {disposition} requires source-owner evidence")
        if "runtime-test" not in evidence_kinds:
            errors.append(f"{candidate}: {disposition} requires runtime-test evidence")

        require_text(row, "ownerSeam", errors, minimum=8)
        require_text(row, "commitBoundary", errors, minimum=20)
        require_text(row, "outboxSaveOwner", errors, minimum=8)
        require_text(row, "replayIdentity", errors, minimum=20)
        require_text(row, "uiQueryProof", errors, minimum=20)
        if disposition == "Record":
            record_count += 1
            outcome_type = require_text(row, "outcomeTypeId", errors, minimum=3)
            if outcome_type and not STABLE_ID.fullmatch(outcome_type):
                errors.append(f"{candidate}: outcomeTypeId is not a stable ID")
            record_outcome_types.add(outcome_type)
            require_text(row, "adapterType", errors, minimum=3)
            require_text(row, "descriptorType", errors, minimum=3)
            require_text(row, "projectorType", errors, minimum=3)
        else:
            substitute = require_text(row, "substituteOutcomeTypeId", errors, minimum=3)
            if substitute and not STABLE_ID.fullmatch(substitute):
                errors.append(f"{candidate}: substituteOutcomeTypeId is not a stable ID")
            require_text(row, "sameOperationProof", errors, minimum=20)
            preserved = row.get("preservedFacts")
            if not isinstance(preserved, list) or not preserved or any(not text(value) for value in preserved):
                errors.append(f"{candidate}: preservedFacts must be a non-empty string array")

    missing = sorted(set(candidates) - set(by_name), key=str.lower)
    extra = sorted(set(by_name) - set(candidates), key=str.lower)
    if missing:
        errors.append("missing closure rows: " + ", ".join(missing))
    if extra:
        errors.append("extra closure rows: " + ", ".join(extra))
    if closure.get("candidateCount") != len(candidates):
        errors.append(
            f"candidateCount mismatch: closure={closure.get('candidateCount')!r}, inventory={len(candidates)}"
        )

    if errors:
        print("FAIL Phase80 final closure manifest")
        for error in errors:
            print(f"- {error}")
        return 1
    print(
        "PASS Phase80 final closure manifest: "
        f"{len(candidates)} candidates, {record_count} direct Record rows, "
        f"{len(record_outcome_types)} registered outcome types"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
