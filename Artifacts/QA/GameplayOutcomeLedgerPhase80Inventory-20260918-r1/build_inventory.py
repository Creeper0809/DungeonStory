#!/usr/bin/env python3
"""Deterministic static inventory for Phase80 gameplay outcome producers.

This intentionally does not execute Unity or load the project assemblies.  It is
a conservative lexical source audit: uncertain authority or dispatch is emitted
as needs-authority-fix/unknown rather than promoted to connected coverage.
"""

from __future__ import annotations

import csv
import argparse
import hashlib
import json
import re
import shutil
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable


ROOT = Path(__file__).resolve().parents[3]
DEFAULT_OUT = Path(__file__).resolve().parent
SCRIPTS = ROOT / "Assets" / "Scripts"


TYPE_RE = re.compile(
    r"(?m)^\s*(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|ref|unsafe|new)\s+)*"
    r"(?P<kind>class|struct|interface|record(?:\s+(?:class|struct))?)\s+"
    r"(?P<name>[A-Za-z_]\w*(?:Receipt|Event|Result|Outcome|Resolution))(?P<generic>\s*<[^>{;\r\n]+>)?\b"
)
NAMESPACE_RE = re.compile(r"(?m)^\s*namespace\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)")
PUBLISH_RE = re.compile(
    r"(?P<receiver>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*(?:\?\.|\.)\s*Publish\s*"
    r"(?:<\s*(?P<generic>[^>\r\n]+)\s*>)?\s*\("
)
RAW_PUBLISH_RE = re.compile(r"(?:\?\.|\.)\s*Publish\s*(?:<\s*([^>\r\n]+)\s*>)?\s*\(")
SUBSCRIBE_RE = re.compile(r"(?<![A-Za-z0-9_])(?:[A-Za-z_]\w*\s*\.\s*)?Subscribe\s*<\s*([A-Za-z_]\w*(?:<[^>]+>)?)\s*>")


GROUPS = (
    (1, "작업·생산·품질·연구·건설·수리", (
        "Work", "Production", "Recipe", "Research", "Blueprint", "Build", "Repair",
        "Synthesis", "Quality", "FacilityOutput", "Buffer", "Industrial", "ManualWater",
    )),
    (2, "전투·침입·의료·생포·사망", (
        "Combat", "Damage", "Killed", "Died", "Death", "Injured", "Health", "Medical",
        "Surgery", "Invasion", "Captive", "Captivity", "Prisoner", "Defense", "Blood",
    )),
    (3, "원정·외부 활동·세력·사건·축제", (
        "Expedition", "Offense", "Exterior", "Faction", "Festival", "Seasonal", "RunVariable",
        "RunFlow", "Milestone", "WorldMap", "RouteArrived", "RouteCargo", "RouteSettlement",
    )),
    (4, "관계·욕구·기분·생애·가족", (
        "Relationship", "Social", "Apology", "Life", "Age", "Career", "Proficiency", "Rest",
        "Meal", "Water", "Survival", "Mental", "Taboo", "Funeral", "Lineage", "Mood",
    )),
    (5, "거래·재고·재정·서비스·손님", (
        "Treasury", "Economy", "Transaction", "Shop", "Purchase", "Revenue", "Stock", "Guest",
        "Visitor", "Customer", "Supply", "Delivery", "Resource", "Item", "Disposition",
        "Relocation", "Tare", "Cargo",
    )),
    (6, "장비·특성·기술·시설 진화", (
        "Equipment", "Evolution", "Trait", "Skill", "MemoryErasure", "Apparel", "FacilityEvolution",
        "Catalyst", "Acquired",
    )),
    (7, "환경·농업·야생동물·질병·재난", (
        "Environment", "Fire", "Crop", "Seed", "Wildlife", "Disease", "Infection", "Animal",
        "Disaster", "Climate", "RoomCondition",
    )),
)


SAVE_OWNER_BY_GROUP = {
    1: (
        "WorkOrdersSaveSection",
        "ProductionBillsSaveSection",
        "ProductionPreparedOutputRoutingSaveSection",
        "BlueprintResearchSaveSection",
        "ModularFacilityWorldSaveSection",
    ),
    2: (
        "CharacterBodyHealthSaveSection",
        "CharacterMedicalSaveSection",
        "SurgerySaveSection",
        "CharacterCombatCommandSaveSection",
        "InvasionSaveSection",
        "CaptivitySaveSection",
    ),
    3: (
        "OffenseAggregateSaveSection",
        "FactionSaveSection",
        "SeasonalWorldEventsSaveSection",
        "RunVariableSaveSection",
        "RunFlowSaveSection",
        "ExteriorActivitySaveSection",
    ),
    4: (
        "CharacterLifeSaveSection",
        "CharacterPsychosocialSaveSection",
        "CharacterWorldSaveSection",
        "CharacterNarrativeSaveSection",
        "SurvivalResourcesSaveSection",
        "DarkSurvivalSaveSection",
    ),
    5: (
        "TreasuryEconomySaveSection",
        "PhysicalItemsSaveSection",
        "FacilityShopSaveSection",
        "OperatingDaySettlementSaveSection",
        "RegularCustomerSaveSection",
        "WorldResourceSaveSection",
    ),
    6: (
        "CombatEquipmentSaveSection",
        "EquipmentEvolutionSaveSection",
        "CharacterWorldSaveSection",
        "CharacterNarrativeSaveSection",
        "ModularFacilityWorldSaveSection",
    ),
    7: (
        "CropEcologySaveSection",
        "CropPlotSaveSection",
        "WildlifeSaveSection",
        "PopulationHealthSaveSection",
        "EnvironmentalFireSaveSection",
        "EnvironmentalFieldSaveSection",
        "CharacterEnvironmentSaveSection",
    ),
    0: (),
}


