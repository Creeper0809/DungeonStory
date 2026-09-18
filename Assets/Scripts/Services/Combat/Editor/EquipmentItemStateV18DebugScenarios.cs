#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

public static class EquipmentItemStateV18DebugScenarios
{
    [MenuItem("DungeonStory/Debug/Combat/Run V18 Equipment Item State Contracts")]
    public static void RunAll()
    {
        CombatEquipmentInstance source = new()
        {
            instanceId = "item-instance:test-equipment-state",
            definitionId = "weapon:test-v18",
            materialId = "material:test-steel",
            quality = CombatEquipmentQuality.Masterwork,
            durabilityRatio = 0.42f,
            loadedAmmunition = new LoadedAmmunitionBatch
            {
                ammunitionItemId = "ammo:test-v21",
                remaining = 7
            },
            worldState = CombatEquipmentWorldState.Stored,
            ownerCharacterId = "character:test-owner",
            sourceStackId = "stack:test-equipment-state",
            evolution = new EquipmentEvolutionState
            {
                evolutionNodes = new List<EvolutionNode>
                {
                    new EvolutionNode
                    {
                        nodeId = "equipment-node:legacy-fixed",
                        effectId = "equipment:force",
                        potencyMultiplier = 1.375f,
                        formulaVersion = 0
                    }
                }
            },
            moduleSlots = new List<EquipmentModuleSlotState>
            {
                new EquipmentModuleSlotState
                {
                    slotIndex = 0,
                    moduleInstanceId = "item-instance:module:test-v18"
                }
            }
        };

        EquipmentModuleInstance attachedModule = new()
        {
            instanceId = "item-instance:module:test-v18",
            definitionId = "module-definition:test-v18",
            grade = 3,
            condition = 0.83f,
            identified = true,
            attachedEquipmentInstanceId = source.instanceId,
            state = EquipmentModuleProcessState.Installed
        };
        ItemInstanceComponentSaveData encoded =
            EquipmentItemStateCodec.Encode(source, new[] { attachedModule });
        Require(encoded.schemaVersion == EquipmentItemStateCodec.CurrentSchemaVersion,
            "Equipment component schema was not upgraded to V4.");
        Require(EquipmentItemStateCodec.TryDecode(
                encoded,
                out CombatEquipmentInstance restored,
                out string error),
            error);
        Require(restored.instanceId == source.instanceId
                && restored.definitionId == source.definitionId
                && restored.materialId == source.materialId
                && restored.quality == source.quality
                && Mathf.Approximately(restored.durabilityRatio, source.durabilityRatio)
                && restored.loadedAmmunition.ammunitionItemId
                    == source.loadedAmmunition.ammunitionItemId
                && restored.loadedAmmunition.remaining
                    == source.loadedAmmunition.remaining
                && restored.worldState == source.worldState
                && restored.ownerCharacterId == source.ownerCharacterId
                && restored.sourceStackId == source.sourceStackId
                && restored.evolution.evolutionNodes.Count == 1
                && restored.evolution.evolutionNodes[0].formulaVersion == 0
                && restored.evolution.evolutionNodes[0].effectId == "equipment:force"
                && Mathf.Approximately(
                    restored.evolution.evolutionNodes[0].potencyMultiplier,
                    1.375f)
                && restored.moduleSlots.Count == 1
                && restored.moduleSlots[0].moduleInstanceId == "item-instance:module:test-v18",
            "The physical equipment component lost mutable equipment state.");
        Require(EquipmentItemStateCodec.TryDecodeFull(
                encoded,
                out EquipmentPhysicalStatePayload fullState,
                out error)
                && fullState.attachedModules.Count == 1
                && fullState.attachedModules[0].instanceId == attachedModule.instanceId
                && Mathf.Approximately(
                    fullState.attachedModules[0].condition,
                    attachedModule.condition),
            "The physical equipment component lost attached module state.");

        ItemInstanceComponentSaveData stale = encoded.Clone();
        stale.schemaVersion = 1;
        Require(!EquipmentItemStateCodec.TryDecode(stale, out _, out _),
            "Legacy partial equipment components must not be accepted as V18 authority.");

        ItemInstanceComponentSaveData legacyV3 = encoded.Clone();
        legacyV3.schemaVersion = 3;
        Require(EquipmentItemStateCodec.TryDecode(
                legacyV3,
                out CombatEquipmentInstance restoredLegacy,
                out error),
            error);
        Require(restoredLegacy.evolution.evolutionNodes.Count == 1
                && restoredLegacy.evolution.evolutionNodes[0].formulaVersion == 0
                && restoredLegacy.evolution.evolutionNodes[0].effectId == "equipment:force"
                && Mathf.Approximately(
                    restoredLegacy.evolution.evolutionNodes[0].potencyMultiplier,
                    1.375f),
            "V3 legacy fixed mechanics changed while migrating to formulaVersion 0.");

        EquipmentEvolutionFormulaCatalogSO formulaCatalog =
            EquipmentEvolutionFormulaCatalogSO.LoadRequired();
        Require(formulaCatalog.formulaPolicy.formulaVersion
                    == EquipmentEvolutionRules.CurrentFormulaVersion
                && string.Equals(
                    formulaCatalog.formulaPolicy.RequireCatalogSha256(),
                    "a582e7b62a3c53b539fc93e02438e1cb5b92b765a36ab7489a6932a5278a01a7",
                    StringComparison.Ordinal)
                && formulaCatalog.capabilities.Any(value =>
                    string.Equals(value.capabilityId, "equipment:reinforced-durability",
                        StringComparison.Ordinal)),
            "Current equipment formula authority did not expose the V4 pure durability catalog.");
        CombatEquipmentDefinitionSO[] equipmentDefinitions =
            Resources.LoadAll<CombatEquipmentDefinitionSO>("SO/Combat/Equipment");
        CombatEquipmentDefinitionSO meleeDefinition = equipmentDefinitions
            .FirstOrDefault(value => value != null
                && value.Kind == CombatEquipmentKind.MeleeWeapon);
        CombatEquipmentDefinitionSO armorDefinition = equipmentDefinitions
            .FirstOrDefault(value => value != null
                && value.Kind == CombatEquipmentKind.Armor);
        CombatEquipmentDefinitionSO shieldDefinition = equipmentDefinitions
            .FirstOrDefault(value => value != null
                && value.Kind == CombatEquipmentKind.Shield);
        Require(meleeDefinition != null && armorDefinition != null && shieldDefinition != null,
            "Equipment formula applicability fixtures require catalog melee, armor, and shield definitions.");
        object meleeTarget = CreateFormulaTargetContext(meleeDefinition);
        object armorTarget = CreateFormulaTargetContext(armorDefinition);
        object shieldTarget = CreateFormulaTargetContext(shieldDefinition);
        Require(InvokeInternal<bool>(
                "HasRuntimeApplicablePositiveOffer", formulaCatalog, meleeTarget)
                && InvokeInternal<bool>(
                    "HasRuntimeApplicablePositiveOffer", formulaCatalog, armorTarget)
                && InvokeInternal<bool>(
                    "HasRuntimeApplicablePositiveOffer", formulaCatalog, shieldTarget),
            "Current equipment formula offers did not retain real weapon, armor, and shield consumers.");
        EquipmentEvolutionState formulaState = new();
        for (int index = 0; index < 12; index++)
        {
            string evidenceId = $"equipment-evidence:formula-budget:{index:D2}";
            string eventId = $"equipment-event:formula-budget:{index:D2}";
            formulaState.formulaEvidence.Add(new EquipmentEvolutionFormulaEvidenceRecord
            {
                evidenceId = evidenceId,
                eventGroupKey = eventId,
                actionKey = eventId,
                relationshipKey = $"character:test-owner>target:{index:D2}",
                domainKey = "equipment",
                attainedMilestoneCount = formulaCatalog.formulaPolicy.milestoneWeights.Count,
                importancePoints = formulaCatalog.formulaPolicy.maximumImportance,
                influenceUseCount = 0,
                originalEvent = new UsageLedgerEvent
                {
                    evidenceId = evidenceId,
                    eventId = eventId,
                    actorId = "character:test-owner",
                    targetId = $"target:{index:D2}",
                    amount = 1f,
                    historicalEvidenceKind = HistoricalEvidenceKind.BossExecution,
                    outcomeId = $"outcome:{index:D2}",
                    generation = 0,
                    repeatCount = 50,
                    sequence = index + 1L
                }
            });
        }
        EvolutionNode formulaNode = InvokeInternal<EvolutionNode>(
            "BuildFormulaNode",
            formulaState,
            formulaCatalog,
            meleeTarget,
            "item-instance:test-formula-budget",
            "equipment-node:test-formula-budget",
            string.Empty,
            "history:test-formula-budget",
            EquipmentEvolutionDirection.Melee,
            string.Empty,
            0,
            true,
            "attunement:character:test-owner:1");
        Require(formulaNode.moduleSelectionOffers
                .Where(value => value.polarity == EvolutionModuleOfferPolarity.Positive)
                .Select(value => value.moduleId)
                .Contains("equipment:force", StringComparer.Ordinal)
                && !formulaNode.moduleSelectionOffers.Any(value =>
                    value.polarity == EvolutionModuleOfferPolarity.Positive
                    && (string.Equals(value.moduleId, "equipment:durability", StringComparison.Ordinal)
                        || string.Equals(value.moduleId, "equipment:reinforced-durability",
                            StringComparison.Ordinal))),
            "Weapon formula offers retained a durability effect without a weapon durability consumer.");
        EquipmentEvolutionState armorFormulaState = formulaState.Clone();
        EvolutionNode armorFormulaNode = InvokeInternal<EvolutionNode>(
            "BuildFormulaNode",
            armorFormulaState,
            formulaCatalog,
            armorTarget,
            "item-instance:test-formula-armor",
            "equipment-node:test-formula-armor",
            string.Empty,
            "history:test-formula-armor",
            EquipmentEvolutionDirection.Protection,
            string.Empty,
            0,
            true,
            "attunement:character:test-owner:1");
        EquipmentEvolutionState shieldFormulaState = formulaState.Clone();
        EvolutionNode shieldFormulaNode = InvokeInternal<EvolutionNode>(
            "BuildFormulaNode",
            shieldFormulaState,
            formulaCatalog,
            shieldTarget,
            "item-instance:test-formula-shield",
            "equipment-node:test-formula-shield",
            string.Empty,
            "history:test-formula-shield",
            EquipmentEvolutionDirection.Protection,
            string.Empty,
            0,
            true,
            "attunement:character:test-owner:1");
        Require(armorFormulaNode.moduleSelectionOffers
                    .Where(value => value.polarity == EvolutionModuleOfferPolarity.Positive)
                    .Select(value => value.moduleId)
                    .SequenceEqual(new[] { "equipment:reinforced-durability" },
                        StringComparer.Ordinal)
                && shieldFormulaNode.moduleSelectionOffers
                    .Where(value => value.polarity == EvolutionModuleOfferPolarity.Positive)
                    .Select(value => value.moduleId)
                    .SequenceEqual(new[] { "equipment:reinforced-durability" },
                        StringComparer.Ordinal),
            "Armor or shield did not receive exactly the pure durability capability.");
        EvolutionNode frozenArmorFormulaNode = InvokeInternal<EvolutionNode>(
            "FreezeSelectedModule",
            armorFormulaNode,
            armorFormulaState,
            formulaCatalog,
            "item-instance:test-formula-armor",
            new NarrativeFormulaModuleSelectionChoice(
                armorFormulaNode.moduleSelectionId,
                new[] { "equipment:reinforced-durability" },
                Array.Empty<string>(),
                armorFormulaNode.evidenceIds));
        Require(frozenArmorFormulaNode.positiveCost > 0
                && frozenArmorFormulaNode.drawbackCredit == 0
                && frozenArmorFormulaNode.calculatedCost
                    == frozenArmorFormulaNode.positiveCost
                && string.IsNullOrEmpty(frozenArmorFormulaNode.burdenEffectId)
                && frozenArmorFormulaNode.burdenPotencyMultiplier == 0f
                && frozenArmorFormulaNode.mechanicalDescription.Contains(
                    "추가 부담 없음", StringComparison.Ordinal),
            "Pure armor durability created a drawback or budget credit without a real burden.");
        VerifyLegacyPendingOfferSemantics(
            armorFormulaNode, armorFormulaState, formulaCatalog, frozenArmorFormulaNode,
            armorDefinition.EquipmentId);

        EquipmentEvolutionState constrainedArmorDrawbackState = formulaState.Clone();
        constrainedArmorDrawbackState.formulaEvidence.RemoveRange(
            1, constrainedArmorDrawbackState.formulaEvidence.Count - 1);
        MarkArmorDrawbackEvidence(constrainedArmorDrawbackState, negativeEvidence: false);
        EvolutionNode constrainedArmorDrawbackNode = InvokeInternal<EvolutionNode>(
            "BuildFormulaNode",
            constrainedArmorDrawbackState,
            formulaCatalog,
            armorTarget,
            "item-instance:test-formula-armor-constrained",
            "equipment-node:test-formula-armor-constrained",
            string.Empty,
            "history:test-formula-armor-constrained",
            EquipmentEvolutionDirection.Protection,
            string.Empty,
            0,
            true,
            "attunement:character:test-owner:1");
        EvolutionModuleDefinition heavyDrawback = new EvolutionModuleRegistry().All.Single(value =>
            string.Equals(value.ModuleId, "equipment:drawback-heavy", StringComparison.Ordinal));
        EvolutionNode constrainedCounterfactual = WithUniqueDrawbackOffer(
            constrainedArmorDrawbackNode, heavyDrawback, formulaCatalog);
        bool constrainedPairResolves = TryFreezeSelectedPair(
            constrainedCounterfactual,
            constrainedArmorDrawbackState,
            formulaCatalog,
            "item-instance:test-formula-armor-constrained",
            "equipment:reinforced-durability",
            heavyDrawback.ModuleId,
            constrainedCounterfactual.evidenceIds);
        bool constrainedOffered = constrainedArmorDrawbackNode.moduleSelectionOffers.Any(value =>
            value.polarity == EvolutionModuleOfferPolarity.Drawback
            && string.Equals(value.moduleId, heavyDrawback.ModuleId, StringComparison.Ordinal));
        Require(!constrainedPairResolves && constrainedOffered == constrainedPairResolves,
            "Generated drawback presence diverged from the actual constrained freeze oracle.");
        EvolutionNode frozenConstrainedArmor = InvokeInternal<EvolutionNode>(
            "FreezeSelectedModule",
            constrainedArmorDrawbackNode,
            constrainedArmorDrawbackState,
            formulaCatalog,
            "item-instance:test-formula-armor-constrained",
            new NarrativeFormulaModuleSelectionChoice(
                constrainedArmorDrawbackNode.moduleSelectionId,
                new[] { "equipment:reinforced-durability" },
                Array.Empty<string>(),
                constrainedArmorDrawbackNode.evidenceIds));
        Require(frozenConstrainedArmor.positiveCost > 0
                && frozenConstrainedArmor.drawbackCredit == 0,
            "Omitting an infeasible burden also removed the legal positive-only armor selection.");

        EquipmentEvolutionState feasibleArmorDrawbackState = formulaState.Clone();
        feasibleArmorDrawbackState.formulaEvidence.RemoveRange(
            3, feasibleArmorDrawbackState.formulaEvidence.Count - 3);
        MarkArmorDrawbackEvidence(feasibleArmorDrawbackState, negativeEvidence: true);
        EvolutionNode feasibleArmorDrawbackNode = InvokeInternal<EvolutionNode>(
            "BuildFormulaNode",
            feasibleArmorDrawbackState,
            formulaCatalog,
            armorTarget,
            "item-instance:test-formula-armor-feasible",
            "equipment-node:test-formula-armor-feasible",
            string.Empty,
            "history:test-formula-armor-feasible",
            EquipmentEvolutionDirection.Protection,
            string.Empty,
            0,
            true,
            "attunement:character:test-owner:1");
        EvolutionNode feasibleCounterfactual = WithUniqueDrawbackOffer(
            feasibleArmorDrawbackNode, heavyDrawback, formulaCatalog);
        bool feasiblePairResolves = TryFreezeSelectedPair(
            feasibleCounterfactual,
            feasibleArmorDrawbackState,
            formulaCatalog,
            "item-instance:test-formula-armor-feasible",
            "equipment:reinforced-durability",
            heavyDrawback.ModuleId,
            feasibleCounterfactual.evidenceIds);
        bool feasibleOffered = feasibleArmorDrawbackNode.moduleSelectionOffers.Any(value =>
            value.polarity == EvolutionModuleOfferPolarity.Drawback
            && string.Equals(value.moduleId, heavyDrawback.ModuleId, StringComparison.Ordinal));
        Require(feasiblePairResolves && feasibleOffered == feasiblePairResolves,
            "Generated drawback presence diverged from the actual feasible freeze oracle.");
        RequireEveryOfferedEquipmentSelectionResolves(
            feasibleArmorDrawbackNode,
            feasibleArmorDrawbackState,
            formulaCatalog,
            "item-instance:test-formula-armor-feasible");

        EquipmentRuntimeProbe armorAttunementProbe = CreateRuntimeProbe(
            "item-instance:test-attunement-armor", armorDefinition);
        EquipmentEvolutionRuntime armorAttunementRuntime =
            CreateEvolutionRuntimeForPresentation(armorAttunementProbe.Proxy);
        EquipmentEvolutionState armorAtThreshold = armorAttunementRuntime.RecordUsage(
            armorAttunementProbe.Instance.instanceId,
            "combat:absorb",
            mastery: 2f,
            amount: 4f,
            ownerPersistentId: "character:test-owner",
            attunementPoints: 30,
            sourceTags: new[] { "armor", "defense" },
            historicalEvidenceKind: HistoricalEvidenceKind.ProtectedOwner,
            outcomeId: "absorbed");
        AttunementRecord armorAtThresholdRecord = armorAtThreshold.attunements.Single();
        Require(armorAtThresholdRecord.affinityScore == 30
                && armorAtThresholdRecord.attainedTier == 1
                && armorAtThreshold.evolutionNodes.Count == 1
                && armorAtThreshold.presentationRequests.Count == 1
                && armorAtThreshold.formulaEvidence.Count == 1
                && armorAtThreshold.formulaEvidence.All(value =>
                    value.influenceUseCount == 0)
                && Mathf.Approximately(armorAtThreshold.mastery, 2f)
                && armorAtThreshold.usageLedger.currentGenerationEvents.Count == 1
                && string.IsNullOrEmpty(armorAttunementRuntime
                    .LastAttunementNodeGenerationFailureForDiagnostics),
            "Armor attunement did not create its lawful pure-durability node at threshold.");
        Require(armorAttunementRuntime.TryRecordUsage(
                armorAttunementProbe.Instance.instanceId,
                "combat:absorb",
                mastery: 3f,
                amount: 5f,
                ownerPersistentId: "character:test-owner",
                attunementPoints: 1,
                sourceTags: new[] { "armor", "defense" },
                historicalEvidenceKind: HistoricalEvidenceKind.ProtectedOwner,
                outcomeId: "absorbed"),
            "Armor follow-up usage unexpectedly failed.");
        EquipmentEvolutionState armorAfterThreshold = armorAttunementProbe.Instance.evolution;
        Require(armorAfterThreshold.attunements.Single().affinityScore == 31
                && armorAfterThreshold.attunements.Single().attainedTier == 1
                && armorAfterThreshold.evolutionNodes.Count == 1
                && armorAfterThreshold.formulaEvidence.Count == 2
                && armorAfterThreshold.formulaEvidence.All(value =>
                    value.influenceUseCount == 0)
                && Mathf.Approximately(armorAfterThreshold.mastery, 5f)
                && armorAfterThreshold.usageLedger.currentGenerationEvents.Count == 2
                && string.IsNullOrEmpty(armorAttunementRuntime
                    .LastAttunementNodeGenerationFailureForDiagnostics),
            "Armor follow-up usage changed the pure-durability node authority or lost affinity.");

        EquipmentRuntimeProbe meleeAttunementProbe = CreateRuntimeProbe(
            "item-instance:test-attunement-melee", meleeDefinition);
        EquipmentEvolutionRuntime meleeAttunementRuntime =
            CreateEvolutionRuntimeForPresentation(meleeAttunementProbe.Proxy);
        EquipmentEvolutionState meleeAtThreshold = meleeAttunementRuntime.RecordUsage(
            meleeAttunementProbe.Instance.instanceId,
            "combat:hit",
            mastery: 2f,
            amount: 4f,
            ownerPersistentId: "character:test-owner",
            attunementPoints: 30,
            sourceTags: new[] { "melee" },
            historicalEvidenceKind: HistoricalEvidenceKind.None,
            outcomeId: "hit");
        Require(meleeAtThreshold.attunements.Single().affinityScore == 30
                && meleeAtThreshold.attunements.Single().attainedTier == 1
                && meleeAtThreshold.evolutionNodes.Count == 1
                && meleeAtThreshold.presentationRequests.Count == 1
                && meleeAtThreshold.formulaEvidence.Count == 1
                && meleeAtThreshold.formulaEvidence.All(value =>
                    value.influenceUseCount == 0)
                && string.IsNullOrEmpty(meleeAttunementRuntime
                    .LastAttunementNodeGenerationFailureForDiagnostics),
            "Runtime-applicable weapon attunement did not create its lawful first node.");
        formulaState.evolutionNodes.Add(formulaNode);
        formulaState.presentationRequests.Add(
            InvokeInternal<EquipmentEvolutionPresentationRequest>(
                "BuildPresentationRequest",
                formulaNode,
                "item-instance:test-formula-budget",
                "history:test-formula-budget",
                "character:test-owner",
                string.Empty,
                0,
                0f));
        formulaState.attunements.Add(new AttunementRecord
        {
            ownerPersistentId = "character:test-owner",
            affinityScore = 50,
            attainedTier = 1,
            startedGeneration = 0
        });
        Require(formulaNode.formulaBudget > formulaState.ResonanceBudget,
            "Equipment formula budget was still capped by generation resonance.");
        Require(formulaNode.presentationState
                == EquipmentEvolutionPresentationState.ModuleSelectionPending
                && formulaNode.formulaCapabilities.Count == 0
                && formulaNode.calculatedCost == 0
                && formulaNode.moduleSelectionOffers.Count > 1,
            "Equipment formula v2 froze mechanics before module selection.");
        InvokeInternal<object>("ValidateFormulaState", formulaState);

        CombatEquipmentInstance formulaEquipment = new()
        {
            instanceId = "item-instance:test-formula-budget",
            definitionId = meleeDefinition.EquipmentId,
            materialId = "material:test-steel",
            evolution = formulaState
        };
        ItemInstanceComponentSaveData formulaEncoded =
            EquipmentItemStateCodec.Encode(formulaEquipment);
        Require(EquipmentItemStateCodec.TryDecode(
                formulaEncoded,
                out CombatEquipmentInstance restoredFormula,
                out error),
            error);
        EvolutionNode restoredFormulaNode = restoredFormula.evolution.evolutionNodes[0];
        Require(restoredFormulaNode.formulaBudget == formulaNode.formulaBudget
                && restoredFormulaNode.calculatedCost == formulaNode.calculatedCost,
            "Equipment formula budget did not survive the physical item save round trip.");

        EquipmentEvolutionFormulaCatalogSO legacyV3FormulaCatalog =
            EquipmentEvolutionFormulaCatalogSO.LoadForPersistedNode(
                EquipmentEvolutionRules.DrawbackModuleSelectionFormulaVersion);
        Require(legacyV3FormulaCatalog.formulaPolicy.formulaVersion
                    == EquipmentEvolutionRules.DrawbackModuleSelectionFormulaVersion
                && string.Equals(
                    legacyV3FormulaCatalog.formulaPolicy.RequireCatalogSha256(),
                    "d17ebf4c8d8e5cb7f558dc65c9f454292354807a5d68097e171992aadbbacd7d",
                    StringComparison.Ordinal)
                && !legacyV3FormulaCatalog.capabilities.Any(value =>
                    string.Equals(value.capabilityId, "equipment:reinforced-durability",
                        StringComparison.Ordinal)),
            "V3 formula authority was not preserved as an explicit frozen catalog.");
        EquipmentEvolutionState legacyV3FormulaState = formulaState.Clone();
        legacyV3FormulaState.evolutionNodes.Clear();
        legacyV3FormulaState.presentationRequests.Clear();
        legacyV3FormulaState.attunements.Clear();
        legacyV3FormulaState.activeHistoricalNodeIds.Clear();
        EvolutionNode legacyV3Pending = InvokeInternal<EvolutionNode>(
            "BuildFormulaNode",
            legacyV3FormulaState,
            legacyV3FormulaCatalog,
            meleeTarget,
            "item-instance:test-formula-v3",
            "equipment-node:test-formula-v3",
            string.Empty,
            "history:test-formula-v3",
            EquipmentEvolutionDirection.Melee,
            string.Empty,
            0,
            false,
            "reforge:0");
        EvolutionNode legacyV3Node = InvokeInternal<EvolutionNode>(
            "FreezeSelectedModule",
            legacyV3Pending,
            legacyV3FormulaState,
            legacyV3FormulaCatalog,
            "item-instance:test-formula-v3",
            new NarrativeFormulaModuleSelectionChoice(
                legacyV3Pending.moduleSelectionId,
                new[] { "equipment:force" },
                Array.Empty<string>(),
                legacyV3Pending.evidenceIds));
        legacyV3Node.active = true;
        legacyV3Node.mechanicallyUnlocked = true;
        legacyV3Node.narrativeReady = true;
        legacyV3Node.uiVisible = true;
        legacyV3Node.playerVisible = true;
        legacyV3Node.presentationState = EquipmentEvolutionPresentationState.Ready;
        legacyV3Node.displayName = "벼린 역습";
        legacyV3Node.narrativeFlavor = "여러 차례 겨룬 감각이 날끝에 고르게 남았다.";
        legacyV3Node.description = legacyV3Node.narrativeFlavor;
        foreach (EquipmentEvolutionFormulaEvidenceRecord evidence in
                 legacyV3FormulaState.formulaEvidence.Where(value =>
                     legacyV3Node.evidenceIds.Contains(value.evidenceId,
                         StringComparer.Ordinal)))
            evidence.influenceUseCount = checked(evidence.influenceUseCount + 1);
        legacyV3FormulaState.evolutionNodes.Add(legacyV3Node);
        CombatEquipmentInstance legacyV3FormulaEquipment = formulaEquipment.Clone();
        legacyV3FormulaEquipment.instanceId = "item-instance:test-formula-v3";
        legacyV3FormulaEquipment.evolution = legacyV3FormulaState;
        ItemInstanceComponentSaveData legacyV3FormulaEncoded =
            EquipmentItemStateCodec.Encode(legacyV3FormulaEquipment);
        Require(EquipmentItemStateCodec.TryDecode(
                legacyV3FormulaEncoded,
                out CombatEquipmentInstance restoredLegacyV3Formula,
                out error)
                && restoredLegacyV3Formula.evolution.evolutionNodes.Count == 1
                && restoredLegacyV3Formula.evolution.evolutionNodes[0].formulaVersion
                    == EquipmentEvolutionRules.DrawbackModuleSelectionFormulaVersion
                && string.Equals(
                    restoredLegacyV3Formula.evolution.evolutionNodes[0].formulaCatalogSha256,
                    legacyV3FormulaCatalog.formulaPolicy.RequireCatalogSha256(),
                    StringComparison.Ordinal)
                && restoredLegacyV3Formula.evolution.evolutionNodes[0].formulaCapabilities
                    .Single().capabilityId == "equipment:force",
            "Committed V3 equipment formula state did not restore through its frozen catalog authority.");

        ICombatEquipmentRuntime equipmentPort =
            DispatchProxy.Create<ICombatEquipmentRuntime, EquipmentRuntimeProbe>();
        EquipmentRuntimeProbe probe = (EquipmentRuntimeProbe)(object)equipmentPort;
        probe.Instance = formulaEquipment.Clone();
        EquipmentEvolutionRuntime evolutionRuntime =
            CreateEvolutionRuntimeForPresentation(equipmentPort);
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            Require(evolutionRuntime.TryRegisterPresentationFailure(
                    probe.Instance.instanceId,
                    formulaNode.presentationId,
                    $"test-failure-{attempt}",
                    out bool awaitingNarrativeRetry),
                "Equipment presentation failure could not be recorded.");
            Require(awaitingNarrativeRetry == (attempt == 5),
                "Equipment presentation entered retry state at the wrong attempt.");
            EquipmentEvolutionState failedState = probe.Instance.evolution;
            EvolutionNode failedNode = failedState.evolutionNodes[0];
            Require(!failedNode.active
                    && !failedNode.mechanicallyUnlocked
                    && !failedNode.narrativeReady
                    && !failedNode.uiVisible
                    && !failedNode.playerVisible
                    && failedState.formulaEvidence.All(value => value.influenceUseCount == 0),
                "Equipment mechanics or narrative influence were consumed after a failed presentation.");
        }
        Require(probe.Instance.evolution.presentationRequests[0].state
                == EquipmentEvolutionPresentationState.AwaitingNarrativeRetry,
            "Five equipment presentation failures did not persist AwaitingNarrativeRetry.");
        Require(evolutionRuntime.TryResumePresentation(
                probe.Instance.instanceId,
                formulaNode.presentationId),
            "Equipment presentation retry could not be resumed.");

