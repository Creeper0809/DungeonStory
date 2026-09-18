#!/usr/bin/env python3

from __future__ import annotations

from collections import Counter
import unittest

import generate_content_database as content_db


def building_row(data: dict) -> content_db.ContentRow:
    values = {
        name: ""
        for name in content_db.ContentRow.__dataclass_fields__
        if name not in {"incoming_reference_count", "data", "authored_fields"}
    }
    values.update(
        group="production-facilities",
        content_type="BuildingSO",
        record_key="BuildingSO|building:test-five-materials",
        stable_id="building:test-five-materials",
        display_name="다섯 재료 시험 시설",
        incoming_reference_count=0,
        data=data,
    )
    return content_db.ContentRow(**values)


class BuildingConstructionDescriptionTests(unittest.TestCase):
    def test_preserves_every_construction_material_and_wu_unit(self) -> None:
        row = building_row(
            {
                "references": {
                    "RefIds": [
                        {
                            "type": {"class": "BuildingWorkAmountAbility"},
                            "data": {
                                "constructionWorkRequired": 12.5,
                                "constructionMaterials": [
                                    {"itemId": "material:first", "amount": 1},
                                    {"itemId": "material:second", "amount": 2},
                                    {"itemId": "material:third", "amount": 3},
                                    {"itemId": "material:fourth", "amount": 4},
                                    {"itemId": "material:fifth", "amount": 5},
                                ],
                            },
                        }
                    ]
                }
            }
        )

        description, _, review_status = content_db.reason_for(
            row, Counter(), Counter(), Counter()
        )

        self.assertEqual("근거 확인", review_status)
        self.assertIn("작업량 12.5 WU", description)
        self.assertIn("material:fifth×5", description)
        self.assertNotIn("외 1개", description)


if __name__ == "__main__":
    unittest.main()
