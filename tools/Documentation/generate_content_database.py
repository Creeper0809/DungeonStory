#!/usr/bin/env python3
"""Generate code-grounded DungeonStory content databases without invoking Unity."""

from __future__ import annotations

import argparse
import csv
import json
import re
import shutil
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Iterable

try:
    import yaml
except ImportError as exc:  # pragma: no cover - environment failure path
    raise SystemExit("PyYAML is required to generate the content database.") from exc

from knowledge_manifest import write_generation_manifest


ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
OUTPUT = ROOT / "docs" / "content-db"

UNITY_HEADER = re.compile(r"^--- !u![^\r\n ]+ &[^\r\n ]+", re.MULTILINE)
CLASS_SUFFIX = re.compile(r"(?:::)?([^:]+)$")
ID_TEXT = re.compile(r"^[a-z0-9][a-z0-9._:/-]*$", re.IGNORECASE)

IGNORED_CLASSES = {
    "ItemDefinitionCatalogSO",
    "GameContentCatalogSO",
    "GameDomainContentCatalogSO",
    "GameMediaCatalogSO",
    "LocalizationSettings",
    "StringTable",
    "SharedTableData",
    "StringTableCollection",
    "Locale",
    "AddressableAssetGroup",
    "AddressableAssetSettings",
    "AddressableAssetSettingsDefaultObject",
    "AddressableAssetGroupTemplate",
    "ProfileDataSourceSettings",
    "BundledAssetGroupSchema",
    "ContentUpdateGroupSchema",
    "BuildScriptPackedPlayMode",
    "BuildScriptFastMode",
    "BuildScriptPackedMode",
    "VolumeProfile",
    "ExternalBehaviorTree",
}

GROUPS: dict[str, set[str]] = {
    "items": {
        "GenericItemDefinitionSO",
        "ResourceItemDefinitionSO",
        "SaleItem",
        "StockInfo",
    },
    "production-facilities": {
        "BuildingSO",
        "ProductionRecipeSO",
        "FacilitySynthesisRecipeSO",
        "FacilityEvolutionRecipeSO",
        "ServiceProcessSO",
        "CropDefinitionSO",
        "CropGenomeDefinitionSO",
        "CraftMaterialDefinitionSO",
        "TextileMaterialDefinitionSO",
        "ApparelDefinitionSO",
        "EquipmentModuleDefinitionSO",
        "EnvironmentalWorkwearSO",
        "FacilityBlueprintSO",
        "FacilityEvolutionRecordTokenDefinitionSO",
    },
    "characters-traits": {
        "CharacterTraitSO",
        "HeritableTraitDefinitionSO",
        "CharacterSpeciesSO",
        "CharacterSpeciesDefinitionSO",
        "SpeciesCultureDefinitionSO",
        "CharacterStartingOriginSO",
        "CharacterStartingHistorySO",
        "CharacterBackgroundDefinitionSO",
        "CharacterAmbitionDefinitionSO",
        "CareerPositionDefinitionSO",
        "AgeConditionDefinitionSO",
        "ReproductionProfileSO",
        "SpeciesLifeHistorySO",
        "FuneralCultureSO",
        "FestivalDefinitionSO",
        "ProficiencyDefinitionSO",
        "CharacterSO",
    },
    "events-campaign": {
        "LifeEventDefinitionSO",
        "SeasonalWorldEventDefinitionSO",
        "ServiceIncidentDefinitionSO",
        "GuestRequestDefinitionSO",
        "CulturalPracticeDefinitionSO",
        "FactionArcDefinitionSO",
        "FactionChapterDefinitionSO",
        "FactionContractDefinitionSO",
        "EndingDefinitionSO",
        "WeatherFrontDefinitionSO",
        "ClimateZoneDefinitionSO",
        "DungeonFactionDefinitionSO",
    },
    "research-effects": {
        "ResearchProjectSO",
        "ResearchUnlockBundleDefinitionSO",
        "GameplayEffectDefinitionSO",
        "GameplayEffectConditionDefinitionSO",
        "CharacterPerformanceFormulaDefinitionSO",
        "CharacterFunctionalCapacityDefinitionSO",
    },
    "combat-health-world": {
        "OffenseEncounterSO",
        "EnemyArchetypeDefinitionSO",
        "EnemyAbilityDefinitionSO",
        "OffenseDecisionCardSO",
        "BattlefieldModifierDefinitionSO",
        "OffenseSiteArchetypeSO",
        "OffenseUrgentSiteDefinitionSO",
        "CombatWeaponSO",
        "CombatArmorSO",
        "CombatShieldSO",
        "DiseaseDefinitionSO",
        "SurgicalProcedureSO",
        "AnatomyProfileSO",
        "AnatomyConditionLexiconSO",
        "WildlifeSpeciesSO",
        "DefenseBurnEffectSO",
        "DefenseChargeEffectSO",
        "DefenseCorrosionEffectSO",
        "DefenseDamageEffectSO",
        "DefenseGuardAttackEffectSO",
        "DefenseSlowEffectSO",
    },
}

CLASS_TO_GROUP = {
    class_name: group
    for group, class_names in GROUPS.items()
    for class_name in class_names
}

ID_CANDIDATES = (
    "itemId",
    "recipeId",
    "contentDefinitionId",
    "definitionId",
    "projectId",
    "traitId",
    "stableId",
    "festivalId",
    "speciesId",
    "cultureId",
    "diseaseId",
    "cropId",
    "genomeId",
    "encounterId",
    "enemyId",
    "abilityId",
    "cardId",
    "processId",
    "researchId",
    "decisionId",
    "modifierId",
    "siteId",
    "siteTypeId",
    "weaponId",
    "armorId",
    "shieldId",
    "moduleId",
    "procedureId",
    "profileId",
    "lexiconId",
    "arcId",
    "chapterId",
    "contractId",
    "endingId",
    "backgroundId",
    "ambitionId",
    "originId",
    "historyId",
    "positionId",
    "conditionId",
    "proficiencyId",
    "effectId",
    "formulaId",
    "capacityId",
    "factionId",
    "tokenId",
    "workwearId",
    "archetypeId",
    "id",
)

DISPLAY_CANDIDATES = (
    "displayName",
    "traitName",
    "speciesName",
    "cultureName",
    "characterName",
    "blueprintName",
    "itemName",
    "objectName",
    "title",
    "name",
    "m_Name",
)

DESCRIPTION_CANDIDATES = (
    "description",
    "summary",
    "flavorText",
    "sourceNote",
)

OWN_ID_FIELDS = set(ID_CANDIDATES) | {"m_Name"}
RELATION_VALUE_KEYS = {
    "itemId",
    "targetId",
    "requiredResearchId",
    "prerequisiteId",
    "recipeId",
    "facilityId",
    "buildingId",
    "speciesId",
    "factionId",
    "effectId",
    "conditionId",
    "projectId",
    "encounterId",
    "abilityId",
    "profileId",
    "definitionId",
    "eventId",
    "requiredFlag",
    "resultFlag",
    "unlockId",
}

TYPE_PREFIX = {
    "CharacterTraitSO": "trait",
    "BuildingSO": "building",
    "ProductionRecipeSO": "recipe",
    "ResearchProjectSO": "research",
    "GameplayEffectDefinitionSO": "effect",
    "GameplayEffectConditionDefinitionSO": "effect-condition",
    "OffenseDecisionCardSO": "offense-card",
    "ServiceProcessSO": "service-process",
    "CharacterSO": "character-archetype",
    "FacilityBlueprintSO": "facility-blueprint",
    "FacilityEvolutionRecordTokenDefinitionSO": "evolution-token",
    "StockInfo": "shop-stock",
    "SaleItem": "sale-item",
}

CLASS_ID_FIELD = {
    "CropGenomeDefinitionSO": "genomeId",
    "OffenseDecisionCardSO": "cardId",
    "ResearchUnlockBundleDefinitionSO": "researchId",
    "ServiceProcessSO": "processId",
    "DungeonFactionDefinitionSO": "factionId",
    "EnvironmentalWorkwearSO": "workwearId",
    "FacilityEvolutionRecordTokenDefinitionSO": "tokenId",
    "CharacterSO": "archetypeId",
    "OffenseSiteArchetypeSO": "siteTypeId",
}

GROUP_LABEL = {
    "items": "아이템",
    "production-facilities": "생산·시설",
    "characters-traits": "인물·특성",
    "events-campaign": "사건·캠페인",
    "research-effects": "연구·효과",
    "combat-health-world": "전투·건강·세계",
}

ITEM_FEATURE_ROLES = {
    "ProductionItemFeature": "생산망의 입력·출력",
    "ResearchGateItemFeature": "연구에 따른 사용 시점 제한",
    "InstallationItemFeature": "시설 설치와 구성",
    "MarketItemFeature": "외부 거래와 가격 형성",
    "EvolutionCatalystItemFeature": "시설 진화 촉매",
    "EquipmentItemFeature": "장비 제작과 장착",
    "FoodItemFeature": "섭취와 영양 공급",
    "AmmunitionItemFeature": "원거리 전투의 탄약 소비",
    "MedicineItemFeature": "치료와 회복",
    "FacilitySupplyItemFeature": "시설 운영 보급",
    "SubstanceItemFeature": "기호품·약물 섭취",
    "PathogenSampleItemFeature": "병원체 연구 시료",
    "BlueprintItemFeature": "설계도 기반 해금",
    "VaccineItemFeature": "예방 접종",
    "CropTreatmentItemFeature": "작물 질병 대응",
    "MedicalProcedureSupplyItemFeature": "의료 시술 소모품",
    "PackagedLotItemFeature": "묶음 단위 보관·거래",
}

BUILDING_ABILITY_ROLES = {
    "BuildingProductionWorkstationAbility": "생산 작업대",
    "BuildingProductionAbility": "생산 공정",
    "BuildingProductionSupportAbility": "생산 보조",
    "BuildingStorageAbility": "재고 보관",
    "BuildingInternalStockAbility": "내부 공정 재고",
    "BuildingResearchCapacityAbility": "연구 수용력",
    "BuildingMedicalAbility": "진료와 치료",
    "BuildingDefenseAbility": "방어 전투",
    "BuildingSecurityAbility": "치안과 보안",
    "BuildingPatrolPostAbility": "순찰 준비와 사건 탐지",
    "BuildingReceptionAbility": "방문객 접수와 첫인상",
    "BuildingServiceAbility": "손님 서비스",
    "BuildingServiceSupportAbility": "서비스 보조",
    "BuildingNeedRecoveryAbility": "욕구 회복",
    "BuildingCropPlotAbility": "작물 재배",
    "BuildingCookingAbility": "조리",
    "BuildingTrainingAbility": "훈련",
    "BuildingRetailAbility": "소매 거래",
    "BuildingPowerProducerAbility": "전력 생산",
    "BuildingPowerConsumerAbility": "전력 소비",
    "BuildingPowerStorageAbility": "전력 저장",
    "BuildingWaterSourceAbility": "용수 취수",
    "BuildingWaterProducerAbility": "용수 생산",
    "BuildingWaterStorageAbility": "용수 저장",
    "BuildingWastewaterProcessorAbility": "폐수 처리",
    "BuildingVentilationAbility": "환기",
    "BuildingThermalEmitterAbility": "열 공급",
    "BuildingFuelConsumerAbility": "연료 소비",
    "BuildingConveyorSegmentAbility": "컨베이어 운송",
    "BuildingConveyorPortAbility": "물류 입출력",
    "BuildingAutomationAbility": "공정 자동화",
    "BuildingExpeditionRecoveryAbility": "원정대 회복",
    "BuildingOutdoorRestAbility": "야외 휴식",
    "BuildingPreservationAbility": "부패 억제와 보존",
    "BuildingCaptiveHousingAbility": "포로 수용",
    "BuildingEquipmentCraftingAbility": "장비 제작",
    "BuildingEquipmentMaintenanceAbility": "장비 정비",
}

TYPE_PURPOSE = {
    "AnatomyConditionLexiconSO": "신체 상태 식별자를 해부 부위별 질환·손상 표현에 연결한다",
    "BattlefieldModifierDefinitionSO": "전장의 지형·환경 조건을 전투 규칙 변화로 변환한다",
    "CombatArmorSO": "방어 부위와 피해 저항을 장비 선택에 연결한다",
    "CombatShieldSO": "방패의 방어 범위와 운용 부담을 전투 선택에 연결한다",
    "CombatWeaponSO": "무기의 공격 방식·사거리·피해 특성을 전투 행동에 연결한다",
    "EnemyArchetypeDefinitionSO": "적의 능력치·행동·보상 구성을 재사용 가능한 전투 개체로 묶는다",
    "EndingDefinitionSO": "장기 운영 목표의 달성 조건과 영구 보상, 후속 압력을 정의한다",
    "FacilityEvolutionRecipeSO": "기존 시설을 상위 역할로 전환하는 재료·조건·결과를 규정한다",
    "FacilitySynthesisRecipeSO": "시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다",
    "GameplayEffectConditionDefinitionSO": "효과 적용 여부를 판정하는 재사용 조건을 제공한다",
    "GameplayEffectDefinitionSO": "서로 다른 콘텐츠가 공유하는 상태 변화를 단일 실행 규약으로 정의한다",
    "OffenseSiteArchetypeSO": "원정 대상의 강도·환경·조우 후보를 하나의 현장 유형으로 묶는다",
    "OffenseUrgentSiteDefinitionSO": "시간 제한이 있는 원정 목표와 실패 압력을 정의한다",
    "SurgicalProcedureSO": "수술의 대상 부위·작업·소모품·위험과 결과를 규정한다",
    "CharacterSO": "종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다",
    "DungeonFactionDefinitionSO": "세력의 종족 정체성, 관계·거래 태그와 보급 구성을 외교·계약 시스템에 제공한다",
    "EnvironmentalWorkwearSO": "종족별 환경 보호 수단을 장비 아이템과 연구 해금에 연결한다",
    "FacilityBlueprintSO": "설계도 비용과 연구 작업, 해금 대상을 시설·연구 진행에 연결한다",
    "FacilityEvolutionRecordTokenDefinitionSO": "운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다",
    "StockInfo": "구형 상점 재고 구성을 특정 상점 식별자와 연결한다",
    "SaleItem": "구형 판매 항목을 현행 아이템 정의와 가격에 연결한다",
    "DefenseBurnEffectSO": "방어 시설의 지속 화상 효과를 수치·기간·중첩 규칙으로 정의한다",
    "DefenseChargeEffectSO": "방어 시설의 충전과 임계 방전 효과를 정의한다",
    "DefenseCorrosionEffectSO": "방어 시설이 부여하는 방어 저하 효과를 정의한다",
    "DefenseDamageEffectSO": "방어 시설의 즉시 피해 효과를 정의한다",
    "DefenseGuardAttackEffectSO": "경비 인력이 수행하는 방어 공격 효과를 정의한다",
    "DefenseSlowEffectSO": "방어 시설이 침입자 이동을 늦추는 효과를 정의한다",
}

