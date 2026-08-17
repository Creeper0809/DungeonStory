#!/usr/bin/env python3
"""Portable CI verification for committed V27 artifacts.

Unity remains the authority that generates the artifacts. This verifier deliberately
does not regenerate gameplay data without a licensed Editor; it proves that the
reviewed artifact set is byte-exact, internally ordered, complete, and bound to the
same analyzer, approvals, and durable gameplay evidence recorded by the manifest.
"""

from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[2]
QA = ROOT / "Artifacts" / "QA"
MANIFEST_PATH = QA / "v27-balance-artifact-manifest.json"
SOURCE_INVENTORY_PATH = QA / "v27-balance-source-inventory.json"


def fail(message: str) -> None:
    raise RuntimeError(message)


def sha256(path: Path) -> str:
    if not path.is_file():
        fail(f"required artifact is missing: {path.relative_to(ROOT)}")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def require_hash(manifest: dict[str, object], key: str, relative: str) -> None:
    expected = str(manifest.get(key, "")).lower()
    actual = sha256(ROOT / relative)
    if not expected or actual != expected:
        fail(f"{key} mismatch for {relative}: expected={expected} actual={actual}")


def require_text(path: Path, markers: tuple[str, ...]) -> None:
    if not path.is_file():
        fail(f"required report is missing: {path.relative_to(ROOT)}")
    text = path.read_text(encoding="utf-8-sig")
    for marker in markers:
        if marker not in text:
            fail(f"missing marker {marker!r} in {path.relative_to(ROOT)}")


def verify_source_inventory(manifest: dict[str, object]) -> None:
    inventory = json.loads(SOURCE_INVENTORY_PATH.read_text(encoding="utf-8"))
    if inventory.get("schemaVersion") != "v27.source.v1":
        fail(f"unexpected source inventory schema: {inventory.get('schemaVersion')}")
    entries = inventory.get("entries")
    if not isinstance(entries, list) or not entries:
        fail("V27 source inventory has no entries")
    if len(entries) != int(manifest.get("sourceCount", -1)):
        fail(
            "V27 source inventory count mismatch: "
            f"manifest={manifest.get('sourceCount')} inventory={len(entries)}"
        )

    previous = ""
    pairs: list[tuple[str, str]] = []
    seen: set[str] = set()
    root = ROOT.resolve()
    for entry in entries:
        if not isinstance(entry, dict):
            fail("V27 source inventory contains a non-object entry")
        relative = str(entry.get("path", ""))
        expected = str(entry.get("sha256", "")).lower()
        if not relative or "\\" in relative or relative.startswith("/") or ".." in Path(relative).parts:
            fail(f"non-canonical source inventory path: {relative!r}")
        if relative in seen or (previous and relative <= previous):
            fail(f"duplicate or unsorted source inventory path: {relative}")
        path = (ROOT / relative).resolve()
        if root not in path.parents or not path.is_file():
            fail(f"source inventory path is missing or outside the repository: {relative}")
        actual = sha256(path)
        if actual != expected:
            fail(f"source digest mismatch for {relative}: expected={expected} actual={actual}")
        seen.add(relative)
        previous = relative
        pairs.append((relative, expected))

    canonical = "".join(f"{path}={digest}\n" for path, digest in pairs).encode("utf-8")
    aggregate = hashlib.sha256(canonical).hexdigest()
    if aggregate != str(manifest.get("sourceDigest", "")).lower():
        fail(
            "aggregate source digest mismatch: "
            f"expected={manifest.get('sourceDigest')} actual={aggregate}"
        )


def verify_csv(manifest: dict[str, object]) -> set[str]:
    path = QA / "v27-balance-before-after.csv"
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        fail("V27 CSV must be UTF-8 without BOM")
    if not raw.endswith(b"\r\n"):
        fail("V27 CSV must end with RFC 4180 CRLF")

    expected_header = ("domain", "definitionKind", "stableId", "metric")
    required_evidence = (
        "balanceBaselineRecordId",
        "sourceAuthority",
        "sourcePropertyPath",
        "executionRoute",
        "saveAuthority",
        "verificationEvidence",
        "dependencyFingerprint",
        "sourceDigest",
        "semanticHash",
    )
    previous: tuple[str, str, str, str] | None = None
    seen: set[tuple[str, str, str, str]] = set()
    baseline_ids: set[str] = set()
    row_count = 0
    with path.open("r", encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream, strict=True)
        if reader.fieldnames is None:
            fail("V27 CSV has no header")
        missing = [name for name in expected_header if name not in reader.fieldnames]
        missing.extend(name for name in required_evidence if name not in reader.fieldnames)
        if missing:
            fail(f"V27 CSV is missing key columns: {missing}")
        for row in reader:
            key = tuple(row[name] for name in expected_header)
            if key in seen:
                fail(f"duplicate V27 stable key: {key}")
            if previous is not None and key < previous:
                fail(f"V27 CSV sort order regressed: previous={previous} current={key}")
            seen.add(key)
            previous = key
            for name in required_evidence:
                if not row[name].strip():
                    fail(f"V27 CSV row {key} has empty required field {name}")
            if row.get("reviewStatus") == "pending" and not row.get(
                "anomalyDisposition", ""
            ).startswith("collapsed-"):
                fail(f"V27 CSV row {key} is pending without a collapsed disposition")
            baseline_ids.add(row["balanceBaselineRecordId"])
            row_count += 1

    expected_rows = int(manifest.get("rowCount", -1))
    if row_count != expected_rows:
        fail(f"V27 CSV row count mismatch: expected={expected_rows} actual={row_count}")
    return baseline_ids


