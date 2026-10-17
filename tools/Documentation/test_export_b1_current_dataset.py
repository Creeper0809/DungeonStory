import copy
import argparse
import importlib.util
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("export_b1_current_dataset.py")
SPEC = importlib.util.spec_from_file_location("export_b1_current_dataset", MODULE_PATH)
exporter = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(exporter)

MATRIX_SPEC = importlib.util.spec_from_file_location(
    "matrix_export_contract_fixture", MODULE_PATH.with_name("build_b1_completion_matrix.py"))
matrix_contract = importlib.util.module_from_spec(MATRIX_SPEC)
assert MATRIX_SPEC.loader is not None
MATRIX_SPEC.loader.exec_module(matrix_contract)


class LimitedDatasetExporterTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "Assets").mkdir()
        (self.root / "Artifacts" / "QA").mkdir(parents=True)
        self.matrix_path = self.root / "Artifacts" / "QA" / "matrix.json"
        (self.root / "Assets" / "Test.cs").write_text(
            "sealed class Test {}\n", encoding="utf-8")
        self.matrix = self.make_matrix("W04", "", "1" * 40, {
            "repair": {"before": 1, "after": 2},
            "replay": {"result": "AlreadyApplied"},
        })
        self.write_matrix(self.matrix)

    def tearDown(self):
        self.temp.cleanup()

    def write_matrix(self, value):
        self.matrix_path.write_bytes(matrix_contract.canonical_json(value) + b"\n")

    def write_fixture(self, name, value):
        path = self.root / "Artifacts" / "QA" / name
        raw = matrix_contract.canonical_json(value) + b"\n"
        path.write_bytes(raw)
        return exporter.descriptor(path, path.relative_to(self.root).as_posix())

    def make_matrix(self, current_row_id, suffix, commit, exports):
        """A complete sealed test contract, never gameplay or training evidence."""
        source_path = self.root / "Assets" / "Test.cs"
        source = {"path": "Assets/Test.cs", "length": source_path.stat().st_size,
                  "sha256": exporter.sha256_bytes(source_path.read_bytes())}
        inventory = {
            "schemaVersion": 1, "files": [source], "gameCommit": commit,
            "dirtySourceSha256": matrix_contract.descriptor_root([source]),
            "contentSha256": matrix_contract.descriptor_root([]),
        }
        inventory_descriptor = self.write_fixture(f"inventory{suffix}.json", inventory)
        row_ids = ["W04", "W05"] + [f"R{index:02d}" for index in range(2, 91)]
        draft = {"rows": [{
            "rowId": row_id, "requirement": f"test contract {row_id}",
            "entryPoints": ["Entry"], "requiredBranches": ["normal", "restore"],
            "evidenceRefs": [], "runnerSteps": [], "rationale": "isolated unit fixture",
            "b0Priority": False,
        } for row_id in row_ids]}
        draft_descriptor = self.write_fixture(f"draft{suffix}.json", draft)
        report = {"passed": True, "exports": exports, "fullAuditClosure": False,
                  "humanApprovalClaimed": False, "trainingEligible": False}
        report_descriptor = self.write_fixture(f"report{suffix}.json", report)
        execution = {"report": report_descriptor, "unityExit": 0,
                     "noninteractiveIsolation": True, "compileErrorLines": 0,
                     "compileWarningLines": 0}
        for key, name, raw in (
            ("unityLog", "unity", b"UNIT_FIXTURE_NOT_UNITY_EXECUTION\n"),
            ("isolationProbe", "probe", b"UNIT_FIXTURE_NOT_ISOLATION_PROBE\n"),
            ("launcherExit", "exit", b"UNIT_FIXTURE_EXIT=0\n"),
        ):
            path = self.root / "Artifacts" / "QA" / f"{name}{suffix}.txt"
            path.write_bytes(raw)
            execution[key] = exporter.descriptor(path, path.relative_to(self.root).as_posix())
        current = {
            "schema": matrix_contract.CURRENT_EVIDENCE_SCHEMA,
            "rowId": current_row_id, "status": "CURRENT_FOCUSED_EVIDENCE_COMPLETE",
            "sourceIdentity": {
                "inventory": inventory_descriptor, "gameCommit": commit,
                "dirtySourceSha256": inventory["dirtySourceSha256"],
                "contentSha256": inventory["contentSha256"],
                "verifiedCurrentBeforeExecution": True, "verifiedCurrentAfterExecution": True,
            },
            "sourceFiles": [source], "execution": execution,
            "branches": [{"branch": name, "state": "covered-by-current-focused",
                          "assertions": ["unit fixture only"]} for name in ("normal", "restore")],
            "exportExamples": [{"source": report_descriptor["path"] + "#/exports/" + name,
                                "value": value} for name, value in exports.items()],
            "rowCurrentEvidenceComplete": True, "fullAuditClosure": False,
            "humanApprovalClaimed": False, "trainingEligible": False,
        }
        current_descriptor = self.write_fixture(f"current{suffix}.json", current)
        return matrix_contract.build_matrix(
            self.root, draft, draft_descriptor, inventory, inventory_descriptor,
            [], [], [(current, current_descriptor)])

    def test_create_and_verify_exports_only_current_rows(self):
        result = exporter.build_package(
            self.root, self.matrix_path, Path("Artifacts/QA/export-r1"))
        self.assertEqual(1, result["rows"])
        self.assertEqual(2, result["samples"])
        verified = exporter.verify_package(
            self.root, Path("Artifacts/QA/export-r1"))
        self.assertEqual("LIMITED_DATASET_VALID", verified["status"])
        self.assertEqual(2, verified["samples"])
        lines = (self.root / "Artifacts" / "QA" / "export-r1"
                 / exporter.DATASET_FILE).read_text(encoding="utf-8").splitlines()
        self.assertTrue(all('"rowId":"W04"' in line for line in lines))
        self.assertTrue(all('"trainingEligible":false' in line for line in lines))

    def test_reused_sample_preserves_original_execution_identity_separately(self):
        row = copy.deepcopy(self.matrix["rows"][0])
        original = {"inventory": {"path": "Artifacts/QA/old-inventory.json"},
                    "dirtySourceSha256": "sha256:" + "a" * 64}
        row["bindingMode"] = "historical-non-impact"
        row["executionSourceIdentity"] = original
        samples = exporter.build_samples(self.matrix, [row])
        for sample in samples:
            provenance = sample["provenance"]
            self.assertEqual("historical-non-impact", provenance["sourceBindingMode"])
            self.assertEqual(original, provenance["executionSourceIdentity"])
            self.assertEqual(self.matrix["sourceIdentity"]["inventory"],
                             provenance["inventory"])
            self.assertNotEqual(original["inventory"], provenance["inventory"])
            self.assertFalse(sample["claims"]["trainingEligible"])

    def test_reviewed_domain_export_keeps_original_execution_identity(self):
        row = copy.deepcopy(self.matrix["rows"][0])
        original = {"inventory": {"path": "Artifacts/QA/original-inventory.json"},
                    "dirtySourceSha256": "sha256:" + "b" * 64}
        row["executionSourceIdentity"] = original
        for mode in ("reviewed-domain-non-impact", "reviewed-editor-report-only"):
            with self.subTest(mode=mode):
                row["bindingMode"] = mode
                samples = exporter.build_samples(self.matrix, [row])
                for sample in samples:
                    self.assertEqual(original, sample["provenance"]["executionSourceIdentity"])
                    self.assertEqual(mode, sample["provenance"]["sourceBindingMode"])
                    self.assertNotEqual(original["inventory"], sample["provenance"]["inventory"])
                    self.assertFalse(sample["claims"]["trainingEligible"])

    def test_refuses_training_eligible_matrix(self):
        changed = copy.deepcopy(self.matrix)
        changed["trainingEligible"] = True
        self.write_matrix(changed)
        with self.assertRaisesRegex(exporter.ExportError, "training eligibility"):
            exporter.build_package(
                self.root, self.matrix_path, Path("Artifacts/QA/export-r2"))

    def test_refuses_matrix_without_current_rows(self):
        changed = copy.deepcopy(self.matrix)
        changed["rows"][0]["disposition"] = "implemented-evidence-gap"
        changed["counts"]["currentEvidenceComplete"] = 0
        self.write_matrix(changed)
        with self.assertRaisesRegex(exporter.ExportError, "no current-complete"):
            exporter.build_package(
                self.root, self.matrix_path, Path("Artifacts/QA/export-r3"))

    def test_create_only_refuses_existing_output(self):
        output = self.root / "Artifacts" / "QA" / "export-r4"
        output.mkdir()
        with self.assertRaisesRegex(exporter.ExportError, "already exists"):
            exporter.build_package(self.root, self.matrix_path, output)

    def test_combines_disjoint_matrices_and_preserves_matrix_provenance(self):
        second = self.make_matrix("W05", "-2", "5" * 40, {"facility": {"completed": True}})
        second_path = self.root / "Artifacts" / "QA" / "matrix-2.json"
        second_path.write_bytes(matrix_contract.canonical_json(second) + b"\n")

        result = exporter.build_bundle_package(
            self.root,
            [self.matrix_path, second_path],
            Path("Artifacts/QA/export-bundle-r1"),
        )
        self.assertEqual(2, result["rows"])
        self.assertEqual(3, result["samples"])
        verified = exporter.verify_package(
            self.root, Path("Artifacts/QA/export-bundle-r1"))
        self.assertEqual("LIMITED_DATASET_VALID", verified["status"])
        manifest = exporter.strict_json((
            self.root / "Artifacts" / "QA" / "export-bundle-r1"
            / exporter.MANIFEST_FILE).read_bytes())
        self.assertEqual(2, manifest["matrixCount"])
        samples = [exporter.strict_json(line) for line in (
            self.root / "Artifacts" / "QA" / "export-bundle-r1"
            / exporter.DATASET_FILE).read_bytes().splitlines()]
        self.assertEqual(
            {"Artifacts/QA/matrix.json", "Artifacts/QA/matrix-2.json"},
            {sample["provenance"]["matrix"]["path"] for sample in samples},
        )

    def test_refuses_duplicate_current_row_across_matrices(self):
        duplicate_path = self.root / "Artifacts" / "QA" / "matrix-duplicate.json"
        duplicate_path.write_bytes(matrix_contract.canonical_json(self.matrix) + b"\n")
        with self.assertRaisesRegex(exporter.ExportError, "multiple matrices: W04"):
            exporter.build_bundle_package(
                self.root,
                [self.matrix_path, duplicate_path],
                Path("Artifacts/QA/export-bundle-r2"),
            )

    def test_sealed_fixture_passes_existing_matrix_verifier(self):
        result = matrix_contract.verify(argparse.Namespace(
            project_root=self.root, matrix=self.matrix_path,
            inventory=Path(self.matrix["sourceIdentity"]["inventory"]["path"]),
            require_current_source=False))
        self.assertEqual("NOT_REQUESTED", result["inventoryCurrentCheck"])

    def test_refuses_example_forged_inside_matrix(self):
        forged = copy.deepcopy(self.matrix)
        forged["rows"][0]["exportExamples"][0]["value"]["after"] = 999
        self.write_matrix(forged)
        with self.assertRaisesRegex(exporter.ExportError, "matrix evidence validation failed"):
            exporter.build_package(self.root, self.matrix_path, Path("Artifacts/QA/forged"))
        self.assertFalse((self.root / "Artifacts" / "QA" / "forged").exists())

    def test_refuses_mutated_report_after_export(self):
        exporter.build_package(self.root, self.matrix_path, Path("Artifacts/QA/sealed"))
        path = self.root / "Artifacts" / "QA" / "report.json"
        report = exporter.strict_json(path.read_bytes())
        report["passed"] = False
        path.write_bytes(exporter.pretty_json(report))
        with self.assertRaisesRegex(exporter.ExportError, "matrix evidence validation failed"):
            exporter.verify_package(self.root, Path("Artifacts/QA/sealed"))

    def test_refuses_rehashed_dataset_with_forged_value(self):
        output = Path("Artifacts/QA/rehashed")
        exporter.build_package(self.root, self.matrix_path, output)
        dataset = self.root / output / exporter.DATASET_FILE
        samples = [exporter.strict_json(line) for line in dataset.read_bytes().splitlines()]
        samples[0]["evidence"]["after"] = 999
        raw = b"".join(exporter.canonical_json(value) + b"\n" for value in samples)
        dataset.write_bytes(raw)
        manifest_path = self.root / output / exporter.MANIFEST_FILE
        manifest = exporter.strict_json(manifest_path.read_bytes())
        manifest["dataset"]["bytes"] = len(raw)
        manifest["dataset"]["sha256"] = exporter.sha256_bytes(raw)
        manifest_path.write_bytes(exporter.pretty_json(manifest))
        with self.assertRaisesRegex(exporter.ExportError, "dataset differs from sealed matrix examples"):
            exporter.verify_package(self.root, output)

    def test_snapshot_remains_historical_and_current_mode_rejects_live_drift(self):
        output = Path("Artifacts/QA/archive")
        exporter.build_package(self.root, self.matrix_path, output)
        (self.root / "Assets" / "Test.cs").write_text(
            "sealed class Test { public int Changed; }\n", encoding="utf-8")
        historical = exporter.verify_package(self.root, output)
        self.assertEqual("snapshot-integrity", historical["matrixVerificationMode"])
        self.assertFalse(historical["trainingEligible"])
        with self.assertRaisesRegex(exporter.ExportError, "current file length differs"):
            exporter.verify_package(self.root, output, require_current_source=True)
        with self.assertRaisesRegex(exporter.ExportError, "current file length differs"):
            exporter.build_package(self.root, self.matrix_path,
                                   Path("Artifacts/QA/current"), require_current_source=True)
        self.assertFalse((self.root / "Artifacts" / "QA" / "current").exists())

    def test_two_exports_of_one_snapshot_are_byte_deterministic(self):
        first = Path("Artifacts/QA/deterministic-1")
        second = Path("Artifacts/QA/deterministic-2")
        exporter.build_package(self.root, self.matrix_path, first)
        exporter.build_package(self.root, self.matrix_path, second)
        for name in (exporter.DATASET_FILE, exporter.MANIFEST_FILE):
            self.assertEqual((self.root / first / name).read_bytes(),
                             (self.root / second / name).read_bytes())


if __name__ == "__main__":
    unittest.main()
