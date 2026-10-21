#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;

public static class EquipmentItemStateV18DebugScenarios
{
    [MenuItem("DungeonStory/Debug/Combat/Run V18 Equipment Item State Contracts")]
    public static void RunAll() => Run(stopAfterModuleSelection: false);

    public static void RunF03ModuleSelectionBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException(
                "A new F03 equipment-evolution output directory is required.");
        Directory.CreateDirectory(output);
        try
        {
            Run(stopAfterModuleSelection: true);
            WriteF03Report(output, new F03Report
            {
                testId = "audit-f03-equipment-module-selection",
                passed = true,
                coverage = new[]
                {
                    "mechanical-vs-narrative-readiness",
                    "owner-publication-and-outcome-rollback",
                    "exact-selection-identity-and-before-after",
                    "same-node-retry-no-op",
                    "owner-and-ledger-restore-retry-no-op"
                }
            });
        }
        catch (Exception error)
        {
            WriteF03Report(output, new F03Report
            {
                testId = "audit-f03-equipment-module-selection",
                passed = false,
                failure = error.ToString()
            });
            throw;
        }
    }

    public static void RunModuleSelectionObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Artifacts",
            "QA")) + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode
            || string.IsNullOrWhiteSpace(output)
            || !Path.IsPathRooted(output)
            || !Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "Equipment module-selection observer checks require new project-local batch evidence.");
        }

        string probe = File.ReadAllText(Path.Combine(
            Directory.GetParent(Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
                && probe.Contains("visible=false")
                && probe.Contains("windowCreatedAndDestroyed=true")
                && probe.Contains("inJob=true"),
            "Equipment module-selection observer checks require the noninteractive desktop probe.");
        Run(stopAfterModuleSelection: true);
        Debug.Log("B88_EQUIPMENT_MODULE_SELECTION_OWNER=PASS");
        Require(LedgerB1NewOutcomeDebugScenarios.RunEquipmentObserverAdapterFocused(),
            "The current equipment observer adapter regression failed.");
        Debug.Log("B88_EQUIPMENT_MODULE_SELECTION_OBSERVER_FOCUSED=PASS");
    }

    private static void Run(bool stopAfterModuleSelection)
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

        EquipmentEvolutionFormulaCatalogs formulaCatalogs = new(
            CharacterAiEditorTestDependencies.ContentDefinitions);
        EquipmentEvolutionFormulaCatalogSO formulaCatalog =
            formulaCatalogs.RequireCurrent();
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
        for (int index = 0; index < 30; index++)
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
        Require(formulaState.formulaEvidence.Count == 30
                && formulaNode.evidenceIds.Count == NarrativeRequestContext.MaximumFacts
                && formulaNode.evidenceIds.SequenceEqual(
                    Enumerable.Range(6, NarrativeRequestContext.MaximumFacts)
                        .Select(index => $"equipment-evidence:formula-budget:{index:D2}"),
                    StringComparer.Ordinal),
            "Equipment formula did not retain the full ledger while freezing a deterministic bounded public evidence window.");
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
        VerifyFrozenPresentationDispatch(meleeAttunementProbe.Instance, meleeDefinition);
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
        InvokeInternal<object>("ValidateFormulaState", formulaState, formulaCatalogs);

        CombatEquipmentInstance formulaEquipment = new()
        {
            instanceId = "item-instance:test-formula-budget",
            definitionId = meleeDefinition.EquipmentId,
            materialId = "material:test-steel",
            evolution = formulaState
        };
        if (!stopAfterModuleSelection)
            EquipmentPresentationContextDebugScenarios.VerifyCodecAndBinding(formulaEquipment);
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
            formulaCatalogs.RequireForPersistedNode(
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
        GameplayOutcomeRegistry moduleOutcomeRegistry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new EquipmentModuleSelectionOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new EquipmentModuleSelectionOutcomeAdapter()
            });
        GameplayOutcomeLedger moduleOutcomeLedger = new(
            moduleOutcomeRegistry,
            new GameplayOutcomeBufferLimits(16, 0, 64),
            new GameplayOutcomeRunId("equipment-module-runtime-entry"),
            1L);
        GameplayOutcomeRecorder moduleOutcomeRecorder = new(
            moduleOutcomeLedger,
            moduleOutcomeRegistry,
            new GameEventBus());
        using DomainFactDispatcher moduleFacts = new();
        using IDisposable moduleBinding = EquipmentModuleSelectedFactMapping
            .CreateBinding(
                moduleOutcomeRegistry,
                moduleOutcomeLedger,
                moduleOutcomeRecorder,
                new GameplayDomainFactCommitGroup(
                    moduleOutcomeRecorder,
                    moduleOutcomeLedger))
            .Connect(moduleFacts);
        FaultingEquipmentModuleFacts faultingModuleFacts = new(moduleFacts);
        EquipmentModuleSelectionPublicationRuntime moduleSelections = new(
            faultingModuleFacts,
            faultingModuleFacts);
        EquipmentEvolutionRuntime evolutionRuntime =
            CreateEvolutionRuntimeForPresentation(equipmentPort, moduleSelections);
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

        EquipmentEvolutionState selectionStateBefore =
            probe.Instance.evolution.Clone();
        EquipmentEvolutionPresentationRequest selectionRequestBefore =
            selectionStateBefore.presentationRequests.Single();
        Require(string.IsNullOrEmpty(probe.Instance.ownerCharacterId)
                && selectionRequestBefore.targetPersistentId == probe.Instance.instanceId
                && selectionRequestBefore.nodeId == formulaNode.nodeId
                && selectionRequestBefore.presentationId == formulaNode.presentationId
                && selectionRequestBefore.attunementOwnerPersistentId == "character:test-owner"
                && string.IsNullOrEmpty(selectionRequestBefore.reforgeOrderId)
                && formulaNode.historical,
            "The F03 attunement fixture lost its exact owner/request context.");
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
        GameplayResultKey moduleOutcomeResultKey = new(
            EquipmentModuleSelectionOutcomeIds.ProducerId,
            new GameplayOperationId(
                "equipment-module:" + probe.Instance.instanceId + ":1"),
            1L,
            0);
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
                    == EquipmentEvolutionPresentationState.ModuleSelectionPending
                && probe.Instance.evolution.moduleSelectionRevision == 0L
                && !moduleOutcomeLedger.TryGetResultIdentity(
                    moduleOutcomeResultKey,
                    out _)
                && moduleSelections.Capture().observations.Count == 0,
            "Rejected equipment state publication leaked mechanics or narrative influence.");

        probe.UpdateSucceeds = true;
        faultingModuleFacts.RejectNextPublish = true;
        Require(!evolutionRuntime.TryCommitModuleSelection(
                probe.Instance.instanceId,
                selectionResponse,
                out string outcomeFailure)
            && outcomeFailure ==
                "editor-injected-equipment-outcome-commit-failure"
            && probe.Instance.evolution.formulaEvidence.All(
                value => value.influenceUseCount == 0)
            && probe.Instance.evolution.evolutionNodes[0].presentationState
                == EquipmentEvolutionPresentationState.ModuleSelectionPending
            && probe.Instance.evolution.moduleSelectionRevision == 0L
            && !moduleOutcomeLedger.TryGetResultIdentity(
                moduleOutcomeResultKey,
                out _)
            && moduleSelections.Capture().observations.Count == 0,
            "Rejected equipment outcome did not restore the published owner state.");
        faultingModuleFacts.HideNextCommittedAcknowledgement = true;
        Require(evolutionRuntime.TryCommitModuleSelection(
                probe.Instance.instanceId,
                selectionResponse,
                out string presentationFailure),
            presentationFailure);
        EquipmentModuleSelectionPublicationData pendingModuleSelection =
            moduleSelections.PendingForEquipment(probe.Instance.instanceId);
        EquipmentEvolutionState committedState = probe.Instance.evolution;
        EvolutionNode committedNode = committedState.evolutionNodes[0];
        bool hasModuleIdentity = moduleOutcomeLedger.TryGetResultIdentity(
            moduleOutcomeResultKey,
            out GameplayOutcomeReplayIdentity moduleIdentity);
        Require(pendingModuleSelection != null
                && pendingModuleSelection.pending
                && !string.IsNullOrWhiteSpace(
                    pendingModuleSelection.publicationKey)
                && string.IsNullOrEmpty(pendingModuleSelection.publicationId)
                && string.IsNullOrEmpty(pendingModuleSelection.fingerprint)
                && moduleSelections.LastFailure.Contains(
                    "equipment-module-selection-observation-pending",
                    StringComparison.Ordinal)
                && faultingModuleFacts.CommittedPublishCalls == 1
                && hasModuleIdentity,
            "Equipment module selection did not retain its frozen observation after a committed ACK loss.");
        Require(moduleOutcomeLedger.TryGetExact(
                moduleIdentity.OutcomeId,
                out GameplayOutcomeSnapshot moduleOutcome),
            "Equipment module selection has no exact outcome snapshot.");
        Require(committedNode.active
                && committedNode.mechanicallyUnlocked
                && committedNode.narrativeReady
                && committedNode.uiVisible
                && committedNode.playerVisible
                && committedNode.presentationState == EquipmentEvolutionPresentationState.Ready
                && committedState.presentationRequests.Count == 0
                && committedState.moduleSelectionRevision == 1L
                && string.Equals(
                     moduleOutcome.outcomeTypeId,
                     EquipmentModuleSelectionOutcomeIds.Selected.Value,
                     StringComparison.Ordinal),
            "Equipment presentation did not atomically publish mechanics and narrative influence once.");
        Require(committedState.formulaEvidence.All(value =>
                {
                    EquipmentEvolutionFormulaEvidenceRecord before =
                        selectionStateBefore.formulaEvidence.Single(candidate =>
                            string.Equals(
                                candidate.evidenceId,
                                value.evidenceId,
                                StringComparison.Ordinal));
                    int expectedDelta = committedNode.evidenceIds.Contains(
                        value.evidenceId,
                        StringComparer.Ordinal) ? 1 : 0;
                    return value.influenceUseCount
                        == before.influenceUseCount + expectedDelta;
                }),
            "Equipment module selection did not consume exactly its frozen evidence window once.");

        string selectedModuleId = selectionResponse.positiveModuleIds.Single();
        string selectedEffectId = committedNode.effectId;
        string selectedEvidenceId = committedNode.evidenceIds.First();
        Require(moduleOutcome.participants.Count == 2
                && moduleOutcome.participants.Any(value => string.Equals(
                        value.roleId,
                        EquipmentModuleSelectionOutcomeIds.SelectedNodeRole.Value,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.entityId,
                        committedNode.nodeId,
                        StringComparison.Ordinal))
                && moduleOutcome.participants.Any(value => string.Equals(
                        value.roleId,
                        EquipmentModuleSelectionOutcomeIds.EquipmentRole.Value,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.entityId,
                        probe.Instance.instanceId,
                        StringComparison.Ordinal))
                && HasMetric(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.RevisionBefore.Value,
                    0d)
                && HasMetric(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.RevisionAfter.Value,
                    1d)
                && HasMetric(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.GenerationBefore.Value,
                    selectionStateBefore.generation)
                && HasMetric(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.GenerationAfter.Value,
                    committedState.generation)
                && HasMetric(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.MasteryBefore.Value,
                    selectionStateBefore.mastery)
                && HasMetric(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.MasteryAfter.Value,
                    committedState.mastery)
                && HasFact(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.SelectionIdFact.Value,
                    selectionResponse.selectionId)
                && HasFact(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.NodeIdFact.Value,
                    committedNode.nodeId)
                && HasFact(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.EquipmentInstanceIdFact.Value,
                    probe.Instance.instanceId)
                && HasFact(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.EquipmentDefinitionIdFact.Value,
                    probe.Instance.definitionId)
                && HasFact(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.OwnershipFact.Value,
                    "0:20:character:test-owner")
                && HasFact(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.ContextFact.Value,
                    "0:4:true")
                && string.Equals(
                    moduleOutcome.operationId,
                    "equipment-module:item-instance:test-formula-budget:1",
                    StringComparison.Ordinal)
                && FactContains(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.ModulesFact.Value,
                    selectedModuleId)
                && FactContains(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.MechanicsFact.Value,
                    selectedEffectId)
                && FactContains(
                    moduleOutcome,
                    EquipmentModuleSelectionOutcomeIds.MechanicsFact.Value,
                    selectedEvidenceId),
            "Equipment module-selection exact outcome lost owner, request, module, effect, evidence, generation, or mastery identity.");

        ItemInstanceComponentSaveData committedComponent =
            EquipmentItemStateCodec.Encode(probe.Instance);
        Require(EquipmentItemStateCodec.TryDecode(
                committedComponent,
                out CombatEquipmentInstance restoredCommitted,
                out string committedRestoreFailure),
            committedRestoreFailure);
        GameplayOutcomeLedgerSaveData savedModuleOutcomes =
            moduleOutcomeLedger.CaptureGameplayOutcomes();
        EquipmentModuleSelectionPublicationSaveSection moduleSelectionSection =
            new(moduleSelections);
        string savedModuleSelectionJson = moduleSelectionSection.Capture();
        EquipmentModuleSelectionPublicationSaveSection.ValidateJson(
            savedModuleSelectionJson);
        EquipmentModuleSelectionPublicationSaveData savedModuleSelections =
            DungeonSaveJson.Deserialize<
                EquipmentModuleSelectionPublicationSaveData>(
                    savedModuleSelectionJson);
        GameplayOutcomeLedger restoredModuleOutcomeLedger = new(
            moduleOutcomeRegistry,
            new GameplayOutcomeBufferLimits(16, 0, 64),
            new GameplayOutcomeRunId("equipment-module-runtime-restore"),
            1L);
        restoredModuleOutcomeLedger.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate restoredLedgerCandidate =
            restoredModuleOutcomeLedger.PrepareGameplayOutcomeRestore(
                savedModuleOutcomes);
        restoredModuleOutcomeLedger.PublishGameplayOutcomeRestore(
            restoredLedgerCandidate);
        restoredModuleOutcomeLedger.PublishRestoreCandidate();
        restoredModuleOutcomeLedger.CompleteRestoreCandidate();
        ICombatEquipmentRuntime restoredEquipmentPort =
            DispatchProxy.Create<ICombatEquipmentRuntime, EquipmentRuntimeProbe>();
        EquipmentRuntimeProbe restoredProbe =
            (EquipmentRuntimeProbe)(object)restoredEquipmentPort;
        restoredProbe.Instance = restoredCommitted;
        GameplayOutcomeRecorder restoredModuleOutcomeRecorder = new(
            restoredModuleOutcomeLedger,
            moduleOutcomeRegistry,
            new GameEventBus());
        using DomainFactDispatcher restoredModuleFacts = new();
        using IDisposable restoredModuleBinding = EquipmentModuleSelectedFactMapping
            .CreateBinding(
                moduleOutcomeRegistry,
                restoredModuleOutcomeLedger,
                restoredModuleOutcomeRecorder,
                new GameplayDomainFactCommitGroup(
                    restoredModuleOutcomeRecorder,
                    restoredModuleOutcomeLedger))
            .Connect(restoredModuleFacts);
        FaultingEquipmentModuleFacts restoredFaultingFacts = new(
            restoredModuleFacts);
        EquipmentModuleSelectionPublicationRuntime restoredModuleSelections =
            new(restoredFaultingFacts, restoredFaultingFacts);
        restoredModuleSelections.Restore(savedModuleSelections);
        string restoredEquipmentBeforeRecovery = JsonUtility.ToJson(
            restoredProbe.Instance);
        string restoredLedgerBeforeRecovery = JsonUtility.ToJson(
            restoredModuleOutcomeLedger.CaptureGameplayOutcomes());
        bool moduleRecoverySucceeded = restoredModuleSelections.RecoverPending(
            out string moduleRecoveryFailure);
        EquipmentModuleSelectionPublicationData restoredObservation =
            restoredModuleSelections.Capture().observations.Single();
        Require(moduleRecoverySucceeded
                && !restoredObservation.pending
                && restoredObservation.fact.afterRevision == 1L
                && restoredObservation.fact.equipmentInstanceId
                    == restoredProbe.Instance.instanceId
                && !string.IsNullOrWhiteSpace(restoredObservation.publicationId)
                && restoredObservation.fingerprint?.Length == 64
                && restoredFaultingFacts.ReplayBatchCalls == 1
                && restoredEquipmentBeforeRecovery == JsonUtility.ToJson(
                    restoredProbe.Instance)
                && restoredLedgerBeforeRecovery == JsonUtility.ToJson(
                    restoredModuleOutcomeLedger.CaptureGameplayOutcomes()),
            "Equipment module-selection observation recovery changed owner effects, reused evidence, or duplicated its canonical result: "
            + moduleRecoveryFailure);
        VerifyEquipmentModuleObserverLifecycle(
            restoredObservation.fact,
            moduleOutcomeRegistry,
            restoredModuleOutcomeLedger,
            restoredModuleOutcomeRecorder);
        EquipmentEvolutionRuntime restoredEvolutionRuntime =
            CreateEvolutionRuntimeForPresentation(
                restoredEquipmentPort,
                restoredModuleSelections);
        string restoredEquipmentJson = JsonUtility.ToJson(restoredProbe.Instance);
        string restoredLedgerJson = JsonUtility.ToJson(
            restoredModuleOutcomeLedger.CaptureGameplayOutcomes());
        Require(restoredProbe.Instance.evolution.moduleSelectionRevision == 1L
                && restoredProbe.Instance.evolution.presentationRequests.Count == 0
                && restoredProbe.Instance.evolution.evolutionNodes.Single().active
                && restoredModuleOutcomeLedger.TryGetResultIdentity(
                    moduleOutcomeResultKey,
                    out GameplayOutcomeReplayIdentity restoredIdentity)
                && restoredModuleOutcomeLedger.TryGetExact(
                    restoredIdentity.OutcomeId,
                    out GameplayOutcomeSnapshot restoredModuleOutcome)
                && restoredModuleOutcome.immutablePayloadHash
                    == moduleOutcome.immutablePayloadHash
                && !restoredEvolutionRuntime.TryCommitModuleSelection(
                    restoredProbe.Instance.instanceId,
                    selectionResponse,
                    out string restoredDuplicateFailure)
                && restoredDuplicateFailure.Contains(
                    "이미 종료",
                    StringComparison.Ordinal)
                && restoredEquipmentJson == JsonUtility.ToJson(restoredProbe.Instance)
                && restoredLedgerJson == JsonUtility.ToJson(
                    restoredModuleOutcomeLedger.CaptureGameplayOutcomes()),
            "Restored terminal equipment selection changed or duplicated on replay.");

        if (stopAfterModuleSelection)
            return;

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

    private static void VerifyFrozenPresentationDispatch(
        CombatEquipmentInstance source, CombatEquipmentDefinitionSO definition)
    {
        GameObject actorRoot = CharacterAiPlanDebugFixtures.CreateActorObject("EquipmentFrozenContextParticipant");
        CharacterSO data = null;
        CombatEquipmentDefinitionSO localDefinition = null;
        try
        {
            data = CharacterAiPlanDebugFixtures.CreateCharacterData(CharacterType.NPC, "처음 지킨이", "human");
            CharacterActor actor = actorRoot.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId("character:test-owner");
            actor.Initialization(data);
            actor.Progression.GrowthState.displayName = "처음 지킨이";
            localDefinition = UnityEngine.Object.Instantiate(definition);
            FieldInfo nameField = typeof(CombatEquipmentDefinitionSO).GetField("displayName",
                BindingFlags.Instance | BindingFlags.NonPublic);
            nameField.SetValue(localDefinition, "오래 지킨 검");
            EquipmentRuntimeProbe probe = CreateRuntimeProbe(source.instanceId, localDefinition);
            probe.Instance = source.Clone();
            EquipmentEvolutionRuntime owner = CreateEvolutionRuntimeForPresentation(probe.Proxy);
            var world = new PresentationWorld(actor);
            var clock = new PresentationClock();
            var transport = new PresentationTransport();
            var runtime = new EvolutionHistoryNarrativeRuntime(world, probe.Proxy,
                new FacilityEvolutionStateComponentFactory(), transport, clock, owner);

            // A failed physical owner write must not leave a transient-only request in flight.
            string before = JsonUtility.ToJson(probe.Instance);
            probe.UpdateSucceeds = false;
            runtime.Tick();
            Require(transport.Prompts.Count == 0 && JsonUtility.ToJson(probe.Instance) == before,
                "Equipment context publication failure dispatched or partially changed its owner.");
            probe.UpdateSucceeds = true;
            clock.Time = 10f;
            transport.OnSubmit = () => Require(
                probe.Instance.evolution.presentationRequests.Single().RestorePublicContext() != null,
                "Equipment transport ran before its public context was persisted.");
            runtime.Tick();
            Require(transport.Prompts.Count == 1, "Equipment formula request did not reach correlated transport.");
            string prompt = transport.Prompts.Single();
            string key = transport.Keys.Single();
            string contextHash = probe.Instance.evolution.presentationRequests.Single().publicContextHash;
            Require(prompt.Contains("처음 지킨이") && prompt.Contains("오래 지킨 검"),
                "Equipment first dispatch did not capture resolved public names.");

            actor.Progression.GrowthState.displayName = "나중 지킨이";
            data.characterName = "나중 지킨이";
            nameField.SetValue(localDefinition, "새로 붙인 검");
            int previousEvidence = probe.Instance.evolution.formulaEvidence.Count;
            Require(owner.TryRecordUsage(source.instanceId, "combat:hit", 1f, 2f,
                    actor.Identity.PersistentId, 1, new[] { "melee" },
                    HistoricalEvidenceKind.None, "later-hit"),
                "Later equipment usage could not be recorded.");
            Require(probe.Instance.evolution.formulaEvidence.Count > previousEvidence,
                "Later usage did not leave a separate authoritative fact.");
            Require(actor.Identity.DisplayName == "나중 지킨이" && localDefinition.DisplayName == "새로 붙인 검",
                "Equipment rename test did not change the current world.");
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                transport.FailPending();
                Require(probe.Instance.evolution.presentationRequests.Single().failureCount == attempt,
                    "Equipment request failure did not persist its attempt.");
                if (attempt == 5) break;
                clock.Time += 128f;
                runtime.Tick();
                Require(transport.Prompts.Count == attempt + 1 && transport.Prompts.Last() == prompt
                        && transport.Keys.Last() == key,
                    "Equipment retry rebuilt its request from later names or events.");
            }
            CombatEquipmentInstance failed = probe.Instance.Clone();
            Require(failed.evolution.presentationRequests.Single().state == EquipmentEvolutionPresentationState.AwaitingNarrativeRetry
                    && failed.evolution.presentationRequests.Single().publicContextHash == contextHash,
                "Equipment retry exhaustion lost frozen public context.");
            Require(EquipmentItemStateCodec.TryDecode(EquipmentItemStateCodec.Encode(failed),
                    out CombatEquipmentInstance restored, out string error), error);
            EquipmentRuntimeProbe restoredProbe = CreateRuntimeProbe(source.instanceId, localDefinition);
            restoredProbe.Instance = restored;
            EquipmentEvolutionRuntime restoredOwner = CreateEvolutionRuntimeForPresentation(restoredProbe.Proxy);
            Require(restoredOwner.TryResumePresentation(source.instanceId, key),
                "Restored equipment request did not resume.");
            var replayTransport = new PresentationTransport();
            // The old participant is intentionally no longer queryable after restoration.
            var replay = new EvolutionHistoryNarrativeRuntime(new PresentationWorld(), restoredProbe.Proxy,
                new FacilityEvolutionStateComponentFactory(), replayTransport, new PresentationClock(), restoredOwner);
            replay.Tick();
            Require(replayTransport.Prompts.Count == 1 && replayTransport.Prompts.Single() == prompt
                    && replayTransport.Keys.Single() == key
                    && restoredProbe.Instance.evolution.formulaEvidence.Count == failed.evolution.formulaEvidence.Count
                    && restoredProbe.Instance.evolution.presentationRequests.Single().publicContextHash == contextHash,
                "Fresh equipment owner lost later evidence or changed the original request on replay.");
            string restoredBefore = JsonUtility.ToJson(restoredProbe.Instance);
            EquipmentEvolutionPresentationRequest request = restoredProbe.Instance.evolution.presentationRequests.Single();
            Require(restoredOwner.TryFreezePresentationContext(source.instanceId, key, request.RestorePublicContext(),
                    out EquipmentEvolutionPresentationRequest unchanged, out error)
                    && JsonUtility.ToJson(restoredProbe.Instance) == restoredBefore,
                "Idempotent equipment context binding changed the owner: " + error);
            unchanged.publicContext.entities[0].displayName = "외부 사본 변경";
            Require(JsonUtility.ToJson(restoredProbe.Instance) == restoredBefore,
                "Returned equipment context was not detached from the owner.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorRoot);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (localDefinition != null) UnityEngine.Object.DestroyImmediate(localDefinition);
        }
    }

    private sealed class PresentationWorld : IBuildingWorldQuery, ICharacterWorldQuery
    {
        public PresentationWorld(params CharacterActor[] actors) { Characters = actors; }
        public int BuildingVersion => 0;
        public int CharacterVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings => Array.Empty<BuildableObject>();
        public IReadOnlyList<CharacterActor> Characters { get; }
    }

    private sealed class PresentationClock : IUiClock
    {
        public float Time { get; set; }
        public float DeltaTime => 0f;
    }

    private sealed class PresentationTransport : ILocalLlmRuntimeProvider, ILocalLlmRuntime,
        ICorrelatedEquipmentEvolutionModuleSelectionLlmRuntime
    {
        public readonly List<string> Prompts = new();
        public readonly List<string> Keys = new();
        public Action OnSubmit;
        private Action<LocalLlmResult> pending;
        public bool TryGetRuntime(out ILocalLlmRuntime runtime) { runtime = this; return true; }
        public ILocalLlmRuntime GetRequiredRuntime() => this;
        public bool GenerateEquipmentEvolutionModuleSelectionAsync(string key, string prompt, Action<LocalLlmResult> callback)
        {
            OnSubmit?.Invoke();
            Keys.Add(key);
            Prompts.Add(prompt);
            pending = callback;
            return true;
        }
        public void FailPending()
        {
            Action<LocalLlmResult> callback = pending ?? throw new InvalidOperationException("No equipment request to fail.");
            pending = null;
            callback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, "{}", string.Empty, string.Empty));
        }
        public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback) => false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool HasMetric(
        GameplayOutcomeSnapshot outcome,
        string metricId,
        double expected) => outcome.metrics.Any(value => string.Equals(
            value.metricId,
            metricId,
            StringComparison.Ordinal) && Math.Abs(value.value - expected) <= 0.0001d);

    private static bool HasFact(
        GameplayOutcomeSnapshot outcome,
        string factId,
        string expected) => outcome.facts.Any(value => string.Equals(
            value.factId,
            factId,
            StringComparison.Ordinal) && string.Equals(
            value.value,
            expected,
            StringComparison.Ordinal));

    private static bool FactContains(
        GameplayOutcomeSnapshot outcome,
        string factId,
        string expected) => outcome.facts.Any(value => string.Equals(
            value.factId,
            factId,
            StringComparison.Ordinal) && value.value.Contains(
            expected,
            StringComparison.Ordinal));

    private static void WriteF03Report(string output, F03Report report)
    {
        using FileStream file = new(
            Path.Combine(output, "report.json"),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(file);
        writer.Write(JsonUtility.ToJson(report, prettyPrint: true));
    }

    [Serializable]
    private sealed class F03Report
    {
        public string testId = string.Empty;
        public bool passed;
        public string[] coverage = Array.Empty<string>();
        public string failure = string.Empty;
    }

    private static T InvokeInternal<T>(string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(EquipmentEvolutionRules).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                typeof(EquipmentEvolutionRules).FullName,
                methodName);
        ParameterInfo[] parameters = method.GetParameters();
        if (arguments.Length < parameters.Length)
        {
            object[] expanded = new object[parameters.Length];
            Array.Copy(arguments, expanded, arguments.Length);
            for (int index = arguments.Length; index < parameters.Length; index++)
            {
                if (!parameters[index].IsOptional)
                {
                    throw new TargetParameterCountException(
                        $"{methodName} requires parameter '{parameters[index].Name}'.");
                }

                expanded[index] = Type.Missing;
            }

            arguments = expanded;
        }

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

    private static void VerifyEquipmentModuleObserverLifecycle(
        EquipmentModuleSelectedFactData data,
        GameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        GameplayOutcomeRecorder recorder)
    {
        var fact = new EquipmentModuleSelectedFact(data);
        using DomainFactDispatcher facts = new();
        Require(!facts.Replay(fact).MayBeCommitted,
            "Equipment module-selection publication succeeded without its required observer.");

        var binding = EquipmentModuleSelectedFactMapping.CreateBinding(
            registry,
            ledger,
            recorder,
            new GameplayDomainFactCommitGroup(recorder, ledger));
        IDisposable connection = binding.Connect(facts);
        bool duplicateRejected = false;
        try
        {
            try
            {
                using IDisposable duplicate = binding.Connect(facts);
            }
            catch (InvalidOperationException error)
            {
                duplicateRejected = error.Message.Contains(
                    "Duplicate durable domain observer",
                    StringComparison.Ordinal);
            }
            Require(duplicateRejected,
                "Equipment module-selection observer accepted a duplicate binding.");
        }
        finally
        {
            connection.Dispose();
        }

        Require(!facts.Replay(fact).MayBeCommitted,
            "Disposed equipment module-selection observer still received facts.");
        using IDisposable rebound = binding.Connect(facts);
        Require(facts.Replay(fact).Completed,
            "Equipment module-selection observer did not resume exact replay after rebinding.");
    }

    private static EquipmentEvolutionRuntime CreateEvolutionRuntimeForPresentation(
        ICombatEquipmentRuntime equipment,
        EquipmentModuleSelectionPublicationRuntime moduleSelections = null)
    {
        EquipmentEvolutionRuntime runtime = (EquipmentEvolutionRuntime)
            FormatterServices.GetUninitializedObject(typeof(EquipmentEvolutionRuntime));
        FieldInfo formulaCatalogsField = typeof(EquipmentEvolutionRuntime).GetField(
            "formulaCatalogs",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(
                typeof(EquipmentEvolutionRuntime).FullName,
                "formulaCatalogs");
        formulaCatalogsField.SetValue(
            runtime,
            new EquipmentEvolutionFormulaCatalogs(
                CharacterAiEditorTestDependencies.ContentDefinitions));
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
        if (moduleSelections != null)
        {
            FieldInfo outcomesField = typeof(EquipmentEvolutionRuntime).GetField(
                "moduleSelections",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(
                    typeof(EquipmentEvolutionRuntime).FullName,
                    "moduleSelections");
            outcomesField.SetValue(runtime, moduleSelections);
        }
        return runtime;
    }

    private sealed class FaultingEquipmentModuleFacts :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        internal FaultingEquipmentModuleFacts(DomainFactDispatcher inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal bool RejectNextPublish { get; set; }
        internal bool HideNextCommittedAcknowledgement { get; set; }
        internal int CommittedPublishCalls { get; private set; }
        internal int ReplayBatchCalls { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) => inner.TryReserve(
                proposed,
                out lease,
                out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (RejectNextPublish)
            {
                RejectNextPublish = false;
                return DomainFactDelivery.Reject(
                    "editor-injected-equipment-outcome-commit-failure");
            }
            DomainFactDelivery delivery = inner.Publish(lease, actual);
            CommittedPublishCalls++;
            if (HideNextCommittedAcknowledgement)
            {
                HideNextCommittedAcknowledgement = false;
                return DomainFactDelivery.Uncertain(
                    "editor-injected-equipment-outcome-ack-loss-after-real-commit");
            }
            return delivery;
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(
                facts,
                results,
                out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            ReplayBatchCalls++;
            return inner.ReplayBatch(facts, results, out failure);
        }
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
            if (targetMethod.Name == "get_Instances")
                return Instance == null ? Array.Empty<CombatEquipmentInstance>() : new[] { Instance.Clone() };
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
