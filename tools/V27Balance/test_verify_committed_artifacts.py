"""Regression tests for portable V27 evidence binding; no Unity data generation."""

import hashlib
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import verify_committed_artifacts as verifier


class TemporaryEvidenceTestCase(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.scene = self.root / "Assets/Scenes/GameplayScene.unity"
        self.scene.parent.mkdir(parents=True)
        self.scene.write_bytes(b"%YAML 1.1\nscene: current\n")
        self.digest = hashlib.sha256(self.scene.read_bytes()).hexdigest()
        self.root_patch = patch.object(verifier, "ROOT", self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)
        self.pin_patch = patch.object(verifier, "REVIEWED_GAMEPLAY_SCENE_SHA256", self.digest)
        self.pin_patch.start()
        self.addCleanup(self.pin_patch.stop)

class GameplaySceneBindingTests(TemporaryEvidenceTestCase):
    def test_regenerated_evidence_does_not_approve_an_unreviewed_scene(self):
        self.scene.write_bytes(b"%YAML 1.1\nscene: unreviewed\n")
        changed = hashlib.sha256(self.scene.read_bytes()).hexdigest()
        with self.assertRaisesRegex(RuntimeError, "not been reviewed"):
            verifier.require_current_gameplay_scene(changed, "new receipt")

    def test_current_scene_with_matching_execution_evidence_is_accepted(self):
        self.assertEqual(
            self.digest,
            verifier.require_current_gameplay_scene(self.digest, "test receipt"),
        )

    def test_scene_edit_invalidates_previous_evidence(self):
        self.scene.write_bytes(b"%YAML 1.1\nscene: changed\n")
        with self.assertRaisesRegex(RuntimeError, "scene binding is stale"):
            verifier.require_current_gameplay_scene(self.digest, "test receipt")

    def test_evidence_from_another_scene_is_rejected(self):
        other = hashlib.sha256(b"another scene").hexdigest()
        with self.assertRaisesRegex(RuntimeError, "scene binding is stale"):
            verifier.require_current_gameplay_scene(other, "test receipt")

    def test_missing_scene_does_not_use_a_historical_digest(self):
        self.scene.unlink()
        with self.assertRaisesRegex(RuntimeError, "does not resolve to a file"):
            verifier.require_current_gameplay_scene(self.digest, "test receipt")

    def test_missing_or_noncanonical_evidence_is_rejected(self):
        for value in (None, "", self.digest.upper(), " " + self.digest, "0" * 63):
            with self.subTest(value=value), self.assertRaises(RuntimeError):
                verifier.require_current_gameplay_scene(value, "test receipt")


class OutputCapacityReceiptTests(TemporaryEvidenceTestCase):
    def setUp(self):
        super().setUp()
        self.source = "a" * 64
        self.qa = self.root / "Artifacts/QA"
        self.qa.mkdir(parents=True)
        self.aggregate = self.qa / "v27-balance-output-capacity-playmode.txt"
        self.children = {}
        for key, filename, marker in (
            ("worldResource", "world-resource", "PASS WORLD_RESOURCE_EXACT_SOURCE_ATOMIC_PUBLICATION conserved=true"),
            ("cropPlot", "crop-plot", "PASS CROP_OUTPUT_FACILITY_BUFFER_WAIT_RESTORE_RETRY_EXACT_ONCE capacityWait=true;frozenRestore=true;replayDelta=0;executionReceipt=true;physicalBatchJoin=true;terminalRestore=true"),
        ):
            path = self.root / f"docs/implementation-reports/{filename}-runtime-latest.txt"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(f"{marker}\ncurrentSourceDigest={self.source}\ngameplaySceneSha256={self.digest}\nvalid=true\n", encoding="utf-8")
            self.children[key] = path
        for name, value in (("QA", self.qa), ("current_source_snapshot", lambda: (self.source, 1, "b" * 64))):
            patcher = patch.object(verifier, name, value)
            patcher.start()
            self.addCleanup(patcher.stop)
        self.bind_aggregate()

    def bind_aggregate(self):
        lines = ["RESULT=PASS; checks=2; failures=0", f"currentSourceDigest={self.source}", f"gameplaySceneSha256={self.digest}"]
        for key, path in self.children.items():
            lines += [f"{key}ReportPath={path.relative_to(self.root).as_posix()}", f"{key}ReportSha256={hashlib.sha256(path.read_bytes()).hexdigest()}"]
        self.aggregate.write_text("\n".join(lines) + "\n", encoding="utf-8")

    def test_current_runtime_receipts_are_accepted(self):
        verifier.verify_output_capacity_receipts()

    def test_new_timestamp_does_not_refresh_stale_runtime_identity(self):
        child = self.children["worldResource"]
        child.write_text(child.read_text().replace(self.source, "c" * 64))
        os.utime(child, (2_000_000_000, 2_000_000_000))
        self.bind_aggregate()
        with self.assertRaisesRegex(RuntimeError, "runtime identity is stale"):
            verifier.verify_output_capacity_receipts()

    def test_changed_child_bytes_invalidate_the_aggregate(self):
        with self.children["cropPlot"].open("a") as stream:
            stream.write("unreviewed edit\n")
        with self.assertRaisesRegex(RuntimeError, "receipt byte hash differs"):
            verifier.verify_output_capacity_receipts()

    def test_missing_or_duplicate_source_field_is_rejected(self):
        child = self.children["worldResource"]
        original = child.read_text()
        for replacement in ("", f"currentSourceDigest={self.source}\n" * 2):
            with self.subTest(replacement=replacement):
                child.write_text(original.replace(f"currentSourceDigest={self.source}\n", replacement))
                self.bind_aggregate()
                with self.assertRaises(RuntimeError):
                    verifier.verify_output_capacity_receipts()

    def test_missing_conservation_marker_is_rejected(self):
        child = self.children["cropPlot"]
        child.write_text(child.read_text().replace("frozenRestore=true", "frozenRestore=false"))
        self.bind_aggregate()
        with self.assertRaisesRegex(RuntimeError, "conservation markers"):
            verifier.verify_output_capacity_receipts()


if __name__ == "__main__":
    unittest.main()