        probe.UpdateSucceeds = false;
        EquipmentEvolutionModuleSelectionResponseDto selectionResponse = new()
        {
            selectionId = formulaNode.moduleSelectionId,
            positiveModuleIds = new List<string>
            {
                formulaNode.moduleSelectionOffers[0].moduleId
            },
            drawbackModuleIds = new List<string>(),
            evidenceFactIds = formulaNode.evidenceIds.ToList(),
            displayName = "끝내 벼린 칼날",
            narrativeFlavor = "오래 쌓인 결전의 기억이 날을 더 매섭게 벼렸다."
        };
        string selectionJson = JsonUtility.ToJson(selectionResponse);
        Require(selectionResponse.Validate(out string selectionSchemaError)
                && NarrativeExactKeyContract.TryValidateProfileResponse(
                    LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                    selectionJson, out _, out selectionSchemaError),
            selectionSchemaError);
        Require(!evolutionRuntime.TryCommitModuleSelection(
                probe.Instance.instanceId,
                selectionResponse,
                out _),
            "Equipment module-selection commit unexpectedly succeeded when state publication failed.");
        Require(probe.Instance.evolution.formulaEvidence.All(
                    value => value.influenceUseCount == 0)
                && probe.Instance.evolution.evolutionNodes[0].presentationState
                    == EquipmentEvolutionPresentationState.ModuleSelectionPending,
            "Rejected equipment state publication leaked mechanics or narrative influence.");

