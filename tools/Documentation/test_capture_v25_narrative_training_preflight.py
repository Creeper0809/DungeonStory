"""Offline regression checks: no Editor, scene, or asset mutation."""
import json
import copy
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import capture_v25_narrative_training_preflight as preflight
import verify_v25_narrative_mechanic_handoff as handoff


class PreflightTests(unittest.TestCase):
    def test_nested_success(self):
        self.assertEqual(preflight.unwrap({"success": True, "data": {
            "success": True, "result": {"success": True, "result": {"passed": True}}}}), {"passed": True})

    def test_each_failure_layer_rejected(self):
        for value in ({"success": False}, {"success": True, "data": {"success": False}},
                      {"success": True, "data": {"result": {"success": False, "diagnostics": ["error"]}}}):
            with self.subTest(value=value), self.assertRaises(RuntimeError):
                preflight.unwrap(value)

    def test_string_result(self):
        self.assertEqual(preflight.unwrap({"success": True, "data": {"result": '{"status":"completed"}'}}),
                         {"status": "completed"})

    def test_incomplete_or_failed_job_is_not_success(self):
        for state in ("queued", "running", "failed", "cancelled"):
            with self.subTest(state=state), self.assertRaises(RuntimeError):
                preflight.unwrap({"success": True, "data": {"state": state, "result": None}})

    def test_version_cannot_escape_output_root(self):
        for value in ("../old", "F:/old", "", "a/b", "a\\b", "a" * 101):
            with self.subTest(value=value), self.assertRaises(RuntimeError):
                preflight.check_version(value)
        self.assertEqual(preflight.check_version("20260915-v26-r1"), "20260915-v26-r1")

    def test_csharp_literal_escapes(self):
        value = 'a"; throw new Exception(); //\\\n한글'
        self.assertEqual(json.loads(preflight.cs(value)), value)

    def test_canonical_unicode_order_and_int64(self):
        self.assertEqual(preflight.canonical({"z": [True, False, None], "a": "한글"}),
                         '{"a":"한글","z":[true,false,null]}'.encode("utf-8"))
        for value in (1.0, float("nan"), 2**63, -(2**63) - 1):
            with self.subTest(value=value), self.assertRaises(ValueError):
                preflight.canonical({"nested": [value]})

    def test_create_only_json(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "result.json"
            preflight.write_json(path, {"old": True})
            before = path.read_bytes()
            with self.assertRaises(FileExistsError):
                preflight.write_json(path, {"old": False})
            self.assertEqual(path.read_bytes(), before)

    def test_identity_drift_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            preflight.write_json(root / "catalog.json", {"trainingEligible": False})
            original = {"source": {"gameCommit": "current"}, "inputDigest": "digest", "uncommittedSourceHash": "dirty"}
            preflight.write_json(root / "manifest.json", original)
            baseline = dict(original, catalogBytesSha256=preflight.digest(root / "catalog.json"))
            preflight.verify_identity(root, baseline)
            for key, changed in (("source", {"gameCommit": "old"}), ("inputDigest", "stale"),
                                 ("uncommittedSourceHash", "changed"), ("catalogBytesSha256", "changed")):
                with self.subTest(key=key), self.assertRaises(RuntimeError):
                    preflight.verify_identity(root, dict(baseline, **{key: changed}))

    def test_failed_gate_saved_but_never_attached(self):
        with tempfile.TemporaryDirectory() as directory:
            capture = object.__new__(preflight.Capture)
            capture.qa = Path(directory)
            capture.identity = {"inputDigest": "digest", "catalogHash": "hash"}
            capture.gates = []
            with self.assertRaises(RuntimeError):
                capture.gate("failed-gate", {"passed": False})
            self.assertEqual(capture.gates, [])
            self.assertFalse(json.loads((capture.qa / "failed-gate.json").read_text())["passed"])

    def test_existing_export_rejected_before_editor_command(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(preflight.shutil, "which", return_value="unity"):
            project = Path(directory)
            (project / "Artifacts/Exports/NarrativeMechanicCatalog/existing").mkdir(parents=True)
            with self.assertRaisesRegex(RuntimeError, "Export already exists"):
                preflight.Capture(project, project, "existing")

    def test_focused_method_ids_unique(self):
        self.assertEqual(len(preflight.FOCUSED), 23)
        self.assertEqual(len(set(preflight.FOCUSED)), len(preflight.FOCUSED))

    def test_wait_timeout_resumes_same_job_without_resubmitting(self):
        capture = object.__new__(preflight.Capture)
        capture.cli, capture.project = "unity", Path("project")
        outputs = [
            {"success": True, "data": {"detached": True, "jobId": "same-job"}},
            {"success": False},
            {"success": True, "data": {"state": "running", "jobId": "same-job"}},
            {"success": True, "data": {"state": "completed", "result": {"success": True, "result": 7}}},
        ]
        with patch.object(capture, "raw_command", side_effect=outputs) as command:
            self.assertEqual(capture.command("eval", "return 7;"), 7)
        calls = [call.args[0] for call in command.call_args_list]
        self.assertEqual(sum("--detach" in call for call in calls), 1)
        self.assertEqual([call[3] for call in calls if call[1:3] == ["job", "wait"]], ["same-job", "same-job"])

    def negative_fixture(self):
        rows = [{"scenarioId": name} for name in sorted(handoff.EXPECTED_NEGATIVE_SCENARIO_IDS)]
        row = next(value for value in rows if value["scenarioId"] == handoff.STALE_COMBINATION_NEGATIVE_SCENARIO_ID)
        stale_id = "skill-combination:sha256:80a6b140ecc1f8fbb34490423c1d0772bc2d644f0a54e937ed7a433371eb19b5"
        reason = f"Unknown or illegal combination '{stale_id}' for rule 'rule'."
        row.update({"profileId": "CharacterSkill", "accepted": False, "failureReason": reason,
            "request": {"rules": [{"combinationOptions": [{"combinationId": "current-id"}]}]},
            "authorityContext": {"expectedAccepted": False, "actualAccepted": False,
                "validatedSkillCount": 0, "actualFailureReason": reason,
                "mutation": "stale-v17-need-changed-cleaning-small-stock-small-combination-id",
                "responseJson": json.dumps({"candidates": [{"combinationId": stale_id}]})}})
        return rows, row, stale_id

    def test_current_negative_inventory_and_historical_14_rejection(self):
        rows, _, _ = self.negative_fixture()
        handoff.validate_negative_inventory(rows)
        with self.assertRaises(RuntimeError):
            handoff.validate_negative_inventory(rows[:-1])
        duplicate = copy.deepcopy(rows)
        duplicate[0] = duplicate[1]
        with self.assertRaises(RuntimeError):
            handoff.validate_negative_inventory(duplicate)

    def test_stale_case_cannot_be_accepted_replaced_or_legal(self):
        for mutation in ("accepted", "wrong-id", "legal-option", "failure-reason"):
            rows, row, stale_id = self.negative_fixture()
            if mutation == "accepted":
                row["authorityContext"]["actualAccepted"] = True
            elif mutation == "wrong-id":
                row["authorityContext"]["responseJson"] = '{"candidates":[{"combinationId":"unrelated"}]}'
            elif mutation == "legal-option":
                row["request"]["rules"][0]["combinationOptions"][0]["combinationId"] = stale_id
            else:
                row["failureReason"] = "unrelated failure"
            with self.subTest(mutation=mutation), self.assertRaises(RuntimeError):
                handoff.validate_negative_inventory(rows)


if __name__ == "__main__":
    unittest.main()
