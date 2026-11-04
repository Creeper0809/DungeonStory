"""Generation-only Persona language boundaries, with no model quality claim."""
from copy import deepcopy
import importlib.util
import json
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest

from jsonschema import Draft7Validator
from jsonschema.exceptions import ValidationError


TOOLS_AI = Path(__file__).resolve().parent
PROJECT_ROOT = TOOLS_AI.parents[1]
RECIPE = PROJECT_ROOT / "Assets" / "StreamingAssets" / "R80" / "DungeonStoryLlm" / "recipe"
SOURCE_BRIDGE = TOOLS_AI / "r80_game_bridge.py"
for directory in (str(RECIPE), str(TOOLS_AI)):
    if directory not in sys.path:
        sys.path.insert(0, directory)

spec = importlib.util.spec_from_file_location("r80_persona_language_tools_source", SOURCE_BRIDGE)
bridge = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bridge)

from general_prototype_auxiliary_grammar import grammar
from live_module_selection_contract import _expand
from test_r80_name_grammar import regex_for


class PersonaLanguageTests(unittest.TestCase):
    def setUp(self):
        catalog = json.loads((RECIPE / "static-schemas.json").read_bytes())
        self.wide = deepcopy(catalog["schemas"]["Persona"])
        self.before = deepcopy(self.wide)
        self.native = bridge.persona_generation_schema(self.wide)
        rules = dict(line.split(" ::= ", 1) for line in grammar(self.native).splitlines() if line)
        self.pattern = regex_for(rules, "root")

    def accepts(self, name, flavor):
        wire = json.dumps({"personaName": name, "flavorText": flavor},
                          ensure_ascii=False, sort_keys=True)
        return bool(self.pattern.fullmatch(wire))

    def test_natural_korean_digits_latin_percent_and_name_punctuation(self):
        for name, flavor in (
            ("조용한 손님", "낯선 길을 천천히 살핀다."),
            ("기록 서기", "기록을 남긴다. 내일도 살펴본다!"),
            ("로웬 4", "로웬 4는 오늘도 주변을 살핀다."),
            ("Arin ５%", "Arin ５%는 조용히 길을 걷는다?"),
            ("기록의 손!?", "기록을 보며 주변을 천천히 살핀다."),
        ):
            with self.subTest(name=name, flavor=flavor):
                self.assertTrue(self.accepts(name, flavor))
                Draft7Validator(self.native).validate({"personaName": name, "flavorText": flavor})

    def test_character_and_final_sentence_boundaries(self):
        valid_name = "조용한 손님"
        valid_flavor = "낯선 길을 천천히 살핀다."
        for name, flavor in (
            ("기록吏", valid_flavor),
            ("㐀기록", valid_flavor),
            ("기록豈", valid_flavor),
            (valid_name, "그는 기록吏를 만난다."),
            (valid_name, "그는 기록을\n천천히 살핀다."),
            (valid_name, "그는 기록을\x01천천히 살핀다."),
            ('기록"손님', valid_flavor),
            ("기록\\손님", valid_flavor),
            ("기록\ud800손님", valid_flavor),
            (valid_name, "그는 기록을 천천히 살핀다"),
        ):
            with self.subTest(name=repr(name), flavor=repr(flavor)):
                self.assertFalse(self.accepts(name, flavor))
        raw_escape = '{"flavorText":"낯선 길을 천천히 살핀다.","personaName":"\\uAE30록"}'
        self.assertEqual(json.loads(raw_escape)["personaName"], "기록")
        self.assertFalse(self.pattern.fullmatch(raw_escape))

    def test_lengths_are_narrowed_only_for_generation(self):
        self.assertEqual(self.wide, self.before)
        self.assertIsNot(self.native, self.wide)
        self.assertEqual((self.wide["properties"]["personaName"]["maxLength"],
                          self.wide["properties"]["flavorText"]["maxLength"]), (40, 180))
        self.assertEqual((self.native["properties"]["personaName"]["minLength"],
                          self.native["properties"]["personaName"]["maxLength"]), (2, 14))
        self.assertEqual((self.native["properties"]["flavorText"]["minLength"],
                          self.native["properties"]["flavorText"]["maxLength"]), (8, 96))
        for name in ("가", "가" * 15, "가" * 40):
            self.assertFalse(self.accepts(name, "가" * 7 + "."))
        for flavor in ("가" * 6 + ".", "가" * 96 + ".", "가" * 179 + "."):
            self.assertFalse(self.accepts("가나", flavor))
        self.assertTrue(self.accepts("가" * 2, "가" * 7 + "."))
        self.assertTrue(self.accepts("가" * 14, "가" * 95 + "."))
        for payload in (
            {"personaName": "가" * 15, "flavorText": "가" * 7 + "."},
            {"personaName": "가나", "flavorText": "가" * 96 + "."},
        ):
            Draft7Validator(self.wide).validate(payload)
            self.assertFalse(Draft7Validator(self.native).is_valid(payload))
        self.assertEqual(self.wide, self.before)

    def test_original_narrower_ranges_are_intersected_and_empty_ranges_fail(self):
        original = deepcopy(self.wide)
        original["properties"]["personaName"].update(minLength=3, maxLength=10)
        original["properties"]["flavorText"].update(minLength=12, maxLength=30)
        snapshot = deepcopy(original)
        native = bridge.persona_generation_schema(original)
        self.assertEqual(original, snapshot)
        for key, bounds in (("personaName", (3, 10)), ("flavorText", (12, 30))):
            self.assertEqual((native["properties"][key]["minLength"],
                              native["properties"][key]["maxLength"]), bounds)
        for key, field, value in (("personaName", "minLength", 15),
                                  ("flavorText", "maxLength", 7)):
            impossible = deepcopy(self.wide)
            impossible["properties"][key][field] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                bridge.persona_generation_schema(impossible)

    def test_unknown_fields_and_existing_string_constraints_fail_explicitly(self):
        extra = deepcopy(self.wide)
        extra["properties"]["other"] = {"type": "string"}
        extra["required"].append("other")
        with self.assertRaises(ValueError):
            bridge.persona_generation_schema(extra)
        for key in ("personaName", "flavorText"):
            for constraint, value in (("pattern", "^가+$"), ("enum", ["가나"]), ("const", "가나")):
                schema = deepcopy(self.wide)
                schema["properties"][key][constraint] = value
                with self.subTest(key=key, constraint=constraint), self.assertRaises(ValueError):
                    bridge.persona_generation_schema(schema)


