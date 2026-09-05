#!/usr/bin/env python3
"""Validate a generated, version-scoped DungeonStory wiki model."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+v$")
PUBLIC_CATEGORY_KIND = {
    "characters": "character",
    "equipment": "combat",
    "events": "event",
    "facilities": "facility",
    "items": "item",
    "health": "medical",
    "nature": "nature",
    "recipes": "recipe",
    "research": "research",
    "world": "world",
}
DEFENSIVE_PROSE_MARKERS = ("아니다", "아니라", "아닌")
UNNATURAL_PUBLIC_PROSE_MARKERS = (
    "외부 활동",
    "운영이 움직이는 순서",
    "정착이 커지며 달라지는 일",
    "축산과 야생은 별도의 부담을 가진다",
    "비축은 실패를 버티는 시간이다",
    "선택은 진행 중인 작업이 될 수 있다",
    "결과는 다음 상황에 남는다",
    "기반망",
    "공급망별로 보는 것",
    "시설의 사용 기록이 만드는 후보",
    "재조율",
    "주문 하나가 움직이는 순서",
    "생산선이 나누는 비용",
    "통로는 작업 순서를 드러낸다",
    "해금은 운영 준비와 연결된다",
    "연구를 운영에 넣는 과정",
    "멈춘 작업은 이 순서로 읽는다",
    "조건을 고친 뒤 볼 변화",
    "책임 상태",
    "책임 아래",
    "해부 프로필",
      "공통으로 적용되는 건강 시스템",
      "바깥 부위",
      "원하는 정보로 바로 가기",
      "궁금한 것",
      "확인하는 내용",
      "함께 볼 문서",
      "먼저 볼 것",
      "보는 순서",
      "읽는 법",
      "확인할 순서",
      "다음으로 볼 문서",
)


def load(path: Path):
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def find_public_prose_marker(value):
    if isinstance(value, str):
        markers = (*DEFENSIVE_PROSE_MARKERS, *UNNATURAL_PUBLIC_PROSE_MARKERS)
        return next((marker for marker in markers if marker in value), None)
    if isinstance(value, dict):
        for nested in value.values():
            marker = find_public_prose_marker(nested)
            if marker:
                return marker
    if isinstance(value, list):
        for nested in value:
            marker = find_public_prose_marker(nested)
            if marker:
                return marker
    return None


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate the DungeonStory player-wiki model.")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--game-version", required=True)
    args = parser.parse_args()
    root = args.repo_root.resolve()
    version = args.game_version
    try:
        if not VERSION_PATTERN.fullmatch(version):
            raise ValueError("invalid game version")
        planned = (root / "docs" / "wiki" / "GAME_VERSION").read_text(encoding="utf-8").strip()
        registry = load(root / "wiki" / "game-versions" / "registry.json")
        version_root = root / "wiki" / "game-versions" / version
        version_meta = load(version_root / "game-version.json")
        manifest = load(version_root / "data" / "manifest.json")
        report = load(version_root / "data" / "qa" / "publication-report.json")
        if not (planned == version == registry.get("current_game_version") == version_meta.get("game_version") == manifest.get("game_version")):
            raise ValueError("game-version authority disagreement")
        if report.get("published_record_count") != manifest.get("counts", {}).get("entities"):
            raise ValueError("entity count does not match publication report")
        entities = []
        for path in sorted((version_root / "data" / "entities").rglob("*.json")):
            entity = load(path)
            required = {"schema_version", "game_version", "kind", "slug", "title", "summary", "facts", "spoiler_tier", "relations"}
            if required.difference(entity):
                raise ValueError(f"entity contract is incomplete: {path}")
            if entity["kind"] == "item":
                if not isinstance(entity.get("in_game_description"), str) or not entity["in_game_description"].strip():
                    raise ValueError(f"item in-game description is missing: {path}")
            elif "in_game_description" in entity:
                raise ValueError(f"non-item entity exposes an item in-game description: {path}")
            if entity["game_version"] != version or entity["spoiler_tier"] not in {"none", "warning"}:
                raise ValueError(f"entity version or spoiler contract is invalid: {path}")
            if any(key in entity for key in ("source_path", "asset_guid", "record_key")):
                raise ValueError(f"source-only field leaked into entity model: {path}")
            marker = find_public_prose_marker(entity)
            if marker:
                raise ValueError(f"public prose marker in entity: {path} ({marker})")
            entities.append(entity)
        if len(entities) != manifest["counts"]["entities"]:
            raise ValueError("entity file count does not match manifest")
        item_narrative_coverage = load(version_root / "data" / "qa" / "item-narrative-coverage.json")
        item_entities = [entity for entity in entities if entity["kind"] == "item"]
        if item_narrative_coverage.get("schema_version") != 1:
            raise ValueError("item narrative coverage schema is invalid")
        if any(item_narrative_coverage.get(key) for key in ("missing_ids", "orphan_ids", "duplicate_texts")):
            raise ValueError("item narrative coverage contains missing, orphan or duplicate text")
        if item_narrative_coverage.get("banned_fragment_count") != 0:
            raise ValueError("item narrative coverage contains banned template text")
        if item_narrative_coverage.get("missing_lore_count") != 0:
            raise ValueError("item narrative coverage contains missing world links")
        if item_narrative_coverage.get("world_grounded_count") != len(item_entities):
            raise ValueError("item narrative world-grounding count does not match the item set")
        if item_narrative_coverage.get("distinct_lore_sentence_count", 0) < 900:
            raise ValueError("item narrative lore-sentence variety is below the reviewed threshold")
        if item_narrative_coverage.get("distinct_lore_frame_count", 0) < 250:
            raise ValueError("item narrative lore-frame variety is below the reviewed threshold")
        largest_lore_frames = item_narrative_coverage.get("largest_lore_frames")
        if not isinstance(largest_lore_frames, list) or any(row.get("count", 0) > 20 for row in largest_lore_frames):
            raise ValueError("an item narrative lore frame is repeated too often")
        reporting_style_terms = item_narrative_coverage.get("reporting_style_term_counts")
        if not isinstance(reporting_style_terms, dict) or any(count > 40 for count in reporting_style_terms.values()):
            raise ValueError("item narrative prose overuses report-like vocabulary")
        if item_narrative_coverage.get("average_description_chars", 999) > 105:
            raise ValueError("item narrative average length exceeds the reviewed limit")
        if item_narrative_coverage.get("max_description_chars", 999) > 180:
            raise ValueError("an item narrative exceeds the reviewed length limit")
        lore_anchor_counts = item_narrative_coverage.get("lore_anchor_counts")
        lore_connection_counts = item_narrative_coverage.get("lore_connection_counts")
        story_layer_counts = item_narrative_coverage.get("story_layer_counts")
        if not all(isinstance(value, dict) for value in (lore_anchor_counts, lore_connection_counts, story_layer_counts)):
            raise ValueError("item narrative world-grounding distributions are missing")
        if any(sum(value.values()) != len(item_entities) for value in (lore_anchor_counts, lore_connection_counts, story_layer_counts)):
            raise ValueError("item narrative world-grounding distributions do not cover every item")
        if item_narrative_coverage.get("distinct_opening_frame_count", 0) < 900:
            raise ValueError("item narrative opening-frame variety is below the reviewed threshold")
        largest_frames = item_narrative_coverage.get("largest_opening_frames")
        if not isinstance(largest_frames, list) or any(row.get("count", 0) > 12 for row in largest_frames):
            raise ValueError("an item narrative opening frame is repeated too often")
        if not (
            len(item_entities)
            == item_narrative_coverage.get("target_count")
            == item_narrative_coverage.get("description_count")
            == item_narrative_coverage.get("unique_description_count")
            == report.get("item_narrative_count")
        ):
            raise ValueError("item narrative counts do not match the published item set")
        urls = {(item["kind"], item["slug"]) for item in entities}
        if len(urls) != len(entities):
            raise ValueError("duplicate public entity URL")
        for entity in entities:
            for relation in entity["relations"]:
                if (relation["target_kind"], relation["target_slug"]) not in urls:
                    raise ValueError(f"broken public relation from {entity['kind']}/{entity['slug']}")
                if ("item", relation["target_slug"]) in urls and relation["target_kind"] != "item":
                    raise ValueError(
                        f"physical item relation bypasses item authority from {entity['kind']}/{entity['slug']} "
                        f"to {relation['target_kind']}/{relation['target_slug']}"
                    )
        canonical_targets = load(version_root / "data" / "qa" / "canonical-relation-targets.json")
        if canonical_targets.get("schema_version") != 1 or not isinstance(canonical_targets.get("records"), list):
            raise ValueError("canonical relation target audit is missing or invalid")
        if report.get("canonical_relation_target_count") != len(canonical_targets["records"]):
            raise ValueError("canonical relation target count does not match publication report")
        for record in canonical_targets["records"]:
            candidate_kinds = {candidate.get("kind") for candidate in record.get("candidates", [])}
            if len(candidate_kinds) < 2:
                raise ValueError(f"canonical relation target is not cross-kind: {record.get('stable_id')}")
            if "item" in candidate_kinds and record.get("canonical_kind") != "item":
                raise ValueError(f"physical item is not canonical relation target: {record.get('stable_id')}")
        guide_root = version_root / "content" / "guides"
        public_prose_paths = [
            *sorted(
                path
                for path in (version_root / "content").rglob("*")
                if path.is_file()
                and path.suffix in {".json", ".md", ".yml"}
                and path.name not in {"source-coverage.json"}
            ),
            *sorted((root / "wiki" / "src").rglob("*.astro")),
            *sorted((root / "wiki" / "src").rglob("*.ts")),
        ]
        for path in public_prose_paths:
            marker = find_public_prose_marker(path.read_text(encoding="utf-8"))
            if marker:
                raise ValueError(f"public prose marker: {path.relative_to(root)} ({marker})")
        guide_sources = {}
        for path in guide_root.glob("*.md"):
            source = path.read_text(encoding="utf-8")
            guide_id = source.split("---", 2)[1].split("id:", 1)[1].splitlines()[0].strip()
            if guide_id in guide_sources:
                raise ValueError(f"duplicate guide ID: {guide_id}")
            guide_sources[guide_id] = source
        guide_ids = set(guide_sources)
        work_references = load(version_root / "content" / "work-references.json")
        if work_references.get("schema_version") != 1 or work_references.get("game_version") != version:
            raise ValueError("work-reference document version is invalid")
        work_items = work_references.get("references")
        if not isinstance(work_items, list) or not work_items:
            raise ValueError("work-reference document has no references")
        work_ids = [item.get("id") for item in work_items]
        if any(not item_id for item_id in work_ids) or len(set(work_ids)) != len(work_ids):
            raise ValueError("work-reference document has invalid reference IDs")
        work_task_urls: list[str] = []
        for work in work_items:
            if not all(isinstance(work.get(field), str) and work[field] for field in ("id", "title", "summary")):
                raise ValueError(f"work-reference entry is incomplete: {work.get('id')}")
            tasks = work.get("tasks")
            if not isinstance(tasks, list) or not tasks:
                raise ValueError(f"work-reference has no tasks: {work['id']}")
            task_ids = [task.get("id") for task in tasks]
            if any(not task_id for task_id in task_ids) or len(set(task_ids)) != len(task_ids):
                raise ValueError(f"work-reference has invalid task IDs: {work['id']}")
            work_task_urls.extend(f"{work['id']}/{task_id}" for task_id in task_ids)
            for task in tasks:
                if not all(isinstance(task.get(field), str) and task[field] for field in ("id", "title", "summary", "prepare", "check")):
                    raise ValueError(f"work task is incomplete: {work['id']}/{task.get('id')}")
            proficiency = work.get("proficiency")
            if proficiency is not None and (not isinstance(proficiency, dict) or not all(proficiency.get(field) for field in ("title", "kind", "slug"))):
                raise ValueError(f"work-reference proficiency is invalid: {work['id']}")
        if len(set(work_task_urls)) != len(work_task_urls):
            raise ValueError("work-reference document has duplicate task URLs")
        categories = load(version_root / "data" / "navigation" / "categories.json")["categories"]
        category_ids = {category["id"] for category in categories}
        if category_ids != set(PUBLIC_CATEGORY_KIND):
            raise ValueError("public categories do not match the player-facing catalogue map")
        category_entity_urls = set()
        for category in categories:
            category_id = category["id"]
            entries = category.get("entries", [])
            expected_kind = PUBLIC_CATEGORY_KIND[category_id]
            if not entries or category.get("entry_count") != len(entries):
                raise ValueError(f"public category has an invalid entry count: {category_id}")
            if any(entry.get("kind") != expected_kind for entry in entries):
                raise ValueError(f"public category mixes entity kinds: {category_id}")
            for entry in entries:
                category_entity_urls.add(f"/db/{entry['kind']}/{entry['slug']}/")
        entity_urls = {f"/db/{entity['kind']}/{entity['slug']}/" for entity in entities}
        if category_entity_urls != entity_urls:
            raise ValueError("public category entries do not cover every entity exactly once")
        coverage = load(guide_root / "source-coverage.json")
        expected_handbooks = {
            "01-game-vision-and-player-loop.md",
            "02-system-map-and-authority.md",
            "03-world-building-facilities-environment.md",
            "04-items-production-logistics-economy.md",
            "05-characters-ai-society-health.md",
            "06-research-progression-and-strategy.md",
            "07-combat-invasions-expeditions-factions.md",
            "08-content-events-and-authoring.md",
            "09-save-restore-determinism-and-validation.md",
        }
        source_map = {entry["handbook"]: entry for entry in coverage["sources"]}
        if coverage.get("schema_version") != 3 or coverage.get("game_version") != version or set(source_map) != expected_handbooks:
            raise ValueError("source coverage ledger is incomplete")
        for handbook, source in source_map.items():
            claims = source.get("claims")
            if not isinstance(claims, list) or not claims:
                raise ValueError(f"source coverage has no public claim groups: {handbook}")
            claim_ids = [claim.get("id") for claim in claims]
            if any(not claim_id for claim_id in claim_ids) or len(set(claim_ids)) != len(claim_ids):
                raise ValueError(f"source coverage has invalid claim IDs: {handbook}")
            claim_sections = [claim.get("section") for claim in claims]
            if any(not isinstance(section, str) or not section for section in claim_sections) or len(set(claim_sections)) != len(claim_sections):
                raise ValueError(f"source coverage has invalid section names: {handbook}")
            for claim in claims:
                destinations = claim.get("destinations")
                if not isinstance(destinations, list) or len(destinations) != 1:
                    raise ValueError(f"source coverage claim must have one authority destination: {handbook}/{claim['id']}")
                for destination in destinations:
                    target_kind = destination.get("kind")
                    target_id = destination.get("id")
                    if target_kind == "guide" and target_id in guide_ids:
                        continue
                    if target_kind == "category" and target_id in category_ids:
                        continue
                    if target_kind == "work" and target_id in work_ids:
                        continue
                    raise ValueError(f"source coverage references an unknown destination: {handbook}/{claim['id']}")
            exclusions = source.get("excluded", [])
            excluded_sections = [exclusion.get("section") for exclusion in exclusions]
            if any(not isinstance(section, str) or not section for section in excluded_sections) or len(set(excluded_sections)) != len(excluded_sections):
                raise ValueError(f"source coverage has invalid excluded section names: {handbook}")
            for exclusion in exclusions:
                if not exclusion.get("id") or not exclusion.get("reason") or not exclusion.get("section"):
                    raise ValueError(f"source coverage exclusion is incomplete: {handbook}")
            handbook_sections = [
                match.group(1).strip()
                for match in re.finditer(r"^##\s+(.+?)\s*$", (root / "docs_final" / "handbook" / handbook).read_text(encoding="utf-8"), re.MULTILINE)
            ]
            covered_sections = set(claim_sections) | set(excluded_sections)
            if set(handbook_sections) != covered_sections or len(handbook_sections) != len(covered_sections):
                raise ValueError(f"source coverage does not map each handbook section once: {handbook}")
        checklist = coverage.get("checklist")
        if not isinstance(checklist, dict) or checklist.get("file") != "system-implementation-checklist.md":
            raise ValueError("source coverage has no implementation-checklist ledger")
        checklist_claims = checklist.get("claims")
        checklist_exclusions = checklist.get("excluded", [])
        if not isinstance(checklist_claims, list) or not checklist_claims:
            raise ValueError("source coverage has no public implementation-checklist groups")
        checklist_claim_sections = [claim.get("section") for claim in checklist_claims]
        checklist_excluded_sections = [exclusion.get("section") for exclusion in checklist_exclusions]
        checklist_declared_sections = checklist_claim_sections + checklist_excluded_sections
        if (
            any(not isinstance(section, str) or not section for section in checklist_declared_sections)
            or len(set(checklist_declared_sections)) != len(checklist_declared_sections)
        ):
            raise ValueError("source coverage has invalid implementation-checklist sections")
        for claim in checklist_claims:
            destinations = claim.get("destinations")
            if not isinstance(destinations, list) or len(destinations) != 1:
                raise ValueError(
                    f"implementation-checklist claim must have one authority destination: {claim.get('id')}"
                )
            destination = destinations[0]
            target_kind = destination.get("kind")
            target_id = destination.get("id")
            if target_kind == "guide" and target_id in guide_ids:
                continue
            if target_kind == "category" and target_id in category_ids:
                continue
            raise ValueError(
                f"implementation-checklist references an unknown destination: {claim.get('id')}"
            )
        for exclusion in checklist_exclusions:
            if not exclusion.get("id") or not exclusion.get("reason") or not exclusion.get("section"):
                raise ValueError("implementation-checklist exclusion is incomplete")
        checklist_text = (root / "docs_final" / checklist["file"]).read_text(encoding="utf-8")
        implementation_block = checklist_text.split("## 구현 확인", 1)[1].split("\n## ", 1)[0]
        checklist_sections = [
            match.group(1).strip()
            for match in re.finditer(r"^- \[x\] \*\*(.+?)\*\*", implementation_block, re.MULTILINE)
        ]
        if set(checklist_sections) != set(checklist_declared_sections) or len(checklist_sections) != len(checklist_declared_sections):
            raise ValueError("source coverage does not map each public implementation-checklist section once")
        omitted_relations = load(version_root / "data" / "qa" / "omitted-relations.json").get("records", [])
        for target_prefix, owner_guide in {
            "work:": "residents-and-work",
            "faction:": "factions-contracts-and-prisoners",
        }.items():
            if not any(record.get("target", "").startswith(target_prefix) for record in omitted_relations):
                raise ValueError(f"omitted relation coverage source is missing: {target_prefix}")
            if owner_guide not in guide_ids:
                raise ValueError(f"omitted relation owner is missing: {owner_guide}")
        disease_slugs = {
            entity["slug"]
            for entity in entities
            if entity["kind"] == "medical" and (entity["slug"].startswith("disease-") or entity["slug"] == "condition-core-corrosion")
        }
        disease_guide = guide_sources["disease-and-public-health"]
        missing_disease_links = [slug for slug in disease_slugs if f"/entry/medical/{slug}/" not in disease_guide]
        if len(disease_slugs) != 16 or missing_disease_links:
            raise ValueError(f"disease guide does not cover public disease records: {', '.join(sorted(missing_disease_links))}")
        navigation = load(guide_root / "guide-navigation.json")
        if navigation.get("schema_version") != 1 or navigation.get("game_version") != version:
            raise ValueError("guide navigation version is invalid")
        sections = navigation.get("sections")
        pages = navigation.get("pages")
        if not isinstance(sections, list) or not isinstance(pages, list):
            raise ValueError("guide navigation is incomplete")
        section_ids = {section.get("id") for section in sections}
        section_guides = [guide_id for section in sections for guide_id in section.get("guide_ids", [])]
        page_map = {page.get("id"): page for page in pages}
        if set(section_guides) != guide_ids or len(section_guides) != len(guide_ids) or set(page_map) != guide_ids:
            raise ValueError("guide navigation does not cover every guide exactly once")
        for page_id, page in page_map.items():
            if page.get("group") not in section_ids or page.get("kind") not in {"topic", "situation"}:
                raise ValueError(f"guide navigation metadata is invalid: {page_id}")
            if page.get("directory_visibility") not in {None, "contextual"}:
                raise ValueError(f"guide directory visibility is invalid: {page_id}")
            redirect_to = page.get("redirect_to")
            if redirect_to is not None and (redirect_to not in guide_ids or redirect_to == page_id):
                raise ValueError(f"guide redirect is invalid: {page_id}")
            if redirect_to is not None and page.get("directory_visibility") != "contextual":
                raise ValueError(f"redirected guide must be contextual: {page_id}")
            for guide_id in page.get("related_guide_ids", []) + page.get("situation_guide_ids", []):
                if guide_id not in guide_ids or guide_id == page_id:
                    raise ValueError(f"guide navigation has an invalid guide link: {page_id}")
            if not set(page.get("category_ids", [])).issubset(category_ids):
                raise ValueError(f"guide navigation has an invalid category link: {page_id}")
        for page_id, page in page_map.items():
            redirect_to = page.get("redirect_to")
            if redirect_to and page_map[redirect_to].get("redirect_to"):
                raise ValueError(f"guide redirect must target a canonical guide: {page_id}")
        directory = load(version_root / "content" / "directory.yml")
        directory_groups = directory.get("groups")
        if directory.get("schema_version") != 1 or directory.get("game_version") != version or not isinstance(directory_groups, list):
            raise ValueError("directory manifest is invalid")
        for group in directory_groups:
            entries = group.get("entries", [])
            if not isinstance(entries, list):
                raise ValueError(f"directory group has invalid entries: {group.get('group_id')}")
            for entry in entries:
                if entry.get("target_kind") != "guide":
                    continue
                page = page_map.get(entry.get("target_id"))
                if page is None or page.get("directory_visibility") == "contextual":
                    raise ValueError(f"directory may not promote a contextual guide: {entry.get('target_id')}")
        print(json.dumps({"status": "valid", "game_version": version, "entities": len(entities)}, ensure_ascii=False))
        return 0
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as error:
        print(f"wiki model validation failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
