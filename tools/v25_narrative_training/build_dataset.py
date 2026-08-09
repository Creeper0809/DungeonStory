#!/usr/bin/env python3
"""Build the deterministic DungeonStory V25 narrative corpus.

The script reads authored Unity YAML assets for facts. Web sources are kept in
sources.json and influence only the documented taxonomy; no source prose is
downloaded or copied into records.
"""

from __future__ import annotations

import argparse
import csv
import gzip
import hashlib
import io
import json
import math
import re
import shutil
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


TOOL_DIR = Path(__file__).resolve().parent
REPO_ROOT = TOOL_DIR.parents[1]
CONFIG_PATH = TOOL_DIR / "config.json"
SOURCE_PATH = TOOL_DIR / "sources.json"


@dataclass(frozen=True)
class ContentFact:
    stable_id: str
    name: str
    description: str
    asset_path: str


STYLES = (
    {
        "id": "adventurer-frontier", "species": "인간", "culture": "개척자 연맹 문화",
        "motifs": ("길", "귀환", "요새", "지도", "변방의 맹세", "닳은 장화"),
        "name_heads": ("길끝", "귀환", "변방", "먼빛", "성문", "등불"),
        "name_tails": ("서약", "기록", "발걸음", "표식", "노래", "증언"),
    },
    {
        "id": "beastkin-pack", "species": "수인", "culture": "무리의 화로 문화",
        "motifs": ("발자국", "송곳니", "사냥", "화톳불", "무리의 맹세", "겨울털"),
        "name_heads": ("잿발", "긴엄니", "달추적", "불냄새", "첫사냥", "붉은발"),
        "name_tails": ("맹세", "포효", "추적", "귀환", "수호", "흔적"),
    },
    {
        "id": "demon-contract", "species": "악마", "culture": "재의 계약 문화",
        "motifs": ("재", "인장", "채무", "계약", "의식의 불꽃", "세 번째 종"),
        "name_heads": ("재빛", "삼중", "봉인", "불씨", "검은인장", "미납"),
        "name_tails": ("조항", "서약", "대가", "증서", "화인", "판결"),
    },
    {
        "id": "golem-core", "species": "골렘", "culture": "핵 공명 문화",
        "motifs": ("핵", "공명", "각인", "강철", "기억판", "정비의 침묵"),
        "name_heads": ("철핵", "공명", "각인", "무언", "기억판", "주조"),
        "name_tails": ("명령", "잔향", "기록", "수호", "재기동", "연산"),
    },
    {
        "id": "harpy-aerie", "species": "하피", "culture": "높은 둥지 문화",
        "motifs": ("바람", "새벽", "고도", "날개", "합창", "비어 있는 둥지"),
        "name_heads": ("새벽", "높은바람", "흰깃", "구름끝", "첫날개", "폭풍"),
        "name_tails": ("합창", "선회", "메아리", "귀환", "경계", "노래"),
    },
    {
        "id": "kobold-toolclan", "species": "코볼트", "culture": "도구씨족 문화",
        "motifs": ("톱니", "쐐기", "도면", "이름 붙인 도구", "공방 계보", "검사표"),
        "name_heads": ("맞물린", "첫톱니", "붉은도면", "쐐기끝", "명명", "열두눈금"),
        "name_tails": ("공정", "손길", "도구", "계보", "수리", "완성"),
    },
    {
        "id": "myconid-grove", "species": "균사체", "culture": "포자정원 문화",
        "motifs": ("포자", "균사", "안개", "정원", "군락으로의 귀환", "축축한 기억"),
        "name_heads": ("흰포자", "깊은균사", "안개꽃", "젖은정원", "귀환", "푸른갓"),
        "name_tails": ("개화", "기억", "맥박", "환류", "꿈", "정착"),
    },
    {
        "id": "orc-vigil", "species": "오크", "culture": "무기 철야 문화",
        "motifs": ("흉터", "철혼", "진형", "무기 철야", "계승된 도구", "큰솥의 몫"),
        "name_heads": ("철혼", "붉은흉터", "밤진형", "부러진창", "큰솥", "마지막불"),
        "name_tails": ("초식", "맹세", "수호", "귀환", "계승", "철야"),
    },
    {
        "id": "slime-confluence", "species": "슬라임", "culture": "합류수 문화",
        "motifs": ("물결", "합류", "맑은 물", "핵의 리듬", "유동하는 기억", "색 변화의 동의"),
        "name_heads": ("맑은핵", "푸른물결", "두갈래", "잔물빛", "깊은합류", "유동"),
        "name_tails": ("공명", "기억", "합류", "보호", "파문", "서약"),
    },
    {
        "id": "vampire-nightcourt", "species": "뱀파이어", "culture": "밤궁정 문화",
        "motifs": ("월식", "혈향", "촛불", "궁정의 맹세", "세기의 기억", "동의의 잔"),
        "name_heads": ("월식", "붉은촛불", "혈향", "검은궁정", "긴밤", "은잔"),
        "name_tails": ("서약", "애가", "기억", "판결", "초대", "계승"),
    },
)

ACTOR_HEADS = ("가", "나", "다", "라", "마", "바", "사", "아", "자", "카", "타", "파", "하", "그", "드", "르", "벨", "세", "오", "유")
ACTOR_TAILS = ("람", "린", "록", "르", "막", "미", "반", "샤", "온", "울", "진", "카", "크", "탄", "펠", "하", "렌", "스", "아", "엔")
STAGES = (("청소년", "adolescent", 13), ("청년", "young-adult", 24), ("장년", "mature-adult", 39), ("노년", "elder", 68))
JOBS = (("대장장이", "대가"), ("의사", "전문가"), ("경비", "기술자"), ("연구원", "전문가"), ("운반자", "숙련"), ("농부", "기술자"), ("재단사", "대가"), ("조율사", "전문가"))
RELATIONS = ("스승의 마지막 제자", "먼 원정에서 돌아온 동반자", "죽은 경비대장의 양자", "첫 도제의 보호자", "세대를 건넌 장비의 계승자", "가구의 맏이")
CATEGORIES = (
    ("multi_perspective", 20000),
    ("equipment_facility_skill_names", 10000),
    ("character_records_monologues", 7500),
    ("rumors_petitions_dialogue", 7500),
    ("cliche_hallucination_corrections", 5000),
)
PROFILE_CYCLES = {
    "multi_perspective": ("MultiPerspective",),
    "equipment_facility_skill_names": ("CharacterSkill", "EvolutionHistory", "FacilityEvolution", "EvolutionHistory", "CharacterSkill"),
    "character_records_monologues": ("CharacterRecord", "Persona", "CharacterRecord", "Persona"),
    "rumors_petitions_dialogue": ("BubbleLine", "SocialRumor", "MacroGoal", "MoodImpulse"),
    "cliche_hallucination_corrections": ("EvolutionHistory", "CharacterSkill", "CharacterRecord", "SocialRumor", "BubbleLine"),
}


