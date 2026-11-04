"""Request-bound history envelope checks against the Tools bridge source."""
from copy import deepcopy
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest

from jsonschema.exceptions import ValidationError


TOOLS_AI = Path(__file__).resolve().parent
PROJECT_ROOT = TOOLS_AI.parents[1]
RECIPE = (PROJECT_ROOT / "Assets" / "StreamingAssets" / "R80"
          / "DungeonStoryLlm" / "recipe")
STATIC_SCHEMAS = RECIPE / "static-schemas.json"
SOURCE_BRIDGE = TOOLS_AI / "r80_game_bridge.py"
for import_path in (str(RECIPE), str(TOOLS_AI)):
    if import_path not in sys.path:
        sys.path.insert(0, import_path)

# The aggregate runner may already have imported the bundled bridge. Load the
# Tools source under a unique name so this fixture always tests the authored
# source instead of whichever r80_game_bridge first occupied sys.modules.
SOURCE_SPEC = importlib.util.spec_from_file_location(
    "r80_history_envelope_tools_source", SOURCE_BRIDGE)
bridge = importlib.util.module_from_spec(SOURCE_SPEC)
SOURCE_SPEC.loader.exec_module(bridge)


FIXED_FIELDS = (
    "requestKey", "targetPersistentId", "nodeId", "parentNodeId",
    "effectId", "effectBudget", "evidenceIds",
)
PROSE_FIELDS = ("displayName", "description", "historyReason")


class SpyEngine:
    def __init__(self, response):
        self.response = response
        self.calls = []

    def call(self, messages, schema, transform, folder, stage, limit,
             excluded_names=()):
        self.calls.append(dict(messages=deepcopy(messages), schema=deepcopy(schema),
            folder=Path(folder), stage=stage, limit=limit,
            excludedNames=list(excluded_names)))
        return transform(self.response)