ENUM_KOREAN_LABELS = {
    "StockCategory": {
        "Food": "식량", "General": "일반", "Weapon": "무기", "Mana": "마력",
        "Water": "물", "Medicine": "의약품", "Fuel": "연료", "Ammunition": "탄약",
        "Biological": "생물", "Knowledge": "지식", "Blueprint": "설계도",
    },
    "BuildingCategory": {
        "None": "없음", "Wall": "벽", "Shop": "상점", "Special": "특수",
        "Movement": "이동", "Production": "생산", "Crafting": "제작", "Resource": "자원",
    },
    "CharacterTraitSelectionRarity": {
        "Common": "일반", "Uncommon": "비일반", "Rare": "희귀", "Exceptional": "특별",
    },
    "CharacterTraitPolarity": {
        "Advantage": "이점", "Tradeoff": "상충", "Negative": "불리", "Quirk": "기벽", "Extreme": "극단",
    },
    "V20ContentEffectKind": {
        "None": "없음", "Mood": "기분", "Trauma": "트라우마", "SkillExperience": "숙련 경험치",
        "Health": "건강", "Relationship": "관계", "FactionRapport": "세력 우호도",
        "FactionGrievance": "세력 원한", "FactionObligation": "세력 의무", "Money": "자금",
        "ItemGrant": "아이템 지급", "ItemConsume": "아이템 소비", "WorldFlag": "세계 플래그",
        "WorkDelayDays": "작업 지연", "Threat": "위협", "DiseaseExposure": "질병 노출",
        "AmbitionProgress": "야망 진행", "MilestonePressure": "이정표 압력",
    },
}

V20_EFFECT_RELATION_KIND = {
    1: "changes-mood",
    2: "changes-trauma",
    3: "grants-skill-experience",
    4: "changes-health",
    5: "changes-relationship",
    6: "changes-faction-rapport",
    7: "changes-faction-grievance",
    8: "changes-faction-obligation",
    9: "changes-money",
    10: "grants-item",
    11: "consumes-item",
    12: "sets-world-flag",
    13: "adds-work-delay",
    14: "changes-threat",
    15: "adds-disease-exposure",
    16: "advances-ambition",
    17: "changes-milestone-pressure",
}

V20_EFFECT_NAMES = {
    0: "None", 1: "Mood", 2: "Trauma", 3: "SkillExperience", 4: "Health",
    5: "Relationship", 6: "FactionRapport", 7: "FactionGrievance",
    8: "FactionObligation", 9: "Money", 10: "ItemGrant", 11: "ItemConsume",
    12: "WorldFlag", 13: "WorkDelayDays", 14: "Threat", 15: "DiseaseExposure",
    16: "AmbitionProgress", 17: "MilestonePressure",
}

ENUM_FIELD_HINTS = {
    ("GenericItemDefinitionSO", "stockCategory"): "StockCategory",
    ("ResourceItemDefinitionSO", "stockCategory"): "StockCategory",
    ("BuildingSO", "category"): "BuildingCategory",
    ("CharacterTraitSO", "selectionRarity"): "CharacterTraitSelectionRarity",
    ("CharacterTraitSO", "polarity"): "CharacterTraitPolarity",
    ("ResearchProjectSO", "field"): "ResearchField",
}

UNITY_METADATA_FIELDS = {
    "m_ObjectHideFlags",
    "m_CorrespondingSourceObject",
    "m_PrefabInstance",
    "m_PrefabAsset",
    "m_GameObject",
    "m_Enabled",
    "m_EditorHideFlags",
    "m_Script",
    "m_EditorClassIdentifier",
    "serializationData",
}

CONTENT_REFERENCE_FIELDS = {
    "itemId",
    "itemDefinitionId",
    "buildingId",
    "buildingDefinitionId",
    "requiredBuildingDefinitionId",
    "requiredResearchId",
    "targetResearchProjectId",
    "prerequisiteId",
    "researchId",
    "projectId",
    "recipeId",
    "speciesId",
    "factionId",
    "effectId",
    "conditionId",
    "encounterId",
    "abilityId",
    "profileId",
    "definitionId",
    "eventId",
    "unlockId",
}

PROTOCOL_REFERENCE_FIELDS = {
    "requiredFlag",
    "resultFlag",
    "excludedFlag",
    "capabilityId",
    "behaviorTag",
    "eventCategoryId",
    "ruleId",
    "needId",
    "workTypeId",
    "workTypeIds",
    "strongWorkTypeIds",
    "weakWorkTypeIds",
    "affectedDomainIds",
    "fieldResponseIds",
    "affectedAnatomyNodeIds",
    "anatomyNodeIds",
    "speciesId",
    "speciesIds",
    "allowedSpeciesIds",
}


@dataclass
class Relation:
    source_type: str
    source_id: str
    source_record_key: str
    kind: str
    target_id: str
    amount: str
    duration: str
    field_path: str
    semantic_label: str
    target_category: str
    resolution_status: str
    target_record_keys: str
    source_path: str


@dataclass
class ContentRow:
    group: str
    content_type: str
    record_key: str
    stable_id: str
    display_name: str
    description: str
    mechanics: str
    relations: str
    existence_reason: str
    reason_basis: str
    review_status: str
    review_reason: str
    lifecycle_status: str
    catalog_memberships: str
    runtime_status: str
    runtime_evidence: str
    save_evidence: str
    incoming_reference_count: int
    incoming_source_types: str
    system_role: str
    strategic_niche: str
    costs_and_risks: str
    comparison_group: str
    alternative_candidates: str
    removal_impact: str
    rationale_evidence: str
    source_path: str
    asset_guid: str
    data: dict[str, Any] = field(repr=False)
    authored_fields: dict[str, str] = field(default_factory=dict, repr=False)


@dataclass(frozen=True)
class EnumDefinition:
    values: dict[int, str]
    flags: bool = False


@dataclass(frozen=True)
class RelationCandidate:
    field_path: str
    target_id: str
    amount: str
    duration: str
    kind: str
    semantic_label: str
    target_category: str


def relative(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output-root",
        default="docs/content-db",
        help="Generated database root, relative to the project root or absolute.",
    )
    return parser.parse_args(argv)


def resolve_output_root(value: str) -> Path:
    candidate = Path(value)
    resolved = candidate.resolve() if candidate.is_absolute() else (ROOT / candidate).resolve()
    relative_output = resolved.relative_to(ROOT.resolve())
    if not relative_output.parts or relative_output.name != "content-db":
        raise ValueError("The generated output must be a project-local directory named 'content-db'.")
    return resolved


def prepare_output_root(path: Path) -> None:
    """Recreate only the dedicated generated database directory."""

    path.resolve().relative_to(ROOT.resolve())
    if path.name != "content-db":
        raise ValueError("Refusing to replace a directory not named 'content-db'.")
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True, exist_ok=True)


def scalar_text(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, float):
        return f"{value:g}"
    if isinstance(value, (str, int)):
        return str(value).strip()
    return ""


def compact_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def humanize_identifier(value: str) -> str:
    text = re.sub(r"(?<!^)(?=[A-Z])", " ", value or "")
    return text.replace("_", " ").strip()


def authored_column_key(value: str) -> str:
    """Normalize case-only C#↔YAML aliases while long-form fields preserve the exact path."""
    return value[:1].lower() + value[1:] if value else value


def strip_csharp_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.DOTALL)
    return re.sub(r"//[^\r\n]*", "", text)


def parse_enum_expression(expression: str, known: dict[str, int], fallback: int) -> int:
    expression = expression.strip()
    if re.fullmatch(r"-?\d+", expression):
        return int(expression)
    if re.fullmatch(r"0x[0-9a-fA-F]+", expression):
        return int(expression, 16)
    shift = re.fullmatch(r"(\d+)\s*<<\s*(\d+)", expression)
    if shift:
        return int(shift.group(1)) << int(shift.group(2))
    if "|" in expression:
        result = 0
        for part in expression.split("|"):
            name = part.strip().split(".")[-1]
            if name not in known:
                return fallback
            result |= known[name]
        return result
    return known.get(expression.split(".")[-1], fallback)


def build_csharp_enum_index() -> tuple[dict[str, EnumDefinition], dict[Path, dict[str, str]]]:
    enums: dict[str, EnumDefinition] = {}
    source_texts: dict[Path, str] = {}
    enum_pattern = re.compile(
        r"(?P<flags>\[\s*Flags\s*\]\s*)?"
        r"(?:(?:public|internal|private|protected)\s+)?enum\s+"
        r"(?P<name>[A-Za-z_]\w*)(?:\s*:\s*[A-Za-z_]\w*)?\s*\{(?P<body>.*?)\}",
        re.DOTALL,
    )
    for source in (ASSETS / "Scripts").rglob("*.cs"):
        try:
            text = strip_csharp_comments(source.read_text(encoding="utf-8-sig", errors="strict"))
        except (OSError, UnicodeError):
            continue
        source_texts[source] = text
        for match in enum_pattern.finditer(text):
            current = -1
            names: dict[str, int] = {}
            values: dict[int, str] = {}
            for raw_entry in match.group("body").split(","):
                entry = re.sub(r"\[[^\]]+\]", "", raw_entry).strip()
                if not entry:
                    continue
                name_and_value = entry.split("=", 1)
                name = name_and_value[0].strip()
                if not re.fullmatch(r"[A-Za-z_]\w*", name):
                    continue
                current = (
                    parse_enum_expression(name_and_value[1], names, current + 1)
                    if len(name_and_value) == 2
                    else current + 1
                )
                names[name] = current
                values[current] = name
            if values:
                enums[match.group("name")] = EnumDefinition(
                    values=values,
                    flags=bool(match.group("flags")),
                )

    fields_by_script: dict[Path, dict[str, str]] = defaultdict(dict)
    field_pattern = re.compile(
        r"(?:public|private|protected|internal)\s+"
        r"(?:(?:static|readonly|const|virtual|override|sealed)\s+)*"
        r"(?P<type>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s+"
        r"(?P<field>[A-Za-z_]\w*)\s*(?:=|;)",
    )
    for source, text in source_texts.items():
        for match in field_pattern.finditer(text):
            enum_name = match.group("type").split(".")[-1]
            if enum_name in enums:
                fields_by_script[source][match.group("field")] = enum_name
    return enums, fields_by_script


def enum_label(
    class_name: str,
    field_path: str,
    value: Any,
    script_path: Path | None,
    enums: dict[str, EnumDefinition],
    fields_by_script: dict[Path, dict[str, str]],
) -> tuple[str, str]:
    raw = scalar_text(value)
    if not raw or not re.fullmatch(r"-?\d+", raw):
        return "", ""
    leaf = field_path.rsplit(".", 1)[-1]
    enum_name = ""
    if ".effects." in f".{field_path}." and leaf == "kind":
        enum_name = "V20ContentEffectKind"
    elif ".worldMetrics." in f".{field_path}." and leaf == "kind":
        enum_name = "V20WorldMetricKind"
    else:
        enum_name = ENUM_FIELD_HINTS.get((class_name, leaf), "")
        if not enum_name and script_path:
            script_fields = fields_by_script.get(script_path, {})
            enum_name = script_fields.get(leaf, "")
            if not enum_name:
                enum_name = next(
                    (value for key, value in script_fields.items() if key.casefold() == leaf.casefold()),
                    "",
                )
    definition = enums.get(enum_name)
    if not definition:
        return "", ""
    numeric = int(raw)
    names: list[str] = []
    if numeric in definition.values:
        names.append(definition.values[numeric])
    elif definition.flags and numeric:
        for flag_value, name in sorted(definition.values.items()):
            if flag_value and numeric & flag_value == flag_value:
                names.append(name)
    if not names:
        return enum_name, f"알 수 없음 ({numeric})"
    labels = []
    translations = ENUM_KOREAN_LABELS.get(enum_name, {})
    for name in names:
        korean = translations.get(name)
        labels.append(f"{name} ({korean})" if korean else humanize_identifier(name))
    return enum_name, " | ".join(labels)


def authored_columns(
    class_name: str,
    data: dict[str, Any],
    script_path: Path | None,
    enums: dict[str, EnumDefinition],
    fields_by_script: dict[Path, dict[str, str]],
) -> dict[str, str]:
    result: dict[str, str] = {}
    for key, value in data.items():
        if key in UNITY_METADATA_FIELDS or key == "references":
            continue
        normalized_key = authored_column_key(key)
        column = f"authored__{normalized_key}"
        result[column] = scalar_text(value) if not isinstance(value, (dict, list)) else compact_json(value)
        if isinstance(value, list):
            result[f"authored__{normalized_key}__count"] = str(len(value))
        enum_name, label = enum_label(class_name, key, value, script_path, enums, fields_by_script)
        if label:
            result[f"authored__{normalized_key}__enum"] = enum_name
            result[f"authored__{normalized_key}__label"] = label
            result[f"authored__{normalized_key}__origin"] = "explicit-serialized"
    if script_path:
        authored_keys = {key.casefold() for key in data}
        for key in sorted(fields_by_script.get(script_path, {})):
            if key.casefold() in authored_keys:
                continue
            enum_name, label = enum_label(class_name, key, 0, script_path, enums, fields_by_script)
            if label:
                normalized_key = authored_column_key(key)
                result[f"authored__{normalized_key}"] = "0"
                result[f"authored__{normalized_key}__enum"] = enum_name
                result[f"authored__{normalized_key}__label"] = label
                result[f"authored__{normalized_key}__origin"] = "implicit-csharp-default"
    return result


def flatten_authored_fields(value: Any, path: tuple[str, ...] = ()) -> Iterable[tuple[str, Any]]:
    if isinstance(value, dict):
        for key, child in value.items():
            if not path and (key in UNITY_METADATA_FIELDS or key == "serializationData"):
                continue
            yield from flatten_authored_fields(child, path + (key,))
    elif isinstance(value, list):
        if not value:
            yield ".".join(path), []
        for index, child in enumerate(value):
            yield from flatten_authored_fields(child, path + (str(index),))
    else:
        yield ".".join(path), value


def first_text(data: dict[str, Any], candidates: Iterable[str]) -> str:
    for key in candidates:
        text = scalar_text(data.get(key))
        if text:
            return text
    return ""


def build_guid_index() -> tuple[dict[str, Path], dict[Path, str]]:
    guid_to_path: dict[str, Path] = {}
    path_to_guid: dict[Path, str] = {}
    for meta in ASSETS.rglob("*.meta"):
        try:
            with meta.open("r", encoding="utf-8-sig", errors="replace") as handle:
                for _ in range(12):
                    line = handle.readline()
                    if not line:
                        break
                    if line.startswith("guid: "):
                        guid = line.split(":", 1)[1].strip()
                        target = Path(str(meta)[:-5])
                        guid_to_path[guid] = target
                        path_to_guid[target] = guid
                        break
        except OSError:
            continue
    return guid_to_path, path_to_guid