class PersonaReadableMessagesTests(unittest.TestCase):
    @staticmethod
    def encoded_document():
        return {
            "encoding": "repeated-public-text-v1",
            "strings": ["character:world:1:000001", "정확한 ", "기록"],
            "data": {
                "publicFacts": {
                    "columns": ["subjectId", "text", "amount", "optional"],
                    "columnValues": [
                        {"$repeat": {"$s": 0}},
                        [{"$text": [{"$s": 1}, {"$s": 2}]}, None],
                        ["0.10000000000000002", "0.1234567890123456789012345678"],
                        {"$repeat": None},
                    ],
                    "rowCount": 2,
                },
                "publicNarrativeContext": {
                    "subjectId": {"$s": 0},
                    "backgroundFactIds": [1, 2],
                    "events": {
                        "columns": ["factId", "note"],
                        "columnValues": [[], []],
                        "rowCount": 0,
                    },
                },
                "opaqueExtension": {"unknown": [None, {"value": 17}], "enabled": False},
            },
        }

    @staticmethod
    def restore_tables(value):
        if isinstance(value, list):
            return [PersonaReadableMessagesTests.restore_tables(item) for item in value]
        if not isinstance(value, dict):
            return value
        if set(value) == {"columns", "records"}:
            columns = value["columns"]
            records = value["records"]
            for record in records:
                if list(record) != columns:
                    raise AssertionError("table columns or their order changed")
            return {
                "columns": columns,
                "rows": [[PersonaReadableMessagesTests.restore_tables(record[column])
                          for column in columns] for record in records],
            }
        return {key: PersonaReadableMessagesTests.restore_tables(item)
                for key, item in value.items()}

    def test_all_documents_and_crlf_caller_lines_are_preserved(self):
        first = self.encoded_document()
        second = deepcopy(first)
        second["data"]["opaqueExtension"]["second"] = "another document"
        line1 = json.dumps(first, ensure_ascii=False, separators=(",", ":"))
        line2 = json.dumps(second, ensure_ascii=False, separators=(",", ":"))
        headers = "Persona header\r\nline before facts\r\n"
        between = "\r\nline between facts\r\n"
        tail = "line after facts\r\n"
        messages = [
            {"role": "system", "content": "원 시스템", "metadata": {"list": [1]}},
            {"role": "user", "content": headers + line1 + "\r\n" + between
             + line2 + "\r\n" + tail, "metadata": {"trace": ["original"]}},
        ]
        original = deepcopy(messages)
        result = bridge.persona_readable_messages(messages)
        self.assertEqual(messages, original)
        self.assertEqual(result[0], original[0])
        self.assertEqual(result[1]["role"], "user")
        self.assertEqual(result[1]["metadata"], original[1]["metadata"])
        packet = json.loads(result[1]["content"])
        self.assertEqual(set(packet), {"profile", "callerInstructions", "publicInputDocuments"})
        self.assertEqual(packet["profile"], "Persona")
        self.assertEqual(packet["callerInstructions"], headers + between + tail)
        self.assertEqual([self.restore_tables(document) for document in packet["publicInputDocuments"]],
                         [_expand(first), _expand(second)])
        result[0]["metadata"]["list"].append(2)
        result[1]["metadata"]["trace"].append("changed")
        self.assertEqual(messages, original)

    def test_invalid_references_and_row_counts_fail_like_existing_decoder(self):
        bad_reference = self.encoded_document()
        bad_reference["data"]["publicNarrativeContext"]["subjectId"] = {"$s": 99}
        bad_rows = self.encoded_document()
        bad_rows["data"]["publicFacts"]["rowCount"] = 3
        for document in (bad_reference, bad_rows):
            with self.subTest(document=document):
                with self.assertRaises(ValueError) as expected:
                    _expand(document)
                content = "header\r\n" + json.dumps(document, ensure_ascii=False) + "\r\n"
                with self.assertRaises(type(expected.exception)):
                    bridge.persona_readable_messages([{"role": "user", "content": content}])

    def test_plain_messages_are_deep_copied_without_content_changes(self):
        messages = [{"role": "system", "content": "system\r\n", "meta": {"x": [1]}},
                    {"role": "user", "content": "plain\r\n{\"other\":true}\r\n"}]
        before = deepcopy(messages)
        result = bridge.persona_readable_messages(messages)
        self.assertEqual(result, before)
        self.assertIsNot(result, messages)
        result[0]["meta"]["x"].append(2)
        self.assertEqual(messages, before)


if __name__ == "__main__":
    unittest.main()
