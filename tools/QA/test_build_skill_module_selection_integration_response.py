from __future__ import annotations

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).with_name(
    "build_skill_module_selection_integration_response.py")
SPEC = importlib.util.spec_from_file_location(
    "skill_module_selection_response_builder", MODULE_PATH)
builder = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = builder
SPEC.loader.exec_module(builder)


class SkillModuleSelectionPublicationTests(unittest.TestCase):
    def setUp(self) -> None:
        repository_temp = Path(__file__).resolve().parents[2] / "Temp"
        repository_temp.mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(
            prefix="skill-response-publication-", dir=repository_temp)
        self.root = Path(self.temp.name)
        (self.root / "Assets").mkdir()
        (self.root / "Artifacts" / "QA").mkdir(parents=True)
        self.source = self.root / "Artifacts" / "QA" / "source-v9-skill"
        self.candidates = [
            self.candidate_json("selection:a", ["skill:power-a"], [],
                                ["fact:a"], "Roma First", "A steady first path."),
            self.candidate_json("selection:b", ["skill:power-b"], [],
                                ["fact:b"], "Roma Second", "A patient second path."),
            self.candidate_json("selection:c", ["skill:power-c"], ["skill:cost-c"],
                                ["fact:c"], "Roma Third", "A costly third path."),
        ]
        self.write_source_package()

    def tearDown(self) -> None:
        self.temp.cleanup()

    def write_json(self, path: Path, value: object) -> bytes:
        raw = builder.helper.canonical_json(value) + b"\n"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        return raw

    def descriptor(self, path: Path) -> dict[str, object]:
        raw = path.read_bytes()
        return {
            "path": path.relative_to(self.source).as_posix(),
            "bytes": len(raw),
            "sha256": builder.helper.sha256_bytes(raw),
        }

    def candidate_json(self, selection_id: str, positive: list[str],
                       drawback: list[str], evidence: list[str],
                       display: str, flavor: str) -> str:
        return builder.helper.canonical_json({
            "selectionId": selection_id,
            "positiveModuleIds": positive,
            "drawbackModuleIds": drawback,
            "evidenceFactIds": evidence,
            "displayName": display,
            "narrativeFlavor": flavor,
        }).decode("utf-8")

    def write_source_package(self) -> None:
        world_path = self.source / "selection" / "skill" / "world.json"
        pending_path = self.source / "selection" / "skill" / "pending.json"
        wire_path = self.source / "selection" / "skill" / "wire.json"
        replay_wire_path = self.source / "selection" / "skill" / "replayed-wire.json"
        self.write_json(world_path, {"world": "controlled"})
        offers = [
            {
                "selectionId": "selection:a",
                "positiveModuleIds": ["skill:power-a"],
                "drawbackModuleIds": [],
                "evidenceFactIds": ["fact:a"],
            },
            {
                "selectionId": "selection:b",
                "positiveModuleIds": ["skill:power-b"],
                "drawbackModuleIds": [],
                "evidenceFactIds": ["fact:b"],
            },
            {
                "selectionId": "selection:c",
                "positiveModuleIds": ["skill:power-c"],
                "drawbackModuleIds": ["skill:cost-c"],
                "evidenceFactIds": ["fact:c"],
            },
        ]
        self.write_json(pending_path, {
            "moduleSelectionOffers": offers,
            "requestContext": {"version": 1, "characterName": "Roma"},
        })
        wire = self.write_json(wire_path, {"request": "skill:request-a"})
        replay_wire_path.parent.mkdir(parents=True, exist_ok=True)
        replay_wire_path.write_bytes(wire)

        mechanics_path = self.source / "mechanics.json"
        self.write_json(mechanics_path, {
            "schema": builder.helper.MECHANICS_SCHEMA,
        })
        selection_path = self.source / "selection-source.json"
        self.write_json(selection_path, {
            "schema": builder.helper.SELECTION_SOURCE_SCHEMA,
            "responseDataIncluded": False,
            "networkSent": False,
            "responseApplied": False,
            "humanApprovalClaimed": False,
            "trainingEligible": False,
            "selections": [{
                "selectionSourceId": "source:skill-a:selection:skill",
                "profileId": builder.SKILL_PROFILE,
                "correlationId": "skill:request-a",
                "selectionIds": ["selection:a", "selection:b", "selection:c"],
                "capture": {
                    "officialWorldCheckpoint": self.descriptor(world_path),
                    "pending": self.descriptor(pending_path),
                },
                "wire": {
                    "original": self.descriptor(wire_path),
                    "restoredReplay": self.descriptor(replay_wire_path),
                    "byteExactReplay": True,
                },
            }],
        })
        self.write_json(self.source / builder.helper.MANIFEST_FILE, {
            "schema": builder.helper.SOURCE_SCHEMA,
            "mechanics": self.descriptor(mechanics_path),
            "selectionSource": self.descriptor(selection_path),
            "selectionCount": 1,
            "humanApprovalClaimed": False,
            "trainingEligible": False,
            "dpoPerformed": False,
            "releaseReady": False,
        })

    def build(self, output: str) -> dict[str, object]:
        return builder.build_package(
            builder.helper.project_root(self.root),
            self.source.relative_to(self.root),
            Path("Artifacts/QA") / output,
            self.candidates,
        )

    def expected_package_bytes(self) -> tuple[bytes, bytes]:
        binding = builder.load_binding(
            builder.helper.project_root(self.root),
            self.source.relative_to(self.root))
        candidates = [
            builder.parse_candidate(raw, binding["offers"][index], index)
            for index, raw in enumerate(self.candidates)
        ]
        accepted_raw = builder.helper.canonical_json(
            {"candidates": candidates}) + b"\n"
        manifest = {
            "schema": builder.helper.RESPONSE_SCHEMA,
            "sourceManifestSha256": binding["manifest_sha256"],
            "selectionSourceId": binding["selection_source_id"],
            "profileId": builder.SKILL_PROFILE,
            "requestId": binding["request_id"],
            "wireSha256": binding["wire_sha256"],
            "authoringOrigin": "integration-fixture-explicit",
            "integrationFixture": True,
            "humanApprovalClaimed": False,
            "trainingEligible": False,
            "dpoPerformed": False,
            "releaseReady": False,
            "acceptedResponse": {
                "path": builder.helper.ACCEPTED_RESPONSE_FILE,
                "bytes": len(accepted_raw),
                "sha256": builder.helper.sha256_bytes(accepted_raw),
            },
            "acceptedResponseSha256": builder.helper.sha256_bytes(
                accepted_raw[:-1]),
        }
        return accepted_raw, builder.helper.canonical_json(manifest) + b"\n"

    def test_replace_failure_preserves_staging_files_byte_exact(self) -> None:
        expected_accepted, expected_manifest = self.expected_package_bytes()
        output = self.root / "Artifacts" / "QA" / "failed-response"
        with mock.patch.object(
                builder.os, "replace", side_effect=OSError("injected replace failure")):
            with self.assertRaisesRegex(
                    builder.helper.ResponseBuildError, "injected replace failure"):
                self.build("failed-response")

        self.assertFalse(output.exists())
        staging = list(output.parent.glob(f".{output.name}.staging-*"))
        self.assertEqual(1, len(staging))
        self.assertEqual(expected_accepted,
                         (staging[0] / builder.helper.ACCEPTED_RESPONSE_FILE).read_bytes())
        self.assertEqual(expected_manifest,
                         (staging[0] / builder.helper.MANIFEST_FILE).read_bytes())

    def test_create_only_existing_output_stays_untouched(self) -> None:
        output = self.root / "Artifacts" / "QA" / "existing-response"
        output.mkdir()
        marker = output / "preserve.txt"
        marker.write_bytes(b"do not overwrite\n")

        with self.assertRaisesRegex(
                builder.helper.ResponseBuildError, "already exists"):
            self.build("existing-response")

        self.assertEqual(b"do not overwrite\n", marker.read_bytes())
        self.assertEqual([marker], list(output.iterdir()))

    def test_successful_publication_still_works(self) -> None:
        result = self.build("published-response")
        output = self.root / result["output"]
        expected_accepted, expected_manifest = self.expected_package_bytes()

        self.assertEqual(expected_accepted,
                         (output / builder.helper.ACCEPTED_RESPONSE_FILE).read_bytes())
        self.assertEqual(expected_manifest,
                         (output / builder.helper.MANIFEST_FILE).read_bytes())
        self.assertEqual([], list(output.parent.glob(
            f".{output.name}.staging-*")))

    def test_missing_character_name_rejected_before_publication(self) -> None:
        self.candidates[0] = self.candidate_json(
            "selection:a", ["skill:power-a"], [], ["fact:a"],
            "First Path", "A steady first path.")
        original = tuple(self.candidates)
        with self.assertRaisesRegex(builder.helper.ResponseBuildError,
                                    "candidate 0 must mention.*Roma"):
            self.build("missing-name")
        self.assertEqual(original, tuple(self.candidates))
        self.assertFalse((self.root / "Artifacts/QA/missing-name").exists())
        self.assertEqual([], list((self.root / "Artifacts/QA").glob(
            ".missing-name.staging-*")))

    def test_character_name_in_flavor_is_sufficient(self) -> None:
        self.candidates[0] = self.candidate_json(
            "selection:a", ["skill:power-a"], [], ["fact:a"],
            "First Path", "Roma follows a steady first path.")
        result = self.build("name-in-flavor")
        self.assertTrue((self.root / result["output"]).is_dir())


if __name__ == "__main__":
    unittest.main()
