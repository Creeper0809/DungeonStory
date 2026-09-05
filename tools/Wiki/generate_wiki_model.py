#!/usr/bin/env python3
"""Create a deterministic, player-safe DungeonStory wiki snapshot.

The `docs_final` indexes are read-only inputs.  This program projects only
verified player-facing records into a version-scoped snapshot; it never edits
the Unity assets or generated documentation authority.
"""

from __future__ import annotations

import argparse
import ast
import csv
import hashlib
import json
import re
import shutil
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable


SCHEMA_VERSION = 1
GAME_VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+v$")
SLUG_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]*$")
NARRATIVE_ENTRY_PATTERN = re.compile(
    r"(?ms)^  - kind: (?P<kind>\d+)\n"
    r"    stableId: (?P<stable_id>.*?)\n"
    r"    inGameDescription: (?P<description>.*?)"
    r"(?=\n    worldBranchTag:)"
)
ITEM_LORE_MARKERS = (
    "하르나크", "므네실라", "크릭 제7굴", "아일라세라", "제4주조도시",
    "베르사디온", "라카쉬 세갈래길", "오르데나", "밀로세아 가도",
    "노르칸드라", "사르베니아 내해", "큰솥의 몫", "무기 철야",
    "동의의 잔", "맑은물 합류", "공유 안개", "첫 도구 이름짓기",
    "새벽 합창", "기억판 안치", "맹세의 잿불", "무리 한솥", "귀환자의 식탁",
)
ITEM_EARLY_REVEAL_TERMS = (
    "첫 협약", "귀환 의식", "원래 몸", "시민 신체", "인간형 등록", "인간의 몸을 시민권",
)
ITEM_REPORTING_STYLE_TERMS = ("장부", "기록", "적는다", "책임", "소유권")
MAX_ITEM_REPORTING_STYLE_TERM_COUNT = 40
MAX_ITEM_AVERAGE_DESCRIPTION_CHARS = 105
MAX_ITEM_DESCRIPTION_CHARS = 180

TECHNICAL_TYPES = {
    "AnatomyConditionLexiconSO",
    "CharacterFunctionalCapacityDefinitionSO",
    "CharacterPerformanceFormulaDefinitionSO",
    "FacilityEvolutionRecordTokenDefinitionSO",
    "GameplayEffectConditionDefinitionSO",
    "GameplayEffectDefinitionSO",
    "OffenseDecisionCardSO",
    "ResearchUnlockBundleDefinitionSO",
    "StockInfo",
}

SPOILER_TYPES = {
    "EndingDefinitionSO",
    "FactionArcDefinitionSO",
    "FactionChapterDefinitionSO",
    "LifeEventDefinitionSO",
    "OffenseEncounterSO",
    "OffenseUrgentSiteDefinitionSO",
    "SeasonalWorldEventDefinitionSO",
    "ServiceIncidentDefinitionSO",
}

KIND_LABELS = {
    "character": "주민·사회",
    "combat": "장비·전투",
    "event": "사건",
    "facility": "시설·방",
    "item": "아이템·자원",
    "medical": "의료·건강",
    "nature": "농업·생태",
    "recipe": "제작식",
    "research": "연구",
    "world": "세계·원정",
}

PUBLIC_CATEGORY_BY_KIND = {
    "character": "characters",
    "combat": "equipment",
    "event": "events",
    "facility": "facilities",
    "item": "items",
    "medical": "health",
    "nature": "nature",
    "recipe": "recipes",
    "research": "research",
    "world": "world",
}

RELATION_LABELS = {
    "prerequisite": "선행 연구",
    "produces-or-unlocks": "생산·해금",
    "requires": "필요 조건",
    "unlocks": "해금",
    "unlocks-building": "시설 해금",
    "unlocks-recipe": "제작식 해금",
    "unlocks-item": "아이템 해금",
    "references": "관련",
}

REFERENCE_TONE_REPLACEMENTS = (
    ("보여 줍니다", "보여 준다"),
    ("돌아옵니다", "돌아온다"),
    ("들어갑니다", "들어간다"),
    ("드리웁니다", "드리운다"),
    ("퍼뜨립니다", "퍼뜨린다"),
    ("흐트러뜨립니다", "흐트러뜨린다"),
    ("울립니다", "울린다"),
    ("얻습니다", "얻는다"),
    ("늘어납니다", "늘어난다"),
    ("줄어듭니다", "줄어든다"),
    ("바뀝니다", "바뀐다"),
    ("열립니다", "열린다"),
    ("보입니다", "보인다"),
    ("필요합니다", "필요하다"),
    ("좋습니다", "좋다"),
    ("아닙니다", "아니다"),
    ("없습니다", "없다"),
    ("있습니다", "있다"),
    ("않습니다", "않는다"),
    ("됩니다", "된다"),
    ("입니다", "이다"),
    ("합니다", "한다"),
    ("봅니다", "본다"),
    ("둡니다", "둔다"),
    ("읽습니다", "읽는다"),
    ("받습니다", "받는다"),
    ("찾습니다", "찾는다"),
    ("고릅니다", "고른다"),
    ("만듭니다", "만든다"),
    ("바꿉니다", "바꾼다"),
    ("막습니다", "막는다"),
    ("넣습니다", "넣는다"),
    ("씁니다", "쓴다"),
    ("나옵니다", "나온다"),
    ("맞춥니다", "맞춘다"),
    ("남습니다", "남는다"),
    ("듭니다", "든다"),
    ("돕습니다", "돕는다"),
    ("줍니다", "준다"),
)

PUBLIC_TITLE_OVERRIDES = {
    "service:bathing:wash": "목욕 서비스",
    "service:dining:meal": "식사 서비스",
    "service:lodging:rest": "숙박 서비스",
    "service:medical:treat": "진료 서비스",
    "service:retail:sale": "판매 서비스",
}

