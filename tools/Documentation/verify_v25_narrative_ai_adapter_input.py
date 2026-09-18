#!/usr/bin/env python3
"""Run the unmodified NarrativeAI adapter-v2 projection over a Unity export.

This verifier lives in the Unity workspace so it can produce review evidence
without writing to the read-only NarrativeAI workspace. It does not invoke a
model and deliberately rejects audit/witness fields in the projected input.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib
import json
import os
from pathlib import Path
import sys
from typing import Any


FORBIDDEN_MODEL_KEYS = {
    "accepted",
    "audit",
    "authorityContext",
    "expected",
    "expectedAccepted",
    "expectedError",
    "failureReason",
    "fixtureInput",
    "humanApprovalClaimed",
    "internalState",
    "privateState",
    "prompt",
    "response",
    "responseJson",
    "trainingEligible",
    "validatorResults",
}


def sha256_bytes(data: bytes) -> str:
    return "sha256:" + hashlib.sha256(data).hexdigest()


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def find_forbidden_keys(value: Any, path: str = "$") -> list[str]:
    findings: list[str] = []
    if isinstance(value, dict):
        for key, child in value.items():
            child_path = f"{path}.{key}"
            if key in FORBIDDEN_MODEL_KEYS:
                findings.append(child_path)
            findings.extend(find_forbidden_keys(child, child_path))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            findings.extend(find_forbidden_keys(child, f"{path}[{index}]"))
    return findings


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_atomic(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(data)
    os.replace(temporary, path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--export-dir", type=Path, required=True)
    parser.add_argument("--narrative-ai-root", type=Path, required=True)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()

    export_dir = args.export_dir.resolve()
    ai_root = args.narrative_ai_root.resolve()
    adapter_dir = ai_root / "tools" / "v25_narrative_training"
    adapter_path = adapter_dir / "scenario_adapter.py"
    continuity_path = adapter_dir / "narrative_continuity.py"
    if not adapter_path.is_file() or not continuity_path.is_file():
        raise SystemExit("NarrativeAI adapter-v2 source is missing.")

    sys.path.insert(0, str(adapter_dir))
    try:
        importlib.import_module("jsonschema")
    except ModuleNotFoundError as error:
        raise SystemExit(
            "NarrativeAI adapter-v2 requires the pinned jsonschema dependency."
        ) from error
    scenario_adapter = importlib.import_module("scenario_adapter")

    catalog = load_json(export_dir / "catalog.json")
    scenario_document = load_json(export_dir / "scenarios_100.json")
    scenarios = scenario_document.get("scenarios")
    if not isinstance(scenarios, list) or len(scenarios) != 100:
        raise SystemExit("Expected exactly 100 positive scenarios.")

    rows: list[dict[str, Any]] = []
    profile_counts: dict[str, dict[str, int]] = {}
    pass_count = 0
    for scenario in scenarios:
        scenario_id = scenario.get("scenarioId", "")
        profile_id = scenario.get("profileId", "")
        bucket = profile_counts.setdefault(profile_id, {"pass": 0, "fail": 0})
        try:
            projected = scenario_adapter.model_input(
                scenario,
                catalog,
                adapter_version=2,
            )
            forbidden = find_forbidden_keys(projected)
            if forbidden:
                raise ValueError(
                    "forbidden audit/witness fields in model input: "
                    + ", ".join(forbidden)
                )
            rows.append(
                {
                    "scenarioId": scenario_id,
                    "profileId": profile_id,
                    "status": "PASS",
                    "modelInputSha256": sha256_bytes(canonical_bytes(projected)),
                }
            )
            pass_count += 1
            bucket["pass"] += 1
        except Exception as error:  # preserve the unchanged adapter's exact reason
            rows.append(
                {
                    "scenarioId": scenario_id,
                    "profileId": profile_id,
                    "status": "FAIL",
                    "error": str(error),
                }
            )
            bucket["fail"] += 1

    fail_count = len(scenarios) - pass_count
    report = {
        "schemaVersion": 1,
        "sourceExport": str(export_dir),
        "adapterVersion": 2,
        "adapterSourceSha256": sha256_bytes(adapter_path.read_bytes()),
        "continuityValidatorSourceSha256": sha256_bytes(continuity_path.read_bytes()),
        "catalogHash": catalog.get("catalogHash", ""),
        "inputDigest": scenario_document.get("inputDigest", ""),
        "modelInputProjectionRun": "RUN",
        "modelInferenceRun": "NOT_RUN",
        "positiveCount": len(scenarios),
        "passCount": pass_count,
        "failCount": fail_count,
        "profileCounts": dict(sorted(profile_counts.items())),
        "status": "PASS" if fail_count == 0 else "FAIL",
        "rows": rows,
    }
    encoded = canonical_bytes(report) + b"\n"
    if args.report is not None:
        write_atomic(args.report.resolve(), encoded)
    sys.stdout.buffer.write(encoded)
    return 0 if fail_count == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