NON_RESULT_NAME_FRAGMENTS = (
    "RequestedEvent",
    "CandidateEvent",
    "WarningEvent",
    "DiagnosticEvent",
    "TraceEvent",
    "InfoFeedEvent",
    "NoticeFeedEvent",
    "PublicModelEvent",
    "PublicEvent",
    "EventAlertLoggedEvent",
    "EventAlertSourceResolvedEvent",
    "OperatingDayStartedEvent",
    "OperatingDayEndedEvent",
    "CodexUpdatedEvent",
    "WorkStartedIdentityEvent",
    "CharacterPrimitiveSurvivalStartedEvent",
    "SettlementActiveIncidentsChangedEvent",
    "SettlementCommittedAlertChangedEvent",
)
EMBEDDED_DETAIL_TYPES = {
    "ProductionRecipeExecutionOutputLineReceipt",
    "ProductionRecipeExecutionPhysicalSliceReceipt",
    "ProductionPreparedOutputPhysicalRouteSliceReceipt",
    "FacilityOutputExactRouteSliceReceipt",
    "FactionRouteQuoteLineReceipt",
}
NON_RESULT_REASONS = {
    "AutomationCaptureResult": "dungeon-player automation screenshot transport DTO; not a gameplay state transition",
    "CharacterAcquiredTraitManifestationDiagnosticEvent": "diagnostic-only manifestation trace; no gameplay state authority",
    "CharacterAiRuntimeTraceEvent": "AI diagnostics trace; not a committed gameplay result",
    "CharacterAiDecisionTickResult": "AI decision-loop control result; authoritative gameplay changes are recorded at the selected command owner",
    "CharacterGrowthTabRequestedEvent": "UI navigation request",
    "CharacterInfectionBurdenReductionRequestedEvent": "mutation request/command input; capture the resulting burden transition instead",
    "CharacterInfectionBurdenRequestedEvent": "mutation request/command input; capture the resulting burden transition instead",
    "CharacterMentalInstabilityBurdenRequestedEvent": "mutation request/command input; capture the resulting burden transition instead",
    "CodexUpdatedEvent": "derived codex/UI invalidation notification",
    "CodexFeatureCommandResult": "presentation command wrapper containing only success and UI message",
    "DungeonDebugCommandResult": "debug-console command response; not runtime gameplay truth",
    "DungeonDurableSaveCommitResult": "persistence-maintenance acknowledgement; does not represent an in-world result",
    "EventAlertLoggedEvent": "derived alert-log notification; source gameplay outcome must be captured upstream",
    "EventAlertRequestedEvent": "UI alert request, not authoritative result",
    "EventAlertSourceResolvedEvent": "UI alert lifecycle signal; no live producer found",
    "FestivalOutcomeEvent": "dormant legacy identity-notification contract with no live producer; the authoritative festival result is FestivalCelebratedEvent",
    "GameplayEffectProjectionResult": "pure effect-value projection; record the later authoritative effect application instead",
    "GridPathSearchResult": "pure navigation search result; movement completion or failure is the gameplay outcome",
    "HostStartupResult": "local-LLM host lifecycle result; not an in-world gameplay result",
    "InfoFeedEvent": "building-summary UI presentation request",
    "InvasionCandidateEvent": "unselected invasion candidate/preview",
    "InvasionThreatWarningEvent": "warning notification before a resolved outcome",
    "NarrativePublicEvent": "LLM/public narrative context DTO, not gameplay truth",
    "NarrativePublicModelEvent": "LLM model input DTO, not gameplay truth",
    "KoreanJosaFormatResult": "text-formatting projection result; not an in-world gameplay result",
    "LocalLlmChoiceResult": "model response/validation result; only an accepted owner commit can become gameplay truth",
    "LocalLlmResult": "model transport result; only an accepted owner commit can become gameplay truth",
    "NarrativeFormulaDrawbackResolution": "mechanic candidate-resolution value before the authoritative owner commit",
    "NarrativeFormulaSelectedModuleResolution": "mechanic module-selection value before the authoritative owner commit",
    "NoticeFeedEvent": "UI notice request",
    "OperationsFeatureCommandResult": "presentation command wrapper containing only success and UI message",
    "OperatingDayEndedEvent": "scheduler/day-boundary lifecycle signal",
    "OperatingDayStartedEvent": "scheduler/day-boundary lifecycle signal",
    "OperatingDayReportEvent": "derived daily report publication; record the committed facility visit/revenue/stock/crime/restock results instead",
    "RunResultReadyEvent": "derived run-result readiness notification; record the committed run end and reward results instead",
    "InvasionCombatReportReadyEvent": "derived combat-report readiness notification; record damage, breach, facility loss, death, and invasion resolution at their authorities instead",
    "DungeonRunFlowEvent": "run-flow reducer input/projection envelope; record its authoritative source transitions instead",
    "ResearchProgressEvent": "incremental work/progress signal; record the completed research result and compacted progress metrics instead",
    "BossInvasionStartedEvent": "invasion lifecycle start signal; capture the later committed invasion results",
    "InvasionStartedEvent": "invasion lifecycle start signal; capture spawn/breach/damage/resolution results",
    "InvasionSpawnedEvent": "invasion actor-spawn lifecycle notification; capture authoritative combat and invasion results",
    "InvasionFinalCombatStartedEvent": "combat phase start signal; capture committed damage/death/resolution results",
    "WorkStartedIdentityEvent": "work start/progress signal; capture completion/cancellation result",
    "CharacterPrimitiveSurvivalStartedEvent": "action start signal; capture completion/failure result",
    "SettlementActiveIncidentsChangedEvent": "derived settlement-alert invalidation notification",
    "SettlementCommittedAlertChangedEvent": "derived settlement-alert invalidation notification",
    "WasteFeedResult": "unused waste-feed contract with no live producer; no committed gameplay transition exists to retain",
}


PARTICIPANT_WORDS = (
    "actor", "character", "worker", "attacker", "killer", "victim", "defender", "target",
    "patient", "healer", "customer", "visitor", "guest", "captive", "performer", "owner",
    "facility", "building", "equipment", "item", "faction", "expedition", "wildlife", "subject",
    "source", "beneficiary", "witness", "intruder", "researcher", "staff",
)
METRIC_WORDS = (
    "amount", "quantity", "count", "damage", "health", "mass", "quality", "cost", "price",
    "revenue", "progress", "trust", "burden", "duration", "hours", "score", "value", "severity",
    "potency", "work", "loss", "supply", "money", "stock", "day", "stage", "state", "outcome",
)


BUS_RECEIVER_NAMES = {
    "gameEventBus", "eventBus", "events", "identityEvents", "gameEvents",
    "GameEventBus", "EventBus", "Events", "CustomerGameEventBus",
}
NON_BUS_RECEIVER_NAMES = {
    "characterObjectFactory", "objectFactory", "characterFactory", "factory", "projection", "port",
    "fixture", "stateful", "unique", "stable", "malformedRoute", "RestoreLifecycle",
    "restoreCoordinator", "RestoreCoordinator", "applicationAdapter", "characterObjects",
}