        probe.UpdateSucceeds = true;
        Require(evolutionRuntime.TryCommitModuleSelection(
                probe.Instance.instanceId,
                selectionResponse,
                out string presentationFailure),
            presentationFailure);
        EquipmentEvolutionState committedState = probe.Instance.evolution;
        EvolutionNode committedNode = committedState.evolutionNodes[0];
        Require(committedNode.active
                && committedNode.mechanicallyUnlocked
                && committedNode.narrativeReady
                && committedNode.uiVisible
                && committedNode.playerVisible
                && committedNode.presentationState == EquipmentEvolutionPresentationState.Ready
                && committedState.presentationRequests.Count == 0
                && committedState.formulaEvidence.All(value => value.influenceUseCount == 1),
            "Equipment presentation did not atomically publish mechanics and narrative influence once.");

        CombatEquipmentInstance overBudgetEquipment = probe.Instance.Clone();
        overBudgetEquipment.evolution.evolutionNodes[0].formulaBudget =
            Mathf.Max(0, overBudgetEquipment.evolution.evolutionNodes[0].calculatedCost - 1);
        ItemInstanceComponentSaveData overBudgetEncoded =
            EquipmentItemStateCodec.Encode(overBudgetEquipment);
        Require(!EquipmentItemStateCodec.TryDecode(overBudgetEncoded, out _, out _),
            "Equipment save validation accepted a formula allocation above its frozen budget.");