def stable_hash(*parts: object) -> str:
    joined = "\x1f".join(str(part) for part in parts)
    return hashlib.sha256(joined.encode("utf-8")).hexdigest()


def yaml_scalar(text: str, key: str) -> str:
    lines = text.splitlines()
    prefix = f"  {key}:"
    for index, line in enumerate(lines):
        if not line.startswith(prefix):
            continue
        value = line[len(prefix):].strip()
        if not value:
            return ""
        if value.startswith('"'):
            combined = value
            cursor = index + 1
            while not combined.endswith('"') and cursor < len(lines):
                combined += " " + lines[cursor].strip()
                cursor += 1
            try:
                return json.loads(combined)
            except json.JSONDecodeError:
                return combined.strip('"')
        return value
    return ""


def read_assets(directory: Path, id_keys: tuple[str, ...], name_keys: tuple[str, ...]) -> list[ContentFact]:
    result: dict[str, ContentFact] = {}
    if not directory.exists():
        return []
    for path in sorted(directory.rglob("*.asset")):
        text = path.read_text(encoding="utf-8")
        stable_id = next((yaml_scalar(text, key) for key in id_keys if yaml_scalar(text, key)), "")
        name = next((yaml_scalar(text, key) for key in name_keys if yaml_scalar(text, key)), "")
        description = yaml_scalar(text, "description")
        if not stable_id or not name:
            continue
        relative = path.relative_to(REPO_ROOT).as_posix()
        result.setdefault(stable_id, ContentFact(stable_id, name, description, relative))
    return sorted(result.values(), key=lambda item: item.stable_id)


def content_inventory() -> dict[str, list[ContentFact]]:
    resources = REPO_ROOT / "Assets/Resources/SO"
    narrative = resources / "V20/Narrative"
    traits = read_assets(resources / "V20/Traits/General", ("stableId", "traitId", "id"), ("displayName", "traitName"))
    traits += read_assets(resources / "Character/Traits", ("stableId", "traitId", "id"), ("displayName", "traitName"))
    normalized_traits = []
    for item in traits:
        stable_id = item.stable_id if ":" in item.stable_id else f"trait:{item.stable_id}"
        normalized_traits.append(ContentFact(stable_id, item.name, item.description, item.asset_path))
    unique_traits = {item.stable_id: item for item in normalized_traits}
    buildings = read_assets(resources / "Building", ("stableId", "buildingId", "id"), ("displayName", "objectName"))
    buildings = [ContentFact(
        item.stable_id if ":" in item.stable_id else f"building:{item.stable_id}",
        item.name, item.description, item.asset_path) for item in buildings]
    return {
        "backgrounds": read_assets(narrative / "Backgrounds", ("stableId",), ("displayName",)),
        "ambitions": read_assets(narrative / "Ambitions", ("stableId",), ("displayName",)),
        "events": read_assets(narrative / "LifeEvents", ("stableId",), ("displayName",)),
        "practices": read_assets(narrative / "Practices", ("stableId",), ("displayName",)),
        "traits": sorted(unique_traits.values(), key=lambda item: item.stable_id),
        "heritable": read_assets(resources / "V20/Traits/Heritable", ("traitId",), ("displayName",)),
        "equipment": read_assets(resources / "Combat/Equipment", ("equipmentId",), ("displayName",)),
        "buildings": buildings,
    }


def require_inventory(inventory: dict[str, list[ContentFact]]) -> None:
    minimums = {"backgrounds": 12, "ambitions": 18, "events": 32, "practices": 20, "traits": 56, "heritable": 24, "equipment": 61, "buildings": 300}
    failures = [f"{key}={len(inventory[key])}<{minimum}" for key, minimum in minimums.items() if len(inventory[key]) < minimum]
    if failures:
        raise SystemExit("Authoritative content inventory is incomplete: " + ", ".join(failures))