# Every pre-existing candidate is assigned to one of plan section 12's seven
# domain groups.  Phase80's own infrastructure is assigned -1 and never counted
# as a pre-existing producer.  These overrides also prevent generic names such
# as *ReportEvent from falling into an ambiguous group 0 bucket.
DOMAIN_GROUP_OVERRIDES = {
    "AutomationCaptureResult": 3,
    "BackgroundInitializationOutcome": 4,
    "CharacterAiDecisionTickResult": 4,
    "CharacterActivityEvent": 4,
    "CharacterAiRuntimeTraceEvent": 4,
    "CharacterGrowthTabRequestedEvent": 4,
    "CodexUpdatedEvent": 3,
    "CodexFeatureCommandResult": 3,
    "ContentResolutionResult": 3,
    "DungeonDebugCommandResult": 3,
    "DungeonDurableSaveCommitResult": 3,
    "DungeonSpaceExpansionResult": 1,
    "EventAlertLoggedEvent": 3,
    "EventAlertRequestedEvent": 3,
    "EventAlertSourceResolvedEvent": 3,
    "ExternalInfluencePressureResult": 3,
    "FacilityCrimeEvent": 5,
    "FacilityVisitEvent": 5,
    "ICharacterIdentityEvent": 4,
    "GameplayEffectProjectionResult": 6,
    "GridPathSearchResult": 3,
    "HostStartupResult": 3,
    "KoreanJosaFormatResult": 4,
    "LocalLlmChoiceResult": 6,
    "LocalLlmResult": 6,
    "NarrativeFormulaDrawbackResolution": 6,
    "NarrativeFormulaSelectedModuleResolution": 6,
    "NarrativePublicEvent": 3,
    "NarrativePublicModelEvent": 3,
    "NoticeFeedEvent": 3,
    "OperationsFeatureCommandResult": 3,
    "OperatingDayEndedEvent": 5,
    "OperatingDayReportEvent": 5,
    "OperatingDayStartedEvent": 5,
    "RunResultReadyEvent": 3,
    "ServiceModeChangeResult": 5,
    "SpeciesIncidentTriggeredEvent": 7,
    "V20ContentEffectsResolvedEvent": 3,
    "V20ResolvedEventResult": 3,
}


@dataclass(frozen=True)
class Site:
    path: str
    line: int
    owner_type: str
    owner_symbol: str
    evidence: str


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def source_tree_digest(files: Iterable[Path]) -> str:
    digest = hashlib.sha256()
    for path in files:
        raw = path.read_bytes()
        digest.update(rel(path).encode("utf-8"))
        digest.update(b"\0")
        digest.update(hashlib.sha256(raw).digest())
    return digest.hexdigest()


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def cs_files() -> list[Path]:
    result = []
    for path in SCRIPTS.rglob("*.cs"):
        relative_parts = path.relative_to(SCRIPTS).parts
        if any(part.lower() == "editor" for part in relative_parts):
            continue
        result.append(path)
    return sorted(result, key=lambda value: rel(value).lower())


def strip_comments_keep_lines(text: str) -> str:
    # Preserve offsets/newlines so line numbers remain stable.
    def repl_block(match: re.Match[str]) -> str:
        value = match.group(0)
        return "".join("\n" if ch == "\n" else " " for ch in value)

    text = re.sub(r"/\*.*?\*/", repl_block, text, flags=re.S)
    text = re.sub(r"//[^\r\n]*", lambda m: " " * len(m.group(0)), text)
    return text