        string restoreSource = File.ReadAllText(
            "Assets/Scripts/Services/Items/WorldItemPersistenceService.cs");
        int exactFormulaValidation = restoreSource.IndexOf(
            "EquipmentEvolutionRules.ValidateFormulaState(",
            StringComparison.Ordinal);
        int equipmentPublication = restoreSource.IndexOf(
            "equipment.Add(unique.itemInstanceId, payload.equipment.Clone());",
            StringComparison.Ordinal);
        Require(exactFormulaValidation >= 0
                && equipmentPublication > exactFormulaValidation,
            "Physical equipment restore must validate exact formula state before publication.");

        Debug.Log(
            "V18 EQUIPMENT ITEM STATE PASS: full mutable equipment state round-tripped "
            + "through physical item component schema V4; V3 legacy fixed mechanics and "
            + "explicit frozen catalog restoration remained intact; narrative formula budgets "
            + "are no longer capped by generation resonance and round-trip exactly; current "
            + "armor/shield generation uses only the pure durability offer with no drawback credit.");
    }

    private static void VerifyLegacyPendingOfferSemantics(
        EvolutionNode current,
        EquipmentEvolutionState sourceState,
        EquipmentEvolutionFormulaCatalogSO catalog,
        EvolutionNode expectedFrozen,
        string equipmentDefinitionId)
    {
        // Exact pre-Phase78 persisted text, intentionally independent of the new
        // semantic producer. Compatibility must not accept merely similar text.
        const string legacyText = "보강 내구 각인: combat.durability ×1.12; 추가 부담 없음";
        EvolutionNode legacy = current.Clone();
        EquipmentEvolutionModuleOfferState legacyOffer = legacy.moduleSelectionOffers.Single(value =>
            string.Equals(value.moduleId, "equipment:reinforced-durability", StringComparison.Ordinal));
        legacyOffer.semanticDescription = legacyText;
        string beforePending = JsonUtility.ToJson(legacy);
        string beforeState = JsonUtility.ToJson(sourceState);
        NarrativeFormulaModuleSelectionRequest request =
            InvokeInternal<NarrativeFormulaModuleSelectionRequest>(
                "BuildModuleSelectionRequest", legacy, catalog);
        Require(request.Offers.All(value =>
                !System.Text.RegularExpressions.Regex.IsMatch(value.SemanticDescription, "[0-9０-９%％]")),
            "Legacy equipment pending prose leaked numerical baselines into a new model request.");
        Require(string.Equals(beforePending, JsonUtility.ToJson(legacy), StringComparison.Ordinal)
                && string.Equals(beforeState, JsonUtility.ToJson(sourceState), StringComparison.Ordinal),
            "Refreshing legacy request semantics mutated pending state, costs or influence.");
        EquipmentEvolutionState saveState = sourceState.Clone();
        saveState.evolutionNodes.Clear();
        saveState.presentationRequests.Clear();
        saveState.evolutionNodes.Add(legacy.Clone());
        saveState.presentationRequests.Add(InvokeInternal<EquipmentEvolutionPresentationRequest>(
            "BuildPresentationRequest", legacy, "item-instance:test-formula-armor",
            "history:test-formula-armor", "character:test-owner", string.Empty, 0, 0f));
        saveState.attunements.Add(new AttunementRecord
        {
            ownerPersistentId = "character:test-owner", affinityScore = 50,
            attainedTier = 1, startedGeneration = 0
        });
        CombatEquipmentInstance legacyEquipment = new()
        {
            instanceId = "item-instance:test-formula-armor", definitionId = equipmentDefinitionId,
            materialId = "material:test-steel", evolution = saveState
        };
        Require(EquipmentItemStateCodec.TryDecode(EquipmentItemStateCodec.Encode(legacyEquipment),
                out CombatEquipmentInstance restoredLegacyPending, out string saveError),
            "Known legacy pending offer invalidated an equipment save: " + saveError);
        Require(restoredLegacyPending.evolution.formulaEvidence.All(value => value.influenceUseCount == 0)
                && restoredLegacyPending.evolution.evolutionNodes.Single().calculatedCost == 0
                && restoredLegacyPending.evolution.evolutionNodes.Single().presentationState
                    == EquipmentEvolutionPresentationState.ModuleSelectionPending,
            "Legacy pending save restoration applied unconfirmed cost, effects or narrative influence.");
        EvolutionNode frozen = InvokeInternal<EvolutionNode>(
            "FreezeSelectedModule", legacy, sourceState.Clone(), catalog,
            "item-instance:test-formula-armor",
            new NarrativeFormulaModuleSelectionChoice(request.SelectionId,
                new[] { "equipment:reinforced-durability" }, Array.Empty<string>(),
                request.EvidenceFactIds));
        Require(frozen.formulaBudget == expectedFrozen.formulaBudget
                && frozen.calculatedCost == expectedFrozen.calculatedCost
                && frozen.positiveCost == expectedFrozen.positiveCost
                && frozen.drawbackCredit == expectedFrozen.drawbackCredit
                && frozen.mechanicalDescription == expectedFrozen.mechanicalDescription,
            "Legacy prose compatibility changed the final authoritative numerical allocation.");
        foreach (string tampered in new[] { legacyText.Replace("1.12", "1.13"), "임의로 바뀐 설명" })
        {
            EvolutionNode invalid = legacy.Clone();
            invalid.moduleSelectionOffers.Single(value =>
                string.Equals(value.moduleId, "equipment:reinforced-durability",
                    StringComparison.Ordinal)).semanticDescription = tampered;
            bool rejected = false;
            try
            {
                InvokeInternal<NarrativeFormulaModuleSelectionRequest>(
                    "BuildModuleSelectionRequest", invalid, catalog);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Require(rejected, "Legacy semantic compatibility accepted an unknown or modified offer.");
        }
    }

    private static void MarkArmorDrawbackEvidence(
        EquipmentEvolutionState state,
        bool negativeEvidence)
    {
        for (int index = 0; index < state.formulaEvidence.Count; index++)
        {
            EquipmentEvolutionFormulaEvidenceRecord evidence = state.formulaEvidence[index];
            string outcome = negativeEvidence ? "blocked" : "guarded";
            string eventId = $"combat:armor:{outcome}:{index:D2}";
            evidence.eventGroupKey = eventId;
            evidence.actionKey = eventId;
            evidence.relationshipKey = $"character:test-owner>target:armor:{index:D2}";
            evidence.domainKey = "equipment";
            evidence.originalEvent.eventId = eventId;
            evidence.originalEvent.outcomeId = outcome;
            evidence.originalEvent.historicalEvidenceKind =
                HistoricalEvidenceKind.ProtectedOwner;
        }
    }

    private static void RequireEveryOfferedEquipmentSelectionResolves(
        EvolutionNode pending,
        EquipmentEvolutionState state,
        EquipmentEvolutionFormulaCatalogSO catalog,
        string equipmentInstanceId)
    {
        NarrativeFormulaModuleSelectionRequest request =
            InvokeInternal<NarrativeFormulaModuleSelectionRequest>(
                "BuildModuleSelectionRequest", pending, catalog);
        NarrativeFormulaModuleOffer[] positives = request.Offers.Where(value =>
            value.Polarity == NarrativeFormulaModulePolarity.Positive).ToArray();
        NarrativeFormulaModuleOffer[] drawbacks = request.Offers.Where(value =>
            value.Polarity == NarrativeFormulaModulePolarity.Drawback).ToArray();
        Require(positives.Length > 0 && drawbacks.Length > 0,
            "The pair-feasibility fixture requires offered positive and drawback modules.");
        foreach (NarrativeFormulaModuleOffer positive in positives)
        {
            EvolutionNode positiveOnly = InvokeInternal<EvolutionNode>(
                "FreezeSelectedModule",
                pending,
                state.Clone(),
                catalog,
                equipmentInstanceId,
                new NarrativeFormulaModuleSelectionChoice(
                    request.SelectionId,
                    new[] { positive.ModuleId },
                    Array.Empty<string>(),
                    request.EvidenceFactIds));
            Require(positiveOnly.positiveCost > 0 && positiveOnly.drawbackCredit == 0,
                $"Offered positive equipment module '{positive.ModuleId}' did not resolve alone.");
            foreach (NarrativeFormulaModuleOffer drawback in drawbacks)
            {
                EvolutionModuleDefinition module = new EvolutionModuleRegistry().All.Single(value =>
                    string.Equals(value.ModuleId, drawback.ModuleId, StringComparison.Ordinal));
                string[] supportingEvidenceIds = state.formulaEvidence.Where(value => value != null
                        && request.EvidenceFactIds.Contains(value.evidenceId,
                            StringComparer.Ordinal)
                        && value.originalEvent != null
                        && module.MatchesNegativeEvidence(
                            value.originalEvent.eventId,
                            value.originalEvent.outcomeId,
                            value.originalEvent.historicalEvidenceKind.ToString()))
                    .Select(value => value.evidenceId)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                Require(supportingEvidenceIds.Length > 0,
                    $"Offered drawback '{drawback.ModuleId}' has no supporting evidence.");
                EvolutionNode paired = InvokeInternal<EvolutionNode>(
                    "FreezeSelectedModule",
                    pending,
                    state.Clone(),
                    catalog,
                    equipmentInstanceId,
                    new NarrativeFormulaModuleSelectionChoice(
                        request.SelectionId,
                        new[] { positive.ModuleId },
                        new[] { drawback.ModuleId },
                        supportingEvidenceIds));
                Require(paired.positiveCost > 0 && paired.drawbackCredit > 0,
                    $"Offered equipment pair '{positive.ModuleId}' + '{drawback.ModuleId}' "
                    + "did not resolve with its supporting evidence.");
            }
        }
    }

    private static EvolutionNode WithUniqueDrawbackOffer(
        EvolutionNode pending,
        EvolutionModuleDefinition drawback,
        EquipmentEvolutionFormulaCatalogSO catalog)
    {
        EvolutionNode counterfactual = pending.Clone();
        counterfactual.moduleSelectionOffers.RemoveAll(value => value != null
            && string.Equals(value.moduleId, drawback.ModuleId, StringComparison.Ordinal));
        counterfactual.moduleSelectionOffers.Add(new EquipmentEvolutionModuleOfferState
        {
            moduleId = drawback.ModuleId,
            polarity = EvolutionModuleOfferPolarity.Drawback,
            semanticDescription = InvokeInternal<string>(
                "DescribeModule", drawback, catalog)
        });
        return counterfactual;
    }

    private static bool TryFreezeSelectedPair(
        EvolutionNode pending,
        EquipmentEvolutionState state,
        EquipmentEvolutionFormulaCatalogSO catalog,
        string equipmentInstanceId,
        string positiveModuleId,
        string drawbackModuleId,
        IEnumerable<string> evidenceIds)
    {
        try
        {
            EvolutionNode frozen = InvokeInternal<EvolutionNode>(
                "FreezeSelectedModule",
                pending,
                state.Clone(),
                catalog,
                equipmentInstanceId,
                new NarrativeFormulaModuleSelectionChoice(
                    pending.moduleSelectionId,
                    new[] { positiveModuleId },
                    new[] { drawbackModuleId },
                    evidenceIds));
            return frozen != null;
        }
        catch (InvalidOperationException error) when (string.Equals(
            error.Message, "Selected equipment module is numerically infeasible.",
            StringComparison.Ordinal))
        {
            return false;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static T InvokeInternal<T>(string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(EquipmentEvolutionRules).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                typeof(EquipmentEvolutionRules).FullName,
                methodName);
        try
        {
            object result = method.Invoke(null, arguments);
            return result is T typed ? typed : default;
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            throw error.InnerException;
        }
    }

    private static object CreateFormulaTargetContext(
        CombatEquipmentDefinitionSO definition)
    {
        Type targetType = typeof(EquipmentEvolutionRules).Assembly.GetType(
            "EquipmentFormulaTargetContext")
            ?? throw new MissingMemberException(
                "EquipmentFormulaTargetContext was not found in the runtime assembly.");
        return Activator.CreateInstance(targetType, new object[] { definition })
            ?? throw new InvalidOperationException(
                "EquipmentFormulaTargetContext could not be constructed from a catalog definition.");
    }

    private static EquipmentEvolutionRuntime CreateEvolutionRuntimeForPresentation(
        ICombatEquipmentRuntime equipment)
    {
        EquipmentEvolutionRuntime runtime = (EquipmentEvolutionRuntime)
            FormatterServices.GetUninitializedObject(typeof(EquipmentEvolutionRuntime));
        FieldInfo equipmentField = typeof(EquipmentEvolutionRuntime).GetField(
            "equipment",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(
                typeof(EquipmentEvolutionRuntime).FullName,
                "equipment");
        equipmentField.SetValue(runtime, equipment);
        FieldInfo ledgerCompactorField = typeof(EquipmentEvolutionRuntime).GetField(
            "ledgerCompactor",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(
                typeof(EquipmentEvolutionRuntime).FullName,
                "ledgerCompactor");
        ledgerCompactorField.SetValue(runtime, new UsageLedgerCompactor());
        return runtime;
    }

    private static EquipmentRuntimeProbe CreateRuntimeProbe(
        string instanceId,
        CombatEquipmentDefinitionSO definition)
    {
        ICombatEquipmentRuntime port =
            DispatchProxy.Create<ICombatEquipmentRuntime, EquipmentRuntimeProbe>();
        EquipmentRuntimeProbe probe = (EquipmentRuntimeProbe)(object)port;
        probe.Definition = definition;
        probe.Instance = new CombatEquipmentInstance
        {
            instanceId = instanceId,
            definitionId = definition.EquipmentId,
            materialId = "material:test-steel",
            evolution = new EquipmentEvolutionState()
        };
        probe.Proxy = port;
        return probe;
    }

    public class EquipmentRuntimeProbe : DispatchProxy
    {
        public CombatEquipmentInstance Instance { get; set; }
        public CombatEquipmentDefinitionSO Definition { get; set; }
        public ICombatEquipmentRuntime Proxy { get; set; }
        public bool UpdateSucceeds { get; set; } = true;

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name == nameof(ICombatEquipmentRuntime.TryGetInstance))
            {
                bool found = Instance != null
                    && string.Equals(
                        arguments[0] as string,
                        Instance.instanceId,
                        StringComparison.Ordinal);
                arguments[1] = found ? Instance.Clone() : null;
                return found;
            }
            if (targetMethod.Name == nameof(ICombatEquipmentRuntime.TryGetDefinition))
            {
                bool found = Definition != null
                    && string.Equals(
                        arguments[0] as string,
                        Definition.EquipmentId,
                        StringComparison.Ordinal);
                arguments[1] = found ? Definition : null;
                return found;
            }
            if (targetMethod.Name == nameof(ICombatEquipmentRuntime.TryUpdateEvolutionState))
            {
                if (!UpdateSucceeds || Instance == null
                    || !string.Equals(
                        arguments[0] as string,
                        Instance.instanceId,
                        StringComparison.Ordinal))
                    return false;
                Instance.evolution =
                    ((EquipmentEvolutionState)arguments[1]).Clone();
                return true;
            }
            throw new NotSupportedException(
                $"Unexpected equipment runtime probe call '{targetMethod.Name}'.");
        }
    }
}
#endif
