#!/usr/bin/env python3
"""Regression tests for the fail-closed Phase80 closure verifier."""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify_final_manifest.py")


class FinalManifestVerifierTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.evidence = self.root / "Assets" / "Scripts" / "proof.cs"
        self.evidence.parent.mkdir(parents=True)
        self.evidence.write_text("sealed class Proof {}\n", encoding="utf-8")
        self.digest = hashlib.sha256(self.evidence.read_bytes()).hexdigest()
        self.runtime_evidence = self.root / "Artifacts" / "QA" / "proof-test.txt"
        self.runtime_evidence.parent.mkdir(parents=True)
        self.runtime_evidence.write_text("PASS proof runtime regression\n", encoding="utf-8")
        self.runtime_digest = hashlib.sha256(self.runtime_evidence.read_bytes()).hexdigest()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def inventory(self) -> dict:
        return {
            "metadata": {
                "run_mode": "final-frozen",
                "source_snapshot_stable_during_scan": True,
                "completeness_claim": "complete-lexical-scan-with-listed-limitations",
                "unresolved_publish_event_type_count": 0,
                "unknown_publish_receiver_count": 0,
                "source_scan_sha256": "a" * 64,
                "source_commit": "fixture-commit",
            },
            "definitions": [
                {
                    "base_type": "ProofReceipt",
                    "definition_path": "Assets/Scripts/proof.cs",
                    "inventory_scope": "pre-phase80-producer",
                }
            ],
        }

    def closure(self) -> dict:
        return {
            "schema": "gameplay-outcome-ledger-final-closure@1",
            "sourceCommit": "fixture-commit",
            "sourceScanSha256": "a" * 64,
            "candidateCount": 1,
            "humanApprovalClaimed": False,
            "trainingEligible": False,
            "rows": [
                {
                    "candidate": "ProofReceipt",
                    "definitionPath": "Assets/Scripts/proof.cs",
                    "status": "VERIFIED",
                    "disposition": "Record",
                    "reason": "A committed player-visible result is recorded.",
                    "sourceOwner": "ProofOwner",
                    "authoritySymbol": "ProofOwner.Commit",
                    "verificationEvidence": [
                        {
                            "kind": "source-owner",
                            "path": "Assets/Scripts/proof.cs",
                            "sha256": self.digest,
                            "proof": "Contains the authoritative commit boundary.",
                        },
                        {
                            "kind": "runtime-test",
                            "path": "Artifacts/QA/proof-test.txt",
                            "sha256": self.runtime_digest,
                            "proof": "Exercises commit, replay and query at runtime.",
                        }
                    ],
                    "ownerSeam": "ProofOwner.Commit",
                    "commitBoundary": "Domain state and its outbox commit together.",
                    "outboxSaveOwner": "ProofOwnerSaveState",
                    "replayIdentity": "Stable operation and revision identify retries.",
                    "uiQueryProof": "The shared outcome query exposes the committed row.",
                    "outcomeTypeId": "fixture.proof-recorded",
                    "adapterType": "ProofAdapter",
                    "descriptorType": "ProofDescriptor",
                    "projectorType": "ProofProjector",
                }
            ],
        }

    def run_verifier(self, inventory: dict, closure: dict) -> subprocess.CompletedProcess[str]:
        inventory_path = self.root / "inventory.json"
        closure_path = self.root / "closure.json"
        inventory_path.write_text(json.dumps(inventory), encoding="utf-8")
        closure_path.write_text(json.dumps(closure), encoding="utf-8")
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "--inventory",
                str(inventory_path),
                "--closure",
                str(closure_path),
                "--source-root",
                str(self.root),
            ],
            capture_output=True,
            check=False,
            text=True,
        )

    def test_valid_manifest_passes(self) -> None:
        result = self.run_verifier(self.inventory(), self.closure())
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_path_traversal_fails_even_with_matching_hash(self) -> None:
        outside = self.root.parent / "phase80-outside-proof.cs"
        outside.write_text("outside\n", encoding="utf-8")
        self.addCleanup(outside.unlink, missing_ok=True)
        closure = self.closure()
        entry = closure["rows"][0]["verificationEvidence"][0]
        entry["kind"] = "source-owner"
        entry["path"] = "../phase80-outside-proof.cs"
        entry["sha256"] = hashlib.sha256(outside.read_bytes()).hexdigest()
        result = self.run_verifier(self.inventory(), closure)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("escapes the repository root", result.stdout)

    def test_hash_mismatch_fails(self) -> None:
        closure = self.closure()
        closure["rows"][0]["verificationEvidence"][0]["sha256"] = "b" * 64
        result = self.run_verifier(self.inventory(), closure)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("evidence hash mismatch", result.stdout)

    def test_human_approval_claim_fails(self) -> None:
        closure = self.closure()
        closure["humanApprovalClaimed"] = True
        result = self.run_verifier(self.inventory(), closure)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("humanApprovalClaimed must remain false", result.stdout)

    def test_missing_candidate_row_fails(self) -> None:
        closure = self.closure()
        closure["rows"] = []
        result = self.run_verifier(self.inventory(), closure)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("missing closure rows: ProofReceipt", result.stdout)

    def test_record_without_runtime_evidence_fails(self) -> None:
        closure = self.closure()
        closure["rows"][0]["verificationEvidence"] = [
            closure["rows"][0]["verificationEvidence"][0]
        ]
        result = self.run_verifier(self.inventory(), closure)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Record requires runtime-test evidence", result.stdout)

    def test_non_result_without_source_audit_fails(self) -> None:
        closure = self.closure()
        row = closure["rows"][0]
        row["disposition"] = "NonResult"
        row["nonResultCategory"] = "pure-query-or-projection"
        result = self.run_verifier(self.inventory(), closure)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("NonResult requires source-audit evidence", result.stdout)


if __name__ == "__main__":
    unittest.main()
