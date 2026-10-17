from __future__ import annotations

import importlib.util
import sys
import tempfile
import unittest
from unittest import mock
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("build_module_selection_integration_response.py")
SPEC = importlib.util.spec_from_file_location("module_selection_response_builder", MODULE_PATH)
builder = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = builder
SPEC.loader.exec_module(builder)

TRAIT_PATH = MODULE_PATH.with_name("build_trait_module_selection_integration_response.py")
TRAIT_SPEC = importlib.util.spec_from_file_location("trait_response_builder", TRAIT_PATH)
trait_builder = importlib.util.module_from_spec(TRAIT_SPEC)
assert TRAIT_SPEC.loader is not None
TRAIT_SPEC.loader.exec_module(trait_builder)


class ModuleSelectionIntegrationResponseTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "Assets").mkdir()
        (self.root / "Artifacts" / "QA").mkdir(parents=True)
        self.source = self.root / "Artifacts" / "QA" / "source-v9-equipment"
        self.write_source_package()

    def tearDown(self) -> None:
        self.temp.cleanup()

    def write_json(self, path: Path, value: object) -> bytes:
        raw = builder.canonical_json(value) + b"\n"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        return raw

    def descriptor(self, path: Path) -> dict[str, object]:
        raw = path.read_bytes()
        return {
            "path": path.relative_to(self.source).as_posix(),
            "bytes": len(raw),
            "sha256": builder.sha256_bytes(raw),
        }

    def write_source_package(self) -> None:
        pending_path = self.source / "selection" / "equipment" / "pending.json"
        world_path = self.source / "selection" / "equipment" / "world.json"
        witness_path = self.source / "selection" / "equipment" / "witness.json"
        wire_path = self.source / "selection" / "equipment" / "wire.json"
        replay_wire_path = self.source / "selection" / "equipment" / "replayed-wire.json"
        request_id = "presentation:equipment:request-a"
        self.write_json(pending_path, {
            "presentationId": request_id,
            "targetPersistentId": "equipment:iron-blade",
            "attunementOwnerPersistentId": "character:owner-a",
            "nodeId": "equipment:node-a",
            "evidenceIds": ["fact:win-a", "fact:forge-a"],
        })
        self.write_json(world_path, {"world": "controlled"})
        self.write_json(witness_path, {"captureOnly": True})
        wire = self.write_json(wire_path, {"request": request_id})
        replay_wire_path.parent.mkdir(parents=True, exist_ok=True)
        replay_wire_path.write_bytes(wire)

        mechanics_path = self.source / "mechanics.json"
        self.write_json(mechanics_path, {
            "schema": builder.MECHANICS_SCHEMA,
            "codeIdentity": [{"path": "Assets/Example.cs"}],
            "b1EvidenceManifest": {"sha256": "sha256:" + "a" * 64},
            "humanApprovalClaimed": False,
            "trainingEligible": False,
        })
        selection_path = self.source / "selection-source.json"
        self.write_json(selection_path, {
            "schema": builder.SELECTION_SOURCE_SCHEMA,
            "sourcePackageId": "source:equipment-a",
            "responseDataIncluded": False,
            "networkSent": False,
            "responseApplied": False,
            "humanApprovalClaimed": False,
            "trainingEligible": False,
            "selections": [{
                "selectionSourceId": "source:equipment-a:selection:equipment",
                "profileId": builder.EQUIPMENT_PROFILE,
                "targetId": "equipment:iron-blade",
                "ownerId": "character:owner-a",
                "triggerId": request_id,
                "correlationId": request_id,
                "candidateCount": 2,
                "selectionIds": ["power:edge", "drawback:fragile"],
                "responseDataIncluded": False,
                "capture": {
                    "officialWorldCheckpoint": self.descriptor(world_path),
                    "pending": self.descriptor(pending_path),
                    "witness": self.descriptor(witness_path),
                },
                "wire": {
                    "original": self.descriptor(wire_path),
                    "restoredReplay": self.descriptor(replay_wire_path),
                    "byteExactReplay": True,
                },
            }],
        })
        manifest = {
            "schema": builder.SOURCE_SCHEMA,
            "sourcePackageId": "source:equipment-a",
            "mechanics": self.descriptor(mechanics_path),
            "selectionSource": self.descriptor(selection_path),
            "selectionCount": 1,
            "integrationFixture": False,
            "naturalPlayClaimed": False,
            "fullAuditClosure": False,
            "humanApprovalClaimed": False,
            "trainingEligible": False,
            "dpoPerformed": False,
            "releaseReady": False,
        }
        self.write_json(self.source / builder.MANIFEST_FILE, manifest)

    def build(self, output: str = "response-a", **changes: object) -> dict[str, object]:
        options: dict[str, object] = {
            "positive_module_ids": ["power:edge"],
            "drawback_module_ids": ["drawback:fragile"],
            "evidence_fact_ids": ["fact:win-a"],
            "display_name": "Tempered Edge",
            "narrative_flavor": "A patient forge remembers the bearer.",
        }
        options.update(changes)
        return builder.build_package(
            builder.project_root(self.root),
            self.source.relative_to(self.root),
            Path("Artifacts/QA") / output,
            **options,
        )

    def test_creates_canonical_source_bound_response(self) -> None:
        result = self.build()
        output = self.root / result["output"]
        accepted_raw = (output / builder.ACCEPTED_RESPONSE_FILE).read_bytes()
        self.assertTrue(accepted_raw.endswith(b"\n"))
        self.assertEqual(accepted_raw[:-1], builder.canonical_json(
            builder.strict_json(accepted_raw[:-1], "accepted response")))
        accepted = builder.strict_json(accepted_raw[:-1], "accepted response")
        self.assertEqual({
            "selectionId", "positiveModuleIds", "drawbackModuleIds",
            "evidenceFactIds", "displayName", "narrativeFlavor",
        }, set(accepted))
        self.assertEqual(["power:edge"], accepted["positiveModuleIds"])
        self.assertEqual(["fact:win-a"], accepted["evidenceFactIds"])

        manifest_raw = (output / builder.MANIFEST_FILE).read_bytes()
        manifest = builder.strict_json(manifest_raw, "response manifest")
        source_raw = (self.source / builder.MANIFEST_FILE).read_bytes()
        self.assertEqual(builder.RESPONSE_SCHEMA, manifest["schema"])
        self.assertEqual(builder.sha256_bytes(source_raw), manifest["sourceManifestSha256"])
        self.assertEqual(builder.sha256_bytes(accepted_raw[:-1]),
                         manifest["acceptedResponseSha256"])
        self.assertEqual(len(accepted_raw), manifest["acceptedResponse"]["bytes"])
        self.assertEqual(builder.sha256_bytes(accepted_raw),
                         manifest["acceptedResponse"]["sha256"])
        self.assertTrue(manifest["integrationFixture"])
        self.assertFalse(manifest["humanApprovalClaimed"])
        self.assertFalse(manifest["trainingEligible"])
        self.assertFalse(manifest["dpoPerformed"])
        self.assertFalse(manifest["releaseReady"])

    def test_create_only_refuses_existing_output(self) -> None:
        output = self.root / "Artifacts" / "QA" / "existing-response"
        output.mkdir()
        marker = output / "preserve.txt"
        marker.write_text("do not overwrite\n", encoding="utf-8")
        with self.assertRaisesRegex(builder.ResponseBuildError, "already exists"):
            self.build("existing-response")
        self.assertEqual("do not overwrite\n", marker.read_text(encoding="utf-8"))

    def test_failed_publication_preserves_staging_and_source(self) -> None:
        source_before = (self.source / builder.MANIFEST_FILE).read_bytes()
        with mock.patch.object(builder.os, "replace", side_effect=PermissionError("blocked publication")):
            with self.assertRaisesRegex(builder.ResponseBuildError, "staging preserved at"):
                self.build("failed-response")
        parent = self.root / "Artifacts" / "QA"
        self.assertFalse((parent / "failed-response").exists())
        staging = list(parent.glob(".failed-response.staging-*"))
        self.assertEqual(1, len(staging))
        manifest = builder.strict_json((staging[0] / builder.MANIFEST_FILE).read_bytes(), "preserved manifest")
        accepted = (staging[0] / builder.ACCEPTED_RESPONSE_FILE).read_bytes()
        self.assertEqual(builder.sha256_bytes(accepted), manifest["acceptedResponse"]["sha256"])
        self.assertEqual(source_before, (self.source / builder.MANIFEST_FILE).read_bytes())

    def test_rejects_tampered_selection_source_descriptor(self) -> None:
        selection_path = self.source / "selection-source.json"
        changed = builder.strict_json(selection_path.read_bytes(), "selection source")
        changed["selections"][0]["profileId"] = "TamperedProfile"
        self.write_json(selection_path, changed)
        with self.assertRaisesRegex(builder.ResponseBuildError, "selection source (byte count|bytes/hash) differs"):
            self.build("tampered-response")
        self.assertFalse((self.root / "Artifacts" / "QA" / "tampered-response").exists())

    def test_rejects_unoffered_or_duplicate_explicit_selection(self) -> None:
        with self.assertRaisesRegex(builder.ResponseBuildError, "not offered"):
            self.build("illegal-module", positive_module_ids=["power:not-offered"])
        with self.assertRaisesRegex(builder.ResponseBuildError, "duplicates"):
            self.build("duplicate-module", positive_module_ids=["power:edge", "power:edge"])
        with self.assertRaisesRegex(builder.ResponseBuildError, "not offered"):
            self.build("illegal-evidence", evidence_fact_ids=["fact:not-offered"])

    def test_rejects_output_outside_project_qa(self) -> None:
        with self.assertRaisesRegex(builder.ResponseBuildError, "inside Artifacts/QA"):
            builder.build_package(
                builder.project_root(self.root),
                self.source.relative_to(self.root),
                self.root / "outside-response",
                ["power:edge"],
                [],
                ["fact:win-a"],
                "Tempered Edge",
                "A patient forge remembers the bearer.",
            )

    def prepare_trait_source(self) -> None:
        selection_path = self.source / "selection-source.json"
        selection = builder.strict_json(selection_path.read_bytes(), "selection")
        row = selection["selections"][0]
        row["profileId"] = trait_builder.TRAIT_PROFILE
        row["selectionIds"] = ["selection:trait-a"]
        pending = self.source / row["capture"]["pending"]["path"]
        self.write_json(pending, {
            "moduleSelectionId": "selection:trait-a",
            "offeredBenefitModuleIds": ["acquired-trait:module:work-rhythm",
                                        "acquired-trait:module:battle-temper"],
            "evidenceFactIds": ["fact:win-a", "fact:forge-a"],
            "evidenceBindings": [{"sourceKind": "exact", "publicFactId": "fact:win-a"}],
        })
        row["capture"]["pending"] = self.descriptor(pending)
        self.write_json(selection_path, selection)
        manifest_path = self.source / builder.MANIFEST_FILE
        manifest = builder.strict_json(manifest_path.read_bytes(), "manifest")
        manifest["selectionSource"] = self.descriptor(selection_path)
        self.write_json(manifest_path, manifest)

    def test_trait_keeps_explicit_offered_module_and_all_evidence(self) -> None:
        self.prepare_trait_source()
        before = (self.source / builder.MANIFEST_FILE).read_bytes()
        for suffix in ("work-rhythm", "battle-temper"):
            module = "acquired-trait:module:" + suffix
            result = trait_builder.build_package(self.root, self.source,
                Path("Artifacts/QA/trait-" + suffix), "검사 특성", "실제 근거의 효과를 검사한다.", module)
            output = self.root / result["output"]
            accepted = builder.strict_json((output / builder.ACCEPTED_RESPONSE_FILE).read_bytes(), "accepted")
            self.assertEqual([module], accepted["positiveModuleIds"])
            self.assertEqual(["fact:forge-a", "fact:win-a"], accepted["evidenceFactIds"])
            self.assertEqual(module, result["selectedModuleId"])
            self.assertFalse(result["trainingEligible"])
            self.assertEqual(before, (self.source / builder.MANIFEST_FILE).read_bytes())

    def test_trait_rejects_unoffered_explicit_module(self) -> None:
        self.prepare_trait_source()
        output = self.root / "Artifacts/QA/trait-invalid"
        with self.assertRaisesRegex(trait_builder.helper.ResponseBuildError, "not offered"):
            trait_builder.build_package(self.root, self.source, output,
                "검사 특성", "실제 근거의 효과를 검사한다.", "acquired-trait:module:unknown")
        self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
