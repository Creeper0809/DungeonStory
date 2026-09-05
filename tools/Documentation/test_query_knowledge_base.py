#!/usr/bin/env python3

from __future__ import annotations

import unittest

import query_knowledge_base as query


class QueryKnowledgeBaseTests(unittest.TestCase):
    def test_all_terms_must_exist_across_the_row(self) -> None:
        row = {
            "state_family": "창고 재고",
            "runtime_write_authority": "WarehouseInventory",
        }
        phrase, terms = query.normalize_terms("창고 WarehouseInventory")
        result = query.score_row(row, phrase, terms)
        self.assertIsNotNone(result)
        self.assertIsNone(query.score_row(row, *query.normalize_terms("창고 missing")))

    def test_exact_identity_match_scores_above_description_match(self) -> None:
        exact = {"stable_id": "material:iron", "description": "metal"}
        descriptive = {"stable_id": "other", "description": "uses material:iron"}
        phrase, terms = query.normalize_terms("material:iron")
        exact_score, _ = query.score_row(exact, phrase, terms) or (0, [])
        descriptive_score, _ = query.score_row(descriptive, phrase, terms) or (0, [])
        self.assertGreater(exact_score, descriptive_score)

    def test_compact_row_keeps_identity_match_and_trace(self) -> None:
        row = {
            "stable_id": "material:iron",
            "description": "x" * 100,
            "source_path": "Assets/Resources/Iron.asset",
            "unrelated": "omit-me",
        }
        result, truncated = query.compact_row(row, ["description"], 40)
        self.assertEqual("material:iron", result["stable_id"])
        self.assertEqual("Assets/Resources/Iron.asset", result["source_path"])
        self.assertNotIn("unrelated", result)
        self.assertEqual(["description"], truncated)

    def test_markdown_includes_fresh_source_digest(self) -> None:
        rendered = query.render_markdown(
            {
                "status": "fresh",
                "freshness": {
                    "failure_count": 0,
                    "artifacts": [
                        {
                            "artifact_kind": "test-artifact",
                            "source_digest": "abc123",
                        }
                    ],
                },
                "available_areas": ["code"],
            }
        )
        self.assertIn("test-artifact", rendered)
        self.assertIn("abc123", rendered)

    def test_markdown_zero_hit_forbids_absence_conclusion(self) -> None:
        rendered = query.render_markdown(
            {
                "status": "fresh",
                "freshness": {"failure_count": 0, "artifacts": []},
                "query": "missing",
                "selected_areas": ["code"],
                "returned_hit_count": 0,
                "total_match_count": 0,
                "hits": [],
            }
        )
        self.assertIn("not evidence of absence", rendered)


if __name__ == "__main__":
    unittest.main()
