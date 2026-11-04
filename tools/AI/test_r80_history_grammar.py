"""Generation-only checks for request-bound evolution-history grammar."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys
import unittest


TOOLS_AI = Path(__file__).resolve().parent
PROJECT_ROOT = TOOLS_AI.parents[1]
if str(TOOLS_AI) not in sys.path:
    sys.path.insert(0, str(TOOLS_AI))

import r80_game_bridge as bridge
from test_r80_name_grammar import regex_for


STATIC_SCHEMAS = (PROJECT_ROOT / "Assets" / "StreamingAssets" / "R80"
                  / "DungeonStoryLlm" / "recipe" / "static-schemas.json")
TOOLS_BRIDGE = TOOLS_AI / "r80_game_bridge.py"
BUNDLED_BRIDGE = (PROJECT_ROOT / "Assets" / "StreamingAssets" / "R80"
                  / "DungeonStoryLlm" / "r80_game_bridge.py")


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class EvolutionHistoryGrammarTests(unittest.TestCase):
    def setUp(self):
        schemas = json.loads(STATIC_SCHEMAS.read_bytes())["schemas"]
        self.wide = deepcopy(schemas["EvolutionHistoryLegacyV2"])
        self.original_wide = deepcopy(self.wide)
        self.fixed = {
            "requestKey": 'evolution-history:Facility:building:alpha\\branch:"sealed":hash',
            "targetPersistentId": 'building:alpha\\branch:"sealed"',
            "nodeId": 'facility-history:node\\one:"quoted"',
            "parentNodeId": "",
            "effectId": 'history:facility\\stable:"v2"',
            "effectBudget": 7,
            "evidenceIds": [
                "evidence:01",
                "evidence:02\\branch",
                'evidence:"03"',
                "evidence:04/route",
                "evidence:05",
                "evidence:06",
                "evidence:07",
                "evidence:08",
            ],
        }
        self.bound = deepcopy(self.wide)
        for key, value in self.fixed.items():
            self.bound["properties"][key]["const"] = deepcopy(value)
        self.before_decoder = deepcopy(self.bound)
        self.native, decoder, self.constraint = bridge.decoder_for(
            self.bound, lambda value: value, "auxiliary")
        rules = dict(line.split(" ::= ", 1)
                     for line in decoder["grammar"].splitlines() if line)
        self.pattern = regex_for(rules, "root")

    def payload(self):
        return dict(self.fixed,
                    displayName="맑은 손길",
                    description="이 시설은 청소 서비스를 운영한다.",
                    historyReason="시설의 실제 청소 기록이 변화를 남겼다.")

    def accepts(self, value):
        text = json.dumps(value, ensure_ascii=False, sort_keys=True)
        return bool(self.pattern.fullmatch(text))

    def test_actual_wide_schema_and_decoder_inputs_remain_unchanged(self):
        imported_bridge = Path(bridge.__file__).resolve()
        self.assertIn(imported_bridge, {TOOLS_BRIDGE, BUNDLED_BRIDGE})
        self.assertEqual(sha256(TOOLS_BRIDGE), sha256(BUNDLED_BRIDGE))
        self.assertEqual(sha256(imported_bridge), sha256(TOOLS_BRIDGE))
        self.assertEqual(self.wide, self.original_wide)
        self.assertEqual(self.bound, self.before_decoder)
        self.assertEqual(self.constraint["originalSchemaUnchanged"], True)
        self.assertEqual(self.constraint["outputRepair"], False)
        for key, value in self.fixed.items():
            original = self.original_wide["properties"][key]
            narrowed = self.bound["properties"][key]
            self.assertEqual(narrowed["const"], value)
            for retained in ("type", "minLength", "maxLength", "minItems",
                             "maxItems", "minimum", "maximum"):
                if retained in original:
                    self.assertEqual(narrowed[retained], original[retained])
            self.assertEqual(self.native["properties"][key], narrowed)
        reloaded = json.loads(STATIC_SCHEMAS.read_bytes())["schemas"]["EvolutionHistoryLegacyV2"]
        self.assertEqual(reloaded, self.original_wide)

    def test_exact_escaped_identifiers_empty_parent_and_evidence_tuple_are_accepted(self):
        value = self.payload()
        self.assertTrue(self.accepts(value))
        bridge.Draft7Validator(self.bound).validate(value)

    def test_missing_reordered_or_changed_frozen_values_are_rejected(self):
        valid = self.payload()
        invalid = []

        missing_evidence = deepcopy(valid)
        missing_evidence["evidenceIds"] = missing_evidence["evidenceIds"][:-1]
        invalid.append(missing_evidence)

        reordered_evidence = deepcopy(valid)
        reordered_evidence["evidenceIds"][2:4] = reversed(
            reordered_evidence["evidenceIds"][2:4])
        invalid.append(reordered_evidence)

        changed_evidence = deepcopy(valid)
        changed_evidence["evidenceIds"][4] = "evidence:invented"
        invalid.append(changed_evidence)

        missing_field = deepcopy(valid)
        del missing_field["evidenceIds"]
        invalid.append(missing_field)

        changed_budget = deepcopy(valid)
        changed_budget["effectBudget"] += 1
        invalid.append(changed_budget)

        for key in ("requestKey", "targetPersistentId", "nodeId", "effectId"):
            changed = deepcopy(valid)
            changed[key] += ":changed"
            invalid.append(changed)

        changed_parent = deepcopy(valid)
        changed_parent["parentNodeId"] = "invented-parent"
        invalid.append(changed_parent)

        for value in invalid:
            self.assertFalse(self.accepts(value), value)
            self.assertFalse(bridge.Draft7Validator(self.bound).is_valid(value), value)


if __name__ == "__main__":
    unittest.main()
