#!/usr/bin/env python3
"""Verify a Unity V25 mechanic export against the real NarrativeAI consumer.

This deliberately validates the cross-workspace boundary instead of accepting a
Unity-only self-check as evidence.  It is read-only unless ``--report`` is given.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import subprocess
import sys
from collections import Counter, defaultdict
from pathlib import Path
from types import ModuleType
from typing import Any, Iterable


EXPECTED_PROFILES = {
    "CharacterSkill",
    "FacilityEvolution",
    "EquipmentChoice",
    "EvolutionHistory",
    "AcquiredTrait",
    "Persona",
}

EXPECTED_SCENARIO_COUNTS = {
    "CharacterSkill": 20,
    "FacilityEvolution": 15,
    "EquipmentChoice": 15,
    "EvolutionHistory": 15,
    "AcquiredTrait": 20,
    "Persona": 15,
}

STALE_COMBINATION_NEGATIVE_SCENARIO_ID = (
    "character-skill-negative-passive-need-changed-v17-cleaning-stock-small-stale-combination-id"
)
EXPECTED_NEGATIVE_SCENARIO_IDS = {
    "acquired-trait-negative-active-module",
    "acquired-trait-negative-budget",
    "acquired-trait-negative-combination-identity",
    "acquired-trait-negative-conflict",
    "acquired-trait-negative-consumed-milestone-after-erasure",
    "acquired-trait-negative-duplicate-evidence",
    "acquired-trait-negative-evidence-forgery",
    "character-skill-negative-duplicate-rule",
    "character-skill-negative-forged-combination",
    STALE_COMBINATION_NEGATIVE_SCENARIO_ID,
    "character-skill-negative-wrong-count",
    "equipment-choice-reject-out-of-range-index",
    "evolution-history-reject-changed-effect-budget",
    "facility-evolution-reject-illegal-proposal-id",
    "persona-reject-extra-mechanical-key",
}
EXPECTED_NEGATIVE_SCENARIO_COUNT = len(EXPECTED_NEGATIVE_SCENARIO_IDS)
CONSUMED_MILESTONE_NEGATIVE_SCENARIO_ID = (
    "acquired-trait-negative-consumed-milestone-after-erasure"
)

REQUIRED_DELIVERY_FILES = {
    "catalog.json",
    "continuity_scenarios.json",
    "delivery_manifest.json",
    "manifest.json",
    "negative_scenarios.json",
    "packet_parity_cases.json",
    "policy.json",
    "README.md",
    "scenario_schema.json",
    "scenarios_100.json",
    "test_evidence.json",
}

EXPECTED_RESPONSE_KEYS = {
    "CharacterSkill": {
        "candidates",
        "candidates[].ruleId",
        "candidates[].combinationId",
        "candidates[].displayName",
        "candidates[].description",
        "candidates[].narrativeReason",
    },
    "FacilityEvolution": {
        "proposalIds",
        "mutationTags",
        "reasons",
        "reasons[].proposalId",
        "reasons[].reason",
        "flavorText",
        "confidence",
    },
    "EquipmentChoice": {"selectedIndex"},
    "EvolutionHistory": {
        "requestKey",
        "targetPersistentId",
        "nodeId",
        "parentNodeId",
        "effectId",
        "effectBudget",
        "evidenceIds",
        "displayName",
        "description",
        "historyReason",
    },
    "AcquiredTrait": {
        "combinationId",
        "displayName",
        "description",
        "narrativeReason",
        "evidenceFactIds",
    },
    "Persona": {"personaName", "flavorText"},
}

REQUIRED_EVIDENCE_GATES = {
    "canonical-json-python-parity",
    "catalog-byte-determinism",
    "packet-parity",
    "v25-focused-unity-regression",
}

CHARACTER_SKILL_REQUEST_KEYS = {
    "candidateCount",
    "kind",
    "requestKey",
    "rules",
}

CHARACTER_SKILL_RULE_KEYS = {
    "allowedModuleIds",
    "allowedVariantIds",
    "budget",
    "combinationOptions",
    "cooldownTurns",
    "rarity",
    "ruleId",
    "target",
    "targetPositions",
    "trigger",
    "ultimateDomain",
    "usableFrom",
}

CHARACTER_SKILL_COMBINATION_KEYS = {
    "combinationId",
    "signature",
}

CHARACTER_SKILL_FULL_COMBINATION_KEYS = {
    "combinationId",
    "cost",
    "mechanicalIdentity",
    "modules",
    "ruleId",
    "signature",
}

CHARACTER_SKILL_MODULE_KEYS = {
    "cost",
    "count",
    "duration",
    "moduleClass",
    "moduleDisplayName",
    "moduleId",
    "primaryValueDecimal",
    "secondaryValueDecimal",
    "variantDisplayName",
    "variantId",
}

ACQUIRED_TRAIT_REQUEST_KEYS = {
    "budget",
    "candidatePacketHash",
    "combinationOptions",
    "eligibleDomains",
    "evidenceFactIds",
    "manifestationMilestone",
    "maximumActiveTraits",
    "modules",
    "rarity",
    "requestId",
    "requestKey",
    "settingsId",
    "targetPersistentId",
}

ACQUIRED_TRAIT_CANDIDATE_KEYS = {
    "combinationId",
    "conflictGroups",
    "domainAffinities",
    "moduleIds",
    "totalCost",
}

ACQUIRED_TRAIT_MODULE_KEYS = {
    "conflictGroups",
    "cost",
    "description",
    "displayName",
    "domainAffinities",
    "moduleId",
}

FACILITY_REQUEST_KEYS = {"candidateIds", "prompt", "requestSignature", "responseJson"}
FACILITY_CANDIDATE_KEYS = {
    "allowedMutationTags",
    "candidateIndex",
    "displayName",
    "recipeId",
    "requiredStarGrade",
}

EQUIPMENT_CHOICE_REQUEST_KEYS = {"candidateCount", "grammar", "requestSnapshot", "responseJson"}
EVOLUTION_HISTORY_REQUEST_KEYS = {"lockedRequest", "responseJson"}
HISTORY_CANDIDATE_KEYS = {"candidateIndex", "description", "displayName", "effectId"}
HISTORY_LOCKED_REQUEST_KEYS = {
    "effectBudget",
    "effectId",
    "evidenceIds",
    "generation",
    "historyHash",
    "legalCandidateEffectIds",
    "nodeId",
    "parentNodeId",
    "participantIds",
    "requestKey",
    "sourceTags",
    "targetKind",
    "targetPersistentId",
}

PERSONA_REQUEST_KEYS = {"prompt", "responseContract", "responseJson"}
PERSONA_CANDIDATE_KEYS = {"candidateId", "candidateIndex", "responseKeys"}

SCENARIO_REQUIRED_FIELDS = {
    "accepted",
    "authorityContext",
    "catalogHash",
    "diversityAxes",
    "effectDescriptions",
    "failureReason",
    "fixtureInput",
    "fullLegalCandidates",
    "inputDigest",
    "producerIdentifier",
    "profileId",
    "publicFacts",
    "publicNarrativeContext",
    "request",
    "scenarioId",
    "schemaVersion",
    "semanticHash",
    "targetPersistentId",
    "validatorIdentifier",
}

PUBLIC_CONTEXT_FIELDS = {
    "schemaVersion",
    "profileId",
    "subjectId",
    "subjectKind",
    "entities",
    "backgroundFactIds",
    "relationshipFactIds",
    "memoryFactIds",
    "priorHistoryFactIds",
    "events",
    "selection",
}
PUBLIC_FACT_REQUIRED_FIELDS = {
    "domain",
    "factId",
    "text",
}
PUBLIC_FACT_OPTIONAL_FIELDS = {
    "subjectId",
    "outcome",
    "lastDay",
    "count",
    "milestoneCount",
    "totalValueDecimal",
}
PUBLIC_EVENT_FIELDS = {
    "factId",
    "evidenceId",
    "actorId",
    "targetId",
    "eventType",
    "outcome",
    "day",
    "count",
    "generation",
}

PROVENANCE_TOKENS = (
    "CharacterAcquiredTrait",
    "MemoryErasureSeal",
)

# These are the semantically critical boundaries behind the V25 vertical slice.
# A textual reference scan alone cannot discover generic effect, custody, save,
# and UI consumers, so the handoff contract names those boundaries explicitly.
EXPLICIT_PROVENANCE_PATHS = {
    "tools/Documentation/verify_v25_narrative_mechanic_handoff.py",
    "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs",
    "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs",
    "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitEffectSource.cs",
    "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitManifestationRuntime.cs",
    "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitInferenceContracts.cs",
    "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitInferenceService.cs",
    "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitState.cs",
    "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitValidation.cs",
    "Assets/Scripts/Services/Character/Core/CharacterProgression.cs",
    "Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs",
    "Assets/Scripts/Services/Character/Core/CharacterSkillModels.cs",
    "Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs",
    "Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs",
    "Assets/Scripts/Services/Character/AI/Editor/EditorCharacterSkillGenerationService.cs",
    "Assets/Scripts/Services/Character/Core/MemoryErasureSealContracts.cs",
    "Assets/Scripts/Services/Character/Core/MemoryErasureSealCommandService.cs",
    "Assets/Scripts/Services/Character/Core/MemoryErasureSealTransactionService.cs",
    "Assets/Scripts/Services/Offense/MemoryErasureSealBossAwardService.cs",
    "Assets/Scripts/Services/Offense/OffenseExpeditionBattleCompletionHandler.cs",
    "Assets/Scripts/Services/Offense/OffenseRegionRuntime.cs",
    "Assets/Scripts/Services/Offense/OffenseAggregateSaveValidation.cs",
    "Assets/Scripts/Services/Items/WorldItemStackRuntime.cs",
    "Assets/Scripts/Services/Items/ItemTransferService.cs",
    "Assets/Scripts/Services/Items/PhysicalItemBatchDispositionService.cs",
    "Assets/Scripts/Services/Items/WorldItemWarehouseService.cs",
    "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
    "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveValidation.cs",
    "Assets/Scripts/Services/Infrastructure/Registration/DungeonCharacterRegistration.cs",
    "Assets/Scripts/Services/Infrastructure/Registration/DungeonProgressionOffenseRegistration.cs",
    "Assets/Scripts/Services/Infrastructure/Registration/DungeonPresentationRegistration.cs",
    "Assets/Scripts/Services/Infrastructure/DungeonRuntimeLifetimeScope.cs",
    "Assets/Scripts/Views/UI/MemoryErasureSealUseModal.cs",
    "Assets/Scripts/Views/UI/CharacterSummaryGrowthPresenter.cs",
    "Assets/Scripts/Views/UI/CharacterSummaryInfo.cs",
    "Assets/Scripts/Services/Items/ItemPileInfoPanel.cs",
    "Assets/Scripts/Services/Effects/Runtime/CharacterDerivedStatsSnapshot.cs",
    "Assets/Scripts/Services/Effects/Runtime/CharacterGameplayEffectProjector.cs",
    "Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs",
    "Assets/Scripts/Services/Character/Core/CharacterPerformanceQuery.cs",
    "Assets/Scripts/Services/Character/Ability/AbilityWork.cs",
    "Assets/Scripts/Services/Character/Core/CharacterActor.cs",
    "Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs",
    "Assets/Scripts/Services/Character/Core/DungeonCharacterSaveData.cs",
    "Assets/Scripts/Services/Infrastructure/Save/WorldAndCharacterSaveSections.cs",
    "Assets/Scripts/Services/Infrastructure/Save/DungeonAggregateReferencePreflight.cs",
    "Assets/Scripts/Services/Infrastructure/CharacterV18RestoreIdentityResolver.cs",
    "Assets/Scripts/Services/Items/CharacterCarryInventory.cs",
    "Assets/Scripts/Services/Items/WorldItemModels.cs",
    "Assets/Scripts/Models/Items/Core/ItemPrimitives.cs",
    "Assets/Scripts/Services/Items/PhysicalItemsSaveSection.cs",
    "Assets/Scripts/Services/Items/WorldItemPersistenceService.cs",
    "Assets/Scripts/Services/Items/PhysicalItemSaveValidation.cs",
    "Assets/Scripts/Services/Items/WarehousePhysicalRestoreValidation.cs",
    "Assets/Scripts/Services/Offense/OffenseSaveSections.cs",
    "Assets/Scripts/Services/Infrastructure/OffenseSaveService.cs",
    "Assets/Scripts/Models/AI/Core/LlmStaticSchemaCatalog.cs",
    "Assets/Scripts/Models/AI/Core/NarrativePublicContext.cs",
    "Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs",
    "Assets/Scripts/Models/AI/Core/V25NarrativeInferenceContracts.cs",
    "Assets/Scripts/Services/Character/AI/NarrativeRequestContext.cs",
    "Assets/Scripts/Services/Character/AI/LlmJsonResponseParser.cs",
    "Assets/Scripts/Services/Character/AI/CustomerPersonaRuntime.cs",
    "Assets/Scripts/Services/Combat/EquipmentEvolutionRules.cs",
    "Assets/Scripts/Services/Evolution/EvolutionHistoryNarrativeRuntime.cs",
    "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionLlmProposalProvider.cs",
    "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs",
    "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionService.cs",
    "Assets/Scripts/Models/Evolution/Core/EvolutionHistoryModels.cs",
    "Assets/Scripts/Services/Character/AI/Editor/V25NarrativeInferenceDebugScenarios.cs",
    "Assets/Scripts/Services/Character/AI/Editor/CharacterProgressionDebugScenarios.cs",
    "Assets/Scripts/Services/Evolution/Editor/InstanceEvolutionDebugScenarios.cs",
    "Assets/Scripts/Services/Offense/Editor/OffenseRewardDebugScenarios.cs",
    "Assets/Scripts/Services/Items/Editor/PhysicalItemPilePlayModeVerifier.cs",
}


class VerificationError(RuntimeError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise VerificationError(message)


def load_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    require(isinstance(value, dict), f"{path}: expected a JSON object")
    return value


def sha256_prefixed(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return "sha256:" + digest.hexdigest()


def relative(project_root: Path, path: Path) -> str:
    return path.resolve().relative_to(project_root.resolve()).as_posix()


def with_meta(paths: Iterable[Path]) -> set[Path]:
    result: set[Path] = set()
    for path in paths:
        if not path.is_file():
            continue
        result.add(path)
        meta = Path(str(path) + ".meta")
        if meta.is_file():
            result.add(meta)
    return result


def discover_required_provenance(project_root: Path) -> set[str]:
    scripts_root = project_root / "Assets" / "Scripts"
    direct_references: set[Path] = set()
    for path in sorted(scripts_root.rglob("*.cs")):
        text = path.read_text(encoding="utf-8-sig")
        if any(token in text for token in PROVENANCE_TOKENS):
            direct_references.add(path)

    exporter_root = scripts_root / "Services" / "Character" / "AI" / "Editor"
    exporter_sources = set(exporter_root.glob("NarrativeMechanicCatalog*.cs"))
    exporter_sources.update(exporter_root.glob("NarrativeMechanicScenario*.cs"))

    authored_root = project_root / "Assets" / "Resources" / "SO" / "V25" / "AcquiredTraits"
    authored_assets = set(path for path in authored_root.rglob("*") if path.is_file())

    seal_authorities: set[Path] = set()
    resources_root = project_root / "Assets" / "Resources"
    for path in sorted(resources_root.rglob("*.asset")):
        if "item:memory-erasure-seal" in path.read_text(encoding="utf-8-sig"):
            seal_authorities.add(path)

    explicit_tools = {
        project_root / "Tools" / "Documentation" / "verify_v25_narrative_mechanic_handoff.py"
    }

    explicit_sources = {project_root / Path(path) for path in EXPLICIT_PROVENANCE_PATHS}
    missing_explicit = sorted(
        relative(project_root, path) for path in explicit_sources if not path.is_file()
    )
    require(
        not missing_explicit,
        "explicit provenance source is missing: " + ", ".join(missing_explicit),
    )

    asset_sources = (
        direct_references
        | exporter_sources
        | authored_assets
        | seal_authorities
        | explicit_sources
        | explicit_tools
    )
    missing_meta = sorted(
        relative(project_root, path)
        for path in asset_sources
        if relative(project_root, path).startswith("Assets/")
        and not path.name.endswith(".meta")
        and not Path(str(path) + ".meta").is_file()
    )
    require(not missing_meta, "required Unity source has no sibling .meta: " + ", ".join(missing_meta))

    required = with_meta(asset_sources)
    return {relative(project_root, path) for path in required}


def validate_manifest(project_root: Path, manifest: dict[str, Any]) -> tuple[int, int]:
    files = manifest.get("files")
    require(isinstance(files, list) and files, "manifest.files must be a non-empty array")
    by_path: dict[str, dict[str, Any]] = {}
    for entry in files:
        require(isinstance(entry, dict), "manifest.files contains a non-object entry")
        path = entry.get("path")
        require(isinstance(path, str) and path, "manifest file path is empty")
        require(path not in by_path, f"manifest contains duplicate path: {path}")
        by_path[path] = entry

        full_path = project_root / Path(path)
        require(full_path.is_file(), f"manifest source is missing: {path}")
        expected_hash = entry.get("sha256")
        actual_hash = sha256_prefixed(full_path)
        require(
            expected_hash == actual_hash,
            f"manifest hash is stale for {path}: expected={expected_hash}, actual={actual_hash}",
        )

    required = discover_required_provenance(project_root)
    missing = sorted(required - set(by_path))
    require(not missing, "manifest dependency closure is incomplete: " + ", ".join(missing))

    commit = subprocess.run(
        ["git", "rev-parse", "HEAD"],
        cwd=project_root,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    source = manifest.get("source")
    require(isinstance(source, dict), "manifest.source must be an object")
    require(source.get("gameCommit") == commit, "manifest gameCommit differs from current HEAD")
    return len(files), len(required)


def validate_test_evidence(
    project_root: Path,
    evidence: dict[str, Any],
    manifest: dict[str, Any],
    catalog_hash: str,
    require_training_eligible: bool,
) -> int:
    input_digest = manifest.get("inputDigest")
    require(isinstance(input_digest, str) and input_digest, "manifest.inputDigest is empty")
    require(evidence.get("catalogHash") == catalog_hash, "test evidence catalogHash is stale")
    require(
        evidence.get("exportInputDigest") == input_digest,
        "test evidence export input digest differs from the manifest",
    )
    if evidence.get("status") == "not-supplied" and not require_training_eligible:
        require(
            evidence.get("capturedInputDigest") in (None, ""),
            "unsupplied test evidence unexpectedly has a captured input digest",
        )
        require(evidence.get("evidenceGates") == [], "unsupplied test evidence must have no gates")
        return 0
    require(
        evidence.get("capturedInputDigest") == input_digest,
        "test evidence input digest differs from the manifest",
    )
    gates = evidence.get("evidenceGates")
    require(isinstance(gates, list) and gates, "test evidence gates must be non-empty")
    by_id: dict[str, dict[str, Any]] = {}
    for gate in gates:
        require(isinstance(gate, dict), "test evidence contains a non-object gate")
        gate_id = gate.get("gateId")
        require(isinstance(gate_id, str) and gate_id, "test evidence gateId is empty")
        require(gate_id not in by_id, f"duplicate test evidence gate: {gate_id}")
        by_id[gate_id] = gate
        artifact_path = gate.get("artifactPath")
        require(isinstance(artifact_path, str) and artifact_path, f"{gate_id}: artifactPath is empty")
        artifact = project_root / Path(artifact_path)
        require(artifact.is_file(), f"{gate_id}: evidence artifact is missing: {artifact_path}")
        require(
            gate.get("artifactHash") == sha256_prefixed(artifact),
            f"{gate_id}: evidence artifact hash is stale",
        )
        require(gate.get("inputDigest") == input_digest, f"{gate_id}: input digest is stale")

    missing_gates = sorted(REQUIRED_EVIDENCE_GATES - set(by_id))
    require(not missing_gates, "required evidence gates are missing: " + ", ".join(missing_gates))
    if require_training_eligible:
        failed = sorted(gate_id for gate_id, gate in by_id.items() if gate.get("passed") is not True)
        require(not failed, "training-eligible export has failed evidence gates: " + ", ".join(failed))
    return len(gates)


def validate_character_skill_request(
    request: Any,
    case_id: str,
    require_full_material: bool = False,
) -> None:
    require(isinstance(request, dict), f"{case_id}: CharacterSkill request must be an object")
    missing = sorted(CHARACTER_SKILL_REQUEST_KEYS - set(request))
    require(not missing, f"{case_id}: CharacterSkill request missing {missing}")
    rules = request.get("rules")
    require(isinstance(rules, list) and rules, f"{case_id}: rules must be non-empty")
    require(request.get("candidateCount") == len(rules), f"{case_id}: candidateCount differs from rules")
    for ordinal, rule in enumerate(rules):
        require(isinstance(rule, dict), f"{case_id}: rule {ordinal} must be an object")
        missing_rule = sorted(CHARACTER_SKILL_RULE_KEYS - set(rule))
        require(not missing_rule, f"{case_id}: rule {ordinal} missing {missing_rule}")
        combinations = rule.get("combinationOptions")
        require(
            isinstance(combinations, list) and combinations,
            f"{case_id}: rule {ordinal} combinationOptions must be non-empty",
        )
        for combination_ordinal, combination in enumerate(combinations):
            require(
                isinstance(combination, dict),
                f"{case_id}: rule {ordinal} combination {combination_ordinal} must be an object",
            )
            missing_combination = sorted(CHARACTER_SKILL_COMBINATION_KEYS - set(combination))
            require(
                not missing_combination,
                f"{case_id}: rule {ordinal} combination {combination_ordinal} missing {missing_combination}",
            )
            if not require_full_material:
                continue
            missing_full_combination = sorted(
                CHARACTER_SKILL_FULL_COMBINATION_KEYS - set(combination)
            )
            require(
                not missing_full_combination,
                f"{case_id}: rule {ordinal} combination {combination_ordinal} missing full material {missing_full_combination}",
            )
            require(
                combination.get("ruleId") == rule.get("ruleId"),
                f"{case_id}: rule {ordinal} combination {combination_ordinal} ruleId differs",
            )
            modules = combination.get("modules")
            require(
                isinstance(modules, list) and modules,
                f"{case_id}: rule {ordinal} combination {combination_ordinal} modules must be non-empty",
            )
            allowed_module_ids = rule.get("allowedModuleIds")
            allowed_variant_ids = rule.get("allowedVariantIds")
            require(isinstance(allowed_module_ids, list), f"{case_id}: rule {ordinal} allowedModuleIds differs")
            require(isinstance(allowed_variant_ids, list), f"{case_id}: rule {ordinal} allowedVariantIds differs")
            for module_ordinal, module in enumerate(modules):
                require(
                    isinstance(module, dict),
                    f"{case_id}: rule {ordinal} combination {combination_ordinal} module {module_ordinal} must be an object",
                )
                missing_module = sorted(CHARACTER_SKILL_MODULE_KEYS - set(module))
                require(
                    not missing_module,
                    f"{case_id}: rule {ordinal} combination {combination_ordinal} module {module_ordinal} missing {missing_module}",
                )
                require(
                    module.get("moduleId") in allowed_module_ids,
                    f"{case_id}: rule {ordinal} combination {combination_ordinal} contains an illegal moduleId",
                )
                variant_id = module.get("variantId")
                require(
                    variant_id in (None, "") or variant_id in allowed_variant_ids,
                    f"{case_id}: rule {ordinal} combination {combination_ordinal} contains an illegal variantId",
                )


def require_object_keys(value: Any, required: set[str], label: str) -> dict[str, Any]:
    require(isinstance(value, dict), f"{label} must be an object")
    missing = sorted(required - set(value))
    require(not missing, f"{label} missing {missing}")
    return value


def require_indexed_candidates(
    candidates: Any,
    required_keys: set[str],
    label: str,
) -> list[dict[str, Any]]:
    require(isinstance(candidates, list) and candidates, f"{label} must be non-empty")
    result: list[dict[str, Any]] = []
    for ordinal, candidate in enumerate(candidates):
        candidate = require_object_keys(candidate, required_keys, f"{label}[{ordinal}]")
        require(candidate.get("candidateIndex") == ordinal, f"{label}[{ordinal}] candidateIndex differs")
        result.append(candidate)
    return result


def require_id(value: Any, label: str, allow_empty: bool = False) -> str:
    require(isinstance(value, str), f"{label} must be a string")
    if allow_empty and value == "":
        return value
    require(bool(value) and len(value) <= 512, f"{label} must contain 1..512 characters")
    require(not any(character.isspace() for character in value), f"{label} contains whitespace")
    return value


def projected_public_fact_id(domain: str, original_fact_id: str, source_subject_id: str) -> str:
    def field(name: str, value: str) -> str:
        return f"{name}#{len(value.encode('utf-8'))}:{value};"

    source_tuple = (
        field("domain", domain)
        + field("originalFactId", original_fact_id)
        + field("subjectId", source_subject_id)
    )
    return "public-fact:sha256:" + hashlib.sha256(source_tuple.encode("utf-8")).hexdigest()


def require_projected_public_fact_id(value: Any, label: str) -> str:
    fact_id = require_id(value, label)
    prefix = "public-fact:sha256:"
    digest = fact_id[len(prefix):] if fact_id.startswith(prefix) else ""
    require(
        len(digest) == 64
        and all(character in "0123456789abcdef" for character in digest),
        f"{label} must be a full lowercase SHA-256 public fact ID",
    )
    return fact_id


def validate_public_narrative_context(
    scenario: dict[str, Any],
    label: str,
) -> dict[str, dict[str, Any]]:
    profile_id = scenario.get("profileId")
    target_id = require_id(scenario.get("targetPersistentId"), f"{label}: targetPersistentId")
    context = scenario.get("publicNarrativeContext")
    require(isinstance(context, dict), f"{label}: publicNarrativeContext must be an object")
    require(set(context) == PUBLIC_CONTEXT_FIELDS, f"{label}: publicNarrativeContext fields differ")
    require(context.get("schemaVersion") == 1, f"{label}: public context schemaVersion differs")
    require(context.get("profileId") == profile_id, f"{label}: public context profile differs")
    require(context.get("subjectId") == target_id, f"{label}: public context subject differs")

    expected_subject_kinds = {
        "CharacterSkill": {"character"},
        "AcquiredTrait": {"character"},
        "Persona": {"character"},
        "EquipmentChoice": {"equipment"},
        "EvolutionHistory": {"equipment", "facility"},
        "FacilityEvolution": {"facility"},
    }
    subject_kind = context.get("subjectKind")
    require(
        subject_kind in expected_subject_kinds[profile_id],
        f"{label}: public context subjectKind differs",
    )

    entities = context.get("entities")
    require(isinstance(entities, list) and 1 <= len(entities) <= 64, f"{label}: entities count differs")
    entity_ids: set[str] = set()
    subject_entity: dict[str, Any] | None = None
    for ordinal, entity in enumerate(entities):
        require(isinstance(entity, dict), f"{label}: entity[{ordinal}] must be an object")
        require(set(entity) == {"entityId", "kind", "displayName"}, f"{label}: entity[{ordinal}] fields differ")
        entity_id = require_id(entity.get("entityId"), f"{label}: entity[{ordinal}].entityId")
        require(entity_id not in entity_ids, f"{label}: duplicate entity {entity_id}")
        entity_ids.add(entity_id)
        require(
            entity.get("kind") in {"character", "equipment", "facility", "place", "group"},
            f"{label}: entity[{ordinal}].kind differs",
        )
        display_name = entity.get("displayName")
        require(
            isinstance(display_name, str) and 1 <= len(display_name) <= 120,
            f"{label}: entity[{ordinal}].displayName is invalid",
        )
        if entity_id == target_id:
            subject_entity = entity
    require(subject_entity is not None, f"{label}: subject entity is missing")
    require(subject_entity.get("kind") == subject_kind, f"{label}: subject entity kind differs")

    facts = scenario.get("publicFacts")
    require(isinstance(facts, list) and 1 <= len(facts) <= 128, f"{label}: publicFacts count differs")
    by_fact_id: dict[str, dict[str, Any]] = {}
    allowed_fact_fields = PUBLIC_FACT_REQUIRED_FIELDS | PUBLIC_FACT_OPTIONAL_FIELDS
    for ordinal, fact in enumerate(facts):
        require(isinstance(fact, dict), f"{label}: publicFacts[{ordinal}] must be an object")
        require(PUBLIC_FACT_REQUIRED_FIELDS <= set(fact), f"{label}: publicFacts[{ordinal}] misses required fields")
        require(set(fact) <= allowed_fact_fields, f"{label}: publicFacts[{ordinal}] has unknown fields")
        require_id(fact.get("domain"), f"{label}: publicFacts[{ordinal}].domain")
        fact_id = require_projected_public_fact_id(
            fact.get("factId"), f"{label}: publicFacts[{ordinal}].factId"
        )
        require(fact_id not in by_fact_id, f"{label}: duplicate public fact {fact_id}")
        text = fact.get("text")
        require(
            isinstance(text, str) and bool(text.strip()) and len(text) <= 2000,
            f"{label}: publicFacts[{ordinal}].text is invalid",
        )
        if "subjectId" in fact:
            public_subject_id = require_id(
                fact.get("subjectId"), f"{label}: publicFacts[{ordinal}].subjectId"
            )
            require(public_subject_id in entity_ids, f"{label}: public fact subject is unresolved")
        for key in ("lastDay", "count", "milestoneCount"):
            if key in fact:
                require(
                    type(fact[key]) is int and fact[key] >= 0,
                    f"{label}: publicFacts[{ordinal}].{key} must be nonnegative",
                )
        if "totalValueDecimal" in fact:
            require(
                isinstance(fact["totalValueDecimal"], str) and bool(fact["totalValueDecimal"]),
                f"{label}: publicFacts[{ordinal}].totalValueDecimal is invalid",
            )
        by_fact_id[fact_id] = fact

    for group_name in (
        "backgroundFactIds",
        "relationshipFactIds",
        "memoryFactIds",
        "priorHistoryFactIds",
    ):
        references = context.get(group_name)
        require(isinstance(references, list) and len(references) <= 128, f"{label}: {group_name} differs")
        require(len(references) == len(set(references)), f"{label}: {group_name} has duplicates")
        for reference in references:
            require_id(reference, f"{label}: {group_name} reference")
            require(reference in by_fact_id, f"{label}: {group_name} references an unselected fact")

    events = context.get("events")
    require(isinstance(events, list) and len(events) <= 64, f"{label}: events count differs")
    for ordinal, event in enumerate(events):
        require(isinstance(event, dict), f"{label}: event[{ordinal}] must be an object")
        require(set(event) == PUBLIC_EVENT_FIELDS, f"{label}: event[{ordinal}] fields differ")
        fact_id = require_id(event.get("factId"), f"{label}: event[{ordinal}].factId")
        require(fact_id in by_fact_id, f"{label}: event[{ordinal}] references an unselected fact")
        evidence_id = event.get("evidenceId")
        require(isinstance(evidence_id, str) and len(evidence_id) <= 512, f"{label}: event evidenceId differs")
        for key in ("actorId", "targetId"):
            entity_id = require_id(
                event.get(key), f"{label}: event[{ordinal}].{key}", allow_empty=True
            )
            require(not entity_id or entity_id in entity_ids, f"{label}: event[{ordinal}].{key} is unresolved")
        require_id(event.get("eventType"), f"{label}: event[{ordinal}].eventType")
        outcome = event.get("outcome")
        require(isinstance(outcome, str) and len(outcome) <= 512, f"{label}: event[{ordinal}].outcome differs")
        for key in ("day", "generation"):
            require(
                event.get(key) is None or (type(event[key]) is int and event[key] >= 0),
                f"{label}: event[{ordinal}].{key} differs",
            )
        require(type(event.get("count")) is int and event["count"] >= 1, f"{label}: event count differs")
    if profile_id != "Persona":
        require(bool(events), f"{label}: {profile_id} requires a readable event")

    selection = context.get("selection")
    require(isinstance(selection, dict), f"{label}: selection must be an object")
    require(
        set(selection) == {"policyId", "availableFactCount", "omittedFactCount"},
        f"{label}: selection fields differ",
    )
    require_id(selection.get("policyId"), f"{label}: selection.policyId")
    available = selection.get("availableFactCount")
    omitted = selection.get("omittedFactCount")
    require(type(available) is int and type(omitted) is int, f"{label}: selection counts must be integers")
    require(available == len(facts) + omitted and omitted >= 0, f"{label}: selection counts do not reconcile")
    return by_fact_id


def validate_profile_scenario_material(scenario: dict[str, Any]) -> None:
    scenario_id = scenario["scenarioId"]
    profile_id = scenario["profileId"]
    request = scenario["request"]
    candidates = scenario["fullLegalCandidates"]
    public_facts = validate_public_narrative_context(scenario, scenario_id)

    if profile_id == "CharacterSkill":
        validate_character_skill_request(request, scenario_id, require_full_material=True)
        require(isinstance(candidates, list), f"{scenario_id}: fullLegalCandidates must be an array")
        require(len(candidates) == len(request["rules"]), f"{scenario_id}: candidate rule count differs")
        for ordinal, (candidate, rule) in enumerate(zip(candidates, request["rules"])):
            candidate = require_object_keys(candidate, {"options", "ruleId"}, f"{scenario_id}: candidate[{ordinal}]")
            require(candidate.get("ruleId") == rule.get("ruleId"), f"{scenario_id}: candidate[{ordinal}] ruleId differs")
            require(candidate.get("options") == rule.get("combinationOptions"), f"{scenario_id}: candidate[{ordinal}] options differ")
        kind = request.get("kind")
        require(kind in {"Active", "Passive", "Ultimate"}, f"{scenario_id}: CharacterSkill kind differs")
        for ordinal, rule in enumerate(request["rules"]):
            require(
                isinstance(rule.get("mechanicalPolicySource"), str)
                and bool(rule["mechanicalPolicySource"]),
                f"{scenario_id}: CharacterSkill rule {ordinal} lacks mechanicalPolicySource",
            )
            if kind == "Ultimate":
                require(rule.get("ultimateDomain") not in (None, "", "None"), f"{scenario_id}: Ultimate rule {ordinal} lacks domain")
            else:
                require(rule.get("ultimateDomain") in (None, "", "None"), f"{scenario_id}: non-Ultimate rule {ordinal} has a domain")
        return

    if profile_id == "AcquiredTrait":
        request = require_object_keys(request, ACQUIRED_TRAIT_REQUEST_KEYS, f"{scenario_id}: request")
        require(request.get("rarity") in {"Common", "Advanced", "Rare"}, f"{scenario_id}: rarity differs")
        require(request.get("manifestationMilestone") in {3, 8, 20}, f"{scenario_id}: manifestationMilestone differs")
        modules = request.get("modules")
        require(isinstance(modules, list) and modules, f"{scenario_id}: modules must be non-empty")
        module_ids: set[str] = set()
        for ordinal, module in enumerate(modules):
            module = require_object_keys(module, ACQUIRED_TRAIT_MODULE_KEYS, f"{scenario_id}: module[{ordinal}]")
            module_id = module.get("moduleId")
            require(isinstance(module_id, str) and module_id, f"{scenario_id}: module[{ordinal}] moduleId is empty")
            require(module_id not in module_ids, f"{scenario_id}: duplicate moduleId {module_id}")
            module_ids.add(module_id)
        require(isinstance(candidates, list) and candidates, f"{scenario_id}: candidates must be non-empty")
        for ordinal, candidate in enumerate(candidates):
            candidate = require_object_keys(candidate, ACQUIRED_TRAIT_CANDIDATE_KEYS, f"{scenario_id}: candidate[{ordinal}]")
            selected_modules = candidate.get("moduleIds")
            require(isinstance(selected_modules, list) and selected_modules, f"{scenario_id}: candidate[{ordinal}] moduleIds is empty")
            require(set(selected_modules) <= module_ids, f"{scenario_id}: candidate[{ordinal}] contains an unknown module")
        require(candidates == request.get("combinationOptions"), f"{scenario_id}: candidate packet differs from request")
        request_evidence = request.get("evidenceFactIds")
        require(
            isinstance(request_evidence, list)
            and bool(request_evidence)
            and request_evidence == sorted(set(request_evidence)),
            f"{scenario_id}: request evidence must be non-empty, unique, and ordinal-sorted",
        )
        readable_event_ids = {
            event["factId"] for event in scenario["publicNarrativeContext"]["events"]
        }
        require(
            set(request_evidence) <= readable_event_ids,
            f"{scenario_id}: requested evidence is not a readable public event subset",
        )
        return

    if profile_id == "FacilityEvolution":
        request = require_object_keys(request, FACILITY_REQUEST_KEYS, f"{scenario_id}: request")
        candidates = require_indexed_candidates(candidates, FACILITY_CANDIDATE_KEYS, f"{scenario_id}: candidates")
        candidate_ids = [candidate["recipeId"] for candidate in candidates]
        require(candidate_ids == request.get("candidateIds"), f"{scenario_id}: candidateIds differ from full candidates")
        require(candidate_ids == scenario["authorityContext"].get("candidateOrder"), f"{scenario_id}: authority candidate order differs")
        return

    if profile_id in {"EquipmentChoice", "EvolutionHistory"}:
        if profile_id == "EquipmentChoice":
            request = require_object_keys(request, EQUIPMENT_CHOICE_REQUEST_KEYS, f"{scenario_id}: request")
            locked = require_object_keys(request.get("requestSnapshot"), HISTORY_LOCKED_REQUEST_KEYS, f"{scenario_id}: requestSnapshot")
            require(request.get("candidateCount") == len(candidates), f"{scenario_id}: candidateCount differs")
        else:
            request = require_object_keys(request, EVOLUTION_HISTORY_REQUEST_KEYS, f"{scenario_id}: request")
            locked = require_object_keys(request.get("lockedRequest"), HISTORY_LOCKED_REQUEST_KEYS, f"{scenario_id}: lockedRequest")
        candidates = require_indexed_candidates(candidates, HISTORY_CANDIDATE_KEYS, f"{scenario_id}: candidates")
        effect_ids = [candidate["effectId"] for candidate in candidates]
        require(effect_ids == locked.get("legalCandidateEffectIds"), f"{scenario_id}: legal candidate order differs")
        require(effect_ids == scenario["authorityContext"].get("candidateOrder", effect_ids), f"{scenario_id}: authority candidate order differs")
        evidence_ids = locked.get("evidenceIds")
        event_evidence_ids = {
            event.get("evidenceId")
            for event in scenario["publicNarrativeContext"].get("events", [])
        }
        require(
            isinstance(evidence_ids, list)
            and bool(evidence_ids)
            and len(evidence_ids) == len(set(evidence_ids))
            and set(evidence_ids) <= event_evidence_ids,
            f"{scenario_id}: locked evidence has no exact readable public event projection",
        )
        if profile_id == "EvolutionHistory":
            require(
                bool(scenario["publicNarrativeContext"].get("priorHistoryFactIds")),
                f"{scenario_id}: evolution history omitted prior-generation continuity",
            )
        return

    if profile_id == "Persona":
        request = require_object_keys(request, PERSONA_REQUEST_KEYS, f"{scenario_id}: request")
        candidates = require_indexed_candidates(candidates, PERSONA_CANDIDATE_KEYS, f"{scenario_id}: candidates")
        require(len(candidates) == 1, f"{scenario_id}: Persona requires one response schema candidate")
        require(request.get("responseContract") == ["personaName", "flavorText"], f"{scenario_id}: responseContract differs")
        require(candidates[0].get("responseKeys") == request.get("responseContract"), f"{scenario_id}: response keys differ")
        required_identity_labels = {"Name", "Role", "Species"}
        observed_identity_labels = {
            fact["text"].split(":", 1)[0]
            for fact in public_facts.values()
            if fact.get("domain") == "Identity"
            and isinstance(fact.get("text"), str)
            and ":" in fact["text"]
            and bool(fact["text"].split(":", 1)[1].strip())
        }
        require(
            required_identity_labels <= observed_identity_labels,
            f"{scenario_id}: Persona public facts are incomplete",
        )
        require(
            bool(scenario["publicNarrativeContext"].get("backgroundFactIds")),
            f"{scenario_id}: Persona omitted its available public background",
        )
        return

    raise VerificationError(f"{scenario_id}: no profile material validator for {profile_id}")


def validate_parity(packet: dict[str, Any]) -> dict[str, int]:
    blockers = packet.get("blockers")
    require(isinstance(blockers, list), "packet parity blockers must be an array")
    require(not blockers, "packet parity remains blocked: " + json.dumps(blockers, ensure_ascii=False))
    cases = packet.get("cases")
    require(isinstance(cases, list) and cases, "packet parity cases must be non-empty")

    by_profile: dict[str, list[dict[str, Any]]] = defaultdict(list)
    case_ids: set[str] = set()
    for case in cases:
        require(isinstance(case, dict), "packet parity contains a non-object case")
        case_id = case.get("caseId")
        profile_id = case.get("profileId")
        require(isinstance(case_id, str) and case_id, "packet parity caseId is empty")
        require(case_id not in case_ids, f"duplicate packet parity caseId: {case_id}")
        case_ids.add(case_id)
        require(profile_id in EXPECTED_PROFILES, f"{case_id}: unknown or missing profileId={profile_id!r}")
        expected = case.get("expected")
        require(isinstance(expected, dict), f"{case_id}: expected must be an object")
        require(isinstance(expected.get("accepted"), bool), f"{case_id}: expected.accepted must be boolean")
        require(
            isinstance(expected.get("responseKeys"), list) and expected["responseKeys"],
            f"{case_id}: expected.responseKeys must be non-empty",
        )
        expected_response_keys = set(expected["responseKeys"])
        require(
            expected_response_keys == EXPECTED_RESPONSE_KEYS[profile_id],
            f"{case_id}: response contract keys differ for {profile_id}: "
            f"expected={sorted(EXPECTED_RESPONSE_KEYS[profile_id])}, "
            f"actual={sorted(expected_response_keys)}",
        )
        require(isinstance(case.get("request"), dict), f"{case_id}: request must be an object")
        require(isinstance(case.get("responseJson"), str), f"{case_id}: responseJson must be a string")
        if profile_id == "CharacterSkill":
            validate_character_skill_request(case["request"], case_id)
        by_profile[profile_id].append(case)

    missing_profiles = sorted(EXPECTED_PROFILES - set(by_profile))
    require(not missing_profiles, f"packet parity profiles are missing: {missing_profiles}")
    for profile in sorted(EXPECTED_PROFILES):
        outcomes = {case["expected"]["accepted"] for case in by_profile[profile]}
        require(outcomes == {False, True}, f"{profile}: parity requires accepted and rejected cases")
    return dict(sorted(Counter(case["profileId"] for case in cases).items()))


def validate_continuity_document(
    document: dict[str, Any],
    catalog_hash: str,
    input_digest: str,
    scenarios_by_id: dict[str, dict[str, Any]],
) -> int:
    require(
        set(document) == {"catalogHash", "inputDigest", "schemaVersion", "witnessCount", "witnesses"},
        "continuity document fields differ",
    )
    require(document.get("catalogHash") == catalog_hash, "continuity catalogHash is stale")
    require(document.get("inputDigest") == input_digest, "continuity inputDigest is stale")
    require(document.get("schemaVersion") == 1, "continuity schemaVersion differs")
    witnesses = document.get("witnesses")
    require(isinstance(witnesses, list), "continuity witnesses must be an array")
    require(document.get("witnessCount") == len(witnesses) == 6, "continuity witness count must be 6")
    expected_kinds = {
        "source-tuple-collision",
        "long-id",
        "different-life",
        "core-event",
        "prior-generation",
        "negative-reference",
    }
    observed_kinds: set[str] = set()
    witness_ids: set[str] = set()
    for witness in witnesses:
        require(isinstance(witness, dict), "continuity witness must be an object")
        require(
            set(witness) == {"assertions", "scenarioReferences", "witnessId", "witnessKind"},
            "continuity witness fields differ",
        )
        witness_id = require_id(witness.get("witnessId"), "continuity witnessId")
        require(witness_id not in witness_ids, f"duplicate continuity witnessId: {witness_id}")
        witness_ids.add(witness_id)
        kind = require_id(witness.get("witnessKind"), f"{witness_id}: witnessKind")
        require(kind not in observed_kinds, f"duplicate continuity witness kind: {kind}")
        observed_kinds.add(kind)
        assertions = witness.get("assertions")
        references = witness.get("scenarioReferences")
        require(isinstance(assertions, dict), f"{witness_id}: assertions must be an object")
        require(isinstance(references, list) and references, f"{witness_id}: scenarioReferences is empty")
        for reference in references:
            require(isinstance(reference, dict), f"{witness_id}: scenario reference must be an object")
            require(
                set(reference)
                == {
                    "accepted",
                    "profileId",
                    "publicFacts",
                    "publicNarrativeContext",
                    "scenarioId",
                    "scenarioSemanticHash",
                    "targetPersistentId",
                },
                f"{witness_id}: scenario reference fields differ",
            )
            reference_id = reference.get("scenarioId")
            require(reference_id in scenarios_by_id, f"{witness_id}: unknown scenario reference {reference_id!r}")
            source = scenarios_by_id[reference_id]
            require(reference.get("accepted") is source.get("accepted"), f"{witness_id}: accepted projection differs")
            require(reference.get("profileId") == source.get("profileId"), f"{witness_id}: profile projection differs")
            require(
                reference.get("targetPersistentId") == source.get("targetPersistentId"),
                f"{witness_id}: target projection differs",
            )
            require(
                reference.get("scenarioSemanticHash") == source.get("semanticHash"),
                f"{witness_id}: semantic-hash projection differs",
            )
            require(reference.get("publicFacts") == source.get("publicFacts"), f"{witness_id}: fact projection differs")
            require(
                reference.get("publicNarrativeContext") == source.get("publicNarrativeContext"),
                f"{witness_id}: context projection differs",
            )
            validate_public_narrative_context(reference, f"{witness_id}/{reference_id}")

        if kind == "source-tuple-collision":
            require(len(references) == 2, f"{witness_id}: collision witness requires two scenarios")
            require(
                set(assertions) == {"domain", "originalFactId", "projectedFactIds", "sourceSubjectIds"},
                f"{witness_id}: collision assertions differ",
            )
            domain = require_id(assertions.get("domain"), f"{witness_id}: collision domain")
            original_fact_id = require_id(
                assertions.get("originalFactId"), f"{witness_id}: collision originalFactId"
            )
            source_subject_ids = assertions.get("sourceSubjectIds")
            projected_fact_ids = assertions.get("projectedFactIds")
            require(
                isinstance(source_subject_ids, list)
                and len(source_subject_ids) == 2
                and len(set(source_subject_ids)) == 2,
                f"{witness_id}: collision source subjects differ",
            )
            require(
                isinstance(projected_fact_ids, list)
                and len(projected_fact_ids) == 2
                and len(set(projected_fact_ids)) == 2,
                f"{witness_id}: collision projected IDs differ",
            )
            expected_ids = {
                projected_public_fact_id(domain, original_fact_id, source_subject_id)
                for source_subject_id in source_subject_ids
            }
            require(
                expected_ids == set(projected_fact_ids),
                f"{witness_id}: collision projection does not match its audit source tuples",
            )
            referenced_fact_ids = {
                fact["factId"] for reference in references for fact in reference["publicFacts"]
            }
            require(
                expected_ids <= referenced_fact_ids,
                f"{witness_id}: collision projections are absent from referenced public facts",
            )
        elif kind == "long-id":
            require(
                set(assertions)
                == {
                    "domain",
                    "minimumObservedOriginalFactIdLength",
                    "originalFactId",
                    "projectedFactId",
                    "sourceSubjectId",
                },
                f"{witness_id}: long-ID assertions differ",
            )
            minimum = assertions.get("minimumObservedOriginalFactIdLength")
            require(type(minimum) is int and minimum >= 64, f"{witness_id}: long-ID assertion is weak")
            domain = require_id(assertions.get("domain"), f"{witness_id}: long-ID domain")
            original_fact_id = require_id(
                assertions.get("originalFactId"), f"{witness_id}: long-ID originalFactId"
            )
            source_subject_id = require_id(
                assertions.get("sourceSubjectId"),
                f"{witness_id}: long-ID sourceSubjectId",
                allow_empty=True,
            )
            projected_fact_id = require_projected_public_fact_id(
                assertions.get("projectedFactId"), f"{witness_id}: long-ID projectedFactId"
            )
            require(len(original_fact_id) == minimum, f"{witness_id}: long-ID length assertion differs")
            require(
                projected_fact_id
                == projected_public_fact_id(domain, original_fact_id, source_subject_id),
                f"{witness_id}: long-ID projection does not match its audit source tuple",
            )
            referenced_fact_ids = {
                fact["factId"] for reference in references for fact in reference["publicFacts"]
            }
            require(
                projected_fact_id in referenced_fact_ids,
                f"{witness_id}: long-ID projection is absent from referenced public facts",
            )
        elif kind == "different-life":
            targets = assertions.get("targetPersistentIds")
            require(isinstance(targets, list) and len(targets) == 2, f"{witness_id}: target list differs")
            require(len(set(targets)) == 2, f"{witness_id}: distinct lives were merged")
            require(set(targets) == {ref["targetPersistentId"] for ref in references}, f"{witness_id}: targets differ")
        elif kind == "core-event":
            event_count = assertions.get("eventCount")
            actual = len(references[0]["publicNarrativeContext"]["events"])
            require(type(event_count) is int and event_count == actual and actual > 0, f"{witness_id}: core event missing")
        elif kind == "prior-generation":
            references_ids = assertions.get("priorHistoryFactIds")
            actual = references[0]["publicNarrativeContext"]["priorHistoryFactIds"]
            require(isinstance(references_ids, list) and references_ids == sorted(actual), f"{witness_id}: prior history differs")
            require(bool(actual), f"{witness_id}: prior history is empty")
        elif kind == "negative-reference":
            require(assertions == {"accepted": False, "evaluationOnly": True}, f"{witness_id}: negative assertion differs")
            require(all(ref["accepted"] is False for ref in references), f"{witness_id}: accepted scenario used as negative")

    require(observed_kinds == expected_kinds, f"continuity witness kinds differ: {sorted(observed_kinds)}")
    return len(witnesses)


def validate_negative_inventory(scenarios: list[dict[str, Any]]) -> None:
    """Pin current C# negative coverage, including the removed v17 combination."""
    require(len(scenarios) == EXPECTED_NEGATIVE_SCENARIO_COUNT, "negative coverage count differs")
    require(all(isinstance(row, dict) for row in scenarios), "negative scenario must be an object")
    require({row.get("scenarioId") for row in scenarios} == EXPECTED_NEGATIVE_SCENARIO_IDS,
            "negative scenario identity coverage differs")
    stale = next(row for row in scenarios if row["scenarioId"] == STALE_COMBINATION_NEGATIVE_SCENARIO_ID)
    context = stale.get("authorityContext", {})
    require(stale.get("profileId") == "CharacterSkill" and stale.get("accepted") is False,
            "stale combination must be a rejected CharacterSkill scenario")
    require(context.get("expectedAccepted") is False and context.get("actualAccepted") is False
            and context.get("validatedSkillCount") == 0, "stale combination rejection evidence differs")
    require(context.get("mutation") == "stale-v17-need-changed-cleaning-small-stock-small-combination-id",
            "stale combination mutation differs")
    response = json.loads(context.get("responseJson", ""))
    candidates = response.get("candidates")
    require(isinstance(candidates, list) and len(candidates) == 1, "stale response candidate count differs")
    stale_id = "skill-combination:sha256:80a6b140ecc1f8fbb34490423c1d0772bc2d644f0a54e937ed7a433371eb19b5"
    require(candidates[0].get("combinationId") == stale_id, "wrong historical combination under stale scenario ID")
    reason = stale.get("failureReason", "")
    require(reason == context.get("actualFailureReason")
            and reason.startswith("Unknown or illegal combination '") and stale_id in reason,
            "stale combination failure reason differs")
    rules = stale.get("request", {}).get("rules", [])
    require(rules and all(stale_id != combination.get("combinationId")
            for rule in rules for combination in rule.get("combinationOptions", [])),
            "historical illegal combination is present in current legal options")


