"""Focused tests for pilot counting and public-offer parsing; no Unity required."""
import json
from pathlib import Path
import tempfile
import unittest

from profile_pilot_requests import Inputs, load_package, offers_from_wire, unique_requests


class PilotProfileTests(unittest.TestCase):
    def test_removed_producer_requires_explicit_history_and_never_counts_current(self):
        qa = Path(__file__).resolve().parents[2] / "Artifacts" / "QA"
        with tempfile.TemporaryDirectory(prefix="pilot-code-currency-", dir=qa) as directory:
            inputs = Inputs(Path(directory))
            descriptor = dict(path="removed.cs", bytes=10, sha256="sha256:" + "0" * 64)
            with self.assertRaises(FileNotFoundError):
                inputs.code_changed(descriptor)
            self.assertTrue(inputs.code_changed(descriptor, include_historical_code=True))
            with self.assertRaises(ValueError):
                inputs.code_changed(dict(descriptor, path="../removed.cs"), include_historical_code=True)

    def test_compact_offers_preserve_positive_and_drawback_roles(self):
        table = {"C_columns": ["positive_M", "drawback_M"], "C": [[[0, 1], [2]], [[0], []]],
                 "M": [["buff", "강화"], ["heal", "회복"], ["strain", "부담"]]}
        wire = {"messages": [{"content": json.dumps(table)}]}
        self.assertEqual(offers_from_wire(wire), {"positive": ["buff", "heal"], "drawback": ["strain"]})

    def test_both_plain_module_formats(self):
        wire = {"messages": [{"content": "- moduleId=trait:x; polarity=Positive; meaning=x\n"
                                         "module=facility:y | polarity=Drawback | y"}]}
        self.assertEqual(offers_from_wire(wire), {"positive": ["trait:x"], "drawback": ["facility:y"]})

    def test_unknown_format_is_not_silently_zero_choices(self):
        with self.assertRaises(ValueError):
            offers_from_wire({"messages": [{"content": "unexpected new format"}]})

    def test_historical_ids_without_polarity_remain_unspecified(self):
        wire = {'messages': [{'content': 'module=facility:drawback-x | title\nmodule=facility:y | title'}]}
        self.assertEqual(offers_from_wire(wire), dict(positive=[], drawback=[],
                         polarityUnspecified=['facility:drawback-x', 'facility:y']))

    def test_historical_skill_sections_do_not_leak_to_other_fields(self):
        wire = {'messages': [{'content': 'positiveModules:\n- id=heal; meaning=회복\n'
                 'drawbackModules:\n- id=tired; meaning=피로\n'
                 'evidenceFactIds=x\n- id=not-an-offer; meaning=irrelevant'}]}
        self.assertEqual(offers_from_wire(wire), dict(positive=['heal'], drawback=['tired']))

    def test_count_requests_not_candidates_or_repeat_exports(self):
        def row(identity, wire):
            return dict(requestKey=identity, wire={"sha256": wire}, selectionSourceId=identity, candidateCount=3)
        unique, duplicates = unique_requests([row("a", "1"), row("a", "2"), row("b", "1"), row("c", "3")])
        self.assertEqual(len(unique), 2)
        self.assertEqual(len(duplicates), 2)

    def test_fixture_and_path_escape_cannot_enter_pilot(self):
        qa = Path(__file__).resolve().parents[2] / "Artifacts" / "QA"
        with tempfile.TemporaryDirectory(prefix="pilot-profiler-unit-", dir=qa) as directory:
            root = Path(directory)
            manifest = root / "manifest.json"
            manifest.write_text(json.dumps({"schema": "dungeonstory-gameplay-module-selection-source@9",
                                            "sourceTier": "integration-fixture", "integrationFixture": True}), encoding="utf-8")
            inputs = Inputs(root)
            with self.assertRaises(ValueError):
                inputs.path(root, "../outside.json")
            with self.assertRaises(ValueError):
                load_package(inputs, manifest)


if __name__ == "__main__":
    unittest.main()
