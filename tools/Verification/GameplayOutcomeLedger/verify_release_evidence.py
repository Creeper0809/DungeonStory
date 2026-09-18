#!/usr/bin/env python3
"""Verify the final, cross-cutting Phase80 release evidence bundle."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any


SCHEMA = "gameplay-outcome-ledger-release-evidence@1"
SHA256 = re.compile(r"^[0-9a-f]{64}$")
REQUIRED_GATES = {
    "sourceFreeze",
    "producerClosure",
    "unityCompile",
    "registry",
    "domainTransactions",
    "saveRestoreFaults",
    "memoryDeterminism",
    "presentationUi",
    "koreanJosa",
    "narrativeEvidence",
    "playerPerformance",
    "balance",
    "knowledgeBase",
}


def load(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError("release evidence root must be an object")
    return value


def clean(value: Any) -> str:
    return value.strip() if isinstance(value, str) else ""


def verify_evidence(entries: Any, root: Path, gate: str, errors: list[str]) -> None:
    if not isinstance(entries, list) or not entries:
        errors.append(f"{gate}: evidence must be a non-empty array")
        return
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            errors.append(f"{gate}: evidence[{index}] must be an object")
            continue
        relative = clean(entry.get("path")).replace("\\", "/")
        expected = clean(entry.get("sha256"))
        proof = clean(entry.get("proof"))
        if not relative or relative.startswith("/") or ":" in relative.split("/")[0]:
            errors.append(f"{gate}: evidence path must be repository-relative")
            continue
        if not SHA256.fullmatch(expected):
            errors.append(f"{gate}: invalid evidence SHA-256 for {relative}")
            continue
        if len(proof) < 12:
            errors.append(f"{gate}: evidence proof is missing for {relative}")
        path = (root / relative).resolve()
        try:
            path.relative_to(root)
        except ValueError:
            errors.append(f"{gate}: evidence path escapes repository root: {relative}")
            continue
        if not path.is_file():
            errors.append(f"{gate}: evidence file does not exist: {relative}")
            continue
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != expected:
            errors.append(f"{gate}: evidence hash mismatch for {relative}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.source_root.resolve()
    report = load(args.report.resolve())
    errors: list[str] = []

    if report.get("schema") != SCHEMA:
        errors.append(f"schema must be {SCHEMA}")
    if report.get("humanApprovalClaimed") is not False:
        errors.append("humanApprovalClaimed must remain false")
    if report.get("trainingEligible") is not False:
        errors.append("trainingEligible must remain false")
    if report.get("unityMcpUsed") is not False:
        errors.append("unityMcpUsed must be false")
    if report.get("dungeonPlayerMcpUsed") is not False:
        errors.append("dungeonPlayerMcpUsed must be false")
    if not SHA256.fullmatch(clean(report.get("sourceScanSha256"))):
        errors.append("sourceScanSha256 is invalid")

    gates = report.get("gates")
    if not isinstance(gates, dict):
        errors.append("gates must be an object")
        gates = {}
    missing = sorted(REQUIRED_GATES - set(gates))
    extra = sorted(set(gates) - REQUIRED_GATES)
    if missing:
        errors.append("missing gates: " + ", ".join(missing))
    if extra:
        errors.append("unknown gates: " + ", ".join(extra))
    for name in sorted(REQUIRED_GATES):
        gate = gates.get(name)
        if not isinstance(gate, dict):
            continue
        if gate.get("status") != "PASS":
            errors.append(f"{name}: status must be PASS")
        verify_evidence(gate.get("evidence"), root, name, errors)

    freeze = gates.get("sourceFreeze", {})
    if freeze.get("startSha256") != report.get("sourceScanSha256") \
            or freeze.get("endSha256") != report.get("sourceScanSha256"):
        errors.append("sourceFreeze: start/end digests must equal sourceScanSha256")

    performance = gates.get("playerPerformance", {})
    if performance.get("playerBackendMeasured") is not True:
        errors.append("playerPerformance: Player backend measurement is required")
    if performance.get("warmAllocationBytesPerResult") != 0:
        errors.append("playerPerformance: warmAllocationBytesPerResult must be 0")
    if performance.get("sustainedCapacityWait") is not False:
        errors.append("playerPerformance: sustainedCapacityWait must be false")
    loads = performance.get("measuredResultLoads")
    if not isinstance(loads, list) or sorted(set(loads)) != [10, 100, 500]:
        errors.append("playerPerformance: measuredResultLoads must be exactly 10,100,500")
    long_run_days = performance.get("longRunDaysMeasured")
    if (not isinstance(long_run_days, list)
            or any(isinstance(day, bool) or not isinstance(day, int) for day in long_run_days)
            or sorted(set(long_run_days)) != [100, 200]):
        errors.append("playerPerformance: longRunDaysMeasured must be exactly 100,200")

    maximum_save_bytes = performance.get("maximumSaveBytes")
    if (isinstance(maximum_save_bytes, bool)
            or not isinstance(maximum_save_bytes, int)
            or maximum_save_bytes <= 0):
        errors.append("playerPerformance: maximumSaveBytes must be a positive integer")

    save_bytes_by_day = performance.get("saveBytesByDay")
    if not isinstance(save_bytes_by_day, dict) or set(save_bytes_by_day) != {"100", "200"}:
        errors.append("playerPerformance: saveBytesByDay must contain exactly day 100 and 200")
    else:
        for day in ("100", "200"):
            byte_count = save_bytes_by_day.get(day)
            if isinstance(byte_count, bool) or not isinstance(byte_count, int) or byte_count <= 0:
                errors.append(f"playerPerformance: saveBytesByDay[{day}] must be a positive integer")
            elif (isinstance(maximum_save_bytes, int)
                    and not isinstance(maximum_save_bytes, bool)
                    and maximum_save_bytes > 0
                    and byte_count > maximum_save_bytes):
                errors.append(
                    f"playerPerformance: saveBytesByDay[{day}] exceeds maximumSaveBytes")

    backlog = performance.get("consolidationBacklogAtEnd")
    if isinstance(backlog, bool) or backlog != 0:
        errors.append("playerPerformance: consolidationBacklogAtEnd must be 0")
    for field in ("hardware", "unityVersion", "backend", "configuration", "receiptShape"):
        if len(clean(performance.get(field))) < 3:
            errors.append(f"playerPerformance: {field} is required")

    balance = gates.get("balance", {})
    if balance.get("gameplayValueChangeCount") != 0:
        errors.append("balance: gameplayValueChangeCount must be 0")
    if balance.get("mechanicsChanged") is not False:
        errors.append("balance: mechanicsChanged must be false")

    kb = gates.get("knowledgeBase", {})
    if kb.get("finalRebuildCount") != 1:
        errors.append("knowledgeBase: finalRebuildCount must be exactly 1")
    if kb.get("failureCount") != 0:
        errors.append("knowledgeBase: failureCount must be 0")

    if errors:
        print("FAIL Phase80 release evidence")
        for error in errors:
            print(f"- {error}")
        return 1
    print("PASS Phase80 release evidence: all 13 gates are hash-backed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
