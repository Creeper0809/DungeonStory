import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import verify_change_scope as gate


class SelectionTests(unittest.TestCase):
    def test_docs_do_not_invalidate_runtime(self):
        self.assertEqual(set(), gate.required_groups(["docs/dev/unity-workflow.md", "tools/V27Balance/README.md"]))

    def test_domain_change_retains_world_and_core(self):
        self.assertEqual({"core", "world", "effects"}, gate.required_groups([
            "Assets/Scripts/Content/GameDomainContentCatalogSO.cs"]))

    def test_unknown_shared_deleted_and_policy_inputs_are_conservative(self):
        for path in ("Assets/Scripts/Foundation/Save.cs", "Packages/manifest.json",
                     "Assets/Resources/crops.asset", "tools/V27Balance/verify_change_scope.py",
                     ".github/workflows/v27-ledger-integrity.yml", *gate.PRODUCERS,
                     "Assets/Scripts/Content/Content.asmdef", "Assets/Scripts/Content/Content.asmdef.meta",
                     "Assets/Scripts/Content/Content.asmref.meta"):
            with self.subTest(path=path):
                self.assertEqual(set(gate.GROUPS), gate.required_groups([path]))

    def test_changed_archive_requires_strict_certification(self):
        self.assertTrue(gate.archive_changed([
            "Artifacts/QA/v27-balance-artifact-manifest.json",
            "Artifacts/QA/v27-balance-before-after.csv"]))
        self.assertFalse(gate.archive_changed([gate.EVIDENCE]))
        self.assertTrue(gate.archive_changed(["docs/implementation-reports/crop-plot-runtime-latest.txt"]))

    def test_rename_is_both_deleted_and_added_paths(self):
        with patch.object(gate.subprocess, "check_output", side_effect=[b"ancestor\n", b"Assets/Old.cs\0docs/Old.cs\0"]) as run:
            self.assertEqual(["Assets/Old.cs", "docs/Old.cs"], gate.changed_paths("base", "head"))
            self.assertIn("--no-renames", run.call_args.args[0])
            self.assertIn("ancestor", run.call_args.args[0])
            self.assertEqual(set(gate.GROUPS), gate.required_groups(["Assets/Old.cs", "docs/Old.cs"]))


class ExecutionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.folder = self.root / "Artifacts/QA/ImpactCI"
        self.folder.mkdir(parents=True)
        self.log = self.folder / "core.log"
        self.summary = self.folder / "summary.json"
        self.log.write_text("IMPACT_CI_PASS group=core;source=abc;config=def;consoleErrors=0\n", encoding="utf-8")
        self.summary_data = dict(exit_code=0, stop_reason=None, compiler_error_count=0, source_drift=[], seconds=20)
        self.summary.write_text(json.dumps(self.summary_data), encoding="utf-8")

    def check(self):
        result = dict(group="core", method=gate.GROUPS["core"])
        for key, path in (("log", self.log), ("summary", self.summary)):
            result[key] = path.relative_to(self.root).as_posix()
            result[key + "Sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
        return result

    def test_complete_actual_execution_accepted(self):
        self.assertEqual("core", gate.verify_execution(self.check(), "abc", "def", self.root))

    def test_stale_or_duplicate_success_is_rejected(self):
        check = self.check()
        for source, config in (("changed", "def"), ("abc", "changed")):
            with self.assertRaises(RuntimeError):
                gate.verify_execution(check, source, config, self.root)
        self.log.write_text(self.log.read_text() * 2)
        with self.assertRaises(RuntimeError):
            gate.verify_execution(self.check(), "abc", "def", self.root)

    def test_failed_interrupted_drift_and_compile_error_rejected_even_with_pass_marker(self):
        for field, value in (("exit_code", 1), ("stop_reason", "signal"),
                             ("compiler_error_count", 1), ("source_drift", ["changed"]), ("seconds", 0)):
            with self.subTest(field=field):
                data = dict(self.summary_data, **{field: value})
                self.summary.write_text(json.dumps(data))
                with self.assertRaises(RuntimeError):
                    gate.verify_execution(self.check(), "abc", "def", self.root)

    def test_mutated_log_or_missing_summary_rejected(self):
        check = self.check()
        self.log.write_text("replacement")
        with self.assertRaises(RuntimeError):
            gate.verify_execution(check, "abc", "def", self.root)
        self.summary.unlink()
        with self.assertRaises(RuntimeError):
            gate.verify_execution(check, "abc", "def", self.root)

    def test_console_error_after_completion_marker_rejected(self):
        original = self.log.read_text()
        for error in ("UnityEngine.Debug:LogError (object)", "NullReferenceException: callback failed",
                      "System.InvalidOperationException: cleanup failed"):
            self.log.write_text(original + error + "\n")
            with self.subTest(error=error), self.assertRaisesRegex(RuntimeError, "console failure"):
                gate.verify_execution(self.check(), "abc", "def", self.root)

    def test_missing_cleanup_field_rejected(self):
        data = dict(self.summary_data)
        del data["stop_reason"]
        self.summary.write_text(json.dumps(data))
        with self.assertRaisesRegex(RuntimeError, "cleanup failed"):
            gate.verify_execution(self.check(), "abc", "def", self.root)

    def test_missing_or_duplicate_group_rejected(self):
        receipt = dict(schema="impact-ci-v1", scope="targeted-contracts-not-full-balance-certification",
                       source=dict(digest="abc", count=1, pathsDigest="paths"), configurationDigest="def", checks=[])
        with patch.object(gate.ledger, "load_json_strict", return_value=receipt), \
             patch.object(gate.ledger, "current_source_snapshot", return_value=("abc", 1, "paths")), \
             patch.object(gate, "configuration_snapshot", return_value="def"):
            with self.assertRaisesRegex(RuntimeError, "missing affected"):
                gate.verify_receipt({"core"})
            receipt["checks"] = [self.check(), copy.deepcopy(self.check())]
            with patch.object(gate, "verify_execution", return_value="core"):
                with self.assertRaisesRegex(RuntimeError, "duplicate targeted"):
                    gate.verify_receipt({"core"})


class ConfigurationTests(unittest.TestCase):
    def test_configuration_identity_normalizes_checkout_newlines_but_detects_mutation_and_deletion(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "Assets").mkdir()
            (root / "ProjectSettings").mkdir()
            (root / "Packages").mkdir()
            asset = root / "Assets/Test.asset"
            asset.write_bytes(b"data: 1\n")
            expected = gate.configuration_snapshot(root)
            asset.write_bytes(b"\xef\xbb\xbfdata: 1\r\n")
            self.assertEqual(expected, gate.configuration_snapshot(root))
            asset.write_bytes(b"data: 2\n")
            self.assertNotEqual(expected, gate.configuration_snapshot(root))
            asset.unlink()
            self.assertNotEqual(expected, gate.configuration_snapshot(root))
            for name in ("LICENSE", "data.XML"):
                path = root / "Assets" / name
                path.write_bytes(b"text\n")
                expected = gate.configuration_snapshot(root)
                path.write_bytes(b"text\r\n")
                self.assertEqual(expected, gate.configuration_snapshot(root))

    def test_runtime_json_and_binary_asset_changes_invalidate_execution(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for folder in ("Assets", "Packages", "ProjectSettings"):
                (root / folder).mkdir()
            for name, before, after in (("profiles.json", b'{"value":1}\n', b'{"value":2}\n'),
                                        ("imported.bin", b'\x00\xff', b'\x00\xfe')):
                path = root / "Assets" / name
                path.write_bytes(before)
                digest = gate.configuration_snapshot(root)
                path.write_bytes(after)
                self.assertNotEqual(digest, gate.configuration_snapshot(root))


if __name__ == "__main__":
    unittest.main()