def verify_approvals(manifest: dict[str, object]) -> None:
    approval_path = ROOT / "docs/game-design/v27-balance-critical-approvals.json"
    payload = json.loads(approval_path.read_text(encoding="utf-8"))
    if payload.get("schemaVersion") != "v27.2":
        fail(f"unexpected approval schema: {payload.get('schemaVersion')}")
    approvals = payload.get("approvals")
    if not isinstance(approvals, list):
        fail("V27 approval authority has no approvals array")
    if len(approvals) != int(manifest.get("approvedCount", -1)):
        fail(
            "V27 approval count mismatch: "
            f"manifest={manifest.get('approvedCount')} file={len(approvals)}"
        )
    required = (
        "approvalKey",
        "rootStableId",
        "metric",
        "exactBeforeValue",
        "exactAfterValue",
        "dependencyFingerprint",
        "sourceDigest",
        "reasonCode",
        "balanceBaselineRecordId",
    )
    keys: set[str] = set()
    for approval in approvals:
        if not isinstance(approval, dict):
            fail("V27 approval authority contains a non-object entry")
        missing = [name for name in required if not str(approval.get(name, "")).strip()]
        if missing:
            fail(f"V27 approval is missing exact fields {missing}: {approval}")
        key = str(approval["approvalKey"])
        if key in keys:
            fail(f"duplicate V27 approval key: {key}")
        keys.add(key)


def verify_combat() -> None:
    require_text(
        QA / "combat-balance-final.txt",
        ("RESULT=PASS", "encounters=36", "samplesPerEncounter=1000", "failures=0"),
    )
    for number in range(1, 37):
        require_text(
            QA / "combat-balance-final" / f"encounter-{number:02d}.txt",
            ("RESULT=PASS; samples=1000; failures=0; stalled=0",),
        )


def verify_daily_routine() -> None:
    for seed in (157181, 157182, 157183):
        require_text(
            QA / f"phase157-daily-routine-wu-seed-{seed}.txt",
            (
                "observedDays=5",
                f"runSeed={seed}",
                "runtimeDiagnosticsGate=ai-runtime-gate-v3",
                "RESULT=PASS; failures=0",
                "capturedIssues=0",
            ),
        )