def actor_name(index: int, style_index: int) -> str:
    # Independent mixed-radix positions avoid the short 20-name cycle produced
    # when all three syllables were derived from index modulo the same length.
    first = ACTOR_HEADS[(index + style_index * 3) % len(ACTOR_HEADS)]
    middle = ACTOR_TAILS[((index // len(ACTOR_HEADS)) + style_index * 5) % len(ACTOR_TAILS)]
    last = ACTOR_TAILS[((index // (len(ACTOR_HEADS) * len(ACTOR_TAILS))) + style_index * 7 + 3) % len(ACTOR_TAILS)]
    return first + middle + last


def has_batchim(value: str) -> bool:
    if not value:
        return False
    code = ord(value[-1])
    return 0xAC00 <= code <= 0xD7A3 and (code - 0xAC00) % 28 != 0


def attach(value: str, consonant: str, vowel: str) -> str:
    base = value.rstrip("?!.,")
    return base + (consonant if has_batchim(base) else vowel)


def packet_entry(ref: str, fact: ContentFact | None, stable_id: str = "", text: str = "", visibility: str = "public") -> dict:
    if fact is not None:
        stable_id = fact.stable_id
        text = fact.name if not fact.description else f"{fact.name}: {fact.description}"
    return {"ref": ref, "stableId": stable_id, "text": text, "visibility": visibility}


def make_context(index: int, category: str, inventory: dict[str, list[ContentFact]], seed: int) -> dict:
    digest = int(stable_hash(seed, category, index)[:16], 16)
    style_index = digest % len(STYLES)
    style = STYLES[style_index]
    background = inventory["backgrounds"][(digest >> 3) % len(inventory["backgrounds"])]
    ambition = inventory["ambitions"][(digest >> 7) % len(inventory["ambitions"])]
    event = inventory["events"][(digest >> 11) % len(inventory["events"])]
    trait = inventory["traits"][(digest >> 17) % len(inventory["traits"])]
    heritable = inventory["heritable"][(digest >> 23) % len(inventory["heritable"])]
    equipment = inventory["equipment"][(digest >> 29) % len(inventory["equipment"])]
    building = inventory["buildings"][(digest >> 35) % len(inventory["buildings"])]
    stage, stage_id, age = STAGES[(digest >> 41) % len(STAGES)]
    job, rank = JOBS[(digest >> 45) % len(JOBS)]
    relation = RELATIONS[(digest >> 49) % len(RELATIONS)]
    actor = actor_name(index, style_index)
    actor_id = f"character:training:{stable_hash(seed, index)[:16]}"
    other_actor = actor_name(index + 7919, (style_index + 3) % len(STYLES))
    other_actor_id = f"character:training:{stable_hash(seed, index, 'other')[:16]}"
    facts = [
        packet_entry("F01", None, f"fact:identity:{stable_hash(actor_id)[:16]}", f"인물: {actor}, {style['species']}, {style['culture']}"),
        packet_entry("F02", background),
        packet_entry("F03", trait),
        packet_entry("F04", None, f"age-stage:{stage_id}", f"나이: 실제 {age}세, 생애 단계 {stage}"),
        packet_entry("F05", ambition),
        packet_entry("F06", event),
        packet_entry("F07", None, f"career:{job}:{rank}", f"경력: {job} {rank}"),
        packet_entry("F08", None, f"relationship:{stable_hash(relation)[:12]}", f"관계: {relation}"),
        packet_entry("F09", equipment),
        packet_entry("F10", building),
        packet_entry("F11", heritable, visibility="player"),
        packet_entry("F12", None, f"fact:related-character:{stable_hash(other_actor_id)[:16]}", f"관계 인물: {other_actor}, 사건 당시 {relation}"),
    ]
    motif_start = (digest >> 53) % len(style["motifs"])
    motifs = []
    for offset in range(min(6, len(style["motifs"]))):
        motif_index = (motif_start + offset) % len(style["motifs"])
        motifs.append({
            "ref": f"M{offset + 1:02d}",
            "stableId": f"motif:{style['id']}:{motif_index + 1}",
            "text": style["motifs"][motif_index],
        })
    return {
        "digest": digest,
        "style": style,
        "actor": actor,
        "actor_id": actor_id,
        "other_actor": other_actor,
        "other_actor_id": other_actor_id,
        "background": background,
        "ambition": ambition,
        "event": event,
        "trait": trait,
        "heritable": heritable,
        "equipment": equipment,
        "building": building,
        "stage": stage,
        "age": age,
        "job": job,
        "rank": rank,
        "relation": relation,
        "facts": facts,
        "motifs": motifs,
    }


def fixed_rule_contract(profile: str, context: dict, variant: int) -> dict:
    if profile == "MultiPerspective":
        return {"eventId": context["event"].stable_id, "viewpointCharacterIds": [context["actor_id"], context["other_actor_id"]]}
    if profile == "CharacterSkill":
        return {"index": 0, "trigger": "ManualCombat", "target": "Ally", "ultimateDomain": "Defense", "cooldownTurns": 3, "combinationId": "rule:defense:guard", "module": {"pairId": "P01", "moduleId": "guard", "variantId": "standard"}}
    if profile == "EvolutionHistory":
        return {"requestKey": f"history:{stable_hash(context['actor_id'], variant)[:16]}", "targetPersistentId": f"item:{stable_hash(context['equipment'].stable_id, context['actor_id'])[:16]}", "nodeId": "history:protected-owner", "parentNodeId": "", "effectId": "effect:guard-response", "effectBudget": 2, "evidenceIds": ["evidence:protected-owner"]}
    if profile == "FacilityEvolution":
        return {"allowedProposalIds": ["proposal:reliable-shift"], "allowedMutationTags": ["history-marked"]}
    if profile == "Persona":
        return {"selfCareMultiplier": 1.0, "curiosityMultiplier": 1.1, "shoppingMultiplier": 0.9, "patienceMultiplier": 1.05, "hungerCurveMultiplier": 1.0, "funCurveMultiplier": 1.0, "moodCurveMultiplier": 1.05, "preferredFacilityTags": ["rest", "culture"]}
    if profile == "SocialRumor":
        return {"rumorType": "Praise", "targetType": "Character", "targetFacilityId": -1, "targetFacilityTag": "", "targetCharacterId": context["actor_id"], "targetCharacterName": context["actor"], "sentiment": 0.6, "spreadChance": 0.45, "trustImpact": 0.08, "validSeconds": 600}
    if profile == "MacroGoal":
        return {"macroGoal": "Continue", "targetFacilityId": -1, "targetFacilityTag": "", "validSeconds": 120}
    if profile == "MoodImpulse":
        return {"moodImpulse": "FollowRoutine", "strength": 0.55, "targetFacilityId": -1, "targetFacilityTag": "", "validSeconds": 90}
    return {}


def prompt_for(profile: str, context: dict, variant: int) -> str:
    fact_lines = "\n".join(f"{item['ref']} = {item['text']}" for item in context["facts"])
    motif_lines = "\n".join(f"{item['ref']} = {item['text']}" for item in context["motifs"])
    task = {
        "MultiPerspective": "사건을 서로 다른 실제 인물 2명의 관점으로 서술하라. 사실을 추가하지 말고 관점별 감정과 이해 차이를 드러내라.",
        "CharacterSkill": "규칙이 정한 발동·대상·수치를 바꾸지 말고, 인물의 삶에 어울리는 기술명과 설명을 작성하라.",
        "EvolutionHistory": "장비 형태를 고정하지 말고, 사용 기록과 계승 관계를 반영한 계보명·역사 설명을 작성하라.",
        "FacilityEvolution": "제시된 합법 제안 ID만 유지하고, 시설이 겪은 사건과 작업자의 흔적을 판타지적으로 해석하라.",
        "CharacterRecord": "60자 안에서 이 인물을 기억하게 만드는 한 줄 기록을 작성하라.",
        "Persona": "수치는 그대로 복사하고, 특성·출신·나이가 드러나는 별칭과 짧은 인물 설명을 작성하라.",
        "BubbleLine": "현재 사건에 반응하는 자연스러운 한국어 한마디를 작성하라. 내부 ID는 쓰지 마라.",
        "SocialRumor": "규칙 수치는 그대로 복사하고, 제공된 사건에 관한 짧고 구체적인 소문을 작성하라.",
        "MacroGoal": "규칙 행동과 대상을 바꾸지 말고, 현재 목표의 이유만 인물답게 작성하라.",
        "MoodImpulse": "규칙 충동과 강도를 바꾸지 말고, 순간적인 감정 이유만 인물답게 작성하라.",
    }[profile]
    return (
        "당신은 DungeonStory 전용 한국어 서사 표현기다. 규칙과 사실은 아래 패킷이 전부다. "
        "현대 작품의 문구나 고유명사를 흉내 내지 말고 JSON만 반환하라.\n\n"
        f"프로필: {profile}\n문화 문체: {context['style']['culture']}\n과제: {task}\n"
        f"고정 규칙 필드(문자열과 수치를 그대로 복사): {json.dumps(fixed_rule_contract(profile, context, variant), ensure_ascii=False, separators=(',', ':'))}\n\n"
        f"사용 가능한 인물 사실:\n{fact_lines}\n\n사용 가능한 문화 모티프:\n{motif_lines}\n"
    )


def narrative_name(context: dict, salt: int) -> str:
    style = context["style"]
    head = style["name_heads"][(context["digest"] + salt) % len(style["name_heads"])]
    tail = style["name_tails"][(context["digest"] // 7 + salt) % len(style["name_tails"])]
    bridge = context["event"].name.replace(" ", "")[:4]
    return (head + "·" + bridge + " " + tail)[:32]


def grounded_line(context: dict, variant: int, perspective: str = "당사자") -> str:
    actor = context["actor"]
    event = context["event"].name
    trait = context["trait"].name
    motif_a = context["motifs"][variant % len(context["motifs"])]["text"]
    motif_b = context["motifs"][(variant + 2) % len(context["motifs"])]["text"]
    openers = (
        f"{actor}에게 {attach(event, '은', '는')} {motif_a}처럼 남았다.",
        f"{event} 뒤, {attach(actor, '은', '는')} {motif_b} 앞에 오래 머물렀다.",
        f"{perspective}의 기록은 {event}보다 {actor}의 표정을 먼저 적었다.",
        f"{attach(actor, '은', '는')} {attach(event, '을', '를')} 승리라고 부르지 않았다.",
        f"{motif_a}의 흔적이 가시기 전, {attach(actor, '은', '는')} {event}의 자취를 더듬었다.",
        f"그날 {perspective}에게 남은 것은 {event}의 소음보다 {motif_b}의 침묵이었다.",
        f"{event} 직후 {actor}의 손에는 {motif_a}의 흔적이 남아 있었다.",
        f"{perspective}가 돌아본 {event}의 중심에는 늘 {actor}의 선택이 있었다.",
    )
    middles = (
        f"{attach(trait, '은', '는')} 침묵보다 행동을 먼저 고르게 했다.",
        f"{attach(actor, '은', '는')} 자신의 {attach(trait, '을', '를')} 핑계로 숨기지 않았다.",
        f"{context['relation']}라는 관계가 물러설 자리를 허락하지 않았다.",
        f"{context['background'].name}의 기억은 가장 안전한 답을 의심하게 했다.",
        f"{context['stage']}의 시간은 성급한 영광보다 남은 사람을 보게 했다.",
        f"{context['job']} {context['rank']}의 버릇대로 결과보다 원인을 먼저 확인했다.",
        f"{attach(motif_a, '과', '와')} {motif_b} 사이에서 오래된 약속이 다시 무게를 얻었다.",
        f"{attach(context['ambition'].name, '이라는', '라는')} 바람은 그 선택을 개인의 일로 끝내지 못하게 했다.",
    )
    endings = (
        f"{attach(perspective, '은', '는')} 그날 지켜 낸 이름을 결말로 삼았다.",
        f"그래서 기록에는 승패보다 누구를 먼저 돌려보냈는지가 남았다.",
        f"그 뒤 {attach(motif_a, '은', '는')} 약속을 확인하는 표식이 되었다.",
        f"누구도 같은 선택을 칭찬하지는 않았지만, 아무도 그 값을 잊지 않았다.",
        f"{attach(perspective, '이', '가')} 기억한 핵심은 용맹이 아니라 책임이었다.",
        f"그 선택은 훗날 {context['relation']}에게 다른 방식으로 계승되었다.",
        f"기록의 마지막 줄에는 {motif_b} 아래 다시 만나자는 말만 남았다.",
        f"그날의 판단은 {actor}의 경력보다 오래 공동체 안에 머물렀다.",
    )
    selector = context["digest"] + variant * 131
    return " ".join((openers[selector % len(openers)], middles[(selector // 7) % len(middles)], endings[(selector // 43) % len(endings)]))


def references(profile: str) -> tuple[list[str], list[str]]:
    if profile == "BubbleLine":
        return [], []
    facts = ["F02", "F03", "F06"]
    motifs = ["M01", "M03"]
    if profile in ("MacroGoal", "MoodImpulse", "SocialRumor"):
        facts = ["F03", "F06"]
        motifs = ["M01"]
    return facts, motifs


def chosen_payload(profile: str, context: dict, variant: int) -> dict:
    fact_refs, motif_refs = references(profile)
    line = grounded_line(context, variant)
    if profile == "MultiPerspective":
        perspective_variant = variant * 2
        return {
            "eventId": context["event"].stable_id,
            "perspectives": [
                {"viewpointCharacterId": context["actor_id"], "line": grounded_line(context, perspective_variant, "당사자")},
                {"viewpointCharacterId": context["other_actor_id"], "line": grounded_line(context, perspective_variant + 1, context["other_actor"])},
            ],
            "usedMotifIds": motif_refs,
            "usedCharacterFactIds": ["F03", "F06", "F12"],
        }
    if profile == "CharacterSkill":
        return {
            "candidates": [{
                "index": 0, "name": narrative_name(context, variant),
                "description": f"{context['event'].name}의 기억을 되살려 전열을 지킨다.",
                "narrativeReason": line[:180], "trigger": "ManualCombat", "target": "Ally",
                "ultimateDomain": "Defense", "cooldownTurns": 3, "combinationId": "rule:defense:guard",
                "modules": [{"pairId": "P01", "moduleId": "guard", "variantId": "standard"}],
            }],
            "usedMotifIds": motif_refs, "usedCharacterFactIds": fact_refs,
        }
    if profile == "EvolutionHistory":
        return {
            "requestKey": f"history:{stable_hash(context['actor_id'], variant)[:16]}",
            "targetPersistentId": f"item:{stable_hash(context['equipment'].stable_id, context['actor_id'])[:16]}",
            "nodeId": "history:protected-owner", "parentNodeId": "", "effectId": "effect:guard-response",
            "effectBudget": 2, "evidenceIds": ["evidence:protected-owner"],
            "displayName": narrative_name(context, variant),
            "description": f"{context['equipment'].name}에 {context['event'].name}의 선택이 계승되었다.",
            "historyReason": line[:180], "usedMotifIds": motif_refs, "usedCharacterFactIds": fact_refs,
        }
    if profile == "FacilityEvolution":
        proposal = "proposal:reliable-shift"
        return {
            "facilityIdentitySummary": f"{context['building'].name}은 {context['actor']}의 {context['event'].name}을 견딘 작업장이다.",
            "proposalIds": [proposal], "reasons": [{"id": proposal, "reason": line[:220]}],
            "rejectedHints": [], "rejectedHintText": "", "mutationTagSuggestions": ["history-marked"],
            "flavorText": grounded_line(context, variant + 1, "작업자")[:260], "confidence": 0.82,
            "usedMotifIds": motif_refs, "usedCharacterFactIds": fact_refs,
        }
    if profile == "CharacterRecord":
        record = f"{context['event'].name} 뒤 {context['motifs'][0]['text']}을 지킨 {context['job']} {context['actor']}"
        return {"line": record[:60], "usedMotifIds": motif_refs, "usedCharacterFactIds": fact_refs}
    if profile == "Persona":
        return {
            "traitName": narrative_name(context, variant), "flavorText": line[:180],
            "selfCareMultiplier": 1.0, "curiosityMultiplier": 1.1, "shoppingMultiplier": 0.9,
            "patienceMultiplier": 1.05, "hungerCurveMultiplier": 1.0, "funCurveMultiplier": 1.0,
            "moodCurveMultiplier": 1.05, "preferredFacilityTags": ["rest", "culture"],
            "usedMotifIds": motif_refs, "usedCharacterFactIds": fact_refs,
        }
    if profile == "BubbleLine":
        bubbles = (
            f"{context['event'].name}, 이번에는 {context['motifs'][0]['text']}처럼 흘려보내진 않겠어.",
            f"내 {context['trait'].name}이 문제라면, {context['event'].name} 뒤에 직접 고치지.",
            f"{context['motifs'][1]['text']} 앞에서 한 말은 아직 끝나지 않았어.",
            f"{context['job']}답게 보자. {context['event'].name}에도 원인은 있었을 거야.",
            f"{context['stage']}까지 살고 보니, {context['motifs'][2]['text']}보다 사람을 먼저 봐야겠더군.",
            f"오늘 일은 {context['relation']}에게 숨길 수 없겠네.",
            f"{context['event'].name}을 기록해 둬. 다음에는 같은 값을 치르지 않게.",
            f"{context['motifs'][0]['text']}의 약속대로, 먼저 돌아오지 못한 이를 챙기자.",
        )
        return {"line": bubbles[(context["digest"] + variant) % len(bubbles)][:80]}
    if profile == "SocialRumor":
        return {
            "rumorType": "Praise", "targetType": "Character", "targetFacilityId": -1,
            "targetFacilityTag": "", "targetCharacterId": context["actor_id"], "targetCharacterName": context["actor"],
            "sentiment": 0.6, "summary": f"{attach(context['actor'], '이', '가')} {context['event'].name} 때 {context['motifs'][0]['text']}의 약속을 지켰대.",
            "spreadChance": 0.45, "trustImpact": 0.08, "validSeconds": 600,
            "usedMotifIds": motif_refs, "usedCharacterFactIds": fact_refs,
        }
    if profile == "MacroGoal":
        return {
            "macroGoal": "Continue", "reason": line[:180], "targetFacilityId": -1,
            "targetFacilityTag": "", "validSeconds": 120,
            "usedMotifIds": motif_refs, "usedCharacterFactIds": fact_refs,
        }
    if profile == "MoodImpulse":
        return {
            "moodImpulse": "FollowRoutine", "strength": 0.55, "targetFacilityId": -1,
            "targetFacilityTag": "", "reason": line[:180], "validSeconds": 90,
            "usedMotifIds": motif_refs, "usedCharacterFactIds": fact_refs,
        }
    raise ValueError(profile)


def rejected_payload(profile: str, context: dict, chosen: dict, index: int) -> tuple[dict, str]:
    """Build a useful DPO contrast instead of a repeated, trivially bad sentence.

    The 5/3/2 modulo split mirrors the configured preference mix.  Each class
    stays structurally compatible with the chosen payload while failing for a
    different reason: weak specificity, invented facts, or motif enumeration.
    """
    rejected = json.loads(json.dumps(chosen, ensure_ascii=False))
    bucket = (index - 1) % 10
    if bucket < 5:
        negative_type = "generic_safe"
        templates = (
            "{actor}의 선택은 오래도록 기억될 이야기가 되었다.",
            "{event} 뒤, 모두는 새로운 내일을 준비하기 시작했다.",
            "한 사람의 굳은 결심이 공동체의 앞날을 조금씩 바꾸었다.",
            "오래된 약속은 시련을 지나 다시 이어졌다.",
            "그날의 용기와 희생은 사람들의 마음에 깊이 남았다.",
            "위기를 이겨 낸 경험은 다음 걸음을 내딛는 힘이 되었다.",
            "서로를 믿은 이들은 마침내 어려움을 넘어설 수 있었다.",
            "끝까지 포기하지 않은 뜻이 새로운 가능성을 열었다.",
        )
        prose = templates[context["digest"] % len(templates)].format(actor=context["actor"], event=context["event"].name)
    elif bucket < 8:
        negative_type = "fact_distortion"
        inventions = (
            "황제 아르켄의 숨겨진 후계자인 {actor}은 왕좌의 명령으로 {event}을 끝냈다.",
            "천 년 전 죽은 스승 라온이 돌아와 {actor}에게 {event}의 승리를 예언했다.",
            "{actor}은 존재하지 않는 일곱 왕국의 성물을 써서 {event}의 시간을 되돌렸다.",
            "북부 용왕의 혈통을 깨달은 {actor}은 홀로 {event}의 모든 적을 굴복시켰다.",
            "비밀 결사 흑월단의 단주인 {actor}은 예정된 계시대로 {event}을 조종했다.",
        )
        prose = inventions[context["digest"] % len(inventions)].format(actor=context["actor"], event=context["event"].name)
    else:
        negative_type = "motif_listing"
        motifs = [item["text"] for item in context["motifs"][:3]]
        prose = f"{motifs[0]}, {motifs[1]}, 그리고 {motifs[2]}. {context['actor']}의 {context['event'].name}은 이 모티프들을 보여 준다."

    generic_name = f"{('오래된', '빛나는', '마지막', '잊힌', '굳센')[context['digest'] % 5]} {('서약', '힘', '기록', '운명', '기적')[(context['digest'] // 5) % 5]}"
    if profile == "MultiPerspective":
        for perspective_index, perspective in enumerate(rejected["perspectives"]):
            perspective["line"] = f"{prose} 관점 {perspective_index + 1}에서도 같은 의미였다."
    elif profile == "CharacterSkill":
        rejected["candidates"][0]["name"] = generic_name
        rejected["candidates"][0]["description"] = prose
        rejected["candidates"][0]["narrativeReason"] = f"그래서 이 기술은 {generic_name}이라 불린다."
    elif profile == "EvolutionHistory":
        rejected["displayName"] = generic_name
        rejected["description"] = prose
        rejected["historyReason"] = f"이 계보에는 {prose}"
    elif profile == "FacilityEvolution":
        rejected["facilityIdentitySummary"] = f"{generic_name}이라 불리는 시설"
        rejected["flavorText"] = prose
        rejected["reasons"][0]["reason"] = f"시설의 역사가 {prose}"
    elif profile == "CharacterRecord":
        rejected["line"] = prose[:60]
    elif profile == "Persona":
        rejected["traitName"] = generic_name
        rejected["flavorText"] = prose
    elif profile == "BubbleLine":
        rejected["line"] = prose[:80]
    elif profile == "SocialRumor":
        rejected["summary"] = prose
    else:
        rejected["reason"] = prose
    if negative_type == "fact_distortion" and profile != "BubbleLine":
        if "usedCharacterFactIds" in rejected:
            rejected["usedCharacterFactIds"] = ["F99"]
        if "usedMotifIds" in rejected:
            rejected["usedMotifIds"] = ["M99"]
    return rejected, negative_type


def make_record(index: int, category: str, category_index: int, inventory: dict[str, list[ContentFact]], seed: int) -> dict:
    profile = PROFILE_CYCLES[category][category_index % len(PROFILE_CYCLES[category])]
    variant = category_index % 2
    context = make_context(index - variant, category, inventory, seed)
    family_number = category_index // 2
    family_id = f"family:{category}:{family_number:05d}"
    chosen = chosen_payload(profile, context, variant)
    rejected, negative_type = rejected_payload(profile, context, chosen, index)
    source_ids = [
        context["background"].asset_path, context["ambition"].asset_path, context["event"].asset_path,
        context["trait"].asset_path, context["heritable"].asset_path,
        context["equipment"].asset_path, context["building"].asset_path,
    ]
    return {
        "exampleId": f"v25:{index:05d}",
        "scenarioFamilyId": family_id,
        "category": category,
        "profileId": profile,
        "cultureStyleId": context["style"]["id"],
        "factPacket": context["facts"],
        "motifPacket": context["motifs"],
        "prompt": prompt_for(profile, context, variant),
        "chosen": json.dumps(chosen, ensure_ascii=False, separators=(",", ":")),
        "rejected": json.dumps(rejected, ensure_ascii=False, separators=(",", ":")),
        "negativeType": negative_type,
        "viewpointCharacterId": context["actor_id"],
        "eventId": context["event"].stable_id,
        "provenance": "rule_generated",
        "sourceAssetIds": source_ids,
    }


def family_quality(records: list[dict]) -> int:
    score = 0
    for record in records:
        chosen = json.loads(record["chosen"])
        rendered = record["chosen"]
        score += len(set(re.findall(r"[가-힣]{2,}", rendered)))
        score += 10 * sum(item["text"] in rendered for item in record["motifPacket"])
        score += 5 * sum(item["text"].split(":", 1)[0] in rendered for item in record["factPacket"][1:7])
        score -= 20 * rendered.count("전설의 힘")
        score += len(chosen)
    return score


def select_filtered(raw: list[dict]) -> list[dict]:
    by_category: dict[str, dict[str, list[dict]]] = defaultdict(lambda: defaultdict(list))
    for record in raw:
        by_category[record["category"]][record["scenarioFamilyId"]].append(record)
    selected: list[dict] = []
    for category, raw_count in CATEGORIES:
        target_records = int(raw_count * 0.8)
        target_families = target_records // 2
        families = list(by_category[category].items())
        if any(len(records) != 2 for _, records in families):
            raise ValueError(f"{category} contains a non-paired scenario family")
        ranked = sorted(families, key=lambda item: (-family_quality(item[1]), stable_hash(item[0])))
        for _, records in ranked[:target_families]:
            selected.extend(records)
    return sorted(selected, key=lambda item: item["exampleId"])


def allocate_splits(filtered: list[dict], seed: int) -> tuple[list[dict], list[dict], list[dict]]:
    held_targets = {category: int(raw_count * 0.04) for category, raw_count in CATEGORIES}
    preference_targets = {category: int(raw_count * 0.12) for category, raw_count in CATEGORIES}
    by_category: dict[str, dict[str, list[dict]]] = defaultdict(lambda: defaultdict(list))
    for record in filtered:
        by_category[record["category"]][record["scenarioFamilyId"]].append(record)
    held: list[dict] = []
    preference: list[dict] = []
    train: list[dict] = []
    for category, _ in CATEGORIES:
        families = sorted(by_category[category].items(), key=lambda item: stable_hash(seed, "split", item[0]))
        held_family_count = held_targets[category] // 2
        preference_family_count = preference_targets[category] // 2
        held_ids = {family_id for family_id, _ in families[:held_family_count]}
        preference_ids = {family_id for family_id, _ in families[held_family_count:held_family_count + preference_family_count]}
        for family_id, records in families:
            target = held if family_id in held_ids else preference if family_id in preference_ids else train
            split = "held_out" if target is held else "preference_train" if target is preference else "sft_train"
            for record in records:
                copied = dict(record)
                copied["split"] = split
                target.append(copied)
    train_all = sorted(train + preference, key=lambda item: item["exampleId"])
    return train_all, sorted(preference, key=lambda item: item["exampleId"]), sorted(held, key=lambda item: item["exampleId"])


def write_jsonl(path: Path, records: Iterable[dict]) -> None:
    if path.suffix == ".gz":
        with path.open("wb") as raw_stream:
            with gzip.GzipFile(filename="", mode="wb", fileobj=raw_stream, mtime=0, compresslevel=6) as compressed:
                with io.TextIOWrapper(compressed, encoding="utf-8", newline="\n") as stream:
                    for record in records:
                        stream.write(json.dumps(record, ensure_ascii=False, separators=(",", ":")) + "\n")
        return
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        for record in records:
            stream.write(json.dumps(record, ensure_ascii=False, separators=(",", ":")) + "\n")


def trl_projection(record: dict) -> dict:
    return {
        "id": record["exampleId"],
        "prompt": [{"role": "system", "content": "DungeonStory 규칙 사실만 표현하고 JSON만 반환한다."}, {"role": "user", "content": record["prompt"]}],
        "completion": [{"role": "assistant", "content": record["chosen"]}],
        "scenarioFamilyId": record["scenarioFamilyId"],
        "profileId": record["profileId"],
        "cultureStyleId": record["cultureStyleId"],
    }


def write_review_package(output: Path, review: list[dict], chunk_size: int) -> None:
    review_dir = output / "review"
    review_dir.mkdir(parents=True, exist_ok=True)
    fields = [
        "review_id", "split", "category", "profile_id", "culture_style", "event_id", "viewpoint_character_id",
        "fact_summary", "motif_summary", "prompt", "candidate_a", "candidate_b",
        "verdict", "selected_candidate", "rewrite", "issue_tags", "reviewer_note",
    ]
    keys = []
    rows = []
    for index, record in enumerate(review):
        chosen_is_a = int(stable_hash(record["exampleId"], "candidate-order")[-1], 16) % 2 == 0
        candidate_a = record["chosen"] if chosen_is_a else record["rejected"]
        candidate_b = record["rejected"] if chosen_is_a else record["chosen"]
        rows.append({
            "review_id": f"R{index + 1:05d}", "split": record["split"], "category": record["category"],
            "profile_id": record["profileId"], "culture_style": record["cultureStyleId"],
            "event_id": record["eventId"], "viewpoint_character_id": record["viewpointCharacterId"],
            "fact_summary": " | ".join(f"{item['ref']}={item['text']}" for item in record["factPacket"]),
            "motif_summary": " | ".join(f"{item['ref']}={item['text']}" for item in record["motifPacket"]),
            "prompt": record["prompt"], "candidate_a": candidate_a, "candidate_b": candidate_b,
            "verdict": "", "selected_candidate": "", "rewrite": "", "issue_tags": "", "reviewer_note": "",
        })
        keys.append({
            "reviewId": f"R{index + 1:05d}", "exampleId": record["exampleId"],
            "scenarioFamilyId": record["scenarioFamilyId"], "split": record["split"],
            "systemPreferred": "A" if chosen_is_a else "B",
        })
    for offset in range(0, len(rows), chunk_size):
        path = review_dir / f"review_{offset + 1:05d}_{min(offset + chunk_size, len(rows)):05d}.csv"
        with path.open("w", encoding="utf-8-sig", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=fields)
            writer.writeheader()
            writer.writerows(rows[offset:offset + chunk_size])
    write_jsonl(review_dir / "review_key_8000.jsonl.gz", keys)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def distribution(records: list[dict], key: str) -> dict[str, int]:
    return dict(sorted(Counter(str(record[key]) for record in records).items()))


def narrative_strings(profile: str, payload: dict) -> list[str]:
    """Return only player-facing prose, excluding IDs and fixed rule fields."""
    if profile == "MultiPerspective":
        return [item["line"] for item in payload["perspectives"]]
    if profile == "CharacterSkill":
        candidate = payload["candidates"][0]
        return [candidate["name"], candidate["description"], candidate["narrativeReason"]]
    if profile == "EvolutionHistory":
        return [payload["displayName"], payload["description"], payload["historyReason"]]
    if profile == "FacilityEvolution":
        return [
            payload["facilityIdentitySummary"],
            *(item["reason"] for item in payload["reasons"]),
            payload["flavorText"],
        ]
    if profile in ("CharacterRecord", "BubbleLine"):
        return [payload["line"]]
    if profile == "Persona":
        return [payload["traitName"], payload["flavorText"]]
    if profile == "SocialRumor":
        return [payload["summary"]]
    if profile in ("MacroGoal", "MoodImpulse"):
        return [payload["reason"]]
    raise ValueError(f"Unknown profile for prose audit: {profile}")


def corpus_quality_audit(records: list[dict]) -> dict:
    prose: list[str] = []
    rejected_prose: list[str] = []
    profile_counts: Counter[str] = Counter()
    negative_types: Counter[str] = Counter()
    for record in records:
        payload = json.loads(record["chosen"])
        values = narrative_strings(record["profileId"], payload)
        prose.extend(value.strip() for value in values if value.strip())
        rejected_values = narrative_strings(record["profileId"], json.loads(record["rejected"]))
        rejected_prose.extend(value.strip() for value in rejected_values if value.strip())
        negative_types[record["negativeType"]] += 1
        profile_counts[record["profileId"]] += len(values)

    token_pattern = re.compile(r"[가-힣]+|[A-Za-z]+|[0-9]+")
    tokens = [token for text in prose for token in token_pattern.findall(text)]
    frequencies = Counter(tokens)
    total_tokens = len(tokens)
    entropy = 0.0
    if total_tokens:
        entropy = -sum((count / total_tokens) * math.log2(count / total_tokens) for count in frequencies.values())

    bigrams = [(tokens[index], tokens[index + 1]) for index in range(max(0, total_tokens - 1))]
    trigrams = [(tokens[index], tokens[index + 1], tokens[index + 2]) for index in range(max(0, total_tokens - 2))]
    exact_duplicate_count = len(prose) - len(set(prose))
    long_prose = [text for text in prose if len(text) >= 40]
    long_duplicate_count = len(long_prose) - len(set(long_prose))
    generic_phrases = ("전설의 운명", "운명이 깨어나", "모든 것을 바꾸")
    generic_count = sum(any(phrase in text for phrase in generic_phrases) for text in prose)
    fixed_fallback = "전설의 운명이 깨어나 모든 것을 바꾸었다."
    rejected_fixed_fallback_count = sum(text == fixed_fallback for text in rejected_prose)
    korean_count = sum(bool(re.search(r"[가-힣]", text)) for text in prose)
    return {
        "recordCount": len(records),
        "proseFieldCount": len(prose),
        "profileProseFieldCounts": dict(sorted(profile_counts.items())),
        "koreanCoverage": round(korean_count / len(prose), 6) if prose else 0.0,
        "exactDuplicateCount": exact_duplicate_count,
        "exactDuplicateRate": round(exact_duplicate_count / len(prose), 6) if prose else 0.0,
        "longProseFieldCount": len(long_prose),
        "longProseExactDuplicateCount": long_duplicate_count,
        "longProseExactDuplicateRate": round(long_duplicate_count / len(long_prose), 6) if long_prose else 0.0,
        "tokenCount": total_tokens,
        "uniqueTokenCount": len(frequencies),
        "vocabularyEntropyBits": round(entropy, 6),
        "distinct2": round(len(set(bigrams)) / len(bigrams), 6) if bigrams else 0.0,
        "distinct3": round(len(set(trigrams)) / len(trigrams), 6) if trigrams else 0.0,
        "genericChosenCount": generic_count,
        "rejectedProseFieldCount": len(rejected_prose),
        "rejectedUniqueProseRate": round(len(set(rejected_prose)) / len(rejected_prose), 6) if rejected_prose else 0.0,
        "rejectedFixedFallbackCount": rejected_fixed_fallback_count,
        "negativeTypeCounts": dict(sorted(negative_types.items())),
        "gate": {
            "koreanCoverageIsComplete": korean_count == len(prose),
            "genericChosenIsZero": generic_count == 0,
            "repeatedFixedRejectedFallbackIsZero": rejected_fixed_fallback_count == 0,
            "allHardNegativeTypesPresent": set(negative_types) == {"generic_safe", "fact_distortion", "motif_listing"},
            "longProseExactDuplicateRateAtMostTwoPercent": long_duplicate_count <= len(long_prose) * 0.02,
        },
    }


def build(output: Path, clean: bool) -> dict:
    config = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    seed = int(config["seed"])
    inventory = content_inventory()
    require_inventory(inventory)
    allowed_root = (REPO_ROOT / "Artifacts/Training").resolve()
    require_safe = output == allowed_root or allowed_root in output.parents
    if clean and output.exists() and not require_safe:
        raise SystemExit(f"Refusing to clean output outside {allowed_root}: {output}")
    if clean and output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True, exist_ok=True)

    raw = []
    global_index = 0
    for category, count in CATEGORIES:
        for category_index in range(count):
            raw.append(make_record(global_index, category, category_index, inventory, seed))
            global_index += 1
    filtered = select_filtered(raw)
    train, preference, held = allocate_splits(filtered, seed)
    review = sorted(preference + held, key=lambda item: stable_hash(seed, "review", item["exampleId"]))

    if len(raw) != 50000 or len(filtered) != 40000 or len(train) != 38000 or len(preference) != 6000 or len(held) != 2000 or len(review) != 8000:
        raise SystemExit(f"Count contract failed: raw={len(raw)} filtered={len(filtered)} train={len(train)} preference={len(preference)} held={len(held)} review={len(review)}")

    write_jsonl(output / "raw_scenarios_50000.jsonl.gz", raw)
    write_jsonl(output / "filtered_pool_40000.jsonl.gz", filtered)
    write_jsonl(output / "sft_train_candidates_38000.jsonl.gz", train)
    write_jsonl(output / "preference_review_candidates_6000.jsonl.gz", preference)
    write_jsonl(output / "held_out_review_candidates_2000.jsonl.gz", held)
    write_jsonl(output / "trl_sft_train_38000.jsonl.gz", (trl_projection(record) for record in train))
    write_review_package(output, review, int(config["dataset"]["review_chunk_size"]))

    inventory_json = {
        key: [{"stableId": item.stable_id, "name": item.name, "assetPath": item.asset_path} for item in values]
        for key, values in inventory.items()
    }
    (output / "content_inventory.json").write_text(json.dumps(inventory_json, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    shutil.copy2(SOURCE_PATH, output / "sources.json")
    quality_audit = corpus_quality_audit(filtered)
    if not all(quality_audit["gate"].values()):
        raise SystemExit(f"Corpus quality gate failed: {json.dumps(quality_audit['gate'], ensure_ascii=False)}")
    (output / "corpus_audit.json").write_text(json.dumps(quality_audit, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    files = sorted(path for path in output.rglob("*") if path.is_file())
    manifest = {
        "formatVersion": 1,
        "seed": seed,
        "counts": {"raw": len(raw), "filtered": len(filtered), "sftTrainCandidates": len(train), "preferenceReview": len(preference), "heldOutReview": len(held), "humanReviewRows": len(review)},
        "rawCategoryDistribution": distribution(raw, "category"),
        "filteredCategoryDistribution": distribution(filtered, "category"),
        "reviewProfileDistribution": distribution(review, "profileId"),
        "reviewCultureDistribution": distribution(review, "cultureStyleId"),
        "contentInventoryCounts": {key: len(values) for key, values in inventory.items()},
        "corpusAudit": quality_audit,
        "humanApprovalClaimed": False,
        "files": [{"path": path.relative_to(output).as_posix(), "bytes": path.stat().st_size, "sha256": sha256(path)} for path in files],
    }
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return manifest


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=REPO_ROOT / "Artifacts/Training/V25")
    parser.add_argument("--no-clean", action="store_true")
    args = parser.parse_args()
    manifest = build(args.output.resolve(), not args.no_clean)
    print(json.dumps(manifest["counts"], ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