PUBLIC_TOKEN_LABELS = {
    "workstation:v19:mentor-academy": "멘토 교육",
    "service:bathing": "목욕 서비스",
    "service:dining": "식사 서비스",
    "service:lodging": "숙박 서비스",
    "service:medical": "진료 서비스",
    "service:retail": "판매 서비스",
}

INTERNAL_TOKEN_PATTERN = re.compile(r"\b([a-z][a-z0-9-]*):[a-z0-9][a-z0-9:-]*")
INTERNAL_TOKEN_FALLBACKS = {
    "workstation": "작업대",
    "service": "서비스 공정",
    "research": "연구 항목",
    "work": "작업",
    "material": "재료",
    "resource": "자원",
    "component": "부품",
    "item": "항목",
    "facility": "시설",
}


@dataclass(frozen=True)
class SourceRow:
    group: str
    content_type: str
    stable_id: str
    record_key: str
    title: str
    description: str
    mechanics: str
    relations: str
    lifecycle_status: str
    review_status: str
    runtime_status: str
    raw: dict[str, str]


def repository_root(start: Path) -> Path:
    candidate = start.resolve()
    while candidate != candidate.parent:
        if (candidate / "docs_final" / "content-db").is_dir() and (candidate / "wiki").is_dir():
            return candidate
        candidate = candidate.parent
    raise ValueError("Repository root containing docs_final/content-db and wiki was not found.")


def read_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    path.write_text(encoded, encoding="utf-8", newline="\n")


