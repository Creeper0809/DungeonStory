using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NarrativePrimaryAllocationDebugScenarios
{
    private static int checkCount;

    [MenuItem("DungeonStory/Narrative Formula/Run Primary Allocation Scenarios")]
    private static void RunFromMenu()
    {
        RunAll();
    }

    public static string RunAll()
    {
        checkCount = 0;
        VerifyHealingAndDefenseComposition();
        VerifyThreeEffectShare();
        VerifyReversingPrimaryChangesAllocation();
        VerifyOddBudgetResidual();
        VerifyExpensiveQuanta();
        VerifyPrimaryCapAllowsTruthfulResidual();
        VerifyInvalidPrimarySelectionFailsClosed();
        VerifyMalformedSnapshotFailsClosed();
        VerifyExplicitPrimaryInputOrderInvariance();
        VerifyLegacyDefaultIsUnchangedAndDeterministic();
        VerifySingleEffectIsTriviallyPrimary();
        VerifySmallExhaustiveSpendObjective();
        VerifyCharacterSkillModuleSelectionIntegration();

        string report = $"Narrative primary allocation scenarios passed ({checkCount} checks).";
        Debug.Log(report);
        return report;
    }

    public static void RunBatch()
    {
        NarrativeFormulaCoreDebugScenarios.RunAll();
        RunAll();
        if (!CharacterProgressionDebugScenarios.RunFormulaPresentationContractScenario())
            throw new InvalidOperationException(
                "CharacterSkill formula presentation contract scenario returned false.");
        Debug.Log("PRIMARY_ALLOCATION_BATCH_PASS");
    }

    private static void VerifyHealingAndDefenseComposition()
    {
        NarrativeFormulaCapabilityDescriptor healing = Descriptor(
            "healing", minimumUnits: 10, maximumSteps: 6,
            quantumUnits: 2, costPerQuantum: 1);
        NarrativeFormulaCapabilityDescriptor defense = Descriptor(
            "defense", minimumUnits: 5, maximumSteps: 6,
            quantumUnits: 1, costPerQuantum: 1);
        NarrativeFormulaCandidate candidate = Optimize(
            new[] { Input(defense), Input(healing) }, 6, healing.CapabilityId);

        Require(candidate.Allocations.Count == 2,
            "Healing/defense allocation must preserve both selected effects.");
        Require(string.Equals(candidate.Allocations[0].Descriptor.CapabilityId,
                healing.CapabilityId, StringComparison.Ordinal),
            "The explicit healing primary must be serialized first.");
        Require(candidate.ProminencePolicyVersion == NarrativeFormulaCore.CurrentProminencePolicyVersion
                && string.Equals(candidate.PrimaryCapabilityId, healing.CapabilityId,
                    StringComparison.Ordinal),
            "A primary-aware healing/defense candidate must persist its policy and primary identity.");
        Require(Enhancement(candidate, healing.CapabilityId)
                >= Enhancement(candidate, defense.CapabilityId),
            "Healing enhancement cost must be at least the defense support enhancement cost.");
    }

    private static void VerifyThreeEffectShare()
    {
        NarrativeFormulaCapabilityDescriptor healing = Descriptor(
            "triage", 0, 8, 1, 1);
        NarrativeFormulaCapabilityDescriptor barrier = Descriptor(
            "barrier", 0, 6, 1, 1);
        NarrativeFormulaCapabilityDescriptor cleanse = Descriptor(
            "cleanse", 0, 6, 1, 1);
        NarrativeFormulaCandidate candidate = Optimize(
            new[] { Input(cleanse), Input(healing), Input(barrier) },
            8,
            healing.CapabilityId);
        int primary = Enhancement(candidate, healing.CapabilityId);
        int supportA = Enhancement(candidate, barrier.CapabilityId);
        int supportB = Enhancement(candidate, cleanse.CapabilityId);

        Require(candidate.Allocations.Count == 3,
            "A three-effect selection must preserve all three effects.");
        Require(candidate.PositiveCost == 8,
            "The equal-quantum three-effect composition must spend its full budget.");
        Require(supportA > 0 && supportB > 0,
            "A balanced three-effect composition must enhance both supports.");
        Require(primary >= supportA + supportB,
            "The three-effect primary must cover the combined support enhancement cost.");
    }

    private static void VerifyReversingPrimaryChangesAllocation()
    {
        NarrativeFormulaCapabilityDescriptor healing = Descriptor(
            "reverse-healing", 0, 3, 1, 3);
        NarrativeFormulaCapabilityDescriptor defense = Descriptor(
            "reverse-defense", 0, 3, 1, 2);
        NarrativeFormulaCapabilityInput[] inputs = { Input(healing), Input(defense) };

        NarrativeFormulaCandidate healingPrimary = Optimize(
            inputs, 7, healing.CapabilityId);
        NarrativeFormulaCandidate defensePrimary = Optimize(
            inputs, 7, defense.CapabilityId);

        Require(healingPrimary.PositiveCost == 6
                && defensePrimary.PositiveCost == 7,
            "Reversing an asymmetric primary must change the best feasible spend.");
        Require(!string.Equals(healingPrimary.CanonicalSignature,
                defensePrimary.CanonicalSignature, StringComparison.Ordinal),
            "Reversing the primary must produce a distinct persisted allocation.");
        Require(string.Equals(healingPrimary.Allocations[0].Descriptor.CapabilityId,
                    healing.CapabilityId, StringComparison.Ordinal)
                && string.Equals(defensePrimary.Allocations[0].Descriptor.CapabilityId,
                    defense.CapabilityId, StringComparison.Ordinal),
            "Each reversed primary must remain first in allocation order.");
        Require(Enhancement(healingPrimary, healing.CapabilityId)
                    >= Enhancement(healingPrimary, defense.CapabilityId)
                && Enhancement(defensePrimary, defense.CapabilityId)
                    >= Enhancement(defensePrimary, healing.CapabilityId),
            "Each reversed allocation must satisfy prominence for its selected primary.");
    }

    private static void VerifyOddBudgetResidual()
    {
        NarrativeFormulaCapabilityDescriptor primary = Descriptor(
            "odd-primary", 0, 4, 1, 2);
        NarrativeFormulaCapabilityDescriptor support = Descriptor(
            "odd-support", 0, 4, 1, 2);
        NarrativeFormulaCandidate candidate = Optimize(
            new[] { Input(primary), Input(support) }, 5, primary.CapabilityId);

        Require(candidate.PositiveCost == 4,
            "An odd budget with cost-two quanta must preserve the truthful one-point residual.");
        Require(5 - candidate.PositiveCost == 1,
            "The odd-budget residual must not be fabricated into an unavailable quantum.");
        Require(Enhancement(candidate, primary.CapabilityId)
                >= Enhancement(candidate, support.CapabilityId),
            "Odd-budget allocation must still preserve primary prominence.");
    }

    private static void VerifyExpensiveQuanta()
    {
        NarrativeFormulaCapabilityDescriptor primary = Descriptor(
            "expensive-primary", 0, 2, 1, 7);
        NarrativeFormulaCapabilityDescriptor support = Descriptor(
            "expensive-support", 0, 2, 1, 5);
        NarrativeFormulaCandidate candidate = Optimize(
            new[] { Input(support), Input(primary) }, 17, primary.CapabilityId);

        Require(candidate.PositiveCost == 14,
            "Expensive quanta must choose the highest feasible prominent spend.");
        Require(candidate.PositiveCost <= 17,
            "Expensive quanta must never overspend the authored budget.");
        Require(Enhancement(candidate, primary.CapabilityId)
                >= Enhancement(candidate, support.CapabilityId),
            "Expensive-quanta allocation must preserve primary prominence.");
    }

    private static void VerifyPrimaryCapAllowsTruthfulResidual()
    {
        NarrativeFormulaCapabilityDescriptor primary = Descriptor(
            "capped-primary", 0, 1, 1, 3);
        NarrativeFormulaCapabilityDescriptor support = Descriptor(
            "capped-support", 0, 3, 1, 2);
        NarrativeFormulaCandidate candidate = Optimize(
            new[] { Input(primary), Input(support) }, 7, primary.CapabilityId);

        Require(candidate.PositiveCost == 5,
            "A capped primary must allow a truthful residual when the next support quantum would dominate it.");
        Require(Enhancement(candidate, primary.CapabilityId) == 3
                && Enhancement(candidate, support.CapabilityId) == 2,
            "The capped-primary allocation must retain its only legal support quantum.");
        Require(7 - candidate.PositiveCost == 2,
            "The residual must remain visible when no prominence-preserving quantum can consume it.");
    }

    private static void VerifyInvalidPrimarySelectionFailsClosed()
    {
        NarrativeFormulaCapabilityDescriptor healing = Descriptor(
            "invalid-primary-healing", 0, 2, 1, 1);
        NarrativeFormulaCapabilityDescriptor defense = Descriptor(
            "invalid-primary-defense", 0, 2, 1, 1);
        NarrativeFormulaCapabilityInput[] inputs = { Input(healing), Input(defense) };

        RequireThrows<InvalidOperationException>(() => OptimizeAll(inputs, 4, string.Empty),
            "An empty explicit primary ID must fail closed.");
        RequireThrows<InvalidOperationException>(() => OptimizeAll(inputs, 4, "not-selected"),
            "An unknown explicit primary ID must fail closed.");
    }

    private static void VerifyMalformedSnapshotFailsClosed()
    {
        NarrativeFormulaCapabilityDescriptor primary = Descriptor(
            "snapshot-primary", 0, 3, 1, 1);
        NarrativeFormulaCapabilityDescriptor support = Descriptor(
            "snapshot-support", 0, 3, 1, 1);
        NarrativeFormulaCapabilityAllocation primaryTwo = Allocation(primary, 2);
        NarrativeFormulaCapabilityAllocation primaryOne = Allocation(primary, 1);
        NarrativeFormulaCapabilityAllocation supportOne = Allocation(support, 1);
        NarrativeFormulaCapabilityAllocation supportTwo = Allocation(support, 2);

        NarrativeFormulaCore.RequirePrimaryProminence(
            0, string.Empty, new[] { supportTwo, primaryOne });
        Require(true,
            "The exact legacy policy/version pair must preserve legacy support-dominant snapshots.");
        NarrativeFormulaCore.RequirePrimaryProminence(
            NarrativeFormulaCore.CurrentProminencePolicyVersion,
            primary.CapabilityId,
            new[] { primaryTwo, supportOne });
        Require(true,
            "A well-formed primary-aware snapshot must validate.");

        RequireThrows<InvalidOperationException>(() =>
                NarrativeFormulaCore.RequirePrimaryProminence(
                    0, primary.CapabilityId, new[] { primaryTwo, supportOne }),
            "A legacy policy snapshot must reject a non-empty primary ID.");
        RequireThrows<InvalidOperationException>(() =>
                NarrativeFormulaCore.RequirePrimaryProminence(
                    NarrativeFormulaCore.CurrentProminencePolicyVersion,
                    string.Empty,
                    new[] { primaryTwo, supportOne }),
            "A primary-aware snapshot must reject an empty primary ID.");
        RequireThrows<InvalidOperationException>(() =>
                NarrativeFormulaCore.RequirePrimaryProminence(
                    NarrativeFormulaCore.CurrentProminencePolicyVersion,
                    "snapshot-missing",
                    new[] { primaryTwo, supportOne }),
            "A primary-aware snapshot must reject an unselected primary ID.");
        RequireThrows<InvalidOperationException>(() =>
                NarrativeFormulaCore.RequirePrimaryProminence(
                    NarrativeFormulaCore.CurrentProminencePolicyVersion,
                    primary.CapabilityId,
                    new[] { supportOne, primaryTwo }),
            "A primary-aware snapshot must reject allocations that do not put the primary first.");
        RequireThrows<InvalidOperationException>(() =>
                NarrativeFormulaCore.RequirePrimaryProminence(
                    NarrativeFormulaCore.CurrentProminencePolicyVersion,
                    primary.CapabilityId,
                    new[] { primaryOne, supportTwo }),
            "A primary-aware snapshot must reject support-dominant enhancement costs.");
        RequireThrows<InvalidOperationException>(() =>
                NarrativeFormulaCore.RequirePrimaryProminence(
                    NarrativeFormulaCore.CurrentProminencePolicyVersion + 1,
                    primary.CapabilityId,
                    new[] { primaryTwo, supportOne }),
            "An unknown prominence policy version must fail closed.");
    }

    private static void VerifyExplicitPrimaryInputOrderInvariance()
    {
        NarrativeFormulaCapabilityDescriptor primary = Descriptor(
            "order-primary", 2, 4, 2, 2);
        NarrativeFormulaCapabilityDescriptor firstSupport = Descriptor(
            "order-alpha", 3, 4, 1, 1);
        NarrativeFormulaCapabilityDescriptor secondSupport = Descriptor(
            "order-zeta", 7, 4, 1, 1);
        NarrativeFormulaCapabilityInput[] forward =
        {
            Input(primary), Input(firstSupport), Input(secondSupport)
        };
        NarrativeFormulaCapabilityInput[] reverse =
        {
            Input(secondSupport), Input(firstSupport), Input(primary)
        };

        string[] left = OptimizeAll(forward, 9, primary.CapabilityId, maximumResults: 3)
            .Select(value => value.CanonicalSignature).ToArray();
        string[] right = OptimizeAll(reverse, 9, primary.CapabilityId, maximumResults: 3)
            .Select(value => value.CanonicalSignature).ToArray();

        Require(left.Length > 0 && left.SequenceEqual(right, StringComparer.Ordinal),
            "Explicit-primary allocation must be invariant to caller input order.");
    }

    private static void VerifyLegacyDefaultIsUnchangedAndDeterministic()
    {
        NarrativeFormulaCapabilityDescriptor alpha = Descriptor(
            "legacy-alpha", 0, 6, 1, 1);
        NarrativeFormulaCapabilityDescriptor zeta = Descriptor(
            "legacy-zeta", 0, 6, 1, 1);
        NarrativeFormulaGenerationCostContext context = Context();
        IReadOnlyList<NarrativeFormulaCandidate> forward =
            NarrativeFormulaCore.OptimizeExactComposition(
                new[] { Input(alpha), Input(zeta) },
                context,
                6,
                new[] { "fact:legacy-default" },
                maximumResults: 3);
        IReadOnlyList<NarrativeFormulaCandidate> reverse =
            NarrativeFormulaCore.OptimizeExactComposition(
                new[] { Input(zeta), Input(alpha) },
                context,
                6,
                new[] { "fact:legacy-default" },
                maximumResults: 3,
                primaryCapabilityId: null);

        Require(forward.Count > 0
                && forward.All(value => value.ProminencePolicyVersion == 0
                    && value.PrimaryCapabilityId.Length == 0),
            "Omitting the primary must preserve the exact legacy policy marker.");
        Require(forward.Select(value => value.CanonicalSignature).SequenceEqual(
                reverse.Select(value => value.CanonicalSignature), StringComparer.Ordinal),
            "Legacy allocation must remain deterministic across input order.");
        Require(forward.All(value => value.PositiveCost == 6),
            "Legacy allocation must retain its prior maximal-spend objective.");
    }

    private static void VerifySingleEffectIsTriviallyPrimary()
    {
        NarrativeFormulaCapabilityDescriptor healing = Descriptor(
            "single-healing", 4, 4, 2, 2);
        NarrativeFormulaCandidate candidate = Optimize(
            new[] { Input(healing) }, 5, healing.CapabilityId);

        Require(candidate.ProminencePolicyVersion
                == NarrativeFormulaCore.CurrentProminencePolicyVersion
                && string.Equals(candidate.PrimaryCapabilityId,
                    healing.CapabilityId, StringComparison.Ordinal),
            "A single selected effect must persist as its own explicit primary.");
        Require(candidate.PositiveCost == 4
                && Enhancement(candidate, healing.CapabilityId) == 4,
            "A single primary must spend to its quantized boundary without support effects.");
    }

    private static void VerifySmallExhaustiveSpendObjective()
    {
        for (int primaryQuantumCost = 1; primaryQuantumCost <= 3; primaryQuantumCost++)
        for (int supportQuantumCost = 1; supportQuantumCost <= 3; supportQuantumCost++)
        for (int primaryMaximumSteps = 1; primaryMaximumSteps <= 3; primaryMaximumSteps++)
        for (int supportMaximumSteps = 1; supportMaximumSteps <= 3; supportMaximumSteps++)
        {
            NarrativeFormulaCapabilityDescriptor primary = Descriptor(
                $"exhaustive-primary-{primaryQuantumCost}-{supportQuantumCost}-{primaryMaximumSteps}-{supportMaximumSteps}",
                3,
                primaryMaximumSteps,
                2,
                primaryQuantumCost);
            NarrativeFormulaCapabilityDescriptor support = Descriptor(
                $"exhaustive-support-{primaryQuantumCost}-{supportQuantumCost}-{primaryMaximumSteps}-{supportMaximumSteps}",
                5,
                supportMaximumSteps,
                3,
                supportQuantumCost);

            for (int budget = 0; budget <= 9; budget++)
            {
                int expected = ExhaustiveMaximumSpend(
                    primaryQuantumCost,
                    primaryMaximumSteps,
                    supportQuantumCost,
                    supportMaximumSteps,
                    budget);
                IReadOnlyList<NarrativeFormulaCandidate> candidates = OptimizeAll(
                    new[] { Input(support), Input(primary) },
                    budget,
                    primary.CapabilityId);
                int actual = candidates.Count == 0
                    ? -1
                    : candidates.Max(value => value.PositiveCost);
                Require(actual == expected,
                    $"Bounded exhaustive objective mismatch for primary cost {primaryQuantumCost}, "
                    + $"support cost {supportQuantumCost}, primary cap {primaryMaximumSteps}, "
                    + $"support cap {supportMaximumSteps}, budget {budget}: "
                    + $"expected {expected}, got {actual}.");
            }
        }
    }

    private static void VerifyCharacterSkillModuleSelectionIntegration()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        try
        {
            settings.formulaPolicy.formulaVersion =
                CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion;
            CharacterSkillFormulaCatalogAssetBuilder.Populate(settings);
            int currentFormulaVersion = settings.RequireFormulaPolicy().FormulaVersion;
            string catalogSha256 = settings.formulaPolicy.RequireCatalogSha256();
            const string ruleId = "rule:primary-allocation:heal-guard";
            const string selectionId = "selection:primary-allocation:heal-guard";
            CharacterSkillDraft draft = new CharacterSkillDraft
            {
                kind = CharacterSkillKind.Active,
                requestKey = "qa:primary-allocation:character-skill",
                formulaVersion = currentFormulaVersion,
                formulaCatalogSha256 = catalogSha256,
                formulaBudget = 30,
                rules = new List<CharacterSkillCandidateRule>
                {
                    new CharacterSkillCandidateRule
                    {
                        ruleId = ruleId,
                        rarity = CharacterSkillRarity.Common,
                        budget = 30,
                        trigger = CharacterSkillTrigger.ManualCombat,
                        target = CharacterSkillTarget.Self,
                        targetingMode = CharacterSkillTargetingMode.Self,
                        effectArea = CharacterSkillEffectArea.Single,
                        areaSize = 1,
                        ultimateDomain = CharacterUltimateDomain.None,
                        cooldownTurns = 0,
                        manualDurationHours = 0,
                        manualCooldownDays = 0,
                        mechanicalPolicySource =
                            CharacterSkillMechanicalPolicySource.AuthoredRule,
                        allowedModuleIds = new List<string> { "heal", "guard" },
                        allowedVariantIds = new List<string>()
                    }
                },
                moduleSelectionOffers = new List<CharacterSkillModuleOfferState>
                {
                    new CharacterSkillModuleOfferState
                    {
                        selectionId = selectionId,
                        ruleId = ruleId,
                        positiveModuleIds = new List<string> { "heal", "guard" },
                        drawbackModuleIds = new List<string>(),
                        evidenceFactIds = new List<string> { "fact:healing" },
                        qualifiedNegativeEvidenceFactIds = new List<string>(),
                        maximumPositiveModules = 2,
                        maximumDrawbackModules = 0
                    }
                }
            };

            NarrativeFormulaModuleSelectionRequest request =
                CharacterSkillFormulaGeneration.BuildModuleSelectionRequest(
                    draft, settings);
            string[] offeredPositiveIds = request.Offers
                .Where(value => value.Polarity
                    == NarrativeFormulaModulePolarity.Positive)
                .Select(value => value.ModuleId).ToArray();
            Require(request.MaximumPositiveModules == 2
                    && offeredPositiveIds.SequenceEqual(
                        new[] { "guard", "heal" }, StringComparer.Ordinal),
                "The live CharacterSkill request did not preserve both legal heal/guard offers.");

            string prompt = CharacterSkillPromptBuilder.BuildModuleSelectionPacket(
                draft, settings);
            Require(prompt.Contains(
                    "positiveModuleIds[0]이 주효과다.",
                    StringComparison.Ordinal)
                    && prompt.Contains(
                        "나머지는 보조효과다.",
                        StringComparison.Ordinal),
                "The live CharacterSkill prompt does not state the primary-first contract.");

            CharacterSkillInstance resolved =
                CharacterSkillFormulaGeneration.ResolveModuleSelection(
                    draft,
                    new NarrativeFormulaModuleSelectionChoice(
                        request.SelectionId,
                        new[] { "heal", "guard" },
                        Array.Empty<string>(),
                        new[] { "fact:healing" }),
                    settings);
            resolved.displayName = "치유 방벽";
            resolved.narrativeFlavor = "상처를 돌본 경험이 먼저 회복하고 곁을 지키는 힘이 되었다.";

            Require(resolved.modules.Count == 2
                    && resolved.formulaCapabilities.Count == 2
                    && resolved.cooldownTurns == 0
                    && resolved.manualDurationHours == 0
                    && resolved.manualCooldownDays == 0
                    && resolved.modules.Select(value => value.moduleId).SequenceEqual(
                        new[] { "heal", "guard" }, StringComparer.Ordinal)
                    && resolved.formulaCapabilities.Select(value => value.capabilityId)
                        .SequenceEqual(new[] { "heal", "guard" },
                            StringComparer.Ordinal),
                "The resolved CharacterSkill did not freeze both modules in primary-first order.");
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(
                resolved, settings);
            Require(true,
                "The live heal-primary CharacterSkill must pass restored-skill validation.");
            Require(resolved.prominencePolicyVersion
                        == NarrativeFormulaCore.CurrentProminencePolicyVersion
                    && string.Equals(resolved.primaryCapabilityId,
                        "heal", StringComparison.Ordinal)
                    && string.Equals(resolved.combinationId,
                        "heal", StringComparison.Ordinal)
                    && string.Equals(resolved.modules[0].moduleId,
                        "heal", StringComparison.Ordinal),
                "The live CharacterSkill did not persist heal as both primary capability and primary module.");
            int healingEnhancement = FrozenEnhancementCost(
                resolved, "heal", settings);
            int guardEnhancement = FrozenEnhancementCost(
                resolved, "guard", settings);
            Require(healingEnhancement >= guardEnhancement,
                "The live CharacterSkill allocated more enhancement cost to guard than to primary healing.");

            string frozenJson = JsonUtility.ToJson(resolved);
            CharacterSkillInstance cloned = resolved.Clone();
            Require(string.Equals(JsonUtility.ToJson(cloned), frozenJson,
                    StringComparison.Ordinal)
                    && cloned.prominencePolicyVersion
                        == NarrativeFormulaCore.CurrentProminencePolicyVersion
                    && string.Equals(cloned.primaryCapabilityId,
                        "heal", StringComparison.Ordinal),
                "CharacterSkill Clone changed the frozen JSON or primary metadata.");

            CharacterSkillInstance restored =
                JsonUtility.FromJson<CharacterSkillInstance>(frozenJson);
            Require(restored != null
                    && string.Equals(JsonUtility.ToJson(restored), frozenJson,
                        StringComparison.Ordinal)
                    && restored.prominencePolicyVersion
                        == NarrativeFormulaCore.CurrentProminencePolicyVersion
                    && string.Equals(restored.primaryCapabilityId,
                        "heal", StringComparison.Ordinal),
                "CharacterSkill JSON round-trip changed the frozen envelope or primary metadata.");
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(
                restored, settings);
            Require(true,
                "The JSON-restored primary-aware CharacterSkill must validate.");

            CharacterSkillInstance changedPrimary = restored.Clone();
            changedPrimary.primaryCapabilityId = "guard";
            RequireThrows<InvalidOperationException>(() =>
                    CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(
                        changedPrimary, settings),
                "Restore validation accepted a changed primary capability.");

            CharacterSkillInstance reversedModules = restored.Clone();
            reversedModules.modules.Reverse();
            RequireThrows<InvalidOperationException>(() =>
                    CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(
                        reversedModules, settings),
                "Restore validation accepted module order that disagrees with the frozen primary.");

            CharacterSkillInstance legacy = restored.Clone();
            string numericEnvelope = FrozenNumericEnvelopeSignature(legacy);
            legacy.prominencePolicyVersion = 0;
            legacy.primaryCapabilityId = string.Empty;
            Require(string.Equals(FrozenNumericEnvelopeSignature(legacy),
                        numericEnvelope, StringComparison.Ordinal)
                    && legacy.prominencePolicyVersion == 0
                    && legacy.primaryCapabilityId.Length == 0,
                "The legacy marker conversion changed the frozen numeric envelope.");
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(
                legacy, settings);
            Require(true,
                "The exact legacy prominence marker must preserve its numeric envelope on restore.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static int ExhaustiveMaximumSpend(
        int primaryQuantumCost,
        int primaryMaximumSteps,
        int supportQuantumCost,
        int supportMaximumSteps,
        int budget)
    {
        int best = 0;
        for (int primarySteps = 0; primarySteps <= primaryMaximumSteps; primarySteps++)
        for (int supportSteps = 0; supportSteps <= supportMaximumSteps; supportSteps++)
        {
            int primaryCost = primarySteps * primaryQuantumCost;
            int supportCost = supportSteps * supportQuantumCost;
            int total = primaryCost + supportCost;
            if (total <= budget && primaryCost >= supportCost)
                best = Math.Max(best, total);
        }
        return best;
    }

    private static int FrozenEnhancementCost(
        CharacterSkillInstance skill,
        string capabilityId,
        CharacterSkillSystemSettingsSO settings)
    {
        CharacterSkillFormulaCapabilityEnvelope envelope =
            skill.formulaCapabilities.Single(value => string.Equals(
                value.capabilityId, capabilityId, StringComparison.Ordinal));
        CharacterSkillModuleRule module = settings.FindModule(capabilityId)
            ?? throw new InvalidOperationException(
                "The integration fixture cannot resolve module '"
                + capabilityId + "'.");
        NarrativeFormulaCapabilityDescriptor descriptor =
            settings.RequireFormulaDescriptor(module);
        return NarrativeFormulaCore.EnhancementCost(
            new NarrativeFormulaCapabilityAllocation(
                descriptor,
                envelope.parameters.Select(value =>
                    new NarrativeFormulaParameterValue(
                        value.parameterId, value.units))));
    }

    private static string FrozenNumericEnvelopeSignature(
        CharacterSkillInstance skill)
    {
        return string.Join("|",
            skill.formulaVersion,
            skill.formulaCatalogSha256,
            skill.calculatedCost,
            skill.positiveCost,
            skill.drawbackCredit,
            skill.narrativeBudget,
            string.Join(";", skill.formulaCapabilities.Select(envelope =>
                envelope.capabilityId + ":" + envelope.formatterId + ":"
                + envelope.applicatorId + ":"
                + string.Join(",", envelope.parameters.Select(parameter =>
                    parameter.parameterId + "=" + parameter.units)))));
    }

    private static NarrativeFormulaCandidate Optimize(
        IEnumerable<NarrativeFormulaCapabilityInput> inputs,
        int budget,
        string primaryCapabilityId)
    {
        return OptimizeAll(inputs, budget, primaryCapabilityId).Single();
    }

    private static IReadOnlyList<NarrativeFormulaCandidate> OptimizeAll(
        IEnumerable<NarrativeFormulaCapabilityInput> inputs,
        int budget,
        string primaryCapabilityId,
        int maximumResults = 1)
    {
        return NarrativeFormulaCore.OptimizeExactComposition(
            inputs,
            Context(),
            budget,
            new[] { "fact:primary-allocation" },
            maximumResults: maximumResults,
            primaryCapabilityId: primaryCapabilityId);
    }

    private static NarrativeFormulaGenerationCostContext Context() => new(0, false, 1);

    private static NarrativeFormulaCapabilityInput Input(
        NarrativeFormulaCapabilityDescriptor descriptor) => new(descriptor, 1d, 1d);

    private static NarrativeFormulaCapabilityDescriptor Descriptor(
        string capabilityId,
        long minimumUnits,
        int maximumSteps,
        long quantumUnits,
        int costPerQuantum)
    {
        return new NarrativeFormulaCapabilityDescriptor(
            capabilityId,
            new[]
            {
                new NarrativeFormulaQuantizedRange(
                    NarrativeFormulaParameterIds.Magnitude,
                    minimumUnits,
                    checked(minimumUnits + maximumSteps * quantumUnits),
                    quantumUnits,
                    0,
                    costPerQuantum),
                FixedRange(NarrativeFormulaParameterIds.Duration, 0),
                FixedRange(NarrativeFormulaParameterIds.Count, 1),
                FixedRange(NarrativeFormulaParameterIds.TargetCount, 1)
            },
            new[] { NarrativeFormulaParameterIds.Magnitude },
            baseCost: 0,
            triggerFrequencyCostPerUnit: 0,
            guaranteedProcCost: 0,
            areaCostPerExtraTarget: 0,
            multiEffectCostPerExtraEffect: 0,
            pairSynergyCosts: Array.Empty<NarrativeFormulaPairSynergyCost>(),
            narrativeAffinity: 1d,
            affinityKeys: new[] { "domain:test" },
            conflictGroups: new[] { "conflict:" + capabilityId },
            forbiddenSynergies: Array.Empty<string>(),
            formatterId: capabilityId,
            applicatorId: capabilityId);
    }

    private static NarrativeFormulaQuantizedRange FixedRange(
        string parameterId,
        long units) => new(parameterId, units, units, 1, 0, 1);

    private static NarrativeFormulaCapabilityAllocation Allocation(
        NarrativeFormulaCapabilityDescriptor descriptor,
        int magnitudeSteps)
    {
        return new NarrativeFormulaCapabilityAllocation(
            descriptor,
            NarrativeFormulaParameterIds.Required.Select(parameterId =>
            {
                NarrativeFormulaQuantizedRange range = descriptor.RequireRange(parameterId);
                long units = string.Equals(parameterId,
                    NarrativeFormulaParameterIds.Magnitude, StringComparison.Ordinal)
                    ? checked(range.MinimumUnits + magnitudeSteps * range.QuantumUnits)
                    : range.MinimumUnits;
                return new NarrativeFormulaParameterValue(parameterId, units);
            }));
    }

    private static int Enhancement(
        NarrativeFormulaCandidate candidate,
        string capabilityId)
    {
        NarrativeFormulaCapabilityAllocation allocation = candidate.Allocations.Single(value =>
            string.Equals(value.Descriptor.CapabilityId, capabilityId,
                StringComparison.Ordinal));
        return NarrativeFormulaCore.EnhancementCost(allocation);
    }

    private static void Require(bool condition, string message)
    {
        checkCount++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        checkCount++;
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
