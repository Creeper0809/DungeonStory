#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class LedgerB1NewOutcomeDebugScenarios
{
    public static void RunF02SkillAcquiredBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException(
                "A new F02 skill-acquired output directory is required.");
        Directory.CreateDirectory(output);
        try
        {
            Require(
                CharacterProgressionDebugScenarios
                    .RunSkillAcquiredOutcomeAtomicityScenario(),
                "The focused F02 production entry did not pass.");
            WriteF02Report(output, new JObject
            {
                ["testId"] = "audit-f02-skill-acquired",
                ["passed"] = true,
                ["coverage"] = new JArray(
                    "active-choice-exact-outcome",
                    "passive-automatic-exact-outcome",
                    "ultimate-automatic-exact-outcome",
                    "automatic-terminal-redelivery-no-op",
                    "acquisition-save-shape-round-trip")
            });
        }
        catch (Exception error)
        {
            WriteF02Report(output, new JObject
            {
                ["testId"] = "audit-f02-skill-acquired",
                ["passed"] = false,
                ["failure"] = error.ToString()
            });
            throw;
        }
    }

    public static bool RunAll(bool logSuccess = false)
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new PolicyValueChangeOutcomeDescriptor(),
                new CharacterSkillAcquiredOutcomeDescriptor(),
                new EquipmentModuleSelectionOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new PolicyValueChangeOutcomeAdapter(),
                new CharacterSkillAcquiredOutcomeAdapter(),
                new EquipmentModuleSelectionOutcomeAdapter()
            });
        GameEventBus events = new();
        GameplayOutcomeLedger ledger = new(
            registry,
            new GameplayOutcomeBufferLimits(32, 0, 128),
            new GameplayOutcomeRunId("b1-new-outcomes"),
            1L);
        GameplayOutcomeRecorder recorder = new(ledger, registry, events);
        EvolutionGameplayOutcomeBridge evolution = new(
            recorder,
            ledger,
            ledger);
        using DomainFactDispatcher facts = new();
        var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
        using IDisposable policyFacts = facts.Subscribe(
            "test:b1:character-consumables-policy-change",
            new GameplayDomainFactObserver<
                CharacterConsumablesPolicyChangedFact,
                PolicyValueChangeReceipt>(
                CharacterConsumablesPolicyFactMapping.Capture,
                registry,
                ledger,
                recorder,
                commits));
        using IDisposable equipmentFacts = EquipmentModuleSelectedFactMapping
            .CreateBinding(registry, ledger, recorder, commits)
            .Connect(facts);

        CommitPolicy(facts, ledger);
        CommitSkill(evolution, ledger);
        CommitEquipment(facts, ledger);
        RejectReservedEquipmentRevisionDrift(facts, ledger);
        VerifyRevisionPersistence();
        Require(
            SurvivalDebugScenarios.RunConsumablesPolicyOutcomeAtomicityFocused()
                .StartsWith("CON01=PASS", StringComparison.Ordinal),
            "The consumables policy production entry did not pass.");
        Require(
            CharacterProgressionDebugScenarios
                .RunSkillAcquiredOutcomeAtomicityScenario(),
            "The CharacterSkill production entry did not pass.");
        EquipmentItemStateV18DebugScenarios.RunAll();

        if (logSuccess)
            Debug.Log(
                "B1 policy/skill/equipment contract plus production-entry atomicity PASS");
        return true;
    }

    public static bool RunEquipmentObserverAdapterFocused()
    {
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[] { new EquipmentModuleSelectionOutcomeDescriptor() },
            new IGameplayOutcomeAdapterRegistration[] { new EquipmentModuleSelectionOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(),
            new GameplayOutcomeRunId("b88-equipment-observer-adapter"), 1L);
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        using var facts = new DomainFactDispatcher();
        using IDisposable binding = EquipmentModuleSelectedFactMapping.CreateBinding(registry, ledger, recorder,
            new GameplayDomainFactCommitGroup(recorder, ledger)).Connect(facts);
        CommitEquipment(facts, ledger);
        RejectReservedEquipmentRevisionDrift(facts, ledger);
        return true;
    }

    private static void CommitPolicy(
        DomainFactDispatcher facts,
        GameplayOutcomeLedger ledger)
    {
        CharacterConsumablesPolicyChangedFact fact = new(
            new CharacterId("character:b1-policy"),
            "연화",
            new Vector2Int(2, 3),
            "character-diet",
            "self",
            "free",
            "vegan",
            "character-summary-ui",
            0L,
            1L,
            7);
        PolicyValueChangeReceipt receipt =
            CharacterConsumablesPolicyFactMapping.Capture(fact);
        Require(facts.TryReserve(
            fact,
            out DomainFactLease lease,
            out string failure), failure);
        DomainFactDelivery delivery = facts.Publish(lease, fact);
        Require(delivery.Completed, delivery.Detail);
        RequireExact(ledger, receipt.ResultKey, PolicyValueChangeOutcomeIds.Changed);
    }

    private static void CommitSkill(
        EvolutionGameplayOutcomeBridge bridge,
        GameplayOutcomeLedger ledger)
    {
        CharacterSkillInstance skill = CreateSkill("skill:b1-active");
        CharacterSkillAcquiredOutcomeReceipt receipt = new(
            "character-skill:character:b1-skill:active:3:1",
            "character:b1-skill",
            "연화",
            0L,
            1L,
            3,
            skill,
            7);
        ICharacterSkillAcquiredOutcomeCommitter committer = bridge;
        Require(committer.TryPrepare(
            receipt,
            out PreparedEvolutionOutcome prepared,
            out string failure), failure);
        Require(committer.TryCommit(prepared, 1L, out failure), failure);
        RequireExact(ledger, receipt.ResultKey, CharacterSkillAcquiredOutcomeIds.Acquired);
    }

    private static void CommitEquipment(
        DomainFactDispatcher facts,
        GameplayOutcomeLedger ledger)
    {
        EquipmentModuleSelectedFact fact = new(EquipmentFactData(
            "equipment-module:item-instance:b1-sword:1",
            "selection:b1-sword:1",
            0L,
            1L));
        EquipmentModuleSelectionOutcomeReceipt receipt =
            EquipmentModuleSelectedFactMapping.Capture(fact);
        Require(facts.TryReserve(
            fact,
            out DomainFactLease lease,
            out string failure), failure);
        DomainFactDelivery delivery = facts.Publish(lease, fact);
        Require(delivery.Completed, delivery.Detail);
        RequireExact(
            ledger,
            receipt.ResultKey,
            EquipmentModuleSelectionOutcomeIds.Selected);
    }

    private static void RejectReservedEquipmentRevisionDrift(
        DomainFactDispatcher facts,
        GameplayOutcomeLedger ledger)
    {
        EquipmentModuleSelectedFactData reservedData = EquipmentFactData(
            "equipment-module:item-instance:b1-sword:2",
            "selection:b1-sword:2",
            1L,
            2L);
        EquipmentModuleSelectedFact reserved = new(reservedData);
        Require(facts.TryReserve(
            reserved,
            out DomainFactLease lease,
            out string failure), failure);

        EquipmentModuleSelectedFactData driftedData = EquipmentFactData(
            "equipment-module:item-instance:b1-sword:3",
            "selection:b1-sword:2",
            2L,
            3L);
        EquipmentModuleSelectedFact drifted = new(driftedData);
        DomainFactDelivery delivery = facts.Publish(lease, drifted);
        Require(
            delivery.Phase == DomainFactDeliveryPhase.Rejected
            && delivery.Detail.Contains(
                "fact-observer-reserved-identity-drift",
                StringComparison.Ordinal),
            "A reserved equipment owner-revision drift was not rejected before commit.");
        Require(!ledger.TryGetResultIdentity(
                EquipmentModuleSelectedFactMapping.ResultKey(reservedData),
                out _)
            && !ledger.TryGetResultIdentity(
                EquipmentModuleSelectedFactMapping.ResultKey(driftedData),
                out _),
            "Rejected equipment owner-revision drift left a replay identity.");
    }

    private static EquipmentModuleSelectedFactData EquipmentFactData(
        string operationId,
        string selectionId,
        long beforeRevision,
        long afterRevision) => new()
        {
            operationId = operationId,
            selectionId = selectionId,
            beforeRevision = beforeRevision,
            afterRevision = afterRevision,
            nodeId = "evolution-node:b1-sword:1",
            nodeDisplayName = "수호의 맥동",
            equipmentInstanceId = "item-instance:b1-sword",
            equipmentDefinitionId = "weapon:b1-sword",
            currentOwnerId = "character:b1-owner",
            attunementOwnerId = string.Empty,
            reforgeOrderId = "reforge-order:b1-sword:1",
            historical = false,
            generationBefore = 0,
            generationAfter = 1,
            masteryBefore = 12f,
            masteryAfter = 4f,
            positiveModuleIds = new[] { "module:defense" },
            drawbackModuleIds = new[] { "module:weight" },
            selectedModuleIds = new[] { "module:defense", "module:weight" },
            capabilityIds = new[] { "capability:defense" },
            effectIds = new[] { "effect:defense-up" },
            evidenceFactIds = new[] { "fact:b1-blocked-hit" },
            absoluteDay = 7
        };

    private static CharacterSkillInstance CreateSkill(string id) => new()
    {
        id = id,
        ruleId = "rule:b1-active",
        combinationId = "combination:b1-active",
        displayName = "치유의 호흡",
        kind = CharacterSkillKind.Active,
        modules = new List<CharacterSkillModuleSelection>
        {
            new() { moduleId = "module:heal", variantId = "variant:self" }
        },
        formulaVersion = 1,
        formulaCatalogSha256 = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        evidenceIds = new List<string> { "fact:b1-wound-treated" },
        evidenceBindings = new List<GameplayOutcomeEvidenceBindingSnapshot>(),
        formulaCapabilities = new List<CharacterSkillFormulaCapabilityEnvelope>
        {
            new()
            {
                capabilityId = "capability:heal",
                formatterId = "formatter:heal",
                applicatorId = "applicator:heal",
                parameters = new List<CharacterSkillFormulaParameter>
                {
                    new() { parameterId = "amount", units = 5 }
                }
            }
        },
        presentationId = "presentation:b1-active",
        mechanicalDescription = "체력을 회복한다.",
        narrativeFlavor = "상처의 호흡을 고른다."
    };

    private static void VerifyRevisionPersistence()
    {
        CharacterGrowthState growth = new() { skillAcquisitionRevision = 4L };
        CharacterGrowthState growthRestored = JsonUtility.FromJson<CharacterGrowthState>(
            JsonUtility.ToJson(growth));
        Require(growthRestored.skillAcquisitionRevision == 4L,
            "Character skill acquisition revision did not round-trip.");

        EquipmentEvolutionState equipment = new() { moduleSelectionRevision = 5L };
        EquipmentEvolutionState equipmentRestored =
            JsonUtility.FromJson<EquipmentEvolutionState>(
                JsonUtility.ToJson(equipment));
        Require(equipmentRestored.moduleSelectionRevision == 5L,
            "Equipment module-selection revision did not round-trip.");

        DungeonCharacterConsumablesSaveData consumables = new()
        {
            timelineTime = 0f,
            policyRevision = 6L
        };
        DungeonCharacterConsumablesSaveData consumablesRestored =
            JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                JsonUtility.ToJson(consumables));
        Require(consumablesRestored.policyRevision == 6L,
            "Consumables policy revision did not round-trip.");
    }

    private static void RequireExact(
        GameplayOutcomeLedger ledger,
        GameplayResultKey resultKey,
        GameplayOutcomeTypeId outcomeType)
    {
        Require(ledger.TryGetResultIdentity(
            resultKey,
            out GameplayOutcomeReplayIdentity identity),
            "Committed result has no replay identity.");
        Require(identity.ResultKey == resultKey,
            "Replay identity changed the result key.");
        Require(ledger.TryGetExact(
            identity.OutcomeId,
            out GameplayOutcomeSnapshot exact),
            "Committed result has no exact outcome snapshot.");
        Require(string.Equals(
                exact.outcomeTypeId,
                outcomeType.Value,
                StringComparison.Ordinal),
            "Committed result has the wrong outcome type.");
    }

    private static void WriteF02Report(string output, JObject report)
    {
        using FileStream file = new(
            Path.Combine(output, "report.json"),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(file);
        writer.Write(report.ToString(Formatting.Indented));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