def parse_unity_asset(path: Path) -> list[dict[str, Any]]:
    text = path.read_text(encoding="utf-8-sig", errors="strict")
    text = "\n".join(line for line in text.splitlines() if not line.startswith("%"))
    text = UNITY_HEADER.sub("---", text)
    documents = []
    for document in yaml.safe_load_all(text):
        if isinstance(document, dict):
            documents.append(document)
    return documents


def resolve_class_name(data: dict[str, Any], guid_to_path: dict[str, Path]) -> str:
    identifier = scalar_text(data.get("m_EditorClassIdentifier"))
    if identifier:
        match = CLASS_SUFFIX.search(identifier)
        if match:
            return match.group(1)
    script = data.get("m_Script")
    if isinstance(script, dict):
        script_path = guid_to_path.get(scalar_text(script.get("guid")))
        if script_path and script_path.suffix == ".cs":
            return script_path.stem
    return ""


def normalize_id(class_name: str, data: dict[str, Any], path: Path) -> tuple[str, bool]:
    preferred = CLASS_ID_FIELD.get(class_name)
    candidates = ([preferred] if preferred else []) + [key for key in ID_CANDIDATES if key != preferred]
    for key in candidates:
        value = data.get(key)
        text = scalar_text(value)
        if not text:
            continue
        if key == "id" and class_name == "CharacterTraitSO":
            return f"trait:{text}", False
        if key == "id" and ":" not in text:
            prefix = TYPE_PREFIX.get(class_name, class_name.removesuffix("DefinitionSO").removesuffix("SO").lower())
            return f"{prefix}:{text}", False
        return text, False
    return f"asset:{path.stem}", True


def collect_reference_types(data: dict[str, Any]) -> list[str]:
    result: list[str] = []
    references = data.get("references")
    if isinstance(references, dict):
        ref_ids = references.get("RefIds")
        if isinstance(ref_ids, list):
            for value in ref_ids:
                if not isinstance(value, dict):
                    continue
                type_info = value.get("type")
                if isinstance(type_info, dict):
                    name = scalar_text(type_info.get("class"))
                    if name:
                        result.append(name)
    return sorted(set(result))


def count_requirements(value: Any) -> int:
    if not isinstance(value, dict):
        return 0
    return sum(len(entry) for entry in value.values() if isinstance(entry, list))


def count_effects(data: dict[str, Any]) -> int:
    effect_fields = (
        "effects",
        "automaticEffects",
        "startEffects",
        "dailyEffects",
        "endEffects",
        "successEffects",
        "failureEffects",
    )
    total = sum(len(data.get(field) or []) for field in effect_fields)
    for choice in data.get("choices") or []:
        if isinstance(choice, dict):
            total += sum(len(choice.get(field) or []) for field in effect_fields)
    return total


def effective_trait_counts(data: dict[str, Any]) -> dict[str, int]:
    modifiers = data.get("modifiers") if isinstance(data.get("modifiers"), dict) else {}
    modifier_count = sum(
        1
        for value in modifiers.values()
        if scalar_text(value) not in {"", "0", "false"}
    )
    experience_multiplier = scalar_text(data.get("earnedWorkExperienceMultiplier"))
    if experience_multiplier and experience_multiplier != "1":
        modifier_count += 1

    combat = data.get("combatAbilities") if isinstance(data.get("combatAbilities"), dict) else {}
    combat_count = len(combat.get("abilities") or [])

    protection = (
        data.get("environmentalProtection")
        if isinstance(data.get("environmentalProtection"), dict)
        else {}
    )
    protection_count = 0
    for key, value in protection.items():
        raw = scalar_text(value)
        if not raw:
            continue
        neutral = "1" if "multiplier" in key.lower() else "0"
        if raw != neutral:
            protection_count += 1

    return {
        "effects": len(data.get("effects") or []),
        "modifiers": modifier_count,
        "combat_abilities": combat_count,
        "environmental_protection": protection_count,
        "behaviors": len(data.get("behaviorPreferences") or []),
        "moods": len(data.get("moodReactions") or []),
        "events": len(data.get("eventWeights") or []),
        "identity_rules": len(data.get("identityRules") or []),
        "consequences": len(data.get("consequences") or []),
    }


def item_amounts(values: Any) -> list[tuple[str, str]]:
    result: list[tuple[str, str]] = []
    if not isinstance(values, list):
        return result
    for value in values:
        if not isinstance(value, dict):
            continue
        item_id = scalar_text(value.get("itemId"))
        if not item_id:
            continue
        amount = scalar_text(value.get("amount")) or "1"
        result.append((item_id, amount))
    return result


def summarize_pairs(values: list[tuple[str, str]], limit: int = 5) -> str:
    if not values:
        return "없음"
    parts = [f"{item_id}×{amount}" for item_id, amount in values[:limit]]
    if len(values) > limit:
        parts.append(f"외 {len(values) - limit}개")
    return ", ".join(parts)


def summarize_mechanics(class_name: str, data: dict[str, Any]) -> str:
    reference_types = collect_reference_types(data)
    if class_name in {"GenericItemDefinitionSO", "ResourceItemDefinitionSO"}:
        parts = [
            f"범주 {scalar_text(data.get('stockCategory')) or '?'}",
            f"질량 {scalar_text(data.get('unitWeight')) or '?'}kg",
            f"스택 {scalar_text(data.get('maxStack')) or '?'}",
            f"가격 {scalar_text(data.get('unitPrice')) or '0'}",
        ]
        if reference_types:
            parts.append("feature " + ", ".join(reference_types))
        return "; ".join(parts)

    if class_name == "ProductionRecipeSO":
        parts = [
            f"입력 {summarize_pairs(item_amounts(data.get('inputs')))}",
            f"출력 {summarize_pairs(item_amounts(data.get('outputs')))}",
            f"작업 {scalar_text(data.get('requiredWork')) or '?'}",
        ]
        for label, key in (("시설", "facilityTag"), ("작업대", "workstationTag"), ("연구", "requiredResearchId")):
            value = scalar_text(data.get(key))
            if value:
                parts.append(f"{label} {value}")
        return "; ".join(parts)

    if class_name == "BuildingSO":
        abilities = reference_types
        parts = [f"범주 {scalar_text(data.get('category')) or '?'}"]
        if abilities:
            parts.append("ability " + ", ".join(abilities[:8]))
        if data.get("deprecatedCompatibilityAsset"):
            parts.append("deprecated 호환 자산")
        return "; ".join(parts)

    if class_name == "ResearchProjectSO":
        prerequisite_count = len(data.get("prerequisites") or []) + len(data.get("prerequisiteLinks") or [])
        facilities = len(data.get("facilityRequirements") or [])
        effects = len(data.get("effects") or [])
        unlocks = data.get("unlocks")
        unlock_count = 0
        if isinstance(unlocks, dict):
            unlock_count = sum(len(value) for value in unlocks.values() if isinstance(value, list))
        return (
            f"분야 {scalar_text(data.get('field')) or '?'}; 작업 {scalar_text(data.get('requiredWork')) or '?'}; "
            f"선행 {prerequisite_count}; 해금 {unlock_count}; 시설 요구 {facilities}; 효과 {effects}"
        )

    if class_name == "CharacterTraitSO":
        counts = effective_trait_counts(data)
        return (
            f"극성 {scalar_text(data.get('polarity')) or '?'}; 희귀도 {scalar_text(data.get('selectionRarity')) or '?'}; "
            f"효과 {counts['effects']}; 정체성 규칙 {counts['identity_rules']}; 유효 구형 보정 {counts['modifiers']}; "
            f"전투 능력 {counts['combat_abilities']}; 환경 보호 {counts['environmental_protection']}; "
            f"행동 선호 {counts['behaviors']}; 기분 반응 {counts['moods']}; 사건 가중치 {counts['events']}"
        )

    if class_name == "HeritableTraitDefinitionSO":
        return f"범주 {scalar_text(data.get('category')) or '?'}; 결과 {len(data.get('consequences') or [])}"

    if class_name in {
        "LifeEventDefinitionSO",
        "SeasonalWorldEventDefinitionSO",
        "ServiceIncidentDefinitionSO",
        "GuestRequestDefinitionSO",
        "CulturalPracticeDefinitionSO",
        "FactionChapterDefinitionSO",
        "FactionContractDefinitionSO",
    }:
        requirements = count_requirements(data.get("triggerRequirements") or data.get("requirements"))
        return (
            f"범주 {scalar_text(data.get('category')) or '?'}; 요구 조건 {requirements}; "
            f"선택지 {len(data.get('choices') or [])}; 효과 {count_effects(data)}"
        )

    scalar_keys = (
        "category",
        "kind",
        "field",
        "requiredWork",
        "rarity",
        "tier",
        "role",
        "durationDays",
        "cooldownDays",
        "baseSeverity",
    )
    parts = []
    for key in scalar_keys:
        value = scalar_text(data.get(key))
        if value:
            parts.append(f"{key} {value}")
    if reference_types:
        parts.append("module " + ", ".join(reference_types[:8]))
    return "; ".join(parts) if parts else "작성 자산 정의"


def relation_kind_for_path(path: str) -> str:
    lowered = path.lower()
    if "prerequisite" in lowered:
        return "prerequisite"
    if "excluded" in lowered or "incompat" in lowered:
        return "excludes"
    if "input" in lowered or "requirement" in lowered or "required" in lowered or "cost" in lowered:
        return "requires"
    if "output" in lowered or "unlock" in lowered or "reward" in lowered:
        return "produces-or-unlocks"
    return "references"


def effect_target_category(kind: int) -> str:
    if kind in {10, 11}:
        return "item-content"
    if kind in {6, 7, 8}:
        return "faction-content"
    if kind == 15:
        return "disease-content"
    if kind == 16:
        return "ambition-content"
    return "effect-channel"


def iter_relation_candidates(value: Any, path: tuple[str, ...] = ()) -> Iterable[RelationCandidate]:
    if isinstance(value, dict):
        for key, child in value.items():
            if not path and (key in UNITY_METADATA_FIELDS or key in {"references", "serializationData"}):
                continue
            child_path = path + (key,)
            path_text = ".".join(child_path)
            in_effect = any(part.lower().endswith("effects") for part in path)
            if key == "targetId" and in_effect and isinstance(child, str) and child:
                raw_kind = scalar_text(value.get("kind"))
                effect_kind = int(raw_kind) if re.fullmatch(r"\d+", raw_kind) else 0
                enum_name = V20_EFFECT_NAMES.get(effect_kind, f"Unknown{effect_kind}")
                korean = ENUM_KOREAN_LABELS["V20ContentEffectKind"].get(enum_name, "알 수 없음")
                yield RelationCandidate(
                    field_path=path_text,
                    target_id=child,
                    amount=scalar_text(value.get("amount")),
                    duration=scalar_text(value.get("durationDays")) or scalar_text(value.get("duration")),
                    kind=V20_EFFECT_RELATION_KIND.get(effect_kind, "unknown-effect"),
                    semantic_label=f"{enum_name} ({korean})",
                    target_category=effect_target_category(effect_kind),
                )
            elif key in CONTENT_REFERENCE_FIELDS or key in PROTOCOL_REFERENCE_FIELDS or key.endswith("Ids"):
                if isinstance(child, str) and child and ID_TEXT.match(child):
                    amount = (
                        scalar_text(value.get("amount"))
                        or scalar_text(value.get("minimumCount"))
                        or scalar_text(value.get("configuredAmount"))
                        or scalar_text(value.get("value"))
                    )
                    if key == "factionId":
                        category = "faction-content"
                    else:
                        category = "protocol-id" if key in PROTOCOL_REFERENCE_FIELDS else "content-reference"
                    yield RelationCandidate(
                        field_path=path_text,
                        target_id=child,
                        amount=amount,
                        duration=scalar_text(value.get("durationDays")) or scalar_text(value.get("duration")),
                        kind=relation_kind_for_path(path_text),
                        semantic_label=humanize_identifier(key),
                        target_category=category,
                    )
                elif isinstance(child, list):
                    for entry in child:
                        if isinstance(entry, str) and entry and ID_TEXT.match(entry):
                            category = "protocol-id" if key in PROTOCOL_REFERENCE_FIELDS else "content-reference"
                            yield RelationCandidate(
                                field_path=path_text,
                                target_id=entry,
                                amount="",
                                duration="",
                                kind=relation_kind_for_path(path_text),
                                semantic_label=humanize_identifier(key),
                                target_category=category,
                            )
            if isinstance(child, (dict, list)):
                yield from iter_relation_candidates(child, child_path)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from iter_relation_candidates(child, path + (str(index),))


def iter_managed_reference_relation_candidates(data: dict[str, Any]) -> Iterable[RelationCandidate]:
    """Recover content relations stored in Unity SerializeReference payloads."""

    references = data.get("references")
    if not isinstance(references, dict):
        return
    ref_ids = references.get("RefIds")
    if not isinstance(ref_ids, list):
        return
    for index, entry in enumerate(ref_ids):
        if not isinstance(entry, dict):
            continue
        type_info = entry.get("type")
        payload = entry.get("data")
        if not isinstance(type_info, dict) or not isinstance(payload, dict):
            continue
        managed_type = scalar_text(type_info.get("class"))
        field_prefix = f"references.RefIds.{index}.data"
        if managed_type in {"BlueprintBuildingUnlock", "BlueprintBasicPurchaseUnlock"}:
            building_id = scalar_text(payload.get("buildingId"))
            if not building_id:
                continue
            yield RelationCandidate(
                field_path=f"{field_prefix}.buildingId",
                target_id=building_id if building_id.startswith("building:") else f"building:{building_id}",
                amount="",
                duration="",
                kind=(
                    "unlocks-building"
                    if managed_type == "BlueprintBuildingUnlock"
                    else "unlocks-basic-purchase"
                ),
                semantic_label=managed_type,
                target_category="content-reference",
            )
        elif managed_type == "BlueprintRecipeUnlock":
            recipe_id = scalar_text(payload.get("recipeId"))
            if not recipe_id:
                continue
            yield RelationCandidate(
                field_path=f"{field_prefix}.recipeId",
                target_id=recipe_id,
                amount="",
                duration="",
                kind="unlocks-recipe",
                semantic_label=managed_type,
                target_category="content-reference",
            )


def iter_content_relation_candidates(data: dict[str, Any]) -> Iterable[RelationCandidate]:
    yield from iter_relation_candidates(data)
    yield from iter_managed_reference_relation_candidates(data)


def collect_guid_relations(value: Any, guid_to_content_id: dict[str, str], path: tuple[str, ...] = ()) -> Iterable[tuple[str, str, str]]:
    if isinstance(value, dict):
        if "guid" in value and "fileID" in value:
            guid = scalar_text(value.get("guid"))
            target = guid_to_content_id.get(guid)
            if target:
                yield ".".join(path), target, ""
            return
        for key, child in value.items():
            yield from collect_guid_relations(child, guid_to_content_id, path + (key,))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from collect_guid_relations(child, guid_to_content_id, path + (str(index),))


