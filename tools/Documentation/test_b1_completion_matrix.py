import base64
import copy
import hashlib
import importlib.util
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace


MODULE_PATH = Path(__file__).with_name("build_b1_completion_matrix.py")
SPEC = importlib.util.spec_from_file_location("build_b1_completion_matrix", MODULE_PATH)
matrix = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(matrix)


def digest(raw: bytes) -> str:
    return "sha256:" + hashlib.sha256(raw).hexdigest()


class CompletionMatrixTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "Assets" / "Editor").mkdir(parents=True)
        (self.root / "Assets" / "Runtime").mkdir()
        (self.root / "Artifacts" / "QA").mkdir(parents=True)
        self.owned_raw = b"sealed class Test {}\n"
        self.old_editor_raw = (
            b"internal static class OtherDebug { public static void Run() {} }\n"
        )
        self.current_editor_raw = (
            b"internal static class OtherDebug { public static void Run() { "
            b"System.Console.WriteLine(\"current\"); } }\n"
        )
        self.stable_runtime_raw = b"sealed class Stable {}\n"
        self.stable_content_raw = b"stable-content\n"
        for relative, raw in {
            "Assets/Test.cs": self.owned_raw,
            "Assets/Editor/OtherDebug.cs": self.current_editor_raw,
            "Assets/Runtime/Stable.cs": self.stable_runtime_raw,
            "Assets/Stable.asset": self.stable_content_raw,
        }.items():
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)

        shared_descriptors = [
            self.source_descriptor("Assets/Test.cs", self.owned_raw),
            self.source_descriptor("Assets/Runtime/Stable.cs", self.stable_runtime_raw),
            self.source_descriptor("Assets/Stable.asset", self.stable_content_raw),
        ]
        self.old_inventory, self.old_inventory_descriptor = self.seal_inventory(
            [
                *shared_descriptors,
                self.source_descriptor(
                    "Assets/Editor/OtherDebug.cs",
                    self.old_editor_raw,
                ),
            ],
            "Artifacts/QA/inventory-old.json",
        )
        self.inventory, self.inventory_descriptor = self.seal_inventory(
            [
                *shared_descriptors,
                self.source_descriptor(
                    "Assets/Editor/OtherDebug.cs",
                    self.current_editor_raw,
                ),
            ],
            "Artifacts/QA/inventory.json",
        )
        self.draft = {
            "rows": [
                {
                    "rowId": f"R{index:02d}",
                    "requirement": f"requirement {index}",
                    "entryPoints": ["Entry"],
                    "requiredBranches": [f"branch-{index}"],
                    "evidenceRefs": [],
                    "runnerSteps": [f"step-{index}"],
                    "rationale": "fixture",
                    "b0Priority": False,
                }
                for index in range(91)
            ]
        }
        self.draft_descriptor = self.seal_json(
            "Artifacts/QA/draft.json",
            self.draft,
        )

    def tearDown(self):
        self.temp.cleanup()

    @staticmethod
    def source_descriptor(path, raw):
        return {
            "path": path,
            "length": len(raw),
            "sha256": digest(raw),
        }

    def seal_json(self, relative, value):
        path = self.root / relative
        raw = matrix.canonical_json(value) + b"\n"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        return {
            "path": relative,
            "bytes": len(raw),
            "sha256": digest(raw),
        }

    def seal_inventory(self, descriptors, relative):
        files = sorted(copy.deepcopy(descriptors), key=lambda item: item["path"])
        sources = [
            item
            for item in files
            if Path(item["path"]).suffix.lower() in matrix.SOURCE_SUFFIXES
        ]
        content = [item for item in files if item not in sources]
        inventory = {
            "schemaVersion": 1,
            "files": files,
            "gameCommit": "1" * 40,
            "dirtySourceSha256": matrix.descriptor_root(sources),
            "contentSha256": matrix.descriptor_root(content),
        }
        return inventory, self.seal_json(relative, inventory)

    def changed_inventory(self, replacements, filename):
        descriptors = {
            item["path"]: copy.deepcopy(item)
            for item in self.inventory["files"]
        }
        for relative, raw in replacements.items():
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            descriptors[relative] = self.source_descriptor(relative, raw)
        return self.seal_inventory(
            list(descriptors.values()),
            f"Artifacts/QA/{filename}",
        )

    def build(self, current_evidence=None, *, inventory=None, inventory_descriptor=None):
        inventory = self.inventory if inventory is None else inventory
        inventory_descriptor = (
            self.inventory_descriptor
            if inventory_descriptor is None
            else inventory_descriptor
        )
        routing = {
            "rows": [{
                "rowId": "R00",
                "requiredBranches": [{"branch": "branch-0", "coverage": "static-covered"}],
            }]
        }
        supplement = {"rowId": "R01", "branch": "branch-1", "status": "PASS"}
        route_descriptor = self.seal_json(
            "Artifacts/QA/routing.json",
            routing,
        )
        supplement_descriptor = self.seal_json(
            "Artifacts/QA/supplement.json",
            supplement,
        )
        return matrix.build_matrix(
            self.root,
            self.draft,
            self.draft_descriptor,
            inventory,
            inventory_descriptor,
            [(routing, route_descriptor)],
            [(supplement, supplement_descriptor)],
            current_evidence or [],
        )

    def current_evidence(
        self,
        *,
        inventory=None,
        inventory_descriptor=None,
        artifact_prefix="",
        evidence_name="current-evidence.json",
    ):
        inventory = self.inventory if inventory is None else inventory
        inventory_descriptor = (
            self.inventory_descriptor
            if inventory_descriptor is None
            else inventory_descriptor
        )
        report = {
            "passed": True,
            "example": {"owner": "owner:fixture", "revision": 1},
            "fullAuditClosure": False,
            "trainingEligible": False,
            "humanApprovalClaimed": False,
        }
        artifacts = {}
        for name, raw in {
            f"{artifact_prefix}report.json": matrix.canonical_json(report) + b"\n",
            f"{artifact_prefix}unity.log": b"PASS\n",
            f"{artifact_prefix}isolation-probe.txt": b"PASS_NONINTERACTIVE_PROBE\n",
            f"{artifact_prefix}launcher-exit.txt": b"UNITY_EXIT_CODE=0\n",
        }.items():
            path = self.root / "Artifacts" / "QA" / name
            path.write_bytes(raw)
            artifacts[name] = {
                "path": path.relative_to(self.root).as_posix(),
                "bytes": len(raw),
                "sha256": digest(raw),
            }
        document = {
            "schema": matrix.CURRENT_EVIDENCE_SCHEMA,
            "rowId": "R00",
            "status": "CURRENT_FOCUSED_EVIDENCE_COMPLETE",
            "sourceIdentity": {
                "inventory": inventory_descriptor,
                "gameCommit": inventory["gameCommit"],
                "dirtySourceSha256": inventory["dirtySourceSha256"],
                "contentSha256": inventory["contentSha256"],
                "verifiedCurrentBeforeExecution": True,
                "verifiedCurrentAfterExecution": True,
            },
            "sourceFiles": [
                copy.deepcopy(next(
                    item for item in inventory["files"]
                    if item["path"] == "Assets/Test.cs"
                ))
            ],
            "execution": {
                "report": artifacts[f"{artifact_prefix}report.json"],
                "unityLog": artifacts[f"{artifact_prefix}unity.log"],
                "isolationProbe": artifacts[f"{artifact_prefix}isolation-probe.txt"],
                "launcherExit": artifacts[f"{artifact_prefix}launcher-exit.txt"],
                "unityExit": 0,
                "noninteractiveIsolation": True,
                "compileErrorLines": 0,
                "compileWarningLines": 0,
            },
            "branches": [{
                "branch": "branch-0",
                "state": "covered-by-current-focused",
                "assertions": ["fixture passed"],
            }],
            "exportExamples": [{
                "kind": "fixture",
                "source": artifacts[f"{artifact_prefix}report.json"]["path"] + "#/example",
                "value": report["example"],
            }],
            "rowCurrentEvidenceComplete": True,
            "fullAuditClosure": False,
            "trainingEligible": False,
            "humanApprovalClaimed": False,
        }
        descriptor = self.seal_json(f"Artifacts/QA/{evidence_name}", document)
        return document, descriptor

    def reused_evidence(
        self,
        *,
        inventory=None,
        inventory_descriptor=None,
        original=None,
        original_descriptor=None,
        evidence_name="reused-evidence.json",
    ):
        inventory = self.inventory if inventory is None else inventory
        inventory_descriptor = (
            self.inventory_descriptor
            if inventory_descriptor is None
            else inventory_descriptor
        )
        if original is None or original_descriptor is None:
            original, original_descriptor = self.current_evidence(
                inventory=self.old_inventory,
                inventory_descriptor=self.old_inventory_descriptor,
                artifact_prefix="original-",
                evidence_name="original-current-evidence.json",
            )
        old_by_path = {
            item["path"]: item for item in self.old_inventory["files"]
        }
        current_by_path = {
            item["path"]: item for item in inventory["files"]
        }
        editor_path = "Assets/Editor/OtherDebug.cs"
        current_raw = (self.root / editor_path).read_bytes()
        document = {
            "schema": matrix.REUSED_EVIDENCE_SCHEMA,
            "rowId": original["rowId"],
            "bindingMode": "historical-non-impact",
            "status": "CURRENT_REUSED_EVIDENCE_COMPLETE",
            "sourceIdentity": copy.deepcopy(original["sourceIdentity"]),
            "sourceFiles": copy.deepcopy(original["sourceFiles"]),
            "execution": copy.deepcopy(original["execution"]),
            "branches": [
                {
                    "branch": branch["branch"],
                    "state": "covered-by-current-reuse",
                    "assertions": copy.deepcopy(branch["assertions"]),
                }
                for branch in original["branches"]
            ],
            "exportExamples": copy.deepcopy(original["exportExamples"]),
            "currentBinding": {
                "inventory": copy.deepcopy(inventory_descriptor),
                "gameCommit": inventory["gameCommit"],
                "dirtySourceSha256": inventory["dirtySourceSha256"],
                "contentSha256": inventory["contentSha256"],
                "verifiedCurrentAtBinding": True,
            },
            "reuseProof": {
                "originalEvidence": copy.deepcopy(original_descriptor),
                "excludedChanges": [{
                    "path": editor_path,
                    "before": copy.deepcopy(old_by_path[editor_path]),
                    "after": copy.deepcopy(current_by_path[editor_path]),
                    "rationale": (
                        "OtherDebug is not referenced by the R00 Entry entry point "
                        "or its owned Assets/Test.cs dependency."
                    ),
                    "currentSourceBase64": base64.b64encode(current_raw).decode("ascii"),
                }],
            },
            "rowCurrentEvidenceComplete": True,
            "fullAuditClosure": False,
            "trainingEligible": False,
            "humanApprovalClaimed": False,
        }
        return document, self.seal_json(
            f"Artifacts/QA/{evidence_name}",
            document,
        )

    def reviewed_domain_reuse(self):
        path = "Assets/Runtime/Stable.cs"
        inventory, inventory_pin = self.changed_inventory(
            {path: b"sealed class Stable { public int Value => 2; }\n"},
            "reviewed-inventory.json")
        document, _ = self.reused_evidence(
            inventory=inventory, inventory_descriptor=inventory_pin)
        document["schema"] = matrix.REVIEWED_EVIDENCE_SCHEMA
        document["bindingMode"] = "reviewed-domain-non-impact"
        compilation, _ = self.current_evidence(
            inventory=inventory, inventory_descriptor=inventory_pin,
            artifact_prefix="review-compile-", evidence_name="review-compile-initial.json")
        after = next(x for x in inventory["files"] if x["path"] == path)
        compilation["sourceFiles"].append(copy.deepcopy(after))
        compilation["execution"]["unityLog"] = self.seal_text(
            "Artifacts/QA/review-compile-unity.log", "CompileScripts: 4.2ms\nPASS\n")
        compilation_pin = self.seal_json("Artifacts/QA/review-compilation.json", compilation)
        document["currentBinding"]["currentCompilationEvidence"] = compilation_pin
        note = self.seal_json("Artifacts/QA/row-review.json", {
            "rowId": "R00", "scope": "Test does not construct Stable or call OtherDebug."})
        reference = {"artifact": note, "pointer": "/scope"}
        changes = document["reuseProof"]["excludedChanges"]
        changes[0].update(classification="editor-only", reviewReferences=[reference])
        changes.append({
            "path": path,
            "before": next(x for x in self.old_inventory["files"] if x["path"] == path),
            "after": after,
            "classification": "production-non-impact",
            "rationale": "The controlled Test composition never constructs Stable.",
            "reviewReferences": [reference],
            "regressionEvidence": compilation_pin,
        })
        review = {
            "schema": "dungeonstory-b1-row-impact-review@1", "rowId": "R00",
            "originalEvidence": document["reuseProof"]["originalEvidence"],
            "currentInventory": inventory_pin, "requiredBranches": ["branch-0"],
            "unresolvedChanges": [], "humanApprovalClaimed": False,
            "trainingEligible": False, "excludedChanges": changes,
        }
        pin = self.reseal_reviewed_domain(document, review)
        return document, pin, inventory, inventory_pin, review

    def reseal_reviewed_domain(self, document, review):
        document["reuseProof"]["impactReview"] = self.seal_json(
            "Artifacts/QA/impact-review.json", review)
        return self.seal_json("Artifacts/QA/reviewed-evidence.json", document)

    def test_reviewed_domain_reuse_preserves_original_execution(self):
        document, pin, inventory, inventory_pin, _ = self.reviewed_domain_reuse()
        result = self.build([(document, pin)], inventory=inventory, inventory_descriptor=inventory_pin)
        row = result["rows"][0]
        self.assertEqual("current-evidence-complete", row["disposition"])
        self.assertEqual("reviewed-domain-non-impact", row["bindingMode"])
        self.assertEqual(document["sourceIdentity"], row["executionSourceIdentity"])
        self.assertNotEqual(inventory_pin, row["executionSourceIdentity"]["inventory"])

    def test_reviewed_domain_rejects_missing_or_foreign_review(self):
        for case in ("unresolved", "branches", "row", "approval", "training", "delta",
                     "no-reference", "no-pointer", "foreign-reference", "no-rationale"):
            with self.subTest(case=case):
                d, _, inv, inv_pin, review = self.reviewed_domain_reuse()
                change = d["reuseProof"]["excludedChanges"][-1]
                if case == "unresolved": review["unresolvedChanges"] = ["unknown"]
                elif case == "branches": review["requiredBranches"] = []
                elif case == "row": review["rowId"] = "R01"
                elif case == "approval": review["humanApprovalClaimed"] = True
                elif case == "training": review["trainingEligible"] = True
                elif case == "delta": review["excludedChanges"] = []
                elif case == "no-reference": change["reviewReferences"] = []
                elif case == "no-pointer": change["reviewReferences"][0]["pointer"] = None
                elif case == "foreign-reference":
                    change["reviewReferences"][0]["artifact"] = self.seal_json(
                        "Artifacts/QA/foreign-review.json", {"rowId": "R01", "scope": "unrelated"})
                elif case == "no-rationale": change["rationale"] = ""
                pin = self.reseal_reviewed_domain(d, review)
                with self.assertRaises(matrix.MatrixError):
                    self.build([(d, pin)], inventory=inv, inventory_descriptor=inv_pin)

    def test_reviewed_domain_requires_exact_production_regression_and_compile(self):
        for case in ("missing-regression", "old-regression", "editor-lie", "guarded-lie",
                     "missing-compile", "no-compile-log"):
            with self.subTest(case=case):
                d, _, inv, inv_pin, review = self.reviewed_domain_reuse()
                change = d["reuseProof"]["excludedChanges"][-1]
                if case == "missing-regression": del change["regressionEvidence"]
                elif case == "old-regression":
                    change["regressionEvidence"] = d["reuseProof"]["originalEvidence"]
                elif case == "editor-lie": change["classification"] = "editor-only"
                elif case == "guarded-lie":
                    change["classification"] = "guarded-editor-only"
                    change["sourceSnapshots"] = self.seal_json("Artifacts/QA/unguarded.json", {
                        "path": change["path"],
                        "beforeSourceBase64": base64.b64encode(self.stable_runtime_raw).decode(),
                        "currentSourceBase64": base64.b64encode(
                            (self.root / change["path"]).read_bytes()).decode(),
                    })
                elif case == "missing-compile": del d["currentBinding"]["currentCompilationEvidence"]
                elif case == "no-compile-log":
                    cp = d["currentBinding"]["currentCompilationEvidence"]
                    compile_doc = matrix.strict_json((self.root / cp["path"]).read_bytes())
                    compile_doc["execution"]["unityLog"] = self.seal_text(
                        "Artifacts/QA/no-compile.log", "PASS\n")
                    replacement = self.seal_json("Artifacts/QA/no-compile.json", compile_doc)
                    d["currentBinding"]["currentCompilationEvidence"] = replacement
                pin = self.reseal_reviewed_domain(d, review)
                with self.assertRaises(matrix.MatrixError):
                    self.build([(d, pin)], inventory=inv, inventory_descriptor=inv_pin)

    def test_reviewed_domain_rejects_owned_delta_and_original_rewrite(self):
        for case in ("owned", "content", "execution", "identity", "assertions", "legacy-schema",
                     "scope", "natural-play", "world-restore"):
            with self.subTest(case=case):
                d, pin, inv, inv_pin, review = self.reviewed_domain_reuse()
                if case in ("owned", "content"):
                    path = "Assets/Test.cs" if case == "owned" else "Assets/Stable.asset"
                    inv, inv_pin = self.changed_inventory({path: b"changed\n"}, "invalid-review-inventory.json")
                    d["currentBinding"].update(inventory=inv_pin, gameCommit=inv["gameCommit"],
                        dirtySourceSha256=inv["dirtySourceSha256"], contentSha256=inv["contentSha256"])
                elif case == "execution": d["execution"]["unityExit"] = 1
                elif case == "identity": d["sourceIdentity"]["inventory"] = inv_pin
                elif case == "assertions": d["branches"][0]["assertions"] = ["weaker"]
                elif case == "legacy-schema": d["schema"] = matrix.REUSED_EVIDENCE_SCHEMA
                elif case == "scope": d["scope"] = "whole game"
                elif case == "natural-play": d["naturalGameplay"] = True
                elif case == "world-restore": d["wholeWorldRestoreClaimed"] = True
                pin = self.reseal_reviewed_domain(d, review)
                with self.assertRaises(matrix.MatrixError):
                    self.build([(d, pin)], inventory=inv, inventory_descriptor=inv_pin)

    def reviewed_report_reuse(self):
        path = "Assets/Editor/OtherDebug.cs"
        self.old_editor_raw = (b'internal static class OtherDebug { static void Other() {\n'
                               b'WriteReport(new JObject { ["coverage"] = new JArray(\n'
                               b'    "existing-label") });\n} }\n')
        inserted = '    "new-label",\n'
        offset = self.old_editor_raw.index(b'    "existing-label"')
        self.current_editor_raw = (self.old_editor_raw[:offset] + inserted.encode()
                                   + self.old_editor_raw[offset:])
        (self.root / path).write_bytes(self.current_editor_raw)
        for attribute, raw, filename in (("old_inventory", self.old_editor_raw, "report-old.json"),
                                         ("inventory", self.current_editor_raw, "report-now.json")):
            files = [x for x in getattr(self, attribute)["files"] if x["path"] != path]
            inv, pin = self.seal_inventory(
                [*files, self.source_descriptor(path, raw)], "Artifacts/QA/" + filename)
            setattr(self, attribute, inv)
            setattr(self, attribute + "_descriptor", pin)
        d, _, inv, inv_pin, review = self.reviewed_domain_reuse()
        original_pin = d["reuseProof"]["originalEvidence"]
        original = matrix.strict_json((self.root / original_pin["path"]).read_bytes())
        before = next(x for x in self.old_inventory["files"] if x["path"] == path)
        after = next(x for x in inv["files"] if x["path"] == path)
        original["sourceFiles"].append(before)
        original_pin = self.seal_json("Artifacts/QA/report-owned-original.json", original)
        d["sourceFiles"] = copy.deepcopy(original["sourceFiles"])
        d["reuseProof"]["originalEvidence"] = original_pin
        review["originalEvidence"] = original_pin
        d["schema"] = matrix.REPORT_REVIEWED_EVIDENCE_SCHEMA
        d["bindingMode"] = "reviewed-editor-report-only"
        d["newExecutionClaimed"] = False
        d["currentBinding"]["sourceFiles"] = [d["sourceFiles"][0], after]
        compilation_pin = d["currentBinding"]["currentCompilationEvidence"]
        compilation = matrix.strict_json((self.root / compilation_pin["path"]).read_bytes())
        compilation["sourceFiles"].append(after)
        compilation_pin = self.seal_json("Artifacts/QA/report-compile.json", compilation)
        d["currentBinding"]["currentCompilationEvidence"] = compilation_pin
        change = d["reuseProof"]["excludedChanges"][0]
        snapshots = {"path": path,
                     "beforeSourceBase64": base64.b64encode(self.old_editor_raw).decode(),
                     "currentSourceBase64": base64.b64encode(self.current_editor_raw).decode(),
                     "insertion": {"offset": offset, "text": inserted}}
        change.update(classification="editor-report-label-only", regressionEvidence=compilation_pin,
                      sourceSnapshots=self.seal_json("Artifacts/QA/report-snapshots.json", snapshots))
        pin = self.reseal_reviewed_domain(d, review)
        return d, pin, inv, inv_pin, review, snapshots

    def test_report_reuse_preserves_old_execution_and_separately_binds_current_source(self):
        d, pin, inv, inv_pin, _, _ = self.reviewed_report_reuse()
        row = self.build([(d, pin)], inventory=inv, inventory_descriptor=inv_pin)["rows"][0]
        self.assertEqual(d["sourceIdentity"], row["executionSourceIdentity"])
        self.assertEqual(d["currentBinding"]["sourceFiles"], row["sourceFiles"])
        self.assertNotEqual(d["sourceFiles"], row["sourceFiles"])
        self.assertEqual("reviewed-editor-report-only", row["bindingMode"])

    def test_report_reuse_rejects_unsealed_or_nonliteral_changes(self):
        for case in ("old-schema", "ordinary-editor", "unbound-current", "drop-old-pin",
                     "claim-execution", "missing-regression", "old-regression", "snapshot-path",
                     "old-bytes", "new-bytes", "offset", "negative-offset", "bool-offset",
                     "code-insertion", "two-labels", "wrong-array"):
            with self.subTest(case=case):
                d, _, inv, inv_pin, review, snapshots = self.reviewed_report_reuse()
                change = d["reuseProof"]["excludedChanges"][0]
                if case == "old-schema":
                    d.update(schema=matrix.REVIEWED_EVIDENCE_SCHEMA,
                             bindingMode="reviewed-domain-non-impact")
                elif case == "ordinary-editor": change["classification"] = "editor-only"
                elif case == "unbound-current": d["currentBinding"]["sourceFiles"] = d["sourceFiles"]
                elif case == "drop-old-pin": d["sourceFiles"] = d["sourceFiles"][:1]
                elif case == "claim-execution": d["newExecutionClaimed"] = True
                elif case == "missing-regression": del change["regressionEvidence"]
                elif case == "old-regression": change["regressionEvidence"] = d["reuseProof"]["originalEvidence"]
                elif case == "snapshot-path": snapshots["path"] = "Assets/Test.cs"
                elif case == "old-bytes": snapshots["beforeSourceBase64"] = base64.b64encode(b"wrong").decode()
                elif case == "new-bytes": snapshots["currentSourceBase64"] = base64.b64encode(b"wrong").decode()
                elif case == "offset": snapshots["insertion"]["offset"] += 1
                elif case == "negative-offset": snapshots["insertion"]["offset"] = -1
                elif case == "bool-offset": snapshots["insertion"]["offset"] = True
                elif case == "code-insertion": snapshots["insertion"]["text"] = '    RunDangerous(),\n'
                elif case == "two-labels": snapshots["insertion"]["text"] *= 2
                elif case == "wrong-array":
                    # Matching snapshot hashes do not make an arbitrary literal
                    # array a reporting-only coverage edit.
                    for field, version in (("beforeSourceBase64", "before"),
                                           ("currentSourceBase64", "after")):
                        raw = base64.b64decode(snapshots[field]).replace(b'"coverage"', b'"gameplay"')
                        snapshots[field] = base64.b64encode(raw).decode()
                        change[version] = self.source_descriptor(change["path"], raw)
                    change["sourceSnapshots"] = self.seal_json("Artifacts/QA/report-mutated.json", snapshots)
                    with self.assertRaises(matrix.MatrixError):
                        matrix._validate_report_label_insertion(self.root, change, "test")
                    continue
                change["sourceSnapshots"] = self.seal_json("Artifacts/QA/report-mutated.json", snapshots)
                pin = self.reseal_reviewed_domain(d, review)
                with self.assertRaises(matrix.MatrixError):
                    self.build([(d, pin)], inventory=inv, inventory_descriptor=inv_pin)

    def recorded_dependency_reuse(self, *, body="new Stable();", extra_member="",
                                  secondary=False):
        before_raw = (
            "#if UNITY_EDITOR\ninternal static class OtherDebug {\n"
            " public static void Run() {}\n}\n#endif\n"
        ).encode()
        after_raw = (
            "#if UNITY_EDITOR\ninternal static class OtherDebug {\n"
            f" public static void Run() {{ {body} }}\n{extra_member}\n}}\n#endif\n"
        ).encode()
        old_files = {item["path"]: copy.deepcopy(item) for item in self.old_inventory["files"]}
        new_files = {item["path"]: copy.deepcopy(item) for item in self.inventory["files"]}
        path = "Assets/Editor/OtherDebug.cs"
        old_files[path] = self.source_descriptor(path, before_raw)
        new_files[path] = self.source_descriptor(path, after_raw)
        old_inventory, old_pin = self.seal_inventory(
            list(old_files.values()), "Artifacts/QA/trace-old-inventory.json")
        inventory, inventory_pin = self.seal_inventory(
            list(new_files.values()), "Artifacts/QA/trace-current-inventory.json")
        (self.root / path).write_bytes(after_raw)
        original, _ = self.current_evidence(
            inventory=old_inventory, inventory_descriptor=old_pin,
            artifact_prefix="trace-original-", evidence_name="trace-original-initial.json")
        original["sourceFiles"].append(copy.deepcopy(old_files["Assets/Runtime/Stable.cs"]))
        original["execution"]["unityLog"] = self.seal_text(
            original["execution"]["unityLog"]["path"], "-executeMethod\nTest.Run\nPASS\n")
        if secondary:
            report = matrix.strict_json((self.root / original["execution"]["report"]["path"]).read_bytes())
            report["extraNativeLogs"] = [self.seal_text(
                "Artifacts/QA/second/unity.log", "-executeMethod\nSecondEntry.Run\nPASS\n")]
            original["execution"]["report"] = self.seal_json(
                original["execution"]["report"]["path"], report)
        original_pin = self.seal_json("Artifacts/QA/trace-original.json", original)
        compilation, _ = self.current_evidence(
            inventory=inventory, inventory_descriptor=inventory_pin,
            artifact_prefix="trace-compile-", evidence_name="trace-compile-initial.json")
        compilation["execution"]["unityLog"] = self.seal_text(
            compilation["execution"]["unityLog"]["path"],
            "-executeMethod\nCompileProbe.Run\nCompileScripts: 4.2ms\nPASS\n")
        compilation_pin = self.seal_json("Artifacts/QA/trace-compilation.json", compilation)
        document, _ = self.reused_evidence(
            inventory=inventory, inventory_descriptor=inventory_pin,
            original=original, original_descriptor=original_pin,
            evidence_name="trace-reuse-initial.json")
        document["currentBinding"]["currentCompilationEvidence"] = compilation_pin
        change = document["reuseProof"]["excludedChanges"][0]
        change["before"] = copy.deepcopy(old_files[path])
        change["beforeSourceBase64"] = base64.b64encode(before_raw).decode()
        change["entrySeparationMode"] = "recorded-native-entrypoints"
        descriptor = self.seal_json("Artifacts/QA/trace-reuse.json", document)
        return document, descriptor, inventory, inventory_pin

    def seal_text(self, relative, text):
        path = self.root / relative
        raw = text.encode()
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        return {"path": relative, "bytes": len(raw), "sha256": digest(raw)}

    def test_recorded_entry_mode_accepts_shared_dependency_not_entry(self):
        document, descriptor, inventory, inventory_pin = self.recorded_dependency_reuse()
        result = self.build([(document, descriptor)], inventory=inventory,
                            inventory_descriptor=inventory_pin)
        self.assertEqual(1, result["counts"]["currentEvidenceComplete"])
        self.assertEqual(document["sourceIdentity"], result["rows"][0]["executionSourceIdentity"])

    def test_recorded_entry_mode_rejects_actual_primary_and_secondary_entries(self):
        for body, secondary in (("Test.Run();", False), ("SecondEntry.Run();", True)):
            with self.subTest(body=body):
                document, descriptor, inventory, inventory_pin = self.recorded_dependency_reuse(
                    body=body, secondary=secondary)
                with self.assertRaises(matrix.MatrixError):
                    self.build([(document, descriptor)], inventory=inventory,
                               inventory_descriptor=inventory_pin)

    def test_recorded_entry_mode_requires_current_compile_and_exact_before_snapshot(self):
        for field in ("missing-compile", "old-compile", "missing-before", "tampered-before"):
            with self.subTest(field=field):
                document, descriptor, inventory, inventory_pin = self.recorded_dependency_reuse()
                if field == "missing-compile":
                    del document["currentBinding"]["currentCompilationEvidence"]
                elif field == "old-compile":
                    document["currentBinding"]["currentCompilationEvidence"] = (
                        document["reuseProof"]["originalEvidence"])
                elif field == "missing-before":
                    del document["reuseProof"]["excludedChanges"][0]["beforeSourceBase64"]
                else:
                    document["reuseProof"]["excludedChanges"][0]["beforeSourceBase64"] = (
                        base64.b64encode(b"tampered").decode())
                with self.assertRaises(matrix.MatrixError):
                    self.build([(document, descriptor)], inventory=inventory,
                               inventory_descriptor=inventory_pin)

    def test_recorded_entry_mode_rejects_changed_initializers_types_and_auto_start(self):
        members = (
            " private static int Value = Build();",
            " private static int Value { get; } = Build();",
            " static OtherDebug() {}",
            " private sealed class Added {}",
            " [InitializeOnLoadMethod] private static void Auto() {}",
        )
        for member in members:
            with self.subTest(member=member):
                document, descriptor, inventory, inventory_pin = self.recorded_dependency_reuse(
                    extra_member=member)
                with self.assertRaises(matrix.MatrixError):
                    self.build([(document, descriptor)], inventory=inventory,
                               inventory_descriptor=inventory_pin)

    def test_recorded_entry_mode_rejects_missing_ambiguous_or_tampered_native_trace(self):
        for text in ("PASS\n", "-executeMethod\nTest.Run\n-executeMethod\nOther.Run\n", None):
            with self.subTest(text=text):
                document, descriptor, inventory, inventory_pin = self.recorded_dependency_reuse()
                original_pin = document["reuseProof"]["originalEvidence"]
                original = matrix.strict_json((self.root / original_pin["path"]).read_bytes())
                log_pin = original["execution"]["unityLog"]
                if text is None:
                    (self.root / log_pin["path"]).write_text("tampered")
                else:
                    original["execution"]["unityLog"] = self.seal_text(log_pin["path"], text)
                    new_original_pin = self.seal_json(original_pin["path"], original)
                    document["reuseProof"]["originalEvidence"] = new_original_pin
                    document["execution"] = copy.deepcopy(original["execution"])
                with self.assertRaises(matrix.MatrixError):
                    self.build([(document, descriptor)], inventory=inventory,
                               inventory_descriptor=inventory_pin)

    def test_recorded_entry_mode_requires_all_aggregate_native_traces(self):
        document, descriptor, inventory, inventory_pin = self.recorded_dependency_reuse(secondary=True)
        original_pin = document["reuseProof"]["originalEvidence"]
        original = matrix.strict_json((self.root / original_pin["path"]).read_bytes())
        self.assertEqual({"Test", "SecondEntry"}, matrix._recorded_execution_entry_symbols(
            self.root, original, "fixture"))
        result = self.build([(document, descriptor)], inventory=inventory,
                            inventory_descriptor=inventory_pin)
        self.assertEqual(1, result["counts"]["currentEvidenceComplete"])

    def test_recorded_entry_mode_allows_manual_generic_method_and_uninitialized_field(self):
        document, descriptor, inventory, inventory_pin = self.recorded_dependency_reuse(
            extra_member=" private static object Capture;\n private static void Require<T>(T value) {}")
        result = self.build([(document, descriptor)], inventory=inventory,
                            inventory_descriptor=inventory_pin)
        self.assertEqual(1, result["counts"]["currentEvidenceComplete"])

    def test_recorded_entry_owned_boundary_rejects_reverse_calls_and_changed_bytes(self):
        source = "#if UNITY_EDITOR\ninternal static class OtherDebug { }\n#endif\n"
        path = "Assets/Runtime/Stable.cs"
        for raw, sealed_raw in (
            (b"sealed class Stable { void Run() { OtherDebug.Run(); } }\n", None),
            (b"sealed class Stable { int changed; }\n", self.stable_runtime_raw),
        ):
            with self.subTest(raw=raw):
                (self.root / path).write_bytes(raw)
                descriptor = self.source_descriptor(path, sealed_raw or raw)
                with self.assertRaises(matrix.MatrixError):
                    matrix._validate_no_owned_callers(self.root, [descriptor], source, "fixture")

    def test_recorded_entry_mode_rejects_unproven_compile_log_even_with_zero_claimed_errors(self):
        for text in ("PASS\n", "CompileScripts: 4.2ms\nerror CS1000: failed\n"):
            with self.subTest(text=text):
                document, descriptor, inventory, inventory_pin = self.recorded_dependency_reuse()
                pin = document["currentBinding"]["currentCompilationEvidence"]
                compilation = matrix.strict_json((self.root / pin["path"]).read_bytes())
                compilation["execution"]["unityLog"] = self.seal_text(
                    compilation["execution"]["unityLog"]["path"], text)
                document["currentBinding"]["currentCompilationEvidence"] = self.seal_json(
                    pin["path"], compilation)
                with self.assertRaises(matrix.MatrixError):
                    self.build([(document, descriptor)], inventory=inventory,
                               inventory_descriptor=inventory_pin)

    def guarded_runtime_reuse(
        self,
        *,
        before_raw=None,
        after_raw=None,
        suffix="valid",
    ):
        before_raw = before_raw or (
            b"\xef\xbb\xbf\n#if UNITY_EDITOR\n"
            b"internal static class Stable { "
            b"public static string Value() => $\"old-{1}\"; }\n"
            b"#endif\n"
        )
        after_raw = after_raw or (
            b"\n#if UNITY_EDITOR\n"
            b"internal static class Stable { "
            b"public static string Value() => $\"new-{2}\"; }\n"
            b"#endif\n\n"
        )
        runtime_path = "Assets/Runtime/Stable.cs"
        old_descriptors = {
            item["path"]: copy.deepcopy(item)
            for item in self.old_inventory["files"]
        }
        old_descriptors[runtime_path] = self.source_descriptor(
            runtime_path,
            before_raw,
        )
        current_descriptors = {
            item["path"]: copy.deepcopy(item)
            for item in self.inventory["files"]
        }
        current_descriptors[runtime_path] = self.source_descriptor(
            runtime_path,
            after_raw,
        )
        (self.root / runtime_path).write_bytes(after_raw)
        old_inventory, old_inventory_descriptor = self.seal_inventory(
            list(old_descriptors.values()),
            f"Artifacts/QA/inventory-guarded-old-{suffix}.json",
        )
        inventory, inventory_descriptor = self.seal_inventory(
            list(current_descriptors.values()),
            f"Artifacts/QA/inventory-guarded-current-{suffix}.json",
        )
        original, original_descriptor = self.current_evidence(
            inventory=old_inventory,
            inventory_descriptor=old_inventory_descriptor,
            artifact_prefix=f"guarded-{suffix}-original-",
            evidence_name=f"guarded-{suffix}-original-evidence.json",
        )
        document, _ = self.reused_evidence(
            inventory=inventory,
            inventory_descriptor=inventory_descriptor,
            original=original,
            original_descriptor=original_descriptor,
            evidence_name=f"guarded-{suffix}-reuse-initial.json",
        )
        document["reuseProof"]["excludedChanges"].append({
            "path": runtime_path,
            "before": copy.deepcopy(old_descriptors[runtime_path]),
            "after": copy.deepcopy(current_descriptors[runtime_path]),
            "rationale": (
                "Stable is not referenced by the R00 Entry entry point or its "
                "owned Assets/Test.cs dependency, and both snapshots are "
                "whole-file UNITY_EDITOR guarded."
            ),
            "beforeSourceBase64": base64.b64encode(before_raw).decode("ascii"),
            "currentSourceBase64": base64.b64encode(after_raw).decode("ascii"),
        })
        descriptor = self.seal_json(
            f"Artifacts/QA/guarded-{suffix}-reuse.json",
            document,
        )
        return (
            document,
            descriptor,
            inventory,
            inventory_descriptor,
            original,
        )

    def test_classifies_every_row_without_completion_claim(self):
        result = self.build()
        self.assertEqual(91, result["counts"]["rows"])
        self.assertEqual(0, result["counts"]["unclassifiedRows"])
        self.assertEqual(0, result["counts"]["currentEvidenceComplete"])
        self.assertFalse(result["completionClaimed"])
        self.assertFalse(result["trainingEligible"])
        self.assertEqual("static-only", result["rows"][0]["branchCoverage"][0]["state"])
        self.assertEqual("historical-only", result["rows"][1]["branchCoverage"][0]["state"])
        self.assertEqual("unresolved-routing", result["rows"][2]["branchCoverage"][0]["state"])

    def test_rejects_branch_denominator_drift(self):
        result = self.build()
        tampered = copy.deepcopy(result)
        tampered["rows"][0]["branchCoverage"] = []
        with self.assertRaises(matrix.MatrixError):
            matrix.validate_matrix(tampered, self.inventory, self.inventory_descriptor)

    def test_rejects_false_current_completion(self):
        result = self.build()
        tampered = copy.deepcopy(result)
        row = tampered["rows"][0]
        row["disposition"] = "current-evidence-complete"
        row["branchCoverage"][0]["state"] = "covered-by-current-step"
        with self.assertRaises(matrix.MatrixError):
            matrix.validate_matrix(tampered, self.inventory, self.inventory_descriptor)

    def test_accepts_only_source_bound_current_evidence(self):
        evidence = self.current_evidence()
        result = self.build([evidence])
        row = result["rows"][0]
        self.assertEqual(1, result["counts"]["currentEvidenceComplete"])
        self.assertEqual("current-evidence-complete", row["disposition"])
        self.assertEqual("covered-by-current-focused", row["branchCoverage"][0]["state"])
        self.assertEqual(evidence[1], row["branchCoverage"][0]["executionRef"])
        self.assertEqual(evidence[0]["sourceFiles"], row["sourceFiles"])
        self.assertTrue(row["exportExamples"])
        self.assertEqual([], row["gaps"])

    def test_accepts_historical_non_impact_reuse(self):
        evidence = self.reused_evidence()
        (self.root / "Assets" / "Editor" / "OtherDebug.cs").write_bytes(
            b"[InitializeOnLoad] internal static class LaterWorktreeState {}\n"
        )

        result = self.build([evidence])

        row = result["rows"][0]
        self.assertEqual(1, result["counts"]["currentEvidenceComplete"])
        self.assertEqual("current-evidence-complete", row["disposition"])
        self.assertEqual(
            "covered-by-current-reuse",
            row["branchCoverage"][0]["state"],
        )
        self.assertEqual(evidence[1], row["branchCoverage"][0]["executionRef"])
        self.assertEqual(evidence[0]["sourceFiles"], row["sourceFiles"])
        self.assertEqual(evidence[0]["exportExamples"], row["exportExamples"])
        self.assertEqual("historical-non-impact", row["bindingMode"])
        self.assertEqual(
            evidence[0]["sourceIdentity"],
            row["executionSourceIdentity"],
        )
        self.assertNotEqual(
            result["sourceIdentity"]["dirtySourceSha256"],
            row["executionSourceIdentity"]["dirtySourceSha256"],
        )
        self.assertEqual([], row["gaps"])

    def test_accepts_whole_file_guarded_runtime_reuse(self):
        (
            document,
            descriptor,
            inventory,
            inventory_descriptor,
            original,
        ) = self.guarded_runtime_reuse()

        result = self.build(
            [(document, descriptor)],
            inventory=inventory,
            inventory_descriptor=inventory_descriptor,
        )

        row = result["rows"][0]
        runtime_change = next(
            item for item in document["reuseProof"]["excludedChanges"]
            if item["path"] == "Assets/Runtime/Stable.cs"
        )
        self.assertEqual("current-evidence-complete", row["disposition"])
        self.assertEqual("covered-by-current-reuse", row["branchCoverage"][0]["state"])
        self.assertEqual(original["sourceIdentity"], row["executionSourceIdentity"])
        self.assertEqual(original["exportExamples"], row["exportExamples"])
        self.assertEqual(
            runtime_change["before"]["sha256"],
            digest(base64.b64decode(runtime_change["beforeSourceBase64"])),
        )
        self.assertEqual(
            runtime_change["after"]["sha256"],
            digest(base64.b64decode(runtime_change["currentSourceBase64"])),
        )

    def test_rejects_guarded_runtime_reuse_with_invalid_before_snapshot(self):
        for case in ("missing-before-bytes", "tampered-before-bytes"):
            with self.subTest(case=case):
                (
                    document,
                    descriptor,
                    inventory,
                    inventory_descriptor,
                    _,
                ) = self.guarded_runtime_reuse(suffix=case)
                runtime_change = next(
                    item for item in document["reuseProof"]["excludedChanges"]
                    if item["path"] == "Assets/Runtime/Stable.cs"
                )
                if case == "missing-before-bytes":
                    del runtime_change["beforeSourceBase64"]
                else:
                    runtime_change["beforeSourceBase64"] = base64.b64encode(
                        b"tampered-before\n"
                    ).decode("ascii")

                with self.assertRaises(matrix.MatrixError):
                    self.build(
                        [(document, descriptor)],
                        inventory=inventory,
                        inventory_descriptor=inventory_descriptor,
                    )

    def test_rejects_guarded_runtime_reuse_when_old_snapshot_is_unguarded(self):
        (
            document,
            descriptor,
            inventory,
            inventory_descriptor,
            _,
        ) = self.guarded_runtime_reuse(
            before_raw=(
                b"internal static class Stable { "
                b"public static string Value() => \"old\"; }\n"
            ),
            suffix="old-unguarded",
        )

        with self.assertRaises(matrix.MatrixError):
            self.build(
                [(document, descriptor)],
                inventory=inventory,
                inventory_descriptor=inventory_descriptor,
            )

    def test_rejects_narrow_guard_lexical_and_directive_failures(self):
        cases = {
            "else": (
                b"#if UNITY_EDITOR\ninternal static class Stable {}\n"
                b"#else\ninternal static class StableRuntime {}\n#endif\n"
            ),
            "elif": (
                b"#if UNITY_EDITOR\ninternal static class Stable {}\n"
                b"#elif DEVELOPMENT_BUILD\ninternal static class Dev {}\n#endif\n"
            ),
            "code-after-close": (
                b"#if UNITY_EDITOR\ninternal static class Stable {}\n#endif\n"
                b"internal static class OutsideGuard {}\n"
            ),
            "additional-directive": (
                b"#if UNITY_EDITOR\n#region Extra\ninternal static class Stable {}\n"
                b"#endregion\n#endif\n"
            ),
            "unbalanced": (
                b"#if UNITY_EDITOR\ninternal static class Stable {}\n"
            ),
            "block-comment": (
                b"#if UNITY_EDITOR\n/* multiline\ncomment */\n"
                b"internal static class Stable {}\n#endif\n"
            ),
            "verbatim-multiline-string": (
                b"#if UNITY_EDITOR\ninternal static class Stable { "
                b"const string Value = @\"line one\nline two\"; }\n#endif\n"
            ),
            "raw-multiline-string": (
                b"#if UNITY_EDITOR\ninternal static class Stable { "
                b"const string Value = \"\"\"\nline\n\"\"\"; }\n#endif\n"
            ),
            "nul": (
                b"#if UNITY_EDITOR\ninternal static class Stable {\x00}\n#endif\n"
            ),
        }
        for case, after_raw in cases.items():
            with self.subTest(case=case):
                (
                    document,
                    descriptor,
                    inventory,
                    inventory_descriptor,
                    _,
                ) = self.guarded_runtime_reuse(
                    after_raw=after_raw,
                    suffix=f"guard-failure-{case}",
                )

                with self.assertRaises(matrix.MatrixError):
                    self.build(
                        [(document, descriptor)],
                        inventory=inventory,
                        inventory_descriptor=inventory_descriptor,
                    )

    def test_rejects_guarded_runtime_reuse_with_forbidden_symbols(self):
        def guarded(line):
            return (
                "#if UNITY_EDITOR\n"
                f"// {line}\n"
                "internal static class Stable { public static void Run() {} }\n"
                "#endif\n"
            ).encode("utf-8")

        cases = {
            "new-auto-init": {
                "after_raw": guarded("InitializeOnLoadMethod"),
            },
            "new-owner-symbol": {
                "after_raw": guarded("Test"),
            },
            "old-auto-init": {
                "before_raw": guarded("DidReloadScripts"),
            },
            "old-owner-symbol": {
                "before_raw": guarded("Test"),
            },
        }
        for case, raw_arguments in cases.items():
            with self.subTest(case=case):
                (
                    document,
                    descriptor,
                    inventory,
                    inventory_descriptor,
                    _,
                ) = self.guarded_runtime_reuse(
                    suffix=f"forbidden-{case}",
                    **raw_arguments,
                )

                with self.assertRaises(matrix.MatrixError):
                    self.build(
                        [(document, descriptor)],
                        inventory=inventory,
                        inventory_descriptor=inventory_descriptor,
                    )

    def test_verify_rejects_altered_reused_row_provenance(self):
        mutations = {
            "bindingMode": lambda row: row.__setitem__(
                "bindingMode", "altered-mode"
            ),
            "executionSourceIdentity": lambda row: row[
                "executionSourceIdentity"
            ].__setitem__("gameCommit", "2" * 40),
        }
        for field, mutate in mutations.items():
            with self.subTest(field=field):
                evidence = self.reused_evidence(
                    evidence_name=f"reuse-row-provenance-{field}.json",
                )
                tampered = self.build([evidence])
                mutate(tampered["rows"][0])
                matrix_path = f"Artifacts/QA/matrix-provenance-{field}.json"
                self.seal_json(matrix_path, tampered)

                with self.assertRaises(matrix.MatrixError):
                    matrix.verify(SimpleNamespace(
                        project_root=self.root,
                        matrix=Path(matrix_path),
                        inventory=Path(self.inventory_descriptor["path"]),
                        require_current_source=False,
                    ))

    def test_current_v1_does_not_accept_reuse_branch_state(self):
        document, descriptor = self.current_evidence()
        document["branches"][0]["state"] = "covered-by-current-reuse"

        with self.assertRaises(matrix.MatrixError):
            self.build([(document, descriptor)])

    def test_rejects_reuse_with_non_editor_or_content_inventory_delta(self):
        cases = {
            "changed-production": {
                "Assets/Runtime/Stable.cs": b"sealed class Stable { int changed; }\n",
            },
            "new-production": {
                "Assets/Runtime/NewThing.cs": b"sealed class NewThing {}\n",
            },
            "changed-content": {
                "Assets/Stable.asset": b"changed-content\n",
            },
            "new-content": {
                "Assets/New.asset": b"new-content\n",
            },
        }
        for name, replacements in cases.items():
            with self.subTest(name=name):
                inventory, inventory_descriptor = self.changed_inventory(
                    replacements,
                    f"inventory-{name}.json",
                )
                evidence = self.reused_evidence(
                    inventory=inventory,
                    inventory_descriptor=inventory_descriptor,
                    evidence_name=f"reuse-{name}.json",
                )

                with self.assertRaises(matrix.MatrixError):
                    self.build(
                        [evidence],
                        inventory=inventory,
                        inventory_descriptor=inventory_descriptor,
                    )

    def test_rejects_reuse_when_owned_source_changed(self):
        inventory, inventory_descriptor = self.changed_inventory(
            {"Assets/Test.cs": b"sealed class Test { int changed; }\n"},
            "inventory-owned-changed.json",
        )
        evidence = self.reused_evidence(
            inventory=inventory,
            inventory_descriptor=inventory_descriptor,
            evidence_name="reuse-owned-changed.json",
        )

        with self.assertRaises(matrix.MatrixError):
            self.build(
                [evidence],
                inventory=inventory,
                inventory_descriptor=inventory_descriptor,
            )

    def test_rejects_reuse_across_game_commit(self):
        inventory = copy.deepcopy(self.inventory)
        inventory["gameCommit"] = "2" * 40
        inventory_descriptor = self.seal_json(
            "Artifacts/QA/inventory-other-commit.json",
            inventory,
        )
        evidence = self.reused_evidence(
            inventory=inventory,
            inventory_descriptor=inventory_descriptor,
            evidence_name="reuse-other-commit.json",
        )

        with self.assertRaises(matrix.MatrixError):
            self.build(
                [evidence],
                inventory=inventory,
                inventory_descriptor=inventory_descriptor,
            )

    def test_rejects_reuse_with_forged_old_inventory_binding(self):
        for field in ("descriptor", "root"):
            with self.subTest(field=field):
                original, _ = self.current_evidence(
                    inventory=self.old_inventory,
                    inventory_descriptor=self.old_inventory_descriptor,
                    artifact_prefix=f"forged-{field}-",
                    evidence_name=f"initial-forged-{field}.json",
                )
                if field == "descriptor":
                    original["sourceIdentity"]["inventory"]["sha256"] = (
                        "sha256:" + "a" * 64
                    )
                else:
                    original["sourceIdentity"]["dirtySourceSha256"] = (
                        "sha256:" + "b" * 64
                    )
                original_descriptor = self.seal_json(
                    f"Artifacts/QA/original-forged-{field}.json",
                    original,
                )
                evidence = self.reused_evidence(
                    original=original,
                    original_descriptor=original_descriptor,
                    evidence_name=f"reuse-forged-{field}.json",
                )

                with self.assertRaises(matrix.MatrixError):
                    self.build([evidence])

    def test_rejects_reuse_with_forged_current_binding(self):
        mutations = {
            "inventory": lambda binding: binding["inventory"].__setitem__(
                "sha256", "sha256:" + "a" * 64
            ),
            "gameCommit": lambda binding: binding.__setitem__(
                "gameCommit", "2" * 40
            ),
            "dirtySourceSha256": lambda binding: binding.__setitem__(
                "dirtySourceSha256", "sha256:" + "b" * 64
            ),
            "contentSha256": lambda binding: binding.__setitem__(
                "contentSha256", "sha256:" + "c" * 64
            ),
        }
        for field, mutate in mutations.items():
            with self.subTest(field=field):
                document, descriptor = self.reused_evidence(
                    evidence_name=f"reuse-current-binding-{field}.json",
                )
                mutate(document["currentBinding"])

                with self.assertRaises(matrix.MatrixError):
                    self.build([(document, descriptor)])

    def test_rejects_reuse_when_inventory_delta_is_omitted(self):
        document, descriptor = self.reused_evidence()
        document["reuseProof"]["excludedChanges"] = []

        with self.assertRaises(matrix.MatrixError):
            self.build([(document, descriptor)])

    def test_rejects_reuse_when_original_evidence_copy_is_altered(self):
        def alter_source_identity(document):
            document["sourceIdentity"]["copyNote"] = "tampered"

        def alter_source_files(document):
            document["sourceFiles"].append(copy.deepcopy(next(
                item for item in self.old_inventory["files"]
                if item["path"] == "Assets/Runtime/Stable.cs"
            )))

        def alter_branches(document):
            document["branches"][0]["assertions"] = ["different assertion"]

        def alter_exports(document):
            document["exportExamples"][0]["kind"] = "different-kind"

        def alter_execution(document):
            document["execution"]["compileWarningLines"] = 1

        mutations = {
            "sourceIdentity": alter_source_identity,
            "sourceFiles": alter_source_files,
            "branches": alter_branches,
            "exportExamples": alter_exports,
            "execution": alter_execution,
        }
        for field, mutate in mutations.items():
            with self.subTest(field=field):
                document, descriptor = self.reused_evidence(
                    evidence_name=f"reuse-altered-{field}.json",
                )
                mutate(document)

                with self.assertRaises(matrix.MatrixError):
                    self.build([(document, descriptor)])

    def test_rejects_reuse_with_automatic_start_editor_change(self):
        tokens = (
            "InitializeOnLoad",
            "InitializeOnLoadMethod",
            "RuntimeInitializeOnLoadMethod",
            "InitializeOnEnterPlayMode",
            "DidReloadScripts",
            "AssetPostprocessor",
            "ScriptedImporter",
        )
        for token in tokens:
            with self.subTest(token=token):
                automatic_raw = (
                    f"// {token}\n"
                    "internal static class OtherDebug { "
                    "public static void Run() {} }\n"
                ).encode("utf-8")
                inventory, inventory_descriptor = self.changed_inventory(
                    {"Assets/Editor/OtherDebug.cs": automatic_raw},
                    f"inventory-automatic-start-{token}.json",
                )
                evidence = self.reused_evidence(
                    inventory=inventory,
                    inventory_descriptor=inventory_descriptor,
                    evidence_name=f"reuse-automatic-start-{token}.json",
                )

                with self.assertRaises(matrix.MatrixError):
                    self.build(
                        [evidence],
                        inventory=inventory,
                        inventory_descriptor=inventory_descriptor,
                    )

    def test_rejects_reuse_with_tampered_current_source_payload(self):
        document, descriptor = self.reused_evidence()
        document["reuseProof"]["excludedChanges"][0]["currentSourceBase64"] = (
            base64.b64encode(b"tampered\n").decode("ascii")
        )

        with self.assertRaises(matrix.MatrixError):
            self.build([(document, descriptor)])

    def test_rejects_malformed_current_evidence(self):
        document, descriptor = self.current_evidence()
        document["trainingEligible"] = True
        with self.assertRaises(matrix.MatrixError):
            self.build([(document, descriptor)])

    def test_rejects_evidence_pinned_to_previous_inventory(self):
        document, descriptor = self.current_evidence()
        target_inventory = copy.deepcopy(self.inventory)
        target_inventory["files"][0]["sha256"] = "sha256:" + "7" * 64
        target_inventory["dirtySourceSha256"] = matrix.descriptor_root(
            [
                item for item in target_inventory["files"]
                if Path(item["path"]).suffix.lower() in matrix.SOURCE_SUFFIXES
            ]
        )
        target_inventory_descriptor = copy.deepcopy(self.inventory_descriptor)
        target_inventory_descriptor["path"] = "Artifacts/QA/inventory-next.json"
        target_inventory_descriptor["sha256"] = "sha256:" + "8" * 64

        with self.assertRaisesRegex(
            matrix.MatrixError,
            "inventory descriptor differs",
        ):
            self.build(
                [(document, descriptor)],
                inventory=target_inventory,
                inventory_descriptor=target_inventory_descriptor,
            )

    def test_rejects_failed_report_with_current_descriptor(self):
        document, descriptor = self.current_evidence()
        report_path = self.root / document["execution"]["report"]["path"]
        report = matrix.strict_json(report_path.read_bytes())
        report["passed"] = False
        raw = matrix.canonical_json(report) + b"\n"
        report_path.write_bytes(raw)
        document["execution"]["report"] = {
            "path": report_path.relative_to(self.root).as_posix(),
            "bytes": len(raw),
            "sha256": digest(raw),
        }

        with self.assertRaisesRegex(
            matrix.MatrixError,
            "execution report did not pass",
        ):
            self.build([(document, descriptor)])

    def test_rejects_export_example_value_mismatch(self):
        document, descriptor = self.current_evidence()
        document["exportExamples"][0]["value"] = {
            "owner": "owner:tampered",
            "revision": 1,
        }

        with self.assertRaisesRegex(
            matrix.MatrixError,
            "export example 0 value differs from the report",
        ):
            self.build([(document, descriptor)])

    def test_rejects_changed_sealed_execution_artifact(self):
        document, descriptor = self.current_evidence()
        unity_log_path = self.root / document["execution"]["unityLog"]["path"]
        unity_log_path.write_bytes(b"FAIL\n")

        with self.assertRaisesRegex(
            matrix.MatrixError,
            "unityLog hash differs",
        ):
            self.build([(document, descriptor)])


if __name__ == "__main__":
    unittest.main()