def line_number(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


def find_matching(text: str, open_offset: int, open_char: str, close_char: str) -> int:
    depth = 0
    in_string = False
    verbatim = False
    escaped = False
    for idx in range(open_offset, len(text)):
        ch = text[idx]
        if in_string:
            if verbatim:
                if ch == '"' and idx + 1 < len(text) and text[idx + 1] == '"':
                    continue
                if ch == '"':
                    in_string = False
                    verbatim = False
            elif escaped:
                escaped = False
            elif ch == "\\":
                escaped = True
            elif ch == '"':
                in_string = False
            continue
        if ch == '"':
            in_string = True
            verbatim = idx > 0 and text[idx - 1] == "@"
            continue
        if ch == open_char:
            depth += 1
        elif ch == close_char:
            depth -= 1
            if depth == 0:
                return idx
    return len(text) - 1


def owner_at(lines: list[str], one_based_line: int) -> tuple[str, str]:
    type_name = "<file>"
    symbol = "<unknown>"
    type_re = re.compile(r"\b(?:class|struct|interface|record(?:\s+(?:class|struct))?)\s+([A-Za-z_]\w*)")
    method_re = re.compile(
        r"^\s*(?:(?:public|private|protected|internal|static|virtual|override|sealed|async|extern|new|partial|readonly)\s+)+"
        r"(?:[A-Za-z_]\w*(?:[.<>,?\[\]]|\s)++\s+)?(?P<name>[A-Za-z_]\w*)\s*(?:<[^>]+>)?\s*\("
    )
    control = {"if", "for", "foreach", "while", "switch", "catch", "using", "lock", "return", "new"}
    start = min(len(lines), max(0, one_based_line - 1))
    for idx in range(start, max(-1, start - 500), -1):
        type_match = type_re.search(lines[idx])
        if type_match and type_name == "<file>":
            type_name = type_match.group(1)
        method_match = method_re.search(lines[idx])
        if method_match and method_match.group("name") not in control:
            symbol = method_match.group("name")
            break
    if type_name == "<file>":
        for idx in range(start, -1, -1):
            type_match = type_re.search(lines[idx])
            if type_match:
                type_name = type_match.group(1)
                break
    return type_name, symbol


def domain_group(name: str, path: str) -> tuple[int, str]:
    if is_phase80_infrastructure(path, name):
        return -1, "Phase80 구현 인프라(기존 생산자 아님)"
    if name in DOMAIN_GROUP_OVERRIDES:
        number = DOMAIN_GROUP_OVERRIDES[name]
        return number, next(title for group, title, _ in GROUPS if group == number)
    explicit = (
        (7, "환경·농업·야생동물·질병·재난", ("Environment", "Wildlife", "Disease", "Infection", "Crop", "CertifiedSeed", "RoomCondition")),
        (6, "장비·특성·기술·시설 진화", ("Equipment", "Evolution", "Trait", "Apparel", "Skill", "MemoryErasure")),
        (2, "전투·침입·의료·생포·사망", ("Combat", "Invasion", "Medical", "Surgery", "Captive", "Prisoner", "Defense", "Death", "Died", "Killed", "Injured", "Health")),
        (3, "원정·외부 활동·세력·사건·축제", ("Expedition", "Offense", "Faction", "Festival", "Seasonal", "Exterior")),
        (1, "작업·생산·품질·연구·건설·수리", ("Production", "Work", "Research", "Blueprint", "Synthesis", "Quality", "FacilityOutput", "FacilityBuffer", "ManualWater")),
        (4, "관계·욕구·기분·생애·가족", ("Relationship", "Social", "Apology", "Life", "Career", "Proficiency", "RestOutcome", "Meal", "Survival", "Mental", "Taboo", "Funeral", "Lineage")),
        (5, "거래·재고·재정·서비스·손님", ("Treasury", "Economy", "Transaction", "Shop", "Purchase", "Revenue", "Stock", "Guest", "Visitor", "Customer", "Supply", "Delivery", "Resource", "Item", "Disposition", "Relocation", "Warehouse", "Tare")),
    )
    for number, title, tokens in explicit:
        if any(token in name for token in tokens):
            return number, title
    normalized_path = path.replace("\\", "/")
    if "/Infrastructure/Industrial/" in normalized_path:
        return 1, "작업·생산·품질·연구·건설·수리"
    haystack = f"{name} {path}".lower()
    best: tuple[int, int, str] = (0, 0, "미분류")
    for number, title, tokens in GROUPS:
        score = sum(1 for token in tokens if token.lower() in haystack)
        if score > best[0]:
            best = (score, number, title)
    return best[1], best[2]


def classify(name: str, kind: str, path: str, producer_count: int) -> tuple[str, str, str, str, str]:
    lowered = path.lower()
    if is_phase80_infrastructure(lowered, name):
        return (
            "NonResult",
            "Phase80 implementation infrastructure notification/contract; not a pre-existing domain result producer",
            "N/A",
            "infrastructure",
            "new-phase80-infrastructure",
        )
    if name == "CharacterActivityEvent":
        return "NonResult", "legacy character-log projection DTO; plan explicitly rejects it as gameplay truth authority", "NonResult", "legacy-projection", "excluded"
    if name in NON_RESULT_REASONS:
        return "NonResult", NON_RESULT_REASONS[name], "NonResult", "N/A", "excluded"
    if kind == "interface":
        return "NonResult", "contract/interface definition; not an emitted committed result", "NonResult", "N/A", "excluded"
    if name in EMBEDDED_DETAIL_TYPES:
        return "NonResult", "embedded detail row of a parent receipt; its parent receipt is the replacement authority", "NonResult", "N/A", "excluded"
    if any(fragment in name for fragment in NON_RESULT_NAME_FRAGMENTS):
        return "NonResult", "request/preview/UI/diagnostic/lifecycle notification rather than committed gameplay result", "NonResult", "N/A", "excluded"
    if any(part in lowered for part in ("/debug", "/diagnostic", "debugscenario", "tests/", "/test")):
        return "NonResult", "debug/test/diagnostic-only source; no runtime gameplay authority", "NonResult", "N/A", "excluded"
    if name == "ExpectedReceipt":
        return "NonResult", "private restore-guard expectation object; the actual equipment material commit is the replacement authority", "NonResult", "N/A", "excluded"
    if producer_count == 0:
        return "needs-authority-fix", "result-like definition has no lexically proven live producer", "Record", "planned", "needs-authority-fix"
    return (
        "needs-authority-fix",
        "result-like producer exists but no GameplayOutcome adapter/descriptor/joint outbox is implemented",
        "Record",
        "planned",
        "transaction-integration-pending",
    )


PHASE80_INFRASTRUCTURE_TYPES_OUTSIDE_NARRATIVE = {
    # Generic owner-facing participant introduced by Phase80. It deliberately
    # lives with the physical-item authority rather than the Narrative folder.
    "IPreparedPhysicalItemGameplayOutcome",
}


def is_phase80_infrastructure(path: str, type_name: str) -> bool:
    normalized = path.replace("\\", "/").lower()
    return "/services/narrative/gameplayoutcomes/" in normalized \
        or "/services/narrative/gameplayoutcomebridges/" in normalized \
        or type_name in PHASE80_INFRASTRUCTURE_TYPES_OUTSIDE_NARRATIVE


def extract_members(type_text: str, name: str) -> list[str]:
    members: set[str] = set()
    for match in re.finditer(
        r"(?m)^\s*(?:(?:public|internal|private|protected)\s+)(?:(?:readonly|static|const)\s+)*"
        r"[A-Za-z_]\w*(?:[.<>,?\[\]]|\s)+\s+(?P<member>[A-Za-z_]\w*)\s*(?:[;{=])",
        type_text,
    ):
        members.add(match.group("member"))
    constructor_re = re.compile(rf"\b{re.escape(name)}\s*\((?P<params>.*?)\)\s*(?:\{{|:)", re.S)
    for ctor in constructor_re.finditer(type_text):
        params = ctor.group("params")
        for part in re.split(r",(?=(?:[^<>]|<[^<>]*>)*$)", params):
            cleaned = re.sub(r"\b(?:in|out|ref|params|this)\b", " ", part)
            cleaned = cleaned.split("=")[0].strip()
            tokens = re.findall(r"[A-Za-z_]\w*", cleaned)
            if tokens:
                members.add(tokens[-1])
    return sorted(members, key=str.lower)


def candidate_members(members: Iterable[str], words: tuple[str, ...]) -> list[str]:
    return sorted(
        {member for member in members if any(word in member.lower() for word in words)},
        key=str.lower,
    )


def build_save_catalog(file_data: dict[Path, tuple[str, str]]) -> list[dict]:
    rows: list[dict] = []
    seen: set[tuple[str, str]] = set()
    shared_ids: dict[str, str] = {}
    for _, (original, _) in file_data.items():
        if "class DungeonSaveSectionIds" not in original:
            continue
        shared_ids.update(dict(re.findall(
            r"public\s+const\s+string\s+([A-Za-z_]\w*)\s*=\s*\"([^\"]+)\"",
            original,
        )))
    declaration = re.compile(r"\b(?:class|struct)\s+([A-Za-z_]\w*SaveSection)\b")
    for path, (original, stripped) in file_data.items():
        for match in declaration.finditer(stripped):
            name = match.group(1)
            if name == "DungeonStrictJsonSaveSection":
                continue
            start = match.start()
            open_brace = stripped.find("{", match.end())
            end = find_matching(stripped, open_brace, "{", "}") if open_brace >= 0 else match.end()
            body = original[start : end + 1]
            id_match = re.search(r"\b(?:public|private|internal)?\s*(?:const|static readonly)\s+string\s+(?:Id|SectionId)\s*=\s*\"([^\"]+)\"", body)
            shared_match = re.search(r"\b(?:Id|SectionId)\s*=\s*DungeonSaveSectionIds\.([A-Za-z_]\w*)", body)
            key = (rel(path), name)
            if key in seen:
                continue
            seen.add(key)
            rows.append({
                "save_section": name,
                "section_id": (
                    id_match.group(1)
                    if id_match
                    else shared_ids.get(shared_match.group(1), "<shared-id-unresolved>")
                    if shared_match
                    else "<resolved-via-shared-id-or-not-lexical>"
                ),
                "path": rel(path),
                "line": line_number(stripped, start),
            })
    return sorted(rows, key=lambda row: (row["save_section"].lower(), row["path"].lower()))


def association_save_owners(group: int, save_catalog: list[dict]) -> list[dict]:
    wanted = set(SAVE_OWNER_BY_GROUP.get(group, ()))
    result = [row for row in save_catalog if row["save_section"] in wanted]
    return sorted(result, key=lambda row: row["save_section"])


def receiver_is_known_non_event_publisher(receiver_leaf: str, path_value: str) -> bool:
    """Separate domain object publication from IGameEventBus publication.

    The repository uses the verb Publish for object factories, restore
    coordinators, test fixtures, and surgical projections as well as for the
    game event bus.  Treating all of them as an event producer was the main
    source of the provisional scan's unresolved receiver bucket.
    """
    if receiver_leaf in NON_BUS_RECEIVER_NAMES:
        return True
    lowered = receiver_leaf.lower()
    if any(token in lowered for token in (
        "factory", "fixture", "projection", "restore", "lifecycle", "characterobjects",
    )):
        return True
    return any(token in path_value for token in (
        "/Diagnostics/", "DebugScenarios.cs",
    ))


def distinct_sites(sites: Iterable[dict]) -> list[dict]:
    by_key: dict[tuple[str, int, str, str], dict] = {}
    for site in sites:
        key = (site["path"], int(site["line"]), site["owner_type"], site["owner_symbol"])
        by_key.setdefault(key, site)
    return sorted(by_key.values(), key=lambda row: (row["path"].lower(), row["line"], row["owner_symbol"]))


def record_coverage_reason(name: str, kind: str) -> str:
    if name.endswith("Receipt"):
        return (
            f"{name} is a result-bearing {kind} carrying exact post-operation data; "
            "retain it as a Record candidate, subject to owner commit proof and duplicate suppression."
        )
    return (
        f"{name} is a result-bearing {kind} for a completed/resolved gameplay state transition; "
        "retain it as a Record candidate rather than a request, preview, start, progress, or UI notification."
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--final-frozen",
        action="store_true",
        help="Assert the orchestrator declared all production writers READY/FROZEN before this scan.",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=DEFAULT_OUT,
        help=(
            "Write generated evidence to this directory. Use a new versioned "
            "directory for every later pass; the original evidence is immutable."
        ),
    )
    args = parser.parse_args()
    out = args.output_dir.resolve()
    out.mkdir(parents=True, exist_ok=True)
    source_script = Path(__file__).resolve()
    if out != source_script.parent:
        shutil.copy2(source_script, out / "build_inventory.py")
        source_review = source_script.parent / "REVIEW.md"
        if source_review.exists():
            shutil.copy2(source_review, out / "REVIEW.md")
    files = cs_files()
    file_data: dict[Path, tuple[str, str]] = {}
    source_digest = hashlib.sha256()
    for path in files:
        raw = path.read_bytes()
        text = raw.decode("utf-8-sig")
        stripped = strip_comments_keep_lines(text)
        file_data[path] = (text, stripped)
        source_digest.update(rel(path).encode("utf-8"))
        source_digest.update(b"\0")
        source_digest.update(hashlib.sha256(raw).digest())

    save_catalog = build_save_catalog(file_data)
    save_paths = {row["path"] for row in save_catalog}

    definitions: list[dict] = []
    by_base: dict[str, list[dict]] = {}
    for path, (original, stripped) in file_data.items():
        namespace_match = NAMESPACE_RE.search(stripped)
        namespace = namespace_match.group(1) if namespace_match else "<global>"
        lines = original.splitlines()
        for match in TYPE_RE.finditer(stripped):
            base_name = match.group("name")
            generic = re.sub(r"\s+", "", match.group("generic") or "")
            display_name = base_name + generic
            open_brace = stripped.find("{", match.end())
            end = find_matching(stripped, open_brace, "{", "}") if open_brace >= 0 else match.end()
            type_text = original[match.start() : end + 1]
            members = extract_members(type_text, base_name)
            entry = {
                "type": display_name,
                "base_type": base_name,
                "kind": match.group("kind"),
                "namespace": namespace,
                "definition_path": rel(path),
                "definition_line": line_number(stripped, match.start()),
                "members": members,
                "participant_member_candidates": candidate_members(members, PARTICIPANT_WORDS),
                "metric_member_candidates": candidate_members(members, METRIC_WORDS),
                "constructor_sites": [],
                "factory_sites": [],
                "publish_sites": [],
                "subscriber_sites": [],
                "ui_consumers": [],
                "query_consumers": [],
                "narrative_consumers": [],
                "other_use_paths": [],
            }
            definitions.append(entry)
            by_base.setdefault(base_name, []).append(entry)

    # Lexically index type construction/use.  Scan each file once rather than
    # each type against every file; this keeps the audit bounded on ~2k files.
    base_names = set(by_base)
    base_union_text = "|".join(sorted((re.escape(name) for name in by_base), key=len, reverse=True))
    declaration_union = re.compile(
        r"\b(?P<type>" + base_union_text + r")(?:\s*<[^>]+>)?\s+(?P<name>[A-Za-z_]\w*)\b"
    )
    for path, (original, stripped) in file_data.items():
        present = sorted(base_names.intersection(re.findall(r"\b[A-Za-z_]\w*\b", stripped)))
        if not present:
            continue
        path_value = rel(path)
        lines = original.splitlines()
        for base_name in present:
            constructors = list(re.finditer(
                rf"\bnew\s+(?:[A-Za-z_]\w*\.)*{re.escape(base_name)}(?:\s*<[^>]+>)?\s*[({{]"
                rf"|\b{re.escape(base_name)}(?:\s*<[^>]+>)?\s+[A-Za-z_]\w*\s*=\s*new\s*[({{]",
                stripped,
            ))
            static_factory_calls = list(re.finditer(
                rf"\b{re.escape(base_name)}\s*\.\s*(?P<factory>[A-Za-z_]\w*)\s*\(",
                stripped,
            ))
            return_factories = list(re.finditer(
                rf"(?m)^\s*(?:(?:public|internal|private|protected|static|virtual|override|sealed|async|new|readonly)\s+)+"
                rf"{re.escape(base_name)}(?:\s*<[^>]+>)?\s+(?P<factory>[A-Za-z_]\w*)\s*\(",
                stripped,
            ))
            for entry in by_base[base_name]:
                if path_value != entry["definition_path"]:
                    entry["other_use_paths"].append(path_value)
                if "/Views/" in f"/{path_value}" or any(token in path_value for token in ("Panel", "Presenter", "UI/")):
                    entry["ui_consumers"].append(path_value)
                if "Query" in path_value or "Query" in original:
                    entry["query_consumers"].append(path_value)
                if any(token in path_value for token in ("Narrative", "CharacterLog", "EvolutionRecord", "IdentityDomainAdapter")):
                    entry["narrative_consumers"].append(path_value)
                for constructed in constructors:
                    ln = line_number(stripped, constructed.start())
                    owner_type, owner_symbol = owner_at(lines, ln)
                    entry["constructor_sites"].append(asdict(Site(
                        path_value,
                        ln,
                        owner_type,
                        owner_symbol,
                        lines[ln - 1].strip()[:240],
                    )))
                for factory in static_factory_calls + return_factories:
                    ln = line_number(stripped, factory.start())
                    owner_type, owner_symbol = owner_at(lines, ln)
                    entry["factory_sites"].append(asdict(Site(
                        path_value,
                        ln,
                        owner_type,
                        factory.group("factory"),
                        lines[ln - 1].strip()[:240],
                    )))

    publish_calls: list[dict] = []
    raw_publish_lexical_count = 0
    for path, (original, stripped) in file_data.items():
        path_value = rel(path)
        lines = original.splitlines()
        declared_bus_names = set(re.findall(r"\bIGameEventBus\s+([A-Za-z_]\w*)", stripped))
        variable_types: dict[str, str] = {
            declared.group("name"): declared.group("type")
            for declared in declaration_union.finditer(stripped)
        }
        identified_open_offsets: set[int] = set()
        for match in PUBLISH_RE.finditer(stripped):
            receiver = match.group("receiver")
            receiver_leaf = receiver.split(".")[-1]
            open_paren = match.end() - 1
            identified_open_offsets.add(open_paren)
            close_paren = find_matching(stripped, open_paren, "(", ")")
            argument = stripped[open_paren + 1 : close_paren].strip()
            event_type = (match.group("generic") or "").strip()
            resolution = "generic-type-argument" if event_type else ""
            if not event_type:
                new_match = re.match(r"\(?\s*new\s+(?:[A-Za-z_]\w*\.)*(?P<type>[A-Za-z_]\w*)", argument)
                if new_match:
                    event_type = new_match.group("type")
                    resolution = "new-expression"
                else:
                    identifier = re.match(r"\(?\s*(?P<name>[A-Za-z_]\w*)\s*\)?\s*$", argument, re.S)
                    if identifier and identifier.group("name") in variable_types:
                        event_type = variable_types[identifier.group("name")]
                        resolution = "lexical-variable-declaration"
                    else:
                        event_type = "<unresolved>"
                        resolution = "unresolved-argument"
            if receiver_leaf in declared_bus_names:
                bus_confidence = "yes:declared-IGameEventBus"
            elif receiver_leaf in BUS_RECEIVER_NAMES:
                bus_confidence = "likely:name-convention"
            elif receiver_is_known_non_event_publisher(receiver_leaf, path_value):
                bus_confidence = "no:known-non-event-publisher"
            elif path_value.endswith("GameEventBus.cs") and receiver_leaf == "channel":
                bus_confidence = "no:internal-event-channel-dispatch"
            else:
                bus_confidence = "unknown:requires-type-resolution"
            if event_type == "<unresolved>" and bus_confidence.startswith("no:"):
                event_type = "<non-game-event-method>"
                resolution = "resolved-non-event-publisher"
            elif event_type == "<unresolved>" and path_value.endswith("BuildableObject.cs") and receiver_leaf == "GameEventBus":
                event_type = "<generic:TEvent>"
                resolution = "generic-wrapper-type-parameter"
            elif event_type == "<unresolved>" and path_value.endswith("BlueprintResearchApplicationAdapter.cs") and receiver_leaf == "gameEventBus":
                event_type = "<generic:TEvent>"
                resolution = "generic-wrapper-type-parameter"
            ln = line_number(stripped, match.start())
            owner_type, owner_symbol = owner_at(lines, ln)
            call = {
                "path": path_value,
                "line": ln,
                "owner_type": owner_type,
                "owner_symbol": owner_symbol,
                "receiver": receiver,
                "event_type": event_type,
                "type_resolution": resolution,
                "game_event_bus": bus_confidence,
                "evidence": lines[ln - 1].strip()[:240],
            }
            publish_calls.append(call)
            base_event_type = re.sub(r"<.*", "", event_type).split(".")[-1]
            for entry in by_base.get(base_event_type, []):
                entry["publish_sites"].append(call)

        raw_matches = list(RAW_PUBLISH_RE.finditer(stripped))
        raw_publish_lexical_count += len(raw_matches)
        for raw_match in raw_matches:
            open_paren = raw_match.end() - 1
            if open_paren in identified_open_offsets:
                continue
            close_paren = find_matching(stripped, open_paren, "(", ")")
            argument = stripped[open_paren + 1 : close_paren].strip()
            event_type = (raw_match.group(1) or "").strip()
            resolution = "generic-type-argument" if event_type else "unresolved-complex-receiver"
            if not event_type:
                new_match = re.match(r"\(?\s*new\s+(?:[A-Za-z_]\w*\.)*(?P<type>[A-Za-z_]\w*)", argument)
                if new_match:
                    event_type = new_match.group("type")
                    resolution = "new-expression"
                elif path_value.endswith("CharacterBodyHealthStateRules.cs"):
                    event_type = "CharacterDeathEvent"
                    resolution = "resolved-death-event-factory-chain"
                elif path_value.endswith("GameEventBus.cs"):
                    event_type = "<generic:TEvent>"
                    resolution = "internal-event-channel-generic-dispatch"
                else:
                    event_type = "<non-game-event-method>"
                    resolution = "resolved-non-event-complex-publisher"
            ln = line_number(stripped, raw_match.start())
            owner_type, owner_symbol = owner_at(lines, ln)
            call = {
                "path": path_value,
                "line": ln,
                "owner_type": owner_type,
                "owner_symbol": owner_symbol,
                "receiver": "<complex-or-unresolved>",
                "event_type": event_type,
                "type_resolution": resolution,
                "game_event_bus": (
                    "yes:death-event-factory-chain"
                    if path_value.endswith("CharacterBodyHealthStateRules.cs")
                    else "no:internal-event-channel-dispatch"
                    if path_value.endswith("GameEventBus.cs")
                    else "no:known-non-event-complex-publisher"
                ),
                "evidence": lines[ln - 1].strip()[:240],
            }
            publish_calls.append(call)
            base_event_type = re.sub(r"<.*", "", event_type).split(".")[-1]
            for entry in by_base.get(base_event_type, []):
                entry["publish_sites"].append(call)

        for match in SUBSCRIBE_RE.finditer(stripped):
            event_type = re.sub(r"<.*", "", match.group(1)).split(".")[-1]
            ln = line_number(stripped, match.start())
            owner_type, owner_symbol = owner_at(lines, ln)
            site = asdict(Site(
                path_value,
                ln,
                owner_type,
                owner_symbol,
                lines[ln - 1].strip()[:240],
            ))
            for entry in by_base.get(event_type, []):
                entry["subscriber_sites"].append(site)

    # Final classification and conservative authority/save conclusions.
    for entry in definitions:
        producer_sites = distinct_sites(entry["publish_sites"] + entry["constructor_sites"] + entry["factory_sites"])
        classification, reason, coverage_disposition, intended, integration_status = classify(
            entry["base_type"], entry["kind"], entry["definition_path"], len(producer_sites)
        )
        group_number, group_title = domain_group(entry["base_type"], entry["definition_path"])
        exact_save_refs = [
            row for row in save_catalog
            if entry["definition_path"] == row["path"]
            or any(use_path == row["path"] for use_path in entry["other_use_paths"])
        ]
        outbox_evidence = []
        boundary_terms = []
        for site in producer_sites:
            path = ROOT / site["path"]
            source = file_data.get(path, ("", ""))[0]
            lines = source.splitlines()
            lo = max(0, site["line"] - 35)
            hi = min(len(lines), site["line"] + 35)
            context = "\n".join(lines[lo:hi])
            if re.search(r"\b(?:Outbox|pendingCommit|batchCommitId|TryCommit|Acknowledge|Rollback)\b", context, re.I):
                outbox_evidence.append(f"{site['path']}:{site['line']}")
            if re.search(r"\b(?:Apply|Commit|Execute|Finalize|Complete|Resolve|Acknowledge)\b", context):
                boundary_terms.append(f"{site['path']}:{site['line']}")

        if classification == "NonResult":
            boundary = "N/A: excluded non-result signal/detail"
            joint_commit = "N/A"
        elif outbox_evidence:
            boundary = "existing domain commit/outbox terms nearby; no gameplay-outcome joint outbox"
            joint_commit = "possible via existing transaction/outbox adapter; requires owner-specific proof"
        elif entry["publish_sites"]:
            boundary = "post-state observer publish; no durable result/outcome joint commit found"
            joint_commit = "requires detached transaction or rollback-capable owner integration before publish"
        else:
            boundary = "receipt construction/capture only; exact commit boundary not proven"
            joint_commit = "needs owner-specific transaction audit"

        entry.update({
            "inventory_scope": "phase80-implementation-infrastructure"
            if is_phase80_infrastructure(entry["definition_path"], entry["base_type"])
                else "pre-existing-runtime-candidate",
            "domain_group": group_number,
            "domain_group_name": group_title,
            "classification": classification,
            "classification_reason": reason,
            "coverage_disposition": coverage_disposition,
            "coverage_reason": (
                reason if coverage_disposition != "Record"
                else record_coverage_reason(entry["base_type"], entry["kind"])
            ),
            "intended_ledger_status": intended,
            "integration_status": integration_status,
            "integration_reason": (
                "No lexically proven producer exists; establish or remove the authoritative producer before integration."
                if integration_status == "needs-authority-fix"
                else "A semantic Record candidate exists, but owner-specific TryPrepare/joint outbox/save/replay proof is absent."
                if integration_status == "transaction-integration-pending"
                else reason
            ),
            "distinct_producer_sites": producer_sites,
            "current_transaction_boundary": boundary,
            "existing_outbox_or_commit_evidence": sorted(set(outbox_evidence)),
            "mutation_boundary_evidence": sorted(set(boundary_terms)),
            "joint_commit_feasibility": joint_commit,
            "outcome_save_owner": "none-found: receipt/event is not persisted as a GameplayOutcome",
            "exact_save_section_references": exact_save_refs,
            "domain_state_save_owner_candidates": association_save_owners(group_number, save_catalog),
            "current_ui_query_narrative_consumer": {
                "ui_paths": sorted(set(entry["ui_consumers"]), key=str.lower),
                "query_paths": sorted(set(entry["query_consumers"]), key=str.lower),
                "narrative_paths": sorted(set(entry["narrative_consumers"]), key=str.lower),
                "subscribers": sorted(entry["subscriber_sites"], key=lambda row: (row["path"].lower(), row["line"])),
            },
        })
        for key in (
            "constructor_sites", "publish_sites", "subscriber_sites", "ui_consumers",
            "factory_sites", "query_consumers", "narrative_consumers", "other_use_paths",
        ):
            if key.endswith("_sites"):
                entry[key] = sorted(entry[key], key=lambda row: (row["path"].lower(), row["line"]))
            elif isinstance(entry[key], list) and entry[key] and isinstance(entry[key][0], str):
                entry[key] = sorted(set(entry[key]), key=str.lower)

    definitions.sort(key=lambda row: (row["base_type"].lower(), row["definition_path"].lower(), row["definition_line"]))
    publish_calls.sort(key=lambda row: (row["path"].lower(), row["line"]))

    final_files = cs_files()
    final_source_digest = source_tree_digest(final_files)
    snapshot_stable = (
        [rel(path) for path in files] == [rel(path) for path in final_files]
        and source_digest.hexdigest() == final_source_digest
    )

    metadata = {
        "schema": "gameplay-outcome-ledger-producer-inventory@1",
        "generated_date": "2026-09-18",
        "scan_kind": "static-lexical-no-unity-execution",
        "source_commit": git("rev-parse", "HEAD"),
        "source_branch": git("branch", "--show-current"),
        "source_dirty_entry_count": len(git("status", "--porcelain=v1").splitlines()),
        "source_scan_sha256": source_digest.hexdigest(),
        "source_scan_end_sha256": final_source_digest,
        "source_snapshot_stable_during_scan": snapshot_stable,
        "run_mode": "final-frozen" if args.final_frozen else "provisional-writers-not-frozen",
        "completeness_claim": (
            "complete-lexical-scan-with-listed-limitations"
            if args.final_frozen and snapshot_stable
            else "withheld-source-mutated-during-scan"
            if not snapshot_stable
            else "withheld-production-writers-not-frozen"
        ),
        "non_editor_cs_file_count": len(files),
        "receipt_event_result_outcome_resolution_definition_count": len(definitions),
        "receipt_event_definition_count": sum(
            row["base_type"].endswith(("Receipt", "Event")) for row in definitions
        ),
        "result_outcome_resolution_definition_count": sum(
            row["base_type"].endswith(("Result", "Outcome", "Resolution"))
            for row in definitions
        ),
        "raw_publish_call_count": len(publish_calls),
        "raw_publish_lexical_count": raw_publish_lexical_count,
        "publish_call_capture_complete": len(publish_calls) == raw_publish_lexical_count,
        "resolved_publish_event_type_count": sum(row["event_type"] != "<unresolved>" for row in publish_calls),
        "unresolved_publish_event_type_count": sum(row["event_type"] == "<unresolved>" for row in publish_calls),
        "likely_game_event_bus_publish_count": sum(
            row["game_event_bus"].startswith(("yes:", "likely:")) for row in publish_calls
        ),
        "unknown_publish_receiver_count": sum(row["game_event_bus"].startswith("unknown:") for row in publish_calls),
        "classification_counts": {
            value: sum(row["classification"] == value for row in definitions)
            for value in ("Record", "NonResult", "needs-authority-fix")
        },
        "coverage_disposition_counts": {
            value: sum(row["coverage_disposition"] == value for row in definitions)
            for value in ("Record", "NonResult", "N/A")
        },
        "integration_status_counts": {
            value: sum(row["integration_status"] == value for row in definitions)
            for value in ("excluded", "needs-authority-fix", "transaction-integration-pending", "connected", "new-phase80-infrastructure")
        },
        "unclassified_domain_definition_count": sum(row["domain_group"] == 0 for row in definitions),
        "phase80_implementation_infrastructure_definition_count": sum(
            row["inventory_scope"] == "phase80-implementation-infrastructure" for row in definitions
        ),
        "limitations": [
            "Lexical scan does not compile C# and cannot resolve every receiver, alias, reflection call, or delegated variable type.",
            "Constructor use proves object creation only; it does not prove domain commit or gameplay reachability.",
            "Save-owner candidates are domain-state owners, not proof that the receipt/event itself is persisted.",
            "The initial pre-implementation probe found no GameplayOutcome symbols; Phase80 infrastructure created concurrently is separated by inventory_scope and is not treated as a pre-existing producer.",
            "Coverage disposition and integration state are separate axes: semantic Record candidates remain planned/transaction-integration-pending until owner-specific joint outbox evidence exists.",
        ],
    }

    inventory = {"metadata": metadata, "definitions": definitions}
    (out / "producer-inventory.json").write_text(
        json.dumps(inventory, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (out / "publish-calls.json").write_text(
        json.dumps({"metadata": metadata, "calls": publish_calls}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    publish_columns = (
        "path", "line", "owner_type", "owner_symbol", "receiver", "event_type",
        "type_resolution", "game_event_bus", "evidence",
    )
    with (out / "publish-calls.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=publish_columns)
        writer.writeheader()
        writer.writerows(publish_calls)
    (out / "save-section-catalog.json").write_text(
        json.dumps({"metadata": metadata, "save_sections": save_catalog}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    csv_columns = (
        "type", "kind", "definition_path", "definition_line", "inventory_scope", "domain_group", "domain_group_name",
        "classification", "classification_reason", "coverage_disposition", "coverage_reason",
        "intended_ledger_status", "integration_status", "integration_reason",
        "producer_count", "producer_sites", "publish_count", "subscriber_count",
        "participant_member_candidates", "metric_member_candidates", "current_transaction_boundary",
        "joint_commit_feasibility", "outcome_save_owner", "domain_state_save_owner_candidates",
        "ui_paths", "query_paths", "narrative_paths",
    )
    with (out / "producer-inventory.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=csv_columns)
        writer.writeheader()
        for row in definitions:
            producers = row["distinct_producer_sites"]
            writer.writerow({
                "type": row["type"],
                "kind": row["kind"],
                "definition_path": row["definition_path"],
                "definition_line": row["definition_line"],
                "inventory_scope": row["inventory_scope"],
                "domain_group": row["domain_group"],
                "domain_group_name": row["domain_group_name"],
                "classification": row["classification"],
                "classification_reason": row["classification_reason"],
                "coverage_disposition": row["coverage_disposition"],
                "coverage_reason": row["coverage_reason"],
                "intended_ledger_status": row["intended_ledger_status"],
                "integration_status": row["integration_status"],
                "integration_reason": row["integration_reason"],
                "producer_count": len(producers),
                "producer_sites": " | ".join(f"{site['path']}:{site['line']}:{site['owner_type']}.{site['owner_symbol']}" for site in producers),
                "publish_count": len(row["publish_sites"]),
                "subscriber_count": len(row["subscriber_sites"]),
                "participant_member_candidates": " | ".join(row["participant_member_candidates"]),
                "metric_member_candidates": " | ".join(row["metric_member_candidates"]),
                "current_transaction_boundary": row["current_transaction_boundary"],
                "joint_commit_feasibility": row["joint_commit_feasibility"],
                "outcome_save_owner": row["outcome_save_owner"],
                "domain_state_save_owner_candidates": " | ".join(owner["save_section"] for owner in row["domain_state_save_owner_candidates"]),
                "ui_paths": " | ".join(row["current_ui_query_narrative_consumer"]["ui_paths"]),
                "query_paths": " | ".join(row["current_ui_query_narrative_consumer"]["query_paths"]),
                "narrative_paths": " | ".join(row["current_ui_query_narrative_consumer"]["narrative_paths"]),
            })

    integration_columns = (
        "domain_group", "domain_group_name", "type", "kind", "definition_path", "definition_line",
        "coverage_reason", "integration_status", "integration_reason", "producer_count", "producer_sites",
        "current_transaction_boundary", "joint_commit_feasibility", "outcome_save_owner",
        "domain_state_save_owner_candidates", "participant_member_candidates", "metric_member_candidates",
        "ui_paths", "query_paths", "narrative_paths", "subscriber_count",
    )
    with (out / "record-integration-input.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=integration_columns)
        writer.writeheader()
        for row in definitions:
            if row["coverage_disposition"] != "Record":
                continue
            producers = row["distinct_producer_sites"]
            writer.writerow({
                "domain_group": row["domain_group"],
                "domain_group_name": row["domain_group_name"],
                "type": row["type"],
                "kind": row["kind"],
                "definition_path": row["definition_path"],
                "definition_line": row["definition_line"],
                "coverage_reason": row["coverage_reason"],
                "integration_status": row["integration_status"],
                "integration_reason": row["integration_reason"],
                "producer_count": len(producers),
                "producer_sites": " | ".join(f"{site['path']}:{site['line']}:{site['owner_type']}.{site['owner_symbol']}" for site in producers),
                "current_transaction_boundary": row["current_transaction_boundary"],
                "joint_commit_feasibility": row["joint_commit_feasibility"],
                "outcome_save_owner": row["outcome_save_owner"],
                "domain_state_save_owner_candidates": " | ".join(owner["save_section"] for owner in row["domain_state_save_owner_candidates"]),
                "participant_member_candidates": " | ".join(row["participant_member_candidates"]),
                "metric_member_candidates": " | ".join(row["metric_member_candidates"]),
                "ui_paths": " | ".join(row["current_ui_query_narrative_consumer"]["ui_paths"]),
                "query_paths": " | ".join(row["current_ui_query_narrative_consumer"]["query_paths"]),
                "narrative_paths": " | ".join(row["current_ui_query_narrative_consumer"]["narrative_paths"]),
                "subscriber_count": len(row["subscriber_sites"]),
            })

    (out / "metadata.json").write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    artifact_hashes = {}
    for name in (
        "build_inventory.py",
        "REVIEW.md",
        "metadata.json",
        "producer-inventory.csv",
        "producer-inventory.json",
        "publish-calls.csv",
        "publish-calls.json",
        "record-integration-input.csv",
        "save-section-catalog.json",
    ):
        artifact_hashes[name] = sha256_bytes((out / name).read_bytes())
    (out / "artifact-sha256.json").write_text(
        json.dumps({"sha256": artifact_hashes}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(metadata, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