def collect_all_guids(value: Any) -> set[str]:
    result: set[str] = set()
    if isinstance(value, dict):
        guid = scalar_text(value.get("guid"))
        if guid:
            result.add(guid)
        for child in value.values():
            result.update(collect_all_guids(child))
    elif isinstance(value, list):
        for child in value:
            result.update(collect_all_guids(child))
    return result


def build_catalog_memberships(
    catalog_assets: dict[str, tuple[str, set[str]]],
) -> dict[str, set[str]]:
    memberships: dict[str, set[str]] = defaultdict(set)

    def visit(catalog_guid: str, current_path: str, visited: set[str]) -> None:
        if catalog_guid in visited:
            return
        visited.add(catalog_guid)
        _, references = catalog_assets[catalog_guid]
        for target_guid in references:
            memberships[target_guid].add(current_path)
            if target_guid in catalog_assets:
                visit(target_guid, current_path, visited)

    for catalog_guid, (catalog_path, _) in catalog_assets.items():
        visit(catalog_guid, catalog_path, set())
    return memberships


def build_code_evidence_index(
    stable_ids: set[str],
    class_names: set[str],
) -> tuple[
    dict[str, set[str]],
    dict[str, set[str]],
    dict[str, set[str]],
    dict[str, set[str]],
]:
    type_references: dict[str, set[str]] = defaultdict(set)
    literal_references: dict[str, set[str]] = defaultdict(set)
    resource_loads: dict[str, set[str]] = defaultdict(set)
    save_references: dict[str, set[str]] = defaultdict(set)
    string_pattern = re.compile(r'"([^"\r\n]+)"')
    word_pattern = re.compile(r"\b[A-Za-z_]\w*\b")
    load_pattern = re.compile(r"Resources\.Load(?:All)?<([A-Za-z_]\w*)>")

    for source in (ASSETS / "Scripts").rglob("*.cs"):
        relative_source = relative(source)
        path_parts = {part.lower() for part in source.parts}
        if "editor" in path_parts or "tests" in path_parts or "test" in source.stem.lower():
            continue
        try:
            text = source.read_text(encoding="utf-8-sig", errors="strict")
        except (OSError, UnicodeError):
            continue
        words = set(word_pattern.findall(text)) & class_names
        for class_name in words:
            type_references[class_name].add(relative_source)
            if re.search(r"\b(?:Save|Restore|Capture|Snapshot|Codec)\w*\b", text):
                save_references[class_name].add(relative_source)
        for loaded_type in load_pattern.findall(text):
            if loaded_type in class_names:
                resource_loads[loaded_type].add(relative_source)
        for literal in string_pattern.findall(text):
            if literal in stable_ids:
                literal_references[literal].add(relative_source)
    return type_references, literal_references, resource_loads, save_references


def runtime_classification(
    row: ContentRow,
    memberships: set[str],
    type_references: set[str],
    literal_references: set[str],
    resource_loads: set[str],
    save_references: set[str],
    duplicate_key: bool,
) -> tuple[str, str, str, str]:
    deprecated = bool(row.data.get("deprecatedCompatibilityAsset"))
    source_lower = row.source_path.lower()
    if deprecated:
        lifecycle = "deprecated-compatibility"
    elif duplicate_key:
        lifecycle = "duplicate-authority-review"
    elif row.content_type in {"StockInfo", "SaleItem"}:
        lifecycle = "legacy-authoring-review"
    elif "legacy" in source_lower:
        lifecycle = "legacy-path-review"
    else:
        lifecycle = "active-authored"

    definition_source = row.source_path.removesuffix(".asset") + ".cs"
    consumers = sorted(path for path in type_references if path != definition_source)
    literals = sorted(literal_references)
    loaders = sorted(resource_loads)
    evidence = sorted(set(consumers + literals + loaders))
    if deprecated:
        runtime_status = "deprecated-compatibility"
    elif memberships and (consumers or loaders):
        runtime_status = "catalog-registered-static-consumer"
    elif loaders:
        runtime_status = "resources-loader-static-consumer"
    elif literals:
        runtime_status = "stable-id-literal-consumer"
    elif consumers:
        runtime_status = "type-consumer-registration-unverified"
    elif "/resources/" in f"/{source_lower}":
        runtime_status = "resources-authored-consumer-unverified"
    else:
        runtime_status = "authored-only-unverified"
    return (
        lifecycle,
        runtime_status,
        "; ".join(evidence[:12]) or "정적 비-Editor 소비자 근거 없음",
        "; ".join(sorted(save_references)[:8]) or "정적 저장 근거 없음",
    )


def comparison_group_for(row: ContentRow) -> str:
    data = row.data
    if row.content_type in {"GenericItemDefinitionSO", "ResourceItemDefinitionSO"}:
        features = "+".join(collect_reference_types(data)) or "no-feature"
        return f"item:{scalar_text(data.get('stockCategory')) or '?'}:{features}"
    if row.content_type == "ProductionRecipeSO":
        outputs = item_amounts(data.get("outputs"))
        output_domains = sorted({item_id.split(":", 1)[0] for item_id, _ in outputs})
        return f"recipe:{scalar_text(data.get('facilityTag')) or '?'}:{'+'.join(output_domains) or 'no-output'}"
    if row.content_type == "BuildingSO":
        roles = [BUILDING_ABILITY_ROLES[value] for value in collect_reference_types(data) if value in BUILDING_ABILITY_ROLES]
        return "building:" + ("+".join(sorted(roles)) if roles else scalar_text(data.get("category")) or "unclassified")
    if row.content_type == "CharacterTraitSO":
        return f"trait:{scalar_text(data.get('selectionFamilyId')) or 'unclassified'}"
    if row.content_type == "ResearchProjectSO":
        return f"research-field:{scalar_text(data.get('field')) or '?'}"
    if row.group == "events-campaign":
        return f"event:{row.content_type}:{scalar_text(data.get('category')) or scalar_text(data.get('kind')) or '?'}"
    for key in ("category", "kind", "field", "role", "tier"):
        value = scalar_text(data.get(key))
        if value:
            return f"{row.content_type}:{key}:{value}"
    return row.content_type


def costs_and_risks_for(row: ContentRow) -> tuple[str, str]:
    data = row.data
    selected: list[str] = []
    evidence: list[str] = []
    key_pattern = re.compile(
        r"(?:cost|price|work|required|duration|cooldown|delay|risk|danger|water|waste|power|fuel|threshold|spoil|mass)",
        re.IGNORECASE,
    )
    for key, value in data.items():
        if not key_pattern.search(key):
            continue
        raw = scalar_text(value)
        if not raw or raw in {"0", "false"}:
            continue
        selected.append(f"{key}={raw}")
        evidence.append(key)
        if len(selected) >= 10:
            break
    if row.content_type == "ProductionRecipeSO":
        inputs = summarize_pairs(item_amounts(data.get("inputs")), 6)
        if inputs != "없음":
            selected.insert(0, f"inputs={inputs}")
            evidence.append("inputs")
    return (
        "; ".join(selected) or "작성 자산에서 직접 비용·위험 수치를 확인할 수 없음",
        ", ".join(sorted(set(evidence))) or "type-contract",
    )


def strategic_niche_for(row: ContentRow) -> tuple[str, str]:
    data = row.data
    class_name = row.content_type
    if class_name in {"GenericItemDefinitionSO", "ResourceItemDefinitionSO"}:
        roles = [ITEM_FEATURE_ROLES.get(value, humanize_identifier(value)) for value in collect_reference_types(data)]
        category = scalar_text(data.get("stockCategory")) or "?"
        return f"재고 범주 {category}에서 {', '.join(roles[:5]) or '일반 물자'} 역할을 맡는다.", "stockCategory+features"
    if class_name == "ProductionRecipeSO":
        return (
            f"{scalar_text(data.get('facilityTag')) or '미지정 시설'} 계열에서 {summarize_pairs(item_amounts(data.get('outputs')), 4)} 공급 경로를 맡는다.",
            "facilityTag+outputs",
        )
    if class_name == "BuildingSO":
        roles = [BUILDING_ABILITY_ROLES[value] for value in collect_reference_types(data) if value in BUILDING_ABILITY_ROLES]
        return f"{', '.join(roles[:5]) or '구조·공간'} 기능 조합을 제공하는 시설 변형이다.", "abilityModules"
    if class_name == "CharacterTraitSO":
        return (
            f"선택군 {scalar_text(data.get('selectionFamilyId')) or '미분류'} 안에서 극성 {scalar_text(data.get('polarity')) or '?'}의 인물 차이를 만든다.",
            "selectionFamilyId+polarity+bindings",
        )
    if class_name == "ResearchProjectSO":
        return f"연구 분야 {scalar_text(data.get('field')) or '?'}의 선행·해금 그래프에서 진행 경계를 만든다.", "field+prerequisites+unlocks"
    if row.group == "events-campaign":
        return f"{class_name} 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다.", "event-type+requirements+effects"
    return TYPE_PURPOSE.get(class_name, f"{GROUP_LABEL[row.group]} 영역의 {class_name} 규칙을 분리해 재사용한다."), "type-contract"


