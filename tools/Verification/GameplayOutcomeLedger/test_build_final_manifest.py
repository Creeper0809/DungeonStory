#!/usr/bin/env python3
"""Regression tests for the deterministic Phase80 closure builder."""

from __future__ import annotations

import copy
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from collections import Counter
from pathlib import Path


SCRIPT = Path(__file__).with_name("build_final_manifest.py")
VERIFIER = Path(__file__).with_name("verify_final_manifest.py")
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
INVENTORY = (
    REPOSITORY_ROOT
    / "Artifacts"
    / "QA"
    / "GameplayOutcomeLedgerPhase80InventoryReviewedGroups1-2-3-4-5-6-7-20260920-r42"
    / "producer-inventory.json"
)
SPEC = importlib.util.spec_from_file_location("phase80_final_manifest_builder", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
builder = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = builder
SPEC.loader.exec_module(builder)


class FinalManifestBuilderTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.inventory = builder.load_json(INVENTORY)

    def test_registered_catalog_and_explicit_bindings_cover_all_records(self) -> None:
        migrated = builder.parse_migrated_bindings(REPOSITORY_ROOT)
        self.assertEqual(57, len(migrated))
        self.assertEqual("character.death", migrated["CharacterDeathEvent"].outcome_type_id)
        self.assertEqual(
            "inventory.stock-supplied",
            migrated["StockSupplyResult"].outcome_type_id,
        )
        self.assertEqual(38, len(builder.EXPLICIT_BINDINGS))

    def test_frozen_r42_builds_and_passes_independent_verifier(self) -> None:
        manifest = builder.build_manifest(self.inventory, REPOSITORY_ROOT)
        self.assertEqual(323, manifest["candidateCount"])
        self.assertFalse(manifest["humanApprovalClaimed"])
        self.assertFalse(manifest["trainingEligible"])
        self.assertEqual(
            {"Record": 95, "NonResult": 228},
            dict(Counter(row["disposition"] for row in manifest["rows"])),
        )
        with tempfile.TemporaryDirectory() as temporary:
            closure = Path(temporary) / "final-closure.json"
            builder.write_manifest(closure, manifest)
            result = subprocess.run(
                [
                    sys.executable,
                    str(VERIFIER),
                    "--inventory",
                    str(INVENTORY),
                    "--closure",
                    str(closure),
                    "--source-root",
                    str(REPOSITORY_ROOT),
                ],
                capture_output=True,
                check=False,
                text=True,
            )
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "323 candidates, 95 direct Record rows, 93 registered outcome types",
            result.stdout,
        )

    def test_unmapped_record_fails_closed(self) -> None:
        inventory = copy.deepcopy(self.inventory)
        record = next(
            item for item in inventory["definitions"]
            if item.get("inventory_scope") != "phase80-implementation-infrastructure"
            and item.get("coverage_disposition") == "Record"
        )
        record["base_type"] = "UnmappedPhase80Receipt"
        with self.assertRaisesRegex(builder.BuildError, "binding coverage mismatch"):
            builder.build_manifest(inventory, REPOSITORY_ROOT)

    def test_immutable_output_is_not_overwritten(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "closure.json"
            output.write_text("preserve me\n", encoding="utf-8")
            with self.assertRaisesRegex(builder.BuildError, "refusing to overwrite"):
                builder.write_manifest(output, {"rows": []})
            self.assertEqual("preserve me\n", output.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
