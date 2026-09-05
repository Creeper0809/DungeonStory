#!/usr/bin/env python3
"""Validate that public design sections have one player-wiki authority."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


H2_PATTERN = re.compile(r"^##\s+(.+?)\s*$", re.MULTILINE)
CHECKED_ITEM_PATTERN = re.compile(r"^- \[x\] \*\*(.+?)\*\*", re.MULTILINE)
FRONTMATTER_ID_PATTERN = re.compile(r"^id:\s*(\S+)\s*$", re.MULTILINE)


def load_json(path: Path) -> dict:
    with path.open(encoding="utf-8") as stream:
        return json.load(stream)


def normalise_heading(value: str) -> str:
    return value.strip()


def require_unique_sections(source_name: str, records: list[dict]) -> set[str]:
    sections: set[str] = set()
    ids: set[str] = set()
    for record in records:
        record_id = str(record.get("id", "")).strip()
        section = normalise_heading(str(record.get("section", "")))
        if not record_id or not section:
            raise ValueError(f"blank coverage record in {source_name}")
        if record_id in ids:
            raise ValueError(f"duplicate coverage id in {source_name}: {record_id}")
        if section in sections:
            raise ValueError(f"duplicate covered section in {source_name}: {section}")
        ids.add(record_id)
        sections.add(section)
    return sections


def validate_destinations(
    source_name: str,
    claims: list[dict],
    guide_ids: set[str],
    category_ids: set[str],
) -> None:
    for claim in claims:
        destinations = claim.get("destinations", [])
        if len(destinations) != 1:
            raise ValueError(
                f"public section must have exactly one authority: "
                f"{source_name} / {claim.get('section')}"
            )
        destination = destinations[0]
        kind = destination.get("kind")
        destination_id = destination.get("id")
        known_ids = guide_ids if kind == "guide" else category_ids if kind == "category" else None
        if known_ids is None or destination_id not in known_ids:
            raise ValueError(
                f"unknown coverage destination: {source_name} / "
                f"{claim.get('section')} -> {kind}:{destination_id}"
            )


def validate_partition(
    source_name: str,
    actual_sections: set[str],
    claims: list[dict],
    excluded: list[dict],
) -> None:
    claimed = require_unique_sections(source_name, claims)
    omitted = require_unique_sections(f"{source_name} excluded", excluded)
    overlap = claimed & omitted
    if overlap:
        raise ValueError(f"covered and excluded sections overlap in {source_name}: {sorted(overlap)}")
    for record in excluded:
        if not str(record.get("reason", "")).strip():
            raise ValueError(f"excluded section has no reason: {source_name} / {record.get('section')}")
    declared = claimed | omitted
    if declared != actual_sections:
        missing = sorted(actual_sections - declared)
        stale = sorted(declared - actual_sections)
        raise ValueError(
            f"coverage partition differs from source {source_name}; "
            f"missing={missing}; stale={stale}"
        )


def guide_ids_from(guides_root: Path) -> set[str]:
    result: set[str] = set()
    for path in guides_root.glob("*.md"):
        match = FRONTMATTER_ID_PATTERN.search(path.read_text(encoding="utf-8"))
        if match:
            result.add(match.group(1))
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate wiki source coverage.")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--game-version", required=True)
    args = parser.parse_args()

    root = args.repo_root.resolve()
    version_root = root / "wiki" / "game-versions" / args.game_version
    coverage_path = version_root / "content" / "guides" / "source-coverage.json"
    handbook_root = root / "docs_final" / "handbook"
    checklist_root = root / "docs_final"

    try:
        coverage = load_json(coverage_path)
        if coverage.get("schema_version") != 3 or coverage.get("game_version") != args.game_version:
            raise ValueError("source coverage schema or game version differs")

        guide_ids = guide_ids_from(version_root / "content" / "guides")
        categories = load_json(version_root / "data" / "navigation" / "categories.json")
        category_ids = {
            str(category.get("id"))
            for category in categories.get("categories", [])
            if category.get("id")
        }

        sources = coverage.get("sources", [])
        expected_handbooks = {f"{index:02d}-" for index in range(1, 10)}
        found_prefixes = {str(source.get("handbook", ""))[:3] for source in sources}
        if found_prefixes != expected_handbooks:
            raise ValueError(
                f"coverage must contain handbook 01 through 09 exactly once: {sorted(found_prefixes)}"
            )

        claim_count = 0
        for source in sources:
            handbook = str(source.get("handbook", ""))
            path = handbook_root / handbook
            if not path.is_file():
                raise ValueError(f"handbook source is missing: {handbook}")
            sections = {
                normalise_heading(value)
                for value in H2_PATTERN.findall(path.read_text(encoding="utf-8"))
            }
            claims = source.get("claims", [])
            excluded = source.get("excluded", [])
            validate_partition(handbook, sections, claims, excluded)
            validate_destinations(handbook, claims, guide_ids, category_ids)
            claim_count += len(claims)

        checklist = coverage.get("checklist", {})
        checklist_file = str(checklist.get("file", ""))
        checklist_path = checklist_root / checklist_file
        if not checklist_path.is_file():
            raise ValueError(f"checklist source is missing: {checklist_file}")
        checklist_text = checklist_path.read_text(encoding="utf-8")
        implementation_block = checklist_text.split("## 구현 확인", 1)[1].split("\n## ", 1)[0]
        checklist_sections = {
            normalise_heading(value)
            for value in CHECKED_ITEM_PATTERN.findall(implementation_block)
        }
        checklist_claims = checklist.get("claims", [])
        checklist_excluded = checklist.get("excluded", [])
        validate_partition(
            checklist_file,
            checklist_sections,
            checklist_claims,
            checklist_excluded,
        )
        validate_destinations(checklist_file, checklist_claims, guide_ids, category_ids)

        print(json.dumps({
            "status": "valid",
            "handbooks": len(sources),
            "handbook_public_sections": claim_count,
            "checklist_public_sections": len(checklist_claims),
            "authority_count_per_public_section": 1,
        }, ensure_ascii=False))
        return 0
    except (IndexError, OSError, ValueError, json.JSONDecodeError) as error:
        print(f"wiki source-coverage validation failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