def reason_for(
    row: ContentRow,
    recipe_inputs: Counter[str],
    recipe_outputs: Counter[str],
    inbound_relations: Counter[str],
) -> tuple[str, str, str]:
    data = row.data
    description = row.description.rstrip(".。 ")
    class_name = row.content_type
    evidence: list[str] = []

    if class_name in {"GenericItemDefinitionSO", "ResourceItemDefinitionSO"}:
        produced = recipe_outputs[row.stable_id]
        consumed = recipe_inputs[row.stable_id]
        features = collect_reference_types(data)
        clauses = []
        if produced and consumed:
            clauses.append(f"생산식 {produced}개의 출력이자 {consumed}개의 입력으로 쓰이는 중간 물자다")
            evidence.append("recipe-input-output")
        elif produced:
            clauses.append(f"생산식 {produced}개가 공급하는 물자다")
            evidence.append("recipe-output")
        elif consumed:
            clauses.append(f"생산식 {consumed}개가 소비하는 원료 또는 소모품이다")
            evidence.append("recipe-input")
        if features:
            roles = [ITEM_FEATURE_ROLES.get(feature, feature) for feature in features[:5]]
            clauses.append("게임 내 역할은 " + ", ".join(roles) + "에 연결된다")
            evidence.append("item-feature")
        if not clauses and description:
            clauses.append(f"작성 목적은 '{description}'이다")
            evidence.append("description")
        if not clauses:
            clauses.append("재고·거래·물리 운반에 사용되는 일반 물자다")
            return ". ".join(clauses) + ".", "generic-type-role", "수동 검토 필요"
        return ". ".join(clauses) + ".", "+".join(evidence), "근거 확인"

    if class_name == "ProductionRecipeSO":
        outputs = item_amounts(data.get("outputs"))
        inputs = item_amounts(data.get("inputs"))
        output_text = summarize_pairs(outputs, 3)
        clauses = [f"{output_text} 생산 경로를 제공한다"]
        if inputs:
            clauses.append(f"입력은 {summarize_pairs(inputs, 4)}이다")
        research = scalar_text(data.get("requiredResearchId"))
        facility = scalar_text(data.get("facilityTag"))
        if research:
            clauses.append(f"{research} 연구가 생산 시점을 제한한다")
        if facility:
            clauses.append(f"{facility} 시설 계열에 생산 역할을 부여한다")
        return ". ".join(clauses) + ".", "recipe-io-and-gates", "근거 확인"

    if class_name == "BuildingSO":
        abilities = collect_reference_types(data)
        clauses = []
        functional_roles = [BUILDING_ABILITY_ROLES[ability] for ability in abilities if ability in BUILDING_ABILITY_ROLES]
        if functional_roles:
            clauses.append("시설의 고유 역할은 " + ", ".join(functional_roles[:5]) + "이다")
        elif abilities:
            clauses.append("시설 동작은 " + ", ".join(abilities[:5]) + " 모듈로 구성된다")

        work_amount = ""
        materials: list[tuple[str, str]] = []
        references = data.get("references")
        if isinstance(references, dict):
            for reference in references.get("RefIds") or []:
                if not isinstance(reference, dict):
                    continue
                type_info = reference.get("type")
                reference_data = reference.get("data")
                if not isinstance(type_info, dict) or not isinstance(reference_data, dict):
                    continue
                if scalar_text(type_info.get("class")) != "BuildingWorkAmountAbility":
                    continue
                work_amount = scalar_text(reference_data.get("constructionWorkRequired"))
                materials = item_amounts(reference_data.get("constructionMaterials"))
                break
        construction_parts = []
        if work_amount:
            construction_parts.append(f"작업량 {work_amount}")
        if materials:
            construction_parts.append(f"재료 {summarize_pairs(materials, 4)}")
        if construction_parts:
            clauses.append("건설에는 " + ", ".join(construction_parts) + "이 필요하다")
        if description:
            clauses.append(description)
        if not clauses:
            clauses.append("그리드에 배치되는 시설 또는 구조물 정의다")
            return ". ".join(clauses) + ".", "building-type", "수동 검토 필요"
        return ". ".join(clauses) + ".", "building-abilities", "근거 확인"

    if class_name == "ResearchProjectSO":
        prerequisite_count = len(data.get("prerequisites") or []) + len(data.get("prerequisiteLinks") or [])
        unlock_count = 0
        unlocks = data.get("unlocks")
        if isinstance(unlocks, dict):
            unlock_count = sum(len(value) for value in unlocks.values() if isinstance(value, list))
        clauses = []
        if description:
            clauses.append(f"진행 역할은 '{description}'이다")
        clauses.append(f"선행 {prerequisite_count}개와 해금 {unlock_count}개를 연구 그래프에 연결한다")
        return ". ".join(clauses) + ".", "research-topology-and-unlocks", "근거 확인"

    if class_name in {"CharacterTraitSO", "HeritableTraitDefinitionSO"}:
        counts = effective_trait_counts(data)
        clauses = []
        if description:
            clauses.append(f"개체 차이의 내용은 '{description}'이다")
        clauses.append(
            f"효과 {counts['effects']}개, 정체성 규칙 {counts['identity_rules']}개, 유효 구형 보정 {counts['modifiers']}개, "
            f"전투 능력 {counts['combat_abilities']}개, 환경 보호 {counts['environmental_protection']}개, "
            f"행동 선호 {counts['behaviors']}개, 기분 반응 {counts['moods']}개, 사건 가중치 {counts['events']}개, "
            f"유전 결과 {counts['consequences']}개로 반영한다"
        )
        binding_count = sum(counts.values())
        review = "근거 확인" if description and binding_count > 0 else "수동 검토 필요"
        return ". ".join(clauses) + ".", "trait-description-and-bindings", review

    if class_name == "ResearchUnlockBundleDefinitionSO":
        intent = scalar_text(data.get("designIntent")) or scalar_text(data.get("singletonReason"))
        research_id = scalar_text(data.get("researchId"))
        reward_count = len(data.get("rewardGroups") or [])
        clauses = [f"{research_id} 완료 시 제시되는 해금 항목을 {reward_count}개 보상 범주로 조직한다"]
        if intent:
            clauses.append(f"해금 경계는 '{intent.rstrip('.。 ')}'라는 진행 의도를 보존한다")
        return ". ".join(clauses) + ".", "unlock-bundle-intent-and-groups", "근거 확인"

    if class_name == "CharacterPerformanceFormulaDefinitionSO":
        capacities = len(data.get("capacityInputs") or [])
        proficiency = scalar_text(data.get("primaryProficiencyId"))
        target = scalar_text(data.get("gameplayEffectTargetId")) or "성능 결과 채널"
        clauses = [f"신체·기능 역량 {capacities}개를 {target} 값으로 환산한다"]
        if proficiency:
            clauses.append(f"{proficiency} 숙련을 계산에 결합해 같은 인물도 담당 작업에 따라 성능이 달라지게 한다")
        return ". ".join(clauses) + ".", "performance-inputs-and-target", "근거 확인"

    if class_name == "OffenseDecisionCardSO":
        situation = scalar_text(data.get("situation")).rstrip(".。 ")
        choices = len(data.get("choices") or [])
        stage = scalar_text(data.get("stage")) or "?"
        clauses = [f"원정 단계 {stage}에서 {choices}개의 대응 방식을 제시해 보급·노출·부상·전투 위험의 교환을 만든다"]
        if situation:
            clauses.insert(0, f"상황은 '{situation}'이다")
        return ". ".join(clauses) + ".", "offense-card-choices-and-effects", "근거 확인" if choices else "수동 검토 필요"

    if class_name == "OffenseEncounterSO":
        enemies = len(data.get("enemies") or [])
        modifiers = len(data.get("battlefieldModifierIds") or [])
        counters = len(data.get("counterTags") or [])
        rewards = len(data.get("rewardItemIds") or [])
        minimum = scalar_text(data.get("minimumSiteStrength")) or "?"
        maximum = scalar_text(data.get("maximumSiteStrength")) or "?"
        return (
            f"현장 강도 {minimum}~{maximum}에서 적 구성 {enemies}개와 전장 조건 {modifiers}개를 조합한다. "
            f"대응 태그 {counters}개와 보상 {rewards}개가 전투 준비와 수익의 관계를 만든다.",
            "encounter-composition-counters-and-rewards",
            "근거 확인" if enemies else "수동 검토 필요",
        )

    if class_name == "FestivalDefinitionSO":
        culture = scalar_text(data.get("cultureId")) or "공동체 문화"
        required_items = len(data.get("requiredItems") or [])
        required_building = scalar_text(data.get("requiredBuildingDefinitionId"))
        participants = scalar_text(data.get("minimumParticipants")) or "0"
        clauses = []
        if description:
            clauses.append(f"행사의 사회적 기능은 '{description}'이다")
        clauses.append(f"{culture} 관습을 최소 {participants}명의 참여와 물자 {required_items}종의 공동 소비로 실행한다")
        if required_building:
            clauses.append(f"{required_building}을 요구해 문화 행사를 실제 공간 투자와 연결한다")
        clauses.append("성공·부분 성공·실패 결과가 기분과 세력 관계의 차이를 남긴다")
        return ". ".join(clauses) + ".", "festival-requirements-and-outcomes", "근거 확인"

    if class_name == "CropDefinitionSO":
        harvest = scalar_text(data.get("harvestItemId")) or "수확물"
        seed = scalar_text(data.get("seedItemId")) or "종자"
        growth = scalar_text(data.get("growthHours")) or "?"
        water = scalar_text(data.get("dailyWater")) or "0"
        crop_yield = scalar_text(data.get("yield")) or "?"
        research = scalar_text(data.get("requiredResearchId"))
        clauses = [f"{seed}를 {growth}시간 재배해 {harvest} {crop_yield}개를 생산하며 하루 물 {water}를 소비한다"]
        if research:
            clauses.append(f"{research}가 재배 시점을 제한한다")
        return ". ".join(clauses) + ".", "crop-input-time-water-and-yield", "근거 확인"

    if class_name == "AnatomyProfileSO":
        species = len(data.get("speciesIds") or [])
        nodes = len(data.get("nodes") or [])
        excluded = len(data.get("notApplicableCapacities") or [])
        return (
            f"종족 {species}종의 신체를 해부 노드 {nodes}개로 구성해 부상·질병·수술의 적용 위치를 제공한다. "
            f"적용 불가 기능 {excluded}개를 분리해 종족별 생리 차이를 보존한다.",
            "anatomy-species-nodes-and-capacities",
            "근거 확인" if nodes else "수동 검토 필요",
        )

    if class_name == "AnatomyConditionLexiconSO":
        family = scalar_text(data.get("anatomyFamily")) or "공통"
        species = len(data.get("speciesIds") or [])
        entries = len(data.get("entries") or [])
        return (
            f"{family} 해부 계열의 종족 {species}종에 대해 신체 상태 표현 {entries}개를 제공한다. 같은 손상 규칙을 종족별 기관 명칭과 생리 표현으로 번역한다.",
            "anatomy-family-species-and-condition-entries",
            "근거 확인" if entries else "수동 검토 필요",
        )

    if class_name == "ReproductionProfileSO":
        species = scalar_text(data.get("speciesTag")) or "종족"
        chance = scalar_text(data.get("baseSuccessChance")) or "?"
        phases = len(data.get("phases") or [])
        low = scalar_text(data.get("viableTemperatureMinimum")) or "?"
        high = scalar_text(data.get("viableTemperatureMaximum")) or "?"
        return (
            f"{species}의 생식 성공률 {chance}, 생식 단계 {phases}개와 생존 온도 {low}~{high}℃를 정의해 인구 증가를 환경·시간 조건에 연결한다.",
            "reproduction-chance-phases-and-environment",
            "근거 확인",
        )

    if class_name == "SpeciesLifeHistorySO":
        species = scalar_text(data.get("speciesTag")) or "종족"
        adult = scalar_text(data.get("adultAgeYears")) or "?"
        elder = scalar_text(data.get("elderAgeYears")) or "?"
        lifespan = scalar_text(data.get("untreatedExpectedLifeYears")) or "?"
        return (
            f"{species}의 성년 {adult}세, 노년 {elder}세, 무치료 기대수명 {lifespan}년을 규정해 성장·노화·세대교체의 시간축을 제공한다.",
            "life-stages-and-longevity",
            "근거 확인",
        )

    if class_name == "EndingDefinitionSO":
        requirements = count_requirements(data.get("completionRequirements"))
        rewards = len(data.get("permanentRewards") or [])
        pressures = len(data.get("counterPressures") or [])
        landmark = scalar_text(data.get("landmarkBuildingId"))
        clauses = []
        if description:
            clauses.append(f"장기 목표는 '{description}'이다")
        clauses.append(f"달성 조건 {requirements}개, 영구 보상 {rewards}개, 후속 압력 {pressures}개로 완료 이후의 운영 변화를 규정한다")
        if landmark:
            clauses.append(f"{landmark} 건설을 목표의 물리적 증거로 요구한다")
        return ". ".join(clauses) + ".", "ending-requirements-rewards-and-pressure", "근거 확인"

    if class_name == "ServiceIncidentDefinitionSO":
        requirements = count_requirements(data.get("triggerRequirements"))
        responses = len(data.get("responses") or [])
        clauses = []
        if description:
            clauses.append(f"서비스 사고는 '{description}'이다")
        clauses.append(f"발생 조건 {requirements}개와 대응 {responses}개를 통해 서비스 운영의 위험을 선택 결과로 전환한다")
        return ". ".join(clauses) + ".", "service-incident-triggers-and-responses", "근거 확인" if responses else "수동 검토 필요"

    if class_name == "FactionArcDefinitionSO":
        chapters = len(data.get("chapterIds") or [])
        contracts = len(data.get("contractIds") or [])
        relics = len(data.get("relicItemIds") or [])
        clauses = []
        if description:
            clauses.append(f"세력 서사의 중심 갈등은 '{description}'이다")
        clauses.append(f"장 {chapters}개, 계약 {contracts}개, 유물 {relics}개를 하나의 관계 진행선으로 묶는다")
        return ". ".join(clauses) + ".", "faction-arc-chapters-contracts-and-relics", "근거 확인"

    if class_name == "CareerPositionDefinitionSO":
        occupants = scalar_text(data.get("maximumOccupants")) or "?"
        facility = scalar_text(data.get("requiredFacilityTag"))
        clauses = [f"조직 내 직책의 권한 범위와 최대 재직자 {occupants}명을 규정한다"]
        if facility:
            clauses.append(f"{facility} 시설을 요구해 직책을 실제 운영 기반과 연결한다")
        return ". ".join(clauses) + ".", "career-scope-capacity-and-facility", "근거 확인"

    if class_name == "WeatherFrontDefinitionSO":
        minimum = scalar_text(data.get("minimumDurationDays")) or "?"
        maximum = scalar_text(data.get("maximumDurationDays")) or "?"
        temperature = scalar_text(data.get("temperatureModifierC")) or "0"
        return (
            f"{minimum}~{maximum}일 동안 기온을 {temperature}℃ 조정하고 계절별 출현 가중치를 달리해 생산·생존 환경의 단기 변동을 만든다.",
            "weather-duration-temperature-and-season-weight",
            "근거 확인",
        )

    if class_name == "ClimateZoneDefinitionSO":
        mean = scalar_text(data.get("meanTemperatureC")) or "?"
        amplitude = scalar_text(data.get("annualAmplitudeC")) or "?"
        offset = scalar_text(data.get("localHourOffset")) or "0"
        return (
            f"평균 기온 {mean}℃, 연교차 {amplitude}℃, 현지 시각 보정 {offset}시간을 제공해 지역별 장기 환경 기준을 만든다.",
            "climate-temperature-amplitude-and-time",
            "근거 확인",
        )

    if class_name == "ServiceProcessSO":
        hub = scalar_text(data.get("ownerHubTag")) or "서비스 시설"
        clean_water = scalar_text(data.get("cleanWater")) or "0"
        wastewater = scalar_text(data.get("wastewater")) or "0"
        modes = len(data.get("modeContracts") or [])
        return (
            f"{hub}에서 서비스 방식 {modes}개를 제공하며, 1회 처리마다 깨끗한 물 {clean_water}를 소비하고 폐수 {wastewater}를 배출한다. 결제와 청소 조건을 같은 공정에 묶는다.",
            "service-modes-water-waste-and-payment",
            "근거 확인",
        )

    if class_name == "AgeConditionDefinitionSO":
        nodes = len(data.get("affectedAnatomyNodeIds") or [])
        subject = "구조체" if data.get("constructCondition") else "생물"
        return (
            f"{subject}의 노화가 해부 부위 {nodes}곳에 남기는 기능 저하를 정의해 나이 증가를 의료·작업 능력 변화에 연결한다.",
            "age-condition-anatomy-binding",
            "근거 확인" if nodes else "수동 검토 필요",
        )

    if row.group == "events-campaign" or class_name == "FestivalDefinitionSO":
        requirements = count_requirements(data.get("triggerRequirements") or data.get("requirements"))
        effects = count_effects(data)
        choices = len(data.get("choices") or [])
        clauses = []
        if description:
            clauses.append(f"사건 역할은 '{description}'이다")
        clauses.append(f"요구 조건 {requirements}개, 선택지 {choices}개, 상태 효과 {effects}개를 통해 발생 조건과 결과를 규정한다")
        review = "근거 확인" if description and (requirements + effects + choices) > 0 else "수동 검토 필요"
        return ". ".join(clauses) + ".", "event-requirements-and-effects", review

    incoming = inbound_relations[row.stable_id]
    clauses = []
    purpose = TYPE_PURPOSE.get(class_name)
    if purpose:
        clauses.append(purpose)
    if description:
        clauses.append(f"작성 목적은 '{description}'이다")
    if incoming:
        clauses.append(f"다른 콘텐츠 {incoming}개가 이 정의를 참조한다")
    mechanics = row.mechanics
    if mechanics and mechanics != "작성 자산 정의":
        clauses.append(f"게임 규칙은 {mechanics}로 구성된다")
    if clauses:
        basis = "type-contract+description-and-relations" if purpose else ("description-and-relations" if description else "relations-or-mechanics")
        review = "근거 확인" if purpose or description or incoming else "수동 검토 필요"
        return ". ".join(clauses) + ".", basis, review
    return f"{GROUP_LABEL[row.group]} 카탈로그의 작성 자산이다.", "generic-type-role", "수동 검토 필요"


def escape_markdown(value: str) -> str:
    return value.replace("|", "\\|").replace("\r", " ").replace("\n", "<br>").strip()


def source_link(path: str, prefix: str = "../../") -> str:
    target = prefix + path
    return f"[{Path(path).name}]({target})"