def validate_delivery_package(
    export_dir: Path,
    manifest: dict[str, Any],
    catalog_hash: str,
) -> tuple[dict[str, int], int, dict[str, dict[str, int]], int]:
    actual_files = {path.name for path in export_dir.iterdir() if path.is_file()}
    missing_files = sorted(REQUIRED_DELIVERY_FILES - actual_files)
    require(not missing_files, "delivery package is missing files: " + ", ".join(missing_files))

    delivery = load_json(export_dir / "delivery_manifest.json")
    require(delivery.get("humanApprovalClaimed") is False, "delivery must not claim human approval")
    require(delivery.get("catalogHash") == catalog_hash, "delivery catalogHash is stale")
    require(delivery.get("inputDigest") == manifest.get("inputDigest"), "delivery inputDigest is stale")
    source = manifest.get("source")
    require(isinstance(source, dict), "manifest.source must be an object")
    require(delivery.get("currentCommit") == source.get("gameCommit"), "delivery currentCommit is stale")

    delivered = delivery.get("files")
    require(isinstance(delivered, list), "delivery files must be an array")
    expected_hashed_files = REQUIRED_DELIVERY_FILES - {"delivery_manifest.json"}
    delivered_paths: set[str] = set()
    for entry in delivered:
        require(isinstance(entry, dict), "delivery files contains a non-object entry")
        path = entry.get("path")
        require(isinstance(path, str) and path, "delivery file path is empty")
        require(path not in delivered_paths, f"delivery contains duplicate file: {path}")
        delivered_paths.add(path)
        artifact = export_dir / path
        require(artifact.is_file(), f"delivery artifact is missing: {path}")
        require(entry.get("sha256") == sha256_prefixed(artifact), f"delivery hash is stale: {path}")
        require(entry.get("byteLength") == artifact.stat().st_size, f"delivery size is stale: {path}")
    require(
        delivered_paths == expected_hashed_files,
        "delivery file set differs: expected="
        + repr(sorted(expected_hashed_files))
        + ", actual="
        + repr(sorted(delivered_paths)),
    )

    schema = load_json(export_dir / "scenario_schema.json")
    require(
        schema.get("$schema") == "https://json-schema.org/draft/2020-12/schema",
        "scenario schema draft differs",
    )
    schema_definitions = schema.get("$defs")
    require(isinstance(schema_definitions, dict), "scenario schema definitions are missing")
    scenario_schema = schema_definitions.get("scenario")
    require(isinstance(scenario_schema, dict), "base scenario schema is missing")
    require(scenario_schema.get("additionalProperties") is False, "scenario schema must reject extra fields")
    require(
        set(scenario_schema.get("required", [])) == SCENARIO_REQUIRED_FIELDS,
        "scenario schema required fields differ",
    )
    require(
        schema.get("oneOf")
        == [
            {"$ref": "#/$defs/positiveDocument"},
            {"$ref": "#/$defs/negativeDocument"},
        ],
        "scenario document alternatives differ",
    )

    positive = load_json(export_dir / "scenarios_100.json")
    negative = load_json(export_dir / "negative_scenarios.json")
    for label, document in (("positive", positive), ("negative", negative)):
        require(document.get("catalogHash") == catalog_hash, f"{label} catalogHash is stale")
        require(document.get("inputDigest") == manifest.get("inputDigest"), f"{label} inputDigest is stale")
    require(positive.get("schemaVersion") == 2, "positive schemaVersion differs")
    require(negative.get("schemaVersion") == 3, "negative schemaVersion differs")

    require(positive.get("expectedCounts") == EXPECTED_SCENARIO_COUNTS, "positive expectedCounts differs")
    positive_scenarios = positive.get("scenarios")
    negative_scenarios = negative.get("scenarios")
    require(isinstance(positive_scenarios, list), "positive scenarios must be an array")
    require(isinstance(negative_scenarios, list), "negative scenarios must be an array")
    require(positive.get("scenarioCount") == 100 == len(positive_scenarios), "positive count must be exactly 100")
    require(
        negative.get("negativeScenarioCount") == len(negative_scenarios),
        "negative count metadata differs",
    )
    require(
        len(negative_scenarios) == EXPECTED_NEGATIVE_SCENARIO_COUNT,
        f"negative count must be exactly {EXPECTED_NEGATIVE_SCENARIO_COUNT}",
    )
    validate_negative_inventory(negative_scenarios)

    scenario_ids: set[str] = set()
    semantic_hashes: set[str] = set()
    profile_counts: Counter[str] = Counter()
    candidate_counts: dict[str, list[int]] = defaultdict(list)
    public_fact_counts: dict[str, list[int]] = defaultdict(list)
    effect_description_counts: dict[str, list[int]] = defaultdict(list)
    skill_kinds: set[str] = set()
    trait_milestones: set[str] = set()

    def validate_scenario(scenario: Any, expected_accepted: bool) -> None:
        require(isinstance(scenario, dict), "scenario must be an object")
        require(set(scenario) == SCENARIO_REQUIRED_FIELDS, "scenario fields differ from the schema")
        scenario_id = scenario.get("scenarioId")
        require(isinstance(scenario_id, str) and scenario_id.strip() == scenario_id and scenario_id, "scenarioId is invalid")
        require(scenario_id not in scenario_ids, f"duplicate scenarioId: {scenario_id}")
        scenario_ids.add(scenario_id)
        profile_id = scenario.get("profileId")
        require(profile_id in EXPECTED_PROFILES, f"{scenario_id}: unknown profile {profile_id!r}")
        require(scenario.get("schemaVersion") == 2, f"{scenario_id}: scenario schemaVersion differs")
        require(scenario.get("accepted") is expected_accepted, f"{scenario_id}: accepted flag differs")
        failure_reason = scenario.get("failureReason")
        require(isinstance(failure_reason, str), f"{scenario_id}: failureReason must be a string")
        require((failure_reason == "") is expected_accepted, f"{scenario_id}: failureReason contract differs")
        require(scenario.get("catalogHash") == catalog_hash, f"{scenario_id}: catalogHash differs")
        require(scenario.get("inputDigest") == manifest.get("inputDigest"), f"{scenario_id}: inputDigest differs")
        semantic_hash = scenario.get("semanticHash")
        require(isinstance(semantic_hash, str) and semantic_hash.startswith("sha256:"), f"{scenario_id}: semanticHash is invalid")
        require(semantic_hash not in semantic_hashes, f"duplicate semanticHash: {semantic_hash}")
        semantic_hashes.add(semantic_hash)
        for field in ("producerIdentifier", "validatorIdentifier"):
            value = scenario.get(field)
            require(isinstance(value, str) and value.strip() == value and value, f"{scenario_id}: {field} is empty")
        for field in ("request", "authorityContext", "fixtureInput", "diversityAxes"):
            require(isinstance(scenario.get(field), dict), f"{scenario_id}: {field} must be an object")
        require(
            isinstance(scenario.get("fullLegalCandidates"), list),
            f"{scenario_id}: fullLegalCandidates must be an array",
        )
        if expected_accepted or scenario_id != CONSUMED_MILESTONE_NEGATIVE_SCENARIO_ID:
            require(
                bool(scenario["fullLegalCandidates"]),
                f"{scenario_id}: fullLegalCandidates must be non-empty",
            )
        for field in ("publicFacts", "effectDescriptions"):
            require(isinstance(scenario.get(field), list) and scenario[field], f"{scenario_id}: {field} must be non-empty")
        validate_public_narrative_context(scenario, scenario_id)
        fact_ids: set[str] = set()
        for fact in scenario["publicFacts"]:
            require(isinstance(fact, dict), f"{scenario_id}: public fact must be an object")
            fact_id = fact.get("factId")
            require(isinstance(fact_id, str) and fact_id, f"{scenario_id}: public factId is empty")
            require(fact_id not in fact_ids, f"{scenario_id}: duplicate public factId {fact_id}")
            fact_ids.add(fact_id)
        require(
            all(isinstance(value, str) and value for value in scenario["effectDescriptions"]),
            f"{scenario_id}: effectDescriptions must be non-empty strings",
        )
        if expected_accepted:
            profile_counts[profile_id] += 1
            candidate_counts[profile_id].append(len(scenario["fullLegalCandidates"]))
            public_fact_counts[profile_id].append(len(scenario["publicFacts"]))
            effect_description_counts[profile_id].append(len(scenario["effectDescriptions"]))
            validate_profile_scenario_material(scenario)
            if profile_id == "CharacterSkill":
                kind = scenario["diversityAxes"].get("kind")
                require(kind in {"Active", "Passive", "Ultimate"}, f"{scenario_id}: CharacterSkill kind differs")
                skill_kinds.add(kind)
            elif profile_id == "AcquiredTrait":
                milestone = scenario["diversityAxes"].get("manifestationMilestone")
                require(milestone in {"3", "8", "20"}, f"{scenario_id}: acquired-trait milestone differs")
                trait_milestones.add(milestone)
        elif scenario_id == CONSUMED_MILESTONE_NEGATIVE_SCENARIO_ID:
            context = scenario["authorityContext"]
            require(profile_id == "AcquiredTrait", f"{scenario_id}: profile differs")
            require(scenario["fullLegalCandidates"] == [], f"{scenario_id}: ineligible command must not invent candidates")
            require(context.get("actualAccepted") is False, f"{scenario_id}: actualAccepted differs")
            require(
                context.get("actualIssueCode") == "MilestoneAlreadyProcessed",
                f"{scenario_id}: rejection code differs",
            )
            require(context.get("candidatePacketGenerated") is False, f"{scenario_id}: rejected command produced a packet")
            require(context.get("stateUnchangedAfterRejectedRequest") is True, f"{scenario_id}: rejected command mutated state")
            require(context.get("ledgerPreserved") is True, f"{scenario_id}: source ledger changed")
            require(context.get("erasedStatePreserved") is True, f"{scenario_id}: erased state changed")
            require(context.get("rejectedRequestAddedEffectCount") == 0, f"{scenario_id}: rejected request added an effect")
            require(context.get("nextUnusedMilestone") == 8, f"{scenario_id}: next unused milestone differs")
            require(context.get("nextUnusedMilestoneAccepted") is True, f"{scenario_id}: next unused milestone was blocked")

    for scenario in positive_scenarios:
        validate_scenario(scenario, True)
    for scenario in negative_scenarios:
        validate_scenario(scenario, False)

    scenarios_by_id = {
        scenario["scenarioId"]: scenario
        for scenario in positive_scenarios + negative_scenarios
    }
    continuity_witness_count = validate_continuity_document(
        load_json(export_dir / "continuity_scenarios.json"),
        catalog_hash,
        manifest.get("inputDigest"),
        scenarios_by_id,
    )

    require(dict(profile_counts) == EXPECTED_SCENARIO_COUNTS, f"positive profile counts differ: {dict(profile_counts)}")
    require(skill_kinds == {"Active", "Passive", "Ultimate"}, f"CharacterSkill kinds incomplete: {sorted(skill_kinds)}")
    require(trait_milestones == {"3", "8", "20"}, f"AcquiredTrait milestones incomplete: {sorted(trait_milestones)}")
    negative_profiles = {scenario.get("profileId") for scenario in negative_scenarios if isinstance(scenario, dict)}
    require(negative_profiles == EXPECTED_PROFILES, f"negative profile coverage differs: {sorted(negative_profiles)}")
    material_summary = {
        profile: {
            "candidateMin": min(candidate_counts[profile]),
            "candidateMax": max(candidate_counts[profile]),
            "publicFactMin": min(public_fact_counts[profile]),
            "publicFactMax": max(public_fact_counts[profile]),
            "effectDescriptionMin": min(effect_description_counts[profile]),
            "effectDescriptionMax": max(effect_description_counts[profile]),
        }
        for profile in sorted(EXPECTED_PROFILES)
    }
    return (
        dict(sorted(profile_counts.items())),
        len(negative_scenarios),
        material_summary,
        continuity_witness_count,
    )