def write_csv(path: Path, rows: Iterable[dict[str, str]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


def tree_digest(root: Path, excluded_relative_paths: set[str] | None = None) -> str:
    digest = hashlib.sha256()
    excluded = excluded_relative_paths or set()
    for path in sorted(item for item in root.rglob("*") if item.is_file()):
        relative_text = path.relative_to(root).as_posix()
        if relative_text in excluded:
            continue
        relative = relative_text.encode("utf-8")
        digest.update(relative)
        digest.update(b"\0")
        digest.update(hashlib.sha256(path.read_bytes()).digest())
    return digest.hexdigest()


def load_rows(content_root: Path) -> list[SourceRow]:
    rows: list[SourceRow] = []
    for csv_path in sorted((content_root / "csv").rglob("*.csv")):
        group = csv_path.parent.name
        with csv_path.open("r", encoding="utf-8", newline="") as stream:
            for raw in csv.DictReader(stream):
                stable_id = (raw.get("stable_id") or "").strip()
                content_type = (raw.get("content_type") or "").strip()
                record_key = (raw.get("record_key") or "").strip()
                if not stable_id or not content_type or not record_key:
                    raise ValueError(f"Malformed content row in {csv_path}: required identity is missing.")
                rows.append(
                    SourceRow(
                        group=group,
                        content_type=content_type,
                        stable_id=stable_id,
                        record_key=record_key,
                        title=(raw.get("display_name") or "").strip(),
                        description=(raw.get("description") or "").strip(),
                        mechanics=(raw.get("mechanics") or "").strip(),
                        relations=(raw.get("relations") or "").strip(),
                        lifecycle_status=(raw.get("lifecycle_status") or "").strip(),
                        review_status=(raw.get("review_status") or "").strip(),
                        runtime_status=(raw.get("runtime_status") or "").strip(),
                        raw=raw,
                    )
                )
    if not rows:
        raise ValueError("No source records were found in docs_final/content-db/csv.")
    return rows


def load_item_narratives(path: Path, authority_path: Path) -> tuple[dict[str, str], dict[str, Any]]:
    """Read item text from the same generated catalogue consumed by the game.

    Unity serializes these strings as wrapped, double-quoted C-style scalars.
    `ast.literal_eval` handles both the `\\u` and `\\x` escapes Unity emits,
    while keeping this projection read-only with respect to the game asset.
    """
    source = path.read_text(encoding="utf-8")
    narratives: dict[str, str] = {}
    for match in NARRATIVE_ENTRY_PATTERN.finditer(source):
        if match.group("kind") != "0":
            continue
        stable_id = match.group("stable_id").strip()
        serialized = re.sub(r"\n\s+", " ", match.group("description").strip())
        try:
            decoded = ast.literal_eval(serialized)
        except (SyntaxError, ValueError) as error:
            raise ValueError(f"Cannot decode item narrative '{stable_id}': {error}") from error
        description = re.sub(r"\s+", " ", decoded).strip() if isinstance(decoded, str) else ""
        if not stable_id or not description:
            raise ValueError(f"Item narrative has an empty ID or description: {stable_id or '<empty>'}")
        if stable_id in narratives:
            raise ValueError(f"Duplicate item narrative ID: {stable_id}")
        narratives[stable_id] = description
    if not narratives:
        raise ValueError(f"No item narratives were found in {path}")
    authority = read_json(authority_path)
    if authority.get("schema_version") != 2 or authority.get("language") != "ko-KR":
        raise ValueError("Item narrative prose authority has an unsupported schema or language.")
    authority_rows = authority.get("items")
    if not isinstance(authority_rows, list):
        raise ValueError("Item narrative prose authority has no item list.")
    authored: dict[str, str] = {}
    lore_anchors: Counter[str] = Counter()
    lore_connections: Counter[str] = Counter()
    story_layers: Counter[str] = Counter()
    lore_sentences: set[str] = set()
    lore_frames: Counter[str] = Counter()
    reporting_style_terms: Counter[str] = Counter()
    description_lengths: list[int] = []
    for row in authority_rows:
        stable_id = (row.get("stable_id") or "").strip()
        display_name = (row.get("display_name") or "").strip()
        description = (row.get("description") or "").strip()
        lore_anchor = (row.get("lore_anchor") or "").strip()
        lore_connection = (row.get("lore_connection") or "").strip()
        story_layer = (row.get("story_layer") or "").strip()
        lore_sentence = (row.get("lore_sentence") or "").strip()
        if not stable_id or not display_name or not description or stable_id in authored:
            raise ValueError(f"Invalid item narrative prose authority row: {stable_id or '<empty>'}")
        if not lore_anchor or not lore_connection or story_layer not in {"everyday", "clue"}:
            raise ValueError(f"Item narrative lore metadata is incomplete: {stable_id}")
        if not lore_sentence or not description.endswith(lore_sentence):
            raise ValueError(f"Item narrative does not carry its reviewed lore sentence: {stable_id}")
        if not any(marker in lore_sentence for marker in ITEM_LORE_MARKERS):
            raise ValueError(f"Item narrative lore sentence has no concrete world marker: {stable_id}")
        if story_layer == "everyday" and any(term in description for term in ITEM_EARLY_REVEAL_TERMS):
            raise ValueError(f"Everyday item narrative leaks the central reveal: {stable_id}")
        authored[stable_id] = description
        lore_anchors[lore_anchor] += 1
        lore_connections[lore_connection] += 1
        story_layers[story_layer] += 1
        lore_sentences.add(lore_sentence)
        description_lengths.append(len(description))
        for term in ITEM_REPORTING_STYLE_TERMS:
            if term in lore_sentence:
                reporting_style_terms[term] += 1
        lore_frame = lore_sentence
        for name in sorted({display_name, display_name.removesuffix(" 설치 키트")}, key=len, reverse=True):
            if name:
                lore_frame = lore_frame.replace(name, "<항목>")
        lore_frame = re.sub(r"<항목>[은는이가을를]", "<항목><조사>", lore_frame)
        lore_frame = re.sub(r"\d+(?:\.\d+)?", "<수치>", lore_frame)
        lore_frames[re.sub(r"\s+", " ", lore_frame).strip()] += 1
    if authority.get("item_count") != len(authored):
        raise ValueError("Item narrative prose authority count does not match its item list.")
    if narratives != authored:
        stale = sorted(
            stable_id
            for stable_id in set(narratives).intersection(authored)
            if narratives[stable_id] != authored[stable_id]
        )
        raise ValueError(
            "Unity item narratives differ from the reviewed prose authority: "
            f"catalogue={len(narratives)}, authority={len(authored)}, stale={stale[:3]}"
        )
    quality = authority.get("quality")
    if not isinstance(quality, dict):
        raise ValueError("Item narrative prose authority has no quality audit.")
    if quality.get("banned_fragment_count") != 0:
        raise ValueError("Item narrative prose authority contains banned template text.")
    if quality.get("world_grounded_count") != len(authored) or quality.get("missing_lore_count") != 0:
        raise ValueError("Item narrative prose authority has incomplete world grounding.")
    if quality.get("distinct_lore_sentence_count", 0) < 900:
        raise ValueError("Item narrative lore sentences have regressed to repeated boilerplate.")
    if quality.get("lore_anchor_counts") != dict(sorted(lore_anchors.items())):
        raise ValueError("Item narrative lore-anchor audit is stale.")
    if quality.get("lore_connection_counts") != dict(sorted(lore_connections.items())):
        raise ValueError("Item narrative lore-connection audit is stale.")
    if quality.get("story_layer_counts") != dict(sorted(story_layers.items())):
        raise ValueError("Item narrative story-layer audit is stale.")
    if quality.get("distinct_lore_sentence_count") != len(lore_sentences):
        raise ValueError("Item narrative lore-sentence audit is stale.")
    if quality.get("distinct_lore_frame_count") != len(lore_frames) or len(lore_frames) < 250:
        raise ValueError("Item narrative lore-frame audit is stale or too repetitive.")
    largest_lore_frames = quality.get("largest_lore_frames")
    if not isinstance(largest_lore_frames, list) or any(row.get("count", 0) > 20 for row in largest_lore_frames):
        raise ValueError("Item narrative lore prose repeats one semantic frame too often.")
    expected_lore_frames = [
        {"count": count, "frame": frame} for frame, count in lore_frames.most_common(10)
    ]
    if largest_lore_frames != expected_lore_frames:
        raise ValueError("Item narrative largest-lore-frame audit is stale.")
    expected_reporting_style_terms = {
        term: reporting_style_terms.get(term, 0) for term in ITEM_REPORTING_STYLE_TERMS
    }
    if quality.get("reporting_style_term_counts") != expected_reporting_style_terms:
        raise ValueError("Item narrative reporting-style vocabulary audit is stale.")
    if any(count > MAX_ITEM_REPORTING_STYLE_TERM_COUNT for count in reporting_style_terms.values()):
        raise ValueError("Item narrative prose overuses report-like vocabulary.")
    average_description_chars = round(sum(description_lengths) / len(description_lengths), 1)
    if quality.get("average_description_chars") != average_description_chars or average_description_chars > MAX_ITEM_AVERAGE_DESCRIPTION_CHARS:
        raise ValueError("Item narrative average length is stale or too long.")
    if quality.get("max_description_chars") != max(description_lengths) or max(description_lengths) > MAX_ITEM_DESCRIPTION_CHARS:
        raise ValueError("Item narrative maximum length is stale or too long.")
    if quality.get("distinct_opening_frame_count", 0) < 900:
        raise ValueError("Item narrative prose authority has regressed to repeated opening templates.")
    largest_frames = quality.get("largest_opening_frames")
    if not isinstance(largest_frames, list) or any(row.get("count", 0) > 12 for row in largest_frames):
        raise ValueError("Item narrative prose authority repeats an opening frame too often.")
    return narratives, quality


def load_review_identities(path: Path) -> set[tuple[str, str]]:
    identities: set[tuple[str, str]] = set()
    with path.open("r", encoding="utf-8", newline="") as stream:
        for row in csv.DictReader(stream):
            identities.add(((row.get("content_type") or "").strip(), (row.get("stable_id") or "").strip()))
    return identities


def load_duplicate_identities(path: Path) -> set[tuple[str, str]]:
    identities: set[tuple[str, str]] = set()
    with path.open("r", encoding="utf-8", newline="") as stream:
        for row in csv.DictReader(stream):
            identities.add(((row.get("content_type") or "").strip(), (row.get("stable_id") or "").strip()))
    return identities


def load_unresolved_identities(path: Path) -> set[tuple[str, str]]:
    identities: set[tuple[str, str]] = set()
    with path.open("r", encoding="utf-8", newline="") as stream:
        for row in csv.DictReader(stream):
            identities.add(((row.get("source_type") or "").strip(), (row.get("source_id") or "").strip()))
    return identities


def slugify(stable_id: str) -> str:
    slug = stable_id.lower().replace(":", "-").replace("_", "-")
    slug = re.sub(r"[^a-z0-9-]+", "-", slug)
    slug = re.sub(r"-+", "-", slug).strip("-")
    if not SLUG_PATTERN.fullmatch(slug):
        raise ValueError(f"Cannot create a safe URL slug from stable ID: {stable_id}")
    return slug


def kind_for(row: SourceRow) -> str:
    content_type = row.content_type
    if content_type == "ProductionRecipeSO":
        return "recipe"
    if content_type == "ResearchProjectSO":
        return "research"
    if content_type in {"BuildingSO", "FacilityBlueprintSO", "FacilityEvolutionRecipeSO", "FacilitySynthesisRecipeSO", "ServiceProcessSO"}:
        return "facility"
    if content_type in {"CombatWeaponSO", "CombatArmorSO", "CombatShieldSO", "EquipmentModuleDefinitionSO", "EnvironmentalWorkwearSO", "ApparelDefinitionSO"}:
        return "combat"
    if content_type in {"DiseaseDefinitionSO", "SurgicalProcedureSO", "AnatomyProfileSO"}:
        return "medical"
    if content_type in {"CropDefinitionSO", "CropGenomeDefinitionSO", "WildlifeSpeciesSO", "WeatherFrontDefinitionSO", "ClimateZoneDefinitionSO"}:
        return "nature"
    if row.group == "items":
        return "item"
    if row.group == "characters-traits":
        return "character"
    if row.group == "events-campaign":
        return "event"
    if row.group == "combat-health-world":
        return "world"
    return "world"


def canonical_relation_target(candidates: list[tuple[SourceRow, dict[str, Any]]]) -> dict[str, Any]:
    """Choose the single public authority for an untyped stable-ID relation.

    Inventory item IDs are also used by combat and craft-material definitions.
    Relations in the source index identify those targets by stable ID only, so a
    physical item page must win whenever one exists.  This keeps recipes, cargo,
    surgery materials and item requirements attached to the inventory authority
    while the combat/material definition remains available at its own kind URL.
    """
    if not candidates:
        raise ValueError("Cannot resolve a relation without public candidates.")
    item_candidates = [candidate for candidate in candidates if candidate[1]["kind"] == "item"]
    eligible = item_candidates or candidates
    return max(
        eligible,
        key=lambda candidate: (
            candidate[1]["kind"],
            candidate[0].title.casefold(),
            candidate[0].content_type,
            candidate[0].record_key,
        ),
    )[1]


def spoiler_tier(row: SourceRow) -> str:
    return "warning" if row.content_type in SPOILER_TYPES else "none"


def parse_relation_tokens(value: str) -> list[tuple[str, str, str | None]]:
    tokens: list[tuple[str, str, str | None]] = []
    for part in value.split(";"):
        token = part.strip()
        if not token or ":" not in token:
            continue
        relation_type, target = token.split(":", 1)
        target = target.strip()
        amount: str | None = None
        amount_match = re.search(r"×([^×]+)$", target)
        if amount_match:
            amount = amount_match.group(1).strip()
            target = target[: amount_match.start()].strip()
        if target:
            tokens.append((relation_type.strip(), target, amount))
    return tokens


def display_text(value: str, title_by_stable_id: dict[str, str]) -> str:
    output = value
    for stable_id in sorted(title_by_stable_id, key=len, reverse=True):
        output = output.replace(stable_id, title_by_stable_id[stable_id])
    for token in sorted(PUBLIC_TOKEN_LABELS, key=len, reverse=True):
        output = output.replace(token, PUBLIC_TOKEN_LABELS[token])
    output = INTERNAL_TOKEN_PATTERN.sub(
        lambda match: INTERNAL_TOKEN_FALLBACKS.get(match.group(1), "관련 항목"),
        output,
    )
    output = re.sub(r"\s+", " ", output).strip()
    return output


def reference_tone(value: str) -> str:
    """Render public prose in the wiki's declarative reference style."""
    output = value
    for source, replacement in REFERENCE_TONE_REPLACEMENTS:
        output = output.replace(source, replacement)
    return output


def safe_summary(row: SourceRow, title_by_stable_id: dict[str, str]) -> str:
    source = next(
        (
            value
            for value in (
                row.description,
                row.raw.get("authored__description"),
                row.raw.get("system_role"),
                row.raw.get("existence_reason"),
                row.mechanics,
            )
            if value and value.strip()
        ),
        "",
    )
    source = reference_tone(display_text(source, title_by_stable_id))
    source = re.sub(r"^(.+ 서비스)에서 서비스 방식 3개를 제공하며", r"\1는 세 가지 방식을 제공하며", source)
    source = source.replace("1회 처리마다 깨끗한 물 0를 소비하고 폐수 0를 배출한다. ", "")
    return source[:500] if source else "이 게임 버전에서 확인된 공개 정보입니다."


def public_title(row: SourceRow) -> str:
    return PUBLIC_TITLE_OVERRIDES.get(row.stable_id, row.title)


def rendered_fact_value(value: str | None, title_by_stable_id: dict[str, str], suffix: str = "") -> str:
    rendered = display_text((value or "").strip(), title_by_stable_id)
    if not rendered or rendered in {"None", "없음", "[]", "null"}:
        return ""
    korean_label = re.search(r"\(([^()]*)\)$", rendered)
    if korean_label and re.search(r"[가-힣]", korean_label.group(1)):
        rendered = korean_label.group(1)
    return f"{rendered}{suffix}"


def percentage_value(value: str | None) -> str:
    try:
        return f"{float((value or '').strip()) * 100:g}%"
    except ValueError:
        return ""


def fact_rows(row: SourceRow, title_by_stable_id: dict[str, str]) -> list[dict[str, str]]:
    raw = row.raw
    candidates = [
        ("분류", raw.get("authored__category__label") or raw.get("authored__category")),
        ("필요 작업", raw.get("authored__requiredWork")),
        ("필요 연구", raw.get("authored__requiredResearchId")),
        ("연구 분야", raw.get("authored__field__label") or raw.get("authored__field")),
        ("역할", raw.get("authored__roles__label") or raw.get("authored__roles")),
    ]
    candidates.extend(
        [
            ("무게", raw.get("authored__unitWeight"), "kg"),
            ("한 칸 적재", raw.get("authored__maxStack"), "개"),
            ("기준 가격", raw.get("authored__unitPrice")),
            ("크기", "×".join(value for value in (raw.get("authored__width"), raw.get("authored__height")) if value)),
            ("준비 작업", raw.get("authored__preparationWork")),
            ("마무리 작업", raw.get("authored__finishingWork")),
            ("사이클당 깨끗한 물", raw.get("authored__cleanWaterPerCycle")),
            ("처리 시간", raw.get("authored__processingGameHours")),
            ("희귀도", raw.get("authored__selectionRarity__label")),
            ("성향", raw.get("authored__polarity__label")),
            ("효과", raw.get("authored__effects__count"), "개"),
            ("정체성 규칙", raw.get("authored__identityRules__count"), "개"),
            ("주전문", raw.get("authored__primaryProficiencyId")),
            ("부전문", raw.get("authored__secondaryProficiencyId")),
            ("시작 효과", raw.get("authored__startingEffects__count"), "개"),
            ("숙련 보너스", raw.get("authored__proficiencyBonuses__count"), "개"),
            ("제작 작업", raw.get("authored__requiredCraftWork")),
            ("무게", raw.get("authored__weight"), "kg"),
            ("최대 사거리", raw.get("authored__maximumRange")),
            ("전면 방어 확률", percentage_value(raw.get("authored__frontalBlockChance"))),
            ("최소 지속 기간", raw.get("authored__minimumDurationDays"), "일"),
            ("최대 지속 기간", raw.get("authored__maximumDurationDays"), "일"),
            ("종료 효과", raw.get("authored__endEffects__count"), "개"),
            ("성공 효과", raw.get("authored__successEffects__count"), "개"),
            ("실패 효과", raw.get("authored__failureEffects__count"), "개"),
            ("평균 기온", raw.get("authored__meanTemperatureC"), "°C"),
            ("기온 변화", raw.get("authored__temperatureModifierC"), "°C"),
            ("봄 가중치", raw.get("authored__springWeight")),
            ("여름 가중치", raw.get("authored__summerWeight")),
            ("가을 가중치", raw.get("authored__autumnWeight")),
            ("겨울 가중치", raw.get("authored__winterWeight")),
            ("최소 강도", raw.get("authored__minimumStrength")),
            ("최대 강도", raw.get("authored__maximumStrength")),
            ("최소 유지 기간", raw.get("authored__minimumLifetimeDays"), "일"),
            ("최대 유지 기간", raw.get("authored__maximumLifetimeDays"), "일"),
            ("완화 작업", raw.get("authored__mitigationWork")),
        ]
    )
    facts: list[dict[str, str]] = []
    seen: set[str] = set()
    for candidate in candidates:
        label, value, *rest = candidate
        suffix = rest[0] if rest else ""
        rendered = rendered_fact_value(value, title_by_stable_id, suffix)
        if (not rendered or label in seen or
                (rendered in {"0", "0개"} and label in {"준비 작업", "마무리 작업", "사이클당 깨끗한 물", "처리 시간", "효과", "정체성 규칙"})):
            continue
        seen.add(label)
        facts.append({"label": label, "value": rendered[:180]})
    return facts


def classification_reason(row: SourceRow, blocked_review: set[tuple[str, str]], duplicates: set[tuple[str, str]], unresolved: set[tuple[str, str]]) -> str | None:
    identity = (row.content_type, row.stable_id)
    if row.content_type in TECHNICAL_TYPES:
        return "internal-type"
    if identity in duplicates:
        return "duplicate-stable-id"
    if identity in blocked_review or row.review_status != "근거 확인":
        return "manual-review"
    if identity in unresolved:
        return "unresolved-reference"
    if row.lifecycle_status != "active-authored":
        return "non-active-lifecycle"
    if row.runtime_status != "catalog-registered-static-consumer":
        return "runtime-not-verified"
    if not row.title:
        return "missing-player-title"
    return None


def load_policy(version_root: Path) -> dict[str, Any]:
    policy_path = version_root / "content" / "publication.yml"
    try:
        return read_json(policy_path)
    except FileNotFoundError as error:
        raise ValueError(f"Required publication policy is missing: {policy_path}") from error
    except json.JSONDecodeError as error:
        raise ValueError(f"publication.yml must contain JSON-compatible YAML: {policy_path}") from error


def validate_policy(policy: dict[str, Any]) -> None:
    required = {"schema_version", "visibility", "allow_lifecycle_status", "allow_review_status", "allow_runtime_status"}
    missing = sorted(required.difference(policy))
    if missing:
        raise ValueError(f"publication policy is missing required fields: {', '.join(missing)}")
    if policy["schema_version"] != SCHEMA_VERSION or policy["visibility"] != "public":
        raise ValueError("publication policy has an unsupported schema or visibility.")
    if policy["allow_lifecycle_status"] != ["active-authored"] or policy["allow_review_status"] != ["근거 확인"]:
        raise ValueError("publication policy must fail closed to active, verified records.")


def ensure_version_contract(repo_root: Path, game_version: str) -> tuple[Path, dict[str, Any], dict[str, Any]]:
    if not GAME_VERSION_PATTERN.fullmatch(game_version):
        raise ValueError(f"Invalid game version: {game_version}")
    planned_version = (repo_root / "docs" / "wiki" / "GAME_VERSION").read_text(encoding="utf-8").strip()
    version_root = repo_root / "wiki" / "game-versions" / game_version
    registry = read_json(repo_root / "wiki" / "game-versions" / "registry.json")
    version_meta = read_json(version_root / "game-version.json")
    if planned_version != game_version or registry.get("current_game_version") != game_version:
        raise ValueError("GAME_VERSION, registry current_game_version, and generator argument must agree.")
    if version_meta.get("game_version") != game_version:
        raise ValueError("game-version.json does not match the version directory.")
    if version_meta.get("status") not in {"planned", "draft", "published", "withdrawn"}:
        raise ValueError("game-version.json has an unsupported status.")
    return version_root, registry, version_meta


def build_snapshot(repo_root: Path, game_version: str, destination: Path | None = None) -> dict[str, Any]:
    content_root = repo_root / "docs_final" / "content-db"
    knowledge_root = repo_root / "docs_final" / "knowledge-base"
    item_narratives, item_narrative_quality = load_item_narratives(
        repo_root / "Assets" / "Resources" / "SO" / "InGameNarrativeTextCatalog.asset",
        repo_root / "docs" / "game-design" / "content" / "item-in-game-descriptions.ko.json",
    )
    version_root, registry, version_meta = ensure_version_contract(repo_root, game_version)
    policy = load_policy(version_root)
    validate_policy(policy)

    content_manifest = read_json(content_root / "generation-manifest.json")
    knowledge_manifest = read_json(knowledge_root / "generation-manifest.json")
    rows = load_rows(content_root)
    blocked_review = load_review_identities(content_root / "manual-review.csv")
    duplicates = load_duplicate_identities(content_root / "duplicate-content.csv")
    unresolved = load_unresolved_identities(content_root / "unresolved-references.csv")

    excluded: list[dict[str, str]] = []
    included_rows: list[SourceRow] = []
    for row in rows:
        reason = classification_reason(row, blocked_review, duplicates, unresolved)
        if reason:
            excluded.append({"content_type": row.content_type, "stable_id": row.stable_id, "reason": reason})
        else:
            included_rows.append(row)

    published_item_ids = {
        row.stable_id for row in included_rows if kind_for(row) == "item"
    }
    missing_item_narratives = sorted(published_item_ids.difference(item_narratives))
    orphan_item_narratives = sorted(set(item_narratives).difference(published_item_ids))
    duplicate_item_narratives = sorted(
        description
        for description, count in Counter(item_narratives.values()).items()
        if count > 1
    )
    if missing_item_narratives or orphan_item_narratives or duplicate_item_narratives:
        raise ValueError(
            "Item narrative coverage is invalid: "
            f"missing={missing_item_narratives[:3]}, "
            f"orphan={orphan_item_narratives[:3]}, "
            f"duplicate_texts={duplicate_item_narratives[:3]}"
        )

    identity_counts = Counter((row.content_type, row.stable_id) for row in included_rows)
    duplicate_included = [identity for identity, count in identity_counts.items() if count != 1]
    if duplicate_included:
        raise ValueError(f"Public projection contains duplicate typed identities: {duplicate_included[:3]}")

    title_by_stable_id: dict[str, str] = {}
    rows_by_stable_id: dict[str, list[SourceRow]] = defaultdict(list)
    for row in included_rows:
        rows_by_stable_id[row.stable_id].append(row)
    for stable_id, candidates in rows_by_stable_id.items():
        preferred = min(
            candidates,
            key=lambda row: (
                row.group != "items",
                not bool(row.description),
                row.content_type in TECHNICAL_TYPES,
                row.title.casefold(),
                row.content_type,
            ),
        )
        title_by_stable_id[stable_id] = public_title(preferred)

    entities: list[dict[str, Any]] = []
    candidates_by_stable_id: dict[str, list[tuple[SourceRow, dict[str, Any]]]] = defaultdict(list)
    source_by_public_key: dict[tuple[str, str], SourceRow] = {}
    seen_urls: set[tuple[str, str]] = set()
    for row in sorted(included_rows, key=lambda item: (kind_for(item), item.title.casefold(), item.stable_id)):
        kind = kind_for(row)
        slug = slugify(row.stable_id)
        if (kind, slug) in seen_urls:
            raise ValueError(f"Duplicate public URL generated for {kind}/{slug}")
        seen_urls.add((kind, slug))
        entity = {
            "schema_version": SCHEMA_VERSION,
            "game_version": game_version,
            "kind": kind,
            "group": row.group,
            "id": row.stable_id,
            "slug": slug,
            "title": public_title(row),
            "summary": safe_summary(row, title_by_stable_id),
            "facts": fact_rows(row, title_by_stable_id),
            "spoiler_tier": spoiler_tier(row),
            "relations": [],
        }
        if kind == "item":
            entity["in_game_description"] = item_narratives[row.stable_id]
        entities.append(entity)
        candidates_by_stable_id[row.stable_id].append((row, entity))
        source_by_public_key[(kind, slug)] = row

    canonical_target_records: list[dict[str, Any]] = []
    for stable_id, candidates in sorted(candidates_by_stable_id.items()):
        if len(candidates) < 2:
            continue
        canonical = canonical_relation_target(candidates)
        canonical_target_records.append(
            {
                "stable_id": stable_id,
                "canonical_kind": canonical["kind"],
                "canonical_slug": canonical["slug"],
                "candidates": [
                    {
                        "content_type": row.content_type,
                        "kind": entity["kind"],
                        "slug": entity["slug"],
                        "title": entity["title"],
                    }
                    for row, entity in sorted(candidates, key=lambda candidate: (candidate[1]["kind"], candidate[0].content_type))
                ],
            }
        )

    forward: dict[str, list[dict[str, Any]]] = defaultdict(list)
    backlinks: dict[str, list[dict[str, Any]]] = defaultdict(list)
    omitted_relations: list[dict[str, str]] = []
    for entity in entities:
        source = source_by_public_key[(entity["kind"], entity["slug"])]
        for relation_type, target_id, amount in parse_relation_tokens(source.relations):
            candidates = candidates_by_stable_id.get(target_id, [])
            if not candidates:
                # The source relation index also contains runtime-domain tags such
                # as background flags and work semantics.  They have no canonical
                # player document, so they are omitted rather than rendered as a
                # broken link or substituted with a guessed page.
                omitted_relations.append(
                    {"source": entity["id"], "relation_type": relation_type, "target": target_id, "reason": "no-public-canonical-target"}
                )
                continue
            target = canonical_relation_target(candidates)
            relation = {
                "type": relation_type,
                "label": RELATION_LABELS.get(relation_type, "연결"),
                "amount": amount,
                "target_kind": target["kind"],
                "target_slug": target["slug"],
                "target_title": target["title"],
                "target_spoiler_tier": target["spoiler_tier"],
            }
            entity["relations"].append(relation)
            source_key = f"{entity['kind']}/{entity['slug']}"
            target_key = f"{target['kind']}/{target['slug']}"
            forward[source_key].append({"from": source_key, "to": target_key, **relation})
            backlinks[target_key].append(
                {
                    "from_kind": entity["kind"],
                    "from_slug": entity["slug"],
                    "from_title": entity["title"],
                    "from_spoiler_tier": entity["spoiler_tier"],
                    "type": relation_type,
                    "label": RELATION_LABELS.get(relation_type, "연결"),
                    "amount": amount,
                }
            )

    for entity in entities:
        entity["relations"].sort(key=lambda relation: (relation["label"], relation["target_title"], relation["target_slug"]))
    for mapping in (forward, backlinks):
        for key in mapping:
            mapping[key].sort(key=lambda relation: json.dumps(relation, ensure_ascii=False, sort_keys=True))

    category_entries: dict[str, list[dict[str, str]]] = defaultdict(list)
    for entity in entities:
        category_id = PUBLIC_CATEGORY_BY_KIND.get(entity["kind"])
        if not category_id:
            raise ValueError(f"missing public category mapping for entity kind: {entity['kind']}")
        category_entries[category_id].append(
            {"kind": entity["kind"], "slug": entity["slug"], "title": entity["title"], "spoiler_tier": entity["spoiler_tier"]}
        )
    categories = []
    for category_id in sorted(category_entries, key=lambda value: (KIND_LABELS.get(next(item["kind"] for item in category_entries[value]), value), value)):
        entries = sorted(category_entries[category_id], key=lambda item: (item["title"].casefold(), item["kind"], item["slug"]))
        categories.append(
            {
                "id": category_id,
                "label": KIND_LABELS.get(entries[0]["kind"], category_id),
                "entry_count": len(entries),
                "entries": entries,
            }
        )

    output_root = destination or (version_root / "data")
    if output_root.exists():
        shutil.rmtree(output_root)
    output_root.mkdir(parents=True, exist_ok=True)

    for entity in entities:
        public_entity = {key: value for key, value in entity.items() if key != "id"}
        write_json(output_root / "entities" / entity["kind"] / f"{entity['slug']}.json", public_entity)

    spoiler_root = repo_root / "wiki" / "public" / "spoiler-data" / game_version
    if spoiler_root.exists():
        shutil.rmtree(spoiler_root)
    for entity in entities:
        if entity["spoiler_tier"] != "warning":
            continue
        write_json(
            spoiler_root / entity["kind"] / f"{entity['slug']}.json",
            {
                "schema_version": SCHEMA_VERSION,
                "title": entity["title"],
                "summary": entity["summary"],
                "facts": entity["facts"],
                "relations": [relation for relation in entity["relations"] if relation["target_spoiler_tier"] == "none"],
            },
        )

    write_json(output_root / "relations" / "forward.json", dict(sorted(forward.items())))
    write_json(output_root / "relations" / "backlinks.json", dict(sorted(backlinks.items())))
    write_json(output_root / "navigation" / "categories.json", {"schema_version": SCHEMA_VERSION, "categories": categories})
    write_json(
        output_root / "navigation" / "redirects.json",
        {"schema_version": SCHEMA_VERSION, "redirects": [{"from": "/changes/", "to": "/updates/", "status": 301}]},
    )
    write_json(
        output_root / "search" / "aliases.json",
        {
            "schema_version": SCHEMA_VERSION,
            "records": [
                {"kind": entity["kind"], "slug": entity["slug"], "title": entity["title"], "aliases": []}
                for entity in entities
                if entity["spoiler_tier"] == "none"
            ],
        },
    )

    graph_nodes: list[dict[str, str]] = []
    for entity in entities:
        node = {"kind": entity["kind"], "slug": entity["slug"], "title": entity["title"], "spoiler_tier": entity["spoiler_tier"]}
        graph_nodes.append(node)
        write_json(output_root / "graph" / "nodes" / entity["kind"] / f"{entity['slug']}.json", node)
        key = f"{entity['kind']}/{entity['slug']}"
        neighbors = forward.get(key, []) + [
            {
                "from": f"{backlink['from_kind']}/{backlink['from_slug']}",
                "to": key,
                "type": backlink["type"],
                "label": backlink["label"],
                "amount": backlink["amount"],
            }
            for backlink in backlinks.get(key, [])
        ]
        write_json(
            output_root / "graph" / "slices" / entity["kind"] / f"{entity['slug']}.json",
            {"schema_version": SCHEMA_VERSION, "center": node, "edges": neighbors},
        )
    edge_count = sum(len(edges) for edges in forward.values())
    write_json(
        output_root / "graph" / "manifest.json",
        {"schema_version": SCHEMA_VERSION, "game_version": game_version, "node_count": len(graph_nodes), "edge_count": edge_count},
    )

    excluded.sort(key=lambda item: (item["reason"], item["content_type"], item["stable_id"]))
    write_json(output_root / "qa" / "excluded-records.json", {"schema_version": SCHEMA_VERSION, "records": excluded})
    write_json(output_root / "qa" / "omitted-relations.json", {"schema_version": SCHEMA_VERSION, "records": omitted_relations})
    write_json(
        output_root / "qa" / "canonical-relation-targets.json",
        {"schema_version": SCHEMA_VERSION, "records": canonical_target_records},
    )
    write_json(
        output_root / "qa" / "item-narrative-coverage.json",
        {
            "schema_version": SCHEMA_VERSION,
            "target_count": len(published_item_ids),
            "description_count": len(item_narratives),
            "unique_description_count": len(set(item_narratives.values())),
            "missing_ids": missing_item_narratives,
            "orphan_ids": orphan_item_narratives,
            "duplicate_texts": duplicate_item_narratives,
            "distinct_opening_frame_count": item_narrative_quality["distinct_opening_frame_count"],
            "largest_opening_frames": item_narrative_quality["largest_opening_frames"],
            "banned_fragment_count": item_narrative_quality["banned_fragment_count"],
            "world_grounded_count": item_narrative_quality["world_grounded_count"],
            "missing_lore_count": item_narrative_quality["missing_lore_count"],
            "distinct_lore_sentence_count": item_narrative_quality["distinct_lore_sentence_count"],
            "distinct_lore_frame_count": item_narrative_quality["distinct_lore_frame_count"],
            "largest_lore_frames": item_narrative_quality["largest_lore_frames"],
            "reporting_style_term_counts": item_narrative_quality["reporting_style_term_counts"],
            "average_description_chars": item_narrative_quality["average_description_chars"],
            "max_description_chars": item_narrative_quality["max_description_chars"],
            "lore_anchor_counts": item_narrative_quality["lore_anchor_counts"],
            "lore_connection_counts": item_narrative_quality["lore_connection_counts"],
            "story_layer_counts": item_narrative_quality["story_layer_counts"],
        },
    )
    write_json(
        output_root / "qa" / "source-provenance.json",
        {
            "schema_version": SCHEMA_VERSION,
            "content_db_source_digest": content_manifest["source_digest"],
            "knowledge_base_source_digest": knowledge_manifest["source_digest"],
            "content_db_output_digest": content_manifest["output_digest"],
            "knowledge_base_output_digest": knowledge_manifest["output_digest"],
        },
    )
    report = {
        "schema_version": SCHEMA_VERSION,
        "game_version": game_version,
        "source_record_count": len(rows),
        "published_record_count": len(entities),
        "published_relation_count": edge_count,
        "excluded_record_count": len(excluded),
        "excluded_by_reason": dict(sorted(Counter(item["reason"] for item in excluded).items())),
        "omitted_relation_count": len(omitted_relations),
        "canonical_relation_target_count": len(canonical_target_records),
        "item_narrative_count": len(item_narratives),
    }
    write_json(output_root / "qa" / "publication-report.json", report)

    write_csv(
        version_root / "content" / "slug-registry.csv",
        [
            {"kind": entity["kind"], "slug": entity["slug"], "title": entity["title"], "game_version": game_version}
            for entity in entities
        ],
        ["kind", "slug", "title", "game_version"],
    )

    manifest = {
        "schema_version": SCHEMA_VERSION,
        "game_version": game_version,
        "source_digests": {
            "content_db": content_manifest["source_digest"],
            "knowledge_base": knowledge_manifest["source_digest"],
        },
        "counts": {"entities": len(entities), "relations": edge_count, "categories": len(categories)},
        "publication_report": "qa/publication-report.json",
    }
    write_json(output_root / "manifest.json", manifest)
    manifest["content_digest"] = tree_digest(output_root, {"manifest.json"})
    write_json(output_root / "manifest.json", manifest)

    version_meta["source_digests"] = manifest["source_digests"]
    version_meta["content_digest"] = manifest["content_digest"]
    write_json(version_root / "game-version.json", version_meta)
    registry["current_game_version"] = game_version
    registry["versions"] = [
        {
            **entry,
            "content_digest": manifest["content_digest"] if entry.get("game_version") == game_version else entry.get("content_digest"),
        }
        for entry in registry.get("versions", [])
    ]
    write_json(repo_root / "wiki" / "game-versions" / "registry.json", registry)
    return {**report, "content_digest": manifest["content_digest"], "output_root": str(output_root)}


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate the versioned DungeonStory player-wiki model.")
    parser.add_argument("--game-version", required=True, help="Game-version folder to generate, e.g. 0.0.1v")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd(), help="Repository root or a path below it")
    args = parser.parse_args()
    try:
        root = repository_root(args.repo_root)
        result = build_snapshot(root, args.game_version)
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"wiki model generation failed: {error}", file=sys.stderr)
        return 1
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