class HistoryEnvelopeTests(unittest.TestCase):
    def setUp(self):
        self.wide = json.loads(STATIC_SCHEMAS.read_bytes())["schemas"][
            "EvolutionHistoryLegacyV2"]
        self.fixed = {
            "requestKey": "evolution-history:Facility:building:원본:node:sealed",
            "targetPersistentId": "building:원본\\branch",
            "nodeId": "facility-history:고정-node",
            "parentNodeId": "",
            "effectId": "history:facility",
            "effectBudget": 7,
            "evidenceIds": [
                "evidence:01", "evidence:02\\branch", 'evidence:"03"',
                "evidence:04/route", "evidence:05", "근거:06",
                "evidence:07", "evidence:08",
            ],
        }
        self.prose = {
            "displayName": "청명한 별길",
            "description": "한글과 日本語, Ω와 달빛 문양이 원문 그대로 남는다.",
            "historyReason": "윤아가 남긴 여덟 사건의 순서가 이 시설의 기억으로 이어졌다.",
        }
        self.schema = deepcopy(self.wide)
        for key, value in self.fixed.items():
            self.schema["properties"][key]["const"] = deepcopy(value)
        self.wire = {
            "model": "actual-r80",
            "stream": False,
            "messages": [
                {"role": "system", "content": "원래 시스템 지시"},
                {"role": "user", "content": "원래 공개 사실과 호출자 조건 Ω"},
            ],
            "response_format": {
                "type": "json_schema",
                "json_schema": {
                    "name": "DungeonStory_EvolutionHistoryLegacyV2",
                    "strict": True,
                    "schema": self.schema,
                },
            },
            "temperature": 0.35,
            "max_tokens": 384,
        }
        self.row = self.make_row(self.wire)

    @staticmethod
    def make_row(wire, profile="EvolutionHistoryLegacyV2"):
        return {
            "profile": profile,
            "wire": wire,
            "requestKey": "request-trace-history-envelope",
            "sourceWireSha256": "sha256:" + hashlib.sha256(
                bridge.canonical(wire)).hexdigest(),
        }

    def expected_prose_schema(self):
        return {
            "type": "object",
            "additionalProperties": False,
            "required": list(PROSE_FIELDS),
            "properties": {
                key: deepcopy(self.wide["properties"][key])
                for key in PROSE_FIELDS
            },
        }

    def test_exact_owner_envelope_preserves_caller_metadata_and_unicode_prose(self):
        self.assertEqual(Path(bridge.__file__).resolve(), SOURCE_BRIDGE)
        row_before = deepcopy(self.row)
        response_before = deepcopy(self.prose)
        engine = SpyEngine(self.prose)
        with TemporaryDirectory() as temporary:
            folder = Path(temporary)
            result = bridge.generate_history_narrative(engine, self.row, folder)
            input_trace = json.loads((folder / "history-envelope-input.json").read_bytes())
            assembly_trace = json.loads((folder / "history-envelope-assembly.json").read_bytes())

        self.assertEqual(self.row, row_before)
        self.assertEqual(engine.response, response_before)
        self.assertEqual(len(engine.calls), 1)
        call = engine.calls[0]
        self.assertEqual(call["messages"][:len(self.wire["messages"])],
                         self.wire["messages"])
        self.assertEqual(call["schema"], self.expected_prose_schema())
        self.assertEqual(call["stage"], "auxiliary")
        self.assertEqual(call["limit"], 384)
        self.assertEqual(call["excludedNames"], [])
        self.assertEqual(json.loads(call["messages"][-1]["content"]), {
            "mode": "LOCKED_HISTORY_PROSE", "responseFields": list(PROSE_FIELDS)})

        self.assertEqual(list(result), list(FIXED_FIELDS + PROSE_FIELDS))
        for key in FIXED_FIELDS:
            self.assertEqual(result[key], self.fixed[key])
        self.assertEqual(result["evidenceIds"], self.fixed["evidenceIds"])
        for key in PROSE_FIELDS:
            self.assertEqual(result[key], self.prose[key])
        self.assertEqual(result, dict(self.fixed, **self.prose))

        self.assertEqual(input_trace, {
            "ownerMetadata": self.fixed,
            "sourceWireSha256": self.row["sourceWireSha256"],
            "modelFields": list(PROSE_FIELDS),
            "modelSelectsMetadata": False,
            "outputRepair": False,
        })
        self.assertEqual(assembly_trace, {
            "ownerMetadataAssembled": True,
            "modelFields": list(PROSE_FIELDS),
            "modelProseUnchanged": True,
            "modelSelectsMetadata": False,
            "outputRepair": False,
        })
        self.assertEqual(self.row["sourceWireSha256"], "sha256:" + hashlib.sha256(
            bridge.canonical(self.row["wire"])).hexdigest())

    def test_wrong_profile_hash_or_missing_owner_lock_is_rejected_before_inference(self):
        original = deepcopy(self.row)
        for key in FIXED_FIELDS:
            wire = deepcopy(self.wire)
            del wire["response_format"]["json_schema"]["schema"]["properties"][key]["const"]
            row = self.make_row(wire)
            before = deepcopy(row)
            engine = SpyEngine(deepcopy(self.prose))
            with TemporaryDirectory() as temporary:
                with self.assertRaises(ValueError, msg=key):
                    bridge.generate_history_narrative(engine, row, Path(temporary))
            self.assertEqual(row, before)
            self.assertEqual(engine.calls, [])

        wrong_hash = deepcopy(self.row)
        wrong_hash["sourceWireSha256"] = "sha256:" + "0" * 64
        wrong_hash_before = deepcopy(wrong_hash)
        with TemporaryDirectory() as temporary:
            with self.assertRaises(ValueError):
                bridge.generate_history_narrative(
                    SpyEngine(deepcopy(self.prose)), wrong_hash, Path(temporary))
        self.assertEqual(wrong_hash, wrong_hash_before)

        wrong_profile = deepcopy(self.row)
        wrong_profile["profile"] = "Persona"
        wrong_profile_before = deepcopy(wrong_profile)
        with TemporaryDirectory() as temporary:
            with self.assertRaises(ValueError):
                bridge.generate_history_narrative(
                    SpyEngine(deepcopy(self.prose)), wrong_profile, Path(temporary))
        self.assertEqual(wrong_profile, wrong_profile_before)
        self.assertEqual(self.row, original)

    def test_model_metadata_or_overlong_prose_is_rejected_without_repair(self):
        cases = {
            "extra-owner-id": dict(self.prose, effectId="history:model-invented"),
            "overlong-description": dict(self.prose, description="가" * 181),
        }
        for label, response in cases.items():
            with self.subTest(label=label):
                row = deepcopy(self.row)
                row_before = deepcopy(row)
                response_before = deepcopy(response)
                engine = SpyEngine(response)
                with TemporaryDirectory() as temporary:
                    folder = Path(temporary)
                    with self.assertRaises(ValidationError):
                        bridge.generate_history_narrative(engine, row, folder)
                    marker = json.loads(
                        (folder / "history-envelope-input.json").read_bytes())
                    self.assertFalse((folder / "history-envelope-assembly.json").exists())
                self.assertEqual(row, row_before)
                self.assertEqual(engine.response, response_before)
                self.assertEqual(len(engine.calls), 1)
                self.assertEqual(engine.calls[0]["schema"], self.expected_prose_schema())
                self.assertFalse(marker["modelSelectsMetadata"])
                self.assertFalse(marker["outputRepair"])


if __name__ == "__main__":
    unittest.main()