def load_narrative_ai_catalog_module(narrative_ai_root: Path) -> ModuleType:
    module_path = narrative_ai_root / "tools" / "v25_narrative_training" / "mechanic_catalog.py"
    require(module_path.is_file(), f"NarrativeAI importer is missing: {module_path}")
    specification = importlib.util.spec_from_file_location("v25_mechanic_catalog_external", module_path)
    require(specification is not None and specification.loader is not None, "could not load NarrativeAI importer")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def validate_external_public_context_schema(narrative_ai_root: Path) -> str:
    schema_path = (
        narrative_ai_root
        / "tools"
        / "v25_narrative_training"
        / "public_narrative_context.schema.json"
    )
    require(schema_path.is_file(), f"NarrativeAI public-context schema is missing: {schema_path}")
    schema = load_json(schema_path)
    require(
        schema.get("$schema") == "https://json-schema.org/draft/2020-12/schema",
        "NarrativeAI public-context schema draft differs",
    )
    require(schema.get("additionalProperties") is False, "public-context schema must be closed")
    require(set(schema.get("required", [])) == PUBLIC_CONTEXT_FIELDS, "public-context schema required fields differ")
    properties = schema.get("properties")
    require(isinstance(properties, dict) and set(properties) == PUBLIC_CONTEXT_FIELDS, "public-context schema properties differ")
    require(properties.get("schemaVersion", {}).get("const") == 1, "public-context schemaVersion differs")
    require(
        set(properties.get("profileId", {}).get("enum", [])) == EXPECTED_PROFILES,
        "public-context schema profiles differ",
    )
    definitions = schema.get("$defs")
    require(isinstance(definitions, dict), "public-context schema definitions are missing")
    identifier = definitions.get("id")
    require(
        isinstance(identifier, dict)
        and identifier.get("minLength") == 1
        and identifier.get("maxLength") == 512
        and identifier.get("pattern") == r"^\S+$",
        "public-context ID contract differs",
    )
    return sha256_prefixed(schema_path)


