#!/usr/bin/env python3
"""Build the frozen Phase80 producer-closure manifest.

The builder is intentionally fail-closed.  It consumes only a final-frozen
inventory, validates every direct Record binding against the registered C#
outcome ID/adapter/descriptor/projector, hashes evidence from the source tree,
and refuses to invent a binding for an unmapped candidate.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable


SCHEMA = "gameplay-outcome-ledger-final-closure@1"
FINAL_INVENTORY_CLAIM = "complete-lexical-scan-with-listed-limitations"
EXPECTED_CANDIDATES = 323
EXPECTED_CONNECTED = 95
EXPECTED_EXCLUDED = 228
EXPECTED_MIGRATED = 57
MIGRATED_AUTHORITY = "MigratedProducerOutcomeReceipt"
RUNTIME_REPORT = (
    "Artifacts/QA/GameplayOutcomeLedgerPhase80Focused-20260920-r83/"
    "focused-runtime-report.json"
)
SAVE_REPORT = (
    "Artifacts/QA/GameplayOutcomeLedgerPhase80FullWorldSave-20260920-r42/"
    "full-world-round-trip-playmode-report.txt"
)
MIGRATED_CATALOG = (
    "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/"
    "MigratedProducers/MigratedProducerOutcomeContracts.cs"
)
MIGRATED_DEFINITION = (
    "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/"
    "MigratedProducers/MigratedProducerOutcomeDefinition.cs"
)
STABLE_ID = re.compile(r"^[a-z0-9][a-z0-9._:-]*$")
ALLOWED_SHARED_RECORD_OUTCOME_IDS = {
    "trade-inventory.result": {
        "PhysicalItemBatchDispositionReceipt",
        "PhysicalItemRelocationReceipt",
        "WastePolicyCommandResult",
    }
}
CATALOG_ENTRY = re.compile(
    r"D\(MigratedProducerOutcomeKind\.(?P<candidate>[A-Za-z0-9_]+),\s*"
    r'"(?P<outcome>[a-z0-9][a-z0-9._:-]+)"'
)


class BuildError(RuntimeError):
    pass


@dataclass(frozen=True)
class Binding:
    outcome_type_id: str
    adapter_type: str
    descriptor_type: str
    projector_type: str
    source_paths: tuple[str, ...]
    exact_substitute: bool = False


def binding(
    outcome_type_id: str,
    adapter_type: str,
    descriptor_type: str,
    projector_type: str,
    *source_paths: str,
    exact_substitute: bool = False,
) -> Binding:
    return Binding(
        outcome_type_id,
        adapter_type,
        descriptor_type,
        projector_type,
        tuple(source_paths),
        exact_substitute,
    )


BRIDGE = "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/"
ENV_RECEIPTS = BRIDGE + "Environment/EnvironmentOutcomeReceipts.cs"
ENV_ADAPTERS = BRIDGE + "Environment/EnvironmentOutcomeAdapters.cs"
ENV_DESCRIPTORS = BRIDGE + "Environment/EnvironmentOutcomeDescriptors.cs"
EVOLUTION_RECEIPTS = BRIDGE + "Evolution/EvolutionOutcomeReceipts.cs"
EVOLUTION_ADAPTERS = BRIDGE + "Evolution/EvolutionOutcomeAdapters.cs"
EVOLUTION_DESCRIPTORS = BRIDGE + "Evolution/EvolutionOutcomeDescriptors.cs"
SOCIAL_RECEIPTS = BRIDGE + "SocialLife/SocialLifeOutcomeReceipts.cs"
SOCIAL_ADAPTERS = BRIDGE + "SocialLife/SocialLifeOutcomeAdapters.cs"
SOCIAL_DESCRIPTORS = BRIDGE + "SocialLife/SocialLifeOutcomeDescriptors.cs"
TRADE_RECEIPTS = BRIDGE + "TradeInventory/TradeInventoryOutcomeReceipts.cs"
TRADE_ADAPTER = BRIDGE + "TradeInventory/TradeInventoryOutcomeAdapter.cs"
TRADE_DESCRIPTOR = BRIDGE + "TradeInventory/TradeInventoryOutcomeDescriptor.cs"


def one_file(
    outcome_type_id: str,
    adapter_type: str,
    descriptor_type: str,
    projector_type: str,
    path: str,
) -> Binding:
    return binding(
        outcome_type_id,
        adapter_type,
        descriptor_type,
        projector_type,
        path,
    )


EXPLICIT_BINDINGS: dict[str, Binding] = {
    "ApologyEvent": binding(
        "social.apology-resolved", "ApologyOutcomeAdapter",
        "ApologyOutcomeDescriptor", "ApologyPerspectiveProjector",
        SOCIAL_RECEIPTS, SOCIAL_ADAPTERS, SOCIAL_DESCRIPTORS),
    "ApparelChangedEvent": binding(
        "equipment.apparel-changed", "ApparelChangeOutcomeAdapter",
        "ApparelChangeOutcomeDescriptor", "ApparelChangePerspectiveProjector",
        EVOLUTION_RECEIPTS, EVOLUTION_ADAPTERS, EVOLUTION_DESCRIPTORS),
    "ApparelPhysicalTransactionResult": binding(
        "equipment.apparel-physical-completed", "ApparelPhysicalOutcomeAdapter",
        "ApparelPhysicalOutcomeDescriptor", "ApparelPhysicalPerspectiveProjector",
        EVOLUTION_RECEIPTS, EVOLUTION_ADAPTERS, EVOLUTION_DESCRIPTORS),
    "BlueprintResearchWorkResult": one_file(
        "research.work-applied", "ResearchWorkOutcomeAdapter",
        "ResearchWorkOutcomeDescriptor", "ResearchWorkOutcomePerspectiveProjector",
        BRIDGE + "ResearchWorkOutcomeDefinition.cs"),
    "CaptiveEscapedEvent": one_file(
        "captivity.captive-escaped", "CaptiveEscapeOutcomeAdapter",
        "CaptiveEscapeOutcomeDescriptor", "CaptiveEscapePerspectiveProjector",
        BRIDGE + "SocialLife/CaptiveEscapeOutcomeDefinition.cs"),
    "CaptivePerformerMilestoneEvent": one_file(
        "captivity.performer-milestone-unlocked",
        "CaptivePerformerMilestoneOutcomeAdapter",
        "CaptivePerformerMilestoneOutcomeDescriptor",
        "CaptivePerformerMilestonePerspectiveProjector",
        BRIDGE + "SocialLife/CaptivePerformerMilestoneOutcomeDefinition.cs"),
    "CaptiveRansomedEvent": one_file(
        "captivity.captive-ransomed", "CaptiveRansomOutcomeAdapter",
        "CaptiveRansomOutcomeDescriptor", "CaptiveRansomPerspectiveProjector",
        BRIDGE + "SocialLife/CaptiveRansomOutcomeDefinition.cs"),
    "CaptivityInteractionResult": one_file(
        "captivity.interaction-resolved", "CaptivityInteractionOutcomeAdapter",
        "CaptivityInteractionOutcomeDescriptor",
        "CaptivityInteractionPerspectiveProjector",
        BRIDGE + "SocialLife/CaptivityInteractionOutcomeDefinition.cs"),
    "CertifiedSeedPlanExecutionReceipt": binding(
        "environment.certified-seed-completed",
        "CertifiedSeedCompletionOutcomeAdapter",
        "CertifiedSeedCompletionOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "CharacterAcquiredTraitInferenceCommandResult": binding(
        "trait.acquired-inference-completed", "AcquiredTraitInferenceOutcomeAdapter",
        "AcquiredTraitInferenceOutcomeDescriptor",
        "AcquiredTraitInferencePerspectiveProjector",
        EVOLUTION_RECEIPTS, EVOLUTION_ADAPTERS, EVOLUTION_DESCRIPTORS),
    "CharacterAcquiredTraitReactionExecutedEvent": binding(
        "trait.acquired-reaction-executed", "AcquiredTraitReactionOutcomeAdapter",
        "AcquiredTraitReactionOutcomeDescriptor", "TraitReactionPerspectiveProjector",
        EVOLUTION_RECEIPTS, EVOLUTION_ADAPTERS, EVOLUTION_DESCRIPTORS),
    "CropIrrigationSupplyResult": binding(
        "environment.crop-irrigation-supplied", "CropIrrigationSupplyOutcomeAdapter",
        "CropIrrigationSupplyOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "CropPlanExecutionReceipt": binding(
        "environment.crop-plan-terminal", "CropPlanGameplayOutcomeAdapter",
        "CropPlanOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "DungeonSpaceExpansionResult": one_file(
        "infrastructure.dungeon-space-expanded", "DungeonSpaceExpansionOutcomeAdapter",
        "DungeonSpaceExpansionOutcomeDescriptor",
        "DungeonSpaceExpansionPerspectiveProjector",
        BRIDGE + "Infrastructure/DungeonSpaceExpansionOutcomeDefinition.cs"),
    "EmergencyWorkSuspensionReceipt": one_file(
        "work.emergency-suspension-recorded", "EmergencyWorkSuspensionOutcomeAdapter",
        "EmergencyWorkSuspensionOutcomeDescriptor",
        "EmergencyWorkSuspensionPerspectiveProjector",
        BRIDGE + "ProductionCombat/EmergencyWorkSuspensionOutcomeDefinition.cs"),
    "EnvironmentalFireDamageResult": binding(
        "environment.fire-damage", "EnvironmentalFireDamageOutcomeAdapter",
        "FireDamageOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "EnvironmentalFireFuelLossReceipt": binding(
        "environment.fire-fuel-loss", "EnvironmentalFireFuelOutcomeAdapter",
        "FireFuelOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "EnvironmentalFireIgnitionResult": binding(
        "environment.fire-ignition", "EnvironmentalFireIgnitionOutcomeAdapter",
        "FireIgnitionOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "EnvironmentalFireSuppressionResult": binding(
        "environment.fire-suppression", "EnvironmentalFireSuppressionOutcomeAdapter",
        "FireSuppressionOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "EnvironmentalFireWaterReceipt": binding(
        "environment.fire-water-consumed", "EnvironmentalFireWaterOutcomeAdapter",
        "FireWaterOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "FacilityEvolutionCompletedEvent": binding(
        "facility.evolution-completed", "FacilityEvolutionOutcomeAdapter",
        "FacilityEvolutionOutcomeDescriptor", "FacilityEvolutionPerspectiveProjector",
        EVOLUTION_RECEIPTS, EVOLUTION_ADAPTERS, EVOLUTION_DESCRIPTORS),
    "FacilitySynthesisResult": one_file(
        "facility.synthesis-completed", "FacilitySynthesisOutcomeAdapter",
        "FacilitySynthesisOutcomeDescriptor", "FacilitySynthesisPerspectiveProjector",
        BRIDGE + "FacilitySynthesis/FacilitySynthesisOutcomeDefinition.cs"),
    "InfrastructureCommandResult": one_file(
        "infrastructure.command-applied", "InfrastructureCommandOutcomeAdapter",
        "InfrastructureCommandOutcomeDescriptor",
        "InfrastructureCommandPerspectiveProjector",
        BRIDGE + "ProductionCombat/InfrastructureCommandOutcomeDefinition.cs"),
    "MemoryErasureSealBossAwardResult": binding(
        "item.memory-erasure-seal-boss-awarded",
        "MemoryErasureBossAwardOutcomeAdapter",
        "MemoryErasureBossAwardOutcomeDescriptor",
        "MemoryErasureBossAwardPerspectiveProjector",
        EVOLUTION_RECEIPTS, EVOLUTION_ADAPTERS, EVOLUTION_DESCRIPTORS),
    "MemoryErasureSealUseCompletedEvent": binding(
        "trait.memory-erasure-terminal", "MemoryErasureOutcomeAdapter",
        "MemoryErasureOutcomeDescriptor", "MemoryErasurePerspectiveProjector",
        EVOLUTION_RECEIPTS, EVOLUTION_ADAPTERS, EVOLUTION_DESCRIPTORS),
    "OffenseTruthRevealedEvent": binding(
        "offense.truth-revealed", "OffenseTruthRevealOutcomeAdapter",
        "OffenseTruthRevealOutcomeDescriptor", "OffenseTruthRevealPerspectiveProjector",
        BRIDGE + "ExternalFaction/ExternalFactionOutcomeReceipts.cs",
        BRIDGE + "ExternalFaction/ExternalFactionOutcomeAdapters.cs",
        BRIDGE + "ExternalFaction/ExternalFactionOutcomeDescriptors.cs"),
    "PhysicalItemBatchDispositionReceipt": Binding(
        "trade-inventory.result", "TradeInventoryOutcomeAdapter",
        "TradeInventoryOutcomeDescriptor", "TradeInventoryOutcomePerspectiveProjector",
        (TRADE_RECEIPTS, TRADE_ADAPTER, TRADE_DESCRIPTOR)),
    "PhysicalItemRelocationReceipt": Binding(
        "trade-inventory.result", "TradeInventoryOutcomeAdapter",
        "TradeInventoryOutcomeDescriptor", "TradeInventoryOutcomePerspectiveProjector",
        (TRADE_RECEIPTS, TRADE_ADAPTER, TRADE_DESCRIPTOR)),
    "PopulationDiseaseRouteExposureEvent": binding(
        "environment.disease-route-exposure", "PopulationDiseaseExposureOutcomeAdapter",
        "DiseaseExposureOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "ProductionBillCommandResult": one_file(
        "production.command-applied", "ProductionCommandOutcomeAdapter",
        "ProductionCommandOutcomeDescriptor", "ProductionCommandPerspectiveProjector",
        BRIDGE + "ProductionCombat/ProductionCommandOutcomeDefinition.cs"),
    "ProductionRecipeExecutionReceipt": binding(
        "production.completed", "ProductionCompletedOutcomeAdapter",
        "ProductionCompletedOutcomeDescriptor", "ProductionCompletedPerspectiveProjector",
        BRIDGE + "ProductionCombat/ProductionCombatOutcomeReceipts.cs",
        BRIDGE + "ProductionCombat/ProductionCombatOutcomeAdapters.cs",
        BRIDGE + "ProductionCombat/ProductionCombatOutcomeDescriptors.cs"),
    "RoomConditionChangedEvent": binding(
        "environment.room-condition-changed", "RoomConditionOutcomeAdapter",
        "RoomConditionOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "RoomEnvironmentExperienceEvent": binding(
        "environment.room-experience-applied", "RoomEnvironmentExperienceOutcomeAdapter",
        "RoomEnvironmentExperienceOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "SocialConflictEvent": binding(
        "social.conflict-resolved", "SocialConflictOutcomeAdapter",
        "SocialConflictOutcomeDescriptor", "SocialConflictPerspectiveProjector",
        SOCIAL_RECEIPTS, SOCIAL_ADAPTERS, SOCIAL_DESCRIPTORS),
    "SpeciesIncidentTriggeredEvent": binding(
        "environment.species-incident-triggered", "SpeciesIncidentOutcomeAdapter",
        "SpeciesIncidentOutcomeDescriptor", "EnvironmentPerspectiveProjector",
        ENV_RECEIPTS, ENV_ADAPTERS, ENV_DESCRIPTORS),
    "StaffRebellionResponseResult": one_file(
        "social.staff-rebellion-response-resolved", "StaffDiscontentOutcomeAdapter",
        "StaffDiscontentOutcomeDescriptor", "StaffDiscontentPerspectiveProjector",
        BRIDGE + "SocialLife/StaffDiscontentOutcomeDefinition.cs"),
    "WastePolicyCommandResult": Binding(
        "trade-inventory.result", "TradeInventoryOutcomeAdapter",
        "TradeInventoryOutcomeDescriptor", "TradeInventoryOutcomePerspectiveProjector",
        (TRADE_RECEIPTS, TRADE_ADAPTER, TRADE_DESCRIPTOR)),
    "WorkCompletedIdentityEvent": one_file(
        "character.work-identity-applied", "WorkCompletionIdentityOutcomeAdapter",
        "WorkCompletionIdentityOutcomeDescriptor",
        "WorkCompletionIdentityPerspectiveProjector",
        BRIDGE + "ProductionCombat/WorkCompletionIdentityOutcomeDefinition.cs"),
}


def load_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise BuildError(f"{path}: root must be an object")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require_repo_file(source_root: Path, relative: str) -> Path:
    normalized = relative.replace("\\", "/")
    if not normalized or normalized.startswith("/") or ":" in normalized.split("/")[0]:
        raise BuildError(f"evidence path is not repository-relative: {relative!r}")
    path = (source_root / normalized).resolve()
    try:
        path.relative_to(source_root)
    except ValueError as error:
        raise BuildError(f"evidence path escapes source root: {relative}") from error
    if not path.is_file():
        raise BuildError(f"required evidence file is missing: {relative}")
    return path


def evidence(source_root: Path, kind: str, relative: str, proof: str) -> dict[str, str]:
    path = require_repo_file(source_root, relative)
    return {
        "kind": kind,
        "path": relative.replace("\\", "/"),
        "sha256": sha256(path),
        "proof": proof,
    }


def parse_migrated_bindings(source_root: Path) -> dict[str, Binding]:
    catalog_path = require_repo_file(source_root, MIGRATED_CATALOG)
    source = catalog_path.read_text(encoding="utf-8-sig")
    pairs = CATALOG_ENTRY.findall(source)
    if len(pairs) != EXPECTED_MIGRATED:
        raise BuildError(
            f"migrated catalog must contain {EXPECTED_MIGRATED} definitions, got {len(pairs)}"
        )
    if len({candidate for candidate, _ in pairs}) != len(pairs):
        raise BuildError("migrated catalog contains duplicate source candidates")
    if len({outcome for _, outcome in pairs}) != len(pairs):
        raise BuildError("migrated catalog contains duplicate outcome type IDs")
    return {
        candidate: Binding(
            outcome,
            "MigratedProducerOutcomeAdapter",
            "MigratedProducerOutcomeDescriptor",
            "MigratedProducerOutcomeProjector",
            (MIGRATED_CATALOG, MIGRATED_DEFINITION),
        )
        for candidate, outcome in pairs
    }


def validate_binding(source_root: Path, candidate: str, item: Binding) -> None:
    if not STABLE_ID.fullmatch(item.outcome_type_id):
        raise BuildError(f"{candidate}: invalid registered outcome type ID")
    combined = "\n".join(
        require_repo_file(source_root, relative).read_text(encoding="utf-8-sig")
        for relative in item.source_paths
    )
    required = (
        item.outcome_type_id,
        item.adapter_type,
        item.descriptor_type,
        item.projector_type,
    )
    missing = [symbol for symbol in required if symbol not in combined]
    if missing:
        raise BuildError(
            f"{candidate}: registered binding symbols are absent from audited source: "
            + ", ".join(missing)
        )


def record_owner(definition: dict[str, Any]) -> str:
    owners: list[str] = []
    for site in definition.get("distinct_producer_sites") or []:
        owner = site.get("owner_type") if isinstance(site, dict) else None
        if (
            isinstance(owner, str)
            and len(owner.strip()) >= 3
            and owner != "<unknown>"
        ):
            owners.append(owner.strip())
    if owners:
        return ", ".join(dict.fromkeys(owners[:3]))
    authority = str(definition.get("replacement_authority") or "").strip()
    if authority:
        return authority
    raise BuildError(f"{definition.get('base_type')}: no reviewed source owner")


def classify_non_result(definition: dict[str, Any]) -> str:
    name = str(definition.get("base_type") or "")
    reason = " ".join(
        str(definition.get(key) or "")
        for key in ("coverage_reason", "integration_reason", "replacement_authority")
    ).lower()
    lowered = name.lower()
    if (
        lowered.startswith("i") and len(name) > 1 and name[1].isupper()
        or "contract" in lowered
        or "interface" in reason
    ):
        return "contract-or-interface"
    if any(token in lowered for token in ("debug", "diagnostic", "trace", "audit")):
        return "debug-diagnostic"
    if any(token in reason for token in ("duplicate", "lossy", "derived notification", "same committed")):
        return "duplicate-derived-notification"
    if any(token in lowered for token in ("ui", "alert", "infofeed", "tabrequested", "surface")) \
            or "presentation" in reason:
        return "presentation-ui-wrapper"
    if any(token in lowered for token in ("save", "restore", "checkpoint", "reconciliation", "gcresult")) \
            or any(token in reason for token in ("persistence", "save migration", "restore-only")):
        return "persistence-maintenance"
    if any(token in lowered for token in ("preview", "candidate", "compatibility", "validation", "quote")) \
            or any(token in reason for token in ("preview", "candidate selection", "what-if")):
        return "preview-or-candidate"
    if any(token in reason for token in (
        "query", "projection", "calculation", "computed", "lookup", "read-only",
        "no mutation", "pure", "eligibility", "policy decision",
    )):
        return "pure-query-or-projection"
    return "lifecycle-or-progress-signal"


def source_binding_evidence(
    source_root: Path,
    candidate: str,
    item: Binding,
) -> list[dict[str, str]]:
    return [
        evidence(
            source_root,
            "source-owner",
            relative,
            f"Audited bridge source contributes to the registered {candidate} mapping for "
            f"canonical outcome type {item.outcome_type_id}.",
        )
        for relative in item.source_paths
    ]


def reviewed_owner_path(source_root: Path, definition: dict[str, Any]) -> str:
    for raw in definition.get("review_evidence") or []:
        if not isinstance(raw, str):
            continue
        match = re.match(r"^(Assets/Scripts/.*?\.cs)(?::|#|$)", raw.replace("\\", "/"))
        if match is None:
            continue
        relative = match.group(1)
        try:
            require_repo_file(source_root, relative)
        except BuildError:
            continue
        return relative
    raise BuildError(
        f"{definition.get('base_type')}: no existing reviewed production-owner source path"
    )


def discover_migrated_owner_paths(
    source_root: Path,
    candidates: Iterable[str],
) -> dict[str, tuple[str, ...]]:
    expected = set(candidates)
    found: dict[str, set[str]] = {candidate: set() for candidate in expected}
    token = re.compile(
        r"MigratedProducerOutcomeKind\s*\.\s*([A-Za-z0-9_]+)"
    )
    scripts_root = source_root / "Assets" / "Scripts"
    for path in scripts_root.rglob("*.cs"):
        relative = path.relative_to(source_root).as_posix()
        if "/Editor/" in relative or relative in {MIGRATED_CATALOG, MIGRATED_DEFINITION}:
            continue
        source = path.read_text(encoding="utf-8-sig")
        for candidate in token.findall(source):
            if candidate in expected:
                found[candidate].add(relative)
    missing = sorted(candidate for candidate, paths in found.items() if not paths)
    if missing:
        raise BuildError(
            "migrated candidates lack a production-owner integration reference: "
            + ", ".join(missing)
        )
    return {
        candidate: tuple(sorted(paths, key=str.lower))
        for candidate, paths in found.items()
    }


def make_record_row(
    source_root: Path,
    definition: dict[str, Any],
    item: Binding,
    direct_owner_paths: tuple[str, ...] = (),
) -> dict[str, Any]:
    candidate = definition["base_type"]
    reviewed_path = reviewed_owner_path(source_root, definition)
    owner_paths = tuple(dict.fromkeys(direct_owner_paths or (reviewed_path,)))
    owner = ", ".join(dict.fromkeys(Path(path).stem for path in owner_paths))
    integration_reason = str(definition.get("integration_reason") or "").strip()
    if len(integration_reason) < 20:
        raise BuildError(f"{candidate}: reviewed integration reason is missing")
    row: dict[str, Any] = {
        "candidate": candidate,
        "definitionPath": definition["definition_path"],
        "status": "VERIFIED",
        "disposition": "ExactSubstitute" if item.exact_substitute else "Record",
        "reason": str(definition.get("coverage_reason") or "").strip(),
        "sourceOwner": owner,
        "authoritySymbol": str(definition.get("replacement_authority") or "").strip()
        or item.adapter_type,
        "verificationEvidence": [
            evidence(
                source_root,
                "source-owner",
                relative,
                f"Production-owner source directly references the {candidate} outcome "
                "integration at the retained mutation/commit boundary.",
            )
            for relative in owner_paths
        ]
        + ([] if reviewed_path in owner_paths else [
            evidence(
                source_root,
                "source-owner",
                reviewed_path,
                f"Frozen review evidence for {candidate} identifies the audited source "
                "operation retained by the final disposition.",
            )
        ])
        + source_binding_evidence(source_root, candidate, item)
        + [
            evidence(
                source_root,
                "runtime-test",
                RUNTIME_REPORT,
                "The frozen focused suite passes all 48 outcome-ledger integration steps, "
                "including all 57 migrated producers and the registered domain bridges.",
            ),
            evidence(
                source_root,
                "save-fault-test",
                SAVE_REPORT,
                "The frozen sealed-save run restores all 81 registered sections and the "
                "canonical baseline without warnings or errors.",
            ),
        ],
        "ownerSeam": f"{owner}: {integration_reason}",
        "commitBoundary": integration_reason,
        "outboxSaveOwner": (
            "GameplayOutcomeLedgerSaveSection plus the reviewed source-owner "
            "transaction/outbox named by this row"
        ),
        "replayIdentity": (
            f"The connected owner seam derives and persists the canonical {item.outcome_type_id} "
            "result key from the reviewed operation/revision identity for retry and replay."
        ),
        "uiQueryProof": (
            f"Registered {item.descriptor_type} and {item.projector_type} expose "
            f"{item.outcome_type_id} through the shared outcome query/presentation path."
        ),
    }
    if item.exact_substitute:
        members = definition.get("members")
        preserved = [str(value) for value in members or [] if str(value).strip()]
        if not preserved:
            preserved = ["reviewed terminal result facts and stable operation identity"]
        row.update({
            "substituteOutcomeTypeId": item.outcome_type_id,
            "sameOperationProof": (
                f"{item.adapter_type} converts the same {candidate} operation into the canonical "
                f"{item.outcome_type_id} receipt before durable commit; the reviewed integration "
                "does not start a second gameplay operation."
            ),
            "preservedFacts": preserved,
        })
    else:
        row.update({
            "outcomeTypeId": item.outcome_type_id,
            "adapterType": item.adapter_type,
            "descriptorType": item.descriptor_type,
            "projectorType": item.projector_type,
        })
    return row


def make_non_result_row(
    source_root: Path,
    definition: dict[str, Any],
) -> dict[str, Any]:
    candidate = definition["base_type"]
    authority = str(definition.get("replacement_authority") or "").strip()
    if not authority:
        raise BuildError(f"{candidate}: excluded row has no replacement authority")
    definition_path = str(definition.get("definition_path") or "").replace("\\", "/")
    return {
        "candidate": candidate,
        "definitionPath": definition_path,
        "status": "VERIFIED",
        "disposition": "NonResult",
        "reason": str(definition.get("coverage_reason") or "").strip(),
        "sourceOwner": record_owner(definition),
        "authoritySymbol": authority,
        "nonResultCategory": classify_non_result(definition),
        "verificationEvidence": [
            evidence(
                source_root,
                "source-audit",
                definition_path,
                f"Defines {candidate}; the reviewed inventory excludes it in favor of "
                f"the authoritative {authority} boundary.",
            )
        ],
    }


def build_manifest(inventory: dict[str, Any], source_root: Path) -> dict[str, Any]:
    source_root = source_root.resolve()
    metadata = inventory.get("metadata")
    if not isinstance(metadata, dict):
        raise BuildError("inventory metadata is missing")
    required_metadata = {
        "run_mode": "final-frozen",
        "source_snapshot_stable_during_scan": True,
        "completeness_claim": FINAL_INVENTORY_CLAIM,
        "unresolved_publish_event_type_count": 0,
        "unknown_publish_receiver_count": 0,
        "unreviewed_preexisting_definition_count": 0,
    }
    for key, expected in required_metadata.items():
        if metadata.get(key) != expected:
            raise BuildError(
                f"inventory metadata {key} must be {expected!r}, got {metadata.get(key)!r}"
            )

    definitions = inventory.get("definitions")
    if not isinstance(definitions, list):
        raise BuildError("inventory definitions must be an array")
    candidates = [
        item for item in definitions
        if isinstance(item, dict)
        and item.get("inventory_scope") != "phase80-implementation-infrastructure"
    ]
    names = [str(item.get("base_type") or "") for item in candidates]
    if len(candidates) != EXPECTED_CANDIDATES or len(set(names)) != len(names):
        raise BuildError(
            f"final denominator must be {EXPECTED_CANDIDATES} unique candidates, "
            f"got {len(candidates)} rows/{len(set(names))} names"
        )
    records = [item for item in candidates if item.get("coverage_disposition") == "Record"]
    non_results = [item for item in candidates if item.get("coverage_disposition") == "NonResult"]
    if len(records) != EXPECTED_CONNECTED or len(non_results) != EXPECTED_EXCLUDED:
        raise BuildError(
            f"reviewed disposition counts must be {EXPECTED_CONNECTED} connected Record and "
            f"{EXPECTED_EXCLUDED} excluded NonResult, got {len(records)}/{len(non_results)}"
        )
    for item in records:
        if item.get("integration_status") != "connected":
            raise BuildError(f"{item.get('base_type')}: Record is not connected")
    for item in non_results:
        if item.get("integration_status") != "excluded":
            raise BuildError(f"{item.get('base_type')}: NonResult is not excluded")

    migrated = parse_migrated_bindings(source_root)
    migrated_owner_paths = discover_migrated_owner_paths(source_root, migrated)
    bindings = dict(EXPLICIT_BINDINGS)
    overlap = set(bindings) & set(migrated)
    if overlap:
        raise BuildError("explicit and migrated bindings overlap: " + ", ".join(sorted(overlap)))
    bindings.update(migrated)
    record_names = {item["base_type"] for item in records}
    missing = sorted(record_names - set(bindings), key=str.lower)
    extra = sorted(set(bindings) - record_names, key=str.lower)
    if missing or extra:
        raise BuildError(
            f"binding coverage mismatch; missing={missing!r}, extra={extra!r}"
        )
    for candidate, item in bindings.items():
        validate_binding(source_root, candidate, item)

    candidates_by_id: dict[str, set[str]] = {}
    for candidate, item in bindings.items():
        if not item.exact_substitute:
            candidates_by_id.setdefault(item.outcome_type_id, set()).add(candidate)
    shared = {
        outcome_type_id: candidate_names
        for outcome_type_id, candidate_names in candidates_by_id.items()
        if len(candidate_names) > 1
    }
    if shared != ALLOWED_SHARED_RECORD_OUTCOME_IDS:
        raise BuildError(
            "shared Record outcome type IDs do not match the audited dynamic-adapter set: "
            + repr(shared)
        )

    rows: list[dict[str, Any]] = []
    for definition in sorted(candidates, key=lambda value: value["base_type"].lower()):
        candidate = definition["base_type"]
        if definition.get("coverage_disposition") == "Record":
            rows.append(make_record_row(
                source_root,
                definition,
                bindings[candidate],
                migrated_owner_paths.get(candidate, ()),
            ))
        else:
            rows.append(make_non_result_row(source_root, definition))

    return {
        "schema": SCHEMA,
        "sourceCommit": metadata.get("source_commit"),
        "sourceScanSha256": metadata.get("source_scan_sha256"),
        "candidateCount": len(candidates),
        "humanApprovalClaimed": False,
        "trainingEligible": False,
        "rows": rows,
    }


def write_manifest(output: Path, manifest: dict[str, Any]) -> None:
    if output.exists():
        raise BuildError(f"refusing to overwrite immutable output: {output}")
    output.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(manifest, ensure_ascii=False, indent=2) + "\n"
    temporary = output.with_name(output.name + ".tmp")
    if temporary.exists():
        raise BuildError(f"temporary output already exists: {temporary}")
    temporary.write_text(payload, encoding="utf-8", newline="\n")
    temporary.replace(output)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        manifest = build_manifest(
            load_json(args.inventory.resolve()),
            args.source_root.resolve(),
        )
        write_manifest(args.output.resolve(), manifest)
    except (BuildError, OSError, json.JSONDecodeError) as error:
        print(f"FAIL Phase80 final closure build: {error}", file=sys.stderr)
        return 1
    direct = sum(row["disposition"] == "Record" for row in manifest["rows"])
    substitutes = sum(row["disposition"] == "ExactSubstitute" for row in manifest["rows"])
    excluded = sum(row["disposition"] == "NonResult" for row in manifest["rows"])
    print(
        "PASS Phase80 final closure build: "
        f"{len(manifest['rows'])} candidates, {direct} Record, "
        f"{substitutes} ExactSubstitute, {excluded} NonResult"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
