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


def verify_csv(manifest: dict[str, object]) -> None:
    path = QA / "v27-balance-before-after.csv"
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        fail("V27 CSV must be UTF-8 without BOM")
    if not raw.endswith(b"\r\n"):
        fail("V27 CSV must end with RFC 4180 CRLF")

    expected_header = ("domain", "definitionKind", "stableId", "metric")
    previous: tuple[str, str, str, str] | None = None
    seen: set[tuple[str, str, str, str]] = set()
    row_count = 0
    with path.open("r", encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream, strict=True)
        if reader.fieldnames is None:
            fail("V27 CSV has no header")
        missing = [name for name in expected_header if name not in reader.fieldnames]
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
            row_count += 1

    expected_rows = int(manifest.get("rowCount", -1))
    if row_count != expected_rows:
        fail(f"V27 CSV row count mismatch: expected={expected_rows} actual={row_count}")


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
        "approvalDigest": "docs/game-design/v27-balance-critical-approvals.json",
        "analyzerSourceHash": "tools/DungeonStory.BalanceAnalyzers/DungeonStoryBalanceAnalyzer.cs",
        "analyzerDllHash": "Assets/Analyzers/DungeonStory.BalanceAnalyzers.dll",
    }
    for key, path in hashes.items():
        require_hash(manifest, key, path)

    verify_csv(manifest)
    verify_combat()
    verify_daily_routine()
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