def verify(
    project_root: Path,
    export_dir: Path,
    narrative_ai_root: Path,
    require_training_eligible: bool,
) -> dict[str, Any]:
    catalog_path = export_dir / "catalog.json"
    manifest_path = export_dir / "manifest.json"
    parity_path = export_dir / "packet_parity_cases.json"
    evidence_path = export_dir / "test_evidence.json"
    require(catalog_path.is_file(), f"catalog is missing: {catalog_path}")
    require(manifest_path.is_file(), f"manifest is missing: {manifest_path}")
    require(parity_path.is_file(), f"packet parity is missing: {parity_path}")
    require(evidence_path.is_file(), f"test evidence is missing: {evidence_path}")

    public_context_schema_hash = validate_external_public_context_schema(narrative_ai_root)
    importer = load_narrative_ai_catalog_module(narrative_ai_root)
    catalog = importer.load_catalog(
        catalog_path,
        require_training_eligible=require_training_eligible,
    )
    manifest = load_json(manifest_path)
    manifest_count, required_provenance_count = validate_manifest(project_root, manifest)
    profile_counts = validate_parity(load_json(parity_path))
    evidence_gate_count = validate_test_evidence(
        project_root,
        load_json(evidence_path),
        manifest,
        catalog["catalogHash"],
        require_training_eligible,
    )
    (
        scenario_profile_counts,
        negative_scenario_count,
        scenario_material_summary,
        continuity_witness_count,
    ) = validate_delivery_package(export_dir, manifest, catalog["catalogHash"])
    return {
        "catalogHash": catalog["catalogHash"],
        "continuityWitnessCount": continuity_witness_count,
        "evidenceGateCount": evidence_gate_count,
        "inputDigest": manifest.get("inputDigest", ""),
        "manifestFileCount": manifest_count,
        "negativeScenarioCount": negative_scenario_count,
        "parityProfileCounts": profile_counts,
        "publicContextSchemaHash": public_context_schema_hash,
        "requiredProvenanceCount": required_provenance_count,
        "scenarioProfileCounts": scenario_profile_counts,
        "scenarioMaterialSummary": scenario_material_summary,
        "scenarioPositiveCount": sum(scenario_profile_counts.values()),
        "status": "PASS",
        "trainingEligible": bool(catalog.get("trainingEligible")),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--export-dir", type=Path, required=True)
    parser.add_argument("--narrative-ai-root", type=Path, required=True)
    parser.add_argument("--require-training-eligible", action="store_true")
    parser.add_argument("--report", type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        result = verify(
            args.project_root.resolve(),
            args.export_dir.resolve(),
            args.narrative_ai_root.resolve(),
            args.require_training_eligible,
        )
    except Exception as exception:
        print(f"V25_HANDOFF_VERIFY=FAIL:{type(exception).__name__}:{exception}", file=sys.stderr)
        return 1

    serialized = json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if args.report is not None:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(serialized, encoding="utf-8", newline="\n")
    print(serialized, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
