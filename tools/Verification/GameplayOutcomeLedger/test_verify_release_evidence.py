#!/usr/bin/env python3
"""Regression tests for the Phase80 release-evidence verifier."""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify_release_evidence.py")
GATES = (
    "sourceFreeze", "producerClosure", "unityCompile", "registry",
    "domainTransactions", "saveRestoreFaults", "memoryDeterminism",
    "presentationUi", "koreanJosa", "narrativeEvidence",
    "playerPerformance", "balance", "knowledgeBase",
)


class ReleaseEvidenceVerifierTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.proof = self.root / "Artifacts" / "QA" / "phase80-proof.txt"
        self.proof.parent.mkdir(parents=True)
        self.proof.write_text("PASS fixture\n", encoding="utf-8")
        self.digest = hashlib.sha256(self.proof.read_bytes()).hexdigest()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def report(self) -> dict:
        evidence = [{
            "path": "Artifacts/QA/phase80-proof.txt",
            "sha256": self.digest,
            "proof": "Fixture gate passed with current source.",
        }]
        gates = {name: {"status": "PASS", "evidence": evidence} for name in GATES}
        gates["sourceFreeze"].update({
            "startSha256": "a" * 64,
            "endSha256": "a" * 64,
        })
        gates["playerPerformance"].update({
            "playerBackendMeasured": True,
            "warmAllocationBytesPerResult": 0,
            "sustainedCapacityWait": False,
            "measuredResultLoads": [10, 100, 500],
            "longRunDaysMeasured": [100, 200],
            "saveBytesByDay": {"100": 1048576, "200": 1572864},
            "maximumSaveBytes": 2097152,
            "consolidationBacklogAtEnd": 0,
            "hardware": "fixture hardware",
            "unityVersion": "6000.fixture",
            "backend": "Mono fixture",
            "configuration": "Development fixture",
            "receiptShape": "small and large fixture",
        })
        gates["balance"].update({
            "gameplayValueChangeCount": 0,
            "mechanicsChanged": False,
        })
        gates["knowledgeBase"].update({
            "finalRebuildCount": 1,
            "failureCount": 0,
        })
        return {
            "schema": "gameplay-outcome-ledger-release-evidence@1",
            "sourceScanSha256": "a" * 64,
            "humanApprovalClaimed": False,
            "trainingEligible": False,
            "unityMcpUsed": False,
            "dungeonPlayerMcpUsed": False,
            "gates": gates,
        }

    def run_verifier(self, report: dict) -> subprocess.CompletedProcess[str]:
        path = self.root / "report.json"
        path.write_text(json.dumps(report), encoding="utf-8")
        return subprocess.run(
            [sys.executable, str(SCRIPT), "--report", str(path),
             "--source-root", str(self.root)],
            capture_output=True, text=True, check=False,
        )

    def test_valid_report_passes(self) -> None:
        result = self.run_verifier(self.report())
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_missing_gate_fails(self) -> None:
        report = self.report()
        del report["gates"]["registry"]
        result = self.run_verifier(report)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("missing gates: registry", result.stdout)

    def test_nonzero_warm_allocation_fails(self) -> None:
        report = self.report()
        report["gates"]["playerPerformance"]["warmAllocationBytesPerResult"] = 1
        result = self.run_verifier(report)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("warmAllocationBytesPerResult must be 0", result.stdout)

    def test_missing_long_run_checkpoint_fails(self) -> None:
        report = self.report()
        report["gates"]["playerPerformance"]["longRunDaysMeasured"] = [100]
        result = self.run_verifier(report)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("longRunDaysMeasured must be exactly 100,200", result.stdout)

    def test_save_checkpoint_over_declared_ceiling_fails(self) -> None:
        report = self.report()
        report["gates"]["playerPerformance"]["saveBytesByDay"]["200"] = 2097153
        result = self.run_verifier(report)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("saveBytesByDay[200] exceeds maximumSaveBytes", result.stdout)

    def test_nonzero_consolidation_backlog_fails(self) -> None:
        report = self.report()
        report["gates"]["playerPerformance"]["consolidationBacklogAtEnd"] = 1
        result = self.run_verifier(report)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("consolidationBacklogAtEnd must be 0", result.stdout)

    def test_false_human_approval_claim_fails(self) -> None:
        report = self.report()
        report["humanApprovalClaimed"] = True
        result = self.run_verifier(report)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("humanApprovalClaimed must remain false", result.stdout)


if __name__ == "__main__":
    unittest.main()