def write_csv(path: Path, rows: Iterable[dict[str, str]], fieldnames: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def code_system_for(source_path: str) -> str:
    parts = Path(source_path).parts
    try:
        scripts_index = parts.index("Scripts")
    except ValueError:
        return "unknown"
    tail = parts[scripts_index + 1 :]
    if not tail:
        return "unknown"
    if tail[0] in {"Services", "Models"} and len(tail) > 1:
        return f"{tail[0].lower()}:{tail[1].lower()}"
    return tail[0].lower()


def code_role_for(source_path: str) -> str:
    lowered = source_path.lower()
    filename = Path(source_path).stem.lower()
    if "/save/" in lowered or any(token in filename for token in ("save", "restore", "snapshot", "codec")):
        return "persistence"
    if "/ai/" in lowered or filename.startswith("ai") or "scheduler" in filename:
        return "ai-decision"
    if "/views/" in lowered or "/ui/" in lowered or any(token in filename for token in ("view", "presenter", "hud")):
        return "player-observation"
    if "catalog" in filename or "/content/" in lowered:
        return "content-loading"
    return "runtime"


def build_code_consumer_rows(
    rows_by_type: dict[str, list[ContentRow]],
    type_refs: dict[str, set[str]],
    literal_refs: dict[str, set[str]],
    resource_loads: dict[str, set[str]],
    save_refs: dict[str, set[str]],
    script_by_record: dict[str, Path | None],
) -> dict[str, list[dict[str, str]]]:
    result: dict[str, list[dict[str, str]]] = defaultdict(list)
    for content_type, type_rows in rows_by_type.items():
        own_scripts = {
            relative(script)
            for row in type_rows
            if (script := script_by_record[row.record_key]) is not None
        }
        for source_path in sorted(type_refs[content_type] - own_scripts):
            evidence = ["type-reference"]
            if source_path in resource_loads[content_type]:
                evidence.append("resources-load")
            if source_path in save_refs[content_type]:
                evidence.append("save-restore-reference")
            result[content_type].append(
                {
                    "content_type": content_type,
                    "scope": "content-type",
                    "stable_id": "",
                    "evidence_kinds": "; ".join(evidence),
                    "system": code_system_for(source_path),
                    "code_role": code_role_for(source_path),
                    "confidence": "structural-type-consumer",
                    "source_path": source_path,
                }
            )
        for row in type_rows:
            for source_path in sorted(literal_refs[row.stable_id] - own_scripts):
                result[content_type].append(
                    {
                        "content_type": content_type,
                        "scope": "stable-id",
                        "stable_id": row.stable_id,
                        "evidence_kinds": "stable-id-literal",
                        "system": code_system_for(source_path),
                        "code_role": code_role_for(source_path),
                        "confidence": "exact-literal-consumer",
                        "source_path": source_path,
                    }
                )
        deduplicated = {
            (
                row["scope"],
                row["stable_id"],
                row["evidence_kinds"],
                row["source_path"],
            ): row
            for row in result[content_type]
        }
        result[content_type] = sorted(
            deduplicated.values(),
            key=lambda row: (row["scope"], row["stable_id"], row["source_path"]),
        )
    return result


def csv_stem_for_type(class_name: str) -> str:
    stem = re.sub(r"(?:Definition)?SO$", "", class_name)
    stem = re.sub(r"(?<!^)(?=[A-Z])", "-", stem).lower()
    return stem


def write_group_markdown(
    group: str,
    rows: list[ContentRow],
    type_index: dict[str, dict[str, Any]],
) -> None:
    filename = {
        "items": "01-items.md",
        "production-facilities": "02-production-and-facilities.md",
        "characters-traits": "03-characters-traits-and-society.md",
        "events-campaign": "04-events-and-campaign.md",
        "research-effects": "05-research-effects-and-progression.md",
        "combat-health-world": "06-combat-health-and-world.md",
    }[group]
    lines = [
        f"# {GROUP_LABEL[group]} 콘텐츠 데이터베이스",
        "",
        "현재 Unity 작성 자산을 유형별로 나눈 영역 색인이다. 개별 항목은 유형 문서와 CSV에서 확인한다.",
        "",
        f"총 {len(rows):,}개 항목이다.",
        "",
        "| 유형 | 항목 | 런타임 확인 | 수동 검토 | 문서 | 작성 필드 | 관계 | 역참조 |",
        "|---|---:|---:|---:|---|---|---|---|",
    ]
    for class_name in sorted({row.content_type for row in rows}):
        entry = type_index[class_name]
        lines.append(
            "| "
            + " | ".join(
                escape_markdown(value)
                for value in (
                    f"`{class_name}`",
                    f"{entry['content_count']:,}",
                    f"{entry['runtime_confirmed_count']:,}",
                    f"{entry['manual_review_count']:,}",
                    f"[열기]({entry['type_doc']})",
                    f"[CSV]({entry['field_csv']})",
                    f"[CSV]({entry['relation_csv']})",
                    f"[CSV]({entry['incoming_csv']})",
                )
            )
            + " |"
        )
    (OUTPUT / filename).write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_type_markdown(group: str, class_name: str, rows: list[ContentRow], entry: dict[str, Any]) -> None:
    directory = OUTPUT / "types" / group / csv_stem_for_type(class_name)
    directory.mkdir(parents=True, exist_ok=True)
    chunk_size = 150
    chunks = [rows[index : index + chunk_size] for index in range(0, len(rows), chunk_size)] or [[]]
    overview = [
        f"# {class_name}",
        "",
        TYPE_PURPOSE.get(class_name, f"{GROUP_LABEL[group]} 영역의 작성 콘텐츠 유형이다."),
        "",
        f"총 {len(rows):,}개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.",
        "",
        "## 데이터",
        "",
        f"- [유형별 콘텐츠 CSV](../../../{entry['content_csv']})",
        f"- [중첩 작성 필드 CSV](../../../{entry['field_csv']})",
        f"- [정방향 관계 CSV](../../../{entry['relation_csv']})",
        f"- [역방향 관계 CSV](../../../{entry['incoming_csv']})",
        "",
    ]
    if len(chunks) > 1:
        overview.extend(["## 항목 문서", ""])
        for index, chunk in enumerate(chunks, start=1):
            start = (index - 1) * chunk_size + 1
            end = start + len(chunk) - 1
            overview.append(f"- [항목 {start:,}–{end:,}](part-{index:03d}.md)")
        overview.append("")
    else:
        overview.extend(render_content_table(rows))
    (directory / "README.md").write_text("\n".join(overview) + "\n", encoding="utf-8")
    if len(chunks) > 1:
        for index, chunk in enumerate(chunks, start=1):
            lines = [f"# {class_name} — {index}/{len(chunks)}", "", f"[유형 개요](README.md)", ""]
            lines.extend(render_content_table(chunk))
            (directory / f"part-{index:03d}.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def render_content_table(rows: list[ContentRow]) -> list[str]:
    lines = [
        "| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |",
        "|---|---|---|---|---|---|---:|---|",
    ]
    for row in rows:
        values = (
            f"`{row.stable_id}`",
            row.display_name,
            row.strategic_niche,
            row.costs_and_risks,
            row.runtime_status,
            row.lifecycle_status,
            str(row.incoming_reference_count),
            source_link(row.source_path, "../../../../../"),
        )
        lines.append("| " + " | ".join(escape_markdown(value) for value in values) + " |")
    return lines


def script_path_for(data: dict[str, Any], guid_to_path: dict[str, Path]) -> Path | None:
    script = data.get("m_Script")
    if not isinstance(script, dict):
        return None
    path = guid_to_path.get(scalar_text(script.get("guid")))
    return path if path and path.suffix == ".cs" else None


def excluded_asset_reason(class_name: str) -> str:
    simple_name = class_name.rsplit(".", 1)[-1]
    if simple_name.endswith("CatalogSO") or "Catalog" in simple_name:
        return "카탈로그·등록 인프라: 개별 콘텐츠가 아니라 다른 자산의 집합 권위"
    if simple_name in IGNORED_CLASSES or class_name.startswith(("UnityEngine.", "UnityEditor.")):
        return "기술·현지화·Addressables·표현 설정: 플레이 규칙 콘텐츠에서 제외"
    lowered = simple_name.lower()
    if simple_name.startswith(("AI", "Consideration")) or simple_name in {"DNPPreset", "StatChange"}:
        return "AI 의사결정 구성: 콘텐츠 정의가 아니라 행동·고려점 실행 그래프"
    if simple_name == "GameData" or simple_name.endswith(("RulesSO", "SettingsSO")):
        return "전역 시스템 규칙·밸런스 설정: 개별 콘텐츠 항목이 아닌 시스템 권위"
    if any(token in lowered for token in ("settings", "library", "theme", "palette", "audio", "visual", "presentation")):
        return "기술 또는 표현 설정: 플레이 규칙 콘텐츠에서 제외"
    if any(token in lowered for token in ("behavior", "blackboard", "runtime", "state", "controller")):
        return "런타임·AI 구성 자산: 독립 콘텐츠 DB에서 제외"
    return "미분류 MonoBehaviour 자산: 콘텐츠 포함 여부 수동 검토"


def raw_field_value(value: Any) -> str:
    return scalar_text(value) if not isinstance(value, (dict, list)) else compact_json(value)


def target_rows_for(candidate: RelationCandidate, stable_rows: dict[str, list[ContentRow]]) -> list[ContentRow]:
    candidates = stable_rows.get(candidate.target_id, [])
    if candidate.target_category == "item-content":
        return [row for row in candidates if row.group == "items"]
    if candidate.target_category == "faction-content":
        return [row for row in candidates if "Faction" in row.content_type]
    if candidate.target_category == "disease-content":
        return [row for row in candidates if row.content_type == "DiseaseDefinitionSO"]
    if candidate.target_category == "ambition-content":
        return [row for row in candidates if row.content_type == "CharacterAmbitionDefinitionSO"]
    return candidates


def relation_resolution(candidate: RelationCandidate, targets: list[ContentRow]) -> tuple[str, str]:
    if candidate.target_category in {"effect-channel", "protocol-id"}:
        return "non-content-protocol", ""
    if targets:
        return "resolved-content", "; ".join(sorted(row.record_key for row in targets))
    if candidate.target_category == "faction-content":
        return "unresolved-runtime-domain-id", ""
    return "unresolved-content-reference", ""


def runtime_confirmed(status: str) -> bool:
    return status in {
        "catalog-registered-static-consumer",
        "resources-loader-static-consumer",
        "stable-id-literal-consumer",
    }


def main(argv: list[str] | None = None) -> int:
    global OUTPUT
    args = parse_args(argv or sys.argv[1:])
    OUTPUT = resolve_output_root(args.output_root)
    guid_to_path, path_to_guid = build_guid_index()
    enums, fields_by_script = build_csharp_enum_index()
    parsed: list[tuple[Path, str, dict[str, Any], str, Path | None]] = []
    errors: list[dict[str, str]] = []
    catalog_assets: dict[str, tuple[str, set[str]]] = {}
    excluded_counts: Counter[str] = Counter()

    for asset in sorted(ASSETS.rglob("*.asset")):
        try:
            documents = parse_unity_asset(asset)
        except Exception as exc:  # noqa: BLE001 - report every malformed source asset
            errors.append({"source_path": relative(asset), "error": f"{type(exc).__name__}: {exc}"})
            continue
        for document in documents:
            data = document.get("MonoBehaviour")
            if not isinstance(data, dict):
                continue
            class_name = resolve_class_name(data, guid_to_path)
            if not class_name:
                continue
            asset_guid = path_to_guid.get(asset, "")
            if asset_guid and (class_name.endswith("CatalogSO") or "Catalog" in class_name):
                catalog_assets[asset_guid] = (relative(asset), collect_all_guids(data))
            if class_name in IGNORED_CLASSES or class_name not in CLASS_TO_GROUP:
                excluded_counts[class_name] += 1
                continue
            stable_id, synthetic_id = normalize_id(class_name, data, asset)
            parsed.append((asset, class_name, data, stable_id, script_path_for(data, guid_to_path)))

    guid_to_content_id: dict[str, str] = {}
    for asset, _, _, stable_id, _ in parsed:
        guid = path_to_guid.get(asset, "")
        if guid:
            guid_to_content_id[guid] = stable_id

    rows: list[ContentRow] = []
    relations: list[Relation] = []
    duplicate_counter = Counter((class_name, stable_id) for _, class_name, _, stable_id, _ in parsed)
    script_by_record: dict[str, Path | None] = {}

    for asset, class_name, data, stable_id, script_path in parsed:
        display_name = first_text(data, DISPLAY_CANDIDATES) or stable_id
        description = first_text(data, DESCRIPTION_CANDIDATES)
        mechanics = summarize_mechanics(class_name, data)
        asset_guid = path_to_guid.get(asset, "")
        _, synthetic_id = normalize_id(class_name, data, asset)
        duplicate_key = duplicate_counter[(class_name, stable_id)] > 1
        review_reasons = []
        if synthetic_id:
            review_reasons.append("synthetic-stable-id")
        if duplicate_key:
            review_reasons.append("duplicate-stable-id-authority")
        review = "수동 검토 필요" if review_reasons else "근거 확인"
        record_key = f"{class_name}|{stable_id}"
        if duplicate_key:
            record_key += f"|{relative(asset)}"
        row = ContentRow(
                group=CLASS_TO_GROUP[class_name],
                content_type=class_name,
                record_key=record_key,
                stable_id=stable_id,
                display_name=display_name,
                description=description,
                mechanics=mechanics,
                relations="",
                existence_reason="",
                reason_basis="",
                review_status=review,
                review_reason="; ".join(review_reasons),
                lifecycle_status="",
                catalog_memberships="",
                runtime_status="",
                runtime_evidence="",
                save_evidence="",
                incoming_reference_count=0,
                incoming_source_types="",
                system_role="",
                strategic_niche="",
                costs_and_risks="",
                comparison_group="",
                alternative_candidates="",
                removal_impact="",
                rationale_evidence="",
                source_path=relative(asset),
                asset_guid=asset_guid,
                data=data,
                authored_fields=authored_columns(class_name, data, script_path, enums, fields_by_script),
            )
        rows.append(row)
        script_by_record[record_key] = script_path

    stable_rows: dict[str, list[ContentRow]] = defaultdict(list)
    row_by_asset_guid: dict[str, list[ContentRow]] = defaultdict(list)
    for row in rows:
        stable_rows[row.stable_id].append(row)
        if row.asset_guid:
            row_by_asset_guid[row.asset_guid].append(row)

    parsed_by_source = {relative(asset): data for asset, _, data, _, _ in parsed}
    for row in rows:
        data = parsed_by_source[row.source_path]
        seen_relations: set[tuple[str, str, str, str, str]] = set()
        row_relations: list[str] = []
        for candidate in iter_content_relation_candidates(data):
            top_field = candidate.field_path.split(".", 1)[0]
            if top_field in OWN_ID_FIELDS and candidate.target_id == row.stable_id:
                continue
            targets = target_rows_for(candidate, stable_rows)
            resolution_status, target_keys = relation_resolution(candidate, targets)
            key = (candidate.field_path, candidate.kind, candidate.target_id, candidate.amount, candidate.duration)
            if key in seen_relations:
                continue
            seen_relations.add(key)
            relations.append(
                Relation(
                    source_type=row.content_type,
                    source_id=row.stable_id,
                    source_record_key=row.record_key,
                    kind=candidate.kind,
                    target_id=candidate.target_id,
                    amount=candidate.amount,
                    duration=candidate.duration,
                    field_path=candidate.field_path,
                    semantic_label=candidate.semantic_label,
                    target_category=candidate.target_category,
                    resolution_status=resolution_status,
                    target_record_keys=target_keys,
                    source_path=row.source_path,
                )
            )
            row_relations.append(f"{candidate.kind}:{candidate.target_id}" + (f"×{candidate.amount}" if candidate.amount else ""))
        for path_text, target_id, amount in collect_guid_relations(data, guid_to_content_id):
            if target_id == row.stable_id:
                continue
            candidate = RelationCandidate(
                field_path=path_text,
                target_id=target_id,
                amount=amount,
                duration="",
                kind=relation_kind_for_path(path_text),
                semantic_label="Unity asset reference",
                target_category="content-reference",
            )
            targets = target_rows_for(candidate, stable_rows)
            resolution_status, target_keys = relation_resolution(candidate, targets)
            key = (path_text, candidate.kind, target_id, amount, "")
            if key in seen_relations:
                continue
            seen_relations.add(key)
            relations.append(
                Relation(
                    source_type=row.content_type,
                    source_id=row.stable_id,
                    source_record_key=row.record_key,
                    kind=candidate.kind,
                    target_id=target_id,
                    amount=amount,
                    duration="",
                    field_path=path_text,
                    semantic_label=candidate.semantic_label,
                    target_category=candidate.target_category,
                    resolution_status=resolution_status,
                    target_record_keys=target_keys,
                    source_path=row.source_path,
                )
            )
            row_relations.append(f"{candidate.kind}:{target_id}")
        row.relations = "; ".join(sorted(row_relations))

    catalog_memberships = build_catalog_memberships(catalog_assets)
    type_refs, literal_refs, resource_loads, save_refs = build_code_evidence_index(
        {row.stable_id for row in rows}, {row.content_type for row in rows}
    )
    for row in rows:
        script_path = script_by_record[row.record_key]
        own_script = relative(script_path) if script_path else ""
        consumers = set(type_refs[row.content_type])
        savers = set(save_refs[row.content_type])
        consumers.discard(own_script)
        savers.discard(own_script)
        memberships = catalog_memberships.get(row.asset_guid, set())
        lifecycle, runtime_status, runtime_evidence, save_evidence = runtime_classification(
            row,
            memberships,
            consumers,
            literal_refs[row.stable_id],
            resource_loads[row.content_type],
            savers,
            duplicate_counter[(row.content_type, row.stable_id)] > 1,
        )
        row.lifecycle_status = lifecycle
        row.catalog_memberships = "; ".join(sorted(memberships)) or "카탈로그 등록 정적 근거 없음"
        row.runtime_status = runtime_status
        row.runtime_evidence = runtime_evidence
        row.save_evidence = save_evidence

    incoming_by_record: dict[str, list[Relation]] = defaultdict(list)
    for relation in relations:
        if relation.resolution_status != "resolved-content":
            continue
        for target_key in filter(None, relation.target_record_keys.split("; ")):
            incoming_by_record[target_key].append(relation)
    for row in rows:
        incoming = incoming_by_record[row.record_key]
        row.incoming_reference_count = len(incoming)
        row.incoming_source_types = "; ".join(sorted({relation.source_type for relation in incoming}))

    recipe_inputs: Counter[str] = Counter()
    recipe_outputs: Counter[str] = Counter()
    inbound_relations: Counter[str] = Counter(
        relation.target_id for relation in relations if relation.resolution_status == "resolved-content"
    )
    for row in rows:
        if row.content_type != "ProductionRecipeSO":
            continue
        for item_id, _ in item_amounts(row.data.get("inputs")):
            recipe_inputs[item_id] += 1
        for item_id, _ in item_amounts(row.data.get("outputs")):
            recipe_outputs[item_id] += 1

    for row in rows:
        reason, basis, review = reason_for(row, recipe_inputs, recipe_outputs, inbound_relations)
        row.existence_reason = reason
        row.reason_basis = basis
        if row.review_status == "근거 확인":
            row.review_status = review
            if review != "근거 확인":
                row.review_reason = "existence-reason-evidence-insufficient"
        row.system_role = reason
        row.strategic_niche, niche_evidence = strategic_niche_for(row)
        row.costs_and_risks, cost_evidence = costs_and_risks_for(row)
        row.comparison_group = comparison_group_for(row)
        sources = incoming_by_record[row.record_key]
        if sources:
            source_labels = sorted({f"{relation.source_type}:{relation.source_id}" for relation in sources})
            row.removal_impact = (
                f"직접 참조 {len(sources)}건에 영향: " + ", ".join(source_labels[:10])
                + (" 외" if len(source_labels) > 10 else "")
            )
        else:
            row.removal_impact = "정적 콘텐츠 역참조는 확인되지 않음; 런타임 직접 조회와 설계상 공백은 별도 검증 필요"
        row.rationale_evidence = f"{basis}; niche={niche_evidence}; cost={cost_evidence}"

    comparison_members: dict[str, list[ContentRow]] = defaultdict(list)
    for row in rows:
        comparison_members[row.comparison_group].append(row)
    for row in rows:
        alternatives = [candidate for candidate in comparison_members[row.comparison_group] if candidate.record_key != row.record_key]
        if alternatives:
            labels = [f"{candidate.content_type}:{candidate.stable_id}" for candidate in alternatives[:12]]
            row.alternative_candidates = "비교 후보(대체 가능성 미검증): " + ", ".join(labels)
            if len(alternatives) > 12:
                row.alternative_candidates += f" 외 {len(alternatives) - 12}개"
        else:
            row.alternative_candidates = "동일 비교군 없음"

    rows.sort(key=lambda row: (row.group, row.content_type, row.stable_id, row.source_path))
    relations.sort(key=lambda value: (value.source_type, value.source_record_key, value.kind, value.target_id, value.field_path))
    prepare_output_root(OUTPUT)

    content_fields = [
        "group",
        "content_type",
        "record_key",
        "stable_id",
        "display_name",
        "description",
        "mechanics",
        "relations",
        "existence_reason",
        "reason_basis",
        "review_status",
        "review_reason",
        "lifecycle_status",
        "catalog_memberships",
        "runtime_status",
        "runtime_evidence",
        "save_evidence",
        "incoming_reference_count",
        "incoming_source_types",
        "system_role",
        "strategic_niche",
        "costs_and_risks",
        "comparison_group",
        "alternative_candidates",
        "removal_impact",
        "rationale_evidence",
        "source_path",
        "asset_guid",
    ]
    relation_fields = [
        "source_type", "source_id", "source_record_key", "kind", "target_id", "amount", "duration",
        "field_path", "semantic_label", "target_category", "resolution_status", "target_record_keys", "source_path",
    ]

    content_csv_root = OUTPUT / "csv"
    relation_csv_root = OUTPUT / "relations"
    field_csv_root = OUTPUT / "fields"
    incoming_csv_root = OUTPUT / "incoming"
    type_doc_root = OUTPUT / "types"
    code_consumer_root = OUTPUT / "code-consumers"
    for generated_directory in (
        content_csv_root,
        relation_csv_root,
        field_csv_root,
        incoming_csv_root,
        type_doc_root,
        code_consumer_root,
    ):
        if generated_directory.exists():
            shutil.rmtree(generated_directory)
    for obsolete_file in (OUTPUT / "content-master.csv", OUTPUT / "content-relations.csv"):
        if obsolete_file.exists():
            obsolete_file.unlink()

    rows_by_type: dict[str, list[ContentRow]] = defaultdict(list)
    relations_by_type: dict[str, list[Relation]] = defaultdict(list)
    incoming_by_type: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        rows_by_type[row.content_type].append(row)
    for relation in relations:
        relations_by_type[relation.source_type].append(relation)
        if relation.resolution_status == "resolved-content":
            for target_key in filter(None, relation.target_record_keys.split("; ")):
                target = next((row for row in stable_rows[relation.target_id] if row.record_key == target_key), None)
                if target:
                    payload = relation.__dict__.copy()
                    payload["target_record_key"] = target_key
                    payload["target_type"] = target.content_type
                    incoming_by_type[target.content_type].append(payload)

    code_consumers_by_type = build_code_consumer_rows(
        rows_by_type,
        type_refs,
        literal_refs,
        resource_loads,
        save_refs,
        script_by_record,
    )

    type_index_rows: list[dict[str, str | int]] = []
    used_enum_types: set[str] = set()
    for class_name in sorted(rows_by_type):
        group = CLASS_TO_GROUP[class_name]
        csv_stem = csv_stem_for_type(class_name)
        content_path = content_csv_root / group / f"{csv_stem}.csv"
        relation_path = relation_csv_root / group / f"{csv_stem}.csv"
        field_path = field_csv_root / group / f"{csv_stem}.csv"
        incoming_path = incoming_csv_root / group / f"{csv_stem}.csv"
        code_consumer_path = code_consumer_root / group / f"{csv_stem}.csv"
        type_rows = rows_by_type[class_name]
        type_relations = relations_by_type[class_name]
        authored_fieldnames = sorted({key for row in type_rows for key in row.authored_fields})
        typed_content_fields = content_fields + authored_fieldnames
        write_csv(
            content_path,
            ({**row.__dict__, **row.authored_fields} for row in type_rows),
            typed_content_fields,
        )
        write_csv(relation_path, (relation.__dict__ for relation in type_relations), relation_fields)
        field_rows: list[dict[str, str]] = []
        for row in type_rows:
            script_path = script_by_record[row.record_key]
            for authored_path, value in flatten_authored_fields(row.data):
                enum_type, label = enum_label(class_name, authored_path, value, script_path, enums, fields_by_script)
                if enum_type:
                    used_enum_types.add(enum_type)
                field_rows.append(
                    {
                        "record_key": row.record_key,
                        "stable_id": row.stable_id,
                        "field_path": authored_path,
                        "raw_value": raw_field_value(value),
                        "enum_type": enum_type,
                        "enum_label": label,
                        "value_origin": "explicit-serialized",
                    }
                )
            if script_path:
                authored_keys = {key.casefold() for key in row.data}
                for enum_field in sorted(fields_by_script.get(script_path, {})):
                    if enum_field.casefold() in authored_keys:
                        continue
                    enum_type, label = enum_label(class_name, enum_field, 0, script_path, enums, fields_by_script)
                    if not label:
                        continue
                    used_enum_types.add(enum_type)
                    field_rows.append(
                        {
                            "record_key": row.record_key,
                            "stable_id": row.stable_id,
                            "field_path": enum_field,
                            "raw_value": "0",
                            "enum_type": enum_type,
                            "enum_label": label,
                            "value_origin": "implicit-csharp-default",
                        }
                    )
        write_csv(
            field_path,
            field_rows,
            ["record_key", "stable_id", "field_path", "raw_value", "enum_type", "enum_label", "value_origin"],
        )
        incoming_fields = ["target_type", "target_record_key"] + relation_fields
        write_csv(incoming_path, incoming_by_type[class_name], incoming_fields)
        code_consumer_fields = [
            "content_type",
            "scope",
            "stable_id",
            "evidence_kinds",
            "system",
            "code_role",
            "confidence",
            "source_path",
        ]
        write_csv(code_consumer_path, code_consumers_by_type[class_name], code_consumer_fields)
        type_doc = f"types/{group}/{csv_stem}/README.md"
        type_index_rows.append(
            {
                "group": group,
                "content_type": class_name,
                "content_count": len(type_rows),
                "content_csv": content_path.relative_to(OUTPUT).as_posix(),
                "relation_count": len(type_relations),
                "relation_csv": relation_path.relative_to(OUTPUT).as_posix(),
                "field_count": len(field_rows),
                "field_csv": field_path.relative_to(OUTPUT).as_posix(),
                "incoming_reference_count": len(incoming_by_type[class_name]),
                "incoming_csv": incoming_path.relative_to(OUTPUT).as_posix(),
                "code_consumer_count": len(code_consumers_by_type[class_name]),
                "code_consumer_csv": code_consumer_path.relative_to(OUTPUT).as_posix(),
                "runtime_confirmed_count": sum(runtime_confirmed(row.runtime_status) for row in type_rows),
                "manual_review_count": sum(row.review_status != "근거 확인" for row in type_rows),
                "type_doc": type_doc,
            }
        )

    write_csv(
        OUTPUT / "content-type-index.csv",
        type_index_rows,
        [
            "group",
            "content_type",
            "content_count",
            "content_csv",
            "relation_count",
            "relation_csv",
            "field_count",
            "field_csv",
            "incoming_reference_count",
            "incoming_csv",
            "code_consumer_count",
            "code_consumer_csv",
            "runtime_confirmed_count",
            "manual_review_count",
            "type_doc",
        ],
    )
    write_csv(
        OUTPUT / "manual-review.csv",
        (row.__dict__ for row in rows if row.review_status != "근거 확인"),
        content_fields,
    )
    write_csv(OUTPUT / "parse-errors.csv", errors, ["source_path", "error"])
    excluded_rows = [
        {"content_type": class_name, "asset_count": count, "exclusion_reason": excluded_asset_reason(class_name)}
        for class_name, count in sorted(excluded_counts.items())
    ]
    write_csv(OUTPUT / "excluded-asset-types.csv", excluded_rows, ["content_type", "asset_count", "exclusion_reason"])
    write_csv(
        OUTPUT / "unresolved-references.csv",
        (relation.__dict__ for relation in relations if relation.resolution_status == "unresolved-content-reference"),
        relation_fields,
    )
    write_csv(
        OUTPUT / "protocol-targets.csv",
        (relation.__dict__ for relation in relations if relation.resolution_status == "non-content-protocol"),
        relation_fields,
    )
    write_csv(
        OUTPUT / "runtime-domain-targets.csv",
        (relation.__dict__ for relation in relations if relation.resolution_status == "unresolved-runtime-domain-id"),
        relation_fields,
    )
    duplicate_rows = []
    for (class_name, stable_id), count in sorted(duplicate_counter.items()):
        if count <= 1:
            continue
        duplicate_rows.append(
            {
                "content_type": class_name,
                "stable_id": stable_id,
                "asset_count": count,
                "record_keys": "; ".join(row.record_key for row in stable_rows[stable_id] if row.content_type == class_name),
            }
        )
    write_csv(OUTPUT / "duplicate-content.csv", duplicate_rows, ["content_type", "stable_id", "asset_count", "record_keys"])

    grouped_rows: dict[str, list[ContentRow]] = defaultdict(list)
    for row in rows:
        grouped_rows[row.group].append(row)
    type_index = {str(entry["content_type"]): entry for entry in type_index_rows}
    for group in GROUPS:
        write_group_markdown(group, grouped_rows[group], type_index)
    for class_name, type_rows in rows_by_type.items():
        write_type_markdown(CLASS_TO_GROUP[class_name], class_name, type_rows, type_index[class_name])

    type_counts = Counter(row.content_type for row in rows)
    review_counts = Counter(row.review_status for row in rows)
    runtime_counts = Counter(row.runtime_status for row in rows)
    lifecycle_counts = Counter(row.lifecycle_status for row in rows)
    relation_status_counts = Counter(relation.resolution_status for relation in relations)
    typed_id_counts = Counter((row.content_type, row.stable_id) for row in rows)
    duplicate_typed_id_groups = sum(1 for count in typed_id_counts.values() if count > 1)
    summary = {
        "generated_from": "Assets/**/*.asset",
        "row_count": len(rows),
        "relation_count": len(relations),
        "content_csv_count": len(type_index_rows),
        "relation_csv_count": len(type_index_rows),
        "field_csv_count": len(type_index_rows),
        "incoming_csv_count": len(type_index_rows),
        "code_consumer_csv_count": len(type_index_rows),
        "code_consumer_row_count": sum(len(values) for values in code_consumers_by_type.values()),
        "parse_error_count": len(errors),
        "excluded_asset_type_count": len(excluded_rows),
        "duplicate_typed_id_groups": duplicate_typed_id_groups,
        "group_counts": {group: len(grouped_rows[group]) for group in GROUPS},
        "type_counts": dict(sorted(type_counts.items())),
        "review_counts": dict(sorted(review_counts.items())),
        "runtime_counts": dict(sorted(runtime_counts.items())),
        "lifecycle_counts": dict(sorted(lifecycle_counts.items())),
        "relation_status_counts": dict(sorted(relation_status_counts.items())),
    }
    (OUTPUT / "content-db-summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    schema_lines = [
        "# 콘텐츠 데이터베이스 스키마",
        "",
        "각 콘텐츠 유형은 요약 행 CSV와 모든 중첩 직렬화 값을 보존하는 작성 필드 CSV를 함께 가진다. 숫자 enum은 원시값을 유지하면서 C# 선언에서 복원한 이름을 별도 열에 기록한다.",
        "",
        "| 계층 | 위치 | 역할 |",
        "|---|---|---|",
        "| 유형 요약 | `csv/<영역>/<유형>.csv` | 안정 ID, 설계 역할, 런타임 근거, 유형 고유 최상위 필드 |",
        "| 작성 필드 | `fields/<영역>/<유형>.csv` | 배열과 중첩 객체를 포함한 전체 직렬화 필드 |",
        "| 정방향 관계 | `relations/<영역>/<유형>.csv` | 요구·생산·해금·효과·참조와 대상 해석 |",
        "| 역방향 관계 | `incoming/<영역>/<유형>.csv` | 해당 콘텐츠를 참조하는 출발 콘텐츠 |",
        "| 코드 소비처 | `code-consumers/<영역>/<유형>.csv` | 유형 단위 소비와 안정 ID 직접 조회를 구분한 C# 근거 |",
        "| 유형 문서 | `types/<영역>/<유형>/` | 전략적 역할과 런타임 상태를 읽기 좋은 크기로 분할 |",
        "",
        "`runtime_status`는 정적 도달 근거의 강도를 나타내며 실제 플레이 실행을 보증하지 않는다. `alternative_candidates`도 비교 시작점이며 대체 가능성의 확정 판정이 아니다.",
    ]
    (OUTPUT / "schema.md").write_text("\n".join(schema_lines) + "\n", encoding="utf-8")

    enum_lines = ["# 직렬화 enum 사전", "", "원시 숫자의 해석 권위는 현재 C# enum 선언이다.", ""]
    for enum_name in sorted(used_enum_types):
        definition = enums.get(enum_name)
        if not definition:
            continue
        enum_lines.extend([f"## {enum_name}", "", "| 값 | 이름 | 문서 표기 |", "|---:|---|---|"])
        translations = ENUM_KOREAN_LABELS.get(enum_name, {})
        for numeric, name in sorted(definition.values.items()):
            label = f"{name} ({translations[name]})" if name in translations else humanize_identifier(name)
            enum_lines.append(f"| {numeric} | `{name}` | {escape_markdown(label)} |")
        enum_lines.append("")
    (OUTPUT / "enum-dictionary.md").write_text("\n".join(enum_lines) + "\n", encoding="utf-8")

    runtime_lines = [
        "# 런타임 도달성 감사",
        "",
        "정적 코드 소비, Resources 로더, 카탈로그 등록, 안정 ID 리터럴을 교차해 작성 자산의 도달 근거를 분류한다. 실제 플레이 경로와 저장 왕복은 별도의 실행 검증 대상이다.",
        "",
        "| 상태 | 항목 | 의미 |",
        "|---|---:|---|",
    ]
    runtime_meanings = {
        "catalog-registered-static-consumer": "카탈로그 등록과 비-Editor 코드 소비가 함께 확인됨",
        "resources-loader-static-consumer": "Resources 로더가 유형을 직접 읽음",
        "stable-id-literal-consumer": "비-Editor 코드가 안정 ID를 직접 조회함",
        "type-consumer-registration-unverified": "유형 소비 코드는 있으나 이 자산의 등록은 확인되지 않음",
        "resources-authored-consumer-unverified": "Resources 아래 작성 자산이나 구체 소비 경로는 미확인",
        "authored-only-unverified": "작성 자산만 확인됨",
        "deprecated-compatibility": "호환성 보존 자산",
    }
    for status, count in sorted(runtime_counts.items()):
        runtime_lines.append(f"| `{status}` | {count:,} | {runtime_meanings.get(status, '')} |")
    runtime_lines.extend(["", "세부 근거는 각 유형 CSV의 `catalog_memberships`, `runtime_evidence`, `save_evidence` 열에 기록한다."])
    (OUTPUT / "runtime-coverage.md").write_text("\n".join(runtime_lines) + "\n", encoding="utf-8")

    relation_lines = [
        "# 콘텐츠 관계 감사",
        "",
        "효과의 `kind`를 먼저 해석해 아이템·세력·질병 같은 콘텐츠 대상과 기분·위협·월드 플래그 같은 상태 채널을 분리한다.",
        "",
        "| 해석 상태 | 관계 |",
        "|---|---:|",
    ]
    for status, count in sorted(relation_status_counts.items()):
        relation_lines.append(f"| `{status}` | {count:,} |")
    relation_lines.extend(
        [
            "",
            "- [해결되지 않은 콘텐츠 참조](unresolved-references.csv)",
            "- [콘텐츠가 아닌 프로토콜·상태 채널](protocol-targets.csv)",
            "- [별도 런타임 도메인 ID](runtime-domain-targets.csv)",
            "- [중복 안정 ID 감사](duplicate-content.csv)",
        ]
    )
    (OUTPUT / "relationship-audit.md").write_text("\n".join(relation_lines) + "\n", encoding="utf-8")

    unresolved_groups: dict[tuple[str, str], list[Relation]] = defaultdict(list)
    for relation in relations:
        if relation.resolution_status == "unresolved-content-reference":
            unresolved_groups[(relation.source_type, relation.field_path)].append(relation)
    gap_lines = [
        "# 콘텐츠 참조 결함 후보",
        "",
        "현재 작성 자산의 ID를 콘텐츠 DB에서 해소하지 못한 참조다. 계획된 선행 콘텐츠, 구형 ID, 오기, effect kind/target 불일치가 섞일 수 있으므로 자동 삭제하거나 임의 치환하지 않는다.",
        "",
        "| 출발 유형 | 필드 | 건수 | 대상 예시 | 판정 |",
        "|---|---|---:|---|---|",
    ]
    for (source_type, field_path), gap_relations in sorted(
        unresolved_groups.items(), key=lambda item: (-len(item[1]), item[0])
    ):
        examples = ", ".join(sorted({relation.target_id for relation in gap_relations})[:6])
        if "buildingDefinitionId" in field_path or field_path == "requiredBuildingDefinitionId":
            assessment = "요구 시설 ID가 현행 BuildingSO 안정 ID와 일치하지 않음"
        elif "Item" in field_path or "item" in field_path:
            assessment = "문화·사건 요구 아이템이 현행 아이템 카탈로그에 없음"
        elif "effects" in field_path.lower():
            assessment = "효과 종류가 요구하는 대상 유형과 targetId가 일치하지 않음"
        else:
            assessment = "대상 콘텐츠 미해소"
        gap_lines.append(
            f"| `{source_type}` | `{field_path}` | {len(gap_relations):,} | "
            f"{escape_markdown(examples)} | {assessment} |"
        )
    gap_lines.extend(["", "전체 행과 출발 자산 경로는 [unresolved-references.csv](unresolved-references.csv)에 보존한다."])
    (OUTPUT / "reference-gaps.md").write_text("\n".join(gap_lines) + "\n", encoding="utf-8")

    overlap_lines = [
        "# 콘텐츠 역할 중첩 감사",
        "",
        "비교군은 범주·기능 모듈·생산 시설·연구 분야·이벤트 유형을 기준으로 묶는다. 동일 비교군은 역할 중복 가능성을 가리킬 뿐 실제 상호 대체를 뜻하지 않는다.",
        "",
        "| 비교군 | 항목 |",
        "|---|---:|",
    ]
    for group_name, members in sorted(comparison_members.items(), key=lambda item: (-len(item[1]), item[0])):
        if len(members) > 1:
            overlap_lines.append(f"| `{escape_markdown(group_name)}` | {len(members):,} |")
    overlap_lines.extend(["", "세부 후보와 제거 영향은 유형별 CSV의 `alternative_candidates`, `removal_impact`에 기록한다."])
    (OUTPUT / "content-overlap-audit.md").write_text("\n".join(overlap_lines) + "\n", encoding="utf-8")

    readme_lines = [
        "# DungeonStory 콘텐츠 데이터베이스",
        "",
        "현재 Unity 작성 자산과 C# 직렬화 권위를 정적으로 교차해 생성한 콘텐츠 색인이다. 유형별 DB는 작성 필드, 관계, 런타임 도달 근거, 전략적 역할과 제거 영향을 분리해 제공한다.",
        "",
        "행 기본키는 `record_key`다. 일반적으로 `콘텐츠 유형|안정 ID`이며, 같은 유형과 ID를 공유하는 자산이 실제로 공존하면 소스 경로까지 붙여 구분한다.",
        "",
        "## 데이터베이스 구성",
        "",
        "- [아이템](01-items.md)",
        "- [생산·시설](02-production-and-facilities.md)",
        "- [인물·특성·사회](03-characters-traits-and-society.md)",
        "- [사건·캠페인](04-events-and-campaign.md)",
        "- [연구·효과·진행](05-research-effects-and-progression.md)",
        "- [전투·건강·세계](06-combat-health-and-world.md)",
        "- [콘텐츠 유형별 CSV 색인](content-type-index.csv)",
        "- [스키마](schema.md)",
        "- [직렬화 enum 사전](enum-dictionary.md)",
        "- [런타임 도달성 감사](runtime-coverage.md)",
        "- [관계 감사](relationship-audit.md)",
        "- [콘텐츠 참조 결함 후보](reference-gaps.md)",
        "- [역할 중첩 감사](content-overlap-audit.md)",
        "- 유형별 콘텐츠 CSV: `csv/<영역>/<유형>.csv`",
        "- 유형별 전체 작성 필드 CSV: `fields/<영역>/<유형>.csv`",
        "- 유형별 관계 CSV: `relations/<영역>/<유형>.csv`",
        "- 유형별 역참조 CSV: `incoming/<영역>/<유형>.csv`",
        "- 유형별 코드 소비처 CSV: `code-consumers/<영역>/<유형>.csv`",
        "- [수동 검토 목록](manual-review.csv)",
        "- [제외 자산 유형 감사](excluded-asset-types.csv)",
        "- [별도 런타임 도메인 ID](runtime-domain-targets.csv)",
        "- [파싱 오류](parse-errors.csv)",
        "- [생성 요약 JSON](content-db-summary.json)",
        "- [원본 파일 manifest](source-files.csv)",
        "- [생성물 manifest](output-files.csv)",
        "- [생성 상태](generation-manifest.json)",
        "",
        "## 현재 스냅샷",
        "",
        f"- 전체 콘텐츠 행: {len(rows):,}",
        f"- 관계 행: {len(relations):,}",
        f"- 콘텐츠 유형별 CSV: {len(type_index_rows):,}개",
        f"- 관계 유형별 CSV: {len(type_index_rows):,}개",
        f"- 코드 소비처 근거: {sum(len(values) for values in code_consumers_by_type.values()):,}건",
        f"- 런타임 정적 근거 확인: {sum(runtime_confirmed(row.runtime_status) for row in rows):,}",
        f"- 근거 확인: {review_counts['근거 확인']:,}",
        f"- 수동 검토 필요: {review_counts['수동 검토 필요']:,}",
        f"- 동일 유형·안정 ID 중복 그룹: {duplicate_typed_id_groups:,}",
        f"- 파싱 오류: {len(errors):,}",
        "",
        "| 영역 | 행 수 |",
        "|---|---:|",
    ]
    for group in GROUPS:
        readme_lines.append(f"| {GROUP_LABEL[group]} | {len(grouped_rows[group]):,} |")
    readme_lines.extend(
        [
        "",
        "## 유형별 CSV",
        "",
        "| 영역 | 콘텐츠 유형 | 콘텐츠 | 문서 | 작성 필드 | 관계 | 역참조 | 코드 소비처 |",
        "|---|---|---:|---|---|---|---|---|",
        *(
            f"| {GROUP_LABEL[row['group']]} | `{row['content_type']}` | {row['content_count']:,} | "
            f"[열기]({row['type_doc']}) | [CSV]({row['field_csv']}) | [CSV]({row['relation_csv']}) | "
            f"[CSV]({row['incoming_csv']}) | [CSV]({row['code_consumer_csv']}) |"
            for row in type_index_rows
        ),
        "",
        "## 존재 이유 판정",
            "",
        "존재 이유는 작성 description만 반복하지 않는다. 생산 입출력, 아이템 feature, 연구 선행·해금, 시설 ability, 사건 requirement·choice·effect, 다른 콘텐츠의 참조를 함께 사용한다. 근거가 부족하거나 안정 ID가 합성된 행은 `수동 검토 필요`로 분리한다.",
        "",
        "같은 유형과 안정 ID의 자산이 여러 경로에 공존하는 경우에도 어느 한쪽을 임의로 제거하지 않는다. 각 자산은 `record_key`로 분리하고 `manual-review.csv`에서 이관·호환 여부를 판단한다.",
        "",
            "## 재생성",
            "",
            "```powershell",
            f"python -X utf8 Tools/Documentation/generate_content_database.py --output-root {relative(OUTPUT)}",
            f"& Tools/Documentation/validate_content_database.ps1 -DatabaseRoot {relative(OUTPUT)}",
            f"python -X utf8 Tools/Documentation/verify_knowledge_base.py {relative(OUTPUT)}",
            "```",
            "",
            f"세 명령은 Unity를 실행하지 않는다. 생성기는 Unity 작성 자산과 C# 직렬화 권위를 읽어 `{relative(OUTPUT)}/`를 갱신한다. 검증기는 스키마·관계·역참조·enum·링크를 검사하고, 마지막 명령은 원본 변경과 생성물 변조를 검출한다.",
        ]
    )
    (OUTPUT / "README.md").write_text("\n".join(readme_lines) + "\n", encoding="utf-8")

    manifest = write_generation_manifest(
        project_root=ROOT,
        output_root=OUTPUT,
        generator_path=Path(__file__),
        source_specs=[
            {"name": "unity-authored-assets", "root": "Assets", "patterns": ["**/*.asset"]},
            {"name": "unity-guid-metadata", "root": "Assets", "patterns": ["**/*.meta"]},
            {"name": "csharp-authority", "root": "Assets/Scripts", "patterns": ["**/*.cs"]},
            {
                "name": "content-db-generator",
                "root": "Tools/Documentation",
                "patterns": ["generate_content_database.py", "knowledge_manifest.py"],
            },
        ],
        schema_version=1,
        artifact_kind="dungeonstory-content-database",
        statistics=summary,
    )
    summary["source_digest"] = manifest["source_digest"]
    summary["output_digest"] = manifest["output_digest"]

    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if not errors else 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
