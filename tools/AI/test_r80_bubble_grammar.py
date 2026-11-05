"""Generation-time BubbleLine length checks against the Tools bridge source."""
from copy import deepcopy
import importlib.util
import json
from pathlib import Path
import sys
import unittest

from jsonschema import Draft7Validator
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

# Aggregate discovery may have imported the bundled bridge first. Always load
# the current Tools source under a private name for this source-level fixture.
SOURCE_SPEC = importlib.util.spec_from_file_location(
    "r80_bubble_grammar_tools_source", SOURCE_BRIDGE)
bridge = importlib.util.module_from_spec(SOURCE_SPEC)
SOURCE_SPEC.loader.exec_module(bridge)

from test_r80_name_grammar import regex_for


class BubbleGrammarTests(unittest.TestCase):
    def setUp(self):
        schemas = json.loads(STATIC_SCHEMAS.read_bytes())["schemas"]
        self.wide = deepcopy(schemas["BubbleLine"])
        self.wide_before = deepcopy(self.wide)
        self.narrow = deepcopy(self.wide)
        self.narrow["properties"]["line"]["maxLength"] = 24
        self.narrow_before = deepcopy(self.narrow)
        self.native, decoder, self.constraint = bridge.decoder_for(
            self.narrow, lambda native: native, "auxiliary")
        rules = dict(line.split(" ::= ", 1)
                     for line in decoder["grammar"].splitlines() if line)
        self.pattern = regex_for(rules, "root")

    def accepts(self, line):
        payload = json.dumps({"line": line}, ensure_ascii=False, sort_keys=True)
        return bool(self.pattern.fullmatch(payload))

    def test_actual_wide_schema_and_decoder_input_remain_unchanged(self):
        self.assertEqual(Path(bridge.__file__).resolve(), SOURCE_BRIDGE)
        self.assertEqual(self.wide, self.wide_before)
        self.assertEqual(self.wide["properties"]["line"]["maxLength"], 80)
        self.assertEqual(self.narrow, self.narrow_before)
        self.assertEqual(self.narrow["properties"]["line"]["maxLength"], 24)
        self.assertEqual(self.native, self.narrow_before)
        self.assertIsNot(self.native, self.narrow)
        self.assertTrue(self.constraint["originalSchemaUnchanged"])
        self.assertFalse(self.constraint["outputRepair"])
        self.assertFalse(self.constraint["semanticChoicesComputedByCode"])
        reloaded = json.loads(STATIC_SCHEMAS.read_bytes())["schemas"]["BubbleLine"]
        self.assertEqual(reloaded, self.wide_before)

    def test_twentyfour_character_shape_accepts_natural_and_escaped_examples(self):
        # These are grammar examples, not a claim that automated checks approve
        # the model's sentence quality.
        examples = (
            "괜찮아, 함께 가자!",
            '그는 "별"\\길을 봤다.',
            "달빛 아래 쉬어 가자.",
        )
        for line in examples:
            with self.subTest(line=line):
                self.assertLessEqual(len(line), 24)
                self.assertTrue(self.accepts(line))
                Draft7Validator(self.narrow).validate({"line": line})

    def test_twentyfive_characters_are_rejected_only_by_narrowed_generation_schema(self):
        line = "가" * 25
        self.assertFalse(self.accepts(line))
        with self.assertRaises(ValidationError):
            Draft7Validator(self.narrow).validate({"line": line})
        Draft7Validator(self.wide).validate({"line": line})
        self.assertEqual(self.wide["properties"]["line"]["maxLength"], 80)


if __name__ == "__main__":
    unittest.main()