def main() -> int:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    if manifest.get("schemaVersion") != "v27.1":
        fail(f"unexpected manifest schema: {manifest.get('schemaVersion')}")
    if int(manifest.get("criticalCount", -1)) != 0:
        fail("unresolved V27 Critical nodes remain")
    if int(manifest.get("integrityFailureCount", -1)) != 0:
        fail("V27 manifest records integrity failures")
    if int(manifest.get("approvedCount", 0)) <= 0:
        fail("V27 manifest has no exact approvals")

    hashes = {
        "sourceInventoryByteHash": "Artifacts/QA/v27-balance-source-inventory.json",
        "csvByteHash": "Artifacts/QA/v27-balance-before-after.csv",
        "markdownByteHash": "docs/generated/V27_Balance_Before_After.md",
        "auditByteHash": "Artifacts/QA/v27-balance-recalibration-audit.txt",
        "anomalyGraphByteHash": "Artifacts/QA/v27-balance-anomaly-graph.json",
        "economy256EvidenceHash": "Artifacts/QA/v27-balance-economy-256-seed.txt",
        "verticalSliceFullLoopEvidenceHash": "Artifacts/QA/v27-balance-vertical-slice-full-loop-playmode.txt",
        "assetRollbackEvidenceHash": "Artifacts/QA/v27-balance-asset-rollback.txt",
        "marketAuthorityEvidenceHash": "Artifacts/QA/v27-balance-market-authority.txt",
        "laborFacilityAuthorityEvidenceHash": "Artifacts/QA/v27-balance-labor-facility-authority.txt",
        "combatOutcome1000SeedEvidenceHash": "Artifacts/QA/combat-balance-final.txt",
        "wholeGameCoverageEvidenceHash": "Artifacts/QA/v27-balance-whole-game-coverage.txt",
        "laborAuthorityMatrixEvidenceHash": "Artifacts/QA/v27-labor-authority-matrix.txt",
        "ledgerContractEvidenceHash": "Artifacts/QA/v27-balance-ledger-contracts.txt",
        "equipmentReadinessEvidenceHash": "Artifacts/QA/v26-equipment-readiness-throughput.md",
        "dailyRoutine157181EvidenceHash": "Artifacts/QA/phase157-daily-routine-wu-seed-157181.txt",
        "dailyRoutine157182EvidenceHash": "Artifacts/QA/phase157-daily-routine-wu-seed-157182.txt",
        "dailyRoutine157183EvidenceHash": "Artifacts/QA/phase157-daily-routine-wu-seed-157183.txt",
        "finalAcceptanceEvidenceHash": "Artifacts/QA/final-acceptance-report.txt",
        "approvalDigest": "docs/game-design/v27-balance-critical-approvals.json",
        "analyzerSourceHash": "tools/DungeonStory.BalanceAnalyzers/DungeonStoryBalanceAnalyzer.cs",
        "analyzerDllHash": "Assets/Analyzers/DungeonStory.BalanceAnalyzers.dll",
    }
    for key, path in hashes.items():
        require_hash(manifest, key, path)

    verify_source_inventory(manifest)
    baseline_ids = verify_csv(manifest)
    manifest_baselines = set(manifest.get("balanceBaselineRecordIds", []))
    if not baseline_ids.issubset(manifest_baselines):
        fail(f"CSV baseline ids are absent from manifest: {sorted(baseline_ids - manifest_baselines)}")
    baseline_text = (ROOT / "docs/game-design/whole-game-balance-baseline.md").read_text(
        encoding="utf-8"
    )
    missing_baselines = sorted(value for value in manifest_baselines if value not in baseline_text)
    if missing_baselines:
        fail(f"manifest baseline records are missing from the authority document: {missing_baselines}")
    verify_approvals(manifest)
    verify_combat()
    verify_daily_routine()
    require_text(
        QA / "v27-balance-ledger-contracts.txt",
        (
            "RESULT=PASS; checks=13",
            "PASS V27_MEWU_ASYMMETRIC_QUANTIZATION",
            "PASS V27_ATTRIBUTION_COLLAPSE_EPSILON_ISOLATED",
            "PASS V27_SCC_ZERO_TOLERANCE",
            "PASS V27_CSV_RFC4180_ESCAPE",
            "PASS V27_STABLE_SORT_P95_2MS_ZERO_ALLOC",
            "PASS V27_CSV_ESCAPE_P95_2MS_ZERO_ALLOC",
        ),
    )
    require_text(
        QA / "v27-balance-asset-rollback.txt",
        (
            "RESULT=PASS; failures=0",
            "PASS V27_ASSET_ROLLBACK_YAML_BYTE_EXACT",
            "PASS V27_ASSET_ROLLBACK_META_GUID_FILEID_EXACT",
        ),
    )
    require_text(
        QA / "v27-balance-market-authority.txt",
        ("RESULT=PASS; failures=0", "MARKET_SALE_OUTPUT_FLOOR_EXACT"),
    )
    require_text(
        QA / "v27-balance-labor-facility-authority.txt",
        (
            "RESULT=PASS; stage=applied; failures=0",
            "V27_LABOR_AUTHORED_WU_SCALE_EXACT rows=730",
            "V27_RESEARCH_WU_ASSET_APPLIED_EXACT applied=180; total=180",
        ),
    )
    require_text(
        QA / "v27-labor-authority-matrix.txt",
        ("RESULT=PASS; cells=360", "PASS V27_LABOR_MATRIX_360_CELLS count=360"),
    )
    require_text(
        QA / "v27-balance-vertical-slice-full-loop-playmode.txt",
        (
            "RESULT=PASS; checks=11; failures=0",
            "PASS V27_SLICE_REBUILD_COMPLETED",
            "PASS V27_SLICE_CONSOLE_ZERO warnings=0; errors=0",
        ),
    )
    require_text(
        QA / "v26-equipment-readiness-throughput.md",
        ("## Checkpoint throughput", "| 960 | 48h |", "| PASS |"),
    )
    require_text(
        QA / "final-acceptance-report.txt",
        ("ExpectedSteps: 33", "ActualSteps: 33", "Passed: 33", "Failed: 0"),
    )
    require_text(
        QA / "v27-balance-economy-256-seed.txt",
        (
            "RESULT=PASS; seeds=256; failures=0",
            f"rows={manifest['rowCount']}; critical=0; scc={manifest['sccCount']}",
        ),
    )
    require_text(
        QA / "v27-balance-whole-game-coverage.txt",
        ("RESULT=PASS", "producerOrphans=0", "consumerOrphans=0", "approvedUnapplied=0"),
    )

    print(
        "RESULT=PASS; "
        f"rows={manifest['rowCount']}; critical=0; scc={manifest['sccCount']}; "
        "combat=36x1000; dailySeeds=3; finalAcceptance=33/33"
    )
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:  # CI needs one stable fail-loud boundary.
        print(f"RESULT=FAIL; {error}", file=sys.stderr)
        sys.exit(1)
